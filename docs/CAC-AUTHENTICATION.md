# CAC Authentication Configuration Guide

## Overview

The Platform Engineering Copilot MCP server supports Common Access Card (CAC) / Personal Identity Verification (PIV) authentication for Azure Government environments. This guide explains how to configure and use CAC-based authentication with user token passthrough.

## Architecture

```
┌─────────────────────┐
│   Client App        │
│   (Web/Desktop)     │
│                     │
│   1. User logs in   │
│      with CAC       │
│   2. Gets JWT from  │
│      Azure AD       │
└──────────┬──────────┘
           │
           │ Bearer Token
           │ (JWT)
           ▼
┌─────────────────────┐
│   MCP Server        │
│                     │
│   3. Validates JWT  │
│   4. Checks CAC/PIV │
│   5. Creates OBO    │
│      credential     │
└──────────┬──────────┘
           │
           │ On-Behalf-Of
           │ Token
           ▼
┌─────────────────────┐
│   Azure Resources   │
│   (ARM, KeyVault)   │
│                     │
│   6. Operations run │
│      with user ID   │
└─────────────────────┘
```

## Authentication Flow

1. **Client Authentication**: User authenticates with CAC/PIV to Azure AD (login.microsoftonline.us)
2. **Token Acquisition**: Client app obtains JWT token with user's identity
3. **MCP Request**: Client sends request to MCP with `Authorization: Bearer <token>` header
4. **Token Validation**: MCP validates JWT signature and issuer
5. **CAC Verification**: MCP checks `amr` (authentication methods reference) claim for CAC/PIV indicators
6. **User Context**: MCP extracts user identity (UPN, ObjectId, TenantId)
7. **OBO Credential**: MCP creates On-Behalf-Of credential to impersonate user
8. **Azure Operations**: All Azure API calls execute with user's permissions
9. **Audit Trail**: All operations logged with user identity

## Azure AD App Registration Requirements

You need **two** Azure AD app registrations:

### 1. Client Application (Web/Desktop App)

**Purpose**: Authenticates users and acquires tokens

**Configuration**:
- Authentication:
  - Platform: Web or Mobile/Desktop
  - Redirect URI: Your app's callback URL
  - Certificate-based authentication: Required for CAC
- API Permissions:
  - `User.Read` (Microsoft Graph)
  - `<MCP App ID>/access_as_user` (delegated)
- Advanced Settings:
  - Allow public client flows: Yes (for desktop apps)
  - Supported account types: This organizational directory only

### 2. MCP Server Application

**Purpose**: Validates tokens and calls Azure APIs on behalf of users

**Configuration**:
- Authentication:
  - Certificates & secrets: Create client secret for OBO flow
- Expose an API:
  - Application ID URI: `api://platform-engineering-copilot`
  - Scopes:
    - `access_as_user` (Admins and users)
- API Permissions:
  - `https://management.azure.us/user_impersonation` (Azure Service Management)
  - `https://vault.azure.us/user_impersonation` (Azure Key Vault)
  - Grant admin consent for all permissions
- Manifest:
  ```json
  {
    "accessTokenAcceptedVersion": 2,
    "signInAudience": "AzureADMyOrg"
  }
  ```

## Configuration

### appsettings.json

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.us/",
    "TenantId": "your-azure-gov-tenant-id",
    "ClientId": "your-mcp-app-registration-id",
    "ClientSecret": "USE_KEY_VAULT_REFERENCE",
    "Audience": "api://platform-engineering-copilot",
    "RequireMfa": true,
    "RequireCac": true,
    "EnableUserTokenPassthrough": true
  },
  "Gateway": {
    "Azure": {
      "SubscriptionId": "your-subscription-id",
      "CloudEnvironment": "AzureGovernment",
      "UseManagedIdentity": false,
      "EnableUserTokenPassthrough": true
    }
  }
}
```

### Configuration Properties

**AzureAd Section**:
- `Instance`: Azure AD authority (use `.us` for Government cloud)
- `TenantId`: Your Azure AD tenant ID
- `ClientId`: MCP server app registration ID
- `ClientSecret`: Client secret for On-Behalf-Of flow (use Key Vault reference)
- `Audience`: Expected audience claim in JWT (`api://platform-engineering-copilot`)
- `RequireMfa`: Require multi-factor authentication (default: `true`)
- `RequireCac`: Require CAC/PIV authentication (default: `true`)
- `EnableUserTokenPassthrough`: Enable OBO flow (default: `true`)

**Gateway.Azure Section**:
- `CloudEnvironment`: `AzureGovernment` for `.us` endpoints
- `UseManagedIdentity`: `false` when using user tokens
- `EnableUserTokenPassthrough`: Must match AzureAd setting

## CAC/PIV Validation

