using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Service interface for managing compliance assessments and findings.
/// Provides data access and caching for ATO compliance assessment operations.
/// </summary>
public interface IAssessmentService
{
    #region Core Assessment Operations

    /// <summary>
    /// Saves a compliance assessment to the database
    /// </summary>
    /// <param name="assessment">The assessment to save</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The saved assessment ID</returns>
    Task<string> SaveAssessmentAsync(AtoComplianceAssessment assessment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a compliance assessment by ID
    /// </summary>
    /// <param name="assessmentId">The assessment ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The assessment or null if not found</returns>
    Task<AtoComplianceAssessment?> GetAssessmentAsync(string assessmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all assessments for a subscription
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of assessments</returns>
    Task<IEnumerable<AtoComplianceAssessment>> GetAssessmentsAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a summary of assessments for a subscription
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Assessment summary</returns>
    Task<AtoScanSummary> GetAssessmentSummaryAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an assessment and all its findings
    /// </summary>
    /// <param name="assessmentId">The assessment ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted, false if not found</returns>
    Task<bool> DeleteAssessmentAsync(string assessmentId, CancellationToken cancellationToken = default);

    #endregion

    #region Finding Operations

    /// <summary>
    /// Retrieves all findings for an assessment
    /// </summary>
    /// <param name="assessmentId">The assessment ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of findings</returns>
    Task<IEnumerable<AtoFinding>> GetFindingsAsync(string assessmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a finding as resolved
    /// </summary>
    /// <param name="assessmentId">The assessment ID</param>
    /// <param name="findingId">The finding ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if resolved, false if not found</returns>
    Task<bool> ResolveFindingAsync(string assessmentId, string findingId, CancellationToken cancellationToken = default);

    #endregion

    #region Extended Methods for AtoComplianceEngine

    /// <summary>
    /// Gets the latest completed assessment with findings for a subscription
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The latest completed assessment or null</returns>
    Task<AtoComplianceAssessment?> GetLatestCompletedAssessmentAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets monitored controls for a subscription based on the latest assessment
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of monitored controls</returns>
    Task<List<MonitoredControl>> GetMonitoredControlsAsync(string subscriptionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unresolved findings (alerts) for a specific control
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="controlId">The control ID</param>
    /// <param name="limit">Maximum number of alerts to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of compliance alerts</returns>
    Task<List<ComplianceAlert>> GetControlAlertsAsync(string subscriptionId, string controlId, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts auto-remediated findings for a subscription
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of auto-remediated findings</returns>
    Task<int> CountAutoRemediatedFindingsAsync(string subscriptionId, CancellationToken cancellationToken = default);

    #endregion

    #region Date-Based Queries

    /// <summary>
    /// Gets compliance score at a specific date
    /// </summary>
    Task<double> GetComplianceScoreAtDateAsync(string subscriptionId, DateTimeOffset date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets failed controls count at a specific date
    /// </summary>
    Task<int> GetFailedControlsAtDateAsync(string subscriptionId, DateTimeOffset date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets passed controls count at a specific date
    /// </summary>
    Task<int> GetPassedControlsAtDateAsync(string subscriptionId, DateTimeOffset date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets active findings count at a specific date
    /// </summary>
    Task<int> GetActiveFindingsAtDateAsync(string subscriptionId, DateTimeOffset date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets remediated findings count at a specific date
    /// </summary>
    Task<int> GetRemediatedFindingsAtDateAsync(string subscriptionId, DateTimeOffset date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets compliance events at a specific date
    /// </summary>
    Task<List<string>> GetComplianceEventsAtDateAsync(string subscriptionId, DateTimeOffset date, CancellationToken cancellationToken = default);

    #endregion

    #region Search and History

    /// <summary>
    /// Gets compliance history for a subscription within a date range
    /// </summary>
    Task<IReadOnlyList<ComplianceAssessmentSummary>> GetComplianceHistoryAsync(string subscriptionId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets assessment audit log entries within a date range
    /// </summary>
    Task<IReadOnlyList<AssessmentAuditEntry>> GetAssessmentAuditLogAsync(string subscriptionId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets compliance trends data with findings
    /// </summary>
    Task<IReadOnlyList<ComplianceAssessmentWithFindings>> GetComplianceTrendsDataAsync(string subscriptionId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a cached assessment if available within the cache window
    /// </summary>
    /// <param name="subscriptionId">The subscription ID</param>
    /// <param name="resourceGroupName">Optional resource group name</param>
    /// <param name="cacheHours">Number of hours to consider for cache validity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cached assessment or null</returns>
    Task<ComplianceAssessmentWithFindings?> GetCachedAssessmentAsync(string subscriptionId, string? resourceGroupName, int cacheHours, CancellationToken cancellationToken = default);

    #endregion

    #region Caching

    /// <summary>
    /// Caches assessment summary for quick access
    /// </summary>
    /// <param name="assessment">The assessment to cache</param>
    void CacheAssessmentSummary(AtoComplianceAssessment assessment);

    #endregion
}
