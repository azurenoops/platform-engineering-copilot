# Data Model: Admin Dashboard Client

**Feature**: 004-admin-client  
**Date**: 2026-02-23  
**Source**: Admin API DTOs (feature 003) — client-side mirrors

---

## Overview

The Admin Client defines client-side DTO classes that mirror the Admin API's request/response models. These are pure data transfer objects with no business logic — serialization is handled by `System.Text.Json` with default camelCase naming.

---

## Template Entities

### TemplateSummaryDto

Represents a template in the catalog list view.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| TemplateId | Guid | No | Unique template identifier |
| Name | string | No | Machine-readable name |
| DisplayName | string | Yes | Human-friendly display name |
| Description | string | No | Template description |
| Version | string | No | Semantic version |
| Category | string | No | Template category (e.g., "Compute", "Networking") |
| Format | string | No | IaC format (e.g., "Bicep", "Terraform") |
| Status | string | No | Lifecycle status: Draft, PendingApproval, Published, Deprecated, Archived |
| DeploymentScope | string | Yes | Azure deployment scope |
| HasGitSource | bool | No | Whether template is sourced from Git |
| GitRepositoryUrl | string | Yes | Git repo URL if sourced from Git |
| LastSyncedFromGit | DateTimeOffset | Yes | Last Git sync timestamp |
| GitAutoSync | bool | No | Whether auto-sync is enabled |
| CreatedAt | DateTimeOffset | No | Creation timestamp |
| UpdatedAt | DateTimeOffset | No | Last update timestamp |

### TemplateDetailDto

Extends TemplateSummaryDto with full template content.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| *(all TemplateSummaryDto fields)* | | | |
| Content | string | No | IaC template content (Bicep/Terraform) |
| ParametersJson | string | Yes | JSON-serialized parameter definitions |
| GuardrailsJson | string | Yes | JSON-serialized guardrail definitions |
| ComplianceFrameworks | string | Yes | Comma-separated compliance frameworks |
| Keywords | string | Yes | Comma-separated keywords |
| UseCases | string | Yes | Comma-separated use cases |
| AiSelectionHints | string | Yes | Hints for AI template matching |
| AdditionalFilesJson | string | Yes | JSON-serialized additional files |
| ParametersOverridden | bool | No | Whether parameters were manually overridden |
| RequiresApproval | bool | No | Whether approval workflow is required |
| ApprovalSource | string | Yes | Who initiated approval |
| ApprovedBy | string | Yes | Who approved |
| ApprovedAt | DateTimeOffset | Yes | Approval timestamp |
| ApprovalComments | string | Yes | Approval comments |
| DeprecatedBy | string | Yes | Who deprecated |
| DeprecatedAt | DateTimeOffset | Yes | Deprecation timestamp |
| DeprecationReason | string | Yes | Reason for deprecation |
| GitBranch | string | Yes | Git branch |
| GitPath | string | Yes | File path in Git repo |
| GitSyncIntervalMinutes | int | Yes | Auto-sync interval |
| CreatedBy | string | Yes | Creator identifier |

### TemplateParameterDto

Parsed parameter definition for a template.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Name | string | No | Parameter name |
| DisplayName | string | Yes | Human-friendly name |
| Description | string | Yes | Parameter description |
| Type | string | No | Data type (string, int, bool, object) |
| Required | bool | No | Whether parameter is required |
| DefaultValue | string | Yes | Default value |
| AllowedValues | List\<string\> | No | Allowed values for choice parameters |
| MinValue | string | Yes | Minimum value constraint |
| MaxValue | string | Yes | Maximum value constraint |
| DisplayOrder | int | No | UI display order |

### TemplateGuardrailDto

Policy guardrail definition for a template.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Type | string | No | Guardrail type |
| Property | string | No | Target property |
| Operator | string | No | Comparison operator |
| Value | string | No | Guardrail value |
| Action | string | No | Action on violation (Warn, Block) |
| ErrorMessage | string | No | Error message shown on violation |

### TemplateValidationResultDto

Result of template validation.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| IsValid | bool | No | Whether template is valid |
| Errors | List\<string\> | No | Validation errors |
| Warnings | List\<string\> | No | Validation warnings |

### GitStatusDto

Git sync status for a template.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| HasChanges | bool | No | Whether remote has changes |
| CurrentCommitSha | string | Yes | Current local commit SHA |
| LatestCommitSha | string | Yes | Latest remote commit SHA |
| LastSyncedAt | DateTimeOffset | Yes | Last sync timestamp |

### TemplateMatchResultDto

Result of natural language template matching.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Matches | List\<TemplateMatchDto\> | No | Matching templates ranked by score |
| Query | string | No | Original search description |
| TotalMatches | int | No | Number of matches found |

