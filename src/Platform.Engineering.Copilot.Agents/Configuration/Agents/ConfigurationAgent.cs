using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Configuration.Configuration;
using Platform.Engineering.Copilot.Agents.Configuration.Tools;
using Platform.Engineering.Copilot.State.Abstractions;

namespace Platform.Engineering.Copilot.Agents.Configuration.Agents;

/// <summary>
/// Configuration Agent for managing Platform Engineering Copilot settings.
/// Handles subscription configuration, environment settings, and user preferences.
/// This is a lightweight agent focused on configuration operations.
/// </summary>
public class ConfigurationAgent : BaseAgent
{
    public override string AgentId => "configuration";
    public override string AgentName => "Configuration Agent";
    public override string Description =>
        "Manages Platform Engineering Copilot configuration including Azure subscription settings, " +
        "default preferences, and environment configuration. Use this agent to set your default " +
        "subscription before running compliance scans or other Azure operations.";

    protected override float Temperature => (float)_options.Temperature;
    protected override int MaxTokens => _options.MaxTokens;

    private readonly ConfigurationAgentOptions _options;

    public ConfigurationAgent(
        IChatClient chatClient,
        ILogger<ConfigurationAgent> logger,
        IOptions<ConfigurationAgentOptions> options,
        ConfigurationTool configurationTool,
        IAgentStateManager? agentStateManager = null,
        ISharedMemory? sharedMemory = null)
        : base(chatClient, logger, agentStateManager, sharedMemory)
    {
        _options = options?.Value ?? new ConfigurationAgentOptions();

        // Register the configuration tool
        RegisterTool(configurationTool);

        Logger.LogInformation("✅ Configuration Agent initialized (Temperature: {Temperature}, MaxTokens: {MaxTokens})",
            _options.Temperature, _options.MaxTokens);
    }

    /// <summary>
    /// Get system prompt with configuration-specific guidance.
    /// </summary>
    protected override string GetSystemPrompt()
    {
        return SystemPromptLoader.LoadFromType<ConfigurationAgent>("ConfigurationAgent.prompt.txt") ?? "";
    }
}
