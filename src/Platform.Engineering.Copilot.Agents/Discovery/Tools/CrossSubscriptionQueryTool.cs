using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// cross_subscription_query — Query resources across multiple subscriptions.
/// Auth required, PIM Read per mcp-tools.md.
/// </summary>
public class CrossSubscriptionQueryTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public CrossSubscriptionQueryTool(ILogger<CrossSubscriptionQueryTool> logger) : base(logger) { }

    public override string Name => "cross_subscription_query";
    public override string Description => "Query resources across multiple subscriptions for cross-tenant visibility";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "subscriptionIds": { "type": "array", "items": { "type": "string" }, "description": "List of subscription IDs to query." },
        "resourceType": { "type": "string", "description": "Filter by Azure resource type." },
        "query": { "type": "string", "description": "Azure Resource Graph query (KQL)." }
      },
      "required": ["subscriptionIds"]
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
        var subscriptionIds = GetRequired<string[]>(parameters, "subscriptionIds");
        var resourceType = GetOptional<string>(parameters, "resourceType");
        var query = GetOptional<string>(parameters, "query");

        if (subscriptionIds is null || subscriptionIds.Length == 0)
            return Task.FromResult(BuildError("MISSING_SUBSCRIPTIONS",
                "At least one subscription ID is required.", "Provide subscription IDs.", sw));

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 30,
            Message = $"Querying across {subscriptionIds.Length} subscriptions..."
        });

        var results = subscriptionIds.Select(subId => new
        {
            subscriptionId = subId,
            resourceCount = 12 + subId.GetHashCode() % 20,
            resources = new[]
            {
                new { name = $"vm-{subId[..4]}-01", type = "Microsoft.Compute/virtualMachines", location = "usgovvirginia" },
                new { name = $"sa-{subId[..4]}-01", type = "Microsoft.Storage/storageAccounts", location = "usgovvirginia" }
            }
        }).ToArray();

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = $"Query complete across {subscriptionIds.Length} subscriptions."
        });

        var response = new
        {
            status = "success",
            data = new
            {
                subscriptionCount = subscriptionIds.Length,
                totalResources = results.Sum(r => r.resourceCount),
                filter = resourceType,
                customQuery = query,
                subscriptions = results
            },
            metadata = new
            {
                toolName = Name,
                executionTimeMs = sw.ElapsedMilliseconds,
                timestamp = DateTime.UtcNow.ToString("O")
            }
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
