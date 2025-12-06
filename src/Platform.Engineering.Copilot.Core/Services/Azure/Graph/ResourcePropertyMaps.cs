namespace Platform.Engineering.Copilot.Core.Services.Azure.Graph;

/// <summary>
/// Maps Azure resource types to their extended properties available in Azure Resource Graph.
/// Used to build KQL queries that project service-specific properties.
/// </summary>
public static class ResourcePropertyMaps
{
    /// <summary>
    /// Map of resource type (lowercase) to extended property paths in Resource Graph
    /// </summary>
    private static readonly Dictionary<string, List<string>> ExtendedPropertyMap = new()
    {
        // App Service (Web Apps)
        ["microsoft.web/sites"] = new()
        {
            "properties.sku.name",
            "properties.httpsOnly",
            "properties.siteConfig.linuxFxVersion",
            "properties.siteConfig.windowsFxVersion",
            "properties.siteConfig.alwaysOn",
            "properties.siteConfig.minTlsVersion",
            "properties.clientCertEnabled",
            "properties.defaultHostName",
            "properties.state",
            "properties.enabled"
        },

        // App Service Plans
        ["microsoft.web/serverfarms"] = new()
        {
            "properties.sku.name",
            "properties.sku.tier",
            "properties.sku.capacity",
            "properties.reserved", // Linux
            "properties.maximumElasticWorkerCount",
            "properties.status"
        },

        // Azure Kubernetes Service (AKS)
        ["microsoft.containerservice/managedclusters"] = new()
        {
            "properties.kubernetesVersion",
            "properties.nodeResourceGroup",
            "properties.enableRBAC",
            "properties.networkProfile.networkPlugin",
            "properties.networkProfile.serviceCidr",
            "properties.networkProfile.dnsServiceIP",
            "properties.addonProfiles.azurepolicy.enabled",
            "properties.addonProfiles.omsagent.enabled",
            "properties.privateFQDN",
            "properties.apiServerAccessProfile.enablePrivateCluster"
        },

        // Storage Accounts
        ["microsoft.storage/storageaccounts"] = new()
        {
            "properties.sku.name",
            "properties.sku.tier",
            "properties.accessTier",
            "properties.supportsHttpsTrafficOnly",
            "properties.minimumTlsVersion",
            "properties.allowBlobPublicAccess",
            "properties.isHnsEnabled", // Hierarchical namespace
            "properties.encryption.services.blob.enabled",
            "properties.encryption.services.file.enabled",
            "properties.networkAcls.defaultAction",
            "properties.primaryEndpoints.blob"
        },

        // SQL Servers
        ["microsoft.sql/servers"] = new()
        {
            "properties.version",
            "properties.administratorLogin",
            "properties.publicNetworkAccess",
            "properties.minimalTlsVersion",
            "properties.state",
            "properties.fullyQualifiedDomainName"
        },

        // SQL Databases
        ["microsoft.sql/servers/databases"] = new()
        {
            "properties.sku.name",
            "properties.sku.tier",
            "properties.status",
            "properties.maxSizeBytes",
            "properties.collation",
            "properties.readScale",
            "properties.zoneRedundant"
        },

        // Key Vaults
        ["microsoft.keyvault/vaults"] = new()
        {
            "properties.sku.name",
            "properties.sku.family",
            "properties.enabledForDeployment",
            "properties.enabledForDiskEncryption",
            "properties.enabledForTemplateDeployment",
            "properties.enableSoftDelete",
            "properties.softDeleteRetentionInDays",
            "properties.enableRbacAuthorization",
            "properties.enablePurgeProtection",
            "properties.networkAcls.defaultAction",
            "properties.vaultUri"
        },

        // Virtual Machines
        ["microsoft.compute/virtualmachines"] = new()
        {
            "properties.hardwareProfile.vmSize",
            "properties.storageProfile.osDisk.osType",
            "properties.storageProfile.osDisk.createOption",
            "properties.storageProfile.osDisk.managedDisk.storageAccountType",
            "properties.osProfile.computerName",
            "properties.osProfile.adminUsername",
            "properties.networkProfile.networkInterfaces",
            "properties.provisioningState",
            "properties.vmId"
        },

        // Virtual Networks
        ["microsoft.network/virtualnetworks"] = new()
        {
            "properties.addressSpace.addressPrefixes",
            "properties.subnets",
            "properties.enableDdosProtection",
            "properties.enableVmProtection",
            "properties.provisioningState"
        },

        // Network Security Groups
        ["microsoft.network/networksecuritygroups"] = new()
        {
            "properties.securityRules",
            "properties.defaultSecurityRules",
            "properties.networkInterfaces",
            "properties.subnets",
            "properties.provisioningState"
        },

        // Public IP Addresses
        ["microsoft.network/publicipaddresses"] = new()
        {
            "properties.sku.name",
            "properties.publicIPAllocationMethod",
            "properties.publicIPAddressVersion",
            "properties.ipAddress",
            "properties.dnsSettings.domainNameLabel",
            "properties.provisioningState"
        },

        // Application Gateways
        ["microsoft.network/applicationgateways"] = new()
        {
            "properties.sku.name",
            "properties.sku.tier",
            "properties.sku.capacity",
            "properties.enableHttp2",
            "properties.autoscaleConfiguration",
            "properties.provisioningState"
        },

        // Cosmos DB
        ["microsoft.documentdb/databaseaccounts"] = new()
        {
            "properties.databaseAccountOfferType",
            "properties.consistencyPolicy.defaultConsistencyLevel",
            "properties.enableAutomaticFailover",
            "properties.enableMultipleWriteLocations",
            "properties.capabilities",
            "properties.publicNetworkAccess",
            "properties.documentEndpoint"
        },

        // Log Analytics Workspaces
        ["microsoft.operationalinsights/workspaces"] = new()
        {
            "properties.sku.name",
            "properties.retentionInDays",
            "properties.publicNetworkAccessForIngestion",
            "properties.publicNetworkAccessForQuery",
            "properties.provisioningState",
            "properties.customerId"
        },

        // Container Registries
        ["microsoft.containerregistry/registries"] = new()
        {
            "properties.sku.name",
            "properties.adminUserEnabled",
            "properties.loginServer",
            "properties.publicNetworkAccess",
            "properties.networkRuleBypassOptions",
            "properties.zoneRedundancy"
        },

        // Azure Functions
        ["microsoft.web/sites"] = new() // Same as web apps, differentiated by kind
        {
            "properties.sku.name",
            "properties.httpsOnly",
            "properties.state",
            "properties.defaultHostName"
        },

        // API Management
        ["microsoft.apimanagement/service"] = new()
        {
            "properties.sku.name",
            "properties.sku.capacity",
            "properties.publisherEmail",
            "properties.publisherName",
            "properties.gatewayUrl",
            "properties.enableClientCertificate",
            "properties.virtualNetworkType"
        },

        // Event Hubs Namespaces
        ["microsoft.eventhub/namespaces"] = new()
        {
            "properties.sku.name",
            "properties.sku.tier",
            "properties.sku.capacity",
            "properties.isAutoInflateEnabled",
            "properties.maximumThroughputUnits",
            "properties.zoneRedundant"
        },

        // Service Bus Namespaces
        ["microsoft.servicebus/namespaces"] = new()
        {
            "properties.sku.name",
            "properties.sku.tier",
            "properties.sku.capacity",
            "properties.zoneRedundant",
            "properties.provisioningState"
        },

        // Application Insights
        ["microsoft.insights/components"] = new()
        {
            "properties.applicationId",
            "properties.applicationType",
            "properties.instrumentationKey",
            "properties.workspaceResourceId",
            "properties.retentionInDays",
            "properties.publicNetworkAccessForIngestion",
            "properties.publicNetworkAccessForQuery"
        }
    };

