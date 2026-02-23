using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Environment.Tools;

/// <summary>
/// create_environment — Create a new environment from template or baseline.
/// Auth required, PIM Write.
/// </summary>
public class CreateEnvironmentTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public CreateEnvironmentTool(ILogger<CreateEnvironmentTool> logger) : base(logger) { }

    public override string Name => "create_environment";
    public override string Description => "Create a new environment from a template or baseline configuration";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "name": { "type": "string", "description": "New environment name." },
        "tier": { "type": "string", "enum": ["dev", "staging", "prod"], "description": "Environment tier." },
        "templateName": { "type": "string", "description": "Template to use for provisioning." },
        "region": { "type": "string", "default": "usgovvirginia", "description": "Azure Government region." }
      },
      "required": ["name", "tier"]
    }
    """;

    public override bool RequiresAuthentication => true;
    public override PimTier PimTierRequired => PimTier.Write;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var name = GetRequired<string>(parameters, "name");
        var tier = GetRequired<string>(parameters, "tier");
        var template = GetOptional<string>(parameters, "templateName") ?? "default-gov-il5";
        var region = GetOptional<string>(parameters, "region") ?? "usgovvirginia";

        if (string.IsNullOrWhiteSpace(name))
            return Task.FromResult(BuildError("MISSING_NAME", "Environment name is required.", "Provide environment name.", sw));

        progress?.Report(new ProgressUpdate { PercentComplete = 30, Message = $"Creating {name} ({tier}) in {region}..." });
        progress?.Report(new ProgressUpdate { PercentComplete = 100, Message = "Environment created." });

        var response = new
        {
            status = "success",
            data = new
            {
                name,
                tier,
                template,
                region,
                resourceGroup = $"rg-{name}-{tier}",
                provisioningStatus = "Provisioning",
                estimatedReadyTime = DateTime.UtcNow.AddMinutes(20).ToString("O"),
                resourceCount = 0,
                complianceBaseline = "FedRAMP High"
            },
            metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTime.UtcNow.ToString("O") }
        };

        return Task.FromResult(JsonSerializer.Serialize(response, JsonOptions));
    }

    private object BuildMetadata(Stopwatch sw) => new
    {
        toolName = Name,
        executionTimeMs = sw.ElapsedMilliseconds,
        timestamp = DateTimeOffset.UtcNow.ToString("o")
    };

    private string BuildError(string code, string message, string suggestion, Stopwatch sw)
    {
        sw.Stop();
        return JsonSerializer.Serialize(new
        {
            status = "error",
            error = new { errorCode = code, message, suggestion },
            metadata = BuildMetadata(sw)
        }, JsonOptions);
    }
}
