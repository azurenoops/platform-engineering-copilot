namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// Represents an ATO (Authority to Operate) compliance finding for an Azure resource
/// </summary>
public class AtoFinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public AtoFindingType FindingType { get; set; }
    public AtoFindingSeverity Severity { get; set; }
    public AtoComplianceStatus ComplianceStatus { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string RemediationGuidance { get; set; } = string.Empty;
    public bool IsAutoRemediable { get; set; }
    public List<string> AffectedControls { get; set; } = new();
    public List<string> AffectedNistControls { get; set; } = new();
    public List<string> ComplianceFrameworks { get; set; } = new();
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public string? Evidence { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public bool IsRemediable { get; set; }
    public List<AtoRemediationAction> RemediationActions { get; set; } = new();
    public AtoRemediationStatus RemediationStatus { get; set; } = AtoRemediationStatus.NotStarted;
    public string? RemediationNotes { get; set; }
    public DateTime? RemediationStartTime { get; set; }
    public DateTime? RemediationEndTime { get; set; }
}

/// <summary>
/// Represents a remediation action for an ATO finding
/// </summary>
public class AtoRemediationAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public AtoRemediationActionType ActionType { get; set; }
    public AtoRemediationComplexity Complexity { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public bool RequiresApproval { get; set; }
    public List<string> Prerequisites { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public string? ScriptPath { get; set; }
    public string? ToolCommand { get; set; }
    public List<string> AffectedResources { get; set; } = new();
    public AtoRemediationImpact Impact { get; set; }
}

/// <summary>
/// Represents the result of scanning a Resource Group for ATO findings
/// </summary>
public class AtoScanResult
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public DateTime ScanStartTime { get; set; }
    public DateTime ScanEndTime { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan ScanDuration => ScanEndTime - ScanStartTime;
    public AtoScanStatus Status { get; set; }
    public List<AtoFinding> Findings { get; set; } = new();
    public AtoScanSummary Summary { get; set; } = new();
    public int TotalResourcesScanned { get; set; }
    public List<string> ResourceTypesScanned { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> ScanMetadata { get; set; } = new();
}

/// <summary>
/// Summary statistics for an ATO scan
/// </summary>
public class AtoScanSummary
{
    public int TotalResourcesScanned { get; set; }
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
    public int HighFindings { get; set; }
    public int MediumFindings { get; set; }
    public int LowFindings { get; set; }
    public int InformationalFindings { get; set; }
    public int RemediableFindings { get; set; }
    public int NonCompliantResources { get; set; }
    public double ComplianceScore { get; set; }
    public Dictionary<string, int> FindingsByType { get; set; } = new();
    public Dictionary<string, int> FindingsByFramework { get; set; } = new();
}

/// <summary>
/// Configuration for ATO compliance scanning
/// </summary>
public class AtoScanConfiguration
{
    public List<string> ComplianceFrameworks { get; set; } = new() { "NIST-800-53", "SOC2", "ISO-27001", "GDPR" };
    public List<AtoFindingType> ScanTypes { get; set; } = Enum.GetValues<AtoFindingType>().ToList();
    public AtoFindingSeverity MinimumSeverity { get; set; } = AtoFindingSeverity.Low;
    public bool IncludeInformational { get; set; } = true;
    public bool EnableAutoRemediation { get; set; } = false;
    public TimeSpan ScanTimeout { get; set; } = TimeSpan.FromMinutes(60);
    public List<string> ExcludedResourceTypes { get; set; } = new();
    public List<string> ExcludedResourceNames { get; set; } = new();
    public Dictionary<string, object> ScannerOptions { get; set; } = new();
}

/// <summary>
/// Request to scan a Resource Group for ATO compliance
/// </summary>
public class AtoScanRequest
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public List<string> Frameworks { get; set; } = new() { "NIST-800-53" };
    public AtoScanConfiguration Configuration { get; set; } = new();
    public bool IncludeRemediation { get; set; } = true;
    public string RequestedBy { get; set; } = string.Empty;
    public string? Justification { get; set; }
}

/// <summary>
/// Request to remediate ATO findings
/// </summary>
public class AtoRemediationRequest
{
    public string Id { get; set; } = string.Empty;
    public string ScanResultId { get; set; } = string.Empty;
    public List<string> FindingIds { get; set; } = new();
    public List<string> RemediationActionIds { get; set; } = new();
    public List<AtoRemediationAction> RemediationActions { get; set; } = new();
    public bool RequireApproval { get; set; } = true;
    public bool RequiresApproval { get; set; } = true;
    public string RequestedBy { get; set; } = string.Empty;
    public string? Justification { get; set; }
    public Dictionary<string, object> Parameters { get; set; } = new();
    public TimeSpan EstimatedDuration { get; set; }
    public int Priority { get; set; }
}

#region Enums

public enum AtoFindingType
{
    Security,
    Compliance,
    Configuration,
    AccessControl,
    DataProtection,
    NetworkSecurity,
    Monitoring,
    Logging,
    Backup,
    Encryption,
    PatchManagement,
    ResourceManagement,
    ContingencyPlanning,
    IdentityManagement,
    ConfigurationManagement,
    IncidentResponse,
    RiskAssessment,
    SecurityAssessment
}

public enum AtoFindingSeverity
{
    Informational = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum AtoComplianceStatus
{
    Compliant,
    NonCompliant,
    PartiallyCompliant,
    Unknown,
    NotApplicable,
    ManualReviewRequired
}

public enum AtoScanStatus
{
    NotStarted,
    InProgress,
    Completed,
    CompletedWithErrors,
    Failed,
    Cancelled,
    TimedOut
}

public enum AtoRemediationActionType
{
    ConfigurationChange,
    ResourceDeployment,
    PolicyApplication,
    AccessControlUpdate,
    NetworkSecurityUpdate,
    EncryptionConfiguration,
    MonitoringSetup,
    LoggingConfiguration,
    BackupConfiguration,
    ScriptExecution,
    ManualAction
}

public enum AtoRemediationComplexity
{
    Simple,
    Moderate,
    Complex,
    Expert
}

public enum AtoRemediationStatus
{
    NotStarted,
    InProgress,
    Completed,
    Failed,
    PartiallyCompleted,
    RequiresApproval,
    Cancelled
}

public enum AtoRemediationImpact
{
    NoImpact,
    LowImpact,
    MediumImpact,
    HighImpact,
    ServiceDisruption
}

#endregion

#region ATO Compliance Reporting Models

/// <summary>
/// Comprehensive ATO compliance report
/// </summary>
public class AtoComplianceReport
{
    public ComplianceReportMetadata Metadata { get; set; } = new();
    public ComplianceSummary Summary { get; set; } = new();
    public List<AtoFinding> Findings { get; set; } = new();
    public List<NistControlCompliance> NistControls { get; set; } = new();
    public List<ComplianceAlert> Alerts { get; set; } = new();
    public RemediationProgress RemediationProgress { get; set; } = new();
    public List<ComplianceTrend> Trends { get; set; } = new();
    public RiskAssessment RiskAssessment { get; set; } = new();
    public List<ResourceCompliance> ResourceCompliance { get; set; } = new();
    public List<FrameworkCompliance> FrameworkCompliance { get; set; } = new();
}

/// <summary>
/// Metadata for compliance report
/// </summary>
public class ComplianceReportMetadata
{
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string GeneratedBy { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public string SubscriptionId { get; set; } = string.Empty;
    public string ReportType { get; set; } = string.Empty;
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
    public TimeSpan GenerationTime { get; set; }
}

/// <summary>
/// Resource type compliance information
/// </summary>
public class ResourceCompliance
{
    public string ResourceType { get; set; } = string.Empty;
    public int TotalResources { get; set; }
    public int CompliantResources { get; set; }
    public int NonCompliantResources { get; set; }
    public int TotalFindings { get; set; }
    public List<FindingSeverityCount> FindingsBySeverity { get; set; } = new();
    public List<string> CommonViolations { get; set; } = new();
    public double RiskScore { get; set; }
}

/// <summary>
/// Framework compliance status
/// </summary>
public class FrameworkCompliance
{
    public string FrameworkName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int TotalControls { get; set; }
    public int CompliantControls { get; set; }
    public int NonCompliantControls { get; set; }
    public double ComplianceScore { get; set; }
    public DateTime LastAssessment { get; set; } = DateTime.UtcNow;
    public List<ControlFamilyCompliance> ControlFamilies { get; set; } = new();
}

/// <summary>
/// Control family compliance information
/// </summary>
public class ControlFamilyCompliance
{
    public string FamilyId { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public int TotalControls { get; set; }
    public int CompliantControls { get; set; }
}

/// <summary>
/// Finding severity count
/// </summary>
public class FindingSeverityCount
{
    public AtoFindingSeverity Severity { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Compliance summary information
/// </summary>
public class ComplianceSummary
{
    public double OverallComplianceScore { get; set; }
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
    public int HighFindings { get; set; }
    public int MediumFindings { get; set; }
    public int LowFindings { get; set; }
    public int TotalResources { get; set; }
    public int CompliantResources { get; set; }
    public int NonCompliantResources { get; set; }
    public int RemediableFindings { get; set; }
    public int AutoRemediableFindings { get; set; }
}

/// <summary>
/// NIST control compliance information
/// </summary>
public class NistControlCompliance
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlTitle { get; set; } = string.Empty;
    public string ControlFamily { get; set; } = string.Empty;
    public ComplianceStatus Status { get; set; }
    public int TotalApplicableResources { get; set; }
    public int CompliantResources { get; set; }
    public List<string> ViolatingResources { get; set; } = new();
    public List<string> ApplicableResourceTypes { get; set; } = new();
    public DateTime LastAssessed { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Compliance alert information
/// </summary>
public class ComplianceAlert
{
    public string? AlertId { get; set; }
    public string? ControlId { get; set; }
    public AlertType Type { get; set; }
    public AlertSeverity Severity { get; set; }
    public string? SeverityString { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> AffectedResources { get; set; } = new();
    public string ActionRequired { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTimeOffset? AlertTime { get; set; }
    public bool Acknowledged { get; set; }
}

/// <summary>
/// Compliance trend data point
/// </summary>
public class ComplianceTrend
{
    public DateTimeOffset Timestamp { get; set; }
    public double ComplianceScore { get; set; }
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
    public int ResolvedFindings { get; set; }
    public int NewFindings { get; set; }
}

/// <summary>
/// Remediation progress information
/// </summary>
public class RemediationProgress
{
    public string SubscriptionId { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public int TotalActivities { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public double SuccessRate { get; set; }
    public int TotalRemediableFindings { get; set; }
    public int RemediationInProgress { get; set; }
    public int RemediationCompleted { get; set; }
    public int RemediationFailed { get; set; }
    public List<RemediationItem> ActiveRemediations { get; set; } = new();
    public List<RemediationItem> RecentlyCompleted { get; set; } = new();
    public List<RemediationActivity> RecentActivities { get; set; } = new();
    public TimeSpan AverageRemediationTime { get; set; }
    public int AutoRemediationsExecuted { get; set; }
}

/// <summary>
/// Remediation activity for progress tracking
/// </summary>
public class RemediationActivity
{
    public string ExecutionId { get; set; } = string.Empty;
    public string FindingId { get; set; } = string.Empty;
    public RemediationExecutionStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>
/// Individual remediation item
/// </summary>
public class RemediationItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FindingId { get; set; } = string.Empty;
    public string? ControlId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string? Priority { get; set; }
    public AtoRemediationStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsAutomated { get; set; }
    public bool AutomationAvailable { get; set; }
    public TimeSpan? EstimatedEffort { get; set; }
    public string? ExecutedBy { get; set; }
    public string? Notes { get; set; }
    public List<RemediationStep>? Steps { get; set; }
    public List<string>? ValidationSteps { get; set; }
    public RollbackPlan? RollbackPlan { get; set; }
    public List<string>? Dependencies { get; set; }
}

/// <summary>
/// Risk assessment information
/// </summary>
public class RiskAssessment
{
    public string? AssessmentId { get; set; }
    public string? SubscriptionId { get; set; }
    public DateTimeOffset AssessmentDate { get; set; } = DateTimeOffset.UtcNow;
    public double RiskScore { get; set; }
    public double OverallRiskScore { get; set; }
    public RiskLevel OverallRiskLevel { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public string? RiskLevelString { get; set; }
    public string RiskRating { get; set; } = string.Empty;
    public Dictionary<string, CategoryRisk> RiskCategories { get; set; } = new();
    public List<string> TopRiskFactors { get; set; } = new();
    public List<string> TopRisks { get; set; } = new();
    public List<RiskMitigation>? MitigationRecommendations { get; set; }
    public int HighRiskResources { get; set; }
    public string? RiskTrend { get; set; }
    public string? ExecutiveSummary { get; set; }
    public DateTime LastAssessment { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Risk category information
/// </summary>
public class RiskCategory
{
    public string Category { get; set; } = string.Empty;
    public int FindingCount { get; set; }
    public double RiskScore { get; set; }
    public RiskLevel Level { get; set; }
    public List<string> MainConcerns { get; set; } = new();
}

#region Additional Enums

public enum ComplianceStatus
{
    Compliant,
    NonCompliant,
    PartiallyCompliant,
    Unknown,
    NotApplicable
}

public enum AlertType
{
    NewCriticalFinding,
    ComplianceScoreDecline,
    SlaViolation,
    RemediationOverdue,
    ComplianceFrameworkUpdate,
    SecurityBaseline
}

public enum AlertSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

#endregion

#region ATO Compliance Assessment Models

/// <summary>
/// Comprehensive ATO compliance assessment result
/// </summary>
public class AtoComplianceAssessment
{
    public required string AssessmentId { get; set; }
    public required string SubscriptionId { get; set; }
    public DateTimeOffset StartTime { get; set; }
    public DateTimeOffset EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, ControlFamilyAssessment> ControlFamilyResults { get; set; } = new();
    public double OverallComplianceScore { get; set; }
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
    public int HighFindings { get; set; }
    public int MediumFindings { get; set; }
    public int LowFindings { get; set; }
    public string? ExecutiveSummary { get; set; }
    public RiskProfile? RiskProfile { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Assessment results for a specific NIST control family
/// </summary>
public class ControlFamilyAssessment
{
    public required string ControlFamily { get; set; }
    public required string FamilyName { get; set; }
    public DateTimeOffset AssessmentTime { get; set; }
    public int TotalControls { get; set; }
    public int PassedControls { get; set; }
    public double ComplianceScore { get; set; }
    public List<AtoFinding> Findings { get; set; } = new();
}

/// <summary>
/// Continuous compliance monitoring status
/// </summary>
public class ContinuousComplianceStatus
{
    public required string SubscriptionId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool MonitoringEnabled { get; set; }
    public DateTimeOffset LastCheckTime { get; set; }
    public DateTimeOffset NextCheckTime { get; set; }
    public double ComplianceScore { get; set; }
    public string TrendDirection { get; set; } = "Stable";
    public int ActiveAlerts { get; set; }
    public int ResolvedToday { get; set; }
    public Dictionary<string, ControlMonitoringStatus> ControlStatuses { get; set; } = new();
    public double ComplianceDriftPercentage { get; set; }
    public int AlertCount { get; set; }
    public int AutoRemediationCount { get; set; }
}

/// <summary>
/// Evidence model used by evidence collectors
/// </summary>
public class ComplianceEvidence
{
    public string EvidenceId { get; set; } = Guid.NewGuid().ToString();
    public required string EvidenceType { get; set; }
    public required string ControlId { get; set; }
    public required string ResourceId { get; set; }
    public DateTimeOffset CollectedAt { get; set; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> Data { get; set; } = new();
    public string? Screenshot { get; set; }
    public string? LogExcerpt { get; set; }
    public string? ConfigSnapshot { get; set; }
}

/// <summary>
/// Real-time monitoring status for a specific control
/// </summary>
public class ControlMonitoringStatus
{
    public required string ControlId { get; set; }
    public DateTimeOffset LastChecked { get; set; }
    public string Status { get; set; } = "Unknown";
    public bool DriftDetected { get; set; }
    public bool AutoRemediationEnabled { get; set; }
    public List<ComplianceAlert> Alerts { get; set; } = new();
}

/// <summary>
/// Evidence package for compliance attestation
/// </summary>
public class EvidencePackage
{
    public required string PackageId { get; set; }
    public required string SubscriptionId { get; set; }
    public required string ControlFamily { get; set; }
    public DateTimeOffset CollectionDate { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset CollectionStartTime { get; set; }
    public DateTimeOffset CollectionEndTime { get; set; }
    public TimeSpan CollectionDuration { get; set; }
    public List<ComplianceEvidence> Evidence { get; set; } = new();
    public int TotalItems { get; set; }
    public string? Summary { get; set; }
    public double CompletenessScore { get; set; }
    public string? AttestationStatement { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Comprehensive remediation plan
/// </summary>
public class RemediationPlan
{
    public required string PlanId { get; set; }
    public required string SubscriptionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int TotalFindings { get; set; }
    public List<RemediationItem> RemediationItems { get; set; } = new();
    public TimeSpan EstimatedEffort { get; set; }
    public string Priority { get; set; } = "Medium";
    public ImplementationTimeline? Timeline { get; set; }
    public double ProjectedRiskReduction { get; set; }
    public string? ExecutiveSummary { get; set; }
}

/// <summary>
/// Step in remediation process
/// </summary>
public class RemediationStep
{
    public int Order { get; set; }
    public required string Description { get; set; }
    public string? Command { get; set; }
    public string? AutomationScript { get; set; }
}

/// <summary>
/// Rollback plan for remediation
/// </summary>
public class RollbackPlan
{
    public required string Description { get; set; }
    public List<string> Steps { get; set; } = new();
    public TimeSpan EstimatedRollbackTime { get; set; }
}

/// <summary>
/// Implementation timeline for remediation
/// </summary>
public class ImplementationTimeline
{
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public List<TimelinePhase> Phases { get; set; } = new();
    public List<TimelineMilestone> Milestones { get; set; } = new();
}

/// <summary>
/// Milestone in implementation timeline
/// </summary>
public class TimelineMilestone
{
    public DateTimeOffset Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> Deliverables { get; set; } = new();
}

/// <summary>
/// Phase in implementation timeline
/// </summary>
public class TimelinePhase
{
    public required string Name { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public List<RemediationItem> Items { get; set; } = new();
}

/// <summary>
/// Compliance timeline with historical data
/// </summary>
public class ComplianceTimeline
{
    public required string SubscriptionId { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public double CurrentScore { get; set; }
    public double PreviousScore { get; set; }
    public double ScoreChange { get; set; }
    public string TrendDirection { get; set; } = "Stable";
    public List<ComplianceDataPoint> DataPoints { get; set; } = new();
    public List<ComplianceEvent> MajorEvents { get; set; } = new();
    public ComplianceTrends? Trends { get; set; }
    public List<string> SignificantEvents { get; set; } = new();
    public List<string> Insights { get; set; } = new();
}

/// <summary>
/// Compliance event for timeline
/// </summary>
public class ComplianceEvent
{
    public DateTimeOffset Date { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Impact { get; set; } = string.Empty;
}

/// <summary>
/// Data point in compliance timeline
/// </summary>
public class ComplianceDataPoint
{
    public DateTimeOffset Date { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public double Score { get; set; }
    public double ComplianceScore { get; set; }
    public int FindingsCount { get; set; }
    public int ControlsFailed { get; set; }
    public int ControlsPassed { get; set; }
    public int ActiveFindings { get; set; }
    public int RemediatedFindings { get; set; }
    public List<string> Events { get; set; } = new();
}

/// <summary>
/// Compliance trends analysis
/// </summary>
public class ComplianceTrends
{
    public required string ComplianceScoreTrend { get; set; }
    public required string FindingsTrend { get; set; }
    public required string RemediationRate { get; set; }
}

/// <summary>
/// Risk assessment for specific category
/// </summary>
public class CategoryRisk
{
    public required string Category { get; set; }
    public double RiskScore { get; set; }
    public double Score { get; set; }
    public required string RiskLevel { get; set; }
    public int FindingCount { get; set; }
    public List<string> TopRisks { get; set; } = new();
    public List<string> Vulnerabilities { get; set; } = new();
    public List<string> Mitigations { get; set; } = new();
}

/// <summary>
/// Risk mitigation recommendation
/// </summary>
public class RiskMitigation
{
    public required string Risk { get; set; }
    public required string Recommendation { get; set; }
    public required string Priority { get; set; }
    public TimeSpan EstimatedEffort { get; set; }
}

/// <summary>
/// Risk profile summary
/// </summary>
public class RiskProfile
{
    public required string RiskLevel { get; set; }
    public double RiskScore { get; set; }
    public List<string> TopRisks { get; set; } = new();
}

/// <summary>
/// Compliance certificate for successful assessments
/// </summary>
public class ComplianceCertificate
{
    public required string CertificateId { get; set; }
    public required string SubscriptionId { get; set; }
    public DateTimeOffset IssuedDate { get; set; }
    public DateTimeOffset ExpirationDate { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    public string ComplianceStatus { get; set; } = string.Empty;
    public string CertificationLevel { get; set; } = string.Empty;
    public double ComplianceScore { get; set; }
    public int TotalControls { get; set; }
    public int CertifiedControls { get; set; }
    public List<string> CertifiedFrameworks { get; set; } = new();
    public List<string> ControlFamiliesCovered { get; set; } = new();
    public List<ComplianceAttestation> Attestations { get; set; } = new();
    public string? AttestationStatement { get; set; }
    public string? SignatoryInformation { get; set; }
    public TimeSpan ValidityPeriod { get; set; }
    public string? VerificationHash { get; set; }
}

/// <summary>
/// Attestation for control family compliance
/// </summary>
public class ComplianceAttestation
{
    public required string ControlFamily { get; set; }
    public required string ComplianceLevel { get; set; }
    public DateTimeOffset AttestationDate { get; set; }
    public List<string> ValidatedControls { get; set; } = new();
    public List<string> Exceptions { get; set; } = new();
}

/// <summary>
/// Monitored control for continuous compliance
/// </summary>
public class MonitoredControl
{
    public required string ControlId { get; set; }
    public DateTimeOffset LastChecked { get; set; }
    public string ComplianceStatus { get; set; } = "Unknown";
    public bool DriftDetected { get; set; }
    public bool AutoRemediationEnabled { get; set; }
}

#endregion

#region ATO Remediation Engine Models

/// <summary>
/// Options for remediation plan generation
/// </summary>
public class RemediationPlanOptions
{
    public AtoFindingSeverity MinimumSeverity { get; set; } = AtoFindingSeverity.Low;
    public List<string> IncludeControlFamilies { get; set; } = new();
    public List<string> ExcludeControlFamilies { get; set; } = new();
    public bool IncludeOnlyAutomatable { get; set; } = false;
    public bool OptimizeForCost { get; set; } = false;
    public bool OptimizeForSpeed { get; set; } = false;
    public bool GroupByResource { get; set; } = false;
    public int MaxConcurrentRemediations { get; set; } = 5;
    public TimeSpan? MaxDuration { get; set; }
}

/// <summary>
/// Options for remediation execution
/// </summary>
public class RemediationExecutionOptions
{
    public bool DryRun { get; set; } = false;
    public bool RequireApproval { get; set; } = true;
    public bool AutoValidate { get; set; } = true;
    public bool AutoRollbackOnFailure { get; set; } = true;
    public bool CaptureSnapshots { get; set; } = true;
    public Dictionary<string, object> CustomParameters { get; set; } = new();
    public List<string> NotificationRecipients { get; set; } = new();
    public string? ExecutedBy { get; set; }
    public string? Justification { get; set; }
}

/// <summary>
/// Options for batch remediation
/// </summary>
public class BatchRemediationOptions
{
    public bool FailFast { get; set; } = false;
    public int MaxConcurrentRemediations { get; set; } = 3;
    public bool PreserveOrder { get; set; } = false;
    public bool ContinueOnError { get; set; } = true;
    public TimeSpan? Timeout { get; set; }
    public RemediationExecutionOptions ExecutionOptions { get; set; } = new();
}

/// <summary>
/// Result of a remediation execution
/// </summary>
public class RemediationExecution
{
    public required string ExecutionId { get; set; }
    public required string FindingId { get; set; }
    public required string SubscriptionId { get; set; }
    public required string ResourceId { get; set; }
    public RemediationExecutionStatus Status { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Error { get; set; }
    public List<string> ChangesApplied { get; set; } = new();
    public string? BackupId { get; set; }
    public List<RemediationStep> StepsExecuted { get; set; } = new();
    public RemediationSnapshot? BeforeSnapshot { get; set; }
    public RemediationSnapshot? AfterSnapshot { get; set; }
    public RemediationValidationResult? ValidationResult { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public string? ExecutedBy { get; set; }
    public bool RequiredApproval { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
}

/// <summary>
/// Result of batch remediation
/// </summary>
public class BatchRemediationResult
{
    public required string BatchId { get; set; }
    public required string SubscriptionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public TimeSpan Duration { get; set; }
    public int TotalRemediations { get; set; }
    public int SuccessfulRemediations { get; set; }
    public int FailedRemediations { get; set; }
    public int SkippedRemediations { get; set; }
    public List<RemediationExecution> Executions { get; set; } = new();
    public BatchRemediationSummary Summary { get; set; } = new();
}

/// <summary>
/// Summary of batch remediation
/// </summary>
public class BatchRemediationSummary
{
    public double SuccessRate { get; set; }
    public int CriticalFindingsRemediated { get; set; }
    public int HighFindingsRemediated { get; set; }
    public double EstimatedRiskReduction { get; set; }
    public List<string> ControlFamiliesAffected { get; set; } = new();
    public Dictionary<string, int> RemediationsByType { get; set; } = new();
}

/// <summary>
/// Result of remediation validation
/// </summary>
public class RemediationValidationResult
{
    public required string ValidationId { get; set; }
    public required string ExecutionId { get; set; }
    public DateTimeOffset ValidatedAt { get; set; }
    public bool IsValid { get; set; }
    public List<ValidationCheck> Checks { get; set; } = new();
    public string? FailureReason { get; set; }
    public bool RequiresManualReview { get; set; }
}

/// <summary>
/// Individual validation check
/// </summary>
public class ValidationCheck
{
    public required string CheckName { get; set; }
    public required string Description { get; set; }
    public bool Passed { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> CheckData { get; set; } = new();
}

/// <summary>
/// Result of remediation rollback
/// </summary>
public class RemediationRollbackResult
{
    public required string RollbackId { get; set; }
    public required string ExecutionId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> StepsExecuted { get; set; } = new();
}

/// <summary>
/// Remediation impact analysis
/// </summary>
public class RemediationImpactAnalysis
{
    public required string AnalysisId { get; set; }
    public required string SubscriptionId { get; set; }
    public DateTimeOffset AnalyzedAt { get; set; }
    public int TotalFindings { get; set; }
    public int AutomatableFindings { get; set; }
    public int ManualFindings { get; set; }
    public TimeSpan EstimatedTotalDuration { get; set; }
    public double EstimatedCost { get; set; }
    public double CurrentRiskScore { get; set; }
    public double ProjectedRiskScore { get; set; }
    public double RiskReduction { get; set; }
    public List<ResourceImpact> ResourceImpacts { get; set; } = new();
    public List<string> Dependencies { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// Impact on specific resource
/// </summary>
public class ResourceImpact
{
    public required string ResourceId { get; set; }
    public required string ResourceType { get; set; }
    public int FindingsCount { get; set; }
    public bool RequiresDowntime { get; set; }
    public TimeSpan? EstimatedDowntime { get; set; }
    public string ImpactLevel { get; set; } = "Low";
    public List<string> AffectedCapabilities { get; set; } = new();
}

/// <summary>
/// Manual remediation guidance
/// </summary>
public class ManualRemediationGuide
{
    public required string GuideId { get; set; }
    public required string FindingId { get; set; }
    public required string Title { get; set; }
    public required string Overview { get; set; }
    public List<RemediationStep> Steps { get; set; } = new();
    public List<string> Prerequisites { get; set; } = new();
    public List<string> ValidationSteps { get; set; } = new();
    public RollbackPlan? RollbackPlan { get; set; }
    public TimeSpan EstimatedDuration { get; set; }
    public string SkillLevel { get; set; } = "Intermediate";
    public List<string> RequiredPermissions { get; set; } = new();
    public List<string> References { get; set; } = new();
}

/// <summary>
/// Remediation workflow status
/// </summary>
public class RemediationWorkflowStatus
{
    public required string WorkflowId { get; set; }
    public required string FindingId { get; set; }
    public required string SubscriptionId { get; set; }
    public RemediationWorkflowState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public int TotalSteps { get; set; }
    public int CompletedSteps { get; set; }
    public double ProgressPercentage { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsApproved { get; set; }
    public string? AssignedTo { get; set; }
}

/// <summary>
/// Remediation approval result
/// </summary>
public class RemediationApprovalResult
{
    public required string RemediationId { get; set; }
    public bool Approved { get; set; }
    public required string ApprovedBy { get; set; }
    public DateTimeOffset ApprovedAt { get; set; }
    public string? Comments { get; set; }
    public bool CanProceed { get; set; }
}

/// <summary>
/// Remediation schedule result
/// </summary>
public class RemediationScheduleResult
{
    public required string ScheduleId { get; set; }
    public required string FindingId { get; set; }
    public DateTimeOffset ScheduledTime { get; set; }
    public bool IsScheduled { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Remediation history for audit purposes
/// </summary>
public class RemediationHistory
{
    public required string SubscriptionId { get; set; }
    public DateTimeOffset StartDate { get; set; }
    public DateTimeOffset EndDate { get; set; }
    public List<RemediationExecution> Executions { get; set; } = new();
    public List<RemediationMetric> Metrics { get; set; } = new();
    public Dictionary<string, int> RemediationsByControlFamily { get; set; } = new();
    public Dictionary<string, int> RemediationsBySeverity { get; set; } = new();
}

/// <summary>
/// Remediation metric for tracking
/// </summary>
public class RemediationMetric
{
    public DateTimeOffset Date { get; set; }
    public int TotalRemediations { get; set; }
    public int SuccessfulRemediations { get; set; }
    public double AverageRemediationTime { get; set; }
    public double ComplianceImprovement { get; set; }
}

/// <summary>
/// Snapshot of resource state before/after remediation
/// </summary>
public class RemediationSnapshot
{
    public required string SnapshotId { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public required string ResourceId { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Remediation execution status
/// </summary>
public enum RemediationExecutionStatus
{
    Pending,
    Approved,
    Rejected,
    InProgress,
    Validating,
    Completed,
    Failed,
    RolledBack,
    Cancelled
}

/// <summary>
/// Remediation workflow state
/// </summary>
public enum RemediationWorkflowState
{
    Created,
    PendingApproval,
    Approved,
    Rejected,
    Scheduled,
    InProgress,
    Validating,
    Completed,
    Failed,
    RolledBack
}

public record NistCatalogRoot
{
    public NistCatalog? Catalog { get; init; }
}

public record NistCatalog
{
    public CatalogMetadata? Metadata { get; init; }
    public IReadOnlyList<ControlGroup>? Groups { get; init; }
    public string? Uuid { get; init; }
}

public record CatalogMetadata
{
    public string? Title { get; init; }
    public string? Version { get; init; }
    public DateTime LastModified { get; init; }
    public string? OscalVersion { get; init; }
}

public record ControlGroup
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<NistControl>? Controls { get; init; }
}

public record NistControl
{
    public string? Id { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<ControlProperty>? Props { get; init; }
    public IReadOnlyList<ControlPart>? Parts { get; init; }
    public IReadOnlyList<NistControl>? Controls { get; init; } // Sub-controls/enhancements
}

public record ControlProperty
{
    public string? Name { get; init; }
    public string? Value { get; init; }
    public string? Class { get; init; }
}

public record ControlPart
{
    public string? Name { get; init; }
    public string? Id { get; init; }
    public string? Prose { get; init; }
    public IReadOnlyList<ControlPart>? Parts { get; init; }
}

public record ControlEnhancement
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Statement { get; init; }
    public required string Guidance { get; init; }
    public required IReadOnlyList<string> Objectives { get; init; }
    public required DateTime LastUpdated { get; init; }
}

#endregion

#endregion