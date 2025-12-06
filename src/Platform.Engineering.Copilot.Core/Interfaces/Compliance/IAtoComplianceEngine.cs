using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Comprehensive ATO Compliance Engine that orchestrates compliance scanning, 
/// evidence collection, continuous monitoring, and automated remediation
/// </summary>
public interface IAtoComplianceEngine
{
    Task<AtoComplianceAssessment> RunComprehensiveAssessmentAsync(
        string subscriptionId, 
        IProgress<AssessmentProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<AtoComplianceAssessment> RunComprehensiveAssessmentAsync(
        string subscriptionId,
        string? resourceGroupName,
        IProgress<AssessmentProgress>? progress = null,
        CancellationToken cancellationToken = default);
    
    Task<AtoComplianceAssessment?> GetLatestAssessmentAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);
    
    Task<ContinuousComplianceStatus> GetContinuousComplianceStatusAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    Task<EvidencePackage> CollectComplianceEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        IProgress<EvidenceCollectionProgress>? progress = null,
        CancellationToken cancellationToken = default);
    Task<ComplianceTimeline> GetComplianceTimelineAsync(string subscriptionId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default);
    Task<RiskAssessment> PerformRiskAssessmentAsync(string subscriptionId, CancellationToken cancellationToken = default);
    Task<ComplianceCertificate> GenerateComplianceCertificateAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    // ==================== Data Access Methods (delegated to repository) ====================
    
    /// <summary>
    /// Get historical compliance assessments for analytics/history view
    /// </summary>
    Task<IReadOnlyList<ComplianceAssessmentSummary>> GetComplianceHistoryAsync(
        string subscriptionId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get assessment audit log (all assessments regardless of status)
    /// </summary>
    Task<IReadOnlyList<AssessmentAuditEntry>> GetAssessmentAuditLogAsync(
        string subscriptionId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get assessments with findings for trend analysis
    /// </summary>
    Task<IReadOnlyList<ComplianceAssessmentWithFindings>> GetComplianceTrendsDataAsync(
        string subscriptionId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a cached assessment if available and not expired
    /// </summary>
    Task<ComplianceAssessmentWithFindings?> GetCachedAssessmentAsync(
        string subscriptionId,
        string? resourceGroupName,
        int cacheHours,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Save an assessment with its findings to the database
    /// </summary>
    Task<string?> SaveAssessmentAsync(
        AtoComplianceAssessment assessment,
        string subscriptionId,
        string? resourceGroupName,
        string initiatedBy,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a finding by its finding ID
    /// </summary>
    Task<AtoFinding?> GetFindingByIdAsync(
        string findingId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a finding by ID with its assessment context
    /// </summary>
    Task<AtoFinding?> GetFindingByIdWithAssessmentAsync(
        string findingId,
        string subscriptionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get unresolved findings for a subscription
    /// </summary>
    Task<IReadOnlyList<AtoFinding>> GetUnresolvedFindingsAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update finding status (e.g., to "Remediating")
    /// </summary>
    Task<bool> UpdateFindingStatusAsync(
        string findingId,
        string status,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Summary of a compliance assessment for history/analytics
/// </summary>
public class ComplianceAssessmentSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTime? CompletedAt { get; set; }
    public decimal ComplianceScore { get; set; }
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
    public int HighFindings { get; set; }
    public int MediumFindings { get; set; }
    public int LowFindings { get; set; }
    public string? InitiatedBy { get; set; }
}

/// <summary>
/// Audit log entry for compliance assessments
/// </summary>
public class AssessmentAuditEntry
{
    public string Id { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? Duration { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? InitiatedBy { get; set; }
    public decimal ComplianceScore { get; set; }
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
    public int HighFindings { get; set; }
    public string? ResourceGroupName { get; set; }
    public string? AssessmentType { get; set; }
}

/// <summary>
/// Assessment with findings for trend analysis
/// </summary>
public class ComplianceAssessmentWithFindings
{
    public string Id { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string? ResourceGroupName { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal ComplianceScore { get; set; }
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
    public int HighFindings { get; set; }
    public int MediumFindings { get; set; }
    public int LowFindings { get; set; }
    public long? Duration { get; set; }
    public string? Results { get; set; }
    public List<ComplianceFindingSummary> Findings { get; set; } = new();
}

/// <summary>
/// Summary of a compliance finding
/// </summary>
public class ComplianceFindingSummary
{
    public Guid Id { get; set; }
    public string? FindingId { get; set; }
    public string? RuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Severity { get; set; }
    public string? ComplianceStatus { get; set; }
    public string? ResourceId { get; set; }
    public string? ControlId { get; set; }
}

