using System.Reflection;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase;

/// <summary>
/// Knowledge Base Agent — 8 tools for offline compliance knowledge queries.
/// All data from INistService embedded OSCAL catalog. No auth required (SC-008).
/// Extends BaseAgent per Constitution Principle II.
/// </summary>
public class KnowledgeBaseAgent : BaseAgent
{
    private readonly string _systemPrompt;

    public KnowledgeBaseAgent(
        ILogger<KnowledgeBaseAgent> logger,
        params BaseTool[] tools)
        : base(logger)
    {
        _systemPrompt = LoadSystemPrompt();
        foreach (var tool in tools)
        {
            RegisterTool(tool);
        }
    }

    public override string AgentId => "knowledgebase";
    public override string AgentName => "Knowledge Base Agent";

    public override string Description =>
        "Provides plain-language explanations of NIST 800-53 controls, framework comparisons, " +
        "STIG guidance, and implementation examples. All offline — no Azure connectivity required.";

    public override string[] Keywords =>
    [
        "explain", "stig", "control", "guidance", "ato", "nist explain",
        "compare frameworks", "search controls", "knowledge", "checklist",
        "framework summary", "implementation example", "mapping"
    ];

    public override PimTier RequiredPimTier => PimTier.None;

    public override string GetSystemPrompt() => _systemPrompt;

    private static string LoadSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("knowledgebase.prompt.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return "You are the Knowledge Base Agent for the Platform Engineering Copilot.";
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
