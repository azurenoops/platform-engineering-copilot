# ACI Environment Configuration - Quick Start Guide

## Overview

This directory contains configuration for deploying the Platform Engineering Copilot to Azure Container Instances (ACI).

## Files

### 1. `.env.aci.example`
Template file with all available environment variables. Copy and customize for your deployment.

**Usage:**
```bash
cp .env.aci.example .env.aci
nano .env.aci  # Edit with your values
```

### 2. `infra/aci-environment-variables.md`
Comprehensive documentation of all environment variables, their purpose, and configuration details.

**Contains:**
- All required and optional variables
- Service-specific variable requirements
- Configuration by deployment method (CLI, Bicep)
- Validation checklist
- Troubleshooting guide

### 3. `scripts/deploy-mcp-to-aci.sh`
Automated deployment script that handles:
- Environment variable validation
- ACR credentials retrieval
- Container deployment with all configs
- Health endpoint testing
- Deployment verification

**Usage:**
```bash
chmod +x scripts/deploy-mcp-to-aci.sh
./scripts/deploy-mcp-to-aci.sh
```

## Quick Start

### Step 1: Prepare Configuration
```bash
# Copy template
cp .env.aci.example .env.aci

# Edit with your Azure OpenAI and Azure details
nano .env.aci

# Load variables into shell
export $(cat .env.aci | xargs)
```

### Step 2: Deploy with Script
```bash
./scripts/deploy-mcp-to-aci.sh
```

**The script will:**
1. ✅ Validate all required variables
2. ✅ Get ACR credentials
3. ✅ Prompt for deployment details
4. ✅ Deploy container to ACI
5. ✅ Test health endpoint
6. ✅ Display URLs and next steps

### Step 3: Verify Deployment
```bash
# Health check
curl -s http://[your-mcp-url]:5100/health | jq .

# Test MCP endpoint
curl -X POST http://[your-mcp-url]:5100/mcp/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "test", "conversationId": "test-001"}' | jq .
```

## Critical Configuration Variables

### 🔴 Azure OpenAI (REQUIRED)

These must be set for the MCP server to function:

```bash
AZURE_OPENAI_ENDPOINT=https://your-instance.openai.azure.us/
AZURE_OPENAI_DEPLOYMENT=gpt-4o
AZURE_OPENAI_API_KEY=your-api-key-here
```

Without these, you'll get HTTP 500 blank responses.

### 🔵 Azure Gateway (REQUIRED)

Authentication for Azure resource access:

```bash
AZURE_TENANT_ID=your-tenant-id
AZURE_CLIENT_ID=service-principal-id
AZURE_CLIENT_SECRET=service-principal-secret
AZURE_SUBSCRIPTION_ID=your-subscription-id
AZURE_CLOUD_ENVIRONMENT=AzureGovernment
```

### 🟢 Optional but Recommended

```bash
NIST_CONTROLS_BASE_URL=https://github.com/.../nist.gov/SP800-53/rev5/json
NIST_CONTROLS_CACHE_DURATION=24
```

## Troubleshooting

### HTTP 500 Blank Responses

**Problem:** MCP server returns empty HTTP 500 errors

**Solution:**
1. Verify Azure OpenAI variables are set:
   ```bash
   echo "Endpoint: $AZURE_OPENAI_ENDPOINT"
   echo "Deployment: $AZURE_OPENAI_DEPLOYMENT"
   echo "API Key: ${AZURE_OPENAI_API_KEY:0:10}***"
   ```

2. Check container environment:
   ```bash
   az container show \
     --resource-group $RESOURCE_GROUP \
     --name platform-mcp-aci \
     --query "containers[0].environmentVariables[?contains(name, 'Gateway__AzureOpenAI')]"
   ```

3. Restart container:
   ```bash
   az container restart \
     --resource-group $RESOURCE_GROUP \
     --name platform-mcp-aci
   ```

### Cannot Connect to Azure OpenAI

**Problem:** Container cannot reach Azure OpenAI endpoint

