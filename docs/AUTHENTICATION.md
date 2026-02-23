# Authentication Guide

**Version:** 3.1  
**Last Updated:** January 2026

---

## Overview

The Platform Engineering Copilot supports three authentication modes:

| Mode | Use Case | Configuration |
|------|----------|---------------|
| **Azure CLI** | Local development | `UseManagedIdentity: false` |
| **Managed Identity** | Production (ACI/AKS) | `UseManagedIdentity: true` |
| **CAC/PIV** | Azure Government | `RequireCac: true` |

---

## Quick Start (Development)

```bash
# Azure Government
az cloud set --name AzureUSGovernment
az login
export AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)

# Azure Commercial
az cloud set --name AzureCloud
az login

# Run MCP server
dotnet run --project src/Platform.Engineering.Copilot.Mcp
```

✅ The application automatically uses your Azure CLI credentials.

---

## Managed Identity (Production)

For Azure deployments (ACI, AKS, App Service):

```bash
# Enable Managed Identity
az webapp identity assign \
  --name your-app-name \
  --resource-group your-rg

# Grant permissions
PRINCIPAL_ID=$(az webapp identity show \
  --name your-app-name \
  --resource-group your-rg \
  --query principalId -o tsv)

az role assignment create \
  --assignee $PRINCIPAL_ID \
  --role "Reader" \
  --scope "/subscriptions/your-subscription-id"
```

Configuration:
```json
{
  "Gateway": {
    "Azure": {
      "UseManagedIdentity": true,
      "CloudEnvironment": "AzureGovernment"
    }
  }
}
```

---

## CAC/PIV Authentication (Azure Government)

CAC/PIV enables smart card authentication with On-Behalf-Of (OBO) flow for Azure operations under user identity.

### Architecture

```
┌────────────────┐      ┌────────────────┐      ┌────────────────┐
│ Client + CAC   │      │   MCP Server   │      │    Azure       │
│   Reader       │      │                │      │  Government    │
└───────┬────────┘      └───────┬────────┘      └───────┬────────┘
        │                       │                       │
        │ 1. CAC login (MSAL)   │                       │
        │                       │                       │
        │ 2. Bearer token ─────►│                       │
        │                       │ 3. Validate JWT       │
        │                       │ 4. Check amr=mfa/rsa  │
        │                       │                       │
        │                       │ 5. OBO exchange ─────►│
        │                       │◄───── ARM token ──────│
        │                       │                       │
        │◄─── Response ─────────│ 6. Azure ops as user  │
```

### Step 1: Azure AD App Registrations

**MCP Server App:**
```bash
# Create MCP app
az ad app create \
  --display-name "Platform Engineering Copilot MCP" \
  --sign-in-audience AzureADMyOrg \
  --identifier-uris "api://platform-engineering-copilot"

MCP_APP_ID="<your-mcp-app-id>"

# Create client secret (store in Key Vault!)
az ad app credential reset --id $MCP_APP_ID --append

# Add ARM permission
az ad app permission add \
  --id $MCP_APP_ID \
  --api "797f4846-ba00-4fd7-ba43-dac1f8f63013" \
  --api-permissions "41094075-9dad-400e-a0bd-54e686782033=Scope"

# Grant admin consent
az ad app permission admin-consent --id $MCP_APP_ID
```

**Client App:**
```bash
az ad app create \
  --display-name "Platform Engineering Copilot Client" \
  --sign-in-audience AzureADMyOrg

CLIENT_APP_ID="<your-client-app-id>"

# Add MCP API permission
az ad app permission add \
  --id $CLIENT_APP_ID \
  --api $MCP_APP_ID \
  --api-permissions <access_as_user-scope-id>=Scope
```

### Step 2: Configuration

