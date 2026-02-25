# Implementation Plan: NIST Controls Knowledge Foundation

**Branch**: `005-nist-controls-foundation` | **Date**: 2026-02-24 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/005-nist-controls-foundation/spec.md`

## Summary

Enhance the existing `NistService` / `INistService` (369-line implementation, 142-line interface, 40+ tests) into a production-ready NIST Controls Knowledge Foundation. Add: (1) `IMemoryCache`-based caching with configurable TTL, (2) Polly retry policies with exponential backoff, (3) `NistControlsCacheWarmupService` background hosted service for proactive cache warming, (4) `NistControlsHealthCheck` implementing `IHealthCheck`, (5) `NistControlsOptions` validated configuration class, (6) new async interface methods (`GetControlEnhancementAsync`, `ValidateControlIdAsync`, `GetVersionAsync`, `GetCatalogAsync`), (7) `ComplianceMetricsService` lightweight tracing/metrics via `System.Diagnostics`, (8) full offline fallback catalog file. All 13+ existing consumers and 40+ tests continue working without modification.

## Technical Context

**Language/Version**: C# / .NET 9.0 (net9.0)
**Primary Dependencies**: Microsoft.Extensions.Caching.Memory (new), Polly.Core 8.5.0 (new), System.Diagnostics.Activity (BCL), xUnit 2.9.2, FluentAssertions 7.0.0, Moq 4.20.72
**Storage**: IMemoryCache (in-memory), embedded JSON resources, offline fallback JSON file
**Testing**: xUnit + FluentAssertions + Moq via `dotnet test`; coverlet.collector 6.0.2 for coverage
**Target Platform**: .NET 9.0 Linux/Windows server (Azure Government cloud + air-gapped)
**Project Type**: Library (Core project) + hosted service (Agents project)
**Performance Goals**: Sub-millisecond cached control lookups; catalog initialization within 15 seconds of startup
**Constraints**: Must work offline (air-gapped); singleton lifetime; zero breaking changes to 13+ consumers; 80%+ test coverage on new code
**Scale/Scope**: 323 NIST controls across 18 families; 52 functional requirements; ~8 files modified/created

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Documentation as Source of Truth | PASS | Feature follows `/specs/005-*/` structure; no guidance conflicts |
| II. BaseAgent/BaseTool Architecture | N/A | Feature does not create agents or tools; enhances an existing service consumed by tools |
| III. Test-First Development | PASS | FR-048 through FR-052 mandate unit tests with 80%+ coverage; existing 40+ tests preserved (FR-049) |
| IV. Azure Government & Compliance First | PASS | Core purpose is NIST 800-53 compliance; supports air-gapped environments (FR-018/FR-041); no Azure RBAC required |
| V. Observability & Structured Logging | PASS | FR-034 (Activity spans), FR-035 (metrics), FR-036 (structured logging with Serilog) |
| Quality Gate: Build | PASS | SC-010 mandates zero errors and zero new warnings |
| Quality Gate: Unit Tests | PASS | SC-002 (existing tests pass) + SC-003 (80%+ coverage on new code) |
| Quality Gate: Linting | PASS | No new warnings (SC-010) |

No constitution violations. No complexity tracking needed.

## Project Structure

### Documentation (this feature)

```text
specs/005-nist-controls-foundation/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (INistService interface contract)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/Platform.Engineering.Copilot.Core/
├── Platform.Engineering.Copilot.Core.csproj   # MODIFY: add Microsoft.Extensions.Caching.Memory
├── Services/
│   ├── INistService.cs                        # MODIFY: add 4 new async methods + ControlEnhancement record
│   ├── NistService.cs                         # MODIFY: add IMemoryCache, Polly retry, new method implementations
│   └── NistData/                              # EXISTING: 6 embedded JSON files (unchanged)
├── Configuration/
│   └── NistControlsOptions.cs                 # CREATE: validated options class
└── Observability/
    ├── HealthCheckService.cs                  # EXISTING: unchanged
    ├── MetricsService.cs                      # EXISTING: unchanged
    ├── NistControlsHealthCheck.cs             # CREATE: IHealthCheck implementation
    └── ComplianceMetricsService.cs            # CREATE: lightweight Activity + Metrics wrapper

src/Platform.Engineering.Copilot.Agents/
├── Platform.Engineering.Copilot.Agents.csproj # EXISTING: no package changes needed
├── Extensions/
│   └── ServiceCollectionExtensions.cs         # MODIFY: register warmup, health check, options, IMemoryCache
└── Compliance/
    └── Services/
        └── NistControlsCacheWarmupService.cs  # CREATE: BackgroundService for cache warming

src/Platform.Engineering.Copilot.Mcp/
├── Platform.Engineering.Copilot.Mcp.csproj    # MODIFY: add CopyToOutputDirectory for fallback JSON
└── appsettings.json                           # MODIFY: update NistData → NistControls section

tests/Platform.Engineering.Copilot.Tests.Unit/
└── Services/
    ├── NistServiceTests.cs                    # EXISTING: 40+ tests (unchanged, must still pass)
    ├── NistServiceEnhancedTests.cs            # CREATE: tests for new async methods + caching + retry
    ├── NistControlsHealthCheckTests.cs        # CREATE: health check tests
    └── NistControlsCacheWarmupServiceTests.cs # CREATE: warmup service tests
```

**Structure Decision**: Enhancement to existing multi-project solution. All changes are in 3 existing projects (Core, Agents, Mcp) plus Tests.Unit. No new projects created. Core handles interface, models, config, health check, and the main service. Agents handles DI registration and the background hosted service (which requires `IHostedService` from the hosting layer). Tests.Unit adds 3 new test files alongside the existing NistServiceTests.cs.
