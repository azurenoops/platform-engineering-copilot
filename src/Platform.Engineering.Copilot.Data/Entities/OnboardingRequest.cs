using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Data.Entities;

/// <summary>
/// Represents a Navy Flankspeed mission owner onboarding request
/// This entity tracks the complete lifecycle from initial chat request through provisioning
/// </summary>
public class OnboardingRequest
{
    /// <summary>
    /// Unique identifier for the onboarding request
    /// </summary>
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    #region Mission Details
    
    /// <summary>
    /// Name of the mission or project (e.g., "Project Seawolf")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string MissionName { get; set; } = string.Empty;
    
    /// <summary>
    /// Full name of the mission owner (e.g., "John Smith")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string MissionOwner { get; set; } = string.Empty;
    
    /// <summary>
    /// Email address for the mission owner (.mil domain)
    /// </summary>
    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string MissionOwnerEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// Military rank of the mission owner (e.g., "CDR", "LCDR", "GS-14")
    /// </summary>
    [MaxLength(50)]
    public string MissionOwnerRank { get; set; } = string.Empty;
    
    /// <summary>
    /// Navy command or organization (e.g., "NAVAIR", "SPAWAR", "NIWC")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Command { get; set; } = string.Empty;
    
    /// <summary>
    /// Data classification level: UNCLASS, SECRET, or TS
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string ClassificationLevel { get; set; } = "UNCLASS";
    
    #endregion
    
    #region Technical Requirements
    
    /// <summary>
    /// Desired Azure subscription name
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string RequestedSubscriptionName { get; set; } = string.Empty;
    
    /// <summary>
    /// Requested VNet CIDR block (e.g., "10.100.0.0/16")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string RequestedVNetCidr { get; set; } = string.Empty;
    
    /// <summary>
    /// List of required Azure services (AKS, Storage, SQL, etc.)
    /// Stored as JSON array
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string RequiredServicesJson { get; set; } = "[]";
    
    /// <summary>
    /// Deserialized list of required services
    /// </summary>
    [NotMapped]
    public List<string> RequiredServices
    {
        get => System.Text.Json.JsonSerializer.Deserialize<List<string>>(RequiredServicesJson) ?? new List<string>();
        set => RequiredServicesJson = System.Text.Json.JsonSerializer.Serialize(value);
    }
    
    /// <summary>
    /// Azure Government region (usgovvirginia, usgovtexas, etc.)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Region { get; set; } = "usgovvirginia";
    
    /// <summary>
    /// Estimated number of users who will access this environment
    /// </summary>
    public int EstimatedUserCount { get; set; }
    
    /// <summary>
    /// Data residency requirement (US, OCONUS, etc.)
    /// </summary>
    [MaxLength(50)]
    public string DataResidency { get; set; } = "US";
    
    /// <summary>
    /// Estimated data volume in terabytes
    /// </summary>
    public decimal EstimatedDataVolumeTB { get; set; }
    
    #endregion
    
    #region Compliance & Security
    
    /// <summary>
    /// Whether PKI certificates are required
    /// </summary>
    public bool RequiresPki { get; set; }
    
    /// <summary>
    /// Whether CAC authentication is required
    /// </summary>
    public bool RequiresCac { get; set; } = true;
    
    /// <summary>
    /// Whether an Authority to Operate (ATO) is required
    /// </summary>
    public bool RequiresAto { get; set; }
    
    /// <summary>
    /// Email for security contact
    /// </summary>
    [EmailAddress]
    [MaxLength(200)]
    public string SecurityContactEmail { get; set; } = string.Empty;
    
    /// <summary>
    /// List of compliance frameworks (NIST 800-53, DISA STIG, etc.)
    /// Stored as JSON array
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string ComplianceFrameworksJson { get; set; } = "[]";
    
    /// <summary>
    /// Deserialized list of compliance frameworks
    /// </summary>
    [NotMapped]
    public List<string> ComplianceFrameworks
    {
        get => System.Text.Json.JsonSerializer.Deserialize<List<string>>(ComplianceFrameworksJson) ?? new List<string>();
        set => ComplianceFrameworksJson = System.Text.Json.JsonSerializer.Serialize(value);
    }
    
    #endregion
    
    #region Business Justification
    
    /// <summary>
    /// Business justification for the request
    /// </summary>
    [Required]
    [Column(TypeName = "TEXT")]
    public string BusinessJustification { get; set; } = string.Empty;
    
    /// <summary>
    /// Detailed use case description
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string UseCase { get; set; } = string.Empty;
    
    /// <summary>
    /// Requested start date for the environment
    /// </summary>
    public DateTime RequestedStartDate { get; set; }
    
