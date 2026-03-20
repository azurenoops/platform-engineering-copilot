using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase;

/// <summary>
/// Knowledge Base Agent — platform knowledge and documentation shell.
/// Currently a tool-less shell; will be repurposed with MCP servers (Azure) in a future feature.
/// Extends BaseAgent per Constitution Principle II.
/// </summary>
public class KnowledgeBaseAgent : BaseAgent
{
    private readonly string _systemPrompt;

    public KnowledgeBaseAgent(
        ILogger<KnowledgeBaseAgent> logger,
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

    public override string AgentId => "knowledgebase";
    public override string AgentName => "Knowledge Base Agent";

    public override string Description =>
        "Platform knowledge and documentation agent. " +
        "Assists with platform engineering documentation, Azure resource guidance, and general knowledge queries.";

    public override string[] Keywords =>
    [
        "knowledge", "documentation", "platform", "help"
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
