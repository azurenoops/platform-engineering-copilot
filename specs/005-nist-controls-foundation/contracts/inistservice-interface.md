# Contract: INistService Interface

**Feature**: 005-nist-controls-foundation  
**Date**: 2026-02-24  
**Type**: Public Service Interface (consumed by 13+ agent tools)

## Interface Summary

`INistService` is the primary contract for NIST 800-53 control data access across the Platform Engineering Copilot. It is injected into 13+ agent tools across KnowledgeBase and Compliance agents. This contract is extended (not replaced) to add 4 new async methods while preserving all 9 existing members.

## Full Interface Contract

```csharp
namespace Platform.Engineering.Copilot.Core.Services;

public interface INistService
{
    // ═══════════════════════════════════════════════════════════
    // EXISTING MEMBERS (unchanged — backward compatible)
    // ═══════════════════════════════════════════════════════════

    /// <summary>Look up a single control by ID (e.g. "AC-2", "AC-2(1)").</summary>
    /// <param name="controlId">Case-insensitive control ID</param>
    /// <returns>The control definition, or null if not found</returns>
    ControlDefinition? GetControl(string controlId);

    /// <summary>Get all controls in a family (e.g. "AC", "SC").</summary>
    /// <param name="familyCode">Two-letter family code, case-insensitive</param>
    /// <returns>Ordered list of controls in the family; empty if family not found</returns>
    IReadOnlyList<ControlDefinition> GetControlsByFamily(string familyCode);

    /// <summary>Full-text search across control titles and descriptions.</summary>
    /// <param name="query">Search term (case-insensitive)</param>
    /// <param name="maxResults">Maximum results to return (default 25)</param>
    /// <returns>Matched controls ranked by relevance; empty if no matches</returns>
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

    // ═══════════════════════════════════════════════════════════
    // NEW MEMBERS (added by this feature)
    // ═══════════════════════════════════════════════════════════

    /// <summary>Get structured enhancement details for a control: statement, guidance, objectives.</summary>
    /// <param name="controlId">Case-insensitive control ID (e.g. "AC-2")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Enhancement details, or null if control not found</returns>
    /// <exception cref="ArgumentException">Thrown if controlId is null or empty</exception>
    Task<ControlEnhancement?> GetControlEnhancementAsync(
        string controlId, CancellationToken cancellationToken = default);

    /// <summary>Validate whether a control ID exists in the loaded catalog.</summary>
    /// <param name="controlId">Case-insensitive control ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>true if the control exists; false otherwise</returns>
    /// <exception cref="ArgumentException">Thrown if controlId is null or empty</exception>
    Task<bool> ValidateControlIdAsync(
        string controlId, CancellationToken cancellationToken = default);

    /// <summary>Get the catalog version string.</summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Version string (e.g. "5.1.1"), or "Unknown" if catalog not loaded</returns>
    Task<string> GetVersionAsync(CancellationToken cancellationToken = default);

    /// <summary>Get a snapshot of the loaded catalog state.</summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Catalog snapshot with version, control count, source info; null if not loaded</returns>
    Task<NistCatalogSnapshot?> GetCatalogAsync(CancellationToken cancellationToken = default);
}
```

## New Model Contracts

```csharp
/// <summary>
/// Enriched view of a NIST control with extracted statement, guidance, and objectives.
/// Immutable record — derived from ControlDefinition.
/// </summary>
public record ControlEnhancement(
    string Id,
    string Title,
    string Statement,
    string Guidance,
    IReadOnlyList<string> Objectives,
    DateTime LastUpdated);

/// <summary>
/// Lightweight snapshot of the loaded NIST catalog state.
/// </summary>
public record NistCatalogSnapshot(
    string Version,
    int TotalControls,
    int FamilyCount,
    DateTimeOffset LoadedAt,
    string Source);
```

## Behavioral Contract

### Parameter Validation

| Method | Parameter | Rule | Error |
|--------|-----------|------|-------|
| `GetControlEnhancementAsync` | `controlId` | Must not be null, empty, or whitespace | `ArgumentException` |
| `ValidateControlIdAsync` | `controlId` | Must not be null, empty, or whitespace | `ArgumentException` |
| `GetVersionAsync` | — | No parameters to validate | — |
| `GetCatalogAsync` | — | No parameters to validate | — |

### Return Value Semantics

| Method | Return | When |
|--------|--------|------|
| `GetControlEnhancementAsync` | `ControlEnhancement` object | Control found |
| `GetControlEnhancementAsync` | `null` | Control not found or catalog not loaded |
| `ValidateControlIdAsync` | `true` | Control exists in catalog |
| `ValidateControlIdAsync` | `false` | Control not found or catalog not loaded |
| `GetVersionAsync` | Version string (e.g., "5.1.1") | Catalog loaded |
| `GetVersionAsync` | `"Unknown"` | Catalog not loaded |
| `GetCatalogAsync` | `NistCatalogSnapshot` | Catalog loaded |
| `GetCatalogAsync` | `null` | Catalog not loaded |

### Thread Safety

All methods are thread-safe. The underlying `ConcurrentDictionary` and `IMemoryCache` (singleton) handle concurrent reads. A `SemaphoreSlim(1,1)` serializes fetch operations to prevent thundering herd.

### Cancellation

All new async methods honor `CancellationToken`. If cancelled:
- Operations in progress are interrupted
- `OperationCanceledException` propagates to the caller
- No partial state corruption occurs

## Consumers (no changes required)

All existing consumers inject `INistService` and call existing synchronous methods. They do not need modification:

| Consumer | Methods Used |
|----------|-------------|
| SearchControlsTool | `SearchControls` |
| ExplainControlTool | `GetControl` |
| ControlMappingTool | `GetControl`, `GetControlsByFamily` |
| CompareFrameworksTool | `CompareFrameworks` |
| FrameworkSummaryTool | `GetControlsByFramework` |
| GetStigGuidanceTool | `GetControl` |
| GetAtoChecklistTool | `GetControlsByFramework` |
| ImplementationExamplesTool | `GetControl` |
| ComplianceAssessTool | `GetControlsByFamily` |
| ComplianceMapControlsTool | `GetControl` |
| ComplianceGetControlFamilyTool | `GetControlsByFamily` |
| ComplianceCompareFrameworksTool | `CompareFrameworks` |
| KnowledgeBaseAgent | `IsLoaded`, `ActiveSource` |

## New Consumers (introduced by this feature)

| Consumer | Methods Used |
|----------|-------------|
| NistControlsCacheWarmupService | `GetCatalogAsync`, `ValidateControlIdAsync` |
| NistControlsHealthCheck | `GetVersionAsync`, `ValidateControlIdAsync` |
