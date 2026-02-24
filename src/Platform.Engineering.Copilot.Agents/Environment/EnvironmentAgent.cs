using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.Environment;

/// <summary>
/// Environment Agent — 10 tools for environment lifecycle management.
/// Read operations require PIM Read, write operations require PIM Write.
/// Extends BaseAgent per Constitution Principle II.
/// </summary>
public class EnvironmentAgent : BaseAgent
{
    private readonly string _systemPrompt;

    public EnvironmentAgent(
        ILogger<EnvironmentAgent> logger,
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

    public override string AgentId => "environment";
    public override string AgentName => "Environment Agent";

    public override string Description =>
        "Manages environment lifecycle — clone, compare, promote, detect drift, " +
        "and validate compliance across dev/staging/prod tiers.";

    public override string[] Keywords =>
    [
        "environment", "clone", "drift", "promote", "compare",
        "staging", "production", "dev", "deploy environment",
        "environment status", "environment history", "validate environment"
    ];

    public override PimTier RequiredPimTier => PimTier.Write;

    public override string GetSystemPrompt() => _systemPrompt;

    private static string LoadSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("environment.prompt.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return "You are the Environment Agent. Help users manage cloud environments.";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
