using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;

namespace Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;

public class KeyVaultHandler : IResourceTypeHandler
{
    public string ResourceType => "microsoft.keyvault/vaults";
    public List<string> ExtendedProperties => new() { "properties.sku.name", "properties.enableSoftDelete", "properties.enablePurgeProtection", "properties.enableRbacAuthorization" };
    
    public Dictionary<string, object> ParseExtendedProperties(AzureResource resource)
    {
        return new Dictionary<string, object>
        {
            ["sku"] = resource.Sku ?? "Standard",
            ["softDelete"] = GetProp<bool>(resource.Properties, "enableSoftDelete", false),
            ["purgeProtection"] = GetProp<bool>(resource.Properties, "enablePurgeProtection", false),
            ["rbacAuthorization"] = GetProp<bool>(resource.Properties, "enableRbacAuthorization", false)
        };
    }
    
    public string GetPropertyDescription(string propertyName, object propertyValue) => 
        propertyName.ToLowerInvariant() switch {
            "softdelete" => (bool)propertyValue ? "✅ Soft delete enabled" : "⚠️ Soft delete disabled",
            "purgeprotection" => (bool)propertyValue ? "✅ Purge protection enabled" : "⚠️ Purge protection disabled",
            _ => $"{propertyName}: {propertyValue}"
        };
    
    private T GetProp<T>(Dictionary<string, object>? props, string key, T def) =>
        props?.TryGetValue(key, out var v) == true && v is T typed ? typed : def;
}
