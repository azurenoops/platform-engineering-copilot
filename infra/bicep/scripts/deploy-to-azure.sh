#!/bin/bash
set -e

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${GREEN}╔════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║   Platform Engineering Copilot - Azure Deployment     ║${NC}"
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
read -p "Project name (3-8 chars) [platsup]: " PROJECT_NAME
PROJECT_NAME=${PROJECT_NAME:-platsup}

read -p "Environment (dev/staging/prod) [dev]: " ENVIRONMENT
ENVIRONMENT=${ENVIRONMENT:-dev}

read -p "Azure region [eastus]: " LOCATION
LOCATION=${LOCATION:-eastus}

read -p "SQL Admin Login [platformadmin]: " SQL_ADMIN_LOGIN
SQL_ADMIN_LOGIN=${SQL_ADMIN_LOGIN:-platformadmin}

read -sp "SQL Admin Password (min 12 chars): " SQL_ADMIN_PASSWORD
echo ""

read -p "App Service SKU (B1/B2/P1v3) [B2]: " APP_SERVICE_SKU
APP_SERVICE_SKU=${APP_SERVICE_SKU:-B2}

read -p "SQL Database SKU (Basic/S0/S1) [S0]: " SQL_DATABASE_SKU
SQL_DATABASE_SKU=${SQL_DATABASE_SKU:-S0}

# Derived values
RESOURCE_GROUP="${PROJECT_NAME}-${ENVIRONMENT}-rg"
MY_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)

echo -e "\n${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}Deployment Configuration Summary:${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "  Project Name:      ${GREEN}$PROJECT_NAME${NC}"
echo -e "  Environment:       ${GREEN}$ENVIRONMENT${NC}"
echo -e "  Resource Group:    ${GREEN}$RESOURCE_GROUP${NC}"
echo -e "  Location:          ${GREEN}$LOCATION${NC}"
echo -e "  Subscription:      ${GREEN}$SUBSCRIPTION_NAME${NC}"
echo -e "  App Service SKU:   ${GREEN}$APP_SERVICE_SKU${NC}"
echo -e "  SQL Database SKU:  ${GREEN}$SQL_DATABASE_SKU${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""

read -p "Continue with deployment? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}Deployment cancelled.${NC}"
    exit 1
fi

# Create resource group
echo -e "\n${YELLOW}[1/7] Creating resource group...${NC}"
if az group show --name $RESOURCE_GROUP > /dev/null 2>&1; then
    echo -e "${YELLOW}  Resource group already exists. Continuing...${NC}"
else
    az group create --name $RESOURCE_GROUP --location $LOCATION --output none
    echo -e "${GREEN}✓ Resource group created${NC}"
fi

# Create ACR
echo -e "\n${YELLOW}[2/7] Creating Azure Container Registry...${NC}"
ACR_NAME="${PROJECT_NAME}${ENVIRONMENT}acr$(openssl rand -hex 4)"
ACR_NAME=$(echo "$ACR_NAME" | tr '[:upper:]' '[:lower:]' | tr -d '-')  # ACR names must be lowercase alphanumeric

if az acr show --name $ACR_NAME --resource-group $RESOURCE_GROUP > /dev/null 2>&1; then
    echo -e "${YELLOW}  ACR already exists. Using existing ACR...${NC}"
else
    az acr create \
      --resource-group $RESOURCE_GROUP \
      --name $ACR_NAME \
      --sku Standard \
      --admin-enabled true \
      --output none
    echo -e "${GREEN}✓ ACR created: $ACR_NAME${NC}"
fi

ACR_LOGIN_SERVER=$(az acr show --name $ACR_NAME --query loginServer -o tsv)
echo -e "  ACR Login Server: ${GREEN}$ACR_LOGIN_SERVER${NC}"

# Login to ACR
echo -e "${YELLOW}  Logging in to ACR...${NC}"
az acr login --name $ACR_NAME
echo -e "${GREEN}✓ ACR login successful${NC}"

# Build and push images
echo -e "\n${YELLOW}[3/7] Building and pushing container images...${NC}"
echo -e "${BLUE}  This may take 10-15 minutes...${NC}"

# Store current directory
REPO_ROOT=$(pwd)

# Image definitions
declare -A images=(
    ["admin-api"]="src/Platform.Engineering.Copilot.Admin.API/Dockerfile"
    ["admin-client"]="src/Platform.Engineering.Copilot.Admin.Client/Dockerfile"
    ["platform-mcp"]="src/Platform.Engineering.Copilot.Mcp/Dockerfile"
    ["platform-chat"]="src/Platform.Engineering.Copilot.Chat/Dockerfile"
)

for image in "${!images[@]}"; do
    dockerfile="${images[$image]}"
    
    echo -e "\n  ${YELLOW}Building $image...${NC}"
    docker build -t ${ACR_LOGIN_SERVER}/${image}:latest -f $dockerfile . --quiet
    
    echo -e "  ${YELLOW}Pushing $image...${NC}"
    docker push ${ACR_LOGIN_SERVER}/${image}:latest --quiet
    
    echo -e "  ${GREEN}✓ $image complete${NC}"
