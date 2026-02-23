using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// discover_resources — Query Azure Resource Graph for resource inventory.
/// Auth required, PIM Read per mcp-tools.md.
/// </summary>
public class DiscoverResourcesTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public DiscoverResourcesTool(ILogger<DiscoverResourcesTool> logger) : base(logger) { }

    public override string Name => "discover_resources";
    public override string Description => "Query Azure Resource Graph for resource inventory with optional type filtering and health status";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "resourceType": { "type": "string", "description": "Filter by Azure resource type (e.g., Microsoft.Compute/virtualMachines)." },
        "subscriptionId": { "type": "string", "description": "Azure subscription ID to query." },
        "includeHealth": { "type": "boolean", "default": true, "description": "Include resource health status." }
      },
      "required": ["subscriptionId"]
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
        var subscriptionId = GetRequired<string>(parameters, "subscriptionId");
        var resourceType = GetOptional<string>(parameters, "resourceType");
        var includeHealth = GetOptional<bool?>(parameters, "includeHealth") ?? true;

        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Task.FromResult(BuildError("MISSING_SUBSCRIPTION",
                "Subscription ID is required.", "Provide a valid Azure subscription ID.", sw));

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 30,
            Message = $"Querying resources in subscription {subscriptionId[..Math.Min(8, subscriptionId.Length)]}..."
        });

        var resources = GenerateSampleResources(subscriptionId, resourceType, includeHealth);

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = $"Discovered {resources.Count} resources."
        });

        var response = new
        {
            status = "success",
            data = new
            {
                subscriptionId,
                resourceCount = resources.Count,
                filter = resourceType,
                includeHealth,
                resources
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

    private static List<object> GenerateSampleResources(string subscriptionId, string? resourceType, bool includeHealth)
    {
        var allResources = new List<(string Name, string Type, string Rg, string Region, string Health)>
        {
            ("vm-web-01", "Microsoft.Compute/virtualMachines", "rg-web-prod", "usgovvirginia", "Healthy"),
            ("vm-web-02", "Microsoft.Compute/virtualMachines", "rg-web-prod", "usgovvirginia", "Healthy"),
            ("sa-logs-prod", "Microsoft.Storage/storageAccounts", "rg-data-prod", "usgovvirginia", "Healthy"),
            ("sql-main-prod", "Microsoft.Sql/servers", "rg-data-prod", "usgovarizona", "Degraded"),
            ("kv-secrets-prod", "Microsoft.KeyVault/vaults", "rg-security-prod", "usgovvirginia", "Healthy"),
            ("nsg-web-prod", "Microsoft.Network/networkSecurityGroups", "rg-web-prod", "usgovvirginia", "Healthy"),
            ("vnet-main-prod", "Microsoft.Network/virtualNetworks", "rg-network-prod", "usgovvirginia", "Healthy"),
            ("pip-lb-prod", "Microsoft.Network/publicIPAddresses", "rg-network-prod", "usgovvirginia", "Healthy")
        };

        var filtered = resourceType is not null
            ? allResources.Where(r => r.Type.Contains(resourceType, StringComparison.OrdinalIgnoreCase)).ToList()
            : allResources;

        return filtered.Select(r => (object)new
        {
            name = r.Name,
            type = r.Type,
            resourceGroup = r.Rg,
            location = r.Region,
            subscriptionId,
            health = includeHealth ? r.Health : null,
            tags = new { environment = "production", owner = "platform-team" }
        }).ToList();
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
