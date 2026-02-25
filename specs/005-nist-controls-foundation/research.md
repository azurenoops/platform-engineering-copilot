# Research: NIST Controls Knowledge Foundation

**Feature**: 005-nist-controls-foundation  
**Date**: 2026-02-24  
**Status**: Complete

## Research Tasks & Findings

### R-001: Memory Caching Strategy

**Question**: What NuGet package and DI pattern should be used for IMemoryCache in .NET 9?

**Decision**: Use `Microsoft.Extensions.Caching.Memory` 9.0.0, added to Core.csproj

**Rationale**: 
- `IMemoryCache` is the standard .NET caching abstraction
- Registered via `AddMemoryCache()` as a singleton — matches `NistService`'s singleton lifetime
- NOT transitively available from any current Core dependency — must be explicitly added
- The existing `ConcurrentDictionary<string, ControlDefinition>` remains for indexed control lookups; `IMemoryCache` is used for the catalog-level cache with TTL

**Alternatives Considered**:
- `LazyCache` — adds unnecessary wrapper over IMemoryCache; overkill for a single cache entry
- Custom timer-based expiration on ConcurrentDictionary — reinvents the wheel; IMemoryCache handles sliding + absolute expiration natively

### R-002: HTTP Resilience / Retry Policy

**Question**: What's the best approach for adding retry policies given the current NistService architecture (singleton, raw HttpClient constructor injection)?

**Decision**: Use `Polly.Core` 8.5.0 directly with a `ResiliencePipeline<HttpResponseMessage>` built in the NistService constructor

**Rationale**:
- The service is registered as singleton with a raw `HttpClient` injected via constructor (not `IHttpClientFactory`)
- `Microsoft.Extensions.Http.Polly` and `Microsoft.Extensions.Http.Resilience` both require `IHttpClientFactory` and `AddHttpClient<T>()`, which would register the service as transient — breaking the singleton requirement and all 13+ consumers
- `Polly.Core` v8 works directly with any HttpClient — wrap `_httpClient.GetAsync()` in `pipeline.ExecuteAsync()`
- Exponential backoff configuration: `Math.Pow(RetryDelaySeconds, attempt)` → 2s, 4s, 8s by default
- Handles: `HttpRequestException`, `TaskCanceledException`, non-success status codes

**Alternatives Considered**:
- Refactoring to `IHttpClientFactory` + `AddStandardResilienceHandler()` — idiomatic .NET 9 but would change service lifetime from singleton to transient, breaking all existing consumers
- Manual retry loop (no Polly) — simpler for a single HTTP call but less configurable and loses the structured retry policy benefits
- `Polly` legacy v7 — superseded by `Polly.Core` v8; the v8 API is cleaner (`ResiliencePipeline`)

### R-003: BackgroundService Availability

**Question**: Does the Agents project have access to `BackgroundService` transitively?

**Decision**: Yes — no additional package references needed

**Rationale**:
- `BackgroundService` lives in `Microsoft.Extensions.Hosting.Abstractions`
- The Agents project references Core, which references `Serilog.AspNetCore`, which transitively brings in `Microsoft.Extensions.Hosting.Abstractions`
- `AddHostedService<T>()` is available in the Mcp project (entry point) which uses `Microsoft.NET.Sdk.Web`

**Alternatives Considered**: None — the transitive dependency chain is sufficient.

### R-004: Health Check Registration Pattern

**Question**: How to add a second `IHealthCheck` alongside the existing `PlatformHealthCheck`?

**Decision**: Chain `.AddCheck<NistControlsHealthCheck>("nist-controls")` on the existing health checks builder

**Rationale**:
- The project already has `AddHealthChecks().AddCheck<PlatformHealthCheck>("platform-health")` in DI setup
- Multiple `.AddCheck<T>()` calls can be chained — all registered checks run on `/health`
- Tags can be added for filtering: `tags: new[] { "nist", "ready" }`

**Alternatives Considered**: None — standard pattern.

### R-005: Observability Without OpenTelemetry SDK

**Question**: Can lightweight distributed tracing and metrics be achieved without adding OpenTelemetry NuGet packages?

**Decision**: Use BCL `System.Diagnostics.ActivitySource` + `System.Diagnostics.Metrics.Meter` — both are built into .NET 9 runtime, no packages needed

**Rationale**:
- `ActivitySource` and `Meter` are in-box with .NET 9's `System.Diagnostics.DiagnosticSource` runtime assembly
- Creates `Activity` spans compatible with OpenTelemetry exporters if added later
- `Counter<T>` and `Histogram<T>` record metrics that can be scraped by any listener
- Matches the spec's explicit statement: "lightweight wrappers around standard System.Diagnostics"
- Current project has no OpenTelemetry SDK — adding it would be scope creep

