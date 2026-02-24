# Feature Specification: Admin API

**Feature Branch**: `003-admin-api`  
**Created**: 2026-02-23  
**Status**: Draft  
**Input**: User description: "Platform Engineering Copilot — Admin API Feature Specification"

## Clarifications

### Session 2026-02-23

- Q: What authentication mechanism should secure the Admin API endpoints? → A: Azure AD / Entra ID JWT bearer tokens (OAuth 2.0)
- Q: Are "admin" and "platform engineer" distinct roles with different API permissions? → A: Two roles: Admin (template management, approval, compliance, purge) and Engineer (environment lifecycle, monitoring, drift)
- Q: What is the soft-delete retention policy before records can be purged? → A: 30-day retention — soft-deleted records are auto-purged after 30 days via background service
- Q: How should the system handle concurrent modifications to the same template? → A: Optimistic concurrency — ETag/row version prevents last-write-wins; concurrent updates return 409 Conflict
- Q: Can environments only be provisioned from Published templates, or from any template status? → A: Published only — environment creation rejects templates not in Published status (400 response)

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Template Catalog CRUD (Priority: P1)

A platform engineering team lead opens the Admin Client UI to curate the organization's infrastructure blueprint library. They create a new Bicep service template ("Secure AKS Cluster"), defining its parameters, guardrails, compliance frameworks, and keywords. They edit the template to refine guardrails, submit it for approval, approve it, and later deprecate an outdated template. They also browse, search, and filter the catalog by category, status, and keyword.

**Why this priority**: Templates are the foundational domain object. Without CRUD and lifecycle management, no environments can be provisioned and the entire system has no value.

**Independent Test**: Can be fully tested by creating, reading, updating, filtering, and deleting templates via REST calls—delivers a working template catalog that the Admin Client can consume.

**Acceptance Scenarios**:

1. **Given** no templates exist, **When** an admin sends POST /api/templates with a valid name, Bicep content, and parameters, **Then** the system returns 201 with the created template ID and a Location header pointing to GET /api/templates/{id}.
2. **Given** a template exists in Draft status, **When** an admin sends GET /api/templates with status=Draft, **Then** the response includes the template in the list with Git sync indicators.
3. **Given** a template exists, **When** an admin sends PUT /api/templates/{id} with updated guardrails, **Then** only the guardrails change; other fields remain unchanged.
4. **Given** a template exists, **When** an admin sends DELETE /api/templates/{id}?deletedBy=admin, **Then** the system returns 204 and subsequent GET returns 404.
5. **Given** a template exists by name "aks-cluster" at version "1.0.0", **When** an admin sends GET /api/templates/by-name/aks-cluster?version=1.0.0, **Then** the correct template is returned.
6. **Given** templates exist across categories, **When** an admin sends GET /api/templates/categories, **Then** a distinct list of category strings is returned.

---

### User Story 2 — Template Approval Workflow (Priority: P1)

An admin user creates a template in Draft status. They submit it for approval, transitioning it to PendingApproval. A reviewer approves it (with source, approver, comments, and optional external approval references), transitioning it to Published. When the template is superseded, an admin deprecates it with a reason.

**Why this priority**: The approval lifecycle gates template quality and ensures only vetted blueprints reach production. It is integral to the template domain and cannot be separated from CRUD.

**Independent Test**: Can be fully tested by creating a template, submitting for approval, approving, and deprecating—all via REST calls—verifying status transitions at each step.

**Acceptance Scenarios**:

1. **Given** a Draft template, **When** POST /api/templates/{id}/submit-for-approval is called, **Then** the template status becomes PendingApproval.
2. **Given** a PendingApproval template, **When** POST /api/templates/{id}/approve is called with an ApprovalRequest body, **Then** the template status becomes Published and approval metadata is stored.
3. **Given** a Published template, **When** POST /api/templates/{id}/deprecate?deprecatedBy=admin&reason=superseded is called, **Then** the template status becomes Deprecated.
4. **Given** a Draft template, **When** POST /api/templates/{id}/approve is called (skipping PendingApproval), **Then** the system returns 400 indicating invalid transition.

---

### User Story 3 — Template Validation and Bicep Parsing (Priority: P2)

An admin pastes raw Bicep/ARM/Terraform content into the Admin UI and clicks "Validate." The system checks name and content are present, then verifies format-specific syntax markers. Separately, the admin clicks "Auto-detect Parameters" and the system extracts parameter definitions from Bicep content (either pasted inline or fetched from a Git repository).

