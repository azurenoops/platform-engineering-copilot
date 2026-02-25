# Tasks: Admin Dashboard Client

**Input**: Design documents from `/specs/004-admin-client/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Unit tests included for service and model layers per constitution (Test-First Development, 80%+ coverage).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Source**: `src/Platform.Engineering.Copilot.Admin.Client/`
- **Tests**: `tests/Platform.Engineering.Copilot.Tests.Unit/AdminClient/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, NuGet packages, configuration, and shared static assets

- [X] T001 Update .csproj to add Blazored.Toast, Blazored.Modal, Blazored.LocalStorage, Microsoft.Extensions.Http NuGet packages in `src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj`
- [X] T002 Create `src/Platform.Engineering.Copilot.Admin.Client/wwwroot/appsettings.json` with `AdminApi:BaseUrl` configuration (default `http://localhost:5050`)
- [X] T003 [P] Update `src/Platform.Engineering.Copilot.Admin.Client/wwwroot/index.html` to replace local Bootstrap with CDN refs for Bootstrap 5.3.2 and Font Awesome 6.5.1, add theme.js script reference
- [X] T004 [P] Create `src/Platform.Engineering.Copilot.Admin.Client/wwwroot/js/theme.js` with JS interop functions: `setTheme(theme)`, `getSystemTheme()`, `watchSystemTheme(dotNetRef)`, and `disposeThemeWatcher()`
- [X] T005 [P] Update `src/Platform.Engineering.Copilot.Admin.Client/wwwroot/css/app.css` with custom styles: theme classes (theme-dark, theme-light), sidebar layout, status badges, loading spinners, empty states, card grid, pagination overrides

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented — data models, HTTP services, shared components, and app bootstrap

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Data Models

- [X] T006 [P] Create template DTOs (TemplateSummaryDto, TemplateDetailDto, TemplateParameterDto, TemplateGuardrailDto, TemplateValidationResultDto, TemplateMatchResultDto, TemplateMatchDto, GitStatusDto) in `src/Platform.Engineering.Copilot.Admin.Client/Models/Templates.cs`
- [X] T007 [P] Create environment DTOs (EnvironmentSummaryDto, TemplateCountDto, EnvironmentDetailDto, ResourceDto, ScaleResultDto, DeleteResourcesResultDto, ResourceFailureDto, RefreshDeploymentStatusResultDto) in `src/Platform.Engineering.Copilot.Admin.Client/Models/Environments.cs`
- [X] T008 [P] Create compliance DTOs (ComplianceSummaryDto, FrameworkScoreDto, EnvironmentComplianceStatusDto, ViolationDto, EnvironmentComplianceDto, FrameworkResultDto, ControlResultDto, ResourceComplianceDto) in `src/Platform.Engineering.Copilot.Admin.Client/Models/Compliance.cs`
- [X] T009 [P] Create drift DTOs (DriftDetectionResultDto, DriftItemDto, RemediateDriftResultDto, DriftFailureDto) in `src/Platform.Engineering.Copilot.Admin.Client/Models/Drift.cs`
- [X] T010 [P] Create health DTOs (EnvironmentHealthDto, ResourceHealthDto) in `src/Platform.Engineering.Copilot.Admin.Client/Models/Health.cs`
- [X] T011 [P] Create activity DTOs (ActivityDto, ActivityListDto) in `src/Platform.Engineering.Copilot.Admin.Client/Models/Activities.cs`
- [X] T012 [P] Create all request DTOs (CreateTemplateRequest, UpdateTemplateRequest, ApprovalRequest, ValidateTemplateRequest, ParseBicepParametersRequest, ParseBicepFromGitRequest, ImportFromGitRequest, CreateEnvironmentRequest, ScaleEnvironmentRequest, CloneEnvironmentRequest, ExtendExpirationRequest, RemediateDriftRequest, TemplateMatchRequest) in `src/Platform.Engineering.Copilot.Admin.Client/Models/Requests.cs`
- [X] T013 [P] Create AppSettings model with 28 properties across 6 categories (General, Notifications, Defaults, Display, Agents, Security) with factory defaults in `src/Platform.Engineering.Copilot.Admin.Client/Models/AppSettings.cs`

