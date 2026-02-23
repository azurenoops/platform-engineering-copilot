using Microsoft.AspNetCore.Mvc;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// T144 — Environments list + detail per admin-api.md.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EnvironmentsController : ControllerBase
{
    private static readonly List<EnvironmentSummary> Environments = new()
    {
        new() { EnvironmentId = "env-001", Name = "Production", SubscriptionId = "sub-prod-001", Region = "usgovvirginia", Classification = "IL5", Status = "Healthy", ResourceCount = 142, ComplianceScore = 94.5, LastScanAt = DateTimeOffset.UtcNow.AddHours(-2) },
        new() { EnvironmentId = "env-002", Name = "Staging", SubscriptionId = "sub-stg-001", Region = "usgovvirginia", Classification = "IL5", Status = "Warning", ResourceCount = 98, ComplianceScore = 87.2, LastScanAt = DateTimeOffset.UtcNow.AddHours(-4) },
        new() { EnvironmentId = "env-003", Name = "Development", SubscriptionId = "sub-dev-001", Region = "usgovarizona", Classification = "IL4", Status = "Healthy", ResourceCount = 67, ComplianceScore = 72.8, LastScanAt = DateTimeOffset.UtcNow.AddHours(-6) }
    };

    [HttpGet]
    public IActionResult GetEnvironments()
    {
        return Ok(new { environments = Environments, totalCount = Environments.Count });
    }

    [HttpGet("{environmentId}")]
    public IActionResult GetEnvironment(string environmentId)
    {
        var env = Environments.FirstOrDefault(e => e.EnvironmentId == environmentId);
        if (env is null) return NotFound(new { error = new { code = "NOT_FOUND", message = "Environment not found" } });

        var detail = new
        {
            env.EnvironmentId,
            env.Name,
            env.SubscriptionId,
            env.Region,
            env.Classification,
            env.Status,
            env.ResourceCount,
            env.ComplianceScore,
            env.LastScanAt,
            resources = new
            {
                compute = new { count = 12, types = new[] { "Microsoft.Compute/virtualMachines", "Microsoft.ContainerService/managedClusters" } },
                storage = new { count = 8, types = new[] { "Microsoft.Storage/storageAccounts" } },
                networking = new { count = 15, types = new[] { "Microsoft.Network/virtualNetworks", "Microsoft.Network/networkSecurityGroups" } }
            },
            recentDeployments = new[]
            {
                new { deploymentId = "dep-001", templateName = "Standard AKS Cluster", status = "Succeeded", startedAt = DateTimeOffset.UtcNow.AddDays(-1) }
            }
        };

        return Ok(detail);
    }
}

public class EnvironmentSummary
{
    public string EnvironmentId { get; set; } = "";
    public string Name { get; set; } = "";
    public string SubscriptionId { get; set; } = "";
    public string Region { get; set; } = "";
    public string Classification { get; set; } = "";
    public string Status { get; set; } = "";
    public int ResourceCount { get; set; }
    public double ComplianceScore { get; set; }
    public DateTimeOffset LastScanAt { get; set; }
}
