#!/usr/bin/env bash
set -e

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${GREEN}╔════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║   Platform Copilot - ACI Deployment (Multi-URL)       ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check prerequisites
echo -e "${YELLOW}Checking prerequisites...${NC}"
command -v az >/dev/null 2>&1 || { echo -e "${RED}✗ Azure CLI not found. Please install it.${NC}"; exit 1; }
command -v docker >/dev/null 2>&1 || { echo -e "${RED}✗ Docker not found. Please install it.${NC}"; exit 1; }
echo -e "${GREEN}✓ Prerequisites check passed${NC}"

# Check Azure login
echo -e "${YELLOW}Checking Azure login...${NC}"
az account show > /dev/null 2>&1 || { echo -e "${RED}✗ Not logged in to Azure. Run 'az login' first.${NC}"; exit 1; }
echo -e "${GREEN}✓ Azure login verified${NC}"

# Configuration
echo -e "\n${BLUE}Please provide deployment configuration:${NC}"
read -p "Project name (3-8 chars) [platengcopilot]: " PROJECT_NAME
PROJECT_NAME=${PROJECT_NAME:-platengcopilot}

read -p "Environment (dev/staging/prod) [dev]: " ENVIRONMENT
ENVIRONMENT=${ENVIRONMENT:-dev}

read -p "Azure region [usgovvirginia]: " LOCATION
LOCATION=${LOCATION:-usgovvirginia}

read -p "SQL Admin Login [platformadmin]: " SQL_ADMIN_LOGIN
SQL_ADMIN_LOGIN=${SQL_ADMIN_LOGIN:-platformadmin}

read -sp "SQL Admin Password (min 12 chars): " SQL_ADMIN_PASSWORD
echo ""

# Service Principal for Azure API access (used by MCP agents)
echo -e "\n${BLUE}Service Principal Configuration (for Azure resource discovery):${NC}"
read -p "Azure Client ID (Service Principal App ID): " AZURE_CLIENT_ID
read -sp "Azure Client Secret: " AZURE_CLIENT_SECRET
echo ""
read -p "Azure Tenant ID: " AZURE_TENANT_ID

# Derived values
RESOURCE_GROUP="${PROJECT_NAME}-${ENVIRONMENT}-rg"
MY_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)

echo -e "\n${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}ACI Deployment Configuration:${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "  Project Name:      ${GREEN}$PROJECT_NAME${NC}"
echo -e "  Environment:       ${GREEN}$ENVIRONMENT${NC}"
echo -e "  Resource Group:    ${GREEN}$RESOURCE_GROUP${NC}"
echo -e "  Location:          ${GREEN}$LOCATION${NC}"
echo -e "  Subscription:      ${GREEN}$SUBSCRIPTION_NAME${NC}"
echo -e "  Deployment Type:   ${GREEN}Azure Container Instances${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""

read -p "Continue with deployment? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}Deployment cancelled.${NC}"
    exit 1
fi

# Create resource group
echo -e "\n${YELLOW}[1/8] Creating resource group...${NC}"
if az group show --name $RESOURCE_GROUP > /dev/null 2>&1; then
    echo -e "${YELLOW}  Resource group already exists. Continuing...${NC}"
else
    az group create --name $RESOURCE_GROUP --location $LOCATION --output none
    echo -e "${GREEN}✓ Resource group created${NC}"
fi

# Create ACR
echo -e "\n${YELLOW}[2/8] Setting up Azure Container Registry...${NC}"

# Check for existing ACR in the resource group
if az group show --name $RESOURCE_GROUP > /dev/null 2>&1; then
    EXISTING_ACR=$(az acr list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv 2>/dev/null)
    if [ -n "$EXISTING_ACR" ]; then
        ACR_NAME=$EXISTING_ACR
        echo -e "${GREEN}✓ Using existing ACR: $ACR_NAME${NC}"
    fi
fi

# If no ACR found, create a new one
if [ -z "$ACR_NAME" ]; then
    ACR_NAME="${PROJECT_NAME}${ENVIRONMENT}acr$(openssl rand -hex 4)"
    ACR_NAME=$(echo "$ACR_NAME" | tr '[:upper:]' '[:lower:]' | tr -d '-')
    
    echo -e "${YELLOW}  Creating new ACR: $ACR_NAME${NC}"
    az acr create \
      --resource-group $RESOURCE_GROUP \
      --name $ACR_NAME \
      --sku Standard \
      --admin-enabled true \
      --output none
    echo -e "${GREEN}✓ ACR created: $ACR_NAME${NC}"
fi

