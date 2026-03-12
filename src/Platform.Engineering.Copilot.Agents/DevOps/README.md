# DevOps Agent Implementation

## Status: **Partially Registered - 2 Tools Functional** (2/20 tools)

This folder contains the DevOps Agent implementation for GitHub and Azure DevOps automation.

**Current State:** The DevOps Agent is **registered in DI container** and will appear in orchestration, but only 2 GitHub tools are functional. The remaining 8 GitHub tools are implemented but need refactoring to match the BaseTool pattern before they can be registered.

## Registered & Functional Tools

### GitHub Tools (2/10) ✅
- ✅ `create_github_repository` - Create repositories with templates and branch protection (**WORKING**)
- ✅ `list_github_repositories` - List and filter repositories  (**WORKING**)
- ⚠️ `update_github_repository` - Update repository settings (**NEEDS REFACTOR**)
- ⚠️ `delete_github_repository` - Delete repositories with confirmation (**NEEDS REFACTOR**)
- ⚠️ `create_github_issue` - Create issues with labels and assignees (**NEEDS REFACTOR**)
- ⚠️ `list_github_issues` - List and filter issues (**NEEDS REFACTOR**)
- ⚠️ `create_github_pull_request` - Create pull requests with reviewers (**NEEDS REFACTOR**)
- ⚠️ `list_github_pull_requests` - List and filter pull requests (**NEEDS REFACTOR**)
- ⚠️ `trigger_github_action` - Trigger GitHub Actions workflows (**NEEDS REFACTOR**)
- ⚠️ `list_github_action_runs` - List workflow runs and status (**NEEDS REFACTOR**)
- ⚠️ `add_github_team_member` - Add users to teams with roles (**NEEDS REFACTOR**)
- ⚠️ `list_github_teams` - List organization teams with permissions (**NEEDS REFACTOR**)

###Azure DevOps Tools (0/10)
- ⬜ All ADO tools pending implementation

## Technical Issue

The 8 GitHub tools marked "NEEDS REFACTOR" were initially implemented using `[KernelFunction]` attributes, which is incompatible with the `BaseTool` pattern used by the Platform Engineering Copilot framework. They need to be rewritten to:

1. Override `Name` and `Description` properties
2. Call `base(logger)` in constructor  
3. Define parameters using `Parameters.Add(new ToolParameter())` 
4. Override `ExecuteAsync(IDictionary<string, object?> arguments, CancellationToken)` method
5 Return JSON-serialized results

See [CreateGitHubRepositoryTool.cs](Tools/GitHub/CreateGitHubRepositoryTool.cs) for the correct pattern.

## Structure

```
DevOps/
├── Agents/
│   └── DevOpsAgent.cs           # Main agent class with all 10 GitHub tools
├── Configuration/
│   └── DevOpsAgentOptions.cs    # Configuration options
├── Models/
│   └── GitHub/
│       └── RepositoryModels.cs  # GitHub data models
└── Tools/
    └── GitHub/
        ├── CreateGitHubRepositoryTool.cs
        ├── ListGitHubRepositoriesTool.cs
        ├── UpdateGitHubRepositoryTool.cs
        ├── DeleteGitHubRepositoryTool.cs
        ├── CreateGitHubIssueTool.cs
        ├── ListGitHubIssuesTool.cs
        ├── CreateGitHubPullRequestTool.cs
        ├── ListGitHubPullRequestsTool.cs
        ├── TriggerGitHubActionTool.cs
        ├── ListGitHubActionRunsTool.cs
        ├── AddGitHubTeamMemberTool.cs
        └── ListGitHubTeamsTool.cs
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

### Repository Management
```plaintext
"Create a new GitHub repository called 'my-api' with .NET template"
→ Creates repo with README, gitignore, license, branch protection

"List all repositories in my organization"
→ Returns list of repos with metadata

"Update repository 'user-service' to make it private"
→ Updates visibility and settings

"Delete repository 'temp-testing' with confirmation"
→ Safely deletes after confirmation
```

### Issue Tracking
```plaintext
"Create bug issue in repo 'my-api' about authentication failure"
→ Creates issue with bug label and assignees

"List all open high-priority issues in 'platform-core'"
→ Filters and lists matching issues
```

### Pull Requests
```plaintext
"Create PR from feature-branch to main in 'my-api'"
→ Creates PR with reviewers and labels

"List all PRs waiting for review in 'user-service'"
→ Shows open PRs needing attention
```

### CI/CD Automation
```plaintext
"Trigger the deploy workflow for production in 'my-api'"
→ Manually triggers deployment workflow

"Show me the last 10 workflow runs for 'my-api'"
→ Lists recent CI/CD runs with status
```

### Team Management
```plaintext
"Add user john.doe to platform-engineering-team as maintainer"
→ Adds team member with specified role

"List all teams in my organization"
→ Shows teams with members and permissions
```

## Next Steps

1. ⬜ Register DevOpsAgent in DI container (`ProgramExtensions.cs` or similar)
2. ⬜ Add DevOpsAgent to PlatformAgentGroupChat orchestration
3. ⬜ Add appsettings.json configuration for DevOpsAgent
4. ⬜ Integration testing with GitHub API
5. ⬜ Implement Azure DevOps tools (10 tools remaining)
6. ⬜ Add workflow/pipeline templates
7. ⬜ Unit tests for all tools

### Immediate Action Required
Before the DevOps Agent can be used:
- ✅ All 10 GitHub tools implemented
- ⬜ Register tools in Dependency Injection container
- ⬜ Add to agent orchestration
- ⬜ Test with live GitHub token

See [DEVOPS-AGENT-DESIGN.md](../../../docs/DEVOPS-AGENT-DESIGN.md) for full specification.