**Solution:**
1. Verify endpoint URL format (must end with `/`):
   ```bash
   # ✅ Correct
   https://your-instance.openai.azure.us/
   
   # ❌ Incorrect
   https://your-instance.openai.azure.us
   ```

2. Test connectivity:
   ```bash
   curl -I https://your-instance.openai.azure.us/
   ```

3. Verify API key:
   ```bash
   az cognitiveservices account keys list \
     --name your-openai-instance \
     --resource-group your-rg
   ```

### Invalid Tenant or Credentials

**Problem:** Azure authentication fails

**Solution:**
1. Verify tenant ID:
   ```bash
   az account show --query tenantId
   ```

2. Verify service principal:
   ```bash
   az ad sp show --id $AZURE_CLIENT_ID
   ```

3. Ensure service principal has required permissions:
   ```bash
   az role assignment list \
     --assignee $AZURE_CLIENT_ID \
     --include-inherited-assignments
   ```

## Environment Variable Reference

See `infra/aci-environment-variables.md` for complete documentation:

- All available variables
- Variable descriptions and purposes
- Required vs optional flags
- Service-specific requirements
- Configuration examples
- Validation checklist

## Manual Deployment (without script)

If you prefer to deploy manually:

```bash
# Load variables
export $(cat .env.aci | xargs)

# Get ACR credentials
ACR_USERNAME=$(az acr credential show --name platengcopilotdevacr759d45b2 --query username -o tsv)
ACR_PASSWORD=$(az acr credential show --name platengcopilotdevacr759d45b2 --query passwords[0].value -o tsv)

# Deploy
az container create \
  --resource-group platengcopilot-dev-rg \
  --name platform-mcp-aci \
  --image platengcopilotdevacr759d45b2.azurecr.us/platform-engineering-copilot-mcp:latest \
  --registry-login-server platengcopilotdevacr759d45b2.azurecr.us \
  --registry-username $ACR_USERNAME \
  --registry-password $ACR_PASSWORD \
  --cpu 2 \
  --memory 4 \
  --ports 5100 \
  --environment-variables \
    Gateway__AzureOpenAI__Endpoint=$AZURE_OPENAI_ENDPOINT \
    Gateway__AzureOpenAI__DeploymentName=$AZURE_OPENAI_DEPLOYMENT \
    Gateway__AzureOpenAI__ApiKey=$AZURE_OPENAI_API_KEY \
    Gateway__Azure__TenantId=$AZURE_TENANT_ID \
    Gateway__Azure__ClientId=$AZURE_CLIENT_ID \
    Gateway__Azure__ClientSecret=$AZURE_CLIENT_SECRET
```

## Useful Commands

### Check Deployment Status
```bash
az container show \
  --resource-group platengcopilot-dev-rg \
  --name platform-mcp-aci \
  --query "{Status: instanceView.currentState.state, IP: ipAddress.fqdn}"
```

### View Logs
```bash
az container logs \
  --resource-group platengcopilot-dev-rg \
  --name platform-mcp-aci \
  --tail 50
```

### Update Environment Variables
```bash
az container update \
  --resource-group platengcopilot-dev-rg \
  --name platform-mcp-aci \
  --environment-variables \
    Gateway__AzureOpenAI__Endpoint="new-endpoint"
```

### Restart Container
```bash
az container restart \
  --resource-group platengcopilot-dev-rg \
  --name platform-mcp-aci
```

### Delete Container
```bash
az container delete \
  --resource-group platengcopilot-dev-rg \
  --name platform-mcp-aci \
  --yes
```

## Support

- For deployment issues: See [DEPLOYMENT-GUIDE-ACR-ACI.md](../DEPLOYMENT-GUIDE-ACR-ACI.md)
- For ACI specifics: See [infra/bicep/ACI-DEPLOYMENT-GUIDE.md](infra/bicep/ACI-DEPLOYMENT-GUIDE.md)
- For environment variables: See [infra/aci-environment-variables.md](infra/aci-environment-variables.md)
