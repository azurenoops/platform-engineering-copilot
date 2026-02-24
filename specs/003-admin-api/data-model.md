# Data Model: Admin API

**Feature**: 003-admin-api  
**Date**: 2026-02-23  
**Status**: Complete

## Entity Relationship Diagram

```
ServiceTemplate (expand existing)
    │
    ├── 1:N ── ProvisionedEnvironment (new)
    │              ├── 1:N ── DeployedResource (new)
    │              ├── 1:N ── DriftItem (new)
    │              └── 1:N ── EnvironmentActivity (new)
    │
    └── (soft-delete: IsDeleted, DeletedAt, DeletedBy)

ProvisionedEnvironment
    └── (soft-delete: IsDeleted, DeletedAt, DeletedBy)
```

## Entities

### ServiceTemplate (expand existing entity)

**Table**: `ServiceTemplates`  
**File**: `src/Platform.Engineering.Copilot.Core/Data/Entities/ServiceTemplate.cs`  
**Change**: Expand existing entity — add new columns, rename `ContentBicep` → `Content`, replace `IsApproved` with `Status` enum.

| Property | Type | Constraints | Existing? | Notes |
|----------|------|-------------|-----------|-------|
| TemplateId | Guid | PK | Yes | No change |
| Name | string | Required, MaxLength(200), unique with Version | Yes | Add composite unique index (Name, Version). DB allows 200 chars; API request validation enforces 3-100 chars (see FR-005). |
| DisplayName | string? | MaxLength(200) | **New** | Human-friendly label |
| Description | string | Required, MaxLength(2000) | Yes | No change |
| Version | string | Required, MaxLength(20), default "1.0.0" | Yes | Part of composite unique index |
| Category | string | Required, MaxLength(100), default "General" | Yes | Update default |
| Format | TemplateFormat | Required, default Bicep | **New** | Enum: Bicep, ARM, Terraform |
| Content | string | Required | Yes | Rename from ContentBicep; format-agnostic |
| Status | TemplateStatus | Required, default Draft | **New** | Enum: Draft, PendingApproval, Published, Deprecated |
| DeploymentScope | string? | MaxLength(50) | **New** | "resourceGroup" or "subscription" |
| ParametersJson | string? | nvarchar(max) | Yes | Rename from Parameters; List<TemplateParameter> serialized |
| GuardrailsJson | string? | nvarchar(max) | **New** | List<TemplateGuardrail> serialized |
| ComplianceFrameworks | string? | MaxLength(1000) | **New** | Comma-separated: "NIST 800-53,FedRAMP High" |
| Keywords | string? | MaxLength(1000) | **New** | Comma-separated for NL matching |
| UseCases | string? | MaxLength(2000) | **New** | Comma-separated use case descriptions |
| AiSelectionHints | string? | MaxLength(2000) | **New** | Hints for AI template selection |
| AdditionalFilesJson | string? | nvarchar(max) | **New** | Bicep modules synced from Git |
| ParametersOverridden | bool | default false | **New** | True when manually edited; prevents Git sync |
| RequiresApproval | bool | default true | **New** | Whether approval workflow is required |
| ApprovalSource | string? | MaxLength(50) | **New** | "Internal" or "External" |
| ApprovedBy | string? | MaxLength(200) | Yes | No change |
| ApprovedAt | DateTimeOffset? | | Yes | No change |
| ApprovalComments | string? | MaxLength(2000) | **New** | Reviewer comments |
| ExternalApprovalId | string? | MaxLength(200) | **New** | External system reference |
| ExternalApprovalUrl | string? | MaxLength(500) | **New** | Link to external approval |
| DeprecatedBy | string? | MaxLength(200) | **New** | Who deprecated |
| DeprecatedAt | DateTimeOffset? | | **New** | When deprecated |
| DeprecationReason | string? | MaxLength(1000) | **New** | Why deprecated |
| GitRepoUrl | string? | MaxLength(500) | Yes | No change |
| GitBranch | string? | MaxLength(200) | Yes | No change |
| GitPath | string? | MaxLength(500) | **New** | File path within repo |
| GitCommitSha | string? | MaxLength(40) | **New** | Last synced commit SHA |
| GitAutoSync | bool | default false | **New** | Enable automatic sync |
| GitSyncIntervalMinutes | int | default 60, Range(5, 1440) | **New** | Sync frequency |
| GitSyncStatus | GitSyncStatus | default NotConfigured | Yes | No change |
| GitLastSyncAt | DateTimeOffset? | | Yes | Rename from GitLastSyncAt (keep) |
| IsDeleted | bool | default false | **New** | Soft-delete flag |
| DeletedAt | DateTimeOffset? | | **New** | When soft-deleted |
| DeletedBy | string? | MaxLength(200) | **New** | Who soft-deleted |
| CreatedAt | DateTimeOffset | Required | Yes | No change |
| CreatedBy | string? | MaxLength(200) | **New** | Who created |
| UpdatedAt | DateTimeOffset | Required | Yes | No change |
| RowVersion | byte[] | [Timestamp] | **New** | Optimistic concurrency |

