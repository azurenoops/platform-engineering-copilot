using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.Security;

/// <summary>
/// Security Agent — 3 tools for Azure security posture assessment and policy management.
/// View operations require PIM Read, modify operations require PIM Write.
/// Extends BaseAgent per Constitution Principle II.
/// </summary>
public class SecurityAgent : BaseAgent
{
    private readonly string _systemPrompt;

    public SecurityAgent(
        ILogger<SecurityAgent> logger,
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

    public override string AgentId => "security";
    public override string AgentName => "Security Agent";

    public override string Description =>
        "Assesses Azure security posture through Secure Score, Defender recommendations, " +
        "and security policy management.";

    public override string[] Keywords =>
    [
        "security", "secure score", "defender", "recommendations",
        "security policy", "vulnerabilities", "threat", "posture",
        "security assessment", "hardening"
    ];

    public override PimTier RequiredPimTier => PimTier.Read;

    public override string GetSystemPrompt() => _systemPrompt;

    private static string LoadSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("security.prompt.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            return "You are the Security Agent. Help users assess and improve their Azure security posture.";

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
