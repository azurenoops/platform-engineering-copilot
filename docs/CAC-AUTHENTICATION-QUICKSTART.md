# CAC Authentication - Quick Start Guide

## Prerequisites

1. Azure Government subscription
2. Two Azure AD app registrations:
   - Client app (for users)
   - MCP server app (for API)
3. CAC/PIV reader and certificate
4. .NET 9.0 SDK

## Step 1: Configure Azure AD App Registrations

### Client Application

```bash
# Create client app registration
az ad app create \
  --display-name "Platform Engineering Copilot Client" \
  --sign-in-audience AzureADMyOrg

# Note the Application (client) ID
CLIENT_APP_ID="<your-client-app-id>"

# Add API permission for MCP
az ad app permission add \
  --id $CLIENT_APP_ID \
  --api <mcp-app-id> \
  --api-permissions <access_as_user-scope-id>=Scope
```

### MCP Server Application

```bash
# Create MCP app registration
az ad app create \
  --display-name "Platform Engineering Copilot MCP" \
  --sign-in-audience AzureADMyOrg \
  --identifier-uris "api://platform-engineering-copilot"

# Note the Application (client) ID
MCP_APP_ID="<your-mcp-app-id>"

# Create client secret
az ad app credential reset \
  --id $MCP_APP_ID \
  --append

# Note the client secret (save securely!)
MCP_CLIENT_SECRET="<your-secret>"

# Add API permissions
az ad app permission add \
  --id $MCP_APP_ID \
  --api "797f4846-ba00-4fd7-ba43-dac1f8f63013" \
  --api-permissions "41094075-9dad-400e-a0bd-54e686782033=Scope"  # ARM

# Grant admin consent
az ad app permission admin-consent --id $MCP_APP_ID
```

## Step 2: Configure MCP Server

### appsettings.json

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.us/",    
    "Audience": "api://platform-engineering-copilot",
    "RequireMfa": true,
    "RequireCac": true,
    "EnableUserTokenPassthrough": true
  },
  "Gateway": {
    "Azure": {
      "SubscriptionId": "<your-subscription-id>",
      "CloudEnvironment": "AzureGovernment",
      "UseManagedIdentity": false,
      "EnableUserTokenPassthrough": true
    }
  }
}
```

## Step 3: Build and Run MCP Server

```bash
# Navigate to MCP project
cd src/Platform.Engineering.Copilot.Mcp

# Restore packages
dotnet restore

# Build
dotnet build

# Run in HTTP mode (port 5000)
dotnet run -- --http --port 5000
```

## Step 4: Test Authentication

### Using curl

```bash
# 1. Get token (use your preferred method - MSAL, browser, etc.)
TOKEN="<your-jwt-token>"

# 2. Test MCP endpoint
curl -X POST http://localhost:5000/invoke \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "method": "tools/list",
    "params": {}
  }'

# Expected response:
# {
#   "tools": [
#     {"name": "list_subscriptions", ...},
#     {"name": "list_resource_groups", ...}
#   ]
# }
```

### Using .NET Client

```csharp
using Microsoft.Identity.Client;

var app = PublicClientApplicationBuilder
    .Create("<client-app-id>")
    .WithAuthority(AzureCloudInstance.AzureUsGovernment, "<tenant-id>")
    .Build();

var result = await app
    .AcquireTokenInteractive(new[] { "api://platform-engineering-copilot/access_as_user" })
    .ExecuteAsync();

var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization = 
    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", result.AccessToken);

var response = await httpClient.PostAsJsonAsync(
    "http://localhost:5000/invoke",
    new { method = "tools/list", @params = new { } }
);
```

## Step 5: Verify CAC Authentication

Check MCP logs for successful authentication:

```
[INF] 🔐 Validating user token...
[INF] ✅ JWT validated for user: john.doe@domain.mil
[INF] ✅ CAC/PIV authentication verified
[INF] 🔑 Created On-Behalf-Of credential for user: john.doe@domain.mil
```

## Common Issues

### Token Missing CAC Claim

**Error**: "CAC/PIV authentication required"

**Fix**: Ensure user logged in with CAC. Check `amr` claim:
```bash
# Decode token at jwt.ms
# Verify "amr" contains: ["mfa", "rsa"] or ["smartcard"]
```

### Invalid Audience

**Error**: "The audience 'api://...' is invalid"

**Fix**: 
1. Client scope must be: `api://platform-engineering-copilot/access_as_user`
2. MCP `Audience` setting must match app ID URI

### OBO Flow Fails

**Error**: "AADSTS65001: The user or administrator has not consented"

**Fix**:
```bash
# Grant admin consent for MCP app
az ad app permission admin-consent --id <mcp-app-id>

# Verify permissions in Azure Portal:
# App registrations > MCP app > API permissions
# Should show "Granted for <tenant-name>"
```

## Production Checklist

- [ ] Store client secret in Azure Key Vault
- [ ] Enable HTTPS with valid certificate
- [ ] Configure Application Insights for monitoring
- [ ] Set up Azure AD Conditional Access policies
- [ ] Enable Azure AD sign-in logs
- [ ] Configure network security groups (NSGs)
- [ ] Deploy behind load balancer for HA
- [ ] Set up backup and disaster recovery
- [ ] Document incident response procedures
- [ ] Complete security compliance review

## Next Steps

1. **Configure Client Apps**: Update web/desktop apps to use CAC authentication
2. **Test User Scenarios**: Verify different user roles and permissions
3. **Enable Monitoring**: Set up alerts for authentication failures
4. **Security Review**: Conduct penetration testing and security audit
5. **Documentation**: Create user guides and troubleshooting runbooks

## Resources

- [Full Documentation](./CAC-AUTHENTICATION.md)
- [Azure Government Documentation](https://docs.microsoft.com/azure/azure-government/)
- [MSAL Documentation](https://docs.microsoft.com/azure/active-directory/develop/msal-overview)
- [On-Behalf-Of Flow](https://docs.microsoft.com/azure/active-directory/develop/v2-oauth2-on-behalf-of-flow)
