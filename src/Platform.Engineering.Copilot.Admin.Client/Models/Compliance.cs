namespace Platform.Engineering.Copilot.Admin.Client.Models;

/// <summary>Platform-wide compliance summary.</summary>
public class ComplianceSummaryDto
{
    public double OverallScore { get; set; }
    public List<FrameworkScoreDto> FrameworkScores { get; set; } = new();
    public List<EnvironmentComplianceStatusDto> EnvironmentStatuses { get; set; } = new();
    public List<ViolationDto> TopViolations { get; set; } = new();
}

/// <summary>Per-framework compliance score.</summary>
public class FrameworkScoreDto
{
    public string Framework { get; set; } = string.Empty;
    public double Score { get; set; }
}

/// <summary>Per-environment compliance status.</summary>
public class EnvironmentComplianceStatusDto
{
    public Guid EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public double ComplianceScore { get; set; }
    public int ViolationCount { get; set; }
}

/// <summary>Compliance violation summary.</summary>
public class ViolationDto
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public int AffectedEnvironments { get; set; }
}

/// <summary>Full compliance details for a specific environment.</summary>
public class EnvironmentComplianceDto
{
    public Guid EnvironmentId { get; set; }
    public string EnvironmentName { get; set; } = string.Empty;
    public double OverallScore { get; set; }
    public List<FrameworkResultDto> FrameworkResults { get; set; } = new();
    public List<ResourceComplianceDto> ResourceCompliance { get; set; } = new();
}

/// <summary>Per-framework result with control details.</summary>
public class FrameworkResultDto
{
    public string Framework { get; set; } = string.Empty;
    public double Score { get; set; }
    public List<ControlResultDto> Controls { get; set; } = new();
}

/// <summary>Individual control result.</summary>
public class ControlResultDto
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? RemediationGuidance { get; set; }
}

/// <summary>Per-resource compliance status.</summary>
public class ResourceComplianceDto
{
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public double ComplianceScore { get; set; }
    public int Violations { get; set; }
}
