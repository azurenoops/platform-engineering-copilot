using System.Reflection;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Orchestrator;

/// <summary>
/// OrchestratorAgent — BaseAgent wrapper around PlatformOrchestrator engine (T029).
/// Provides the orchestrator as a first-class agent per Constitution Principle II.
/// Routes user messages to specialized agents via keyword matching, direct targeting,
/// and LLM fallback classification.
/// </summary>
public class OrchestratorAgent : BaseAgent
{
    private readonly PlatformOrchestrator _orchestrator;
    private readonly string _systemPrompt;

    public OrchestratorAgent(
        ILogger<OrchestratorAgent> logger,
        PlatformOrchestrator orchestrator)
        : base(logger)
    {
        _orchestrator = orchestrator;
        _systemPrompt = LoadSystemPrompt();
    }

    public override string AgentId => "orchestrator";
    public override string AgentName => "Orchestrator Agent";
    public override string Description =>
        "Routes user requests to the appropriate specialized agent based on intent analysis. " +
        "Supports direct targeting (@agent), keyword matching, and LLM-based classification.";
    public override string[] Keywords =>
    [
        "help", "route", "which agent", "available", "capabilities", "agents",
        "who can", "what can"
    ];
    public override PimTier RequiredPimTier => PimTier.None;

    public override string GetSystemPrompt() => _systemPrompt;

    /// <summary>
    /// Get the underlying PlatformOrchestrator for direct access to routing.
    /// </summary>
    public PlatformOrchestrator Orchestrator => _orchestrator;

    private static string LoadSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("orchestrator.prompt.txt", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            return "You are the Orchestrator Agent for the Platform Engineering Copilot.";
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