# Get ACR credentials (needed for ACI deployment later)
ACR_LOGIN_SERVER=$(az acr show --name $ACR_NAME --query loginServer -o tsv)
ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query passwords[0].value -o tsv)
echo -e "  ACR Login Server: ${GREEN}$ACR_LOGIN_SERVER${NC}"

# Login to Docker for pushing
echo -e "${YELLOW}  Logging into ACR...${NC}"
echo "$ACR_PASSWORD" | docker login $ACR_LOGIN_SERVER -u $ACR_USERNAME --password-stdin >/dev/null 2>&1
echo -e "${GREEN}✓ Docker login successful${NC}"

# Setup buildx for cross-platform builds (required for Apple Silicon -> amd64)
echo -e "\n${YELLOW}[3/8] Setting up Docker buildx for cross-platform builds...${NC}"
if ! docker buildx inspect multiplatform >/dev/null 2>&1; then
    echo -e "${YELLOW}  Creating buildx builder 'multiplatform'...${NC}"
    docker buildx create --name multiplatform --driver docker-container --bootstrap --use >/dev/null 2>&1
    echo -e "${GREEN}✓ Buildx builder created${NC}"
else
    echo -e "${GREEN}✓ Using existing buildx builder${NC}"
    docker buildx use multiplatform
fi

# Build and push images using Docker buildx (MUCH faster than ACR build)
echo -e "\n${YELLOW}Building and pushing container images...${NC}"
echo -e "${BLUE}  Using local Docker buildx with cross-platform support${NC}"
echo -e "${BLUE}  Building for linux/amd64 (Azure compatible)${NC}"
echo -e "${BLUE}  This may take 5-10 minutes...${NC}"

# Function to check and build image
build_image_if_needed() {
  local image_name=$1
  local dockerfile=$2
  
  echo -e "\n${YELLOW}  Checking $image_name...${NC}"
  
  # Check if image already exists with correct platform
  if az acr repository show --name $ACR_NAME --repository $image_name >/dev/null 2>&1; then
    # Simple check - if it exists, assume it's correct (we can verify later if needed)
    echo -e "${GREEN}✓ $image_name already exists in ACR, skipping build${NC}"
    return 0
  fi
  
  echo -e "${YELLOW}  Building $image_name for linux/amd64...${NC}"
  
  # Use Docker buildx with the multiplatform builder
  docker buildx build \
    --builder multiplatform \
    --platform linux/amd64 \
    --tag ${ACR_LOGIN_SERVER}/${image_name}:latest \
    --file $dockerfile \
    --push \
    . || {
    echo -e "${RED}✗ Failed to build $image_name${NC}"
    exit 1
  }
  
  echo -e "${GREEN}✓ $image_name built and pushed${NC}"
}

# Build all images
build_image_if_needed "admin-api" "src/Platform.Engineering.Copilot.Admin.API/Dockerfile"
build_image_if_needed "admin-client" "src/Platform.Engineering.Copilot.Admin.Client/Dockerfile"
build_image_if_needed "platform-mcp" "src/Platform.Engineering.Copilot.Mcp/Dockerfile"
build_image_if_needed "platform-chat" "src/Platform.Engineering.Copilot.Chat/Dockerfile"

echo -e "${GREEN}✓ All images built and pushed${NC}"

# Create SQL Server
echo -e "\n${YELLOW}[4/8] Setting up SQL Server...${NC}"

# Check for existing SQL Server in the resource group
if az group show --name $RESOURCE_GROUP > /dev/null 2>&1; then
    EXISTING_SQL_SERVER=$(az sql server list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv 2>/dev/null)
    if [ -n "$EXISTING_SQL_SERVER" ]; then
        SQL_SERVER_NAME=$EXISTING_SQL_SERVER
        echo -e "${GREEN}✓ Using existing SQL Server: $SQL_SERVER_NAME${NC}"
        
        # Get existing database
        EXISTING_DB=$(az sql db list --resource-group $RESOURCE_GROUP --server $SQL_SERVER_NAME --query "[?name!='master'].name | [0]" -o tsv 2>/dev/null)
        if [ -n "$EXISTING_DB" ]; then
            SQL_DATABASE_NAME=$EXISTING_DB
            echo -e "${GREEN}✓ Using existing database: $SQL_DATABASE_NAME${NC}"
        fi
    fi
fi

