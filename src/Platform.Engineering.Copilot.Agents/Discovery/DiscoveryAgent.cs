using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.Discovery;

/// <summary>
/// Discovery Agent — 9 tools for Azure resource inventory, dependency mapping, and analysis.
/// All operations require CAC authentication and PIM Read elevation.
/// Extends BaseAgent per Constitution Principle II.
/// </summary>
public class DiscoveryAgent : BaseAgent
{
    private readonly string _systemPrompt;

    public DiscoveryAgent(
        ILogger<DiscoveryAgent> logger,
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

    public override string AgentId => "discovery";
    public override string AgentName => "Discovery Agent";

    public override string Description =>
        "Discovers and inventories Azure resources, maps dependencies, analyzes tags, " +
        "and monitors resource health across subscriptions.";

    public override string[] Keywords =>
    [
        "discover", "resources", "inventory", "dependencies", "topology",
        "tags", "health", "orphaned", "metrics", "resource graph",
        "cross-subscription", "network map"
    ];

    public override PimTier RequiredPimTier => PimTier.Read;

    public override string GetSystemPrompt() => _systemPrompt;

    private static string LoadSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("discovery.prompt.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return "You are the Discovery Agent. Help users discover and inventory Azure resources.";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
