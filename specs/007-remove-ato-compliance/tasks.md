# Tasks: Remove ATO Compliance Engine & NIST Controls Foundation

**Input**: Design documents from `/specs/007-remove-ato-compliance/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: No new tests — this is a pure removal feature. Existing compliance tests are deleted; remaining tests must pass.

**Organization**: Tasks are grouped by removal story to enable ordered execution. Each user story represents a distinct removal scope.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which removal story this task belongs to (US1–US6)
- All paths are relative to repository root

## User Story Summary

| Story | Priority | Scope | Tasks |
|-------|----------|-------|-------|
| US1 | P1 | Bulk file & directory deletions | T002–T012 |
| US2 | P2 | KnowledgeBase agent transformation | T013–T014 |
| US3 | P3 | Core edits — DI, DbContext, entities | T015–T019 |
| US4 | P4 | API, config, NuGet, prompt cleanup | T020–T025 |
| US5 | P5 | Test file cleanup | T026–T032 (includes T029b) |
| US6 | P6 | Documentation & database | T033–T037 |

---

## Phase 1: Setup

**Purpose**: Verify baseline build before any changes

- [x] T001 Verify baseline build and tests pass by running `dotnet build Platform.Engineering.Copilot.sln && dotnet test Platform.Engineering.Copilot.sln`

**Checkpoint**: Baseline green — proceed with removal

---

## Phase 2: Bulk File & Directory Deletions (US1, Priority: P1) 🎯 MVP

**Goal**: Delete all compliance-only source files and directories — ~70 files removed with no edits required

**Independent Test**: Build will NOT compile after this phase (dangling references expected) — verify deletions only via `find`/`ls`

### Implementation for US1

- [x] T002 [P] [US1] Delete Compliance agent directory at `src/Platform.Engineering.Copilot.Agents/Compliance/` (45 .cs + 1 prompt)
- [x] T003 [P] [US1] Delete KnowledgeBase tools directory at `src/Platform.Engineering.Copilot.Agents/KnowledgeBase/Tools/` (8 .cs files)
- [x] T004 [P] [US1] Delete 3 compliance configuration files: `AtoComplianceEngineOptions.cs`, `EvidenceStorageOptions.cs`, `NistControlsOptions.cs` from `src/Platform.Engineering.Copilot.Core/Configuration/`
- [x] T005 [P] [US1] Delete 4 compliance entity files: `ComplianceAssessment.cs`, `ComplianceDocument.cs`, `ComplianceFinding.cs`, `EvidencePackage.cs` from `src/Platform.Engineering.Copilot.Core/Data/Entities/`
- [x] T006 [P] [US1] Delete 5 compliance enumeration files: `ComplianceFramework.cs`, `ScanType.cs`, `AssessmentStatus.cs`, `FindingStatus.cs`, `DocumentType.cs` from `src/Platform.Engineering.Copilot.Core/Data/Enumerations/`
- [x] T007 [P] [US1] Delete 6 compliance interface files: `IAtoComplianceEngine.cs`, `IComplianceScanner.cs`, `IDefenderForCloudService.cs`, `IEvidenceCollector.cs`, `IEvidenceStorageService.cs`, `KnowledgeServiceInterfaces.cs` from `src/Platform.Engineering.Copilot.Core/Interfaces/`
- [x] T008 [P] [US1] Delete compliance models directory at `src/Platform.Engineering.Copilot.Core/Models/Compliance/` (10 files)
- [x] T009 [P] [US1] Delete 2 compliance observability files: `ComplianceMetricsService.cs`, `NistControlsHealthCheck.cs` from `src/Platform.Engineering.Copilot.Core/Observability/`
- [x] T010 [P] [US1] Delete 2 service files (`INistService.cs`, `NistService.cs`) and `NistData/` directory (6 JSON) from `src/Platform.Engineering.Copilot.Core/Services/`
- [x] T011 [P] [US1] Delete `ComplianceController.cs` from `src/Platform.Engineering.Copilot.Admin.API/Controllers/`
- [x] T012 [P] [US1] Delete `nist-800-53-fallback.json` from `src/Platform.Engineering.Copilot.Mcp/Data/`

**Checkpoint**: All compliance-only source files deleted. ~70 files removed. Build will fail until edits in US2–US4 complete.

---

## Phase 3: KnowledgeBase Agent Transformation (US2, Priority: P2)

**Goal**: Transform KnowledgeBase from a NIST compliance tool agent into a generic platform knowledge shell — agent stays, tools removed

**Independent Test**: After US3–US4 edits complete, `KnowledgeBaseAgent` should compile with empty tools array and no NIST references

### Implementation for US2

- [x] T013 [US2] Edit `src/Platform.Engineering.Copilot.Agents/KnowledgeBase/KnowledgeBaseAgent.cs` — remove NIST/compliance references from description and keywords; update to generic platform knowledge shell
- [x] T014 [US2] Edit `src/Platform.Engineering.Copilot.Agents/KnowledgeBase/knowledgebase.prompt.txt` — replace NIST/compliance prompt content with generic platform knowledge prompt

**Checkpoint**: KnowledgeBase agent is a generic shell. Description, keywords, and system prompt contain no NIST/compliance references.

---

## Phase 4: Core Source Edits (US3, Priority: P3)

**Goal**: Edit surviving source files that reference deleted compliance types — DI container, DbContext, entity model, auth service

**Independent Test**: After this phase + US4, `dotnet build` on Core and Agents projects should begin to compile

### Implementation for US3

- [x] T015 [US3] Edit `src/Platform.Engineering.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs` — remove all compliance agent, tool, scanner, collector, engine, and service DI registrations; remove knowledge tool registrations; keep KnowledgeBaseAgent registration with empty tools array
- [x] T015b [P] [US3] Edit `src/Platform.Engineering.Copilot.Core/Platform.Engineering.Copilot.Core.csproj` — remove `<EmbeddedResource Include="Services\NistData\*.json" />` item group (files deleted in T010)
- [x] T016 [US3] Edit `src/Platform.Engineering.Copilot.Core/Data/PlatformEngineeringCopilotContext.cs` — remove 4 compliance DbSet properties and all compliance entity configurations from OnModelCreating
- [x] T017 [US3] Edit `src/Platform.Engineering.Copilot.Core/Data/Entities/Configuration.cs` — remove `BaselineLevel Baseline` property and related using statement
- [x] T018 [US3] Delete `BaselineLevel.cs` from `src/Platform.Engineering.Copilot.Core/Data/Enumerations/` (safe after T017 removes the reference)
- [x] T019 [P] [US3] Edit `src/Platform.Engineering.Copilot.Core/Auth/AuthDenialMessageService.cs` — remove compliance-related denial message strings

**Checkpoint**: Core and Agents projects should compile once remaining appsettings/csproj cleanups (US4) complete.

---

## Phase 5: API, Config & NuGet Cleanup (US4, Priority: P4)

**Goal**: Remove compliance configuration from appsettings, MCP health check registration, NuGet packages, and orchestrator prompt routing

**Independent Test**: After this phase, `dotnet build Platform.Engineering.Copilot.sln` should succeed with 0 errors

### Implementation for US4

- [x] T020 [P] [US4] Edit `src/Platform.Engineering.Copilot.Admin.API/appsettings.json` — remove `AtoComplianceEngine` and `EvidenceStorage` configuration sections
- [x] T021 [P] [US4] Edit `src/Platform.Engineering.Copilot.Mcp/appsettings.json` — remove `NistControls` configuration section
- [x] T022 [P] [US4] Edit `src/Platform.Engineering.Copilot.Mcp/appsettings.Development.json` — remove `NistControls` configuration section
- [x] T023 [US4] Edit `src/Platform.Engineering.Copilot.Mcp/Program.cs` — remove `NistControlsHealthCheck` registration
- [x] T024 [US4] Edit `src/Platform.Engineering.Copilot.Agents/Platform.Engineering.Copilot.Agents.csproj` — remove 9 NuGet PackageReference entries (Azure.ResourceManager.Authorization, .Monitor, .Network, .Compute, .Storage, .KeyVault, .PolicyInsights, .SecurityCenter, Azure.Storage.Blobs)
- [x] T025 [P] [US4] Edit `src/Platform.Engineering.Copilot.Agents/Orchestrator/orchestrator.prompt.txt` — remove Compliance Agent routing section; update KnowledgeBase Agent description to generic platform knowledge

**Checkpoint**: `dotnet build Platform.Engineering.Copilot.sln` passes with 0 errors. All source code is clean.

---

## Phase 6: Test Cleanup (US5, Priority: P5)

**Goal**: Delete compliance test files and edit remaining tests that reference compliance types — ~30 test files deleted, 5 test files edited

**Independent Test**: `dotnet test Platform.Engineering.Copilot.sln` passes with all remaining tests green

### Implementation for US5

- [x] T026 [P] [US5] Delete compliance unit test files from `tests/Platform.Engineering.Copilot.Tests.Unit/`: `Agents/ComplianceAgentTests.cs`, `Agents/ComplianceAssessToolTests.cs`, `Agents/ComplianceControlToolTests.cs`, `Agents/ComplianceWorkflowToolTests.cs`, `Agents/KnowledgeBaseAgentTests.cs`, `Scanners/Compliance/` (2 files), `Services/Compliance/` (9 files), `Services/NistServiceTests.cs`, `Services/NistServiceEnhancedTests.cs`, `Services/NistControlsCacheWarmupServiceTests.cs`, `Services/NistControlsHealthCheckTests.cs`, `Tools/Compliance/` (4 files), `Tools/KnowledgeBase/CompareFrameworksToolTests.cs`, `Tools/KnowledgeBase/ExplainControlToolTests.cs`, `AdminClient/Services/ComplianceApiServiceTests.cs`, `ComplianceMockHelper.cs`
- [x] T027 [P] [US5] Delete compliance integration test files from `tests/Platform.Engineering.Copilot.Tests.Integration/`: `AdminApi/ComplianceApiTests.cs`, `Agents/ComplianceMockHelper.cs`, `Agents/ComplianceToolEngineIntegrationTests.cs`, `Agents/EvidenceCollectionFlowTests.cs`, `Agents/KnowledgeBaseFlowTests.cs`
- [x] T028 [US5] Edit `tests/Platform.Engineering.Copilot.Tests.Integration/AdminApi/AdminApiWebApplicationFactory.cs` — remove compliance service mocks and registrations
- [x] T029 [US5] Edit `tests/Platform.Engineering.Copilot.Tests.Unit/Agents/OrchestratorAgentTests.cs` — remove compliance routing test cases
- [x] T029b [US5] Edit `tests/Platform.Engineering.Copilot.Tests.Unit/Agents/OrchestratorTests.cs` — remove `TestComplianceAgent` class and compliance routing test cases
- [x] T030 [US5] Edit `tests/Platform.Engineering.Copilot.Tests.Unit/Data/EntityTests.cs` — remove compliance entity test cases
- [x] T031 [P] [US5] Edit `tests/Platform.Engineering.Copilot.Tests.Integration/Chat/ChatHubIntegrationTests.cs` — remove compliance references
- [x] T032 [P] [US5] Edit `tests/Platform.Engineering.Copilot.Tests.Integration/Chat/ChatHubAIIntegrationTests.cs` — remove compliance references

**Checkpoint**: `dotnet test Platform.Engineering.Copilot.sln` passes. All remaining tests green. No compliance test code remains.

---

## Phase 7: Documentation, Database & Verification

**Purpose**: Update architecture docs, remove obsolete specs, verify SQL drop script, final build+test gate

- [x] T033 [P] [US6] Update `ARCHITECTURE.md` — remove ATO Compliance Engine section; update KnowledgeBase Agent description to generic platform knowledge shell; verify 7-agent roster
- [x] T034 [P] [US6] Delete `docs/standards/adr-002-scanner-dictionary-dispatch.md`
- [x] T035 [P] [US6] Delete `specs/005-nist-controls-foundation/` directory
- [x] T036 [P] [US6] Delete `specs/006-ato-compliance-engine/` directory
- [x] T037 [US6] Verify SQL drop script exists and is correct at `specs/007-remove-ato-compliance/scripts/drop-compliance-tables.sql` (drops EvidencePackages → ComplianceDocuments → ComplianceFindings → ComplianceAssessments in FK-safe order)
- [x] T038 Run `dotnet build Platform.Engineering.Copilot.sln` — must pass with 0 errors (AC1)
- [x] T039 Run `dotnet test Platform.Engineering.Copilot.sln` — all remaining tests must pass (AC2)
- [x] T040 Grep for dangling compliance references: `grep -r "Compliance\|AtoCompliance\|NistControl\|ComplianceFramework\|IEvidenceCollector\|IComplianceScanner" src/ docs/ --include="*.cs" --include="*.json" --include="*.txt" --include="*.md"` — must return 0 results (AC3, AC4)

**Checkpoint**: All acceptance criteria verified. Feature complete.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — verify baseline first
- **Bulk Deletions (Phase 2 / US1)**: Depends on Setup — can start immediately after
- **KB Transform (Phase 3 / US2)**: Can run in parallel with US1 (different files)
- **Core Edits (Phase 4 / US3)**: Depends on US1 (deleted types must be gone before editing references)
- **API/Config/NuGet (Phase 5 / US4)**: Can run in parallel with US3 (different files, except T024 should follow US1)
- **Test Cleanup (Phase 6 / US5)**: Depends on US1–US4 (source must compile before test edits)
- **Documentation (Phase 7)**: T033–T037 can run in parallel with US5; T038–T040 must be last

### User Story Dependencies

- **US1 (P1)**: Can start after Setup — No dependencies on other stories
- **US2 (P2)**: Can start after Setup — Independent of US1 (different files)
- **US3 (P3)**: Depends on US1 completion (references deleted types)
- **US4 (P4)**: Can run in parallel with US3 (different files); T024 benefits from US1
- **US5 (P5)**: Depends on US1–US4 (source must be clean before test cleanup)
- **US6 (P6)**: Documentation tasks (T033–T037) independent; verification tasks (T038–T040) depend on all prior stories

### Within Each User Story

- All deletion tasks within a story are parallelizable (different files)
- Edit tasks follow a logical dependency: entity edits before enum deletions
- T017 (Configuration.cs edit) before T018 (BaselineLevel.cs deletion)
- Build verification (T038) before test verification (T039)
- Dangling reference check (T040) after both build and test pass

### Parallel Opportunities

- **Phase 2**: All 11 tasks (T002–T012) can run in parallel — different directories/files
- **Phase 3**: T013 and T014 can run in parallel — different files
- **Phase 5**: T020, T021, T022, T025 can run in parallel — different files
- **Phase 6**: T026 and T027 can run in parallel; T031 and T032 can run in parallel
- **Phase 7**: T033, T034, T035, T036 can run in parallel — different files

---

## Parallel Example: US1 Bulk Deletions

```bash
# Launch all deletion tasks together (all different directories):
Task T002: "Delete Compliance agent directory"
Task T003: "Delete KnowledgeBase tools directory"
Task T004: "Delete 3 compliance configuration files"
Task T005: "Delete 4 compliance entity files"
Task T006: "Delete 5 compliance enumeration files"
Task T007: "Delete 6 compliance interface files"
Task T008: "Delete compliance models directory"
Task T009: "Delete 2 compliance observability files"
Task T010: "Delete 2 service files and NistData directory"
Task T011: "Delete ComplianceController.cs"
Task T012: "Delete nist-800-53-fallback.json"
```

## Parallel Example: US4 Config Cleanup

```bash
# Launch all config edits together (different files):
Task T020: "Edit Admin.API/appsettings.json"
Task T021: "Edit Mcp/appsettings.json"
Task T022: "Edit Mcp/appsettings.Development.json"
Task T025: "Edit orchestrator.prompt.txt"
```

---

## Implementation Strategy

### MVP First (US1 + US2 + US3 + US4)

1. Complete Phase 1: Setup (baseline verification)
2. Complete Phase 2: Bulk deletions (US1) — removes ~70 files
3. Complete Phase 3: KB transform (US2) — 2 edits
4. Complete Phase 4: Core edits (US3) — 5 tasks
5. Complete Phase 5: API/config/NuGet (US4) — 6 tasks
6. **STOP and VALIDATE**: `dotnet build` must pass
7. Source is clean — proceed to test cleanup

### Incremental Delivery

1. US1 (bulk deletions) → Largest risk removed
2. US2 (KB transform) → Agent layer clean
3. US3 (core edits) → Core layer clean
4. US4 (API/config/NuGet) → **BUILD GATE** — solution compiles
5. US5 (test cleanup) → **TEST GATE** — all tests pass
6. US6 (docs/verification) → **DONE** — all acceptance criteria met

### Single Developer Strategy

Execute phases sequentially (P1 → P6). Within each phase, tasks marked [P] can be batched as a single commit since they touch different files.

---

## Notes

- [P] tasks = different files, no dependencies on each other
- [USn] label maps task to removal story for traceability
- Build will NOT compile between US1 and US4 — this is expected
- First green build expected after US4 completion
- First green test run expected after US5 completion
- SQL drop script is for post-deployment; it does NOT affect build or tests
- Commit after each phase or logical group
- Stop at any checkpoint to validate progress
