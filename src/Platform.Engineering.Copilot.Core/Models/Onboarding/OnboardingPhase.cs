using System.Collections.Generic;

namespace Platform.Engineering.Copilot.Core.Models.Onboarding;

/// <summary>
/// Defines a phase in the onboarding workflow with configurable fields and validation rules
/// </summary>
public class OnboardingPhase
{
    /// <summary>
    /// Unique identifier for this phase (e.g., "mission_details", "technical_requirements")
    /// </summary>
    public required string PhaseId { get; set; }

    /// <summary>
    /// Display name for this phase
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Order in which this phase appears in the workflow (1-based)
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Description of what information is collected in this phase
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of fields to collect in this phase
    /// </summary>
    public List<OnboardingFieldDefinition> Fields { get; set; } = new();

    /// <summary>
    /// Required fields that must be completed before advancing to next phase
    /// </summary>
    public List<string> RequiredFields { get; set; } = new();

    /// <summary>
    /// Prompt to show user when entering this phase
    /// </summary>
    public string? InitialPrompt { get; set; }

    /// <summary>
    /// Prompt to show when phase is complete
    /// </summary>
    public string? CompletionPrompt { get; set; }

    /// <summary>
    /// Whether this phase can be skipped
    /// </summary>
    public bool IsOptional { get; set; }
}

/// <summary>
/// Defines a field that can be collected during onboarding
/// </summary>
public class OnboardingFieldDefinition
{
    /// <summary>
    /// Unique field identifier (e.g., "missionName", "vnetCidr")
    /// </summary>
    public required string FieldId { get; set; }

    /// <summary>
    /// Display name for this field
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Field data type (string, int, decimal, bool, array, etc.)
    /// </summary>
    public required string DataType { get; set; }

    /// <summary>
    /// Description/help text for this field
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Regex patterns for extracting this field from natural language
    /// </summary>
    public List<string> ExtractionPatterns { get; set; } = new();

    /// <summary>
    /// Example values to show user
    /// </summary>
    public List<string> Examples { get; set; } = new();

    /// <summary>
    /// Default value if not provided
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Whether this field is required
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Validation rules (regex patterns, value ranges, etc.)
    /// </summary>
    public List<string> ValidationRules { get; set; } = new();

    /// <summary>
    /// Custom transformation function to apply after extraction (e.g., uppercase, lowercase, normalization)
    /// </summary>
    public string? TransformationType { get; set; }

    /// <summary>
    /// Whether this field should be stored in the database with a different name
    /// </summary>
    public string? DatabaseFieldName { get; set; }
}