### Unit Tests for Models

- [X] T014 [P] Create template model unit tests (serialization, defaults, null handling) in `tests/Platform.Engineering.Copilot.Tests.Unit/AdminClient/Models/TemplateModelTests.cs`
- [X] T015 [P] Create environment model unit tests (serialization, defaults) in `tests/Platform.Engineering.Copilot.Tests.Unit/AdminClient/Models/EnvironmentModelTests.cs`
- [X] T016 [P] Create AppSettings model tests (factory defaults, serialization round-trip) in `tests/Platform.Engineering.Copilot.Tests.Unit/AdminClient/Models/AppSettingsTests.cs`

### HTTP Services

- [X] T017 Implement TemplateApiService with all template API methods (CRUD, approval workflow, validation, Git operations, matching) with try/catch error handling and ILogger in `src/Platform.Engineering.Copilot.Admin.Client/Services/TemplateApiService.cs`
- [X] T018 Implement EnvironmentApiService with all environment API methods (CRUD, lifecycle, drift, health, activities, resources, expiration, soft-delete) with try/catch error handling and ILogger in `src/Platform.Engineering.Copilot.Admin.Client/Services/EnvironmentApiService.cs`
- [X] T019 [P] Implement ComplianceApiService with compliance summary, scan, and environment detail methods with try/catch error handling and ILogger in `src/Platform.Engineering.Copilot.Admin.Client/Services/ComplianceApiService.cs`
- [X] T020 Implement AppSettingsService with localStorage persistence, theme JS interop, InitializeAsync, OnSettingsChanged event, and ResetToDefaults in `src/Platform.Engineering.Copilot.Admin.Client/Services/AppSettingsService.cs`. Edge case: handle localStorage unavailable/full gracefully (catch exceptions, fall back to in-memory defaults, show toast)

### Unit Tests for Services

- [X] T021 Create TemplateApiService unit tests using MockHttpMessageHandler for all API methods (success + error scenarios) in `tests/Platform.Engineering.Copilot.Tests.Unit/AdminClient/Services/TemplateApiServiceTests.cs`
- [X] T022 Create EnvironmentApiService unit tests using MockHttpMessageHandler for all API methods in `tests/Platform.Engineering.Copilot.Tests.Unit/AdminClient/Services/EnvironmentApiServiceTests.cs`
- [X] T023 [P] Create ComplianceApiService unit tests using MockHttpMessageHandler in `tests/Platform.Engineering.Copilot.Tests.Unit/AdminClient/Services/ComplianceApiServiceTests.cs`
- [X] T024 Create AppSettingsService unit tests (load, save, reset, theme application) with mocked ILocalStorageService and IJSRuntime in `tests/Platform.Engineering.Copilot.Tests.Unit/AdminClient/Services/AppSettingsServiceTests.cs`

### Shared Components

- [X] T025 [P] Create StatusBadge.razor shared component (status text → Bootstrap badge color mapping) in `src/Platform.Engineering.Copilot.Admin.Client/Shared/StatusBadge.razor`
- [X] T026 [P] Create Pagination.razor shared component (client-side pagination: page size, page numbers, previous/next, page-changed event) in `src/Platform.Engineering.Copilot.Admin.Client/Shared/Pagination.razor`
- [X] T027 [P] Create LoadingSpinner.razor shared component (centered spinner for page load, inline spinner for buttons) in `src/Platform.Engineering.Copilot.Admin.Client/Shared/LoadingSpinner.razor`
- [X] T028 [P] Create EmptyState.razor shared component (icon, message, action button, Retry button for error states) in `src/Platform.Engineering.Copilot.Admin.Client/Shared/EmptyState.razor`
- [X] T029 [P] Create ConfirmModal.razor shared component (standard delete/destructive confirmation with backdrop overlay) in `src/Platform.Engineering.Copilot.Admin.Client/Shared/ConfirmModal.razor`
- [X] T030 [P] Create TypeToConfirmModal.razor shared component (type-to-confirm for bulk actions: text input, phrase matching, disabled submit until match) in `src/Platform.Engineering.Copilot.Admin.Client/Shared/TypeToConfirmModal.razor`
- [X] T031 [P] Create Breadcrumb.razor shared component (dynamic breadcrumb trail from route segments) in `src/Platform.Engineering.Copilot.Admin.Client/Shared/Breadcrumb.razor`

