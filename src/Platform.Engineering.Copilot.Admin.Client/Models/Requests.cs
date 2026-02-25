namespace Platform.Engineering.Copilot.Admin.Client.Models;

// ===== Template Requests =====

/// <summary>Request to create a new template.</summary>
public class CreateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public string? Category { get; set; }
    public string? Format { get; set; }
    public string? DeploymentScope { get; set; }
    public string? ParametersJson { get; set; }
    public string? GuardrailsJson { get; set; }
    public string? ComplianceFrameworks { get; set; }
    public string? Keywords { get; set; }
    public string? GitRepoUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? GitPath { get; set; }
    public bool GitAutoSync { get; set; }
}

/// <summary>Request to update an existing template.</summary>
public class UpdateTemplateRequest
{
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public string? Content { get; set; }
    public string? Category { get; set; }
    public string? Format { get; set; }
    public string? DeploymentScope { get; set; }
    public string? ParametersJson { get; set; }
    public string? GuardrailsJson { get; set; }
    public string? ComplianceFrameworks { get; set; }
    public string? Keywords { get; set; }
    public string? UseCases { get; set; }
    public string? AiSelectionHints { get; set; }
    public string? GitRepoUrl { get; set; }
    public string? GitBranch { get; set; }
    public string? GitPath { get; set; }
    public bool? GitAutoSync { get; set; }
}

/// <summary>Request to approve a template.</summary>
public class ApprovalRequest
{
    public string ApprovalSource { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public string? Comments { get; set; }
}

/// <summary>Request to validate a template.</summary>
public class ValidateTemplateRequest
{
    public string? Name { get; set; }
    public string? Content { get; set; }
    public string? Format { get; set; }
}

/// <summary>Request to parse Bicep parameters from content.</summary>
public class ParseBicepParametersRequest
{
    public string BicepContent { get; set; } = string.Empty;
}

/// <summary>Request to parse Bicep parameters from a Git repository.</summary>
public class ParseBicepFromGitRequest
{
    public string GitRepoUrl { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public string? FilePath { get; set; }
}

/// <summary>Request to import a template from Git.</summary>
public class ImportFromGitRequest
{
    public string GitRepoUrl { get; set; } = string.Empty;
    public string? Branch { get; set; }
    public string? FilePath { get; set; }
    public string? Name { get; set; }
    public string? Category { get; set; }
    public bool GitAutoSync { get; set; }
}

/// <summary>Request for natural language template matching.</summary>
public class TemplateMatchRequest
{
    public string Description { get; set; } = string.Empty;
    public double MinScore { get; set; } = 0.3;
    public int MaxResults { get; set; } = 5;
}

// ===== Environment Requests =====

/// <summary>Request to create a new environment.</summary>
public class CreateEnvironmentRequest
{
    public Guid TemplateId { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? ParameterValuesJson { get; set; }
    public string? TagsJson { get; set; }
    public string? OwnerEmail { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool AutoDelete { get; set; }
}

/// <summary>Request to scale an environment.</summary>
public class ScaleEnvironmentRequest
{
    public int? NodeCount { get; set; }
    public int? ReplicaCount { get; set; }
    public string? Sku { get; set; }
    public string? Tier { get; set; }
    public string? AdditionalParameters { get; set; }
}

/// <summary>Request to clone an environment.</summary>
public class CloneEnvironmentRequest
{
    public string NewName { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ResourceGroup { get; set; }
    public string? SubscriptionId { get; set; }
}

/// <summary>Request to extend environment expiration.</summary>
public class ExtendExpirationRequest
{
    public DateTimeOffset NewExpiresAt { get; set; }
}

/// <summary>Request to remediate specific drift items.</summary>
public class RemediateDriftRequest
{
    public List<Guid> DriftItemIds { get; set; } = new();
}
