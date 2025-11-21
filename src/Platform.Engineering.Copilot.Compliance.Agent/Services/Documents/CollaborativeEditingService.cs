using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Documents;

/// <summary>
/// Service for managing collaborative editing sessions
/// </summary>
public class CollaborativeEditingService : ICollaborativeEditingService
{
    private readonly ILogger<CollaborativeEditingService> _logger;
    private readonly string _containerName = "compliance-editing-sessions";
    
    // In-memory cache for active sessions (in production, use Redis or similar)
    private static readonly ConcurrentDictionary<string, EditingSession> _activeSessions = new();
    private static readonly ConcurrentDictionary<string, List<DocumentComment>> _comments = new();

    public CollaborativeEditingService(ILogger<CollaborativeEditingService> logger)
    {
        _logger = logger;
    }

    public async Task<EditingSession> StartSessionAsync(
        string documentId,
        string versionId,
        string initiatedBy,
        string sessionType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = new EditingSession
            {
                DocumentId = documentId,
                VersionId = versionId,
                InitiatedBy = initiatedBy,
                SessionType = sessionType,
                Status = EditingSessionStatus.Active,
                Participants = new List<SessionParticipant>
                {
                    new SessionParticipant
                    {
                        UserId = initiatedBy,
                        UserName = initiatedBy,
                        Role = ParticipantRole.Owner
                    }
                }
            };

            _activeSessions[session.SessionId] = session;
            await PersistSessionAsync(session, cancellationToken);

            _logger.LogInformation("Started editing session {SessionId} for document {DocumentId}", 
                session.SessionId, documentId);

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting editing session for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<SessionParticipant> JoinSessionAsync(
        string sessionId,
        string userId,
        string userName,
        string userEmail,
        ParticipantRole role,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId, cancellationToken);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            var participant = new SessionParticipant
            {
                UserId = userId,
                UserName = userName,
                UserEmail = userEmail,
                Role = role
            };

            session.Participants.Add(participant);
            _activeSessions[sessionId] = session;
            await PersistSessionAsync(session, cancellationToken);

            _logger.LogInformation("User {UserId} joined session {SessionId}", userId, sessionId);

            return participant;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error joining session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task LeaveSessionAsync(
        string sessionId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId, cancellationToken);
            if (session == null) return;

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                participant.LeftAt = DateTime.UtcNow;
                participant.IsActive = false;

                // Release all locks held by this user
                session.Locks.RemoveAll(l => l.LockedBy == userId);

                _activeSessions[sessionId] = session;
                await PersistSessionAsync(session, cancellationToken);

                _logger.LogInformation("User {UserId} left session {SessionId}", userId, sessionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error leaving session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<EditingLock> AcquireLockAsync(
        string sessionId,
        string sectionPath,
        string userId,
        LockType lockType,
        int durationMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId, cancellationToken);
            if (session == null)
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            // Check if section is already locked
            var existingLock = session.Locks.FirstOrDefault(l => 
                l.SectionPath == sectionPath && l.LockExpires > DateTime.UtcNow);

            if (existingLock != null && existingLock.Type == LockType.Exclusive)
            {
                throw new InvalidOperationException(
                    $"Section {sectionPath} is already locked by {existingLock.LockedBy}");
            }

            var newLock = new EditingLock
            {
                SectionPath = sectionPath,
                LockedBy = userId,
                LockExpires = DateTime.UtcNow.AddMinutes(durationMinutes),
                Type = lockType
            };

            session.Locks.Add(newLock);
            _activeSessions[sessionId] = session;
            await PersistSessionAsync(session, cancellationToken);

            _logger.LogInformation("Lock acquired on {SectionPath} by {UserId} in session {SessionId}", 
                sectionPath, userId, sessionId);

            return newLock;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acquiring lock on {SectionPath}", sectionPath);
            throw;
        }
    }