### App Bootstrap

- [X] T032 Update `src/Platform.Engineering.Copilot.Admin.Client/_Imports.razor` with global usings for Models, Services, Shared, Blazored libraries, and Microsoft.AspNetCore.Components
- [X] T033 Update `src/Platform.Engineering.Copilot.Admin.Client/Program.cs` to register HttpClientFactory with AdminApi base URL from config, register all 4 scoped services, add Blazored service registrations, and call AppSettingsService.InitializeAsync() after build
- [X] T034 Update `src/Platform.Engineering.Copilot.Admin.Client/App.razor` to wrap Router in CascadingBlazoredModal, add BlazoredToasts (top-right, 5s timeout, progress bar), and add NotFound template

**Checkpoint**: Foundation ready - all models, services, shared components, and bootstrap are complete. User story implementation can begin.

---

## Phase 3: User Story 9 — Shell Layout & Navigation (Priority: P1) 🎯 MVP

**Goal**: Persistent sidebar, top header bar, dynamic page title, admin dropdown, active link highlighting — the shell that contains all other pages.

**Independent Test**: Click through all sidebar links, verify correct routing, active highlighting, and page title updates.

### Implementation for User Story 9

- [X] T035 [US9] Rewrite MainLayout.razor with fixed sidebar (5 nav sections: Dashboard, Service Templates, Environments, Operations with Drift Detection and Health Status sub-links, Compliance — each with Font Awesome icons), top header bar with dynamic page title (switch expression on URL), admin dropdown (Settings link + Sign Out placeholder), and @Body content area in `src/Platform.Engineering.Copilot.Admin.Client/Layout/MainLayout.razor`
- [X] T036 [US9] Update MainLayout.razor.css with scoped styles for sidebar (fixed, full-height, 250px width), active nav link highlighting, header bar, and admin dropdown in `src/Platform.Engineering.Copilot.Admin.Client/Layout/MainLayout.razor.css`
- [X] T037 [US9] Delete the old NavMenu.razor and NavMenu.razor.css files from `src/Platform.Engineering.Copilot.Admin.Client/Layout/` (replaced by sidebar in MainLayout)

**Checkpoint**: Shell layout renders with sidebar navigation. Clicking links routes to placeholder pages. Active link highlighting works.

---

## Phase 4: User Story 1 — Dashboard Overview (Priority: P1)

**Goal**: Summary cards, recent environments table, quick action buttons — the operator's at-a-glance platform view.

**Independent Test**: Load `/` and verify 8 summary cards render with API data, recent environments table shows 5 entries, and quick action buttons navigate correctly.

### Implementation for User Story 1

- [X] T038 [US1] Create Dashboard.razor page at route `/` with parallel data loading (Task.WhenAll for summary, environments, templates), 8 stat cards in 2 rows of 4, recent environments table (5 entries with StatusBadge, drift indicator, cost, name links), and quick action buttons (Create Template, Provision Environment, Detect Drift) with empty state and retry on API failure in `src/Platform.Engineering.Copilot.Admin.Client/Pages/Dashboard.razor`
- [X] T039 [US1] Delete the old Home.razor placeholder page from `src/Platform.Engineering.Copilot.Admin.Client/Pages/Home.razor`

**Checkpoint**: Dashboard loads with live data from Admin API. Summary cards, recent environments, and quick actions all render correctly.

---

## Phase 5: User Story 2 — Template Catalog & Creation (Priority: P1)

**Goal**: Browse, search, filter, create (paste + Git import), validate, approve, view detail, and edit templates.

**Independent Test**: Create a template via paste, validate it, submit for approval, approve it, view it in the catalog, edit it, and verify all detail fields render correctly.

