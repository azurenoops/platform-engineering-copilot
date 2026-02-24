using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Admin.API.Models;

public class CreateTemplateRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? Category { get; set; }
    public string? Format { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public string? DeploymentScope { get; set; }
    public string? ParametersJson { get; set; }
    public string? GuardrailsJson { get; set; }
    public string? ComplianceFrameworks { get; set; }
    public string? Keywords { get; set; }
    public string? UseCases { get; set; }
    public string? AiSelectionHints { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public string? GitRepoUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? GitPath { get; set; }
    public bool GitAutoSync { get; set; }
    public int? GitSyncIntervalMinutes { get; set; }
}

public class UpdateTemplateRequest
{
    [StringLength(100, MinimumLength = 3)]
    public string? Name { get; set; }

    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? Category { get; set; }
    public string? Format { get; set; }
    public string? Content { get; set; }
    public string? DeploymentScope { get; set; }
    public string? ParametersJson { get; set; }
    public string? GuardrailsJson { get; set; }
    public string? ComplianceFrameworks { get; set; }
    public string? Keywords { get; set; }
    public string? UseCases { get; set; }
    public string? AiSelectionHints { get; set; }
    public bool? RequiresApproval { get; set; }
    public string? GitRepoUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? GitPath { get; set; }
    public bool? GitAutoSync { get; set; }
    public int? GitSyncIntervalMinutes { get; set; }
}

public class ApprovalRequest
{
    [Required]
    public string ApprovalSource { get; set; } = string.Empty;

    [Required]
    public string ApprovedBy { get; set; } = string.Empty;

    public string? Comments { get; set; }
    public string? ExternalApprovalId { get; set; }
    public string? ExternalApprovalUrl { get; set; }
}

public class ValidateTemplateRequest
{
    public string? Name { get; set; }
    public string? Content { get; set; }
    public string? Format { get; set; }
}

public class ParseBicepParametersRequest
{
    [Required]
    public string BicepContent { get; set; } = string.Empty;
}

public class ParseBicepFromGitRequest
{
    [Required]
    public string GitRepoUrl { get; set; } = string.Empty;

    public string? Branch { get; set; }
    public string? FilePath { get; set; }
}

public class TemplateMatchRequest
{
    [Required]
    public string Description { get; set; } = string.Empty;

    public double MinScore { get; set; } = 0.3;
    public int MaxResults { get; set; } = 5;
}

public class ExtractParametersRequest
{
    [Required]
    public string Description { get; set; } = string.Empty;
}

public class ExplainMatchRequest
{
    [Required]
    public string Description { get; set; } = string.Empty;
}

public class ImportFromGitRequest
{
    [Required]
    public string GitRepoUrl { get; set; } = string.Empty;

    public string? Branch { get; set; }
    public string? FilePath { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public bool GitAutoSync { get; set; }
    public int? GitSyncIntervalMinutes { get; set; }
}

public class CreateEnvironmentRequest
{
    [Required]
    public Guid TemplateId { get; set; }

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
    public string? ParameterValuesJson { get; set; }
    public string? TagsJson { get; set; }
    public string? OwnerEmail { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool AutoDelete { get; set; }
}

public class ScaleEnvironmentRequest
{
    public int? NodeCount { get; set; }
    public int? ReplicaCount { get; set; }
    public string? Sku { get; set; }
    public string? Tier { get; set; }
    public Dictionary<string, string>? AdditionalParameters { get; set; }
}

public class CloneEnvironmentRequest
{
    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string NewName { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
    public string? ResourceGroup { get; set; }
    public string? SubscriptionId { get; set; }
}

public class ExtendExpirationRequest
{
    [Required]
    public DateTimeOffset NewExpiresAt { get; set; }
}

public class RemediateDriftRequest
{
    public List<Guid>? DriftItemIds { get; set; }
}

public class UpdateStatusRequest
{
    [Required]
    public string Status { get; set; } = string.Empty;

    public string? Reason { get; set; }
}
