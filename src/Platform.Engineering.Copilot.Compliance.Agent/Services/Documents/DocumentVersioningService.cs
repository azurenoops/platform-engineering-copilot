using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Documents;

/// <summary>
/// Service for managing document versions and revisions
/// </summary>
public class DocumentVersioningService : IDocumentVersioningService
{
    private readonly ILogger<DocumentVersioningService> _logger;
    private readonly string _containerName = "compliance-document-versions";

    public DocumentVersioningService(ILogger<DocumentVersioningService> logger)
    {
        _logger = logger;
    }

    public async Task<DocumentVersion> CreateVersionAsync(
        string documentId,
        string createdBy,
        VersionChangeType changeType,
        string comments,
        byte[] content,
        ComplianceDocumentFormat format,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get existing versions to calculate next version number
            var existingVersions = await GetVersionsAsync(documentId, cancellationToken);
            var currentVersion = existingVersions.OrderByDescending(v => v.CreatedDate).FirstOrDefault();
            var nextVersion = currentVersion != null 
                ? CalculateNextVersion(currentVersion.VersionNumber, changeType)
                : "1.0";

            // Calculate content hash
            var contentHash = CalculateHash(content);

            // Create version object
            var version = new DocumentVersion
            {
                DocumentId = documentId,
                VersionNumber = nextVersion,
                CreatedBy = createdBy,
                Comments = comments,
                ChangeType = changeType,
                ContentHash = contentHash,
                SizeBytes = content.Length,
                Format = format,
                ChangeSummary = new List<string> { $"{changeType} - {comments}" }
            };

            // Store version content to blob storage
            var blobUri = await StoreVersionContentAsync(version.VersionId, content, format, cancellationToken);
            version.BlobUri = blobUri;

            // Store version metadata
            await StoreVersionMetadataAsync(version, cancellationToken);

            _logger.LogInformation("Created version {VersionNumber} for document {DocumentId}", 
                version.VersionNumber, documentId);

            return version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating version for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<List<DocumentVersion>> GetVersionsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Azure Storage connection string not configured");
                return new List<DocumentVersion>();
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

            var versions = new List<DocumentVersion>();
            var prefix = $"versions/{documentId}/";

            await foreach (var blobItem in containerClient.GetBlobsAsync(
                prefix: prefix,
                cancellationToken: cancellationToken))
            {
                if (blobItem.Name.EndsWith(".metadata.json"))
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var download = await blobClient.DownloadContentAsync(cancellationToken);
                    var version = JsonSerializer.Deserialize<DocumentVersion>(download.Value.Content.ToString());
                    if (version != null)
                    {
                        versions.Add(version);
                    }
                }
            }

            return versions.OrderByDescending(v => v.CreatedDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting versions for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<DocumentVersion?> GetVersionAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                return null;
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

            // Search for the version metadata
            await foreach (var blobItem in containerClient.GetBlobsAsync(
                prefix: "versions/",
                cancellationToken: cancellationToken))
            {
                if (blobItem.Name.EndsWith(".metadata.json"))
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var download = await blobClient.DownloadContentAsync(cancellationToken);
                    var version = JsonSerializer.Deserialize<DocumentVersion>(download.Value.Content.ToString());
                    
                    if (version?.VersionId == versionId)
                    {
                        return version;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting version {VersionId}", versionId);
            throw;
        }
    }

    public async Task<List<RevisionChange>> CompareVersionsAsync(
        string versionId1,
        string versionId2,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var version1 = await GetVersionAsync(versionId1, cancellationToken);
            var version2 = await GetVersionAsync(versionId2, cancellationToken);

            if (version1 == null || version2 == null)
            {
                return new List<RevisionChange>();
            }

            var content1 = await GetVersionContentAsync(versionId1, cancellationToken);
            var content2 = await GetVersionContentAsync(versionId2, cancellationToken);

            if (content1 == null || content2 == null)
            {
                return new List<RevisionChange>();
            }

            // Simple comparison - in production, use a proper diff algorithm
            var changes = new List<RevisionChange>();
            
            if (version1.ContentHash != version2.ContentHash)
            {
                changes.Add(new RevisionChange
                {
                    Operation = ChangeOperationType.Modify,
                    ChangeDescription = $"Content changed from version {version1.VersionNumber} to {version2.VersionNumber}",
                    OldContent = $"Hash: {version1.ContentHash}",
                    NewContent = $"Hash: {version2.ContentHash}",
                    Timestamp = version2.CreatedDate,
                    ChangedBy = version2.CreatedBy
                });
            }

            return changes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing versions {VersionId1} and {VersionId2}", versionId1, versionId2);
            throw;
        }
    }

