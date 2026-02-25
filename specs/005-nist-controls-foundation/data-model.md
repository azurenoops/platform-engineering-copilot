# Data Model: NIST Controls Knowledge Foundation

**Feature**: 005-nist-controls-foundation  
**Date**: 2026-02-24

## Entity Diagram

```
┌──────────────────────────────────────────────────────────┐
│                    INistService                          │
│                  (extended interface)                     │
│                                                          │
│  EXISTING (unchanged):                                   │
│  ├── GetControl(controlId) → ControlDefinition?          │
│  ├── GetControlsByFamily(family) → List<ControlDef>      │
│  ├── SearchControls(query, max) → List<ControlDef>       │
│  ├── GetControlsByBaseline(baseline) → List<ControlDef>  │
│  ├── GetControlsByFramework(fw) → List<ControlDef>       │
│  ├── CompareFrameworks(a, b) → FrameworkComparisonResult │
│  ├── GetFamilyCodes() → List<string>                     │
│  ├── RefreshFromGitHubAsync(ct) → Task                   │
│  ├── IsLoaded → bool                                     │
│  └── ActiveSource → NistDataSourceInfo                   │
│                                                          │
│  NEW (added):                                            │
│  ├── GetControlEnhancementAsync(id, ct)                  │
│  │   → Task<ControlEnhancement?>                         │
│  ├── ValidateControlIdAsync(id, ct) → Task<bool>         │
│  ├── GetVersionAsync(ct) → Task<string>                  │
│  └── GetCatalogAsync(ct) → Task<NistCatalogSnapshot?>    │
└──────────────────────────────────────────────────────────┘
         │ uses                    │ uses
         ▼                        ▼
┌────────────────────┐   ┌───────────────────────┐
│  ControlDefinition │   │  ControlEnhancement   │
│  (EXISTING - no    │   │  (NEW record)         │
│   changes)         │   │                       │
│                    │   │  Id: string            │
│  ControlId         │──▶│  Title: string         │
│  Family            │   │  Statement: string     │
│  FamilyName        │   │  Guidance: string      │
│  Title             │   │  Objectives: List<str> │
│  Description       │   │  LastUpdated: DateTime │
│  Guidance?         │   └───────────────────────┘
│  Baselines         │
│  Frameworks        │
│  AzureServiceMap[] │   ┌───────────────────────┐
│  StigReferences[]? │   │  NistCatalogSnapshot  │
│  Priority?         │   │  (NEW record)         │
│  Related[]         │   │                       │
└────────────────────┘   │  Version: string      │
                         │  TotalControls: int   │
                         │  FamilyCount: int     │
                         │  LoadedAt: DateTimeOff │
                         │  Source: string        │
                         └───────────────────────┘

┌───────────────────────────────────────────────────────┐
│              NistControlsOptions (NEW)                │
│                                                       │
│  BaseUrl: string [Required]                           │
│  TargetVersion: string? (nullable)                    │
│  TimeoutSeconds: int [Range(10,300)] = 60             │
│  CacheDurationHours: int [Range(1,168)] = 24          │
│  MaxRetryAttempts: int [Range(1,5)] = 3               │
│  RetryDelaySeconds: int [Range(1,60)] = 2             │
│  EnableOfflineFallback: bool = true                   │
│  OfflineFallbackPath: string? = "Data/nist-...-fb"    │
│  EnableMemoryCache: bool = true                       │
│  EnableDetailedLogging: bool = false                  │
└───────────────────────────────────────────────────────┘
```

## Entities

### ControlDefinition (EXISTING — NO CHANGES)

The primary model for a NIST 800-53 control. Already defined in `INistService.cs`. All 13+ consumers use this model. No fields added, removed, or modified.

| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| ControlId | `string` | e.g. "AC-2", "AC-2(1)" | Non-empty |
| Family | `string` | Two-letter code: AC, AT, AU, etc. | Non-empty |
| FamilyName | `string` | e.g. "Access Control" | Non-empty |
| Title | `string` | Control title | Non-empty |
| Description | `string` | Full statement text | Non-empty |
| ImplementationGuidance | `string?` | Supplemental guidance | Nullable |
| Baselines | `BaselineApplicability` | High/Moderate/Low flags | Non-null |
| Frameworks | `FrameworkApplicability` | NIST/FedRAMP/DoD flags | Non-null |
| AzureServiceMappings | `string[]` | Azure resource types | Default empty |
| StigReferences | `StigReference[]?` | DISA STIG cross-references | Nullable |
| Priority | `string?` | P1/P2/P3 | Nullable |
| Related | `string[]` | Related control IDs | Default empty |

### ControlEnhancement (NEW)

Enriched view of a control with extracted statement, guidance, and assessment objectives. Read-only, derived from `ControlDefinition`.

| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| Id | `string` | Control ID (e.g. "AC-2") | Required, non-empty |
| Title | `string` | Control title | Required, non-empty |
| Statement | `string` | Text from "statement" part/Description | Required (may be empty string) |
| Guidance | `string` | Text from "guidance" part/ImplementationGuidance | May be empty string |
| Objectives | `IReadOnlyList<string>` | Assessment objective texts | Empty list if none |
| LastUpdated | `DateTime` | Timestamp of extraction | UTC now |