**Why this priority**: Validation and parameter extraction improve template authoring quality and speed, but the catalog is usable without them.

**Independent Test**: Can be fully tested by calling POST /api/templates/validate with known-good and known-bad content, and by calling POST /api/templates/parse-bicep-parameters with sample Bicep to verify extracted parameters.

**Acceptance Scenarios**:

1. **Given** valid Bicep content containing `param` and `resource` keywords, **When** POST /api/templates/validate is called, **Then** the response indicates valid with no errors.
2. **Given** content with empty name, **When** POST /api/templates/validate is called, **Then** the response contains a validation error for the missing name.
3. **Given** ARM content missing $schema, **When** POST /api/templates/validate is called, **Then** the response contains a syntax warning.
4. **Given** raw Bicep content with two parameters, **When** POST /api/templates/parse-bicep-parameters is called, **Then** a list of two TemplateParameterDto objects is returned with name, type, and default values.
5. **Given** a Git repo URL containing a Bicep file, **When** POST /api/templates/parse-bicep-parameters-from-git is called, **Then** the file is fetched and its parameters are extracted.

---

### User Story 4 — Natural Language Template Matching (Priority: P2)

A platform user types a natural language request: "I need a secure AKS cluster with FedRAMP compliance." The system matches this against the template catalog, returning a ranked list of matching templates with scores, reasoning, and suggested parameter values. If an LLM is available, it uses AI-powered matching; otherwise, it falls back to keyword matching. The user can also ask the system to extract parameter values for a specific template or explain why a template matches their request.

**Why this priority**: AI-powered discovery is a high-value differentiator but is not required for basic catalog functionality. The fallback to keyword matching ensures the feature works without LLM infrastructure.

**Independent Test**: Can be tested by calling POST /api/templates/match with a natural language description and verifying scored results. Can also test extract-parameters and explain-match endpoints independently.

**Acceptance Scenarios**:

1. **Given** published templates exist with keywords "aks" and "fedramp," **When** POST /api/templates/match is called with description "secure AKS with FedRAMP," **Then** matching templates are returned with scores ≥ 0.3, sorted by relevance.
2. **Given** the NL matching service is unavailable, **When** POST /api/templates/match is called, **Then** the system returns 503 Service Unavailable.
3. **Given** a template with parameters (nodeCount, vmSize), **When** POST /api/templates/{id}/extract-parameters is called with "I need 5 nodes on D4s VMs," **Then** extracted parameters include nodeCount=5 and vmSize with confidence scores.
4. **Given** a template matches a request, **When** POST /api/templates/{id}/explain-match is called, **Then** a human-readable explanation is returned describing why the template fits.

---

### User Story 5 — Git-Sourced Template Sync (Priority: P2)

An admin connects a service template to a Git repository so that template content, parameters, and modules stay in sync with a source-of-truth repo. Templates can be imported from Git, manually synced, bulk-synced, diffed for pending changes, and have their manually-overridden parameters reset to Git source. A background service periodically polls Git for changes on templates with auto-sync enabled.

**Why this priority**: Git integration enables GitOps workflows and ensures templates stay current with infrastructure code in version control. However, templates can be created and managed entirely without Git.

**Independent Test**: Can be tested by importing a template from a Git URL, syncing it, checking its diff status, and resetting parameters—all verifiable via REST calls and response data.

**Acceptance Scenarios**:

1. **Given** a valid Git repo URL with a Bicep file, **When** POST /api/templates/import-from-git is called, **Then** a new template is created with content from the repo and Git source metadata populated.
2. **Given** a template with a Git source configured, **When** POST /api/templates/{id}/sync is called, **Then** the template content is updated from the latest Git commit and LastSyncedFromGit timestamp is refreshed.
3. **Given** multiple Git-sourced templates, **When** POST /api/templates/sync-all is called, **Then** all templates with Git sources are synced.
4. **Given** a template whose Git source has newer commits, **When** GET /api/templates/{id}/git-status is called, **Then** the response shows HasChanges=true with current and latest commit SHAs.
5. **Given** a template with ParametersOverridden=true, **When** POST /api/templates/{id}/reset-parameters is called, **Then** ParametersOverridden is cleared and parameters are restored from Git.
6. **Given** a template with ParametersOverridden=true and a Git source, **When** POST /api/templates/{id}/sync is called without force, **Then** template content syncs but parameters are preserved.

