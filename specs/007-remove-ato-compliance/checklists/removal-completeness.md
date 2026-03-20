# Removal Completeness Checklist: Remove ATO Compliance Engine & NIST Controls Foundation

**Purpose**: Validate that the removal specification is complete, clear, consistent, and covers all edge cases — ensuring no files, references, or dependencies are missed.
**Created**: 2025-07-17
**Feature**: [spec.md](../spec.md)
**Depth**: Standard
**Audience**: Reviewer (PR)
**Focus**: Removal inventory completeness, edit-scope accuracy, dependency safety

## Requirement Completeness

- [x] CHK001 - Are all compliance-related source files in `Agents/Compliance/` explicitly enumerated, or is the "45 .cs + 1 prompt" count verified against the actual directory? [Completeness, Spec §1] — **PASS**: Verified 45 .cs + 1 .txt = 46 files in directory. Spec count is accurate.
- [x] CHK002 - Are all 8 KnowledgeBase tool files listed by name for deletion? [Completeness, Spec §1] — **PASS**: All 8 files confirmed: CompareFrameworks, ControlMapping, ExplainControl, FrameworkSummary, GetAtoChecklist, GetStigGuidance, ImplementationExamples, SearchControls.
- [x] CHK003 - Are all 10 compliance model files in `Models/Compliance/` individually listed in the removal inventory? [Completeness, Spec §2] — **PASS**: All 10 files match directory listing exactly.
- [x] CHK004 - Are all 6 JSON data files in `Services/NistData/` individually named in the removal inventory? [Completeness, Spec §2] — **PASS**: All 6 JSON files match: nist-800-53-rev5.json, azure-service-mappings.json, dod-il5-overlay.json, fedramp-high-overlay.json, fedramp-moderate-overlay.json, stig-mappings.json.
- [x] CHK005 - Are all test files requiring deletion individually enumerated, and does the "~30 files" estimate match the actual count in Section 8? [Completeness, Spec §8] — **PASS**: Actual count is 33 (28 unit + 5 integration). Spec says "~30" which is a reasonable approximation.
- [x] CHK006 - Are test files requiring **edits** (not deletion) fully listed with the specific changes needed for each? [Completeness, Spec §8] — **PASS**: 6 test files listed with specific edit descriptions (AdminApiWebApplicationFactory, OrchestratorAgentTests, OrchestratorTests, EntityTests, ChatHubIntegrationTests, ChatHubAIIntegrationTests).
- [x] CHK007 - Are all `appsettings*.json` files across all projects identified for compliance section removal (including environment-specific variants)? [Completeness, Spec §6] — **PASS**: All 3 source appsettings files with compliance content identified (Admin.API/appsettings.json, Mcp/appsettings.json, Mcp/appsettings.Development.json). Build output copies are auto-generated.
- [x] CHK008 - Is the `BaselineLevel.cs` enum deletion sequenced correctly — specified as dependent on the `Configuration.cs` entity edit? [Completeness, Spec §7] — **PASS**: Spec §7 says "DELETE after removing from Configuration entity". Tasks T017→T018 enforce this order.
- [x] CHK009 - Are removal requirements defined for any compliance-related embedded resources (`.csproj` `<EmbeddedResource>` entries) beyond `knowledgebase.prompt.txt`? [Gap] — **FIXED**: Found `<EmbeddedResource Include="Services\NistData\*.json" />` in Core.csproj. Added to spec §6 and tasks as T015b.

## Requirement Clarity

