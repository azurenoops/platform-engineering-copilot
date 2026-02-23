using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.Compliance;

/// <summary>
/// Compliance Agent — 12 tools for NIST 800-53, FedRAMP, and DoD IL5 compliance
/// assessment, remediation, monitoring, and documentation.
/// Extends BaseAgent per Constitution Principle II.
/// </summary>
public class ComplianceAgent : BaseAgent
{
    private readonly string _systemPrompt;

    public ComplianceAgent(
        ILogger<ComplianceAgent> logger,
        IEnumerable<BaseTool> complianceTools,
        IChatClient? chatClient = null,
        IOptions<AzureOpenAIOptions>? aiOptions = null)
        : base(logger, chatClient, aiOptions)
    {
        _systemPrompt = LoadSystemPrompt();

        foreach (var tool in complianceTools)
        {
            RegisterTool(tool);
        }
    }

    public override string AgentId => "compliance";
    public override string AgentName => "Compliance Agent";

    public override string Description =>
        "Manages Azure Government compliance posture through NIST 800-53, FedRAMP, and DoD IL5 frameworks. " +
        "Runs assessments, remediates findings, collects evidence, and generates compliance documents.";

    public override string[] Keywords =>
    [
        "compliance", "nist", "fedramp", "assessment", "finding", "control",
        "remediate", "remediation", "audit", "evidence", "ssp", "sar", "poam",
        "compliance score", "control family", "il5", "dod", "stig",
        "800-53", "baseline", "framework"
    ];

    public override PimTier RequiredPimTier => PimTier.Read;

    public override string GetSystemPrompt() => _systemPrompt;

    private static string LoadSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("compliance.prompt.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return "You are the Compliance Agent for the Platform Engineering Copilot.";
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
