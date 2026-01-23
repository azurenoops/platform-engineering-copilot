namespace Platform.Engineering.Copilot.Core.Services.Governance;

/// <summary>
/// Service for validating governance policies before provisioning operations.
/// Enforces approved regions, naming conventions, required tags, and other policies.
/// </summary>
public interface IGovernanceValidationService
{
    /// <summary>
    /// Validate a provisioning request against all governance policies.
    /// </summary>
    /// <param name="request">The validation request containing all parameters to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with any violations</returns>
    Task<GovernanceValidationResult> ValidateAsync(
        GovernanceValidationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a specific region is approved for deployment.
    /// </summary>
    bool IsRegionApproved(string region);

    /// <summary>
    /// Validate a resource name against naming conventions.
    /// </summary>
    NamingValidationResult ValidateResourceName(string resourceName, string? resourceType = null);

    /// <summary>
    /// Check if required tags are present.
    /// </summary>
    TagValidationResult ValidateRequiredTags(Dictionary<string, string>? tags);
}

/// <summary>
/// Request model for governance validation.
/// </summary>
public class GovernanceValidationRequest
{
    /// <summary>
    /// The Azure region for deployment.
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// The environment name being created.
    /// </summary>
    public string? EnvironmentName { get; set; }

    /// <summary>
    /// The resource group name.
    /// </summary>
    public string? ResourceGroupName { get; set; }

    /// <summary>
    /// Tags to be applied to resources.
    /// </summary>
    public Dictionary<string, string>? Tags { get; set; }

    /// <summary>
    /// The template being used (for context).
    /// </summary>
    public string? TemplateId { get; set; }

    /// <summary>
    /// Additional parameters to validate.
    /// </summary>
    public Dictionary<string, object>? Parameters { get; set; }

    /// <summary>
    /// The requesting user/identity.
    /// </summary>
    public string? RequestedBy { get; set; }
}

/// <summary>
/// Result of governance validation.
/// </summary>
public class GovernanceValidationResult
{
    /// <summary>
    /// Whether all governance policies passed.
    /// </summary>
    public bool IsValid => !Violations.Any(v => v.Severity == GovernanceViolationSeverity.Error);

    /// <summary>
    /// List of governance violations found.
    /// </summary>
    public List<GovernanceViolation> Violations { get; set; } = new();

    /// <summary>
    /// Warnings that don't block provisioning.
    /// </summary>
    public List<string> Warnings => Violations
        .Where(v => v.Severity == GovernanceViolationSeverity.Warning)
        .Select(v => v.Message)
        .ToList();

    /// <summary>
    /// Errors that block provisioning.
    /// </summary>
    public List<string> Errors => Violations
        .Where(v => v.Severity == GovernanceViolationSeverity.Error)
        .Select(v => v.Message)
        .ToList();
}

/// <summary>
/// A single governance policy violation.
/// </summary>
public class GovernanceViolation
{
    /// <summary>
    /// The type of policy violated.
    /// </summary>
    public GovernancePolicyType PolicyType { get; set; }

    /// <summary>
    /// Human-readable message describing the violation.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// The property or field that violated the policy.
    /// </summary>
    public string? Property { get; set; }

    /// <summary>
    /// The value that was provided.
    /// </summary>
    public object? ProvidedValue { get; set; }

    /// <summary>
    /// The allowed/required value(s).
    /// </summary>
    public object? AllowedValue { get; set; }

    /// <summary>
    /// Severity of the violation.
    /// </summary>
    public GovernanceViolationSeverity Severity { get; set; } = GovernanceViolationSeverity.Error;

    /// <summary>
    /// NIST control(s) this policy enforces.
    /// </summary>
    public string? NistControls { get; set; }
}

/// <summary>
/// Types of governance policies.
/// </summary>
public enum GovernancePolicyType
{
    /// <summary>
    /// Approved Azure regions.
    /// </summary>
    ApprovedRegion,

    /// <summary>
    /// Resource naming conventions.
    /// </summary>
    NamingConvention,

    /// <summary>
    /// Required resource tags.
    /// </summary>
    RequiredTags,

    /// <summary>
    /// Cost threshold limits.
    /// </summary>
    CostThreshold,

    /// <summary>
    /// Resource type restrictions.
    /// </summary>
    ResourceTypeRestriction,

    /// <summary>
    /// Security configuration requirements.
    /// </summary>
    SecurityConfiguration
}

/// <summary>
/// Severity of governance violations.
/// </summary>
public enum GovernanceViolationSeverity
{
    /// <summary>
    /// Informational - logged but doesn't affect provisioning.
    /// </summary>
    Info,

    /// <summary>
    /// Warning - provisioning proceeds but user is notified.
    /// </summary>
    Warning,

    /// <summary>
    /// Error - provisioning is blocked.
    /// </summary>
    Error
}

/// <summary>
/// Result of naming convention validation.
/// </summary>
public class NamingValidationResult
{
    public bool IsValid { get; set; }
    public string? ViolationMessage { get; set; }
    public string? SuggestedName { get; set; }
}

/// <summary>
/// Result of tag validation.
/// </summary>
public class TagValidationResult
{
    public bool IsValid { get; set; }
    public List<string> MissingTags { get; set; } = new();
    public string? ViolationMessage { get; set; }
}
