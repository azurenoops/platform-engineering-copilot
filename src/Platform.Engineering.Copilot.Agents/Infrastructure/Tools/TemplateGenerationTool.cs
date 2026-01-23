using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Infrastructure.Configuration;
using Platform.Engineering.Copilot.Agents.Infrastructure.State;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Azure;
using CoreModels = Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Agents.Infrastructure.Tools;

/// <summary>
/// Tool for generating Azure infrastructure templates (Bicep/Terraform).
/// Uses IDynamicTemplateGenerator for template generation, AzureMcpClient for best practices,
/// and ITemplateStorageService for database persistence.
/// </summary>
public class TemplateGenerationTool : BaseTool
{
    private readonly InfrastructureStateAccessors _stateAccessors;
    private readonly InfrastructureAgentOptions _options;
    private readonly IDynamicTemplateGenerator _templateGenerator;
    private readonly IComplianceAwareTemplateEnhancer? _complianceEnhancer;
    private readonly ITemplateStorageService _templateStorage;
    private readonly AzureMcpClient _azureMcpClient;

    public override string Name => "generate_infrastructure_template";

    public override string Description =>
        "Generates Azure infrastructure templates (Bicep or Terraform) with optional compliance " +
        "enhancement. Supports VMs, AKS, App Services, SQL, Storage, Key Vault, networking, and more. " +
        "Templates can be enhanced with FedRAMP High, DoD IL5, NIST 800-53, or other compliance frameworks. " +
        "Templates are stored in the database for reuse and tracking.";