### TemplateMatchDto

Individual template match entry.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| TemplateId | Guid | No | Matched template ID |
| TemplateName | string | No | Template name |
| DisplayName | string | Yes | Template display name |
| Score | double | No | Match confidence score (0.0–1.0) |
| Reason | string | Yes | Explanation of why this template matched |

---

## Environment Entities

### EnvironmentSummaryDto

Platform-wide environment summary for the dashboard.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| TotalCount | int | No | Total environment count |
| HealthyCount | int | No | Healthy environments |
| DegradedCount | int | No | Degraded environments |
| UnhealthyCount | int | No | Unhealthy environments |
| ByStatus | Dictionary\<string, int\> | No | Count by status |
| DriftCount | int | No | Environments with drift |
| ExpiringWithin7Days | int | No | Environments expiring soon |
| TotalEstimatedMonthlyCost | decimal | No | Total estimated monthly cost |
| ByTemplate | List\<TemplateCountDto\> | No | Count per template |

### TemplateCountDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| TemplateName | string | No | Template name |
| Count | int | No | Environment count |

### EnvironmentDetailDto

Full environment details.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Id | Guid | No | Environment identifier |
| Name | string | No | Environment name |
| DisplayName | string | Yes | Human-friendly name |
| Description | string | Yes | Description |
| TemplateId | Guid | No | Source template ID |
| TemplateName | string | Yes | Source template name |
| SubscriptionId | string | No | Azure subscription ID |
| ResourceGroup | string | No | Azure resource group |
| Location | string | No | Azure region |
| Status | string | No | Lifecycle status |
| StatusMessage | string | Yes | Status details |
| DeploymentId | string | Yes | Azure deployment ID |
| ParameterValuesJson | string | Yes | JSON parameter values |
| DeployedResourcesJson | string | Yes | JSON deployed resources |
| TagsJson | string | Yes | JSON tags |
| HasDrift | bool | No | Whether drift detected |
| DriftCount | int | No | Number of drift items |
| EstimatedMonthlyCost | decimal | Yes | Estimated cost |
| OwnerEmail | string | Yes | Environment owner |
| ExpiresAt | DateTimeOffset | Yes | Expiration date |
| AutoDelete | bool | No | Auto-delete on expiration |
| DeploymentScope | string | Yes | Azure deployment scope |
| RequestedBy | string | Yes | Who requested provisioning |
| CreatedAt | DateTimeOffset | No | Creation timestamp |
| UpdatedAt | DateTimeOffset | No | Last update timestamp |

### ResourceDto

Deployed Azure resource.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Id | Guid | No | Resource record ID |
| AzureResourceId | string | No | Full Azure resource ID |
| Name | string | No | Resource name |
| Type | string | No | Azure resource type (e.g., Microsoft.Compute/virtualMachines) |
| Location | string | Yes | Azure region |
| Sku | string | Yes | Resource SKU |
| ProvisioningState | string | Yes | Provisioning state |
| DeployedAt | DateTimeOffset | Yes | Deployment timestamp |
| PortalUrl | string | Yes | Azure Portal link |
| ResourceGroupName | string | Yes | Resource group name |

### ScaleResultDto

Result of scaling an environment.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| EnvironmentId | Guid | No | Environment ID |
| PreviousScale | string | Yes | Previous scale configuration |
| NewScale | string | Yes | New scale configuration |
| Status | string | No | Scale operation status |
| Message | string | Yes | Status message |

### DeleteResourcesResultDto

Result of deleting Azure resources for an environment.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| DeletedCount | int | No | Successfully deleted resource count |
| FailedCount | int | No | Failed deletions |
| Failures | List\<ResourceFailureDto\> | No | Failure details |

### ResourceFailureDto

Details of a failed resource operation.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| ResourceId | string | No | Azure resource ID |
| ResourceName | string | Yes | Resource name |
| Error | string | No | Error message |

### RefreshDeploymentStatusResultDto

Result of refreshing deployment status from Azure.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| EnvironmentId | Guid | No | Environment ID |
| PreviousStatus | string | Yes | Previous deployment status |
| CurrentStatus | string | No | Current deployment status |
| ResourceCount | int | No | Number of deployed resources |

### ActivityDto

Environment activity log entry.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Id | Guid | No | Activity ID |
| ActivityType | string | No | Type (Created, Scaled, DriftDetected, etc.) |
| Description | string | No | Human-readable description |
| UserId | string | Yes | User who performed action |
| UserName | string | Yes | User display name |
| MetadataJson | string | Yes | Additional metadata |
| Timestamp | DateTimeOffset | No | Activity timestamp |
| Status | string | Yes | Activity status |
| ErrorMessage | string | Yes | Error details |

### ActivityListDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Activities | List\<ActivityDto\> | No | Activity entries |
| HasMore | bool | No | Whether more entries available |

### EnvironmentHealthDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| EnvironmentId | Guid | No | Environment ID |
| OverallStatus | string | No | Health status (Healthy, Degraded, Unhealthy) |
| HasDrift | bool | No | Whether drift exists |
| DriftCount | int | No | Drift item count |
| EstimatedMonthlyCost | decimal | Yes | Cost estimate |
| Issues | List\<string\> | No | Health issues |
| ResourceHealth | List\<ResourceHealthDto\> | No | Per-resource health |

### ResourceHealthDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| ResourceName | string | No | Resource name |
| ResourceType | string | No | Azure resource type |
| Status | string | No | Health status |
| Issues | List\<string\> | No | Resource issues |

---

## Drift Entities

### DriftDetectionResultDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| EnvironmentId | Guid | No | Environment ID |
| DriftItems | List\<DriftItemDto\> | No | Detected drift items |
| TotalDriftCount | int | No | Total count |
| DetectedAt | DateTimeOffset | No | Detection timestamp |

### DriftItemDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Id | Guid | No | Drift item ID |
| ResourceId | string | No | Azure resource ID |
| ResourceName | string | Yes | Resource name |
| ResourceType | string | Yes | Azure resource type |
| PropertyPath | string | No | Drifted property path |
| ExpectedValue | string | Yes | Expected (template) value |
| ActualValue | string | Yes | Actual (Azure) value |
| DriftType | string | Yes | Type: Missing, Extra, ConfigChange |
| Severity | string | No | Severity level |
| CanAutoRemediate | bool | No | Whether auto-remediation is possible |

### RemediateDriftResultDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| RemediatedCount | int | No | Successfully remediated |
| FailedCount | int | No | Failed remediations |
| RemainingCount | int | No | Remaining drift items |
| Failures | List\<DriftFailureDto\> | No | Failure details |

### DriftFailureDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| DriftItemId | Guid | No | Failed drift item ID |
| Error | string | No | Error message |

---

## Compliance Entities

### ComplianceSummaryDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| OverallScore | double | No | Overall compliance score (0-100) |
| FrameworkScores | List\<FrameworkScoreDto\> | No | Per-framework scores |
| EnvironmentStatuses | List\<EnvironmentComplianceStatusDto\> | No | Per-environment compliance |
| TopViolations | List\<ViolationDto\> | No | Top compliance violations |

### FrameworkScoreDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Framework | string | No | Framework name (e.g., NIST 800-53, FedRAMP High) |
| Score | double | No | Framework compliance score (0-100) |

### EnvironmentComplianceStatusDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| EnvironmentId | Guid | No | Environment ID |
| EnvironmentName | string | No | Environment name |
| ComplianceScore | double | No | Environment compliance score |
| ViolationCount | int | No | Number of violations |

### ViolationDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| ControlId | string | No | Control identifier |
| ControlName | string | No | Control name |
| Severity | string | No | Violation severity |
| AffectedEnvironments | int | No | Number of affected environments |

### EnvironmentComplianceDto

Full compliance details for a specific environment.

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| EnvironmentId | Guid | No | Environment ID |
| EnvironmentName | string | No | Environment name |
| OverallScore | double | No | Compliance score |
| FrameworkResults | List\<FrameworkResultDto\> | No | Per-framework results |
| ResourceCompliance | List\<ResourceComplianceDto\> | No | Per-resource compliance |

### FrameworkResultDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| Framework | string | No | Framework name |
| Score | double | No | Framework score |
| Controls | List\<ControlResultDto\> | No | Control-level results |

### ControlResultDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| ControlId | string | No | Control ID |
| ControlName | string | No | Control name |
| Status | string | No | Compliant / Non-Compliant |
| Severity | string | No | Severity level |
| RemediationGuidance | string | Yes | Remediation guidance text |

### ResourceComplianceDto

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| ResourceName | string | No | Resource name |
| ResourceType | string | No | Azure resource type |
| ComplianceScore | double | No | Resource compliance score |
| Violations | int | No | Number of violations |

---

## Application Settings

### AppSettings

Browser-persisted settings model (~40 properties across 6 categories).

