# Feature Specification: Admin Dashboard Client

**Feature Branch**: `004-admin-client`  
**Created**: 2026-02-23  
**Status**: Draft  
**Input**: User description: "Blazor WebAssembly admin dashboard for managing service templates, provisioned environments, compliance posture, drift detection, health monitoring, and application settings. Pure client-side SPA talking to Admin API over HTTP."

## Clarifications

### Session 2026-02-23

- Q: How should list views handle data volume (pagination strategy)? → A: Client-side pagination — load all data from the API, paginate in browser with page controls.
- Q: Should pages provide a retry mechanism when API calls fail? → A: Show a "Retry" button on error/empty states that re-invokes the page's data load.
- Q: What level of confirmation is required for bulk destructive operations (e.g., Purge All)? → A: Standard modal for single deletes; type-to-confirm (e.g., type "PURGE ALL") for bulk destructive actions.
- Q: What should the "Auto" theme option do? → A: Follow OS/browser `prefers-color-scheme` preference and update live if the OS setting changes mid-session.
- Q: How should forms validate user input? → A: Inline validation on field blur (lose focus) — show per-field errors immediately, disable submit until all fields are valid.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Dashboard Overview (Priority: P1)

An infrastructure platform operator opens the admin dashboard in their browser and immediately sees a summary of their platform's health: environment counts by status, template count, drift warnings, estimated costs, and recent environment activity. This gives them an at-a-glance operational picture without navigating to multiple pages.

**Why this priority**: The dashboard is the entry point and primary orientation tool. Without it, operators have no centralized view of platform state and must navigate blindly. It also validates the entire application bootstrap, layout, API connectivity, and data rendering pipeline.

**Independent Test**: Can be fully tested by loading the root URL and verifying that summary cards, recent environments table, and quick action buttons render correctly with data from the Admin API.

**Acceptance Scenarios**:

1. **Given** the Admin API is running with at least one environment, **When** the operator navigates to `/`, **Then** eight summary cards display with correct values (Total Environments, Healthy, With Drift, Failed, Templates, Provisioning, Expiring Soon, Est. Monthly Cost).
2. **Given** environments exist in the system, **When** the dashboard loads, **Then** the five most recent environments appear in a table with name links, template, status badge, drift indicator, and cost.
3. **Given** the dashboard is loaded, **When** the operator clicks "Create Template", **Then** they are navigated to `/templates/create`.
4. **Given** the dashboard is loaded, **When** the operator clicks "Provision Environment", **Then** they are navigated to `/environments/create`.
5. **Given** the Admin API is unreachable, **When** the dashboard loads, **Then** a toast error notification appears, the page shows an empty/default state with a "Retry" button, and clicking "Retry" re-attempts data loading without crashing.

---

### User Story 2 — Template Catalog Browsing & Creation (Priority: P1)

An operator browses the template catalog to find existing IaC templates, filters by category or status, creates new templates (via paste or Git import), validates template content, manages parameters and guardrails, and moves templates through the approval workflow (submit → approve → deprecate → delete).

**Why this priority**: Templates are the foundational building blocks of the platform — environments can't be provisioned without them. The full template lifecycle (create, validate, approve, deploy) is the most critical operator workflow.

**Independent Test**: Can be tested by creating a template via the form, validating it, submitting for approval, approving it, viewing it in the catalog, and verifying all detail fields render correctly.

**Acceptance Scenarios**:

1. **Given** templates exist in the system, **When** the operator navigates to `/templates`, **Then** template cards display in a grid with name, description, category, format, version, status badge, and deployment count.
2. **Given** the template catalog is shown, **When** the operator types in the search box, **Then** templates are filtered by display name, description, or keywords in real time.
3. **Given** the operator navigates to `/templates/create`, **When** they fill in basic info, paste Bicep content, click "Parse from Content", add parameters and guardrails, and submit, **Then** a new template is created and they are navigated to the detail page.
4. **Given** the operator leaves a required field empty on the create template form, **When** the field loses focus, **Then** an inline validation error appears beneath the field and the submit button remains disabled.
5. **Given** the operator chooses Git import mode, **When** they enter a repository URL, branch, and path, **Then** the template is imported from Git and created in the system.
6. **Given** a draft template exists, **When** the operator clicks "Submit for Approval" then "Approve" (with `ApprovedBy`), **Then** the template status transitions to Published.
7. **Given** a published template exists, **When** the operator opens its detail page, **Then** all fields are displayed: content in a code block, parameters, guardrails, Git source info, metadata, and approval info.
8. **Given** the operator navigates to `/templates/edit/{id}`, **When** they modify fields and save, **Then** the template is updated and they are navigated to the detail page with updated values.

---

### User Story 3 — Environment Provisioning & Lifecycle (Priority: P1)

An operator provisions a new cloud environment by selecting a published template, configuring parameters, specifying Azure resource details (subscription, resource group, location), and setting lifecycle policies (expiration, auto-delete). After creation, they manage the environment through its lifecycle: scaling, cloning, deleting, and purging.

**Why this priority**: Environment provisioning is the core value proposition — operators create cloud infrastructure from templates. Without this, the platform has no operational purpose.

**Independent Test**: Can be tested by selecting a published template, filling in environment details, submitting, verifying the environment appears in the list with correct status, then performing lifecycle actions.

**Acceptance Scenarios**:

1. **Given** at least one published template exists, **When** the operator navigates to `/environments/create` and selects a template, **Then** the form shows dynamically rendered parameter inputs matching the template's parameter definitions (checkboxes for booleans, dropdowns for choices, number inputs, text inputs).
2. **Given** template parameters have defaults, **When** the template is selected, **Then** parameter inputs are pre-populated with default values.
3. **Given** the operator fills in all required fields, **When** they submit, **Then** the environment is created and they are navigated to `/environments/{id}`.
4. **Given** environments exist, **When** the operator navigates to `/environments`, **Then** environments are listed with status badges, drift indicators, cost, expiration warnings, and action dropdowns.
5. **Given** a running environment, **When** the operator selects "Scale" from actions, **Then** a scale request is sent to the API.
6. **Given** a soft-deleted environment, **When** the operator toggles "View Deleted", **Then** deleted environments are shown with purge options.
7. **Given** the operator clicks "Purge All" in the deleted view, **When** the purge-all modal appears, **Then** the confirm button is disabled until the operator types "PURGE ALL" in the text input.
8. **Given** the operator enables "Also delete Azure resources" in the delete modal, **When** they confirm deletion, **Then** Azure resources are deleted before the environment record is soft-deleted.

---

### User Story 4 — Environment Details & Monitoring (Priority: P2)

An operator views detailed information about a specific environment across seven tabs: overview, parameters, tags, deployed resources, deployment logs, activity timeline, and drift status. They can sync resources from Azure, detect drift, remediate drift, and extend expiration.

**Why this priority**: After provisioning, operators need deep visibility into environment state. This is essential for troubleshooting, auditing, and ongoing management, but depends on environments existing first.

**Independent Test**: Can be tested by navigating to an environment's detail page and verifying all seven tabs render correct data, action buttons function, and lazy-loaded tabs (Activity, Resources) fetch data on activation.

**Acceptance Scenarios**:

1. **Given** an environment exists, **When** the operator navigates to `/environments/{id}`, **Then** the overview tab shows status, cost, drift status, expiration, and metadata in a definition list.
2. **Given** the operator clicks the "Resources" tab, **Then** deployed Azure resources are loaded and displayed with type-specific icons, provisioning state, and Azure Portal links.
3. **Given** the operator clicks "Sync from Azure", **Then** resources are synchronized and a toast shows the count of new resources found.
4. **Given** the operator clicks the "Activity" tab, **Then** the activity timeline loads with typed icons and supports "Load More" pagination.
5. **Given** drift exists on the environment, **When** the operator views the Drift tab, **Then** summary cards show counts by drift type (Missing, Extra, Config Changes, Auto-Remediable) and individual drift items are listed with expected vs actual values.
6. **Given** drift items exist, **When** the operator clicks "Remediate All", **Then** drift remediation is triggered and the results are displayed.
7. **Given** an environment is expiring within 7 days, **When** the detail page loads, **Then** the expiration card shows red text with a days-until count.

