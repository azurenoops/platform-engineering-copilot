# Feature 007 — Quickstart (Removal Execution Guide)

## Prerequisites

- .NET 9.0 SDK installed
- Branch `007-remove-ato-compliance` checked out
- Solution builds cleanly before starting: `dotnet build Platform.Engineering.Copilot.sln`

## Execution Order

### Phase 1: Delete Compliance Agent Directory

```bash
rm -rf src/Platform.Engineering.Copilot.Agents/Compliance/
```

### Phase 2: Strip KnowledgeBase Agent

```bash
rm -rf src/Platform.Engineering.Copilot.Agents/KnowledgeBase/Tools/
```

Then edit:
- `KnowledgeBaseAgent.cs` — remove NIST references from description/keywords
- `knowledgebase.prompt.txt` — update to generic prompt

### Phase 3: Delete Core Compliance Files

```bash
# Configuration
rm src/Platform.Engineering.Copilot.Core/Configuration/AtoComplianceEngineOptions.cs
rm src/Platform.Engineering.Copilot.Core/Configuration/EvidenceStorageOptions.cs
rm src/Platform.Engineering.Copilot.Core/Configuration/NistControlsOptions.cs

# Entities
rm src/Platform.Engineering.Copilot.Core/Data/Entities/ComplianceAssessment.cs
rm src/Platform.Engineering.Copilot.Core/Data/Entities/ComplianceDocument.cs
rm src/Platform.Engineering.Copilot.Core/Data/Entities/ComplianceFinding.cs
rm src/Platform.Engineering.Copilot.Core/Data/Entities/EvidencePackage.cs

# Enumerations
rm src/Platform.Engineering.Copilot.Core/Data/Enumerations/ComplianceFramework.cs
rm src/Platform.Engineering.Copilot.Core/Data/Enumerations/ScanType.cs
rm src/Platform.Engineering.Copilot.Core/Data/Enumerations/AssessmentStatus.cs
rm src/Platform.Engineering.Copilot.Core/Data/Enumerations/FindingStatus.cs
rm src/Platform.Engineering.Copilot.Core/Data/Enumerations/DocumentType.cs

# Interfaces
rm src/Platform.Engineering.Copilot.Core/Interfaces/IAtoComplianceEngine.cs
rm src/Platform.Engineering.Copilot.Core/Interfaces/IComplianceScanner.cs
rm src/Platform.Engineering.Copilot.Core/Interfaces/IDefenderForCloudService.cs
rm src/Platform.Engineering.Copilot.Core/Interfaces/IEvidenceCollector.cs
rm src/Platform.Engineering.Copilot.Core/Interfaces/IEvidenceStorageService.cs
rm src/Platform.Engineering.Copilot.Core/Interfaces/KnowledgeServiceInterfaces.cs

# Models
rm -rf src/Platform.Engineering.Copilot.Core/Models/Compliance/

# Observability
rm src/Platform.Engineering.Copilot.Core/Observability/ComplianceMetricsService.cs
rm src/Platform.Engineering.Copilot.Core/Observability/NistControlsHealthCheck.cs

# Services
rm src/Platform.Engineering.Copilot.Core/Services/INistService.cs
rm src/Platform.Engineering.Copilot.Core/Services/NistService.cs
rm -rf src/Platform.Engineering.Copilot.Core/Services/NistData/

# MCP
rm src/Platform.Engineering.Copilot.Mcp/Data/nist-800-53-fallback.json
```

### Phase 4: Clean DI (ServiceCollectionExtensions.cs)

Edit `src/Platform.Engineering.Copilot.Agents/Extensions/ServiceCollectionExtensions.cs`:
- Remove all compliance agent, tool, scanner, collector, engine, service registrations
- Remove knowledge tool registrations
- **Keep** KnowledgeBaseAgent registration (with empty tools array)

### Phase 5: Clean Data Layer

1. Edit `PlatformEngineeringCopilotContext.cs` — remove compliance DbSets + entity configurations
2. Edit `Configuration.cs` — remove `BaselineLevel Baseline` property
3. Delete `BaselineLevel.cs` enum
4. Create SQL drop script at `specs/007-remove-ato-compliance/scripts/drop-compliance-tables.sql`

### Phase 6: Clean API & Config

```bash
rm src/Platform.Engineering.Copilot.Admin.API/Controllers/ComplianceController.cs
```

Edit appsettings files to remove compliance config sections. Edit MCP `Program.cs` to remove health check.

### Phase 7: Remove NuGet Packages

Edit `Agents.csproj` — remove 9 `<PackageReference>` items.

### Phase 8: Clean Cross-References

Edit `orchestrator.prompt.txt`, `AuthDenialMessageService.cs`, and audit remaining usings.

### Phase 9: Delete Test Files

```bash
# Unit tests
rm tests/Platform.Engineering.Copilot.Tests.Unit/Agents/ComplianceAgentTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Unit/Agents/ComplianceAssessToolTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Unit/Agents/ComplianceControlToolTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Unit/Agents/ComplianceWorkflowToolTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Unit/Agents/KnowledgeBaseAgentTests.cs
rm -rf tests/Platform.Engineering.Copilot.Tests.Unit/Scanners/Compliance/
rm -rf tests/Platform.Engineering.Copilot.Tests.Unit/Services/Compliance/
rm -rf tests/Platform.Engineering.Copilot.Tests.Unit/Tools/Compliance/
rm -rf tests/Platform.Engineering.Copilot.Tests.Unit/Tools/KnowledgeBase/
rm tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistServiceTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistServiceEnhancedTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistControlsCacheWarmupServiceTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Unit/Services/NistControlsHealthCheckTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Unit/AdminClient/Services/ComplianceApiServiceTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Unit/ComplianceMockHelper.cs

# Integration tests
rm tests/Platform.Engineering.Copilot.Tests.Integration/AdminApi/ComplianceApiTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Integration/Agents/ComplianceMockHelper.cs
rm tests/Platform.Engineering.Copilot.Tests.Integration/Agents/ComplianceToolEngineIntegrationTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Integration/Agents/EvidenceCollectionFlowTests.cs
rm tests/Platform.Engineering.Copilot.Tests.Integration/Agents/KnowledgeBaseFlowTests.cs
```

Then edit test files with compliance references.

### Phase 10: Documentation

```bash
rm -f docs/standards/adr-002-scanner-dictionary-dispatch.md
rm -rf specs/005-nist-controls-foundation/
rm -rf specs/006-ato-compliance-engine/
```

Edit `ARCHITECTURE.md` — remove ATO section, update KnowledgeBase description.

### Phase 11: Verify

```bash
dotnet build Platform.Engineering.Copilot.sln
dotnet test Platform.Engineering.Copilot.sln
```

Both must pass with 0 errors.

## Post-Deployment

Apply the SQL drop script to any production/staging databases:

```bash
sqlcmd -S <server> -d <database> -i specs/007-remove-ato-compliance/scripts/drop-compliance-tables.sql
```
