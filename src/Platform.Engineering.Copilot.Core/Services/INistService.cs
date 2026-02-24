using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Dual-source OSCAL catalog service providing offline-capable NIST 800-53 control access.
/// Both Compliance Agent and Knowledge Base Agent consume this via DI (FR-080).
/// </summary>
public interface INistService
{
    /// <summary>Look up a single control by ID (e.g. "AC-2", "AC-2(1)").</summary>
    ControlDefinition? GetControl(string controlId);

    /// <summary>Get all controls in a family (e.g. "AC", "SC").</summary>
    IReadOnlyList<ControlDefinition> GetControlsByFamily(string familyCode);

    /// <summary>Full-text search across control titles and descriptions.</summary>
    IReadOnlyList<ControlDefinition> SearchControls(string query, int maxResults = 25);

    /// <summary>Get controls for a baseline level (High, Moderate, Low).</summary>
    IReadOnlyList<ControlDefinition> GetControlsByBaseline(BaselineLevel baseline);

    /// <summary>Get controls for a compliance framework.</summary>
    IReadOnlyList<ControlDefinition> GetControlsByFramework(ComplianceFramework framework);

    /// <summary>Compare two frameworks showing common, unique-to-A, unique-to-B controls.</summary>
    FrameworkComparisonResult CompareFrameworks(ComplianceFramework a, ComplianceFramework b);

    /// <summary>Get all NIST 800-53 family codes (AC, AT, AU, etc.).</summary>
    IReadOnlyList<string> GetFamilyCodes();

    /// <summary>Attempt to refresh catalog from GitHub (usnistgov/oscal-content).</summary>
    Task RefreshFromGitHubAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the catalog has been loaded.</summary>
    bool IsLoaded { get; }

    /// <summary>Information about the active data source.</summary>
    NistDataSourceInfo ActiveSource { get; }

