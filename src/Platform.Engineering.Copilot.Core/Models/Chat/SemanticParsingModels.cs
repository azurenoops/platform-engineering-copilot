using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Core.Models.SemanticParsing;

/// <summary>
/// Represents the intent and parameters extracted from a natural language query
/// </summary>
public class ParsedQuery
{
    [JsonPropertyName("originalQuery")]
    public string OriginalQuery { get; set; } = string.Empty;

    [JsonPropertyName("intent")]
    public QueryIntent Intent { get; set; } = new();

    [JsonPropertyName("entities")]
    public Dictionary<string, object> Entities { get; set; } = new();

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("suggestedTools")]
    public List<ToolSuggestion> SuggestedTools { get; set; } = new();

    [JsonPropertyName("parameters")]
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Represents the identified intent from a natural language query
/// </summary>
public class QueryIntent
{
    [JsonPropertyName("category")]
    public IntentCategory Category { get; set; }

    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Represents a suggested tool for executing a query
/// </summary>
public class ToolSuggestion
{
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonPropertyName("extractedParameters")]
    public Dictionary<string, object> ExtractedParameters { get; set; } = new();

    [JsonPropertyName("missingParameters")]
    public List<string> MissingParameters { get; set; } = new();
}

/// <summary>
/// Categories of intents that can be identified
/// </summary>
public enum IntentCategory
{
    Unknown,
    Infrastructure,
    Security,
    Monitoring,
    Cost,
    Deployment,
    Configuration,
    Analysis,
    Discovery,
    Compliance,
    Automation
}

/// <summary>
/// Represents a registered tool in the semantic parsing system
/// </summary>
public class ToolSchema
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public IntentCategory Category { get; set; }

    [JsonPropertyName("parameters")]
    public Dictionary<string, ParameterSchema> Parameters { get; set; } = new();

    [JsonPropertyName("examples")]
    public List<string> Examples { get; set; } = new();

    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new();

    [JsonPropertyName("aliases")]
    public List<string> Aliases { get; set; } = new();
}

/// <summary>
/// Schema for a tool parameter
/// </summary>
public class ParameterSchema
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("defaultValue")]
    public object? DefaultValue { get; set; }

    [JsonPropertyName("examples")]
    public List<string> Examples { get; set; } = new();

    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; set; } = new();
}