---

### User Story 6 — Environment Lifecycle Management (Priority: P1)

A platform engineer provisions a new environment from a published template by specifying the template, resource group, subscription, location, and parameters. The system triggers an Azure deployment. The engineer monitors provisioning status (which is polled automatically every 30 seconds). Once running, they can scale, clone, reprovision (if failed), and eventually soft-delete the environment. Admins can view and permanently purge soft-deleted environments.

**Why this priority**: Environment provisioning is the second core domain object and the primary value delivery mechanism—turning templates into live infrastructure. Without this, templates are just documents.

**Independent Test**: Can be fully tested by creating an environment from a template, checking its status, scaling, cloning, deleting, and purging—all via REST calls.

**Acceptance Scenarios**:

1. **Given** a published template, **When** POST /api/environments is called with templateId, environmentName, resourceGroup, and subscriptionId, **Then** the system returns 201 with the environment DTO including a deployment ID.
2. **Given** a Running environment, **When** POST /api/environments/{id}/scale is called with nodeCount=5, **Then** a ScaleResultDto is returned showing old and new values.
3. **Given** a Running environment, **When** POST /api/environments/{id}/clone is called with a new name, **Then** a cloned environment is returned with 201 status.
4. **Given** a Failed environment, **When** POST /api/environments/{id}/reprovision is called, **Then** the deployment is retried.
5. **Given** a Running environment, **When** DELETE /api/environments/{id}?deletedBy=admin is called, **Then** the environment is soft-deleted (204 returned).
6. **Given** soft-deleted environments exist, **When** GET /api/environments/deleted is called, **Then** the soft-deleted environments are listed.
7. **Given** a soft-deleted environment, **When** DELETE /api/environments/{id}/purge is called, **Then** the environment is permanently removed.
8. **Given** multiple soft-deleted environments, **When** DELETE /api/environments/purge-all is called, **Then** all are permanently removed and a count is returned.

---

### User Story 7 — Environment Monitoring and Health (Priority: P2)

A platform engineer views deployed resources for an environment, syncs resource state from Azure via Resource Graph, checks environment health (including drift and cost), reviews activity history, and monitors environments nearing expiration. A dashboard summary provides aggregate counts (healthy, degraded, unhealthy, by status, by template) and total estimated monthly cost.

**Why this priority**: Monitoring and health are critical for operational awareness but depend on environments existing first. They enhance the value of provisioned environments rather than enabling them.

**Independent Test**: Can be tested by provisioning an environment, then calling resources, health, activities, summary, and expiring endpoints to verify structured responses.

**Acceptance Scenarios**:

1. **Given** a Running environment with deployed resources, **When** GET /api/environments/{id}/resources is called, **Then** a list of resources with Azure IDs, types, locations, SKUs, and portal URLs (portal.azure.us) is returned.
2. **Given** a Running environment, **When** POST /api/environments/{id}/sync-resources is called, **Then** resource counts (found and added) are returned.
3. **Given** environments in various states, **When** GET /api/environments/summary is called, **Then** aggregate dashboard data is returned with total, healthy, degraded, unhealthy counts, by-template and by-status breakdowns, and estimated monthly cost.
4. **Given** environments with expiration dates, **When** GET /api/environments/expiring?withinDays=7 is called, **Then** only environments expiring within 7 days are returned.
5. **Given** a Running environment, **When** POST /api/environments/{id}/extend is called with a new expiration date, **Then** the expiration is updated.
6. **Given** activities have been recorded, **When** GET /api/environments/{id}/activities?skip=0&take=10 is called, **Then** a paginated list of activities with HasMore indicator is returned.

---

### User Story 8 — Drift Detection and Remediation (Priority: P2)

A platform engineer suspects an environment has drifted from its desired state. They trigger drift detection, which compares expected vs. actual resource properties. The results show which resources drifted, what property changed, severity, and whether it can be auto-remediated. The engineer can then remediate all drift or specific drift items.

**Why this priority**: Drift detection is a key compliance and reliability feature but depends on environments being provisioned and having deployed resources.

**Independent Test**: Can be tested by calling POST /api/environments/{id}/detect-drift and POST /api/environments/{id}/remediate-drift and verifying the response structure (drift items, remediation counts).

**Acceptance Scenarios**:

