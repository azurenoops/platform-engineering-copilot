# Feature Specification: NIST Controls Knowledge Foundation

**Feature Branch**: `005-nist-controls-foundation`  
**Created**: 2026-02-24  
**Status**: Draft  
**Input**: User description: "NIST Controls Knowledge Foundation"

## Overview

Enhance the existing `NistService` / `INistService` into a production-ready NIST Controls Knowledge Foundation. The current implementation provides a functional dual-source OSCAL catalog service (GitHub primary, embedded JSON fallback) with control lookup, family enumeration, search, baseline filtering, framework comparison, overlay loading, STIG mappings, and Azure service mappings. It lacks several production-readiness features: a background hosted service to initialize the catalog at startup and refresh it proactively, a dedicated health check, structured configuration options, memory caching with configurable TTL, resilience policies (retries with exponential backoff), distributed tracing, compliance metrics, control enhancement extraction, and control ID validation. This feature bridges those gaps while preserving the existing interface, models, consumers (13+ agent tools), and 40+ unit tests.

## Existing Code Inventory

The following code already exists and must be preserved/enhanced (not replaced):

- **`INistService`** (Core/Services/INistService.cs, 142 lines): 9-member interface + 6 model classes (`ControlDefinition`, `BaselineApplicability`, `FrameworkApplicability`, `StigReference`, `NistDataSourceInfo`, `FrameworkComparisonResult`)
- **`NistService`** (Core/Services/NistService.cs, 369 lines): Full implementation with `ConcurrentDictionary`, dual-source loading, embedded resource parsing, overlay/STIG/Azure mappings
- **`NistServiceTests`** (Tests.Unit/Services/NistServiceTests.cs, 698 lines): 40+ tests covering all existing functionality
- **Embedded data** (Core/Services/NistData/): 6 JSON files — catalog, overlays, STIG/Azure mappings
- **DI Registration** (Agents/Extensions/ServiceCollectionExtensions.cs, line 49): `AddSingleton<INistService, NistService>()`
- **13 consumer tools** across KnowledgeBase and Compliance agents injecting `INistService`

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Automatic Catalog Initialization at Startup (Priority: P1)

When the platform application starts, the NIST catalog is automatically loaded and validated in the background so that the first user request for compliance data never hits a cold-start delay.

**Why this priority**: Without automatic startup initialization, the first user to request compliance data would trigger a synchronous catalog load, causing visible delays. Every downstream consumer (13+ tools) depends on the catalog being ready.

**Independent Test**: Start the application and verify that within 15 seconds the catalog is loaded, `IsLoaded` returns true, and health check reports Healthy — all without any user interaction.

**Acceptance Scenarios**:

1. **Given** the application just started, **When** the background service runs, **Then** the catalog is fetched from remote (or loaded from fallback) and cached within 15 seconds of startup
2. **Given** the catalog is cached, **When** the cache approaches expiration (at 90% of TTL), **Then** the background service proactively refreshes the cache before it expires
3. **Given** the remote fetch fails during background refresh, **When** the error is logged, **Then** the service waits 5 minutes and retries without crashing
4. **Given** the catalog was loaded, **When** a consumer calls any query method, **Then** results are returned from cache with sub-10ms latency

---

### User Story 2 — Resilient Data Fetching with Retry & Fallback (Priority: P1)

When fetching the NIST catalog from the upstream OSCAL repository, the system retries failed requests with exponential backoff and falls back to an offline copy when all retries are exhausted, ensuring the platform works in air-gapped environments.

**Why this priority**: The platform serves FedRAMP and DoD IL5+ environments where outbound internet access may be restricted. Without resilience, a single network glitch would leave the system without compliance data.

**Independent Test**: Configure the remote URL to an unreachable endpoint, verify the service retries 3 times with increasing delays, then loads the offline fallback successfully.

**Acceptance Scenarios**:

1. **Given** the remote OSCAL endpoint is unreachable, **When** the service attempts to fetch the catalog, **Then** it retries up to the configured maximum (default 3) with exponential backoff (2s, 4s, 8s)
2. **Given** all remote retries are exhausted, **When** offline fallback is enabled, **Then** the service loads the local fallback JSON file and logs a warning
3. **Given** both remote fetch and offline fallback fail, **When** a consumer calls a query method, **Then** the method returns null/empty gracefully (no exceptions thrown to callers)
4. **Given** the system is deployed in an air-gapped environment, **When** the application starts, **Then** the catalog loads exclusively from the offline fallback file

---

### User Story 3 — Control Enhancement Extraction (Priority: P2)

When a user asks the Compliance Agent to explain a NIST control, the system extracts and returns the control's statement (what to do), supplemental guidance (how to do it), and assessment objectives in a structured format.

**Why this priority**: The `NistControlExplainerTool` needs structured control text to generate conversational explanations. This enriches the existing `GetControl` capability with part-level extraction.

**Independent Test**: Call `GetControlEnhancementAsync("AC-2")` and verify the response contains a non-empty statement, guidance text, and list of objectives.

**Acceptance Scenarios**:

1. **Given** a valid control ID, **When** `GetControlEnhancementAsync` is called, **Then** a structured response is returned with statement, guidance, and objectives extracted from the control's parts
2. **Given** a control that has no guidance part, **When** the enhancement is extracted, **Then** the guidance field is empty but the statement is still returned
3. **Given** an invalid control ID, **When** `GetControlEnhancementAsync` is called, **Then** null is returned

---

### User Story 4 — Control ID Validation (Priority: P2)

System operators and compliance workflows can validate that control IDs referenced in configuration, templates, and scanning rules actually exist in the loaded NIST catalog.

**Why this priority**: The system depends on 11+ hardcoded control IDs (SC-13, SC-28, AC-3, etc.). If a catalog version changes control numbering, the system needs to detect and report invalid references early.

**Independent Test**: Call `ValidateControlIdAsync("AC-2")` and verify it returns true; call with "AC-99" and verify false.

**Acceptance Scenarios**:

1. **Given** a control ID that exists in the catalog, **When** `ValidateControlIdAsync` is called, **Then** true is returned
2. **Given** a control ID that does not exist, **When** validated, **Then** false is returned
3. **Given** multiple control IDs are validated in bulk (e.g., system startup validation), **When** any ID is invalid, **Then** a warning is logged identifying the missing control

---

### User Story 5 — Health Monitoring (Priority: P2)

Operations teams can check the health of the NIST Controls Service through the platform's health check endpoint, getting status on catalog availability, version, and response times.

**Why this priority**: Production deployments need health probes for Kubernetes liveness checks and monitoring dashboards. A degraded NIST service affects all compliance operations.

**Independent Test**: Hit the health check endpoint and verify the response includes version, control validation results, and response time — all without manual intervention.

**Acceptance Scenarios**:

1. **Given** the catalog is loaded and 3 test controls (AC-3, SC-13, AU-2) are valid, **When** the health check runs, **Then** it returns Healthy with version and response time
2. **Given** some test controls are invalid, **When** checked, **Then** it returns Degraded with the count of valid controls
3. **Given** the catalog is not loaded, **When** checked, **Then** it returns Unhealthy
4. **Given** the health check takes longer than 5 seconds, **When** it completes, **Then** the result is Degraded with a timeout note

---

### User Story 6 — Structured Configuration (Priority: P2)

Administrators can configure NIST service behavior (remote URL, timeout, cache duration, retry policy, fallback settings) through standard application configuration without code changes.

**Why this priority**: Different deployment environments (dev, staging, prod, air-gapped) require different settings. Currently the service reads configuration via raw `IConfiguration` keys without validation.

**Independent Test**: Set `NistControls:CacheDurationHours` to 48 in appsettings, restart, and verify the cache TTL changes accordingly.

**Acceptance Scenarios**:

1. **Given** configuration is provided in appsettings.json under a dedicated section, **When** the service starts, **Then** validated options are bound and used for all operations
2. **Given** invalid configuration values (e.g., timeout < 10), **When** validated, **Then** startup fails fast with a clear error message
3. **Given** no configuration is provided, **When** the service starts, **Then** sensible defaults are used (24h cache, 60s timeout, 3 retries, fallback enabled)

