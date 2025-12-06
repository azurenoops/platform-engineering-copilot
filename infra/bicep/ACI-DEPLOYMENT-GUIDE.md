# Azure Container Instances (ACI) Deployment Guide

## Overview

This guide helps you deploy the Platform Engineering Copilot to **Azure Container Instances** with each service exposed on its own public URL.

### ✅ Benefits of ACI Deployment

- **Serverless** - No need to manage VMs or clusters
- **Pay-per-second** - Only pay when containers are running
- **Fast deployment** - Containers start in seconds
- **Individual URLs** - Each service gets its own DNS name
- **Simple scaling** - Easy to scale individual containers
- **Cost-effective** - Great for dev/test environments

### 📊 Architecture

```
┌─────────────────────────────────────────────────────┐
│                  Azure Container Registry           │
│              (Stores container images)              │
└──────────────────┬──────────────────────────────────┘
                   │
       ┌───────────┴───────────┬─────────────┬────────────┐
       │                       │             │            │
┌──────▼──────┐   ┌───────────▼────┐   ┌───▼────┐  ┌───▼────┐
│  Admin API  │   │  Admin Client  │   │  Chat  │  │  MCP   │
│  Container  │   │   Container    │   │ Contain│  │ Server │
│             │   │                │   │  er    │  │ Contain│
│ Port: 5002  │   │  Port: 5003    │   │ Port   │  │ Port   │
│ Public URL  │   │  Public URL    │   │ 5001   │  │ 5100   │
└─────────────┘   └────────────────┘   └────────┘  └────────┘
       │                  │                 │           │
       └──────────────────┴─────────────────┴───────────┘
                          │
                   ┌──────▼──────┐
                   │  SQL Server │
                   │  (Database) │
                   └─────────────┘
```

Each service gets:
- Its own public IP address
- Its own DNS name (FQDN)
- Its own port
- Independent scaling
- Separate logs and monitoring

## 🚀 Quick Start

### Prerequisites

1. **Azure CLI** installed
   ```bash
   az --version
   az login
   ```

2. **Docker** installed
   ```bash
   docker --version
   ```

3. **Repository** cloned
   ```bash
   cd /Users/johnspinella/repos/platform-engineering-copilot
   ```

### One-Command Deployment

```bash
./infra/bicep/scripts/deploy-to-aci.sh
```

The script will prompt you for:
- Project name (default: `platsup`)
- Environment (default: `dev`)
- Azure region (default: `eastus`)
- SQL credentials

### What Gets Created

The script creates:

1. **Resource Group** - Contains all resources
2. **Azure Container Registry** - Stores your container images
3. **SQL Server & Database** - Backend database
4. **Storage Account** - For persistent data
5. **Virtual Network** - Network isolation
6. **4 Container Instances** - One for each service:
   - `admin-api` - Administrative backend
   - `admin-client` - Admin web interface
   - `platform-chat` - Chat interface
   - `platform-mcp` - MCP server

### Expected Output

```
═══════════════════════════════════════════════════════
Service URLs (each exposed separately):
═══════════════════════════════════════════════════════
  admin-api:
    http://platsup-dev-admin-api.eastus.azurecontainer.io:5002
  
  admin-client:
    http://platsup-dev-admin-client.eastus.azurecontainer.io:5003
  
  platform-chat:
    http://platsup-dev-platform-chat.eastus.azurecontainer.io:5001
  
  platform-mcp:
    http://platsup-dev-platform-mcp.eastus.azurecontainer.io:5100

Health Check URLs:
  Admin API:     http://platsup-dev-admin-api.eastus.azurecontainer.io:5002/health
  Admin Client:  http://platsup-dev-admin-client.eastus.azurecontainer.io:5003/health
  MCP Server:    http://platsup-dev-platform-mcp.eastus.azurecontainer.io:5100/health
  Chat:          http://platsup-dev-platform-chat.eastus.azurecontainer.io:5001/health

Agent Management:
  API Endpoint:  http://platsup-dev-admin-api.eastus.azurecontainer.io:5002/api/admin/agents
  UI Dashboard:  http://platsup-dev-admin-client.eastus.azurecontainer.io:5003/dashboard
```

## 🔧 Manual Deployment Steps

If you prefer to understand each step:

### 1. Set Variables

```bash
export PROJECT_NAME="platsup"
export ENVIRONMENT="dev"
export LOCATION="eastus"
export RESOURCE_GROUP="${PROJECT_NAME}-${ENVIRONMENT}-rg"
export SQL_PASSWORD="YourSecureP@ssw0rd123!"
```

### 2. Create Resource Group

```bash
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

### 3. Create ACR

```bash
export ACR_NAME="${PROJECT_NAME}${ENVIRONMENT}acr$(openssl rand -hex 4)"

az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Standard \
  --admin-enabled true

az acr login --name $ACR_NAME
```

### 4. Build and Push Images

```bash
export ACR_LOGIN_SERVER=$(az acr show --name $ACR_NAME --query loginServer -o tsv)

# Admin API
docker build -t ${ACR_LOGIN_SERVER}/admin-api:latest \
  -f src/Platform.Engineering.Copilot.Admin.API/Dockerfile .
docker push ${ACR_LOGIN_SERVER}/admin-api:latest

# Admin Client  
docker build -t ${ACR_LOGIN_SERVER}/admin-client:latest \
  -f src/Platform.Engineering.Copilot.Admin.Client/Dockerfile .
