namespace Platform.Engineering.Copilot.Agents.Discovery.Tools;

/// <summary>
/// Shared formatting helpers for Discovery tools to produce consistent, pretty output.
/// </summary>
public static class DiscoveryFormatHelpers
{
    /// <summary>
    /// Get an emoji icon for a resource type
    /// </summary>
    public static string GetResourceTypeIcon(string resourceType)
    {
        return resourceType.ToLowerInvariant() switch
        {
            var t when t.Contains("virtualmachine") => "🖥️",
            var t when t.Contains("storage") => "💾",
            var t when t.Contains("keyvault") => "🔐",
            var t when t.Contains("sql") || t.Contains("database") => "🗄️",
            var t when t.Contains("containerinstance") || t.Contains("containergroup") => "📦",
            var t when t.Contains("containerregistry") => "🐳",
            var t when t.Contains("kubernetes") || t.Contains("managedcluster") => "☸️",
            var t when t.Contains("network") && t.Contains("watcher") => "👁️",
            var t when t.Contains("virtualnetwork") => "🌐",
            var t when t.Contains("networksecuritygroup") => "🛡️",
            var t when t.Contains("publicip") => "🔗",
            var t when t.Contains("loadbalancer") => "⚖️",
            var t when t.Contains("cognitiveservice") || t.Contains("openai") => "🤖",
            var t when t.Contains("machinelearning") => "🧠",
            var t when t.Contains("webapp") || t.Contains("sites") => "🌍",
            var t when t.Contains("serverfarm") => "🏭",
            var t when t.Contains("function") => "⚡",
            var t when t.Contains("insight") || t.Contains("monitor") => "📈",
            var t when t.Contains("loganalytics") || t.Contains("operationalinsights") => "📊",
            var t when t.Contains("redis") => "🔴",
            var t when t.Contains("cosmosdb") => "🌌",
            var t when t.Contains("servicefabric") => "🧵",
            var t when t.Contains("eventhub") => "📡",
            var t when t.Contains("servicebus") => "🚌",
            var t when t.Contains("logic") => "🔀",
            var t when t.Contains("apimanagement") => "🚪",
            var t when t.Contains("cdn") => "🌐",
            var t when t.Contains("frontdoor") => "🚪",
            var t when t.Contains("firewall") => "🔥",
            var t when t.Contains("bastion") => "🏰",
            var t when t.Contains("vpn") || t.Contains("gateway") => "🚧",
            var t when t.Contains("disk") => "💿",
            var t when t.Contains("snapshot") => "📸",
            var t when t.Contains("backup") => "💾",
            var t when t.Contains("recovery") => "🔄",
            var t when t.Contains("automation") => "⚙️",
            var t when t.Contains("devops") || t.Contains("devtest") => "🔧",
            _ => "📌"
        };
    }

    /// <summary>
    /// Get a friendly name for an Azure resource type
    /// </summary>
    public static string GetFriendlyTypeName(string resourceType)
    {
        return resourceType.ToLowerInvariant() switch
        {
            "microsoft.compute/virtualmachines" => "Virtual Machines",
            "microsoft.compute/disks" => "Managed Disks",
            "microsoft.compute/snapshots" => "Snapshots",
            "microsoft.storage/storageaccounts" => "Storage Accounts",
            "microsoft.keyvault/vaults" => "Key Vaults",
            "microsoft.sql/servers" => "SQL Servers",
            "microsoft.sql/servers/databases" => "SQL Databases",
            "microsoft.containerinstance/containergroups" => "Container Instances",
            "microsoft.containerregistry/registries" => "Container Registries",
            "microsoft.containerservice/managedclusters" => "AKS Clusters",
            "microsoft.network/virtualnetworks" => "Virtual Networks",
            "microsoft.network/networkwatchers" => "Network Watchers",
            "microsoft.network/networksecuritygroups" => "NSGs",
            "microsoft.network/publicipaddresses" => "Public IPs",
            "microsoft.network/loadbalancers" => "Load Balancers",
            "microsoft.network/applicationgateways" => "App Gateways",
            "microsoft.network/bastionhosts" => "Bastion Hosts",
            "microsoft.network/azurefirewalls" => "Firewalls",
            "microsoft.cognitiveservices/accounts" => "Cognitive Services",
            "microsoft.machinelearningservices/workspaces" => "ML Workspaces",
            "microsoft.web/sites" => "Web Apps",
            "microsoft.web/serverfarms" => "App Service Plans",
            "microsoft.insights/components" => "App Insights",
            "microsoft.insights/datacollectionendpoints" => "Data Collection Endpoints",
            "microsoft.insights/datacollectionrules" => "Data Collection Rules",
            "microsoft.operationalinsights/workspaces" => "Log Analytics",
            "microsoft.cache/redis" => "Redis Cache",
            "microsoft.documentdb/databaseaccounts" => "Cosmos DB",
            "microsoft.eventhub/namespaces" => "Event Hubs",
            "microsoft.servicebus/namespaces" => "Service Bus",
            "microsoft.logic/workflows" => "Logic Apps",
            "microsoft.apimanagement/service" => "API Management",
            "microsoft.cdn/profiles" => "CDN Profiles",
            "microsoft.recoveryservices/vaults" => "Recovery Vaults",
            "microsoft.automation/automationaccounts" => "Automation Accounts",
            _ => resourceType.Split('/').LastOrDefault() ?? resourceType
        };
    }

