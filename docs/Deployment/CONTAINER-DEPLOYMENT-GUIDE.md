# Container Infrastructure Deployment Guide

This guide covers deploying the Platform Engineering Copilot using containerized infrastructure on Azure.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Deployment Options](#deployment-options)
3. [Quick Start](#quick-start)
4. [Deployment Targets](#deployment-targets)
5. [GitHub Actions CI/CD](#github-actions-cicd)
6. [Manual Deployment](#manual-deployment)
7. [Configuration](#configuration)
8. [Troubleshooting](#troubleshooting)

## Prerequisites

### Required Tools

- **Azure CLI** (v2.50.0 or later)
  ```bash
  az --version
  az login
  ```

- **Docker** (with BuildKit support)
  ```bash
  docker version
  export DOCKER_BUILDKIT=1
  ```

- **kubectl** (for AKS deployments)
  ```bash
  kubectl version --client
  ```

- **.NET 9.0 SDK** (for local builds)
  ```bash
  dotnet --version
  ```

### Azure Permissions

You need the following Azure RBAC roles:

- **Contributor** - Deploy resources
- **User Access Administrator** - Assign RBAC roles
- **AcrPush** - Push images to ACR (if using ACR)

### GitHub Secrets (for CI/CD)

Configure these secrets in your GitHub repository:

```
AZURE_CLIENT_ID         - Service Principal Client ID
AZURE_TENANT_ID         - Azure AD Tenant ID
AZURE_SUBSCRIPTION_ID   - Azure Subscription ID
SQL_ADMIN_PASSWORD      - SQL Server Admin Password
ACR_NAME                - Container Registry Name (without .azurecr.io)
ACR_LOGIN_SERVER        - Full ACR URL (e.g., myacr.azurecr.io)
```

## Deployment Options

The platform supports **three deployment targets**:

| Target | Best For | Pros | Cons |
|--------|----------|------|------|
| **ACI** (Container Instances) | Dev/Test | Simple, Fast, Cost-effective | No orchestration, Limited scaling |
| **AKS** (Kubernetes Service) | Production | Full orchestration, Auto-scaling, HA | Complex, Higher cost |
| **App Service** | Staging | Managed PaaS, Easy deployment | Less control, Platform limitations |

## Quick Start

### 1. Deploy with Azure CLI

**Deploy to ACI (Development):**
```bash
# Set environment variables
ENVIRONMENT="dev"
DEPLOYMENT_TARGET="aci"
LOCATION="eastus"
SQL_PASSWORD="YourSecurePassword123!"

# Deploy infrastructure (all parameters now included in .json files)
az deployment sub create \
  --name "platform-engineering-${ENVIRONMENT}-$(date +%Y%m%d)" \
  --location $LOCATION \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.aci.json \
  --parameters environment=$ENVIRONMENT \
  --parameters containerDeploymentTarget=$DEPLOYMENT_TARGET \
  --parameters sqlAdminPassword=$SQL_PASSWORD

# Optional: Override deployment flags for conditional deployment
# --parameters deployAdminApi=true deployChat=false  # Deploy only Admin API
# --parameters deployAdminApi=false deployChat=true  # Deploy only Chat

# Optional: Use existing Azure resources
# --parameters useExistingNetwork=true existingVNetName=my-vnet existingVNetResourceGroup=my-rg
# --parameters useExistingLogAnalytics=true existingLogAnalyticsWorkspaceName=my-laws existingLogAnalyticsResourceGroup=my-rg
# --parameters useExistingKeyVault=true existingKeyVaultName=my-kv existingKeyVaultResourceGroup=my-rg
```

**Deploy to AKS (Production):**
```bash
ENVIRONMENT="prod"
DEPLOYMENT_TARGET="aks"

az deployment sub create \
  --name "platform-engineering-${ENVIRONMENT}-$(date +%Y%m%d)" \
  --location eastus \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.aks.json \
  --parameters environment=$ENVIRONMENT \
  --parameters containerDeploymentTarget=$DEPLOYMENT_TARGET \
  --parameters sqlAdminPassword=$SQL_PASSWORD
```

### 2. Build and Push Container Images

**Build all images:**
```bash
# Enable BuildKit
export DOCKER_BUILDKIT=1

# Build MCP Server
docker build \
  --cache-from type=local,src=/tmp/.buildx-cache \
  --cache-to type=local,dest=/tmp/.buildx-cache \
  -t platform-engineering-copilot-mcp:latest \
  -f src/Platform.Engineering.Copilot.Mcp/Dockerfile .

# Build Chat
docker build \
  -t platform-engineering-copilot-chat:latest \
  -f src/Platform.Engineering.Copilot.Chat/Dockerfile .

# Build Admin API
docker build \
  -t platform-engineering-copilot-admin-api:latest \
  -f src/Platform.Engineering.Copilot.Admin.API/Dockerfile .

# Build Admin Client
docker build \
  -t platform-engineering-copilot-admin-client:latest \
  -f src/Platform.Engineering.Copilot.Admin.Client/Dockerfile .
```

**Push to ACR:**
```bash
# Login to ACR
ACR_NAME="your-acr-name"
az acr login --name $ACR_NAME

# Tag and push
docker tag platform-engineering-copilot-mcp:latest ${ACR_NAME}.azurecr.io/platform-engineering-copilot-mcp:latest
docker push ${ACR_NAME}.azurecr.io/platform-engineering-copilot-mcp:latest

docker tag platform-engineering-copilot-chat:latest ${ACR_NAME}.azurecr.io/platform-engineering-copilot-chat:latest
docker push ${ACR_NAME}.azurecr.io/platform-engineering-copilot-chat:latest

docker tag platform-engineering-copilot-admin-api:latest ${ACR_NAME}.azurecr.io/platform-engineering-copilot-admin-api:latest
docker push ${ACR_NAME}.azurecr.io/platform-engineering-copilot-admin-api:latest

docker tag platform-engineering-copilot-admin-client:latest ${ACR_NAME}.azurecr.io/platform-engineering-copilot-admin-client:latest
docker push ${ACR_NAME}.azurecr.io/platform-engineering-copilot-admin-client:latest
```

## Deployment Targets

### Azure Container Instances (ACI)

**When to use:**
- Development and testing
- Quick proof-of-concepts
- Cost-sensitive workloads
- Simple deployments without orchestration needs

**Features:**
- 4 separate container groups (MCP, Chat, Admin API, Admin Client)
- VNet integration for production
- Managed identity for ACR authentication
- Log Analytics integration
- Health probes (liveness + readiness)

**Deployment:**
```bash
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.aci.json \
  --parameters environment=dev containerDeploymentTarget=aci
```

**Access:**
```bash
# Get container IPs
az deployment sub show \
  --name platform-engineering-dev-20240101 \
  --query 'properties.outputs.aciMcpIpAddress.value'

# Get FQDNs (dev/staging only)
az deployment sub show \
  --name platform-engineering-dev-20240101 \
  --query 'properties.outputs.aciMcpFqdn.value'
```

### Azure Kubernetes Service (AKS)

**When to use:**
- Production workloads
- High availability requirements
- Auto-scaling needs
- Complex orchestration scenarios

**Features:**
- Private cluster with Azure AD integration
- Azure Policy and Defender enabled
- System node pool (3 nodes) + User node pool (auto-scale 3-10)
- Workload identity with OIDC
- Network policies (Azure CNI)
- FIPS 140-2 compliance

**Deployment:**
```bash
# Deploy infrastructure
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.aks.json \
  --parameters environment=prod containerDeploymentTarget=aks

# Get AKS credentials
az aks get-credentials \
  --resource-group rg-platform-engineering-prod \
  --name aks-platform-engineering-prod

# Create namespaces
kubectl create namespace platform-engineering
kubectl create namespace platform-engineering-system

# Create image pull secret
ACR_NAME="your-acr-name"
kubectl create secret docker-registry acr-secret \
  --docker-server=${ACR_NAME}.azurecr.io \
  --docker-username=00000000-0000-0000-0000-000000000000 \
  --docker-password=$(az acr login --name $ACR_NAME --expose-token --output tsv --query accessToken) \
  --namespace platform-engineering

# Apply Kubernetes manifests (see AKS Deployment Guide)
kubectl apply -f infra/kubernetes/
```

**Verify:**
```bash
kubectl get nodes
kubectl get pods -n platform-engineering
kubectl get services -n platform-engineering
```

### App Service (Existing)

**When to use:**
- Staging environments
- Managed PaaS requirements
- Simplified operations

**Features:**
- Managed hosting platform
- Easy CI/CD integration
- Built-in monitoring and diagnostics

**Deployment:**
```bash
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.appservice.json \
  --parameters environment=staging containerDeploymentTarget=appservice
```

## GitHub Actions CI/CD

### Container Build Pipeline

**Workflow:** `.github/workflows/container-build.yml`

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests
- Manual workflow dispatch

**Steps:**
1. Build all 4 container images with BuildKit cache
2. Scan images with Trivy for vulnerabilities
3. Push to ACR (on main/develop push)
4. Sign images with Notary (content trust)
5. Generate SBOM (Software Bill of Materials)

**Usage:**
```bash
# Trigger manually
gh workflow run container-build.yml -f environment=dev

# View status
gh run list --workflow=container-build.yml
```

### Infrastructure Deployment Pipeline

**Workflow:** `.github/workflows/infrastructure-deploy.yml`

**Steps:**
1. Validate Bicep templates
2. Run What-If analysis
3. Deploy infrastructure
4. Configure AKS (if target=aks)
5. Generate deployment summary

**Usage:**
```bash
# Deploy to dev with ACI
gh workflow run infrastructure-deploy.yml \
  -f environment=dev \
  -f deploymentTarget=aci

# Deploy to prod with AKS
gh workflow run infrastructure-deploy.yml \
  -f environment=prod \
  -f deploymentTarget=aks

# Destroy infrastructure
gh workflow run infrastructure-deploy.yml \
  -f environment=dev \
  -f destroyInfrastructure=true
```

## Manual Deployment

### Step 1: Deploy Infrastructure

```bash
# Set variables
ENVIRONMENT="dev"
LOCATION="eastus"
DEPLOYMENT_TARGET="aci"
SQL_PASSWORD="YourSecurePassword123!"

# Deploy
az deployment sub create \
  --name "platform-engineering-${ENVIRONMENT}-$(date +%Y%m%d-%H%M%S)" \
  --location $LOCATION \
  --template-file infra/bicep/main.bicep \
  --parameters environment=$ENVIRONMENT \
  --parameters containerDeploymentTarget=$DEPLOYMENT_TARGET \
  --parameters sqlAdminPassword=$SQL_PASSWORD

# Get outputs
az deployment sub show \
  --name platform-engineering-${ENVIRONMENT}-20240101-120000 \
  --query 'properties.outputs'
```

### Step 2: Build Container Images

```bash
# Build with BuildKit cache
export DOCKER_BUILDKIT=1

docker build \
  --cache-from type=registry,ref=myacr.azurecr.io/platform-engineering-copilot-mcp:latest \
  --cache-to type=inline \
  -t myacr.azurecr.io/platform-engineering-copilot-mcp:v1.0.0 \
  -f src/Platform.Engineering.Copilot.Mcp/Dockerfile .
```

### Step 3: Push to ACR

```bash
ACR_NAME="myacr"
VERSION="v1.0.0"

az acr login --name $ACR_NAME

docker push ${ACR_NAME}.azurecr.io/platform-engineering-copilot-mcp:${VERSION}
docker push ${ACR_NAME}.azurecr.io/platform-engineering-copilot-chat:${VERSION}
docker push ${ACR_NAME}.azurecr.io/platform-engineering-copilot-admin-api:${VERSION}
docker push ${ACR_NAME}.azurecr.io/platform-engineering-copilot-admin-client:${VERSION}
```

### Step 4: Update Container Instances (ACI)

```bash
RESOURCE_GROUP="rg-platform-engineering-dev"

# Restart containers to pull new images
az container restart \
  --resource-group $RESOURCE_GROUP \
  --name aci-mcp-dev

az container restart \
  --resource-group $RESOURCE_GROUP \
  --name aci-chat-dev
```

### Step 5: Deploy to AKS (if using AKS)

See [AKS Deployment Guide](./AKS-DEPLOYMENT-GUIDE.md)

## Configuration

### Environment Variables

All containers support these environment variables:

```bash
# ASP.NET Core
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:5000

# Azure SQL
SQL_CONNECTION_STRING=Server=tcp:...;Database=...;

# Application Insights
APPLICATIONINSIGHTS_CONNECTION_STRING=InstrumentationKey=...;IngestionEndpoint=...

# Key Vault
AZURE_KEY_VAULT_URI=https://kv-....vault.azure.net/

# Logging
Logging__LogLevel__Default=Information
Logging__LogLevel__Microsoft=Warning
```

### Parameter Files

All parameter files now include conditional deployment and existing resource parameters with default values.

**Development (ACI):** `main.parameters.aci.json`
- Public endpoints
- Standard ACR SKU
- No private endpoints
- `deployAdminApi: true` (default)
- `deployChat: true` (default)
- `useExistingNetwork: false` (default)
- `useExistingLogAnalytics: false` (default)
- `useExistingKeyVault: false` (default)

**Production (AKS):** `main.parameters.aks.json`
- Private cluster
- Premium ACR SKU
- Geo-replication enabled
- Content trust and quarantine
- All conditional deployment flags included
- All existing resource flags included

**Staging (App Service):** `main.parameters.appservice.json`
- Traditional App Service deployment
- No container registry
- All conditional deployment flags included
- All existing resource flags included

### Conditional Deployment

Control which components are deployed:

```bash
# Deploy only Chat (no Admin API)
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters @infra/bicep/main.parameters.aci.json \
  --parameters deployAdminApi=false deployChat=true

# Deploy only Admin API (no Chat)
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters @infra/bicep/main.parameters.aks.json \
  --parameters deployAdminApi=true deployChat=false
```

### Using Existing Resources

Reuse existing Azure infrastructure:

```bash
# Use existing VNet
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters @infra/bicep/main.parameters.aks.json \
  --parameters useExistingNetwork=true \
  --parameters existingVNetName=my-existing-vnet \
  --parameters existingVNetResourceGroup=my-network-rg \
  --parameters existingAppServiceSubnetName=app-subnet \
  --parameters existingPrivateEndpointSubnetName=pe-subnet

# Use existing Log Analytics Workspace
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters @infra/bicep/main.parameters.aci.json \
  --parameters useExistingLogAnalytics=true \
  --parameters existingLogAnalyticsWorkspaceName=my-laws \
  --parameters existingLogAnalyticsResourceGroup=my-monitoring-rg

# Use existing Key Vault
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters @infra/bicep/main.parameters.aks.json \
  --parameters useExistingKeyVault=true \
  --parameters existingKeyVaultName=my-kv \
  --parameters existingKeyVaultResourceGroup=my-security-rg

# Combine multiple existing resources
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters @infra/bicep/main.parameters.aks.json \
  --parameters useExistingNetwork=true \
  --parameters existingVNetName=my-vnet \
  --parameters existingVNetResourceGroup=my-network-rg \
  --parameters existingAppServiceSubnetName=app-subnet \
  --parameters existingPrivateEndpointSubnetName=pe-subnet \
  --parameters useExistingLogAnalytics=true \
  --parameters existingLogAnalyticsWorkspaceName=my-laws \
  --parameters existingLogAnalyticsResourceGroup=my-monitoring-rg \
  --parameters useExistingKeyVault=true \
  --parameters existingKeyVaultName=my-kv \
  --parameters existingKeyVaultResourceGroup=my-security-rg
```

## Troubleshooting

### Container Build Issues

**Problem:** BuildKit cache not working
```bash
# Clear Docker build cache
docker buildx prune -af

# Rebuild without cache
docker build --no-cache -t myimage:latest .
```

**Problem:** Multi-stage build fails
```bash
# Build specific stage
docker build --target build -t myimage:build .
docker build --target runtime -t myimage:runtime .
```

### ACR Authentication Issues

**Problem:** Cannot push to ACR
```bash
# Re-login to ACR
az acr login --name $ACR_NAME

# Check ACR credentials
az acr credential show --name $ACR_NAME

# Test ACR connectivity
az acr check-health --name $ACR_NAME
```

**Problem:** ACI cannot pull from ACR
```bash
# Verify managed identity
az container show \
  --resource-group $RESOURCE_GROUP \
  --name $CONTAINER_NAME \
  --query 'identity'

# Check ACR role assignment
az role assignment list \
  --scope /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.ContainerRegistry/registries/{acr}
```

### ACI Deployment Issues

**Problem:** Container keeps restarting
```bash
# View logs
az container logs \
  --resource-group $RESOURCE_GROUP \
  --name $CONTAINER_NAME \
  --tail 100

# Check container state
az container show \
  --resource-group $RESOURCE_GROUP \
  --name $CONTAINER_NAME \
  --query 'containers[0].instanceView'
```

**Problem:** Cannot access container
```bash
# Get IP address
az container show \
  --resource-group $RESOURCE_GROUP \
  --name $CONTAINER_NAME \
  --query 'ipAddress.ip' -o tsv

# Test connectivity
curl http://<IP>:5000/health
```

### AKS Deployment Issues

**Problem:** Cannot connect to AKS
```bash
# Get credentials
az aks get-credentials \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_NAME \
  --overwrite-existing

# Test connectivity
kubectl get nodes
```

**Problem:** Pods not starting
```bash
# Describe pod
kubectl describe pod <pod-name> -n platform-engineering

# View events
kubectl get events -n platform-engineering --sort-by='.lastTimestamp'

# Check logs
kubectl logs <pod-name> -n platform-engineering
```

### Bicep Deployment Issues

**Problem:** Deployment validation fails
```bash
# Validate template
az deployment sub validate \
  --location eastus \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.aci.json

# Check what will change
az deployment sub what-if \
  --location eastus \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.aci.json
```

**Problem:** Resource already exists
```bash
# Option 1: Use existing resources instead
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters @infra/bicep/main.parameters.aci.json \
  --parameters useExistingNetwork=true \
  --parameters existingVNetName=existing-vnet \
  --parameters existingVNetResourceGroup=existing-rg

# Option 2: Delete and recreate
az group delete --name $RESOURCE_GROUP --yes
az deployment sub create ...
```

**Problem:** Cannot find existing resource
```bash
# Verify resource exists
az network vnet show --name my-vnet --resource-group my-rg
az monitor log-analytics workspace show --workspace-name my-laws --resource-group my-rg
az keyvault show --name my-kv --resource-group my-rg

# Check resource group name is correct
az group list --query "[].name" -o table
```

## Next Steps

- [AKS Deployment Guide](./AKS-DEPLOYMENT-GUIDE.md) - Detailed AKS setup
- [Container Build Guide](./CONTAINER-BUILD-GUIDE.md) - Advanced build techniques
- [Security Guide](./SECURITY.md) - Security best practices
- [Monitoring Guide](./MONITORING.md) - Observability setup

## Support

For issues or questions:
1. Check existing GitHub issues
2. Review Azure Monitor logs
3. Consult Application Insights telemetry
4. Create a new GitHub issue with deployment logs