    public async Task<DocumentRevision> CreateRevisionAsync(
        string versionId,
        string revisedBy,
        string revisionReason,
        List<RevisionChange> changes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var version = await GetVersionAsync(versionId, cancellationToken);
            if (version == null)
            {
                throw new InvalidOperationException($"Version {versionId} not found");
            }

            var revision = new DocumentRevision
            {
                DocumentId = version.DocumentId,
                VersionId = versionId,
                RevisedBy = revisedBy,
                RevisionReason = revisionReason,
                Changes = changes
            };

            await StoreRevisionAsync(revision, cancellationToken);

            _logger.LogInformation("Created revision {RevisionId} for version {VersionId}", 
                revision.RevisionId, versionId);

            return revision;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating revision for version {VersionId}", versionId);
            throw;
        }
    }

    public async Task<List<DocumentRevision>> GetRevisionHistoryAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                return new List<DocumentRevision>();
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

            var revisions = new List<DocumentRevision>();
            var prefix = $"revisions/{documentId}/";

            await foreach (var blobItem in containerClient.GetBlobsAsync(
                prefix: prefix,
                cancellationToken: cancellationToken))
            {
                if (blobItem.Name.EndsWith(".json"))
                {
                    var blobClient = containerClient.GetBlobClient(blobItem.Name);
                    var download = await blobClient.DownloadContentAsync(cancellationToken);
                    var revision = JsonSerializer.Deserialize<DocumentRevision>(download.Value.Content.ToString());
                    if (revision != null)
                    {
                        revisions.Add(revision);
                    }
                }
            }

            return revisions.OrderByDescending(r => r.RevisionDate).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revision history for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<DocumentRevision> ApproveRevisionAsync(
        string revisionId,
        string approvedBy,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var revisions = await GetAllRevisionsAsync(cancellationToken);
            var revision = revisions.FirstOrDefault(r => r.RevisionId == revisionId);

            if (revision == null)
            {
                throw new InvalidOperationException($"Revision {revisionId} not found");
            }

            revision.ApprovalStatus = "Approved";
            revision.ApprovedBy = approvedBy;
            revision.ApprovalDate = DateTime.UtcNow;

            await StoreRevisionAsync(revision, cancellationToken);

            _logger.LogInformation("Approved revision {RevisionId} by {ApprovedBy}", revisionId, approvedBy);

            return revision;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving revision {RevisionId}", revisionId);
            throw;
        }
    }

    public async Task<DocumentRevision> RejectRevisionAsync(
        string revisionId,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var revisions = await GetAllRevisionsAsync(cancellationToken);
            var revision = revisions.FirstOrDefault(r => r.RevisionId == revisionId);

            if (revision == null)
            {
                throw new InvalidOperationException($"Revision {revisionId} not found");
            }

            revision.ApprovalStatus = "Rejected";
            revision.ApprovedBy = rejectedBy;
            revision.ApprovalDate = DateTime.UtcNow;
            revision.RevisionReason += $" | Rejected: {reason}";

            await StoreRevisionAsync(revision, cancellationToken);

            _logger.LogInformation("Rejected revision {RevisionId} by {RejectedBy}: {Reason}", 
                revisionId, rejectedBy, reason);

            return revision;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting revision {RevisionId}", revisionId);
            throw;
        }
    }

    public async Task<DocumentVersion> RollbackToVersionAsync(
        string documentId,
        string versionId,
        string rolledBackBy,
        string reason,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targetVersion = await GetVersionAsync(versionId, cancellationToken);
            if (targetVersion == null)
            {
                throw new InvalidOperationException($"Version {versionId} not found");
            }

            var content = await GetVersionContentAsync(versionId, cancellationToken);
            if (content == null)
            {
                throw new InvalidOperationException($"Version content not found for {versionId}");
            }

            // Create new version based on rolled-back content
            var newVersion = await CreateVersionAsync(
                documentId,
                rolledBackBy,
                VersionChangeType.MajorUpdate,
                $"Rolled back to version {targetVersion.VersionNumber}. Reason: {reason}",
                content,
                targetVersion.Format,
                cancellationToken);

            _logger.LogInformation("Rolled back document {DocumentId} to version {VersionNumber}", 
                documentId, targetVersion.VersionNumber);

            return newVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rolling back document {DocumentId} to version {VersionId}", 
                documentId, versionId);
            throw;
        }
    }

    public string CalculateNextVersion(string currentVersion, VersionChangeType changeType)
    {
        var parts = currentVersion.Split('.');
        var major = int.Parse(parts[0]);
        var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        var patch = parts.Length > 2 ? int.Parse(parts[2]) : 0;

        return changeType switch
        {
            VersionChangeType.MajorUpdate => $"{major + 1}.0.0",
            VersionChangeType.MinorUpdate => $"{major}.{minor + 1}.0",
            VersionChangeType.PatchUpdate => $"{major}.{minor}.{patch + 1}",
            VersionChangeType.InitialVersion => "1.0.0",
            VersionChangeType.Revision => currentVersion,
            _ => $"{major}.{minor}.{patch + 1}"
        };
    }

    public async Task<byte[]?> GetVersionContentAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var version = await GetVersionAsync(versionId, cancellationToken);
            if (version == null || string.IsNullOrEmpty(version.BlobUri))
            {
                return null;
            }

            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                return null;
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
            
            // Extract blob name from URI
            var blobName = version.BlobUri.Split(new[] { _containerName + "/" }, StringSplitOptions.None).Last();
            var blobClient = containerClient.GetBlobClient(blobName);

            var download = await blobClient.DownloadContentAsync(cancellationToken);
            return download.Value.Content.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting version content for {VersionId}", versionId);
            throw;
        }
    }

    // Private helper methods

    private async Task<string> StoreVersionContentAsync(
        string versionId,
        byte[] content,
        ComplianceDocumentFormat format,
        CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Azure Storage connection string not configured");
        }

        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var extension = format switch
        {
            ComplianceDocumentFormat.DOCX => "docx",
            ComplianceDocumentFormat.PDF => "pdf",
            ComplianceDocumentFormat.HTML => "html",
            _ => "md"
        };

        var blobName = $"versions/{versionId}/content.{extension}";
        var blobClient = containerClient.GetBlobClient(blobName);

        await blobClient.UploadAsync(
            new BinaryData(content),
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = GetContentType(format)
                }
            },
            cancellationToken);

        return blobClient.Uri.ToString();
    }

    private async Task StoreVersionMetadataAsync(
        DocumentVersion version,
        CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Azure Storage connection string not configured");
        }

        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"versions/{version.DocumentId}/{version.VersionId}.metadata.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        var json = JsonSerializer.Serialize(version, new JsonSerializerOptions { WriteIndented = true });
        await blobClient.UploadAsync(
            new BinaryData(json),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
    }

    private async Task StoreRevisionAsync(
        DocumentRevision revision,
        CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Azure Storage connection string not configured");
        }

        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"revisions/{revision.DocumentId}/{revision.RevisionId}.json";
        var blobClient = containerClient.GetBlobClient(blobName);

        var json = JsonSerializer.Serialize(revision, new JsonSerializerOptions { WriteIndented = true });
        await blobClient.UploadAsync(
            new BinaryData(json),
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" } },
            cancellationToken);
    }

    private async Task<List<DocumentRevision>> GetAllRevisionsAsync(CancellationToken cancellationToken)
    {
        var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
        if (string.IsNullOrEmpty(connectionString))
        {
            return new List<DocumentRevision>();
        }

        var blobServiceClient = new BlobServiceClient(connectionString);
        var containerClient = blobServiceClient.GetBlobContainerClient(_containerName);

        var revisions = new List<DocumentRevision>();

        await foreach (var blobItem in containerClient.GetBlobsAsync(
            prefix: "revisions/",
            cancellationToken: cancellationToken))
        {
            if (blobItem.Name.EndsWith(".json"))
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                var download = await blobClient.DownloadContentAsync(cancellationToken);
                var revision = JsonSerializer.Deserialize<DocumentRevision>(download.Value.Content.ToString());
                if (revision != null)
                {
                    revisions.Add(revision);
                }
            }
        }

        return revisions;
    }

    private string CalculateHash(byte[] content)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(content);
        return Convert.ToBase64String(hash);
    }

    private string GetContentType(ComplianceDocumentFormat format)
    {
        return format switch
        {
            ComplianceDocumentFormat.DOCX => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ComplianceDocumentFormat.PDF => "application/pdf",
            ComplianceDocumentFormat.HTML => "text/html",
            _ => "text/markdown"
        };
    }
}
