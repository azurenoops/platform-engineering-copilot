using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Database entity for tracking JIT (Just-In-Time) privilege elevation requests.
/// This tracks both Azure PIM role activations and JIT VM access requests.
/// </summary>
public class JitRequestEntity
{
    /// <summary>
    /// Unique identifier for this Copilot tracking record.
    /// </summary>
    [Key]
    [MaxLength(100)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The type of JIT request.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string RequestType { get; set; } = string.Empty;

    /// <summary>
    /// The Azure PIM request ID (for PIM activations).
    /// </summary>
    [MaxLength(200)]
    public string? PimRequestId { get; set; }

    /// <summary>
    /// The Azure JIT request ID (for VM access).
    /// </summary>
    [MaxLength(200)]
    public string? JitVmRequestId { get; set; }

    /// <summary>
    /// The Azure AD object ID of the user who made the request.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the user.
    /// </summary>
    [MaxLength(200)]
    public string? UserDisplayName { get; set; }

    /// <summary>
    /// Email of the user.
    /// </summary>
    [MaxLength(200)]
    public string? UserEmail { get; set; }

    /// <summary>
    /// The Copilot conversation ID for audit correlation.
    /// </summary>
    [MaxLength(100)]
    public string? ConversationId { get; set; }

    /// <summary>
    /// The session ID from the Copilot chat.
    /// </summary>
    [MaxLength(100)]
    public string? SessionId { get; set; }

    #region For PIM Role Activations

    /// <summary>
    /// The Azure role definition ID being requested.
    /// </summary>
    [MaxLength(100)]
    public string? RoleDefinitionId { get; set; }

    /// <summary>
    /// Display name of the role.
    /// </summary>
    [MaxLength(200)]
    public string? RoleName { get; set; }

    /// <summary>
    /// The scope of the role assignment.
    /// </summary>
    [MaxLength(500)]
    public string? Scope { get; set; }

    /// <summary>
    /// Friendly name for the scope (e.g., subscription name, resource group name).
    /// </summary>
    [MaxLength(200)]
    public string? ScopeName { get; set; }

    #endregion

    #region For JIT VM Access

    /// <summary>
    /// The Azure resource ID of the VM (for JIT VM access).
    /// </summary>
    [MaxLength(500)]
    public string? VmResourceId { get; set; }

    /// <summary>
    /// Name of the VM.
    /// </summary>
    [MaxLength(200)]
    public string? VmName { get; set; }

    /// <summary>
    /// Ports requested (JSON serialized).
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? PortsJson { get; set; }

    /// <summary>
    /// Allowed source IP address.
    /// </summary>
    [MaxLength(50)]
    public string? AllowedSourceIp { get; set; }

    #endregion

    #region Request Details

    /// <summary>
    /// Business justification for the request.
    /// </summary>
    [MaxLength(2000)]
    public string Justification { get; set; } = string.Empty;

    /// <summary>
    /// Associated ticket number (e.g., ServiceNow, Jira).
    /// </summary>
    [MaxLength(100)]
    public string? TicketNumber { get; set; }

    /// <summary>
    /// Ticket system identifier.
    /// </summary>
    [MaxLength(100)]
    public string? TicketSystem { get; set; }

    /// <summary>
    /// Requested duration in minutes.
    /// </summary>
    public int RequestedDurationMinutes { get; set; }

    #endregion

    #region Status & Timestamps

    /// <summary>
    /// Current status of the request.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Submitted";

    /// <summary>
    /// When the request was submitted.
    /// </summary>
    [Required]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the request was approved (if applicable).
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Who approved the request.
    /// </summary>
    [MaxLength(200)]
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// Approval comments.
    /// </summary>
    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    /// <summary>
    /// When the request was denied (if applicable).
    /// </summary>
    public DateTime? DeniedAt { get; set; }

    /// <summary>
    /// Who denied the request.
    /// </summary>
    [MaxLength(200)]
    public string? DeniedBy { get; set; }

    /// <summary>
    /// Reason for denial.
    /// </summary>
    [MaxLength(1000)]
    public string? DenialReason { get; set; }

    /// <summary>
    /// When the role/access was activated.
    /// </summary>
    public DateTime? ActivatedAt { get; set; }

    /// <summary>
    /// When the role/access expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// When the role/access was deactivated/revoked.
    /// </summary>
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>
    /// Whether deactivation was manual or automatic (expiration).
    /// </summary>
    [MaxLength(50)]
    public string? DeactivationReason { get; set; }

    #endregion

    #region Error Handling

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; } = 0;

    /// <summary>
    /// Last retry timestamp.
    /// </summary>
    public DateTime? LastRetryAt { get; set; }

    #endregion

    #region Audit Information

    /// <summary>
    /// IP address of the requester.
    /// </summary>
    [MaxLength(50)]
    public string? RequesterIpAddress { get; set; }

    /// <summary>
    /// User agent string of the requester's client.
    /// </summary>
    [MaxLength(500)]
    public string? RequesterUserAgent { get; set; }

    /// <summary>
    /// Additional metadata as JSON.
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Azure Activity Log correlation ID for cross-referencing.
    /// </summary>
    [MaxLength(100)]
    public string? AzureCorrelationId { get; set; }

    #endregion
}

/// <summary>
/// Enumeration of JIT request types.
/// </summary>
public static class JitRequestTypes
{
    /// <summary>
    /// Azure PIM role activation.
    /// </summary>
    public const string PimRoleActivation = "PimRoleActivation";

    /// <summary>
    /// Azure Security Center JIT VM access.
    /// </summary>
    public const string JitVmAccess = "JitVmAccess";

    /// <summary>
    /// Azure AD Entitlement Management access package.
    /// </summary>
    public const string AccessPackage = "AccessPackage";

    /// <summary>
    /// Azure PIM for Groups membership.
    /// </summary>
    public const string PimGroupMembership = "PimGroupMembership";
}

/// <summary>
/// Status values for JIT requests.
/// </summary>
public static class JitRequestStatuses
{
    /// <summary>
    /// Request has been submitted to Azure.
    /// </summary>
    public const string Submitted = "Submitted";

    /// <summary>
    /// Request is pending approval.
    /// </summary>
    public const string PendingApproval = "PendingApproval";

    /// <summary>
    /// Request has been approved.
    /// </summary>
    public const string Approved = "Approved";

    /// <summary>
    /// Request has been denied.
    /// </summary>
    public const string Denied = "Denied";

    /// <summary>
    /// Role/access is currently active.
    /// </summary>
    public const string Active = "Active";

    /// <summary>
    /// Role/access has expired.
    /// </summary>
    public const string Expired = "Expired";

    /// <summary>
    /// Role/access was manually deactivated.
    /// </summary>
    public const string Deactivated = "Deactivated";

    /// <summary>
    /// Request failed.
    /// </summary>
    public const string Failed = "Failed";

    /// <summary>
    /// Request was canceled by the user.
    /// </summary>
    public const string Canceled = "Canceled";
}