docker push ${ACR_LOGIN_SERVER}/admin-client:latest

# MCP Server
docker build -t ${ACR_LOGIN_SERVER}/platform-mcp:latest \
  -f src/Platform.Engineering.Copilot.Mcp/Dockerfile .
docker push ${ACR_LOGIN_SERVER}/platform-mcp:latest

# Chat
docker build -t ${ACR_LOGIN_SERVER}/platform-chat:latest \
  -f src/Platform.Engineering.Copilot.Chat/Dockerfile .
docker push ${ACR_LOGIN_SERVER}/platform-chat:latest
```

### 5. Create SQL Server

```bash
export SQL_SERVER_NAME="${PROJECT_NAME}-${ENVIRONMENT}-sql"

az sql server create \
  --resource-group $RESOURCE_GROUP \
  --name $SQL_SERVER_NAME \
  --location $LOCATION \
  --admin-user platformadmin \
  --admin-password $SQL_PASSWORD

# Allow Azure services
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Create database
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name platform-db \
  --service-objective S0
```

### 6. Get ACR Credentials

```bash
export ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query username -o tsv)
export ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query passwords[0].value -o tsv)
```

### 7. Deploy Admin API Container

```bash
az container create \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci \
  --image ${ACR_LOGIN_SERVER}/admin-api:latest \
  --registry-login-server $ACR_LOGIN_SERVER \
  --registry-username $ACR_USERNAME \
  --registry-password $ACR_PASSWORD \
  --cpu 2 \
  --memory 4 \
  --ports 5002 \
  --dns-name-label ${PROJECT_NAME}-${ENVIRONMENT}-admin-api \
  --location $LOCATION \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5002

# Get the URL
az container show \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci \
  --query ipAddress.fqdn -o tsv
```

### 8. Repeat for Other Services

Deploy Admin Client (port 5003), Chat (port 5001), and MCP (port 5100) using the same pattern.

## 🔍 Managing Your Deployment

### View Container Status

```bash
az container show \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci
```

### View Logs

```bash
az container logs \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci
```

### Stream Logs

```bash
az container attach \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci
```

### Restart Container

```bash
az container restart \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci
```

### Update Container

```bash
# Push new image
docker push ${ACR_LOGIN_SERVER}/admin-api:latest

# Delete and recreate container
az container delete \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci \
  --yes

# Recreate with new image...
```

### Scale Container Resources

```bash
az container create \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci \
  --cpu 4 \          # Increased from 2
  --memory 8 \       # Increased from 4
  # ... other params
```

## 💰 Cost Estimation

ACI pricing is pay-per-second based on CPU and memory:

**Development (2 CPU, 4GB RAM per container, 8 hours/day):**
- 4 containers × $0.0000185/CPU-second × 2 CPU × 28,800 seconds/day
- = ~$4.28/day × 30 days = **~$128/month**

**Production (2 CPU, 4GB RAM per container, 24/7):**
- 4 containers × $0.0000185/CPU-second × 2 CPU × 86,400 seconds/day  
- = ~$12.84/day × 30 days = **~$385/month**

Plus:
- SQL Database: $15-100/month
- ACR: $5-20/month
- Storage: $1-5/month

**Total: $150-500/month depending on usage**

## 🔒 Security Recommendations

1. **Enable HTTPS**
   - Use Azure Front Door or Application Gateway
   - Terminate SSL at the edge

2. **Private Networking**
   - Deploy to VNet
   - Use private endpoints

3. **Managed Identity**
   - Enable for ACR access
   - Remove registry credentials

4. **Secrets Management**
   - Use Azure Key Vault
   - Don't hardcode passwords

5. **Firewall Rules**
   - Restrict SQL Server access
   - Use IP whitelisting

## 🔧 Troubleshooting

### Container Won't Start

```bash
# Check events
az container show \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci \
  --query instanceView.events

# Check logs
az container logs \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci
```

### Can't Pull Image

```bash
# Verify ACR credentials
az acr credential show --name $ACR_NAME

# Test manually
docker login $ACR_LOGIN_SERVER \
  -u $ACR_USERNAME \
  -p $ACR_PASSWORD
```

### Database Connection Issues

```bash
# Check firewall rules
az sql server firewall-rule list \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME

# Add your IP
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowMyIP \
  --start-ip-address YOUR_IP \
  --end-ip-address YOUR_IP
```

## 🧹 Cleanup

### Delete Everything

```bash
az group delete \
  --name $RESOURCE_GROUP \
  --yes \
  --no-wait
```

### Delete Specific Container

```bash
az container delete \
  --resource-group $RESOURCE_GROUP \
  --name admin-api-aci \
  --yes
```

## 📚 Next Steps

1. ✅ Deploy containers using the script
2. ✅ Test each service's health endpoint
3. ⬜ Configure custom domain names
4. ⬜ Set up HTTPS with Azure Front Door
5. ⬜ Enable Application Insights
6. ⬜ Configure auto-restart policies
7. ⬜ Set up monitoring and alerts

## 🆘 Support

- [Azure Container Instances Documentation](https://learn.microsoft.com/en-us/azure/container-instances/)
- [ACI Pricing Calculator](https://azure.microsoft.com/en-us/pricing/details/container-instances/)
- [Troubleshooting Guide](https://learn.microsoft.com/en-us/azure/container-instances/container-instances-troubleshooting)
