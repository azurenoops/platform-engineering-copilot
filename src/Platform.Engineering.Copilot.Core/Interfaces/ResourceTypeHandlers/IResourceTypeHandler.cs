using Platform.Engineering.Copilot.Core.Models.Azure;

namespace Platform.Engineering.Copilot.Core.Interfaces.ResourceTypeHandlers;

/// <summary>
/// Interface for resource type-specific property handlers
/// </summary>
public interface IResourceTypeHandler
{
    /// <summary>
    /// The Azure resource type this handler supports (e.g., "microsoft.web/sites")
    /// </summary>
    string ResourceType { get; }

    /// <summary>
    /// Extended properties to retrieve from Resource Graph for this resource type
    /// </summary>
    List<string> ExtendedProperties { get; }

    /// <summary>
    /// Parse and extract extended properties from a Resource Graph result
    /// </summary>
    /// <param name="resource">The AzureResource from Resource Graph</param>
    /// <returns>Dictionary of parsed property names to values</returns>
    Dictionary<string, object> ParseExtendedProperties(AzureResource resource);

    /// <summary>
    /// Get a human-readable description of a property value
    /// </summary>
    /// <param name="propertyName">The property name</param>
    /// <param name="propertyValue">The property value</param>
    /// <returns>Human-readable description</returns>
    string GetPropertyDescription(string propertyName, object propertyValue);
}
