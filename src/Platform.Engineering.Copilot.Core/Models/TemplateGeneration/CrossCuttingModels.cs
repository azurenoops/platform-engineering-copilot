namespace Platform.Engineering.Copilot.Core.Models.TemplateGeneration;

/// <summary>
/// Types of cross-cutting infrastructure components that can be attached to resources
/// These represent reusable patterns that apply across multiple resource types
/// </summary>
public enum CrossCuttingType
{
    /// <summary>
    /// Private endpoint for network isolation (FedRAMP SC-7)
    /// </summary>
    PrivateEndpoint,
    
    /// <summary>
    /// Diagnostic settings for logging and monitoring (FedRAMP AU-2, AU-6)
    /// </summary>
    DiagnosticSettings,
    
    /// <summary>
    /// RBAC role assignments for access control (FedRAMP AC-3, AC-6)
    /// </summary>
    RBACAssignment,
    
    /// <summary>
    /// Network Security Group for traffic filtering (FedRAMP SC-7, AC-4)
    /// </summary>
    NetworkSecurityGroup,
    
    /// <summary>
    /// User-Assigned Managed Identity (FedRAMP IA-2, AC-2)
    /// </summary>
    ManagedIdentity,
    
    /// <summary>
    /// Private DNS Zone for private endpoint resolution
    /// </summary>
    PrivateDNSZone,
    
    /// <summary>
    /// Public IP Address (when required)
    /// </summary>
    PublicIPAddress,
    
    /// <summary>
    /// Service Endpoint for Azure PaaS services
    /// </summary>
    ServiceEndpoint
}

/// <summary>
/// Request model for generating cross-cutting infrastructure modules
/// </summary>
public class CrossCuttingRequest
{
    /// <summary>
    /// Reference to the resource this cross-cutting component attaches to
    /// Example: "keyVault.outputs.id" or "storageAccount.id"
    /// </summary>
    public string ResourceReference { get; set; } = string.Empty;
    
    /// <summary>
    /// Name of the parent resource
    /// Example: "my-keyvault", "my-storage"
    /// </summary>
    public string ResourceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure resource type of the parent resource
    /// Example: "Microsoft.KeyVault/vaults", "Microsoft.Storage/storageAccounts"
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;
    
    /// <summary>
    /// Output path for generated module files
    /// Example: "modules/keyvault-pe", "modules/storage-diag"
    /// </summary>
    public string ModulePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure region for resource deployment
    /// </summary>
    public string Location { get; set; } = "eastus";
    
    /// <summary>
    /// Type-specific configuration for the cross-cutting component (generic)
    /// </summary>
    public Dictionary<string, object> Config { get; set; } = new();
    
    /// <summary>
    /// Tags to apply to generated resources
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();
    
    // ===== Strongly-typed configurations for specific cross-cutting types =====
    
    /// <summary>
    /// Private Endpoint configuration (when generating PE modules)
    /// </summary>
    public PrivateEndpointConfig? PrivateEndpoint { get; set; }
    
    /// <summary>
    /// Diagnostic Settings configuration (when generating diagnostics modules)
    /// </summary>
    public DiagnosticSettingsConfig? DiagnosticSettings { get; set; }
    
    /// <summary>
    /// RBAC Assignment configuration (when generating RBAC modules)
    /// </summary>
    public RBACAssignmentConfig? RBAC { get; set; }
    
    /// <summary>
    /// NSG configuration (when generating NSG modules)
    /// </summary>
    public NSGConfig? NSG { get; set; }
}

/// <summary>
/// Configuration specific to Private Endpoint generation
/// </summary>
public class PrivateEndpointConfig
{
    /// <summary>
    /// Subnet ID where the private endpoint will be created
    /// </summary>
    public string SubnetId { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether to create a private DNS zone
    /// </summary>
    public bool CreateDnsZone { get; set; } = true;
    
    /// <summary>
    /// Existing private DNS zone ID (if not creating new)
    /// </summary>
    public string? ExistingDnsZoneId { get; set; }
    
    /// <summary>
    /// VNet ID for DNS zone link (required if CreateDnsZone is true)
    /// </summary>
    public string? VNetId { get; set; }
}

/// <summary>
/// Configuration specific to Diagnostic Settings generation
/// </summary>
public class DiagnosticSettingsConfig
{
    /// <summary>
    /// Log Analytics Workspace ID
    /// </summary>
    public string WorkspaceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Storage Account ID for long-term archival (optional)
    /// </summary>
    public string? StorageAccountId { get; set; }
    