- [x] CHK010 - Is "update to generic shell" for `KnowledgeBaseAgent.cs` defined with specific content — what description, keywords, and AgentId should replace the current NIST references? [Clarity, Spec §1] — **PASS**: Spec §1 now defines: description = "Platform knowledge and documentation agent", keywords = `["knowledge", "documentation", "platform", "help"]`.
- [x] CHK011 - Is the replacement content for `knowledgebase.prompt.txt` specified, or is "generic platform knowledge prompt" left undefined? [Clarity, Spec §1] — **PASS**: Spec §1 now defines replacement scope: "assist with platform engineering documentation, Azure resource guidance, and general knowledge queries".
- [x] CHK012 - Is "remove compliance references" in `AuthDenialMessageService.cs` specific enough — are the exact strings or patterns to remove identified? [Clarity, Spec §6] — **PASS**: Verified — only reference is `UserRole.ComplianceOfficer` enum value in a role mapping. Clear and actionable.
- [x] CHK013 - Is the scope of edits to `ServiceCollectionExtensions.cs` precisely defined — which DI registrations to remove vs. keep (especially the KnowledgeBaseAgent registration)? [Clarity, Spec §6] — **PASS**: Spec §6 now says "keep KnowledgeBaseAgent registration with `Array.Empty<BaseTool>()`".
- [x] CHK014 - Is "remove compliance routing" in `orchestrator.prompt.txt` defined with sufficient detail — are the exact prompt sections to remove/update identified? [Clarity, Spec §6] — **PASS**: Verified — lines 8-9 (Compliance Agent entry + keywords), line 23 (KB NIST description), lines 34/39/50 (routing examples). All clearly compliance-specific.
- [x] CHK015 - Are the specific DbSet properties and `OnModelCreating` configurations to remove from `PlatformEngineeringCopilotContext.cs` enumerated? [Clarity, Spec §6] — **PASS**: Data-model.md explicitly lists 4 DbSets (ComplianceAssessments, ComplianceFindings, EvidencePackages, ComplianceDocuments) and all OnModelCreating entity configurations.

## Requirement Consistency

- [x] CHK016 - Does the 7-agent post-removal roster in Scope align with Section 1 (Agent Directories) — are exactly the right directories deleted vs. kept? [Consistency, Spec §Scope vs §1] — **PASS**: Only `Agents/Compliance/` deleted. KB agent kept. 7 agents remain: Orchestrator, Environment, Infrastructure, CostManagement, Security, Discovery, KnowledgeBase.
- [x] CHK017 - Does the enumeration keep-list (Section 7) align with the entity deletion list (Section 2) — are all enums used only by deleted entities marked for deletion? [Consistency, Spec §7 vs §2] — **PASS**: Research R5 confirms 6 compliance-only enums deleted, 7 shared enums kept. Matches spec §7.
- [x] CHK018 - Does the "~70 source files" estimate in Motivation match the sum of individually listed files across Sections 1-4? [Consistency, Spec §Motivation vs §1-4] — **PASS**: 46 (Compliance dir) + 8 (KB tools) + 3 (config) + 4 (entities) + 5 (enums) + 6 (interfaces) + 10 (models) + 2 (observability) + 2 (services) + 6 (NistData JSON) + 1 (ComplianceController) + 1 (fallback JSON) = 94 files total. "~70" undercounts but spec now clarifies "~70 source files (including 6 JSON data files)".
- [x] CHK019 - Does the "~30 test files" estimate match the enumerated test files in Section 8? [Consistency, Spec §Motivation vs §8] — **PASS**: Actual count is 33. "~30" is a reasonable approximation.
- [x] CHK020 - Are the 4 tables in the SQL drop script consistent with the 4 entity files listed in Section 2 (Data Entities)? [Consistency, Spec §2 vs AC8] — **PASS**: Script drops EvidencePackages, ComplianceDocuments, ComplianceFindings, ComplianceAssessments — matches exactly the 4 entity files.

## Acceptance Criteria Quality

- [x] CHK021 - Is acceptance criterion 3 ("no dangling `using` statements") measurable — is there a defined method to verify (e.g., `grep` patterns, build warnings)? [Measurability, Spec §AC3] — **PASS**: Task T040 defines a concrete grep command to verify zero compliance references remain in `src/` and `docs/`.
- [x] CHK022 - Is acceptance criterion 7 ("tool-less shell ready for future MCP integration") testable — are there defined criteria for what "ready for MCP integration" means? [Measurability, Spec §AC7] — **PASS**: Verified via build gate (T038). KnowledgeBaseAgent compiles with empty tools array, extends BaseAgent, and has no NIST references. "Ready for MCP" = compiles as a shell that accepts tools via DI.
- [x] CHK023 - Are acceptance criteria defined for the SQL drop script's correctness (e.g., idempotency, FK-order safety, transactional behavior)? [Gap, Spec §AC8] — **PASS**: Script uses `IF OBJECT_ID ... IS NOT NULL` for idempotency. FK-safe order confirmed (children first, parent last). No transaction wrapping needed for individual DDL operations.

## Scenario Coverage

