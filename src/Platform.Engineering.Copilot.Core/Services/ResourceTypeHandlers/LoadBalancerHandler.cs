using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;

/// <summary>
/// Handler for Azure Load Balancer resources
/// </summary>
public class LoadBalancerHandler : IResourceTypeHandler
{
    public string ResourceType => "microsoft.network/loadbalancers";

    public List<string> ExtendedProperties => new()
    {
        "sku.name",
        "sku.tier",
        "properties.frontendIPConfigurations",
        "properties.backendAddressPools",
        "properties.loadBalancingRules",
        "properties.probes",
        "properties.inboundNatRules",
        "properties.outboundRules",
        "properties.provisioningState"
    };

    public Dictionary<string, object> ParseExtendedProperties(AzureResource resource)
    {
        var result = new Dictionary<string, object>();

        try
        {
            // SKU
            result["sku"] = resource.Sku ?? "Basic";
            result["tier"] = GetPropertyValue<string>(resource.Properties, "sku.tier", "Regional");

            // Frontend IP Configurations
            var frontends = GetFrontendConfigurations(resource.Properties);
            result["frontendCount"] = frontends.Count;
            result["frontendConfigs"] = frontends;
            result["hasPublicIP"] = frontends.Any(f => f.GetValueOrDefault("type")?.ToString() == "Public");
            result["hasPrivateIP"] = frontends.Any(f => f.GetValueOrDefault("type")?.ToString() == "Private");

            // Backend Pools
            var backends = GetBackendPools(resource.Properties);
            result["backendPoolCount"] = backends.Count;
            result["backendPools"] = backends;
            result["totalBackendInstances"] = backends.Sum(b => 
                int.TryParse(b.GetValueOrDefault("instanceCount")?.ToString(), out var count) ? count : 0);

            // Load Balancing Rules
            var rules = GetLoadBalancingRules(resource.Properties);
            result["loadBalancingRuleCount"] = rules.Count;
            result["loadBalancingRules"] = rules;

            // Health Probes
            var probes = GetHealthProbes(resource.Properties);
            result["healthProbeCount"] = probes.Count;
            result["healthProbes"] = probes;

            // NAT Rules
            var natRules = GetNatRules(resource.Properties);
            result["natRuleCount"] = natRules.Count;

            // Outbound Rules
            var outboundRules = GetOutboundRules(resource.Properties);
            result["outboundRuleCount"] = outboundRules.Count;

            // Provisioning State
            result["provisioningState"] = resource.ProvisioningState ?? "Unknown";

            // Check if LB is in use
            result["isInUse"] = backends.Any(b => 
                int.TryParse(b.GetValueOrDefault("instanceCount")?.ToString(), out var count) && count > 0);
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
            "sku" => propertyValue?.ToString() == "Standard" ? "✅ Standard SKU" : $"⚠️ {propertyValue} SKU (consider Standard for production)",
            "tier" => $"Tier: {propertyValue}",
            "frontendcount" => $"Frontend IPs: {propertyValue}",
            "haspublicip" => (bool)propertyValue ? "Public-facing load balancer" : "",
            "hasprivateip" => (bool)propertyValue ? "Internal load balancer" : "",
            "backendpoolcount" => $"Backend Pools: {propertyValue}",
            "totalbackendinstances" => $"Total Backend Instances: {propertyValue}",
            "loadbalancingrulecount" => $"Load Balancing Rules: {propertyValue}",
            "healthprobecount" => $"Health Probes: {propertyValue}",
            "natrulecount" => $"NAT Rules: {propertyValue}",
            "outboundrulecount" => $"Outbound Rules: {propertyValue}",
            "isinuse" => (bool)propertyValue ? "✅ Load balancer is active" : "⚠️ No backend instances (orphaned)",
            "provisioningstate" => propertyValue?.ToString() == "Succeeded" ? "✅ Provisioned successfully" : $"⚠️ State: {propertyValue}",
            _ => $"{propertyName}: {propertyValue}"
        };
    }

