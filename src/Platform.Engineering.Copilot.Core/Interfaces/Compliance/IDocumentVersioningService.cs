using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Service for managing document versions and revisions
/// </summary>
public interface IDocumentVersioningService
{
    /// <summary>
    /// Create a new version of a document
    /// </summary>
    Task<DocumentVersion> CreateVersionAsync(
        string documentId,
        string createdBy,
        VersionChangeType changeType,
        string comments,
        byte[] content,
        ComplianceDocumentFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all versions of a document
    /// </summary>
    Task<List<DocumentVersion>> GetVersionsAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific version
    /// </summary>
    Task<DocumentVersion?> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compare two versions
    /// </summary>
    Task<List<RevisionChange>> CompareVersionsAsync(
        string versionId1,
        string versionId2,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a revision for a version
    /// </summary>
    Task<DocumentRevision> CreateRevisionAsync(
        string versionId,
        string revisedBy,
        string revisionReason,
        List<RevisionChange> changes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get revision history for a document
    /// </summary>
    Task<List<DocumentRevision>> GetRevisionHistoryAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approve a revision
    /// </summary>
    Task<DocumentRevision> ApproveRevisionAsync(
        string revisionId,
        string approvedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reject a revision
    /// </summary>
    Task<DocumentRevision> RejectRevisionAsync(
        string revisionId,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback to a previous version
    /// </summary>
    Task<DocumentVersion> RollbackToVersionAsync(
        string documentId,
        string versionId,
        string rolledBackBy,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate the next version number
    /// </summary>
    string CalculateNextVersion(string currentVersion, VersionChangeType changeType);

    /// <summary>
    /// Get version content
    /// </summary>
    Task<byte[]?> GetVersionContentAsync(
        string versionId,
        CancellationToken cancellationToken = default);
}