    /// <summary>
    /// Get enriched control data including statement, guidance, and assessment objectives.
    /// Returns null if the control does not exist in the loaded catalog.
    /// </summary>
    /// <param name="controlId">NIST control ID (e.g. "AC-2").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ControlEnhancement"/> or null if not found.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="controlId"/> is null or empty.</exception>
    Task<ControlEnhancement?> GetControlEnhancementAsync(string controlId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate whether a control ID exists in the loaded catalog.
    /// Returns false if the catalog has not been loaded.
    /// </summary>
    /// <param name="controlId">NIST control ID to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the control exists; false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="controlId"/> is null or empty.</exception>
    Task<bool> ValidateControlIdAsync(string controlId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the catalog version string, or "Unknown" if the catalog is not loaded.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The catalog version string.</returns>
    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a snapshot of the loaded catalog including version, control count, and source.
    /// Returns null if the catalog has not been loaded.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="NistCatalogSnapshot"/> or null if not loaded.</returns>
    Task<NistCatalogSnapshot?> GetCatalogAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// A single NIST 800-53 Rev 5 control with framework overlay metadata.
/// Read-only, in-memory model — not an EF entity.
/// </summary>
public class ControlDefinition
{
    /// <summary>e.g. "AC-2", "AC-2(1)" — includes enhancements.</summary>
    public string ControlId { get; set; } = string.Empty;

    /// <summary>Two-letter family code: AC, AT, AU, etc.</summary>
    public string Family { get; set; } = string.Empty;

    /// <summary>e.g. "Access Control".</summary>
    public string FamilyName { get; set; } = string.Empty;

    /// <summary>Control title from NIST catalog.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Full control statement text.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Supplemental guidance.</summary>
    public string? ImplementationGuidance { get; set; }

    /// <summary>Which baselines include this control.</summary>
    public BaselineApplicability Baselines { get; set; } = new();

    /// <summary>Which frameworks include this control.</summary>
    public FrameworkApplicability Frameworks { get; set; } = new();

    /// <summary>Azure services relevant to this control.</summary>
    public string[] AzureServiceMappings { get; set; } = [];

    /// <summary>DISA STIG cross-references when applicable.</summary>
    public StigReference[]? StigReferences { get; set; }

    /// <summary>P1/P2/P3 priority code from NIST.</summary>
    public string? Priority { get; set; }

    /// <summary>Related control IDs (e.g. AC-2 relates to AC-3, AC-6).</summary>
    public string[] Related { get; set; } = [];
}

/// <summary>
/// Baseline applicability flags.
/// </summary>
public class BaselineApplicability
{
    public bool High { get; set; }
    public bool Moderate { get; set; }
    public bool Low { get; set; }
}

/// <summary>
/// Framework applicability flags.
/// </summary>
public class FrameworkApplicability
{
    /// <summary>Always true (source catalog).</summary>
    public bool Nist80053Rev5 { get; set; } = true;
    public bool FedRampHigh { get; set; }
    public bool FedRampModerate { get; set; }
    public bool DoDIL5 { get; set; }
}

/// <summary>
/// DISA STIG cross-reference.
/// </summary>
public class StigReference
{
    public string StigId { get; set; } = string.Empty;
    public string BenchmarkId { get; set; } = string.Empty;
    /// <summary>CAT I / CAT II / CAT III.</summary>
    public string Severity { get; set; } = string.Empty;
}

/// <summary>
/// Information about the active NIST data source.
/// </summary>
public record NistDataSourceInfo(
    /// <summary>"GitHub" or "EmbeddedFallback".</summary>
    string Source,
    /// <summary>e.g. "NIST SP 800-53 Rev 5 — 2024-12-10".</summary>
    string CatalogVersion,
    DateTimeOffset LoadedAt
);

/// <summary>
/// Enriched view of a NIST control with extracted statement, guidance, and assessment objectives.
/// Read-only record derived from <see cref="ControlDefinition"/>.
/// </summary>
/// <param name="Id">Control ID (e.g. "AC-2").</param>
/// <param name="Title">Control title.</param>
/// <param name="Statement">Text from the control's Description/statement part.</param>
/// <param name="Guidance">Text from implementation guidance (empty string if none).</param>
/// <param name="Objectives">Assessment objective texts (empty list if none).</param>
/// <param name="LastUpdated">Timestamp when the enhancement was extracted.</param>
public record ControlEnhancement(
    string Id,
    string Title,
    string Statement,
    string Guidance,
    IReadOnlyList<string> Objectives,
    DateTime LastUpdated);

/// <summary>
/// Lightweight snapshot of the loaded NIST catalog state.
/// Returned by <see cref="INistService.GetCatalogAsync"/> to avoid exposing internal data structures.
/// </summary>
/// <param name="Version">Catalog version from metadata (e.g. "NIST SP 800-53 Rev 5").</param>
/// <param name="TotalControls">Total control count across all families.</param>
/// <param name="FamilyCount">Number of control families loaded.</param>
/// <param name="LoadedAt">When the catalog was loaded.</param>
/// <param name="Source">"GitHub" or "EmbeddedFallback".</param>
public record NistCatalogSnapshot(
    string Version,
    int TotalControls,
    int FamilyCount,
    DateTimeOffset LoadedAt,
    string Source);

/// <summary>
/// Result of comparing two compliance frameworks.
/// </summary>
public class FrameworkComparisonResult
{
    public ComplianceFramework FrameworkA { get; set; }
    public ComplianceFramework FrameworkB { get; set; }
    public IReadOnlyList<ControlDefinition> Common { get; set; } = [];
    public IReadOnlyList<ControlDefinition> UniqueToA { get; set; } = [];
    public IReadOnlyList<ControlDefinition> UniqueToB { get; set; } = [];
    public int TotalA => Common.Count + UniqueToA.Count;
    public int TotalB => Common.Count + UniqueToB.Count;
}
