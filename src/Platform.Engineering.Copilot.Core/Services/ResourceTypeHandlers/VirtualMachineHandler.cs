using Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;
using Platform.Engineering.Copilot.Core.Models.Azure;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.ResourceTypeHandlers;

/// <summary>
/// Handler for Azure Virtual Machine resources
/// </summary>
public class VirtualMachineHandler : IResourceTypeHandler
{
    public string ResourceType => "microsoft.compute/virtualmachines";

    public List<string> ExtendedProperties => new()
    {
        "properties.hardwareProfile.vmSize",
        "properties.storageProfile.osDisk.osType",
        "properties.storageProfile.osDisk.diskSizeGB",
        "properties.storageProfile.osDisk.managedDisk.storageAccountType",
        "properties.storageProfile.imageReference.publisher",
        "properties.storageProfile.imageReference.offer",
        "properties.storageProfile.imageReference.sku",
        "properties.osProfile.computerName",
        "properties.osProfile.adminUsername",
        "properties.networkProfile.networkInterfaces",
        "properties.provisioningState",
        "properties.diagnosticsProfile.bootDiagnostics.enabled",
        "properties.securityProfile.securityType",
        "properties.securityProfile.uefiSettings.secureBootEnabled",
        "properties.securityProfile.uefiSettings.vTpmEnabled",
        "zones"
    };

    public Dictionary<string, object> ParseExtendedProperties(AzureResource resource)
    {
        var result = new Dictionary<string, object>();

        try
        {
            // VM Size
            result["vmSize"] = GetPropertyValue<string>(resource.Properties, "hardwareProfile.vmSize", "Unknown");

            // OS Type
            result["osType"] = GetPropertyValue<string>(resource.Properties, "storageProfile.osDisk.osType", "Unknown");

            // OS Disk Size
            var diskSize = GetPropertyValue<int?>(resource.Properties, "storageProfile.osDisk.diskSizeGB", null);
            result["osDiskSizeGB"] = diskSize?.ToString() ?? "Auto";

            // Disk Type
            result["diskType"] = GetPropertyValue<string>(resource.Properties, "storageProfile.osDisk.managedDisk.storageAccountType", "Standard_LRS");

            // Image Reference
            var publisher = GetPropertyValue<string>(resource.Properties, "storageProfile.imageReference.publisher", "");
            var offer = GetPropertyValue<string>(resource.Properties, "storageProfile.imageReference.offer", "");
            var imageSku = GetPropertyValue<string>(resource.Properties, "storageProfile.imageReference.sku", "");
            result["image"] = !string.IsNullOrEmpty(publisher) ? $"{publisher}:{offer}:{imageSku}" : "Custom Image";

            // Computer Name
            result["computerName"] = GetPropertyValue<string>(resource.Properties, "osProfile.computerName", "Not configured");

            // Admin Username
            result["adminUsername"] = GetPropertyValue<string>(resource.Properties, "osProfile.adminUsername", "Not configured");

            // Provisioning State
            result["provisioningState"] = resource.ProvisioningState ?? "Unknown";

            // Boot Diagnostics
            result["bootDiagnostics"] = GetPropertyValue<bool>(resource.Properties, "diagnosticsProfile.bootDiagnostics.enabled", false);

            // Security Profile
            result["securityType"] = GetPropertyValue<string>(resource.Properties, "securityProfile.securityType", "Standard");
            result["secureBootEnabled"] = GetPropertyValue<bool>(resource.Properties, "securityProfile.uefiSettings.secureBootEnabled", false);
            result["vTpmEnabled"] = GetPropertyValue<bool>(resource.Properties, "securityProfile.uefiSettings.vTpmEnabled", false);

            // Availability Zones
            if (resource.Properties?.TryGetValue("zones", out var zones) == true && zones != null)
            {
                result["availabilityZones"] = zones.ToString() ?? "None";
            }
            else
            {
                result["availabilityZones"] = "None";
            }

            // Network Interfaces Count
            if (resource.Properties?.TryGetValue("networkProfile", out var networkProfile) == true)
            {
                var nics = GetNestedValue(networkProfile, "networkInterfaces");
                if (nics is JsonElement jsonArray && jsonArray.ValueKind == JsonValueKind.Array)
                {
                    result["networkInterfaceCount"] = jsonArray.GetArrayLength();
                }
                else
                {
                    result["networkInterfaceCount"] = 1;
                }
            }
            else
            {
                result["networkInterfaceCount"] = 0;
            }
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
            "vmsize" => $"VM Size: {propertyValue}",
            "ostype" => $"OS Type: {propertyValue}",
            "osdisksizegb" => $"OS Disk: {propertyValue} GB",
            "disktype" => $"Disk Type: {propertyValue}",
            "image" => $"Image: {propertyValue}",
            "computername" => $"Computer Name: {propertyValue}",
            "provisioningstate" => propertyValue?.ToString() == "Succeeded" ? "✅ Provisioned successfully" : $"⚠️ State: {propertyValue}",
            "bootdiagnostics" => (bool)propertyValue ? "✅ Boot diagnostics enabled" : "⚠️ Boot diagnostics disabled",
            "securitytype" => propertyValue?.ToString() == "TrustedLaunch" ? "✅ Trusted Launch enabled" : $"Security: {propertyValue}",
            "securebootenabled" => (bool)propertyValue ? "✅ Secure Boot enabled" : "⚠️ Secure Boot disabled",
            "vtpmenabled" => (bool)propertyValue ? "✅ vTPM enabled" : "⚠️ vTPM disabled",
            "availabilityzones" => propertyValue?.ToString() != "None" ? $"✅ Zone: {propertyValue}" : "⚠️ No availability zone",
            "networkinterfacecount" => $"NICs: {propertyValue}",
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
