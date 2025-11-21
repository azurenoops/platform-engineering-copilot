using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Service for managing collaborative editing sessions
/// </summary>
public interface ICollaborativeEditingService
{
    /// <summary>
    /// Start a new editing session
    /// </summary>
    Task<EditingSession> StartSessionAsync(
        string documentId,
        string versionId,
        string initiatedBy,
        string sessionType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Join an existing editing session
    /// </summary>
    Task<SessionParticipant> JoinSessionAsync(
        string sessionId,
        string userId,
        string userName,
        string userEmail,
        ParticipantRole role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Leave an editing session
    /// </summary>
    Task LeaveSessionAsync(
        string sessionId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquire a lock on a document section
    /// </summary>
    Task<EditingLock> AcquireLockAsync(
        string sessionId,
        string sectionPath,
        string userId,
        LockType lockType,
        int durationMinutes = 15,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Release a lock on a document section
    /// </summary>
    Task ReleaseLockAsync(
        string lockId,
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Refresh a lock (extend expiration)
    /// </summary>
    Task<EditingLock> RefreshLockAsync(
        string lockId,
        string userId,
        int additionalMinutes = 15,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active locks for a session
    /// </summary>
    Task<List<EditingLock>> GetSessionLocksAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a comment to a document section
    /// </summary>
    Task<DocumentComment> AddCommentAsync(
        string documentId,
        string versionId,
        string sectionPath,
        string content,
        string authorId,
        string authorName,
        CommentType type,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reply to a comment
    /// </summary>
    Task<DocumentComment> ReplyToCommentAsync(
        string commentId,
        string content,
        string authorId,
        string authorName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a comment
    /// </summary>
    Task ResolveCommentAsync(
        string commentId,
        string resolvedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get comments for a document
    /// </summary>
    Task<List<DocumentComment>> GetCommentsAsync(
        string documentId,
        string? versionId = null,
        bool includeResolved = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get active editing sessions for a document
    /// </summary>
    Task<List<EditingSession>> GetActiveSessionsAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// End an editing session
    /// </summary>
    Task EndSessionAsync(
        string sessionId,
        string endedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get session details
    /// </summary>
    Task<EditingSession?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update participant's current section
    /// </summary>
    Task UpdateParticipantSectionAsync(
        string sessionId,
        string userId,
        string sectionPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a section is locked
    /// </summary>
    Task<(bool IsLocked, EditingLock? Lock)> CheckSectionLockAsync(
        string sessionId,
        string sectionPath,
        CancellationToken cancellationToken = default);
}