    private List<Dictionary<string, object>> GetFrontendConfigurations(Dictionary<string, object>? props)
    {
        var frontends = new List<Dictionary<string, object>>();
        if (props == null) return frontends;

        if (props.TryGetValue("frontendIPConfigurations", out var configsObj))
        {
            if (configsObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var config in jsonArray.EnumerateArray())
                {
                    var info = new Dictionary<string, object>();

                    if (config.TryGetProperty("name", out var name))
                        info["name"] = name.GetString() ?? "Unknown";

                    if (config.TryGetProperty("properties", out var props2))
                    {
                        if (props2.TryGetProperty("publicIPAddress", out _))
                        {
                            info["type"] = "Public";
                        }
                        else if (props2.TryGetProperty("privateIPAddress", out var privateIp))
                        {
                            info["type"] = "Private";
                            info["privateIP"] = privateIp.GetString() ?? "";
                        }
                        else
                        {
                            info["type"] = "Unknown";
                        }

                        if (props2.TryGetProperty("privateIPAllocationMethod", out var alloc))
                            info["allocationMethod"] = alloc.GetString() ?? "Dynamic";
                    }

                    if (info.Any())
                        frontends.Add(info);
                }
            }
        }
        return frontends;
    }

    private List<Dictionary<string, object>> GetBackendPools(Dictionary<string, object>? props)
    {
        var backends = new List<Dictionary<string, object>>();
        if (props == null) return backends;

        if (props.TryGetValue("backendAddressPools", out var poolsObj))
        {
            if (poolsObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var pool in jsonArray.EnumerateArray())
                {
                    var info = new Dictionary<string, object>();

                    if (pool.TryGetProperty("name", out var name))
                        info["name"] = name.GetString() ?? "Unknown";

                    if (pool.TryGetProperty("properties", out var props2))
                    {
                        var instanceCount = 0;
                        
                        if (props2.TryGetProperty("backendIPConfigurations", out var configs) &&
                            configs.ValueKind == JsonValueKind.Array)
                        {
                            instanceCount = configs.GetArrayLength();
                        }
                        
                        if (props2.TryGetProperty("loadBalancerBackendAddresses", out var addresses) &&
                            addresses.ValueKind == JsonValueKind.Array)
                        {
                            instanceCount = Math.Max(instanceCount, addresses.GetArrayLength());
                        }

                        info["instanceCount"] = instanceCount;
                    }

                    if (info.Any())
                        backends.Add(info);
                }
            }
        }
        return backends;
    }

    private List<Dictionary<string, object>> GetLoadBalancingRules(Dictionary<string, object>? props)
    {
        var rules = new List<Dictionary<string, object>>();
        if (props == null) return rules;

        if (props.TryGetValue("loadBalancingRules", out var rulesObj))
        {
            if (rulesObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in jsonArray.EnumerateArray())
                {
                    var info = new Dictionary<string, object>();

                    if (rule.TryGetProperty("name", out var name))
                        info["name"] = name.GetString() ?? "Unknown";

                    if (rule.TryGetProperty("properties", out var props2))
                    {
                        if (props2.TryGetProperty("protocol", out var protocol))
                            info["protocol"] = protocol.GetString() ?? "Tcp";

                        if (props2.TryGetProperty("frontendPort", out var frontPort))
                            info["frontendPort"] = frontPort.GetInt32();

                        if (props2.TryGetProperty("backendPort", out var backPort))
                            info["backendPort"] = backPort.GetInt32();

                        if (props2.TryGetProperty("enableFloatingIP", out var floatingIp))
                            info["floatingIP"] = floatingIp.GetBoolean();

                        if (props2.TryGetProperty("idleTimeoutInMinutes", out var timeout))
                            info["idleTimeout"] = timeout.GetInt32();
                    }

                    if (info.Any())
                        rules.Add(info);
                }
            }
        }
        return rules;
    }

    private List<Dictionary<string, object>> GetHealthProbes(Dictionary<string, object>? props)
    {
        var probes = new List<Dictionary<string, object>>();
        if (props == null) return probes;

        if (props.TryGetValue("probes", out var probesObj))
        {
            if (probesObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var probe in jsonArray.EnumerateArray())
                {
                    var info = new Dictionary<string, object>();

                    if (probe.TryGetProperty("name", out var name))
                        info["name"] = name.GetString() ?? "Unknown";

                    if (probe.TryGetProperty("properties", out var props2))
                    {
                        if (props2.TryGetProperty("protocol", out var protocol))
                            info["protocol"] = protocol.GetString() ?? "Tcp";

                        if (props2.TryGetProperty("port", out var port))
                            info["port"] = port.GetInt32();

                        if (props2.TryGetProperty("intervalInSeconds", out var interval))
                            info["interval"] = interval.GetInt32();

                        if (props2.TryGetProperty("numberOfProbes", out var probeCount))
                            info["unhealthyThreshold"] = probeCount.GetInt32();

                        if (props2.TryGetProperty("requestPath", out var path))
                            info["path"] = path.GetString() ?? "/";
                    }

                    if (info.Any())
                        probes.Add(info);
                }
            }
        }
        return probes;
    }

    private List<Dictionary<string, object>> GetNatRules(Dictionary<string, object>? props)
    {
        var rules = new List<Dictionary<string, object>>();
        if (props == null) return rules;

        if (props.TryGetValue("inboundNatRules", out var rulesObj))
        {
            if (rulesObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in jsonArray.EnumerateArray())
                {
                    var info = new Dictionary<string, object>();

                    if (rule.TryGetProperty("name", out var name))
                        info["name"] = name.GetString() ?? "Unknown";

                    if (info.Any())
                        rules.Add(info);
                }
            }
        }
        return rules;
    }

    private List<Dictionary<string, object>> GetOutboundRules(Dictionary<string, object>? props)
    {
        var rules = new List<Dictionary<string, object>>();
        if (props == null) return rules;

        if (props.TryGetValue("outboundRules", out var rulesObj))
        {
            if (rulesObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in jsonArray.EnumerateArray())
                {
                    var info = new Dictionary<string, object>();

                    if (rule.TryGetProperty("name", out var name))
                        info["name"] = name.GetString() ?? "Unknown";

                    if (info.Any())
                        rules.Add(info);
                }
            }
        }
        return rules;
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
            else if (current is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Object)
            {
                if (jsonElement.TryGetProperty(part, out var prop))
                {
                    current = prop;
                }
                else
                {
                    return defaultValue;
                }
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

        if (current is JsonElement element)
        {
            try
            {
                if (typeof(T) == typeof(string))
                    return element.ValueKind == JsonValueKind.String ? (T)(object)element.GetString()! : defaultValue;
            }
            catch { }
        }

        return defaultValue;
    }
}