**Removed columns**:
- `IsApproved` → Replaced by `Status == Published`
- `ContentBicep` → Renamed to `Content`
- `Parameters` → Renamed to `ParametersJson`

**Indexes**:
- `(Name, Version)` UNIQUE — template name uniqueness within version
- `(Category, Status)` — catalog filtering
- `(IsDeleted)` — support global query filter performance

**Query Filter**: `HasQueryFilter(t => !t.IsDeleted)`

---

### ProvisionedEnvironment (new entity)

**Table**: `ProvisionedEnvironments`  
**File**: `src/Platform.Engineering.Copilot.Core/Data/Entities/ProvisionedEnvironment.cs`

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| Id | Guid | PK | |
| Name | string | Required, MaxLength(200) | Environment name. DB allows 200 chars; API request validation enforces 3-100 chars (see FR-033). |
| DisplayName | string? | MaxLength(200) | Human-friendly label |
| Description | string? | MaxLength(2000) | |
| TemplateId | Guid | FK → ServiceTemplate.TemplateId | |
| TemplateName | string? | MaxLength(200) | Denormalized for display |
| SubscriptionId | string | Required, MaxLength(100) | Azure subscription |
| ResourceGroup | string | Required, MaxLength(200) | Azure resource group |
| Location | string | Required, MaxLength(50), default "eastus" | Azure region |
| Status | EnvironmentStatus | Required, default Provisioning | |
| StatusMessage | string? | MaxLength(1000) | Current status detail |
| DeploymentId | string? | MaxLength(200) | Azure deployment ID |
| ParameterValuesJson | string? | nvarchar(max) | Deployment parameter values |
| DeployedResourcesJson | string? | nvarchar(max) | Denormalized cache for list responses |
| TagsJson | string? | nvarchar(max) | Key-value tags |
| HasDrift | bool | default false | |
| DriftCount | int | default 0 | Count of drift items |
| EstimatedMonthlyCost | decimal? | | Azure cost estimate |
| OwnerEmail | string? | MaxLength(200) | |
| ExpiresAt | DateTimeOffset? | | Expiration date |
| AutoDelete | bool | default false | Auto-delete on expiration |
| DeploymentScope | string? | MaxLength(50) | Auto-detected from resources |
| RequestedBy | string? | MaxLength(200) | Who requested provisioning |
| IsDeleted | bool | default false | Soft-delete flag |
| DeletedAt | DateTimeOffset? | | |
| DeletedBy | string? | MaxLength(200) | |
| CreatedAt | DateTimeOffset | Required | |
| UpdatedAt | DateTimeOffset | Required | |
| RowVersion | byte[] | [Timestamp] | Optimistic concurrency |

**Navigation Properties**:
- `ServiceTemplate Template` — FK navigation
- `ICollection<DeployedResource> DeployedResources`
- `ICollection<DriftItem> DriftItems`
- `ICollection<EnvironmentActivity> Activities`

