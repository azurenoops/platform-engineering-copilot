using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Admin.Models;

namespace Platform.Engineering.Copilot.Admin.Services;

/// <summary>
/// Service for platform engineers to manage service templates
/// </summary>
public interface ITemplateAdminService
{
    Task<TemplateCreationResponse> CreateTemplateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default);
    Task<TemplateCreationResponse> UpdateTemplateAsync(string templateId, UpdateTemplateRequest request, CancellationToken cancellationToken = default);
    Task<List<EnvironmentTemplate>> ListTemplatesAsync(string? searchTerm = null, CancellationToken cancellationToken = default);
    Task<EnvironmentTemplate?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default);
    Task<bool> DeleteTemplateAsync(string templateId, CancellationToken cancellationToken = default);
    Task<bool> UpdateTemplateFileAsync(string templateId, string fileName, string content, CancellationToken cancellationToken = default);
}

public class TemplateAdminService : ITemplateAdminService
{
    private readonly ILogger<TemplateAdminService> _logger;
    private readonly IDynamicTemplateGenerator _templateGenerator;
    private readonly ITemplateStorageService _templateStorage;
    private readonly IEnvironmentManagementEngine _environmentEngine;

    public TemplateAdminService(
        ILogger<TemplateAdminService> logger,
        IDynamicTemplateGenerator templateGenerator,
        ITemplateStorageService templateStorage,
        IEnvironmentManagementEngine environmentEngine)
    {
        _logger = logger;
        _templateGenerator = templateGenerator;
        _templateStorage = templateStorage;
        _environmentEngine = environmentEngine;
    }

    public async Task<TemplateCreationResponse> CreateTemplateAsync(
        CreateTemplateRequest request, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating template: {TemplateName}", request.TemplateName);

        try
        {
            // Build TemplateGenerationRequest from admin request
            var templateRequest = new TemplateGenerationRequest
            {
                ServiceName = request.ServiceName,
                Description = request.Description,
                TemplateType = request.TemplateType, // Pass through template type for infrastructure detection
                Application = request.Application,
                Databases = request.Databases ?? new List<DatabaseSpec>(),
                Infrastructure = request.Infrastructure ?? new InfrastructureSpec(),
                Deployment = request.Deployment ?? new DeploymentSpec(),
                Security = request.Security ?? new SecuritySpec(),
                Observability = request.Observability ?? new ObservabilitySpec()
            };
            
            // Apply compute configuration to deployment spec if provided
            if (request.Compute != null)
            {
                ApplyComputeConfiguration(templateRequest, request.Compute);
            }
            
            // Apply network configuration to infrastructure spec if provided
            if (request.Network != null)
            {
                ApplyNetworkConfiguration(templateRequest, request.Network);
            }

            // Generate template files
            var generationResult = await _templateGenerator.GenerateTemplateAsync(templateRequest, cancellationToken);

            if (!generationResult.Success)
            {
                return new TemplateCreationResponse
                {
                    Success = false,
                    ErrorMessage = generationResult.ErrorMessage,
                    TemplateName = request.TemplateName
                };
            }

            // Create template entity for storage
            var template = new EnvironmentTemplate
            {
                Id = Guid.NewGuid(),
                Name = request.TemplateName,
                Description = request.Description,
                TemplateType = request.TemplateType ?? request.Application?.Language.ToString() ?? "Infrastructure",
                Version = request.Version ?? "1.0.0",
                Content = generationResult.Files.ContainsKey("infra/main.bicep") 
                    ? generationResult.Files["infra/main.bicep"]
                    : generationResult.Files.ContainsKey("infra/main.tf")
                        ? generationResult.Files["infra/main.tf"]
                        : generationResult.Files.First().Value,
                Format = request.Infrastructure?.Format.ToString() ?? "Kubernetes",
                DeploymentTier = request.Deployment?.Replicas >= 5 ? "Premium" : "Standard",
                AzureService = request.Infrastructure?.ComputePlatform.ToString(),
                AutoScalingEnabled = request.Deployment?.AutoScaling ?? false,
                MonitoringEnabled = request.Observability?.Prometheus ?? true,
                BackupEnabled = false,
                FilesCount = generationResult.Files.Count,
                MainFileType = DetermineMainFileType(generationResult.Files),
                Summary = generationResult.Summary,
                CreatedBy = request.CreatedBy ?? "PlatformAdmin",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
                IsPublic = request.IsPublic
            };

            // Store template with files
            var templateWithFiles = new { 
                template, 
                files = generationResult.Files 
            };
            var savedTemplate = await _templateStorage.StoreTemplateAsync(template.Name, templateWithFiles, cancellationToken);

            _logger.LogInformation("Template {TemplateName} created successfully with ID: {TemplateId}", 
                request.TemplateName, savedTemplate.Id);

            return new TemplateCreationResponse
            {
                Success = true,
                TemplateId = savedTemplate.Id.ToString(),
                TemplateName = savedTemplate.Name,
                GeneratedFiles = generationResult.Files.Keys.ToList(),
                Summary = generationResult.Summary,
                ComponentsGenerated = generationResult.GeneratedComponents
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template: {TemplateName}", request.TemplateName);
            return new TemplateCreationResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                TemplateName = request.TemplateName
            };
        }
    }