    /// <summary>
    /// Event Hub Authorization Rule ID (optional)
    /// </summary>
    public string? EventHubAuthorizationRuleId { get; set; }
    
    /// <summary>
    /// Log categories to enable (if null, uses resource defaults)
    /// </summary>
    public List<string>? LogCategories { get; set; }
    
    /// <summary>
    /// Metric categories to enable (if null, enables AllMetrics)
    /// </summary>
    public List<string>? MetricCategories { get; set; }
    
    /// <summary>
    /// Retention days for logs (0 = indefinite)
    /// </summary>
    public int RetentionDays { get; set; } = 90;
}

/// <summary>
/// Configuration specific to RBAC Assignment generation
/// </summary>
public class RBACAssignmentConfig
{
    /// <summary>
    /// Principal ID to assign the role to
    /// </summary>
    public string PrincipalId { get; set; } = string.Empty;
    
    /// <summary>
    /// Role definition ID or built-in role name
    /// Example: "Key Vault Administrator", "Storage Blob Data Contributor"
    /// </summary>
    public string RoleDefinitionIdOrName { get; set; } = string.Empty;
    
    /// <summary>
    /// Principal type (ServicePrincipal, User, Group)
    /// </summary>
    public string PrincipalType { get; set; } = "ServicePrincipal";
    
    /// <summary>
    /// Description for the role assignment
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Configuration specific to NSG generation
/// </summary>
public class NSGConfig
{
    /// <summary>
    /// NSG rules to create
    /// </summary>
    public List<NSGRuleConfig> Rules { get; set; } = new();
    
    /// <summary>
    /// Subnet IDs to associate the NSG with
    /// </summary>
    public List<string> SubnetAssociations { get; set; } = new();
}

/// <summary>
/// Individual NSG rule configuration
/// </summary>
public class NSGRuleConfig
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Direction { get; set; } = "Inbound";
    public string Access { get; set; } = "Allow";
    public string Protocol { get; set; } = "Tcp";
    public string SourceAddressPrefix { get; set; } = "*";
    public string SourcePortRange { get; set; } = "*";
    public string DestinationAddressPrefix { get; set; } = "*";
    public string DestinationPortRange { get; set; } = "*";
    public string? Description { get; set; }
}

/// <summary>
/// Result from generating a core resource module
/// </summary>
public class ResourceModuleResult
{
    /// <summary>
    /// Generated files (path -> content)
    /// </summary>
    public Dictionary<string, string> Files { get; set; } = new();
    
    /// <summary>
    /// Bicep/Terraform reference to the resource for use in cross-cutting modules
    /// Example: "keyVault" (can use "keyVault.outputs.id")
    /// </summary>
    public string ResourceReference { get; set; } = string.Empty;
    
    /// <summary>
    /// Azure resource type
    /// Example: "Microsoft.KeyVault/vaults"
    /// </summary>
    public string ResourceType { get; set; } = string.Empty;
    
    /// <summary>
    /// Output names exposed by this resource
    /// Example: ["id", "name", "uri"]
    /// </summary>
    public List<string> OutputNames { get; set; } = new();
    
    /// <summary>
    /// Cross-cutting types this resource supports
    /// </summary>
    public List<CrossCuttingType> SupportedCrossCutting { get; set; } = new();
}

/// <summary>
/// Static mapping of Azure resource types to their cross-cutting capabilities
/// and private endpoint group IDs
/// </summary>
public static class CrossCuttingCapabilityMap
{
    /// <summary>
    /// Maps Azure resource types to private endpoint group IDs
    /// </summary>
    public static readonly Dictionary<string, string> PrivateEndpointGroupIds = new()
    {
        ["Microsoft.KeyVault/vaults"] = "vault",
        ["Microsoft.Storage/storageAccounts"] = "blob",
        ["Microsoft.Storage/storageAccounts/blob"] = "blob",
        ["Microsoft.Storage/storageAccounts/file"] = "file",
        ["Microsoft.Storage/storageAccounts/queue"] = "queue",
        ["Microsoft.Storage/storageAccounts/table"] = "table",
        ["Microsoft.Sql/servers"] = "sqlServer",
        ["Microsoft.ContainerRegistry/registries"] = "registry",
        ["Microsoft.Web/sites"] = "sites",
        ["Microsoft.App/managedEnvironments"] = "managedEnvironments",
        ["Microsoft.CognitiveServices/accounts"] = "account",
        ["Microsoft.EventHub/namespaces"] = "namespace",
        ["Microsoft.ServiceBus/namespaces"] = "namespace",
        ["Microsoft.Devices/IotHubs"] = "iotHub",
        ["Microsoft.DocumentDB/databaseAccounts"] = "Sql",
        ["Microsoft.Cache/redis"] = "redisCache",
        ["Microsoft.Search/searchServices"] = "searchService"
    };
    
