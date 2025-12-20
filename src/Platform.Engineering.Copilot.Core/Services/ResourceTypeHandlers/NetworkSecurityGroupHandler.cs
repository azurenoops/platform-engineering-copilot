using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;

/// <summary>
/// Handler for Azure Network Security Group resources
/// </summary>
public class NetworkSecurityGroupHandler : IResourceTypeHandler
{
    public string ResourceType => "microsoft.network/networksecuritygroups";

    public List<string> ExtendedProperties => new()
    {
        "properties.securityRules",
        "properties.defaultSecurityRules",
        "properties.networkInterfaces",
        "properties.subnets",
        "properties.provisioningState",
        "properties.flowLogs"
    };

    public Dictionary<string, object> ParseExtendedProperties(AzureResource resource)
    {
        var result = new Dictionary<string, object>();

        try
        {
            // Security Rules
            var customRules = GetSecurityRules(resource.Properties, "securityRules");
            result["customRuleCount"] = customRules.Count;
            result["customRules"] = customRules;

            // Default Rules
            var defaultRules = GetSecurityRules(resource.Properties, "defaultSecurityRules");
            result["defaultRuleCount"] = defaultRules.Count;

            // Attached NICs
            var nics = GetAttachedResources(resource.Properties, "networkInterfaces");
            result["attachedNicCount"] = nics.Count;
            result["attachedNics"] = nics;

            // Attached Subnets
            var subnets = GetAttachedResources(resource.Properties, "subnets");
            result["attachedSubnetCount"] = subnets.Count;
            result["attachedSubnets"] = subnets;

            // Check if NSG is attached to anything
            result["isAttached"] = nics.Count > 0 || subnets.Count > 0;

            // Security Analysis
            var analysis = AnalyzeSecurityRules(customRules);
            result["hasInboundInternetAccess"] = analysis.HasInboundInternetAccess;
            result["hasOutboundInternetAccess"] = analysis.HasOutboundInternetAccess;
            result["openPorts"] = analysis.OpenPorts;
            result["riskyRules"] = analysis.RiskyRules;

            // Provisioning State
            result["provisioningState"] = resource.ProvisioningState ?? "Unknown";

            // Flow Logs
            var flowLogs = GetFlowLogs(resource.Properties);
            result["flowLogsEnabled"] = flowLogs.Any();
            result["flowLogCount"] = flowLogs.Count;
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
            "customrulecount" => $"Custom Rules: {propertyValue}",
            "defaultrulecount" => $"Default Rules: {propertyValue}",
            "attachedniccount" => $"Attached NICs: {propertyValue}",
            "attachedsubnetcount" => $"Attached Subnets: {propertyValue}",
            "isattached" => (bool)propertyValue ? "✅ NSG is in use" : "⚠️ NSG is not attached (orphaned)",
            "hasinboundinternetaccess" => (bool)propertyValue ? "⚠️ Allows inbound from Internet" : "✅ No direct inbound Internet access",
            "hasoutboundinternetaccess" => (bool)propertyValue ? "Allows outbound to Internet" : "Restricted outbound",
            "openports" => $"Open Ports: {propertyValue}",
            "riskyrules" => $"Security Concerns: {propertyValue}",
            "flowlogsenabled" => (bool)propertyValue ? "✅ Flow logs enabled" : "⚠️ Flow logs not configured",
            "provisioningstate" => propertyValue?.ToString() == "Succeeded" ? "✅ Provisioned successfully" : $"⚠️ State: {propertyValue}",
            _ => $"{propertyName}: {propertyValue}"
        };
    }

