# Implementation Plan: Remove ATO Compliance Engine & NIST Controls Foundation

**Branch**: `007-remove-ato-compliance` | **Date**: 2026-03-05 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/007-remove-ato-compliance/spec.md`

## Summary

Remove all ATO compliance engine and NIST controls functionality from the Platform Engineering Copilot. Delete the Compliance agent (45 files), strip the KnowledgeBase agent of its 8 NIST tools (keeping the agent shell), remove ~30 Core project compliance files, 9 NuGet packages, 4 EF entity types (with SQL drop script for production databases), compliance API endpoints, test files, and documentation. The KnowledgeBase agent is retained as a shell for future MCP server integration. Post-removal: 7 agents, ~36 tools.

## Technical Context

**Language/Version**: C# / .NET 9.0
**Primary Dependencies**: Microsoft.Extensions.AI, Azure.AI.OpenAI, Azure.ResourceManager, Entity Framework Core, SignalR, ModelContextProtocol SDK 0.4.0-preview.2
**Storage**: EF Core with SQL Server (InMemory for tests); Azure Blob Storage (evidence — being removed)
**Testing**: xUnit 2.9.2, FluentAssertions 7.0.0, Moq 4.20.72, WebApplicationFactory for integration tests
**Target Platform**: Linux server (Azure Government), macOS dev
**Project Type**: Multi-project .NET solution (8 source + 2 test projects)
**Performance Goals**: N/A (removal feature — no new functionality)
**Constraints**: Must not break existing 6 non-compliance agents or their tests; KnowledgeBase agent shell must compile without tools
**Scale/Scope**: ~70 source files deleted (including 6 JSON data files), ~30 test files deleted, 9 NuGet packages removed, 1 SQL drop script added

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Applies? | Status | Notes |
|-----------|----------|--------|-------|
| I. Documentation as Source of Truth | Yes | PASS | ARCHITECTURE.md will be updated; ADR-002 removed (no longer applies) |
| II. BaseAgent/BaseTool Architecture | Yes | PASS | KnowledgeBase agent retains BaseAgent extension; Compliance agent deleted entirely |
| III. Test-First Development (NON-NEGOTIABLE) | Yes | PASS | All compliance tests deleted; remaining tests must pass; no new behavior = no new tests needed |
| IV. Azure Government & Compliance First | Yes | PASS with note | Compliance functionality moves to external ATO Copilot; this repo no longer owns NIST assessment. Future MCP integration feature will restore compliance posture |
| V. Observability & Structured Logging | Yes | PASS | ComplianceMetricsService removed (no longer applicable); remaining agent logging unchanged |

**Quality Gates:**

| Gate | Verification |
|------|-------------|
| Build | `dotnet build Platform.Engineering.Copilot.sln` MUST pass with 0 errors |
| Unit Tests | `dotnet test` MUST pass; coverage unaffected for remaining code |
| Linting | No new warnings in modified files |
| Documentation | ARCHITECTURE.md updated; ADR-002 removed; specs/005,006 archived |

**Gate Result: PASS** — No violations. Proceed to Phase 0.

## Project Structure

### Documentation (this feature)

```text
specs/007-remove-ato-compliance/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output (entity removal mapping)
├── quickstart.md        # Phase 1 output (removal execution guide)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/
├── Platform.Engineering.Copilot.Admin.API/     # Delete ComplianceController; edit appsettings
├── Platform.Engineering.Copilot.Admin.Client/   # No changes
├── Platform.Engineering.Copilot.Agents/         # Delete Compliance/; strip KnowledgeBase/Tools/; edit DI, csproj
├── Platform.Engineering.Copilot.Channels/       # No changes
├── Platform.Engineering.Copilot.Chat/           # No changes
├── Platform.Engineering.Copilot.Core/           # Heavy deletion: config, entities, enums, interfaces, models, services
├── Platform.Engineering.Copilot.Mcp/            # Delete fallback JSON; edit Program.cs, appsettings
└── Platform.Engineering.Copilot.State/          # No changes

tests/
├── Platform.Engineering.Copilot.Tests.Integration/  # Delete 5 compliance test files; edit factory + ChatHub tests
└── Platform.Engineering.Copilot.Tests.Unit/         # Delete ~25 compliance test files; edit entity/orchestrator tests
```

**Structure Decision**: Existing multi-project structure preserved. No projects added or removed — only files within existing projects are deleted or edited.

## Complexity Tracking

No constitution violations requiring justification. This is a pure removal feature.
