# MCP Integration Guide

This guide covers integrating Microsoft's official MCP servers (Azure and GitHub) into the Platform Engineering Copilot.

## Overview

The platform now supports:
- **Azure MCP Server** - Native Azure resource management via Model Context Protocol (subprocess)
- **GitHub Copilot MCP API** - Repository operations, PR management via GitHub's official MCP API endpoint

## Prerequisites

### 1. Install Azure MCP Server

```bash
# Install Azure MCP Server (for subprocess mode)
npm install -g @azure/mcp-server-azure
```

### 2. Set Up Authentication

**Azure MCP:**
- Uses your existing Azure CLI authentication
- Run `az login` to authenticate

**GitHub Copilot MCP:**
```bash
# Create a GitHub Personal Access Token at:
# https://github.com/settings/tokens/new
# Required scopes: repo, read:user, workflow

# Set environment variable
export GITHUB_PERSONAL_TOKEN="your-token-here"

# Or add to your ~/.zshrc (macOS) or ~/.bashrc (Linux)
echo 'export GITHUB_PERSONAL_TOKEN="your-token-here"' >> ~/.zshrc
source ~/.zshrc
```

## Configuration

The MCP servers are configured in `src/Platform.Engineering.Copilot.Mcp/appsettings.json`:

```json
{
  "AzureMcp": {
    "Enabled": true,
    "ExecutablePath": "npx",
    "Package": "@azure/mcp-server-azure"
  },
  "GitHubMcp": {
    "Enabled": true,
    "ApiUrl": "https://api.githubcopilot.com/mcp",
    "PersonalAccessToken": "${GITHUB_PERSONAL_TOKEN}"
  }
}
```

**Note:** GitHub MCP uses the official GitHub Copilot MCP API endpoint with Bearer token authentication, not a subprocess.

## Usage Examples

### Azure MCP Operations

```csharp
// In your service/plugin
private readonly McpIntegrationManager _mcpManager;

// List Azure resources
var resources = await _mcpManager.Azure_ListResourcesAsync(subscriptionId);

// Get specific resource
var resource = await _mcpManager.Azure_GetResourceAsync(resourceId);

// Query Azure Resource Graph
var query = "Resources | where type == 'microsoft.compute/virtualmachines' | project name, location";
var results = await _mcpManager.Azure_QueryResourceGraphAsync(query, new[] { subscriptionId });

// Execute Azure CLI command
var result = await _mcpManager.Azure_ExecuteCliAsync("az vm list -g myResourceGroup");
```

### GitHub MCP Operations

```csharp
// Repository operations
var repo = await _mcpManager.GitHub_GetRepositoryAsync("owner", "repo-name");
var repos = await _mcpManager.GitHub_ListRepositoriesAsync("owner");

// File operations
var content = await _mcpManager.GitHub_GetFileContentsAsync("owner", "repo", "path/to/file.cs");
await _mcpManager.GitHub_CreateOrUpdateFileAsync(
    "owner", 
    "repo", 
    "path/to/file.cs",
    "new content",
    "Update file via MCP"
);

// Pull Requests
await _mcpManager.GitHub_CreatePullRequestAsync(
    "owner",
    "repo",
    "Add new feature",
    "feature-branch",
    "main",
    "This PR adds a new feature"
);

// Issues
await _mcpManager.GitHub_CreateIssueAsync(
    "owner",
    "repo",
    "Bug: Something is broken",
    "Description of the issue",
    new[] { "bug", "high-priority" }
);

// Workflows
var runs = await _mcpManager.GitHub_ListWorkflowRunsAsync("owner", "repo");
```

## Integration Points

### CompliancePlugin Enhancement

Use Azure MCP to enhance compliance scanning:

