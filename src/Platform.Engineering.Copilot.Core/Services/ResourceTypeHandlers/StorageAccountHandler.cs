using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;

/// <summary>
/// Handler for Azure Storage Account resources
/// </summary>
public class StorageAccountHandler : IResourceTypeHandler
{
    public string ResourceType => "microsoft.storage/storageaccounts";

    public List<string> ExtendedProperties => new()
    {
        "properties.sku.name",
        "properties.sku.tier",
        "properties.accessTier",
        "properties.supportsHttpsTrafficOnly",
        "properties.minimumTlsVersion",
        "properties.allowBlobPublicAccess",
        "properties.isHnsEnabled",
        "properties.encryption.services.blob.enabled",
        "properties.encryption.services.file.enabled",
        "properties.networkAcls.defaultAction",
        "properties.primaryEndpoints.blob"
    };

    public Dictionary<string, object> ParseExtendedProperties(AzureResource resource)
    {
        var result = new Dictionary<string, object>();

        try
        {
            // SKU
            result["sku"] = resource.Sku ?? "Not configured";
            result["tier"] = GetPropertyValue<string>(resource.Properties, "sku.tier", "Standard");

            // Access Tier (Hot/Cool/Archive)
            result["accessTier"] = GetPropertyValue<string>(resource.Properties, "accessTier", "Not configured");

            // HTTPS Only
            result["httpsOnly"] = GetPropertyValue<bool>(resource.Properties, "supportsHttpsTrafficOnly", false);

            // TLS Version
            result["minTlsVersion"] = GetPropertyValue<string>(resource.Properties, "minimumTlsVersion", "TLS1_0");

            // Public Blob Access
            result["allowBlobPublicAccess"] = GetPropertyValue<bool>(resource.Properties, "allowBlobPublicAccess", true);

            // Hierarchical Namespace (Data Lake Gen2)
            result["isDataLakeGen2"] = GetPropertyValue<bool>(resource.Properties, "isHnsEnabled", false);

            // Encryption
            result["blobEncryption"] = GetPropertyValue<bool>(resource.Properties, "encryption.services.blob.enabled", true);
            result["fileEncryption"] = GetPropertyValue<bool>(resource.Properties, "encryption.services.file.enabled", true);

            // Network ACLs
            result["networkDefaultAction"] = GetPropertyValue<string>(resource.Properties, "networkAcls.defaultAction", "Allow");

            // Primary Blob Endpoint
            result["blobEndpoint"] = GetPropertyValue<string>(resource.Properties, "primaryEndpoints.blob", "Not available");
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
            "sku" => $"SKU: {propertyValue}",
            "tier" => $"Performance tier: {propertyValue}",
            "accesstier" => $"Access tier: {propertyValue}",
            "httpsonly" => (bool)propertyValue ? "✅ HTTPS only enforced" : "⚠️ HTTP allowed",
            "mintlsversion" => $"Minimum TLS: {propertyValue}",
            "allowblobpublicaccess" => (bool)propertyValue ? "⚠️ Public blob access allowed" : "✅ Public blob access disabled",
            "isdatalakegen2" => (bool)propertyValue ? "✅ Data Lake Gen2 enabled" : "Standard blob storage",
            "blobencryption" => (bool)propertyValue ? "✅ Blob encryption enabled" : "❌ Blob encryption disabled",
            "fileencryption" => (bool)propertyValue ? "✅ File encryption enabled" : "❌ File encryption disabled",
            "networkdefaultaction" => propertyValue.ToString() == "Deny" ? "✅ Network access restricted" : "⚠️ Network access allowed by default",
            _ => $"{propertyName}: {propertyValue}"
        };
    }

    private T? GetPropertyValue<T>(Dictionary<string, object>? props, string path, T? defaultValue)
    {
        if (props == null) return defaultValue;

        var parts = path.Split('.');
        object? current = props;

        foreach (var part in parts)
        {
            if (current is Dictionary<string, object> dict && dict.TryGetValue(part, out var value))
            {
                current = value;
            }
            else
            {
                return defaultValue;
            }
        }

        if (current is T typedValue)
        {
            return typedValue;
        }

        if (current is JsonElement jsonElement)
        {
            if (typeof(T) == typeof(bool))
            {
                return jsonElement.ValueKind == JsonValueKind.True ? (T)(object)true :
                       jsonElement.ValueKind == JsonValueKind.False ? (T)(object)false :
                       defaultValue;
            }
            if (typeof(T) == typeof(string))
            {
                return jsonElement.ValueKind == JsonValueKind.String ? (T)(object)jsonElement.GetString()! : defaultValue;
            }
        }

        return defaultValue;
    }
}