# If no SQL Server found, create a new one
if [ -z "$SQL_SERVER_NAME" ]; then
    SQL_SERVER_NAME="${PROJECT_NAME}-${ENVIRONMENT}-sql-$(openssl rand -hex 4)"
    SQL_SERVER_NAME=$(echo "$SQL_SERVER_NAME" | tr '[:upper:]' '[:lower:]')
    
    echo -e "${YELLOW}  Creating new SQL Server: $SQL_SERVER_NAME${NC}"
    az sql server create \
      --resource-group $RESOURCE_GROUP \
      --name $SQL_SERVER_NAME \
      --location $LOCATION \
      --admin-user $SQL_ADMIN_LOGIN \
      --admin-password $SQL_ADMIN_PASSWORD \
      --output none

    # Configure firewall to allow Azure services
    az sql server firewall-rule create \
      --resource-group $RESOURCE_GROUP \
      --server $SQL_SERVER_NAME \
      --name AllowAzureServices \
      --start-ip-address 0.0.0.0 \
      --end-ip-address 0.0.0.0 \
      --output none

    # Create database
    SQL_DATABASE_NAME="${PROJECT_NAME}-${ENVIRONMENT}-db"
    az sql db create \
      --resource-group $RESOURCE_GROUP \
      --server $SQL_SERVER_NAME \
      --name $SQL_DATABASE_NAME \
      --service-objective S0 \
      --output none
    
    echo -e "${GREEN}✓ SQL Server and Database created${NC}"
fi

# Determine the correct SQL DNS suffix based on Azure cloud environment
SQL_DNS_SUFFIX="database.windows.net"
if [[ "$LOCATION" == *"usgov"* ]] || [[ "$LOCATION" == *"gov"* ]]; then
    SQL_DNS_SUFFIX="database.usgovcloudapi.net"
fi

SQL_CONNECTION_STRING="Server=tcp:${SQL_SERVER_NAME}.${SQL_DNS_SUFFIX},1433;Initial Catalog=${SQL_DATABASE_NAME};Persist Security Info=False;User ID=${SQL_ADMIN_LOGIN};Password=${SQL_ADMIN_PASSWORD};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"

# Create Storage Account for file shares
echo -e "\n${YELLOW}[5/8] Setting up Storage Account...${NC}"

# Check for existing Storage Account
EXISTING_STORAGE=$(az storage account list --resource-group $RESOURCE_GROUP --query "[0].name" -o tsv 2>/dev/null)
if [ -n "$EXISTING_STORAGE" ]; then
    STORAGE_ACCOUNT_NAME=$EXISTING_STORAGE
    echo -e "${GREEN}✓ Using existing Storage Account: $STORAGE_ACCOUNT_NAME${NC}"
else
    STORAGE_ACCOUNT_NAME="${PROJECT_NAME}${ENVIRONMENT}st$(openssl rand -hex 4)"
    STORAGE_ACCOUNT_NAME=$(echo "$STORAGE_ACCOUNT_NAME" | tr '[:upper:]' '[:lower:]' | tr -d '-' | cut -c1-24)
    
    echo -e "${YELLOW}  Creating new Storage Account: $STORAGE_ACCOUNT_NAME${NC}"
    az storage account create \
      --resource-group $RESOURCE_GROUP \
      --name $STORAGE_ACCOUNT_NAME \
      --location $LOCATION \
      --sku Standard_LRS \
      --output none
    
    echo -e "${GREEN}✓ Storage Account created${NC}"
fi

# Create Virtual Network for ACI
echo -e "\n${YELLOW}[6/8] Setting up Virtual Network...${NC}"
VNET_NAME="${PROJECT_NAME}-${ENVIRONMENT}-vnet"

# Check for existing VNet
if az network vnet show --resource-group $RESOURCE_GROUP --name $VNET_NAME > /dev/null 2>&1; then
    echo -e "${GREEN}✓ Using existing VNet: $VNET_NAME${NC}"
else
    echo -e "${YELLOW}  Creating new VNet: $VNET_NAME${NC}"
    az network vnet create \
      --resource-group $RESOURCE_GROUP \
      --name $VNET_NAME \
      --address-prefix 10.0.0.0/16 \
      --subnet-name aci-subnet \
      --subnet-prefix 10.0.1.0/24 \
      --output none
    
    echo -e "${GREEN}✓ Virtual Network created${NC}"
fi

SUBNET_ID=$(az network vnet subnet show \
  --resource-group $RESOURCE_GROUP \
  --vnet-name $VNET_NAME \
  --name aci-subnet \
  --query id -o tsv)

echo -e "${GREEN}✓ Virtual Network ready${NC}"

# Deploy ACI Container Groups
echo -e "\n${YELLOW}[7/8] Deploying Container Instances...${NC}"