1. **Given** a Running environment with deployed resources, **When** POST /api/environments/{id}/detect-drift is called, **Then** a DriftDetectionResultDto is returned with drift items listing resource, property path, expected vs. actual, severity, and auto-remediation eligibility.
2. **Given** drift has been detected, **When** POST /api/environments/{id}/remediate-drift is called with specific drift item IDs, **Then** only the specified items are remediated and counts (remediated, failed, remaining) are returned.
3. **Given** drift has been detected, **When** POST /api/environments/{id}/remediate-drift is called without specifying IDs, **Then** all drift items are remediated.

---

### User Story 9 — Deployment Status Management (Priority: P2)

A platform engineer monitors deployment progress. A background service automatically polls Azure every 30 seconds for environments in Provisioning state, updating their status as deployments complete (or fail). An admin can also manually refresh a single environment's status or bulk-refresh all provisioning environments. For recovery scenarios, an admin can manually override an environment's status.

**Why this priority**: Deployment status tracking completes the provisioning lifecycle but is supplementary to the core create/delete flow.

**Independent Test**: Can be tested by calling status refresh endpoints and verifying previous/current status and the StatusChanged flag.

**Acceptance Scenarios**:

1. **Given** an environment in Provisioning state, **When** POST /api/environments/{id}/refresh-status is called, **Then** a RefreshDeploymentStatusResultDto is returned with previous/current status and StatusChanged flag.
2. **Given** multiple environments in Provisioning state, **When** POST /api/environments/refresh-all-provisioning is called, **Then** all provisioning environments are refreshed.
3. **Given** an environment whose Azure deployment completed outside the normal flow, **When** PATCH /api/environments/{id}/status is called with the new status, **Then** the environment status is updated.
4. **Given** the background polling service is running, **When** an environment transitions from Provisioning to Running in Azure, **Then** within 60 seconds the local status is updated automatically.

---

### User Story 10 — Azure Resource Cleanup (Priority: P3)

When an environment is decommissioned, an admin needs to destroy the actual Azure resources. This is separate from soft-deleting the environment record—it actively calls Azure to delete resource groups or individual resources.

**Why this priority**: Resource cleanup is a destructive operation used less frequently than other lifecycle actions. It requires the environment and resource tracking features to be in place first.

**Independent Test**: Can be tested by calling POST /api/environments/{id}/delete-resources and verifying the DeleteResourcesResultDto with lists of deleted and failed resources.

**Acceptance Scenarios**:

1. **Given** a Running environment with Azure resources, **When** POST /api/environments/{id}/delete-resources is called, **Then** a result is returned listing successfully deleted resources and any failures, with counts.

---

### User Story 11 — Compliance Reporting (Stub) (Priority: P3)

An admin views a compliance summary showing overall score, per-framework breakdowns (NIST 800-53, FedRAMP High), per-environment compliance statuses, and top violations. They can trigger a compliance scan for a specific environment or all environments, and drill into per-environment compliance detail with individual control results and remediation guidance.

**Why this priority**: The compliance surface is currently stubbed with mock data. It is designed to be wired into the ComplianceAgent later. Including it now establishes the API contract and UI surface without requiring the agent integration.

**Independent Test**: Can be tested by calling the compliance endpoints and verifying the structure of the mock responses.

**Acceptance Scenarios**:

1. **Given** the compliance controller returns mock data, **When** GET /api/compliance/summary is called, **Then** a ComplianceSummaryDto is returned with overall score, framework scores, environment statuses, and top violations.
2. **Given** an environment ID, **When** POST /api/compliance/scan?environmentId={id} is called, **Then** a 202 Accepted is returned.
3. **Given** an environment ID, **When** GET /api/compliance/environments/{environmentId} is called, **Then** per-framework scores, control results with remediation guidance, and per-resource compliance are returned.

---

### User Story 12 — API Infrastructure (Priority: P1)

The API project is properly configured as an ASP.NET Core 9.0 Web API with Serilog structured logging (console + rolling daily files), Swagger UI in development, CORS with configurable origins, DI registration via a single AddAdminServices extension method, health endpoint, and a multi-stage multi-architecture Dockerfile.

**Why this priority**: The API host infrastructure is necessary before any controller can serve requests. It is the foundation on which all other user stories depend.

**Independent Test**: Can be verified by starting the API, confirming Swagger UI loads in development, health endpoint responds, CORS headers are set, and structured logs appear.

