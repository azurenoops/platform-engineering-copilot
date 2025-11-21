namespace Platform.Engineering.Copilot.Core.Models.Compliance;

/// <summary>
/// Document version information
/// </summary>
public class DocumentVersion
{
    public string VersionId { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string VersionNumber { get; set; } = "1.0";
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public VersionChangeType ChangeType { get; set; } = VersionChangeType.MinorUpdate;
    public string ContentHash { get; set; } = string.Empty;
    public string BlobUri { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public ComplianceDocumentFormat Format { get; set; } = ComplianceDocumentFormat.Markdown;
    public List<string> ChangeSummary { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
}

/// <summary>
/// Type of version change
/// </summary>
public enum VersionChangeType
{
    MajorUpdate,      // 1.0 -> 2.0 (significant changes, re-review required)
    MinorUpdate,      // 1.0 -> 1.1 (moderate changes, partial review)
    PatchUpdate,      // 1.0.0 -> 1.0.1 (minor fixes, typos)
    InitialVersion,   // First version
    Revision          // Same version, different revision
}

/// <summary>
/// Document revision history
/// </summary>
public class DocumentRevision
{
    public string RevisionId { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public DateTime RevisionDate { get; set; } = DateTime.UtcNow;
    public string RevisedBy { get; set; } = string.Empty;
    public string RevisionReason { get; set; } = string.Empty;
    public List<RevisionChange> Changes { get; set; } = new();
    public string ApprovalStatus { get; set; } = "Pending"; // Pending, Approved, Rejected
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime? ApprovalDate { get; set; }
}

/// <summary>
/// Individual change within a revision
/// </summary>
public class RevisionChange
{
    public string ChangeId { get; set; } = Guid.NewGuid().ToString();
    public string SectionPath { get; set; } = string.Empty; // e.g., "3.1.2" or "sections/access-control/ac-2"
    public ChangeOperationType Operation { get; set; } = ChangeOperationType.Modify;
    public string OldContent { get; set; } = string.Empty;
    public string NewContent { get; set; } = string.Empty;
    public string ChangeDescription { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ChangedBy { get; set; } = string.Empty;
}

/// <summary>
/// Type of change operation
/// </summary>
public enum ChangeOperationType
{
    Add,
    Modify,
    Delete,
    Move,
    Rename
}

/// <summary>
/// Collaborative editing session
/// </summary>
public class EditingSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public string InitiatedBy { get; set; } = string.Empty;
    public List<SessionParticipant> Participants { get; set; } = new();
    public List<EditingLock> Locks { get; set; } = new();
    public EditingSessionStatus Status { get; set; } = EditingSessionStatus.Active;
    public string SessionType { get; set; } = "Collaborative"; // Collaborative, Exclusive, ReadOnly
}

/// <summary>
/// Participant in editing session
/// </summary>
public class SessionParticipant
{
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LeftAt { get; set; }
    public ParticipantRole Role { get; set; } = ParticipantRole.Editor;
    public bool IsActive { get; set; } = true;
    public string CurrentSection { get; set; } = string.Empty; // Section they're viewing/editing
}

/// <summary>
/// Participant role in editing session
/// </summary>
public enum ParticipantRole
{
    Owner,         // Can make any changes, manage session
    Editor,        // Can edit content
    Reviewer,      // Can add comments, suggest changes
    Viewer         // Read-only access
}

/// <summary>
/// Editing lock on a document section
/// </summary>
public class EditingLock
{
    public string LockId { get; set; } = Guid.NewGuid().ToString();
    public string SectionPath { get; set; } = string.Empty;
    public string LockedBy { get; set; } = string.Empty;
    public DateTime LockAcquired { get; set; } = DateTime.UtcNow;
    public DateTime LockExpires { get; set; } = DateTime.UtcNow.AddMinutes(15);
    public LockType Type { get; set; } = LockType.Exclusive;
}

/// <summary>
/// Type of editing lock
/// </summary>
public enum LockType
{
    Exclusive,     // Only lock holder can edit
    Shared,        // Multiple users can edit (with conflict resolution)
    Advisory       // Lock is advisory only
}

/// <summary>
/// Editing session status
/// </summary>
public enum EditingSessionStatus
{
    Active,
    Suspended,
    Completed,
    Cancelled
}

/// <summary>
/// Comment on a document section
/// </summary>
public class DocumentComment
{
    public string CommentId { get; set; } = Guid.NewGuid().ToString();
    public string DocumentId { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public string SectionPath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string AuthorId { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string ResolvedBy { get; set; } = string.Empty;
    public CommentType Type { get; set; } = CommentType.General;
    public List<DocumentComment> Replies { get; set; } = new();
}

/// <summary>
/// Type of comment
/// </summary>
public enum CommentType
{
    General,
    Question,
    Suggestion,
    Issue,
    Approval,
    Rejection
}
