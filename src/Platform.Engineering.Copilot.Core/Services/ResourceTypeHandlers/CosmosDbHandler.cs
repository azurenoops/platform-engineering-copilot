using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;

/// <summary>
/// Handler for Azure Cosmos DB resources
/// </summary>
public class CosmosDbHandler : IResourceTypeHandler
{
    public string ResourceType => "microsoft.documentdb/databaseaccounts";

    public List<string> ExtendedProperties => new()
    {
        "properties.databaseAccountOfferType",
        "properties.consistencyPolicy.defaultConsistencyLevel",
        "properties.locations",
        "properties.enableAutomaticFailover",
        "properties.enableMultipleWriteLocations",
        "properties.isVirtualNetworkFilterEnabled",
        "properties.ipRules",
        "properties.publicNetworkAccess",
        "properties.enableFreeTier",
        "properties.backupPolicy.type",
        "properties.capabilities",
        "properties.disableKeyBasedMetadataWriteAccess",
        "properties.enableAnalyticalStorage",
        "properties.provisioningState"
    };

    public Dictionary<string, object> ParseExtendedProperties(AzureResource resource)
    {
        var result = new Dictionary<string, object>();

        try
        {
            // Database Offer Type
            result["offerType"] = GetPropertyValue<string>(resource.Properties, "databaseAccountOfferType", "Standard");

            // Consistency Level
            result["consistencyLevel"] = GetPropertyValue<string>(resource.Properties, "consistencyPolicy.defaultConsistencyLevel", "Session");

            // Locations/Regions
            var locations = GetLocations(resource.Properties);
            result["regionCount"] = locations.Count;
            result["regions"] = locations;
            result["isMultiRegion"] = locations.Count > 1;

            // Automatic Failover
            result["automaticFailover"] = GetPropertyValue<bool>(resource.Properties, "enableAutomaticFailover", false);

            // Multi-Write (Multi-Master)
            result["multiWriteEnabled"] = GetPropertyValue<bool>(resource.Properties, "enableMultipleWriteLocations", false);

            // Network Security
            result["vnetFilterEnabled"] = GetPropertyValue<bool>(resource.Properties, "isVirtualNetworkFilterEnabled", false);
            result["publicNetworkAccess"] = GetPropertyValue<string>(resource.Properties, "publicNetworkAccess", "Enabled");

            // IP Rules
            var ipRules = GetIpRules(resource.Properties);
            result["ipRuleCount"] = ipRules.Count;
            result["hasIpRestrictions"] = ipRules.Count > 0;

            // Free Tier
            result["freeTier"] = GetPropertyValue<bool>(resource.Properties, "enableFreeTier", false);

            // Backup Policy
            var backupType = GetBackupType(resource.Properties);
            result["backupType"] = backupType;

            // API/Capabilities
            var capabilities = GetCapabilities(resource.Properties);
            result["apiType"] = DetermineApiType(capabilities, resource.Kind);
            result["capabilities"] = capabilities;

            // Security
            result["disableKeyBasedMetadataWrite"] = GetPropertyValue<bool>(resource.Properties, "disableKeyBasedMetadataWriteAccess", false);

            // Analytical Storage
            result["analyticalStorage"] = GetPropertyValue<bool>(resource.Properties, "enableAnalyticalStorage", false);

            // Provisioning State
            result["provisioningState"] = resource.ProvisioningState ?? "Unknown";
        }
        catch (Exception ex)
        {
            result["error"] = $"Failed to parse properties: {ex.Message}";
        }

        return result;
    }

    public string GetPropertyDescription(string propertyName, object propertyValue)
    {
        return propertyName.ToLowerInvariant() switch
        {
            "offertype" => $"Offer Type: {propertyValue}",
            "consistencylevel" => $"Consistency: {propertyValue}",
            "regioncount" => $"Regions: {propertyValue}",
            "ismultiregion" => (bool)propertyValue ? "✅ Multi-region deployment" : "Single region",
            "automaticfailover" => (bool)propertyValue ? "✅ Automatic failover enabled" : "⚠️ Manual failover only",
            "multiwriteenabled" => (bool)propertyValue ? "✅ Multi-write (multi-master) enabled" : "Single write region",
            "vnetfilterenabled" => (bool)propertyValue ? "✅ VNet filter enabled" : "⚠️ VNet filter disabled",
            "publicnetworkaccess" => propertyValue?.ToString() == "Disabled" ? "✅ Public access disabled" : "⚠️ Public network access enabled",
            "hasiprestrictions" => (bool)propertyValue ? "✅ IP restrictions configured" : "⚠️ No IP restrictions",
            "freetier" => (bool)propertyValue ? "Free tier account" : "Standard billing",
            "backuptype" => $"Backup: {propertyValue}",
            "apitype" => $"API: {propertyValue}",
            "disablekeybasedmetadatawrite" => (bool)propertyValue ? "✅ Key-based metadata write disabled" : "⚠️ Key-based metadata write allowed",
            "analyticalstorage" => (bool)propertyValue ? "✅ Analytical storage enabled" : "Analytical storage disabled",
            "provisioningstate" => propertyValue?.ToString() == "Succeeded" ? "✅ Provisioned successfully" : $"⚠️ State: {propertyValue}",
            _ => $"{propertyName}: {propertyValue}"
        };
    }