**Acceptance Scenarios**:

1. **Given** the API is started in Development mode, **When** a browser navigates to /swagger, **Then** the Swagger UI loads with all endpoints documented.
2. **Given** the API is running, **When** a request arrives from an origin listed in Cors:AllowedOrigins, **Then** CORS headers allow the request.
3. **Given** the API is running, **When** GET /health is called, **Then** a healthy response is returned.
4. **Given** the API is running, **When** any controller action is called, **Then** structured Serilog logs are written to both console and rolling daily files under logs/.
5. **Given** the API is built with Docker, **When** the multi-stage build completes, **Then** the final image runs on both linux/amd64 and linux/arm64.

---

### Edge Cases

- What happens when a template is created with a Git source that returns a 404 or times out? The system returns 400 with a descriptive error message indicating the Git source is unreachable.
- How does the system handle duplicate template names at the same version? The system rejects with a 400/409 and a clear duplicate error message.
- What happens when environment creation references a template that does not exist? The system returns 400 with "template not found" error.
- What happens when environment creation references a template that is not in Published status (e.g., Draft or Deprecated)? The system returns 400 indicating only Published templates can be used for provisioning.
- What happens when scaling is attempted on an environment in Provisioning or Failed state? The system returns 400 indicating the environment is not in a scalable state.
- How does the system handle concurrent Git syncs on the same template? The system uses optimistic concurrency (ETag/row version); if a sync conflicts with an in-progress update, the conflicting operation returns 409 Conflict.
- What happens when DELETE /api/environments/{id} is called on an already-deleted environment? The system returns 404.
- What happens when approval is attempted on a template not in PendingApproval status? The system returns 400 with an invalid state transition message.
- What happens when drift remediation fails for some items? The response includes both remediated and failed counts with error details per failed item.
- How does the background polling service handle Azure API rate limits or transient failures? It logs warnings and retries on the next polling interval without crashing.
- What happens when the NL matching service is at capacity or unresponsive? The system returns 503 rather than timing out silently.

## Requirements *(mandatory)*

### Functional Requirements

#### Template Catalog

- **FR-001**: System MUST provide a paginated list of templates with optional filters for category, status, keyword search, and pagination via skip/take (default take=50).
- **FR-002**: System MUST return a summary DTO for list operations that includes Git sync status indicators (HasGitSource, GitRepositoryUrl, LastSyncedFromGit, GitAutoSync).
- **FR-003**: System MUST return a full template DTO for single-template retrieval that includes content, parameters with display metadata, guardrails, approval info, compliance frameworks, keywords, use cases, AI selection hints, deployment scope, Git source details, additional files, and ParametersOverridden flag.
- **FR-004**: System MUST support template lookup by ID and by name (with optional version).
- **FR-005**: System MUST accept template creation with required name (3-100 characters) and sensible defaults (version "1.0.0", category "General", format Bicep, status Draft).
- **FR-006**: System MUST support partial updates where only non-null fields are applied.
- **FR-006a**: System MUST use optimistic concurrency control via ETag/row version on template and environment updates. Concurrent modifications to the same record MUST return 409 Conflict with a message indicating the resource was modified since last read.
- **FR-007**: System MUST set ParametersOverridden=true when parameters are explicitly updated, preventing Git sync from overwriting manual edits.
- **FR-008**: System MUST support soft-delete of templates with a deletedBy identifier.
- **FR-009**: System MUST return a distinct list of template categories.

#### Template Approval Lifecycle

- **FR-010**: System MUST support status transitions: Draft → PendingApproval (submit), PendingApproval → Published (approve), Published → Deprecated (deprecate).
- **FR-011**: System MUST reject invalid status transitions with a 400 response.
- **FR-012**: Approval MUST capture source (Internal/External), approver, comments, and optional external approval ID/URL.

#### Template Validation

- **FR-013**: System MUST validate that template name and content are non-empty.
- **FR-014**: System MUST perform format-specific syntax checks: Bicep content should contain `param`/`resource`, ARM content should contain `$schema`, Terraform content should contain `resource`/`provider`.
- **FR-015**: Validation MUST return a structured result with separate errors and warnings.

#### Natural Language Matching

