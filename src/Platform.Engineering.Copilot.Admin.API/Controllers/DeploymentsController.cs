using Microsoft.AspNetCore.Mvc;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// T144 — Deployments list + create per admin-api.md.
/// POST requires CAC + PIM Write.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DeploymentsController : ControllerBase
{
    private static readonly List<DeploymentRecord> Deployments = new()
    {
        new() { DeploymentId = "dep-001", TemplateName = "Standard AKS Cluster", EnvironmentName = "Production", Status = "Succeeded", InitiatedBy = "Jane Smith", StartedAt = DateTimeOffset.UtcNow.AddDays(-1), CompletedAt = DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(12) },
        new() { DeploymentId = "dep-002", TemplateName = "Secure Storage Account", EnvironmentName = "Staging", Status = "Succeeded", InitiatedBy = "John Doe", StartedAt = DateTimeOffset.UtcNow.AddDays(-3), CompletedAt = DateTimeOffset.UtcNow.AddDays(-3).AddMinutes(5) },
        new() { DeploymentId = "dep-003", TemplateName = "Key Vault Premium", EnvironmentName = "Development", Status = "Failed", InitiatedBy = "Jane Smith", StartedAt = DateTimeOffset.UtcNow.AddDays(-2), CompletedAt = DateTimeOffset.UtcNow.AddDays(-2).AddMinutes(3), ErrorMessage = "Insufficient permissions on target subscription" }
    };

    [HttpGet]
    public IActionResult GetDeployments([FromQuery] string? environment, [FromQuery] string? status)
    {
        var results = Deployments.AsEnumerable();
        if (!string.IsNullOrEmpty(environment))
            results = results.Where(d => d.EnvironmentName.Equals(environment, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(status))
            results = results.Where(d => d.Status.Equals(status, StringComparison.OrdinalIgnoreCase));

        var list = results.ToList();
        return Ok(new { deployments = list, totalCount = list.Count });
    }

    [HttpPost]
    public IActionResult CreateDeployment([FromBody] CreateDeploymentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TemplateId) || string.IsNullOrWhiteSpace(request.EnvironmentId))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "templateId and environmentId are required" } });

        var deployment = new DeploymentRecord
        {
            DeploymentId = $"dep-{Guid.NewGuid().ToString()[..8]}",
            TemplateName = request.TemplateId,
            EnvironmentName = request.EnvironmentId,
            Status = "InProgress",
            InitiatedBy = "Current User",
            StartedAt = DateTimeOffset.UtcNow,
            Parameters = request.Parameters
        };
        Deployments.Add(deployment);
        return Accepted(new { deploymentId = deployment.DeploymentId, status = "InProgress", message = "Deployment initiated" });
    }
}

public class DeploymentRecord
{
    public string DeploymentId { get; set; } = "";
    public string TemplateName { get; set; } = "";
    public string EnvironmentName { get; set; } = "";
    public string Status { get; set; } = "";
    public string InitiatedBy { get; set; } = "";
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}

public class CreateDeploymentRequest
{
    public string TemplateId { get; set; } = "";
    public string EnvironmentId { get; set; } = "";
    public Dictionary<string, string>? Parameters { get; set; }
}
