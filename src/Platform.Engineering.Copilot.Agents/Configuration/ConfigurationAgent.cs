using System.Reflection;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Configuration;

/// <summary>
/// Configuration Agent — manages ATO Copilot settings (FR-043–FR-045).
/// Stores subscription, framework, baseline, and preferences in IAgentStateManager
/// for consumption by other agents.
/// </summary>
public class ConfigurationAgent : BaseAgent
{
    private readonly string _systemPrompt;

    public ConfigurationAgent(
        ILogger<ConfigurationAgent> logger,
        params BaseTool[] tools)
        : base(logger)
    {
        _systemPrompt = LoadSystemPrompt();
        foreach (var tool in tools)
        {
            RegisterTool(tool);
        }
    }

    public override string AgentId => "configuration";
    public override string AgentName => "Configuration Agent";
    public override string Description =>
        "Manages ATO Copilot settings including subscription, framework, baseline, " +
        "and operational preferences. Settings are shared across all agents.";
    public override string[] Keywords =>
    [
        "configure", "configuration", "set subscription", "set framework",
        "set baseline", "settings", "show configuration", "preferences",
        "subscription", "switch", "environment"
    ];
    public override PimTier RequiredPimTier => PimTier.None;

    public override string GetSystemPrompt() => _systemPrompt;

    private static string LoadSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("configuration.prompt.txt", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return "You are the Configuration Agent for the Platform Engineering Copilot.";
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
