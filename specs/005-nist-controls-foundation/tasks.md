# Tasks: NIST Controls Knowledge Foundation

**Input**: Design documents from `/specs/005-nist-controls-foundation/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — spec mandates unit tests (FR-048 through FR-052) with 80%+ coverage on new code.

**Organization**: Tasks grouped by user story to enable independent implementation and testing. US6 (Configuration) is delivered through the Foundational phase since it architecturally blocks all other stories.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Core project**: `src/Platform.Engineering.Copilot.Core/`
- **Agents project**: `src/Platform.Engineering.Copilot.Agents/`
- **Mcp project**: `src/Platform.Engineering.Copilot.Mcp/`
- **Unit tests**: `tests/Platform.Engineering.Copilot.Tests.Unit/`

---

## Phase 1: Setup

**Purpose**: Add NuGet dependencies and update configuration files

- [x] T001 Add Microsoft.Extensions.Caching.Memory 9.0.0 and Polly.Core 8.5.0 package references to src/Platform.Engineering.Copilot.Core/Platform.Engineering.Copilot.Core.csproj
- [x] T002 [P] Update src/Platform.Engineering.Copilot.Mcp/appsettings.json — rename NistData section to NistControls with expanded properties (BaseUrl, TimeoutSeconds, CacheDurationHours, MaxRetryAttempts, RetryDelaySeconds, EnableOfflineFallback, OfflineFallbackPath, EnableMemoryCache, EnableDetailedLogging) and update appsettings.Development.json accordingly

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Configuration class, data model records, interface extension, and DI registration that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

**Note**: This phase also delivers **User Story 6 — Structured Configuration (P2)** since `NistControlsOptions` is architecturally foundational to all other stories (FR-007, FR-008, FR-009).

- [x] T003 Create NistControlsOptions.cs with 10 validated properties (BaseUrl [Required], TimeoutSeconds [Range(10,300)] default 60, CacheDurationHours [Range(1,168)] default 24, MaxRetryAttempts [Range(1,5)] default 3, RetryDelaySeconds [Range(1,60)] default 2, EnableOfflineFallback default true, OfflineFallbackPath default "Data/nist-800-53-fallback.json", EnableMemoryCache default true, EnableDetailedLogging default false, TargetVersion nullable) in src/Platform.Engineering.Copilot.Core/Configuration/NistControlsOptions.cs
- [x] T004 [P] Add ControlEnhancement record (Id, Title, Statement, Guidance, Objectives IReadOnlyList<string>, LastUpdated DateTime) and NistCatalogSnapshot record (Version, TotalControls, FamilyCount, LoadedAt DateTimeOffset, Source) to src/Platform.Engineering.Copilot.Core/Services/INistService.cs
- [x] T005 Add 4 new async method signatures to INistService interface — GetControlEnhancementAsync(string, CancellationToken), ValidateControlIdAsync(string, CancellationToken), GetVersionAsync(CancellationToken), GetCatalogAsync(CancellationToken) — in src/Platform.Engineering.Copilot.Core/Services/INistService.cs
- [x] T006 Register IMemoryCache via AddMemoryCache(), bind and validate NistControlsOptions from NistControls config section via AddOptions with ValidateDataAnnotations, in src/Platform.Engineering.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs

**Checkpoint**: Foundation ready — all interfaces, models, configuration, and DI wiring in place for user story implementation

---

## Phase 3: User Story 8 — Memory Caching with Configurable TTL (Priority: P1) 🎯 MVP

**Goal**: Cache the NIST catalog in IMemoryCache after first fetch so repeated queries by 13+ consumer tools never re-fetch from remote

**Independent Test**: Fetch the catalog, call GetControl 100 times, verify zero additional HTTP requests are made

### Implementation for User Story 8

- [x] T007 [US8] Refactor NistService constructor to accept IMemoryCache and IOptions<NistControlsOptions> alongside existing ILogger, IConfiguration, HttpClient — preserve all existing behavior in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T008 [US8] Implement IMemoryCache catalog caching with configurable absolute expiration (default 24h), sliding expiration at 25% of absolute (default 6h), and CacheItemPriority.High in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T009 [US8] Add SemaphoreSlim(1,1) with double-check cache pattern to prevent thundering herd on concurrent cache misses in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T010 [US8] Implement GetCatalogAsync returning NistCatalogSnapshot with version, total controls, family count, load timestamp, and source in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T011 [US8] Implement GetVersionAsync returning catalog version string or "Unknown" if not loaded in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T012 [P] [US8] Write caching unit tests — cache hit/miss, TTL expiration, sliding expiration, High priority, thundering herd serialization, GetCatalogAsync snapshot, GetVersionAsync, cached lookup performance assertion (<10ms per SC-004) — in tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistServiceEnhancedTests.cs

**Checkpoint**: Catalog is cached with configurable TTL, concurrent access is safe, catalog snapshot and version are queryable

---

## Phase 4: User Story 2 — Resilient Data Fetching with Retry & Fallback (Priority: P1)

**Goal**: Retry failed HTTP requests with exponential backoff and fall back to offline copy when all retries exhausted

**Independent Test**: Configure remote URL to unreachable endpoint, verify 3 retries with increasing delays, then offline fallback loads successfully

### Implementation for User Story 2

- [x] T013 [US2] Build Polly ResiliencePipeline<HttpResponseMessage> with exponential backoff (base delay from options, Math.Pow for escalation) handling HttpRequestException, TaskCanceledException, and non-success status codes in NistService constructor in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T014 [US2] Wrap RefreshFromGitHubAsync HTTP calls with retry pipeline and add structured retry logging (attempt number, max retries, delay duration) in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T015 [US2] Implement offline fallback file loading — resolve path via Path.Combine(ContentRootPath, options.OfflineFallbackPath), load and parse when remote fails and EnableOfflineFallback is true, return null gracefully when both fail in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T016 [P] [US2] Create offline fallback JSON file scaffold using OSCAL format matching upstream GitHub source at src/Platform.Engineering.Copilot.Mcp/Data/nist-800-53-fallback.json with CopyToOutputDirectory in Mcp.csproj
- [x] T017 [P] [US2] Write retry policy (attempt count, backoff delays), offline fallback (success and failure), and graceful error handling unit tests in tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistServiceEnhancedTests.cs

**Checkpoint**: Service retries with exponential backoff, falls back to offline file, works in air-gapped environments

---

## Phase 5: User Story 3 — Control Enhancement Extraction (Priority: P2)

**Goal**: Extract structured statement, guidance, and assessment objectives from a NIST control

**Independent Test**: Call GetControlEnhancementAsync("AC-2") and verify non-empty statement, guidance text, and objectives list

### Implementation for User Story 3

- [x] T018 [US3] Implement GetControlEnhancementAsync — extract Statement from Description, Guidance from ImplementationGuidance, Objectives from parts, with ArgumentException for null/empty controlId and null return for missing controls in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T019 [P] [US3] Write control enhancement extraction unit tests — valid control with full data, control with no guidance (empty string), invalid control ID (null return), null/empty argument (ArgumentException) in tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistServiceEnhancedTests.cs

**Checkpoint**: Control enhancement extraction returns structured data for any valid control ID

---

## Phase 6: User Story 4 — Control ID Validation (Priority: P2)

**Goal**: Validate whether a control ID exists in the loaded catalog

**Independent Test**: ValidateControlIdAsync("AC-2") returns true; ValidateControlIdAsync("AC-99") returns false

### Implementation for User Story 4

- [x] T020 [US4] Implement ValidateControlIdAsync — check ConcurrentDictionary for control existence, throw ArgumentException for null/empty controlId, return false if catalog not loaded in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T021 [P] [US4] Write control ID validation unit tests — valid ID (true), invalid ID (false), null/empty (ArgumentException), catalog not loaded (false) in tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistServiceEnhancedTests.cs

**Checkpoint**: Control ID validation works for all edge cases

---

## Phase 7: User Story 1 — Automatic Catalog Initialization at Startup (Priority: P1)

**Goal**: Background service initializes and proactively refreshes the NIST catalog cache so no user request hits cold-start delay

**Independent Test**: Start application, verify catalog loaded within 15 seconds, IsLoaded returns true, health check reports Healthy — without user interaction

**Note**: This story is P1 priority but executes in Phase 7 because it depends on caching (US8/Phase 3) and validation (US4/Phase 6)

### Implementation for User Story 1

- [x] T022 [US1] Create NistControlsCacheWarmupService as BackgroundService — configurable startup delay (default 10s), call GetCatalogAsync to populate cache, validate 11 critical control IDs (SC-13, SC-28, AC-3, AC-6, SC-7, AC-4, AU-2, SI-4, CP-9, CP-10, IA-5), proactive refresh at 90% of TTL, 5-minute retry on failure, graceful OperationCanceledException handling in src/Platform.Engineering.Copilot.Agents/Compliance/Services/NistControlsCacheWarmupService.cs
- [x] T023 [US1] Register NistControlsCacheWarmupService via AddHostedService in src/Platform.Engineering.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [x] T024 [P] [US1] Write warmup service unit tests — successful initialization with control count logging, proactive refresh timing, retry after failure with 5-min wait, critical control validation warnings, graceful cancellation on shutdown in tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistControlsCacheWarmupServiceTests.cs

**Checkpoint**: Application starts with automatic catalog initialization, proactive refresh, and critical control validation

---

## Phase 8: User Story 5 — Health Monitoring (Priority: P2)

**Goal**: Health check endpoint reports NIST service status with version, control validation, and response time

**Independent Test**: Hit health check endpoint, verify response includes version, valid control count, response time

### Implementation for User Story 5

- [x] T025 [US5] Create NistControlsHealthCheck implementing IHealthCheck — check catalog version via GetVersionAsync, validate 3 test controls (AC-3, SC-13, AU-2) via ValidateControlIdAsync, measure response time, return Healthy (all valid + <5s), Degraded (partial valid or >5s or version unknown), Unhealthy (none valid or exception), include structured data (version, validControlCount, responseTimeMs, timestamp, cacheDurationHours, offlineFallbackEnabled) in src/Platform.Engineering.Copilot.Core/Observability/NistControlsHealthCheck.cs
- [x] T026 [US5] Register NistControlsHealthCheck via AddCheck<NistControlsHealthCheck>("nist-controls", tags: new[] { "nist", "ready" }) in health check builder in src/Platform.Engineering.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs
- [x] T027 [P] [US5] Write health check unit tests — Healthy (all 3 test controls valid, <5s), Degraded (partial controls valid), Degraded (version unknown), Degraded (>5s timeout), Unhealthy (no controls valid), Unhealthy (exception thrown) in tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistControlsHealthCheckTests.cs

**Checkpoint**: Health check endpoint reports accurate NIST service status for Kubernetes liveness probes

---

## Phase 9: User Story 7 — Observability & Metrics (Priority: P3)

**Goal**: Distributed tracing spans and counter/histogram metrics for catalog operations

**Independent Test**: Trigger catalog fetch, verify Activity span created with cache.hit tag and metric counter incremented

### Implementation for User Story 7

- [x] T028 [US7] Create ComplianceMetricsService with ActivitySource ("Platform.Engineering.Copilot.Compliance") for tracing and Meter ("Platform.Engineering.Copilot.Compliance") with Counter<long> for operation counts and Histogram<double> for durations in src/Platform.Engineering.Copilot.Core/Observability/ComplianceMetricsService.cs
- [x] T029 [US7] Integrate ComplianceMetricsService into NistService — add Activity spans for fetch operations with tags (cache.hit, success, control.count, error, fallback.used), record counter and histogram metrics for catalog API calls in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T030 [P] [US7] Write observability unit tests — Activity span creation with correct tags, metric counter increment on operations, error tags on failure in tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistServiceEnhancedTests.cs

**Checkpoint**: All catalog operations produce structured traces and metrics compatible with OpenTelemetry exporters

---

## Phase 10: Polish & Cross-Cutting Concerns

**Purpose**: Validate all success criteria, verify backward compatibility, ensure production readiness

- [x] T031 Verify all existing 40+ NistServiceTests pass unchanged by running dotnet test --filter FullyQualifiedName~NistServiceTests
- [x] T032 [P] Run full solution build (dotnet build Platform.Engineering.Copilot.sln) and verify zero errors, zero new warnings
- [x] T033 Run complete test suite (dotnet test Platform.Engineering.Copilot.sln) and verify all tests pass including 771+ existing plus all new tests
- [x] T034 [P] Run quickstart.md validation steps end-to-end — build, test, config verification, startup behavior, health endpoint

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **US8 - Caching (Phase 3)**: Depends on Foundational (Phase 2)
- **US2 - Resilience (Phase 4)**: Depends on Foundational (Phase 2); can run parallel with US8 but shares NistService.cs
- **US3 - Enhancement (Phase 5)**: Depends on Foundational (Phase 2); can run after US8
- **US4 - Validation (Phase 6)**: Depends on Foundational (Phase 2); can run after US8
- **US1 - Warmup (Phase 7)**: Depends on US8 (caching) and US4 (validation) — `GetCatalogAsync` and `ValidateControlIdAsync` must be implemented first
- **US5 - Health Check (Phase 8)**: Depends on US4 (validation) and US8 (version) — `ValidateControlIdAsync` and `GetVersionAsync` must be implemented first
- **US7 - Observability (Phase 9)**: Depends on US8 (caching) — adds spans to existing fetch/query paths
- **Polish (Phase 10)**: Depends on all user stories being complete

### Priority Reordering Note

US1 (P1) is scheduled in Phase 7 despite being P1 priority because it depends on:
- US8 (P1, Phase 3): `GetCatalogAsync` for cache population
- US4 (P2, Phase 6): `ValidateControlIdAsync` for critical control validation

This is the optimal execution order considering actual implementation dependencies.

### User Story Dependencies

```
Phase 2 (Foundational) ──┬──► Phase 3 (US8 Caching) ──┬──► Phase 7 (US1 Warmup)
                         │                             │
                         ├──► Phase 4 (US2 Resilience)  ├──► Phase 8 (US5 Health Check)
                         │                             │
                         ├──► Phase 5 (US3 Enhancement) └──► Phase 9 (US7 Observability)
                         │
                         └──► Phase 6 (US4 Validation) ──► Phase 7 (US1 Warmup)
                                                       └──► Phase 8 (US5 Health Check)