**Alternatives Considered**:
- OpenTelemetry .NET SDK packages — heavyweight dependency for the current feature scope; can be added later as an enhancement
- Extending existing `MetricsService` — that service tracks tool invocations; NIST fetch metrics have different semantics (cache hits, fetch duration, retry counts)

### R-006: Singleton Warmup Service ↔ Singleton NistService DI

**Question**: Can the background warmup service directly inject the singleton NistService?

**Decision**: Yes — direct constructor injection is safe and correct

**Rationale**:
- Both `NistService` (singleton) and `BackgroundService` (effectively singleton via `AddHostedService`) share the same lifetime
- No `IServiceScopeFactory` pattern needed — that's only required for scoped dependencies like `DbContext`
- Constructor: `NistControlsCacheWarmupService(INistService nistService, IOptions<NistControlsOptions> options, ILogger<> logger)`

**Alternatives Considered**:
- `IServiceScopeFactory` pattern — unnecessary complexity since all dependencies are singleton

### R-007: Offline Fallback File Strategy

**Question**: Should the offline fallback use the existing embedded resources or a separate deployable file?

**Decision**: Use a separate deployable file at `Data/nist-800-53-fallback.json`, distinct from the embedded 10-control test catalog

**Rationale**:
- The existing embedded JSON (`Services/NistData/nist-800-53-rev5.json`) is a 124-line, 10-control simplified catalog for development/testing — not suitable as a production fallback
- The full NIST 800-53 Rev 5 catalog has 323+ controls across 18 families — too large to embed efficiently
- A separate file at `Data/nist-800-53-fallback.json` can be updated independently of the application binary
- For air-gapped deployments, the file is deployed alongside the application
- Resolved with `Path.Combine(ContentRootPath, options.OfflineFallbackPath)`

**Alternatives Considered**:
- Embedding the full catalog as a resource — bloats the assembly; harder to update in air-gapped environments
- Downloading at deploy time — requires network access, defeating the purpose of air-gapped support

### R-008: Configuration Section Naming

**Question**: The existing config uses `NistData` section name. Should we rename to `NistControls` to match the spec?

**Decision**: Rename from `NistData` to `NistControls` in appsettings.json and migrate the code to use `NistControlsOptions` bound from this section

**Rationale**:
- The spec specifies `NistControls` as the configuration section name
- The existing `NistData` section in `appsettings.json` has only 4 raw keys read via `IConfiguration.GetValue<>()` — no options class exists
- Renaming is a clean break that aligns with the new validated options class
- The `NistService` constructor currently takes `IConfiguration` directly — it will be changed to take `IOptions<NistControlsOptions>` instead

**Alternatives Considered**:
- Keeping `NistData` name — would create confusion between old raw keys and new strongly-typed options
- Supporting both names with a fallback — unnecessary complexity; no external consumers depend on the config key names

### R-009: Interface Extension vs New Interface

**Question**: Should new async methods be added to the existing `INistService` or to a new `INistControlsService` interface?

**Decision**: Extend the existing `INistService` interface with the 4 new async methods

**Rationale**:
- The spec explicitly states "All existing synchronous methods MUST continue to work without modification" (FR-006)
- Adding async methods to an existing interface is backward-compatible — existing consumers don't need to call the new methods
- The 13+ consumers inject `INistService` — splitting into two interfaces would require consumers to choose which to inject
- The `NistService` implementation already handles both sync and async patterns (it has `RefreshFromGitHubAsync`)

**Alternatives Considered**:
- New `INistControlsService` interface that extends `INistService` — adds indirection without benefit; consumers would need to decide which interface to inject
- Adapter pattern wrapping the service — overcomplicated for adding 4 methods

### R-010: Preventing Thundering Herd on Cache Miss

**Question**: How to prevent concurrent requests from all triggering separate catalog fetches on a cache miss?

**Decision**: Use `SemaphoreSlim(1, 1)` in `NistService` to serialize fetch operations; double-check cache after acquiring the semaphore

**Rationale**:
- Standard pattern: acquire semaphore → check cache again → fetch if still missing → release
- Prevents N concurrent callers from all hitting GitHub when the cache expires
- `SemaphoreSlim` is await-friendly and low-overhead
- The warmup service normally prevents cache misses entirely, so this is a safety net

**Alternatives Considered**:
- `Lazy<Task<T>>` — harder to invalidate and refresh; doesn't work well with TTL-based caching
- `ConcurrentDictionary.GetOrAdd` with async factory — not directly supported; requires workaround
