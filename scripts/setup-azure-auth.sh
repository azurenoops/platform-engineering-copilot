#!/bin/bash

# ==============================================
# Azure Authentication Setup for Real Deployments
# ==============================================
# This script helps configure Azure credentials for the Platform Engineering Copilot
# so it can deploy real infrastructure (not just simulations)

set -e

BLUE='\033[0;34m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${BLUE}╔════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║  Platform Engineering Copilot - Azure Setup          ║${NC}"
echo -e "${BLUE}║  Enable Real Infrastructure Deployments              ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check if .env exists
if [ -f .env ]; then
    echo -e "${YELLOW}⚠️  .env file already exists${NC}"
    read -p "Do you want to update it? (y/n) " -n 1 -r
    echo ""
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Exiting without changes"
        exit 0
    fi
else
    echo -e "${GREEN}Creating .env file from template...${NC}"
    cp .env.example .env
fi

# Check if Azure CLI is installed
if ! command -v az &> /dev/null; then
    echo -e "${RED}❌ Azure CLI not found${NC}"
    echo "Please install Azure CLI: https://docs.microsoft.com/cli/azure/install-azure-cli"
    exit 1
fi

echo -e "${GREEN}✓ Azure CLI found${NC}"
echo ""

# Check if logged in
if ! az account show &> /dev/null; then
    echo -e "${YELLOW}⚠️  Not logged into Azure${NC}"
    echo "Please login first..."
    
    # Detect cloud environment
    echo ""
    echo "Which Azure cloud are you using?"
    echo "1) Azure Commercial (AzureCloud)"
    echo "2) Azure Government (AzureUSGovernment)"
    read -p "Enter choice [1-2]: " cloud_choice
    
    if [ "$cloud_choice" = "2" ]; then
        az cloud set --name AzureUSGovernment
        echo -e "${GREEN}Switched to Azure Government${NC}"
    else
        az cloud set --name AzureCloud
        echo -e "${GREEN}Switched to Azure Commercial${NC}"
    fi
    
    az login
fi

# Get current context
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
TENANT_ID=$(az account show --query tenantId -o tsv)
CLOUD_NAME=$(az cloud show --query name -o tsv)

echo -e "${BLUE}Current Azure Context:${NC}"
echo -e "  Subscription: ${GREEN}$SUBSCRIPTION_NAME${NC}"
echo -e "  Subscription ID: ${GREEN}$SUBSCRIPTION_ID${NC}"
echo -e "  Tenant ID: ${GREEN}$TENANT_ID${NC}"
echo -e "  Cloud: ${GREEN}$CLOUD_NAME${NC}"
echo ""

read -p "Is this correct? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Please switch to the correct subscription with: az account set --subscription <subscription-id>"
    exit 1
fi

# Authentication method
echo ""
echo -e "${BLUE}Choose authentication method:${NC}"
echo "1) Azure CLI credentials (easiest - uses your current az login)"
echo "2) Service Principal (recommended for production)"
read -p "Enter choice [1-2]: " auth_choice

if [ "$auth_choice" = "2" ]; then
    # Service Principal setup
    echo ""
    echo -e "${YELLOW}Creating Service Principal...${NC}"
    read -p "Service Principal name [platform-engineering-copilot]: " sp_name
    sp_name=${sp_name:-platform-engineering-copilot}
    
    echo ""
    echo "Creating Service Principal with Contributor role on subscription..."
    SP_OUTPUT=$(az ad sp create-for-rbac \
        --name "$sp_name" \
        --role Contributor \
        --scopes "/subscriptions/$SUBSCRIPTION_ID" \
        --output json)
    
    CLIENT_ID=$(echo $SP_OUTPUT | jq -r '.appId')
    CLIENT_SECRET=$(echo $SP_OUTPUT | jq -r '.password')
    
    echo ""
    echo -e "${GREEN}✓ Service Principal created${NC}"
    echo -e "  Client ID: ${GREEN}$CLIENT_ID${NC}"
    echo -e "  Client Secret: ${YELLOW}***hidden***${NC}"
    
    # Update .env file
    sed -i.bak "s/^AZURE_TENANT_ID=.*/AZURE_TENANT_ID=$TENANT_ID/" .env
    sed -i.bak "s/^AZURE_CLIENT_ID=.*/AZURE_CLIENT_ID=$CLIENT_ID/" .env
    sed -i.bak "s/^AZURE_CLIENT_SECRET=.*/AZURE_CLIENT_SECRET=$CLIENT_SECRET/" .env
    sed -i.bak "s/^AZURE_SUBSCRIPTION_ID=.*/AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID/" .env
    
    # Set cloud environment
    if [[ "$CLOUD_NAME" == *"Government"* ]]; then
        sed -i.bak "s/^AZURE_CLOUD_ENVIRONMENT=.*/AZURE_CLOUD_ENVIRONMENT=AzureGovernment/" .env
    else
        sed -i.bak "s/^AZURE_CLOUD_ENVIRONMENT=.*/AZURE_CLOUD_ENVIRONMENT=AzureCloud/" .env
    fi
    
    rm .env.bak 2>/dev/null || true
    
    echo ""
    echo -e "${GREEN}✓ Updated .env file with Service Principal credentials${NC}"
else
    # Azure CLI method
    echo ""
    echo -e "${YELLOW}Using Azure CLI authentication...${NC}"
    
    # Update .env with tenant and subscription only
    sed -i.bak "s/^AZURE_TENANT_ID=.*/AZURE_TENANT_ID=$TENANT_ID/" .env
    sed -i.bak "s/^AZURE_SUBSCRIPTION_ID=.*/AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID/" .env
    
    # Comment out service principal credentials
    sed -i.bak "s/^AZURE_CLIENT_ID=/#AZURE_CLIENT_ID=/" .env
    sed -i.bak "s/^AZURE_CLIENT_SECRET=/#AZURE_CLIENT_SECRET=/" .env
    
    # Set cloud environment
    if [[ "$CLOUD_NAME" == *"Government"* ]]; then
        sed -i.bak "s/^AZURE_CLOUD_ENVIRONMENT=.*/AZURE_CLOUD_ENVIRONMENT=AzureGovernment/" .env
    else
        sed -i.bak "s/^AZURE_CLOUD_ENVIRONMENT=.*/AZURE_CLOUD_ENVIRONMENT=AzureCloud/" .env
    fi
    
    rm .env.bak 2>/dev/null || true
    
    echo -e "${GREEN}✓ Configured to use ~/.azure CLI credentials${NC}"
    echo -e "${YELLOW}Note: Containers will mount ~/.azure volume${NC}"
fi

echo ""
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}✓ Azure authentication configured!${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════${NC}"
echo ""
echo "Next steps:"
echo "1. Review .env file and add any optional credentials (OpenAI, GitHub, etc.)"
echo "2. Restart containers: docker-compose -f docker-compose.mcp-admin.yml down && docker-compose -f docker-compose.mcp-admin.yml up -d"
echo "3. Provision an environment from a template"
echo "4. Check logs: docker logs pec-admin-api | grep -i 'deploy\\|bicep'"
echo ""
echo -e "${YELLOW}Important:${NC} Environment will now deploy REAL Azure resources!"
echo -e "${YELLOW}Monitor costs in Azure Portal to avoid unexpected charges.${NC}"
echo ""