    public async Task ReleaseLockAsync(
        string lockId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var session in _activeSessions.Values)
            {
                var lockToRemove = session.Locks.FirstOrDefault(l => l.LockId == lockId);
                if (lockToRemove != null)
                {
                    if (lockToRemove.LockedBy != userId)
                    {
                        throw new UnauthorizedAccessException(
                            $"User {userId} cannot release lock held by {lockToRemove.LockedBy}");
                    }

                    session.Locks.Remove(lockToRemove);
                    await PersistSessionAsync(session, cancellationToken);

                    _logger.LogInformation("Lock {LockId} released by {UserId}", lockId, userId);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing lock {LockId}", lockId);
            throw;
        }
    }

    public async Task<EditingLock> RefreshLockAsync(
        string lockId,
        string userId,
        int additionalMinutes = 15,
        CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var session in _activeSessions.Values)
            {
                var lockToRefresh = session.Locks.FirstOrDefault(l => l.LockId == lockId);
                if (lockToRefresh != null)
                {
                    if (lockToRefresh.LockedBy != userId)
                    {
                        throw new UnauthorizedAccessException(
                            $"User {userId} cannot refresh lock held by {lockToRefresh.LockedBy}");
                    }

                    lockToRefresh.LockExpires = lockToRefresh.LockExpires.AddMinutes(additionalMinutes);
                    await PersistSessionAsync(session, cancellationToken);

                    _logger.LogInformation("Lock {LockId} refreshed by {UserId}", lockId, userId);
                    return lockToRefresh;
                }
            }

