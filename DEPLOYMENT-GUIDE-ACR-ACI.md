# ACR/ACI Deployment Guide

## 🔴 CRITICAL: Azure OpenAI Configuration Required

**The MCP server requires Azure OpenAI configuration to function.** Without this, you'll get blank HTTP 500 responses.

### Prerequisites: Azure OpenAI Instance

Before deploying, ensure you have:
- ✅ Azure OpenAI instance in Azure Government Cloud (`usgovvirginia` or `usgoviowa`)
- ✅ Deployment name (e.g., `gpt-4o`, `gpt-35-turbo`)
- ✅ Azure OpenAI endpoint URL (e.g., `https://your-instance.openai.azure.us/`)
- ✅ Azure OpenAI API key **OR** Managed Identity access

**Get these values:**
```bash
# Find your Azure OpenAI instance
az cognitiveservices account list \
  --resource-group <your-rg> \
  --query "[?kind=='OpenAI'].{name:name, endpoint:properties.endpoint}"

# Get the endpoint
az cognitiveservices account show \
  --name <your-openai-instance> \
  --resource-group <your-rg> \
  --query properties.endpoint

# Get API key (if using key-based auth)
az cognitiveservices account keys list \
  --name <your-openai-instance> \
  --resource-group <your-rg>
```

## Prepare Environment Variables

See [infra/aci-environment-variables.md](infra/aci-environment-variables.md) for complete configuration guide.

**Quick setup:**
```bash
# Copy the template
cp .env.aci.example .env.aci

# Edit with your values
nano .env.aci

# Load environment variables
export $(cat .env.aci | xargs)

# Verify key variables
echo "Azure OpenAI Endpoint: $AZURE_OPENAI_ENDPOINT"
echo "Tenant ID: $AZURE_TENANT_ID"
```

## Option 1: Quick Deployment with Config (Recommended)

### For macOS with Docker Desktop ARM64

```bash
cd /path/to/platform-engineering-copilot

ACR_NAME="platengcopilotdevacr759d45b2"
RESOURCE_GROUP="platengcopilot-dev-rg"
CONTAINER_NAME="platengcopilot-dev-platform-mcp-aci"

# Login to ACR
az acr login --name $ACR_NAME

# Build and push with buildx (multi-platform support)
docker buildx build --push \
  -t ${ACR_NAME}.azurecr.us/platform-engineering-copilot-mcp:latest \
  -f src/Platform.Engineering.Copilot.Mcp/Dockerfile \
  .

# Update container with Azure OpenAI configuration
az container create \
  --resource-group $RESOURCE_GROUP \
  --name $CONTAINER_NAME \
  --image ${ACR_NAME}.azurecr.us/platform-engineering-copilot-mcp:latest \
  --environment-variables \
    Gateway__AzureOpenAI__Endpoint="https://your-instance.openai.azure.us/" \
    Gateway__AzureOpenAI__DeploymentName="gpt-4o" \
    Gateway__AzureOpenAI__UseManagedIdentity="true" \
  --registry-login-server ${ACR_NAME}.azurecr.us \
  --registry-username <acr-username> \
  --registry-password <acr-password> \
  --cpu 2 \
  --memory 4 \
  --ports 5100 \
  --protocol TCP
```

## Option 2: Full Redeployment with Bicep (Recommended for production)

### Deploy complete infrastructure stack with proper configuration

First, create a parameters override file with your Azure OpenAI details:

**File: `infra/bicep/main.parameters.aci-with-aoai.json`**
```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "projectName": { "value": "platform-engineering" },
    "environment": { "value": "dev" },
    "location": { "value": "usgovvirginia" },
    "containerDeploymentTarget": { "value": "aci" },
    "deployACR": { "value": true },
    "deployACI": { "value": true },
    "aciMcpCpuCores": { "value": 2 },
    "aciMcpMemoryInGB": { "value": 4 },
    "mcpEnvironmentVariables": {
      "value": [
        {
          "name": "Gateway__AzureOpenAI__Endpoint",
          "value": "https://your-instance.openai.azure.us/"
        },
        {
          "name": "Gateway__AzureOpenAI__DeploymentName",
          "value": "gpt-4o"
        },
        {
          "name": "Gateway__AzureOpenAI__UseManagedIdentity",
          "value": "true"
        }
      ]
    }
  }
}
```

Then deploy:
```bash
# 1. Build using docker buildx
ACR_NAME="platengcopilotdevacr759d45b2"
az acr login --name $ACR_NAME

docker buildx build --push \
  -t ${ACR_NAME}.azurecr.us/platform-engineering-copilot-mcp:latest \
  -f src/Platform.Engineering.Copilot.Mcp/Dockerfile \
  .

# 2. Deploy with Bicep (update parameters file with your Azure OpenAI details)
az deployment sub create \
  --name "platform-engineering-dev-$(date +%Y%m%d-%H%M%S)" \
  --location usgovvirginia \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.aci-with-aoai.json \
  --parameters sqlAdminPassword="YourSecurePassword123!"
```

## Option 3: Linux/WSL Native Build

If running on Linux or WSL:

```bash
ACR_NAME="platengcopilotdevacr759d45b2"

# Login to ACR
az acr login --name $ACR_NAME

# Build with buildx and push directly
docker buildx build --push \
  -t ${ACR_NAME}.azurecr.us/platform-engineering-copilot-mcp:latest \
  -f src/Platform.Engineering.Copilot.Mcp/Dockerfile \
  .

# Update container with Azure OpenAI configuration
az container update \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci \
  --set \
    containers[0].environmentVariables[0].name=Gateway__AzureOpenAI__Endpoint \
    containers[0].environmentVariables[0].value=https://your-instance.openai.azure.us/ \
    containers[0].environmentVariables[1].name=Gateway__AzureOpenAI__DeploymentName \
    containers[0].environmentVariables[1].value=gpt-4o \
    containers[0].environmentVariables[2].name=Gateway__AzureOpenAI__UseManagedIdentity \
    containers[0].environmentVariables[2].value=true
```

