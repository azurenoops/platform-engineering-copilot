# Azure Deployment Guide - Platform Engineering Copilot

This guide walks you through deploying the Platform Engineering Copilot to Azure using the provided Bicep templates.

## 📋 Prerequisites

Before you begin, ensure you have:

1. **Azure CLI** installed and configured
   ```bash
   az --version
   az login
   ```

2. **Azure Subscription** with appropriate permissions
   - Contributor role at subscription or resource group level
   - Ability to create Azure AD applications (for managed identities)

3. **Docker** (for building and pushing container images)
   ```bash
   docker --version
   ```

4. **Your Azure AD Object ID** (for Key Vault access)
   ```bash
   az ad signed-in-user show --query id -o tsv
   ```

## 🎯 Deployment Options

The infrastructure supports three deployment targets:

1. **Azure App Service** (Recommended for production) - PaaS, fully managed
2. **Azure Kubernetes Service (AKS)** - Full Kubernetes orchestration
3. **Azure Container Instances (ACI)** - Serverless containers

## 🚀 Quick Start - App Service Deployment

### Step 1: Set Your Variables

```bash
# Project configuration
export PROJECT_NAME="platsup"           # 3-8 characters
export ENVIRONMENT="dev"                # dev, staging, or prod
export LOCATION="eastus"                # Azure region
export RESOURCE_GROUP="${PROJECT_NAME}-${ENVIRONMENT}-rg"

# Get your Object ID for Key Vault access
export MY_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)

# SQL credentials (change these!)
export SQL_ADMIN_LOGIN="platformadmin"
export SQL_ADMIN_PASSWORD="YourSecureP@ssw0rd123!"

# Azure subscription
export SUBSCRIPTION_ID=$(az account show --query id -o tsv)
```

### Step 2: Create Resource Group

```bash
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

### Step 3: Create Azure Container Registry (ACR)

```bash
# Create ACR
export ACR_NAME="${PROJECT_NAME}${ENVIRONMENT}acr$(openssl rand -hex 4)"

az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Standard \
  --admin-enabled true

# Get ACR login server
export ACR_LOGIN_SERVER=$(az acr show --name $ACR_NAME --query loginServer -o tsv)

# Login to ACR
az acr login --name $ACR_NAME
```

### Step 4: Build and Push Container Images

```bash
# Navigate to repository root
cd /Users/johnspinella/repos/platform-engineering-copilot

# Build and push Admin API
docker build -t ${ACR_LOGIN_SERVER}/admin-api:latest \
  -f src/Platform.Engineering.Copilot.Admin.API/Dockerfile .
docker push ${ACR_LOGIN_SERVER}/admin-api:latest

# Build and push Admin Client
docker build -t ${ACR_LOGIN_SERVER}/admin-client:latest \
  -f src/Platform.Engineering.Copilot.Admin.Client/Dockerfile .
docker push ${ACR_LOGIN_SERVER}/admin-client:latest

# Build and push MCP Server
docker build -t ${ACR_LOGIN_SERVER}/platform-mcp:latest \
  -f src/Platform.Engineering.Copilot.Mcp/Dockerfile .
docker push ${ACR_LOGIN_SERVER}/platform-mcp:latest

# Build and push Chat
docker build -t ${ACR_LOGIN_SERVER}/platform-chat:latest \
  -f src/Platform.Engineering.Copilot.Chat/Dockerfile .
docker push ${ACR_LOGIN_SERVER}/platform-chat:latest
```

### Step 5: Update Parameters File

Edit `infra/bicep/main.parameters.appservice.json`:

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "projectName": {
      "value": "platsup"
    },
    "environment": {
      "value": "dev"
    },
    "location": {
      "value": "eastus"
    },
    "sqlAdminLogin": {
      "value": "platformadmin"
    },
    "sqlAdminPassword": {
      "value": "YourSecureP@ssw0rd123!"
    },
    "keyVaultAdminObjectId": {
      "value": "YOUR_OBJECT_ID_HERE"
    },
    "appServiceSku": {
      "value": "B2"
    },
    "sqlDatabaseSku": {
      "value": "S0"
    },
    "containerDeploymentTarget": {
      "value": "appservice"
    },
    "deployACR": {
      "value": true
    },
    "deployAdminApi": {
      "value": true
    },
    "deployChat": {
      "value": true
    }
  }
}
```

### Step 6: Deploy Infrastructure

```bash
cd infra/bicep

# Validate deployment
az deployment group validate \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters main.parameters.appservice.json \
  --parameters keyVaultAdminObjectId=$MY_OBJECT_ID \
  --parameters sqlAdminPassword=$SQL_ADMIN_PASSWORD

# Deploy (this takes 10-15 minutes)
az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters main.parameters.appservice.json \
  --parameters keyVaultAdminObjectId=$MY_OBJECT_ID \
  --parameters sqlAdminPassword=$SQL_ADMIN_PASSWORD \
  --name "platform-copilot-deployment-$(date +%Y%m%d-%H%M%S)"
```

