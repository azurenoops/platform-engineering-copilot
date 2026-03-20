# Feature 007 — Remove ATO Compliance Engine & NIST Controls Foundation

## Overview

Remove all ATO compliance and NIST controls functionality from the Platform Engineering Copilot. This capability will be handled externally by a dedicated **ATO Copilot** service. A future feature spec will define MCP-based integration with that service.

The **KnowledgeBase agent** is retained as a shell — its 8 NIST/compliance tools are removed, but the agent itself stays. A future feature will repurpose it to use MCP servers (Azure) for platform knowledge.

## Clarifications

### Session 2026-03-05

- Q: Should we generate an EF migration to drop the 4 compliance tables, or just remove the code? → A: Generate an EF migration to drop the 4 compliance tables. **Updated per research**: No EF migration infrastructure exists in the project (no `Migrations/` directory, no baseline snapshot). Delivering a hand-written idempotent SQL script (`scripts/drop-compliance-tables.sql`) instead. See [research.md](research.md#r1-ef-migration-strategy-for-table-removal).

## Scope

**Targeted removal** — ATO compliance engine, NIST controls, and compliance-specific tools are removed. The system continues to operate with its 7 agents:

1. **Orchestrator** — multi-agent routing
2. **Environment** — Azure environment management
3. **Infrastructure** — IaC templates and deployment
4. **Cost Management** — Azure cost analysis
5. **Security** — security posture and identity
6. **Discovery** — resource and service discovery
7. **KnowledgeBase** — retained shell, tools to be added via MCP servers (future feature)

## Motivation

- Compliance/ATO functionality is being extracted into a standalone **ATO Copilot** service
- KnowledgeBase agent will be repurposed to use MCP servers (Azure) — future feature spec
- Reduces codebase complexity and build times
- Removes 9 Azure ARM NuGet packages from the Agents project
- Eliminates ~70 source files (including 6 JSON data files) + ~30 test files

## Removal Inventory

### 1. Agent Directories

**DELETE entirely:**

| Directory | Files | Description |
|-----------|-------|-------------|
| `Agents/Compliance/` | 45 .cs + 1 prompt | ComplianceAgent, engine, 11 scanners, 12 evidence collectors, 4 services, 16 tools |

**STRIP tools (keep agent shell):**

| Directory | Action | Description |
|-----------|--------|-------------|
| `Agents/KnowledgeBase/Tools/` | DELETE all 8 .cs files | CompareFrameworks, ControlMapping, ExplainControl, FrameworkSummary, GetAtoChecklist, GetStigGuidance, ImplementationExamples, SearchControls |
| `Agents/KnowledgeBase/KnowledgeBaseAgent.cs` | EDIT | Remove NIST references; update description to "Platform knowledge and documentation agent", keywords to `["knowledge", "documentation", "platform", "help"]` |
| `Agents/KnowledgeBase/knowledgebase.prompt.txt` | EDIT | Replace with generic platform knowledge prompt: assist with platform engineering documentation, Azure resource guidance, and general knowledge queries (no NIST/compliance references) |

### 2. Core Project Files (DELETE)

**Configuration (3 files):**
- `Configuration/AtoComplianceEngineOptions.cs`
- `Configuration/EvidenceStorageOptions.cs`
- `Configuration/NistControlsOptions.cs`

**Data Entities (4 files):**
- `Data/Entities/ComplianceAssessment.cs`
- `Data/Entities/ComplianceDocument.cs`
- `Data/Entities/ComplianceFinding.cs`
- `Data/Entities/EvidencePackage.cs`

**Data Enumerations (5 files):**
- `Data/Enumerations/ComplianceFramework.cs`
- `Data/Enumerations/ScanType.cs`
- `Data/Enumerations/AssessmentStatus.cs`
- `Data/Enumerations/FindingStatus.cs`
- `Data/Enumerations/DocumentType.cs`

**Interfaces (6 files):**
- `Interfaces/IAtoComplianceEngine.cs`
- `Interfaces/IComplianceScanner.cs`
- `Interfaces/IDefenderForCloudService.cs`
- `Interfaces/IEvidenceCollector.cs`
- `Interfaces/IEvidenceStorageService.cs`
- `Interfaces/KnowledgeServiceInterfaces.cs`

**Models (10 files — entire `Models/Compliance/` directory):**
- AssessmentProgress, AtoComplianceAssessment, AtoFinding, ComplianceCertificate,
  ComplianceTimeline, ContinuousComplianceStatus, ControlFamilyAssessment,
  EvidenceModels, RiskAssessment, RiskProfile

**Observability (2 files):**
- `Observability/ComplianceMetricsService.cs`
- `Observability/NistControlsHealthCheck.cs`

**Services (2 files + 1 directory):**
- `Services/INistService.cs`
- `Services/NistService.cs`
- `Services/NistData/` (6 JSON files: nist-800-53-rev5.json, azure-service-mappings.json, dod-il5-overlay.json, fedramp-high-overlay.json, fedramp-moderate-overlay.json, stig-mappings.json)

### 3. Admin API (DELETE)

- `Controllers/ComplianceController.cs`

### 4. MCP Project (DELETE + EDIT)

- **DELETE:** `Data/nist-800-53-fallback.json`
- **EDIT:** `Program.cs` — remove `NistControlsHealthCheck` registration

### 5. NuGet Packages to Remove (from Agents.csproj)

| Package | Reason |
|---------|--------|
| `Azure.ResourceManager.Authorization` | Compliance scanners |
| `Azure.ResourceManager.Monitor` | Compliance scanners |
| `Azure.ResourceManager.Network` | Compliance scanners |
| `Azure.ResourceManager.Compute` | Compliance scanners |
| `Azure.ResourceManager.Storage` | Compliance scanners |
| `Azure.ResourceManager.KeyVault` | Compliance scanners |
| `Azure.ResourceManager.PolicyInsights` | Compliance scanners |
| `Azure.ResourceManager.SecurityCenter` | Compliance scanners |
| `Azure.Storage.Blobs` | Evidence storage |

### 6. Files Requiring Edits (NOT deletion)

| File | Change |
|------|--------|
| `Agents/Extensions/ServiceCollectionExtensions.cs` | Remove all compliance DI; remove knowledge tool registrations; keep KnowledgeBaseAgent registration with `Array.Empty<BaseTool>()` |
| `Core/Platform.Engineering.Copilot.Core.csproj` | Remove `<EmbeddedResource Include="Services\NistData\*.json" />` item group |
| `Core/Data/PlatformEngineeringCopilotContext.cs` | Remove compliance DbSets + entity configurations |
| `Core/Data/Entities/Configuration.cs` | Remove `BaselineLevel Baseline` property |
| `Core/Auth/AuthDenialMessageService.cs` | Remove compliance references |
| `Agents/Orchestrator/orchestrator.prompt.txt` | Remove Compliance Agent routing; update Knowledge Base Agent description |
| `Agents/KnowledgeBase/KnowledgeBaseAgent.cs` | _(See Section 1 for details)_ |
| `Agents/KnowledgeBase/knowledgebase.prompt.txt` | _(See Section 1 for details)_ |
| `Admin.API/appsettings.json` | Remove `AtoComplianceEngine` and `EvidenceStorage` sections |
| `Mcp/appsettings.json` | Remove `NistControls` section |
| `Mcp/appsettings.Development.json` | Remove `NistControls` section |
| `ARCHITECTURE.md` | Remove ATO Compliance Engine section; update KnowledgeBase description |

### 7. Enumerations to Keep

These enumerations are **NOT** compliance-specific and must be preserved:
- `Severity.cs` (used by RemediationTask)
- `AuditOutcome.cs` (used by AuditLogEntry)
- `DriftCategory.cs` (used by Alert)
- `DriftSeverity.cs` (used by DriftItem)
- `AlertState.cs` (used by Alert)
- `HealthStatus.cs` (used by HealthCheckService)
- `MonitoringAction.cs`
- `BaselineLevel.cs` — DELETE after removing from Configuration entity

### 8. Test Files to Delete (~30 files)

**Unit Tests:**
- `Agents/ComplianceAgentTests.cs`
- `Agents/ComplianceAssessToolTests.cs`
- `Agents/ComplianceControlToolTests.cs`
- `Agents/ComplianceWorkflowToolTests.cs`
- `Agents/KnowledgeBaseAgentTests.cs`
- `Scanners/Compliance/` (2 files)
- `Services/Compliance/` (9 files)
- `Services/NistServiceTests.cs`
- `Services/NistServiceEnhancedTests.cs`
- `Services/NistControlsCacheWarmupServiceTests.cs`
- `Services/NistControlsHealthCheckTests.cs`
- `Tools/Compliance/` (4 files)
- `Tools/KnowledgeBase/CompareFrameworksToolTests.cs`
- `Tools/KnowledgeBase/ExplainControlToolTests.cs`
- `AdminClient/Services/ComplianceApiServiceTests.cs`
- `ComplianceMockHelper.cs` (root)

**Integration Tests:**
- `AdminApi/ComplianceApiTests.cs`
- `Agents/ComplianceMockHelper.cs`
- `Agents/ComplianceToolEngineIntegrationTests.cs`
- `Agents/EvidenceCollectionFlowTests.cs`
- `Agents/KnowledgeBaseFlowTests.cs`

**Test Files Requiring Edits:**
- `AdminApi/AdminApiWebApplicationFactory.cs` — remove compliance mocks
- `Agents/OrchestratorAgentTests.cs` — remove compliance routing tests
- `Agents/OrchestratorTests.cs` — remove `TestComplianceAgent` class and compliance routing test cases
- `Data/EntityTests.cs` — remove compliance entity tests
- `Chat/ChatHubIntegrationTests.cs` — remove compliance references
- `Chat/ChatHubAIIntegrationTests.cs` — remove compliance references

### 9. Documentation to Delete

- `docs/standards/adr-002-scanner-dictionary-dispatch.md`
- `specs/005-nist-controls-foundation/` (entire directory)
- `specs/006-ato-compliance-engine/` (entire directory)

## Post-Removal State

- **Agents:** 7 (Orchestrator, Environment, Infrastructure, Cost Management, Security, Discovery, KnowledgeBase)
- **KnowledgeBase:** Shell agent with no tools — will be repurposed via MCP servers (Azure) in a future feature
- **Tools:** ~36 (down from ~52)
- **NuGet packages:** 9 fewer in Agents.csproj
- **Source files:** ~70 fewer (including 6 JSON data files)
- **Test files:** ~30 fewer
- Solution builds cleanly, all remaining tests pass

## Acceptance Criteria

1. `dotnet build` succeeds with zero errors across all projects
2. All remaining tests pass (`dotnet test`)
3. No dangling `using` statements or references to removed types
4. No compliance-related configuration sections in appsettings files
5. Orchestrator prompt no longer references Compliance Agent; KnowledgeBase Agent description updated
6. ARCHITECTURE.md reflects current 7-agent roster
7. KnowledgeBase agent compiles as a tool-less shell ready for future MCP integration
8. SQL script exists at `specs/007-remove-ato-compliance/scripts/drop-compliance-tables.sql` to drop `ComplianceAssessments`, `ComplianceDocuments`, `ComplianceFindings`, and `EvidencePackages` tables
