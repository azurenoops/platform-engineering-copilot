using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Admin.API.Models;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// Manages provisioned environment lifecycle: CRUD, scale, clone, drift, health, activities.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class EnvironmentsController : ControllerBase
{
    private readonly IProvisionedEnvironmentService _environmentService;
    private readonly IAzureResourceService _azureResourceService;
    private readonly ILogger<EnvironmentsController> _logger;

    public EnvironmentsController(
        IProvisionedEnvironmentService environmentService,
        IAzureResourceService azureResourceService,
        ILogger<EnvironmentsController> logger)
    {
        _environmentService = environmentService;
        _azureResourceService = azureResourceService;
        _logger = logger;
    }

    /// <summary>GET /api/environments — List with filtering/pagination.</summary>
    [HttpGet]
    public async Task<IActionResult> GetEnvironments(
        [FromQuery] string? subscriptionId = null, [FromQuery] Guid? templateId = null,
        [FromQuery] string? status = null, [FromQuery] bool? hasDrift = null,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var (items, totalCount) = await _environmentService.GetAllAsync(subscriptionId, templateId, status, hasDrift, skip, take, cancellationToken);
            return Ok(new { items = items.Select(MapToSummaryDto), totalCount, skip, take });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing environments");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to list environments." } });
        }
    }

    /// <summary>GET /api/environments/{id} — Get by ID with ETag.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetEnvironment(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var env = await _environmentService.GetByIdAsync(id, cancellationToken);
            if (env is null) return NotFound(new { error = new { code = "NOT_FOUND", message = $"Environment {id} not found." } });

            if (env.RowVersion is not null)
                Response.Headers["ETag"] = $"\"{Convert.ToBase64String(env.RowVersion)}\"";

            return Ok(MapToDetailDto(env));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to get environment." } });
        }
    }

    /// <summary>POST /api/environments — Create from a published template.</summary>
    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> CreateEnvironment([FromBody] CreateEnvironmentRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var env = new ProvisionedEnvironment
            {
                Name = request.EnvironmentName,
                DisplayName = request.DisplayName,
                Description = request.Description,
                TemplateId = request.TemplateId,
                SubscriptionId = request.SubscriptionId,
                ResourceGroup = request.ResourceGroup,
                Location = request.Location ?? "usgovvirginia",
                ParameterValuesJson = request.ParameterValuesJson,
                TagsJson = request.TagsJson,
                OwnerEmail = request.OwnerEmail,
                ExpiresAt = request.ExpiresAt,
                AutoDelete = request.AutoDelete,
                RequestedBy = User.Identity?.Name ?? "unknown"
            };

            var created = await _environmentService.CreateAsync(env, cancellationToken);
            _logger.LogInformation("Created environment {EnvironmentId} '{Name}'", created.Id, created.Name);
            return CreatedAtAction(nameof(GetEnvironment), new { id = created.Id }, MapToDetailDto(created));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = new { code = "TEMPLATE_NOT_FOUND", message = ex.Message } });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating environment");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to create environment." } });
        }
    }

    /// <summary>POST /api/environments/{id}/scale — Scale an environment.</summary>
    [HttpPost("{id:guid}/scale")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> ScaleEnvironment(Guid id, [FromBody] ScaleEnvironmentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.ScaleAsync(id, request.NodeCount, request.ReplicaCount,
                request.Sku, request.Tier, request.AdditionalParameters, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scaling environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to scale environment." } });
        }
    }

    /// <summary>POST /api/environments/{id}/clone — Clone an environment.</summary>
    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> CloneEnvironment(Guid id, [FromBody] CloneEnvironmentRequest request, CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var clone = await _environmentService.CloneAsync(id, request.NewName, request.DisplayName,
                request.ResourceGroup, request.SubscriptionId, cancellationToken);
            return CreatedAtAction(nameof(GetEnvironment), new { id = clone.Id }, MapToDetailDto(clone));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cloning environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to clone environment." } });
        }
    }

    /// <summary>POST /api/environments/{id}/reprovision — Reprovision a failed environment.</summary>
    [HttpPost("{id:guid}/reprovision")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> ReprovisionEnvironment(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var env = await _environmentService.ReprovisionAsync(id, cancellationToken);
            return Ok(MapToDetailDto(env));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reprovisioning environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to reprovision environment." } });
        }
    }

    /// <summary>DELETE /api/environments/{id} — Soft-delete.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> DeleteEnvironment(Guid id, [FromQuery] string deletedBy, [FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deletedBy))
            return BadRequest(new { error = new { code = "VALIDATION_ERROR", message = "deletedBy query parameter is required." } });

        try
        {
            await _environmentService.DeleteAsync(id, deletedBy, force, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to delete environment." } });
        }
    }

    /// <summary>GET /api/environments/deleted — List soft-deleted environments.</summary>
    [HttpGet("deleted")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> GetDeletedEnvironments(CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _environmentService.GetDeletedAsync(cancellationToken);
            return Ok(new { items = items.Select(MapToDetailDto), totalCount = items.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing deleted environments");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to list deleted environments." } });
        }
    }

    /// <summary>DELETE /api/environments/{id}/purge — Permanently delete.</summary>
    [HttpDelete("{id:guid}/purge")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> PurgeEnvironment(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _environmentService.PurgeAsync(id, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to purge environment." } });
        }
    }

    /// <summary>DELETE /api/environments/purge-all — Purge all soft-deleted environments.</summary>
    [HttpDelete("purge-all")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> PurgeAllEnvironments(CancellationToken cancellationToken = default)
    {
        try
        {
            var purgedCount = await _environmentService.PurgeAllAsync(cancellationToken);
            return Ok(new { purgedCount });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging all deleted environments");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to purge environments." } });
        }
    }

    /// <summary>GET /api/environments/{id}/resources — Get deployed resources.</summary>
    [HttpGet("{id:guid}/resources")]
    public async Task<IActionResult> GetResources(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var resources = await _azureResourceService.GetResourcesAsync(id, cancellationToken);
            return Ok(new { resources = resources.Select(r => new
            {
                r.Id, r.AzureResourceId, r.Name, r.Type, r.Location, r.Sku, r.ProvisioningState
            }), totalCount = resources.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resources for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to get resources." } });
        }
    }

    /// <summary>POST /api/environments/{id}/sync-resources — Sync from Azure Resource Graph.</summary>
    [HttpPost("{id:guid}/sync-resources")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> SyncResources(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _azureResourceService.SyncResourcesAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing resources for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to sync resources." } });
        }
    }

    /// <summary>GET /api/environments/{id}/health — Health status.</summary>
    [HttpGet("{id:guid}/health")]
    public async Task<IActionResult> GetHealth(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.GetHealthAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to get health." } });
        }
    }

    /// <summary>GET /api/environments/{id}/activities — Paginated activity history.</summary>
    [HttpGet("{id:guid}/activities")]
    public async Task<IActionResult> GetActivities(Guid id, [FromQuery] int skip = 0, [FromQuery] int take = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.GetActivitiesAsync(id, skip, take, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting activities for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to get activities." } });
        }
    }

    /// <summary>GET /api/environments/summary — Aggregate dashboard summary.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.GetSummaryAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting environment summary");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to get summary." } });
        }
    }

    /// <summary>GET /api/environments/expiring — List environments expiring soon.</summary>
    [HttpGet("expiring")]
    public async Task<IActionResult> GetExpiring([FromQuery] int withinDays = 7, CancellationToken cancellationToken = default)
    {
        try
        {
            var items = await _environmentService.GetExpiringAsync(withinDays, cancellationToken);
            return Ok(new { items = items.Select(MapToSummaryDto), totalCount = items.Count, withinDays });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring environments");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to get expiring environments." } });
        }
    }

    /// <summary>POST /api/environments/{id}/extend — Extend expiration date.</summary>
    [HttpPost("{id:guid}/extend")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> ExtendExpiration(Guid id, [FromBody] ExtendExpirationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var env = await _environmentService.ExtendExpirationAsync(id, request.NewExpiresAt, cancellationToken);
            return Ok(MapToDetailDto(env));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending expiration for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to extend expiration." } });
        }
    }

    /// <summary>POST /api/environments/{id}/detect-drift — Detect configuration drift.</summary>
    [HttpPost("{id:guid}/detect-drift")]
    public async Task<IActionResult> DetectDrift(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _azureResourceService.DetectDriftAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting drift for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to detect drift." } });
        }
    }

    /// <summary>POST /api/environments/{id}/remediate-drift — Fix drift items.</summary>
    [HttpPost("{id:guid}/remediate-drift")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> RemediateDrift(Guid id, [FromBody] RemediateDriftRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _azureResourceService.RemediateDriftAsync(id, request?.DriftItemIds, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error remediating drift for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to remediate drift." } });
        }
    }

    /// <summary>POST /api/environments/{id}/refresh-status — Refresh deployment status from Azure.</summary>
    [HttpPost("{id:guid}/refresh-status")]
    public async Task<IActionResult> RefreshStatus(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.RefreshStatusAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing status for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to refresh status." } });
        }
    }

    /// <summary>POST /api/environments/refresh-all-provisioning — Bulk-refresh.</summary>
    [HttpPost("refresh-all-provisioning")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> RefreshAllProvisioning(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.RefreshAllProvisioningAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing all provisioning environments");
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to refresh provisioning statuses." } });
        }
    }

    /// <summary>PATCH /api/environments/{id}/status — Manual status override.</summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        try
        {
            var env = await _environmentService.UpdateStatusAsync(id, request.Status, request.Reason, cancellationToken);
            return Ok(MapToDetailDto(env));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = new { code = "INVALID_OPERATION", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to update status." } });
        }
    }

    /// <summary>POST /api/environments/{id}/delete-resources — Delete Azure resources.</summary>
    [HttpPost("{id:guid}/delete-resources")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> DeleteResources(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _azureResourceService.DeleteResourcesAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = new { code = "NOT_FOUND", message = ex.Message } }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting resources for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = new { code = "INTERNAL_ERROR", message = "Failed to delete resources." } });
        }
    }

    private static object MapToSummaryDto(ProvisionedEnvironment e) => new
    {
        e.Id, e.Name, e.DisplayName, e.TemplateName, e.SubscriptionId, e.ResourceGroup, e.Location,
        status = e.Status.ToString(), e.HasDrift, e.DriftCount, e.EstimatedMonthlyCost,
        e.OwnerEmail, e.ExpiresAt, e.CreatedAt, e.UpdatedAt
    };

    private static object MapToDetailDto(ProvisionedEnvironment e) => new
    {
        e.Id, e.Name, e.DisplayName, e.Description, e.TemplateId, e.TemplateName,
        e.SubscriptionId, e.ResourceGroup, e.Location,
        status = e.Status.ToString(), e.StatusMessage, e.DeploymentId,
        e.ParameterValuesJson, e.TagsJson, e.HasDrift, e.DriftCount,
        e.EstimatedMonthlyCost, e.OwnerEmail, e.ExpiresAt, e.AutoDelete,
        e.DeploymentScope, e.RequestedBy, e.IsDeleted, e.DeletedAt, e.DeletedBy,
        e.CreatedAt, e.UpdatedAt,
        etag = e.RowVersion is not null ? Convert.ToBase64String(e.RowVersion) : null
    };
}
