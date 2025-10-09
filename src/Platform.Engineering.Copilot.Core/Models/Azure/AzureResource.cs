using System;
using System.Collections.Generic;

namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// Represents an Azure resource with its properties for compliance scanning
/// </summary>
public class AzureResource
{
    /// <summary>
    /// Unique resource identifier
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// Resource name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Resource type (e.g., Microsoft.Storage/storageAccounts)
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Azure region where the resource is deployed
    /// </summary>
    public required string Location { get; set; }

    /// <summary>
    /// Resource group name
    /// </summary>
    public required string ResourceGroup { get; set; }

    /// <summary>
    /// Resource properties as key-value pairs
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();

    /// <summary>
    /// Resource tags
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();

    /// <summary>
    /// Subscription ID
    /// </summary>
    public string? SubscriptionId { get; set; }

    /// <summary>
    /// Resource creation timestamp
    /// </summary>
    public DateTime? CreatedTime { get; set; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime? ModifiedTime { get; set; }

    /// <summary>
    /// Current provisioning state
    /// </summary>
    public string? ProvisioningState { get; set; }

    /// <summary>
    /// Resource SKU information
    /// </summary>
    public string? Sku { get; set; }

    /// <summary>
    /// Resource kind (if applicable)
    /// </summary>
    public string? Kind { get; set; }

    /// <summary>
    /// Gets a property value safely with type conversion
    /// </summary>
    public T? GetProperty<T>(string key, T? defaultValue = default)
    {
        if (!Properties.TryGetValue(key, out var value) || value == null)
        {
            return defaultValue;
        }

        try
        {
            if (value is T directValue)
            {
                return directValue;
            }

            // Handle type conversion
            if (typeof(T) == typeof(string))
            {
                return (T)(object)value.ToString()!;
            }

            if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
            {
                if (bool.TryParse(value.ToString(), out var boolValue))
                {
                    return (T)(object)boolValue;
                }
            }

            if (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            {
                if (int.TryParse(value.ToString(), out var intValue))
                {
                    return (T)(object)intValue;
                }
            }

            // Try direct conversion as fallback
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Checks if a property exists and has the specified value
    /// </summary>
    public bool HasPropertyValue(string key, object expectedValue)
    {
        if (!Properties.TryGetValue(key, out var value))
        {
            return false;
        }

        return value?.ToString()?.Equals(expectedValue.ToString(), StringComparison.OrdinalIgnoreCase) == true;
    }

    /// <summary>
    /// Gets the subscription ID from the resource ID if not explicitly set
    /// </summary>
    public string GetSubscriptionId()
    {
        if (!string.IsNullOrEmpty(SubscriptionId))
        {
            return SubscriptionId;
        }

        // Extract from resource ID: /subscriptions/{subscriptionId}/...
        var parts = Id.Split('/');
        if (parts.Length >= 3 && parts[1] == "subscriptions")
        {
            return parts[2];
        }

        return string.Empty;
    }

    /// <summary>
    /// Determines if this resource type supports a specific compliance feature
    /// </summary>
    public bool SupportsComplianceFeature(string feature)
    {
        return feature.ToLowerInvariant() switch
        {
            "encryption" => Type.Contains("Storage") || Type.Contains("Sql") || Type.Contains("Compute"),
            "networking" => Type.Contains("Network") || Type.Contains("Web") || Type.Contains("Compute"),
            "access_control" => true, // All resources support access control
            "auditing" => Type.Contains("Sql") || Type.Contains("KeyVault") || Type.Contains("Storage"),
            "backup" => Type.Contains("Compute") || Type.Contains("Sql") || Type.Contains("Storage"),
            _ => false
        };
    }
}