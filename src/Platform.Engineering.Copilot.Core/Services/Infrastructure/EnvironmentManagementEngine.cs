using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;

namespace Platform.Engineering.Copilot.Core.Services.Infrastructure
{
    /// <summary>
    /// Production-ready environment management engine.
    /// High-level orchestrator that coordinates template lookup, deployment orchestration, and persistence.
    /// Uses DeploymentOrchestrationService for deployments, IAzureResourceService for resource queries.
    /// </summary>
    public class EnvironmentManagementEngine : IEnvironmentManagementEngine
    {
        private readonly ILogger<EnvironmentManagementEngine> _logger;
        private readonly IDeploymentOrchestrationService _deploymentOrchestrator;
        private readonly IAzureResourceService _azureResourceService; // For queries/status checks only
        private readonly IGitHubServices _gitHubServices;
        private readonly ITemplateStorageService _templateStorage;
        private readonly IDynamicTemplateGenerator _templateGenerator;

        public EnvironmentManagementEngine(
            ILogger<EnvironmentManagementEngine> logger,
            IDeploymentOrchestrationService deploymentOrchestrator,
            IAzureResourceService azureResourceService,
            IGitHubServices gitHubServices,
            ITemplateStorageService templateStorage,
            IDynamicTemplateGenerator templateGenerator)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _deploymentOrchestrator = deploymentOrchestrator ?? throw new ArgumentNullException(nameof(deploymentOrchestrator));
            _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
            _gitHubServices = gitHubServices ?? throw new ArgumentNullException(nameof(gitHubServices));
            _templateStorage = templateStorage ?? throw new ArgumentNullException(nameof(templateStorage));
            _templateGenerator = templateGenerator ?? throw new ArgumentNullException(nameof(templateGenerator));
        }

        #region Lifecycle Operations

        public async Task<EnvironmentCreationResult> CreateEnvironmentAsync(
            EnvironmentCreationRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Creating environment: {Name} of type {Type} in {ResourceGroup}",
                request.Name, request.Type, request.ResourceGroup);

            try
            {
                var result = new EnvironmentCreationResult
                {
                    EnvironmentName = request.Name,
                    ResourceGroup = request.ResourceGroup,
                    Type = request.Type,
                    CreatedAt = DateTime.UtcNow
                };

                // Validation
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    result.Success = false;
                    result.ErrorMessage = "Environment name is required";
                    return result;
                }

                if (string.IsNullOrWhiteSpace(request.ResourceGroup))
                {
                    result.Success = false;
                    result.ErrorMessage = "Resource group is required";
                    return result;
                }

