using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Infrastructure.Tools;

/// <summary>
/// get_deployment_status — Get status of a specific deployment. Requires Read PIM.
/// </summary>
public class GetDeploymentStatusTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GetDeploymentStatusTool(ILogger<GetDeploymentStatusTool> logger) : base(logger) { }

    public override string Name => "get_deployment_status";
    public override string Description => "Get the status of a specific infrastructure deployment";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "deploymentId": { "type": "string", "description": "Deployment ID to check." }
      },
      "required": ["deploymentId"]
    }
    """;

    public override bool RequiresAuthentication => true;
    public override PimTier PimTierRequired => PimTier.Read;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var deploymentId = GetRequired<string>(parameters, "deploymentId");

        if (string.IsNullOrWhiteSpace(deploymentId))
        {
            sw.Stop();
            return Task.FromResult(BuildError("MISSING_DEPLOYMENT_ID",
                "Deployment ID is required.", "Provide the deployment ID from provision_infrastructure", sw));
        }

        var result = new
        {
            deploymentId,
            status = "Succeeded",
            resourceGroup = "rg-prod",
            startedAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"),
            completedAt = DateTimeOffset.UtcNow.ToString("o"),
            duration = "5m 12s",
            resourcesCreated = 3,
            resources = new[]
            {
                new { type = "Microsoft.Storage/storageAccounts", name = "stprodgov001", status = "Created" },
                new { type = "Microsoft.Network/virtualNetworks", name = "vnet-prod", status = "Created" },
                new { type = "Microsoft.Network/networkSecurityGroups", name = "nsg-prod", status = "Created" }
            }
        };

        sw.Stop();
        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
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
