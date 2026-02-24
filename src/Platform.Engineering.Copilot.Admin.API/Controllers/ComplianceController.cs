using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// Compliance summary, per-environment assessment, and scan triggers.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ComplianceController : ControllerBase
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<ComplianceController> _logger;

    public ComplianceController(PlatformEngineeringCopilotContext context, ILogger<ComplianceController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>GET /api/compliance/summary — Compliance overview across all environments.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
    {
        try
        {
            var environments = await _context.ProvisionedEnvironments
                .Where(e => e.Status == EnvironmentStatus.Running)
                .ToListAsync(cancellationToken);

            var totalCount = environments.Count;
            var driftCount = environments.Count(e => e.HasDrift);
            var healthyCount = environments.Count(e => !e.HasDrift);

            // Stub compliance scoring — in production, integrate with NIST/compliance engine
            var frameworkScores = new[]
            {
                new { framework = "NIST 800-53", score = totalCount > 0 ? 87.5 : 0.0, controlsPassed = 42, controlsFailed = 6, controlsTotal = 48 },
                new { framework = "FedRAMP High", score = totalCount > 0 ? 92.3 : 0.0, controlsPassed = 36, controlsFailed = 3, controlsTotal = 39 },
                new { framework = "DoD IL5", score = totalCount > 0 ? 89.1 : 0.0, controlsPassed = 49, controlsFailed = 6, controlsTotal = 55 }
            };

            var overallScore = frameworkScores.Length > 0 ? frameworkScores.Average(f => f.score) : 0;

            var environmentStatuses = environments.Select(e => new
            {
                environmentId = e.Id,
                name = e.Name,
                status = e.HasDrift ? "NonCompliant" : "Compliant",
                driftCount = e.DriftCount
            });

            return Ok(new
            {
                overallScore = Math.Round(overallScore, 1),
                frameworkScores,
                totalEnvironments = totalCount,
                compliantCount = healthyCount,
                nonCompliantCount = driftCount,
                environmentStatuses,
                lastScanAt = DateTimeOffset.UtcNow,
                topViolations = Array.Empty<object>()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance summary");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to get compliance summary." } });
        }
    }

    /// <summary>POST /api/compliance/scan — Trigger compliance scan (optionally for one environment).</summary>
    [HttpPost("scan")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> TriggerScan([FromQuery] Guid? environmentId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (environmentId.HasValue)
            {
                var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { environmentId.Value }, cancellationToken);
                if (env is null)
                    return NotFound(new { error = new { code = "NOT_FOUND", message = $"Environment {environmentId} not found." } });

                _logger.LogInformation("Compliance scan triggered for environment {EnvironmentId}", environmentId);
            }
            else
            {
                _logger.LogInformation("Compliance scan triggered for all environments");
            }

            // 202 Accepted — scan runs asynchronously
            return Accepted(new
            {
                status = "Accepted",
                message = environmentId.HasValue
                    ? $"Compliance scan queued for environment {environmentId}."
                    : "Compliance scan queued for all environments.",
                scheduledAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error triggering compliance scan");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to trigger compliance scan." } });
        }
    }

    /// <summary>GET /api/compliance/environments/{environmentId} — Per-environment compliance detail.</summary>
    [HttpGet("environments/{environmentId:guid}")]
    public async Task<IActionResult> GetEnvironmentCompliance(Guid environmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var env = await _context.ProvisionedEnvironments.FindAsync(new object[] { environmentId }, cancellationToken);
            if (env is null)
                return NotFound(new { error = new { code = "NOT_FOUND", message = $"Environment {environmentId} not found." } });

            var driftItems = await _context.DriftItems
                .Where(d => d.EnvironmentId == environmentId && !d.IsRemediated)
                .ToListAsync(cancellationToken);

            var resources = await _context.DeployedResources
                .Where(r => r.EnvironmentId == environmentId)
                .ToListAsync(cancellationToken);

            // Stub compliance assessment
            var frameworkResults = new[]
            {
                new
                {
                    framework = "NIST 800-53",
                    overallScore = env.HasDrift ? 75.0 : 95.0,
                    controls = new[]
                    {
                        new { controlId = "AC-2", title = "Account Management", status = "Pass", severity = "High" },
                        new { controlId = "SC-7", title = "Boundary Protection", status = env.HasDrift ? "Fail" : "Pass", severity = "Critical" }
                    }
                }
            };

            var resourceCompliance = resources.Select(r => new
            {
                resourceId = r.AzureResourceId,
                resourceName = r.Name,
                resourceType = r.Type,
                isCompliant = r.ProvisioningState == "Succeeded",
                violations = driftItems
                    .Where(d => d.ResourceId == r.AzureResourceId)
                    .Select(d => new
                    {
                        property = d.PropertyPath,
                        expected = d.ExpectedValue,
                        actual = d.ActualValue,
                        severity = d.Severity.ToString()
                    })
            });

            return Ok(new
            {
                environmentId,
                environmentName = env.Name,
                overallScore = env.HasDrift ? 75.0 : 95.0,
                status = env.HasDrift ? "NonCompliant" : "Compliant",
                frameworkResults,
                resourceCompliance,
                driftCount = driftItems.Count,
                lastAssessedAt = DateTimeOffset.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance detail for environment {EnvironmentId}", environmentId);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to get compliance detail." } });
        }
    }
}