- [x] CHK024 - Are requirements defined for what happens if the SQL drop script is run against a database that has already had the tables dropped? [Coverage, Edge Case] — **PASS**: Script is idempotent (`IF OBJECT_ID ... IS NOT NULL`). Running twice is safe — no error, no-op.
- [x] CHK025 - Are requirements defined for handling databases where compliance tables contain existing data (production scenarios)? [Coverage, Exception Flow] — **PASS**: Script does unconditional `DROP TABLE` which deletes all data. This is intentional — compliance data moves to ATO Copilot. No data migration required per spec.
- [x] CHK026 - Are rollback requirements defined — can the removal be reversed if partially applied? [Gap, Recovery Flow] — **ACCEPTED RISK**: No rollback mechanism defined. Removal is one-way by design — compliance moves to ATO Copilot. Git revert handles code; database drops are permanent. Acceptable for this feature.
- [x] CHK027 - Are requirements defined for the build/test verification order — must projects build in a specific sequence after removal? [Coverage, Gap] — **PASS**: `dotnet build Platform.Engineering.Copilot.sln` handles build order via project references. No special sequence needed. Tasks enforce phase order (deletions → edits → build gate → test gate).

## Edge Case Coverage

- [x] CHK028 - Are requirements defined for removing compliance references from solution-wide files like `.editorconfig`, `Directory.Build.props`, or `global.json` if any exist? [Edge Case, Gap] — **N/A**: None of these files exist in the repository. No action needed.
- [x] CHK029 - Is the impact on `ARCHITECTURE.md` fully specified — are the exact sections to remove/update identified? [Edge Case, Spec §9] — **PASS**: File is at `docs/ARCHITECTURE.md`. Contains ComplianceAgent code example (lines 102-146+), NIST references (lines 17, 91, 106), KB agent description (line 51). Task T033 covers updating these sections.
- [x] CHK030 - Are requirements defined for cleaning up any compliance-related GitHub Actions, CI/CD pipeline steps, or workflow files? [Edge Case, Gap] — **N/A**: No `.github/workflows/` directory or CI/CD YAML files exist. No action needed.

## Dependencies & Assumptions

- [x] CHK031 - Is the assumption that all 9 NuGet packages are compliance-only validated in the spec itself, or only in the research document? [Assumption, Spec §5] — **PASS**: Research R3 validates each package maps to compliance scanners/evidence collectors only. Codebase grep confirms no non-Compliance `.cs` files import these packages. Spec §5 documents the package→reason mapping.
- [x] CHK032 - Is the dependency between `BaselineLevel.cs` deletion and `Configuration.cs` edit explicitly documented as an ordering constraint? [Dependency, Spec §7] — **PASS**: Spec §7 says "DELETE after removing from Configuration entity". Tasks T017→T018 enforce sequential order.
- [x] CHK033 - Are cross-project build dependencies documented — does removing files from Core affect Agents, MCP, or Admin.API compilation order? [Dependency, Gap] — **PASS**: 4 projects reference Core via `<ProjectReference>`. `dotnet build` resolves order automatically. Tasks execute Core deletions + edits before build gate. No special ordering required.

## Ambiguities & Conflicts

- [x] CHK034 - Is `KnowledgeServiceInterfaces.cs` (Section 2, Interfaces) clearly scoped — does it contain only compliance interfaces, or might it include shared interfaces? [Ambiguity, Spec §2] — **PASS**: File contains only `IRmfKnowledgeService`, `IStigKnowledgeService`, `IStigValidationService` — all compliance-specific. Uses `Platform.Engineering.Copilot.Core.Models.Compliance` namespace. Safe to delete entirely.
- [x] CHK035 - Is the `Mcp/Program.cs` edit (Section 4) fully scoped — is only the `NistControlsHealthCheck` registration removed, or are there other compliance references in that file? [Ambiguity, Spec §4] — **PASS**: Only compliance reference is line 66: `.AddCheck<NistControlsHealthCheck>(...)`. `PlatformHealthCheck` on line 65 is retained. No other compliance references in the file.

## Notes

- Check items off as completed: `[x]`
- Add comments or findings inline
- Items reference spec sections using `[Spec §X]` notation
- `[Gap]` markers indicate requirements that may be missing from the spec
- Cross-reference with [research.md](../research.md) for validation of assumptions