    private List<Dictionary<string, object>> GetSecurityRules(Dictionary<string, object>? props, string rulesKey)
    {
        var rules = new List<Dictionary<string, object>>();
        if (props == null) return rules;

        if (props.TryGetValue(rulesKey, out var rulesObj))
        {
            if (rulesObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var rule in jsonArray.EnumerateArray())
                {
                    var ruleInfo = new Dictionary<string, object>();

                    if (rule.TryGetProperty("name", out var name))
                        ruleInfo["name"] = name.GetString() ?? "Unknown";

                    if (rule.TryGetProperty("properties", out var props2))
                    {
                        if (props2.TryGetProperty("direction", out var direction))
                            ruleInfo["direction"] = direction.GetString() ?? "Unknown";

                        if (props2.TryGetProperty("access", out var access))
                            ruleInfo["access"] = access.GetString() ?? "Unknown";

                        if (props2.TryGetProperty("priority", out var priority))
                            ruleInfo["priority"] = priority.GetInt32();

                        if (props2.TryGetProperty("protocol", out var protocol))
                            ruleInfo["protocol"] = protocol.GetString() ?? "*";

                        if (props2.TryGetProperty("sourceAddressPrefix", out var sourceAddr))
                            ruleInfo["sourceAddressPrefix"] = sourceAddr.GetString() ?? "*";

                        if (props2.TryGetProperty("destinationAddressPrefix", out var destAddr))
                            ruleInfo["destinationAddressPrefix"] = destAddr.GetString() ?? "*";

                        if (props2.TryGetProperty("destinationPortRange", out var destPort))
                            ruleInfo["destinationPortRange"] = destPort.GetString() ?? "*";

                        if (props2.TryGetProperty("sourcePortRange", out var srcPort))
                            ruleInfo["sourcePortRange"] = srcPort.GetString() ?? "*";
                    }

                    if (ruleInfo.Any())
                        rules.Add(ruleInfo);
                }
            }
        }
        return rules;
    }

    private List<string> GetAttachedResources(Dictionary<string, object>? props, string resourceKey)
    {
        var resources = new List<string>();
        if (props == null) return resources;

        if (props.TryGetValue(resourceKey, out var resourcesObj))
        {
            if (resourcesObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var res in jsonArray.EnumerateArray())
                {
                    if (res.TryGetProperty("id", out var id))
                    {
                        var idStr = id.GetString() ?? "";
                        resources.Add(idStr.Contains("/") ? idStr.Split('/').Last() : idStr);
                    }
                }
            }
        }
        return resources;
    }

    private List<string> GetFlowLogs(Dictionary<string, object>? props)
    {
        var flowLogs = new List<string>();
        if (props == null) return flowLogs;

        if (props.TryGetValue("flowLogs", out var logsObj))
        {
            if (logsObj is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var log in jsonArray.EnumerateArray())
                {
                    if (log.TryGetProperty("id", out var id))
                    {
                        var idStr = id.GetString() ?? "";
                        flowLogs.Add(idStr.Contains("/") ? idStr.Split('/').Last() : idStr);
                    }
                }
            }
        }
        return flowLogs;
    }

    private (bool HasInboundInternetAccess, bool HasOutboundInternetAccess, string OpenPorts, string RiskyRules) AnalyzeSecurityRules(List<Dictionary<string, object>> rules)
    {
        var hasInbound = false;
        var hasOutbound = false;
        var openPorts = new List<string>();
        var riskyRules = new List<string>();

        foreach (var rule in rules)
        {
            var direction = rule.GetValueOrDefault("direction")?.ToString() ?? "";
            var access = rule.GetValueOrDefault("access")?.ToString() ?? "";
            var source = rule.GetValueOrDefault("sourceAddressPrefix")?.ToString() ?? "";
            var dest = rule.GetValueOrDefault("destinationAddressPrefix")?.ToString() ?? "";
            var port = rule.GetValueOrDefault("destinationPortRange")?.ToString() ?? "";
            var ruleName = rule.GetValueOrDefault("name")?.ToString() ?? "";

            if (access.Equals("Allow", StringComparison.OrdinalIgnoreCase))
            {
                // Check for Internet access
                if (direction.Equals("Inbound", StringComparison.OrdinalIgnoreCase) && 
                    (source == "*" || source.Equals("Internet", StringComparison.OrdinalIgnoreCase)))
                {
                    hasInbound = true;
                    openPorts.Add($"{port} (inbound)");

                    // Check for risky ports
                    if (port == "*" || port == "22" || port == "3389" || port == "445" || port == "1433")
                    {
                        riskyRules.Add($"{ruleName}: {port} open to Internet");
                    }
                }

                if (direction.Equals("Outbound", StringComparison.OrdinalIgnoreCase) && 
                    (dest == "*" || dest.Equals("Internet", StringComparison.OrdinalIgnoreCase)))
                {
                    hasOutbound = true;
                }
            }
        }

        return (
            hasInbound,
            hasOutbound,
            openPorts.Any() ? string.Join(", ", openPorts) : "None",
            riskyRules.Any() ? string.Join("; ", riskyRules) : "None"
        );
    }
}
