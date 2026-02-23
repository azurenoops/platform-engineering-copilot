# Tasks: Build Platform Copilot Core

**Input**: Design documents from `/specs/001-platform-copilot-core/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (mcp-tools.md, signalr-hub.md, admin-api.md, configuration-tools.md, compliance-tools.md), quickstart.md

**Tests**: Included per Constitution Principle III (Test-First Development — NON-NEGOTIABLE). Write tests first, ensure they fail, then implement.

**Organization**: Tasks grouped by user story (P1–P9, P12/P13). US10 (Monitoring) and US11 (Admin Dashboard) are **deferred** to a follow-on phase per spec.md Out of Scope.

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Exact file paths based on plan.md project structure

## Path Conventions

- **Solution root**: `Platform.Engineering.Copilot.sln`
- **Source**: `src/Platform.Engineering.Copilot.<Project>/`
- **Tests**: `tests/Platform.Engineering.Copilot.Tests.<Tier>/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create .NET 9.0 solution structure, configure dependencies, Docker, and environment

- [x] T001 Create solution file Platform.Engineering.Copilot.sln and all 11 project files (.csproj) per plan.md project structure in src/ and tests/ directories
- [x] T002 [P] Configure NuGet package references for all projects — Microsoft.Agents.Protocols, Microsoft.Agents.Builder, Microsoft.Agents.Client, Semantic Kernel 1.26.0, Microsoft.Extensions.AI 9.1.0, ModelContextProtocol 0.4.0-preview, EF Core 9.0, Serilog 4.2+, SignalR 1.1, Azure SDK packages, xUnit 2.9+, FluentAssertions, Moq per plan.md Primary Dependencies
- [x] T003 [P] Create Docker compose files (docker-compose.mcp.yml, docker-compose.mcp-chat.yml, docker-compose.mcp-admin.yml, docker-compose.mcp-chat-admin.yml) with Dockerfiles and Azure SQL Edge configuration per quickstart.md
- [x] T004 [P] Create environment configuration files — appsettings.json, appsettings.Development.json (with RequireCac: false, RequirePim: false bypass per FR-015), .env.example per quickstart.md for all runnable projects (Mcp, Chat, Admin.API, Admin.Client)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core abstractions, data layer, auth, observability, and shared services that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Data Layer

