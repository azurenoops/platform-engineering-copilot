using System.Text;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.TemplateGeneration;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Composite;

/// <summary>
/// Orchestrates generation of multi-resource infrastructure with dependencies
/// Always produces a main.bicep or main.tf that orchestrates all modules
/// Uses composition pattern: core resource + cross-cutting modules
/// </summary>
public class CompositeInfrastructureGenerator : ICompositeInfrastructureGenerator
{
    private readonly ILogger<CompositeInfrastructureGenerator> _logger;
    private readonly IEnumerable<IModuleGenerator> _moduleGenerators;
    private readonly IEnumerable<ICrossCuttingModuleGenerator> _crossCuttingGenerators;

    public CompositeInfrastructureGenerator(
        ILogger<CompositeInfrastructureGenerator> logger,
        IEnumerable<IModuleGenerator> moduleGenerators,
        IEnumerable<ICrossCuttingModuleGenerator> crossCuttingGenerators)
    {
        _logger = logger;
        _moduleGenerators = moduleGenerators;
        _crossCuttingGenerators = crossCuttingGenerators;
    }

    /// <summary>
    /// Generate composite infrastructure from a request
    /// </summary>
    public async Task<CompositeGenerationResult> GenerateAsync(
        CompositeInfrastructureRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new CompositeGenerationResult();
        var allFiles = new Dictionary<string, string>();

        try
        {
            _logger.LogInformation("🏗️ Starting composite infrastructure generation for pattern: {Pattern}",
                request.Pattern);

            // Expand architecture pattern into concrete resources if using a pre-defined pattern
            var resources = request.Pattern == ArchitecturePattern.Custom
                ? request.Resources
                : ExpandPattern(request);

            if (!resources.Any())
            {
                result.Success = false;
                result.ErrorMessage = "No resources specified and pattern did not expand to any resources";
                return result;
            }

            _logger.LogInformation("📦 Generating {Count} resources", resources.Count);

            // Build dependency graph and sort topologically
            var sortedResources = TopologicalSort(resources, request.Dependencies);

            // Track outputs from each generated resource for dependency wiring
            var resourceOutputs = new Dictionary<string, Dictionary<string, string>>();

            // Generate each resource module
            foreach (var resource in sortedResources)
            {
                var resourceResult = await GenerateResourceModuleAsync(
                    resource,
                    request,
                    resourceOutputs,
                    cancellationToken);

                result.ResourceResults.Add(resourceResult);

                if (!resourceResult.Success)
                {
                    _logger.LogWarning("⚠️ Failed to generate resource: {ResourceId}", resource.Id);
                    continue;
                }

                result.ModulePaths.Add(resourceResult.ModulePath);

                // Track outputs for dependency wiring
                resourceOutputs[resource.Id] = new Dictionary<string, string>();
                foreach (var output in resourceResult.OutputNames)
                {
                    resourceOutputs[resource.Id][output] = $"module.{resource.Id}.outputs.{output}";
                }
            }

            // Generate the main orchestrator file (main.bicep or main.tf)
            var mainFile = request.Format == InfrastructureFormat.Bicep
                ? GenerateBicepMain(request, sortedResources, result.ModulePaths)
                : GenerateTerraformMain(request, sortedResources, result.ModulePaths);

            var mainFileName = request.Format == InfrastructureFormat.Bicep
                ? "main.bicep"
                : "main.tf";

            allFiles[mainFileName] = mainFile.Content;
            result.MainFilePath = mainFileName;

            // Add parameters file
            var paramsFile = request.Format == InfrastructureFormat.Bicep
                ? GenerateBicepParameters(request)
                : GenerateTerraformVariables(request);

            var paramsFileName = request.Format == InfrastructureFormat.Bicep
                ? "main.parameters.json"
                : "variables.tf";

            allFiles[paramsFileName] = paramsFile;

            // Generate module files
            foreach (var resourceResult in result.ResourceResults.Where(r => r.Success))
            {
                var moduleFiles = await GenerateModuleFilesAsync(
                    sortedResources.First(r => r.Id == resourceResult.ResourceId),
                    request,
                    cancellationToken);

                foreach (var file in moduleFiles)
                {
                    allFiles[file.Key] = file.Value;
                }
            }

            // Add README
            allFiles["README.md"] = GenerateReadme(request, sortedResources);

            result.Files = allFiles;
            result.Success = result.ResourceResults.All(r => r.Success) || result.ResourceResults.Any(r => r.Success);
            result.Summary = GenerateSummary(request, result);

            _logger.LogInformation("✅ Composite generation complete: {FileCount} files, {ModuleCount} modules",
                allFiles.Count, result.ModulePaths.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Composite infrastructure generation failed");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Expand a pre-defined architecture pattern into concrete resource specs
    /// </summary>
    private List<ResourceSpec> ExpandPattern(CompositeInfrastructureRequest request)
    {
        var resources = new List<ResourceSpec>();

        switch (request.Pattern)
        {
            case ArchitecturePattern.ThreeTier:
                resources = ExpandThreeTierPattern(request);
                break;

            case ArchitecturePattern.AksWithVNet:
                resources = ExpandAksWithVNetPattern(request);
                break;

            case ArchitecturePattern.LandingZone:
                resources = ExpandLandingZonePattern(request);
                break;

            case ArchitecturePattern.Microservices:
                resources = ExpandMicroservicesPattern(request);
                break;

            case ArchitecturePattern.Serverless:
                resources = ExpandServerlessPattern(request);
                break;

            case ArchitecturePattern.DataPlatform:
                resources = ExpandDataPlatformPattern(request);
                break;

            case ArchitecturePattern.SccaCompliant:
                resources = ExpandSccaCompliantPattern(request);
                break;
        }

        _logger.LogInformation("📐 Expanded pattern {Pattern} to {Count} resources",
            request.Pattern, resources.Count);

        return resources;
    }

    /// <summary>
    /// 3-tier pattern: VNet with web/app/data subnets + NSGs
    /// </summary>
    private List<ResourceSpec> ExpandThreeTierPattern(CompositeInfrastructureRequest request)
    {
        var subnets = SccaNetworkConfiguration.GetThreeTierSubnets();

        return new List<ResourceSpec>
        {
            new ResourceSpec
            {
                Id = "vnet",
                Name = $"{request.ServiceName}-vnet",
                ResourceType = "vnet",
                Platform = ComputePlatform.Networking,
                Configuration = new Dictionary<string, object>
                {
                    ["addressSpace"] = "10.0.0.0/16",
                    ["subnets"] = subnets
                }
            },
            new ResourceSpec
            {
                Id = "nsg-web",
                Name = $"{request.ServiceName}-nsg-web",
                ResourceType = "nsg",
                Platform = ComputePlatform.Networking,
                ParentResourceId = "vnet",
                Configuration = new Dictionary<string, object>
                {
                    ["subnetName"] = "web-tier",
                    ["rules"] = SccaNetworkConfiguration.GetWebTierNsgRules()
                }
            },
            new ResourceSpec
            {
                Id = "nsg-app",
                Name = $"{request.ServiceName}-nsg-app",
                ResourceType = "nsg",
                Platform = ComputePlatform.Networking,
                ParentResourceId = "vnet",
                Configuration = new Dictionary<string, object>
                {
                    ["subnetName"] = "app-tier",
                    ["rules"] = SccaNetworkConfiguration.GetAppTierNsgRules()
                }
            },
            new ResourceSpec
            {
                Id = "nsg-data",
                Name = $"{request.ServiceName}-nsg-data",
                ResourceType = "nsg",
                Platform = ComputePlatform.Networking,
                ParentResourceId = "vnet",
                Configuration = new Dictionary<string, object>
                {
                    ["subnetName"] = "data-tier",
                    ["rules"] = SccaNetworkConfiguration.GetDataTierNsgRules()
                }
            }
        };
    }

    /// <summary>
    /// AKS with VNet pattern: VNet + AKS + ACR + Key Vault + Managed Identity
    /// </summary>
    private List<ResourceSpec> ExpandAksWithVNetPattern(CompositeInfrastructureRequest request)
    {
        var aksSubnets = SccaNetworkConfiguration.GetAksSubnets();

        return new List<ResourceSpec>
        {
            new ResourceSpec
            {
                Id = "vnet",
                Name = $"{request.ServiceName}-vnet",
                ResourceType = "vnet",
                Platform = ComputePlatform.Networking,
                Configuration = new Dictionary<string, object>
                {
                    ["addressSpace"] = "10.0.0.0/16",
                    ["subnets"] = aksSubnets
                }
            },
            new ResourceSpec
            {
                Id = "identity",
                Name = $"{request.ServiceName}-identity",
                ResourceType = "managed-identity",
                Platform = ComputePlatform.Security,
                Configuration = new Dictionary<string, object>()
            },
            new ResourceSpec
            {
                Id = "acr",
                Name = $"{request.ServiceName}acr",
                ResourceType = "container-registry",
                Platform = ComputePlatform.Storage,
                Configuration = new Dictionary<string, object>
                {
                    ["sku"] = "Premium",
                    ["adminEnabled"] = false
                }
            },
            new ResourceSpec
            {
                Id = "keyvault",
                Name = $"{request.ServiceName}-kv",
                ResourceType = "keyvault",
                Platform = ComputePlatform.Security,
                Configuration = new Dictionary<string, object>
                {
                    ["enableRbacAuthorization"] = true,
                    ["enableSoftDelete"] = true,
                    ["softDeleteRetentionInDays"] = 90
                }
            },
            new ResourceSpec
            {
                Id = "aks",
                Name = $"{request.ServiceName}-aks",
                ResourceType = "aks",
                Platform = ComputePlatform.AKS,
                ParentResourceId = "vnet",
                Configuration = new Dictionary<string, object>
                {
                    ["nodeCount"] = 3,
                    ["vmSize"] = "Standard_D4s_v3",
                    ["enableWorkloadIdentity"] = true,
                    ["enablePrivateCluster"] = true,
                    ["systemSubnetName"] = "aks-system",
                    ["userSubnetName"] = "aks-user"
                }
            }
        };
    }

    /// <summary>
    /// Landing Zone pattern: Hub-spoke VNet + Management + Shared Services + AKS
    /// </summary>
    private List<ResourceSpec> ExpandLandingZonePattern(CompositeInfrastructureRequest request)
    {
        var landingZoneSubnets = SccaNetworkConfiguration.GetLandingZoneSubnets();

        return new List<ResourceSpec>
        {
            new ResourceSpec
            {
                Id = "vnet",
                Name = $"{request.ServiceName}-vnet",
                ResourceType = "vnet",
                Platform = ComputePlatform.Networking,
                Configuration = new Dictionary<string, object>
                {
                    ["addressSpace"] = "10.0.0.0/16",
                    ["subnets"] = landingZoneSubnets
                }
            },
            new ResourceSpec
            {
                Id = "log-analytics",
                Name = $"{request.ServiceName}-logs",
                ResourceType = "log-analytics",
                Platform = ComputePlatform.Security,
                Configuration = new Dictionary<string, object>
                {
                    ["retentionInDays"] = 90,
                    ["sku"] = "PerGB2018"
                }
            },
            new ResourceSpec
            {
                Id = "keyvault",
                Name = $"{request.ServiceName}-kv",
                ResourceType = "keyvault",
                Platform = ComputePlatform.Security,
                Configuration = new Dictionary<string, object>
                {
                    ["enableRbacAuthorization"] = true,
                    ["enableSoftDelete"] = true
                }
            },
            new ResourceSpec
            {
                Id = "identity",
                Name = $"{request.ServiceName}-identity",
                ResourceType = "managed-identity",
                Platform = ComputePlatform.Security,
                Configuration = new Dictionary<string, object>()
            },
            new ResourceSpec
            {
                Id = "acr",
                Name = $"{request.ServiceName}acr",
                ResourceType = "container-registry",
                Platform = ComputePlatform.Storage,
                Configuration = new Dictionary<string, object>
                {
                    ["sku"] = "Premium"
                }
            },
            new ResourceSpec
            {
                Id = "aks",
                Name = $"{request.ServiceName}-aks",
                ResourceType = "aks",
                Platform = ComputePlatform.AKS,
                ParentResourceId = "vnet",
                Configuration = new Dictionary<string, object>
                {
                    ["nodeCount"] = 3,
                    ["enableWorkloadIdentity"] = true,
                    ["subnetName"] = "workload"
                }
            }
        };
    }

    /// <summary>
    /// Microservices pattern: AKS + Service Mesh + Observability
    /// </summary>
    private List<ResourceSpec> ExpandMicroservicesPattern(CompositeInfrastructureRequest request)
    {
        var resources = ExpandAksWithVNetPattern(request);

        // Add observability components
        resources.Add(new ResourceSpec
        {
            Id = "app-insights",
            Name = $"{request.ServiceName}-ai",
            ResourceType = "application-insights",
            Platform = ComputePlatform.Security,
            Configuration = new Dictionary<string, object>
            {
                ["applicationType"] = "web"
            }
        });

        return resources;
    }

    /// <summary>
    /// Serverless pattern: Functions + Storage + Event Grid
    /// </summary>
    private List<ResourceSpec> ExpandServerlessPattern(CompositeInfrastructureRequest request)
    {
        return new List<ResourceSpec>
        {
            new ResourceSpec
            {
                Id = "storage",
                Name = $"{request.ServiceName}stor",
                ResourceType = "storage-account",
                Platform = ComputePlatform.Storage,
                Configuration = new Dictionary<string, object>
                {
                    ["sku"] = "Standard_LRS",
                    ["kind"] = "StorageV2"
                }
            },
            new ResourceSpec
            {
                Id = "app-insights",
                Name = $"{request.ServiceName}-ai",
                ResourceType = "application-insights",
                Platform = ComputePlatform.Security,
                Configuration = new Dictionary<string, object>()
            },
            new ResourceSpec
            {
                Id = "functions",
                Name = $"{request.ServiceName}-func",
                ResourceType = "function-app",
                Platform = ComputePlatform.Functions,
                Configuration = new Dictionary<string, object>
                {
                    ["runtime"] = "dotnet-isolated",
                    ["version"] = "4"
                }
            }
        };
    }

    /// <summary>
    /// Data Platform pattern: Storage + SQL + Cosmos + Data Factory
    /// </summary>
    private List<ResourceSpec> ExpandDataPlatformPattern(CompositeInfrastructureRequest request)
    {
        return new List<ResourceSpec>
        {
            new ResourceSpec
            {
                Id = "storage",
                Name = $"{request.ServiceName}datalake",
                ResourceType = "storage-account",
                Platform = ComputePlatform.Storage,
                Configuration = new Dictionary<string, object>
                {
                    ["kind"] = "StorageV2",
                    ["isHnsEnabled"] = true,  // Enable hierarchical namespace for Data Lake
                    ["sku"] = "Standard_LRS"
                }
            },
            new ResourceSpec
            {
                Id = "sql",
                Name = $"{request.ServiceName}-sql",
                ResourceType = "sql-database",
                Platform = ComputePlatform.Database,
                Configuration = new Dictionary<string, object>
                {
                    ["sku"] = "S1"
                }
            },
            new ResourceSpec
            {
                Id = "keyvault",
                Name = $"{request.ServiceName}-kv",
                ResourceType = "keyvault",
                Platform = ComputePlatform.Security,
                Configuration = new Dictionary<string, object>()
            }
        };
    }

    /// <summary>
    /// SCCA-Compliant pattern: Full Landing Zone with Firewall + Bastion
    /// </summary>
    private List<ResourceSpec> ExpandSccaCompliantPattern(CompositeInfrastructureRequest request)
    {
        var resources = ExpandLandingZonePattern(request);

        // SCCA requires additional security controls
        resources.Add(new ResourceSpec
        {
            Id = "bastion",
            Name = $"{request.ServiceName}-bastion",
            ResourceType = "bastion",
            Platform = ComputePlatform.Security,
            ParentResourceId = "vnet",
            Configuration = new Dictionary<string, object>
            {
                ["subnetName"] = "AzureBastionSubnet"
            }
        });

        return resources;
    }

    /// <summary>
    /// Topological sort of resources by dependencies
    /// </summary>
    private List<ResourceSpec> TopologicalSort(List<ResourceSpec> resources, List<ResourceDependency> dependencies)
    {
        // Build adjacency list and in-degree map
        var adjacency = new Dictionary<string, List<string>>();
        var inDegree = new Dictionary<string, int>();

        foreach (var resource in resources)
        {
            adjacency[resource.Id] = new List<string>();
            inDegree[resource.Id] = 0;
        }

        // Add explicit dependencies
        foreach (var dep in dependencies)
        {
            if (adjacency.ContainsKey(dep.TargetResourceId) && inDegree.ContainsKey(dep.SourceResourceId))
            {
                adjacency[dep.TargetResourceId].Add(dep.SourceResourceId);
                inDegree[dep.SourceResourceId]++;
            }
        }

        // Add implicit dependencies from ParentResourceId
        foreach (var resource in resources)
        {
            if (!string.IsNullOrEmpty(resource.ParentResourceId) &&
                adjacency.ContainsKey(resource.ParentResourceId))
            {
                adjacency[resource.ParentResourceId].Add(resource.Id);
                inDegree[resource.Id]++;
            }
        }

        // Kahn's algorithm
        var queue = new Queue<string>();
        foreach (var kv in inDegree.Where(kv => kv.Value == 0))
        {
            queue.Enqueue(kv.Key);
        }

        var sorted = new List<ResourceSpec>();
        var resourceMap = resources.ToDictionary(r => r.Id);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (resourceMap.TryGetValue(current, out var resource))
            {
                sorted.Add(resource);
            }

            foreach (var neighbor in adjacency[current])
            {
                inDegree[neighbor]--;
                if (inDegree[neighbor] == 0)
                {
                    queue.Enqueue(neighbor);
                }
            }
        }

        if (sorted.Count != resources.Count)
        {
            _logger.LogWarning("⚠️ Dependency cycle detected, returning unsorted resources");
            return resources;
        }

        return sorted;
    }

    /// <summary>
    /// Generate a single resource module
    /// </summary>
    private async Task<ResourceGenerationResult> GenerateResourceModuleAsync(
        ResourceSpec resource,
        CompositeInfrastructureRequest request,
        Dictionary<string, Dictionary<string, string>> parentOutputs,
        CancellationToken cancellationToken)
    {
        var result = new ResourceGenerationResult
        {
            ResourceId = resource.Id,
            ResourceType = resource.ResourceType,
            ModulePath = $"modules/{resource.Id}"
        };

        try
        {
            // Find appropriate generator
            var generator = _moduleGenerators.FirstOrDefault(g =>
                g.Format == request.Format &&
                g.Platform == resource.Platform &&
                g.Provider == request.Provider);

            if (generator == null)
            {
                // Try matching by resource type string
                generator = _moduleGenerators.FirstOrDefault(g =>
                    g.Format == request.Format &&
                    g.Provider == request.Provider &&
                    MatchesResourceType(g.Platform, resource.ResourceType));
            }

            if (generator == null)
            {
                _logger.LogWarning("No generator found for {ResourceType} ({Platform}) with {Format}",
                    resource.ResourceType, resource.Platform, request.Format);

                result.Success = false;
                result.ErrorMessage = $"No generator found for resource type: {resource.ResourceType}";
                return result;
            }

            // Build template generation request
            var templateRequest = BuildTemplateRequest(resource, request);

            // Wire in parent outputs
            if (!string.IsNullOrEmpty(resource.ParentResourceId) &&
                parentOutputs.TryGetValue(resource.ParentResourceId, out var outputs))
            {
                templateRequest.Infrastructure!.NetworkConfig ??= new NetworkingConfiguration();
                // Pass VNet reference if parent is VNet
                if (outputs.TryGetValue("vnetId", out var vnetId))
                {
                    templateRequest.Infrastructure.NetworkConfig.ExistingVNetResourceId = vnetId;
                    templateRequest.Infrastructure.NetworkConfig.Mode = NetworkMode.UseExisting;
                }
            }

            result.Success = true;

            // Determine expected outputs based on resource type
            result.OutputNames = GetResourceOutputNames(resource);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate resource module: {ResourceId}", resource.Id);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    /// <summary>
    /// Generate the actual module files for a resource using composition pattern:
    /// 1. Generate core resource module
    /// 2. Attach cross-cutting modules (PE, diagnostics, RBAC, NSG) based on request
    /// </summary>
    private async Task<Dictionary<string, string>> GenerateModuleFilesAsync(
        ResourceSpec resource,
        CompositeInfrastructureRequest request,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, string>();
        var modulePath = $"modules/{resource.Id}";

        // Build template generation request
        var templateRequest = BuildTemplateRequest(resource, request);

        // Find appropriate generator - prefer IResourceModuleGenerator for composition
        var resourceGenerator = _moduleGenerators
            .OfType<IResourceModuleGenerator>()
            .FirstOrDefault(g =>
                g.Format == request.Format &&
                g.Provider == request.Provider &&
                g.SupportedResourceTypes.Any(rt => rt.Equals(resource.ResourceType, StringComparison.OrdinalIgnoreCase)));

        if (resourceGenerator != null)
        {
            // Use composition pattern: core + cross-cutting
            _logger.LogDebug("🔧 Using composition pattern for {ResourceType}", resource.ResourceType);
            
            var coreResult = resourceGenerator.GenerateCoreResource(templateRequest);
            foreach (var file in coreResult.Files)
            {
                var filePath = file.Key.StartsWith("modules/") ? file.Key : $"{modulePath}/{file.Key}";
                files[filePath] = file.Value;
            }

            // Generate cross-cutting modules based on supported capabilities and request
            await GenerateCrossCuttingModulesAsync(
                resource, request, templateRequest, modulePath, coreResult, files, cancellationToken);
        }
        else
        {
            // Fallback to legacy GenerateModule() for generators not yet migrated
            var generator = _moduleGenerators.FirstOrDefault(g =>
                g.Format == request.Format &&
                (g.Platform == resource.Platform || MatchesResourceType(g.Platform, resource.ResourceType)) &&
                g.Provider == request.Provider);

            if (generator != null)
            {
                _logger.LogDebug("📦 Using legacy GenerateModule for {ResourceType}", resource.ResourceType);
                var moduleFiles = generator.GenerateModule(templateRequest);
                foreach (var file in moduleFiles)
                {
                    var filePath = file.Key.StartsWith("modules/") ? file.Key : $"{modulePath}/{file.Key}";
                    files[filePath] = file.Value;
                }
            }
            else
            {
                // Generate placeholder module
                files[$"{modulePath}/main.bicep"] = GeneratePlaceholderModule(resource, request);
            }
        }

        return files;
    }

    /// <summary>
    /// Generate cross-cutting modules (Private Endpoint, Diagnostics, RBAC, NSG) for a resource
    /// Note: All diagnostic settings reference a single shared Log Analytics Workspace
    /// </summary>
    private async Task GenerateCrossCuttingModulesAsync(
        ResourceSpec resource,
        CompositeInfrastructureRequest request,
        TemplateGenerationRequest templateRequest,
        string modulePath,
        ResourceModuleResult coreResult,
        Dictionary<string, string> files,
        CancellationToken cancellationToken)
    {
        var security = templateRequest.Security ?? new SecuritySpec();
        var observability = templateRequest.Observability ?? new ObservabilitySpec();

        // Private Endpoint - use unique name combining resource name and ID
        if (security.EnablePrivateEndpoint == true)
        {
            var peGenerator = _crossCuttingGenerators
                .FirstOrDefault(g => g.Type == CrossCuttingType.PrivateEndpoint && g.Format == request.Format);
            
            if (peGenerator?.CanGenerate(resource.ResourceType) == true)
            {
                // Use resource.Id for unique PE naming to avoid collisions
                var uniquePeName = $"{resource.Name}-{resource.Id}";
                var peRequest = new CrossCuttingRequest
                {
                    ResourceType = resource.ResourceType,
                    ResourceName = uniquePeName,  // Unique name per resource
                    ResourceReference = coreResult.ResourceReference,
                    Location = request.Region,
                    Tags = request.Tags,
                    PrivateEndpoint = new PrivateEndpointConfig
                    {
                        SubnetId = "${subnetId}", // Will be wired via parameters
                        CreateDnsZone = true
                    }
                };
                
                var peFiles = peGenerator.GenerateModule(peRequest);
                foreach (var file in peFiles)
                {
                    files[$"{modulePath}/{file.Key}"] = file.Value;
                }
                _logger.LogDebug("🔒 Added Private Endpoint module for {ResourceType} with unique name {PeName}", 
                    resource.ResourceType, uniquePeName);
            }
        }

        // Diagnostic Settings - all resources reference the SAME shared Log Analytics Workspace
        // The shared LAWS is created at composite level, not per-resource
        if (observability.EnableDiagnostics == true)
        {
            var diagGenerator = _crossCuttingGenerators
                .FirstOrDefault(g => g.Type == CrossCuttingType.DiagnosticSettings && g.Format == request.Format);
            
            if (diagGenerator?.CanGenerate(resource.ResourceType) == true)
            {
                var diagRequest = new CrossCuttingRequest
                {
                    ResourceType = resource.ResourceType,
                    ResourceName = resource.Name,
                    ResourceReference = coreResult.ResourceReference,
                    DiagnosticSettings = new DiagnosticSettingsConfig
                    {
                        // Reference shared LAWS - wired via main orchestrator
                        WorkspaceId = "${sharedLogAnalyticsWorkspaceId}",
                        RetentionDays = 90 // FedRAMP AU-11 compliant default
                    }
                };
                
                var diagFiles = diagGenerator.GenerateModule(diagRequest);
                foreach (var file in diagFiles)
                {
                    files[$"{modulePath}/{file.Key}"] = file.Value;
                }
                _logger.LogDebug("📊 Added Diagnostic Settings for {ResourceType} (references shared LAWS)", 
                    resource.ResourceType);
            }
        }

        // RBAC
        if (security.RBAC)
        {
            var rbacGenerator = _crossCuttingGenerators
                .FirstOrDefault(g => g.Type == CrossCuttingType.RBACAssignment && g.Format == request.Format);
            
            if (rbacGenerator?.CanGenerate(resource.ResourceType) == true)
            {
                var rbacRequest = new CrossCuttingRequest
                {
                    ResourceType = resource.ResourceType,
                    ResourceName = resource.Name,
                    ResourceReference = coreResult.ResourceReference,
                    RBAC = new RBACAssignmentConfig
                    {
                        PrincipalId = "${principalId}",
                        PrincipalType = "ServicePrincipal"
                    }
                };
                
                var rbacFiles = rbacGenerator.GenerateModule(rbacRequest);
                foreach (var file in rbacFiles)
                {
                    files[$"{modulePath}/{file.Key}"] = file.Value;
                }
                _logger.LogDebug("🔐 Added RBAC module for {ResourceType}", resource.ResourceType);
            }
        }

        await Task.CompletedTask; // Async for future extensibility
    }

    /// <summary>
    /// Build TemplateGenerationRequest from ResourceSpec
    /// Maps ResourceSpec.Configuration to strongly-typed InfrastructureSpec properties
    /// </summary>
    private TemplateGenerationRequest BuildTemplateRequest(ResourceSpec resource, CompositeInfrastructureRequest request)
    {
        var templateRequest = new TemplateGenerationRequest
        {
            ServiceName = resource.Name,
            Description = $"{resource.ResourceType} for {request.ServiceName}",
            Infrastructure = new InfrastructureSpec
            {
                Format = request.Format,
                Provider = request.Provider,
                Region = request.Region,
                ComputePlatform = resource.Platform,
                Environment = request.Environment,
                Tags = request.Tags.Any() ? request.Tags : null
            }
        };

        // Map configuration based on resource type/platform
        MapConfigurationToInfrastructureSpec(resource, templateRequest);

        return templateRequest;
    }

    /// <summary>
    /// Maps ResourceSpec.Configuration to strongly-typed InfrastructureSpec properties
    /// </summary>
    private void MapConfigurationToInfrastructureSpec(ResourceSpec resource, TemplateGenerationRequest request)
    {
        var config = resource.Configuration;
        if (config == null || !config.Any())
            return;

        request.Infrastructure ??= new InfrastructureSpec();

        switch (resource.Platform)
        {
            case ComputePlatform.Networking:
                MapNetworkConfiguration(config, request);
                break;

            case ComputePlatform.AKS:
                MapAksConfiguration(config, request);
                break;

            case ComputePlatform.Security:
                MapSecurityConfiguration(config, request, resource.ResourceType);
                break;

            case ComputePlatform.Storage:
                MapStorageConfiguration(config, request, resource.ResourceType);
                break;

            case ComputePlatform.Database:
                MapDatabaseConfiguration(config, request);
                break;
        }
    }

    private void MapNetworkConfiguration(Dictionary<string, object> config, TemplateGenerationRequest request)
    {
        request.Infrastructure!.NetworkConfig ??= new NetworkingConfiguration();
        var networkConfig = request.Infrastructure.NetworkConfig;

        if (config.TryGetValue("addressSpace", out var addressSpace))
            networkConfig.VNetAddressSpace = addressSpace?.ToString() ?? "10.0.0.0/16";

        // Handle subnets - they come as List<SubnetConfiguration> from pattern expansion
        if (config.TryGetValue("subnets", out var subnets))
        {
            if (subnets is IEnumerable<SubnetConfiguration> subnetList)
            {
                networkConfig.Subnets = subnetList.ToList();
            }
        }

        if (config.TryGetValue("enableDDoSProtection", out var ddos) && ddos is bool enableDdos)
            networkConfig.EnableDDoSProtection = enableDdos;

        if (config.TryGetValue("enableNsg", out var nsg) && nsg is bool enableNsg)
            networkConfig.EnableNetworkSecurityGroup = enableNsg;
    }

    private void MapAksConfiguration(Dictionary<string, object> config, TemplateGenerationRequest request)
    {
        // Map to flat InfrastructureSpec properties for AKS
        var infra = request.Infrastructure!;

        if (config.TryGetValue("nodeCount", out var nodeCount) && nodeCount is int count)
            infra.NodeCount = count;

        if (config.TryGetValue("vmSize", out var vmSize))
            infra.VmSize = vmSize?.ToString() ?? "Standard_D4s_v3";

        if (config.TryGetValue("enableWorkloadIdentity", out var workloadIdentity) && workloadIdentity is bool enableWi)
            infra.EnableWorkloadIdentity = enableWi;

        if (config.TryGetValue("enablePrivateCluster", out var privateCluster) && privateCluster is bool enablePrivate)
            infra.EnablePrivateCluster = enablePrivate;

        if (config.TryGetValue("kubernetesVersion", out var version))
            infra.KubernetesVersion = version?.ToString() ?? "1.30";
    }

    private void MapSecurityConfiguration(Dictionary<string, object> config, TemplateGenerationRequest request, string resourceType)
    {
        var normalized = resourceType.ToLowerInvariant();
        var infra = request.Infrastructure!;

        // KeyVault configuration - append config to Description for adapter to parse
        if (normalized.Contains("keyvault") || normalized.Contains("vault"))
        {
            if (config.TryGetValue("enableRbacAuthorization", out var rbac) && rbac is bool enableRbac)
                request.Description += $" [RBAC: {enableRbac}]";

            if (config.TryGetValue("enableSoftDelete", out var softDelete) && softDelete is bool enableSoftDelete)
                request.Description += $" [SoftDelete: {enableSoftDelete}]";

            if (config.TryGetValue("softDeleteRetentionInDays", out var retention) && retention is int retentionDays)
                request.Description += $" [Retention: {retentionDays}]";
        }
        // Log Analytics - use LogAnalyticsWorkspaceId field if available
        else if (normalized.Contains("log") || normalized.Contains("analytics"))
        {
            if (config.TryGetValue("retentionInDays", out var retention) && retention is int retentionDays)
                request.Description += $" [Retention: {retentionDays}]";

            if (config.TryGetValue("sku", out var sku))
                request.Description += $" [SKU: {sku}]";
        }
        // Managed Identity - minimal configuration
        else if (normalized.Contains("identity"))
        {
            infra.EnableManagedIdentity = true;
        }
    }

    private void MapStorageConfiguration(Dictionary<string, object> config, TemplateGenerationRequest request, string resourceType)
    {
        var normalized = resourceType.ToLowerInvariant();

        if (normalized.Contains("acr") || normalized.Contains("registry") || normalized.Contains("container"))
        {
            // ACR-specific configuration - append to Description for adapter
            if (config.TryGetValue("sku", out var sku))
                request.Description += $" [SKU: {sku}]";

            if (config.TryGetValue("adminEnabled", out var admin) && admin is bool adminEnabled)
                request.Description += $" [Admin: {adminEnabled}]";
        }
        else
        {
            // General storage configuration - use IncludeStorage flag
            request.Infrastructure!.IncludeStorage = true;
            if (config.TryGetValue("sku", out var sku))
                request.Description += $" [SKU: {sku}]";
        }
    }

    private void MapDatabaseConfiguration(Dictionary<string, object> config, TemplateGenerationRequest request)
    {
        // Database configuration - append to Description for adapter
        if (config.TryGetValue("sku", out var sku))
            request.Description += $" [SKU: {sku}]";

        if (config.TryGetValue("enablePublicAccess", out var publicAccess) && publicAccess is bool enablePublic)
            request.Description += $" [PublicAccess: {enablePublic}]";
    }

    /// <summary>
    /// Check if a ComputePlatform matches a resource type string
    /// </summary>
    private bool MatchesResourceType(ComputePlatform platform, string resourceType)
    {
        var normalized = resourceType.ToLowerInvariant().Replace("-", "").Replace("_", "");

        return platform switch
        {
            ComputePlatform.AKS => normalized.Contains("aks") || normalized.Contains("kubernetes"),
            ComputePlatform.Networking => normalized.Contains("vnet") || normalized.Contains("network") || normalized.Contains("nsg"),
            ComputePlatform.Storage => normalized.Contains("storage") || normalized.Contains("acr") || normalized.Contains("container"),
            ComputePlatform.Database => normalized.Contains("sql") || normalized.Contains("database") || normalized.Contains("cosmos"),
            ComputePlatform.Security => normalized.Contains("keyvault") || normalized.Contains("identity") || normalized.Contains("bastion"),
            ComputePlatform.Functions => normalized.Contains("function"),
            ComputePlatform.AppService => normalized.Contains("webapp") || normalized.Contains("appservice"),
            _ => false
        };
    }

    /// <summary>
    /// Get standard output names for a resource type
    /// All adapters now output at minimum: resourceId, resourceName
    /// Plus type-specific outputs for dependency wiring
    /// </summary>
    private List<string> GetResourceOutputNames(ResourceSpec resource)
    {
        // Base outputs all adapters provide
        var outputs = new List<string> { "resourceId", "resourceName" };

        // Add type-specific outputs
        switch (resource.Platform)
        {
            case ComputePlatform.Networking:
                outputs.AddRange(new[] { "vnetId", "vnetName", "subnetIds" });
                break;

            case ComputePlatform.AKS:
                outputs.AddRange(new[] { "aksName", "kubeletIdentityId", "nodeResourceGroup", "oidcIssuerUrl" });
                break;

            case ComputePlatform.Storage:
                // Differentiate between storage account and ACR
                var resourceType = resource.ResourceType.ToLowerInvariant();
                if (resourceType.Contains("acr") || resourceType.Contains("registry"))
                {
                    outputs.AddRange(new[] { "loginServer", "acrId" });
                }
                else
                {
                    outputs.AddRange(new[] { "primaryEndpoint", "primaryKey" });
                }
                break;

            case ComputePlatform.Security:
                // Differentiate between KeyVault, Identity, and Log Analytics
                var securityType = resource.ResourceType.ToLowerInvariant();
                if (securityType.Contains("identity"))
                {
                    outputs.AddRange(new[] { "principalId", "clientId", "tenantId" });
                }
                else if (securityType.Contains("log") || securityType.Contains("analytics"))
                {
                    outputs.AddRange(new[] { "workspaceId", "primarySharedKey" });
                }
                else
                {
                    // KeyVault
                    outputs.AddRange(new[] { "vaultUri", "vaultId" });
                }
                break;

            case ComputePlatform.Database:
                outputs.AddRange(new[] { "serverId", "serverName", "connectionString", "fqdn" });
                break;

            case ComputePlatform.AppService:
                outputs.AddRange(new[] { "defaultHostName", "outboundIpAddresses" });
                break;

            case ComputePlatform.Functions:
                outputs.AddRange(new[] { "defaultHostName", "functionAppId" });
                break;

            case ComputePlatform.ContainerApps:
                outputs.AddRange(new[] { "fqdn", "latestRevisionFqdn" });
                break;
        }

        return outputs;
    }

    // ===== MAIN FILE GENERATION =====

    /// <summary>
    /// Generate main.bicep orchestrator
    /// Creates a single shared Log Analytics Workspace for all diagnostic settings
    /// </summary>
    private (string Content, List<string> ModuleReferences) GenerateBicepMain(
        CompositeInfrastructureRequest request,
        List<ResourceSpec> resources,
        List<string> modulePaths)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("// =============================================================================");
        sb.AppendLine($"// {request.ServiceName} - Composite Infrastructure");
        sb.AppendLine($"// Pattern: {request.Pattern}");
        sb.AppendLine($"// Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("// =============================================================================");
        sb.AppendLine();
        sb.AppendLine("targetScope = 'resourceGroup'");
        sb.AppendLine();

        // Parameters
        sb.AppendLine("// ===== PARAMETERS =====");
        sb.AppendLine();
        sb.AppendLine("@description('Base name for all resources')");
        sb.AppendLine($"param serviceName string = '{request.ServiceName}'");
        sb.AppendLine();
        sb.AppendLine("@description('Azure region for deployment')");
        sb.AppendLine($"param location string = '{request.Region}'");
        sb.AppendLine();
        sb.AppendLine("@description('Environment (dev, staging, prod)')");
        sb.AppendLine($"param environment string = '{request.Environment}'");
        sb.AppendLine();
        sb.AppendLine("@description('Tags to apply to all resources')");
        sb.AppendLine("param tags object = {");
        sb.AppendLine($"  Environment: environment");
        sb.AppendLine($"  ManagedBy: 'bicep'");
        sb.AppendLine($"  Service: serviceName");
        if (request.Tags.Any())
        {
            foreach (var tag in request.Tags)
            {
                sb.AppendLine($"  '{tag.Key}': '{tag.Value}'");
            }
        }
        sb.AppendLine("}");
        sb.AppendLine();

        // Check if any resource needs diagnostics - if so, create shared Log Analytics
        var needsSharedLaws = resources.Any(r => 
            r.ResourceType.ToLowerInvariant() != "log-analytics" && 
            r.ResourceType.ToLowerInvariant() != "logs");
        
        // Check if log-analytics already exists in resources
        var hasExplicitLaws = resources.Any(r => 
            r.ResourceType.ToLowerInvariant().Contains("log") || 
            r.ResourceType.ToLowerInvariant().Contains("analytics"));

        // Create shared Log Analytics Workspace if needed and not already in resources
        if (needsSharedLaws && !hasExplicitLaws)
        {
            sb.AppendLine("// ===== SHARED LOG ANALYTICS WORKSPACE =====");
            sb.AppendLine("// Single workspace for all resource diagnostic settings");
            sb.AppendLine();
            sb.AppendLine("resource sharedLogAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {");
            sb.AppendLine("  name: '${serviceName}-shared-logs'");
            sb.AppendLine("  location: location");
            sb.AppendLine("  tags: union(tags, {");
            sb.AppendLine("    'security-control': 'AU-2,AU-3,AU-6,AU-11'");
            sb.AppendLine("    'shared-resource': 'true'");
            sb.AppendLine("  })");
            sb.AppendLine("  properties: {");
            sb.AppendLine("    sku: {");
            sb.AppendLine("      name: 'PerGB2018'");
            sb.AppendLine("    }");
            sb.AppendLine("    retentionInDays: 90  // FedRAMP AU-11 minimum");
            sb.AppendLine("    features: {");
            sb.AppendLine("      enableLogAccessUsingOnlyResourcePermissions: true");
            sb.AppendLine("    }");
            sb.AppendLine("  }");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Module invocations
        sb.AppendLine("// ===== MODULES =====");
        sb.AppendLine();

        var moduleRefs = new List<string>();
        
        // Determine shared LAWS reference for diagnostic settings
        var sharedLawsRef = hasExplicitLaws 
            ? resources.First(r => r.ResourceType.ToLowerInvariant().Contains("log") || 
                                   r.ResourceType.ToLowerInvariant().Contains("analytics")).Id.Replace("-", "_")
            : "sharedLogAnalytics";
        
        foreach (var resource in resources)
        {
            var moduleName = resource.Id.Replace("-", "_");
            var modulePath = $"modules/{resource.Id}/main.bicep";
            moduleRefs.Add(moduleName);

            sb.AppendLine($"// {resource.Name} ({resource.ResourceType})");
            sb.AppendLine($"module {moduleName} './{modulePath}' = {{");
            sb.AppendLine($"  name: '{resource.Id}-deployment'");
            sb.AppendLine("  params: {");
            sb.AppendLine($"    name: '{resource.Name}'");
            sb.AppendLine("    location: location");
            sb.AppendLine("    tags: tags");
            
            // Pass shared LAWS reference for diagnostic settings (not to LAWS itself)
            var isLaws = resource.ResourceType.ToLowerInvariant().Contains("log") || 
                         resource.ResourceType.ToLowerInvariant().Contains("analytics");
            if (!isLaws)
            {
                if (hasExplicitLaws)
                {
                    sb.AppendLine($"    sharedLogAnalyticsWorkspaceId: {sharedLawsRef}.outputs.workspaceId");
                }
                else if (needsSharedLaws)
                {
                    sb.AppendLine($"    sharedLogAnalyticsWorkspaceId: sharedLogAnalytics.id");
                }
            }

            // Add dependency parameters
            if (!string.IsNullOrEmpty(resource.ParentResourceId))
            {
                var parentModule = resource.ParentResourceId.Replace("-", "_");
                if (resources.Any(r => r.Id == resource.ParentResourceId && r.Platform == ComputePlatform.Networking))
                {
                    sb.AppendLine($"    vnetId: {parentModule}.outputs.vnetId");
                }
            }

            // Add resource-specific config
            foreach (var config in resource.Configuration)
            {
                if (config.Value is string strValue)
                {
                    sb.AppendLine($"    {config.Key}: '{strValue}'");
                }
                else if (config.Value is int intValue)
                {
                    sb.AppendLine($"    {config.Key}: {intValue}");
                }
                else if (config.Value is bool boolValue)
                {
                    sb.AppendLine($"    {config.Key}: {boolValue.ToString().ToLower()}");
                }
            }

            sb.AppendLine("  }");

            // Add dependsOn for parent and shared LAWS
            var dependencies = new List<string>();
            if (!string.IsNullOrEmpty(resource.ParentResourceId))
            {
                dependencies.Add(resource.ParentResourceId.Replace("-", "_"));
            }
            if (!isLaws && needsSharedLaws && !hasExplicitLaws)
            {
                dependencies.Add("sharedLogAnalytics");
            }
            
            if (dependencies.Any())
            {
                sb.AppendLine($"  dependsOn: [{string.Join(", ", dependencies)}]");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Outputs
        sb.AppendLine("// ===== OUTPUTS =====");
        sb.AppendLine();
        
        // Output shared LAWS if created
        if (needsSharedLaws && !hasExplicitLaws)
        {
            sb.AppendLine("output sharedLogAnalyticsWorkspaceId string = sharedLogAnalytics.id");
            sb.AppendLine("output sharedLogAnalyticsWorkspaceName string = sharedLogAnalytics.name");
            sb.AppendLine();
        }
        
        foreach (var resource in resources)
        {
            var moduleName = resource.Id.Replace("-", "_");
            var outputPrefix = resource.Id.Replace("-", "_");

            // Output primary identifiers
            sb.AppendLine($"output {outputPrefix}_id string = {moduleName}.outputs.resourceId");

            if (resource.Platform == ComputePlatform.Networking)
            {
                sb.AppendLine($"output {outputPrefix}_name string = {moduleName}.outputs.vnetName");
            }
            else if (resource.Platform == ComputePlatform.AKS)
            {
                sb.AppendLine($"output {outputPrefix}_name string = {moduleName}.outputs.aksName");
            }
        }

        return (sb.ToString(), moduleRefs);
    }

    /// <summary>
    /// Generate main.tf orchestrator
    /// Creates a single shared Log Analytics Workspace for all diagnostic settings
    /// </summary>
    private (string Content, List<string> ModuleReferences) GenerateTerraformMain(
        CompositeInfrastructureRequest request,
        List<ResourceSpec> resources,
        List<string> modulePaths)
    {
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("# =============================================================================");
        sb.AppendLine($"# {request.ServiceName} - Composite Infrastructure");
        sb.AppendLine($"# Pattern: {request.Pattern}");
        sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine("# =============================================================================");
        sb.AppendLine();

        // Terraform block
        sb.AppendLine("terraform {");
        sb.AppendLine("  required_version = \">= 1.0.0\"");
        sb.AppendLine();
        sb.AppendLine("  required_providers {");
        sb.AppendLine("    azurerm = {");
        sb.AppendLine("      source  = \"hashicorp/azurerm\"");
        sb.AppendLine("      version = \"~> 3.0\"");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Provider
        sb.AppendLine("provider \"azurerm\" {");
        sb.AppendLine("  features {}");
        sb.AppendLine("}");
        sb.AppendLine();

        // Check if any resource needs diagnostics - if so, create shared Log Analytics
        var needsSharedLaws = resources.Any(r => 
            r.ResourceType.ToLowerInvariant() != "log-analytics" && 
            r.ResourceType.ToLowerInvariant() != "logs");
        
        // Check if log-analytics already exists in resources
        var hasExplicitLaws = resources.Any(r => 
            r.ResourceType.ToLowerInvariant().Contains("log") || 
            r.ResourceType.ToLowerInvariant().Contains("analytics"));

        // Locals
        sb.AppendLine("locals {");
        sb.AppendLine($"  service_name = var.service_name");
        sb.AppendLine($"  location     = var.location");
        sb.AppendLine($"  environment  = var.environment");
        sb.AppendLine("  common_tags = merge(var.tags, {");
        sb.AppendLine("    Environment = local.environment");
        sb.AppendLine("    ManagedBy   = \"terraform\"");
        sb.AppendLine("    Service     = local.service_name");
        sb.AppendLine("  })");
        sb.AppendLine("}");
        sb.AppendLine();

        // Resource Group
        sb.AppendLine("resource \"azurerm_resource_group\" \"main\" {");
        sb.AppendLine("  name     = \"rg-${local.service_name}-${local.environment}\"");
        sb.AppendLine("  location = local.location");
        sb.AppendLine("  tags     = local.common_tags");
        sb.AppendLine("}");
        sb.AppendLine();

        // Create shared Log Analytics Workspace if needed and not already in resources
        if (needsSharedLaws && !hasExplicitLaws)
        {
            sb.AppendLine("# ===== SHARED LOG ANALYTICS WORKSPACE =====");
            sb.AppendLine("# Single workspace for all resource diagnostic settings");
            sb.AppendLine();
            sb.AppendLine("resource \"azurerm_log_analytics_workspace\" \"shared\" {");
            sb.AppendLine("  name                = \"${local.service_name}-shared-logs\"");
            sb.AppendLine("  location            = azurerm_resource_group.main.location");
            sb.AppendLine("  resource_group_name = azurerm_resource_group.main.name");
            sb.AppendLine("  sku                 = \"PerGB2018\"");
            sb.AppendLine("  retention_in_days   = 90  # FedRAMP AU-11 minimum");
            sb.AppendLine();
            sb.AppendLine("  tags = merge(local.common_tags, {");
            sb.AppendLine("    \"security-control\" = \"AU-2,AU-3,AU-6,AU-11\"");
            sb.AppendLine("    \"shared-resource\"  = \"true\"");
            sb.AppendLine("  })");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Module invocations
        sb.AppendLine("# ===== MODULES =====");
        sb.AppendLine();

        // Determine shared LAWS reference for diagnostic settings
        var sharedLawsRef = hasExplicitLaws 
            ? $"module.{resources.First(r => r.ResourceType.ToLowerInvariant().Contains("log") || r.ResourceType.ToLowerInvariant().Contains("analytics")).Id.Replace("-", "_")}.workspace_id"
            : "azurerm_log_analytics_workspace.shared.id";

        var moduleRefs = new List<string>();
        foreach (var resource in resources)
        {
            var moduleName = resource.Id.Replace("-", "_");
            var modulePath = $"./modules/{resource.Id}";
            moduleRefs.Add(moduleName);

            sb.AppendLine($"# {resource.Name} ({resource.ResourceType})");
            sb.AppendLine($"module \"{moduleName}\" {{");
            sb.AppendLine($"  source = \"{modulePath}\"");
            sb.AppendLine();
            sb.AppendLine($"  name                = \"{resource.Name}\"");
            sb.AppendLine("  location            = local.location");
            sb.AppendLine("  resource_group_name = azurerm_resource_group.main.name");
            sb.AppendLine("  tags                = local.common_tags");
            
            // Pass shared LAWS reference for diagnostic settings (not to LAWS itself)
            var isLaws = resource.ResourceType.ToLowerInvariant().Contains("log") || 
                         resource.ResourceType.ToLowerInvariant().Contains("analytics");
            if (!isLaws && needsSharedLaws)
            {
                sb.AppendLine($"  shared_log_analytics_workspace_id = {sharedLawsRef}");
            }

            // Add dependency parameters
            if (!string.IsNullOrEmpty(resource.ParentResourceId))
            {
                var parentModule = resource.ParentResourceId.Replace("-", "_");
                if (resources.Any(r => r.Id == resource.ParentResourceId && r.Platform == ComputePlatform.Networking))
                {
                    sb.AppendLine($"  vnet_id = module.{parentModule}.vnet_id");
                }
            }

            sb.AppendLine();

            // Add depends_on for parent and shared LAWS
            var dependencies = new List<string>();
            if (!string.IsNullOrEmpty(resource.ParentResourceId))
            {
                dependencies.Add($"module.{resource.ParentResourceId.Replace("-", "_")}");
            }
            if (!isLaws && needsSharedLaws && !hasExplicitLaws)
            {
                dependencies.Add("azurerm_log_analytics_workspace.shared");
            }
            
            if (dependencies.Any())
            {
                sb.AppendLine($"  depends_on = [{string.Join(", ", dependencies)}]");
            }

            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Outputs
        sb.AppendLine("# ===== OUTPUTS =====");
        sb.AppendLine();
        
        // Output shared LAWS if created
        if (needsSharedLaws && !hasExplicitLaws)
        {
            sb.AppendLine("output \"shared_log_analytics_workspace_id\" {");
            sb.AppendLine("  value = azurerm_log_analytics_workspace.shared.id");
            sb.AppendLine("}");
            sb.AppendLine();
            sb.AppendLine("output \"shared_log_analytics_workspace_name\" {");
            sb.AppendLine("  value = azurerm_log_analytics_workspace.shared.name");
            sb.AppendLine("}");
            sb.AppendLine();
        }
        
        foreach (var resource in resources)
        {
            var moduleName = resource.Id.Replace("-", "_");
            var outputPrefix = resource.Id.Replace("-", "_");

            sb.AppendLine($"output \"{outputPrefix}_id\" {{");
            sb.AppendLine($"  value = module.{moduleName}.resource_id");
            sb.AppendLine("}");
            sb.AppendLine();
        }

        return (sb.ToString(), moduleRefs);
    }

    /// <summary>
    /// Generate Bicep parameters file
    /// </summary>
    private string GenerateBicepParameters(CompositeInfrastructureRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine("{");
        sb.AppendLine("  \"$schema\": \"https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#\",");
        sb.AppendLine("  \"contentVersion\": \"1.0.0.0\",");
        sb.AppendLine("  \"parameters\": {");
        sb.AppendLine("    \"serviceName\": {");
        sb.AppendLine($"      \"value\": \"{request.ServiceName}\"");
        sb.AppendLine("    },");
        sb.AppendLine("    \"location\": {");
        sb.AppendLine($"      \"value\": \"{request.Region}\"");
        sb.AppendLine("    },");
        sb.AppendLine("    \"environment\": {");
        sb.AppendLine($"      \"value\": \"{request.Environment}\"");
        sb.AppendLine("    }");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate Terraform variables file
    /// </summary>
    private string GenerateTerraformVariables(CompositeInfrastructureRequest request)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"variable \"service_name\" {{");
        sb.AppendLine($"  description = \"Base name for all resources\"");
        sb.AppendLine($"  type        = string");
        sb.AppendLine($"  default     = \"{request.ServiceName}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"variable \"location\" {{");
        sb.AppendLine($"  description = \"Azure region for deployment\"");
        sb.AppendLine($"  type        = string");
        sb.AppendLine($"  default     = \"{request.Region}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"variable \"environment\" {{");
        sb.AppendLine($"  description = \"Environment (dev, staging, prod)\"");
        sb.AppendLine($"  type        = string");
        sb.AppendLine($"  default     = \"{request.Environment}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"tags\" {");
        sb.AppendLine("  description = \"Tags to apply to all resources\"");
        sb.AppendLine("  type        = map(string)");
        sb.AppendLine("  default     = {}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate placeholder module for unsupported resource types
    /// </summary>
    private string GeneratePlaceholderModule(ResourceSpec resource, CompositeInfrastructureRequest request)
    {
        if (request.Format == InfrastructureFormat.Bicep)
        {
            return $@"// Placeholder module for {resource.ResourceType}
// TODO: Implement resource-specific generation

param name string
param location string
param tags object = {{}}

// Add resource definition here

output resourceId string = 'placeholder-id'
output resourceName string = name
";
        }
        else
        {
            return $@"# Placeholder module for {resource.ResourceType}
# TODO: Implement resource-specific generation

variable ""name"" {{
  type = string
}}

variable ""location"" {{
  type = string
}}

variable ""resource_group_name"" {{
  type = string
}}

variable ""tags"" {{
  type    = map(string)
  default = {{}}
}}

# Add resource definition here

output ""resource_id"" {{
  value = ""placeholder-id""
}}

output ""resource_name"" {{
  value = var.name
}}
";
        }
    }

    /// <summary>
    /// Generate README documentation
    /// </summary>
    private string GenerateReadme(CompositeInfrastructureRequest request, List<ResourceSpec> resources)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# {request.ServiceName} Infrastructure");
        sb.AppendLine();
        sb.AppendLine($"**Architecture Pattern:** {request.Pattern}");
        sb.AppendLine($"**Format:** {request.Format}");
        sb.AppendLine($"**Provider:** {request.Provider}");
        sb.AppendLine($"**Region:** {request.Region}");
        sb.AppendLine($"**Environment:** {request.Environment}");
        sb.AppendLine();

        sb.AppendLine("## Resources");
        sb.AppendLine();
        sb.AppendLine("| Resource | Type | Platform | Dependencies |");
        sb.AppendLine("|----------|------|----------|--------------|");
        foreach (var resource in resources)
        {
            var deps = string.IsNullOrEmpty(resource.ParentResourceId) ? "-" : resource.ParentResourceId;
            sb.AppendLine($"| {resource.Name} | {resource.ResourceType} | {resource.Platform} | {deps} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Deployment");
        sb.AppendLine();

        if (request.Format == InfrastructureFormat.Bicep)
        {
            sb.AppendLine("```bash");
            sb.AppendLine("# Deploy using Azure CLI");
            sb.AppendLine($"az deployment group create \\");
            sb.AppendLine($"  --resource-group rg-{request.ServiceName}-{request.Environment} \\");
            sb.AppendLine($"  --template-file main.bicep \\");
            sb.AppendLine($"  --parameters main.parameters.json");
            sb.AppendLine("```");
        }
        else
        {
            sb.AppendLine("```bash");
            sb.AppendLine("# Initialize and apply");
            sb.AppendLine("terraform init");
            sb.AppendLine("terraform plan -out=tfplan");
            sb.AppendLine("terraform apply tfplan");
            sb.AppendLine("```");
        }

        sb.AppendLine();
        sb.AppendLine("## File Structure");
        sb.AppendLine();
        sb.AppendLine("```");
        if (request.Format == InfrastructureFormat.Bicep)
        {
            sb.AppendLine("├── main.bicep              # Main orchestrator");
            sb.AppendLine("├── main.parameters.json    # Parameter values");
        }
        else
        {
            sb.AppendLine("├── main.tf                 # Main orchestrator");
            sb.AppendLine("├── variables.tf            # Variable definitions");
        }
        sb.AppendLine("├── README.md               # This file");
        sb.AppendLine("└── modules/");
        foreach (var resource in resources)
        {
            sb.AppendLine($"    └── {resource.Id}/");
            sb.AppendLine($"        └── main.{(request.Format == InfrastructureFormat.Bicep ? "bicep" : "tf")}");
        }
        sb.AppendLine("```");

        return sb.ToString();
    }

    /// <summary>
    /// Generate summary of generation results
    /// </summary>
    private string GenerateSummary(CompositeInfrastructureRequest request, CompositeGenerationResult result)
    {
        var successful = result.ResourceResults.Count(r => r.Success);
        var failed = result.ResourceResults.Count(r => !r.Success);

        return $"Generated {request.Pattern} pattern with {successful} resources ({failed} failed). " +
               $"Main orchestrator: {result.MainFilePath}, {result.Files.Count} total files.";
    }
}

/// <summary>
/// Interface for composite infrastructure generation
/// </summary>
public interface ICompositeInfrastructureGenerator
{
    Task<CompositeGenerationResult> GenerateAsync(
        CompositeInfrastructureRequest request,
        CancellationToken cancellationToken = default);
}