            throw new InvalidOperationException($"Lock {lockId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing lock {LockId}", lockId);
            throw;
        }
    }

    public async Task<List<EditingLock>> GetSessionLocksAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return new List<EditingLock>();
        }

        // Remove expired locks
        var now = DateTime.UtcNow;
        session.Locks.RemoveAll(l => l.LockExpires <= now);
        
        return session.Locks;
    }

    public async Task<DocumentComment> AddCommentAsync(
        string documentId,
        string versionId,
        string sectionPath,
        string content,
        string authorId,
        string authorName,
        CommentType type,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var comment = new DocumentComment
            {
                DocumentId = documentId,
                VersionId = versionId,
                SectionPath = sectionPath,
                Content = content,
                AuthorId = authorId,
                AuthorName = authorName,
                Type = type
            };

            var key = $"{documentId}:{versionId}";
            if (!_comments.ContainsKey(key))
            {
                _comments[key] = new List<DocumentComment>();
            }
            _comments[key].Add(comment);

            await PersistCommentAsync(comment, cancellationToken);

            _logger.LogInformation("Comment {CommentId} added to document {DocumentId} section {SectionPath}", 
                comment.CommentId, documentId, sectionPath);

            return comment;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<DocumentComment> ReplyToCommentAsync(
        string commentId,
        string content,
        string authorId,
        string authorName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var commentList in _comments.Values)
            {
                var parentComment = FindComment(commentList, commentId);
                if (parentComment != null)
                {
                    var reply = new DocumentComment
                    {
                        DocumentId = parentComment.DocumentId,
                        VersionId = parentComment.VersionId,
                        SectionPath = parentComment.SectionPath,
                        Content = content,
                        AuthorId = authorId,
                        AuthorName = authorName,
                        Type = CommentType.General
                    };

                    parentComment.Replies.Add(reply);
                    await PersistCommentAsync(parentComment, cancellationToken);

                    _logger.LogInformation("Reply {ReplyId} added to comment {CommentId}", 
                        reply.CommentId, commentId);

                    return reply;
                }
            }

            throw new InvalidOperationException($"Comment {commentId} not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error replying to comment {CommentId}", commentId);
            throw;
        }
    }

    public async Task ResolveCommentAsync(
        string commentId,
        string resolvedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var commentList in _comments.Values)
            {
                var comment = FindComment(commentList, commentId);
                if (comment != null)
                {
                    comment.ResolvedAt = DateTime.UtcNow;
                    comment.ResolvedBy = resolvedBy;
                    await PersistCommentAsync(comment, cancellationToken);

                    _logger.LogInformation("Comment {CommentId} resolved by {ResolvedBy}", commentId, resolvedBy);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving comment {CommentId}", commentId);
            throw;
        }
    }

    public async Task<List<DocumentComment>> GetCommentsAsync(
        string documentId,
        string? versionId = null,
        bool includeResolved = false,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var key = versionId != null ? $"{documentId}:{versionId}" : null;
        
        var comments = key != null && _comments.ContainsKey(key)
            ? _comments[key]
            : _comments.Values.SelectMany(c => c).Where(c => c.DocumentId == documentId).ToList();

        if (!includeResolved)
        {
            comments = comments.Where(c => c.ResolvedAt == null).ToList();
        }

        return comments;
    }

    public async Task<List<EditingSession>> GetActiveSessionsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        return _activeSessions.Values
            .Where(s => s.DocumentId == documentId && s.Status == EditingSessionStatus.Active)
            .ToList();
    }

    public async Task EndSessionAsync(
        string sessionId,
        string endedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId, cancellationToken);
            if (session == null) return;

            session.EndTime = DateTime.UtcNow;
            session.Status = EditingSessionStatus.Completed;
            session.Locks.Clear();

            _activeSessions[sessionId] = session;
            await PersistSessionAsync(session, cancellationToken);

            _logger.LogInformation("Session {SessionId} ended by {EndedBy}", sessionId, endedBy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending session {SessionId}", sessionId);
            throw;
        }
    }

    public async Task<EditingSession?> GetSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (_activeSessions.TryGetValue(sessionId, out var session))
        {
            return session;
        }

        // Try to load from blob storage
        return await LoadSessionAsync(sessionId, cancellationToken);
    }

    public async Task UpdateParticipantSectionAsync(
        string sessionId,
        string userId,
        string sectionPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await GetSessionAsync(sessionId, cancellationToken);
            if (session == null) return;

            var participant = session.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant != null)
            {
                participant.CurrentSection = sectionPath;
                _activeSessions[sessionId] = session;
                await PersistSessionAsync(session, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating participant section");
            throw;
        }
    }

    public async Task<(bool IsLocked, EditingLock? Lock)> CheckSectionLockAsync(
        string sessionId,
        string sectionPath,
        CancellationToken cancellationToken = default)
    {
        var session = await GetSessionAsync(sessionId, cancellationToken);
        if (session == null)
        {
            return (false, null);
        }

        var activeLock = session.Locks.FirstOrDefault(l => 
            l.SectionPath == sectionPath && l.LockExpires > DateTime.UtcNow);

        return (activeLock != null, activeLock);
    }

    // Private helper methods

    private async Task PersistSessionAsync(EditingSession session, CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString)) return;

        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"sessions/{session.DocumentId}/{session.SessionId}.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        var json = JsonSerializer.Serialize(session, new JsonSerializerOptions { WriteIndented = true });
        await blobClient.UploadAsync(
            new BinaryData(json),
            overwrite: true,
            cancellationToken: cancellationToken);
    }

    private async Task<EditingSession?> LoadSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString)) return null;

        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

        await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: "sessions/", cancellationToken: cancellationToken))
        {
            if (blobItem.Name.Contains(sessionId))
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                var download = await blobClient.DownloadContentAsync(cancellationToken);
                return JsonSerializer.Deserialize<EditingSession>(download.Value.Content.ToString());
            }
        }

        return null;
    }

    private async Task PersistCommentAsync(DocumentComment comment, CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString)) return;

        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"comments/{comment.DocumentId}/{comment.CommentId}.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        var json = JsonSerializer.Serialize(comment, new JsonSerializerOptions { WriteIndented = true });
        await blobClient.UploadAsync(
            new BinaryData(json),
            overwrite: true,
            cancellationToken: cancellationToken);
    }

    private DocumentComment? FindComment(List<DocumentComment> comments, string commentId)
    {
        foreach (var comment in comments)
        {
            if (comment.CommentId == commentId) return comment;
            
            var found = FindComment(comment.Replies, commentId);
            if (found != null) return found;
        }
        return null;
    }
}