- **FR-016**: System MUST accept a natural language description and return ranked template matches with scores, reasoning, and suggested parameter values.
- **FR-017**: System MUST support configurable minimum score (default 0.3) and maximum results (default 5).
- **FR-018**: System MUST indicate whether LLM or keyword fallback was used (UsedLlm flag).
- **FR-019**: System MUST return 503 when the NL matching service is unavailable.
- **FR-020**: System MUST extract parameter values from natural language for a specific template, with confidence scores and reasoning.
- **FR-021**: System MUST generate human-readable explanations of why a template matches a request.

#### Git Integration

- **FR-022**: System MUST import templates from a Git repository given a repo URL, branch, and file path.
- **FR-023**: System MUST sync individual templates from their configured Git source, with optional force flag.
- **FR-024**: System MUST bulk-sync all templates with Git sources.
- **FR-025**: System MUST report whether a template's Git source has pending changes (HasChanges, current/latest commit SHAs).
- **FR-026**: System MUST reset manually-overridden parameters by clearing the ParametersOverridden flag and force-syncing from Git.
- **FR-027**: When a template is created with a Git source, the system MUST automatically trigger an initial sync.
- **FR-028**: When a template is updated with a new or changed Git source, the system MUST auto-sync and refresh.

#### Git Background Sync

- **FR-029**: System MUST run a background hosted service that periodically polls Git for changes on templates with auto-sync enabled.

#### Environment Lifecycle

- **FR-030**: System MUST provide a paginated list of environments with optional filters for subscriptionId, templateId, status, hasDrift, and pagination via skip/take.
- **FR-031**: Environment list DTOs MUST include drift info, estimated monthly cost, owner email, expiration date, auto-delete flag, tags, parameter values, and deployment scope (auto-detected from resource IDs). The `resourceGroup` field from creation serves as the primary resource group; no separate computed resource groups field is needed.
- **FR-032**: System MUST create environments from a template, triggering an Azure deployment and returning the environment DTO with deployment ID.
- **FR-032a**: System MUST reject environment creation when the referenced template is not in Published status, returning 400 with a message indicating only Published templates can be used for provisioning.
- **FR-033**: Environment creation MUST require templateId, environmentName (3-100 chars), resourceGroup, and subscriptionId; location defaults to "eastus."
- **FR-034**: System MUST support scaling environments with optional nodeCount, replicaCount, SKU, tier, and arbitrary parameters. Scaling MUST only be allowed for environments in Running status; attempts on environments in other states (e.g., Provisioning, Failed, Scaling) MUST return 400.
- **FR-035**: System MUST support cloning an environment into a new named copy.
- **FR-036**: System MUST support soft-delete of environments with deletedBy and optional force flag. When force=false (default), only environments in Running or Failed status may be soft-deleted; other states return 400. When force=true, the state check is bypassed and the environment is immediately soft-deleted regardless of current status.
- **FR-037**: System MUST support reprovisioning (retrying deployment) only for environments in Failed status. Attempts to reprovision environments in other states MUST return 400 with an invalid state transition message.
- **FR-038**: System MUST list soft-deleted environments, permanently purge individual environments, and bulk-purge all soft-deleted environments.
- **FR-038a**: System MUST automatically purge soft-deleted records (templates and environments) older than 30 days via a background service. Manual purge remains available for immediate removal by Admins.

#### Environment Monitoring

- **FR-039**: System MUST return deployed resources for an environment with Azure resource ID, name, type, location, SKU, provisioning state, deploy timestamp, and Azure Government portal URL (portal.azure.us).
- **FR-040**: System MUST sync resources from Azure via Resource Graph, returning counts of resources found and added.
- **FR-041**: System MUST return paginated activity history for an environment with type, description, user info, metadata, timestamp, status, error message, and HasMore indicator.
- **FR-042**: System MUST return environment health including overall status, drift info, estimated cost, issues list, and per-resource health.
- **FR-043**: System MUST return an aggregate status summary with total count, healthy/degraded/unhealthy breakdown, per-status counts, drift count, expiring-within-7-days count, total estimated monthly cost, and by-template/by-status breakdowns.
- **FR-044**: System MUST list environments expiring within a configurable number of days (default 7).
- **FR-045**: System MUST support extending an environment's expiration date.

#### Drift Detection and Remediation

- **FR-046**: System MUST detect drift by comparing expected vs. actual resource properties, returning resource identity, property path, expected/actual values, drift type, severity, and auto-remediation eligibility.
- **FR-047**: System MUST remediate drift for all items or a specified subset, returning counts of remediated, failed, and remaining items.

#### Deployment Status

