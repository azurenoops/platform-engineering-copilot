using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;
using Platform.Engineering.Copilot.Data.Entities;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Admin.Models;

namespace Platform.Engineering.Copilot.Admin.Controllers;

/// <summary>
/// Admin API for environment lifecycle management using EnvironmentManagementEngine
/// </summary>
[ApiController]
[Route("api/admin/environments")]
[Produces("application/json")]
public class EnvironmentAdminController : ControllerBase
{
    private readonly ILogger<EnvironmentAdminController> _logger;
    private readonly IEnvironmentManagementEngine _environmentEngine;
    private readonly IAtoComplianceEngine? _complianceEngine;
    private readonly EnvironmentStorageService _environmentStorage;
    private readonly IDeploymentOrchestrationService _deploymentOrchestrator;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public EnvironmentAdminController(
        ILogger<EnvironmentAdminController> logger,
        IEnvironmentManagementEngine environmentEngine,
        EnvironmentStorageService environmentStorage,
        IDeploymentOrchestrationService deploymentOrchestrator,
        IServiceScopeFactory serviceScopeFactory,
        IAtoComplianceEngine? complianceEngine = null)
    {
        _logger = logger;
        _environmentEngine = environmentEngine;
        _complianceEngine = complianceEngine;
        _environmentStorage = environmentStorage;
        _deploymentOrchestrator = deploymentOrchestrator;
        _serviceScopeFactory = serviceScopeFactory;
    }

    // ========== LIFECYCLE OPERATIONS ==========

    /// <summary>
    /// List all environments with optional filtering
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(EnvironmentListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<EnvironmentListResponse>> ListEnvironments(
        [FromQuery] string? resourceGroup = null,
        [FromQuery] string? environmentType = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin API: Listing environments (ResourceGroup: {RG}, Type: {Type}, Status: {Status})", 
            resourceGroup, environmentType, status);

        try
        {
            // Parse status if provided
            Data.Entities.DeploymentStatus? deploymentStatus = null;
            if (!string.IsNullOrEmpty(status) && Enum.TryParse<Data.Entities.DeploymentStatus>(status, true, out var parsedStatus))
            {
                deploymentStatus = parsedStatus;
            }

            var environments = await _environmentStorage.ListEnvironmentsAsync(
                environmentType, 
                resourceGroup, 
                deploymentStatus, 
                cancellationToken);

            var response = new EnvironmentListResponse
            {
                Environments = environments.Select(e => new EnvironmentResponse
                {
                    Id = e.Id.ToString(),
                    Name = e.Name,
                    TemplateId = e.TemplateId?.ToString() ?? string.Empty,
                    TemplateName = e.Template?.Name ?? "Unknown",
                    ResourceGroup = e.ResourceGroupName,
                    Location = e.Location ?? string.Empty,
                    Status = e.Status.ToString(),
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt,
                    DeployedBy = e.DeployedBy ?? "System",
                    Tags = string.IsNullOrEmpty(e.Tags) 
                        ? null 
                        : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(e.Tags)
                }).ToList(),
                TotalCount = environments.Count,
                FilteredBy = !string.IsNullOrEmpty(resourceGroup) || !string.IsNullOrEmpty(environmentType) || !string.IsNullOrEmpty(status)
                    ? $"ResourceGroup={resourceGroup}, Type={environmentType}, Status={status}"
                    : null
            };

            _logger.LogInformation("Admin API: Found {Count} environments", response.TotalCount);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing environments");
            return StatusCode(500, new { error = ex.Message, details = ex.ToString() });
        }
    }

