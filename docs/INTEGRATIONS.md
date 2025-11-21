# Platform Engineering Copilot - Integrations Guide

> **Quick Start:** [GitHub Copilot](#github-copilot-integration) | [Microsoft 365](#microsoft-365-copilot-integration) | [GitHub MCP API](#github-mcp-api-integration)

---

## Table of Contents

1. [GitHub Copilot Integration](#github-copilot-integration)
2. [Microsoft 365 Copilot Integration](#microsoft-365-copilot-integration)
3. [GitHub MCP API Integration](#github-mcp-api-integration)
4. [Comparison Matrix](#integration-comparison-matrix)

---

## GitHub Copilot Integration

### Overview

Integrate the Platform Engineering Copilot with GitHub Copilot, enabling developers to manage Azure infrastructure directly from their IDE (VS Code, Visual Studio, JetBrains) using natural language comments and inline suggestions.

### Architecture

```
GitHub Copilot (IDE)
    ↓
GitHub Copilot Extension API
    ↓
Platform Engineering Copilot Agent
    ↓
Platform MCP Server (:5100 HTTP + stdio)
    ↓
Azure Resources
```

### Prerequisites

1. **GitHub Copilot License** (Individual, Business, or Enterprise)
2. **VS Code** with GitHub Copilot extensions installed
3. **Node.js 18+** and npm
4. **Platform MCP Server** running (http://localhost:5100 or deployed)
5. **GitHub App** (for authentication)

### Quick Start

**1. Install GitHub Copilot in VS Code**
```bash
code --install-extension GitHub.copilot
code --install-extension GitHub.copilot-chat
```

**2. Install Platform Engineering Copilot Extension**
```bash
cd extensions/platform-engineering-copilot-github
npm install
npm run compile
npm run package
code --install-extension platform-engineering-copilot-github-0.0.1.vsix
```

**3. Configure the Extension**

Edit VS Code settings (`Cmd+,` or `Ctrl+,`):
```json
{
  "platform-copilot.serverUrl": "http://localhost:5100",
  "platform-copilot.enableWorkspaceCreation": true,
  "platform-copilot.autoDetectTemplates": true
}
```

**4. Test the Integration**

Open GitHub Copilot Chat (`Cmd+Shift+I`) and try:
```
@platform Show me all resource groups in my subscription
@platform Create a Bicep template for Azure Storage with encryption
@platform Generate Terraform for an AKS cluster
```

### Features

✅ **Natural Language Infrastructure Management**
- Query Azure resources with plain English
- Generate IaC templates (Bicep, Terraform, ARM, K8s)
- Get compliance guidance (RMF, STIG, NIST)
- Review architecture for security issues

✅ **Workspace Creation**
- One-click template workspace creation
- Auto-organized folder structures
- README files with deployment instructions
- No copy-pasting needed

✅ **Multi-Agent Orchestration**
- Discovery Agent - Azure resource queries
- Infrastructure Agent - Template generation
- Compliance Agent - Security compliance
- Cost Agent - Cost analysis
- Document Agent - Architecture documentation

✅ **Phase 1 Compliance**
- Advisory-only mode (no auto-deployment)
- Manual review workflow
- Clear deployment instructions
- IL5/IL6 ready

### Extension Structure

```
platform-engineering-copilot-github/
├── src/
│   ├── extension.ts         # Extension entry point
│   ├── chatParticipant.ts   # Chat integration
│   ├── commands/            # VS Code commands
│   ├── services/
│   │   ├── apiService.ts    # API client
│   │   └── workspaceService.ts  # Template workspace creation
│   └── types/               # TypeScript types
├── package.json             # Extension manifest
└── README.md
```

### Configuration Options

| Setting | Description | Default |
|---------|-------------|---------|
| `platform-copilot.serverUrl` | MCP Server URL | `http://localhost:5100` |
| `platform-copilot.enableWorkspaceCreation` | Enable template workspace creation | `true` |
| `platform-copilot.autoDetectTemplates` | Auto-detect templates in responses | `true` |
| `platform-copilot.participantId` | Chat participant ID | `platform` |

### Usage Examples

**Infrastructure Queries:**
```
@platform What AKS clusters are deployed in my subscription?
@platform Show me all storage accounts in resource group rg-prod
@platform List virtual machines with public IP addresses
```

**Template Generation:**
```
@platform Create a Bicep template for an Azure Storage Account with encryption
@platform Generate Terraform for an AKS cluster with monitoring
@platform Create Kubernetes manifests for a web application
```

**Compliance Queries:**
```
@platform Explain RMF Step 4
@platform What STIGs apply to NIST control AC-2?
@platform Show me the Navy ATO process
```

**Architecture Documentation:**
```
@platform Generate C4 container diagram for resource group rg-prod
@platform Create a sequence diagram for the PR review workflow
```

### Troubleshooting

**Extension not appearing in chat:**
- Verify extension is installed: `code --list-extensions | grep platform`
- Restart VS Code
- Check Output panel for errors

**Server connection failed:**
- Verify MCP Server is running: `curl http://localhost:5100/health`
- Check `platform-copilot.serverUrl` setting
- Review server logs

**Templates not saving:**
- Ensure workspace folder is open
- Check file permissions
- Review workspaceService.ts logs

---

## Microsoft 365 Copilot Integration

### Overview

Integrate the Platform Engineering Copilot with Microsoft 365 Copilot, enabling users to manage Azure infrastructure directly from Teams, Outlook, or other M365 apps.

### Integration Options

#### Option 1: Declarative Agent with API Plugin (Recommended)

**Best For:** Teams-based infrastructure management  
**Complexity:** Medium  
**Features:** Full natural language integration, authentication, rich responses

**Architecture:**
```
Microsoft Teams Chat
    ↓
M365 Copilot
    ↓
Declarative Agent (manifest.json)
    ↓
API Plugin (OpenAPI spec)
    ↓
Platform MCP Server (:5100)
    ↓
Azure Resources
```

#### Option 2: Microsoft Graph Connector

**Best For:** Making infrastructure data searchable in M365  
**Complexity:** Low  
**Features:** Search integration, knowledge mining

#### Option 3: Custom Copilot via Bot Framework

**Best For:** Standalone bot experience  
**Complexity:** High  
**Features:** Full control, custom UI

### Quick Start (Declarative Agent)

**1. Create Declarative Agent Manifest**

File: `manifest.json`
```json
{
  "$schema": "https://developer.microsoft.com/json-schemas/copilot/declarative-agent/v1.0/schema.json",
  "version": "1.0",
  "id": "platform-engineering-copilot",
  "name": "Platform Engineering Copilot",
  "description": "AI assistant for Azure infrastructure management",
  "instructions": "You are an expert Azure platform engineer...",
  "capabilities": {
    "actions": [{
      "id": "azureInfrastructure",
      "file": "openapi.json"
    }]
  }
}
```

**2. Create OpenAPI Specification**

File: `openapi.json`
```json
{
  "openapi": "3.0.0",
  "info": {
    "title": "Platform Engineering Copilot API",
    "version": "0.7.1"
  },
  "servers": [{
    "url": "https://your-mcp-server.azurewebsites.net"
  }],
  "paths": {
    "/query": {
      "post": {
        "operationId": "queryInfrastructure",
        "summary": "Query Azure infrastructure",
        "requestBody": {
          "required": true,
          "content": {
            "application/json": {
              "schema": {
                "type": "object",
                "properties": {
                  "query": { "type": "string" }
                }
              }
            }
          }
        }
      }
    }
  }
}
```

**3. Deploy to Teams**

```bash
# Package the app
zip -r platform-copilot.zip manifest.json openapi.json color.png outline.png

# Upload to Teams Admin Center
# https://admin.teams.microsoft.com/policies/manage-apps
```

**4. Test in Teams**

Open Teams chat and try:
```
@Platform Engineering Copilot show me all AKS clusters
@Platform Engineering Copilot create a Bicep template for storage
```

### Features

✅ **Teams Integration**
- Natural language queries in Teams
- Rich card responses
- Adaptive Cards for interactive UI
- Workflow automation

✅ **Authentication**
- Azure AD single sign-on
- User-level permissions
- Secure token handling

✅ **Multi-Channel Support**
- Teams chat
- Teams channels
- Outlook (future)
- Word/PowerPoint (future)

### Configuration

**Required Azure AD App Registration:**
1. Navigate to Azure Portal → Azure AD → App Registrations
2. Create new registration
3. Add API permissions:
   - Microsoft Graph (User.Read)
   - Azure Service Management (user_impersonation)
4. Create client secret
5. Configure redirect URI

**Environment Variables:**
```bash
AZURE_CLIENT_ID=your-app-id
AZURE_CLIENT_SECRET=your-client-secret
AZURE_TENANT_ID=your-tenant-id
M365_COPILOT_ENABLED=true
```

### Usage Examples

**In Teams Chat:**
```
@Platform Engineering Copilot show resource groups
@Platform Engineering Copilot deploy AKS cluster to dev environment
@Platform Engineering Copilot check compliance for rg-prod
@Platform Engineering Copilot estimate costs for last month
```

**In Teams Channel:**
```
/platform-copilot status
/platform-copilot deploy --template bicep --file storage.bicep
/platform-copilot compliance-report
```

### Limitations

- Requires M365 Copilot license
- Limited to 10 API calls per request
- 30-second timeout per operation
- Response size limited to 25KB
- Declarative agents in preview

---

## GitHub MCP API Integration

### Overview

The GitHub MCP integration uses GitHub's official MCP API endpoint for direct JSONRPC communication instead of subprocess-based communication.

### Architecture

**Before (Subprocess):**
```
GitHubMcpTool
    ↓
Process.Start("npx @modelcontextprotocol/server-github")
    ↓
stdin/stdout JSONRPC
```

**After (API):**
```
GitHubMcpTool
    ↓
HTTP POST https://api.githubcopilot.com/mcp
    ↓
JSONRPC 2.0 over HTTP
```

### Configuration

**Before:** `appsettings.json`
```json
{
  "GitHubMcp": {
    "Enabled": true,
    "ExecutablePath": "npx",
    "Package": "@modelcontextprotocol/server-github",
    "PersonalAccessToken": "${GITHUB_PERSONAL_TOKEN}"
  }
}
```

**After:** `appsettings.json`
```json
{
  "GitHubMcp": {
    "Enabled": true,
    "ApiUrl": "https://api.githubcopilot.com/mcp",
    "PersonalAccessToken": "${GITHUB_PERSONAL_TOKEN}"
  }
}
```

### Implementation Changes

**File:** `src/Platform.Engineering.Copilot.Mcp/Tools/GitHubMcpTool.cs`

**Changes Made:**
- ❌ Removed subprocess-based communication (`Process`, stdin/stdout)
- ✅ Added HTTP client for direct API calls
- ✅ Added Bearer token authentication header
- ✅ Changed from JSONRPC over stdin/stdout to JSONRPC over HTTP POST

**New Code:**
```csharp
public class GitHubMcpTool
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl;
    private readonly string _personalAccessToken;

    public GitHubMcpTool(IHttpClientFactory httpClientFactory, IConfiguration config)
    {
        _httpClient = httpClientFactory.CreateClient("GitHubMcp");
        _apiUrl = config["GitHubMcp:ApiUrl"];
        _personalAccessToken = config["GitHubMcp:PersonalAccessToken"];
        
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", _personalAccessToken);
    }

    public async Task<string> CallToolAsync(string method, object parameters)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method = method,
            @params = parameters
        };

        var response = await _httpClient.PostAsJsonAsync(_apiUrl, request);
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadAsStringAsync();
        return result;
    }
}
```

### Available Functions

**Repository Operations:**
- `get_file_contents` - Read file from repository
- `search_repositories` - Search GitHub repositories
- `create_or_update_file` - Create/update repository file
- `push_files` - Push multiple files to repository
- `create_repository` - Create new repository
- `get_issue` - Get issue details
- `create_issue` - Create new issue
- `update_issue` - Update existing issue
- `add_issue_comment` - Comment on issue
- `search_code` - Search code in repositories
- `create_pull_request` - Create PR
- `get_pull_request` - Get PR details
- `list_commits` - List repository commits

### Authentication

**Setup GitHub Personal Access Token:**

1. Navigate to GitHub Settings → Developer settings → Personal access tokens
2. Generate new token (classic)
3. Select scopes:
   - `repo` - Full repository access
   - `read:org` - Organization read access
4. Copy token
5. Set environment variable:
   ```bash
   export GITHUB_PERSONAL_TOKEN=ghp_your_token_here
   ```

### Usage Examples

```csharp
// Get file contents
var file = await _githubMcpTool.CallToolAsync(
    "get_file_contents",
    new {
        owner = "azurenoops",
        repo = "platform-engineering-copilot",
        path = "README.md"
    }
);

// Search repositories
var repos = await _githubMcpTool.CallToolAsync(
    "search_repositories",
    new {
        query = "bicep azure",
        sort = "stars"
    }
);

// Create issue
var issue = await _githubMcpTool.CallToolAsync(
    "create_issue",
    new {
        owner = "azurenoops",
        repo = "platform-engineering-copilot",
        title = "Feature request",
        body = "Add support for..."
    }
);
```

### Benefits of API Approach

✅ **Reliability:**
- No subprocess management
- No stdin/stdout parsing issues
- Consistent error handling

✅ **Performance:**
- Faster initialization (no process spawn)
- Connection pooling via HttpClient
- Better timeout handling

✅ **Security:**
- Token in HTTP header (more secure than env vars for subprocess)
- HTTPS encryption
- Standard OAuth flows

✅ **Maintainability:**
- Simpler code (no process management)
- Standard HTTP patterns
- Easier to test and debug

### Troubleshooting

**Authentication Failed:**
```bash
# Verify token is set
echo $GITHUB_PERSONAL_TOKEN

# Test token manually
curl -H "Authorization: Bearer $GITHUB_PERSONAL_TOKEN" \
  https://api.github.com/user
```

**Connection Issues:**
```bash
# Test API endpoint
curl -X POST https://api.githubcopilot.com/mcp \
  -H "Authorization: Bearer $GITHUB_PERSONAL_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":"1","method":"ping"}'
```

---

## Integration Comparison Matrix

| Feature | GitHub Copilot | M365 Copilot | GitHub MCP API |
|---------|----------------|--------------|----------------|
| **Target Users** | Developers | Business users | Automation |
| **Interface** | VS Code | Teams/Outlook | Programmatic |
| **Authentication** | GitHub App | Azure AD | GitHub PAT |
| **Deployment** | VS Code Extension | Teams App | Library |
| **Use Cases** | Local development | Team collaboration | CI/CD, automation |
| **License Required** | GitHub Copilot | M365 Copilot | GitHub account |
| **Complexity** | Medium | Medium | Low |
| **Best For** | IDE-based workflows | Chat-based workflows | API integrations |

### When to Use Each

**Use GitHub Copilot Integration When:**
- Developers need IDE-based infrastructure management
- Template generation from natural language
- Local development and testing
- Workspace creation from chat

**Use M365 Copilot Integration When:**
- Business users need Teams-based access
- Cross-team collaboration required
- Non-developer audience
- Integration with M365 workflows

**Use GitHub MCP API When:**
- Automating GitHub operations
- CI/CD pipeline integration
- Programmatic repository management
- Background jobs and scripts

---

## Getting Help

### Common Issues

**GitHub Copilot:**
- Extension not loading → Restart VS Code, check extensions list
- Server connection failed → Verify MCP Server URL
- Templates not saving → Check workspace folder is open

**M365 Copilot:**
- App not appearing → Verify Teams admin approval
- Authentication failed → Check Azure AD app registration
- API timeout → Reduce operation complexity

**GitHub MCP API:**
- 401 Unauthorized → Verify GitHub PAT is valid
- 404 Not Found → Check repository owner/name
- Rate limited → Implement retry with exponential backoff

### Additional Resources

- **GitHub Copilot:** [VS Code Extension Docs](https://code.visualstudio.com/api)
- **M365 Copilot:** [Microsoft Learn - Declarative Agents](https://learn.microsoft.com/microsoft-365-copilot)
- **GitHub API:** [GitHub REST API Docs](https://docs.github.com/rest)
- **MCP Protocol:** [Model Context Protocol Spec](https://spec.modelcontextprotocol.io)

---

**Document Version:** 2.0  
**Last Updated:** November 2025  
**Status:** ✅ Consolidated and Production Ready

**Supersedes:** GITHUB-COPILOT-INTEGRATION.md, M365-COPILOT-INTEGRATION.md, GITHUB-MCP-API-INTEGRATION.md