    public async Task<TemplateCreationResponse> UpdateTemplateAsync(
        string templateId, 
        UpdateTemplateRequest request, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating template: {TemplateId}", templateId);

        try
        {
            // Get existing template
            var existingTemplate = await _templateStorage.GetTemplateByIdAsync(templateId, cancellationToken);
            if (existingTemplate == null)
            {
                return new TemplateCreationResponse
                {
                    Success = false,
                    ErrorMessage = $"Template {templateId} not found",
                    TemplateId = templateId
                };
            }

            // If template generation request provided, regenerate files
            if (request.TemplateGenerationRequest != null)
            {
                var generationResult = await _templateGenerator.GenerateTemplateAsync(
                    request.TemplateGenerationRequest, 
                    cancellationToken);

                if (!generationResult.Success)
                {
                    return new TemplateCreationResponse
                    {
                        Success = false,
                        ErrorMessage = generationResult.ErrorMessage,
                        TemplateId = templateId
                    };
                }

                // Update template with new content
                existingTemplate.Content = generationResult.Files.ContainsKey("infra/main.bicep")
                    ? generationResult.Files["infra/main.bicep"]
                    : generationResult.Files.ContainsKey("infra/main.tf")
                        ? generationResult.Files["infra/main.tf"]
                        : generationResult.Files.First().Value;
                existingTemplate.FilesCount = generationResult.Files.Count;
                existingTemplate.Summary = generationResult.Summary;
            }

            // Update metadata
            if (!string.IsNullOrEmpty(request.Description))
                existingTemplate.Description = request.Description;
            
            if (!string.IsNullOrEmpty(request.Version))
                existingTemplate.Version = request.Version;

            if (!string.IsNullOrEmpty(request.TemplateType))
                existingTemplate.TemplateType = request.TemplateType;

            // Extract format from either flat Format field or Infrastructure.Format
            if (!string.IsNullOrEmpty(request.Format))
                existingTemplate.Format = request.Format;
            else if (request.Infrastructure?.Format != null)
                existingTemplate.Format = request.Infrastructure.Format.ToString();

            if (request.IsActive.HasValue)
                existingTemplate.IsActive = request.IsActive.Value;

            existingTemplate.UpdatedAt = DateTime.UtcNow;

            // Save updated template
            await _templateStorage.StoreTemplateAsync(existingTemplate.Name, existingTemplate, cancellationToken);

            _logger.LogInformation("Template {TemplateId} updated successfully", templateId);

            return new TemplateCreationResponse
            {
                Success = true,
                TemplateId = templateId,
                TemplateName = existingTemplate.Name,
                Summary = "Template updated successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template: {TemplateId}", templateId);
            return new TemplateCreationResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                TemplateId = templateId
            };
        }
    }

    public async Task<List<EnvironmentTemplate>> ListTemplatesAsync(
        string? searchTerm = null, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing templates. Search term: {SearchTerm}", searchTerm ?? "none");

        var templates = await _templateStorage.ListAllTemplatesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            templates = templates.Where(t => 
                t.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                t.TemplateType.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
            ).ToList();
        }

        return templates;
    }

    public async Task<EnvironmentTemplate?> GetTemplateAsync(
        string templateId, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Getting template: {TemplateId}", templateId);
        return await _templateStorage.GetTemplateByIdAsync(templateId, cancellationToken);
    }

    public async Task<bool> DeleteTemplateAsync(
        string templateId, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting template: {TemplateId}", templateId);

        try
        {
            var template = await _templateStorage.GetTemplateByIdAsync(templateId, cancellationToken);
            if (template == null)
            {
                _logger.LogWarning("Template {TemplateId} not found", templateId);
                return false;
            }

            return await _templateStorage.DeleteTemplateAsync(template.Name, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template: {TemplateId}", templateId);
            return false;
        }
    }

    /// <summary>
    /// Applies compute configuration from admin request to template generation request
    /// Maps platform-specific compute settings to deployment and infrastructure specs
    /// </summary>
    private void ApplyComputeConfiguration(TemplateGenerationRequest templateRequest, ComputeConfiguration compute)
    {
        if (compute == null) return;

        // Apply resource limits to deployment spec
        if (!string.IsNullOrEmpty(compute.CpuLimit))
        {
            templateRequest.Deployment.Resources.CpuLimit = compute.CpuLimit;
        }
        
        if (!string.IsNullOrEmpty(compute.MemoryLimit))
        {
            templateRequest.Deployment.Resources.MemoryLimit = compute.MemoryLimit;
        }

        // Apply scaling configuration
        if (compute.EnableAutoScaling.HasValue)
        {
            templateRequest.Deployment.AutoScaling = compute.EnableAutoScaling.Value;
            
            if (compute.MinInstances.HasValue)
            {
                templateRequest.Deployment.MinReplicas = compute.MinInstances.Value;
            }
            
            if (compute.MaxInstances.HasValue)
            {
                templateRequest.Deployment.MaxReplicas = compute.MaxInstances.Value;
            }
        }

        // Store platform-specific metadata in infrastructure spec
        // This can be used by template generators to create platform-specific configurations
        if (compute.PlatformSpecificConfig != null && compute.PlatformSpecificConfig.Any())
        {
            // Store in a way that template generators can access
            _logger.LogDebug("Applied platform-specific compute config with {Count} properties", 
                compute.PlatformSpecificConfig.Count);
        }
        
        // Log the compute configuration being applied
        _logger.LogInformation(
            "Applied compute configuration: InstanceType={InstanceType}, AutoScaling={AutoScaling}, " +
            "MinInstances={MinInstances}, MaxInstances={MaxInstances}, CPU={CPU}, Memory={Memory}",
            compute.InstanceType ?? "default",
            compute.EnableAutoScaling ?? false,
            compute.MinInstances ?? 0,
            compute.MaxInstances ?? 0,
            compute.CpuLimit ?? "default",
            compute.MemoryLimit ?? "default");
    }

    /// <summary>
    /// Applies network configuration from admin request to template generation request
    /// Maps network settings to infrastructure spec for VNet, subnets, NSG, etc.
    /// </summary>
    private void ApplyNetworkConfiguration(TemplateGenerationRequest templateRequest, NetworkConfiguration network)
    {
        if (network == null) return;

        // Enable networking in infrastructure spec
        templateRequest.Infrastructure.IncludeNetworking = true;

        // Map admin network model to template generation network model
        templateRequest.Infrastructure.NetworkConfig = new NetworkingConfiguration
        {
            VNetName = network.VNetName ?? "vnet-default",
            VNetAddressSpace = network.VNetAddressSpace ?? "10.0.0.0/16",
            Subnets = network.Subnets?.Select(s => new SubnetConfiguration
            {
                Name = s.Name ?? string.Empty,
                AddressPrefix = s.AddressPrefix ?? string.Empty,
                EnableServiceEndpoints = s.EnableServiceEndpoints ?? false,
                ServiceEndpoints = s.ServiceEndpointTypes ?? new List<string>(),
                Purpose = SubnetPurpose.Application // Default purpose
            }).ToList() ?? new List<SubnetConfiguration>(),
            ServiceEndpoints = network.ServiceEndpoints ?? new List<string>(),
            EnableServiceEndpoints = true, // Enable if any subnet has service endpoints
            EnableNetworkSecurityGroup = network.EnableNetworkSecurityGroup ?? false,
            NsgMode = network.NsgMode ?? "new",
            NsgName = network.NsgName,
            ExistingNsgResourceId = network.ExistingNsgResourceId,
            NsgRules = network.NsgRules?.Select(r => new NetworkSecurityRule
            {
                Name = r.Name ?? string.Empty,
                Priority = r.Priority,
                Direction = r.Direction ?? "Inbound",
                Access = r.Access ?? "Allow",
                Protocol = r.Protocol ?? "Tcp",
                SourcePortRange = r.SourcePortRange ?? "*",
                DestinationPortRange = r.DestinationPortRange ?? "*",
                SourceAddressPrefix = r.SourceAddressPrefix ?? "*",
                DestinationAddressPrefix = r.DestinationAddressPrefix ?? "*"
            }).ToList() ?? new List<NetworkSecurityRule>(),
            EnableDDoSProtection = network.EnableDDoSProtection ?? false,
            DdosMode = network.DdosMode ?? "new",
            DDoSProtectionPlanId = network.DdosProtectionPlanId,
            EnablePrivateDns = network.EnablePrivateDns ?? false,
            PrivateDnsMode = network.PrivateDnsMode ?? "new",
            PrivateDnsZoneName = network.PrivateDnsZoneName,
            ExistingPrivateDnsZoneResourceId = network.ExistingPrivateDnsZoneResourceId,
            EnableVNetPeering = network.EnableVNetPeering ?? false,
            VNetPeerings = network.VNetPeerings?.Select(p => new VNetPeeringConfiguration
            {
                Name = p.Name ?? string.Empty,
                RemoteVNetResourceId = p.RemoteVNetResourceId ?? string.Empty,
                RemoteVNetName = p.RemoteVNetName,
                AllowVirtualNetworkAccess = p.AllowVirtualNetworkAccess ?? true,
                AllowForwardedTraffic = p.AllowForwardedTraffic ?? false,
                AllowGatewayTransit = p.AllowGatewayTransit ?? false,
                UseRemoteGateways = p.UseRemoteGateways ?? false
            }).ToList() ?? new List<VNetPeeringConfiguration>()
        };

        // Log network configuration being applied
        _logger.LogInformation(
            "Applied network configuration: VNet={VNet}, AddressSpace={AddressSpace}, " +
            "Subnets={SubnetCount}, NSG={NSG} (Mode={NsgMode}), DDoS={DDoS} (Mode={DdosMode}), Peerings={PeeringCount}",
            network.VNetName ?? "default",
            network.VNetAddressSpace ?? "default",
            network.Subnets?.Count ?? 0,
            network.EnableNetworkSecurityGroup ?? false,
            network.NsgMode ?? "new",
            network.EnableDDoSProtection ?? false,
            network.DdosMode ?? "new",
            network.VNetPeerings?.Count ?? 0);

        // Network configuration will be used by infrastructure template generators
        // to create VNets, subnets, NSGs, etc. The actual generation logic is in
        // the template generators (Bicep, Terraform, etc.)
    }

    public async Task<bool> UpdateTemplateFileAsync(
        string templateId, 
        string fileName, 
        string content, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating file {FileName} in template {TemplateId}", fileName, templateId);

        var template = await GetTemplateAsync(templateId, cancellationToken);
        
        if (template == null)
        {
            _logger.LogWarning("Template {TemplateId} not found", templateId);
            return false;
        }

        // Find the file to update
        var fileToUpdate = template.Files?.FirstOrDefault(f => f.FileName == fileName);
        
        if (fileToUpdate == null)
        {
            _logger.LogWarning("File {FileName} not found in template {TemplateId}", fileName, templateId);
            return false;
        }

        // Update the file content
        fileToUpdate.Content = content;
        template.UpdatedAt = DateTime.UtcNow;

        // Save the template back to storage
        await _templateStorage.UpdateTemplateAsync(template.Name, template, cancellationToken);

        _logger.LogInformation("Successfully updated file {FileName} in template {TemplateId}", fileName, templateId);
        return true;
    }

    private string DetermineMainFileType(Dictionary<string, string> files)
    {
        if (files.ContainsKey("infra/main.bicep")) return "bicep";
        if (files.ContainsKey("infra/main.tf")) return "terraform";
        if (files.Any(f => f.Key.StartsWith("k8s/"))) return "yaml";
        if (files.Any(f => f.Key.EndsWith(".cs"))) return "csharp";
        if (files.Any(f => f.Key.EndsWith(".js"))) return "javascript";
        if (files.Any(f => f.Key.EndsWith(".py"))) return "python";
        if (files.Any(f => f.Key.EndsWith(".java"))) return "java";
        return "mixed";
    }
}