    /// <summary>
    /// Maps Azure resource types to their private DNS zone names
    /// </summary>
    public static readonly Dictionary<string, string> PrivateDnsZones = new()
    {
        ["Microsoft.KeyVault/vaults"] = "privatelink.vaultcore.azure.net",
        ["Microsoft.Storage/storageAccounts"] = "privatelink.blob.core.windows.net",
        ["Microsoft.Sql/servers"] = "privatelink.database.windows.net",
        ["Microsoft.ContainerRegistry/registries"] = "privatelink.azurecr.io",
        ["Microsoft.Web/sites"] = "privatelink.azurewebsites.net",
        ["Microsoft.App/managedEnvironments"] = "privatelink.azurecontainerapps.io",
        ["Microsoft.CognitiveServices/accounts"] = "privatelink.cognitiveservices.azure.com",
        ["Microsoft.EventHub/namespaces"] = "privatelink.servicebus.windows.net",
        ["Microsoft.ServiceBus/namespaces"] = "privatelink.servicebus.windows.net",
        ["Microsoft.DocumentDB/databaseAccounts"] = "privatelink.documents.azure.com",
        ["Microsoft.Cache/redis"] = "privatelink.redis.cache.windows.net"
    };
    
    /// <summary>
    /// Maps Azure resource types to their default log categories for diagnostics
    /// </summary>
    public static readonly Dictionary<string, List<string>> DefaultLogCategories = new()
    {
        ["Microsoft.KeyVault/vaults"] = new() { "AuditEvent", "AzurePolicyEvaluationDetails" },
        ["Microsoft.Storage/storageAccounts"] = new() { "StorageRead", "StorageWrite", "StorageDelete" },
        ["Microsoft.Sql/servers/databases"] = new() { "SQLSecurityAuditEvents", "QueryStoreRuntimeStatistics" },
        ["Microsoft.ContainerRegistry/registries"] = new() { "ContainerRegistryRepositoryEvents", "ContainerRegistryLoginEvents" },
        ["Microsoft.Web/sites"] = new() { "AppServiceHTTPLogs", "AppServiceConsoleLogs", "AppServiceAppLogs" },
        ["Microsoft.App/containerApps"] = new() { "ContainerAppConsoleLogs", "ContainerAppSystemLogs" },
        ["Microsoft.ContainerService/managedClusters"] = new() { "kube-apiserver", "kube-audit", "kube-controller-manager", "kube-scheduler", "cluster-autoscaler" }
    };
    
    /// <summary>
    /// Maps Azure resource types to supported cross-cutting capabilities
    /// </summary>
    public static readonly Dictionary<string, List<CrossCuttingType>> SupportedCapabilities = new()
    {
        ["Microsoft.KeyVault/vaults"] = new() 
        { 
            CrossCuttingType.PrivateEndpoint, 
            CrossCuttingType.DiagnosticSettings, 
            CrossCuttingType.RBACAssignment,
            CrossCuttingType.PrivateDNSZone 
        },
        ["Microsoft.Storage/storageAccounts"] = new() 
        { 
            CrossCuttingType.PrivateEndpoint, 
            CrossCuttingType.DiagnosticSettings, 
            CrossCuttingType.RBACAssignment,
            CrossCuttingType.NetworkSecurityGroup,
            CrossCuttingType.PrivateDNSZone 
        },
        ["Microsoft.Sql/servers"] = new() 
        { 
            CrossCuttingType.PrivateEndpoint, 
            CrossCuttingType.DiagnosticSettings,
            CrossCuttingType.PrivateDNSZone 
        },
        ["Microsoft.ContainerRegistry/registries"] = new() 
        { 
            CrossCuttingType.PrivateEndpoint, 
            CrossCuttingType.DiagnosticSettings, 
            CrossCuttingType.RBACAssignment,
            CrossCuttingType.PrivateDNSZone 
        },
        ["Microsoft.Web/sites"] = new() 
        { 
            CrossCuttingType.PrivateEndpoint, 
            CrossCuttingType.DiagnosticSettings, 
            CrossCuttingType.ManagedIdentity,
            CrossCuttingType.PrivateDNSZone 
        },
        ["Microsoft.App/containerApps"] = new() 
        { 
            CrossCuttingType.DiagnosticSettings, 
            CrossCuttingType.ManagedIdentity 
        },
        ["Microsoft.ContainerService/managedClusters"] = new() 
        { 
            CrossCuttingType.DiagnosticSettings, 
            CrossCuttingType.ManagedIdentity,
            CrossCuttingType.RBACAssignment,
            CrossCuttingType.NetworkSecurityGroup 
        },
        ["Microsoft.Network/virtualNetworks"] = new() 
        { 
            CrossCuttingType.DiagnosticSettings, 
            CrossCuttingType.NetworkSecurityGroup 
        },
        ["Microsoft.ManagedIdentity/userAssignedIdentities"] = new() 
        { 
            CrossCuttingType.RBACAssignment 
        }
    };
    
