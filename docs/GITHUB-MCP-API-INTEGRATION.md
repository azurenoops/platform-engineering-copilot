# GitHub Copilot MCP API Integration

## Overview

The GitHub MCP integration has been updated to use GitHub's official MCP API endpoint instead of the subprocess-based approach.

## API Endpoint

**URL:** `https://api.githubcopilot.com/mcp`  
**Authentication:** `Authorization: Bearer YOUR_GITHUB_PAT`  
**Protocol:** JSONRPC 2.0 over HTTP POST

## Changes Made

### 1. Configuration Update

**File:** `src/Platform.Engineering.Copilot.Mcp/appsettings.json`

**Before:**
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

**After:**
```json
{
  "GitHubMcp": {
    "Enabled": true,
    "ApiUrl": "https://api.githubcopilot.com/mcp",
    "PersonalAccessToken": "${GITHUB_PERSONAL_TOKEN}"
  }
}
```

### 2. GitHubMcpTool Refactoring

**File:** `src/Platform.Engineering.Copilot.Mcp/Tools/GitHubMcpTool.cs`

**Changes:**
- ❌ Removed subprocess-based communication (`Process`, stdin/stdout)
- ✅ Added HTTP client for direct API calls
- ✅ Added Bearer token authentication header
- ✅ Changed from JSONRPC over stdin/stdout to JSONRPC over HTTP POST
- ✅ Simplified initialization (no subprocess management)
- ✅ Cleaner error handling with HTTP status codes

**Implementation:**
```csharp
public class GitHubMcpTool
{
    private readonly HttpClient _httpClient;
    private readonly string _apiUrl = "https://api.githubcopilot.com/mcp";
    
    public GitHubMcpTool(ILogger<GitHubMcpTool> logger, string? apiUrl = null, string? githubToken = null)
    {
        _httpClient = new HttpClient();
        if (!string.IsNullOrEmpty(githubToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", githubToken);
        }
    }
    
    private async Task<string> SendRequestAsync(string method, object? parameters = null)
    {
        var request = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method,
            @params = parameters
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request), 
            Encoding.UTF8, 
            "application/json"
        );
        
        var response = await _httpClient.PostAsync(_apiUrl, content);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadAsStringAsync();
    }
}
```

### 3. McpIntegrationManager Update

**File:** `src/Platform.Engineering.Copilot.Mcp/Tools/McpIntegrationManager.cs`

**Changes:**
- Updated to read `ApiUrl` instead of `ExecutablePath` for GitHub MCP
- Updated disposal to call `Dispose()` instead of `StopGitHubMcpServer()`

## Benefits

### Performance
✅ **No subprocess overhead** - Direct HTTP calls instead of spawning processes  
✅ **Faster startup** - No npm package download or process initialization  
✅ **Lower resource usage** - No separate Node.js process running  

### Reliability
✅ **Better error handling** - HTTP status codes provide clear error information  
✅ **No process management** - No zombie processes or cleanup issues  
✅ **Simpler debugging** - Standard HTTP traffic can be inspected  

### Maintainability
✅ **Fewer dependencies** - No need for npm package installation  
✅ **Cleaner code** - Standard HttpClient instead of Process management  
✅ **Official API** - Direct integration with GitHub's hosted service  

## Setup

### 1. Create GitHub Personal Access Token

1. Go to https://github.com/settings/tokens/new
2. Generate new token with scopes:
   - `repo` - Full control of private repositories
   - `workflow` - Update GitHub Action workflows
   - `read:user` - Read user profile data

### 2. Set Environment Variable

```bash
export GITHUB_PERSONAL_TOKEN="ghp_your_token_here"

# Or add to ~/.zshrc for persistence
echo 'export GITHUB_PERSONAL_TOKEN="ghp_your_token_here"' >> ~/.zshrc
source ~/.zshrc
```

### 3. Verify Configuration

```bash
# Check appsettings.json
cat src/Platform.Engineering.Copilot.Mcp/appsettings.json

# Should show:
# "GitHubMcp": {
#   "Enabled": true,
#   "ApiUrl": "https://api.githubcopilot.com/mcp",
#   "PersonalAccessToken": "${GITHUB_PERSONAL_TOKEN}"
# }
```