    /// <summary>
    /// Create a new environment from a template
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EnvironmentCreationResult), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EnvironmentCreationResult>> CreateEnvironment(
        [FromBody] CreateEnvironmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Creating environment {Name}", request.EnvironmentName);

        try
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(request.EnvironmentName))
            {
                return BadRequest(new { error = "Environment name is required" });
            }
            
            if (string.IsNullOrWhiteSpace(request.TemplateId))
            {
                return BadRequest(new { error = "Template ID is required" });
            }

            // Map AdminModels request to Core request
            var envTypeValue = Enum.TryParse<EnvironmentType>(request.EnvironmentType, out var parsedEnvType) 
                ? parsedEnvType 
                : EnvironmentType.Unknown;
            
            var engineRequest = new EnvironmentCreationRequest
            {
                Name = request.EnvironmentName,
                Type = envTypeValue,
                ResourceGroup = request.ResourceGroup,
                Location = request.Location,
                SubscriptionId = request.SubscriptionId,
                Tags = request.Tags ?? new Dictionary<string, string>(),
                EnableMonitoring = request.EnableMonitoring,
                EnableLogging = request.EnableLogging,
                TemplateId = request.TemplateId,
                TemplateParameters = request.Parameters
            };

            // Generate deployment ID for tracking
            var deploymentId = Guid.NewGuid();
            var deploymentIdString = deploymentId.ToString();
            
            // Create initial environment record with "Deploying" status BEFORE starting deployment
            // This allows the UI to show the environment immediately
            try
            {
                var initialResult = new EnvironmentCreationResult
                {
                    Success = true,
                    EnvironmentId = deploymentIdString,
                    EnvironmentName = request.EnvironmentName,
                    ResourceGroup = request.ResourceGroup,
                    Type = envTypeValue,
                    Status = "Deploying",
                    DeploymentId = deploymentIdString,
                    CreatedAt = DateTime.UtcNow,
                    Message = "Deployment in progress..."
                };

                await _environmentStorage.StoreEnvironmentAsync(
                    initialResult,
                    request.TemplateId,
                    request.SubscriptionId,
                    request.Location,
                    cancellationToken);
                
                _logger.LogInformation("Created initial environment record for {Name} with deployment {DeploymentId}", 
                    request.EnvironmentName, deploymentIdString);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create initial environment record for {Name}", request.EnvironmentName);
                return BadRequest(new { error = $"Failed to create environment record: {ex.Message}" });
            }
            
            // Start deployment in background with a new DI scope - don't await
            _ = Task.Run(async () =>
            {
                // Create a new service scope for the background task
                // This ensures we get fresh instances of scoped services (like DbContext)
                using var scope = _serviceScopeFactory.CreateScope();
                var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<EnvironmentAdminController>>();
                var scopedEnvironmentEngine = scope.ServiceProvider.GetRequiredService<IEnvironmentManagementEngine>();
                var scopedEnvironmentStorage = scope.ServiceProvider.GetRequiredService<EnvironmentStorageService>();
                
                try
                {
                    scopedLogger.LogInformation("Starting background deployment {DeploymentId} for environment {Name}", 
                        deploymentIdString, request.EnvironmentName);
                    
                    var result = await scopedEnvironmentEngine.CreateEnvironmentAsync(engineRequest, CancellationToken.None);

                    // Update the environment record with final status
                    try
                    {
                        result.DeploymentId = deploymentIdString; // Preserve deployment ID
                        var finalStatus = result.Success 
                            ? Platform.Engineering.Copilot.Data.Entities.DeploymentStatus.Succeeded 
                            : Platform.Engineering.Copilot.Data.Entities.DeploymentStatus.Failed;
                        
                        await scopedEnvironmentStorage.UpdateEnvironmentStatusAsync(
                            deploymentId,
                            finalStatus,
                            result.Success ? null : result.ErrorMessage,
                            CancellationToken.None);
                        
                        scopedLogger.LogInformation("Environment {Name} updated with final status: {Status}", 
                            request.EnvironmentName, finalStatus);
                    }
                    catch (Exception ex)
                    {
                        scopedLogger.LogError(ex, "Failed to update environment {Name} status", request.EnvironmentName);
                    }

                    if (result.Success)
                    {
                        scopedLogger.LogInformation("Background deployment {DeploymentId} completed successfully for {Name}", 
                            deploymentIdString, request.EnvironmentName);
                    }
                    else
                    {
                        scopedLogger.LogError("Background deployment {DeploymentId} failed for {Name}: {Error}", 
                            deploymentIdString, request.EnvironmentName, result.ErrorMessage);
                    }
                }
                catch (Exception ex)
                {
                    scopedLogger.LogError(ex, "Background deployment {DeploymentId} exception for {Name}", 
                        deploymentIdString, request.EnvironmentName);
                    
                    // Update environment record with error status
                    try
                    {
                        await scopedEnvironmentStorage.UpdateEnvironmentStatusAsync(
                            deploymentId,
                            Platform.Engineering.Copilot.Data.Entities.DeploymentStatus.Failed,
                            ex.Message,
                            CancellationToken.None);
                    }
                    catch (Exception updateEx)
                    {
                        scopedLogger.LogError(updateEx, "Failed to update environment status after exception");
                    }
                }
            }, CancellationToken.None);

            // Return immediately with deployment ID for client-side polling
            var asyncResult = new EnvironmentCreationResult
            {
                Success = true,
                EnvironmentName = request.EnvironmentName,
                DeploymentId = deploymentIdString,
                Status = "Deploying",
                Message = $"Deployment {deploymentId} started for environment {request.EnvironmentName}"
            };

            return Accepted(asyncResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating environment {Name}", request.EnvironmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Clone an existing environment
    /// </summary>
    [HttpPost("{environmentName}/clone")]
    [ProducesResponseType(typeof(EnvironmentCloneResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EnvironmentCloneResult>> CloneEnvironment(
        string environmentName,
        [FromBody] CloneEnvironmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Cloning environment {Name}", environmentName);

        try
        {
            var engineRequest = new EnvironmentCloneRequest
            {
                SourceEnvironment = environmentName,
                SourceResourceGroup = request.SourceResourceGroup,
                TargetEnvironments = new List<string> { request.TargetEnvironmentName },
                TargetResourceGroup = request.TargetResourceGroup,
                PreserveData = request.IncludeData,
                CloneConfiguration = request.CopyNetworkConfiguration
            };

            var result = await _environmentEngine.CloneEnvironmentAsync(engineRequest, cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return CreatedAtAction(
                nameof(GetEnvironmentStatus),
                new { environmentName = request.TargetEnvironmentName, resourceGroup = request.TargetResourceGroup },
                result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cloning environment {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete an environment
    /// </summary>
    [HttpDelete("{environmentName}")]
    [ProducesResponseType(typeof(EnvironmentDeletionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnvironmentDeletionResult>> DeleteEnvironment(
        string environmentName,
        [FromQuery] string resourceGroup,
        [FromQuery] bool createBackup = true,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin API: Deleting environment {Name}, Backup: {Backup}", 
            environmentName, createBackup);

        try
        {
            // Get environment from database to retrieve subscription ID
            var environment = await _environmentStorage.GetEnvironmentByNameAsync(
                environmentName, resourceGroup, cancellationToken);
            
            if (environment == null)
            {
                _logger.LogWarning("Environment {Name} not found in {ResourceGroup}", environmentName, resourceGroup);
                return NotFound(new { error = $"Environment '{environmentName}' not found in resource group '{resourceGroup}'" });
            }

            var result = await _environmentEngine.DeleteEnvironmentAsync(
                environmentName, 
                resourceGroup, 
                createBackup,
                environment.SubscriptionId,
                cancellationToken);

            if (!result.Success)
            {
                if (result.ErrorMessage?.Contains("not found") == true)
                {
                    return NotFound(new { error = result.ErrorMessage });
                }
                return BadRequest(new { error = result.ErrorMessage });
            }

            // Delete from database after successful Azure deletion
            try
            {
                await _environmentStorage.DeleteEnvironmentAsync(environment.Id, cancellationToken);
                _logger.LogInformation("Environment {Name} removed from database", environmentName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete environment {Name} from database", environmentName);
                // Continue - Azure resources are deleted, just log the database error
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting environment {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Bulk delete environments based on filter
    /// </summary>
    [HttpPost("bulk-delete")]
    [ProducesResponseType(typeof(BulkOperationResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkOperationResult>> BulkDeleteEnvironments(
        [FromBody] BulkDeleteEnvironmentsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Bulk deleting environments with filter");

        try
        {
            var filter = new EnvironmentFilter
            {
                SubscriptionId = request.SubscriptionIds?.FirstOrDefault(),
                ResourceGroups = request.ResourceGroups,
                Locations = request.Locations,
                Tags = request.Tags,
                AgeInDays = request.MaxAgeInDays
            };

            var settings = new BulkOperationSettings
            {
                SafetyChecks = !request.ContinueOnError,
                CreateBackup = request.CreateBackups,
                ConfirmationRequired = request.RequireConfirmation,
                MaxResources = request.MaxConcurrentOperations ?? 10
            };

            var result = await _environmentEngine.BulkDeleteEnvironmentsAsync(
                filter, 
                settings, 
                cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bulk delete operation");
            return BadRequest(new { error = ex.Message });
        }
    }

    // ========== MONITORING & HEALTH ==========

    /// <summary>
    /// Get environment details by name
    /// </summary>
    [HttpGet("{environmentName}")]
    [ProducesResponseType(typeof(EnvironmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnvironmentResponse>> GetEnvironment(
        string environmentName,
        [FromQuery] string resourceGroup,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin API: Getting environment {Name} from {ResourceGroup}", 
            environmentName, resourceGroup);

        try
        {
            var environment = await _environmentStorage.GetEnvironmentByNameAsync(
                environmentName,
                resourceGroup,
                cancellationToken);

            if (environment == null)
            {
                return NotFound(new { error = $"Environment '{environmentName}' not found in resource group '{resourceGroup}'" });
            }

            var response = new EnvironmentResponse
            {
                Id = environment.Id.ToString(),
                Name = environment.Name,
                TemplateId = environment.TemplateId?.ToString() ?? string.Empty,
                TemplateName = environment.Template?.Name ?? "Unknown",
                ResourceGroup = environment.ResourceGroupName,
                Location = environment.Location ?? string.Empty,
                Status = environment.Status.ToString(),
                CreatedAt = environment.CreatedAt,
                UpdatedAt = environment.UpdatedAt,
                DeployedBy = environment.DeployedBy ?? "System",
                Tags = string.IsNullOrEmpty(environment.Tags)
                    ? null
                    : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(environment.Tags)
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting environment {Name}", environmentName);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get environment health status
    /// </summary>
    [HttpGet("{environmentName}/health")]
    [ProducesResponseType(typeof(EnvironmentHealthReport), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnvironmentHealthReport>> GetEnvironmentHealth(
        string environmentName,
        [FromQuery] string resourceGroup,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Getting health for environment {Name}", environmentName);

        try
        {
            var health = await _environmentEngine.GetEnvironmentHealthAsync(
                environmentName, 
                resourceGroup, 
                cancellationToken);

            if (health == null)
            {
                return NotFound(new { error = $"Environment {environmentName} not found" });
            }

            return Ok(health);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting environment health for {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get environment metrics
    /// </summary>
    [HttpGet("{environmentName}/metrics")]
    [ProducesResponseType(typeof(Core.Models.EnvironmentManagement.EnvironmentMetrics), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Core.Models.EnvironmentManagement.EnvironmentMetrics>> GetEnvironmentMetrics(
        string environmentName,
        [FromQuery] string resourceGroup,
        [FromQuery] int durationHours = 24,
        [FromQuery] string aggregation = "Average",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin API: Getting metrics for environment {Name}, Duration: {Hours}h", 
            environmentName, durationHours);

        try
        {
            var metricsRequest = new MetricsRequest
            {
                TimeRange = $"{durationHours}h",
                MetricTypes = new List<string> { "cpu", "memory", "network", "requests" },
                Aggregation = aggregation
            };

            var metrics = await _environmentEngine.GetEnvironmentMetricsAsync(
                environmentName,
                resourceGroup,
                metricsRequest,
                cancellationToken);

            if (metrics == null)
            {
                return NotFound(new { error = $"Environment {environmentName} not found or no metrics available" });
            }

            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting metrics for environment {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get environment status and configuration
    /// </summary>
    [HttpGet("{environmentName}/status")]
    [ProducesResponseType(typeof(EnvironmentStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnvironmentStatus>> GetEnvironmentStatus(
        string environmentName,
        [FromQuery] string resourceGroup,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Getting status for environment {Name}", environmentName);

        try
        {
            // Get the environment from database to retrieve subscription ID
            var environment = await _environmentStorage.GetEnvironmentByNameAsync(
                environmentName,
                resourceGroup,
                cancellationToken);

            if (environment == null)
            {
                return NotFound(new { error = $"Environment {environmentName} not found in resource group {resourceGroup}" });
            }

            var status = await _environmentEngine.GetEnvironmentStatusAsync(
                environmentName,
                resourceGroup,
                environment.SubscriptionId,
                cancellationToken);

            if (status == null)
            {
                return NotFound(new { error = $"Environment {environmentName} not found" });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status for environment {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    // ========== OPERATIONS ==========

    /// <summary>
    /// Scale environment (manual or auto-scaling)
    /// </summary>
    [HttpPost("{environmentName}/scale")]
    [ProducesResponseType(typeof(ScalingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScalingResult>> ScaleEnvironment(
        string environmentName,
        [FromQuery] string resourceGroup,
        [FromBody] ScaleEnvironmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Scaling environment {Name} to {Replicas} replicas", 
            environmentName, request.TargetReplicas);

        try
        {
            var scaleSettings = new ScaleSettings
            {
                TargetReplicas = request.TargetReplicas,
                AutoScaling = request.AutoScalingEnabled,
                MinReplicas = request.MinReplicas,
                MaxReplicas = request.MaxReplicas,
                TargetCpuUtilization = request.TargetCpuUtilization,
                TargetMemoryUtilization = request.TargetMemoryUtilization
            };

            var result = await _environmentEngine.ScaleEnvironmentAsync(
                environmentName,
                resourceGroup,
                scaleSettings,
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scaling environment {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Discover all environments across subscriptions
    /// </summary>
    [HttpGet("discover")]
    [ProducesResponseType(typeof(List<EnvironmentDiscoveryResult>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<EnvironmentDiscoveryResult>>> DiscoverEnvironments(
        [FromQuery] string? subscriptionId = null,
        [FromQuery] string? resourceGroup = null,
        [FromQuery] string? location = null,
        [FromQuery] string? environmentType = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin API: Discovering environments");

        try
        {
            var filter = new EnvironmentFilter
            {
                SubscriptionId = subscriptionId,
                ResourceGroups = resourceGroup != null ? new List<string> { resourceGroup } : null,
                Locations = location != null ? new List<string> { location } : null,
                Type = !string.IsNullOrEmpty(environmentType) && Enum.TryParse<EnvironmentType>(environmentType, out var envType) 
                    ? envType 
                    : (EnvironmentType?)null
            };

            var environments = await _environmentEngine.DiscoverEnvironmentsAsync(filter, cancellationToken);

            return Ok(new
            {
                count = environments.Count,
                environments
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering environments");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Migrate environment to different region or subscription
    /// </summary>
    [HttpPost("{environmentName}/migrate")]
    [ProducesResponseType(typeof(MigrationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MigrationResult>> MigrateEnvironment(
        string environmentName,
        [FromBody] MigrateEnvironmentRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Migrating environment {Name} to {Location}", 
            environmentName, request.TargetLocation);

        try
        {
            var migrationRequest = new MigrationRequest
            {
                SourceEnvironment = environmentName,
                SourceResourceGroup = request.SourceResourceGroup,
                TargetEnvironment = request.TargetEnvironmentName ?? environmentName,
                TargetResourceGroup = request.TargetResourceGroup,
                TargetSubscriptionId = request.TargetSubscriptionId,
                TargetLocation = request.TargetLocation,
                MigrateData = request.IncludeData,
                ValidateBeforeMigration = !request.ValidationOnly
            };

            var result = await _environmentEngine.MigrateEnvironmentAsync(migrationRequest, cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new { error = result.ErrorMessage });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error migrating environment {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Clean up old or unused environments
    /// </summary>
    [HttpPost("cleanup")]
    [ProducesResponseType(typeof(CleanupResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<CleanupResult>> CleanupOldEnvironments(
        [FromBody] CleanupEnvironmentsRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Cleaning up old environments");

        try
        {
            var policy = new CleanupPolicy
            {
                MinimumAgeInDays = request.MaxAgeInDays,
                OnlyUnusedEnvironments = request.DeleteIfNoActivity,
                CreateBackup = request.DryRun ? false : true,
                ExcludedResourceGroups = request.ExcludeEnvironmentTypes
            };

            var result = await _environmentEngine.CleanupOldEnvironmentsAsync(policy, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in cleanup operation");
            return BadRequest(new { error = ex.Message });
        }
    }

    // ========== COMPLIANCE & SECURITY ==========

    /// <summary>
    /// Trigger compliance scan for environment using IAtoComplianceEngine
    /// </summary>
    [HttpPost("{environmentName}/compliance/scan")]
    [ProducesResponseType(typeof(AtoComplianceAssessment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AtoComplianceAssessment>> TriggerComplianceScan(
        string environmentName,
        [FromQuery] string subscriptionId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Triggering compliance scan for environment {Name}", environmentName);

        try
        {
            var assessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                subscriptionId,
                null,
                cancellationToken);

            return Ok(assessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running compliance scan for environment {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get continuous compliance status using IAtoComplianceEngine
    /// </summary>
    [HttpGet("{environmentName}/compliance/status")]
    [ProducesResponseType(typeof(ContinuousComplianceStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<ContinuousComplianceStatus>> GetComplianceStatus(
        string environmentName,
        [FromQuery] string subscriptionId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Getting compliance status for environment {Name}", environmentName);

        try
        {
            var status = await _complianceEngine.GetContinuousComplianceStatusAsync(
                subscriptionId,
                cancellationToken);

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting compliance status for environment {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get risk assessment for environment using IAtoComplianceEngine
    /// </summary>
    [HttpGet("{environmentName}/compliance/risk")]
    [ProducesResponseType(typeof(RiskAssessment), StatusCodes.Status200OK)]
    public async Task<ActionResult<RiskAssessment>> GetRiskAssessment(
        string environmentName,
        [FromQuery] string subscriptionId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Getting risk assessment for environment {Name}", environmentName);

        try
        {
            var riskAssessment = await _complianceEngine.PerformRiskAssessmentAsync(
                subscriptionId,
                cancellationToken);

            return Ok(riskAssessment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting risk assessment for environment {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Collect compliance evidence using IAtoComplianceEngine
    /// </summary>
    [HttpPost("{environmentName}/compliance/evidence")]
    [ProducesResponseType(typeof(EvidencePackage), StatusCodes.Status200OK)]
    public async Task<ActionResult<EvidencePackage>> CollectComplianceEvidence(
        string environmentName,
        [FromQuery] string subscriptionId,
        [FromQuery] string controlFamily,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Collecting compliance evidence for environment {Name}, Control Family: {Family}", 
            environmentName, controlFamily);

        try
        {
            var evidence = await _complianceEngine.CollectComplianceEvidenceAsync(
                subscriptionId,
                controlFamily,
                null,
                cancellationToken);

            return Ok(evidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting compliance evidence for environment {Name}", environmentName);
            return BadRequest(new { error = ex.Message });
        }
    }

    // ========== INFRASTRUCTURE PROVISIONING ==========
}