**Indexes**:
- `(SubscriptionId, ResourceGroup)` — Azure resource lookup
- `(Status, HasDrift)` — status monitoring dashboard
- `(TemplateId)` — FK index
- `(IsDeleted)` — support global query filter performance

**Query Filter**: `HasQueryFilter(e => !e.IsDeleted)`

---

### DeployedResource (new entity)

**Table**: `DeployedResources`  
**File**: `src/Platform.Engineering.Copilot.Core/Data/Entities/DeployedResource.cs`

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| Id | Guid | PK | |
| EnvironmentId | Guid | FK → ProvisionedEnvironment.Id | |
| AzureResourceId | string | Required, MaxLength(500) | Full Azure resource ID |
| Name | string | Required, MaxLength(200) | Resource name |
| Type | string | Required, MaxLength(200) | e.g., "Microsoft.ContainerService/managedClusters" |
| Location | string? | MaxLength(50) | Azure region |
| Sku | string? | MaxLength(100) | |
| ProvisioningState | string? | MaxLength(50) | |
| DeployedAt | DateTimeOffset? | | |
| PortalUrl | string? | MaxLength(500) | Computed: portal.azure.us URL |
| ResourceGroupName | string? | MaxLength(200) | Extracted from AzureResourceId |

**Navigation**: `ProvisionedEnvironment Environment`

---

### DriftItem (new entity)

**Table**: `DriftItems`  
**File**: `src/Platform.Engineering.Copilot.Core/Data/Entities/DriftItem.cs`

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| Id | Guid | PK | |
| EnvironmentId | Guid | FK → ProvisionedEnvironment.Id | |
| ResourceId | string | Required, MaxLength(500) | Azure resource ID |
| ResourceName | string? | MaxLength(200) | |
| ResourceType | string? | MaxLength(200) | |
| PropertyPath | string | Required, MaxLength(500) | e.g., "properties.storageProfile.osDisk.diskSizeGB" |
| ExpectedValue | string? | MaxLength(1000) | |
| ActualValue | string? | MaxLength(1000) | |
| DriftType | string? | MaxLength(100) | e.g., "PropertyChanged", "ResourceAdded", "ResourceRemoved" |
| Severity | DriftSeverity | Required, default Medium | Enum: Low, Medium, High, Critical |
| CanAutoRemediate | bool | default false | |
| IsRemediated | bool | default false | |
| DetectedAt | DateTimeOffset | Required | |
| RemediatedAt | DateTimeOffset? | | |

**Navigation**: `ProvisionedEnvironment Environment`

---

### EnvironmentActivity (new entity)

**Table**: `EnvironmentActivities`  
**File**: `src/Platform.Engineering.Copilot.Core/Data/Entities/EnvironmentActivity.cs`

| Property | Type | Constraints | Notes |
|----------|------|-------------|-------|
| Id | Guid | PK | |
| EnvironmentId | Guid | FK → ProvisionedEnvironment.Id | |
| ActivityType | string | Required, MaxLength(100) | e.g., "Created", "Scaled", "DriftDetected", "Deleted" |
| Description | string | Required, MaxLength(2000) | Human-readable description |
| UserId | string? | MaxLength(200) | Who performed the action |
| UserName | string? | MaxLength(200) | Display name |
| MetadataJson | string? | nvarchar(max) | Arbitrary key-value metadata |
| Timestamp | DateTimeOffset | Required | When it happened |
| Status | string? | MaxLength(50) | "Success", "Failed", "InProgress" |
| ErrorMessage | string? | MaxLength(2000) | If failed |

**Navigation**: `ProvisionedEnvironment Environment`

**Index**: `(EnvironmentId, Timestamp DESC)` — paginated activity queries

---

## Enumerations

### TemplateStatus (new)

**File**: `src/Platform.Engineering.Copilot.Core/Data/Enumerations/TemplateStatus.cs`

```
Draft = 0
PendingApproval = 1
Published = 2
Deprecated = 3
```

### TemplateFormat (new)

**File**: `src/Platform.Engineering.Copilot.Core/Data/Enumerations/TemplateFormat.cs`

