using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;

/// <summary>
/// Handler for Azure Kubernetes Service (AKS) resources
/// </summary>
public class AksHandler : IResourceTypeHandler
{
    public string ResourceType => "microsoft.containerservice/managedclusters";

    public List<string> ExtendedProperties => new()
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
    };

    public Dictionary<string, object> ParseExtendedProperties(AzureResource resource)
    {
        var result = new Dictionary<string, object>();

        try
        {
            // Kubernetes Version
            result["kubernetesVersion"] = GetPropertyValue<string>(resource.Properties, "kubernetesVersion", "Unknown");

            // Node Resource Group
            result["nodeResourceGroup"] = GetPropertyValue<string>(resource.Properties, "nodeResourceGroup", "Unknown");

            // RBAC
            result["rbacEnabled"] = GetPropertyValue<bool>(resource.Properties, "enableRBAC", false);

            // Network Plugin
            result["networkPlugin"] = GetPropertyValue<string>(resource.Properties, "networkProfile.networkPlugin", "kubenet");

            // Service CIDR
            result["serviceCidr"] = GetPropertyValue<string>(resource.Properties, "networkProfile.serviceCidr", "Not configured");

            // DNS Service IP
            result["dnsServiceIP"] = GetPropertyValue<string>(resource.Properties, "networkProfile.dnsServiceIP", "Not configured");

            // Azure Policy Addon
            result["azurePolicyEnabled"] = GetPropertyValue<bool>(resource.Properties, "addonProfiles.azurepolicy.enabled", false);

            // Monitoring Addon (Container Insights)
            result["monitoringEnabled"] = GetPropertyValue<bool>(resource.Properties, "addonProfiles.omsagent.enabled", false);

            // Private Cluster
            result["privateCluster"] = GetPropertyValue<bool>(resource.Properties, "apiServerAccessProfile.enablePrivateCluster", false);
            result["privateFQDN"] = GetPropertyValue<string>(resource.Properties, "privateFQDN", null);
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
            "kubernetesversion" => $"Kubernetes version: {propertyValue}",
            "noderesourcegroup" => $"Node resource group: {propertyValue}",
            "rbacenabled" => (bool)propertyValue ? "✅ RBAC enabled" : "⚠️ RBAC disabled",
            "networkplugin" => $"Network plugin: {propertyValue}",
            "servicec idr" => $"Service CIDR: {propertyValue}",
            "dnsserviceip" => $"DNS service IP: {propertyValue}",
            "azurepolicyenabled" => (bool)propertyValue ? "✅ Azure Policy enabled" : "Azure Policy disabled",
            "monitoringenabled" => (bool)propertyValue ? "✅ Container Insights enabled" : "⚠️ Monitoring disabled",
            "privatecluster" => (bool)propertyValue ? "✅ Private cluster" : "Public cluster",
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