- **FR-048**: System MUST manually refresh deployment status for a single environment, returning previous/current status and StatusChanged flag.
- **FR-049**: System MUST bulk-refresh all environments in Provisioning state.
- **FR-050**: System MUST support manual status override via PATCH for recovery scenarios. Valid target statuses for manual override are Running, Failed, and Suspended only; other values MUST return 400.
- **FR-051**: System MUST run a background hosted service that polls Azure for deployment status updates every 30 seconds (configurable via DeploymentPolling:IntervalSeconds) with a 10-second initial delay.

#### Azure Resource Cleanup

- **FR-052**: System MUST delete Azure resources for an environment, returning lists of successfully deleted and failed resources with counts.

#### Compliance (Stub)

- **FR-053**: System MUST return a compliance summary with overall score, per-framework scores, per-environment statuses with violation counts, and top violations.
- **FR-054**: System MUST accept compliance scan requests for specific environments or all environments, returning 202 Accepted.
- **FR-055**: System MUST return per-environment compliance detail with framework scores, control results with remediation guidance, and per-resource compliance.
- **FR-056**: All compliance endpoints MUST return mock/hardcoded data with TODO markers indicating future ComplianceAgent integration.

#### API Infrastructure

- **FR-057**: System MUST run as an ASP.NET Core 9.0 Web API on port 5050 by default.
- **FR-058**: System MUST use Serilog for structured logging with console and rolling daily file sinks under logs/.
- **FR-059**: System MUST expose Swagger UI at /swagger in development mode.
- **FR-060**: System MUST configure CORS with wide-open policy in development and configurable Cors:AllowedOrigins (comma-separated) in production, defaulting to localhost ports 5000, 5003, 5200, and 5201.
- **FR-061**: System MUST register all services via a single AddAdminServices extension method, including EF Core context (with in-memory toggle), repositories, deployers, Azure service clients, template catalog service, environment service, activity tracking, NL matching service, and Git sync service.
- **FR-062**: System MUST register GitTemplateSyncBackgroundService, DeploymentStatusPollingBackgroundService, and SoftDeletePurgeBackgroundService as hosted services.
- **FR-063**: System MUST provide a health endpoint.
- **FR-064**: System MUST include a multi-stage, multi-architecture Dockerfile targeting linux/amd64 and linux/arm64 with .NET 9.0 SDK build and ASP.NET runtime images.

#### Environment State Definitions

_Note: FR-071 through FR-075 were added during the clarification phase, accounting for the numbering gap from FR-070._

- **FR-076**: The `Updating` environment status is reserved for future use when in-place configuration updates (without full redeployment) are supported. No API endpoint currently transitions an environment into Updating status.
- **FR-077**: The `Suspended` environment status represents an administratively paused environment. Environments may be placed into Suspended status only via the manual status override endpoint (FR-050, PATCH). Suspended environments cannot be scaled, cloned, or deleted without first being returned to Running status or force-deleted.

#### Authentication & Authorization

- **FR-071**: System MUST authenticate all API requests using Azure AD / Entra ID JWT bearer tokens (OAuth 2.0).
- **FR-072**: System MUST return 401 Unauthorized for requests with missing or invalid tokens.
- **FR-073**: System MUST return 403 Forbidden for requests with valid tokens but insufficient permissions.
- **FR-074**: System MUST enforce two roles: **Admin** (template CRUD, approval workflow, deprecation, compliance, environment purge, bulk operations) and **Engineer** (environment create, scale, clone, reprovision, delete, monitoring, drift, resource sync). Both roles may read templates and environments.
- **FR-075**: Read-only operations (GET endpoints) on templates and environments MUST be accessible to both Admin and Engineer roles.

#### Cross-Cutting

- **FR-065**: Every controller action MUST wrap operations in try/catch with structured Serilog logging.
- **FR-066**: Controllers MUST return typed DTOs, never domain models.
- **FR-067**: Controllers MUST accept CancellationToken on all async operations.
- **FR-068**: Controllers MUST return appropriate HTTP status codes: 201 for creates (with CreatedAtAction), 204 for deletes, 400 for validation failures, 404 for not found, 500 for unexpected errors.
- **FR-069**: Request models MUST use data annotation validation attributes.
- **FR-070**: DTO mapping MUST be performed via private MapToDto methods in each controller.

### Key Entities

**5 persisted database entities:**