    private List<Dictionary<string, object>> GetLocations(Dictionary<string, object>? props)
    {
        var locations = new List<Dictionary<string, object>>();
        if (props == null) return locations;

        if (props.TryGetValue("locations", out var locationsObj))
        {
            if (locationsObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var loc in jsonArray.EnumerateArray())
                {
                    var info = new Dictionary<string, object>();

                    if (loc.TryGetProperty("locationName", out var name))
                        info["location"] = name.GetString() ?? "Unknown";

                    if (loc.TryGetProperty("failoverPriority", out var priority))
                        info["failoverPriority"] = priority.GetInt32();

                    if (loc.TryGetProperty("isZoneRedundant", out var zoneRedundant))
                        info["zoneRedundant"] = zoneRedundant.GetBoolean();

                    if (info.Any())
                        locations.Add(info);
                }
            }
        }

        return locations.OrderBy(l => l.GetValueOrDefault("failoverPriority")).ToList();
    }

    private List<string> GetIpRules(Dictionary<string, object>? props)
    {
        var rules = new List<string>();
        if (props == null) return rules;

        if (props.TryGetValue("ipRules", out var rulesObj))
        {
            if (rulesObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in jsonArray.EnumerateArray())
                {
                    if (rule.TryGetProperty("ipAddressOrRange", out var ip))
                    {
                        rules.Add(ip.GetString() ?? "");
                    }
                }
            }
        }
        return rules;
    }

    private string GetBackupType(Dictionary<string, object>? props)
    {
        if (props == null) return "Unknown";

        if (props.TryGetValue("backupPolicy", out var backupObj))
        {
            if (backupObj is JsonElement json && json.ValueKind == JsonValueKind.Object)
            {
                if (json.TryGetProperty("type", out var backupType))
                {
                    return backupType.GetString() ?? "Periodic";
                }
            }
        }
        return "Periodic";
    }

    private List<string> GetCapabilities(Dictionary<string, object>? props)
    {
        var capabilities = new List<string>();
        if (props == null) return capabilities;

        if (props.TryGetValue("capabilities", out var capsObj))
        {
            if (capsObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var cap in jsonArray.EnumerateArray())
                {
                    if (cap.TryGetProperty("name", out var name))
                    {
                        capabilities.Add(name.GetString() ?? "");
                    }
                }
            }
        }
        return capabilities;
    }

    private string DetermineApiType(List<string> capabilities, string? kind)
    {
        // Check capabilities first
        if (capabilities.Any(c => c.Contains("EnableMongo", StringComparison.OrdinalIgnoreCase)))
            return "MongoDB";
        if (capabilities.Any(c => c.Contains("EnableCassandra", StringComparison.OrdinalIgnoreCase)))
            return "Cassandra";
        if (capabilities.Any(c => c.Contains("EnableGremlin", StringComparison.OrdinalIgnoreCase)))
            return "Gremlin (Graph)";
        if (capabilities.Any(c => c.Contains("EnableTable", StringComparison.OrdinalIgnoreCase)))
            return "Table";

        // Check kind
        if (!string.IsNullOrEmpty(kind))
        {
            if (kind.Contains("MongoDB", StringComparison.OrdinalIgnoreCase))
                return "MongoDB";
            if (kind.Contains("GlobalDocumentDB", StringComparison.OrdinalIgnoreCase))
                return "SQL (Core)";
        }

        return "SQL (Core)";
    }

    private T? GetPropertyValue<T>(Dictionary<string, object>? props, string path, T? defaultValue)
    {
        if (props == null) return defaultValue;

        var parts = path.Split('.');
        object? current = props;

        foreach (var part in parts)
        {
            current = GetNestedValue(current, part);
            if (current == null) return defaultValue;
        }

        if (current is T typedValue)
        {
            return typedValue;
        }

        if (current is JsonElement jsonElement)
        {
            return ConvertJsonElement<T>(jsonElement, defaultValue);
        }

        return defaultValue;
    }

    private object? GetNestedValue(object? obj, string key)
    {
        if (obj is Dictionary<string, object> dict && dict.TryGetValue(key, out var value))
        {
            return value;
        }
        if (obj is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
        {
            if (jsonElement.TryGetProperty(key, out var prop))
            {
                return prop;
            }
        }
        return null;
    }

    private T? ConvertJsonElement<T>(JsonElement element, T? defaultValue)
    {
        try
        {
            if (typeof(T) == typeof(bool))
            {
                return element.ValueKind == JsonValueKind.True ? (T)(object)true :
                       element.ValueKind == JsonValueKind.False ? (T)(object)false :
                       defaultValue;
            }
            if (typeof(T) == typeof(string))
            {
                return element.ValueKind == JsonValueKind.String ? (T)(object)element.GetString()! : defaultValue;
            }
            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            {
                return element.ValueKind == JsonValueKind.Number ? (T)(object)element.GetInt32() : defaultValue;
            }
        }
        catch { }
        return defaultValue;
    }
}