    /// <summary>
    /// Get a friendly name for an Azure region
    /// </summary>
    public static string GetFriendlyLocationName(string location)
    {
        return location.ToLowerInvariant() switch
        {
            "eastus" => "🇺🇸 East US",
            "eastus2" => "🇺🇸 East US 2",
            "westus" => "🇺🇸 West US",
            "westus2" => "🇺🇸 West US 2",
            "westus3" => "🇺🇸 West US 3",
            "centralus" => "🇺🇸 Central US",
            "northcentralus" => "🇺🇸 North Central US",
            "southcentralus" => "🇺🇸 South Central US",
            "usgovvirginia" => "🏛️ US Gov Virginia",
            "usgovarizona" => "🏛️ US Gov Arizona",
            "usgovtexas" => "🏛️ US Gov Texas",
            "usdodeast" => "🎖️ DoD East",
            "usdodcentral" => "🎖️ DoD Central",
            "westeurope" => "🇪🇺 West Europe",
            "northeurope" => "🇪🇺 North Europe",
            "uksouth" => "🇬🇧 UK South",
            "ukwest" => "🇬🇧 UK West",
            "germanywestcentral" => "🇩🇪 Germany West Central",
            "francecentral" => "🇫🇷 France Central",
            "switzerlandnorth" => "🇨🇭 Switzerland North",
            "norwayeast" => "🇳🇴 Norway East",
            "swedencentral" => "🇸🇪 Sweden Central",
            "australiaeast" => "🇦🇺 Australia East",
            "australiasoutheast" => "🇦🇺 Australia Southeast",
            "japaneast" => "🇯🇵 Japan East",
            "japanwest" => "🇯🇵 Japan West",
            "koreacentral" => "🇰🇷 Korea Central",
            "southeastasia" => "🌏 Southeast Asia",
            "eastasia" => "🌏 East Asia",
            "centralindia" => "🇮🇳 Central India",
            "brazilsouth" => "🇧🇷 Brazil South",
            "canadacentral" => "🇨🇦 Canada Central",
            "canadaeast" => "🇨🇦 Canada East",
            _ => $"🌍 {location}"
        };
    }

    /// <summary>
    /// Get health status icon
    /// </summary>
    public static string GetHealthIcon(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "available" or "healthy" or "succeeded" => "✅",
            "degraded" or "warning" => "⚠️",
            "unavailable" or "unhealthy" or "failed" => "❌",
            "unknown" => "❓",
            _ => "🔵"
        };
    }

    /// <summary>
    /// Get dependency relationship icon
    /// </summary>
    public static string GetDependencyIcon(string relationship)
    {
        return relationship.ToLowerInvariant() switch
        {
            "downstream" => "⬇️",
            "upstream" => "⬆️",
            "both" => "↕️",
            _ => "🔗"
        };
    }

    /// <summary>
    /// Get subscription state icon
    /// </summary>
    public static string GetSubscriptionStateIcon(string state)
    {
        return state.ToLowerInvariant() switch
        {
            "enabled" => "✅",
            "disabled" => "⛔",
            "warned" => "⚠️",
            "pastdue" => "📅",
            "deleted" => "🗑️",
            _ => "🔵"
        };
    }

    /// <summary>
    /// Format a count with proper singular/plural
    /// </summary>
    public static string Pluralize(int count, string singular, string? plural = null)
    {
        plural ??= singular + "s";
        return count == 1 ? $"{count} {singular}" : $"{count} {plural}";
    }

    /// <summary>
    /// Format a percentage with icon
    /// </summary>
    public static string FormatPercentage(double percentage)
    {
        var icon = percentage switch
        {
            >= 90 => "🟢",
            >= 70 => "🟡",
            >= 50 => "🟠",
            _ => "🔴"
        };
        return $"{icon} {percentage:F1}%";
    }
}