- [x] T005 [P] Create all 20 enumeration types (UserRole, PimTier, CloudEnvironment, ComplianceFramework, BaselineLevel, ScanType, AssessmentStatus, Severity, FindingStatus, TaskStatus, DocumentType, TemplateMethod, GitSyncStatus, DriftCategory, AlertState, AuditOutcome, HealthStatus, MessageRole, MonitoringAction, plus MessageAttachmentType) in src/Platform.Engineering.Copilot.Core/Data/Enumerations/
- [x] T006 [P] Create User entity with UserRole[] JSON column, CacSubjectDN unique index, PimTier, and validation rules per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/User.cs
- [x] T007 [P] Create Configuration entity with 1:1 User relationship, PimRoleEligibility JSON, and all settings per data-model.md and configuration-tools.md in src/Platform.Engineering.Copilot.Core/Data/Entities/Configuration.cs
- [x] T008 [P] Create ComplianceAssessment entity with state transitions (Running→Completed|Failed|Cancelled), RetentionExpiresAt (3yr default), soft-delete, and ResourceCount per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/ComplianceAssessment.cs
- [x] T009 [P] Create ComplianceFinding entity with Severity, FindingStatus, ControlFamily, RemediationGuidance, PolicyDefinitionId, DefenderRecommendationId per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/ComplianceFinding.cs
- [x] T010 [P] Create RemediationBoard, RemediationTask (with DisplayId REM-###, SLA-based DueDate, IsOverdue computed, BlockedReason, ValidationScanId), and TaskComment entities per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/
- [x] T011 [P] Create EvidencePackage entity (append-only default, replace opt-in, JSON artifact columns, RetentionExpiresAt 3yr) per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/EvidencePackage.cs
- [x] T012 [P] Create ComplianceDocument entity (DocumentType SSP/SAR/POAM, ContentSizeBytes max 5MB, IsTruncated, RetentionExpiresAt 3yr) per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/ComplianceDocument.cs
- [x] T013 [P] Create IaCTemplate entity (TemplateMethod, 30-min TTL ExpiresAt, IsExpired computed, AnnotationCoverage ≥80% per SC-009) per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/IaCTemplate.cs
- [x] T014 [P] Create ServiceTemplate entity (Git sync fields, approval workflow, semantic version) per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/ServiceTemplate.cs
- [x] T015 [P] Create Alert entity (AlertState lifecycle, DriftCategory, SLA deadline, GroupingKey for 5-min window, EscalationCount) per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/Alert.cs
- [x] T016 [P] Create AuditLogEntry entity — immutable, append-only, repository with AddAsync only (no Update/Remove), CorrelationId, PimJustification, RetentionExpiresAt 7yr, ConcurrencyToken per data-model.md and FR-073 in src/Platform.Engineering.Copilot.Core/Data/Entities/AuditLogEntry.cs
- [x] T017 [P] Create AgentDefinition and ToolDefinition entities with HealthStatus, RequiresAuthentication, PimTierRequired per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/
- [x] T018 [P] Create Conversation, ChatMessage, ConversationContext entities for ChatDbContext per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/
- [x] T019 Create PlatformEngineeringCopilotContext (16 entities) with all indexes (16 indexes per data-model.md), relationships, JSON columns, and retention policy defaults in src/Platform.Engineering.Copilot.Core/Data/PlatformEngineeringCopilotContext.cs
- [x] T020 Create ChatDbContext (Conversation, ChatMessage, ConversationContext) with IX_Message_ConversationId_Timestamp index in src/Platform.Engineering.Copilot.Core/Data/ChatDbContext.cs
- [x] T021 Create DatabaseInitializationService with SQL Server primary / SQLite fallback (config: DatabaseProvider), EF migrations for SQL Server, EnsureCreated for SQLite per research.md §4 in src/Platform.Engineering.Copilot.Core/Data/Services/DatabaseInitializationService.cs
- [x] T022 [P] Write unit tests for all entity validation rules, state transitions (Assessment, Task, Alert), and computed properties (IsOverdue, IsExpired) in tests/Platform.Engineering.Copilot.Tests.Unit/Data/

### NistService (Shared Service)

- [x] T023 Create INistService interface and non-EF read-only models (ControlDefinition, BaselineApplicability, FrameworkApplicability, StigReference, NistDataSourceInfo record) per data-model.md in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T024 [P] Create embedded OSCAL JSON data files (nist-800-53-rev5.json, fedramp-high-overlay.json, fedramp-moderate-overlay.json, dod-il5-overlay.json, stig-mappings.json, azure-service-mappings.json) as embedded resources per data-model.md in src/Platform.Engineering.Copilot.Core/Services/NistData/
- [x] T025 Implement NistService with dual-source strategy — attempt GitHub fetch (usnistgov/oscal-content) at startup, fall back to embedded OSCAL snapshot; expose GetControl, GetControlsByFamily, SearchControls, GetControlsByBaseline, GetControlsByFramework, CompareFrameworks, GetFamilyCodes, RefreshFromGitHubAsync; log active source and catalog version per FR-080 in src/Platform.Engineering.Copilot.Core/Services/NistService.cs
- [x] T026 [P] Write unit tests for NistService — control lookup, family lookup, search, baseline filtering, framework comparison, GitHub fetch simulation, embedded fallback, dual-source logging in tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistServiceTests.cs

### Core Abstractions

- [x] T027 Create BaseAgent abstract class wrapping Microsoft.Agents.Builder.AgentApplication — AgentId, AgentName, Description, GetSystemPrompt(), RegisterTool() per Constitution Principle II and research.md §1 in src/Platform.Engineering.Copilot.Core/Agents/BaseAgent.cs
- [x] T028 Create BaseTool abstract class — Name, Description, Parameters (JSON schema), ExecuteAsync(), RequiresAuthentication, PimTierRequired, IProgress<ProgressUpdate> support per Constitution Principle II in src/Platform.Engineering.Copilot.Core/Tools/BaseTool.cs
- [x] T029 Create PlatformOrchestrator with keyword fast-path routing (O(1) dictionary lookup) + IChatClient LLM fallback + direct targeting (@agent) per FR-001, FR-005, and research.md §1 in src/Platform.Engineering.Copilot.Core/Agents/PlatformOrchestrator.cs
- [x] T030 Create ResponseEnvelope<T>, ErrorResponse, PaginationInfo classes matching platform-wide envelope schema (status, data, metadata with toolName/executionTimeMs/timestamp) per FR-079 and compliance-tools.md in src/Platform.Engineering.Copilot.Core/ResponseEnvelope.cs
- [x] T031 [P] Write unit tests for PlatformOrchestrator — keyword routing, LLM fallback, direct targeting, ambiguity handling, transparent routing explanation in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/OrchestratorTests.cs

### Authentication & Authorization

- [x] T032 Create CacAuthenticationHandler — JWT validation for amr claim (mfa + rsa/smartcard), aud/iss validation for Gov tenant, configurable 8hr timeout per FR-008–FR-012 and research.md §3 in src/Platform.Engineering.Copilot.Core/Auth/CacAuthenticationHandler.cs
- [x] T033 Create PimAuthorizationHandler — PIM tier enforcement middleware (None/Read/Write), role claim validation, eligibility check before activation prompt, justification logging per FR-069–FR-071 in src/Platform.Engineering.Copilot.Core/Auth/PimAuthorizationHandler.cs
- [x] T034 Create DevBypassHandler — configurable bypass mode (RequireCac: false, RequirePim: false) using DefaultAzureCredential, requires ASPNETCORE_ENVIRONMENT=Development per FR-015 in src/Platform.Engineering.Copilot.Core/Auth/DevBypassHandler.cs
- [x] T035 [P] Write unit tests for CacAuthenticationHandler (valid/invalid/expired CAC), PimAuthorizationHandler (tier enforcement, eligibility check, justification), DevBypassHandler (bypass flags), including validation that CAC certificate details and PIM tokens are not leaked in logs or responses per FR-016 in tests/Platform.Engineering.Copilot.Tests.Unit/Auth/

### Observability

- [x] T036 [P] Create CorrelationIdMiddleware — assign and propagate Guid correlation ID through all agent calls per FR-077 in src/Platform.Engineering.Copilot.Core/Observability/CorrelationIdMiddleware.cs
- [x] T037 [P] Create HealthCheckService — /health endpoint returning per-agent availability (Healthy/Degraded/Unavailable) within 2 seconds per FR-075 and SC-013 in src/Platform.Engineering.Copilot.Core/Observability/HealthCheckService.cs
- [x] T038 [P] Create MetricsService — structured metrics emission for agent/tool invocations (p50/p95/p99 latency, error rate, throughput, active sessions) per FR-076 in src/Platform.Engineering.Copilot.Core/Observability/MetricsService.cs
- [x] T039 Configure Serilog — console + file sinks (dev), Application Insights (prod), structured log format with correlationId, agentName, toolName, userId (redacted), timestamp per FR-078 and Constitution Principle V in src/Platform.Engineering.Copilot.Core/Observability/SerilogConfig.cs

### MCP Server

- [x] T040 Create MCP server Program.cs with dual transport mode switch — HTTP default (port 5100) + --stdio flag, shared tool registry, ModelContextProtocol 0.4.0-preview per FR-007 and research.md §2 in src/Platform.Engineering.Copilot.Mcp/Program.cs
- [x] T041 Create McpHttpBridge mapping JSON-RPC methods (tools/list, tools/call) to HTTP endpoints with auth metadata (requiresAuthentication, pimTierRequired) per mcp-tools.md in src/Platform.Engineering.Copilot.Mcp/McpHttpBridge.cs

### Test Infrastructure

- [x] T042 [P] Create test fixtures — WebApplicationFactory for API tests, in-memory SQLite DbContext factory, mock IChatClient, mock Azure SDK clients in tests/Platform.Engineering.Copilot.Tests.Integration/
- [x] T043 [P] Create test helpers — BaseAgentTestHelper (agent construction with mocked dependencies), BaseToolTestHelper (tool execution with auth context), ResponseEnvelopeAssertions (validate envelope schema) in tests/Platform.Engineering.Copilot.Tests.Unit/

**Checkpoint**: Foundation ready — all user stories can now begin. Verify: `dotnet build`, `dotnet test`, entities compile, NistService loads embedded data, orchestrator routes test messages.

---

## Phase 3: User Story 1 — Compliance Officer Runs an Assessment (Priority: P1) 🎯 MVP

**Goal**: A Compliance Officer sends "run a compliance assessment" → Orchestrator routes to Compliance Agent → CAC+PIM enforced → combined assessment executes with progress streaming → results grouped by control family

**Independent Test**: Send NL message, verify routing to Compliance Agent, confirm CAC gate, validate structured assessment summary with correct grouping per SC-001.

### Tests for User Story 1

> **Write these tests FIRST, ensure they FAIL before implementation**

- [x] T044 [P] [US1] Write unit tests for ComplianceAgent — constructor registers 12 tools, extends BaseAgent, system prompt loaded from compliance.prompt.txt in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/ComplianceAgentTests.cs
- [x] T045 [P] [US1] Write unit tests for ComplianceAssessTool — parameter validation (subscriptionId, framework, scanType), combined scan orchestration, progress streaming via IProgress, response envelope conformance, >5K resource warning per SC-001 in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/ComplianceAssessToolTests.cs
- [x] T046 [P] [US1] Write unit tests for ComplianceGetControlFamilyTool — familyId required, includeControls default true, INistService dependency, response envelope in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/ComplianceGetControlFamilyToolTests.cs
- [x] T047 [P] [US1] Write unit tests for ComplianceStatusTool (DB-only read, Read PIM), ComplianceHistoryTool (no auth, paginated, default 30 days) in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/
- [x] T048 [US1] Write integration test for full assessment flow — SendMessage → routing → CAC/PIM check → assessment execution → streaming progress → results in tests/Platform.Engineering.Copilot.Tests.Integration/Agents/ComplianceAssessmentFlowTests.cs

### Implementation for User Story 1

- [x] T049 [US1] Create ComplianceAgent extending BaseAgent, register all 12 tools per compliance-tools.md, load system prompt from compliance.prompt.txt in src/Platform.Engineering.Copilot.Agents/Compliance/ComplianceAgent.cs
- [x] T050 [P] [US1] Create compliance.prompt.txt system prompt — role definition, assessment workflow, response formatting (Markdown tables, severity badges, control family grouping), FR-023 summary format in src/Platform.Engineering.Copilot.Agents/Compliance/compliance.prompt.txt
- [x] T051 [US1] Implement ComplianceAssessTool (compliance_assess) — Azure Resource Graph + Policy combined scan, INistService for control mapping, progress streaming via IProgress<ProgressUpdate>, configurable timeouts (≤60s/500, ≤5min/2K, ≤10min/5K), >5K resource warning + confirmation, response envelope per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceAssessTool.cs
- [x] T052 [P] [US1] Implement ComplianceGetControlFamilyTool (compliance_get_control_family) — INistService.GetControlsByFamily, includeControls parameter, response envelope per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceGetControlFamilyTool.cs
- [x] T053 [P] [US1] Implement ComplianceStatusTool (compliance_status) — DB-only read of latest assessment summary, Read PIM, response envelope per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceStatusTool.cs
- [x] T054 [P] [US1] Implement ComplianceHistoryTool (compliance_history) — assessment history with trend data, no auth required, paginated (default 25, max 100), default 30 days per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceHistoryTool.cs
- [x] T055 [P] [US1] Implement ComplianceMapControlsTool (compliance_map_controls) — map resources to controls via INistService, response envelope per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceMapControlsTool.cs
- [x] T056 [P] [US1] Implement ComplianceCompareFrameworksTool (compliance_compare_frameworks) — INistService.CompareFrameworks, no auth, response envelope per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceCompareFrameworksTool.cs
- [x] T057 [P] [US1] Implement ComplianceMonitoringTool (compliance_monitoring) — lightweight on-demand (status/scan/alerts/trend), Read PIM, paginated alerts, NOT full US10 infrastructure per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceMonitoringTool.cs
- [x] T058 [P] [US1] Implement ComplianceDashboardTool (compliance_dashboard) — aggregated compliance posture view, response envelope per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceDashboardTool.cs
- [x] T059 [P] [US1] Implement ComplianceExportTool (compliance_export) — export assessment data in various formats, response envelope per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceExportTool.cs
- [x] T060 [US1] Register ComplianceAgent with PlatformOrchestrator — add keyword mappings ("compliance", "nist", "fedramp", "assessment", "finding", "control") in src/Platform.Engineering.Copilot.Core/Agents/PlatformOrchestrator.cs

**Checkpoint**: MVP complete — "run a compliance assessment" end-to-end. Validate: routing, CAC/PIM enforcement, streaming progress, grouped results per SC-001.

---

## Phase 4: User Story 2 — Platform Engineer Remediates a Finding (Priority: P2)

**Goal**: Engineer types "fix AC-2.1" → CAC + PIM Write enforced → dry-run preview → high-risk warning for AC/IA/SC → explicit confirmation → remediation executes with progress → success summary

**Independent Test**: Trigger remediation, verify Write PIM enforcement, verify dry-run preview, confirm high-risk warning for AC/IA/SC families, validate no changes without explicit confirmation.

### Tests for User Story 2

- [x] T061 [P] [US2] Write unit tests for ComplianceRemediateTool — dry-run default, high-risk families (AC/IA/SC) trigger extra warning, batch remediation (group by severity, scope estimate, confirmation), PIM Write enforcement in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/ComplianceRemediateToolTests.cs
- [x] T062 [P] [US2] Write unit tests for ComplianceValidateRemediationTool — findingId required, Read PIM, validation scan, response envelope in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/ComplianceValidateRemediationToolTests.cs
- [x] T063 [P] [US2] Write unit tests for ComplianceGeneratePlanTool — prioritized plan generation, Read PIM, response envelope in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/ComplianceGeneratePlanToolTests.cs
- [x] T064 [US2] Write integration test for full remediation flow — fix command → dry-run → high-risk warning → confirm → execute → validate in tests/Platform.Engineering.Copilot.Tests.Integration/Agents/ComplianceRemediationFlowTests.cs

### Implementation for User Story 2

- [x] T065 [US2] Implement ComplianceRemediateTool (compliance_remediate) — single finding (findingId) or batch (controlFamily+severity), dryRun default true, applyRemediation false default, high-risk warning for AC/IA/SC families (FR-025), batch grouping by severity with scope estimate and sequential execution per FR-026, PIM Write, progress streaming, response envelope per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceRemediateTool.cs
- [x] T066 [P] [US2] Implement ComplianceValidateRemediationTool (compliance_validate_remediation) — validate applied remediation, Read PIM, re-scan affected resources per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceValidateRemediationTool.cs
- [x] T067 [P] [US2] Implement ComplianceGeneratePlanTool (compliance_generate_plan) — prioritized remediation plan for open findings, Read PIM per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceGeneratePlanTool.cs
- [x] T068 [US2] Implement AzureErrorHandler — plain-language explanations for Azure API failures, troubleshooting suggestions, retry options, exponential backoff for rate limiting per FR-067 in src/Platform.Engineering.Copilot.Core/Services/AzureErrorHandler.cs
- [x] T069 [US2] Implement failed remediation handling — stop immediately, describe failure, offer rollback guidance, audit log per FR-068 in ComplianceRemediateTool

**Checkpoint**: Assessment + remediation pipeline complete. Validate: dry-run → confirm → execute → validate cycle, high-risk warning for AC/IA/SC.

---

## Phase 5: User Story 3 — Orchestrator Routes Messages to Specialized Agents (Priority: P3)

**Goal**: NL message → Orchestrator routes to correct agent 90%+ of the time. Direct targeting with @agent prefix. Ambiguous messages get transparent routing explanation.

**Independent Test**: Send messages with varying intents, verify correct agent routing, test direct targeting, verify ambiguity handling and routing explanation per SC-002.

### Tests for User Story 3

- [x] T070 [P] [US3] Write unit tests for OrchestratorAgent — keyword routing accuracy for all 8 agents, LLM fallback invocation, direct targeting (@compliance, @security, etc.), ambiguity resolution, routing explanation messages, unrecognized intent clarification in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/OrchestratorAgentTests.cs
- [x] T071 [US3] Write integration test for multi-agent routing — send messages targeting each agent, verify correct routing, measure routing accuracy ≥90% per SC-002 in tests/Platform.Engineering.Copilot.Tests.Integration/Agents/OrchestratorRoutingFlowTests.cs

### Implementation for User Story 3

- [x] T072 [US3] Create OrchestratorAgent extending BaseAgent, load orchestrator.prompt.txt, register keyword dictionaries for all 8 agents in src/Platform.Engineering.Copilot.Agents/Orchestrator/OrchestratorAgent.cs _(Note: wraps the PlatformOrchestrator engine (T029) in a BaseAgent-derived class per Constitution Principle II)_
- [x] T073 [P] [US3] Create orchestrator.prompt.txt — agent descriptions, routing rules, direct targeting syntax, ambiguity handling instructions, transparent explanation format in src/Platform.Engineering.Copilot.Agents/Orchestrator/orchestrator.prompt.txt
- [x] T074 [US3] Implement full keyword routing dictionary — compliance/nist/fedramp/assessment/finding → Compliance, cost/spending/budget → CostManagement, infrastructure/deploy/template/bicep → Infrastructure, discover/inventory/resource → Discovery, environment/clone/drift → Environment, explain/stig/control/guidance → KnowledgeBase, configure/set/subscription/setting → Configuration, secure/score/defender/security → Security in PlatformOrchestrator _(Note: consolidates the canonical keyword map; individual agent registration tasks T060, T091, T104, T114, T123, T137 add their own keywords incrementally as agents are built)_
- [x] T075 [US3] Implement LLM fallback routing — when no keyword matches, classify intent against agent descriptions via IChatClient, return transparent routing explanation per FR-001 in PlatformOrchestrator
- [x] T076 [US3] Implement unrecognized intent handling — respond with available agents and capabilities when no agent clearly matches per edge case in spec.md in PlatformOrchestrator

**Checkpoint**: All agents reachable via NL. Validate: ≥90% routing accuracy (SC-002), direct targeting works, ambiguous messages explained.

---

## Phase 6: User Story 4 — Auditor Reviews Compliance Evidence (Priority: P4)

**Goal**: Auditor types "collect evidence for AC-2" → CAC + PIM Read → evidence package with config exports, policy snapshots, Defender recommendations, activity logs, resource inventories. Browse cached results without auth. Remediation attempts denied with role message.

**Independent Test**: Request evidence collection, verify package contents (5 artifact types per SC-007), confirm auth enforcement, confirm role-based denial for remediation per SC-005.

### Tests for User Story 4

- [x] T077 [P] [US4] Write unit tests for ComplianceCollectEvidenceTool — controlId required, append default (immutable records), replace opt-in, previousEvidenceCount in response, Read PIM, paginated, response envelope per compliance-tools.md in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/ComplianceCollectEvidenceToolTests.cs
- [x] T078 [P] [US4] Write unit tests for ComplianceGenerateDocumentTool — documentType required (SSP/SAR/POAM), no auth, max 5MB with truncation, response envelope per compliance-tools.md in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/ComplianceGenerateDocumentToolTests.cs
- [x] T079 [P] [US4] Write unit tests for ComplianceAuditLogTool — no auth, paginated, default 7 days, actionType filter, response envelope per compliance-tools.md in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/ComplianceAuditLogToolTests.cs
- [x] T080 [US4] Write integration test for evidence collection flow — collect → verify 5 artifact types → browse cached → deny remediation with role message in tests/Platform.Engineering.Copilot.Tests.Integration/Agents/EvidenceCollectionFlowTests.cs

### Implementation for User Story 4

- [x] T081 [US4] Implement ComplianceCollectEvidenceTool (compliance_collect_evidence) — gather config exports, policy snapshots, Defender recommendations, activity logs, resource inventories; append mode default (new immutable records), replace: true opt-in, previousEvidenceCount, Read PIM, paginated per compliance-tools.md and FR-027 in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceCollectEvidenceTool.cs
- [x] T082 [P] [US4] Implement ComplianceGenerateDocumentTool (compliance_generate_document) — SSP/SAR/POA&M in Markdown following FedRAMP templates, no auth, max 5MB with truncation flag per FR-028 and compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceGenerateDocumentTool.cs
- [x] T083 [P] [US4] Implement ComplianceAuditLogTool (compliance_audit_log) — query immutable audit trail, no auth, paginated, default 7 days, actionType filter per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceAuditLogTool.cs
- [x] T084 [US4] Implement role-based denial messages — when Auditor attempts remediation, return descriptive message with required role, required PIM tier, and user's current roles per FR-020 in auth middleware

**Checkpoint**: Evidence collection + document generation + audit log. Validate: 5 artifact types (SC-007), append/replace modes, role-based denial.

---

## Phase 7: User Story 5 — Platform Engineer Configures Subscription Context (Priority: P5)

**Goal**: Engineer types "set my subscription to abc-123" → Configuration Agent stores setting. "show my configuration" → all settings displayed. Missing subscription produces clear error when assessment attempted.

**Independent Test**: Set/get configuration values, verify other agents read stored config, verify missing subscription error per FR-043–FR-045.

### Tests for User Story 5

- [x] T085 [P] [US5] Write unit tests for ConfigurationAgent — extends BaseAgent, registers configuration_manage tool, loads configuration.prompt.txt in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/ConfigurationAgentTests.cs
- [x] T086 [P] [US5] Write unit tests for ConfigurationManageTool — all 5 sub-actions (get_configuration, set_subscription, set_framework, set_baseline, set_preference), IAgentStateManager writes with config: prefix, validation (GUID format, enum values), all 6 error codes per configuration-tools.md in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Configuration/ConfigurationManageToolTests.cs
- [x] T087 [US5] Write integration test for configuration flow — set subscription → set framework → show config → attempt assessment without sub → verify error in tests/Platform.Engineering.Copilot.Tests.Integration/Agents/ConfigurationFlowTests.cs

### Implementation for User Story 5

- [x] T088 [US5] Create ConfigurationAgent extending BaseAgent, register configuration_manage tool, load configuration.prompt.txt in src/Platform.Engineering.Copilot.Agents/Configuration/ConfigurationAgent.cs
- [x] T089 [P] [US5] Create configuration.prompt.txt — setting management, validation rules, error messages, routing patterns per configuration-tools.md Agent Routing table in src/Platform.Engineering.Copilot.Agents/Configuration/configuration.prompt.txt
- [x] T090 [US5] Implement ConfigurationManageTool (configuration_manage) — 5 sub-actions, IAgentStateManager shared state (config:settings, config:subscriptionId, config:framework, config:baseline), all preference validations, 6 error codes, no auth for local settings, Read PIM for subscription validation per configuration-tools.md in src/Platform.Engineering.Copilot.Agents/Configuration/Tools/ConfigurationManageTool.cs
- [x] T091 [US5] Register ConfigurationAgent with PlatformOrchestrator — keyword mappings ("configure", "set subscription", "set framework", "settings", "show configuration") per configuration-tools.md Agent Routing table in PlatformOrchestrator
- [x] T092 [US5] Wire IAgentStateManager config reads into ComplianceAgent tools — when parameters omitted, resolve defaults from config:subscriptionId, config:framework, config:baseline per FR-044 in ComplianceAssessTool and other tools

**Checkpoint**: Configuration management complete. Validate: set/get all settings, other agents read config, missing subscription error.

---

## Phase 8: User Story 6 — Knowledge Base Agent Answers Compliance Questions (Priority: P6)

**Goal**: User types "explain NIST AC-2" → KB Agent returns plain-language explanation with Azure service mappings and implementation guidance. No auth required. All data from INistService embedded OSCAL.

**Independent Test**: Query framework controls, verify responses include explanations + Azure mappings + guidance, confirm no auth required per SC-008.

### Tests for User Story 6

- [x] T093 [P] [US6] Write unit tests for KnowledgeBaseAgent — extends BaseAgent, registers 8 tools, loads knowledgebase.prompt.txt, INistService dependency in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/KnowledgeBaseAgentTests.cs
- [x] T094 [P] [US6] Write unit tests for ExplainControlTool — controlId required, INistService.GetControl, Azure service mappings, related controls, no auth in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/KnowledgeBase/ExplainControlToolTests.cs
- [x] T095 [P] [US6] Write unit tests for CompareFrameworksTool — INistService.CompareFrameworks, shared/unique controls, no auth in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/KnowledgeBase/CompareFrameworksToolTests.cs
- [x] T096 [US6] Write integration test for KB flow — explain control → compare frameworks → search → STIG guidance, all without auth in tests/Platform.Engineering.Copilot.Tests.Integration/Agents/KnowledgeBaseFlowTests.cs

### Implementation for User Story 6

- [x] T097 [US6] Create KnowledgeBaseAgent extending BaseAgent, register 8 tools, inject INistService, load knowledgebase.prompt.txt in src/Platform.Engineering.Copilot.Agents/KnowledgeBase/KnowledgeBaseAgent.cs
- [x] T098 [P] [US6] Create knowledgebase.prompt.txt — compliance expert role, plain-language explanations, Azure service mapping, implementation guidance format, no auth messaging in src/Platform.Engineering.Copilot.Agents/KnowledgeBase/knowledgebase.prompt.txt
- [x] T099 [US6] Implement ExplainControlTool (explain_control) — INistService.GetControl, plain-language explanation, Azure service mappings, implementation guidance, related controls, no auth per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/KnowledgeBase/Tools/ExplainControlTool.cs
- [x] T100 [P] [US6] Implement CompareFrameworksTool (compare_frameworks) — INistService.CompareFrameworks, shared/unique controls across NIST/FedRAMP/IL5, no auth per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/KnowledgeBase/Tools/CompareFrameworksTool.cs
- [x] T101 [P] [US6] Implement SearchControlsTool (search_controls) — INistService.SearchControls, full-text search across control titles/descriptions, no auth per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/KnowledgeBase/Tools/SearchControlsTool.cs
- [x] T102 [P] [US6] Implement GetStigGuidanceTool (get_stig_guidance) — STIG implementation guidance from NistService StigReference data, no auth per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/KnowledgeBase/Tools/GetStigGuidanceTool.cs
- [x] T103 [P] [US6] Implement GetAtoChecklistTool (get_ato_checklist), FrameworkSummaryTool, ControlMappingTool, ImplementationExamplesTool — remaining 4 KB tools, all no auth per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/KnowledgeBase/Tools/
- [x] T104 [US6] Register KnowledgeBaseAgent with PlatformOrchestrator — keyword mappings ("explain", "stig", "control", "guidance", "ato", "nist explain", "compare frameworks") in PlatformOrchestrator

**Checkpoint**: KB Agent fully operational offline. Validate: explain control + search + compare + STIG, all without auth (SC-008).

---

## Phase 9: User Story 7 — Infrastructure Agent Generates and Deploys Templates (Priority: P7)

**Goal**: Engineer types "Generate Bicep for an AKS cluster" → Infrastructure Agent produces compliant template with NIST annotations (≥80% coverage per SC-009). Template generation requires no auth. Deployment requires CAC + PIM Write + confirmation.

**Independent Test**: Request template generation, verify compliance annotations ≥80%, confirm no auth for generation, verify CAC+PIM+confirmation for deployment.

### Tests for User Story 7

- [x] T105 [P] [US7] Write unit tests for InfrastructureAgent — extends BaseAgent, registers 6 tools, loads infrastructure.prompt.txt in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/InfrastructureAgentTests.cs
- [x] T106 [P] [US7] Write unit tests for GenerateInfrastructureTemplateTool — 3 methods (template-generator default, ai-generated, bicep-acr), compliance annotations ≥80% (SC-009), no auth, 30-min TTL in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Infrastructure/GenerateTemplateToolTests.cs
- [x] T107 [P] [US7] Write unit tests for ProvisionInfrastructureTool — templateId required, resourceGroup required, confirm gate, PIM Write, progress streaming in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Infrastructure/ProvisionInfrastructureToolTests.cs
- [x] T108 [US7] Write integration test for template generation + deployment flow — generate → verify annotations → deploy → confirm → verify in tests/Platform.Engineering.Copilot.Tests.Integration/Agents/InfrastructureFlowTests.cs

### Implementation for User Story 7

- [x] T109 [US7] Create InfrastructureAgent extending BaseAgent, register 6 tools, load infrastructure.prompt.txt in src/Platform.Engineering.Copilot.Agents/Infrastructure/InfrastructureAgent.cs
- [x] T110 [P] [US7] Create infrastructure.prompt.txt — template generation rules, compliance annotation format (// SC-8: Transmission Confidentiality), 3 generation methods, deployment workflow steps in src/Platform.Engineering.Copilot.Agents/Infrastructure/infrastructure.prompt.txt
- [x] T111 [US7] Implement GenerateInfrastructureTemplateTool (generate_infrastructure_template) — 3 methods: Template Generator (default, deterministic, known-compliant patterns), AI-generated (LLM-powered), Bicep ACR (registry modules); compliance annotations mapping properties to NIST controls (≥80% per SC-009); no auth, 30-min TTL per FR-030–FR-032 and mcp-tools.md in src/Platform.Engineering.Copilot.Agents/Infrastructure/Tools/GenerateInfrastructureTemplateTool.cs
- [x] T112 [US7] Implement ProvisionInfrastructureTool (provision_infrastructure) — templateId+resourceGroup required, resource preview, confirmation gate, PIM Write, progress updates, deployment status tracking per FR-033 and mcp-tools.md in src/Platform.Engineering.Copilot.Agents/Infrastructure/Tools/ProvisionInfrastructureTool.cs
- [x] T113 [P] [US7] Implement ValidateTemplateTool, ListDeploymentsTool, GetDeploymentStatusTool, RollbackDeploymentTool — remaining 4 infrastructure tools per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/Infrastructure/Tools/
- [x] T114 [US7] Register InfrastructureAgent with PlatformOrchestrator — keyword mappings ("infrastructure", "deploy", "template", "bicep", "terraform", "generate", "provision") in PlatformOrchestrator

**Checkpoint**: Template generation + deployment pipeline. Validate: compliance annotations ≥80% (SC-009), no auth for generation, CAC+PIM for deployment.

---

## Phase 10: User Story 8 — Cost Management Agent Analyzes Spending (Priority: P8)

**Goal**: User types "show cost analysis for last 30 days" → Cost Management Agent returns formatted breakdown. "how can I save money?" → optimization suggestions. Cached reports accessible without auth.

**Independent Test**: Request cost analysis, verify formatted breakdown with totals/trends/anomalies, verify cached reports accessible without CAC.

### Tests for User Story 8

- [x] T115 [P] [US8] Write unit tests for CostManagementAgent — extends BaseAgent, registers 6 tools, loads costmanagement.prompt.txt in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/CostManagementAgentTests.cs
- [x] T116 [P] [US8] Write unit tests for GetCostAnalysisTool — timeframe options (7d/30d/90d/custom), groupBy options, Read PIM, response format in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/CostManagement/GetCostAnalysisToolTests.cs
- [x] T117 [US8] Write integration test for cost analysis flow — live query → view results → optimization suggestions → cached report without auth in tests/Platform.Engineering.Copilot.Tests.Integration/Agents/CostManagementFlowTests.cs

### Implementation for User Story 8

- [x] T118 [US8] Create CostManagementAgent extending BaseAgent, register 6 tools, load costmanagement.prompt.txt in src/Platform.Engineering.Copilot.Agents/CostManagement/CostManagementAgent.cs
- [x] T119 [P] [US8] Create costmanagement.prompt.txt — cost analysis role, spending breakdown format, optimization suggestions format, anomaly detection rules in src/Platform.Engineering.Copilot.Agents/CostManagement/costmanagement.prompt.txt
- [x] T120 [US8] Implement GetCostAnalysisTool (get_cost_analysis) — Azure Cost Management API, timeframe (7d/30d/90d/custom), groupBy (resourceType/resourceGroup/service/tag), Read PIM per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/CostManagement/Tools/GetCostAnalysisTool.cs
- [x] T121 [P] [US8] Implement GetOptimizationSuggestionsTool (get_optimization_suggestions) — idle resources, oversized VMs, unused disks, reserved instances, estimated savings per FR-035 and mcp-tools.md in src/Platform.Engineering.Copilot.Agents/CostManagement/Tools/GetOptimizationSuggestionsTool.cs
- [x] T122 [P] [US8] Implement GetCachedCostReportTool (get_cached_cost_report) — no auth, previously fetched data, GetCostForecastTool, GetBudgetStatusTool, GetCostAnomaliesTool — remaining tools per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/CostManagement/Tools/
- [x] T123 [US8] Register CostManagementAgent with PlatformOrchestrator — keyword mappings ("cost", "spending", "budget", "forecast", "optimize", "savings") in PlatformOrchestrator

**Checkpoint**: Cost management complete. Validate: cost analysis with grouping, optimization suggestions with estimated savings, cached reports without auth.

---

## Phase 11: User Story 9 — Compliance Officer Creates a Remediation Board (Priority: P9)

**Goal**: After assessment, officer types "create remediation board" → Kanban board with 6 columns, task cards with REM-### IDs, severity badges, SLA-based due dates. "Done" triggers validation scan (CAC+PIM). Board viewing/commenting requires no auth.

**Independent Test**: Create board from assessment results, verify task properties (ID, title, severity, SLA), test column transitions including validation triggers per SC-011.

### Tests for User Story 9

- [x] T124 [P] [US9] Write unit tests for ComplianceChatTool — message required, conversationId, NL interaction, conversation memory, no auth in tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/ComplianceChatToolTests.cs _(Note: compliance_chat is a general-purpose NL tool placed in US9 because it enables board-related conversational workflows; it is not an assessment tool)_
- [x] T125 [P] [US9] Write unit tests for RemediationBoard creation — board from assessment findings, task card generation (REM-###, severity, SLA dates), 6 columns in tests/Platform.Engineering.Copilot.Tests.Unit/Data/RemediationBoardTests.cs
- [x] T126 [P] [US9] Write unit tests for task transitions — Blocked requires comment (FR-053), Done triggers validation scan (FR-053), overdue highlighting, SLA calculation (Critical:24h, High:7d, Medium:30d, Low:90d per FR-052) in tests/Platform.Engineering.Copilot.Tests.Unit/Data/RemediationTaskTests.cs
- [x] T127 [US9] Write integration test for board creation + task workflow — create board → move tasks → add comments → complete task with validation scan in tests/Platform.Engineering.Copilot.Tests.Integration/Agents/RemediationBoardFlowTests.cs

### Implementation for User Story 9

- [x] T128 [US9] Implement ComplianceChatTool (compliance_chat) — NL compliance interaction with conversation memory, conversationId, no auth per compliance-tools.md in src/Platform.Engineering.Copilot.Agents/Compliance/Tools/ComplianceChatTool.cs
- [x] T129 [US9] Implement RemediationBoardService — create board from assessment findings, auto-generate REM-### display IDs, derive titles from controls, set SLA-based due dates (Critical:24h, High:7d, Medium:30d, Low:90d), compute IsOverdue per FR-050–FR-055 in src/Platform.Engineering.Copilot.Core/Data/Services/RemediationBoardService.cs
- [x] T130 [US9] Implement task transition logic — Blocked requires comment (FR-053), Done triggers validation ComplianceAssessment (CAC+PIM), assignee visual distinction (FR-055), comment management (unlimited, own edit/delete, ComplianceOfficer delete any per FR-054) in RemediationBoardService
- [x] T131 [US9] Implement board API endpoints or tool integration — board viewing + commenting without auth (FR-056), validation scans with auth per FR-056 in appropriate agent/tool layer

**Checkpoint**: Remediation tracking complete. Validate: board creation, SLA dates (SC-011), task transitions, validation scans, no-auth viewing.

---

## Phase 12: User Story 12 & 13 — Extension Scaffolds (Priority: P12/P13)

**Purpose**: Scaffold-only per spec.md Out of Scope. Create project structure but defer full functionality.

- [x] T132 [P] [US12] Create GitHub Copilot extension scaffold — project structure, @platform participant manifest, placeholder for inline compliance checking per FR-064 in src/Platform.Engineering.Copilot.Channels/GitHub/
- [x] T133 [P] [US13] Create M365 Copilot extension scaffold — project structure, Teams bot manifest placeholder, Adaptive Cards placeholder per FR-065 in src/Platform.Engineering.Copilot.Channels/M365/

---

## Phase 13: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

### Remaining Agents

- [x] T134 [P] Create DiscoveryAgent extending BaseAgent, register 9 tools (discover_resources, get_resource_dependencies, cross_subscription_query, +6), load discovery.prompt.txt per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/Discovery/
- [x] T135 [P] Create EnvironmentAgent extending BaseAgent, register 10 tools (clone_environment, detect_drift, +8), load environment.prompt.txt per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/Environment/
- [x] T136 [P] Create SecurityAgent extending BaseAgent, register tools (get_secure_score, get_security_recommendations, manage_security_policy), load security.prompt.txt per mcp-tools.md in src/Platform.Engineering.Copilot.Agents/Security/
- [x] T137 Register DiscoveryAgent, EnvironmentAgent, SecurityAgent with PlatformOrchestrator keyword mappings in PlatformOrchestrator

### Chat UI & SignalR

- [x] T138 Implement ChatHub per signalr-hub.md — SendMessage, StreamToken, ProgressUpdate, AuthRequired, SessionStatus, ErrorNotification server→client methods; SendMessage, ConfirmAction, CancelAction, UpdateAuth client→server methods; including integration tests for session context retention (≥10 messages per SC-006) and Markdown rendering validation (FR-048) in src/Platform.Engineering.Copilot.Chat/Hubs/ChatHub.cs
- [x] T139 Implement Chat UI Razor pages with Markdown rendering (tables, code blocks, collapsible sections, severity badges), CAC/PIM status bar (FR-013: "🔒 CAC: 6h 32m | PIM: 3h 15m"), action buttons, WCAG 2.1 AA compliance (keyboard nav, screen reader, ARIA, 4.5:1 contrast, visible focus, no color-only info per FR-081) in src/Platform.Engineering.Copilot.Chat/Pages/

### Cross-Cutting Services

- [x] T140 Implement AuditLogService for all agent actions — who/what/when/which/outcome fields, correlationId, PimJustification, append-only repository per FR-066 and FR-077 in src/Platform.Engineering.Copilot.Core/Data/Services/AuditLogService.cs
- [x] T141 Implement RetentionService — background service for 3yr assessment archival (soft-delete), 30-min IaC template cleanup, 7yr immutable audit log partitioning per FR-072–FR-074 in src/Platform.Engineering.Copilot.Core/Data/Services/RetentionService.cs
- [x] T142 Implement AzureErrorHandler global integration — apply plain-language error handling across all agents for Azure API failures per FR-067 in all agent tools
- [x] T143 Implement CAC/PIM session expiration mid-operation handling — graceful stop, partial result preservation, re-auth prompt for only expired component, resume from checkpoint per FR-014

### Admin API & Dashboard

- [x] T144 [P] Implement Admin API REST endpoints for service templates (CRUD + Git sync), environments, deployments, governance snapshots, and cost summary per admin-api.md contract in src/Platform.Engineering.Copilot.Admin.API/Controllers/
- [x] T145 [P] Create Admin Dashboard Blazor WASM scaffold pages — service template management, environment overview, deployment tracking, WCAG 2.1 AA compliance per FR-062–FR-063 and FR-081 in src/Platform.Engineering.Copilot.Admin.Client/Pages/

### Testing & Validation

- [x] T146 [P] Create manual test scenarios documenting quickstart.md verification steps (health check, MCP tool list, KB query, Chat session) in tests/Platform.Engineering.Copilot.Tests.Manual/
- [x] T147 Run full quickstart.md validation — dotnet restore, build, test, docker compose up, health check, MCP tools/list (52 tools), KB query without auth, Chat session with streaming

### ChatHub Integration Tests

- [x] T148 [P] Write integration tests for ChatHub covering: (a) session context retention ≥10 messages (FR-047, SC-006), (b) Markdown rendering with tables, code blocks, collapsible sections, severity badges (FR-048), (c) real-time streaming token delivery (FR-049), (d) CAC/PIM status updates via AuthRequired method in tests/Platform.Engineering.Copilot.Tests.Integration/Chat/ChatHubIntegrationTests.cs

### Key Vault Provider

- [x] T149 Implement ISecretProvider with Azure Key Vault backend — managed identity authentication, FIPS 140-2 Level 2 compliance, .env fallback for local development, consumed by CacAuthenticationHandler and PimAuthorizationHandler for credential storage per FR-082 in src/Platform.Engineering.Copilot.Core/Services/KeyVaultSecretProvider.cs

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — **BLOCKS all user stories**
- **User Stories (Phases 3–11)**: All depend on Foundational phase completion
  - User stories can proceed in parallel (if staffed) or sequentially in priority order
- **Extension Scaffolds (Phase 12)**: Depends on Foundational only (project structure)
- **Polish (Phase 13)**: Depends on all desired user stories being complete

### User Story Dependencies

| Story | Depends On | Notes |
|-------|-----------|-------|
| **US1 (P1)** | Foundational only | MVP — can start immediately after Phase 2 |
| **US2 (P2)** | US1 | Needs assessment results to remediate findings |
| **US3 (P3)** | Foundational only | Refines Orchestrator built in Foundational; needs agents from US1 as routing targets |
| **US4 (P4)** | US1 | Needs assessment results for evidence collection |
| **US5 (P5)** | Foundational only | Validates ConfigurationAgent built in Foundational |
| **US6 (P6)** | Foundational only | No Azure dependencies; fully independent |
| **US7 (P7)** | US5 | Needs subscription configuration for deployment |
| **US8 (P8)** | US5 | Needs subscription configuration for cost queries |
| **US9 (P9)** | US1, US2 | Needs assessment findings and remediation infrastructure |
| **US12/US13** | Foundational only | Scaffold only; minimal dependencies |

### Within Each User Story

1. Tests MUST be written and FAIL before implementation (Constitution Principle III)
2. Agent class + system prompt before tools
3. Core tools before integration/edge-case tools
4. Registration in Orchestrator after agent implementation
5. Story complete before moving to next priority

### Parallel Opportunities

- **Phase 1**: T002, T003, T004 run in parallel
- **Phase 2**: All entity tasks (T005–T018) run in parallel; auth (T032–T034) and observability (T036–T039) run in parallel; test infrastructure (T042–T043) runs in parallel; NistService (T024, T026) runs in parallel with main tasks
- **After Phase 2**: US1, US5, US6, US3 can start in parallel (different teams)
- **After US1**: US2, US4, US9 can start in parallel
- **After US5**: US7, US8 can start in parallel
- **Within each story**: All tasks marked [P] can run in parallel

---

## Parallel Example: User Story 1 (Phase 3)

```bash
# Launch all tests in parallel (ensure they FAIL):
T044: "Unit tests for ComplianceAgent in Tests.Unit/Agents/"
T045: "Unit tests for ComplianceAssessTool in Tests.Unit/Tools/"
T046: "Unit tests for ComplianceGetControlFamilyTool in Tests.Unit/Tools/"
T047: "Unit tests for ComplianceStatus/History tools in Tests.Unit/Tools/"

# Launch agent+prompt in parallel:
T049: "Create ComplianceAgent in Agents/Compliance/"
T050: "Create compliance.prompt.txt"

# Launch independent tools in parallel:
T052: "Implement compliance_get_control_family tool"
T053: "Implement compliance_status tool"
T054: "Implement compliance_history tool"
T055: "Implement compliance_map_controls tool"
T056: "Implement compliance_compare_frameworks tool"
T057: "Implement compliance_monitoring tool"
T058: "Implement compliance_dashboard tool"
T059: "Implement compliance_export tool"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL** — blocks all stories)
3. Complete Phase 3: User Story 1 (Compliance Assessment)
4. **STOP and VALIDATE**: Run quickstart.md verification — send "run a compliance assessment", verify streaming response
5. Deploy/demo if ready — this is the core value proposition

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. **US1** → Compliance assessment works → **MVP!**
3. **US2** → Add remediation → assessment + fix workflow
4. **US5** → Configuration validation → proper subscription management
5. **US6** → Knowledge Base → offline compliance queries (high value, zero auth)
6. **US3** → Full routing → all agents accessible via natural language
7. **US4** → Evidence + documents → audit readiness
8. **US7** → Infrastructure templates → proactive compliance
9. **US8** → Cost management → operational efficiency
10. **US9** → Kanban boards → remediation tracking workflow
11. **US12/US13** → Extension scaffolds
12. Polish → cross-cutting improvements, remaining agents, Chat UI, Admin endpoints, retention enforcement

### Parallel Team Strategy

With multiple developers after Phase 2 completes:

| Developer | Stories | Rationale |
|-----------|---------|-----------|
| Dev A | US1 → US2 → US9 | Compliance pipeline (assessment → remediation → tracking) |
| Dev B | US6 → US7 | Offline value (KB) then templates (both in Agents project, different subdirs) |
| Dev C | US3 → US5 → US8 | Orchestrator + Configuration + Cost (infrastructure filling) |
| Dev D | US4 → US12/US13 → Polish | Evidence + scaffolds + cross-cutting |

---

## Deferred Stories (Not in Scope)

| Story | Priority | Reason | Dependencies |
|-------|----------|--------|-------------|
| **US10 — Compliance Monitoring** | P10 | Operationally complex; depends on assessment engine, agent infra, config stability. Lightweight `compliance_monitoring` tool included in US1. | US1, US3, US5 |
| **US11 — Admin Dashboard** | P11 | Visual oversight not on critical path; Admin API endpoints scaffolded in Polish | Foundational, Admin API |

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks within the same phase
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable after foundational tasks
- Constitution Principle III: Write tests first, ensure they fail, then implement
- Constitution Principle II: ALL agents extend BaseAgent, ALL tools extend BaseTool (NON-NEGOTIABLE)
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All MCP tools must declare RequiresAuthentication and PimTierRequired per FR-010
- All tool responses must conform to platform-wide response envelope (FR-079) per compliance-tools.md
- Canonical terminology: "assessment" (not "scan"), "finding" (not "violation")
- Microsoft Agents SDK (Microsoft.Agents.*) for multi-agent orchestration per research.md §1
- NistService dual-source OSCAL (GitHub fetch + embedded fallback) per FR-080
- Azure Key Vault with managed identity for production secrets per FR-082
- WCAG 2.1 Level AA for all user-facing interfaces per FR-081