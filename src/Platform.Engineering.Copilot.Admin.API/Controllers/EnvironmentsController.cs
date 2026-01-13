using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;
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
    private readonly ILogger<EnvironmentsController> _logger;

    public EnvironmentsController(
        IProvisionedEnvironmentService environmentService,
        IServiceTemplateCatalogService catalogService,
        ILogger<EnvironmentsController> logger)
    {
        _environmentService = environmentService;
        _catalogService = catalogService;
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
