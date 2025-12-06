#!/usr/bin/env bash
set -e

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${GREEN}╔════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║   Update MCP Container with Azure Credentials          ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════╝${NC}"
echo ""

# Configuration
RESOURCE_GROUP="platengcopilot-dev-rg"
CONTAINER_NAME="platengcopilot-dev-platform-mcp-aci"
LOCATION="usgovvirginia"

# Get existing container info
echo -e "${YELLOW}Getting existing container configuration...${NC}"
ACR_LOGIN_SERVER=$(az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --query "imageRegistryCredentials[0].server" -o tsv)
ACR_USERNAME=$(az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --query "imageRegistryCredentials[0].username" -o tsv)
IMAGE=$(az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --query "containers[0].image" -o tsv)
SQL_CONNECTION=$(az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --query "containers[0].environmentVariables[?name=='ConnectionStrings__DefaultConnection'].value | [0]" -o tsv)

echo -e "${GREEN}✓ ACR: $ACR_LOGIN_SERVER${NC}"
echo -e "${GREEN}✓ Image: $IMAGE${NC}"

# Get ACR password
ACR_NAME=$(echo $ACR_LOGIN_SERVER | cut -d'.' -f1)
ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query passwords[0].value -o tsv)

# Get subscription ID
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
echo -e "${GREEN}✓ Subscription: $SUBSCRIPTION_ID${NC}"

# Get Service Principal credentials
echo -e "\n${BLUE}Enter Service Principal credentials:${NC}"
read -p "Azure Client ID (Service Principal App ID): " AZURE_CLIENT_ID
read -sp "Azure Client Secret: " AZURE_CLIENT_SECRET
echo ""
read -p "Azure Tenant ID: " AZURE_TENANT_ID

echo -e "\n${YELLOW}Redeploying MCP container with Azure credentials...${NC}"

# Delete existing container
echo -e "${YELLOW}Deleting existing container...${NC}"
az container delete --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --yes --output none

# Recreate with new environment variables
echo -e "${YELLOW}Creating container with Service Principal credentials...${NC}"
az container create \
  --resource-group $RESOURCE_GROUP \
  --name $CONTAINER_NAME \
  --image $IMAGE \
  --registry-login-server $ACR_LOGIN_SERVER \
  --registry-username $ACR_USERNAME \
  --registry-password "$ACR_PASSWORD" \
  --os-type Linux \
  --cpu 2 \
  --memory 4 \
  --ports 5100 \
  --dns-name-label "platengcopilot-dev-platform-mcp" \
  --location $LOCATION \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5100 \
    "ConnectionStrings__DefaultConnection=$SQL_CONNECTION" \
    AZURE_CLIENT_ID="$AZURE_CLIENT_ID" \
    AZURE_TENANT_ID="$AZURE_TENANT_ID" \
    AZURE_SUBSCRIPTION_ID="$SUBSCRIPTION_ID" \
  --secure-environment-variables \
    AZURE_CLIENT_SECRET="$AZURE_CLIENT_SECRET" \
  --output none

# Get new IP
MCP_IP=$(az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --query ipAddress.ip -o tsv)
MCP_FQDN=$(az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --query ipAddress.fqdn -o tsv)

echo -e "\n${GREEN}════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}✓ MCP Container updated with Azure credentials!${NC}"
echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
echo -e "  MCP URL: ${BLUE}http://${MCP_IP}:5100${NC}"
echo -e "  MCP FQDN: ${BLUE}http://${MCP_FQDN}:5100${NC}"
echo ""
echo -e "${YELLOW}Testing MCP endpoint...${NC}"
sleep 10
curl -s http://${MCP_IP}:5100/health || echo -e "${YELLOW}(Container may still be starting...)${NC}"
echo ""
echo -e "\n${GREEN}Done!${NC}"
