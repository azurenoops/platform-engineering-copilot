using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Interface for document processing and text extraction
/// </summary>
public interface IDocumentProcessor
{
    /// <summary>
    /// Extracts text content from various document types
    /// </summary>
    Task<string> ExtractTextAsync(AnalysisDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes architecture diagrams and extracts component information
    /// </summary>
    Task<ArchitectureAnalysisResult> ProcessArchitectureDiagramAsync(
        AnalysisDocument document, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets supported document types
    /// </summary>
    IEnumerable<string> GetSupportedFileTypes();
}

/// <summary>
/// Interface for vector-based search of NIST standards
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Searches for relevant NIST standards using semantic similarity
    /// </summary>
    Task<IEnumerable<NistStandard>> SearchRelevantStandardsAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs hybrid search combining vector and keyword search
    /// </summary>
    Task<IEnumerable<NistStandard>> HybridSearchAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes NIST standards for search
    /// </summary>
    Task IndexStandardsAsync(
        IEnumerable<NistStandard> standards,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for NIST standards repository
/// </summary>
public interface INistStandardsRepository
{
    /// <summary>
    /// Gets all NIST 800-53 standards
    /// </summary>
    Task<IEnumerable<NistStandard>> GetAllStandardsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets standards by control family
    /// </summary>
    Task<IEnumerable<NistStandard>> GetStandardsByFamilyAsync(
        string family,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific standard by ID
    /// </summary>
    Task<NistStandard?> GetStandardByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches standards by text query
    /// </summary>
    Task<IEnumerable<NistStandard>> SearchStandardsAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets standards for a specific baseline (Low, Moderate, High)
    /// </summary>
    Task<IEnumerable<NistStandard>> GetStandardsByBaselineAsync(
        string baseline,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for architecture analysis capabilities
/// </summary>
public interface IArchitectureAnalyzer
{
    /// <summary>
    /// Analyzes architecture diagrams for component identification
    /// </summary>
    Task<ArchitectureAnalysisResult> AnalyzeArchitectureAsync(
        AnalysisDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Identifies security gaps in architecture
    /// </summary>
    Task<IEnumerable<ComplianceGap>> IdentifySecurityGapsAsync(
        ArchitectureAnalysisResult architecture,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates platform integration recommendations
    /// </summary>
    Task<IEnumerable<ArchitectureRecommendation>> GeneratePlatformRecommendationsAsync(
        ArchitectureAnalysisResult architecture,
        string platformType = "FlankSpeed",
        CancellationToken cancellationToken = default);
}