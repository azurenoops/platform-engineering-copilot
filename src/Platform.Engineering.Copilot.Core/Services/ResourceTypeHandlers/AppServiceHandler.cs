using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;

/// <summary>
/// Handler for Azure App Service (Web Apps) resources
/// </summary>
public class AppServiceHandler : IResourceTypeHandler
{
    public string ResourceType => "microsoft.web/sites";

    public List<string> ExtendedProperties => new()
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
        "properties.enabled",
        "identity.type"
    };

    public Dictionary<string, object> ParseExtendedProperties(AzureResource resource)
    {
        var result = new Dictionary<string, object>();

        try
        {
            // SKU (pricing tier)
            result["sku"] = resource.Sku ?? "Not configured";

            // HTTPS enforcement
            result["httpsOnly"] = GetPropertyValue<bool>(resource.Properties, "httpsOnly", false);

            // Runtime stack
            var linuxRuntime = GetPropertyValue<string>(resource.Properties, "siteConfig.linuxFxVersion", null);
            var windowsRuntime = GetPropertyValue<string>(resource.Properties, "siteConfig.windowsFxVersion", null);
            result["runtime"] = linuxRuntime ?? windowsRuntime ?? "Not configured";

            // Always On
            result["alwaysOn"] = GetPropertyValue<bool>(resource.Properties, "siteConfig.alwaysOn", false);

            // TLS Version
            result["minTlsVersion"] = GetPropertyValue<string>(resource.Properties, "siteConfig.minTlsVersion", "1.2");

            // Client Certificate
            result["clientCertEnabled"] = GetPropertyValue<bool>(resource.Properties, "clientCertEnabled", false);

            // Default Host Name
            result["defaultHostName"] = GetPropertyValue<string>(resource.Properties, "defaultHostName", "Unknown");

            // State
            result["state"] = GetPropertyValue<string>(resource.Properties, "state", "Unknown");

            // Enabled
            result["enabled"] = GetPropertyValue<bool>(resource.Properties, "enabled", true);

            // Managed Identity
            var identityType = GetNestedPropertyValue(resource.Properties, "identity", "type");
            result["identity"] = identityType ?? "None";
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
            "sku" => $"Pricing tier: {propertyValue}",
            "httpsonly" => (bool)propertyValue ? "✅ HTTPS enforced" : "⚠️ HTTP allowed",
            "runtime" => $"Runtime stack: {propertyValue}",
            "alwayson" => (bool)propertyValue ? "✅ Always On enabled" : "⚠️ Always On disabled",
            "mintlsversion" => $"Minimum TLS version: {propertyValue}",
            "clientcertenabled" => (bool)propertyValue ? "✅ Client certificates required" : "Client certificates not required",
            "state" => $"App state: {propertyValue}",
            "enabled" => (bool)propertyValue ? "✅ Enabled" : "❌ Disabled",
            "identity" => $"Managed identity: {propertyValue}",
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

    private string? GetNestedPropertyValue(Dictionary<string, object>? props, string key, string nestedKey)
    {
        if (props == null || !props.TryGetValue(key, out var value)) return null;

        if (value is Dictionary<string, object> nestedDict && nestedDict.TryGetValue(nestedKey, out var nestedValue))
        {
            return nestedValue?.ToString();
        }

        return null;
    }
}
