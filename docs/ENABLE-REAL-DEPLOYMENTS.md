# Enable Real Azure Deployments

The Platform Engineering Copilot currently **simulates** deployments by default. Follow these steps to enable real Azure infrastructure provisioning.

## Quick Start

```bash
# 1. Run the setup script
./scripts/setup-azure-auth.sh

# 2. Restart containers
docker-compose -f docker-compose.mcp-admin.yml down
docker-compose -f docker-compose.mcp-admin.yml up -d

# 3. Verify deployment capability
docker logs pec-admin-api 2>&1 | grep -i "deployer"
```

You should see:
```
🚀 Using BicepDeployer to deploy {template-name} (Bicep)
```

Instead of:
```
⚠️ No deployer available for format Bicep, using simulation
```

## Authentication Methods

### Option 1: Azure CLI (Easiest)

**Requirements:**
- Azure CLI installed: `az --version`
- Logged in: `az login`
- For Azure Government: `az cloud set --name AzureUSGovernment && az login`

**Configuration:**
The `docker-compose.mcp-admin.yml` already mounts `~/.azure:/root/.azure:ro` to share your CLI credentials with containers.

**Pros:**
- No secrets in files
- Works immediately after `az login`
- Automatic credential refresh

**Cons:**
- Requires `az login` on host machine
- Not suitable for CI/CD pipelines

### Option 2: Service Principal (Production)

**Create Service Principal:**
```bash
# Commercial Cloud
az ad sp create-for-rbac \
  --name platform-engineering-copilot \
  --role Contributor \
  --scopes /subscriptions/{subscription-id}

# Government Cloud  
az cloud set --name AzureUSGovernment
az ad sp create-for-rbac \
  --name platform-engineering-copilot \
  --role Contributor \
  --scopes /subscriptions/{subscription-id}
```

**Output:**
```json
{
  "appId": "00000000-0000-0000-0000-000000000000",
  "displayName": "platform-engineering-copilot",
  "password": "your-client-secret",
  "tenant": "00000000-0000-0000-0000-000000000000"
}
```

**Add to `.env`:**
```bash
AZURE_TENANT_ID=00000000-0000-0000-0000-000000000000
AZURE_CLIENT_ID=00000000-0000-0000-0000-000000000000  # appId
AZURE_CLIENT_SECRET=your-client-secret                 # password
AZURE_SUBSCRIPTION_ID=00000000-0000-0000-0000-000000000000
AZURE_CLOUD_ENVIRONMENT=AzureGovernment  # or AzureCloud
```

**Pros:**
- Works in CI/CD pipelines
- Fine-grained permissions via RBAC
- Secrets manageable via Key Vault

**Cons:**
- Requires secret management
- Manual credential rotation

## Verify Configuration

### 1. Check Environment Variables

```bash
# MCP Server
docker exec pec-mcp env | grep AZURE

# Admin API
docker exec pec-admin-api env | grep AZURE
```

Should show:
```
AZURE_TENANT_ID=00000000-...
AZURE_CLIENT_ID=00000000-...
AZURE_SUBSCRIPTION_ID=00000000-...
AZURE_CLOUD_ENVIRONMENT=AzureGovernment
```

### 2. Test Deployment

1. Navigate to http://localhost:5000/environments
2. Click "Provision New"
3. Select template: "Standard Web Application"
4. Fill in parameters:
   - **Subscription**: Your subscription ID
   - **Location**: `usgovvirginia` or `eastus`
   - **Resource Group**: `rg-test-deployment`
5. Click "Provision"

### 3. Monitor Deployment

```bash
# Real-time logs
docker logs -f pec-admin-api

# Look for:
🚀 Starting Bicep deployment for test-env in rg-test-deployment
✅ Deployment test-env-20260114123456 completed with status: Succeeded
```

### 4. Verify in Azure Portal

- Navigate to https://portal.azure.us (Government) or https://portal.azure.com (Commercial)
- Check resource group `rg-test-deployment`
- Should see provisioned resources (App Service, App Service Plan, etc.)

## Troubleshooting

### "No deployer available for format Bicep, using simulation"

**Cause**: Azure credentials not configured or not accessible

**Fix**:
```bash
# Check if credentials are set
docker exec pec-admin-api env | grep AZURE_CLIENT_ID

# If empty, update .env and restart:
docker-compose -f docker-compose.mcp-admin.yml restart platform-admin-api
```

### "DefaultAzureCredential failed to retrieve a token"

**Cause**: 
- Service Principal credentials invalid
- Azure CLI not logged in (when using CLI auth)
- Wrong cloud environment

**Fix**:
```bash
# Verify Azure CLI login
az account show

# For Government Cloud
az cloud set --name AzureUSGovernment
az login

# Restart containers
docker-compose -f docker-compose.mcp-admin.yml restart
```

### "Deployment failed: Unauthorized"

**Cause**: Service Principal lacks Contributor permissions

**Fix**:
```bash
# Grant Contributor role
az role assignment create \
  --assignee <AZURE_CLIENT_ID> \
  --role Contributor \
  --scope /subscriptions/<SUBSCRIPTION_ID>
```

### "Resource group already exists"

**Cause**: Normal - deployer reuses existing resource groups

**Result**: Deployment continues, adds resources to existing RG

## Deployment Formats Supported

| Format | Status | Deployer Class | Notes |
|--------|--------|----------------|-------|
| **Bicep** | ✅ Fully Implemented | `BicepDeployer` | Uses Azure SDK, native ARM |
| **ARM JSON** | ✅ Fully Implemented | `BicepDeployer` | Same as Bicep (ARM compiles to JSON) |
| **Terraform** | ✅ Implemented | `TerraformDeployer` | Requires Terraform CLI in container |

## Cost Management

**Important**: Deployments create REAL Azure resources that incur costs!

**Best Practices:**
1. Set **Expiration** on environments (auto-delete after X days)
2. Use **Dev/Test SKUs** (B1, F1) for testing
3. Monitor costs in Azure Portal > Cost Management
4. Tag all resources: `CreatedBy=platform-engineering-copilot`
5. Set up budget alerts

**Clean Up:**
```bash
# Delete environment via UI
http://localhost:5000/environments/{id}
# Click "Delete Environment"

# Or via Azure CLI
az group delete --name rg-test-deployment --yes --no-wait
```

## Production Recommendations

1. **Use Key Vault** for secrets:
   ```yaml
   # docker-compose override
   environment:
     - AZURE_CLIENT_SECRET_KEY_VAULT=https://your-kv.vault.azure.us/secrets/client-secret
   ```

2. **Use Managed Identity** when running in Azure:
   ```bash
   AZURE_USE_MANAGED_IDENTITY=true
   # Remove CLIENT_ID and CLIENT_SECRET
   ```

3. **Enable audit logging**:
   ```bash
   Logging__LogLevel__Platform.Engineering.Copilot.Agents.Infrastructure.Deployment=Information
   ```

4. **Restrict subscription scope**:
   ```bash
   # Create SP with limited scope
   az ad sp create-for-rbac \
     --role Contributor \
     --scopes /subscriptions/{sub}/resourceGroups/rg-allowed-*
   ```

## Next Steps

- [Service Template Creation](../docs/SERVICE-TEMPLATES.md)
- [Compliance Controls](../docs/COMPLIANCE.md)
- [Drift Detection](../docs/DRIFT-DETECTION.md)