### Implementation for User Story 2

- [X] T040 [P] [US2] Create TemplateCatalog.razor page at route `/templates` with card grid (3-col lg, 2-col md), search box, category dropdown filter, status dropdown filter, client-side pagination, empty state, loading spinner, and StatusBadge on each card in `src/Platform.Engineering.Copilot.Admin.Client/Pages/TemplateCatalog.razor`
- [X] T041 [US2] Create TemplateCreate.razor page at route `/templates/create` with breadcrumb, radio toggle (Paste/Git Import), basic info fields (Name, DisplayName, Description, Version, Category, Format, DeploymentScope), content textarea or Git fields, "Parse from Content" button, parameter list with add/remove/edit (de-duplicate by name; skip duplicates and show count in toast), guardrail list with add/remove, inline blur validation, and submit with navigation to detail page in `src/Platform.Engineering.Copilot.Admin.Client/Pages/TemplateCreate.razor`. Edge cases: handle empty parse results (toast "No parameters found"); handle duplicate parameter names (skip with toast count)
- [X] T042 [P] [US2] Create TemplateDetail.razor page at route `/templates/{Id:guid}` with breadcrumb, content code block, parameters table, guardrails table, Git source info, metadata, approval info, additional files with expand/collapse, and action buttons (Submit for Approval, Approve with ApprovedBy input, Deprecate, Edit, Delete with ConfirmModal) in `src/Platform.Engineering.Copilot.Admin.Client/Pages/TemplateDetail.razor`. Edge case: handle 404 (template deleted while viewing) by showing error toast and navigating back to catalog
- [X] T043 [US2] Create TemplateEdit.razor page at route `/templates/edit/{Id:guid}` with breadcrumb, pre-populated form, disabled Name/Version fields, in-place parameter editing, "Parse from Content" with smart merge of new parameters, inline blur validation, and save with navigation to detail page in `src/Platform.Engineering.Copilot.Admin.Client/Pages/TemplateEdit.razor`
- [X] T044 [US2] Delete the old Templates.razor placeholder page from `src/Platform.Engineering.Copilot.Admin.Client/Pages/Templates.razor`

**Checkpoint**: Full template lifecycle works: catalog → create → validate → approve → detail → edit → deprecate → delete.

---

## Phase 6: User Story 3 — Environment Provisioning & Lifecycle (Priority: P1)

**Goal**: List environments, provision from templates with dynamic parameters, manage lifecycle (scale, clone, delete, purge).

**Independent Test**: Select a published template, provision an environment, verify it appears in the list, perform lifecycle actions.

### Implementation for User Story 3

- [X] T045 [P] [US3] Create EnvironmentList.razor page at route `/environments` with environment table (StatusBadge, drift indicator, cost, expiration warnings), per-environment action dropdown (View, Scale, Clone, Detect Drift, Remediate, Reprovision, Delete with ConfirmModal including "Also delete Azure resources" checkbox), "View Deleted" toggle with purge options and TypeToConfirmModal for Purge All, client-side pagination, empty state, and loading spinner in `src/Platform.Engineering.Copilot.Admin.Client/Pages/EnvironmentList.razor`
- [X] T046 [US3] Create EnvironmentCreate.razor page at route `/environments/create` with breadcrumb, template selector dropdown (published templates only), dynamic parameter rendering based on selected template's parameter definitions (bool→checkbox, choice→dropdown, number→number input, default→text), pre-populated defaults from AppSettingsService (subscription, location, expiration days), Azure fields (SubscriptionId, ResourceGroup, Location), lifecycle fields (ExpiresAt, AutoDelete), inline blur validation, and submit with navigation to detail page in `src/Platform.Engineering.Copilot.Admin.Client/Pages/EnvironmentCreate.razor`
- [X] T047 [US3] Delete the old Environments.razor placeholder page from `src/Platform.Engineering.Copilot.Admin.Client/Pages/Environments.razor`
- [X] T048 [US3] Delete the old Deployments.razor placeholder page from `src/Platform.Engineering.Copilot.Admin.Client/Pages/Deployments.razor`

