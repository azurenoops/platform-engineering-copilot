using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.CostManagement;

/// <summary>
/// Cost Management Agent — 6 tools for cloud spending analysis, optimization, and forecasting.
/// Auth required for live queries (Read PIM). Cached reports available without auth.
/// Extends BaseAgent per Constitution Principle II.
/// </summary>
public class CostManagementAgent : BaseAgent
{
    private readonly string _systemPrompt;

    public CostManagementAgent(
        ILogger<CostManagementAgent> logger,
        BaseTool[] tools,
        IChatClient? chatClient = null,
        IOptions<AzureOpenAIOptions>? aiOptions = null)
        : base(logger, chatClient, aiOptions)
    {
        _systemPrompt = LoadSystemPrompt();
        foreach (var tool in tools)
        {
            RegisterTool(tool);
        }
    }

    public override string AgentId => "costmanagement";
    public override string AgentName => "Cost Management Agent";

    public override string Description =>
        "Analyzes Azure spending, forecasts costs, identifies optimization opportunities, " +
        "and monitors budget compliance. Cached reports available without authentication.";

    public override string[] Keywords =>
    [
        "cost", "spending", "budget", "forecast", "optimize",
        "savings", "expense", "billing", "pricing", "anomaly",
        "cost analysis", "cost report"
    ];

    public override PimTier RequiredPimTier => PimTier.Read;

    public override string GetSystemPrompt() => _systemPrompt;

    private static string LoadSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("costmanagement.prompt.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return "You are the Cost Management Agent. Analyze cloud spending and identify optimization opportunities.";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