- **Service Template**: A reusable infrastructure-as-code blueprint (Bicep, ARM, or Terraform) with parameters, guardrails, compliance framework associations, keywords, AI selection hints, deployment scope, version, approval status, and optional Git source linkage. Templates progress through a lifecycle: Draft → PendingApproval → Published → Deprecated.
- **Provisioned Environment**: A running instance of a service template deployed into an Azure subscription. Tracks resource group, subscription, deployment state, parameters, tags, owner, expiration, estimated cost, drift status, and deployed resources.
- **Deployed Resource**: An Azure resource belonging to a provisioned environment — identified by Azure resource ID, type, location, SKU, and provisioning state.
- **Environment Activity**: An auditable event in an environment's lifecycle — type, description, user, metadata, timestamp, status, and error info.
- **Drift Item**: An individual property discrepancy between expected and actual state of a deployed resource — resource identity, property path, expected/actual values, drift type, severity, and auto-remediation eligibility.

**3 embedded value objects (serialized as JSON columns within ServiceTemplate):**

- **Template Parameter**: Display metadata for a template input — name, display name, description, type, required flag, default value, allowed values, min/max constraints, and display ordering. Stored in `ParametersJson`.
- **Template Guardrail**: A policy rule attached to a template — specifies a type, property, comparison operator, value, enforcement action (Deny or Warn), and error message. Stored in `GuardrailsJson`.
- **Git Source**: Git repository metadata linked to a template — repo URL, branch, file path, commit SHA, auto-sync flag, and sync interval. Stored as individual columns on ServiceTemplate (not a separate table).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: All template CRUD operations complete in under 2 seconds for catalogs of up to 500 templates.
- **SC-002**: Template list with pagination and filters returns results in under 1 second for a catalog of 500 templates.
- **SC-003**: Environment creation request is accepted and returns within 5 seconds (asynchronous deployment is tracked separately).
- **SC-004**: Natural language template matching returns results in under 5 seconds when using keyword fallback, and under 15 seconds when using LLM.
- **SC-005**: Background deployment status polling detects Azure deployment state changes within 60 seconds of occurrence.
- **SC-006**: Drift detection scan completes and returns results in under 30 seconds for environments with up to 50 deployed resources.
- **SC-007**: The API handles 50 concurrent users performing template and environment operations without error rate exceeding 1%.
- **SC-008**: All API responses conform to documented DTO schemas with correct HTTP status codes for success, validation failure, not-found, and server error cases.
- **SC-009**: 100% of controller actions produce structured Serilog log entries for both successful and failed operations.
- **SC-010**: The Swagger UI accurately documents all endpoints, request/response models, and status codes.

## Assumptions

- The existing domain models (ServiceTemplate, ProvisionedEnvironment, etc.) in the Core project are stable and match the DTO contracts described in this specification.
- Azure Government (portal.azure.us, .us endpoints) is the target cloud. Azure commercial is not a primary concern.
- The EF Core in-memory database toggle is sufficient for local development and testing; production uses SQL Server.
- The NL matching service is optional — the system degrades gracefully to keyword matching when no LLM is configured.
- The ComplianceAgent integration is deferred — the compliance controller surface is a stub that returns mock data to establish the API contract.
- Background services (Git sync, deployment polling) are managed by ASP.NET Core's hosted service infrastructure and do not require external schedulers.
- The DeploymentPolling:IntervalSeconds configuration defaults to 30 seconds with a 10-second initial delay.
- Template names are unique within a given version; the system enforces this at the service layer.
- CORS configuration supports multiple Admin Client dev configurations including macOS AirPlay port conflicts (port 5000).
- The API is secured with Azure AD / Entra ID JWT bearer tokens; all endpoints require a valid OAuth 2.0 access token. Development mode may use a bypass or test token for local testing.
- Two authorization roles exist: Admin (template management, approval, compliance, purge) and Engineer (environment lifecycle, monitoring, drift). Both roles have read access to all resources. Role claims are sourced from Azure AD app roles.
- Soft-deleted records (templates and environments) are retained for 30 days before automatic purge. Admins can manually purge before the retention window expires.
- Optimistic concurrency is enforced on template and environment mutations using ETag/row version (EF Core ConcurrencyToken). Clients must include the ETag from their last read; stale writes receive 409 Conflict.
- Only templates in Published status can be used to provision environments. Draft, PendingApproval, and Deprecated templates are blocked at the environment creation endpoint.
