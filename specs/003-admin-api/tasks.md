# Tasks: Admin API

**Input**: Design documents from `/specs/003-admin-api/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/api-contracts.md, quickstart.md

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (e.g., US1, US6, US12)
- Exact file paths included in all task descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Update project configuration, add NuGet packages, remove obsolete stub controllers

- [X] T001 Update csproj with required NuGet packages (Swashbuckle.AspNetCore, Serilog.AspNetCore, Serilog.Sinks.File, Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.EntityFrameworkCore.SqlServer, Microsoft.EntityFrameworkCore.InMemory) and remove Microsoft.AspNetCore.OpenApi in src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj
- [X] T002 [P] Remove obsolete stub controllers: delete src/Platform.Engineering.Copilot.Admin.API/Controllers/CostsController.cs, src/Platform.Engineering.Copilot.Admin.API/Controllers/DeploymentsController.cs, and src/Platform.Engineering.Copilot.Admin.API/Controllers/GovernanceController.cs
- [X] T003 [P] Update appsettings.json to add Cors, GitSync, DeploymentPolling, and Authentication:DevBypass configuration sections in src/Platform.Engineering.Copilot.Admin.API/appsettings.json
- [X] T004 [P] Update appsettings.Development.json to add DatabaseProvider InMemory toggle and DevBypass=true in src/Platform.Engineering.Copilot.Admin.API/appsettings.Development.json

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core entities, enumerations, DbContext, interfaces, DTOs, request models, and DI infrastructure that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Enumerations

- [X] T005 [P] Create TemplateStatus enum (Draft, PendingApproval, Published, Deprecated) in src/Platform.Engineering.Copilot.Core/Data/Enumerations/TemplateStatus.cs
- [X] T006 [P] Create TemplateFormat enum (Bicep, ARM, Terraform) in src/Platform.Engineering.Copilot.Core/Data/Enumerations/TemplateFormat.cs
- [X] T007 [P] Create EnvironmentStatus enum (Provisioning, Running, Failed, Updating, Scaling, Deleting, Deleted, Suspended) in src/Platform.Engineering.Copilot.Core/Data/Enumerations/EnvironmentStatus.cs
- [X] T008 [P] Create DriftSeverity enum (Low, Medium, High, Critical) in src/Platform.Engineering.Copilot.Core/Data/Enumerations/DriftSeverity.cs

### Entities

- [X] T009 Expand ServiceTemplate entity with ~20 new properties (DisplayName, Format, Status, Content rename, ParametersJson rename, GuardrailsJson, ComplianceFrameworks, Keywords, UseCases, AiSelectionHints, AdditionalFilesJson, ParametersOverridden, RequiresApproval, approval fields, deprecation fields, Git fields, soft-delete fields, RowVersion, CreatedBy), remove IsApproved/ContentBicep/Parameters per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/ServiceTemplate.cs
- [X] T010 [P] Create ProvisionedEnvironment entity with 26 properties, navigation properties to DeployedResource/DriftItem/EnvironmentActivity collections, and FK to ServiceTemplate per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/ProvisionedEnvironment.cs
- [X] T011 [P] Create DeployedResource entity with 11 properties and FK to ProvisionedEnvironment per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/DeployedResource.cs
- [X] T012 [P] Create DriftItem entity with 14 properties and FK to ProvisionedEnvironment per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/DriftItem.cs
- [X] T013 [P] Create EnvironmentActivity entity with 10 properties and FK to ProvisionedEnvironment per data-model.md in src/Platform.Engineering.Copilot.Core/Data/Entities/EnvironmentActivity.cs

### DbContext

- [X] T014 Update PlatformEngineeringCopilotContext: add 4 new DbSets (ProvisionedEnvironments, DeployedResources, DriftItems, EnvironmentActivities); update OnModelCreating with composite unique index (Name,Version), indexes on (Category,Status), (SubscriptionId,ResourceGroup), (Status,HasDrift), (EnvironmentId,Timestamp DESC); global query filters for ServiceTemplate and ProvisionedEnvironment (!IsDeleted); enum string conversions for Status/Format; FK cascade delete configurations per data-model.md in src/Platform.Engineering.Copilot.Core/Data/PlatformEngineeringCopilotContext.cs

### Service Interfaces

- [X] T015 [P] Create IServiceTemplateCatalogService interface with methods for CRUD, filtering, approval workflow, soft-delete, categories, and template lookup by name in src/Platform.Engineering.Copilot.Core/Interfaces/IServiceTemplateCatalogService.cs
- [X] T016 [P] Create IProvisionedEnvironmentService interface with methods for CRUD, filtering, scale, clone, reprovision, soft-delete, purge, health, summary, expiring, activities, and extend expiration in src/Platform.Engineering.Copilot.Core/Interfaces/IProvisionedEnvironmentService.cs
- [X] T017 [P] Create ITemplateDeployer interface with DeployAsync, GetStatusAsync, ScaleAsync, DeleteResourcesAsync methods and DeployerFactory for deployer creation in src/Platform.Engineering.Copilot.Core/Interfaces/ITemplateDeployer.cs
- [X] T018 [P] Create IAzureResourceService interface with SyncResourcesAsync, GetResourcesAsync, GetResourceHealthAsync, DetectDriftAsync, RemediateDriftAsync methods in src/Platform.Engineering.Copilot.Core/Interfaces/IAzureResourceService.cs
- [X] T019 [P] Create INaturalLanguageTemplateMatchingService interface with MatchTemplatesAsync, ExtractParametersAsync, ExplainMatchAsync methods in src/Platform.Engineering.Copilot.Core/Interfaces/INaturalLanguageTemplateMatchingService.cs
- [X] T020 [P] Create IGitTemplateSyncService interface with ImportFromGitAsync, SyncAsync, SyncAllAsync, GetGitStatusAsync, ResetParametersAsync methods in src/Platform.Engineering.Copilot.Core/Interfaces/IGitTemplateSyncService.cs

### DTOs and Request Models

- [X] T021 [P] Create template and environment DTOs (TemplateSummaryDto, TemplateDetailDto, TemplateParameterDto, TemplateGuardrailDto, EnvironmentDetailDto, ScaleResultDto, DriftDetectionResultDto, RemediateDriftResultDto, RefreshDeploymentStatusResultDto, TemplateMatchResultDto, TemplateExplanationDto, ExtractedParametersDto, GitStatusDto, TemplateValidationResultDto, EnvironmentSummaryDto, EnvironmentHealthDto, DeleteResourcesResultDto, ResourceDto, ActivityDto, ActivityListDto) per contracts/api-contracts.md in src/Platform.Engineering.Copilot.Admin.API/Models/Dtos.cs
- [X] T022 [P] Create compliance DTOs (ComplianceSummaryDto, EnvironmentComplianceDto, FrameworkScoreDto, ControlResultDto, ResourceComplianceDto, ViolationDto, EnvironmentComplianceStatusDto) per contracts/api-contracts.md in src/Platform.Engineering.Copilot.Admin.API/Models/ComplianceDtos.cs
- [X] T023 [P] Create request models with DataAnnotation validation (CreateTemplateRequest, UpdateTemplateRequest, ApprovalRequest, ValidateTemplateRequest, ParseBicepParametersRequest, ParseBicepFromGitRequest, TemplateMatchRequest, ExtractParametersRequest, ExplainMatchRequest, ImportFromGitRequest, CreateEnvironmentRequest, ScaleEnvironmentRequest, CloneEnvironmentRequest, ExtendExpirationRequest, RemediateDriftRequest, UpdateStatusRequest) per contracts/api-contracts.md in src/Platform.Engineering.Copilot.Admin.API/Models/Requests.cs

### Program.cs and DI

- [X] T024 Rewrite Program.cs with Serilog bootstrap (console + rolling daily file sinks), AddAdminServices() call, JwtBearer authentication with Azure Gov .us authority and DevBypass, Admin/Engineer authorization policies, CORS configuration from Cors:AllowedOrigins, Swagger UI (Swashbuckle), UseHttpsRedirection, MapControllers, MapHealthChecks, port 5050 per FR-057 through FR-064 and FR-071 through FR-075 in src/Platform.Engineering.Copilot.Admin.API/Program.cs
- [X] T025 Create AddAdminServices extension method registering EF Core context (InMemory/SqlServer toggle via DatabaseProvider config), all service interfaces→implementations, DeployerFactory, BicepParameterParser, EnvironmentActivityService, background services (GitTemplateSyncBackgroundService, DeploymentStatusPollingBackgroundService; SoftDeletePurgeBackgroundService added in T053) per FR-061/FR-062 in src/Platform.Engineering.Copilot.Admin.API/Extensions/ServiceCollectionExtensions.cs

**Checkpoint**: Foundation ready — all entities, interfaces, DTOs, DI, and Program.cs in place. User story implementation can now begin.

---

## Phase 3: User Story 12 — API Infrastructure (Priority: P1) 🎯 MVP Foundation

**Goal**: The API project starts, serves Swagger UI, responds to health checks, logs with Serilog, and returns CORS headers.

**Independent Test**: Start API → confirm /swagger loads, GET /health returns 200, structured logs appear in console and logs/ directory, CORS headers present.

- [X] T026 [US12] Rewrite HealthController to return { status: "Healthy", timestamp } at GET /health with Serilog logging per FR-063 in src/Platform.Engineering.Copilot.Admin.API/Controllers/HealthController.cs
- [X] T027 [US12] Update Dockerfile to multi-stage multi-arch build targeting linux/amd64 and linux/arm64 with .NET 9.0 SDK build and ASP.NET 9.0 runtime images, EXPOSE 5050, ASPNETCORE_URLS per FR-064 in src/Platform.Engineering.Copilot.Admin.API/Dockerfile

**Checkpoint**: API boots, /health responds, /swagger loads, Serilog outputs to console+file, CORS works.

---

## Phase 4: User Stories 1 & 2 — Template Catalog CRUD + Approval Workflow (Priority: P1)

**Goal**: Full template CRUD with list/filter/search/pagination, lookup by name, categories endpoint, soft-delete, and complete approval lifecycle (Draft→PendingApproval→Published→Deprecated).

**Independent Test**: Create template via POST → list/filter with GET → update with PUT → get by name → get categories → submit for approval → approve → deprecate → soft-delete → verify 404 on GET.

### Services

- [X] T028 Implement ServiceTemplateCatalogService with all IServiceTemplateCatalogService methods: GetAllAsync (paginated, filtered by category/status/search), GetByIdAsync, GetByNameAsync, CreateAsync (with defaults and duplicate check), UpdateAsync (partial update, set ParametersOverridden when parameters changed), DeleteAsync (soft-delete), GetCategoriesAsync, SubmitForApprovalAsync, ApproveAsync (with ApprovalRequest data), DeprecateAsync, optimistic concurrency handling (catch DbUpdateConcurrencyException) per FR-001 through FR-012 and FR-006a in src/Platform.Engineering.Copilot.Core/Services/ServiceTemplateCatalogService.cs

### Controller

- [X] T029 Rewrite TemplatesController with all template CRUD endpoints: GET /api/templates (list with query params), GET /api/templates/{id}, GET /api/templates/by-name/{name}, GET /api/templates/categories, POST /api/templates, PUT /api/templates/{id} (with If-Match ETag), DELETE /api/templates/{id}, POST /api/templates/{id}/submit-for-approval, POST /api/templates/{id}/approve, POST /api/templates/{id}/deprecate; all endpoints with try/catch structured Serilog logging, CancellationToken, typed DTOs via private MapToDto/MapToSummaryDto methods, ETag response headers, proper HTTP status codes (201/200/204/400/404/409/500), [Authorize] with Admin/Engineer role policies per contracts/api-contracts.md and FR-065 through FR-070 in src/Platform.Engineering.Copilot.Admin.API/Controllers/TemplatesController.cs

### Tests

- [X] T029a [P] Create ServiceTemplateCatalogServiceTests: test CRUD operations, pagination/filtering, approval workflow state transitions (valid and invalid), soft-delete, duplicate name+version rejection, optimistic concurrency conflict in tests/Platform.Engineering.Copilot.Tests.Unit/AdminApi/ServiceTemplateCatalogServiceTests.cs
- [X] T029b [P] Create TemplatesControllerTests: test all template endpoints with mocked service, verify HTTP status codes (201/200/204/400/404/409/500), ETag headers, Location headers, DTO shapes, CancellationToken propagation in tests/Platform.Engineering.Copilot.Tests.Unit/AdminApi/TemplatesControllerTests.cs

**Checkpoint**: Template catalog is fully operational — create, read, update, delete, filter, search, paginate, approval lifecycle, soft-delete, concurrency control all working via REST. Unit tests verify service and controller behavior.

---

## Phase 5: User Story 6 — Environment Lifecycle Management (Priority: P1)

**Goal**: Create environments from published templates, list/filter, scale, clone, reprovision, soft-delete, and purge.

**Independent Test**: Create environment from published template (201) → list environments → scale → clone → soft-delete (204) → list deleted → purge → verify removed.

### Services

- [X] T030 Implement ProvisionedEnvironmentService with core lifecycle methods: GetAllAsync (paginated, filtered by subscriptionId/templateId/status/hasDrift), GetByIdAsync, CreateAsync (validate template is Published per FR-032a, create entity, trigger deployer), ScaleAsync, CloneAsync, ReprovisionAsync, DeleteAsync (soft-delete with deletedBy), GetDeletedAsync, PurgeAsync, PurgeAllAsync, concurrency handling per FR-030 through FR-038 in src/Platform.Engineering.Copilot.Core/Services/ProvisionedEnvironmentService.cs
- [X] T031 [P] Create stub DeployerFactory returning a no-op ITemplateDeployer implementation that generates fake deployment IDs and returns success for deploy/scale/delete operations in src/Platform.Engineering.Copilot.Core/Services/DeployerFactory.cs
- [X] T032 [P] Create EnvironmentActivityService for recording environment lifecycle events (Created, Scaled, Cloned, Deleted, etc.) with environment ID, activity type, description, user info, and metadata per FR-041 in src/Platform.Engineering.Copilot.Core/Services/EnvironmentActivityService.cs

### Controller

- [X] T033 Rewrite EnvironmentsController with lifecycle endpoints: GET /api/environments (list), GET /api/environments/{id}, POST /api/environments (create from template), POST /api/environments/{id}/scale, POST /api/environments/{id}/clone, POST /api/environments/{id}/reprovision, DELETE /api/environments/{id} (soft-delete), GET /api/environments/deleted, DELETE /api/environments/{id}/purge, DELETE /api/environments/purge-all; all with try/catch Serilog logging, CancellationToken, typed DTOs, ETag headers, proper status codes, [Authorize] policies per contracts/api-contracts.md and FR-065 through FR-075 in src/Platform.Engineering.Copilot.Admin.API/Controllers/EnvironmentsController.cs

### Tests

- [X] T033a [P] Create ProvisionedEnvironmentServiceTests: test create (Published-only validation), list/filter, scale (valid/invalid states), clone, reprovision (Failed-only), soft-delete, purge, purge-all in tests/Platform.Engineering.Copilot.Tests.Unit/AdminApi/ProvisionedEnvironmentServiceTests.cs
- [X] T033b [P] Create EnvironmentsControllerTests: test all environment endpoints with mocked service, verify HTTP status codes, ETag headers, authorization requirements, DTO shapes in tests/Platform.Engineering.Copilot.Tests.Unit/AdminApi/EnvironmentsControllerTests.cs

**Checkpoint**: Templates + Environments core lifecycle is complete. MVP delivered — can create templates, approve them, provision environments, manage lifecycle. Unit tests verify all service and controller behavior.

---

## Phase 6: User Story 3 — Template Validation and Bicep Parsing (Priority: P2)

**Goal**: Validate template content (format-specific checks) and extract parameters from Bicep content.

**Independent Test**: POST /api/templates/validate with valid Bicep content → isValid=true. POST with empty name → error. POST /api/templates/parse-bicep-parameters with Bicep containing params → extracted parameters returned.

- [X] T034 [P] [US3] Create BicepParameterParser utility that parses raw Bicep content to extract parameter definitions (name, type, defaultValue, description from @description decorator, allowed values from @allowed decorator) using regex-based parsing per FR-013 through FR-015 in src/Platform.Engineering.Copilot.Core/Services/BicepParameterParser.cs
- [X] T035 [US3] Add validation and Bicep parsing endpoints to TemplatesController: POST /api/templates/validate (check name, content, format-specific syntax markers per FR-013/FR-014), POST /api/templates/parse-bicep-parameters, POST /api/templates/parse-bicep-parameters-from-git (fetch from Git then parse); return TemplateValidationResultDto and TemplateParameterDto[] per contracts/api-contracts.md in src/Platform.Engineering.Copilot.Admin.API/Controllers/TemplatesController.cs
- [X] T035a [P] [US3] Create BicepParameterParserTests: test parameter extraction from Bicep content with various param types, @description decorators, @allowed decorators, default values, edge cases (empty content, no params) in tests/Platform.Engineering.Copilot.Tests.Unit/AdminApi/BicepParameterParserTests.cs

**Checkpoint**: Template authoring enhanced with validation and auto-detection of parameters. Parser tests verify extraction logic.

---

## Phase 7: User Story 4 — Natural Language Template Matching (Priority: P2)

**Goal**: Match templates to natural language descriptions using weighted keyword scoring, with extract-parameters and explain-match capabilities.

**Independent Test**: POST /api/templates/match with "secure AKS with FedRAMP" → matching templates returned with scores ≥ 0.3. POST extract-parameters → parameter values with confidence. POST explain-match → human-readable explanation.

- [X] T036 [US4] Implement NaturalLanguageTemplateMatchingService with weighted keyword overlap scoring per research.md decision 6: tokenize input, remove stopwords, score across Name (3.0x), Keywords (2.5x), UseCases (2.0x), ComplianceFrameworks (2.0x), Category (1.5x), Description (1.0x), normalize to 0.0-1.0, filter by minScore, sort descending; implement ExtractParametersAsync (keyword-based extraction with confidence scores) and ExplainMatchAsync (factor-based explanation) per FR-016 through FR-021 in src/Platform.Engineering.Copilot.Core/Services/NaturalLanguageTemplateMatchingService.cs
- [X] T037 [US4] Add NL matching endpoints to TemplatesController: POST /api/templates/match, POST /api/templates/{id}/extract-parameters, POST /api/templates/{id}/explain-match; return 503 when service unavailable per FR-019; return TemplateMatchResultDto, ExtractedParametersDto, TemplateExplanationDto per contracts/api-contracts.md in src/Platform.Engineering.Copilot.Admin.API/Controllers/TemplatesController.cs
- [X] T037a [P] [US4] Create NaturalLanguageMatchingServiceTests: test keyword scoring, stopword removal, score normalization, minScore filtering, maxResults limit, extract-parameters, explain-match in tests/Platform.Engineering.Copilot.Tests.Unit/AdminApi/NaturalLanguageMatchingServiceTests.cs

**Checkpoint**: AI-powered template discovery available via REST, with keyword fallback always functional. NL matching tests verify scoring logic.

---

## Phase 8: User Story 5 — Git-Sourced Template Sync (Priority: P2)

**Goal**: Import templates from Git repos, sync content, bulk-sync, check git-status, reset manually-overridden parameters, background auto-sync.

**Independent Test**: POST /api/templates/import-from-git → template created with Git metadata. POST sync → content updated. GET git-status → HasChanges. POST reset-parameters → ParametersOverridden cleared. POST sync-all → all Git-sourced templates synced.

- [X] T038 [US5] Implement GitTemplateSyncService with ImportFromGitAsync (fetch content from Git repo URL/branch/path, create template with Git metadata), SyncAsync (update content from Git, preserve parameters if ParametersOverridden unless force), SyncAllAsync (bulk sync all Git-sourced templates returning synced/failed counts), GetGitStatusAsync (compare current vs latest commit SHA, return HasChanges), ResetParametersAsync (clear ParametersOverridden, force-sync parameters from Git) per FR-022 through FR-028 in src/Platform.Engineering.Copilot.Core/Services/GitTemplateSyncService.cs
- [X] T039 [US5] Add Git sync endpoints to TemplatesController: POST /api/templates/import-from-git, POST /api/templates/{id}/sync, POST /api/templates/sync-all, GET /api/templates/{id}/git-status, POST /api/templates/{id}/reset-parameters per contracts/api-contracts.md in src/Platform.Engineering.Copilot.Admin.API/Controllers/TemplatesController.cs
- [X] T040 [US5] Create GitTemplateSyncBackgroundService using BackgroundService + PeriodicTimer pattern per research.md decision 5: configurable interval from GitSync:IntervalMinutes, CreateScope per tick, poll and sync all templates with GitAutoSync=true, catch-all with Serilog warning on failure, graceful shutdown via CancellationToken per FR-029 in src/Platform.Engineering.Copilot.Core/BackgroundServices/GitTemplateSyncBackgroundService.cs
- [X] T040a [P] [US5] Create GitTemplateSyncServiceTests: test import-from-git, sync (with and without force, ParametersOverridden preservation), sync-all, git-status, reset-parameters in tests/Platform.Engineering.Copilot.Tests.Unit/AdminApi/GitTemplateSyncServiceTests.cs

**Checkpoint**: GitOps workflow operational — templates stay in sync with Git repos automatically and manually. Sync service tests verify all Git operations.

---

## Phase 9: User Story 7 — Environment Monitoring and Health (Priority: P2)

**Goal**: View deployed resources, sync from Azure, check health, view activities, dashboard summary, expiring environments, extend expiration.

**Independent Test**: GET /api/environments/{id}/resources → resource list with portal URLs. GET summary → aggregate counts. GET expiring → environments expiring within N days. GET activities → paginated with HasMore. POST extend → expiration updated.

- [X] T041 [US7] Add monitoring methods to ProvisionedEnvironmentService: GetHealthAsync, GetSummaryAsync (aggregate counts by status/template, drift count, expiring within 7 days, total estimated cost), GetExpiringAsync (filter by withinDays), ExtendExpirationAsync, GetActivitiesAsync (paginated with skip/take and HasMore via EnvironmentActivityService) per FR-039 through FR-045 in src/Platform.Engineering.Copilot.Core/Services/ProvisionedEnvironmentService.cs
- [X] T042 [P] [US7] Create stub IAzureResourceService implementation with GetResourcesAsync (return resources from DB), SyncResourcesAsync (return mock counts), and GetResourceHealthAsync (return mock health data) per FR-039/FR-040 in src/Platform.Engineering.Copilot.Core/Services/AzureResourceService.cs
- [X] T043 [US7] Add monitoring endpoints to EnvironmentsController: GET /api/environments/{id}/resources, POST /api/environments/{id}/sync-resources, GET /api/environments/{id}/health, GET /api/environments/{id}/activities, GET /api/environments/summary, GET /api/environments/expiring, POST /api/environments/{id}/extend per contracts/api-contracts.md in src/Platform.Engineering.Copilot.Admin.API/Controllers/EnvironmentsController.cs

**Checkpoint**: Full environment observability — resource visibility, health status, activity history, dashboard summary, expiration management.

---

## Phase 10: User Stories 8 & 9 — Drift Detection + Deployment Status (Priority: P2)

**Goal**: Detect drift between expected and actual resource state, remediate drift items, poll deployment status automatically, manual status refresh and override.

**Independent Test**: POST detect-drift → drift items with severity and auto-remediation eligibility. POST remediate-drift → remediated/failed/remaining counts. POST refresh-status → previous/current status with StatusChanged flag. PATCH status → manual override.

### Drift (US8)

- [X] T044 [US8] Add drift methods to IAzureResourceService stub: DetectDriftAsync (return mock drift items for environment resources), RemediateDriftAsync (mark specified or all drift items as remediated, return counts) per FR-046/FR-047 in src/Platform.Engineering.Copilot.Core/Services/AzureResourceService.cs
- [X] T045 [US8] Add drift endpoints to EnvironmentsController: POST /api/environments/{id}/detect-drift, POST /api/environments/{id}/remediate-drift (optional body with driftItemIds) per contracts/api-contracts.md in src/Platform.Engineering.Copilot.Admin.API/Controllers/EnvironmentsController.cs

### Deployment Status (US9)

- [X] T046 [US9] Add deployment status methods to ProvisionedEnvironmentService: RefreshStatusAsync (check deployer for current status, return previous/current/StatusChanged), RefreshAllProvisioningAsync (bulk refresh all Provisioning environments), UpdateStatusAsync (manual PATCH override for admin recovery) per FR-048 through FR-050 in src/Platform.Engineering.Copilot.Core/Services/ProvisionedEnvironmentService.cs
- [X] T047 [US9] Add deployment status endpoints to EnvironmentsController: POST /api/environments/{id}/refresh-status, POST /api/environments/refresh-all-provisioning, PATCH /api/environments/{id}/status per contracts/api-contracts.md in src/Platform.Engineering.Copilot.Admin.API/Controllers/EnvironmentsController.cs
- [X] T048 [US9] Create DeploymentStatusPollingBackgroundService using BackgroundService + PeriodicTimer pattern: configurable interval from DeploymentPolling:IntervalSeconds (default 30s), 10-second initial delay, CreateScope per tick, refresh all Provisioning environments, Serilog logging, resilient error handling per FR-051 and research.md decision 5 in src/Platform.Engineering.Copilot.Core/BackgroundServices/DeploymentStatusPollingBackgroundService.cs

### Tests

- [X] T048a [P] Create BackgroundServiceTests: test GitTemplateSyncBackgroundService and DeploymentStatusPollingBackgroundService start/stop with CancellationToken, verify they call expected service methods in tests/Platform.Engineering.Copilot.Tests.Integration/AdminApi/BackgroundServiceTests.cs

**Checkpoint**: Drift detection and deployment status management complete — automatic polling and manual controls operational. Background service tests verify lifecycle.

---

## Phase 11: User Stories 10 & 11 — Resource Cleanup + Compliance Stub (Priority: P3)

**Goal**: Delete Azure resources for an environment, and compliance reporting endpoints returning mock data.

**Independent Test**: POST delete-resources → list of deleted/failed resources. GET compliance/summary → mock scores. POST compliance/scan → 202. GET compliance/environments/{id} → mock framework results.

### Resource Cleanup (US10)

- [X] T049 [US10] Add DeleteResourcesAsync method to IAzureResourceService stub implementation returning mock deleted/failed resource lists per FR-052 in src/Platform.Engineering.Copilot.Core/Services/AzureResourceService.cs
- [X] T050 [US10] Add resource cleanup endpoint to EnvironmentsController: POST /api/environments/{id}/delete-resources returning DeleteResourcesResultDto per contracts/api-contracts.md in src/Platform.Engineering.Copilot.Admin.API/Controllers/EnvironmentsController.cs

### Compliance Stub (US11)

- [X] T051 [P] [US11] Create ComplianceController with all stub endpoints returning hardcoded mock data with TODO comments for future ComplianceAgent integration: GET /api/compliance/summary, POST /api/compliance/scan (return 202), GET /api/compliance/environments/{environmentId}; all with [Authorize(Policy = "Admin")], try/catch Serilog logging, CancellationToken per FR-053 through FR-056 in src/Platform.Engineering.Copilot.Admin.API/Controllers/ComplianceController.cs

### Tests

- [X] T051a [P] [US11] Create ComplianceControllerTests: test all stub endpoints return expected mock data structures and correct HTTP status codes in tests/Platform.Engineering.Copilot.Tests.Unit/AdminApi/ComplianceControllerTests.cs

**Checkpoint**: Full API surface complete — all 45+ endpoints implemented across 4 controllers. Compliance stub tests verify mock data contracts.

---

## Phase 12: User Story FR-038a — Auto-Purge Background Service

**Purpose**: Background service to automatically purge soft-deleted records older than 30 days

- [X] T052 Create SoftDeletePurgeBackgroundService using BackgroundService + PeriodicTimer pattern: run daily, query IgnoreQueryFilters().Where(t => t.IsDeleted && t.DeletedAt < DateTimeOffset.UtcNow.AddDays(-30)), permanently delete matching ServiceTemplates and ProvisionedEnvironments, Serilog logging with purged counts per FR-038a and research.md decisions 4/5 in src/Platform.Engineering.Copilot.Core/BackgroundServices/SoftDeletePurgeBackgroundService.cs
- [X] T053 Register SoftDeletePurgeBackgroundService as hosted service in AddAdminServices extension method in src/Platform.Engineering.Copilot.Admin.API/Extensions/ServiceCollectionExtensions.cs

---

## Phase 13: Integration Tests & Final Validation

**Purpose**: Integration tests (WebApplicationFactory), shared test infrastructure, build validation, and quickstart verification

_Note: Unit tests were distributed to their corresponding implementation phases (T029a/b, T033a/b, T035a, T037a, T040a, T048a, T051a). This phase covers integration tests and final validation only._

- [X] T054 [P] Create AdminApiWebApplicationFactory shared test fixture with InMemory EF Core, dev auth bypass, and common test helpers for integration tests in tests/Platform.Engineering.Copilot.Tests.Integration/AdminApi/AdminApiWebApplicationFactory.cs
- [X] T055 [P] Create TemplatesApiTests using AdminApiWebApplicationFactory: full integration tests for template CRUD lifecycle, approval workflow, filtering/pagination, concurrency conflict (409), soft-delete and 404 after delete in tests/Platform.Engineering.Copilot.Tests.Integration/AdminApi/TemplatesApiTests.cs
- [X] T056 [P] Create EnvironmentsApiTests using AdminApiWebApplicationFactory: full integration tests for environment creation from Published template, rejection of non-Published templates (400), scale, clone, delete, purge lifecycle in tests/Platform.Engineering.Copilot.Tests.Integration/AdminApi/EnvironmentsApiTests.cs
- [X] T057 [P] Create ComplianceApiTests using AdminApiWebApplicationFactory: integration tests for compliance stub endpoints returning mock data with correct HTTP status codes in tests/Platform.Engineering.Copilot.Tests.Integration/AdminApi/ComplianceApiTests.cs
- [X] T058 Run dotnet build Platform.Engineering.Copilot.sln and fix any compilation errors
- [X] T059 Run dotnet test and verify all new and existing tests pass (target: 0 failures, 80%+ coverage on new code)
- [X] T060 Run quickstart.md validation: start API, verify /health responds, verify /swagger loads, verify CORS headers, verify structured Serilog logs appear in console and logs/ directory

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US12 Infrastructure)**: Depends on Phase 2 — validates API boots correctly
- **Phases 4-11 (User Stories)**: All depend on Phase 2; Phase 5 depends on Phase 4 (needs Published templates)
- **Phase 12 (Auto-Purge)**: Depends on Phase 2
- **Phase 13 (Integration Tests & Validation)**: Depends on all implementation phases

### User Story Dependencies

| Story | Depends On | Can Parallel With |
|-------|-----------|-------------------|
| US12 (Infrastructure) | Phase 2 | — |
| US1+US2 (Template CRUD+Approval) | Phase 2 | US12 |
| US6 (Environment Lifecycle) | US1+US2 (needs Published templates) | — |
| US3 (Validation/Parsing) | Phase 2 | US1, US4, US5, US6 |
| US4 (NL Matching) | Phase 2 | US1, US3, US5 |
| US5 (Git Sync) | Phase 2 | US1, US3, US4 |
| US7 (Monitoring/Health) | US6 | US3, US4, US5 |
| US8+US9 (Drift+Status) | US6 | US7 |
| US10+US11 (Cleanup+Compliance) | US6 | US7, US8, US9 |

### Within Each User Story

1. Services before controllers
2. Core implementation before background services
3. Models/entities already in Foundational phase

### Parallel Opportunities

- **Phase 1**: T002, T003, T004 all parallel
- **Phase 2**: T005-T008 (enums) all parallel; T010-T013 (new entities) all parallel; T015-T020 (interfaces) all parallel; T021-T023 (DTOs/requests) all parallel
- **Phase 4+5 vs Phase 6+7**: US3 (Validation) can run parallel with US1/US6 since it only touches BicepParameterParser (new file) and adds endpoints to TemplatesController
- **Phase 13**: Integration test tasks (T054-T057) are fully parallel

---

## Parallel Example: Phase 2 (Foundational)

```bash
# Batch 1: All enums in parallel (T005-T008)
T005: TemplateStatus.cs
T006: TemplateFormat.cs
T007: EnvironmentStatus.cs
T008: DriftSeverity.cs

