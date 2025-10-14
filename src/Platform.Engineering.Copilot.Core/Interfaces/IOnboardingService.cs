using Platform.Engineering.Copilot.Core.Models.Onboarding;
using Platform.Engineering.Copilot.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Service interface for managing Navy Flankspeed mission owner onboarding requests
/// </summary>
public interface IOnboardingService
{
    #region Request Management
    
    /// <summary>
    /// Creates a new draft onboarding request
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created request ID</returns>
    Task<string> CreateDraftRequestAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Updates a draft onboarding request with new data
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="updates">Dictionary of field updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful</returns>
    Task<bool> UpdateDraftAsync(string requestId, object updates, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Submits a draft request for NNWC review
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="submittedBy">Email of the user submitting the request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful</returns>
    Task<bool> SubmitRequestAsync(string requestId, string? submittedBy = null, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates a request for submission and returns any validation errors
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of validation errors (empty if valid)</returns>
    Task<List<string>> ValidateForSubmissionAsync(string requestId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets a specific onboarding request by ID
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The onboarding request or null if not found</returns>
    Task<OnboardingRequest?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all pending onboarding requests awaiting NNWC review
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending requests</returns>
    Task<List<OnboardingRequest>> GetPendingRequestsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all onboarding requests for a specific mission owner
    /// </summary>
    /// <param name="email">Mission owner email</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of requests</returns>
    Task<List<OnboardingRequest>> GetRequestsByOwnerAsync(string email, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Cancels a pending request
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="reason">Cancellation reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful</returns>
    Task<bool> CancelRequestAsync(string requestId, string reason, CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Approval Workflow
    
    /// <summary>
    /// Approves an onboarding request and triggers provisioning
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="approvedBy">Name/ID of approver</param>
    /// <param name="comments">Optional approval comments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Provisioning result with job ID</returns>
    Task<ProvisioningResult> ApproveRequestAsync(
        string requestId, 
        string approvedBy, 
        string? comments = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Rejects an onboarding request
    /// </summary>
    /// <param name="requestId">The request ID</param>
    /// <param name="rejectedBy">Name/ID of person rejecting</param>
    /// <param name="reason">Rejection reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if successful</returns>
    Task<bool> RejectRequestAsync(
        string requestId, 
        string rejectedBy, 
        string reason, 
        CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Provisioning Status
    
    /// <summary>
    /// Gets the status of a provisioning job
    /// </summary>
    /// <param name="jobId">The provisioning job ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Provisioning status</returns>
    Task<ProvisioningStatus> GetProvisioningStatusAsync(string jobId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets all requests currently being provisioned
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of requests in provisioning state</returns>
    Task<List<OnboardingRequest>> GetProvisioningRequestsAsync(CancellationToken cancellationToken = default);
    
    #endregion
    
    #region Statistics & Reporting
    
    /// <summary>
    /// Gets onboarding statistics for dashboard
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Statistics summary</returns>
    Task<OnboardingStats> GetStatsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets onboarding history for a specific time period
    /// </summary>
    /// <param name="startDate">Start date</param>
    /// <param name="endDate">End date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of requests in the time period</returns>
    Task<List<OnboardingRequest>> GetHistoryAsync(
        DateTime startDate, 
        DateTime endDate, 
        CancellationToken cancellationToken = default);
    
    #endregion
}

/// <summary>
/// Result of a provisioning operation
/// </summary>
public class ProvisioningResult
{
    public bool Success { get; set; }
    public string? JobId { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, string> Errors { get; set; } = new();
}

/// <summary>
/// Status of an ongoing provisioning job
/// </summary>
public class ProvisioningStatus
{
    public string JobId { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // InProgress, Completed, Failed
    public int PercentComplete { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public List<string> CompletedSteps { get; set; } = new();
    public List<string> FailedSteps { get; set; } = new();
    public Dictionary<string, string> ProvisionedResources { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Onboarding statistics for dashboard
/// </summary>
public class OnboardingStats
{
    public int TotalRequests { get; set; }
    public int PendingReview { get; set; }
    public int Approved { get; set; }
    public int Rejected { get; set; }
    public int InProvisioning { get; set; }
    public int Completed { get; set; }
    public int Failed { get; set; }
    public double AverageApprovalTimeHours { get; set; }
    public double AverageProvisioningTimeHours { get; set; }
    public double SuccessRate { get; set; }
    public List<OnboardingTrend> Trends { get; set; } = new();
}

/// <summary>
/// Trend data for onboarding metrics
/// </summary>
public class OnboardingTrend
{
    public DateTime Date { get; set; }
    public int RequestsSubmitted { get; set; }
    public int RequestsCompleted { get; set; }
    public int RequestsRejected { get; set; }
}