    public TemplateGenerationTool(
        ILogger<TemplateGenerationTool> logger,
        InfrastructureStateAccessors stateAccessors,
        IOptions<InfrastructureAgentOptions> options,
        IDynamicTemplateGenerator templateGenerator,
        ITemplateStorageService templateStorage,
        AzureMcpClient azureMcpClient,
        IComplianceAwareTemplateEnhancer? complianceEnhancer = null) : base(logger)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _options = options?.Value ?? new InfrastructureAgentOptions();
        _templateGenerator = templateGenerator ?? throw new ArgumentNullException(nameof(templateGenerator));
        _templateStorage = templateStorage ?? throw new ArgumentNullException(nameof(templateStorage));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
        _complianceEnhancer = complianceEnhancer; // Optional - null is acceptable
        // Define parameters using explicit type from Common namespace
        Parameters.Add(new Common.ToolParameter("resource_type",
            "The type of Azure resource: vm, aks, app_service, sql, storage, keyvault, vnet, redis, cosmos, etc.",
            true));
        Parameters.Add(new Common.ToolParameter("format", "IaC format: 'bicep' (default) or 'terraform'", false));
        Parameters.Add(new Common.ToolParameter("location", "Azure region. Default: eastus", false));
        Parameters.Add(new Common.ToolParameter("name", "Base name for resources", false));
        Parameters.Add(new Common.ToolParameter("subscription_id", "Azure subscription ID", false));
        Parameters.Add(new Common.ToolParameter("environment", "Environment: dev, test, staging, prod. Default: dev", false));
        Parameters.Add(new Common.ToolParameter("enable_compliance", "Enable compliance enhancement. Default: true", false));
        Parameters.Add(new Common.ToolParameter("compliance_framework", "Framework: FedRAMPHigh, DoD_IL5, NIST80053. Default: FedRAMPHigh", false));
        Parameters.Add(new Common.ToolParameter("include_networking", "Include VNet/subnet config. Default: true", false));
        Parameters.Add(new Common.ToolParameter("fetch_best_practices", "Fetch Azure best practices from Azure MCP. Default: true", false));
        Parameters.Add(new Common.ToolParameter("store_template", "Store template in database. Default: true", false));
        Parameters.Add(new Common.ToolParameter("conversation_id", "Conversation ID for state tracking", false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var resourceType = GetOptionalString(arguments, "resource_type")?.ToLowerInvariant()
                ?? throw new ArgumentException("resource_type is required");
            var format = GetOptionalString(arguments, "format") ?? _options.TemplateGeneration.DefaultFormat;
            var location = GetOptionalString(arguments, "location") ?? _options.DefaultRegion;
            var name = GetOptionalString(arguments, "name") ?? GenerateDefaultName(resourceType);
            var environment = GetOptionalString(arguments, "environment") ?? "dev";
            var enableCompliance = GetOptionalBool(arguments, "enable_compliance", _options.EnableComplianceEnhancement);
            var complianceFramework = GetOptionalString(arguments, "compliance_framework") ?? _options.DefaultComplianceFramework;
            var includeNetworking = GetOptionalBool(arguments, "include_networking", true);
            var fetchBestPractices = GetOptionalBool(arguments, "fetch_best_practices", true);
            var storeTemplate = GetOptionalBool(arguments, "store_template", true);
            var conversationId = GetOptionalString(arguments, "conversation_id") ?? Guid.NewGuid().ToString();

            var subscriptionId = GetOptionalString(arguments, "subscription_id")
                ?? await _stateAccessors.GetCurrentSubscriptionAsync(conversationId, cancellationToken)
                ?? _options.DefaultSubscriptionId;

            Logger.LogInformation("Generating {Format} template for {ResourceType} with compliance={Compliance}",
                format, resourceType, enableCompliance);


            // Map resource type to compute platform
            var computePlatform = MapResourceTypeToComputePlatform(resourceType);

            // Check if template already exists in database
            var templateName = $"{name}-{resourceType}-{environment}";
            var existingTemplate = await _templateStorage.GetTemplateByNameAsync(templateName, cancellationToken);
            if (existingTemplate != null)
            {
                Logger.LogInformation("Found existing template in database: {TemplateName}", templateName);
                return ToJson(new
                {
                    success = true,
                    fromDatabase = true,
                    template = new
                    {
                        id = existingTemplate.Id,
                        name = existingTemplate.Name,
                        templateType = existingTemplate.TemplateType,
                        format = existingTemplate.Format,
                        content = existingTemplate.Content,
                        createdAt = existingTemplate.CreatedAt
                    },
                    message = $"Retrieved existing template '{templateName}' from database"
                });
            }

            // Fetch Azure best practices if enabled
            Dictionary<string, object>? bestPractices = null;
            if (fetchBestPractices)
            {
                bestPractices = await FetchAzureBestPracticesAsync(resourceType, cancellationToken);
            }

            // Build template generation request
            var request = BuildTemplateRequest(resourceType, name, location, environment,
                enableCompliance, complianceFramework, includeNetworking, format, bestPractices, computePlatform, subscriptionId);

            // Use compliance enhancer to inject controls and validate (if enabled)
            TemplateGenerationResult result;
            if (_complianceEnhancer != null)
            {
                result = await _complianceEnhancer.EnhanceWithComplianceAsync(
                    request,
                    complianceFramework,
                    cancellationToken);
            }
            else
            {
                // Fallback to basic template generation without compliance enhancement
                // Generate template using IDynamicTemplateGenerator
                result = await _templateGenerator.GenerateTemplateAsync(request, cancellationToken);
            }
            if (!result.Success)
            {
                return ToJson(new
                {
                    success = false,
                    error = result.ErrorMessage ?? "Template generation failed",
                    resourceType
                });
            }

            // Store template in database if enabled
            CoreModels.InfrastructureTemplate? storedTemplate = null;
            if (storeTemplate && result.Files?.Any() == true)
            {
                try
                {
                    var templateData = new
                    {
                        resourceType,
                        format,
                        location,
                        environment,
                        complianceEnhanced = enableCompliance,
                        complianceFramework = enableCompliance ? complianceFramework : null,
                        files = result.Files,
                        bestPracticesApplied = bestPractices != null,
                        generatedAt = DateTime.UtcNow
                    };

                    storedTemplate = await _templateStorage.StoreTemplateAsync(templateName, templateData, cancellationToken);
                    Logger.LogInformation("Stored template in database: {TemplateName} (ID: {Id})",
                        templateName, storedTemplate?.Id);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to store template in database, continuing without persistence");
                }
            }

            // Cache in state accessors for quick retrieval
            var templateCache = new TemplateCache
            {
                TemplateName = templateName,
                ResourceType = resourceType,
                Format = format,
                Files = result.Files ?? new Dictionary<string, string>(),
                GeneratedAt = DateTime.UtcNow,
                Description = $"{resourceType} template for {environment} in {location}"
            };

            _stateAccessors.CacheTemplate(conversationId, resourceType, format, templateCache,
                TimeSpan.FromMinutes(_options.TemplateGeneration.CacheDurationMinutes));

            // Share result summary
            var summary = new TemplateResultSummary
            {
                TemplateName = templateName,
                ResourceType = resourceType,
                Format = format,
                Location = location,
                FileCount = result.Files?.Count ?? 0,
                FileNames = result.Files?.Keys.ToList() ?? new List<string>(),
                GeneratedAt = DateTime.UtcNow,
                ComplianceEnhanced = enableCompliance,
                ComplianceFramework = enableCompliance ? complianceFramework : null
            };
            await _stateAccessors.ShareTemplateResultAsync(conversationId, summary, cancellationToken);

            return ToJson(new
            {
                success = true,
                template = new
                {
                    name = templateName,
                    databaseId = storedTemplate?.Id,
                    resourceType,
                    format,
                    location,
                    environment,
                    complianceEnhanced = enableCompliance,
                    complianceFramework = enableCompliance ? complianceFramework : null,
                    bestPracticesApplied = bestPractices != null,
                    files = result.Files?.Select(f => new { fileName = f.Key, content = f.Value }),
                    generatedAt = templateCache.GeneratedAt,
                    storedInDatabase = storedTemplate != null
                },
                message = $"Generated {format} template for {resourceType} with {result.Files?.Count ?? 0} file(s)" +
                    (storedTemplate != null ? " and stored in database" : "")
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating infrastructure template");
            return ToJson(new { success = false, error = ex.Message });
        }
    }

    /// <summary>
    /// Fetch Azure best practices from Azure MCP Server
    /// </summary>
    private async Task<Dictionary<string, object>?> FetchAzureBestPracticesAsync(
        string resourceType,
        CancellationToken cancellationToken)
    {
        try
        {
            Logger.LogInformation("Fetching Azure best practices for {ResourceType}", resourceType);

            // Map resource type to Azure MCP tool
            var mcpToolName = resourceType switch
            {
                "storage" or "storageaccount" => "azure_storage",
                "keyvault" or "key_vault" => "azure_keyvault",
                "aks" or "kubernetes" => "azure_aks",
                "sql" or "sqldatabase" => "azure_sql",
                "cosmos" or "cosmosdb" => "azure_cosmos",
                "vnet" or "network" => "azure_network",
                "app_service" or "appservice" or "webapp" => "azure_appservice",
                "vm" or "virtualmachine" => "azure_compute",
                _ => null
            };

            if (mcpToolName == null)
            {
                Logger.LogDebug("No Azure MCP tool mapping for resource type: {ResourceType}", resourceType);
                return null;
            }

            // Call Azure MCP to get resource-specific guidance
            var result = await _azureMcpClient.CallToolAsync(mcpToolName, new Dictionary<string, object?>
            {
                ["action"] = "get-best-practices",
                ["resourceType"] = resourceType
            }, cancellationToken);

            if (result.Success && result.Result != null)
            {
                Logger.LogInformation("Retrieved best practices for {ResourceType}", resourceType);
                return new Dictionary<string, object>
                {
                    ["source"] = "Azure MCP Server",
                    ["resourceType"] = resourceType,
                    ["guidance"] = result.Result.ToString() ?? string.Empty
                };
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to fetch Azure best practices for {ResourceType}, continuing without", resourceType);
        }

        return null;
    }

    /// <summary>
    /// Build a TemplateGenerationRequest from tool parameters
    /// </summary>
    private CoreModels.TemplateGenerationRequest BuildTemplateRequest(
        string resourceType,
        string name,
        string location,
        string environment,
        bool enableCompliance,
        string complianceFramework,
        bool includeNetworking,
        string format,
        Dictionary<string, object>? bestPractices,
        ComputePlatform computePlatform,
        string? subscriptionId)
    {
        var request = new CoreModels.TemplateGenerationRequest
        {
            ServiceName = name,
            Description = $"{resourceType} infrastructure for {environment} environment",
            TemplateType = "infrastructure",
            Infrastructure = new CoreModels.InfrastructureSpec
            {
                Format = format.ToLowerInvariant() == "terraform"
                    ? CoreModels.InfrastructureFormat.Terraform
                    : CoreModels.InfrastructureFormat.Bicep,
                Provider = CoreModels.CloudProvider.Azure,
                Region = location,
                IncludeNetworking = includeNetworking,
                Environment = environment,
                ComputePlatform = computePlatform,
                SubscriptionId = subscriptionId
            },
            Security = new CoreModels.SecuritySpec
            {
                // Enable compliance-related security settings
                EnablePrivateCluster = enableCompliance,
                EnableWorkloadIdentity = enableCompliance,
                EnableAzurePolicy = enableCompliance,
                EnableDefender = enableCompliance,
                EnableKeyVault = enableCompliance,
                DisableLocalAccounts = enableCompliance,
                EnablePrivateEndpoint = enableCompliance,
                NetworkPolicies = enableCompliance,
                // Store compliance framework as a standard
                ComplianceStandards = enableCompliance
                    ? new List<string> { complianceFramework }
                    : new List<string>()
            },
            Deployment = new CoreModels.DeploymentSpec
            {
                // Set replicas based on environment
                Replicas = environment switch
                {
                    "prod" or "production" => 3,
                    "staging" => 2,
                    _ => 1
                },
                AutoScaling = environment == "prod" || environment == "production"
            }
        };

        // Add resource-specific configurations
        switch (resourceType)
        {
            case "storage" or "storageaccount":
                // Storage-specific settings already in InfrastructureSpec
                request.Infrastructure.IncludeStorage = true;
                // Storage defaults - minimal configuration for infrastructure-only
                request.Security.EnablePrivateEndpoint = true;
                request.Observability.EnableDiagnostics = true;
                break;

            case "aks" or "kubernetes":
                request.Infrastructure.ComputePlatform = CoreModels.ComputePlatform.AKS;
                if (enableCompliance)
                {
                    request.Infrastructure.ClusterName = $"{resourceType}-cluster";
                    request.Infrastructure.NodeCount = 3;
                    request.Infrastructure.NodeSize = "Standard_D4s_v3";
                    request.Infrastructure.KubernetesVersion = "1.30";
                    request.Infrastructure.NetworkPlugin = "azure";
                    request.Infrastructure.EnableAutoScaling = true;
                    request.Infrastructure.MinNodeCount = 1;
                    request.Infrastructure.MaxNodeCount = 10;

                    // Zero Trust security defaults for AKS
                    request.Security.EnableWorkloadIdentity = true;
                    request.Security.EnableAzurePolicy = true;
                    request.Security.EnableSecretStore = true;
                    request.Security.EnableDefender = true;
                    request.Security.EnablePrivateCluster = true;
                    request.Security.NetworkPolicy = "azure";
                    request.Security.EnableAzureRBAC = true;
                    request.Security.EnableAADIntegration = true;

                    // Monitoring defaults for AKS
                    request.Observability.EnableContainerInsights = true;
                    request.Observability.EnablePrometheus = true;
                }
                break;

            case "app_service" or "appservice" or "webapp":
                request.Infrastructure.ComputePlatform = CoreModels.ComputePlatform.AppService;
                request.Infrastructure.AppServicePlanSku = "P1v3"; // Production-grade
                request.Infrastructure.AlwaysOn = true;
                request.Infrastructure.HttpsOnly = true;
                request.Infrastructure.EnableVnetIntegration = true;

                // Application defaults
                request.Application = new ApplicationSpec
                {
                    Language = ProgrammingLanguage.DotNet, // Default, should be overridden
                    Framework = "aspnetcore"
                };

                // Security defaults for App Service
                request.Security.EnableManagedIdentity = true;
                request.Security.EnablePrivateEndpoint = true;
                request.Security.EnableKeyVault = true;
                request.Security.HttpsOnly = true;

                // Monitoring defaults for App Service
                request.Observability.ApplicationInsights = true;
                request.Observability.EnableDiagnostics = true;
                break;

            case "sql" or "sqldatabase":
                request.Databases.Add(new CoreModels.DatabaseSpec
                {
                    Name = $"{name}-db",
                    Type = CoreModels.DatabaseType.AzureSQL,
                    HighAvailability = environment == "prod",
                    BackupEnabled = true
                });
                break;

            case "keyvault" or "key_vault":
                request.Security.EnableKeyVault = true;
                break;
            case "container-apps" or "containerapps":
                request.Security.EnableKeyVault = true;
                break;
            case "vnets" or "virtualnetworks" or "virtual-network" or "vnet" or "network":
                request.Security.EnableKeyVault = true;
                break;
        }

        return request;
    }

    /// <summary>
    /// Maps resource type string to ComputePlatform enum
    /// </summary>
    private ComputePlatform MapResourceTypeToComputePlatform(string resourceType)
    {
        var normalized = resourceType?.ToLowerInvariant().Replace("-", "").Replace("_", "");
        
        return normalized switch
        {
            // Kubernetes
            "aks" => ComputePlatform.AKS,
            "kubernetes" => ComputePlatform.AKS,
            "k8s" => ComputePlatform.AKS,
            "eks" => ComputePlatform.EKS,
            "gke" => ComputePlatform.GKE,
            
            // App Services
            "appservice" => ComputePlatform.AppService,
            "webapp" => ComputePlatform.AppService,
            "webapps" => ComputePlatform.AppService,
            
            // Containers
            "containerapps" => ComputePlatform.ContainerApps,
            "containerapp" => ComputePlatform.ContainerApps,
            "functions" => ComputePlatform.Functions,
            "lambda" => ComputePlatform.Lambda,
            "ecs" => ComputePlatform.ECS,
            "fargate" => ComputePlatform.Fargate,
            "cloudrun" => ComputePlatform.CloudRun,
            
            // Virtual Machines
            "vm" => ComputePlatform.VirtualMachines,
            "virtualmachine" => ComputePlatform.VirtualMachines,
            "virtualmachines" => ComputePlatform.VirtualMachines,
            
            // Storage
            "storage" => ComputePlatform.Storage,
            "storageaccount" => ComputePlatform.Storage,
            "blob" => ComputePlatform.Storage,
            "blobstorage" => ComputePlatform.Storage,
            
            // Database
            "sql" => ComputePlatform.Database,
            "sqldatabase" => ComputePlatform.Database,
            "database" => ComputePlatform.Database,
            "postgres" => ComputePlatform.Database,
            "postgresql" => ComputePlatform.Database,
            "mysql" => ComputePlatform.Database,
            "cosmosdb" => ComputePlatform.Database,
            "cosmos" => ComputePlatform.Database,
            
            // Networking
            "vnet" => ComputePlatform.Networking,
            "virtualnetwork" => ComputePlatform.Networking,
            "network" => ComputePlatform.Networking,
            "networking" => ComputePlatform.Networking,
            "subnet" => ComputePlatform.Networking,
            "nsg" => ComputePlatform.Networking,
            
            // Security
            "keyvault" => ComputePlatform.Security,
            "vault" => ComputePlatform.Security,
            "managedidentity" => ComputePlatform.Security,
            "identity" => ComputePlatform.Security,
            
            // Default - return Networking for infrastructure-only resources instead of AKS
            _ => ComputePlatform.Networking
        };
    }

    private string GenerateDefaultName(string resourceType)
    {
        var prefix = resourceType.Length > 8 ? resourceType[..8] : resourceType;
        return $"{prefix}-{DateTime.UtcNow:yyyyMMdd}";
    }
}
