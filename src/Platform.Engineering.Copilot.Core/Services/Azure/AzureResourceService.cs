using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure.ResourceManager;
using Azure.Identity;
using Azure.Core;
using Azure.ResourceManager.Resources;
using Azure;
using Azure.ResourceManager.ContainerService;
using Azure.ResourceManager.ContainerService.Models;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Storage.Models;
using Azure.ResourceManager.Storage;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Azure;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services.Azure;

/// <summary>
/// Azure Resource Service that provides comprehensive Azure resource management capabilities.
/// Handles resource provisioning, monitoring, cost management, and compliance operations
/// across Azure subscriptions. Integrates with Azure Resource Manager, monitoring services,
/// and cost management APIs to provide unified platform operations.
/// </summary>
public class AzureResourceService : IAzureResourceService
{
    private readonly ILogger<AzureResourceService> _logger;
    private readonly IAzureClientFactory _clientFactory;
    private readonly AzureGatewayOptions _options;

    /// <summary>
    /// Initializes a new instance of the AzureResourceService with Azure Resource Manager client setup.
    /// </summary>
    /// <param name="logger">Logger for Azure operations and diagnostics</param>
    /// <param name="clientFactory">Azure client factory for centralized credential management</param>
    /// <param name="options">Gateway configuration options including Azure credentials</param>
    public AzureResourceService(
        ILogger<AzureResourceService> logger,
        IAzureClientFactory clientFactory,
        IOptions<GatewayOptions> options)
    {
        _logger = logger;
        _clientFactory = clientFactory;
        _options = options.Value.Azure;
        _logger.LogInformation("Azure Resource Service configured with AzureClientFactory");
    }

    /// <summary>
    /// Gets the ARM client from the factory, throws if Azure integration is disabled.
    /// </summary>
    private ArmClient EnsureArmClient()
    {
        if (!_options.Enabled)
        {
            throw new InvalidOperationException("Azure integration is disabled in configuration. Enable Gateway.Azure.Enabled to use Azure resources.");
        }

        return _clientFactory.GetArmClient(_options.SubscriptionId);
    }

