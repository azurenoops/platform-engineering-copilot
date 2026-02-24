namespace Platform.Engineering.Copilot.Admin.Client.Models;

/// <summary>Template summary for catalog list view.</summary>
public class TemplateSummaryDto
{
    public Guid TemplateId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? DeploymentScope { get; set; }
    public bool HasGitSource { get; set; }
    public string? GitRepositoryUrl { get; set; }
    public DateTimeOffset? LastSyncedFromGit { get; set; }
    public bool GitAutoSync { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Full template details including content and metadata.</summary>
public class TemplateDetailDto : TemplateSummaryDto
{
    public string Content { get; set; } = string.Empty;
    public string? ParametersJson { get; set; }
    public string? GuardrailsJson { get; set; }
    public string? ComplianceFrameworks { get; set; }
    public string? Keywords { get; set; }
    public string? UseCases { get; set; }
    public string? AiSelectionHints { get; set; }
    public string? AdditionalFilesJson { get; set; }
    public bool ParametersOverridden { get; set; }
    public bool RequiresApproval { get; set; }
    public string? ApprovalSource { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public string? ApprovalComments { get; set; }
    public string? DeprecatedBy { get; set; }
    public DateTimeOffset? DeprecatedAt { get; set; }
    public string? DeprecationReason { get; set; }
    public string? GitBranch { get; set; }
    public string? GitPath { get; set; }
    public int? GitSyncIntervalMinutes { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>Parsed parameter definition for a template.</summary>
public class TemplateParameterDto
{
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public List<string> AllowedValues { get; set; } = new();
    public string? MinValue { get; set; }
    public string? MaxValue { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>Policy guardrail definition for a template.</summary>
public class TemplateGuardrailDto
{
    public string Type { get; set; } = string.Empty;
    public string Property { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>Result of template validation.</summary>
public class TemplateValidationResultDto
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>Git sync status for a template.</summary>
public class GitStatusDto
{
    public bool HasChanges { get; set; }
    public string? CurrentCommitSha { get; set; }
    public string? LatestCommitSha { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
}

/// <summary>Result of natural language template matching.</summary>
public class TemplateMatchResultDto
{
    public List<TemplateMatchDto> Matches { get; set; } = new();
    public string Query { get; set; } = string.Empty;
    public int TotalMatches { get; set; }
}

/// <summary>Individual template match entry.</summary>
public class TemplateMatchDto
{
    public Guid TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public double Score { get; set; }
    public string? Reason { get; set; }
}
