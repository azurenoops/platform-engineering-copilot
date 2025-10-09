using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Models.Onboarding;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Generic onboarding service interface for command-specific implementations
/// </summary>
/// <typeparam name="TRequest">The type of onboarding request (e.g., FlankspeedRequest, NavwarRequest)</typeparam>
public interface IGenericOnboardingService<TRequest> where TRequest : class
{
    /// <summary>
    /// Get the onboarding workflow configuration (phases, fields, validation rules)
    /// </summary>
    OnboardingWorkflowConfig GetWorkflowConfig();

    /// <summary>
    /// Create a new draft onboarding request
    /// </summary>
    Task<TRequest> CreateDraftAsync(string userEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update a draft request with new field values
    /// </summary>
    Task<TRequest> UpdateDraftAsync(string requestId, Dictionary<string, object> updates, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a draft request by ID
    /// </summary>
    Task<TRequest?> GetDraftAsync(string requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all pending draft requests for a user
    /// </summary>
    Task<List<TRequest>> GetPendingRequestsByUserAsync(string userEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate a request against the workflow configuration
    /// </summary>
    Task<OnboardingValidationResult> ValidateRequestAsync(TRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submit a request for approval
    /// </summary>
    Task<TRequest> SubmitForApprovalAsync(string requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancel/delete a draft request
    /// </summary>
    Task<bool> CancelDraftAsync(string requestId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Defines the complete onboarding workflow configuration
/// </summary>
public class OnboardingWorkflowConfig
{
    /// <summary>
    /// Unique identifier for this workflow (e.g., "flankspeed", "navwar", "niwc")
    /// </summary>
    public required string WorkflowId { get; set; }

    /// <summary>
    /// Display name for this onboarding workflow
    /// </summary>
    public required string DisplayName { get; set; }

    /// <summary>
    /// Description of what this workflow is for
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// List of phases in this workflow (in order)
    /// </summary>
    public List<OnboardingPhase> Phases { get; set; } = new();

    /// <summary>
    /// Command/organization this workflow is for
    /// </summary>
    public string? TargetCommand { get; set; }

    /// <summary>
    /// Approval authority for this workflow (e.g., "NNWC Admin", "NAVWAR Director")
    /// </summary>
    public string? ApprovalAuthority { get; set; }

    /// <summary>
    /// Welcome message shown when starting this workflow
    /// </summary>
    public string? WelcomeMessage { get; set; }

    /// <summary>
    /// Completion message shown after submission
    /// </summary>
    public string? CompletionMessage { get; set; }

    /// <summary>
    /// Custom field transformations (rank normalization, service detection, etc.)
    /// </summary>
    public Dictionary<string, FieldTransformation> FieldTransformations { get; set; } = new();
}

/// <summary>
/// Defines a custom transformation to apply to a field value
/// </summary>
public class FieldTransformation
{
    /// <summary>
    /// Type of transformation (uppercase, lowercase, normalize_rank, detect_service, etc.)
    /// </summary>
    public required string TransformationType { get; set; }

    /// <summary>
    /// Additional configuration for the transformation
    /// </summary>
    public Dictionary<string, object>? Config { get; set; }
}

/// <summary>
/// Result of validating an onboarding request
/// </summary>
public class OnboardingValidationResult
{
    /// <summary>
    /// Whether the request is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// List of validation errors
    /// </summary>
    public List<ValidationError> Errors { get; set; } = new();

    /// <summary>
    /// List of warnings (non-blocking issues)
    /// </summary>
    public List<ValidationWarning> Warnings { get; set; } = new();

    /// <summary>
    /// Current phase the request is in
    /// </summary>
    public string? CurrentPhase { get; set; }

    /// <summary>
    /// Percentage of required fields completed (0-100)
    /// </summary>
    public int CompletionPercentage { get; set; }

    /// <summary>
    /// Fields that are still required
    /// </summary>
    public List<string> MissingRequiredFields { get; set; } = new();
}

/// <summary>
/// Validation error for a specific field
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Field that has the error
    /// </summary>
    public required string FieldId { get; set; }

    /// <summary>
    /// Error message
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Error code for programmatic handling
    /// </summary>
    public string? ErrorCode { get; set; }
}

/// <summary>
/// Validation warning for a specific field
/// </summary>
public class ValidationWarning
{
    /// <summary>
    /// Field that has the warning
    /// </summary>
    public required string FieldId { get; set; }

    /// <summary>
    /// Warning message
    /// </summary>
    public required string Message { get; set; }
}