## Verification

### Check Container Status
```bash
az container show \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci \
  --query "containers[0].{name:name, image:image, state:instanceView.currentState.state}"
```

### Check Azure OpenAI Configuration
```bash
# Verify environment variables are set
az container show \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci \
  --query "containers[0].environmentVariables[?contains(name, 'Gateway')]"
```

### Health Check
```bash
curl -s http://20.158.32.162:5100/health | jq .
```

### Test Plugin Functions
```bash
# Test documentation search (KnowledgeBasePlugin)
curl -s -X POST http://20.158.32.162:5100/mcp/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Search Azure documentation for AKS networking",
    "conversationId": "test-123"
  }' | jq '.{intentType, success, response}'

# Test infrastructure operations  
curl -s -X POST http://20.158.32.162:5100/mcp/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Generate a Bicep template for AKS",
    "conversationId": "test-456"
  }' | jq '.{intentType, success, response}'
```

### If you get blank responses (HTTP 500):
```bash
# This indicates Azure OpenAI is not configured. Check:
# 1. Environment variables are set
az container show \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci \
  --query "containers[0].environmentVariables"

# 2. Container is running
az container show \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci \
  --query "containers[0].instanceView.currentState"

# 3. Restart container after updating environment variables
az container restart \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci

# Wait 30 seconds for container to initialize
sleep 30

# Test again
curl -s http://20.158.32.162:5100/health | jq .
```

## Dockerfile Changes

The Dockerfile was updated to remove Node.js installation (not needed for C# MCP server):

**Before**: 65 lines for Node.js + 40 lines for setup  
**After**: Only curl for health checks (minimal footprint)

This fix resolves Rosetta/ARM64 compilation issues on macOS Docker Desktop.

## Next Steps for Continuous Deployment

### Option A: GitHub Actions (Recommended)
Create `.github/workflows/deploy-mcp.yml`:
```yaml
name: Build and Deploy MCP to ACR/ACI
on:
  push:
    branches: [main]
    paths:
      - 'src/Platform.Engineering.Copilot.Mcp/**'
      - 'src/Platform.Engineering.Copilot.Core/**'
      
jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: docker/setup-buildx-action@v2
      - uses: docker/login-action@v2
        with:
          registry: ${{ secrets.ACR_NAME }}.azurecr.us
          username: ${{ secrets.ACR_USERNAME }}
          password: ${{ secrets.ACR_PASSWORD }}
      - name: Build and Push with buildx
        uses: docker/build-push-action@v4
        with:
          context: .
          file: ./src/Platform.Engineering.Copilot.Mcp/Dockerfile
          push: true
          tags: ${{ secrets.ACR_NAME }}.azurecr.us/platform-engineering-copilot-mcp:latest
          platforms: linux/amd64,linux/arm64
      - name: Restart ACI
        run: |
          az container restart \
            --resource-group ${{ secrets.RESOURCE_GROUP }} \
            --name platengcopilot-dev-platform-mcp-aci
```

### Option B: Deploy Script
Run the provided deployment script:
```bash
chmod +x scripts/deploy-mcp-aci.sh
./scripts/deploy-mcp-aci.sh
```

## Troubleshooting

### ❌ Blank responses / HTTP 500 errors

**Cause**: Azure OpenAI configuration missing or not initialized

**Solution**:
```bash
# 1. Verify environment variables are set
az container show \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci \
  --query "containers[0].environmentVariables[?contains(name, 'Gateway')]" \
  --output table

# 2. If missing, update the container
az container update \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci \
  --environment-variables \
    Gateway__AzureOpenAI__Endpoint="https://your-instance.openai.azure.us/" \
    Gateway__AzureOpenAI__DeploymentName="gpt-4o" \
    Gateway__AzureOpenAI__UseManagedIdentity="true"

# 3. Restart the container
az container restart \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci

# 4. Wait for initialization (30-45 seconds)
sleep 45

# 5. Test again
curl -s http://20.158.32.162:5100/health | jq .
```

**Error message**: Look for `Chat completion service not available` in logs - this confirms Azure OpenAI isn't configured.

### Container won't start
```bash
# Check logs
az container logs \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci

# Restart
az container restart \
  --resource-group platengcopilot-dev-rg \
  --name platengcopilot-dev-platform-mcp-aci
```

### Image pull errors
```bash
# Verify ACR login
az acr login --name platengcopilotdevacr759d45b2

# Check image exists
az acr repository show-tags \
  --name platengcopilotdevacr759d45b2 \
  --repository platform-engineering-copilot-mcp
```

### Build fails on ARM64 macOS
**Always use ACR cloud build** (Option 1) instead of local Docker Desktop.

## Summary

✅ Code successfully refactored and compiled  
✅ MCP Container running at 20.158.32.162:5100  
✅ **CRITICAL**: Azure OpenAI configuration required for AI features
✅ Blank response fix: Set `Gateway__AzureOpenAI__*` environment variables
✅ Ready for deployment

### Recommended next steps:

1. **Get Azure OpenAI credentials** (endpoint, deployment name)
2. **Use Option 1 or Option 2** with proper configuration
3. **Verify environment variables** are set before testing
4. **Restart container** after configuration changes
5. **Test with health check and chat endpoint**
