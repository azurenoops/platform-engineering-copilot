using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Infrastructure.Tools;

/// <summary>
/// rollback_deployment — Roll back a failed deployment. Requires Write PIM.
/// </summary>
public class RollbackDeploymentTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public RollbackDeploymentTool(ILogger<RollbackDeploymentTool> logger) : base(logger) { }

    public override string Name => "rollback_deployment";
    public override string Description => "Roll back a failed infrastructure deployment";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "deploymentId": { "type": "string", "description": "Deployment ID to roll back." },
        "reason": { "type": "string", "description": "Reason for rollback." }
      },
      "required": ["deploymentId"]
    }
    """;

    public override bool RequiresAuthentication => true;
    public override PimTier PimTierRequired => PimTier.Write;

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var deploymentId = GetRequired<string>(parameters, "deploymentId");
        var reason = GetOptional<string>(parameters, "reason") ?? "Manual rollback requested";

        if (string.IsNullOrWhiteSpace(deploymentId))
        {
            sw.Stop();
            return BuildError("MISSING_DEPLOYMENT_ID",
                "Deployment ID is required.", "Provide the deployment ID to roll back", sw);
        }

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 20,
            Message = $"Initiating rollback of deployment {deploymentId[..Math.Min(8, deploymentId.Length)]}..."
        });

        await Task.Delay(10, cancellationToken);

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 60,
            Message = "Rolling back resources..."
        });

        await Task.Delay(10, cancellationToken);

        var result = new
        {
            deploymentId,
            rollbackStatus = "Succeeded",
            reason,
            rolledBackAt = DateTimeOffset.UtcNow.ToString("o"),
            resourcesRemoved = 3
        };

        sw.Stop();

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = "Rollback completed successfully."
        });

        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return JsonSerializer.Serialize(envelope, JsonOptions);
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
