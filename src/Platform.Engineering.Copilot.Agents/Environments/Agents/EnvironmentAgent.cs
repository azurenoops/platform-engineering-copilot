using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Environments.Configuration;
using Platform.Engineering.Copilot.Agents.Environments.State;
using Platform.Engineering.Copilot.Agents.Environments.Tools;
using Platform.Engineering.Copilot.State.Abstractions;

namespace Platform.Engineering.Copilot.Agents.Environments.Agents;

/// <summary>
/// Environment Agent for Azure environment lifecycle management using Platform Engineering service templates.
/// Enables developers to request environments via natural language while Platform Team controls templates.
/// </summary>
public class EnvironmentAgent : BaseAgent
{
    public override string AgentId => "environment";
    public override string AgentName => "Environment Agent";
    public override string Description =>
        "Handles PROVISIONED ENVIRONMENTS - template-based deployments created from Platform Engineering service templates. " +
        "Use this agent when users ask to 'list environments', 'show environments', 'create environment', " +
        "'scale environment', 'clone environment', or 'check drift'. " +
        "Includes listing provisioned environments, creating new environments from templates, scaling, cloning, " +
        "configuration drift detection and remediation. " +
        "NOTE: This is NOT for listing Azure resources or resource groups - use Discovery Agent for that.";

    protected override float Temperature => (float)_options.Temperature;
    protected override int MaxTokens => _options.MaxTokens;

    private readonly EnvironmentStateAccessors _stateAccessors;
    private readonly EnvironmentAgentOptions _options;

    public EnvironmentAgent(
        IChatClient chatClient,
        ILogger<EnvironmentAgent> logger,
        IOptions<EnvironmentAgentOptions> options,
        EnvironmentStateAccessors stateAccessors,
        // Template-based tools (new Platform Engineering approach)
        ServiceTemplateListTool templateListTool,
        ServiceTemplateDetailsTool templateDetailsTool,
        ServiceTemplateMatchTool templateMatchTool,
        CreateEnvironmentFromTemplateTool createFromTemplateTool,
        ProvisionedEnvironmentListTool provisionedListTool,
        EnvironmentScaleFromTemplateTool scaleFromTemplateTool,
        EnvironmentCloneFromTemplateTool cloneFromTemplateTool,
        EnvironmentDeleteTool deleteTool,
        EnvironmentDriftDetectionTool driftDetectionTool,
        EnvironmentDriftRemediationTool driftRemediationTool,
        IAgentStateManager? agentStateManager = null,
        ISharedMemory? sharedMemory = null)
        : base(chatClient, logger, agentStateManager, sharedMemory)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _options = options?.Value ?? new EnvironmentAgentOptions();

        // Register template-based tools (Platform Engineering approach)
        RegisterTool(templateListTool);
        RegisterTool(templateDetailsTool);
        RegisterTool(templateMatchTool);
        RegisterTool(createFromTemplateTool);
        RegisterTool(provisionedListTool);
        RegisterTool(scaleFromTemplateTool);
        RegisterTool(cloneFromTemplateTool);
        RegisterTool(deleteTool);
        RegisterTool(driftDetectionTool);
        RegisterTool(driftRemediationTool);

        Logger.LogInformation("✅ Environment Agent initialized with Platform Engineering templates (Temperature: {Temperature}, MaxTokens: {MaxTokens})",
            _options.Temperature, _options.MaxTokens);
    }

    /// <summary>
    /// Get the system prompt for the Environment Agent
    /// </summary>
    protected override string GetSystemPrompt()
    {
        return SystemPromptLoader.LoadFromType<EnvironmentAgent>("EnvironmentAgent.prompt.txt") ?? "";
    }
}