```
Bicep = 0
ARM = 1
Terraform = 2
```

### EnvironmentStatus (new)

**File**: `src/Platform.Engineering.Copilot.Core/Data/Enumerations/EnvironmentStatus.cs`

```
Provisioning = 0
Running = 1
Failed = 2
Updating = 3
Scaling = 4
Deleting = 5
Deleted = 6
Suspended = 7
```

### DriftSeverity (new)

**File**: `src/Platform.Engineering.Copilot.Core/Data/Enumerations/DriftSeverity.cs`

```
Low = 0
Medium = 1
High = 2
Critical = 3
```

### GitSyncStatus (existing, no change)

```
NotConfigured = 0
Synced = 1
OutOfSync = 2
SyncFailed = 3
```

---

## DbContext Changes

**File**: `src/Platform.Engineering.Copilot.Core/Data/PlatformEngineeringCopilotContext.cs`

**New DbSets**:
```csharp
public DbSet<ProvisionedEnvironment> ProvisionedEnvironments { get; set; }
public DbSet<DeployedResource> DeployedResources { get; set; }
public DbSet<DriftItem> DriftItems { get; set; }
public DbSet<EnvironmentActivity> EnvironmentActivities { get; set; }
```

**OnModelCreating additions**:
- ServiceTemplate: composite unique index `(Name, Version)`, index on `(Category, Status)`, global query filter `!IsDeleted`, `Status` stored as string, `Format` stored as string, `RowVersion` as `[Timestamp]`
- ProvisionedEnvironment: FK to ServiceTemplate, indexes, global query filter `!IsDeleted`, `Status` stored as string, `RowVersion` as `[Timestamp]`
- DeployedResource: FK to ProvisionedEnvironment with cascade delete
- DriftItem: FK to ProvisionedEnvironment with cascade delete
- EnvironmentActivity: FK to ProvisionedEnvironment with cascade delete, descending index on `(EnvironmentId, Timestamp)`

**Existing ServiceTemplate ModelCreating updates**:
- Change unique index from `Name` alone to `(Name, Version)` composite
- Add `Status` stored as string conversion
- Add `Format` stored as string conversion
- Add global query filter

---

## Validation Rules

| Entity | Field | Rule |
|--------|-------|------|
| ServiceTemplate | Name | Required, 3-200 chars |
| ServiceTemplate | Version | Required, semver pattern |
| ServiceTemplate | Content | Required, non-empty |
| ServiceTemplate | Status transitions | Draft→PendingApproval→Published→Deprecated only |
| ServiceTemplate | Approval | Requires PendingApproval status |
| ProvisionedEnvironment | Name | Required, 3-200 chars |
| ProvisionedEnvironment | SubscriptionId | Required |
| ProvisionedEnvironment | ResourceGroup | Required |
| ProvisionedEnvironment | TemplateId | Must reference Published template |

## State Machines

### Template Lifecycle

```
Draft ──submit──→ PendingApproval ──approve──→ Published ──deprecate──→ Deprecated
```

Invalid transitions return 400.

### Environment Lifecycle

```
Provisioning ──success──→ Running ──scale──→ Scaling ──done──→ Running
     │                       │                                    │
     └──fail──→ Failed ──reprovision──→ Provisioning              │
                                                                  │
Running ──delete──→ Deleting ──done──→ Deleted                    │
Running ──drift──→ Running (HasDrift=true)                        │
Running ──suspend (PATCH)──→ Suspended ──resume (PATCH)──→ Running│
```

**Status vs. Soft-Delete**: The `Status` field tracks the environment's operational state (e.g., Running, Failed, Deleted). The `IsDeleted` flag is a separate soft-delete marker. When an environment is soft-deleted via DELETE, `IsDeleted` is set to true and the record is excluded from normal queries via EF Core global query filters. The `Deleted` status value represents an environment whose Azure resources have been cleaned up, whereas `IsDeleted=true` means the record itself is logically removed.

**Reserved Status**: `Updating` (enum value 3) is reserved for future in-place configuration update support and is not currently used by any API endpoint.
