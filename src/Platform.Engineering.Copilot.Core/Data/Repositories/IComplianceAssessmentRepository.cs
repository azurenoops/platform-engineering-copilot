using Platform.Engineering.Copilot.Compliance.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Repositories;

/// <summary>
/// Repository interface for compliance assessment operations.
/// Manages ComplianceAssessments (ATO compliance scan results) and related ComplianceFindings.
/// </summary>
public interface IComplianceAssessmentRepository
{
    // ==================== Assessment Operations ====================
    
    /// <summary>
    /// Get an assessment by ID
    /// </summary>
    Task<ComplianceAssessment?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get an assessment by ID with all findings
    /// </summary>
    Task<ComplianceAssessment?> GetByIdWithFindingsAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all assessments for a subscription
    /// </summary>
    Task<IReadOnlyList<ComplianceAssessment>> GetBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all assessments for a subscription with findings
    /// </summary>
    Task<IReadOnlyList<ComplianceAssessment>> GetBySubscriptionWithFindingsAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the latest assessment for a subscription
    /// </summary>
    Task<ComplianceAssessment?> GetLatestBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get the latest completed assessment for a subscription with findings
    /// </summary>
    Task<ComplianceAssessment?> GetLatestCompletedWithFindingsAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get assessment by subscription and date
    /// </summary>
    Task<ComplianceAssessment?> GetBySubscriptionAndDateAsync(string subscriptionId, DateTime date, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get assessment by subscription and date with findings
    /// </summary>
    Task<ComplianceAssessment?> GetBySubscriptionAndDateWithFindingsAsync(string subscriptionId, DateTime date, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get previous assessment before a given assessment
    /// </summary>
    Task<ComplianceAssessment?> GetPreviousAssessmentAsync(string subscriptionId, DateTime beforeDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get assessments by status
    /// </summary>
    Task<IReadOnlyList<ComplianceAssessment>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get assessments by assessment type (NIST-800-53, FedRAMP, etc.)
    /// </summary>
    Task<IReadOnlyList<ComplianceAssessment>> GetByTypeAsync(string assessmentType, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get assessments within a date range
    /// </summary>
    Task<IReadOnlyList<ComplianceAssessment>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Search assessments with multiple filters
    /// </summary>
    Task<IReadOnlyList<ComplianceAssessment>> SearchAsync(
        string? subscriptionId = null,
        string? resourceGroup = null,
        string? status = null,
        string? assessmentType = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if an assessment exists
    /// </summary>
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Count assessments by subscription
    /// </summary>
    Task<int> CountBySubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a new assessment
    /// </summary>
    Task<ComplianceAssessment> AddAsync(ComplianceAssessment assessment, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update an existing assessment
    /// </summary>
    Task<ComplianceAssessment> UpdateAsync(ComplianceAssessment assessment, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update assessment status
    /// </summary>
    Task<bool> UpdateStatusAsync(string id, string status, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete an assessment and all its findings
    /// </summary>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);
    
    // ==================== Finding Operations ====================
    
    /// <summary>
    /// Get a finding by ID
    /// </summary>
    Task<ComplianceFinding?> GetFindingByIdAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get a finding by assessment ID and finding ID
    /// </summary>
    Task<ComplianceFinding?> GetFindingAsync(string assessmentId, string findingId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get all findings for an assessment
    /// </summary>
    Task<IReadOnlyList<ComplianceFinding>> GetFindingsAsync(string assessmentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get findings by severity
    /// </summary>
    Task<IReadOnlyList<ComplianceFinding>> GetFindingsBySeverityAsync(string assessmentId, string severity, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get findings by compliance status
    /// </summary>
    Task<IReadOnlyList<ComplianceFinding>> GetFindingsByStatusAsync(string assessmentId, string complianceStatus, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get findings by resource
    /// </summary>
    Task<IReadOnlyList<ComplianceFinding>> GetFindingsByResourceAsync(string resourceId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get findings by control ID
    /// </summary>
    Task<IReadOnlyList<ComplianceFinding>> GetFindingsByControlAsync(string controlId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get unresolved high-priority findings by control for a subscription
    /// </summary>
    Task<IReadOnlyList<ComplianceFinding>> GetUnresolvedFindingsByControlAsync(
        string subscriptionId, 
        string controlId, 
        int limit = 10, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Count auto-remediated findings for a subscription
    /// </summary>
    Task<int> CountAutoRemediatedFindingsAsync(string subscriptionId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Count findings by severity for an assessment
    /// </summary>
    Task<Dictionary<string, int>> CountFindingsBySeverityAsync(string assessmentId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add a finding to an assessment
    /// </summary>
    Task<ComplianceFinding> AddFindingAsync(ComplianceFinding finding, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Add multiple findings to an assessment
    /// </summary>
    Task<IReadOnlyList<ComplianceFinding>> AddFindingsAsync(IEnumerable<ComplianceFinding> findings, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Update a finding
    /// </summary>
    Task<ComplianceFinding> UpdateFindingAsync(ComplianceFinding finding, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Mark a finding as resolved
    /// </summary>
    Task<bool> ResolveFindingAsync(string assessmentId, string findingId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete a finding
    /// </summary>
    Task<bool> DeleteFindingAsync(Guid id, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete all findings for an assessment
    /// </summary>
    Task<int> DeleteFindingsAsync(string assessmentId, CancellationToken cancellationToken = default);
    
    // ==================== Analytics Operations ====================
    
    /// <summary>
    /// Get compliance score trend for a subscription
    /// </summary>
    Task<IReadOnlyList<ComplianceScoreDataPoint>> GetComplianceScoreTrendAsync(
        string subscriptionId, 
        int limit = 10, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get finding statistics across all assessments for a subscription
    /// </summary>
    Task<FindingStatistics> GetFindingStatisticsAsync(string subscriptionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Data point for compliance score trend
/// </summary>
public class ComplianceScoreDataPoint
{
    public string AssessmentId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public decimal ComplianceScore { get; set; }
    public int TotalFindings { get; set; }
    public int CriticalFindings { get; set; }
}

/// <summary>
/// Aggregate finding statistics
/// </summary>
public class FindingStatistics
{
    public int TotalAssessments { get; set; }
    public int TotalFindings { get; set; }
    public int ResolvedFindings { get; set; }
    public int OpenFindings { get; set; }
    public Dictionary<string, int> FindingsBySeverity { get; set; } = new();
    public Dictionary<string, int> FindingsByType { get; set; } = new();
    public Dictionary<string, int> FindingsByStatus { get; set; } = new();
    public decimal AverageComplianceScore { get; set; }
}