```

### Within Each User Story

- Implementation tasks execute sequentially (same files)
- Test tasks marked [P] can run parallel with next story's implementation
- Tests are written after implementation stubs compile (interface-extension pattern requires compilable method signatures before tests can reference them); this satisfies Constitution Principle III's mandate that "all behavior changes MUST include corresponding test changes" while accommodating the constraint that new interface methods must exist before test code can compile against them

### Parallel Opportunities

**Setup (Phase 1)**: T001 and T002 can run in parallel (different files)

**Foundational (Phase 2)**: T003 and T004 can run in parallel (different files); T005 depends on T004 (same file)

**After Foundational**: US8 implementation → immediately start US8 tests [P] while beginning US2 or US3 implementation

**Independent test files**: All test tasks across different test files can run parallel:
- NistServiceEnhancedTests.cs (T012, T017, T019, T021, T030)
- NistControlsCacheWarmupServiceTests.cs (T024)
- NistControlsHealthCheckTests.cs (T027)

---

## Parallel Example: User Story 8 (Caching)

```bash
# Sequential implementation in NistService.cs:
T007: Refactor constructor (IMemoryCache, IOptions<NistControlsOptions>)
T008: Implement IMemoryCache caching with TTL
T009: Add SemaphoreSlim thundering herd prevention
T010: Implement GetCatalogAsync
T011: Implement GetVersionAsync

