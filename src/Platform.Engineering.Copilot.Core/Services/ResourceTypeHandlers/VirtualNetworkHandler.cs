using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;

/// <summary>
/// Handler for Azure Virtual Network resources
/// </summary>
public class VirtualNetworkHandler : IResourceTypeHandler
{
    public string ResourceType => "microsoft.network/virtualnetworks";

    public List<string> ExtendedProperties => new()
    {
        "properties.addressSpace.addressPrefixes",
        "properties.subnets",
        "properties.dhcpOptions.dnsServers",
        "properties.enableDdosProtection",
        "properties.enableVmProtection",
        "properties.virtualNetworkPeerings",
        "properties.provisioningState",
        "properties.flowTimeoutInMinutes"
    };

    public Dictionary<string, object> ParseExtendedProperties(AzureResource resource)
    {
        var result = new Dictionary<string, object>();

        try
        {
            // Address Space
            var addressPrefixes = GetAddressPrefixes(resource.Properties);
            result["addressSpace"] = addressPrefixes.Any() ? string.Join(", ", addressPrefixes) : "Not configured";

            // Subnets
            var subnets = GetSubnets(resource.Properties);
            result["subnetCount"] = subnets.Count;
            result["subnets"] = subnets;

            // DNS Servers
            var dnsServers = GetDnsServers(resource.Properties);
            result["dnsServers"] = dnsServers.Any() ? string.Join(", ", dnsServers) : "Azure Default";

            // DDoS Protection
            result["ddosProtection"] = GetPropertyValue<bool>(resource.Properties, "enableDdosProtection", false);

            // VM Protection
            result["vmProtection"] = GetPropertyValue<bool>(resource.Properties, "enableVmProtection", false);

            // Peerings
            var peerings = GetPeerings(resource.Properties);
            result["peeringCount"] = peerings.Count;
            result["peerings"] = peerings;

            // Provisioning State
            result["provisioningState"] = resource.ProvisioningState ?? "Unknown";

            // Flow Timeout
            var flowTimeout = GetPropertyValue<int?>(resource.Properties, "flowTimeoutInMinutes", null);
            result["flowTimeoutMinutes"] = flowTimeout?.ToString() ?? "Default";
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
            "addressspace" => $"Address Space: {propertyValue}",
            "subnetcount" => $"Subnets: {propertyValue}",
            "dnsservers" => $"DNS Servers: {propertyValue}",
            "ddosprotection" => (bool)propertyValue ? "✅ DDoS Protection enabled" : "⚠️ DDoS Protection disabled",
            "vmprotection" => (bool)propertyValue ? "✅ VM Protection enabled" : "VM Protection: Standard",
            "peeringcount" => $"VNet Peerings: {propertyValue}",
            "provisioningstate" => propertyValue?.ToString() == "Succeeded" ? "✅ Provisioned successfully" : $"⚠️ State: {propertyValue}",
            "flowtimeoutminutes" => $"Flow Timeout: {propertyValue} minutes",
            _ => $"{propertyName}: {propertyValue}"
        };
    }

    private List<string> GetAddressPrefixes(Dictionary<string, object>? props)
    {
        var prefixes = new List<string>();
        if (props == null) return prefixes;

        if (props.TryGetValue("addressSpace", out var addressSpace))
        {
            var addrPrefixes = GetNestedValue(addressSpace, "addressPrefixes");
            if (addrPrefixes is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonArray.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        prefixes.Add(item.GetString()!);
                    }
                }
            }
        }
        return prefixes;
    }

    private List<Dictionary<string, string>> GetSubnets(Dictionary<string, object>? props)
    {
        var subnets = new List<Dictionary<string, string>>();
        if (props == null) return subnets;

        if (props.TryGetValue("subnets", out var subnetsObj))
        {
            if (subnetsObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var subnet in jsonArray.EnumerateArray())
                {
                    var subnetInfo = new Dictionary<string, string>();
                    
                    if (subnet.TryGetProperty("name", out var name))
                        subnetInfo["name"] = name.GetString() ?? "Unknown";
                    
                    if (subnet.TryGetProperty("properties", out var props2) &&
                        props2.TryGetProperty("addressPrefix", out var prefix))
                        subnetInfo["addressPrefix"] = prefix.GetString() ?? "Unknown";

                    if (subnetInfo.Any())
                        subnets.Add(subnetInfo);
                }
            }
        }
        return subnets;
    }

    private List<string> GetDnsServers(Dictionary<string, object>? props)
    {
        var servers = new List<string>();
        if (props == null) return servers;

        if (props.TryGetValue("dhcpOptions", out var dhcpOptions))
        {
            var dnsServers = GetNestedValue(dhcpOptions, "dnsServers");
            if (dnsServers is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in jsonArray.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        servers.Add(item.GetString()!);
                    }
                }
            }
        }
        return servers;
    }

    private List<Dictionary<string, string>> GetPeerings(Dictionary<string, object>? props)
    {
        var peerings = new List<Dictionary<string, string>>();
        if (props == null) return peerings;

        if (props.TryGetValue("virtualNetworkPeerings", out var peeringsObj))
        {
            if (peeringsObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var peering in jsonArray.EnumerateArray())
                {
                    var peeringInfo = new Dictionary<string, string>();
                    
                    if (peering.TryGetProperty("name", out var name))
                        peeringInfo["name"] = name.GetString() ?? "Unknown";
                    
                    if (peering.TryGetProperty("properties", out var props2))
                    {
                        if (props2.TryGetProperty("peeringState", out var state))
                            peeringInfo["state"] = state.GetString() ?? "Unknown";
                        
                        if (props2.TryGetProperty("remoteVirtualNetwork", out var remote) &&
                            remote.TryGetProperty("id", out var remoteId))
                        {
                            var id = remoteId.GetString() ?? "";
                            peeringInfo["remoteVNet"] = id.Contains("/") ? id.Split('/').Last() : id;
                        }
                    }

                    if (peeringInfo.Any())
                        peerings.Add(peeringInfo);
                }
            }
        }
        return peerings;
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
