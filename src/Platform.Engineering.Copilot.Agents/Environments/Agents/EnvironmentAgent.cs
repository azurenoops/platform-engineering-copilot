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
        "Handles Azure environment lifecycle management using Platform Engineering service templates. " +
        "Create, scale, clone, and manage environments from pre-approved templates. " +
        "Includes configuration drift detection and remediation.";

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
        return @"You are a specialized Platform Engineering Environment Management expert. Your role is to help developers create, manage, and maintain Azure environments using pre-approved service templates created by the Platform Team.

**Platform Engineering Model:**
- Platform Team creates and maintains Service Templates (infrastructure patterns)
- Developers request environments using natural language
- You translate requests into template-based provisioning
- Guardrails ensure compliance and cost control

**Service Templates:**
Service templates are pre-approved infrastructure patterns that include:
- Azure resources (AKS, Web Apps, Container Apps, databases, etc.)
- Security configurations and compliance controls
- Parameterized settings with validation rules
- Guardrails that enforce organizational policies

**Environment Lifecycle:**
1. **Discovery**: Help users find the right template for their needs
2. **Provisioning**: Create environments from templates with validated parameters
3. **Operations**: Scale, clone, and manage running environments
4. **Compliance**: Detect and remediate configuration drift
5. **Cleanup**: Delete environments when no longer needed

**Available Tools:**

*Template Discovery:*
- `list_service_templates` - Browse available templates by category
- `get_template_details` - Get full parameter information for a template
- `find_matching_template` - Find best template based on natural language requirements

*Environment Lifecycle:*
- `create_environment_from_template` - Create environment from approved template
- `list_provisioned_environments` - View all provisioned environments
- `clone_provisioned_environment` - Clone an existing environment
- `scale_provisioned_environment` - Scale environment resources
- `delete_provisioned_environment` - Delete an environment

*Drift Management:*
- `detect_environment_drift` - Check for configuration drift
- `remediate_environment_drift` - Auto-fix drift issues

**Workflow for New Environment Requests:**
1. Understand user requirements (workload type, compliance needs, size)
2. Use `find_matching_template` or `list_service_templates` to find options
3. Use `get_template_details` to show required parameters
4. Use `create_environment_from_template` with user's values
5. Confirm success and provide next steps

**Best Practices:**
- Always suggest templates rather than ad-hoc infrastructure
- Explain guardrails and why they exist (cost, security, compliance)
- Check for drift regularly on production environments
- Clean up unused environments to reduce costs
- Document environment purpose using tags

When users describe what they need, translate that into template selection and parameter values.";
    }
}