**Checkpoint**: Environment provisioning works end-to-end. List shows environments with actions. Delete/Purge workflows work with confirmation.

---

## Phase 7: User Story 4 — Environment Details & Monitoring (Priority: P2)

**Goal**: 7-tab environment detail page with overview, parameters, tags, resources, logs, activity, and drift tabs.

**Independent Test**: Navigate to an environment detail page, verify all 7 tabs render data, action buttons work, and lazy-loaded tabs fetch on activation.

### Implementation for User Story 4

- [X] T049 [US4] Create EnvironmentDetail.razor page at route `/environments/{Id:guid}` with breadcrumb, 7 Bootstrap tabs (Overview, Parameters, Tags, Resources, Logs, Activity Log, Drift), overview tab (status, cost, drift, expiration, metadata definition list), parameters tab (key-value table from ParameterValuesJson), tags tab (key-value table from TagsJson), resources tab (lazy-load with type-specific icons, provisioning state, Azure Portal links, Sync from Azure button), logs tab (deployment logs), activity tab (lazy-load with typed icons, Load More pagination), drift tab (summary cards for Missing/Extra/Config Changes/Auto-Remediable, drift item list with expected vs actual, Detect Drift and Remediate All buttons), and expiration warning card (red text if <7 days, extend button) in `src/Platform.Engineering.Copilot.Admin.Client/Pages/EnvironmentDetail.razor`

**Checkpoint**: Environment detail page renders all 7 tabs with correct data from the API. Lazy-loaded tabs fetch on activation.

---

## Phase 8: User Story 5 — Compliance Dashboard & Scanning (Priority: P2)

**Goal**: Compliance overview with framework scores, scan triggering, and per-environment compliance drill-down.

**Independent Test**: Load compliance dashboard, verify scores and violations render, trigger scan, drill into environment compliance detail.

### Implementation for User Story 5

- [X] T050 [P] [US5] Create ComplianceDashboard.razor page at route `/compliance` with overall score card (color-coded: green ≥80, yellow ≥60, red <60), framework progress bars with score %, top violations table, per-environment compliance status table with "View Details" links, "Scan All" button with 2-second delay reload, and "Scan Environment" dropdown, loading spinner, and empty state in `src/Platform.Engineering.Copilot.Admin.Client/Pages/ComplianceDashboard.razor`
- [X] T051 [P] [US5] Create ComplianceDetail.razor page at route `/compliance/environment/{Id:guid}` with breadcrumb, framework results with control lists, filterable control status (Compliant/Non-Compliant dropdown), expandable remediation guidance rows, resource compliance table, and loading spinner in `src/Platform.Engineering.Copilot.Admin.Client/Pages/ComplianceDetail.razor`

**Checkpoint**: Compliance dashboard and detail pages render correctly. Scanning triggers API call and refreshes data.

---

## Phase 9: User Story 6 — Drift Detection & Remediation (Priority: P2)

**Goal**: Centralized drift detection page with bulk scan, per-environment scan with individual spinners, and remediation.

**Independent Test**: Load drift page, click "Scan All", verify per-environment results, trigger individual scans with spinners.

### Implementation for User Story 6

- [X] T052 [US6] Create DriftDetection.razor page at route `/drift` with environment table (drift status badges: "In Sync" or drift count), "Scan All Environments" button (parallel scan), per-environment scan button with individual spinner (tracked via HashSet<string>), remediate button per environment, loading spinner, and empty state in `src/Platform.Engineering.Copilot.Admin.Client/Pages/DriftDetection.razor`

**Checkpoint**: Drift detection page scans environments individually with per-row spinners and shows drift status.

---

## Phase 10: User Story 7 — Health Status Monitoring (Priority: P3)

**Goal**: Health overview page with summary cards and per-environment health checks.

**Independent Test**: Load health page, verify summary cards render, per-environment health checks show correctly.

### Implementation for User Story 7