---

### User Story 7 — Observability & Metrics (Priority: P3)

Platform operators can monitor NIST service performance through OpenTelemetry-compatible metrics and distributed tracing, seeing cache hit rates, fetch latencies, and error counts.

**Why this priority**: Observability is essential for production operations but the core functionality works without it. It enhances troubleshooting and SLA monitoring.

**Independent Test**: Trigger a catalog fetch, verify that a tracing span is created with cache.hit tag and a metric counter is incremented.

**Acceptance Scenarios**:

1. **Given** a catalog fetch occurs, **When** the operation completes, **Then** a distributed tracing span is recorded with tags for cache hit/miss, control count, and success/failure
2. **Given** any catalog query method is called, **When** metrics are enabled, **Then** call count and duration are recorded
3. **Given** an error occurs during fetch, **When** traced, **Then** the span includes the error message and fallback usage indicator

---

### User Story 8 — Memory Caching with Configurable TTL (Priority: P1)

The catalog is cached in memory after the first fetch, with configurable absolute and sliding expiration, so that repeated queries by the 13+ consumer tools never re-fetch from remote.

**Why this priority**: Without caching, every compliance assessment (which calls `GetControlsByFamily` 18 times — once per family) would trigger 18 separate catalog fetches. Caching is fundamental to performance.

**Independent Test**: Fetch the catalog, call `GetControl` 100 times, verify zero additional HTTP requests are made and all responses come from cache.

**Acceptance Scenarios**:

1. **Given** the catalog has been fetched, **When** any query method is called within the cache TTL, **Then** the cached catalog is used (no HTTP request)
2. **Given** the cache has expired, **When** a query method is called, **Then** a fresh fetch is triggered
3. **Given** memory pressure on the host, **When** the cache eviction policy runs, **Then** the NIST catalog is among the last items evicted (high priority)

---

### Edge Cases

- What happens when the embedded fallback JSON file is corrupted or missing? The service logs an error and returns null; consumer tools display "No controls available" gracefully.
- What happens when the GitHub OSCAL repository changes its JSON schema? Deserialization fails, the service falls back to the offline copy, and a Degraded health status is reported.
- What happens when the catalog contains zero controls after parsing? The service logs a warning and treats it as a failed load, falling back to offline.
- What happens when a consumer passes a null or empty control ID? An `ArgumentException` is thrown before any catalog lookup.
- What happens when the background warmup service is cancelled during shutdown? The cancellation token is honored gracefully and the loop exits cleanly.
- What happens when two concurrent requests trigger a cache miss simultaneously? Only one fetch executes; the other awaits the same result (no thundering herd).

## Requirements *(mandatory)*

### Functional Requirements

#### Interface Enhancement

- **FR-001**: System MUST add `GetControlEnhancementAsync(string controlId, CancellationToken)` to `INistService` returning a structured record with statement, guidance, objectives, and timestamp
- **FR-002**: System MUST add `ValidateControlIdAsync(string controlId, CancellationToken)` to `INistService` returning a boolean indicating whether the control exists
- **FR-003**: System MUST add `GetVersionAsync(CancellationToken)` to `INistService` returning the catalog version string or "Unknown" if unavailable
- **FR-004**: System MUST add `GetCatalogAsync(CancellationToken)` to `INistService` returning the full parsed catalog for bulk operations
- **FR-005**: All new interface methods MUST accept `CancellationToken` for cooperative cancellation
- **FR-006**: All existing synchronous methods MUST continue to work without modification to preserve backward compatibility with 13+ consumers

#### Configuration

- **FR-007**: System MUST define a `NistControlsOptions` class with validated properties: BaseUrl, TargetVersion (string?, nullable), TimeoutSeconds (10–300, default 60), CacheDurationHours (1–168, default 24), MaxRetryAttempts (1–5, default 3), RetryDelaySeconds (1–60, default 2), EnableOfflineFallback (default true), OfflineFallbackPath (default "Data/nist-800-53-fallback.json"), EnableMemoryCache (default true), EnableDetailedLogging (default false)
- **FR-008**: System MUST bind configuration from the `NistControls` section of appsettings.json
- **FR-009**: System MUST validate configuration at startup using data annotation attributes (`[Required]`, `[Range]`)

