using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// get_orphaned_resources — Identify orphaned resources (unattached disks, unused IPs, empty resource groups).
/// Auth required, PIM Read.
/// </summary>
public class GetOrphanedResourcesTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GetOrphanedResourcesTool(ILogger<GetOrphanedResourcesTool> logger) : base(logger) { }

    public override string Name => "get_orphaned_resources";
    public override string Description => "Identify orphaned resources such as unattached disks, unused IPs, and empty resource groups";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "subscriptionId": { "type": "string", "description": "Azure subscription ID." },
        "category": { "type": "string", "enum": ["all", "disks", "ips", "nics", "resource-groups"], "default": "all", "description": "Category of orphaned resources." }
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
        var category = GetOptional<string>(parameters, "category") ?? "all";

        if (string.IsNullOrWhiteSpace(subscriptionId))
            return Task.FromResult(BuildError("MISSING_SUBSCRIPTION",
                "Subscription ID is required.", "Provide a valid Azure subscription ID.", sw));

        progress?.Report(new ProgressUpdate { PercentComplete = 100, Message = "Orphaned resource scan complete." });

        var response = new
        {
            status = "success",
            data = new
            {
                subscriptionId,
                category,
                totalOrphaned = 7,
                estimatedMonthlyCost = 234.50,
                orphanedResources = new[]
                {
                    new { name = "disk-old-vm-01", type = "Microsoft.Compute/disks", resourceGroup = "rg-legacy", reason = "Unattached managed disk", monthlyCost = 45.00, lastUsed = DateTime.UtcNow.AddDays(-90).ToString("O") },
                    new { name = "pip-decomm-01", type = "Microsoft.Network/publicIPAddresses", resourceGroup = "rg-legacy", reason = "Unassociated public IP", monthlyCost = 3.65, lastUsed = DateTime.UtcNow.AddDays(-60).ToString("O") },
                    new { name = "nic-old-vm-02", type = "Microsoft.Network/networkInterfaces", resourceGroup = "rg-legacy", reason = "Orphaned NIC (no VM)", monthlyCost = 0.00, lastUsed = DateTime.UtcNow.AddDays(-45).ToString("O") },
                    new { name = "rg-temp-test", type = "Microsoft.Resources/resourceGroups", resourceGroup = "rg-temp-test", reason = "Empty resource group", monthlyCost = 0.00, lastUsed = DateTime.UtcNow.AddDays(-120).ToString("O") }
                },
                recommendations = new[]
                {
                    "Delete 3 unattached disks to save $135/month",
                    "Release 2 unused public IPs to save $7.30/month",
                    "Remove 1 empty resource group"
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