---

### User Story 5 — Compliance Dashboard & Scanning (Priority: P2)

An operator monitors compliance posture across all environments from the compliance dashboard, drills into individual environment compliance details, and triggers compliance scans. They view framework scores, control violations, and resource-level compliance status.

**Why this priority**: Compliance is critical for regulated environments (FedRAMP, NIST) but is a monitoring/auditing concern that depends on environments being provisioned first.

**Independent Test**: Can be tested by loading the compliance dashboard, verifying framework scores and violations render, triggering a scan, and drilling into an environment's compliance detail page.

**Acceptance Scenarios**:

1. **Given** compliance data exists, **When** the operator navigates to `/compliance`, **Then** summary cards show overall score (color-coded), compliant/non-compliant/total controls.
2. **Given** framework scores exist, **When** the compliance dashboard loads, **Then** each framework shows a progress bar with score percentage and control counts.
3. **Given** the operator clicks "Scan All Environments", **Then** a compliance scan is triggered, a 2-second delay occurs, and fresh data is loaded.
4. **Given** the operator clicks "View Details" for an environment, **When** they navigate to `/compliance/environment/{id}`, **Then** control compliance details show with filterable status (Compliant/Non-Compliant) and expandable remediation guidance.
5. **Given** a non-compliant control, **When** the operator clicks "Guidance", **Then** remediation guidance text and affected resource IDs are shown in an expandable row.

---

### User Story 6 — Drift Detection & Remediation (Priority: P2)

An operator uses the centralized drift detection page to scan all environments for configuration drift, view drift status per environment, and trigger remediation. The page shows which environments are in sync and which have drifted, with the ability to scan individual environments.

**Why this priority**: Drift detection is a key operational concern but is a monitoring capability that builds on top of provisioned environments.

**Independent Test**: Can be tested by loading the drift page, clicking "Scan All", verifying per-environment drift results, and triggering individual scans.

**Acceptance Scenarios**:

1. **Given** environments exist, **When** the operator navigates to `/drift`, **Then** all environments are listed with drift status badges (In Sync or drift count).
2. **Given** the operator clicks "Scan All Environments", **Then** drift detection runs on all environments in parallel and results update in the table.
3. **Given** an environment has drift, **When** the operator clicks the remediate button, **Then** drift remediation is triggered and results are displayed.
4. **Given** a scan is in progress for a specific environment, **Then** that environment's scan button shows a spinner and is disabled.

---

### User Story 7 — Health Status Monitoring (Priority: P3)

An operator views the health status of all environments from a centralized health page, seeing which environments are healthy, degraded, or unhealthy, with the ability to check individual environments.

**Why this priority**: Health monitoring provides operational awareness but overlaps with information available on the dashboard and individual environment detail pages. It's a convenience aggregation view.

**Independent Test**: Can be tested by loading the health page and verifying health status cards and per-environment health checks render correctly.

**Acceptance Scenarios**:

1. **Given** environments exist, **When** the operator navigates to `/health`, **Then** summary cards show Healthy/Degraded/Unhealthy counts.
2. **Given** the health page is loaded, **When** individual health checks complete, **Then** each environment shows a health badge and estimated cost.
3. **Given** the operator clicks "Refresh All", **Then** all health status data is re-checked and updated.

---

### User Story 8 — Application Settings Management (Priority: P3)

An operator configures application-wide settings including general preferences, notification toggles, deployment defaults, display/theme preferences, AI agent behavior, and security policies. Settings persist in browser localStorage and apply immediately.

**Why this priority**: Settings personalize the experience but the application functions fully with defaults. This is a convenience and customization feature.

**Independent Test**: Can be tested by navigating to `/settings`, changing values across all six tabs, saving, refreshing the page, and verifying settings persisted.

**Acceptance Scenarios**:

