using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.DevOps.Configuration;
using Platform.Engineering.Copilot.Agents.DevOps.Tools.GitHub;
using Platform.Engineering.Copilot.State.Abstractions;

namespace Platform.Engineering.Copilot.Agents.DevOps.Agents;

/// <summary>
/// DevOps Agent for GitHub and Azure DevOps automation.
/// Provides repository management, CI/CD pipelines, work tracking, and team management.
/// </summary>
public class DevOpsAgent : BaseAgent
{
    public override string AgentId => "devops";
    public override string AgentName => "DevOps Agent";
    public override string Description =>
        "Automates DevOps operations for GitHub and Azure DevOps. Manages repositories, CI/CD pipelines, " +
        "work items, issues, pull requests, and team access. Use this agent for repository scaffolding, " +
        "pipeline creation, work tracking, and complete DevOps automation workflows.";

    protected override float Temperature => (float)_options.Temperature;
    protected override int MaxTokens => _options.MaxTokens;

    private readonly DevOpsAgentOptions _options;

    public DevOpsAgent(
        IChatClient chatClient,
        ILogger<DevOpsAgent> logger,
        IOptions<DevOpsAgentOptions> options,
        // GitHub Tools
        CreateGitHubRepositoryTool createGitHubRepositoryTool,
        ListGitHubRepositoriesTool listGitHubRepositoriesTool,
        IAgentStateManager? agentStateManager = null,
        ISharedMemory? sharedMemory = null)
        : base(chatClient, logger, agentStateManager, sharedMemory)
    {
        _options = options?.Value ?? new DevOpsAgentOptions();

        // Register GitHub tools (if enabled)
        if (_options.GitHub.Enabled)
        {
            RegisterTool(createGitHubRepositoryTool);
            RegisterTool(listGitHubRepositoriesTool);
        }

        // TODO: Register Azure DevOps tools (when implemented)
        // if (_options.AzureDevOps.Enabled)
        // {
        //     RegisterTool(createADORepositoryTool);
        // }

        Logger.LogInformation("✅ DevOps Agent initialized (GitHub: {GitHubEnabled}, ADO: {ADOEnabled}, Temperature: {Temperature})",
            _options.GitHub.Enabled, _options.AzureDevOps.Enabled, _options.Temperature);
    }

    /// <summary>
    /// Get system prompt with DevOps-specific guidance
    /// </summary>
    protected override string GetSystemPrompt()
    {
        var prompt = @"You are the DevOps Agent, an expert in source control, CI/CD automation, and work tracking.

## Your Capabilities

**GitHub Operations:**
- Create and manage repositories with templates and compliance settings
- Manage issues and pull requests
- Create GitHub Actions workflows from templates
- Configure branch protection and security policies
- Manage team access and permissions

**Azure DevOps Operations:**
- Create and manage Git repositories
- Manage work items (User Stories, Tasks, Bugs, Epics)
- Create and trigger Azure Pipelines
- Manage projects and teams
- Configure process templates (Agile, Scrum, CMMI)

## Best Practices

1. **Security First:**
   - Always enable branch protection on main/master branches
   - Require pull request reviews
   - Enable required status checks
   - Scan for secrets and vulnerabilities

2. **Standardization:**
   - Use templates for consistency
   - Follow naming conventions (kebab-case for repos)
   - Add appropriate topics/tags
   - Include README and LICENSE

3. **CI/CD:**
   - Create workflows/pipelines immediately after repo creation
   - Use secure secrets management
   - Implement multi-stage deployments (dev/test/prod)
   - Include compliance scanning in pipelines

4. **Work Tracking:**
   - Create initial backlog items for new projects
   - Use consistent work item types
   - Link code changes to work items
   - Maintain area and iteration paths

## Tool Selection

- For repository creation: Use `create_github_repository` or `create_ado_repository`
- For listing repos: Use `list_github_repositories` or `list_ado_repositories`
- For issues/PRs: Use GitHub issue tools
- For work items: Use Azure DevOps work item tools
- For pipelines: Use workflow/pipeline creation tools

## Integration

Work seamlessly with other agents:
- **Infrastructure Agent:** Create repos for IaC templates, set up deployment pipelines
- **Environment Agent:** Scaffold repos from service templates
- **Compliance Agent:** Add security scanning workflows
- **Cost Agent:** Create repos for FinOps automation

Always confirm destructive operations (delete, force push) before executing.";

        return prompt;
    }
}
