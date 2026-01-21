using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Database entity for approval workflows
/// </summary>
public class ApprovalWorkflowEntity
{
    [Key]
    [MaxLength(100)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [MaxLength(100)]
    public string ToolCallId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000)]
    public string Justification { get; set; } = string.Empty;

    public int Priority { get; set; } = 1;

    // Infrastructure provisioning approval properties
    [MaxLength(100)]
    public string ResourceType { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ResourceName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ResourceGroupName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string Location { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Environment { get; set; } = string.Empty;

    [MaxLength(200)]
    public string RequestedBy { get; set; } = string.Empty;

    [Required]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime ExpiresAt { get; set; }

    [MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    [MaxLength(200)]
    public string? RejectedBy { get; set; }

    public DateTime? RejectedAt { get; set; }

    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // JSON serialized data - using nvarchar(max) for SQL Server compatibility
    [Column(TypeName = "nvarchar(max)")]
    public string RequiredApproversJson { get; set; } = "[]";

    [Column(TypeName = "nvarchar(max)")]
    public string PolicyViolationsJson { get; set; } = "[]";

    [Column(TypeName = "nvarchar(max)")]
    public string OriginalToolCallJson { get; set; } = "{}";

    [Column(TypeName = "nvarchar(max)")]
    public string DecisionsJson { get; set; } = "[]";

    [Column(TypeName = "nvarchar(max)")]
    public string RequestPayload { get; set; } = string.Empty;
}
