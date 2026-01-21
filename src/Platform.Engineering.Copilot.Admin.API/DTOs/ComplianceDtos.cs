namespace Platform.Engineering.Copilot.Admin.API.DTOs;

public class ComplianceSummaryDto
{
    public decimal OverallScore { get; set; }
    public int TotalControls { get; set; }
    public int CompliantControls { get; set; }
    public int NonCompliantControls { get; set; }
    public List<FrameworkScoreDto> FrameworkScores { get; set; } = new();
    public List<EnvironmentComplianceStatusDto> EnvironmentStatuses { get; set; } = new();
    public List<ControlViolationDto> TopViolations { get; set; } = new();
}

public class FrameworkScoreDto
{
    public string Framework { get; set; } = string.Empty;
    public decimal Score { get; set; }
    public int CompliantControls { get; set; }
    public int TotalControls { get; set; }
}

public class EnvironmentComplianceStatusDto
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal ComplianceScore { get; set; }
    public int CriticalViolations { get; set; }
    public int HighViolations { get; set; }
    public DateTime LastScannedAt { get; set; }
}

public class ControlViolationDto
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int AffectedResourceCount { get; set; }
}

public class EnvironmentComplianceDetailDto
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public decimal OverallScore { get; set; }
    public decimal ComplianceScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime LastScannedAt { get; set; }
    public List<FrameworkScoreDto> FrameworkScores { get; set; } = new();
    public List<ControlResultDto> ControlResults { get; set; } = new();
    public List<ControlComplianceDetailDto> Controls { get; set; } = new();
    public List<ResourceComplianceDto> Resources { get; set; } = new();
}

public class ControlResultDto
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AffectedResources { get; set; } = new();
}

public class ControlComplianceDetailDto
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlName { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> AffectedResources { get; set; } = new();
    public string? RemediationGuidance { get; set; }
}

public class ResourceComplianceDto
{
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
    public int ViolationCount { get; set; }
    public List<string> FailedControls { get; set; } = new();
}