1. **Given** the operator navigates to `/settings`, **Then** six tabs are shown: General, Notifications, Defaults, Display, Agents, Security.
2. **Given** the operator changes the theme to "Dark", **When** they save, **Then** the body CSS class changes to `theme-dark` immediately.
3. **Given** the operator selects "Auto" theme, **When** the OS switches from light to dark mode, **Then** the application theme updates to dark without page reload or manual intervention.
4. **Given** the operator clicks "Reset to Defaults", **Then** all settings revert to default values and a confirmation toast appears.
5. **Given** the operator saves settings, **When** they refresh the page, **Then** previously saved settings are restored from localStorage.

---

### User Story 9 — Shell Layout & Navigation (Priority: P1)

An operator navigates the application using a persistent sidebar with organized navigation sections and a top header bar showing the current page title. The sidebar groups links logically (Dashboard, Service Templates, Environments, Operations, Compliance) with icons, and the active page is highlighted. The Operations section includes links to Drift Detection and Health Status.

**Why this priority**: The shell layout is the container for all other user stories — without it, no page is reachable. It's foundational infrastructure for the entire UI.

**Independent Test**: Can be tested by clicking through all sidebar links and verifying correct routing, active link highlighting, and dynamic page title updates.

**Acceptance Scenarios**:

1. **Given** the application loads, **Then** the sidebar is visible with five navigation sections: Dashboard, Service Templates, Environments, Operations (with Drift Detection and Health Status sub-links), Compliance.
2. **Given** the operator clicks "Template Catalog", **Then** the browser navigates to `/templates` and the link receives the `active` CSS class.
3. **Given** the operator is on `/templates/edit/abc-123`, **Then** the top bar shows "Edit Template".
4. **Given** the operator clicks the admin dropdown, **Then** a link to `/settings` and a "Sign Out" placeholder are shown.

---

### User Story 10 — Containerized Deployment (Priority: P3)

The operations team builds the admin client as a Docker image with a two-stage build (SDK publish + nginx), serves the Blazor WASM static files with proper caching, gzip compression, client-side routing fallback, API reverse proxy, and a health check endpoint.

**Why this priority**: Containerization is a deployment concern, not a user-facing feature. The app works in development without it. It's needed for production but doesn't affect functionality.

**Independent Test**: Can be tested by building the Docker image, running the container, and verifying `/health` returns 200, `/_framework/` assets have immutable cache headers, `/api/` requests are proxied, and client-side routing works.

**Acceptance Scenarios**:

1. **Given** the Dockerfile exists, **When** `docker build` is run, **Then** a valid Docker image is produced.
2. **Given** the container is running, **When** a request is made to `/health`, **Then** a 200 response with "healthy" is returned.
3. **Given** the container is running, **When** a request is made to `/_framework/blazor.webassembly.js`, **Then** the response has `Cache-Control: public, max-age=31536000, immutable`.
4. **Given** the container is running, **When** a request is made to `/templates/some-id`, **Then** the response falls back to `index.html` for client-side routing.
5. **Given** the container is running with the Admin API available, **When** a request is made to `/api/templates`, **Then** the request is proxied to `platform-admin-api:5050`.

---

### Edge Cases

- What happens when the Admin API is completely unreachable? → All pages display gracefully with empty states and error toasts; no unhandled exceptions.
- What happens when a template is deleted while the operator is viewing its detail page? → API returns 404, page shows error toast and navigates back to catalog.
- What happens when the operator submits a template creation form with duplicate parameter names? → Only unique parameters are added; duplicates are skipped with a count shown in a toast.
- What happens when an environment's expiration date is in the past? → The expiration badge shows with danger/red styling indicating it has expired.
- What happens when the browser localStorage is full or unavailable? → Settings operations fail gracefully with a toast notification; the app continues with defaults.
- What happens when a Bicep template has no parseable parameters? → The parser returns an empty list and a toast indicates no parameters were found.
- What happens when multiple environments are being drift-scanned in parallel? → Each environment's scan button independently shows a spinner tracked via a `HashSet<string>`.
- What happens when the operator navigates to a route that doesn't exist? → The Router's `NotFound` template renders a "Page not found" message inside the layout.

