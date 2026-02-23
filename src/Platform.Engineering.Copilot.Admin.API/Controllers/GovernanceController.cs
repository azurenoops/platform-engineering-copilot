using Microsoft.AspNetCore.Mvc;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// T144 — Governance snapshots per admin-api.md.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class GovernanceController : ControllerBase
{
    [HttpGet("snapshots")]
    public IActionResult GetSnapshots()
    {
        var snapshot = new
        {
            snapshotId = $"snap-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            generatedAt = DateTimeOffset.UtcNow,
            overallComplianceScore = 89.7,
            frameworks = new[]
            {
                new { framework = "NIST 800-53 Rev5", controlsAssessed = 325, controlsPassing = 298, compliancePercentage = 91.7 },
                new { framework = "FedRAMP High", controlsAssessed = 421, controlsPassing = 372, compliancePercentage = 88.4 }
            },
            environmentScores = new[]
            {
                new { environment = "Production", score = 94.5, status = "Healthy" },
                new { environment = "Staging", score = 87.2, status = "Warning" },
                new { environment = "Development", score = 72.8, status = "Healthy" }
            },
            criticalFindings = new[]
            {
                new { findingId = "CF-001", severity = "High", description = "3 storage accounts missing encryption at rest", affectedResources = 3, recommendation = "Enable Azure Storage encryption with CMK" },
                new { findingId = "CF-002", severity = "Medium", description = "NSG rules allow broad inbound access", affectedResources = 5, recommendation = "Restrict NSG rules to specific IP ranges" }
            }
        };

        return Ok(snapshot);
    }
}
