using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// Plugin manifest that describes a plugin's metadata, dependencies, and capabilities
/// </summary>
public class PluginManifest
{
    /// <summary>
    /// Unique identifier for the plugin
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the plugin
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Plugin version (semantic versioning)
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Plugin description
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Plugin author information
    /// </summary>
    [JsonPropertyName("author")]
    public PluginAuthor Author { get; set; } = new();

    /// <summary>
    /// Plugin category
    /// </summary>
    [JsonPropertyName("category")]
    public PluginCategory Category { get; set; } = PluginCategory.General;

    /// <summary>
    /// Tags for plugin discovery
    /// </summary>
    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Plugin dependencies
    /// </summary>
    [JsonPropertyName("dependencies")]
    public List<PluginDependency> Dependencies { get; set; } = new();

    /// <summary>
    /// Required platform version
    /// </summary>
    [JsonPropertyName("requiredPlatformVersion")]
    public string RequiredPlatformVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Entry point assembly name
    /// </summary>
    [JsonPropertyName("entryAssembly")]
    public string EntryAssembly { get; set; } = string.Empty;

    /// <summary>
    /// Entry point type name
    /// </summary>
    [JsonPropertyName("entryType")]
    public string EntryType { get; set; } = string.Empty;

    /// <summary>
    /// Plugin capabilities
    /// </summary>
    [JsonPropertyName("capabilities")]
    public PluginCapabilities Capabilities { get; set; } = new();

    /// <summary>
    /// Plugin permissions required
    /// </summary>
    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Plugin icon URL
    /// </summary>
    [JsonPropertyName("iconUrl")]
    public string? IconUrl { get; set; }

    /// <summary>
    /// Plugin repository URL
    /// </summary>
    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; set; }

    /// <summary>
    /// Plugin documentation URL
    /// </summary>
    [JsonPropertyName("documentationUrl")]
    public string? DocumentationUrl { get; set; }

    /// <summary>
    /// License type
    /// </summary>
    [JsonPropertyName("license")]
    public string License { get; set; } = "MIT";

    /// <summary>
    /// Is this plugin enabled by default
    /// </summary>
    [JsonPropertyName("enabledByDefault")]
    public bool EnabledByDefault { get; set; } = false;

    /// <summary>
    /// Plugin configuration schema
    /// </summary>
    [JsonPropertyName("configurationSchema")]
    public Dictionary<string, object>? ConfigurationSchema { get; set; }
}

/// <summary>
/// Plugin author information
/// </summary>
public class PluginAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("website")]
    public string? Website { get; set; }
}

/// <summary>
/// Plugin dependency information
/// </summary>
public class PluginDependency
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("optional")]
    public bool Optional { get; set; } = false;
}

/// <summary>
/// Plugin capabilities declaration
/// </summary>
public class PluginCapabilities
{
    [JsonPropertyName("tools")]
    public List<string> Tools { get; set; } = new();

    [JsonPropertyName("resources")]
    public List<string> Resources { get; set; } = new();

    [JsonPropertyName("gateways")]
    public List<string> Gateways { get; set; } = new();

    [JsonPropertyName("events")]
    public List<string> Events { get; set; } = new();
}

/// <summary>
/// Plugin categories
/// </summary>
public enum PluginCategory
{
    General,
    Infrastructure,
    Security,
    Compliance,
    Monitoring,
    CostManagement,
    Development,
    DataManagement,
    Networking,
    Integration
}