```csharp
public class CompliancePlugin
{
    private readonly McpIntegrationManager _mcpManager;
    
    public async Task<string> ScanComplianceAsync(string subscriptionId)
    {
        // Use Azure MCP for resource discovery
        var resources = await _mcpManager.Azure_ListResourcesAsync(subscriptionId);
        
        // Parse and process for compliance checks
        // ... existing compliance logic
    }
}
```

### InfrastructurePlugin Enhancement

Use both Azure and GitHub MCP for infrastructure management:

```csharp
public class InfrastructurePlugin
{
    private readonly McpIntegrationManager _mcpManager;
    
    public async Task DeployInfrastructureAsync(string repo, string branch)
    {
        // Get Terraform/Bicep files from GitHub
        var iacCode = await _mcpManager.GitHub_GetFileContentsAsync(
            "owner", repo, "infra/main.bicep", branch
        );
        
        // Deploy to Azure
        var deployment = await _mcpManager.Azure_ExecuteCliAsync(
            $"az deployment group create --template-file main.bicep ..."
        );
        
        // Create PR with deployment results
        await _mcpManager.GitHub_CreatePullRequestAsync(...);
    }
}
```

## Benefits

### Azure MCP
✅ **Native Azure integration** - Use Microsoft's official MCP server  
✅ **Reduce Azure SDK dependencies** - Less custom code to maintain  
✅ **Resource Graph queries** - Powerful Azure resource discovery  
✅ **CLI parity** - Execute any Azure CLI command  

### GitHub MCP
✅ **Official GitHub Copilot API** - Direct integration with GitHub Copilot MCP endpoint  
✅ **Complete GitHub API coverage** - Repos, PRs, Issues, Workflows  
✅ **No subprocess overhead** - Direct HTTP API calls with Bearer token auth  
✅ **Automated workflows** - Trigger GitHub Actions from platform  
✅ **Code search** - Find code across repositories  
✅ **Collaboration automation** - Auto-create PRs and issues  

## Troubleshooting

### Azure MCP Issues

**Problem:** `az: command not found`  
**Solution:** Install Azure CLI: `brew install azure-cli` (macOS)

**Problem:** Authentication errors  
**Solution:** Run `az login` and verify with `az account show`

### GitHub MCP Issues

**Problem:** `GITHUB_PERSONAL_TOKEN not found`  
**Solution:** Set environment variable before starting the platform

**Problem:** `401 Unauthorized` errors  
**Solution:** Verify your GitHub token is valid and has required scopes (repo, workflow)

**Problem:** `403 Forbidden` errors  
**Solution:** Token may lack permissions for the requested resource. Check repository access.

### General MCP Issues

**Problem:** Azure MCP server won't start  
**Solution:** Verify npm package is installed globally:
```bash
npm list -g @azure/mcp-server-azure
```

**Problem:** npx not found  
**Solution:** Install Node.js/npm: `brew install node` (macOS)

## Architecture

```
Platform Engineering Copilot
    │
    ├── Platform.API (Port 7001)
    │   └── Plugins (Compliance, Infrastructure, etc.)
    │
    └── Platform.Mcp (Stdio MCP Server)
        ├── PlatformTools (Existing MCP tools)
        ├── McpIntegrationManager
        │   ├── AzureMcpTool → npx @azure/mcp-server-azure
        │   └── GitHubMcpTool → npx @modelcontextprotocol/server-github
        └── MCP Clients (GitHub Copilot, Claude Desktop, etc.)
```

## Next Steps

1. **Test Azure MCP:**
   ```bash
   cd src/Platform.Engineering.Copilot.Mcp
   dotnet run
   ```

2. **Test GitHub MCP API connection** by calling a GitHub operation

3. **Create example plugin** that uses both Azure and GitHub MCP

4. **Add MCP operation logging** for debugging

## References

- [Azure MCP Server](https://github.com/Azure/azure-mcp-server)
- [GitHub Copilot MCP API](https://api.githubcopilot.com/mcp)
- [Model Context Protocol Spec](https://spec.modelcontextprotocol.io/)