**Derivation rule**: Given a `ControlDefinition cd`:
- `Statement` = `cd.Description` (the existing Description field contains the statement text)
- `Guidance` = `cd.ImplementationGuidance ?? ""`
- `Objectives` = empty list (the simplified embedded catalog doesn't have structured objective parts; full OSCAL catalog from GitHub may have them in the raw JSON)
- `LastUpdated` = `DateTime.UtcNow`

### NistCatalogSnapshot (NEW)

Lightweight summary of the loaded catalog state, returned by `GetCatalogAsync`. Avoids exposing the full internal data structure.

| Field | Type | Description | Validation |
|-------|------|-------------|------------|
| Version | `string` | Catalog version from metadata | Non-empty, default "Unknown" |
| TotalControls | `int` | Total control count across all families | ≥ 0 |
| FamilyCount | `int` | Number of control families loaded | ≥ 0 |
| LoadedAt | `DateTimeOffset` | When the catalog was loaded | UTC |
| Source | `string` | "GitHub" or "EmbeddedFallback" | Non-empty |

### NistControlsOptions (NEW)

Configuration POCO bound from `appsettings.json` section `NistControls`. Validated at startup.

| Field | Type | Default | Validation | Description |
|-------|------|---------|------------|-------------|
| BaseUrl | `string` | NIST OSCAL GitHub URL | `[Required]` | Base URL for catalog download |
| TargetVersion | `string?` | `null` | — | Specific version; used as cache key suffix |
| TimeoutSeconds | `int` | `60` | `[Range(10, 300)]` | HTTP client timeout |
| CacheDurationHours | `int` | `24` | `[Range(1, 168)]` | IMemoryCache absolute expiration |
| MaxRetryAttempts | `int` | `3` | `[Range(1, 5)]` | Polly retry count |
| RetryDelaySeconds | `int` | `2` | `[Range(1, 60)]` | Exponential backoff base |
| EnableOfflineFallback | `bool` | `true` | — | Try local file on remote failure |
| OfflineFallbackPath | `string?` | `"Data/nist-800-53-fallback.json"` | — | Relative path to fallback JSON |
| EnableMemoryCache | `bool` | `true` | — | Whether to use IMemoryCache TTL |
| EnableDetailedLogging | `bool` | `false` | — | Verbose HTTP/lookup logging |

### NistDataSourceInfo (EXISTING — NO CHANGES)

```csharp
public record NistDataSourceInfo(string Source, string CatalogVersion, DateTimeOffset LoadedAt);
```

### BaselineApplicability (EXISTING — NO CHANGES)

```csharp
public class BaselineApplicability { bool High; bool Moderate; bool Low; }
```

### FrameworkApplicability (EXISTING — NO CHANGES)

```csharp
public class FrameworkApplicability { bool Nist80053Rev5; bool FedRampHigh; bool FedRampModerate; bool DoDIL5; }
```

### StigReference (EXISTING — NO CHANGES)

```csharp
public class StigReference { string StigId; string BenchmarkId; string Severity; }
```

### FrameworkComparisonResult (EXISTING — NO CHANGES)

```csharp
public class FrameworkComparisonResult { FrameworkA; FrameworkB; Common; UniqueToA; UniqueToB; TotalA; TotalB; }
```

## Relationships

- `ControlEnhancement` is derived from `ControlDefinition` (1:1, read-only projection)
- `NistCatalogSnapshot` is derived from the loaded catalog state (singleton snapshot)
- `NistControlsOptions` configures the `NistService` behavior (injected via `IOptions<T>`)
- `NistDataSourceInfo` describes which source loaded the current catalog (set on load)

## State Transitions

### Catalog Load State

```
                  ┌──────────────┐
                  │  Not Loaded  │  (IsLoaded = false)
                  │  Source=None │
                  └──────┬───────┘
                         │
                         ▼ InitializeAsync() or GetCatalogAsync()
                  ┌──────────────┐
              ┌───│   Loading    │───┐
              │   └──────────────┘   │
              │                      │
      Remote OK                Remote FAIL
              │                      │
              ▼                      ▼
   ┌────────────────┐     ┌────────────────┐
   │ Loaded (GitHub) │     │Loaded (Fallback)│
   │ Source=GitHub   │     │ Source=Embedded │
   │ IsLoaded=true   │     │ IsLoaded=true   │
   └────────────────┘     └────────────────┘
              │                      │
              └──────────┬───────────┘
                         │
                         ▼ Cache expires / Warmup refresh
                  ┌──────────────┐
                  │  Refreshing  │  (still serving cached data)
                  └──────────────┘
```

### Cache Entry Lifecycle

```
Cache Miss → Fetch (with Polly retry) → Cache Set (24h abs, 6h sliding, High priority) → Serve from cache → ... → Warmup refresh at 90% TTL → Cache Set (fresh entry)
```