# Deploy Admin API
echo -e "\n  ${YELLOW}Deploying admin-api...${NC}"
az container create \
  --resource-group $RESOURCE_GROUP \
  --name "${PROJECT_NAME}-${ENVIRONMENT}-admin-api-aci" \
  --image ${ACR_LOGIN_SERVER}/admin-api:latest \
  --registry-login-server $ACR_LOGIN_SERVER \
  --registry-username $ACR_USERNAME \
  --registry-password $ACR_PASSWORD \
  --os-type Linux \
  --cpu 2 \
  --memory 4 \
  --ports 5002 \
  --dns-name-label "${PROJECT_NAME}-${ENVIRONMENT}-admin-api" \
  --location $LOCATION \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5002 \
    ConnectionStrings__DefaultConnection="$SQL_CONNECTION_STRING" \
  --output none

ADMIN_API_FQDN=$(az container show \
  --resource-group $RESOURCE_GROUP \
  --name "${PROJECT_NAME}-${ENVIRONMENT}-admin-api-aci" \
  --query ipAddress.fqdn -o tsv)
ADMIN_API_URL="http://${ADMIN_API_FQDN}:5002"
echo -e "  ${GREEN}✓ admin-api deployed at: $ADMIN_API_URL${NC}"

# Deploy Admin Client
echo -e "\n  ${YELLOW}Deploying admin-client...${NC}"
az container create \
  --resource-group $RESOURCE_GROUP \
  --name "${PROJECT_NAME}-${ENVIRONMENT}-admin-client-aci" \
  --image ${ACR_LOGIN_SERVER}/admin-client:latest \
  --registry-login-server $ACR_LOGIN_SERVER \
  --registry-username $ACR_USERNAME \
  --registry-password $ACR_PASSWORD \
  --os-type Linux \
  --cpu 2 \
  --memory 4 \
  --ports 5003 \
  --dns-name-label "${PROJECT_NAME}-${ENVIRONMENT}-admin-client" \
  --location $LOCATION \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5003 \
    REACT_APP_ADMIN_API_URL="$ADMIN_API_URL" \
  --output none

ADMIN_CLIENT_FQDN=$(az container show \
  --resource-group $RESOURCE_GROUP \
  --name "${PROJECT_NAME}-${ENVIRONMENT}-admin-client-aci" \
  --query ipAddress.fqdn -o tsv)
ADMIN_CLIENT_URL="http://${ADMIN_CLIENT_FQDN}:5003"
echo -e "  ${GREEN}✓ admin-client deployed at: $ADMIN_CLIENT_URL${NC}"

# Deploy Platform MCP
echo -e "\n  ${YELLOW}Deploying platform-mcp...${NC}"
az container create \
  --resource-group $RESOURCE_GROUP \
  --name "${PROJECT_NAME}-${ENVIRONMENT}-platform-mcp-aci" \
  --image ${ACR_LOGIN_SERVER}/platform-mcp:latest \
  --registry-login-server $ACR_LOGIN_SERVER \
  --registry-username $ACR_USERNAME \
  --registry-password $ACR_PASSWORD \
  --os-type Linux \
  --cpu 2 \
  --memory 4 \
  --ports 5100 \
  --dns-name-label "${PROJECT_NAME}-${ENVIRONMENT}-platform-mcp" \
  --location $LOCATION \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5100 \
    ConnectionStrings__DefaultConnection="$SQL_CONNECTION_STRING" \
    AZURE_CLIENT_ID="$AZURE_CLIENT_ID" \
    AZURE_TENANT_ID="$AZURE_TENANT_ID" \
    AZURE_SUBSCRIPTION_ID="$SUBSCRIPTION_ID" \
  --secure-environment-variables \
    AZURE_CLIENT_SECRET="$AZURE_CLIENT_SECRET" \
  --output none

PLATFORM_MCP_FQDN=$(az container show \
  --resource-group $RESOURCE_GROUP \
  --name "${PROJECT_NAME}-${ENVIRONMENT}-platform-mcp-aci" \
  --query ipAddress.fqdn -o tsv)
PLATFORM_MCP_URL="http://${PLATFORM_MCP_FQDN}:5100"
echo -e "  ${GREEN}✓ platform-mcp deployed at: $PLATFORM_MCP_URL${NC}"

# Deploy Platform Chat
echo -e "\n  ${YELLOW}Deploying platform-chat...${NC}"
az container create \
  --resource-group $RESOURCE_GROUP \
  --name "${PROJECT_NAME}-${ENVIRONMENT}-platform-chat-aci" \
  --image ${ACR_LOGIN_SERVER}/platform-chat:latest \
  --registry-login-server $ACR_LOGIN_SERVER \
  --registry-username $ACR_USERNAME \
  --registry-password $ACR_PASSWORD \
  --os-type Linux \
  --cpu 2 \
  --memory 4 \
  --ports 5001 \
  --dns-name-label "${PROJECT_NAME}-${ENVIRONMENT}-platform-chat" \
  --location $LOCATION \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5001 \
    ConnectionStrings__DefaultConnection="$SQL_CONNECTION_STRING" \
    MCP_SERVER_URL="$PLATFORM_MCP_URL" \
  --output none