- [X] T053 [US7] Create HealthStatus.razor page at route `/health` with summary cards (Healthy/Degraded/Unhealthy counts), per-environment health table (health badge, estimated cost, individual check button), "Refresh All" button, loading spinner, and empty state in `src/Platform.Engineering.Copilot.Admin.Client/Pages/HealthStatus.razor`

**Checkpoint**: Health page renders with summary cards and per-environment health status.

---

## Phase 11: User Story 8 — Application Settings Management (Priority: P3)

**Goal**: 6-tab settings page with 28 configurable properties, localStorage persistence, immediate theme application, and reset to defaults.

**Independent Test**: Navigate to `/settings`, change values across all 6 tabs, save, refresh, verify persistence. Toggle theme and verify instant apply.

### Implementation for User Story 8

- [X] T054 [US8] Create Settings.razor page at route `/settings` with 6 Bootstrap tabs (General, Notifications, Defaults, Display, Agents, Security), bound form inputs for all 28 AppSettings properties, "Save Settings" button (persist to localStorage, apply theme, show success toast), "Reset to Defaults" button (factory reset, save, show toast), inline blur validation, and Auto theme with live OS preference tracking in `src/Platform.Engineering.Copilot.Admin.Client/Pages/Settings.razor`

**Checkpoint**: Settings page persists to localStorage. Theme changes apply instantly. Reset to defaults works.

---

## Phase 12: User Story 10 — Containerized Deployment (Priority: P3)

**Goal**: Two-stage Docker build, nginx with reverse proxy, caching, gzip, health endpoint, and client-side routing fallback.

**Independent Test**: Build Docker image, run container, verify `/health` → 200, `/_framework/` → immutable cache, `/templates/x` → fallback to index.html, `/api/templates` → proxied to Admin API.

### Implementation for User Story 10

- [X] T055 [P] [US10] Update nginx.conf with `/health` endpoint (return 200 "healthy"), `/_framework/` immutable cache (1 year), `try_files` fallback to index.html, `/api/` reverse proxy to `platform-admin-api:5050`, gzip for text/css/json/js/xml/wasm in `src/Platform.Engineering.Copilot.Admin.Client/nginx.conf`
- [X] T056 [P] [US10] Update Dockerfile with two-stage build (SDK 9.0 publish → nginx:alpine), copy wwwroot output to nginx html, copy nginx.conf, expose port 80 in `src/Platform.Engineering.Copilot.Admin.Client/Dockerfile`

**Checkpoint**: Docker image builds. Container serves SPA with correct caching, gzip, routing fallback, and API proxying.

---

## Phase 13: Polish & Cross-Cutting Concerns

**Purpose**: Cleanup, validation, and documentation

- [X] T057 [P] Remove old `src/Platform.Engineering.Copilot.Admin.Client/wwwroot/sample-data/weather.json` sample data file
- [X] T058 [P] Remove local Bootstrap library files from `src/Platform.Engineering.Copilot.Admin.Client/wwwroot/lib/bootstrap/` (replaced by CDN)
- [X] T059 Verify `dotnet build Platform.Engineering.Copilot.sln` passes with zero errors and zero new warnings
- [X] T060 Run all unit tests with `dotnet test` and verify 80%+ coverage on AdminClient service and model code
- [X] T061 Run quickstart.md validation: start Admin API, start Admin Client, verify dashboard loads at `http://localhost:5000`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Story 9 (Phase 3)**: Depends on Phase 2 — layout is prerequisite for all pages
- **User Stories 1-3 (Phases 4-6)**: Depend on Phase 3 (layout) — P1 stories, can be parallel after layout
- **User Stories 4-6 (Phases 7-9)**: Depend on Phase 3 (layout) — P2 stories, can be parallel after layout
- **User Stories 7-8 (Phases 10-11)**: Depend on Phase 3 (layout) — P3 stories, can be parallel after layout
- **User Story 10 (Phase 12)**: Depends on Phase 1 only — Docker/nginx are independent
- **Polish (Phase 13)**: Depends on all phases being complete

### User Story Dependencies