# Batch 2: Entity expansion + new entities in parallel (T009-T013)
T009: ServiceTemplate.cs (expand)
T010: ProvisionedEnvironment.cs
T011: DeployedResource.cs
T012: DriftItem.cs
T013: EnvironmentActivity.cs

# Batch 3: DbContext (depends on entities)
T014: PlatformEngineeringCopilotContext.cs

# Batch 4: Interfaces + DTOs + Requests in parallel (T015-T023)
T015-T020: All interfaces
T021-T023: All DTOs and requests

# Batch 5: Program.cs + DI (depends on interfaces)
T024: Program.cs
T025: ServiceCollectionExtensions.cs
```

---

## Implementation Strategy

### MVP First (P1 Stories Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: US12 (API Infrastructure) — API boots and serves
4. Complete Phase 4: US1+US2 (Template CRUD + Approval) — working template catalog
5. Complete Phase 5: US6 (Environment Lifecycle) — environments can be provisioned
6. **STOP and VALIDATE**: Full MVP — templates, approval, environments all functional

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US12 → API boots → Deploy/Demo
3. US1+US2 → Template catalog → Deploy/Demo (MVP templates!)
4. US6 → Environment lifecycle → Deploy/Demo (MVP complete!)
5. US3 → Validation/parsing → Deploy/Demo
6. US4 → NL matching → Deploy/Demo
7. US5 → Git sync → Deploy/Demo
8. US7 → Monitoring → Deploy/Demo
9. US8+US9 → Drift + status → Deploy/Demo
10. US10+US11 → Cleanup + compliance → Full API surface complete

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] labels map tasks to spec.md user stories for traceability
- Each user story checkpoint should be independently testable
- Commit after each task or logical group
- Total tasks: 68 (60 implementation + 8 test tasks distributed across phases: T029a/b, T033a/b, T035a, T037a, T040a, T048a, T051a; plus T054 shared fixture and T055-T057 integration tests in Phase 13)
- All controllers follow the same cross-cutting pattern: try/catch, Serilog, CancellationToken, typed DTOs, MapToDto, ETag, proper HTTP status codes
- Background services all use the same BackgroundService + PeriodicTimer + CreateScope pattern from research.md decision 5
