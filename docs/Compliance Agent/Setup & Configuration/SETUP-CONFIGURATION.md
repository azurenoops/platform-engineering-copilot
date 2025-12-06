# Setup & Configuration Guide

> **Complete installation and configuration guide for Compliance Agent**

---

## Table of Contents

- [Prerequisites](#prerequisites)
- [Initial Setup](#initial-setup)
- [Azure AD Configuration](#azure-ad-configuration)
- [Key Vault Setup](#key-vault-setup)
- [Storage Account Configuration](#storage-account-configuration)
- [Framework Baseline Selection](#framework-baseline-selection)
- [Continuous Monitoring](#continuous-monitoring)
- [Troubleshooting](#troubleshooting)

---

## Prerequisites

### Azure Requirements
- ✅ **Azure Subscription** - Active subscription with Owner or Contributor role
- ✅ **Azure AD Tenant** - For authentication and RBAC
- ✅ **Storage Account** - For document storage (can be created during setup)
- ✅ **Key Vault** - For secure secret management (optional but recommended)

### Resource Permissions
Minimum required Azure RBAC roles:
- **Reader** - View resources for assessment
- **Storage Blob Data Contributor** - Upload compliance documents
- **Key Vault Secrets User** - Read secrets (if using Key Vault)

### Software Requirements
- **.NET 9.0 SDK** - For running the application
- **Azure CLI** - For resource management
- **PowerShell 7+** or **Bash** - For automation scripts

---

## Initial Setup

### 1. Clone Repository

```bash
git clone https://github.com/azurenoops/platform-engineering-copilot.git
cd platform-engineering-copilot
```

### 2. Configure Application Settings

Create `appsettings.json` from template:

```bash
cp appsettings.example.json appsettings.json
```

Edit `appsettings.json`:

```json
{
  "AzureAD": {
    "Instance": "https://login.microsoftonline.com/",    
    "Audience": "api://your-app-client-id",
    "RequireMfa": true,
    "RequireCac": false
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-openai.openai.azure.com",
    "DeploymentName": "gpt-4",
    "ApiVersion": "2024-06-01"
  },
  "AgentConfiguration": {
    "EnabledAgents": ["Compliance", "Infrastructure", "Security"],
    "ComplianceAgent": {
      "Enabled": true,
      "DefaultFramework": "NIST80053R5",
      "DefaultBaseline": "High",
      "EnableAutomatedRemediation": false,
      "EnableContinuousMonitoring": false
    }
  },
  "BlobStorage": {
    "ConnectionString": "",
    "ContainerName": "compliance-documents"
  }
}
```

### 3. Set Azure Subscription

```bash
# Login to Azure
az login

# List subscriptions
az account list --output table

# Set default subscription
az account set --subscription "Your-Subscription-Name"
```

### 4. Create Required Azure Resources

Run the setup script:

```bash
cd scripts
chmod +x setup-compliance-resources.sh
./setup-compliance-resources.sh
```

Or manually create resources:

```bash
# Variables
RESOURCE_GROUP="rg-platform-copilot"
LOCATION="eastus2"
STORAGE_ACCOUNT="peccompliancestorage"
KEY_VAULT="pec-compliance-kv"

# Create resource group
az group create --name $RESOURCE_GROUP --location $LOCATION

# Create storage account
az storage account create \
  --name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --sku Standard_LRS \
  --kind StorageV2 \
  --allow-blob-public-access false

# Create blob container
az storage container create \
  --name compliance-documents \
  --account-name $STORAGE_ACCOUNT \
  --auth-mode login

# Create Key Vault (optional)
az keyvault create \
  --name $KEY_VAULT \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --enable-rbac-authorization true
```

---

## Azure AD Configuration

### 1. Create App Registration

**Via Azure Portal:**

1. Navigate to **Azure Active Directory** → **App Registrations**
2. Click **New registration**
3. Configure:
   - **Name**: `Platform Engineering Copilot`
   - **Supported account types**: `Accounts in this organizational directory only`
   - **Redirect URI**: `https://your-domain.com/signin-oidc` (for web) or leave blank (for API)
4. Click **Register**
5. Note the **Application (client) ID** and **Directory (tenant) ID**

**Via Azure CLI:**

```bash
# Create app registration
APP_NAME="Platform Engineering Copilot"
APP_ID=$(az ad app create \
  --display-name "$APP_NAME" \
  --sign-in-audience AzureADMyOrg \
  --query appId -o tsv)

echo "Application ID: $APP_ID"

# Get tenant ID
TENANT_ID=$(az account show --query tenantId -o tsv)
echo "Tenant ID: $TENANT_ID"
```

### 2. Configure App Roles

Edit the app manifest to add roles:

```bash
# Download manifest
az ad app show --id $APP_ID > app-manifest.json
```

Add `appRoles` section (see [RBAC-AUTHORIZATION.md](RBAC-AUTHORIZATION.md) for complete role definitions):

```json
{
  "appRoles": [
    {
      "allowedMemberTypes": ["User"],
      "description": "Full administrative access to compliance operations",
      "displayName": "Compliance Administrator",
      "id": "8e3af657-a8ff-443c-a75c-2fe8c4bcb635",
      "isEnabled": true,
      "value": "Compliance.Administrator"
    },
    {
      "allowedMemberTypes": ["User"],
      "description": "Can view and audit compliance data",
      "displayName": "Compliance Auditor",
      "id": "9e3af657-a8ff-443c-a75c-2fe8c4bcb636",
      "isEnabled": true,
      "value": "Compliance.Auditor"
    }
  ]
}
```

Upload updated manifest:

```bash
az ad app update --id $APP_ID --app-roles @app-roles.json
```

### 3. Assign Roles to Users

**Via Azure Portal:**

1. Navigate to **Azure AD** → **Enterprise Applications**
2. Find **Platform Engineering Copilot**
3. Click **Users and groups** → **Add user/group**
4. Select user and role (e.g., `Compliance.Administrator`)
5. Click **Assign**

**Via Azure CLI:**

```bash
# Get service principal ID
SP_ID=$(az ad sp list --display-name "$APP_NAME" --query "[0].id" -o tsv)

# Get user object ID
USER_EMAIL="admin@yourcompany.com"
USER_ID=$(az ad user show --id "$USER_EMAIL" --query id -o tsv)

# Assign Compliance.Administrator role
ROLE_ID="8e3af657-a8ff-443c-a75c-2fe8c4bcb635"
az rest --method POST \
  --uri "https://graph.microsoft.com/v1.0/users/$USER_ID/appRoleAssignments" \
  --headers "Content-Type=application/json" \
  --body "{
    \"principalId\": \"$USER_ID\",
    \"resourceId\": \"$SP_ID\",
    \"appRoleId\": \"$ROLE_ID\"
  }"
```

### 4. Configure Authentication

Update `appsettings.json`:

```json
{
  "AzureAD": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-app-client-id>",
    "Audience": "api://<your-app-client-id>"
  }
}
```

---

## Key Vault Setup

### 1. Create Key Vault

```bash
KEY_VAULT="pec-compliance-kv"
RESOURCE_GROUP="rg-platform-copilot"

az keyvault create \
  --name $KEY_VAULT \
  --resource-group $RESOURCE_GROUP \
  --location eastus2 \
  --enable-rbac-authorization true
```

### 2. Store Secrets

```bash
# Azure OpenAI API Key
az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name AzureOpenAI--ApiKey \
  --value "your-openai-api-key"

# Azure AD Client Secret (if using client credentials)
az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name AzureAD--ClientSecret \
  --value "your-client-secret"

# Storage connection string
STORAGE_CONNECTION=$(az storage account show-connection-string \
  --name peccompliancestorage \
  --resource-group $RESOURCE_GROUP \
  --query connectionString -o tsv)

az keyvault secret set \
  --vault-name $KEY_VAULT \
  --name BlobStorage--ConnectionString \
  --value "$STORAGE_CONNECTION"
```

### 3. Enable Managed Identity

**For Azure App Service:**

```bash
APP_SERVICE="pec-web-app"

# Enable system-assigned managed identity
az webapp identity assign \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP

# Get managed identity object ID
MI_OBJECT_ID=$(az webapp identity show \
  --name $APP_SERVICE \
  --resource-group $RESOURCE_GROUP \
  --query principalId -o tsv)
```

**For local development:**

Use your own user account:

```bash
# Get your user object ID
USER_ID=$(az ad signed-in-user show --query id -o tsv)
```

### 4. Grant Key Vault Access

```bash
# Assign Key Vault Secrets User role to managed identity
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $MI_OBJECT_ID \
  --scope /subscriptions/<subscription-id>/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.KeyVault/vaults/$KEY_VAULT

# For local development, assign to your user
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $USER_ID \
  --scope /subscriptions/<subscription-id>/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.KeyVault/vaults/$KEY_VAULT
```

### 5. Update Configuration to Use Key Vault

**Program.cs:**

```csharp
// Add Key Vault configuration
builder.Configuration.AddAzureKeyVault(
    new Uri($"https://{builder.Configuration["KeyVaultName"]}.vault.azure.net/"),
    new DefaultAzureCredential());
```

**appsettings.json:**

```json
{
  "KeyVaultName": "pec-compliance-kv",
  "AzureOpenAI": {
    "Endpoint": "https://your-openai.openai.azure.com",
    "ApiKey": ""  // Will be loaded from Key Vault
  }
}
```

**See:** [KEY-VAULT-MIGRATION.md](KEY-VAULT-MIGRATION.md) for complete migration guide.

---

## Storage Account Configuration

### 1. Create Blob Container

```bash
STORAGE_ACCOUNT="peccompliancestorage"

az storage container create \
  --name compliance-documents \
  --account-name $STORAGE_ACCOUNT \
  --auth-mode login
```

### 2. Configure RBAC

```bash
# Grant Storage Blob Data Contributor to managed identity
az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee $MI_OBJECT_ID \
  --scope /subscriptions/<subscription-id>/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT

# For local development
az role assignment create \
  --role "Storage Blob Data Contributor" \
  --assignee $USER_ID \
  --scope /subscriptions/<subscription-id>/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.Storage/storageAccounts/$STORAGE_ACCOUNT
```

### 3. Enable Versioning and Soft Delete

```bash
# Enable blob versioning
az storage account blob-service-properties update \
  --account-name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --enable-versioning true

# Enable soft delete (retain for 30 days)
az storage account blob-service-properties update \
  --account-name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --enable-delete-retention true \
  --delete-retention-days 30
```

### 4. Configure Lifecycle Management

Create `lifecycle-policy.json`:

```json
{
  "rules": [
    {
      "enabled": true,
      "name": "archiveOldDocuments",
      "type": "Lifecycle",
      "definition": {
        "filters": {
          "blobTypes": ["blockBlob"],
          "prefixMatch": ["ato-packages/"]
        },
        "actions": {
          "baseBlob": {
            "tierToArchive": {
              "daysAfterModificationGreaterThan": 365
            }
          }
        }
      }
    }
  ]
}
```

Apply policy:

```bash
az storage account management-policy create \
  --account-name $STORAGE_ACCOUNT \
  --resource-group $RESOURCE_GROUP \
  --policy @lifecycle-policy.json
```

---

## Framework Baseline Selection

### Available NIST 800-53 Baselines

| Baseline | Controls | Use Case |
|----------|----------|----------|
| **Low** | 125 controls | Development environments, low-risk systems |
| **Moderate** | 325 controls | Production systems with CUI |
| **High** | 421 controls | National security systems, classified data |

### Configure Default Baseline

**appsettings.json:**

```json
{
  "AgentConfiguration": {
    "ComplianceAgent": {
      "DefaultFramework": "NIST80053R5",
      "DefaultBaseline": "High"  // Options: Low, Moderate, High
    }
  }
}
```

### Override Per Assessment

**Via Chat:**

```
Run NIST 800-53 Moderate baseline assessment
```

**Via API:**

```csharp
await compliancePlugin.RunComplianceAssessmentAsync(
    subscriptionId: "abc-123",
    baseline: "Moderate"
);
```

**See:** [FRAMEWORK-BASELINES.md](FRAMEWORK-BASELINES.md) for complete baseline documentation.

---

## Continuous Monitoring

### Enable Scheduled Assessments

**appsettings.json:**

```json
{
  "AgentConfiguration": {
    "ComplianceAgent": {
      "EnableContinuousMonitoring": true,
      "MonitoringSchedule": "0 0 * * 0",  // Cron: Every Sunday at midnight
      "AlertOnNewFindings": true,
      "AlertSeverityThreshold": "High"
    }
  }
}
```

### Configure Alerts

**Email Notifications:**

```json
{
  "Notifications": {
    "Email": {
      "Enabled": true,
      "SmtpServer": "smtp.office365.com",
      "SmtpPort": 587,
      "From": "compliance@yourcompany.com",
      "To": ["security-team@yourcompany.com"],
      "AlertOnCritical": true,
      "AlertOnHigh": true
    }
  }
}
```

**Teams Webhook:**

```json
{
  "Notifications": {
    "Teams": {
      "Enabled": true,
      "WebhookUrl": "https://outlook.office.com/webhook/..."
    }
  }
}
```

---

## Troubleshooting

### Issue: Authentication Fails

**Symptoms:**
- "Unauthorized" errors
- Token validation failures

**Diagnosis:**

```bash
# Test Azure AD authentication
az login

# Get access token
az account get-access-token --resource "api://<your-app-client-id>"

# Verify token claims
# Paste token at https://jwt.ms
```

**Solutions:**

1. Verify `TenantId` and `ClientId` in `appsettings.json`
2. Check app role assignments in Azure AD
3. Ensure user has logged out and back in after role assignment
4. Verify token includes `roles` claim

---

### Issue: Key Vault Access Denied

**Symptoms:**
- "Forbidden" errors when accessing Key Vault
- Secrets not loading

**Diagnosis:**

```bash
# Test Key Vault access
az keyvault secret show \
  --vault-name pec-compliance-kv \
  --name AzureOpenAI--ApiKey
```

**Solutions:**

1. Verify managed identity is enabled
2. Check RBAC role assignment:
   ```bash
   az role assignment list \
     --assignee $MI_OBJECT_ID \
     --scope /subscriptions/<sub-id>/resourceGroups/<rg>/providers/Microsoft.KeyVault/vaults/<kv-name>
   ```
3. Ensure `Key Vault Secrets User` role is assigned
4. For local dev, use `DefaultAzureCredential` with your user account

---

### Issue: Storage Access Errors

**Symptoms:**
- Cannot upload documents
- Blob not found errors

**Diagnosis:**

```bash
# Test storage access
az storage blob list \
  --container-name compliance-documents \
  --account-name peccompliancestorage \
  --auth-mode login
```

**Solutions:**

1. Verify container exists:
   ```bash
   az storage container show \
     --name compliance-documents \
     --account-name peccompliancestorage \
     --auth-mode login
   ```
2. Check RBAC permissions: Need `Storage Blob Data Contributor`
3. Verify storage account firewall allows access
4. Ensure connection string is correct in configuration

---

### Issue: Assessment Times Out

**Symptoms:**
- Assessment runs longer than 15 minutes
- No results returned

**Diagnosis:**

```bash
# Check resource count
az resource list --subscription <sub-id> --query "length(@)"
```

**Solutions:**

1. Scope assessment to resource group:
   ```
   Run compliance assessment for resource group production-rg
   ```
2. Check Azure API throttling limits
3. Increase timeout in configuration:
   ```json
   {
     "ComplianceAgent": {
       "AssessmentTimeoutMinutes": 30
     }
   }
   ```

---

## Next Steps

### Production Deployment

1. ✅ [Enable automated remediation](AUTOMATED-REMEDIATION.md)
2. ✅ [Configure Defender integration](DEFENDER-INTEGRATION.md)
3. ✅ [Set up CI/CD scanning](REPOSITORY-SCANNING.md)
4. ✅ [Enable versioning](VERSIONING-COLLABORATION.md)

### Team Onboarding

1. ✅ [Assign RBAC roles](RBAC-AUTHORIZATION.md)
2. ✅ [Train on quick start](QUICK-START.md)
3. ✅ [Review enhancement roadmap](ENHANCEMENT-ROADMAP.md)

---

## Additional Resources

- **Azure AD App Roles**: https://learn.microsoft.com/en-us/azure/active-directory/develop/howto-add-app-roles-in-azure-ad-apps
- **Key Vault RBAC**: https://learn.microsoft.com/en-us/azure/key-vault/general/rbac-guide
- **Storage RBAC**: https://learn.microsoft.com/en-us/azure/storage/blobs/assign-azure-role-data-access
- **NIST 800-53**: https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final

---

*Last Updated: November 25, 2025*  
*Version: 1.0*