    /// <summary>
    /// Maps built-in role names to their Azure role definition IDs
    /// </summary>
    public static readonly Dictionary<string, string> BuiltInRoleIds = new()
    {
        // Key Vault roles
        ["Key Vault Administrator"] = "00482a5a-887f-4fb3-b363-3b7fe8e74483",
        ["Key Vault Secrets Officer"] = "b86a8fe4-44ce-4948-aee5-eccb2c155cd7",
        ["Key Vault Secrets User"] = "4633458b-17de-408a-b874-0445c86b69e6",
        ["Key Vault Crypto Officer"] = "14b46e9e-c2b7-41b4-b07b-48a6ebf60603",
        ["Key Vault Certificates Officer"] = "a4417e6f-fecd-4de8-b567-7b0420556985",
        
        // Storage roles
        ["Storage Blob Data Owner"] = "b7e6dc6d-f1e8-4753-8033-0f276bb0955b",
        ["Storage Blob Data Contributor"] = "ba92f5b4-2d11-453d-a403-e96b0029c9fe",
        ["Storage Blob Data Reader"] = "2a2b9908-6ea1-4ae2-8e65-a410df84e7d1",
        ["Storage Queue Data Contributor"] = "974c5e8b-45b9-4653-ba55-5f855dd0fb88",
        
        // Container Registry roles
        ["AcrPull"] = "7f951dda-4ed3-4680-a7ca-43fe172d538d",
        ["AcrPush"] = "8311e382-0749-4cb8-b61a-304f252e45ec",
        ["AcrDelete"] = "c2f4ef07-c644-48eb-af81-4b1b4947fb11",
        
        // AKS roles
        ["Azure Kubernetes Service RBAC Admin"] = "3498e952-d568-435e-9b2c-8d77e338d7f7",
        ["Azure Kubernetes Service RBAC Cluster Admin"] = "b1ff04bb-8a4e-4dc4-8eb5-8693973ce19b",
        ["Azure Kubernetes Service Cluster User Role"] = "4abbcc35-e782-43d8-92c5-2d3f1bd2253f",
        
        // General roles
        ["Contributor"] = "b24988ac-6180-42a0-ab88-20f7382dd24c",
        ["Reader"] = "acdd72a7-3385-48ef-bd42-f606fba81ae7",
        ["Owner"] = "8e3af657-a8ff-443c-a75c-2fe8c4bcb635"
    };
    
    /// <summary>
    /// Get the private endpoint group ID for a resource type
    /// </summary>
    public static string? GetGroupId(string resourceType)
    {
        return PrivateEndpointGroupIds.TryGetValue(resourceType, out var groupId) ? groupId : null;
    }
    
    /// <summary>
    /// Get the private DNS zone name for a resource type
    /// </summary>
    public static string? GetDnsZoneName(string resourceType)
    {
        return PrivateDnsZones.TryGetValue(resourceType, out var zone) ? zone : null;
    }
    
    /// <summary>
    /// Get default log categories for a resource type
    /// </summary>
    public static List<string> GetDefaultLogCategories(string resourceType)
    {
        return DefaultLogCategories.TryGetValue(resourceType, out var categories) 
            ? categories 
            : new List<string> { "allLogs" };
    }
    
    /// <summary>
    /// Check if a resource type supports a cross-cutting capability
    /// </summary>
    public static bool SupportsCapability(string resourceType, CrossCuttingType capability)
    {
        return SupportedCapabilities.TryGetValue(resourceType, out var capabilities) 
            && capabilities.Contains(capability);
    }
    
    /// <summary>
    /// Get the role definition ID from a role name or ID
    /// </summary>
    public static string GetRoleDefinitionId(string roleNameOrId)
    {
        // If it's already a GUID, return it
        if (Guid.TryParse(roleNameOrId, out _))
            return roleNameOrId;
            
        // Try to look up the built-in role
        return BuiltInRoleIds.TryGetValue(roleNameOrId, out var roleId) 
            ? roleId 
            : roleNameOrId;
    }
}