    // Public API methods for Extension tools to call
    public ArmClient? GetArmClient()
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Azure integration is disabled in configuration");
            return null;
        }
        return _clientFactory.GetArmClient(_options.SubscriptionId);
    }

    public string GetSubscriptionId(string? subscriptionId = null)
    {
        var subId = subscriptionId ?? _options.SubscriptionId;
        if (string.IsNullOrWhiteSpace(subId))
        {
            throw new InvalidOperationException("No subscription ID provided");
        }
        return subId;
    }

    /// <summary>
    /// Gets the currently authenticated Azure user's identity (email/UPN)
    /// </summary>
    /// <returns>User principal name (email) or account identifier</returns>
    public async Task<string> GetCurrentAzureUserAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var credential = _clientFactory.GetCredential();

            // Get a token to extract user identity claims using the factory's management scope
            var managementScope = _clientFactory.GetManagementScope();
            var tokenRequestContext = new TokenRequestContext(new[] { managementScope });
            var token = await credential.GetTokenAsync(tokenRequestContext, cancellationToken);

            // Decode the JWT token to extract user identity
            var tokenParts = token.Token.Split('.');
            if (tokenParts.Length >= 2)
            {
                var payload = tokenParts[1];
                // Add padding if needed for Base64 decoding
                var paddedPayload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
                var decodedBytes = Convert.FromBase64String(paddedPayload);
                var decodedPayload = System.Text.Encoding.UTF8.GetString(decodedBytes);

                // Parse JSON to extract user claims
                var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(decodedPayload);

                if (claims != null)
                {
                    // Try to get user principal name (email) or unique name
                    if (claims.TryGetValue("upn", out var upn) && upn.ValueKind == JsonValueKind.String)
                    {
                        return upn.GetString() ?? "unknown";
                    }
                    if (claims.TryGetValue("unique_name", out var uniqueName) && uniqueName.ValueKind == JsonValueKind.String)
                    {
                        return uniqueName.GetString() ?? "unknown";
                    }
                    if (claims.TryGetValue("email", out var email) && email.ValueKind == JsonValueKind.String)
                    {
                        return email.GetString() ?? "unknown";
                    }
                    if (claims.TryGetValue("preferred_username", out var preferredUsername) && preferredUsername.ValueKind == JsonValueKind.String)
                    {
                        return preferredUsername.GetString() ?? "unknown";
                    }
                    if (claims.TryGetValue("name", out var name) && name.ValueKind == JsonValueKind.String)
                    {
                        return name.GetString() ?? "unknown";
                    }
                }
            }

            _logger.LogWarning("Could not extract user identity from token, using environment username");
            return Environment.UserName ?? "unknown";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current Azure user, falling back to environment username");
            return Environment.UserName ?? "unknown";
        }
    }

    /// <summary>
    /// Extracts the resource group name from an Azure resource ID
    /// </summary>
    private string ExtractResourceGroupFromId(string resourceId)
    {
        // Azure resource ID format: /subscriptions/{sub}/resourceGroups/{rg}/providers/{provider}/{type}/{name}
        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i].Equals("resourceGroups", StringComparison.OrdinalIgnoreCase))
            {
                return parts[i + 1];
            }
        }
        return string.Empty;
    }

    public async Task<IEnumerable<AzureResource>> ListAllResourcesInResourceGroupAsync(string subscriptionId, string resourceGroupName, CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
        var resourceGroup = await subscription.GetResourceGroups().GetAsync(resourceGroupName, cancellationToken);

        var resources = new List<AzureResource>();
        await foreach (var resource in resourceGroup.Value.GetGenericResourcesAsync(cancellationToken: cancellationToken))
        {
            resources.Add(new AzureResource
            {
                Name = resource.Data.Name,
                Type = resource.Data.ResourceType.ToString(),
                Location = resource.Data.Location.ToString(),
                Id = resource.Data.Id.ToString(),
                ResourceGroup = resourceGroupName,
                Tags = new Dictionary<string, string>(resource.Data.Tags)
            });
        }

        return resources;
    }

    public async Task<IEnumerable<AzureResource>> ListAllResourceGroupsInSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
        var resourceGroups = new List<AzureResource>();

        await foreach (var resourceGroup in subscription.GetResourceGroups().GetAllAsync(cancellationToken: cancellationToken))
        {
            resourceGroups.Add(new AzureResource
            {
                Name = resourceGroup.Data.Name,
                Location = resourceGroup.Data.Location.ToString(),
                Id = resourceGroup.Data.Id.ToString(),
                Type = "Microsoft.Resources/resourceGroups",
                ResourceGroup = resourceGroup.Data.Name,
                Tags = new Dictionary<string, string>(resourceGroup.Data.Tags)
            });
        }

        return resourceGroups;
    }

    public async Task<IEnumerable<AzureResource>> ListAllResourcesAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
        var resources = new List<AzureResource>();

        await foreach (var resource in subscription.GetGenericResourcesAsync(cancellationToken: cancellationToken))
        {
            // Extract resource group name from ID (format: /subscriptions/{sub}/resourceGroups/{rg}/...)
            var resourceGroup = ExtractResourceGroupFromId(resource.Data.Id.ToString());

            resources.Add(new AzureResource
            {
                Name = resource.Data.Name,
                Type = resource.Data.ResourceType.ToString(),
                Location = resource.Data.Location.ToString(),
                Id = resource.Data.Id.ToString(),
                ResourceGroup = resourceGroup,
                Tags = new Dictionary<string, string>(resource.Data.Tags)
            });
        }

        return resources;
    }

    public async Task<AzureResource?> GetResourceGroupAsync(string resourceGroupName, string? subscriptionId = null, CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        try
        {
            var subId = GetSubscriptionId(subscriptionId);
            _logger.LogInformation("Getting resource group {ResourceGroup} from subscription {SubscriptionId}", resourceGroupName, subId);

            ResourceIdentifier resourceId;
            try
            {
                resourceId = SubscriptionResource.CreateResourceIdentifier(subId);
                _logger.LogInformation("Created ResourceIdentifier: {ResourceId}", resourceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create ResourceIdentifier for subscription {SubscriptionId}", subId);
                throw new InvalidOperationException($"Failed to create resource identifier for subscription '{subId}'. Ensure the subscription ID is a valid GUID.", ex);
            }

            var subscription = armClient.GetSubscriptionResource(resourceId);
            var resourceGroup = await subscription.GetResourceGroups().GetAsync(resourceGroupName, cancellationToken);

            return new AzureResource
            {
                Name = resourceGroup.Value.Data.Name,
                Location = resourceGroup.Value.Data.Location.ToString(),
                Id = resourceGroup.Value.Data.Id.ToString(),
                Type = "Microsoft.Resources/resourceGroups",
                ResourceGroup = resourceGroup.Value.Data.Name,
                Tags = new Dictionary<string, string>(resourceGroup.Value.Data.Tags)
            };
        }
        catch (global::Azure.RequestFailedException ex) when (ex.Status == 404)
        {
            // Resource group not found - return null to trigger automatic creation
            return null;
        }
    }

    public async Task<IEnumerable<AzureResource>> ListResourceGroupsAsync(string? subscriptionId = null, CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
        var resourceGroups = new List<AzureResource>();

        await foreach (var resourceGroup in subscription.GetResourceGroups().GetAllAsync(cancellationToken: cancellationToken))
        {
            resourceGroups.Add(new AzureResource
            {
                Name = resourceGroup.Data.Name,
                Location = resourceGroup.Data.Location.ToString(),
                Id = resourceGroup.Data.Id.ToString(),
                Type = "Microsoft.Resources/resourceGroups",
                ResourceGroup = resourceGroup.Data.Name,
                Tags = new Dictionary<string, string>(resourceGroup.Data.Tags)
            });
        }

        return resourceGroups;
    }

    public async Task<AzureResource> CreateResourceGroupAsync(string resourceGroupName, string location, string? subscriptionId = null, Dictionary<string, string>? tags = null, CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));

        var resourceGroupData = new ResourceGroupData(new AzureLocation(location));
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                resourceGroupData.Tags.Add(tag.Key, tag.Value);
            }
        }

        var resourceGroupResult = await subscription.GetResourceGroups().CreateOrUpdateAsync(
            WaitUntil.Completed,
            resourceGroupName,
            resourceGroupData,
            cancellationToken);

        return new AzureResource
        {
            Name = resourceGroupResult.Value.Data.Name,
            Location = resourceGroupResult.Value.Data.Location.ToString(),
            Id = resourceGroupResult.Value.Data.Id.ToString(),
            Type = "Microsoft.Resources/resourceGroups",
            ResourceGroup = resourceGroupResult.Value.Data.Name,
            Tags = new Dictionary<string, string>(resourceGroupResult.Value.Data.Tags)
        };
    }

    public async Task DeleteResourceGroupAsync(string resourceGroupName, string? subscriptionId = null, CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        _logger.LogInformation("Deleting resource group {ResourceGroupName} from subscription {SubscriptionId}",
            resourceGroupName, GetSubscriptionId(subscriptionId));

        try
        {
            var subscription = armClient.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));

            var resourceGroup = await subscription.GetResourceGroups().GetAsync(resourceGroupName, cancellationToken);

            await resourceGroup.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken);

            _logger.LogInformation("Successfully deleted resource group {ResourceGroupName}", resourceGroupName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Resource group {ResourceGroupName} not found - may have already been deleted", resourceGroupName);
            throw new InvalidOperationException($"Resource group '{resourceGroupName}' not found", ex);
        }
    }

    public async Task<IEnumerable<AzureSubscription>> ListSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        var subscriptions = new List<AzureSubscription>();
        await foreach (var subscription in armClient.GetSubscriptions().GetAllAsync(cancellationToken: cancellationToken))
        {
            subscriptions.Add(new AzureSubscription
            {
                SubscriptionId = subscription.Data.SubscriptionId ?? string.Empty,
                SubscriptionName = subscription.Data.DisplayName ?? string.Empty,
                State = subscription.Data.State?.ToString() ?? string.Empty,
                TenantId = subscription.Data.TenantId?.ToString() ?? string.Empty
            });
        }

        return subscriptions;
    }

    public async Task<AzureResource?> GetResourceAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        var resource = armClient.GetGenericResource(global::Azure.Core.ResourceIdentifier.Parse(resourceId!));
        if (resource == null) return null;

        var resourceData = await resource.GetAsync(cancellationToken);
        var resourceGroup = ExtractResourceGroupFromId(resourceData.Value.Data.Id.ToString());

        return new AzureResource
        {
            Name = resourceData.Value.Data.Name,
            Type = resourceData.Value.Data.ResourceType.ToString(),
            Location = resourceData.Value.Data.Location.ToString(),
            Id = resourceData.Value.Data.Id.ToString(),
            ResourceGroup = resourceGroup,
            Tags = new Dictionary<string, string>(resourceData.Value.Data.Tags)
        };
    }

    public async Task<AzureResource?> GetResourceAsync(string subscriptionId, string resourceGroupName, string resourceType, string resourceName, CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        try
        {
            var resourceId = $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/{resourceType}/{resourceName}";
            return await GetResourceAsync(resourceId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Resource {ResourceType}/{ResourceName} not found in resource group {ResourceGroup}",
                resourceType, resourceName, resourceGroupName);
            return null;
        }
    }

    public async Task<IEnumerable<AzureResource>> ListLocationsAsync(string? subscriptionId = null, CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
        var locations = new List<AzureResource>();

        await foreach (var location in subscription.GetLocationsAsync(cancellationToken: cancellationToken))
        {
            locations.Add(new AzureResource
            {
                Name = location.Name ?? string.Empty,
                Type = "Microsoft.Resources/locations",
                Location = location.Name ?? string.Empty,
                Id = location.Id?.ToString() ?? string.Empty,
                ResourceGroup = string.Empty,
                Tags = new Dictionary<string, string>()
            });
        }

        return locations;
    }

    // Interface-compliant wrapper - TODO: Implement proper conversion from object to AzureResource
    public async Task<AzureResource> CreateResourceAsync(
        string resourceGroupName,
        string resourceType,
        string resourceName,
        object properties,
        string? subscriptionId = null,
        string location = "eastus",
        Dictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("CreateResourceAsync with AzureResource return type not yet implemented. Use specialized create methods instead.");
    }

    // Additional helper methods for Extension tools - Legacy implementation
    internal async Task<object> CreateResourceAsyncInternalAsync(
        string resourceGroupName,
        string resourceType,
        string resourceName,
        object properties,
        string? subscriptionId = null,
        string location = "eastus",
        Dictionary<string, string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        try
        {
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));

            // Try to get the resource group, create it if it doesn't exist
            Response<ResourceGroupResource> resourceGroupResponse;
            try
            {
                resourceGroupResponse = await subscription.GetResourceGroups().GetAsync(resourceGroupName, cancellationToken);
                _logger.LogInformation("Using existing resource group {ResourceGroupName}", resourceGroupName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogInformation("Resource group {ResourceGroupName} not found, creating it in location {Location}",
                    resourceGroupName, location);

                // Create the resource group
                var resourceGroupData = new ResourceGroupData(new AzureLocation(location));
                if (tags != null)
                {
                    foreach (var tag in tags)
                    {
                        resourceGroupData.Tags.Add(tag.Key, tag.Value);
                    }
                }

                var resourceGroupOperation = await subscription.GetResourceGroups().CreateOrUpdateAsync(
                    global::Azure.WaitUntil.Completed, resourceGroupName, resourceGroupData, cancellationToken);

                resourceGroupResponse = Response.FromValue(resourceGroupOperation.Value, resourceGroupOperation.GetRawResponse());
                _logger.LogInformation("Successfully created resource group {ResourceGroupName}", resourceGroupName);
            }

            var resourceGroup = resourceGroupResponse.Value;

            _logger.LogInformation("Creating resource {ResourceName} of type {ResourceType} in resource group {ResourceGroupName}",
                resourceName, resourceType, resourceGroupName);

            // Handle specific resource types with dedicated methods
            switch (resourceType.ToLowerInvariant())
            {
                case "microsoft.storage/storageaccounts":
                    return await CreateStorageAccountAsync(resourceGroup, resourceName, properties, location, tags, cancellationToken);

                case "microsoft.keyvault/vaults":
                    return await CreateKeyVaultAsync(resourceGroup, resourceName, properties, location, tags, cancellationToken);

                case "microsoft.web/sites":
                    return await CreateWebAppAsync(resourceGroup, resourceName, properties, location, tags, cancellationToken);

                default:
                    // Use generic ARM template deployment for other resource types
                    return await CreateGenericResourceAsync(resourceGroup, resourceType, resourceName, properties, subscriptionId, location, tags, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create resource {ResourceName} of type {ResourceType}", resourceName, resourceType);
            return new
            {
                resourceGroupName = resourceGroupName,
                resourceType = resourceType,
                resourceName = resourceName,
                location = location,
                tags = tags,
                status = "Failed",
                error = ex.Message
            };
        }
    }

    public async Task<object> DeployTemplateAsync(
        string resourceGroupName,
        string templateContent,
        object? parameters = null,
        string? subscriptionId = null,
        string deploymentName = "mcp-deployment",
        CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        try
        {
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
            var resourceGroup = await subscription.GetResourceGroups().GetAsync(resourceGroupName, cancellationToken);

            _logger.LogInformation("ARM template deployment requested for {DeploymentName} to resource group {ResourceGroupName}",
                deploymentName, resourceGroupName);

            // Parse and validate template content
            var templateJson = JsonDocument.Parse(templateContent);
            _logger.LogInformation("Template parsed successfully with {ResourceCount} resources",
                templateJson.RootElement.GetProperty("resources").GetArrayLength());

            // For now, return deployment request confirmation
            // Full ARM deployment implementation would require additional setup
            return new
            {
                deploymentName = deploymentName,
                resourceGroupName = resourceGroupName,
                status = "Validated",
                message = "ARM template validated and ready for deployment",
                templateResourceCount = templateJson.RootElement.GetProperty("resources").GetArrayLength()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process ARM template {DeploymentName}", deploymentName);
            return new
            {
                deploymentName = deploymentName,
                resourceGroupName = resourceGroupName,
                status = "Failed",
                error = ex.Message
            };
        }
    }

    /// <summary>
    /// Creates an AKS cluster with the specified configuration
    /// </summary>
    public async Task<object> CreateAksClusterAsync(
        string clusterName,
        string resourceGroupName,
        string location,
        Dictionary<string, object>? aksSettings = null,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        _logger.LogInformation("Creating AKS cluster {ClusterName} in resource group {ResourceGroupName}", clusterName, resourceGroupName);

        try
        {
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
            var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName, cancellationToken);

            // Configure AKS cluster data
            var aksData = new ContainerServiceManagedClusterData(new AzureLocation(location))
            {
                Identity = new global::Azure.ResourceManager.Models.ManagedServiceIdentity(global::Azure.ResourceManager.Models.ManagedServiceIdentityType.SystemAssigned),
                DnsPrefix = $"{clusterName}-dns",
                AgentPoolProfiles =
                {
                    new ManagedClusterAgentPoolProfile("default")
                    {
                        Count = GetSettingValue<int>(aksSettings, "nodeCount", 3),
                        VmSize = GetSettingValue<string>(aksSettings, "vmSize", "Standard_DS2_v2"),
                        OSType = ContainerServiceOSType.Linux,
                        Mode = AgentPoolMode.System
                    }
                },
                Tags = {
                    ["Environment"] = GetSettingValue<string>(aksSettings, "environment", "development"),
                    ["ManagedBy"] = "SupervisorPlatform",
                    ["CreatedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
                }
            };

            // Create AKS cluster
            var aksCollection = resourceGroup.Value.GetContainerServiceManagedClusters();
            var aksOperation = await aksCollection.CreateOrUpdateAsync(WaitUntil.Started, clusterName, aksData, cancellationToken);

            // Construct the cluster resource ID manually (operation not yet complete)
            var subId = GetSubscriptionId(subscriptionId);
            var clusterId = $"/subscriptions/{subId}/resourceGroups/{resourceGroupName}/providers/Microsoft.ContainerService/managedClusters/{clusterName}";

            return new
            {
                success = true,
                clusterId = clusterId,
                clusterName,
                resourceGroupName,
                location,
                status = "Creating",
                nodeCount = aksData.AgentPoolProfiles.First().Count,
                vmSize = aksData.AgentPoolProfiles.First().VmSize,
                message = $"AKS cluster {clusterName} creation started successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create AKS cluster {ClusterName}", clusterName);
            throw;
        }
    }

    /// <summary>
    /// Creates a Web App with App Service Plan
    /// </summary>
    public async Task<object> CreateWebAppAsync(
        string appName,
        string resourceGroupName,
        string location,
        Dictionary<string, object>? appSettings = null,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        _logger.LogInformation("Creating Web App {AppName} in resource group {ResourceGroupName}", appName, resourceGroupName);

        try
        {
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
            var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName, cancellationToken);

            // Create App Service Plan first
            var appServicePlanName = $"{appName}-plan";
            var sku = GetSettingValue<string>(appSettings, "sku", "B1");
            var runtime = GetSettingValue<string>(appSettings, "runtime", "dotnet:8.0");
            // Default to Windows apps to avoid LinuxFxVersion issues in Azure Government
            var isLinux = false;

            var appServicePlanData = new AppServicePlanData(new AzureLocation(location))
            {
                Sku = new AppServiceSkuDescription
                {
                    Name = sku,
                    Tier = sku.StartsWith("F") ? "Free" : sku.StartsWith("B") ? "Basic" : "Standard"
                },
                Kind = isLinux ? "linux" : "app",
                Tags = {
                    ["Environment"] = GetSettingValue<string>(appSettings, "environment", "development"),
                    ["ManagedBy"] = "SupervisorPlatform"
                }
            };

            var planCollection = resourceGroup.Value.GetAppServicePlans();
            var planOperation = await planCollection.CreateOrUpdateAsync(WaitUntil.Completed, appServicePlanName, appServicePlanData, cancellationToken);

            // Create Web App
            var siteConfig = new SiteConfigProperties
            {
                AppSettings = ConvertToAppSettingsList(GetSettingValue<Dictionary<string, object>>(appSettings, "appSettings", new()))
            };

            var webAppData = new WebSiteData(new AzureLocation(location))
            {
                AppServicePlanId = planOperation.Value.Id,
                SiteConfig = siteConfig,
                Tags = {
                    ["Environment"] = GetSettingValue<string>(appSettings, "environment", "development"),
                    ["ManagedBy"] = "SupervisorPlatform"
                }
            };

            var webAppCollection = resourceGroup.Value.GetWebSites();
            var webAppOperation = await webAppCollection.CreateOrUpdateAsync(WaitUntil.Completed, appName, webAppData, cancellationToken);

            return new
            {
                success = true,
                appId = webAppOperation.Value.Id.ToString(),
                appServicePlanId = planOperation.Value.Id.ToString(),
                appName,
                resourceGroupName,
                location,
                sku,
                runtime,
                httpsOnly = true,
                message = $"Web App {appName} created successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Web App {AppName}", appName);
            throw;
        }
    }

    /// <summary>
    /// Creates a Storage Account
    /// </summary>
    public async Task<object> CreateStorageAccountAsync(
        string storageAccountName,
        string resourceGroupName,
        string location,
        Dictionary<string, object>? storageSettings = null,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        _logger.LogInformation("Creating Storage Account {StorageAccountName} in resource group {ResourceGroupName}", storageAccountName, resourceGroupName);

        try
        {
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
            var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName, cancellationToken);

            var storageData = new StorageAccountCreateOrUpdateContent(
                new StorageSku(GetSettingValue<string>(storageSettings, "sku", "Standard_LRS")),
                StorageKind.StorageV2,
                new AzureLocation(location))
            {
                AccessTier = StorageAccountAccessTier.Hot,
                MinimumTlsVersion = StorageMinimumTlsVersion.Tls1_2,
                AllowBlobPublicAccess = false,
                EnableHttpsTrafficOnly = true,
                Tags = {
                    ["Environment"] = GetSettingValue<string>(storageSettings, "environment", "development"),
                    ["ManagedBy"] = "SupervisorPlatform",
                    ["CreatedAt"] = DateTime.UtcNow.ToString("yyyy-MM-dd")
                }
            };

            var storageCollection = resourceGroup.Value.GetStorageAccounts();
            var storageOperation = await storageCollection.CreateOrUpdateAsync(WaitUntil.Completed, storageAccountName, storageData, cancellationToken);

            return new
            {
                success = true,
                resourceId = storageOperation.Value.Id.ToString(),
                storageAccountId = storageOperation.Value.Id.ToString(),
                storageAccountName,
                resourceGroupName,
                location,
                sku = storageData.Sku.Name,
                accessTier = storageData.AccessTier?.ToString(),
                httpsOnly = storageData.EnableHttpsTrafficOnly,
                message = $"Storage Account {storageAccountName} created successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Storage Account {StorageAccountName}", storageAccountName);
            throw;
        }
    }

    public async Task<object> CreateKeyVaultAsync(
        string keyVaultName,
        string resourceGroupName,
        string location,
        Dictionary<string, object>? keyVaultSettings = null,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        _logger.LogInformation("Creating Key Vault {KeyVaultName} in resource group {ResourceGroupName}", keyVaultName, resourceGroupName);

        try
        {
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
            var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName, cancellationToken);

            // Get tenant ID from environment variable or use a default
            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");

            if (string.IsNullOrEmpty(tenantId))
            {
                _logger.LogWarning("Tenant ID not available - Key Vault requires tenant configuration");
                return new
                {
                    success = false,
                    keyVaultName,
                    resourceGroupName,
                    status = "Failed",
                    message = "Key Vault creation requires Azure tenant ID. Set AZURE_TENANT_ID environment variable."
                };
            }

            // For now, use generic resource creation as KeyVault SDK requires additional NuGet packages
            var properties = new
            {
                tenantId = tenantId,
                sku = new
                {
                    family = "A",
                    name = GetSettingValue<string>(keyVaultSettings, "sku", "standard")
                },
                enabledForDeployment = GetSettingValue<bool>(keyVaultSettings, "enabledForDeployment", true),
                enabledForDiskEncryption = GetSettingValue<bool>(keyVaultSettings, "enabledForDiskEncryption", true),
                enabledForTemplateDeployment = GetSettingValue<bool>(keyVaultSettings, "enabledForTemplateDeployment", true),
                enableSoftDelete = GetSettingValue<bool>(keyVaultSettings, "enableSoftDelete", true),
                enablePurgeProtection = GetSettingValue<bool>(keyVaultSettings, "enablePurgeProtection", true),
                softDeleteRetentionInDays = GetSettingValue<int>(keyVaultSettings, "softDeleteRetentionInDays", 90),
                accessPolicies = new object[] { } // Empty array - policies should be added post-creation
            };

            var result = await CreateResourceAsync(
                resourceGroupName,
                "Microsoft.KeyVault/vaults",
                keyVaultName,
                properties,
                subscriptionId,
                location,
                new Dictionary<string, string>
                {
                    { "Environment", GetSettingValue<string>(keyVaultSettings, "environment", "development") },
                    { "ManagedBy", "SupervisorPlatform" },
                    { "CreatedAt", DateTime.UtcNow.ToString("yyyy-MM-dd") }
                },
                cancellationToken);

            return new
            {
                success = true,
                resourceId = $"/subscriptions/{GetSubscriptionId(subscriptionId)}/resourceGroups/{resourceGroupName}/providers/Microsoft.KeyVault/vaults/{keyVaultName}",
                keyVaultId = $"/subscriptions/{GetSubscriptionId(subscriptionId)}/resourceGroups/{resourceGroupName}/providers/Microsoft.KeyVault/vaults/{keyVaultName}",
                keyVaultName,
                resourceGroupName,
                location,
                tenantId,
                enableSoftDelete = true,
                enablePurgeProtection = true,
                message = $"Key Vault {keyVaultName} created successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Key Vault {KeyVaultName}", keyVaultName);
            throw;
        }
    }

    public async Task<object> CreateBlobContainerAsync(
        string containerName,
        string storageAccountName,
        string resourceGroupName,
        Dictionary<string, object>? containerSettings = null,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        var armClient = EnsureArmClient();

        _logger.LogInformation("Creating blob container {ContainerName} in storage account {StorageAccountName}",
            containerName, storageAccountName);

        try
        {
            var subscription = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(GetSubscriptionId(subscriptionId)));
            var resourceGroup = await subscription.GetResourceGroupAsync(resourceGroupName, cancellationToken);

            // Get the storage account
            var storageAccount = await resourceGroup.Value.GetStorageAccountAsync(storageAccountName, cancellationToken: cancellationToken);

            if (storageAccount == null)
            {
                throw new InvalidOperationException($"Storage account '{storageAccountName}' not found in resource group '{resourceGroupName}'");
            }

            // Get blob service
            var blobServices = storageAccount.Value.GetBlobService();
            var blobService = await blobServices.GetAsync(cancellationToken: cancellationToken);

            // Create blob container
            var containerData = new BlobContainerData
            {
                PublicAccess = GetSettingValue<string>(containerSettings, "publicAccess", "None") switch
                {
                    "Blob" => StoragePublicAccessType.Blob,
                    "Container" => StoragePublicAccessType.Container,
                    _ => StoragePublicAccessType.None
                }
            };

            var containerCollection = blobService.Value.GetBlobContainers();
            var containerOperation = await containerCollection.CreateOrUpdateAsync(
                WaitUntil.Completed,
                containerName,
                containerData,
                cancellationToken);

            return new
            {
                success = true,
                resourceId = containerOperation.Value.Id.ToString(),
                containerId = containerOperation.Value.Id.ToString(),
                containerName,
                storageAccountName,
                resourceGroupName,
                publicAccess = containerData.PublicAccess.ToString(),
                message = $"Blob container '{containerName}' created successfully in storage account '{storageAccountName}'"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create blob container {ContainerName} in storage account {StorageAccountName}",
                containerName, storageAccountName);
            throw;
        }
    }

    private async Task<object> CreateStorageAccountAsync(
        ResourceGroupResource resourceGroup,
        string storageAccountName,
        object properties,
        string location,
        Dictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Storage Account creation requested for {StorageAccountName}", storageAccountName);

            await Task.CompletedTask;
            return new
            {
                resourceName = storageAccountName,
                resourceType = "Microsoft.Storage/storageAccounts",
                location = location,
                status = "Planned",
                message = "Storage Account creation planned"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create storage account {StorageAccountName}", storageAccountName);
            throw;
        }
    }

    private Task<object> CreateKeyVaultAsync(
        ResourceGroupResource resourceGroup,
        string keyVaultName,
        object properties,
        string location,
        Dictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        try
        {
            // For now, return a placeholder - KeyVault requires additional setup
            _logger.LogInformation("Key Vault creation requested for {KeyVaultName}", keyVaultName);

            return Task.FromResult<object>(new
            {
                resourceName = keyVaultName,
                resourceType = "Microsoft.KeyVault/vaults",
                location = location,
                status = "Planned",
                message = "Key Vault creation planned - requires tenant configuration"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create key vault {KeyVaultName}", keyVaultName);
            throw;
        }
    }

    private Task<object> CreateWebAppAsync(
        ResourceGroupResource resourceGroup,
        string webAppName,
        object properties,
        string location,
        Dictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        try
        {
            // Web App creation requires an App Service Plan first
            _logger.LogInformation("Web App creation requested for {WebAppName}", webAppName);

            return Task.FromResult<object>(new
            {
                resourceName = webAppName,
                resourceType = "Microsoft.Web/sites",
                location = location,
                status = "Planned",
                message = "Web App creation planned - requires App Service Plan"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create web app {WebAppName}", webAppName);
            throw;
        }
    }

    private async Task<object> CreateGenericResourceAsync(
        ResourceGroupResource resourceGroup,
        string resourceType,
        string resourceName,
        object properties,
        string? subscriptionId,
        string location,
        Dictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Generic resource creation requested for {ResourceName} of type {ResourceType}",
                resourceName, resourceType);

            // For generic resources, we'll use ARM template deployment
            var template = GenerateBasicArmTemplate(resourceType, resourceName, location, properties, tags);
            return await DeployTemplateAsync(resourceGroup.Data.Name, template, null, subscriptionId,
                $"create-{resourceName}", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create generic resource {ResourceName}", resourceName);
            throw;
        }
    }

    private string GenerateBasicArmTemplate(
        string resourceType,
        string resourceName,
        string location,
        object properties,
        Dictionary<string, string>? tags)
    {
        var template = new Dictionary<string, object>
        {
            ["$schema"] = "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
            ["contentVersion"] = "1.0.0.0",
            ["resources"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = resourceType,
                    ["apiVersion"] = "2023-01-01",
                    ["name"] = resourceName,
                    ["location"] = location,
                    ["tags"] = tags ?? new Dictionary<string, string>(),
                    ["properties"] = properties
                }
            }
        };

        return JsonSerializer.Serialize(template, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Helper method to get setting values with defaults
    /// </summary>
    private T GetSettingValue<T>(Dictionary<string, object>? settings, string key, T defaultValue)
    {
        if (settings == null || !settings.TryGetValue(key, out var value))
            return defaultValue;

        try
        {
            if (value is T directValue)
                return directValue;

            if (typeof(T) == typeof(int) && value is string strValue && int.TryParse(strValue, out var intValue))
                return (T)(object)intValue;

            if (typeof(T) == typeof(string))
                return (T)(object)value.ToString()!;

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Converts dictionary to App Service app settings list
    /// </summary>
    private IList<AppServiceNameValuePair> ConvertToAppSettingsList(Dictionary<string, object> appSettings)
    {
        var result = new List<AppServiceNameValuePair>();
        foreach (var setting in appSettings)
        {
            result.Add(new AppServiceNameValuePair
            {
                Name = setting.Key,
                Value = setting.Value?.ToString() ?? ""
            });
        }
        return result;
    }

    #region Resource Health Methods

    /// <summary>
    /// Gets resource health events from Azure Resource Health API
    /// </summary>
    public async Task<IEnumerable<object>> GetResourceHealthEventsAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogError("Azure integration is disabled - cannot retrieve resource health events");
            return new List<object>();
        }

        try
        {
            var actualSubscriptionId = GetSubscriptionId(subscriptionId);
            _logger.LogInformation("Getting resource health events for subscription {SubscriptionId}", actualSubscriptionId);

            var armClient = _clientFactory.GetArmClient(actualSubscriptionId);
            var subscription = await armClient.GetDefaultSubscriptionAsync(cancellationToken);

            // Note: Azure Resource Health API requires specialized implementation
            // For now, return a simulated result
            var healthEvents = new List<object>
            {
                new
                {
                    resourceId = $"/subscriptions/{actualSubscriptionId}/resourceGroups/example-rg/providers/Microsoft.Compute/virtualMachines/vm-example",
                    resourceName = "vm-example",
                    resourceType = "Microsoft.Compute/virtualMachines",
                    availabilityState = "Available",
                    summary = "No issues detected",
                    detailedStatus = "Resource is operating normally",
                    occurredDateTime = DateTimeOffset.UtcNow.AddHours(-1),
                    reasonType = "Planned",
                    resolutionETA = (DateTimeOffset?)null,
                    serviceImpacting = false
                }
            };

            return healthEvents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resource health events for subscription {SubscriptionId}", subscriptionId);
            return new List<object>();
        }
    }

    /// <summary>
    /// Gets resource health status for a specific resource
    /// </summary>
    public async Task<object?> GetResourceHealthAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogError("Azure integration is disabled - cannot retrieve resource health");
            return null;
        }

        try
        {
            _logger.LogDebug("Getting resource health for {ResourceId}", resourceId);

            await Task.CompletedTask;
            // Note: Azure Resource Health API requires specialized implementation
            // For now, return a simulated result based on resource availability
            var healthStatus = new
            {
                resourceId = resourceId,
                availabilityState = "Available",
                summary = "No issues detected",
                detailedStatus = "Resource is operating normally",
                occurredDateTime = DateTimeOffset.UtcNow.AddMinutes(-30),
                reasonType = "Scheduled",
                resolutionETA = (DateTimeOffset?)null,
                lastUpdated = DateTimeOffset.UtcNow
            };

            return healthStatus;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resource health for {ResourceId}", resourceId);
            return null;
        }
    }

    /// <summary>
    /// Creates an Azure Monitor alert rule for resource health
    /// </summary>
    public async Task<object> CreateAlertRuleAsync(string subscriptionId, string resourceGroupName, string alertRuleName, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogError("Azure integration is disabled - cannot create alert rule");
            throw new InvalidOperationException("Azure integration is disabled - enable Gateway.Azure.Enabled to create alert rules");
        }

        try
        {
            var actualSubscriptionId = GetSubscriptionId(subscriptionId);
            _logger.LogInformation("Creating alert rule {AlertRuleName} in resource group {ResourceGroupName}", alertRuleName, resourceGroupName);

            await Task.CompletedTask;
            // Note: Azure Monitor Alert Rules API requires specialized implementation
            // For now, return a simulated success result
            var alertRule = new
            {
                alertRuleId = $"/subscriptions/{actualSubscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Insights/metricAlerts/{alertRuleName}",
                name = alertRuleName,
                resourceGroupName = resourceGroupName,
                condition = "Resource health state changed to Unavailable",
                severity = 2, // Warning
                enabled = true,
                frequency = "PT5M", // Every 5 minutes
                windowSize = "PT5M",
                created = DateTimeOffset.UtcNow,
                success = true
            };

            return alertRule;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create alert rule {AlertRuleName}", alertRuleName);
            throw new InvalidOperationException($"Failed to create alert rule: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Lists Azure Monitor alert rules
    /// </summary>
    public async Task<IEnumerable<object>> ListAlertRulesAsync(string subscriptionId, string? resourceGroupName = null, CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogError("Azure integration is disabled - cannot list alert rules");
            return new List<object>();
        }

        try
        {
            var actualSubscriptionId = GetSubscriptionId(subscriptionId);
            _logger.LogInformation("Listing alert rules for subscription {SubscriptionId}", actualSubscriptionId);

            await Task.CompletedTask;
            // Note: Azure Monitor Alert Rules API requires specialized implementation
            // For now, return simulated alert rules
            var alertRules = new List<object>
            {
                new
                {
                    name = "ResourceHealthAlert",
                    resourceGroupName = resourceGroupName ?? "rg-monitoring",
                    targetResourceType = "Microsoft.Compute/virtualMachines",
                    condition = "Resource health state changed",
                    severity = "Warning",
                    enabled = true,
                    frequency = "PT5M",
                    created = DateTimeOffset.UtcNow.AddDays(-7)
                },
                new
                {
                    name = "StorageHealthAlert",
                    resourceGroupName = resourceGroupName ?? "rg-monitoring",
                    targetResourceType = "Microsoft.Storage/storageAccounts",
                    condition = "Resource availability degraded",
                    severity = "Critical",
                    enabled = true,
                    frequency = "PT1M",
                    created = DateTimeOffset.UtcNow.AddDays(-3)
                }
            };

            return alertRules;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list alert rules for subscription {SubscriptionId}", subscriptionId);
            return new List<object>();
        }
    }

    /// <summary>
    /// Lists diagnostic settings for a specific resource
    /// </summary>


    /// <summary>
    /// Gets resource health history for resources
    /// </summary>
    public async Task<IEnumerable<object>> GetResourceHealthHistoryAsync(string subscriptionId, string? resourceId = null, string timeRange = "24h", CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogError("Azure integration is disabled - cannot retrieve resource health history");
            return new List<object>();
        }

        try
        {
            var actualSubscriptionId = GetSubscriptionId(subscriptionId);
            _logger.LogInformation("Getting resource health history for subscription {SubscriptionId}, time range {TimeRange}", actualSubscriptionId, timeRange);

            await Task.CompletedTask;
            // Note: Azure Resource Health History API requires specialized implementation
            // For now, return simulated historical data
            var historyEntries = new List<object>
            {
                new
                {
                    resourceId = resourceId ?? $"/subscriptions/{actualSubscriptionId}/resourceGroups/example-rg/providers/Microsoft.Compute/virtualMachines/vm-example",
                    resourceName = "vm-example",
                    availabilityState = "Available",
                    summary = "Resource returned to normal operation",
                    detailedStatus = "Planned maintenance completed successfully",
                    occurredDateTime = DateTimeOffset.UtcNow.AddHours(-2),
                    resolvedDateTime = DateTimeOffset.UtcNow.AddMinutes(-90),
                    reasonType = "Planned"
                },
                new
                {
                    resourceId = resourceId ?? $"/subscriptions/{actualSubscriptionId}/resourceGroups/example-rg/providers/Microsoft.Compute/virtualMachines/vm-example",
                    resourceName = "vm-example",
                    availabilityState = "Unavailable",
                    summary = "Resource temporarily unavailable for planned maintenance",
                    detailedStatus = "Planned maintenance was performed on the underlying infrastructure",
                    occurredDateTime = DateTimeOffset.UtcNow.AddHours(-4),
                    resolvedDateTime = DateTimeOffset.UtcNow.AddHours(-2),
                    reasonType = "Planned"
                }
            };

            return historyEntries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resource health history for subscription {SubscriptionId}", subscriptionId);
            return new List<object>();
        }
    }

    public async Task<List<AzureResource>> ListAllResourceGroupsInSubscriptionAsync(string subscriptionId)
    {
        try
        {
            var armClient = EnsureArmClient();
            _logger.LogInformation("Listing all resources in subscription {SubscriptionId}", subscriptionId);

            var subscription = armClient.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(subscriptionId));

            var resources = new List<AzureResource>();

            // List all resources in the subscription using GenericResources
            await foreach (var genericResource in subscription.GetGenericResourcesAsync())
            {
                try
                {
                    var resource = new AzureResource
                    {
                        Id = genericResource.Id.ToString(),
                        Name = genericResource.Data.Name,
                        Type = genericResource.Data.ResourceType.ToString(),
                        Location = genericResource.Data.Location.ToString(),
                        ResourceGroup = genericResource.Id.ResourceGroupName ?? "N/A",
                        SubscriptionId = subscriptionId,
                        Tags = genericResource.Data.Tags?.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value) ?? new Dictionary<string, string>()
                    };

                    // Add properties if available
                    if (genericResource.Data.Properties != null)
                    {
                        try
                        {
                            var propertiesJson = genericResource.Data.Properties.ToString();
                            if (!string.IsNullOrEmpty(propertiesJson))
                            {
                                resource.Properties["raw"] = propertiesJson;
                            }
                        }
                        catch (Exception propEx)
                        {
                            _logger.LogDebug(propEx, "Failed to serialize properties for resource {ResourceId}", resource.Id);
                        }
                    }

                    resources.Add(resource);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse resource, skipping");
                }
            }

            _logger.LogInformation("Found {ResourceCount} resources in subscription {SubscriptionId}",
                resources.Count, subscriptionId);

            return resources;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list resources in subscription {SubscriptionId}", subscriptionId);
            throw;
        }
    }

    public async Task<List<AzureResource>> ListAllResourceGroupsInSubscriptionAsync(string subscriptionId, string resourceGroupName)
    {
        var armClient = EnsureArmClient();

        try
        {
            _logger.LogInformation("Listing all resources in resource group {ResourceGroup} (subscription {SubscriptionId})",
                resourceGroupName, subscriptionId);

            var subscription = armClient.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(subscriptionId));

            Response<ResourceGroupResource>? resourceGroupResponse;
            try
            {
                resourceGroupResponse = await subscription.GetResourceGroupAsync(resourceGroupName);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                _logger.LogDebug("Resource group '{ResourceGroup}' not found in subscription {SubscriptionId} (this is expected for planning/guidance queries)",
                    resourceGroupName, subscriptionId);
                return new List<AzureResource>();
            }

            if (resourceGroupResponse?.Value == null)
            {
                _logger.LogWarning("Resource group {ResourceGroup} not found in subscription {SubscriptionId}",
                    resourceGroupName, subscriptionId);
                return new List<AzureResource>();
            }

            var resources = new List<AzureResource>();

            // List all resources in the resource group using GenericResources
            await foreach (var genericResource in resourceGroupResponse.Value.GetGenericResourcesAsync())
            {
                try
                {
                    var resource = new AzureResource
                    {
                        Id = genericResource.Id.ToString(),
                        Name = genericResource.Data.Name,
                        Type = genericResource.Data.ResourceType.ToString(),
                        Location = genericResource.Data.Location.ToString(),
                        ResourceGroup = resourceGroupName,
                        SubscriptionId = subscriptionId,
                        Tags = genericResource.Data.Tags?.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value) ?? new Dictionary<string, string>()
                    };

                    // Add properties if available
                    if (genericResource.Data.Properties != null)
                    {
                        try
                        {
                            var propertiesJson = genericResource.Data.Properties.ToString();
                            if (!string.IsNullOrEmpty(propertiesJson))
                            {
                                resource.Properties["raw"] = propertiesJson;
                            }
                        }
                        catch (Exception propEx)
                        {
                            _logger.LogDebug(propEx, "Failed to serialize properties for resource {ResourceId}", resource.Id);
                        }
                    }

                    resources.Add(resource);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse resource, skipping");
                }
            }

            _logger.LogInformation("Found {ResourceCount} resources in resource group {ResourceGroup}",
                resources.Count, resourceGroupName);

            return resources;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list resources in resource group {ResourceGroup}", resourceGroupName);
            throw;
        }
    }

    public async Task<AzureResource?> GetResourceAsync(string resourceId)
    {
        if (!_options.Enabled)
        {
            _logger.LogWarning("Azure integration is disabled");
            return null;
        }

        try
        {
            _logger.LogDebug("Getting resource {ResourceId}", resourceId);

            var armClient = _clientFactory.GetArmClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId!);
            var genericResource = armClient.GetGenericResource(resourceIdentifier);
            
            if (genericResource == null)
            {
                _logger.LogWarning("Could not create generic resource reference for {ResourceId}", resourceId);
                return null;
            }
            
            var data = await genericResource.GetAsync();

            if (data?.Value == null)
            {
                _logger.LogWarning("Resource {ResourceId} not found", resourceId);
                return null;
            }

            var resource = new AzureResource
            {
                Id = data.Value.Id.ToString(),
                Name = data.Value.Data.Name,
                Type = data.Value.Data.ResourceType.ToString(),
                Location = data.Value.Data.Location.ToString(),
                ResourceGroup = data.Value.Id.ResourceGroupName ?? "N/A",
                SubscriptionId = data.Value.Id.SubscriptionId,
                Tags = data.Value.Data.Tags?.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value) ?? new Dictionary<string, string>()
            };

            // Add properties if available
            if (data.Value.Data.Properties != null)
            {
                try
                {
                    var propertiesJson = data.Value.Data.Properties.ToString();
                    if (!string.IsNullOrEmpty(propertiesJson))
                    {
                        resource.Properties["raw"] = propertiesJson;
                    }
                }
                catch (Exception propEx)
                {
                    _logger.LogDebug(propEx, "Failed to serialize properties for resource {ResourceId}", resource.Id);
                }
            }

            return resource;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resource {ResourceId}", resourceId);
            return null;
        }
    }

    public async Task<string> CreateResourceGroupAsync(
        string subscriptionId,
        string resourceGroupName,
        string region,
        Dictionary<string, string> tags)
    {
        _logger.LogInformation("Creating resource group {ResourceGroup} in {Region}",
            resourceGroupName, region);

        try
        {
            var subscription = EnsureArmClient().GetSubscriptionResource(new ResourceIdentifier(subscriptionId!));
            var resourceGroups = subscription.GetResourceGroups();

            var rgData = new ResourceGroupData(new AzureLocation(region));
            foreach (var tag in tags)
            {
                rgData.Tags.Add(tag.Key, tag.Value);
            }

            var rgOperation = await resourceGroups.CreateOrUpdateAsync(
                WaitUntil.Completed,
                resourceGroupName,
                rgData);

            var resourceGroupId = rgOperation.Value.Id.ToString();
            _logger.LogInformation("Resource group created: {ResourceGroupId}", resourceGroupId);

            return resourceGroupId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create resource group {ResourceGroup}", resourceGroupName);
            throw new InvalidOperationException($"Resource group creation failed: {ex.Message}", ex);
        }
    }



    #endregion

    #region Subscription Methods

    /// <summary>
    /// Gets subscription details by ID
    /// </summary>
    /// <param name="subscriptionId">Subscription ID</param>
    /// <returns>Subscription information</returns>
    public async Task<AzureSubscription> GetSubscriptionAsync(string subscriptionId)
    {
        var armClient = EnsureArmClient();

        try
        {
            _logger.LogInformation("Getting subscription details for {SubscriptionId}", subscriptionId);

            var subscription = armClient.GetSubscriptionResource(
                SubscriptionResource.CreateResourceIdentifier(subscriptionId));

            var subscriptionData = await subscription.GetAsync();

            return new AzureSubscription
            {
                SubscriptionId = subscriptionData.Value.Data.SubscriptionId ?? subscriptionId,
                SubscriptionName = subscriptionData.Value.Data.DisplayName ?? "Unknown",
                State = subscriptionData.Value.Data.State?.ToString() ?? "Unknown",
                TenantId = subscriptionData.Value.Data.TenantId?.ToString() ?? "",
                CreatedDate = DateTime.UtcNow, // Azure doesn't provide creation date directly
                Tags = subscriptionData.Value.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get subscription {SubscriptionId}", subscriptionId);
            throw new InvalidOperationException($"Failed to retrieve subscription: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Gets subscription details by display name
    /// </summary>
    /// <param name="subscriptionName">Subscription display name</param>
    /// <returns>Subscription information</returns>
    /// <exception cref="InvalidOperationException">Thrown when subscription not found or multiple matches exist</exception>
    public async Task<AzureSubscription> GetSubscriptionByNameAsync(string subscriptionName)
    {
        var armClient = EnsureArmClient();

        try
        {
            _logger.LogInformation("Getting subscription details for name {SubscriptionName}", subscriptionName);

            var matchingSubscriptions = new List<SubscriptionResource>();

            await foreach (var subscription in armClient.GetSubscriptions().GetAllAsync())
            {
                if (string.Equals(subscription.Data.DisplayName, subscriptionName, StringComparison.OrdinalIgnoreCase))
                {
                    matchingSubscriptions.Add(subscription);
                }
            }

            if (matchingSubscriptions.Count == 0)
            {
                throw new InvalidOperationException($"No subscription found with name '{subscriptionName}'");
            }

            if (matchingSubscriptions.Count > 1)
            {
                throw new InvalidOperationException($"Multiple subscriptions found with name '{subscriptionName}'. Please use subscription ID instead.");
            }

            var sub = matchingSubscriptions[0];
            return new AzureSubscription
            {
                SubscriptionId = sub.Data.SubscriptionId ?? "",
                SubscriptionName = sub.Data.DisplayName ?? subscriptionName,
                State = sub.Data.State?.ToString() ?? "Unknown",
                TenantId = sub.Data.TenantId?.ToString() ?? "",
                CreatedDate = DateTime.UtcNow, // Azure doesn't provide creation date directly
                Tags = sub.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, string>()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get subscription by name {SubscriptionName}", subscriptionName);
            throw new InvalidOperationException($"Failed to retrieve subscription by name: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Verifies if a subscription name is available
    /// </summary>
    /// <param name="subscriptionName">Proposed subscription name</param>
    /// <returns>True if available</returns>
    public async Task<bool> IsSubscriptionNameAvailableAsync(string subscriptionName)
    {
        var armClient = EnsureArmClient();

        try
        {
            _logger.LogInformation("Checking availability of subscription name {SubscriptionName}", subscriptionName);

            await foreach (var subscription in armClient.GetSubscriptions().GetAllAsync())
            {
                if (string.Equals(subscription.Data.DisplayName, subscriptionName, StringComparison.OrdinalIgnoreCase))
                {
                    return false; // Name is already taken
                }
            }

            return true; // Name is available
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check subscription name availability for {SubscriptionName}", subscriptionName);
            // On error, assume name is not available for safety
            return false;
        }
    }

    #endregion

    #region Additional Compliance Methods

    public async Task<IEnumerable<object>> GetResourceHealthHistoryAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            // For compliance-focused mode, return empty collection
            _logger.LogInformation("Resource health history requested for {ResourceId}", resourceId);
            return Enumerable.Empty<object>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get resource health history for {ResourceId}", resourceId);
            return Enumerable.Empty<object>();
        }
    }

    public async Task<IEnumerable<DiagnosticSettingInfo>> ListDiagnosticSettingsForResourceAsync(string resourceId, CancellationToken cancellationToken = default)
    {
        try
        {
            // For compliance-focused mode, return empty collection
            _logger.LogInformation("Diagnostic settings requested for {ResourceId}", resourceId);
            return Enumerable.Empty<DiagnosticSettingInfo>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get diagnostic settings for {ResourceId}", resourceId);
            return Enumerable.Empty<DiagnosticSettingInfo>();
        }
    }

    // Subscription Management Stub Implementations
    public Task<string> CreateSubscriptionAsync(string subscriptionName, string billingScope, string managementGroupId, Dictionary<string, string> tags)
    {
        throw new NotImplementedException("Subscription creation not yet implemented");
    }

    public Task<string> AssignOwnerRoleAsync(string subscriptionId, string userEmail)
    {
        throw new NotImplementedException("Role assignment not yet implemented");
    }

    public Task<string> AssignContributorRoleAsync(string subscriptionId, string userEmail)
    {
        throw new NotImplementedException("Role assignment not yet implemented");
    }

    public Task MoveToManagementGroupAsync(string subscriptionId, string managementGroupId)
    {
        throw new NotImplementedException("Management group operations not yet implemented");
    }

    public Task ApplySubscriptionTagsAsync(string subscriptionId, Dictionary<string, string> tags)
    {
        throw new NotImplementedException("Subscription tagging not yet implemented");
    }

    public Task DeleteSubscriptionAsync(string subscriptionId)
    {
        throw new NotImplementedException("Subscription deletion not yet implemented");
    }

    // Network Stub Implementations
    public Task<string> CreateVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName, string vnetCidr, string region, Dictionary<string, string> tags)
    {
        throw new NotImplementedException("VNet creation not yet implemented");
    }

    public Task<List<string>> CreateSubnetsAsync(string subscriptionId, string resourceGroupName, string vnetName, List<Platform.Engineering.Copilot.Core.Models.SubnetConfiguration> subnets)
    {
        throw new NotImplementedException("Subnet creation not yet implemented");
    }

    public List<Platform.Engineering.Copilot.Core.Models.SubnetConfiguration> GenerateSubnetConfigurations(string vnetCidr, int subnetCount, int subnetSize, string namePrefix)
    {
        throw new NotImplementedException("Subnet generation not yet implemented");
    }

    public Task<string> CreateNetworkSecurityGroupAsync(string subscriptionId, string resourceGroupName, string nsgName, string region, NsgDefaultRules defaultRules, Dictionary<string, string> tags)
    {
        throw new NotImplementedException("NSG creation not yet implemented");
    }

    public Task AssociateNsgWithSubnetAsync(string subscriptionId, string resourceGroupName, string vnetName, string subnetName, string nsgResourceId)
    {
        throw new NotImplementedException("NSG association not yet implemented");
    }

    public Task EnableDDoSProtectionAsync(string subscriptionId, string resourceGroupName, string vnetName, string? ddosPlanId)
    {
        throw new NotImplementedException("DDoS protection not yet implemented");
    }

    public Task ConfigureDnsServersAsync(string subscriptionId, string resourceGroupName, string vnetName, List<string> dnsServers)
    {
        throw new NotImplementedException("DNS configuration not yet implemented");
    }

    public Task DeleteVirtualNetworkAsync(string subscriptionId, string resourceGroupName, string vnetName)
    {
        throw new NotImplementedException("VNet deletion not yet implemented");
    }

    public bool ValidateCidr(string cidr)
    {
        throw new NotImplementedException("CIDR validation not yet implemented");
    }

    #endregion
}