| Category | Field | Type | Default | Description |
|----------|-------|------|---------|-------------|
| General | OrganizationName | string | "Platform Engineering" | Organization display name |
| General | DefaultSubscriptionId | string | "" | Default Azure subscription |
| General | DefaultLocation | string | "usgovvirginia" | Default Azure region |
| General | AutoRefreshInterval | int | 30 | Dashboard auto-refresh (seconds) |
| General | PageSize | int | 10 | Default items per page |
| Notifications | EnableToastNotifications | bool | true | Show toast notifications |
| Notifications | ToastDuration | int | 5 | Toast timeout (seconds) |
| Notifications | ShowSuccessToasts | bool | true | Show success toasts |
| Notifications | ShowErrorToasts | bool | true | Show error toasts |
| Notifications | ShowWarningToasts | bool | true | Show warning toasts |
| Defaults | DefaultExpirationDays | int | 90 | Default env expiration |
| Defaults | DefaultAutoDelete | bool | false | Auto-delete on expiration |
| Defaults | DefaultRequiresApproval | bool | true | Require template approval |
| Defaults | DefaultDeploymentScope | string | "ResourceGroup" | Default deployment scope |
| Display | Theme | string | "Auto" | Theme: Dark, Light, Auto |
| Display | SidebarCollapsed | bool | false | Sidebar collapsed state |
| Display | ShowCostEstimates | bool | true | Show cost data |
| Display | DateFormat | string | "relative" | Date display format |
| Display | CompactMode | bool | false | Compact list views |
| Agents | EnableAiMatching | bool | true | Enable AI template matching |
| Agents | AiMatchMinScore | double | 0.3 | Minimum AI match score |
| Agents | AiMatchMaxResults | int | 5 | Max AI match results |
| Security | SessionTimeout | int | 30 | Session timeout (minutes) |
| Security | RequireConfirmation | bool | true | Confirm destructive actions |
| Security | AuditLogEnabled | bool | true | Enable audit logging |

**Persistence**: Serialized as JSON to browser localStorage under key `"platform_engineering_settings"`.

---

## Request DTOs

### Template Requests

| Class | Key Fields | Used By |
|-------|-----------|---------|
| CreateTemplateRequest | Name (req), Content (req), DisplayName, Description, Version, Category, Format, ParametersJson, GuardrailsJson, ComplianceFrameworks, Keywords, GitRepoUrl, GitBranch, GitPath, GitAutoSync | POST /api/templates |
| UpdateTemplateRequest | Same fields, all nullable (partial update) | PUT /api/templates/{id} |
| ApprovalRequest | ApprovalSource (req), ApprovedBy (req), Comments | POST /api/templates/{id}/approve |
| ValidateTemplateRequest | Name, Content, Format | POST /api/templates/validate |
| ParseBicepParametersRequest | BicepContent (req) | POST /api/templates/parse-bicep-parameters |
| ParseBicepFromGitRequest | GitRepoUrl (req), Branch, FilePath | POST /api/templates/parse-bicep-parameters-from-git |
| ImportFromGitRequest | GitRepoUrl (req), Branch, FilePath, Name, Category, GitAutoSync | POST /api/templates/import-from-git |

### Environment Requests

| Class | Key Fields | Used By |
|-------|-----------|---------|
| CreateEnvironmentRequest | TemplateId (req), EnvironmentName (req), ResourceGroup (req), SubscriptionId (req), Location, ParameterValuesJson, TagsJson, OwnerEmail, ExpiresAt, AutoDelete | POST /api/environments |
| ScaleEnvironmentRequest | NodeCount, ReplicaCount, Sku, Tier, AdditionalParameters | POST /api/environments/{id}/scale |
| CloneEnvironmentRequest | NewName (req), DisplayName, ResourceGroup, SubscriptionId | POST /api/environments/{id}/clone |
| ExtendExpirationRequest | NewExpiresAt (req) | POST /api/environments/{id}/extend |
| RemediateDriftRequest | DriftItemIds (List\<Guid\>) | POST /api/environments/{id}/remediate-drift |

---

## State Transitions

### Template Status Lifecycle

```text
Draft → PendingApproval → Published → Deprecated → Archived
                ↑                          |
                └──────────────────────────┘ (can re-submit)
```

- **Draft**: Initial state after creation. Editable.
- **PendingApproval**: Submitted for review. Not editable.
- **Published**: Approved and available for environment provisioning.
- **Deprecated**: Marked as deprecated. Existing environments continue; no new provisioning.
- **Archived**: End of life. Not visible in catalog by default.

### Environment Status Lifecycle

```text
Provisioning → Running → Scaling → Running
                  ↓         ↓
              Failed    Deleting → Deleted → Purged
```

- **Provisioning**: Initial deployment in progress.
- **Running**: Active and healthy.
- **Scaling**: Scale operation in progress.
- **Failed**: Deployment or operation failed.
- **Deleting**: Soft-delete in progress.
- **Deleted**: Soft-deleted (recoverable).
- **Purged**: Permanently removed.
