# Feature 007 — Research

## Research Tasks

### R1: EF Migration Strategy for Table Removal

**Context**: The spec requires dropping 4 compliance entity tables (`ComplianceAssessments`, `ComplianceDocuments`, `ComplianceFindings`, `EvidencePackages`). The user chose Option B (generate EF migration) during clarification.

**Findings**:
- The project has **no existing EF migration infrastructure** — no `Migrations/` directory exists anywhere in the solution
- `DatabaseInitializationService` uses a dual strategy: `MigrateAsync()` for SQL Server, `EnsureCreated()` for SQLite
- Since no migrations exist, `MigrateAsync()` would fail on SQL Server — the project is in a bootstrapping state
- No `IDesignTimeDbContextFactory` implementation exists; the docs reference one that was never created
- `Microsoft.EntityFrameworkCore.Design` is referenced in the Core project, and `dotnet-ef` tool v9.0.9 is installed globally
- Tests use InMemory or SQLite with `EnsureCreated()` — migrations are not relevant for tests

**Decision**: Provide a **hand-written SQL migration script** instead of EF-scaffolded migration, because:
1. No migration baseline exists to diff against — EF needs an initial snapshot to generate a removal migration
2. Bootstrapping the entire migration system (InitialCreate + RemoveCompliance) is out of scope for a removal feature
3. A SQL script is deterministic and can be applied manually by a DBA or via deployment pipeline

**Alternatives considered**:
- **Full EF migration bootstrap** (InitialCreate + RemoveCompliance): Creates ~3000 lines of auto-generated migration code for a one-time table drop. Rejected as disproportionate scope.
- **Code-only removal** (no migration, no script): Tables would be orphaned in production DB. Rejected per user decision.

**Deliverable**: `specs/007-remove-ato-compliance/scripts/drop-compliance-tables.sql` — idempotent SQL script to drop the 4 compliance tables.

---

### R2: KnowledgeBase Agent Shell — Dependencies

**Context**: KnowledgeBase agent is being stripped of its 8 NIST tools but kept as a shell. Need to verify what dependencies the agent shell has after tool removal.

**Findings**:
- `KnowledgeBaseAgent.cs` imports:
  - `System.Reflection` — for loading embedded prompt
  - `Microsoft.Extensions.AI` — for `IChatClient`
  - `Microsoft.Extensions.Logging` — standard
  - `Microsoft.Extensions.Options` — for `IOptions<AzureOpenAIOptions>`
  - `Platform.Engineering.Copilot.Core.Agents` — `BaseAgent`, `BaseTool`
  - `Platform.Engineering.Copilot.Core.Data.Enumerations` — for `PimTier`
  - `Platform.Engineering.Copilot.Core.Services` — for `AzureOpenAIOptions`
- After removing the `using Platform.Engineering.Copilot.Core.Services` import for `INistService` (which is actually in the tools, not the agent), the agent shell compiles independently
- The agent takes `BaseTool[] tools` in constructor — passing an empty array is valid
- `knowledgebase.prompt.txt` is embedded via `.csproj` `<EmbeddedResource>` — needs to be updated but kept

**Decision**: Strip NIST/compliance references from description, keywords, and prompt text. Agent continues to extend `BaseAgent` per Constitution Principle II. Pass empty tools array from DI.

---

### R3: NuGet Package Usage Audit

**Context**: 9 Azure ARM packages are listed for removal from Agents.csproj. Need to confirm no non-compliance code depends on them.

**Findings**:
- All 9 packages are used exclusively in `Agents/Compliance/Scanners/` and `Agents/Compliance/EvidenceCollectors/`:
  - `Azure.ResourceManager.Authorization` → `RbacScanner.cs`
  - `Azure.ResourceManager.Monitor` → `LoggingScanner.cs`
  - `Azure.ResourceManager.Network` → `NetworkScanner.cs`
  - `Azure.ResourceManager.Compute` → `EncryptionScanner.cs`
  - `Azure.ResourceManager.Storage` → `EncryptionScanner.cs`, `LoggingScanner.cs`
  - `Azure.ResourceManager.KeyVault` → `EncryptionScanner.cs`
  - `Azure.ResourceManager.PolicyInsights` → `PolicyScanner.cs`
  - `Azure.ResourceManager.SecurityCenter` → `DefenderForCloudService.cs`
  - `Azure.Storage.Blobs` → `EvidenceStorageService.cs`
- The Security agent (`Agents/Security/`) uses `Azure.ResourceManager` (base package) which is NOT being removed
- Discovery agent uses `Azure.ResourceManager` (base) — not affected
- No other agents import any of the 9 packages being removed

**Decision**: Safe to remove all 9 packages. No impact on remaining agents.

---

### R4: Cross-Reference Audit — Files That Reference Compliance Types

**Context**: Need to identify files that import or reference compliance types but are NOT in the deletion list.

**Findings** (files needing edits, not deletion):
1. `ServiceCollectionExtensions.cs` — massive DI file; compliance registrations interspersed throughout
2. `PlatformEngineeringCopilotContext.cs` — DbSets for 4 compliance entities + `OnModelCreating` configurations
3. `Configuration.cs` (entity) — `BaselineLevel Baseline` property; after removing it, `BaselineLevel.cs` enum can be deleted
4. `AuthDenialMessageService.cs` — references compliance in denial message strings
5. `orchestrator.prompt.txt` — routes to `@compliance` and `@knowledge` agents
6. `AdminApiWebApplicationFactory.cs` — mocks compliance services for integration tests
7. `EntityTests.cs` — tests compliance entities
8. `OrchestratorAgentTests.cs` / `OrchestratorTests.cs` — tests compliance routing
9. `ChatHubIntegrationTests.cs` / `ChatHubAIIntegrationTests.cs` — may reference compliance in test scenarios
10. `EnvironmentModelTests.cs` / `TemplateModelTests.cs` — may have incidental compliance references

**Decision**: All identified in spec Section 6 (Files Requiring Edits). No additional files found.

---

### R5: Enumeration Safety — Which Enums to Keep vs Delete

**Context**: The Enumerations directory has 20+ enums. Need to verify compliance-only vs shared usage.

**Findings** (DELETE — compliance-only):
- `ComplianceFramework.cs` — only used by compliance entities/models
- `ScanType.cs` — only used by compliance scanners
- `AssessmentStatus.cs` — only used by `ComplianceAssessment` entity
- `FindingStatus.cs` — only used by `ComplianceFinding` entity and compliance models
- `DocumentType.cs` — only used by `ComplianceDocument` entity
- `BaselineLevel.cs` — used by `Configuration.cs` entity and `NistService.cs`; after removing both references, delete

**Findings** (KEEP — used by non-compliance code):
- `Severity.cs` — `RemediationTask.cs`, `DriftItem.cs`
- `AuditOutcome.cs` — `AuditLogEntry.cs`
- `DriftCategory.cs` — `Alert.cs`
- `DriftSeverity.cs` — `DriftItem.cs`
- `AlertState.cs` — `Alert.cs`
- `HealthStatus.cs` — `HealthCheckService.cs`
- `MonitoringAction.cs` — may be used by monitoring tools

**Decision**: Delete 6 enums (including `BaselineLevel` after editing `Configuration.cs`). Keep 7 non-compliance enums. Matches spec Section 7.