## Testing

### Manual Test

```bash
cd src/Platform.Engineering.Copilot.Mcp
dotnet run
```

### Expected Behavior

1. On startup, you should see:
   ```
   [Information] GitHub MCP integration initialized
   [Information] Initializing GitHub MCP API connection to https://api.githubcopilot.com/mcp...
   [Information] GitHub MCP API connection established successfully
   ```

2. If token is missing:
   ```
   [Error] GitHub Personal Access Token is required. Set GITHUB_PERSONAL_TOKEN environment variable.
   ```

3. If token is invalid:
   ```
   [Error] Failed to initialize GitHub MCP API connection
   ```

## Available Operations

All GitHub operations remain the same. The tool provides:

### Repository Operations
- Create repository
- Get repository details
- List repositories
- Fork repository

### File Operations
- Get file contents
- Create/update files
- Push multiple files
- Search code

### Pull Request Operations
- Create pull request
- List pull requests
- Get pull request details
- Merge pull request

### Issue Operations
- Create issue
- List issues
- Update issue
- Add issue comment

### Workflow Operations
- List workflow runs
- Get workflow run details
- Trigger workflow

### Search Operations
- Search repositories
- Search issues
- Search code

## Migration Notes

### No Action Required For:
- Existing code calling GitHub MCP operations
- Plugin integrations using `McpIntegrationManager`
- Method signatures and return types

### What Changed:
- Configuration format (ApiUrl instead of ExecutablePath/Package)
- No need to install npm package `@modelcontextprotocol/server-github`
- Authentication happens via HTTP header instead of environment variable in subprocess

## Troubleshooting

### Problem: 401 Unauthorized
**Solution:** Check that `GITHUB_PERSONAL_TOKEN` is set and valid:
```bash
echo $GITHUB_PERSONAL_TOKEN
curl -H "Authorization: Bearer $GITHUB_PERSONAL_TOKEN" https://api.github.com/user
```

### Problem: 403 Forbidden
**Solution:** Token may lack required scopes. Regenerate with `repo` and `workflow` scopes.

### Problem: Connection refused
**Solution:** Check internet connectivity and firewall rules. The endpoint requires HTTPS access to `api.githubcopilot.com`.

## Architecture Diagram

```
┌─────────────────────────────────────────┐
│   Platform.Engineering.Copilot.Mcp      │
│                                         │
│  ┌────────────────────────────────┐    │
│  │   McpIntegrationManager        │    │
│  │                                │    │
│  │  ┌──────────────────────────┐ │    │
│  │  │   GitHubMcpTool          │ │    │
│  │  │   - HttpClient           │ │    │
│  │  │   - Bearer Token Auth    │ │    │
│  │  └──────────┬───────────────┘ │    │
│  └─────────────┼─────────────────┘    │
└────────────────┼──────────────────────┘
                 │
                 │ HTTPS POST
                 │ (JSONRPC 2.0)
                 │
                 ▼
┌─────────────────────────────────────────┐
│  https://api.githubcopilot.com/mcp      │
│  GitHub Copilot MCP API Endpoint        │
└─────────────────────────────────────────┘
```

## Comparison: Old vs New

| Aspect | Old (Subprocess) | New (HTTP API) |
|--------|-----------------|----------------|
| **Communication** | stdin/stdout | HTTP POST |
| **Dependencies** | npm package required | None |
| **Process Management** | Complex (spawn, kill, cleanup) | Simple (HttpClient) |
| **Startup Time** | Slow (npm download + spawn) | Fast (direct HTTP) |
| **Resource Usage** | High (Node.js process) | Low (HTTP client) |
| **Error Handling** | Process errors + JSONRPC | HTTP status + JSONRPC |
| **Debugging** | Complex (process logs) | Simple (HTTP traffic) |
| **Authentication** | Environment variable in subprocess | Bearer token in header |

## Next Steps

1. ✅ Configuration updated
2. ✅ Code refactored to use HTTP API
3. ✅ Documentation updated
4. ⏳ Test GitHub MCP operations
5. ⏳ Integrate into CompliancePlugin for auto-remediation PRs
6. ⏳ Add workflow triggers for CI/CD automation