    /// <summary>
    /// Funding source (OPTAR, RDT&E, O&M, etc.)
    /// </summary>
    [MaxLength(100)]
    public string FundingSource { get; set; } = string.Empty;
    
    /// <summary>
    /// Estimated monthly cost in dollars
    /// </summary>
    public decimal EstimatedMonthlyCost { get; set; }
    
    /// <summary>
    /// Duration of the mission in months
    /// </summary>
    public int MissionDurationMonths { get; set; }
    
    #endregion
    
    #region Workflow State
    
    /// <summary>
    /// Current status of the onboarding request
    /// </summary>
    [Required]
    public OnboardingStatus Status { get; set; } = OnboardingStatus.Draft;
    
    /// <summary>
    /// When the request was initially created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// When the request was reviewed by NNWC
    /// </summary>
    public DateTime? ReviewedAt { get; set; }
    
    /// <summary>
    /// When provisioning started
    /// </summary>
    public DateTime? ProvisionedAt { get; set; }
    
    /// <summary>
    /// When the entire process completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }
    
    /// <summary>
    /// Last updated timestamp
    /// </summary>
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    
    #endregion
    
    #region Approval Workflow
    
    /// <summary>
    /// Name/ID of the NNWC team member who approved the request
    /// </summary>
    [MaxLength(200)]
    public string? ApprovedBy { get; set; }
    
    /// <summary>
    /// Comments from approver
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? ApprovalComments { get; set; }
    
    /// <summary>
    /// Name/ID of the person who rejected the request
    /// </summary>
    [MaxLength(200)]
    public string? RejectedBy { get; set; }
    
    /// <summary>
    /// Reason for rejection
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? RejectionReason { get; set; }
    
    /// <summary>
    /// Priority level (1-5, 5 being highest)
    /// </summary>
    public int Priority { get; set; } = 3;
    
    #endregion
    
    #region Provisioned Resources
    
    /// <summary>
    /// Azure subscription ID that was created
    /// </summary>
    [MaxLength(100)]
    public string? ProvisionedSubscriptionId { get; set; }
    
    /// <summary>
    /// Azure VNet resource ID
    /// </summary>
    [MaxLength(500)]
    public string? ProvisionedVNetId { get; set; }
    
    /// <summary>
    /// Azure resource group ID
    /// </summary>
    [MaxLength(500)]
    public string? ProvisionedResourceGroupId { get; set; }
    
    /// <summary>
    /// Dictionary of other provisioned resource IDs
    /// Stored as JSON
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string ProvisionedResourcesJson { get; set; } = "{}";
    
    /// <summary>
    /// Deserialized dictionary of provisioned resources
    /// </summary>
    [NotMapped]
    public Dictionary<string, string> ProvisionedResources
    {
        get => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(ProvisionedResourcesJson) ?? new Dictionary<string, string>();
        set => ProvisionedResourcesJson = System.Text.Json.JsonSerializer.Serialize(value);
    }
    
    /// <summary>
    /// Provisioning job ID for tracking async operations
    /// </summary>
    [MaxLength(100)]
    public string? ProvisioningJobId { get; set; }
    
    /// <summary>
    /// Error message if provisioning failed
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? ProvisioningError { get; set; }
    
    #endregion
    
    #region Notifications
    
    /// <summary>
    /// Whether notification has been sent to mission owner
    /// </summary>
    public bool NotificationSent { get; set; }
    
    /// <summary>
    /// When notification was sent
    /// </summary>
    public DateTime? NotificationSentAt { get; set; }
    
    /// <summary>
    /// History of notifications sent
    /// Stored as JSON array of notification records
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string NotificationHistoryJson { get; set; } = "[]";
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Checks if the request can be approved
    /// </summary>
    public bool CanBeApproved()
    {
        return Status == OnboardingStatus.PendingReview || Status == OnboardingStatus.UnderReview;
    }
    
    /// <summary>
    /// Checks if the request can be rejected
    /// </summary>
    public bool CanBeRejected()
    {
        return Status == OnboardingStatus.PendingReview || Status == OnboardingStatus.UnderReview;
    }
    
    /// <summary>
    /// Checks if provisioning can start
    /// </summary>
    public bool CanStartProvisioning()
    {
        return Status == OnboardingStatus.Approved;
    }
    
    /// <summary>
    /// Checks if the request is in a terminal state
    /// </summary>
    public bool IsTerminalState()
    {
        return Status == OnboardingStatus.Completed 
            || Status == OnboardingStatus.Rejected 
            || Status == OnboardingStatus.Cancelled 
            || Status == OnboardingStatus.Failed;
    }
    
    #endregion
}
