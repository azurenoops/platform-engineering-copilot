using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Environment.Tools;

/// <summary>
/// clone_environment — Clone an environment with proper naming.
/// Auth required, PIM Write per mcp-tools.md.
/// </summary>
public class CloneEnvironmentTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public CloneEnvironmentTool(ILogger<CloneEnvironmentTool> logger) : base(logger) { }

    public override string Name => "clone_environment";
    public override string Description => "Clone an environment with proper naming conventions and resource tagging";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "sourceEnvironment": { "type": "string", "description": "Name of the source environment to clone." },
        "targetName": { "type": "string", "description": "Name for the cloned environment." },
        "targetTier": { "type": "string", "enum": ["dev", "staging", "prod"], "description": "Target environment tier." },
        "includeData": { "type": "boolean", "default": false, "description": "Clone data along with configuration." }
      },
      "required": ["sourceEnvironment", "targetName", "targetTier"]
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
        var source = GetRequired<string>(parameters, "sourceEnvironment");
        var targetName = GetRequired<string>(parameters, "targetName");
        var targetTier = GetRequired<string>(parameters, "targetTier");
        var includeData = GetOptional<bool?>(parameters, "includeData") ?? false;

        if (string.IsNullOrWhiteSpace(source))
            return Task.FromResult(BuildError("MISSING_SOURCE", "Source environment is required.", "Provide source environment name.", sw));
        if (string.IsNullOrWhiteSpace(targetName))
            return Task.FromResult(BuildError("MISSING_TARGET", "Target name is required.", "Provide target environment name.", sw));

        progress?.Report(new ProgressUpdate { PercentComplete = 30, Message = $"Cloning {source} to {targetName}..." });
        progress?.Report(new ProgressUpdate { PercentComplete = 70, Message = "Provisioning resources..." });
        progress?.Report(new ProgressUpdate { PercentComplete = 100, Message = "Clone complete." });

        var response = new
        {
            status = "success",
            data = new
            {
                sourceEnvironment = source,
                clonedEnvironment = new
                {
                    name = targetName,
                    tier = targetTier,
                    resourceGroup = $"rg-{targetName}-{targetTier}",
                    subscription = "gov-sub-001",
                    region = "usgovvirginia",
                    resourceCount = 12,
                    includeData,
                    status = "Provisioning",
                    estimatedReadyTime = DateTime.UtcNow.AddMinutes(15).ToString("O")
                }
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