- **US9 (Shell Layout)**: Must complete first — all pages render inside this layout
- **US1 (Dashboard)**: Depends on US9 for layout shell; uses EnvironmentApiService + TemplateApiService
- **US2 (Templates)**: Depends on US9; uses TemplateApiService
- **US3 (Environments)**: Depends on US9; uses EnvironmentApiService + TemplateApiService
- **US4 (Env Details)**: Depends on US9; uses EnvironmentApiService
- **US5 (Compliance)**: Depends on US9; uses ComplianceApiService
- **US6 (Drift)**: Depends on US9; uses EnvironmentApiService
- **US7 (Health)**: Depends on US9; uses EnvironmentApiService
- **US8 (Settings)**: Depends on US9; uses AppSettingsService
- **US10 (Docker)**: No dependency on other stories — pure infrastructure

### Within Each Phase

- Models before services (Phase 2)
- Services before pages (Phase 2 → Phase 3+)
- Tests alongside or after the code they test
- Shared components before pages that use them

### Parallel Opportunities

- T003, T004, T005 (Setup static assets) — all parallel
- T006–T013 (all data models) — all parallel
- T014–T016 (all model tests) — all parallel
- T025–T031 (all shared components) — all parallel
- T040 and T042 (TemplateCatalog and TemplateDetail) — parallel
- T045 (EnvironmentList) can parallel with T040
- T050 and T051 (Compliance pages) — parallel
- T055 and T056 (nginx + Dockerfile) — parallel
- T057 and T058 (cleanup) — parallel

---

## Parallel Example: Phase 2 Models

```bash
# Launch all model files in parallel (T006–T013):
Task: "Create template DTOs in Models/Templates.cs"
Task: "Create environment DTOs in Models/Environments.cs"
Task: "Create compliance DTOs in Models/Compliance.cs"
Task: "Create drift DTOs in Models/Drift.cs"
Task: "Create health DTOs in Models/Health.cs"
Task: "Create activity DTOs in Models/Activities.cs"
Task: "Create request DTOs in Models/Requests.cs"
Task: "Create AppSettings model in Models/AppSettings.cs"
```

## Parallel Example: Phase 5 Template Pages

```bash
# After TemplateApiService (T017) is complete:
Task: "Create TemplateCatalog.razor" (T040) — parallel with —
Task: "Create TemplateDetail.razor" (T042)
# Then sequentially:
Task: "Create TemplateCreate.razor" (T041)
Task: "Create TemplateEdit.razor" (T043)
```

---

## Implementation Strategy

### MVP First (User Stories 9 + 1 Only)

1. Complete Phase 1: Setup (T001–T005)
2. Complete Phase 2: Foundational (T006–T034)
3. Complete Phase 3: US9 Shell Layout (T035–T037)
4. Complete Phase 4: US1 Dashboard (T038–T039)
5. **STOP and VALIDATE**: Layout + Dashboard render with live Admin API data
6. Deploy/demo if ready — operators have centralized platform visibility

### Incremental Delivery

1. Setup + Foundational + US9 → Shell ready
2. + US1 Dashboard → **MVP!** Operators can see platform state
3. + US2 Templates → Full template lifecycle
4. + US3 Environments → Full environment provisioning
5. + US4 Environment Details → Deep environment visibility
6. + US5 Compliance → Compliance monitoring
7. + US6 Drift → Drift detection
8. + US7 Health → Health monitoring
9. + US8 Settings → Customization
10. + US10 Docker → Production-ready deployment
11. Polish → Cleanup + validation

### Parallel Team Strategy

With multiple developers after Phase 2:

1. Team completes Setup + Foundational together
2. Developer A: US9 Shell → then US1 Dashboard
3. Once US9 layout is done:
   - Developer A: US1 Dashboard
   - Developer B: US2 Templates
   - Developer C: US3 Environments
4. P2 stories (US4, US5, US6) can proceed in parallel
5. P3 stories (US7, US8, US10) can proceed in parallel

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- All services use try/catch + ILogger + return null/empty pattern per FR-016
- All pages use LoadingSpinner + EmptyState (with Retry) + toast notifications pattern
- Delete old placeholder pages (Home.razor, Templates.razor, Environments.razor, Deployments.razor) as they are replaced