## Requirements *(mandatory)*

### Functional Requirements

#### Project Setup & Bootstrap

- **FR-001**: System MUST be a Blazor WebAssembly project using `Microsoft.NET.Sdk.BlazorWebAssembly` targeting `net9.0` with no project references to other solution projects.
- **FR-002**: System MUST register a named `HttpClient` with base address from configuration (`AdminApi:BaseUrl`, default `http://localhost:5050`).
- **FR-003**: System MUST register four scoped services: `TemplateApiService`, `EnvironmentApiService`, `ComplianceApiService`, and `AppSettingsService`.
- **FR-004**: System MUST add Blazored.Toast, Blazored.Modal, and Blazored.LocalStorage service registrations.
- **FR-005**: System MUST call `AppSettingsService.InitializeAsync()` after host build to load persisted settings and apply the saved theme before first render.

#### Root & Layout

- **FR-006**: System MUST wrap the Blazor Router in `CascadingBlazoredModal` and render `BlazoredToasts` positioned top-right with 5-second timeout and progress bar.
- **FR-007**: System MUST provide a fixed sidebar layout with five navigation sections (Dashboard, Service Templates, Environments, Operations, Compliance), each with icon and header, and active link highlighting. The Operations section MUST include links to Drift Detection (`/drift`) and Health Status (`/health`).
- **FR-008**: System MUST display a dynamic page title in the top header bar derived from the current URL path via a switch expression.
- **FR-009**: System MUST include an admin dropdown in the top bar with a link to `/settings` and a "Sign Out" placeholder.

#### Data Models

- **FR-010**: System MUST define client-side DTO classes mirroring the Admin API's request/response models for templates, environments, compliance, deployed resources, drift, health, and activity entities.
- **FR-011**: System MUST use `System.Text.Json` serialization for all DTO classes.

#### HTTP Service Layer

- **FR-012**: `TemplateApiService` MUST provide methods for template CRUD, approval workflow (submit/approve/deprecate), validation, Git operations (import/sync/sync-all/status), and natural language matching.
- **FR-013**: `EnvironmentApiService` MUST provide methods for environment CRUD, lifecycle (scale/clone/reprovision/delete), drift (detect/remediate), health, activities, resources (list/sync), expiration extension, and soft-delete management (deleted list/purge/purge-all).
- **FR-014**: `ComplianceApiService` MUST provide methods for compliance summary, scan (global and per-environment), and environment compliance detail.
- **FR-015**: `AppSettingsService` MUST persist settings in browser localStorage under key `"platform_engineering_settings"`, apply dark/light/auto themes via JS interop, and expose an `OnSettingsChanged` event.
- **FR-016**: All HTTP service methods MUST wrap API calls in try/catch blocks, log errors via `ILogger`, and return null/empty collections on failure.

#### Dashboard Page

- **FR-017**: Dashboard MUST load data in parallel using `Task.WhenAll` for summary, environments, and templates API calls.
- **FR-018**: Dashboard MUST display eight summary stat cards in two rows of four showing environment counts, template count, and estimated cost.
- **FR-019**: Dashboard MUST show a recent environments table with the 5 most recent entries including status badges and drift indicators.
- **FR-020**: Dashboard MUST provide quick action buttons navigating to template creation, environment provisioning, and drift detection.

#### Template Pages

- **FR-021**: Template catalog MUST display templates in a card grid (3 columns lg, 2 md) with search, category filter, status filter, and client-side pagination controls.
- **FR-022**: Template cards MUST show display name, description (truncated to 100 chars), status badge, Git source indicator, category, format, version, deployment count, and relative sync time.
- **FR-023**: Create template form MUST support two source modes: paste IaC content and import from Git, with a radio toggle to switch between them.
- **FR-024**: Create template form MUST support Bicep parameter parsing from pasted content or Git source via the Admin API, with de-duplication of existing parameters.
- **FR-025**: Create template form MUST allow adding/removing parameters with typed inputs (Name, Display Name, Description, Type, Required, Default Value) and guardrails (Name, Type, Action, Property, Operator, Value, Error Message).
- **FR-026**: Template detail page MUST display all template fields including content in a code block, additional files with expand/collapse, parameters, guardrails, Git source info, metadata, and approval info.
- **FR-027**: Edit template form MUST pre-populate all fields from the existing template, disable Name and Version fields, support in-place parameter editing, and perform smart merge of parsed Bicep parameters.

