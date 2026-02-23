using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Infrastructure.Tools;

/// <summary>
/// list_deployments — Lists recent deployments. Requires Read PIM.
/// </summary>
public class ListDeploymentsTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ListDeploymentsTool(ILogger<ListDeploymentsTool> logger) : base(logger) { }

    public override string Name => "list_deployments";
    public override string Description => "List recent infrastructure deployments";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "resourceGroup": { "type": "string", "description": "Filter by resource group." },
        "limit": { "type": "integer", "default": 10, "description": "Max results." }
      },
      "required": []
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
        var limit = GetOptional<int>(parameters, "limit");
        if (limit <= 0) limit = 10;

        var deployments = new[]
        {
            new { deploymentId = Guid.NewGuid().ToString(), resourceGroup = "rg-prod", status = "Succeeded", createdAt = DateTimeOffset.UtcNow.AddHours(-2).ToString("o"), resourcesCreated = 5 },
            new { deploymentId = Guid.NewGuid().ToString(), resourceGroup = "rg-staging", status = "Succeeded", createdAt = DateTimeOffset.UtcNow.AddDays(-1).ToString("o"), resourcesCreated = 3 },
            new { deploymentId = Guid.NewGuid().ToString(), resourceGroup = "rg-dev", status = "Failed", createdAt = DateTimeOffset.UtcNow.AddDays(-3).ToString("o"), resourcesCreated = 0 }
        };

        var result = new
        {
            totalDeployments = deployments.Length,
            deployments = deployments.Take(limit).ToArray()
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
}
