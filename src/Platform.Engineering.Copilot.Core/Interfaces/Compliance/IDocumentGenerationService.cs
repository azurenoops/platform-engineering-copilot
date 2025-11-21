using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Core.Interfaces.Compliance;

/// <summary>
/// Service for generating ATO compliance documents (SSP, SAR, POA&M, control narratives)
/// </summary>
public interface IDocumentGenerationService
{
    /// <summary>
    /// Generate a control implementation narrative
    /// </summary>
    Task<ControlNarrative> GenerateControlNarrativeAsync(
        string controlId,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate System Security Plan (SSP)
    /// </summary>
    Task<GeneratedDocument> GenerateSSPAsync(
        string subscriptionId,
        SspParameters parameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate Security Assessment Report (SAR)
    /// </summary>
    Task<GeneratedDocument> GenerateSARAsync(
        string subscriptionId,
        string assessmentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generate Plan of Action & Milestones (POA&M)
    /// </summary>
    Task<GeneratedDocument> GeneratePOAMAsync(
        string subscriptionId,
        List<AtoFinding>? findings = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all documents in an ATO package
    /// </summary>
    Task<List<ComplianceDocumentMetadata>> ListDocumentsAsync(
        string packageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export document to specified format
    /// </summary>
    Task<byte[]> ExportDocumentAsync(
        string documentId,
        ComplianceDocumentFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply formatting standards to document
    /// </summary>
    Task<GeneratedDocument> FormatDocumentAsync(
        GeneratedDocument document,
        FormattingStandard standard,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Store a generated document to Azure Blob Storage
    /// </summary>
    Task<string> StoreDocumentAsync(
        GeneratedDocument document,
        byte[]? exportedBytes = null,
        ComplianceDocumentFormat format = ComplianceDocumentFormat.Markdown,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a document from Azure Blob Storage
    /// </summary>
    Task<(byte[] Content, string ContentType)?> RetrieveDocumentAsync(
        string blobName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all stored documents for a package/subscription
    /// </summary>
    Task<List<ComplianceDocumentMetadata>> ListStoredDocumentsAsync(
        string packageId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a document from blob storage
    /// </summary>
    Task<bool> DeleteDocumentAsync(
        string blobName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Formatting standards for compliance documents
/// </summary>
public enum FormattingStandard
{
    NIST,
    FedRAMP,
    DoD_RMF,
    FISMA
}