    /// <summary>
    /// Get extended properties for a specific resource type
    /// </summary>
    public static List<string> GetExtendedPropertiesForType(string resourceType)
    {
        var normalizedType = resourceType.ToLowerInvariant();
        
        if (ExtendedPropertyMap.TryGetValue(normalizedType, out var properties))
        {
            return properties;
        }

        // Return empty list for unknown types - will use default properties only
        return new List<string>();
    }

    /// <summary>
    /// Check if a resource type has extended property mappings
    /// </summary>
    public static bool HasExtendedProperties(string resourceType)
    {
        var normalizedType = resourceType.ToLowerInvariant();
        return ExtendedPropertyMap.ContainsKey(normalizedType);
    }

    /// <summary>
    /// Get all supported resource types
    /// </summary>
    public static List<string> GetSupportedResourceTypes()
    {
        return ExtendedPropertyMap.Keys.ToList();
    }

    /// <summary>
    /// Build KQL projection clause for extended properties
    /// </summary>
    public static string BuildExtendProjection(string resourceType)
    {
        var properties = GetExtendedPropertiesForType(resourceType);
        
        if (!properties.Any())
        {
            return string.Empty;
        }

        var projections = new List<string>();
        
        foreach (var prop in properties)
        {
            // Extract the last segment as the alias
            var alias = prop.Split('.').Last();
            projections.Add($"| extend {alias} = {prop}");
        }

        return string.Join("\n", projections);
    }
}