#### Memory Caching

- **FR-010**: System MUST cache the parsed catalog in `IMemoryCache` with configurable absolute expiration (default 24 hours)
- **FR-011**: System MUST set sliding expiration to 25% of the absolute expiration (default 6 hours)
- **FR-012**: System MUST set cache entry priority to High to resist eviction under memory pressure
- **FR-013**: System MUST cache both the catalog object and the version string with identical expiration settings
- **FR-014**: System MUST use a versioned cache key when `TargetVersion` is configured, otherwise use "latest"

#### Resilience

- **FR-015**: System MUST retry failed HTTP requests using exponential backoff with configurable attempt count and base delay
- **FR-016**: System MUST handle `HttpRequestException`, `TaskCanceledException`, and non-success HTTP status codes in the retry policy
- **FR-017**: System MUST log each retry attempt with the attempt number, maximum retries, and delay duration
- **FR-018**: System MUST fall back to a local offline JSON file when remote fetch fails and `EnableOfflineFallback` is true
- **FR-019**: System MUST resolve the offline fallback path relative to the application's content root directory
- **FR-020**: System MUST return null gracefully when both remote fetch and offline fallback fail

#### Background Warmup Service

- **FR-021**: System MUST implement a `BackgroundService` that initializes the catalog cache at startup
- **FR-022**: The warmup service MUST wait a configurable delay (default 10 seconds) before first initialization to allow the HTTP pipeline to warm up
- **FR-023**: The warmup service MUST proactively refresh the cache at 90% of the configured TTL (default 21.6 hours for 24-hour TTL)
- **FR-024**: The warmup service MUST log the total control count on successful cache population
- **FR-025**: The warmup service MUST validate system-critical control IDs after each refresh (SC-13, SC-28, AC-3, AC-6, SC-7, AC-4, AU-2, SI-4, CP-9, CP-10, IA-5)
- **FR-026**: The warmup service MUST wait 5 minutes and retry when initialization fails, without crashing
- **FR-027**: The warmup service MUST handle `OperationCanceledException` gracefully during application shutdown

#### Health Check

- **FR-028**: System MUST implement `IHealthCheck` for the NIST Controls Service
- **FR-029**: The health check MUST verify catalog version availability and validate 3 test controls (AC-3, SC-13, AU-2)
- **FR-030**: The health check MUST return Healthy when all test controls are valid and response time is under 5 seconds
- **FR-031**: The health check MUST return Degraded when some (but not all) test controls are valid, or the version is unknown, or response time exceeds 5 seconds
- **FR-032**: The health check MUST return Unhealthy when no test controls are valid or an exception occurs
- **FR-033**: The health check MUST include structured data: version, valid test control count, response time in milliseconds, timestamp, cache duration hours, offline fallback enabled status

#### Observability

- **FR-034**: System MUST create an `Activity` span for every catalog fetch operation with tags: cache.hit, success, control.count, error, fallback.used
- **FR-035**: System MUST record counter and histogram metrics for catalog API calls: operation name, success/failure, duration
- **FR-036**: System MUST use structured logging with semantic parameters throughout all operations

#### Data Models

- **FR-037**: System MUST define a `ControlEnhancement` record with properties: Id, Title, Statement, Guidance, Objectives (list of strings), LastUpdated (DateTime)
- **FR-038**: The `ControlEnhancement.Statement` MUST be extracted from the control's "statement" part prose
- **FR-039**: The `ControlEnhancement.Guidance` MUST be extracted from the control's "guidance" part prose
- **FR-040**: The `ControlEnhancement.Objectives` MUST be collected from all "objective" part prose values

#### Offline Fallback Data