### Step 7: Get Deployment Outputs

```bash
# Get all outputs
az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name "platform-copilot-deployment-*" \
  --query properties.outputs

# Get specific URLs
export ADMIN_API_URL=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name "platform-copilot-deployment-*" \
  --query properties.outputs.adminApiUrl.value -o tsv)

export ADMIN_CLIENT_URL=$(az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name "platform-copilot-deployment-*" \
  --query properties.outputs.adminClientUrl.value -o tsv)

echo "Admin API: $ADMIN_API_URL"
echo "Admin Client: $ADMIN_CLIENT_URL"
```

### Step 8: Configure Application Settings

```bash
# Get the App Service names
export ADMIN_API_APP=$(az webapp list \
  --resource-group $RESOURCE_GROUP \
  --query "[?contains(name, 'admin-api')].name" -o tsv)

export ADMIN_CLIENT_APP=$(az webapp list \
  --resource-group $RESOURCE_GROUP \
  --query "[?contains(name, 'admin-client')].name" -o tsv)

# Configure Admin API
az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $ADMIN_API_APP \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID

# Configure Admin Client
az webapp config appsettings set \
  --resource-group $RESOURCE_GROUP \
  --name $ADMIN_CLIENT_APP \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    REACT_APP_ADMIN_API_URL=$ADMIN_API_URL
```

### Step 9: Verify Deployment

```bash
# Test Admin API health
curl https://${ADMIN_API_APP}.azurewebsites.net/health

# Test Admin Client
curl https://${ADMIN_CLIENT_APP}.azurewebsites.net/health

# Check agent configurations
curl https://${ADMIN_API_APP}.azurewebsites.net/api/admin/agents
```

## 🔧 Complete Deployment Script

I've created a complete deployment script for you. Save this as `deploy-to-azure.sh`:

```bash
#!/bin/bash
set -e

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}╔════════════════════════════════════════════════════════╗${NC}"
echo -e "${GREEN}║   Platform Engineering Copilot - Azure Deployment     ║${NC}"
echo -e "${GREEN}╚════════════════════════════════════════════════════════╝${NC}"
echo ""

# Check prerequisites
echo -e "${YELLOW}Checking prerequisites...${NC}"
command -v az >/dev/null 2>&1 || { echo -e "${RED}Azure CLI not found. Please install it.${NC}"; exit 1; }
command -v docker >/dev/null 2>&1 || { echo -e "${RED}Docker not found. Please install it.${NC}"; exit 1; }

# Configuration
read -p "Project name (3-8 chars) [platsup]: " PROJECT_NAME
PROJECT_NAME=${PROJECT_NAME:-platsup}

read -p "Environment (dev/staging/prod) [dev]: " ENVIRONMENT
ENVIRONMENT=${ENVIRONMENT:-dev}

read -p "Azure region [eastus]: " LOCATION
LOCATION=${LOCATION:-eastus}

read -p "SQL Admin Login [platformadmin]: " SQL_ADMIN_LOGIN
SQL_ADMIN_LOGIN=${SQL_ADMIN_LOGIN:-platformadmin}

read -sp "SQL Admin Password: " SQL_ADMIN_PASSWORD
echo ""

# Derived values
RESOURCE_GROUP="${PROJECT_NAME}-${ENVIRONMENT}-rg"
MY_OBJECT_ID=$(az ad signed-in-user show --query id -o tsv)
SUBSCRIPTION_ID=$(az account show --query id -o tsv)

echo -e "\n${GREEN}Configuration:${NC}"
echo "  Project: $PROJECT_NAME"
echo "  Environment: $ENVIRONMENT"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Location: $LOCATION"
echo "  Subscription: $SUBSCRIPTION_ID"
echo ""

read -p "Continue with deployment? (y/n) " -n 1 -r
echo ""
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    exit 1
fi

# Create resource group
echo -e "\n${YELLOW}Creating resource group...${NC}"
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create ACR
echo -e "\n${YELLOW}Creating Azure Container Registry...${NC}"
ACR_NAME="${PROJECT_NAME}${ENVIRONMENT}acr$(openssl rand -hex 4)"
az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Standard \
  --admin-enabled true

ACR_LOGIN_SERVER=$(az acr show --name $ACR_NAME --query loginServer -o tsv)
az acr login --name $ACR_NAME

# Build and push images
echo -e "\n${YELLOW}Building and pushing container images...${NC}"

images=("admin-api" "admin-client" "platform-mcp" "platform-chat")
dockerfiles=(
  "src/Platform.Engineering.Copilot.Admin.API/Dockerfile"
  "src/Platform.Engineering.Copilot.Admin.Client/Dockerfile"
  "src/Platform.Engineering.Copilot.Mcp/Dockerfile"
  "src/Platform.Engineering.Copilot.Chat/Dockerfile"
)

for i in "${!images[@]}"; do
  image="${images[$i]}"
  dockerfile="${dockerfiles[$i]}"
  
  echo -e "${GREEN}Building $image...${NC}"
  docker build -t ${ACR_LOGIN_SERVER}/${image}:latest -f $dockerfile .
  docker push ${ACR_LOGIN_SERVER}/${image}:latest
done

# Deploy infrastructure
echo -e "\n${YELLOW}Deploying infrastructure with Bicep...${NC}"
cd infra/bicep

DEPLOYMENT_NAME="platform-copilot-$(date +%Y%m%d-%H%M%S)"

az deployment group create \
  --resource-group $RESOURCE_GROUP \
  --template-file main.bicep \
  --parameters main.parameters.appservice.json \
  --parameters projectName=$PROJECT_NAME \
  --parameters environment=$ENVIRONMENT \
  --parameters location=$LOCATION \
  --parameters keyVaultAdminObjectId=$MY_OBJECT_ID \
  --parameters sqlAdminLogin=$SQL_ADMIN_LOGIN \
  --parameters sqlAdminPassword=$SQL_ADMIN_PASSWORD \
  --parameters deployACR=true \
  --parameters deployAdminApi=true \
  --parameters deployChat=true \
  --name $DEPLOYMENT_NAME

# Get outputs
echo -e "\n${GREEN}Deployment complete!${NC}"
echo -e "\n${YELLOW}Getting deployment outputs...${NC}"

az deployment group show \
  --resource-group $RESOURCE_GROUP \
  --name $DEPLOYMENT_NAME \
  --query properties.outputs

echo -e "\n${GREEN}✓ Deployment successful!${NC}"
echo -e "\nNext steps:"
echo "1. Navigate to the Azure Portal to configure additional settings"
echo "2. Set up custom domain and SSL certificates"
echo "3. Configure Azure AD authentication"
echo "4. Review and adjust scaling settings"
```