done

echo -e "${GREEN}✓ All images built and pushed${NC}"

# Create parameters file
echo -e "\n${YELLOW}[4/7] Creating deployment parameters...${NC}"
cat > /tmp/deployment-params.json <<EOF
{
  "\$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "projectName": {
      "value": "$PROJECT_NAME"
    },
    "environment": {
      "value": "$ENVIRONMENT"
    },
    "location": {
      "value": "$LOCATION"
    },
    "sqlAdminLogin": {
      "value": "$SQL_ADMIN_LOGIN"
    },
    "sqlAdminPassword": {
      "value": "$SQL_ADMIN_PASSWORD"
    },
    "keyVaultAdminObjectId": {
      "value": "$MY_OBJECT_ID"
    },
    "appServiceSku": {
      "value": "$APP_SERVICE_SKU"
    },
    "sqlDatabaseSku": {
      "value": "$SQL_DATABASE_SKU"
    },
    "containerDeploymentTarget": {
      "value": "appservice"
    },
    "deployACR": {
      "value": false
    },
    "deployAdminApi": {
      "value": true
    },
    "deployChat": {
      "value": true
    }
  }
}
EOF
echo -e "${GREEN}✓ Parameters file created${NC}"

# Validate deployment
echo -e "\n${YELLOW}[5/7] Validating Bicep template...${NC}"
cd infra/bicep

VALIDATION_RESULT=$(az deployment group validate \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters /tmp/deployment-params.json \
  --query "properties.provisioningState" -o tsv 2>&1)

if [[ $VALIDATION_RESULT == *"Succeeded"* ]] || [[ $VALIDATION_RESULT == "Succeeded" ]]; then
    echo -e "${GREEN}✓ Template validation successful${NC}"
else
    echo -e "${RED}✗ Template validation failed:${NC}"
    echo "$VALIDATION_RESULT"
    exit 1
fi

# Deploy infrastructure
echo -e "\n${YELLOW}[6/7] Deploying infrastructure with Bicep...${NC}"
echo -e "${BLUE}  This may take 15-20 minutes...${NC}"

DEPLOYMENT_NAME="platform-copilot-$(date +%Y%m%d-%H%M%S)"

az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters /tmp/deployment-params.json \
  --name $DEPLOYMENT_NAME \
  --output table

# Get outputs
echo -e "\n${YELLOW}[7/7] Retrieving deployment outputs...${NC}"

OUTPUTS=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name $DEPLOYMENT_NAME \
  --query properties.outputs -o json)

# Parse outputs
ADMIN_API_URL=$(echo $OUTPUTS | grep -o '"adminApiUrl"[^}]*value":\s*"[^"]*"' | grep -o 'https://[^"]*' || echo "")
ADMIN_CLIENT_URL=$(echo $OUTPUTS | grep -o '"adminClientUrl"[^}]*value":\s*"[^"]*"' | grep -o 'https://[^"]*' || echo "")
MCP_URL=$(echo $OUTPUTS | grep -o '"mcpServerUrl"[^}]*value":\s*"[^"]*"' | grep -o 'https://[^"]*' || echo "")
CHAT_URL=$(echo $OUTPUTS | grep -o '"chatUrl"[^}]*value":\s*"[^"]*"' | grep -o 'https://[^"]*' || echo "")

# Clean up temp file
rm /tmp/deployment-params.json

# Success message
echo -e "\n${GREEN}╔════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║            Deployment Completed Successfully!          ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════╝${NC}"
echo -e "\n${BLUE}Resource Group:${NC}  $RESOURCE_GROUP"
echo -e "${BLUE}ACR:${NC}             $ACR_NAME"
echo -e "\n${BLUE}Application URLs:${NC}"
echo -e "  Admin API:     ${GREEN}${ADMIN_API_URL:-Not deployed}${NC}"
echo -e "  Admin Client:  ${GREEN}${ADMIN_CLIENT_URL:-Not deployed}${NC}"
echo -e "  MCP Server:    ${GREEN}${MCP_URL:-Not deployed}${NC}"
echo -e "  Chat:          ${GREEN}${CHAT_URL:-Not deployed}${NC}"

echo -e "\n${BLUE}Next Steps:${NC}"
echo -e "  1. Navigate to Azure Portal to review resources"
echo -e "  2. Configure custom domain and SSL certificates"
echo -e "  3. Set up Azure AD authentication"
echo -e "  4. Configure Application Insights alerts"
echo -e "  5. Review and adjust scaling settings"

echo -e "\n${BLUE}View deployment in Azure Portal:${NC}"
echo -e "  ${GREEN}https://portal.azure.com/#@/resource/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}${NC}"

echo -e "\n${YELLOW}Deployment logs saved to: deployment-${DEPLOYMENT_NAME}.log${NC}"