The MCP server validates CAC authentication by checking the `amr` (authentication methods reference) claim in the JWT token. Valid CAC indicators:

- `mfa`: Multi-factor authentication
- `rsa`: RSA SecurID (common for CAC)
- `smartcard`: Smart card authentication

If none of these are present and `RequireCac` is `true`, authentication fails.

## Client Implementation Examples

### .NET Client (MSAL)

```csharp
using Microsoft.Identity.Client;
using System.Net.Http.Headers;

public class McpClient
{
    private readonly IPublicClientApplication _msalClient;
    private readonly HttpClient _httpClient;

    public McpClient(string clientId, string tenantId)
    {
        _msalClient = PublicClientApplicationBuilder
            .Create(clientId)
            .WithAuthority(AzureCloudInstance.AzureUsGovernment, tenantId)
            .WithRedirectUri("http://localhost")
            .Build();

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:5000")
        };
    }

    public async Task<string> GetTokenAsync()
    {
        var scopes = new[] { "api://platform-engineering-copilot/access_as_user" };

        // Certificate-based authentication for CAC
        var result = await _msalClient
            .AcquireTokenInteractive(scopes)
            .WithUseEmbeddedWebView(false)
            .ExecuteAsync();

        return result.AccessToken;
    }

    public async Task<string> CallMcpAsync(string endpoint, object request)
    {
        var token = await GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.PostAsJsonAsync(endpoint, request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
```

### JavaScript Client (MSAL.js)

```javascript
import * as msal from "@azure/msal-browser";

const msalConfig = {
    auth: {
        clientId: "your-client-app-id",
        authority: "https://login.microsoftonline.us/your-tenant-id",
        redirectUri: window.location.origin,
        cloudDiscoveryMetadata: JSON.stringify({
            "tenant_discovery_endpoint": 
                "https://login.microsoftonline.us/your-tenant-id/.well-known/openid-configuration"
        })
    },
    cache: {
        cacheLocation: "sessionStorage",
        storeAuthStateInCookie: false,
    }
};

const msalInstance = new msal.PublicClientApplication(msalConfig);

async function getMcpToken() {
    const request = {
        scopes: ["api://platform-engineering-copilot/access_as_user"]
    };

    try {
        const response = await msalInstance.acquireTokenSilent(request);
        return response.accessToken;
    } catch (error) {
        // Fallback to interactive login
        const response = await msalInstance.acquireTokenPopup(request);
        return response.accessToken;
    }
}

async function callMcp(endpoint, data) {
    const token = await getMcpToken();
    
    const response = await fetch(`http://localhost:5000${endpoint}`, {
        method: "POST",
        headers: {
            "Authorization": `Bearer ${token}`,
            "Content-Type": "application/json"
        },
        body: JSON.stringify(data)
    });

    return response.json();
}
```

## On-Behalf-Of (OBO) Flow

The MCP server uses the On-Behalf-Of flow to exchange the user's token for an Azure Resource Manager token:

```csharp
// Middleware extracts user token from Authorization header
var userToken = context.Request.Headers["Authorization"]
    .ToString()
    .Replace("Bearer ", "");

// Create OBO credential
var credential = new OnBehalfOfCredential(
    tenantId: azureAdOptions.TenantId,
    clientId: azureAdOptions.ClientId,
    clientSecret: azureAdOptions.ClientSecret,
    userAssertion: userToken,
    options: new OnBehalfOfCredentialOptions
    {
        AuthorityHost = AzureAuthorityHosts.AzureGovernment
    }
);

// Store in HttpContext for agents to use
context.Items["AzureCredential"] = credential;
context.Items["UserPrincipal"] = userPrincipal;
```

## Security Considerations

### 1. Client Secret Protection
- **Never** commit client secrets to source control
- Use Azure Key Vault references in production:
  ```json
  "ClientSecret": "@Microsoft.KeyVault(VaultName=my-vault;SecretName=mcp-client-secret)"
  ```

### 2. Token Validation
- JWT signature verified against Azure AD public keys
- Issuer must be `https://sts.windows.net/{tenantId}/`
- Audience must match MCP app registration

### 3. CAC Enforcement
- `RequireCac: true` ensures only CAC-authenticated users can access MCP
- Validation happens on every request
- No cached credentials without CAC proof

### 4. Least Privilege
- Users execute Azure operations with their own permissions
- No service account credentials exposed
- Role-based access control (RBAC) applies at user level

### 5. Audit Trail
- All operations logged with user UPN
- Azure Activity Log shows individual user actions
- Full compliance with DoD audit requirements

## Troubleshooting

### "CAC/PIV authentication required" Error

**Problem**: Token rejected because CAC wasn't used