#### Environment Pages

- **FR-028**: Environments list MUST show active environments with status badges, drift indicators, cost, expiration warnings, per-environment action dropdowns (View, Detect Drift, Remediate, Reprovision, Delete), and client-side pagination controls.
- **FR-029**: Environments list MUST support a "View Deleted" toggle showing soft-deleted environments with purge options.
- **FR-030**: Create environment form MUST dynamically render template parameters based on the selected template's parameter definitions (boolean → checkbox, choice → dropdown, number → number input, others → text input).
- **FR-031**: Create environment form MUST pre-populate defaults from `AppSettingsService` (subscription ID, location, expiration days).
- **FR-032**: Environment detail page MUST provide seven tabs: Overview, Parameters, Tags, Resources, Logs, Activity Log, Drift, with lazy-loading for Activity and Resources tabs.
- **FR-033**: Environment detail Resources tab MUST display type-specific icons for Azure resource types and support resource sync from Azure.
- **FR-034**: Environment detail Drift tab MUST show drift summary cards (Missing, Extra, Config Changes, Auto-Remediable) and individual drift items with expected vs actual values.

#### Compliance Pages

- **FR-035**: Compliance dashboard MUST display overall score (color-coded), framework progress bars, top violations, and per-environment compliance status table.
- **FR-036**: Compliance dashboard MUST support scanning all environments or a selected environment, with automatic data reload after a 2-second delay.
- **FR-037**: Environment compliance detail page MUST show filterable control compliance (Compliant/Non-Compliant), expandable remediation guidance, and resource compliance table.

#### Drift & Health Pages

- **FR-038**: Drift detection page MUST list all environments with drift status, support bulk scan (parallel), and per-environment scan with individual spinners tracked via a `HashSet<string>`.
- **FR-039**: Health status page MUST display Healthy/Degraded/Unhealthy summary cards and per-environment health check results with individual check buttons.

#### Settings Page

- **FR-040**: Settings page MUST provide six tabs: General, Notifications, Defaults, Display, Agents, Security, with 28 configurable properties (5 General, 5 Notifications, 4 Defaults, 5 Display, 3 Agents, 3 Security — see data-model.md AppSettings).
- **FR-041**: Settings page MUST support "Save Settings" (persist + apply theme + toast) and "Reset to Defaults" (factory reset + save + toast).
- **FR-042**: Theme changes (Dark/Light/Auto) MUST apply immediately via JS interop manipulating `document.body.classList`. When "Auto" is selected, the theme MUST follow the OS/browser `prefers-color-scheme` media query and update live if the OS preference changes mid-session (via a `matchMedia` change listener).

#### Containerization

- **FR-043**: System MUST include a two-stage Dockerfile: SDK publish then nginx:alpine with `wwwroot` output.
- **FR-044**: nginx MUST serve `/health` returning 200, cache `/_framework/` immutably (1 year), support `try_files` fallback for client-side routing, and reverse proxy `/api/` to the Admin API.
- **FR-045**: nginx MUST enable gzip compression for text, CSS, JSON, JS, XML, and WASM content types.

#### UI Framework & Patterns

