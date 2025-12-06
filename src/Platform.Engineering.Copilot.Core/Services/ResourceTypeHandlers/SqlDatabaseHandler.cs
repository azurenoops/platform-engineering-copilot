using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;

namespace Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;

public class SqlDatabaseHandler : IResourceTypeHandler
{
    public string ResourceType => "microsoft.sql/servers/databases";
    public List<string> ExtendedProperties => new() { "properties.sku.name", "properties.status", "properties.maxSizeBytes" };
    
    public Dictionary<string, object> ParseExtendedProperties(AzureResource resource)
    {
        return new Dictionary<string, object>
        {
            ["sku"] = resource.Sku ?? "Not configured",
            ["status"] = GetProp(resource.Properties, "status", "Unknown"),
            ["maxSizeBytes"] = GetProp(resource.Properties, "maxSizeBytes", "Unknown")
        };
    }
    
    public string GetPropertyDescription(string propertyName, object propertyValue) => 
        $"{propertyName}: {propertyValue}";
    
    private string GetProp(Dictionary<string, object>? props, string key, string def) =>
        props?.TryGetValue(key, out var v) == true ? v?.ToString() ?? def : def;
}