**appsettings.json:**
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.us/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<mcp-app-id>",
    "ClientSecret": "<store-in-keyvault>",
    "Audience": "api://platform-engineering-copilot",
    "RequireMfa": true,
    "RequireCac": true,
    "EnableUserTokenPassthrough": true
  },
  "Gateway": {
    "Azure": {
      "CloudEnvironment": "AzureGovernment",
      "UseManagedIdentity": false,
      "EnableUserTokenPassthrough": true
    }
  }
}
```

**Docker environment variables:**
```bash
AZURE_AD_TENANT_ID=<your-tenant-id>
AZURE_AD_CLIENT_ID=<mcp-app-id>
AZURE_AD_CLIENT_SECRET=<secret>
AZURE_AD_REQUIRE_CAC=true
AZURE_AD_REQUIRE_MFA=true
AZURE_AD_ENABLE_USER_TOKEN_PASSTHROUGH=true
```

### Step 3: Test CAC Authentication

```bash
# Get token via MSAL with CAC
TOKEN="<jwt-from-cac-login>"

# Test MCP endpoint
curl -X POST http://localhost:5100/invoke \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"method": "tools/list", "params": {}}'
```

Expected logs:
```
[INF] ✅ JWT validated for user: john.doe@domain.mil
[INF] ✅ CAC/PIV authentication verified
[INF] 🔑 Created On-Behalf-Of credential
```

### .NET Client Example

```csharp
var app = PublicClientApplicationBuilder
    .Create("<client-app-id>")
    .WithAuthority(AzureCloudInstance.AzureUsGovernment, "<tenant-id>")
    .Build();

var result = await app
    .AcquireTokenInteractive(new[] { "api://platform-engineering-copilot/access_as_user" })
    .ExecuteAsync();

var client = new HttpClient();
client.DefaultRequestHeaders.Authorization = 
    new AuthenticationHeaderValue("Bearer", result.AccessToken);
```

---

## Configuration Reference

### AzureAd Section

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Instance` | string | `https://login.microsoftonline.us/` | Azure AD instance |
| `TenantId` | string | - | Azure AD tenant ID |
| `ClientId` | string | - | MCP app registration ID |
| `ClientSecret` | string | - | MCP app secret (use Key Vault) |
| `Audience` | string | - | Expected audience in JWT |
| `RequireMfa` | bool | `false` | Require MFA in token |
| `RequireCac` | bool | `false` | Require CAC/PIV auth |
| `EnableUserTokenPassthrough` | bool | `false` | Use OBO for Azure ops |
| `ValidIssuers` | string[] | `[]` | Additional valid issuers |

### Gateway.Azure Section

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CloudEnvironment` | string | `AzureGovernment` | `AzureGovernment` or `AzurePublicCloud` |
| `UseManagedIdentity` | bool | `false` | Use managed identity |
| `EnableUserTokenPassthrough` | bool | `false` | Use user token for Azure |
| `SubscriptionId` | string | - | Default subscription |
| `TenantId` | string | - | Azure tenant ID |

---

## Troubleshooting

### Common Issues

| Error | Solution |
|-------|----------|
| "CAC/PIV authentication required" | Verify user logged in with CAC. Check `amr` claim contains `mfa`, `rsa`, or `smartcard` |
| "The audience is invalid" | Ensure client scope is `api://platform-engineering-copilot/access_as_user` |
| "AADSTS65001: Consent not granted" | Run `az ad app permission admin-consent --id <mcp-app-id>` |
| "No subscriptions found" | Run `az login` and verify permissions |

### Verify Token Claims

Decode your JWT at [jwt.ms](https://jwt.ms) and check:
- `aud`: Should match `Audience` config
- `iss`: Should be in `ValidIssuers`
- `amr`: For CAC, should contain `mfa` and `rsa`

### Debug Logging

Enable debug logging in `appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Platform.Engineering.Copilot.Mcp.Middleware": "Debug"
    }
  }
}
```

---

## Production Checklist

- [ ] Store secrets in Azure Key Vault
- [ ] Enable HTTPS with valid certificate
- [ ] Configure Azure AD Conditional Access
- [ ] Enable Azure AD sign-in logs
- [ ] Set up monitoring alerts
- [ ] Conduct security review

---

## Related Documentation

- [DEPLOYMENT.md](./DEPLOYMENT.md) - Deployment guide
- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [Azure Government Docs](https://docs.microsoft.com/azure/azure-government/)
- [On-Behalf-Of Flow](https://docs.microsoft.com/azure/active-directory/develop/v2-oauth2-on-behalf-of-flow)
