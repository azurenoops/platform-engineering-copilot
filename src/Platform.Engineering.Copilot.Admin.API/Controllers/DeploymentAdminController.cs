using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Data.Context;
using Microsoft.EntityFrameworkCore;

namespace Platform.Engineering.Copilot.Admin.Controllers;

/// <summary>
/// Admin controller for deployment management.
/// NOTE: This controller depends on the removed DeploymentPollingService from the Extensions project.
/// Deployment tracking functionality has been simplified - use EnvironmentDeployments table directly.
/// </summary>
[Obsolete("DeploymentAdminController is obsolete. It depends on the removed DeploymentPollingService from Extensions project. " +
          "Use EnvironmentDeployments database table directly for deployment status. " +
          "Will be removed in a future release.", error: false)]
[ApiController]
[Route("api/admin/deployments")]
public class DeploymentAdminController : ControllerBase
{
    private readonly ILogger<DeploymentAdminController> _logger;
    private readonly PlatformEngineeringCopilotContext _dbContext;

    public DeploymentAdminController(
        ILogger<DeploymentAdminController> logger,
        PlatformEngineeringCopilotContext dbContext)
    {
        _logger = logger;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Get deployment status by ID
    /// </summary>
    [HttpGet("{deploymentId}/status")]
    public async Task<ActionResult<DeploymentStatusResponse>> GetDeploymentStatusAsync(
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting deployment status for {DeploymentId}", deploymentId);

            // Check database for deployment
            var deployment = await _dbContext.EnvironmentDeployments
                .FirstOrDefaultAsync(d => d.Id == deploymentId && !d.IsDeleted, cancellationToken);

            if (deployment == null)
            {
                return NotFound(new { error = $"Deployment {deploymentId} not found" });
            }

            // Map database deployment to response
            return Ok(new DeploymentStatusResponse
            {
                DeploymentId = deployment.Id.ToString(),
                DeploymentName = deployment.Name,
                State = deployment.Status.ToString(),
                ProgressPercentage = deployment.Status == Data.Entities.DeploymentStatus.Succeeded ? 100 : 0,
                CurrentOperation = deployment.Status.ToString(),
                StartTime = deployment.CreatedAt.ToString("o"),
                EndTime = deployment.UpdatedAt.ToString("o"),
                ErrorMessage = deployment.Status == Data.Entities.DeploymentStatus.Failed 
                    ? "Deployment failed - check logs for details" 
                    : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get deployment status for {DeploymentId}", deploymentId);
            return StatusCode(500, new { error = "Failed to retrieve deployment status", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all active deployments
    /// </summary>
    [HttpGet("active")]
    public async Task<ActionResult<List<DeploymentStatusResponse>>> GetActiveDeploymentsAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting all active deployments");

            // Get deployments that are in progress from database
            var activeDeployments = await _dbContext.EnvironmentDeployments
                .Where(d => !d.IsDeleted && 
                            d.Status == Data.Entities.DeploymentStatus.InProgress)
                .ToListAsync(cancellationToken);

            var response = activeDeployments
                .Select(d => new DeploymentStatusResponse
                {
                    DeploymentId = d.Id.ToString(),
                    DeploymentName = d.Name,
                    State = d.Status.ToString(),
                    CurrentOperation = "Processing...",
                    StartTime = d.CreatedAt.ToString("o")
                })
                .ToList();

            _logger.LogInformation("Found {Count} active deployments", response.Count);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get active deployments");
            return StatusCode(500, new { error = "Failed to retrieve active deployments", details = ex.Message });
        }
    }

    /// <summary>
    /// Cancel a running deployment
    /// </summary>
    [HttpPost("{deploymentId}/cancel")]
    public async Task<ActionResult> CancelDeploymentAsync(
        Guid deploymentId,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Cancelling deployment {DeploymentId}", deploymentId);

            // Update database status
            var deployment = await _dbContext.EnvironmentDeployments
                .FirstOrDefaultAsync(d => d.Id == deploymentId && !d.IsDeleted, cancellationToken);

            if (deployment == null)
            {
                return NotFound(new { error = $"Deployment {deploymentId} not found" });
            }

            deployment.Status = Data.Entities.DeploymentStatus.Cancelled;
            deployment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(new { message = "Deployment cancelled successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel deployment {DeploymentId}", deploymentId);
            return StatusCode(500, new { error = "Failed to cancel deployment", details = ex.Message });
        }
    }
}

/// <summary>
/// Response model for deployment status
/// </summary>
public class DeploymentStatusResponse
{
    public string DeploymentId { get; set; } = string.Empty;
    public string? DeploymentName { get; set; }
    public string State { get; set; } = string.Empty;
    public int? ProgressPercentage { get; set; }
    public string? CurrentOperation { get; set; }
    public List<DeploymentStepResponse>? Steps { get; set; }
    public List<string>? ResourcesCreated { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? EstimatedCompletion { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Response model for deployment step
/// </summary>
public class DeploymentStepResponse
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Duration { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
}