# Then parallel test writing:
T012: [P] Write caching unit tests in NistServiceEnhancedTests.cs
# While T012 runs, can start T013 (US2 implementation in same file — sequential)
```

---

## Implementation Strategy

### MVP First (User Story 8 Only)

1. Complete Phase 1: Setup (NuGet + config)
2. Complete Phase 2: Foundational (options, records, interface, DI)
3. Complete Phase 3: User Story 8 — Memory Caching
4. **STOP and VALIDATE**: Catalog is cached, repeated queries hit cache, thundering herd prevented
5. Existing 40+ tests still pass

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US8 (Caching) → Cached catalog queries → **MVP!**
3. US2 (Resilience) → Retry + offline fallback → Air-gapped ready
4. US3 + US4 (Enhancement + Validation) → Rich query API
5. US1 (Warmup) → Zero cold-start, proactive refresh
6. US5 (Health Check) → Production monitoring
7. US7 (Observability) → Full tracing and metrics
8. Polish → Build + test validation

### File Modification Summary

| File | Tasks | Stories |
|------|-------|---------|
| Core.csproj | T001 | Setup |
| appsettings.json / .Development.json | T002 | Setup |
| NistControlsOptions.cs | T003 | US6 (Foundational) |
| INistService.cs | T004, T005 | Foundational |
| ServiceCollectionExtensions.cs | T006, T023, T026 | Foundational, US1, US5 |
| NistService.cs | T007–T011, T013–T015, T018, T020, T029 | US8, US2, US3, US4, US7 |
| nist-800-53-fallback.json | T016 | US2 |
| NistControlsCacheWarmupService.cs | T022 | US1 |
| NistControlsHealthCheck.cs | T025 | US5 |
| ComplianceMetricsService.cs | T028 | US7 |
| NistServiceEnhancedTests.cs | T012, T017, T019, T021, T030 | US8, US2, US3, US4, US7 |
| NistControlsCacheWarmupServiceTests.cs | T024 | US1 |
| NistControlsHealthCheckTests.cs | T027 | US5 |

---

## FR Coverage Matrix

| FR | Task(s) | Story |
|----|---------|-------|
| FR-001 | T005, T018 | Foundational, US3 |
| FR-002 | T005, T020 | Foundational, US4 |
| FR-003 | T005, T011 | Foundational, US8 |
| FR-004 | T005, T010 | Foundational, US8 |
| FR-005 | T005 | Foundational |
| FR-006 | T007 | US8 |
| FR-007 | T003 | US6/Foundational |
| FR-008 | T002 | Setup |
| FR-009 | T003 | US6/Foundational |
| FR-010 | T008 | US8 |
| FR-011 | T008 | US8 |
| FR-012 | T008 | US8 |
| FR-013 | T008 | US8 |
| FR-014 | T008 | US8 |
| FR-015 | T013 | US2 |
| FR-016 | T013 | US2 |
| FR-017 | T014 | US2 |
| FR-018 | T015 | US2 |
| FR-019 | T015 | US2 |
| FR-020 | T015 | US2 |
| FR-021 | T022 | US1 |
| FR-022 | T022 | US1 |
| FR-023 | T022 | US1 |
| FR-024 | T022 | US1 |
| FR-025 | T022 | US1 |
| FR-026 | T022 | US1 |
| FR-027 | T022 | US1 |
| FR-028 | T025 | US5 |
| FR-029 | T025 | US5 |
| FR-030 | T025 | US5 |
| FR-031 | T025 | US5 |
| FR-032 | T025 | US5 |
| FR-033 | T025 | US5 |
| FR-034 | T028, T029 | US7 |
| FR-035 | T028, T029 | US7 |
| FR-036 | T029 | US7 |
| FR-037 | T004 | Foundational |
| FR-038 | T004, T018 | Foundational, US3 |
| FR-039 | T004, T018 | Foundational, US3 |
| FR-040 | T004, T018 | Foundational, US3 |
| FR-041 | T016 | US2 |
| FR-042 | T016 | US2 |
| FR-043 | T006, T007 | Foundational, US8 |
| FR-044 | T006 | Foundational |
| FR-045 | T023 | US1 |
| FR-046 | T026 | US5 |
| FR-047 | T006 | Foundational |
| FR-048 | T012, T017, T019, T021, T030 | US8, US2, US3, US4, US7 |
| FR-049 | T031 | Polish |
| FR-050 | T017 | US2 |
| FR-051 | T024 | US1 |
| FR-052 | T027 | US5 |

---

## Notes

- [P] tasks = different files, no dependencies on in-progress tasks
- [Story] label maps task to specific user story for traceability
- US6 (Configuration) is delivered through Phase 2 (Foundational) since it architecturally blocks all other stories
- Existing NistServiceTests.cs (40+ tests) must NEVER be modified — verified in T031
- Commit after each phase completion
- Stop at any checkpoint to validate independently
- The offline fallback JSON (T016) MUST contain the full NIST SP 800-53 Rev 5 catalog (323+ controls, 18 families) in OSCAL JSON format matching the upstream GitHub source, per FR-041/FR-042