PLATFORM_CHAT_FQDN=$(az container show \
  --resource-group $RESOURCE_GROUP \
  --name "${PROJECT_NAME}-${ENVIRONMENT}-platform-chat-aci" \
  --query ipAddress.fqdn -o tsv)
PLATFORM_CHAT_URL="http://${PLATFORM_CHAT_FQDN}:5001"
echo -e "  ${GREEN}✓ platform-chat deployed at: $PLATFORM_CHAT_URL${NC}"

echo -e "${GREEN}✓ All containers deployed${NC}"

# Success message
echo -e "\n${GREEN}╔════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║         ACI Deployment Completed Successfully!         ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════╝${NC}"
echo -e "\n${BLUE}Resource Group:${NC}  $RESOURCE_GROUP"
echo -e "${BLUE}ACR:${NC}             $ACR_NAME"
echo -e "${BLUE}SQL Server:${NC}      $SQL_SERVER_NAME"

echo -e "\n${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}Service URLs (each exposed separately):${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "  ${GREEN}admin-api:${NC}"
echo -e "    $ADMIN_API_URL"
echo -e ""
echo -e "  ${GREEN}admin-client:${NC}"
echo -e "    $ADMIN_CLIENT_URL"
echo -e ""
echo -e "  ${GREEN}platform-chat:${NC}"
echo -e "    $PLATFORM_CHAT_URL"
echo -e ""
echo -e "  ${GREEN}platform-mcp:${NC}"
echo -e "    $PLATFORM_MCP_URL"

echo -e "\n${BLUE}Health Check URLs:${NC}"
echo -e "  Admin API:     ${GREEN}${ADMIN_API_URL}/health${NC}"
echo -e "  Admin Client:  ${GREEN}${ADMIN_CLIENT_URL}/health${NC}"
echo -e "  MCP Server:    ${GREEN}${PLATFORM_MCP_URL}/health${NC}"
echo -e "  Chat:          ${GREEN}${PLATFORM_CHAT_URL}/health${NC}"

echo -e "\n${BLUE}Agent Management:${NC}"
echo -e "  API Endpoint:  ${GREEN}${ADMIN_API_URL}/api/admin/agents${NC}"
echo -e "  UI Dashboard:  ${GREEN}${ADMIN_CLIENT_URL}/dashboard${NC}"

echo -e "\n${BLUE}Connection Strings (save these):${NC}"
echo -e "  SQL Server:    ${GREEN}${SQL_SERVER_NAME}.database.windows.net${NC}"
echo -e "  Database:      ${GREEN}${SQL_DATABASE_NAME}${NC}"
echo -e "  ACR:           ${GREEN}${ACR_LOGIN_SERVER}${NC}"

echo -e "\n${YELLOW}⚠️  Important Notes:${NC}"
echo -e "  • Each service is accessible on its own public URL"
echo -e "  • Services are running on standard ports (5001-5003, 5100)"
echo -e "  • SQL Server firewall allows Azure services only"
echo -e "  • Container instances auto-restart on failure"
echo -e "  • ACR credentials are configured for automatic pull"

echo -e "\n${BLUE}Next Steps:${NC}"
echo -e "  1. Test each service health endpoint"
echo -e "  2. Configure custom domains (optional)"
echo -e "  3. Set up Azure Front Door for HTTPS/SSL (recommended)"
echo -e "  4. Configure Application Insights for monitoring"
echo -e "  5. Review container logs: az container logs -g $RESOURCE_GROUP -n <container-name>"

echo -e "\n${BLUE}View deployment in Azure Portal:${NC}"
echo -e "  ${GREEN}https://portal.azure.com/#@/resource/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}${NC}"

echo -e "\n${YELLOW}To view container logs:${NC}"
echo -e "  az container logs -g $RESOURCE_GROUP -n ${PROJECT_NAME}-${ENVIRONMENT}-admin-api-aci"

echo -e "\n${YELLOW}To restart a container:${NC}"
echo -e "  az container restart -g $RESOURCE_GROUP -n ${PROJECT_NAME}-${ENVIRONMENT}-admin-api-aci"

echo -e "\n${YELLOW}To delete all resources:${NC}"
echo -e "  az group delete -n $RESOURCE_GROUP --yes --no-wait"
