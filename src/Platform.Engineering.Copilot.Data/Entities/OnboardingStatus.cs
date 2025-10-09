namespace Platform.Engineering.Copilot.Data.Entities;

/// <summary>
/// Represents the status of a Navy Flankspeed onboarding request
/// </summary>
public enum OnboardingStatus
{
    /// <summary>
    /// Request is being filled out in chat (not yet submitted)
    /// </summary>
    Draft = 0,
    
    /// <summary>
    /// Request has been submitted and is awaiting NNWC review
    /// </summary>
    PendingReview = 1,
    
    /// <summary>
    /// NNWC team is actively reviewing the request
    /// </summary>
    UnderReview = 2,
    
    /// <summary>
    /// Request approved by NNWC, ready for provisioning
    /// </summary>
    Approved = 3,
    
    /// <summary>
    /// Azure resources are being provisioned
    /// </summary>
    Provisioning = 4,
    
    /// <summary>
    /// Successfully provisioned and mission owner notified
    /// </summary>
    Completed = 5,
    
    /// <summary>
    /// Request denied by NNWC
    /// </summary>
    Rejected = 6,
    
    /// <summary>
    /// Provisioning failed - requires manual intervention
    /// </summary>
    Failed = 7,
    
    /// <summary>
    /// Request cancelled by mission owner before approval
    /// </summary>
    Cancelled = 8
}