**Solution**:
1. Check `amr` claim in JWT token (use jwt.ms):
   ```json
   {
     "amr": ["mfa", "rsa"]  // Must include mfa/rsa/smartcard
   }
   ```
2. Ensure client app uses certificate-based auth
3. Verify conditional access policies require CAC in Azure AD

### "Invalid audience" Error

**Problem**: JWT audience doesn't match MCP configuration

**Solution**:
1. Client must request scope: `api://platform-engineering-copilot/access_as_user`
2. MCP `Audience` setting must match app ID URI in app registration
3. Check `aud` claim in token matches configuration

### "Unauthorized" Error

**Problem**: OBO flow fails to get Azure token

**Solution**:
1. Verify MCP app has API permissions:
   - `https://management.azure.us/user_impersonation`
2. Grant admin consent in Azure AD
3. Check user has appropriate RBAC roles on Azure resources
4. Ensure client secret is valid and not expired

### "Token expired" Error

**Problem**: JWT token lifetime exceeded (typically 1 hour)

**Solution**:
1. Client should refresh token before expiration
2. Implement token caching with auto-refresh in client app
3. Check `exp` claim in JWT for expiration time

## Testing

### 1. Verify JWT Token

Use jwt.ms to decode and inspect your token:

```bash
# Copy token from browser/app
# Paste into https://jwt.ms
# Verify claims:
{
  "aud": "api://platform-engineering-copilot",
  "iss": "https://sts.windows.net/<tenant-id>/",
  "amr": ["mfa", "rsa"],
  "upn": "user@domain.mil",
  "oid": "<user-object-id>",
  "tid": "<tenant-id>"
}
```

### 2. Test Token Acquisition

```bash
# .NET CLI (requires MSAL)
dotnet run -- acquire-token \
  --client-id <client-app-id> \
  --tenant-id <tenant-id> \
  --scope api://platform-engineering-copilot/access_as_user
```

### 3. Test MCP Endpoint

```bash
# Get token (from MSAL or other method)
TOKEN="eyJ0eXAiOiJKV1QiLCJhbGc..."

# Call MCP endpoint
curl -X POST http://localhost:5000/invoke \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "method": "tools/list",
    "params": {}
  }'
```

### 4. Verify User Identity in Logs

Check MCP logs for user identity:

```
[INF] User authenticated: user@domain.mil (ObjectId: abc123...)
[INF] Azure credential created for user: user@domain.mil
[DBG] Executing Azure operation with user credential
```

## Production Deployment

### 1. Key Vault Configuration

Store secrets in Azure Key Vault:

```bash
# Create Key Vault
az keyvault create \
  --name mcp-secrets \
  --resource-group platform-engineering \
  --location usgovvirginia

# Store client secret
az keyvault secret set \
  --vault-name mcp-secrets \
  --name mcp-client-secret \
  --value "<your-secret>"

# Grant MCP managed identity access
az keyvault set-policy \
  --name mcp-secrets \
  --object-id <mcp-managed-identity-id> \
  --secret-permissions get
```

Update appsettings:
```json
{
  "AzureAd": {
    "ClientSecret": "@Microsoft.KeyVault(VaultName=mcp-secrets;SecretName=mcp-client-secret)"
  }
}
```

### 2. Network Security

Configure NSGs and firewalls:

```bash
# Allow only authenticated traffic
az network nsg rule create \
  --resource-group platform-engineering \
  --nsg-name mcp-nsg \
  --name allow-https \
  --priority 100 \
  --source-address-prefixes VirtualNetwork \
  --destination-port-ranges 443 \
  --access Allow \
  --protocol Tcp
```

### 3. Monitoring

Enable Application Insights:

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...;EndpointSuffix=applicationinsights.us"
  }
}
```

### 4. High Availability

Deploy multiple instances with load balancer:

```bash
# Create load balancer
az network lb create \
  --resource-group platform-engineering \
  --name mcp-lb \
  --sku Standard \
  --location usgovvirginia

# Configure health probe
az network lb probe create \
  --resource-group platform-engineering \
  --lb-name mcp-lb \
  --name health-probe \
  --protocol http \
  --port 80 \
  --path /health
```

## Compliance

This CAC authentication implementation satisfies:

- **NIST 800-53**: AC-2 (Account Management), IA-2 (Identification and Authentication)
- **DISA STIG**: User identification requirements
- **FedRAMP**: Multi-factor authentication controls
- **DoD Cloud Computing SRG**: IL5/IL6 access control requirements

All Azure operations execute with user-level permissions and maintain full audit trail for compliance reporting.

## Support

For issues or questions:
- Check MCP logs: `logs/mcp-server.log`
- Review Azure AD sign-in logs in Azure Portal
- Contact Azure Government support for cloud-specific issues
- Refer to Microsoft documentation: https://docs.microsoft.com/azure/government/
