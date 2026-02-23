using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.Infrastructure;

/// <summary>
/// Infrastructure Agent — 6 tools for IaC template generation and deployment.
/// Template generation requires no auth. Deployment requires CAC + PIM Write + confirmation.
/// Extends BaseAgent per Constitution Principle II.
/// </summary>
public class InfrastructureAgent : BaseAgent
{
    private readonly string _systemPrompt;

    public InfrastructureAgent(
        ILogger<InfrastructureAgent> logger,
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

    public override string AgentId => "infrastructure";
    public override string AgentName => "Infrastructure Agent";

    public override string Description =>
        "Generates compliant Infrastructure as Code templates (Bicep/Terraform) with NIST compliance annotations " +
        "and manages deployments to Azure Government environments.";

    public override string[] Keywords =>
    [
        "infrastructure", "deploy", "template", "bicep", "terraform",
        "generate", "provision", "aks", "storage", "deployment",
        "rollback", "validate template"
    ];

    public override PimTier RequiredPimTier => PimTier.Read;

    public override string GetSystemPrompt() => _systemPrompt;

    private static string LoadSystemPrompt()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("infrastructure.prompt.txt", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            return "You are the Infrastructure Agent for the Platform Engineering Copilot.";
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