## 📚 Additional Resources

- [Bicep Documentation](https://learn.microsoft.com/en-us/azure/azure-resource-manager/bicep/)
- [Azure App Service](https://learn.microsoft.com/en-us/azure/app-service/)
- [Azure Container Registry](https://learn.microsoft.com/en-us/azure/container-registry/)
- [Azure SQL Database](https://learn.microsoft.com/en-us/azure/azure-sql/)

## 🔍 Troubleshooting

### Deployment Fails

1. **Check Azure CLI login:**
   ```bash
   az account show
   ```

2. **Validate Bicep template:**
   ```bash
   az deployment group validate \
     --resource-group $RESOURCE_GROUP \
     --template-file main.bicep \
     --parameters main.parameters.appservice.json
   ```

3. **View deployment logs:**
   ```bash
   az deployment group show \
     --resource-group $RESOURCE_GROUP \
     --name $DEPLOYMENT_NAME
   ```

### Container Images Won't Push

1. **Verify ACR login:**
   ```bash
   az acr login --name $ACR_NAME
   ```

2. **Check ACR credentials:**
   ```bash
   az acr credential show --name $ACR_NAME
   ```

### Application Won't Start

1. **Check App Service logs:**
   ```bash
   az webapp log tail \
     --resource-group $RESOURCE_GROUP \
     --name $ADMIN_API_APP
   ```

2. **Review application settings:**
   ```bash
   az webapp config appsettings list \
     --resource-group $RESOURCE_GROUP \
     --name $ADMIN_API_APP
   ```

## 🔒 Security Best Practices

1. **Never commit secrets to Git**
   - Use Azure Key Vault for secrets
   - Use managed identities where possible

2. **Use strong SQL passwords**
   - Minimum 12 characters
   - Mix of uppercase, lowercase, numbers, and symbols

3. **Enable Application Insights**
   - Monitor application performance
   - Set up alerts for errors

4. **Configure firewall rules**
   - Restrict SQL Server access
   - Use private endpoints for production

## 💰 Cost Optimization

**Development/Testing:**
- App Service: B1 or B2 ($13-54/month)
- SQL Database: S0 ($15/month)
- ACR: Basic ($5/month)
- **Estimated: $33-74/month**

**Production:**
- App Service: P1v3 ($125/month per app)
- SQL Database: S2 or S3 ($30-100/month)
- ACR: Standard ($20/month)
- Application Insights: Pay-as-you-go
- **Estimated: $250-500/month**

Use `az consumption` commands to monitor actual costs.
