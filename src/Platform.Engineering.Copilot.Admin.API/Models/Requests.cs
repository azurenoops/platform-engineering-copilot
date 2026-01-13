using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Admin.API.Models;

#region Template Requests

/// <summary>
/// Request to create a new service template
/// </summary>
public class CreateTemplateRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? DisplayName { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(20)]
    public string? Version { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    public string? Format { get; set; }

    public string? TemplateContent { get; set; }

    public string? CreatedBy { get; set; }

    public bool RequiresApproval { get; set; }

    public int? DefaultExpirationDays { get; set; }

    public List<string>? ComplianceFrameworks { get; set; }

    public List<string>? Keywords { get; set; }

    public List<string>? UseCases { get; set; }

    public string? AiSelectionHint { get; set; }

    public List<CreateParameterRequest>? Parameters { get; set; }

    public List<CreateGuardrailRequest>? Guardrails { get; set; }
}

/// <summary>
/// Request to create a template parameter
/// </summary>
public class CreateParameterRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public List<object>? AllowedValues { get; set; }
    public object? MinValue { get; set; }
    public object? MaxValue { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>
/// Request to create a guardrail
/// </summary>
public class CreateGuardrailRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? Property { get; set; }
    public string? Operator { get; set; }
    public string? Value { get; set; }
    public string? Action { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request to update a template
/// </summary>
public class UpdateTemplateRequest
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? TemplateContent { get; set; }
    public List<string>? Keywords { get; set; }
    public List<string>? UseCases { get; set; }
    public string? AiSelectionHint { get; set; }
    public int? DefaultExpirationDays { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Request to approve a template
/// </summary>
public class ApprovalRequest
{
    public string? Source { get; set; }
    public string? ApprovedBy { get; set; }
    public string? Comments { get; set; }
    public string? ExternalApprovalId { get; set; }
    public string? ExternalApprovalUrl { get; set; }
}

/// <summary>
/// Request to validate a template
/// </summary>
public class ValidateTemplateRequest
{
    public string? Name { get; set; }
    public string? TemplateContent { get; set; }
    public string? Format { get; set; }
}

#endregion

#region Environment Requests

/// <summary>
/// Request to create a new environment
/// </summary>
public class CreateEnvironmentRequest
{
    [Required]
    public string TemplateId { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string EnvironmentName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
    public string? Description { get; set; }

    [Required]
    public string ResourceGroup { get; set; } = string.Empty;

    [Required]
    public string SubscriptionId { get; set; } = string.Empty;

    public string? Location { get; set; }

    public Dictionary<string, object>? Parameters { get; set; }

    public Dictionary<string, string>? Tags { get; set; }

    public string? OwnerEmail { get; set; }

    public string? RequestedBy { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public bool AutoDelete { get; set; }
}

/// <summary>
/// Request to scale an environment
/// </summary>
public class ScaleEnvironmentApiRequest
{
    public int? NodeCount { get; set; }
    public int? ReplicaCount { get; set; }
    public string? Sku { get; set; }
    public string? Tier { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public string? ScaledBy { get; set; }
}

/// <summary>
/// Request to clone an environment
/// </summary>
public class CloneEnvironmentRequest
{
    [Required]
    public string NewName { get; set; } = string.Empty;

    public string? ClonedBy { get; set; }
}

/// <summary>
/// Request to remediate drift
/// </summary>
public class RemediateDriftRequest
{
    public List<string>? DriftItemIds { get; set; }
    public string? RemediatedBy { get; set; }
}

/// <summary>
/// Request to extend environment expiration
/// </summary>
public class ExtendExpirationRequest
{
    [Required]
    public DateTime NewExpiration { get; set; }

    public string? ExtendedBy { get; set; }
}

#endregion
