# DevOps Agent Implementation

## Status: **Initial Implementation** (2/20 tools)

This folder contains the DevOps Agent implementation for GitHub and Azure DevOps automation.

## Implemented Tools

### GitHub Tools (2/10)
- ✅ `create_github_repository` - Create repositories with templates and branch protection
- ✅ `list_github_repositories` - List and filter repositories
- ⬜ `update_github_repository` - Update repository settings
- ⬜ `delete_github_repository` - Delete repositories
- ⬜ `create_github_issue` - Create issues
- ⬜ `list_github_issues` - List and filter issues/PRs
- ⬜ `create_github_pull_request` - Create pull requests
- ⬜ `create_github_workflow` - Create GitHub Actions workflows
- ⬜ `list_github_workflow_runs` - List workflow runs
- ⬜ `manage_github_team` - Manage teams and members

### Azure DevOps Tools (0/10)
- ⬜ All ADO tools pending implementation

## Structure

```
DevOps/
├── Agents/
│   └── DevOpsAgent.cs           # Main agent class
├── Configuration/
│   └── DevOpsAgentOptions.cs    # Configuration options
├── Models/
│   └── GitHub/
│       └── RepositoryModels.cs  # GitHub data models
└── Tools/
    └── GitHub/
        ├── CreateGitHubRepositoryTool.cs
        └── ListGitHubRepositoriesTool.cs
```

## Configuration

### appsettings.json
```json
{
  "AgentConfiguration": {
    "DevOpsAgent": {
      "Enabled": true,
      "Temperature": 0.3,
      "MaxTokens": 4000,
      "GitHub": {
        "Enabled": true,
        "DefaultOrg": "your-org",
        "RequireBranchProtection": true,
        "RequireCodeOwners": false
      },
      "AzureDevOps": {
        "Enabled": false,
        "DefaultOrganization": "https://dev.azure.com/your-org",
        "DefaultProject": "Platform"
      }
    }
  }
}
```

### .env
GitHub configuration is already in `.env`:
```bash
GITHUB_TOKEN=ghp_your_token
GITHUB_API_BASE_URL=https://api.github.com
GITHUB_DEFAULT_OWNER=your-org
```

## Usage Examples

```plaintext
"Create a new GitHub repository called 'my-api' with .NET template"
→ Creates repo with README, gitignore, license, branch protection

"List all repositories in my organization"
→ Returns list of repos with metadata

"Create a private repo 'user-service' with topics 'microservice' and 'dotnet'"
→ Creates repo with specified topics
```

## Next Steps

1. ⬜ Register DevOpsAgent in DI container
2. ⬜ Add DevOpsAgent to PlatformAgentGroupChat
3. ⬜ Add remaining GitHub tools (8 more)
4. ⬜ Implement Azure DevOps tools (10 tools)
5. ⬜ Add workflow templates
6. ⬜ Integration testing

See DEVOPS-AGENT-DESIGN.md in docs/ for full specification.
