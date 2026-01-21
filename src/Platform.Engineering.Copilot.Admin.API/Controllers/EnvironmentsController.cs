using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Admin.API.Models;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// API controller for Provisioned Environment management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EnvironmentsController : ControllerBase
{
    private readonly IProvisionedEnvironmentService _environmentService;
    private readonly IServiceTemplateCatalogService _catalogService;
    private readonly IEnvironmentActivityRepository _activityRepository;
    private readonly ILogger<EnvironmentsController> _logger;

    public EnvironmentsController(
        IProvisionedEnvironmentService environmentService,
        IServiceTemplateCatalogService catalogService,
        IEnvironmentActivityRepository activityRepository,
        ILogger<EnvironmentsController> logger)
    {
        _environmentService = environmentService;
        _catalogService = catalogService;
        _activityRepository = activityRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get all provisioned environments
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ProvisionedEnvironmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProvisionedEnvironmentDto>>> GetEnvironments(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? templateId = null,
        [FromQuery] EnvironmentStatus? status = null,
        [FromQuery] bool? hasDrift = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var criteria = new EnvironmentSearchCriteria
            {
                SubscriptionId = subscriptionId,
                TemplateId = templateId,
                Status = status,
                HasDrift = hasDrift,
                Skip = skip,
                Take = take
            };

            var environments = await _environmentService.SearchEnvironmentsAsync(criteria, cancellationToken);
            var dtos = environments.Select(MapToDto).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving environments");
            return StatusCode(500, new { error = "Failed to retrieve environments" });
        }
    }

    /// <summary>
    /// Get a specific environment by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ProvisionedEnvironmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProvisionedEnvironmentDto>> GetEnvironment(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var environment = await _environmentService.GetEnvironmentAsync(id, cancellationToken);
            if (environment == null)
                return NotFound(new { error = $"Environment {id} not found" });

            return Ok(MapToDto(environment));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to retrieve environment" });
        }
    }

    /// <summary>
    /// Admin: Manually update environment status and deployment ID.
    /// Use this to sync status when Azure deployment completed outside the normal flow.
    /// </summary>
    [HttpPatch("{id}/status")]
    [ProducesResponseType(typeof(ProvisionedEnvironmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProvisionedEnvironmentDto>> UpdateEnvironmentStatus(
        string id,
        [FromBody] UpdateEnvironmentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var environment = await _environmentService.GetEnvironmentAsync(id, cancellationToken);
            if (environment == null)
                return NotFound(new { error = $"Environment {id} not found" });

            // Update status if provided
            if (!string.IsNullOrEmpty(request.Status) && 
                Enum.TryParse<EnvironmentStatus>(request.Status, true, out var status))
            {
                environment.Status = status;
            }

            // Update status message if provided
            if (request.StatusMessage != null)
            {
                environment.StatusMessage = request.StatusMessage;
            }

            // Update deployment ID if provided
            if (!string.IsNullOrEmpty(request.DeploymentId))
            {
                environment.DeploymentId = request.DeploymentId;
            }

            var updated = await _environmentService.UpdateEnvironmentAsync(
                environment, 
                request.UpdatedBy ?? "admin", 
                cancellationToken);

            _logger.LogInformation("Updated environment {EnvironmentId} status to {Status}", 
                id, environment.Status);

            return Ok(MapToDto(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating environment status {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to update environment status" });
        }
    }

    /// <summary>
    /// Get activity history for an environment
    /// </summary>
    [HttpGet("{id}/activities")]
    [ProducesResponseType(typeof(EnvironmentActivityListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnvironmentActivityListDto>> GetActivities(
        string id,
        [FromQuery] string? activityType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Guid.TryParse(id, out var environmentId))
                return BadRequest(new { error = "Invalid environment ID format" });

            // Verify environment exists
            var environment = await _environmentService.GetEnvironmentAsync(id, cancellationToken);
            if (environment == null)
                return NotFound(new { error = $"Environment {id} not found" });

            var (activities, totalCount) = await _activityRepository.GetByEnvironmentIdPagedAsync(
                environmentId,
                activityType,
                fromDate,
                toDate,
                skip,
                take,
                cancellationToken);

            var result = new EnvironmentActivityListDto
            {
                Items = activities.Select(a => new EnvironmentActivityDto
                {
                    Id = a.Id.ToString(),
                    EnvironmentId = a.EnvironmentId.ToString(),
                    ActivityType = a.ActivityType,
                    Description = a.Description,
                    UserId = a.UserId,
                    UserName = a.UserName,
                    Metadata = string.IsNullOrEmpty(a.Metadata) ? null : 
                        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(a.Metadata),
                    Timestamp = a.Timestamp,
                    Status = a.Status,
                    ErrorMessage = a.ErrorMessage
                }).ToList(),
                TotalCount = totalCount,
                Skip = skip,
                Take = take
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving activities for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to retrieve activities" });
        }
    }

    /// <summary>
    /// Get deployed resources for an environment
    /// </summary>
    [HttpGet("{id}/resources")]
    [ProducesResponseType(typeof(DeployedResourceListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeployedResourceListDto>> GetResources(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get environment with resources
            var environment = await _environmentService.GetEnvironmentAsync(id, cancellationToken);
            if (environment == null)
                return NotFound(new { error = $"Environment {id} not found" });

            // Get resources from the environment (stored as DeployedResources or Resources)
            var resources = environment.DeployedResources ?? environment.Resources ?? new List<Core.Models.ServiceTemplates.DeployedResource>();

            var result = new DeployedResourceListDto
            {
                EnvironmentId = environment.Id,
                EnvironmentName = environment.Name,
                TotalCount = resources.Count,
                Items = resources.Select(r => new DeployedResourceDto
                {
                    Id = Guid.NewGuid().ToString(), // Resources don't have a separate ID in the model
                    EnvironmentId = environment.Id,
                    ResourceId = r.ResourceId,
                    Name = r.Name,
                    ResourceType = r.Type,
                    Location = r.Location,
                    Sku = r.Sku,
                    ProvisioningState = r.ProvisioningState,
                    DeployedAt = r.DeployedAt,
                    AzurePortalUrl = BuildAzurePortalUrl(r.ResourceId, environment.SubscriptionId)
                }).ToList()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving resources for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to retrieve resources" });
        }
    }

    /// <summary>
    /// Sync deployed resources from Azure for an environment.
    /// Queries Azure Resource Graph to find resources tagged with this environment.
    /// </summary>
    [HttpPost("{id}/sync-resources")]
    [ProducesResponseType(typeof(SyncResourcesResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SyncResourcesResultDto>> SyncResourcesFromAzure(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var environment = await _environmentService.GetEnvironmentAsync(id, cancellationToken);
            if (environment == null)
                return NotFound(new { error = $"Environment {id} not found" });

            var result = await _environmentService.SyncResourcesFromAzureAsync(id, cancellationToken);

            return Ok(new SyncResourcesResultDto
            {
                EnvironmentId = result.EnvironmentId,
                EnvironmentName = result.EnvironmentName,
                ResourcesFound = result.ResourcesFound,
                ResourcesAdded = result.ResourcesAdded,
                Message = result.Message,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing resources for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to sync resources from Azure" });
        }
    }

    private static string? BuildAzurePortalUrl(string? resourceId, string? subscriptionId)
    {
        if (string.IsNullOrEmpty(resourceId)) return null;
        
        // For Azure Commercial
        // return $"https://portal.azure.com/#@/resource{resourceId}";
        
        // For Azure Government (primary target)
        return $"https://portal.azure.us/#@/resource{resourceId}";
    }

    /// <summary>
    /// Create a new environment from a template
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateEnvironmentResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateEnvironmentResultDto>> CreateEnvironment(
        [FromBody] CreateEnvironmentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var createRequest = new CreateEnvironmentFromTemplateRequest
            {
                TemplateId = request.TemplateId,
                EnvironmentName = request.EnvironmentName,
                DisplayName = request.DisplayName,
                Description = request.Description,
                ResourceGroup = request.ResourceGroup,
                SubscriptionId = request.SubscriptionId,
                Location = request.Location ?? "eastus",
                Parameters = request.Parameters ?? new Dictionary<string, object>(),
                Tags = request.Tags,
                OwnerEmail = request.OwnerEmail,
                RequestedBy = request.RequestedBy ?? "admin",
                ExpiresAt = request.ExpiresAt,
                AutoDelete = request.AutoDelete
            };

            var result = await _environmentService.CreateFromTemplateAsync(createRequest, cancellationToken);

            var resultDto = new CreateEnvironmentResultDto
            {
                Success = result.Success,
                EnvironmentId = result.EnvironmentId,
                EnvironmentName = result.EnvironmentName,
                DeploymentId = result.DeploymentId,
                Status = result.Status.ToString(),
                Message = result.Message,
                Errors = result.Errors,
                Environment = result.Environment != null ? MapToDto(result.Environment) : null
            };

            if (!result.Success)
            {
                return BadRequest(resultDto);
            }

            _logger.LogInformation("Created environment {EnvironmentId} from template {TemplateId}", 
                result.EnvironmentId, request.TemplateId);

            return CreatedAtAction(
                nameof(GetEnvironment),
                new { id = result.EnvironmentId },
                resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating environment");
            return StatusCode(500, new { error = "Failed to create environment" });
        }
    }

    /// <summary>
    /// Scale an environment
    /// </summary>
    [HttpPost("{id}/scale")]
    [ProducesResponseType(typeof(ScaleResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScaleResultDto>> ScaleEnvironment(
        string id,
        [FromBody] ScaleEnvironmentApiRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scaleRequest = new ScaleEnvironmentRequest
            {
                EnvironmentId = id,
                ScaledBy = request.ScaledBy ?? "admin",
                NodeCount = request.NodeCount,
                ReplicaCount = request.ReplicaCount,
                Sku = request.Sku,
                Tier = request.Tier,
                ScalingParameters = request.Parameters
            };

            var result = await _environmentService.ScaleEnvironmentAsync(scaleRequest, cancellationToken);

            var resultDto = new ScaleResultDto
            {
                Success = result.Success,
                EnvironmentId = result.EnvironmentId,
                Message = result.Message,
                Errors = result.Errors,
                OldValues = result.OldValues,
                NewValues = result.NewValues
            };

            if (!result.Success)
            {
                if (result.Errors?.Any(e => e.Contains("not found")) == true)
                    return NotFound(resultDto);
                return BadRequest(resultDto);
            }

            _logger.LogInformation("Scaled environment {EnvironmentId}", id);
            return Ok(resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scaling environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to scale environment" });
        }
    }

    /// <summary>
    /// Clone an environment
    /// </summary>
    [HttpPost("{id}/clone")]
    [ProducesResponseType(typeof(CreateEnvironmentResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateEnvironmentResultDto>> CloneEnvironment(
        string id,
        [FromBody] CloneEnvironmentRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.CloneEnvironmentAsync(
                id,
                request.NewName,
                request.ClonedBy ?? "admin",
                cancellationToken);

            var resultDto = new CreateEnvironmentResultDto
            {
                Success = result.Success,
                EnvironmentId = result.EnvironmentId,
                EnvironmentName = result.EnvironmentName,
                Status = result.Status.ToString(),
                Message = result.Message,
                Errors = result.Errors,
                Environment = result.Environment != null ? MapToDto(result.Environment) : null
            };

            if (!result.Success)
            {
                if (result.Errors?.Any(e => e.Contains("not found")) == true)
                    return NotFound(resultDto);
                return BadRequest(resultDto);
            }

            _logger.LogInformation("Cloned environment {SourceId} to {NewId}", id, result.EnvironmentId);

            return CreatedAtAction(
                nameof(GetEnvironment),
                new { id = result.EnvironmentId },
                resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cloning environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to clone environment" });
        }
    }

    /// <summary>
    /// Delete an environment
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteEnvironment(
        string id,
        [FromQuery] string deletedBy = "admin",
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _environmentService.DeleteEnvironmentAsync(
                id, deletedBy, force, cancellationToken);

            if (!success)
                return NotFound(new { error = $"Environment {id} not found" });

            _logger.LogInformation("Deleted environment {EnvironmentId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to delete environment" });
        }
    }

    /// <summary>
    /// Reprovision a failed environment (retry deployment)
    /// </summary>
    [HttpPost("{id}/reprovision")]
    [ProducesResponseType(typeof(CreateEnvironmentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateEnvironmentResultDto>> ReprovisionEnvironment(
        string id,
        [FromQuery] string requestedBy = "admin",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.ReprovisionEnvironmentAsync(
                id, requestedBy, cancellationToken);

            var resultDto = new CreateEnvironmentResultDto
            {
                Success = result.Success,
                EnvironmentId = result.EnvironmentId,
                EnvironmentName = result.EnvironmentName,
                Status = result.Status.ToString(),
                Message = result.Success ? "Environment reprovisioned successfully" : "Reprovisioning failed",
                Errors = result.Errors
            };

            if (!result.Success && result.Errors?.Any(e => e.Contains("not found")) == true)
                return NotFound(resultDto);

            if (!result.Success)
                return BadRequest(resultDto);

            _logger.LogInformation("Reprovisioned environment {EnvironmentId}", id);
            return Ok(resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reprovisioning environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to reprovision environment" });
        }
    }

    /// <summary>
    /// Refresh deployment status from Azure for an environment in Provisioning state
    /// </summary>
    [HttpPost("{id}/refresh-status")]
    [ProducesResponseType(typeof(RefreshDeploymentStatusResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RefreshDeploymentStatusResultDto>> RefreshDeploymentStatus(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.RefreshDeploymentStatusAsync(id, cancellationToken);

            var resultDto = new RefreshDeploymentStatusResultDto
            {
                EnvironmentId = result.EnvironmentId,
                EnvironmentName = result.EnvironmentName,
                DeploymentId = result.DeploymentId,
                PreviousStatus = result.PreviousStatus.ToString(),
                CurrentStatus = result.CurrentStatus.ToString(),
                StatusMessage = result.StatusMessage,
                StatusChanged = result.StatusChanged,
                Error = result.Error
            };

            if (!string.IsNullOrEmpty(result.Error) && result.Error.Contains("not found"))
                return NotFound(resultDto);

            _logger.LogInformation("Refreshed deployment status for environment {EnvironmentId}: {Status}", 
                id, result.CurrentStatus);
            return Ok(resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing deployment status for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to refresh deployment status" });
        }
    }

    /// <summary>
    /// Refresh deployment status for all environments in Provisioning state
    /// </summary>
    [HttpPost("refresh-all-provisioning")]
    [ProducesResponseType(typeof(List<RefreshDeploymentStatusResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RefreshDeploymentStatusResultDto>>> RefreshAllProvisioningEnvironments(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var results = await _environmentService.RefreshAllProvisioningEnvironmentsAsync(cancellationToken);

            var resultDtos = results.Select(r => new RefreshDeploymentStatusResultDto
            {
                EnvironmentId = r.EnvironmentId,
                EnvironmentName = r.EnvironmentName,
                DeploymentId = r.DeploymentId,
                PreviousStatus = r.PreviousStatus.ToString(),
                CurrentStatus = r.CurrentStatus.ToString(),
                StatusMessage = r.StatusMessage,
                StatusChanged = r.StatusChanged,
                Error = r.Error
            }).ToList();

            _logger.LogInformation("Refreshed deployment status for {Count} provisioning environments", results.Count);
            return Ok(resultDtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing deployment status for provisioning environments");
            return StatusCode(500, new { error = "Failed to refresh deployment status" });
        }
    }

    /// <summary>
    /// Delete Azure resources for an environment
    /// </summary>
    [HttpPost("{id}/delete-resources")]
    [ProducesResponseType(typeof(DeleteResourcesResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeleteResourcesResultDto>> DeleteAzureResources(
        string id,
        [FromQuery] string deletedBy = "admin",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.DeleteAzureResourcesAsync(
                id, deletedBy, cancellationToken);

            var resultDto = new DeleteResourcesResultDto
            {
                Success = result.Success,
                EnvironmentId = result.EnvironmentId,
                Message = result.Message,
                DeletedResources = result.DeletedResources,
                FailedResources = result.FailedResources,
                Errors = result.Errors,
                TotalResourcesDeleted = result.TotalResourcesDeleted,
                TotalResourcesFailed = result.TotalResourcesFailed
            };

            if (!result.Success && result.Errors?.Any(e => e.Contains("not found")) == true)
                return NotFound(resultDto);

            return Ok(resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Azure resources for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to delete Azure resources" });
        }
    }

    /// <summary>
    /// Get all soft-deleted environments
    /// </summary>
    [HttpGet("deleted")]
    [ProducesResponseType(typeof(List<ProvisionedEnvironmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProvisionedEnvironmentDto>>> GetDeletedEnvironments(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var environments = await _environmentService.GetDeletedEnvironmentsAsync(cancellationToken);
            var dtos = environments.Select(MapToDto).ToList();
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving deleted environments");
            return StatusCode(500, new { error = "Failed to retrieve deleted environments" });
        }
    }

    /// <summary>
    /// Purge a single soft-deleted environment (permanent delete)
    /// </summary>
    [HttpDelete("{id}/purge")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> PurgeEnvironment(
        string id,
        [FromQuery] string purgedBy = "admin",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _environmentService.PurgeEnvironmentAsync(
                id, purgedBy, cancellationToken);

            if (!success)
                return NotFound(new { error = $"Environment {id} not found or not soft-deleted" });

            _logger.LogWarning("Permanently purged environment {EnvironmentId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to purge environment" });
        }
    }

    /// <summary>
    /// Purge all soft-deleted environments (permanent delete)
    /// </summary>
    [HttpDelete("purge-all")]
    [ProducesResponseType(typeof(PurgeAllResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PurgeAllResultDto>> PurgeAllDeletedEnvironments(
        [FromQuery] string purgedBy = "admin",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _environmentService.PurgeAllDeletedEnvironmentsAsync(
                purgedBy, cancellationToken);

            _logger.LogWarning("Permanently purged {Count} soft-deleted environments", count);
            return Ok(new PurgeAllResultDto { PurgedCount = count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purging all deleted environments");
            return StatusCode(500, new { error = "Failed to purge environments" });
        }
    }

    /// <summary>
    /// Detect drift for an environment
    /// </summary>
    [HttpPost("{id}/detect-drift")]
    [ProducesResponseType(typeof(DriftDetectionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriftDetectionResultDto>> DetectDrift(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.DetectDriftAsync(id, cancellationToken);

            var resultDto = new DriftDetectionResultDto
            {
                Success = result.Success,
                EnvironmentId = result.EnvironmentId,
                EnvironmentName = result.EnvironmentName,
                HasDrift = result.HasDrift,
                DriftCount = result.DriftCount,
                DetectedAt = result.DetectedAt,
                DriftItems = result.DriftItems?.Select(d => new DriftItemDto
                {
                    Id = d.Id,
                    ResourceId = d.ResourceId,
                    ResourceName = d.ResourceName,
                    PropertyPath = d.PropertyPath,
                    ExpectedValue = d.ExpectedValue,
                    ActualValue = d.ActualValue,
                    DriftType = d.DriftType,
                    Severity = d.Severity,
                    CanAutoRemediate = d.CanAutoRemediate
                }).ToList()
            };

            if (!result.Success && result.Errors?.Any(e => e.Contains("not found")) == true)
                return NotFound(resultDto);

            return Ok(resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting drift for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to detect drift" });
        }
    }

    /// <summary>
    /// Remediate drift for an environment
    /// </summary>
    [HttpPost("{id}/remediate-drift")]
    [ProducesResponseType(typeof(RemediateDriftResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RemediateDriftResultDto>> RemediateDrift(
        string id,
        [FromBody] RemediateDriftRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _environmentService.RemediateDriftAsync(
                id,
                request.DriftItemIds,
                request.RemediatedBy ?? "admin",
                cancellationToken);

            var resultDto = new RemediateDriftResultDto
            {
                Success = result.Success,
                EnvironmentId = result.EnvironmentId,
                ItemsRemediated = result.ItemsRemediated,
                ItemsFailed = result.ItemsFailed,
                RemainingDriftCount = result.RemainingDriftCount,
                Errors = result.Errors
            };

            if (!result.Success && result.Errors?.Any(e => e.Contains("not found")) == true)
                return NotFound(resultDto);

            return Ok(resultDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error remediating drift for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to remediate drift" });
        }
    }

    /// <summary>
    /// Get environment health
    /// </summary>
    [HttpGet("{id}/health")]
    [ProducesResponseType(typeof(EnvironmentHealthDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnvironmentHealthDto>> GetHealth(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _environmentService.GetHealthAsync(id, cancellationToken);

            var dto = new EnvironmentHealthDto
            {
                EnvironmentId = health.EnvironmentId,
                EnvironmentName = health.EnvironmentName,
                OverallHealth = health.OverallHealth,
                HasDrift = health.HasDrift,
                DriftCount = health.DriftCount,
                EstimatedMonthlyCost = health.EstimatedMonthlyCost ?? 0m,
                LastChecked = health.LastChecked ?? DateTime.UtcNow,
                Issues = health.Issues,
                ResourceHealth = health.ResourceHealth?.Select(r => new ResourceHealthItemDto
                {
                    ResourceId = r.ResourceId,
                    ResourceName = r.ResourceName,
                    ResourceType = r.ResourceType,
                    Health = r.Health,
                    Message = r.Message
                }).ToList()
            };

            if (string.IsNullOrEmpty(dto.EnvironmentId))
                return NotFound(new { error = $"Environment {id} not found" });

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting health for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to get health" });
        }
    }

    /// <summary>
    /// Get status summary for all environments
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(EnvironmentStatusSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EnvironmentStatusSummaryDto>> GetStatusSummary(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await _environmentService.GetStatusSummaryAsync(cancellationToken);

            var dto = new EnvironmentStatusSummaryDto
            {
                TotalEnvironments = summary.TotalEnvironments,
                HealthyCount = summary.HealthyCount,
                DegradedCount = summary.DegradedCount,
                UnhealthyCount = summary.UnhealthyCount,
                RunningEnvironments = summary.RunningEnvironments,
                ProvisioningEnvironments = summary.ProvisioningEnvironments,
                FailedEnvironments = summary.FailedEnvironments,
                EnvironmentsWithDrift = summary.EnvironmentsWithDrift,
                ExpiringWithin7Days = summary.ExpiringWithin7Days,
                TotalEstimatedMonthlyCost = summary.TotalEstimatedMonthlyCost,
                ByTemplate = summary.EnvironmentsByTemplate,
                ByStatus = summary.EnvironmentsByStatus
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting environment status summary");
            return StatusCode(500, new { error = "Failed to get status summary" });
        }
    }

    /// <summary>
    /// Get expiring environments
    /// </summary>
    [HttpGet("expiring")]
    [ProducesResponseType(typeof(List<ProvisionedEnvironmentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ProvisionedEnvironmentDto>>> GetExpiringEnvironments(
        [FromQuery] int withinDays = 7,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var environments = await _environmentService.GetExpiringEnvironmentsAsync(
                withinDays, cancellationToken);
            var dtos = environments.Select(MapToDto).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting expiring environments");
            return StatusCode(500, new { error = "Failed to get expiring environments" });
        }
    }

    /// <summary>
    /// Extend environment expiration
    /// </summary>
    [HttpPost("{id}/extend")]
    [ProducesResponseType(typeof(ProvisionedEnvironmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProvisionedEnvironmentDto>> ExtendExpiration(
        string id,
        [FromBody] ExtendExpirationRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var environment = await _environmentService.ExtendExpirationAsync(
                id,
                request.NewExpiration,
                request.ExtendedBy ?? "admin",
                cancellationToken);

            _logger.LogInformation("Extended expiration for environment {EnvironmentId} to {NewExpiration}", 
                id, request.NewExpiration);

            return Ok(MapToDto(environment));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = $"Environment {id} not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending expiration for environment {EnvironmentId}", id);
            return StatusCode(500, new { error = "Failed to extend expiration" });
        }
    }

    private static ProvisionedEnvironmentDto MapToDto(ProvisionedEnvironment env)
    {
        return new ProvisionedEnvironmentDto
        {
            Id = env.Id,
            Name = env.Name,
            DisplayName = env.DisplayName,
            Description = env.Description,
            TemplateId = env.TemplateId,
            TemplateName = env.TemplateName,
            TemplateVersion = env.TemplateVersion,
            SubscriptionId = env.SubscriptionId,
            ResourceGroup = env.ResourceGroup,
            Location = env.Location,
            Status = env.Status.ToString(),
            StatusMessage = env.StatusMessage,
            HasDrift = env.HasDrift,
            DriftCount = env.DriftCount,
            DriftItems = env.DriftItems?.Select(d => new DriftItemDto
            {
                Id = d.Id,
                ResourceId = d.ResourceId,
                ResourceName = d.ResourceName,
                PropertyPath = d.PropertyPath,
                ExpectedValue = d.ExpectedValue,
                ActualValue = d.ActualValue,
                DriftType = d.DriftType,
                Severity = d.Severity,
                CanAutoRemediate = d.CanAutoRemediate
            }).ToList(),
            EstimatedMonthlyCost = env.EstimatedMonthlyCost ?? 0m,
            OwnerEmail = env.OwnerEmail,
            CreatedAt = env.CreatedAt,
            CreatedBy = env.CreatedBy,
            UpdatedAt = env.UpdatedAt,
            ExpiresAt = env.ExpiresAt,
            AutoDelete = env.AutoDelete,
            Tags = env.Tags,
            ParameterValues = env.ParameterValues
        };
    }
}
