namespace Platform.Engineering.Copilot.Core.Models.Validation;

/// <summary>
/// Result of configuration validation with errors, warnings, and recommendations
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Whether the configuration is valid and can be deployed
    /// </summary>
    public bool IsValid { get; set; } = true;

    /// <summary>
    /// Critical errors that prevent deployment
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// Non-critical warnings that may affect performance or cost
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Suggestions for optimal configuration
    /// </summary>
    public List<ValidationRecommendation> Recommendations { get; set; } = new();

    /// <summary>
    /// Platform being validated (AKS, ECS, Lambda, etc.)
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    /// Total validation time in milliseconds
    /// </summary>
    public long ValidationTimeMs { get; set; }
}

/// <summary>
/// Critical validation error that prevents deployment
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Field or property that failed validation
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable error message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Error code for programmatic handling (e.g., "LAMBDA_INVALID_MEMORY")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Current invalid value
    /// </summary>
    public string? CurrentValue { get; set; }

    /// <summary>
    /// Valid range or expected format
    /// </summary>
    public string? ExpectedValue { get; set; }

    /// <summary>
    /// Link to documentation about this error
    /// </summary>
    public string? DocumentationUrl { get; set; }
    public string Severity { get; internal set; }
}

/// <summary>
/// Non-critical warning about configuration
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// Field or property with warning
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable warning message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Warning code for programmatic handling (e.g., "LAMBDA_HIGH_MEMORY")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Severity level: Low, Medium, High
    /// </summary>
    public WarningSeverity Severity { get; set; } = WarningSeverity.Medium;

    /// <summary>
    /// Potential impact (e.g., "May increase costs by 30%")
    /// </summary>
    public string? Impact { get; set; }
}

/// <summary>
/// Recommendation for optimal configuration
/// </summary>
public class ValidationRecommendation
{
    /// <summary>
    /// Field or property to optimize
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable recommendation
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Recommendation code (e.g., "LAMBDA_OPTIMIZE_MEMORY")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Current value
    /// </summary>
    public string? CurrentValue { get; set; }

    /// <summary>
    /// Recommended value
    /// </summary>
    public string? RecommendedValue { get; set; }

    /// <summary>
    /// Reason for recommendation
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Expected benefit (e.g., "Reduce costs by 15%")
    /// </summary>
    public string? Benefit { get; set; }
}

/// <summary>
/// Warning severity levels
/// </summary>
public enum WarningSeverity
{
    /// <summary>
    /// Informational, minor concern
    /// </summary>
    Low = 1,

    /// <summary>
    /// Moderate concern, should be reviewed
    /// </summary>
    Medium = 2,

    /// <summary>
    /// Significant concern, strongly recommend fixing
    /// </summary>
    High = 3
}