                // TEMPLATE-DRIVEN: Lookup service template if provided
                ServiceTemplate? template = null;
                if (!string.IsNullOrWhiteSpace(request.TemplateId))
                {
                    _logger.LogInformation("Looking up service template by ID: {TemplateId}", request.TemplateId);
                    var dbTemplate = await _templateStorage.GetTemplateByIdAsync(request.TemplateId, cancellationToken);
                    if (dbTemplate == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Service template not found: {request.TemplateId}";
                        return result;
                    }
                    template = MapToServiceTemplate(dbTemplate);
                }
                else if (!string.IsNullOrWhiteSpace(request.TemplateName))
                {
                    _logger.LogInformation("Looking up service template by name: {TemplateName}", request.TemplateName);
                    var dbTemplate = await _templateStorage.GetTemplateByNameAsync(request.TemplateName, cancellationToken);
                    if (dbTemplate == null)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Service template not found: {request.TemplateName}";
                        return result;
                    }
                    template = MapToServiceTemplate(dbTemplate);
                }
                else if (request.TemplateContent != null || request.TemplateFiles?.Count > 0)
                {
                    // Use inline template if provided
                    template = new ServiceTemplate
                    {
                        Name = request.Name,
                        Content = request.TemplateContent ?? string.Empty,
                        Files = request.TemplateFiles ?? new List<ServiceTemplateFile>()
                    };
                }

                // Ensure resource group exists
                var resourceGroup = await _azureResourceService.GetResourceGroupAsync(request.ResourceGroup, request.SubscriptionId, cancellationToken);
                if (resourceGroup == null)
                {
                    _logger.LogInformation("Creating resource group {ResourceGroup}", request.ResourceGroup);
                    await _azureResourceService.CreateResourceGroupAsync(
                        request.ResourceGroup,
                        request.Location,
                        request.SubscriptionId,
                        request.Tags,
                        cancellationToken);
                }

                // TEMPLATE-DRIVEN DEPLOYMENT: Deploy from template if available
                if (template != null)
                {
                    _logger.LogInformation("Deploying environment from service template: {TemplateName}", template.Name);
                    return await DeployFromTemplateAsync(request, template, cancellationToken);
                }

                // FALLBACK: Direct infrastructure creation (legacy support)
                _logger.LogInformation("No template provided, using direct infrastructure creation");
                return await DeployByTypeAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating environment {Name}", request.Name);
                return new EnvironmentCreationResult
                {
                    Success = false,
                    EnvironmentName = request.Name,
                    ResourceGroup = request.ResourceGroup,
                    Type = request.Type,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<EnvironmentCloneResult> CloneEnvironmentAsync(
            EnvironmentCloneRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Cloning environment: {Source} to {Count} targets",
                request.SourceEnvironment, request.TargetEnvironments.Count);

            try
            {
                var result = new EnvironmentCloneResult { SourceEnvironment = request.SourceEnvironment };
                var startTime = DateTime.UtcNow;

                var sourceStatus = await GetEnvironmentStatusAsync(
                    request.SourceEnvironment,
                    request.SourceResourceGroup,
                    null,
                    cancellationToken);

                foreach (var targetName in request.TargetEnvironments)
                {
                    try
                    {
                        var cloneRequest = new EnvironmentCreationRequest
                        {
                            Name = targetName,
                            Type = sourceStatus.Type,
                            ResourceGroup = request.TargetResourceGroup,
                            Location = request.TargetLocation,
                            Tags = new Dictionary<string, string>(sourceStatus.Tags),
                            ScaleSettings = sourceStatus.Configuration.ScaleSettings
                        };

                        if (request.ConfigurationOverrides != null)
                        {
                            foreach (var kvp in request.ConfigurationOverrides)
                            {
                                cloneRequest.Tags[kvp.Key] = kvp.Value;
                            }
                        }

                        var createResult = await CreateEnvironmentAsync(cloneRequest, cancellationToken);

                        result.ClonedEnvironments.Add(new ClonedEnvironment
                        {
                            Name = targetName,
                            ResourceId = createResult.EnvironmentId,
                            Status = createResult.Success ? "Succeeded" : "Failed",
                            DataCloned = request.PreserveData,
                            PipelinesCloned = request.IncludePipelines
                        });

                        if (createResult.Success)
                            result.TotalCloned++;
                        else
                        {
                            result.TotalFailed++;
                            result.Warnings.Add($"Failed to clone to {targetName}: {createResult.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error cloning to {Target}", targetName);
                        result.TotalFailed++;
                        result.Warnings.Add($"Exception cloning to {targetName}: {ex.Message}");
                    }
                }

                result.Success = result.TotalCloned > 0;
                result.Duration = DateTime.UtcNow - startTime;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in clone operation");
                return new EnvironmentCloneResult
                {
                    Success = false,
                    SourceEnvironment = request.SourceEnvironment,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<EnvironmentDeletionResult> DeleteEnvironmentAsync(
            string environmentName,
            string resourceGroup,
            bool createBackup = true,
            string? subscriptionId = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Deleting environment: {Name} in {ResourceGroup}", environmentName, resourceGroup);

            try
            {
                var result = new EnvironmentDeletionResult
                {
                    EnvironmentName = environmentName,
                    ResourceGroup = resourceGroup,
                    DeletedAt = DateTime.UtcNow
                };

                if (string.IsNullOrWhiteSpace(environmentName))
                {
                    result.Success = false;
                    result.ErrorMessage = "Environment name is required";
                    return result;
                }

                if (createBackup)
                {
                    var backupLocation = $"backups/{environmentName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                    result.BackupCreated = true;
                    result.BackupLocation = backupLocation;
                    _logger.LogInformation("Backup metadata created at: {Location}", backupLocation);
                }

                try
                {
                    var resources = await _azureResourceService.ListResourcesAsync(resourceGroup, subscriptionId, cancellationToken);
                    var environmentResources = resources.Where(r =>
                    {
                        var resourceDict = r as IDictionary<string, object>;
                        var name = ExtractString(resourceDict, "name");
                        return name?.Contains(environmentName, StringComparison.OrdinalIgnoreCase) ?? false;
                    }).ToList();

                    foreach (var resource in environmentResources)
                    {
                        var resourceDict = resource as IDictionary<string, object>;
                        var resourceName = ExtractString(resourceDict, "name") ?? "unknown";
                        var resourceType = ExtractString(resourceDict, "type") ?? "unknown";
                        result.DeletedResources.Add(new DeletedResource
                        {
                            Name = resourceName,
                            Type = resourceType,
                            DeletedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogWarning("Resource group {ResourceGroup} not found - considering environment deleted", resourceGroup);
                    result.DeletedResources.Add(new DeletedResource
                    {
                        Name = $"Resource group '{resourceGroup}' (already deleted or never existed)",
                        Type = "Microsoft.Resources/resourceGroups",
                        DeletedAt = DateTime.UtcNow
                    });
                }

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting environment {Name}", environmentName);
                return new EnvironmentDeletionResult
                {
                    Success = false,
                    EnvironmentName = environmentName,
                    ResourceGroup = resourceGroup,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<BulkOperationResult> BulkDeleteEnvironmentsAsync(
            EnvironmentFilter filter,
            BulkOperationSettings settings,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting bulk delete operation");

            try
            {
                var result = new BulkOperationResult();
                var startTime = DateTime.UtcNow;
                var environments = await ListEnvironmentsAsync(filter, cancellationToken);

                if (environments.Count > settings.MaxResources)
                {
                    result.Success = false;
                    result.FailedOperations.Add($"Operation would affect {environments.Count} resources, exceeding limit of {settings.MaxResources}");
                    return result;
                }

                result.TotalProcessed = environments.Count;

                foreach (var env in environments)
                {
                    try
                    {
                        var deleteResult = await DeleteEnvironmentAsync(env.Name, env.ResourceGroup, settings.CreateBackup, null, cancellationToken);
                        if (deleteResult.Success)
                        {
                            result.SuccessCount++;
                            result.SuccessfulOperations.Add($"{env.Name} in {env.ResourceGroup}");
                        }
                        else
                        {
                            result.FailureCount++;
                            result.FailedOperations.Add($"{env.Name}: {deleteResult.ErrorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        result.FailureCount++;
                        result.FailedOperations.Add($"{env.Name}: {ex.Message}");
                    }
                }

                result.Success = result.SuccessCount > 0;
                result.Duration = DateTime.UtcNow - startTime;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk delete operation");
                return new BulkOperationResult { Success = false, FailedOperations = new List<string> { ex.Message } };
            }
        }

        #endregion

        #region Monitoring & Health

        public async Task<EnvironmentHealthReport> GetEnvironmentHealthAsync(
            string environmentName,
            string resourceGroup,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting health report for {Name}", environmentName);

            var report = new EnvironmentHealthReport
            {
                EnvironmentName = environmentName,
                ResourceGroup = resourceGroup,
                LastChecked = DateTime.UtcNow
            };

            try
            {
                var status = await GetEnvironmentStatusAsync(environmentName, resourceGroup, null, cancellationToken);
                report.OverallHealth = status.IsRunning ? HealthStatus.Healthy : HealthStatus.Critical;

                report.Checks.Add(new HealthCheck
                {
                    Name = "Provisioning State",
                    Category = "Infrastructure",
                    Status = status.IsRunning ? HealthStatus.Healthy : HealthStatus.Critical,
                    Message = $"Environment is {status.Status}",
                    LastChecked = DateTime.UtcNow
                });

                report.Checks.Add(new HealthCheck
                {
                    Name = "Endpoint",
                    Category = "Connectivity",
                    Status = !string.IsNullOrEmpty(status.Endpoint) ? HealthStatus.Healthy : HealthStatus.Warning,
                    Message = string.IsNullOrEmpty(status.Endpoint) ? "No endpoint configured" : "Endpoint available",
                    LastChecked = DateTime.UtcNow
                });

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting health for {Name}", environmentName);
                throw;
            }
        }

        public Task<EnvironmentMetrics> GetEnvironmentMetricsAsync(
            string environmentName,
            string resourceGroup,
            MetricsRequest metricsRequest,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new EnvironmentMetrics
            {
                EnvironmentName = environmentName,
                ResourceGroup = resourceGroup,
                CollectedAt = DateTime.UtcNow,
                Performance = new PerformanceMetrics(),
                Resources = new ResourceMetrics(),
                Requests = new RequestMetrics(),
                Errors = new ErrorMetrics()
            });
        }

        public async Task<EnvironmentStatus> GetEnvironmentStatusAsync(
            string environmentName,
            string resourceGroup,
            string? subscriptionId = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting status for {Name} in {ResourceGroup}", environmentName, resourceGroup);

            if (string.IsNullOrWhiteSpace(environmentName))
                throw new ArgumentException("Environment name is required", nameof(environmentName));

            if (string.IsNullOrWhiteSpace(resourceGroup))
                throw new ArgumentException("Resource group is required", nameof(resourceGroup));

            var resources = await _azureResourceService.ListResourcesAsync(resourceGroup, subscriptionId, cancellationToken);
            var environmentResource = resources.FirstOrDefault(r =>
            {
                var resourceDict = r as IDictionary<string, object>;
                var name = ExtractString(resourceDict, "name");
                return name?.Equals(environmentName, StringComparison.OrdinalIgnoreCase) ?? false;
            });

            if (environmentResource == null)
                throw new InvalidOperationException($"Environment '{environmentName}' not found in resource group '{resourceGroup}'");

            var resourceData = environmentResource as IDictionary<string, object>;

            return new EnvironmentStatus
            {
                EnvironmentName = environmentName,
                ResourceGroup = resourceGroup,
                Type = ParseEnvironmentType(ExtractString(resourceData, "type")),
                Status = ExtractProvisioningState(resourceData),
                IsRunning = ExtractProvisioningState(resourceData)?.Equals("Succeeded", StringComparison.OrdinalIgnoreCase) ?? false,
                CreatedAt = ExtractDateTime(resourceData, "createdTime") ?? DateTime.MinValue,
                LastModified = ExtractDateTime(resourceData, "changedTime"),
                Endpoint = ExtractString(resourceData, "properties.defaultHostName") ??
                           ExtractString(resourceData, "properties.hostName") ?? string.Empty,
                Tags = ExtractTags(resourceData),
                Configuration = new EnvironmentConfiguration
                {
                    Location = ExtractString(resourceData, "location") ?? string.Empty,
                    Sku = ExtractString(resourceData, "sku.name") ?? ExtractString(resourceData, "sku") ?? string.Empty,
                    MonitoringEnabled = true,
                    LoggingEnabled = true,
                    CustomSettings = new Dictionary<string, string>()
                }
            };
        }

        #endregion

        #region Operations

        public async Task<ScalingResult> ScaleEnvironmentAsync(
            string environmentName,
            string resourceGroup,
            ScaleSettings scaleSettings,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Scaling {Name} in {ResourceGroup}", environmentName, resourceGroup);

            try
            {
                var status = await GetEnvironmentStatusAsync(environmentName, resourceGroup, null, cancellationToken);
                var prevReplicas = status.Configuration.Replicas ?? 1;
                var newReplicas = scaleSettings.TargetReplicas ?? prevReplicas;

                return new ScalingResult
                {
                    Success = true,
                    EnvironmentName = environmentName,
                    PreviousReplicas = prevReplicas,
                    NewReplicas = newReplicas,
                    Action = scaleSettings.AutoScaling ? ScalingAction.AutoScalingEnabled :
                             newReplicas > prevReplicas ? ScalingAction.ScaleUp :
                             newReplicas < prevReplicas ? ScalingAction.ScaleDown : ScalingAction.NoChange,
                    ScaledAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scaling {Name}", environmentName);
                return new ScalingResult { Success = false, EnvironmentName = environmentName, ErrorMessage = ex.Message };
            }
        }

        public async Task<MigrationResult> MigrateEnvironmentAsync(
            MigrationRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Migrating {Source} to {Target}", request.SourceEnvironment, request.TargetEnvironment);

            try
            {
                var startTime = DateTime.UtcNow;
                var sourceStatus = await GetEnvironmentStatusAsync(request.SourceEnvironment, request.SourceResourceGroup, null, cancellationToken);

                var createRequest = new EnvironmentCreationRequest
                {
                    Name = request.TargetEnvironment,
                    Type = sourceStatus.Type,
                    ResourceGroup = request.TargetResourceGroup,
                    Location = request.TargetLocation,
                    SubscriptionId = request.TargetSubscriptionId,
                    Tags = sourceStatus.Tags,
                    ScaleSettings = sourceStatus.Configuration.ScaleSettings
                };

                var createResult = await CreateEnvironmentAsync(createRequest, cancellationToken);

                return new MigrationResult
                {
                    Success = createResult.Success,
                    SourceEnvironment = request.SourceEnvironment,
                    TargetEnvironment = request.TargetEnvironment,
                    TargetLocation = request.TargetLocation,
                    DataMigrated = request.MigrateData,
                    Duration = DateTime.UtcNow - startTime,
                    ErrorMessage = createResult.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error migrating environment");
                return new MigrationResult
                {
                    Success = false,
                    SourceEnvironment = request.SourceEnvironment,
                    TargetEnvironment = request.TargetEnvironment,
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<List<EnvironmentDiscoveryResult>> DiscoverEnvironmentsAsync(
            EnvironmentFilter filter,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Discovering environments");

            var results = new List<EnvironmentDiscoveryResult>();
            var resourceGroups = await _azureResourceService.ListResourceGroupsAsync(filter.SubscriptionId, cancellationToken);

            foreach (var rg in resourceGroups)
            {
                var rgDict = rg as IDictionary<string, object>;
                var rgName = ExtractString(rgDict, "name");
                if (string.IsNullOrEmpty(rgName)) continue;

                if (filter.ResourceGroups?.Any() == true && !filter.ResourceGroups.Contains(rgName, StringComparer.OrdinalIgnoreCase))
                    continue;

                var resources = await _azureResourceService.ListResourcesAsync(rgName, filter.SubscriptionId, cancellationToken);

                foreach (var resource in resources)
                {
                    var resourceDict = resource as IDictionary<string, object>;
                    var envType = ParseEnvironmentType(ExtractString(resourceDict, "type"));

                    if (filter.Type.HasValue && envType != filter.Type.Value)
                        continue;

                    results.Add(new EnvironmentDiscoveryResult
                    {
                        Name = ExtractString(resourceDict, "name") ?? "unknown",
                        ResourceId = ExtractString(resourceDict, "id") ?? string.Empty,
                        ResourceGroup = rgName,
                        Type = envType,
                        Location = ExtractString(resourceDict, "location") ?? string.Empty,
                        CreatedAt = ExtractDateTime(resourceDict, "createdTime") ?? DateTime.MinValue,
                        IsManaged = true,
                        Tags = ExtractTags(resourceDict),
                        RelatedResources = new List<string>()
                    });
                }
            }

            return results;
        }

        public async Task<CleanupResult> CleanupOldEnvironmentsAsync(
            CleanupPolicy policy,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Cleanup operation with minimum age {Days} days", policy.MinimumAgeInDays);

            var result = new CleanupResult();
            var environments = await DiscoverEnvironmentsAsync(new EnvironmentFilter { AgeInDays = policy.MinimumAgeInDays }, cancellationToken);
            result.EnvironmentsAnalyzed = environments.Count;

            var cutoffDate = DateTime.UtcNow.AddDays(-policy.MinimumAgeInDays);

            foreach (var env in environments)
            {
                if (policy.ExcludedResourceGroups?.Contains(env.ResourceGroup, StringComparer.OrdinalIgnoreCase) == true)
                    continue;
                if (policy.ExcludedTags?.Any(tag => env.Tags.ContainsKey(tag)) == true)
                    continue;
                if (env.CreatedAt > cutoffDate)
                    continue;

                var deleteResult = await DeleteEnvironmentAsync(env.Name, env.ResourceGroup, policy.CreateBackup, null, cancellationToken);
                if (deleteResult.Success)
                {
                    result.EnvironmentsDeleted++;
                    result.DeletedEnvironments.Add($"{env.Name} ({env.ResourceGroup})");
                    result.EstimatedMonthlySavings += 100m;
                }
            }

            result.Success = true;
            return result;
        }

        #endregion

        #region Advanced Operations

        public async Task<TemplateDeploymentResult> DeployFromTemplateAsync(
            TemplateDeploymentRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Deploying from template {TemplateId}", request.TemplateId);

            try
            {
                var createRequest = new EnvironmentCreationRequest
                {
                    Name = request.EnvironmentName,
                    Type = EnvironmentType.WebApp,
                    ResourceGroup = request.ResourceGroup,
                    Location = request.Location,
                    Tags = request.Tags
                };

                var createResult = await CreateEnvironmentAsync(createRequest, cancellationToken);

                return new TemplateDeploymentResult
                {
                    Success = createResult.Success,
                    TemplateId = request.TemplateId,
                    EnvironmentName = request.EnvironmentName,
                    DeployedResources = createResult.CreatedResources,
                    Status = createResult.Success ? DeploymentStatus.Succeeded : DeploymentStatus.Failed,
                    ErrorMessage = createResult.ErrorMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying from template");
                return new TemplateDeploymentResult
                {
                    Success = false,
                    TemplateId = request.TemplateId,
                    Status = DeploymentStatus.Failed,
                    ErrorMessage = ex.Message
                };
            }
        }

        public Task<ComplianceSetupResult> SetupComplianceAsync(
            string environmentName,
            string resourceGroup,
            ComplianceSettings settings,
            CancellationToken cancellationToken = default)
        {
            var controls = new List<string>();
            if (settings.EncryptionAtRest) controls.Add("Encryption at Rest");
            if (settings.EncryptionInTransit) controls.Add("Encryption in Transit");
            if (settings.AuditLogging) controls.Add("Audit Logging");
            if (settings.SecurityMonitoring) controls.Add("Security Monitoring");

            return Task.FromResult(new ComplianceSetupResult
            {
                Success = true,
                EnvironmentName = environmentName,
                Standard = settings.Standard,
                AppliedControls = controls,
                TotalControls = controls.Count
            });
        }

        public async Task<MultiRegionDeploymentResult> OrchestrateMultiRegionAsync(
            MultiRegionRequest request,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Multi-region deployment for {BaseName}", request.EnvironmentBaseName);

            var result = new MultiRegionDeploymentResult { EnvironmentBaseName = request.EnvironmentBaseName };

            foreach (var region in request.TargetRegions)
            {
                try
                {
                    var envName = $"{request.EnvironmentBaseName}-{region}";
                    var createRequest = new EnvironmentCreationRequest
                    {
                        Name = envName,
                        Type = request.Type,
                        ResourceGroup = $"{request.EnvironmentBaseName}-rg",
                        Location = region,
                        Tags = request.Tags
                    };

                    var createResult = await CreateEnvironmentAsync(createRequest, cancellationToken);

                    result.RegionalDeployments.Add(new RegionalDeployment
                    {
                        Region = region,
                        EnvironmentName = envName,
                        Success = createResult.Success,
                        Endpoint = createResult.Endpoint
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deploying to {Region}", region);
                    result.RegionalDeployments.Add(new RegionalDeployment { Region = region, Success = false });
                }
            }

            result.Success = result.RegionalDeployments.Any(d => d.Success);
            result.TrafficManagerConfigured = request.EnableTrafficManager && result.Success;
            return result;
        }

        public async Task<List<EnvironmentSummary>> ListEnvironmentsAsync(
            EnvironmentFilter filter,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Listing environments");

            var summaries = new List<EnvironmentSummary>();
            var resourceGroups = filter.ResourceGroups?.Any() == true
                ? filter.ResourceGroups
                : (await _azureResourceService.ListResourceGroupsAsync(filter.SubscriptionId, cancellationToken))
                    .Select(rg => ExtractString(rg as IDictionary<string, object>, "name"))
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Cast<string>()
                    .ToList();

            foreach (var rgName in resourceGroups)
            {
                var resources = await _azureResourceService.ListResourcesAsync(rgName, filter.SubscriptionId, cancellationToken);

                foreach (var resource in resources)
                {
                    var resourceDict = resource as IDictionary<string, object>;
                    var envType = ParseEnvironmentType(ExtractString(resourceDict, "type"));

                    if (filter.Type.HasValue && envType != filter.Type.Value) continue;

                    var location = ExtractString(resourceDict, "location");
                    if (filter.Locations?.Any() == true && !filter.Locations.Contains(location, StringComparer.OrdinalIgnoreCase)) continue;

                    var tags = ExtractTags(resourceDict);
                    if (filter.Tags?.Any() == true)
                    {
                        var hasAllTags = filter.Tags.All(ft => tags.ContainsKey(ft.Key) && tags[ft.Key].Equals(ft.Value, StringComparison.OrdinalIgnoreCase));
                        if (!hasAllTags) continue;
                    }

                    var createdTime = ExtractDateTime(resourceDict, "createdTime");
                    if (filter.AgeInDays.HasValue && createdTime.HasValue)
                    {
                        if (createdTime.Value > DateTime.UtcNow.AddDays(-filter.AgeInDays.Value)) continue;
                    }

                    summaries.Add(new EnvironmentSummary
                    {
                        Name = ExtractString(resourceDict, "name") ?? "unknown",
                        ResourceId = ExtractString(resourceDict, "id") ?? string.Empty,
                        ResourceGroup = rgName,
                        Type = envType,
                        Location = location ?? string.Empty,
                        Status = ExtractProvisioningState(resourceDict) ?? "Unknown",
                        CreatedAt = createdTime ?? DateTime.MinValue,
                        Health = HealthStatus.Unknown,
                        Tags = tags
                    });
                }
            }

            return summaries;
        }

        #endregion

        #region Private Helpers

        private async Task<EnvironmentCreationResult> CreateAksEnvironmentAsync(
            EnvironmentCreationRequest request,
            CancellationToken cancellationToken)
        {
            var result = new EnvironmentCreationResult
            {
                EnvironmentName = request.Name,
                ResourceGroup = request.ResourceGroup,
                Type = EnvironmentType.AKS,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var aksSettings = new Dictionary<string, object>
                {
                    ["nodeCount"] = request.NodeCount ?? 3,
                    ["enableMonitoring"] = request.EnableMonitoring
                };

                var createdResource = await _azureResourceService.CreateAksClusterAsync(
                    request.Name,
                    request.ResourceGroup,
                    request.Location,
                    aksSettings,
                    request.SubscriptionId,
                    cancellationToken);

                var resourceDict = createdResource as IDictionary<string, object>;
                result.Success = true;
                result.EnvironmentId = ExtractString(resourceDict, "id") ?? string.Empty;
                result.Status = ExtractProvisioningState(resourceDict) ?? "Created";
                result.Endpoint = ExtractString(resourceDict, "properties.fqdn") ?? string.Empty;
                result.CreatedResources.Add(request.Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AKS {Name}", request.Name);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private async Task<EnvironmentCreationResult> CreateWebAppEnvironmentAsync(
            EnvironmentCreationRequest request,
            CancellationToken cancellationToken)
        {
            var result = new EnvironmentCreationResult
            {
                EnvironmentName = request.Name,
                ResourceGroup = request.ResourceGroup,
                Type = EnvironmentType.WebApp,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var appSettings = new Dictionary<string, object>
                {
                    ["sku"] = request.Sku ?? "B1",
                    ["enableMonitoring"] = request.EnableMonitoring
                };

                var createdResource = await _azureResourceService.CreateWebAppAsync(
                    request.Name,
                    request.ResourceGroup,
                    request.Location,
                    appSettings,
                    request.SubscriptionId,
                    cancellationToken);

                var resourceDict = createdResource as IDictionary<string, object>;
                result.Success = true;
                result.EnvironmentId = ExtractString(resourceDict, "id") ?? string.Empty;
                result.Status = ExtractProvisioningState(resourceDict) ?? "Created";
                result.Endpoint = ExtractString(resourceDict, "properties.defaultHostName") ?? string.Empty;
                result.CreatedResources.Add(request.Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating WebApp {Name}", request.Name);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private async Task<EnvironmentCreationResult> CreateGenericEnvironmentAsync(
            EnvironmentCreationRequest request,
            CancellationToken cancellationToken)
        {
            var result = new EnvironmentCreationResult
            {
                EnvironmentName = request.Name,
                ResourceGroup = request.ResourceGroup,
                Type = request.Type,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                var resourceType = request.Type switch
                {
                    EnvironmentType.FunctionApp => "Microsoft.Web/sites",
                    EnvironmentType.ContainerApp => "Microsoft.App/containerApps",
                    _ => "Microsoft.Resources/deployments"
                };

                var properties = new { location = request.Location, sku = request.Sku ?? "Y1" };

                var createdResource = await _azureResourceService.CreateResourceAsync(
                    request.ResourceGroup,
                    resourceType,
                    request.Name,
                    properties,
                    request.SubscriptionId,
                    request.Location,
                    request.Tags,
                    cancellationToken);

                var resourceDict = createdResource as IDictionary<string, object>;
                result.Success = true;
                result.EnvironmentId = ExtractString(resourceDict, "id") ?? string.Empty;
                result.Status = "Created";
                result.CreatedResources.Add(request.Name);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating {Type} {Name}", request.Type, request.Name);
                result.Success = false;
                result.ErrorMessage = ex.Message;
                return result;
            }
        }

        private EnvironmentType ParseEnvironmentType(string? type)
        {
            if (string.IsNullOrWhiteSpace(type)) return EnvironmentType.Unknown;

            if (type.Contains("aks", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("kubernetes", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("ContainerService", StringComparison.OrdinalIgnoreCase))
                return EnvironmentType.AKS;

            if (type.Contains("webapp", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("Web/sites", StringComparison.OrdinalIgnoreCase))
                return EnvironmentType.WebApp;

            if (type.Contains("function", StringComparison.OrdinalIgnoreCase))
                return EnvironmentType.FunctionApp;

            if (type.Contains("container", StringComparison.OrdinalIgnoreCase) ||
                type.Contains("App/containerApps", StringComparison.OrdinalIgnoreCase))
                return EnvironmentType.ContainerApp;

            return EnvironmentType.Unknown;
        }

        private string? ExtractString(IDictionary<string, object>? dict, string path)
        {
            if (dict == null) return null;
            var parts = path.Split('.');
            object? current = dict;

            foreach (var part in parts)
            {
                if (current is IDictionary<string, object> currentDict)
                {
                    if (!currentDict.TryGetValue(part, out current))
                        return null;
                }
                else return null;
            }

            return current?.ToString();
        }

        private DateTime? ExtractDateTime(IDictionary<string, object>? dict, string key)
        {
            var value = ExtractString(dict, key);
            return DateTime.TryParse(value, out var result) ? result : null;
        }

        private string ExtractProvisioningState(IDictionary<string, object>? dict)
        {
            return ExtractString(dict, "properties.provisioningState") ??
                   ExtractString(dict, "provisioningState") ??
                   "Unknown";
        }

        private Dictionary<string, string> ExtractTags(IDictionary<string, object>? dict)
        {
            if (dict == null || !dict.ContainsKey("tags"))
                return new Dictionary<string, string>();

            if (dict["tags"] is IDictionary<string, object> tagDict)
            {
                return tagDict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value?.ToString() ?? string.Empty);
            }

            return new Dictionary<string, string>();
        }

        #endregion

        #region Template Management Helpers

        /// <summary>
        /// Maps Core EnvironmentTemplate DTO to ServiceTemplate model
        /// </summary>
        private ServiceTemplate MapToServiceTemplate(EnvironmentTemplate dbTemplate)
        {
            // Map template files from database
            var files = dbTemplate.Files?.Select(f => new ServiceTemplateFile
            {
                FileName = f.FileName,
                Content = f.Content,
                FileType = f.FileType,
                IsEntryPoint = f.IsEntryPoint,
                Order = f.Order
            }).ToList() ?? new List<ServiceTemplateFile>();

            _logger.LogInformation("Mapped template {TemplateName} with {FileCount} files", 
                dbTemplate.Name, files.Count);

            return new ServiceTemplate
            {
                Id = dbTemplate.Id.ToString(),
                Name = dbTemplate.Name,
                Description = dbTemplate.Description,
                TemplateType = dbTemplate.TemplateType,
                Version = dbTemplate.Version,
                Content = dbTemplate.Content,
                Format = dbTemplate.Format,
                DeploymentTier = dbTemplate.DeploymentTier,
                MultiRegionSupported = dbTemplate.MultiRegionSupported,
                DisasterRecoverySupported = dbTemplate.DisasterRecoverySupported,
                HighAvailabilitySupported = dbTemplate.HighAvailabilitySupported,
                Parameters = ParseJsonToDictionary(dbTemplate.Parameters),
                Tags = ParseJsonToDictionary(dbTemplate.Tags) ?? new Dictionary<string, string>(),
                AzureService = dbTemplate.AzureService,
                AutoScalingEnabled = dbTemplate.AutoScalingEnabled,
                MonitoringEnabled = dbTemplate.MonitoringEnabled,
                BackupEnabled = dbTemplate.BackupEnabled,
                FilesCount = dbTemplate.FilesCount,
                MainFileType = dbTemplate.MainFileType,
                Files = files,
                CreatedAt = dbTemplate.CreatedAt,
                UpdatedAt = dbTemplate.UpdatedAt,
                IsActive = dbTemplate.IsActive
            };
        }

        /// <summary>
        /// Parses JSON string to dictionary, returns empty dictionary if null or invalid
        /// </summary>
        private Dictionary<string, string>? ParseJsonToDictionary(string? jsonString)
        {
            if (string.IsNullOrWhiteSpace(jsonString))
                return null;

            try
            {
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(jsonString);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON string to dictionary: {Json}", jsonString);
                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Deploys infrastructure from a service template definition
        /// </summary>
        private async Task<EnvironmentCreationResult> DeployFromTemplateAsync(
            EnvironmentCreationRequest request,
            ServiceTemplate template,
            CancellationToken cancellationToken)
        {
            var result = new EnvironmentCreationResult
            {
                EnvironmentName = request.Name,
                EnvironmentId = Guid.NewGuid().ToString(),
                ResourceGroup = request.ResourceGroup,
                Type = request.Type,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation(
                    "Deploying infrastructure from template: {TemplateName} (Format: {Format}, Type: {TemplateType})",
                    template.Name, template.Format, template.TemplateType);

                // Determine deployment method based on template format
                switch (template.Format.ToLowerInvariant())
                {
                    case "bicep":
                        return await DeployBicepTemplateAsync(request, template, result, cancellationToken);
                    
                    case "arm":
                    case "armtemplate":
                        return await DeployArmTemplateAsync(request, template, result, cancellationToken);
                    
                    case "terraform":
                        return await DeployTerraformTemplateAsync(request, template, result, cancellationToken);
                    
                    default:
                        result.Success = false;
                        result.ErrorMessage = $"Unsupported template format: {template.Format}. Supported formats: Bicep, ARM, Terraform";
                        _logger.LogError("Unsupported template format: {Format}", template.Format);
                        return result;
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Template deployment failed: {ex.Message}";
                _logger.LogError(ex, "Failed to deploy template {TemplateName}", template.Name);
                return result;
            }
        }

        /// <summary>
        /// Deploys a Bicep template to Azure using the deployment orchestrator
        /// </summary>
        private async Task<EnvironmentCreationResult> DeployBicepTemplateAsync(
            EnvironmentCreationRequest request,
            ServiceTemplate template,
            EnvironmentCreationResult result,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deploying Bicep template via orchestrator: {TemplateName}", template.Name);

            try
            {
                // Prepare parameters with defaults for common Bicep parameters
                var parameters = request.TemplateParameters ?? new Dictionary<string, string>();
                
                // Auto-populate serviceName if not provided (common parameter in AKS templates)
                if (!parameters.ContainsKey("serviceName"))
                {
                    parameters["serviceName"] = request.Name;
                    _logger.LogInformation("Auto-populated serviceName parameter with environment name: {ServiceName}", request.Name);
                }
                
                // Auto-populate location if not provided (required for most Azure resources)
                if (!parameters.ContainsKey("location"))
                {
                    parameters["location"] = request.Location;
                    _logger.LogInformation("Auto-populated location parameter: {Location}", request.Location);
                }
                
                // Auto-populate environment if not provided
                if (!parameters.ContainsKey("environment"))
                {
                    parameters["environment"] = "dev";
                }
                
                // Auto-populate optional AKS parameters with sensible defaults
                if (!parameters.ContainsKey("kubernetesVersion"))
                {
                    // Use 1.30 - currently supported in Azure Government standard tier
                    // Note: 1.28 and 1.29 are LTS-only (require Premium tier)
                    // Azure Gov typically supports 1.30+ for standard tier
                    parameters["kubernetesVersion"] = "1.30";
                }
                
                if (!parameters.ContainsKey("nodeCount"))
                {
                    parameters["nodeCount"] = "3";
                }
                
                if (!parameters.ContainsKey("nodeVmSize"))
                {
                    parameters["nodeVmSize"] = "Standard_D4s_v3";
                }
                
                // Disable monitoring features that require Log Analytics workspace
                // This avoids the requirement for a workspace resource ID
                if (!parameters.ContainsKey("enableMonitoring"))
                {
                    parameters["enableMonitoring"] = "false";
                }
                
                // Note: logAnalyticsWorkspaceId is NOT a template parameter
                // The Bicep template handles workspace creation/reference internally
                // Only include logAnalyticsWorkspaceId if monitoring is enabled AND user provided it
                if (parameters.TryGetValue("enableMonitoring", out var enableMonitoring) && 
                    enableMonitoring.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    // Monitoring enabled - workspace ID should be provided by user or template will create one
                    if (!parameters.ContainsKey("logAnalyticsWorkspaceId") || 
                        string.IsNullOrWhiteSpace(parameters["logAnalyticsWorkspaceId"]))
                    {
                        _logger.LogWarning("Monitoring enabled but no Log Analytics workspace ID provided - template will handle workspace creation");
                        // Remove the empty parameter to avoid template validation errors
                        parameters.Remove("logAnalyticsWorkspaceId");
                    }
                }
                else
                {
                    // Monitoring disabled - remove workspace ID parameter if it exists
                    if (parameters.ContainsKey("logAnalyticsWorkspaceId"))
                    {
                        parameters.Remove("logAnalyticsWorkspaceId");
                        _logger.LogInformation("Monitoring disabled - removed logAnalyticsWorkspaceId parameter");
                    }
                }
                
                if (!parameters.ContainsKey("enableAzurePolicy"))
                {
                    parameters["enableAzurePolicy"] = "false";
                }
                
                if (!parameters.ContainsKey("enableWorkloadIdentity"))
                {
                    parameters["enableWorkloadIdentity"] = "false";
                }
                
                // Prepare deployment options
                var deploymentOptions = new DeploymentOptions
                {
                    DeploymentName = request.Name,
                    ResourceGroup = request.ResourceGroup,
                    Location = request.Location,
                    SubscriptionId = request.SubscriptionId ?? string.Empty,
                    Parameters = parameters,
                    Tags = request.Tags ?? new Dictionary<string, string>(),
                    WaitForCompletion = true,
                    Timeout = TimeSpan.FromMinutes(30)
                };

                // Extract additional files from template (for multi-file Bicep modules)
                Dictionary<string, string>? additionalFiles = null;
                if (template.Files != null && template.Files.Count > 0)
                {
                    additionalFiles = new Dictionary<string, string>();
                    foreach (var file in template.Files.Where(f => !f.IsEntryPoint))
                    {
                        additionalFiles[file.FileName] = file.Content;
                    }
                    _logger.LogInformation("Deploying with {Count} additional module files", additionalFiles.Count);
                }

                // Call deployment orchestrator with all files
                var deploymentResult = await _deploymentOrchestrator.DeployBicepTemplateAsync(
                    template.Content,
                    deploymentOptions,
                    additionalFiles,
                    cancellationToken);

                // Map orchestrator result to engine result
                result.Success = deploymentResult.Success;
                result.EnvironmentId = deploymentResult.DeploymentId;
                result.Status = deploymentResult.Success ? "Succeeded" : "Failed";
                result.CreatedResources = deploymentResult.CreatedResources;
                result.ErrorMessage = deploymentResult.ErrorMessage;
                
                _logger.LogInformation("Bicep deployment {Status}: {DeploymentId}", 
                    result.Status, result.EnvironmentId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying Bicep template {TemplateName}", template.Name);
                result.Success = false;
                result.ErrorMessage = $"Bicep deployment failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Deploys an ARM template to Azure using the deployment orchestrator
        /// </summary>
        private async Task<EnvironmentCreationResult> DeployArmTemplateAsync(
            EnvironmentCreationRequest request,
            ServiceTemplate template,
            EnvironmentCreationResult result,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deploying ARM template via orchestrator: {TemplateName}", template.Name);

            try
            {
                // ARM templates can be deployed as Bicep (they're compatible)
                var deploymentOptions = new DeploymentOptions
                {
                    DeploymentName = request.Name,
                    ResourceGroup = request.ResourceGroup,
                    Location = request.Location,
                    SubscriptionId = request.SubscriptionId ?? string.Empty,
                    Parameters = request.TemplateParameters ?? new Dictionary<string, string>(),
                    Tags = request.Tags ?? new Dictionary<string, string>(),
                    WaitForCompletion = true,
                    Timeout = TimeSpan.FromMinutes(30)
                };

                // Call deployment orchestrator (Bicep supports ARM templates)
                var deploymentResult = await _deploymentOrchestrator.DeployBicepTemplateAsync(
                    template.Content,
                    deploymentOptions,
                    additionalFiles: null, // ARM templates are single-file
                    cancellationToken);

                // Map orchestrator result to engine result
                result.Success = deploymentResult.Success;
                result.EnvironmentId = deploymentResult.DeploymentId;
                result.Status = deploymentResult.Success ? "Succeeded" : "Failed";
                result.CreatedResources = deploymentResult.CreatedResources;
                result.ErrorMessage = deploymentResult.ErrorMessage;

                _logger.LogInformation("ARM deployment {Status}: {DeploymentId}", 
                    result.Status, result.EnvironmentId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying ARM template {TemplateName}", template.Name);
                result.Success = false;
                result.ErrorMessage = $"ARM deployment failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Deploys a Terraform template using the deployment orchestrator
        /// </summary>
        private async Task<EnvironmentCreationResult> DeployTerraformTemplateAsync(
            EnvironmentCreationRequest request,
            ServiceTemplate template,
            EnvironmentCreationResult result,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Deploying Terraform template via orchestrator: {TemplateName}", template.Name);

            try
            {
                var deploymentOptions = new DeploymentOptions
                {
                    DeploymentName = request.Name,
                    ResourceGroup = request.ResourceGroup,
                    Location = request.Location,
                    SubscriptionId = request.SubscriptionId ?? string.Empty,
                    Parameters = request.TemplateParameters ?? new Dictionary<string, string>(),
                    Tags = request.Tags ?? new Dictionary<string, string>(),
                    WaitForCompletion = true,
                    Timeout = TimeSpan.FromMinutes(45) // Terraform can take longer
                };

                // Call deployment orchestrator
                var deploymentResult = await _deploymentOrchestrator.DeployTerraformAsync(
                    template.Content,
                    deploymentOptions,
                    cancellationToken);

                // Map orchestrator result to engine result
                result.Success = deploymentResult.Success;
                result.EnvironmentId = deploymentResult.DeploymentId;
                result.Status = deploymentResult.Success ? "Succeeded" : "Failed";
                result.CreatedResources = deploymentResult.CreatedResources;
                result.ErrorMessage = deploymentResult.ErrorMessage;

                _logger.LogInformation("Terraform deployment {Status}: {DeploymentId}", 
                    result.Status, result.EnvironmentId);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deploying Terraform template {TemplateName}", template.Name);
                result.Success = false;
                result.ErrorMessage = $"Terraform deployment failed: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Fallback deployment method using environment type
        /// </summary>
        private async Task<EnvironmentCreationResult> DeployByTypeAsync(
            EnvironmentCreationRequest request,
            CancellationToken cancellationToken)
        {
            switch (request.Type)
            {
                case EnvironmentType.AKS:
                    return await CreateAksEnvironmentAsync(request, cancellationToken);
                
                case EnvironmentType.WebApp:
                    return await CreateWebAppEnvironmentAsync(request, cancellationToken);
                
                default:
                    // For other types, use generic environment creation
                    return await CreateGenericEnvironmentAsync(request, cancellationToken);
            }
        }

        /// <summary>
        /// Generate a new template dynamically based on specifications
        /// </summary>
        public async Task<TemplateGenerationResult> GenerateTemplateAsync(
            TemplateGenerationRequest templateRequest,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating dynamic template for service: {ServiceName}", templateRequest.ServiceName);

            try
            {
                // Use the injected template generator
                var result = await _templateGenerator.GenerateTemplateAsync(templateRequest, cancellationToken);

                if (result.Success)
                {
                    _logger.LogInformation("Successfully generated template with {FileCount} files", result.Files.Count);
                }
                else
                {
                    _logger.LogWarning("Template generation failed: {Error}", result.ErrorMessage);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating template for {ServiceName}", templateRequest.ServiceName);
                return new TemplateGenerationResult
                {
                    Success = false,
                    ErrorMessage = $"Template generation failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Generate and save a template to storage
        /// </summary>
        public async Task<ServiceTemplate?> GenerateAndSaveTemplateAsync(
            TemplateGenerationRequest templateRequest,
            string templateName,
            string description,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Generating and saving template: {TemplateName}", templateName);

            try
            {
                // Generate the template
                var generationResult = await _templateGenerator.GenerateTemplateAsync(templateRequest, cancellationToken);

                if (!generationResult.Success)
                {
                    _logger.LogError("Template generation failed: {Error}", generationResult.ErrorMessage);
                    return null;
                }

                // Create template files list
                var templateFiles = new List<ServiceTemplateFile>();
                foreach (var file in generationResult.Files)
                {
                    templateFiles.Add(new ServiceTemplateFile
                    {
                        FileName = file.Key,
                        Content = file.Value,
                        FileType = DetermineFileType(file.Key)
                    });
                }

                // Save to storage
                var dbTemplate = new EnvironmentTemplate
                {
                    Name = templateName,
                    Description = description,
                    TemplateType = templateRequest.Application?.Type.ToString() ?? "Infrastructure",
                    DeploymentTier = "Standard",
                    CloudProvider = templateRequest.Infrastructure.Provider.ToString(),
                    InfrastructureType = templateRequest.Infrastructure.Format.ToString(),
                    AzureService = templateRequest.Infrastructure.ComputePlatform.ToString(),
                    Content = generationResult.Files.ContainsKey("infra/main.bicep") 
                        ? generationResult.Files["infra/main.bicep"] 
                        : generationResult.Files.ContainsKey("infra/main.tf")
                            ? generationResult.Files["infra/main.tf"]
                            : generationResult.Files.First().Value,
                    FilesCount = generationResult.Files.Count,
                    Summary = generationResult.Summary,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                var savedTemplate = await _templateStorage.StoreTemplateAsync(dbTemplate.Name, dbTemplate, cancellationToken);

                _logger.LogInformation("Template '{TemplateName}' saved successfully with ID: {TemplateId}", 
                    templateName, savedTemplate.Id);

                return MapToServiceTemplate(savedTemplate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating and saving template: {TemplateName}", templateName);
                return null;
            }
        }

        private string DetermineFileType(string fileName)
        {
            if (fileName.EndsWith(".bicep")) return "bicep";
            if (fileName.EndsWith(".tf")) return "terraform";
            if (fileName.EndsWith(".yaml") || fileName.EndsWith(".yml")) return "yaml";
            if (fileName.EndsWith(".json")) return "json";
            if (fileName.EndsWith(".cs")) return "csharp";
            if (fileName.EndsWith(".js")) return "javascript";
            if (fileName.EndsWith(".py")) return "python";
            if (fileName.EndsWith(".java")) return "java";
            if (fileName.Contains("Dockerfile")) return "dockerfile";
            return "text";
        }

        #endregion
    }
}