- **FR-041**: System MUST include a full NIST SP 800-53 Rev 5 catalog JSON file as an offline fallback at the configured path
- **FR-042**: The offline fallback MUST use the same OSCAL JSON format as the upstream GitHub source for consistent deserialization

#### DI Registration

- **FR-043**: System MUST register the enhanced `NistService` maintaining singleton lifetime for the service instance
- **FR-044**: System MUST register `IMemoryCache` if not already registered
- **FR-045**: System MUST register the background warmup service as a hosted service
- **FR-046**: System MUST register the health check in the health check builder
- **FR-047**: System MUST bind and validate `NistControlsOptions` from configuration

#### Testing

- **FR-048**: All new methods MUST have unit tests achieving 80%+ code coverage
- **FR-049**: Existing 40+ unit tests MUST continue to pass without modification
- **FR-050**: Resilience scenarios (retry, fallback, timeout) MUST be covered by unit tests
- **FR-051**: Background warmup service lifecycle MUST be covered by unit tests
- **FR-052**: Health check scenarios (Healthy, Degraded, Unhealthy) MUST be covered by unit tests

### Key Entities

- **ControlDefinition**: Existing model representing a single NIST 800-53 control with ID, family, title, description, guidance, baselines, framework flags, STIG references, Azure service mappings, priority, and related controls. Preserved as-is.
- **ControlEnhancement**: New enriched view of a control with extracted statement text, supplemental guidance text, assessment objectives list, and extraction timestamp. Read-only, derived from ControlDefinition's parts.
- **NistControlsOptions**: New configuration record defining service behavior — remote URL, timeouts, cache TTL, retry policy, fallback settings. Validated at startup.
- **NistDataSourceInfo**: Existing record tracking the active data source (GitHub/Embedded), catalog version, and load timestamp. Preserved as-is.

## Assumptions

- The existing `INistService` interface will be extended with new async methods while keeping all existing synchronous methods intact. No existing consumer code needs to change.
- The embedded OSCAL JSON data files in `Core/Services/NistData/` contain a simplified 10-control catalog sufficient for development/testing. The full 323-control production catalog will be fetched from GitHub at runtime and cached.
- The offline fallback file path (`Data/nist-800-53-fallback.json`) refers to a file deployed alongside the application binary, separate from the embedded resources used for the simplified development catalog.
- The `ComplianceMetricsService` will be a lightweight wrapper around standard `System.Diagnostics.ActivitySource` (for tracing) and `System.Diagnostics.Metrics.Meter` (for counters/histograms) — not heavyweight OpenTelemetry SDK dependencies. Both the `ActivitySource` and `Meter` are internal members of `ComplianceMetricsService`, not standalone classes.
- Control ID validation for the 11 system-critical control IDs is handled inline within `NistControlsCacheWarmupService` (FR-025), not as a separate `ComplianceValidationService` class.
- The singleton registration of `NistService` will be preserved (not changed to scoped) because the existing consumers are registered as singletons and expect a singleton NIST service.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Application startup completes NIST catalog initialization within 15 seconds, with the first user request seeing cached data (zero cold-start penalty)
- **SC-002**: 100% of the existing 40+ NistService unit tests continue to pass without modification after all enhancements
- **SC-003**: New functionality achieves 80%+ unit test code coverage across all added methods, services, and health checks
- **SC-004**: Control lookup, family enumeration, and search queries return results in under 10 milliseconds when served from cache
- **SC-005**: The platform functions correctly in an air-gapped environment with no outbound internet, loading the catalog from the offline fallback within 5 seconds
- **SC-006**: Health check endpoint returns structured status (Healthy/Degraded/Unhealthy) within 5 seconds with version, control validation results, and response time
- **SC-007**: Failed remote fetches trigger exactly the configured number of retries with exponential backoff before falling back to offline data
- **SC-008**: The background warmup service refreshes the cache proactively before expiration, with zero cache-miss fetches occurring on user-facing request paths under normal operation
- **SC-009**: All 11 system-critical control IDs are validated at startup, with warnings logged for any missing controls
- **SC-010**: Build produces zero errors and zero new warnings after all changes
