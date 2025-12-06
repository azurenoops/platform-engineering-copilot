using Platform.Engineering.Copilot.Core.Models.CodeScanning;
using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Interface for storing and retrieving compliance evidence in blob storage.
/// Supports evidence packages, compliance packages, and scan results with
/// optional versioning and immutability for audit compliance.
/// </summary>
public interface IEvidenceStorageService
{
    /// <summary>
    /// Stores security evidence package to blob storage.
    /// </summary>
    /// <param name="evidencePackage">The security evidence package to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URI of the stored blob.</returns>
    Task<string> StoreEvidencePackageAsync(
        SecurityEvidencePackage evidencePackage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores compliance evidence package to blob storage.
    /// </summary>
    /// <param name="evidencePackage">The compliance evidence package to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URI of the stored blob.</returns>
    Task<string> StoreComplianceEvidencePackageAsync(
        EvidencePackage evidencePackage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores scan results to blob storage.
    /// </summary>
    /// <param name="scanType">Type of scan (e.g., "security", "compliance", "code").</param>
    /// <param name="scanResults">The scan results object to store.</param>
    /// <param name="projectPath">Path to the scanned project.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The URI of the stored blob.</returns>
    Task<string> StoreScanResultsAsync(
        string scanType,
        object scanResults,
        string projectPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves evidence package from storage.
    /// </summary>
    /// <param name="packageId">The unique identifier of the evidence package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The security evidence package, or null if not found.</returns>
    Task<SecurityEvidencePackage?> RetrieveEvidencePackageAsync(
        string packageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up evidence older than the configured retention period.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of deleted evidence items.</returns>
    Task<int> CleanupExpiredEvidenceAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all evidence packages within an optional date range.
    /// </summary>
    /// <param name="startDate">Optional start date filter.</param>
    /// <param name="endDate">Optional end date filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of evidence package metadata.</returns>
    Task<List<EvidencePackageMetadata>> ListEvidencePackagesAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Metadata for stored evidence package.
/// </summary>
public class EvidencePackageMetadata
{
    /// <summary>
    /// Unique identifier for the evidence package.
    /// </summary>
    public string PackageId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the blob in storage.
    /// </summary>
    public string BlobName { get; set; } = string.Empty;

    /// <summary>
    /// URI to access the blob.
    /// </summary>
    public string BlobUri { get; set; } = string.Empty;

    /// <summary>
    /// When the evidence was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Size of the evidence package in bytes.
    /// </summary>
    public long SizeInBytes { get; set; }

    /// <summary>
    /// Compliance frameworks this evidence relates to.
    /// </summary>
    public List<string> ComplianceFrameworks { get; set; } = new();

    /// <summary>
    /// Path to the project this evidence was collected from.
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;
}