- **FR-046**: System MUST use Bootstrap 5.3.2 and Font Awesome 6.5.1 loaded from CDN in `index.html`.
- **FR-047**: All pages MUST show a centered loading spinner during initial data load and inline spinners on action buttons during processing.
- **FR-048**: All API errors MUST be shown as toast notifications; success operations MUST show green toasts.
- **FR-049**: Single-item destructive actions (delete, deprecate) MUST use a standard confirmation modal with backdrop overlay. Bulk destructive actions (Purge All Deleted Environments) MUST require a type-to-confirm input where the operator types a confirmation phrase (e.g., "PURGE ALL") before the action button is enabled.
- **FR-050**: List views MUST show friendly empty states with icon, message, and action button.
- **FR-051**: Detail and create pages MUST include breadcrumb navigation.
- **FR-052**: All list views MUST implement client-side pagination: load the full dataset from the API, display a configurable page size (default 10), and render page navigation controls (previous/next, page numbers).
- **FR-053**: When an API call fails during page load, the error/empty state MUST include a "Retry" button that re-invokes the page's data loading logic without requiring a full browser refresh.
- **FR-054**: All forms (create template, create environment, edit template, settings) MUST validate required fields on blur (when the field loses focus), display inline error messages beneath invalid fields, and disable the submit button until all validation errors are resolved.

### Key Entities

- **TemplateSummaryDto / TemplateDetailDto**: Service template catalog entries with IaC content, parameters, guardrails, Git source metadata, approval state, and compliance framework associations. Templates have a status lifecycle: Draft → PendingApproval → Published → Deprecated → Archived.
- **EnvironmentSummaryDto / EnvironmentDetailDto**: Provisioned cloud environments linked to templates, with Azure resource details (subscription, resource group, location), lifecycle state, cost estimates, drift status, and expiration policies.
- **ComplianceSummaryDto / EnvironmentComplianceDto**: Compliance posture data with framework scores, control compliance, resource compliance, violations, and remediation guidance.
- **DriftItemDto / DriftDetectionResultDto**: Configuration drift entries with resource identification, property paths, expected vs actual values, severity, and auto-remediation capability.
- **ResourceDto**: Azure resources deployed as part of an environment, with type, location, provisioning state, and Azure Portal URL.
- **AppSettings**: Browser-persisted application settings covering general, notifications, defaults, display, agents, and security preferences (28 properties).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Operators can view the platform dashboard within 3 seconds of page load, showing live summary data from the Admin API.
- **SC-002**: Operators can create a new template (either paste or Git import) and have it appear in the catalog within 30 seconds of form submission.
- **SC-003**: Operators can provision a new environment from a published template in under 2 minutes, including parameter configuration and Azure resource details.
- **SC-004**: Operators can detect drift across all environments and view results within 30 seconds of clicking "Scan All".
- **SC-005**: The compliance dashboard displays framework scores and violations accurately, with per-environment drill-down available in one click.
- **SC-006**: Theme changes (Dark/Light/Auto) apply instantly without page reload.
- **SC-007**: Settings persist across browser sessions via localStorage with 100% fidelity.
- **SC-008**: The application handles Admin API unavailability gracefully — all pages load with empty states and error toasts, with zero unhandled exceptions.
- **SC-009**: The Docker image builds successfully and the containerized application serves the SPA with correct caching headers, gzip compression, API proxying, and client-side routing fallback.
- **SC-010**: All navigation links in the sidebar correctly route to their target pages with active link highlighting and dynamic page title updates.

## Assumptions

- The Admin API (feature 003) is fully implemented and accessible at `http://localhost:5050` during development.
- Bootstrap 5.3.2 and Font Awesome 6.5.1 CDN URLs are stable and accessible.
- Browser localStorage is available and has sufficient capacity for settings persistence (~2KB).
- The application is used by authenticated operators; the "Sign Out" link in the admin dropdown is a placeholder for future auth integration.
- Git import/sync functionality depends on the Admin API's Git integration being operational.
- Cost estimates displayed are informational/estimated values provided by the Admin API, not real-time Azure billing data.
- The application runs as a pure client-side SPA with no server-side rendering or Blazor Server components.
- The nginx reverse proxy configuration assumes the Admin API container is named `platform-admin-api` on the same Docker network.
- Blazored component libraries (Toast 4.2.1, Modal 7.3.1, LocalStorage 4.5.0) are compatible with .NET 9.0.
- All API response models use camelCase JSON serialization matching the Admin API's output format.
