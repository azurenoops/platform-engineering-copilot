# Data Model: Build Platform Copilot Core

**Branch**: `001-platform-copilot-core` | **Date**: 2026-02-22

## Overview

Two EF Core 9.0 `DbContext` classes with distinct lifecycle and retention policies:

- **`PlatformEngineeringCopilotContext`**: All platform operational entities (compliance, infrastructure, remediation, audit, configuration)
- **`ChatDbContext`**: Conversational history (isolated for independent scaling and purge)

Primary storage: SQL Server (Azure SQL Edge in containers, Azure SQL in production).  
Fallback: SQLite for local development and disconnected scenarios.

---

## Entity Relationship Diagram

```
┌──────────────────┐     ┌────────────────────┐     ┌──────────────────────┐
│      User        │────<│   Configuration     │     │   AgentDefinition    │
│                  │     │                     │     │                      │
│ UserId (PK)      │     │ ConfigId (PK)       │     │ AgentId (PK)         │
│ CacSubjectDN     │     │ UserId (FK)         │     │ AgentName            │
│ DisplayName      │     │ DefaultSubscription │     │ Description          │
│ Email            │     │ CloudEnvironment    │     │ SystemPromptPath     │
│ Roles []         │     │ DefaultFramework    │     │ IsEnabled            │
│ CacExpiry        │     │ PimEligibility {}   │     │ HealthStatus         │
│ PimExpiry        │     │ CreatedAt           │     └──────────┬───────────┘
│ PimTier          │     │ UpdatedAt           │                │
│ IsActive         │     └────────────────────┘                │
└───────┬──────────┘                                    ┌──────┴───────────┐
        │                                               │  ToolDefinition  │
        │                                               │                  │
        │     ┌──────────────────────┐                  │ ToolId (PK)      │
        │     │ ComplianceAssessment │                  │ AgentId (FK)     │
        │────<│                      │                  │ Name             │
        │     │ AssessmentId (PK)    │                  │ Description      │
        │     │ UserId (FK)          │                  │ ParameterSchema  │
        │     │ ScanType             │                  │ RequiresAuth     │
        │     │ Framework            │                  │ PimTierRequired  │
        │     │ SubscriptionId       │                  └──────────────────┘
        │     │ TotalControls        │
        │     │ Passing              │     ┌─────────────────────┐
        │     │ Failing              │────<│  ComplianceFinding  │
        │     │ NotApplicable        │     │                     │
        │     │ StartedAt            │     │ FindingId (PK)      │
        │     │ CompletedAt          │     │ AssessmentId (FK)   │
        │     │ RetentionExpiresAt   │     │ ControlId           │
        │     │ IsDeleted            │     │ ControlFamily       │
        │     └──────────────────────┘     │ ResourceId          │
        │                                  │ ResourceType        │
        │                                  │ Severity            │
        │                                  │ Status              │
        │                                  │ Description         │
        │                                  │ RemediationGuidance │
        │                                  │ CreatedAt           │
        │                                  └─────────┬───────────┘
        │                                            │
        │     ┌──────────────────────┐               │
        │     │  RemediationTask     │<──────────────┘
        │     │                      │
        │     │ TaskId (PK)          │     ┌────────────────────┐
        │     │ FindingId (FK)       │────<│   TaskComment      │
        │     │ BoardId (FK)         │     │                    │
        │     │ DisplayId (REM-###)  │     │ CommentId (PK)     │
        │     │ Title                │     │ TaskId (FK)        │
        │     │ Severity             │     │ UserId (FK)        │
        │     │ AssigneeUserId (FK)  │     │ Content            │
        │     │ Status               │     │ CreatedAt          │
        │     │ DueDate              │     │ UpdatedAt          │
        │     │ SlaHours             │     │ IsDeleted          │
        │     │ IsOverdue            │     └────────────────────┘
        │     │ CreatedAt            │
        │     │ UpdatedAt            │
        │     └──────────────────────┘
        │
        │     ┌──────────────────────┐     ┌──────────────────────┐
        │────<│   EvidencePackage    │     │ ComplianceDocument   │
        │     │                      │     │                      │
        │     │ PackageId (PK)       │     │ DocumentId (PK)      │
        │     │ AssessmentId (FK)    │     │ AssessmentId (FK)    │
        │     │ ControlId            │     │ UserId (FK)          │
        │     │ UserId (FK)          │     │ DocumentType         │
        │     │ Artifacts []         │     │ Framework            │
        │     │ CollectedAt          │     │ Title                │
        │     │ RetentionExpiresAt   │     │ ContentMarkdown      │
        │     │ IsDeleted            │     │ GeneratedAt          │
        │     └──────────────────────┘     │ RetentionExpiresAt   │
        │                                  │ IsDeleted            │
        │                                  └──────────────────────┘
        │
        │     ┌──────────────────────┐
        │────<│   AuditLogEntry      │  ← IMMUTABLE (append-only)
              │                      │
              │ AuditLogId (PK)      │
              │ UserId               │
              │ UserDisplayName      │
              │ Action               │
              │ AgentId              │
              │ ToolName             │
              │ CorrelationId        │
              │ AffectedResources [] │
              │ Outcome              │
              │ PimJustification     │
              │ Details              │
              │ Timestamp            │
              │ RetentionExpiresAt   │
              │ IsArchived           │
              └──────────────────────┘


┌──────────────────────┐     ┌──────────────────────┐
│ RemediationBoard     │     │   Alert              │
│                      │     │                      │
│ BoardId (PK)         │     │ AlertId (PK)         │
│ AssessmentId (FK)    │     │ Severity             │
│ UserId (FK)          │     │ LifecycleState       │
│ Title                │     │ Category             │
│ CreatedAt            │     │ ControlId            │
│ UpdatedAt            │     │ ResourceId           │
└──────────────────────┘     │ ChangeAuthor         │
                             │ Description          │
┌──────────────────────┐     │ RecommendedAction    │
│ IaCTemplate          │     │ GroupingKey           │
│                      │     │ SlaDeadline          │
│ TemplateId (PK)      │     │ AcknowledgedAt       │
│ UserId (FK)          │     │ ResolvedAt           │
│ GenerationMethod     │     │ EscalationCount      │
│ ResourceType         │     │ CreatedAt            │
│ Region               │     │ UpdatedAt            │
│ Framework            │     │ IsArchived           │
│ ContentBicep         │     └──────────────────────┘
│ ContentTerraform     │
│ ComplianceAnnotations│     ┌──────────────────────┐
│ ExpiresAt            │     │ ServiceTemplate      │
│ CreatedAt            │     │                      │
│ IsExpired            │     │ TemplateId (PK)      │
└──────────────────────┘     │ Name                 │
                             │ Description          │
                             │ Category             │
                             │ ContentBicep         │
                             │ GitRepoUrl           │
                             │ GitBranch            │
                             │ GitSyncStatus        │
                             │ Version              │
                             │ IsApproved           │
                             │ ApprovedBy           │
                             │ CreatedAt            │
                             │ UpdatedAt            │
                             └──────────────────────┘
```

---

## Entity Definitions

### PlatformEngineeringCopilotContext Entities

#### User

Represents a platform user with identity derived from CAC certificate.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `UserId` | `Guid` | PK | System-generated |
| `CacSubjectDN` | `string` | Required, Unique, Max 500 | Distinguished Name from CAC certificate |
| `DisplayName` | `string` | Required, Max 200 | |
| `Email` | `string` | Required, Max 320 | |
| `Roles` | `UserRole[]` | Required | Stored as JSON column. Enum: ComplianceOfficer, PlatformEngineer, SecurityLead, Auditor |
| `CacSessionExpiry` | `DateTimeOffset?` | | Current CAC session expiration |
| `PimElevationExpiry` | `DateTimeOffset?` | | Current PIM elevation expiration |
| `PimActiveTier` | `PimTier` | Default: None | Enum: None, Read, Write |
| `IsActive` | `bool` | Default: true | |
| `CreatedAt` | `DateTimeOffset` | Required | |
| `UpdatedAt` | `DateTimeOffset` | Required | |

**Validation Rules**:
- Must have at least one role assigned
- Multi-role: permissions are the union of all assigned roles (FR-017)
- Role derived from CAC identity + directory groups + PIM assignments (FR-018)

---

#### Configuration

Stores per-user platform settings. Detailed contract in [configuration-tools.md](contracts/configuration-tools.md).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `ConfigurationId` | `Guid` | PK | |
| `UserId` | `Guid` | FK → User, Required | |
| `DefaultSubscriptionId` | `string?` | Max 36 | Azure subscription GUID |
| `CloudEnvironment` | `CloudEnvironment` | Default: AzureUSGovernment | Enum: AzureUSGovernment, AzureCloud |
| `DefaultFramework` | `ComplianceFramework` | Default: Nist80053Rev5 | Enum: Nist80053Rev5, FedRampHigh, FedRampModerate, DoDIL5 |
| `Baseline` | `BaselineLevel` | Default: High | Enum: High, Moderate, Low |
| `DefaultScanType` | `ScanType` | Default: Combined | Enum: ResourceBased, PolicyBased, Combined |
| `DefaultRegion` | `string` | Default: "usgovvirginia", Max 50 | Azure region |
| `DryRunDefault` | `bool` | Default: true | Default to dry-run for remediations |
| `PimRoleEligibility` | `string?` | JSON | Cached PIM eligibility: `{ "read": true, "write": false }` |
| `CacCertificateMapping` | `string?` | Max 500 | Mapping between CAC cert and Azure AD identity |
| `CreatedAt` | `DateTimeOffset` | Required | |
| `UpdatedAt` | `DateTimeOffset` | Required | |

**Validation Rules**:
- One Configuration per User (1:1 relationship)
- Missing subscription must produce a clear error when scan is attempted (FR-044)

---

#### ComplianceAssessment

A compliance evaluation result containing scan results and summary scores.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `AssessmentId` | `Guid` | PK | |
| `UserId` | `Guid` | FK → User, Required | Who initiated |
| `ScanType` | `ScanType` | Required | Enum: ResourceBased, PolicyBased, Combined |
| `Framework` | `ComplianceFramework` | Required | |
| `SubscriptionId` | `string` | Required, Max 36 | Target subscription |
| `TotalControls` | `int` | Required | |
| `Passing` | `int` | Required | |
| `Failing` | `int` | Required | |
| `NotApplicable` | `int` | Required | |
| `StartedAt` | `DateTimeOffset` | Required | |
| `CompletedAt` | `DateTimeOffset?` | | Null if still running |
| `DurationSeconds` | `double?` | | Computed on completion |
| `ResourceCount` | `int` | Required | Number of resources scanned |
| `Status` | `AssessmentStatus` | Required | Enum: Running, Completed, Failed, Cancelled |
| `RetentionExpiresAt` | `DateTimeOffset` | Required | Default: CreatedAt + 3 years (FR-072) |
| `IsDeleted` | `bool` | Default: false | Soft-delete for archival |
| `CreatedAt` | `DateTimeOffset` | Required | |

**Validation Rules**:
- `TotalControls` = `Passing` + `Failing` + `NotApplicable`
- Retained for minimum 3 years from `CreatedAt` (FR-072)
- Findings grouped by control family with critical findings first (FR-023)

**State Transitions**: `Running` → `Completed` | `Failed` | `Cancelled`

---

#### ComplianceFinding

A single compliance violation or observation from an assessment.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `FindingId` | `Guid` | PK | |
| `AssessmentId` | `Guid` | FK → ComplianceAssessment, Required | |
| `ControlId` | `string` | Required, Max 20 | e.g., "AC-2", "SC-8" |
| `ControlFamily` | `string` | Required, Max 50 | e.g., "Access Control" |
| `ControlTitle` | `string` | Required, Max 500 | |
| `ResourceId` | `string` | Required, Max 1000 | Azure resource ID |
| `ResourceType` | `string` | Required, Max 200 | e.g., "Microsoft.Storage/storageAccounts" |
| `ResourceName` | `string` | Required, Max 200 | |
| `Severity` | `Severity` | Required | Enum: Critical, High, Medium, Low |
| `Status` | `FindingStatus` | Required | Enum: Failing, Passing, NotApplicable, Error |
| `Description` | `string` | Required, Max 2000 | Plain-language finding description |
| `RemediationGuidance` | `string?` | Max 4000 | How to fix |
| `PolicyDefinitionId` | `string?` | Max 500 | Azure Policy definition if applicable |
| `DefenderRecommendationId` | `string?` | Max 500 | Defender for Cloud reference |
| `CreatedAt` | `DateTimeOffset` | Required | |

---

#### RemediationBoard

A Kanban board created from assessment findings.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `BoardId` | `Guid` | PK | |
| `AssessmentId` | `Guid` | FK → ComplianceAssessment, Required | Source assessment |
| `UserId` | `Guid` | FK → User, Required | Creator |
| `Title` | `string` | Required, Max 200 | |
| `CreatedAt` | `DateTimeOffset` | Required | |
| `UpdatedAt` | `DateTimeOffset` | Required | |

---

#### RemediationTask

A work item on the Kanban board derived from a finding.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `TaskId` | `Guid` | PK | |
| `BoardId` | `Guid` | FK → RemediationBoard, Required | |
| `FindingId` | `Guid` | FK → ComplianceFinding, Required | Source finding |
| `DisplayId` | `string` | Required, Unique, Max 10 | Auto-generated: REM-001, REM-002, etc. |
| `Title` | `string` | Required, Max 500 | Derived from control (FR-051) |
| `Severity` | `Severity` | Required | Mirrors finding severity |
| `AssigneeUserId` | `Guid?` | FK → User | |
| `Status` | `TaskStatus` | Required, Default: Backlog | Enum: Backlog, ToDo, InProgress, InReview, Blocked, Done |
| `DueDate` | `DateTimeOffset` | Required | SLA-based (FR-052) |
| `SlaHours` | `int` | Required | Critical:24, High:168, Medium:720, Low:2160 |
| `IsOverdue` | `bool` | Computed | `DueDate < DateTimeOffset.UtcNow && Status != Done` |
| `BlockedReason` | `string?` | Max 1000 | Required when Status = Blocked (FR-053) |
| `ValidationScanId` | `Guid?` | FK → ComplianceAssessment | Assessment triggered on "Done" |
| `CreatedAt` | `DateTimeOffset` | Required | |
| `UpdatedAt` | `DateTimeOffset` | Required | |

**Validation Rules**:
- Moving to "Blocked" requires a comment (FR-053)
- Moving to "Done" triggers a validation scan (FR-053)
- SLA: Critical 24h, High 7d, Medium 30d, Low 90d (FR-052)

**State Transitions**: `Backlog` → `ToDo` → `InProgress` → `InReview` → `Done` | `Blocked` (from any state except Done)

---

#### TaskComment

Comments on remediation tasks.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `CommentId` | `Guid` | PK | |
| `TaskId` | `Guid` | FK → RemediationTask, Required | |
| `UserId` | `Guid` | FK → User, Required | |
| `Content` | `string` | Required, Max 4000 | |
| `CreatedAt` | `DateTimeOffset` | Required | |
| `UpdatedAt` | `DateTimeOffset?` | | |
| `IsDeleted` | `bool` | Default: false | Owners can delete own; ComplianceOfficers can delete any (FR-054) |

---

#### EvidencePackage

A timestamped, immutable evidence collection for a specific control. Default behavior is **append** (new records created per collection). See [compliance-tools.md](contracts/compliance-tools.md) for evidence deduplication behavior.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `PackageId` | `Guid` | PK | |
| `AssessmentId` | `Guid?` | FK → ComplianceAssessment | Linked assessment if applicable |
| `ControlId` | `string` | Required, Max 20 | Target control |
| `UserId` | `Guid` | FK → User, Required | Collector |
| `SubscriptionId` | `string` | Required, Max 36 | |
| `ConfigExports` | `string?` | JSON | Configuration export data |
| `PolicySnapshots` | `string?` | JSON | Policy assignment snapshots |
| `DefenderRecommendations` | `string?` | JSON | Defender for Cloud recommendations |
| `ActivityLogs` | `string?` | JSON | Azure Activity Log excerpts |
| `ResourceInventory` | `string?` | JSON | Resource inventory data |
| `ContentSizeBytes` | `long` | Required | Total size of artifacts in bytes |
| `CollectedAt` | `DateTimeOffset` | Required | Timestamp of collection |
| `RetentionExpiresAt` | `DateTimeOffset` | Required | Default: CollectedAt + 3 years (FR-072) |
| `IsDeleted` | `bool` | Default: false | |

**Validation Rules**:
- Each collection creates new immutable records (append mode by default per FR-027)
- `replace: true` parameter in tool call deletes prior evidence for the same ControlId+SubscriptionId before inserting
- Response includes `previousEvidenceCount` when existing evidence is present

---

#### ComplianceDocument

Generated compliance documentation (SSP, SAR, POA&M).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `DocumentId` | `Guid` | PK | |
| `AssessmentId` | `Guid?` | FK → ComplianceAssessment | Source assessment |
| `UserId` | `Guid` | FK → User, Required | Generator |
| `DocumentType` | `DocumentType` | Required | Enum: SSP, SAR, POAM |
| `Framework` | `ComplianceFramework` | Required | |
| `Title` | `string` | Required, Max 500 | |
| `ContentMarkdown` | `string` | Required | Full Markdown content |
| `ContentSizeBytes` | `long` | Required | Document size in bytes (max 5MB per compliance-tools.md) |
| `IsTruncated` | `bool` | Default: false | True if content was truncated due to 5MB limit |
| `GeneratedAt` | `DateTimeOffset` | Required | |
| `RetentionExpiresAt` | `DateTimeOffset` | Required | Default: GeneratedAt + 3 years (FR-072) |
| `IsDeleted` | `bool` | Default: false | |

---

#### IaCTemplate

A generated infrastructure-as-code template (temporary, 30-minute TTL).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `TemplateId` | `Guid` | PK | |
| `UserId` | `Guid` | FK → User, Required | Requester |
| `GenerationMethod` | `TemplateMethod` | Required | Enum: TemplateGenerator, AiGenerated, BicepAcr |
| `ResourceType` | `string` | Required, Max 200 | e.g., "AKS Cluster", "Storage Account" |
| `Region` | `string` | Required, Max 50 | e.g., "usgovvirginia" |
| `Framework` | `ComplianceFramework?` | | Compliance framework used for annotations |
| `ContentBicep` | `string?` | | Generated Bicep content |
| `ContentTerraform` | `string?` | | Generated Terraform content |
| `ComplianceAnnotations` | `string?` | JSON | Control mappings: `[{ "property": "...", "controlId": "SC-8", "controlName": "..." }]` |
| `AnnotationCoverage` | `double?` | | Percentage of security properties annotated (SC-009: ≥80%) |
| `ExpiresAt` | `DateTimeOffset` | Required | Default: CreatedAt + 30 minutes |
| `IsExpired` | `bool` | Computed | `ExpiresAt < DateTimeOffset.UtcNow` |
| `CreatedAt` | `DateTimeOffset` | Required | |

---

#### ServiceTemplate

Predefined IaC configurations for common Azure Government workloads (Admin Dashboard).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `TemplateId` | `Guid` | PK | |
| `Name` | `string` | Required, Unique, Max 200 | |
| `Description` | `string` | Required, Max 2000 | |
| `Category` | `string` | Required, Max 100 | e.g., "Compute", "Networking", "Security" |
| `ContentBicep` | `string` | Required | Bicep template content |
| `Parameters` | `string?` | JSON | Template parameter definitions |
| `GitRepoUrl` | `string?` | Max 500 | Git sync source |
| `GitBranch` | `string?` | Max 200 | Default: "main" |
| `GitSyncStatus` | `GitSyncStatus` | Default: NotConfigured | Enum: NotConfigured, Synced, OutOfSync, SyncFailed |
| `GitLastSyncAt` | `DateTimeOffset?` | | |
| `Version` | `string` | Required, Max 20 | Semantic version |
| `IsApproved` | `bool` | Default: false | |
| `ApprovedBy` | `string?` | Max 200 | |
| `ApprovedAt` | `DateTimeOffset?` | | |
| `CreatedAt` | `DateTimeOffset` | Required | |
| `UpdatedAt` | `DateTimeOffset` | Required | |

---

#### Alert

Monitoring alert triggered by compliance drift.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `AlertId` | `Guid` | PK | |
| `Severity` | `Severity` | Required | Critical, High, Medium, Low |
| `LifecycleState` | `AlertState` | Required, Default: New | Enum: New, Acknowledged, InProgress, Resolved, Dismissed |
| `Category` | `DriftCategory` | Required | Enum: BaselineDrift, PolicyDrift, ComplianceStateDrift, SecureScoreDrop |
| `ControlId` | `string?` | Max 20 | Affected NIST control |
| `ResourceId` | `string` | Required, Max 1000 | |
| `ResourceType` | `string` | Required, Max 200 | |
| `ChangeAuthor` | `string?` | Max 200 | Who made the drift-causing change |
| `Description` | `string` | Required, Max 2000 | What changed |
| `RecommendedAction` | `string` | Required, Max 2000 | |
| `GroupingKey` | `string` | Required, Max 200 | For 5-minute grouping (FR-060) |
| `SlaDeadline` | `DateTimeOffset` | Required | Based on severity SLA (FR-059) |
| `AcknowledgedAt` | `DateTimeOffset?` | | |
| `AcknowledgedBy` | `string?` | Max 200 | |
| `ResolvedAt` | `DateTimeOffset?` | | |
| `EscalationCount` | `int` | Default: 0 | |
| `IsArchived` | `bool` | Default: false | |
| `CreatedAt` | `DateTimeOffset` | Required | |
| `UpdatedAt` | `DateTimeOffset` | Required | |

**Validation Rules**:
- SLA: Critical 1h, High 4h, Medium 24h, Low 7d (FR-059)
- Related alerts within 5-minute window grouped by `GroupingKey` (FR-060)
- Auto-escalate if not acknowledged within SLA (FR-061)

**State Transitions**: `New` → `Acknowledged` → `InProgress` → `Resolved` | `Dismissed`

---

#### AuditLogEntry

Immutable record of every agent action. **APPEND-ONLY** — no updates or deletes permitted (FR-073).

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `AuditLogId` | `Guid` | PK | |
| `UserId` | `string` | Required, Max 200 | User identity (may be redacted in logs per FR-078) |
| `UserDisplayName` | `string` | Required, Max 200 | |
| `Action` | `string` | Required, Max 200 | Action performed (FR-066) |
| `AgentId` | `string` | Required, Max 50 | Which agent handled it |
| `ToolName` | `string` | Required, Max 100 | Tool that executed |
| `CorrelationId` | `Guid` | Required | Distributed tracing ID (FR-077) |
| `AffectedResources` | `string?` | JSON | List of Azure resource IDs affected |
| `Outcome` | `AuditOutcome` | Required | Enum: Success, Failure, Denied, Cancelled |
| `PimJustification` | `string?` | Max 1000 | Business justification for PIM elevation (FR-070) |
| `Details` | `string?` | JSON | Additional context |
| `ErrorMessage` | `string?` | Max 2000 | Plain-language error if failed |
| `Timestamp` | `DateTimeOffset` | Required | |
| `RetentionExpiresAt` | `DateTimeOffset` | Required | Default: Timestamp + 7 years (FR-073) |
| `IsArchived` | `bool` | Default: false | Cold storage transition flag |
| `ConcurrencyToken` | `Guid` | Required | For rowversion-less concurrency (SQLite compat) |

**Immutability Enforcement**:
- Repository exposes ONLY `AddAsync()` and query methods
- No `Update` or `Remove` methods
- Production DB: `DENY UPDATE, DELETE ON AuditLogs TO [app_role]`
- Partitioned by year on `Timestamp`

---

#### AgentDefinition

Runtime configuration for each specialized agent.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `AgentId` | `string` | PK, Max 50 | e.g., "compliance", "infrastructure" |
| `AgentName` | `string` | Required, Max 100 | Display name |
| `Description` | `string` | Required, Max 500 | |
| `SystemPromptPath` | `string` | Required, Max 500 | Path to `.prompt.txt` file |
| `IsEnabled` | `bool` | Default: true | |
| `HealthStatus` | `HealthStatus` | Default: Healthy | Enum: Healthy, Degraded, Unavailable |
| `LastHealthCheck` | `DateTimeOffset?` | | |
| `CreatedAt` | `DateTimeOffset` | Required | |
| `UpdatedAt` | `DateTimeOffset` | Required | |

---

#### ToolDefinition

Metadata for tools registered to agents.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `ToolId` | `string` | PK, Max 100 | e.g., "run_compliance_assessment" |
| `AgentId` | `string` | FK → AgentDefinition, Required | |
| `Name` | `string` | Required, Max 100 | |
| `Description` | `string` | Required, Max 500 | |
| `ParameterSchema` | `string` | Required, JSON | JSON Schema for parameters |
| `RequiresAuthentication` | `bool` | Required | FR-010 |
| `PimTierRequired` | `PimTier` | Required, Default: None | Enum: None, Read, Write |
| `IsEnabled` | `bool` | Default: true | |
| `CreatedAt` | `DateTimeOffset` | Required | |

---

### ChatDbContext Entities

#### Conversation

A chat session with context.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `ConversationId` | `Guid` | PK | |
| `UserId` | `Guid` | Required | |
| `Title` | `string?` | Max 200 | Auto-generated from first message |
| `ActiveAgentId` | `string?` | Max 50 | Currently active agent |
| `CreatedAt` | `DateTimeOffset` | Required | |
| `UpdatedAt` | `DateTimeOffset` | Required | |
| `IsArchived` | `bool` | Default: false | |

---

#### ChatMessage

Individual messages in a conversation.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `MessageId` | `Guid` | PK | |
| `ConversationId` | `Guid` | FK → Conversation, Required | |
| `Role` | `MessageRole` | Required | Enum: User, Assistant, System |
| `AgentId` | `string?` | Max 50 | Which agent responded |
| `Content` | `string` | Required | Markdown content |
| `CorrelationId` | `Guid?` | | Links to AuditLogEntry |
| `Timestamp` | `DateTimeOffset` | Required | |

**Validation Rules**:
- Context maintained across at least 10 sequential follow-up messages (SC-006)

---

#### ConversationContext

Cached assessment results and state for multi-turn conversations.

| Field | Type | Constraints | Notes |
|-------|------|-------------|-------|
| `ContextId` | `Guid` | PK | |
| `ConversationId` | `Guid` | FK → Conversation, Required | |
| `Key` | `string` | Required, Max 100 | e.g., "last_assessment_id" |
| `Value` | `string` | Required | JSON-serialized context data |
| `UpdatedAt` | `DateTimeOffset` | Required | |

---

## Non-EF Read-Only Models (NistService)

The following models are **not** EF Core entities. They are loaded once at application startup by `NistService` (`src/Platform.Engineering.Copilot.Core/Services/NistService.cs`) and served in-memory. No database table is required. Both the Compliance Agent and Knowledge Base Agent consume `INistService` via dependency injection (FR-080).

**Data Source Strategy (Dual-Source)**:
1. **Primary**: Fetch the authoritative NIST OSCAL machine-readable catalog (JSON) from the official NIST GitHub repository (`usnistgov/oscal-content`) at startup.
2. **Fallback**: If GitHub is unreachable (air-gapped IL5/IL6 environments, network failure), load from embedded OSCAL JSON snapshot shipped with the application.
3. The OSCAL JSON format is authoritative in both cases — the embedded snapshot is simply a pinned version of the same OSCAL data.
4. `NistService` logs which source was used (GitHub fetch vs. embedded fallback) and the catalog version/date.

### ControlDefinition

A single NIST 800-53 Rev 5 control entry with framework overlay metadata.

| Field | Type | Notes |
|-------|------|-------|
| `ControlId` | `string` | e.g., "AC-2", "AC-2(1)" — includes enhancements |
| `Family` | `string` | Two-letter family code: AC, AT, AU, CA, CM, CP, IA, IR, MA, MP, PE, PL, PM, PS, PT, RA, SA, SC, SI, SR |
| `FamilyName` | `string` | e.g., "Access Control", "System and Communications Protection" |
| `Title` | `string` | Control title from NIST catalog |
| `Description` | `string` | Full control statement text |
| `ImplementationGuidance` | `string?` | Supplemental guidance |
| `Baselines` | `BaselineApplicability` | Which baselines include this control |
| `Frameworks` | `FrameworkApplicability` | Which frameworks include this control |
| `AzureServiceMappings` | `string[]` | Azure services relevant to this control (e.g., "Microsoft.KeyVault/vaults", "Microsoft.Network/networkSecurityGroups") |
| `StigReferences` | `StigReference[]?` | DISA STIG IDs and finding severity when applicable |
| `Priority` | `string?` | P1/P2/P3 priority code from NIST |
| `Related` | `string[]` | Related control IDs (e.g., AC-2 relates to AC-3, AC-6) |

### BaselineApplicability

| Field | Type | Notes |
|-------|------|-------|
| `High` | `bool` | Included in High baseline |
| `Moderate` | `bool` | Included in Moderate baseline |
| `Low` | `bool` | Included in Low baseline |

### FrameworkApplicability

| Field | Type | Notes |
|-------|------|-------|
| `Nist80053Rev5` | `bool` | Always true (source catalog) |
| `FedRampHigh` | `bool` | Included in FedRAMP High overlay |
| `FedRampModerate` | `bool` | Included in FedRAMP Moderate overlay |
| `DoDIL5` | `bool` | Included in DoD IL5 overlay |

### StigReference

| Field | Type | Notes |
|-------|------|-------|
| `StigId` | `string` | DISA STIG identifier |
| `BenchmarkId` | `string` | STIG benchmark, e.g., "Azure_STIG" |
| `Severity` | `string` | CAT I / CAT II / CAT III |

### INistService Interface

```csharp
public interface INistService
{
    ControlDefinition? GetControl(string controlId);
    IReadOnlyList<ControlDefinition> GetControlsByFamily(string familyCode);
    IReadOnlyList<ControlDefinition> SearchControls(string query, int maxResults = 25);
    IReadOnlyList<ControlDefinition> GetControlsByBaseline(BaselineLevel baseline);
    IReadOnlyList<ControlDefinition> GetControlsByFramework(ComplianceFramework framework);
    FrameworkComparisonResult CompareFrameworks(ComplianceFramework a, ComplianceFramework b);
    IReadOnlyList<string> GetFamilyCodes();
    Task RefreshFromGitHubAsync(CancellationToken cancellationToken = default);
    bool IsLoaded { get; }
    NistDataSourceInfo ActiveSource { get; }
}

public record NistDataSourceInfo(
    string Source,           // "GitHub" or "EmbeddedFallback"
    string CatalogVersion,   // e.g., "NIST SP 800-53 Rev 5 — 2024-12-10"
    DateTimeOffset LoadedAt
);
```

### Embedded JSON Data Files

Located at `src/Platform.Engineering.Copilot.Core/Services/NistData/`:

| File | Contents | Source |
|------|----------|--------|
| `nist-800-53-rev5.json` | Full NIST 800-53 Rev 5 catalog (~1,189 controls + enhancements) | NIST OSCAL (`usnistgov/oscal-content`) |
| `fedramp-high-overlay.json` | FedRAMP High baseline overlay mappings | NIST OSCAL / FedRAMP automation repo |
| `fedramp-moderate-overlay.json` | FedRAMP Moderate baseline overlay mappings | NIST OSCAL / FedRAMP automation repo |
| `dod-il5-overlay.json` | DoD IL5 overlay mappings | DoD SRG / DISA |
| `stig-mappings.json` | DISA STIG cross-reference data | DISA STIG Viewer exports |
| `azure-service-mappings.json` | Control → Azure service type mappings | Microsoft compliance documentation |

All files use the OSCAL JSON format as the authoritative schema. Files are embedded resources (`.csproj` `<EmbeddedResource>`). At startup, `NistService` first attempts to fetch current versions from GitHub (`usnistgov/oscal-content`); if unreachable, it falls back to these embedded snapshots. The service logs the active source and catalog version.

---

## Enumerations

| Enum | Values |
|------|--------|
| `UserRole` | ComplianceOfficer, PlatformEngineer, SecurityLead, Auditor |
| `PimTier` | None, Read, Write |
| `CloudEnvironment` | AzureUSGovernment, AzureCloud |
| `ComplianceFramework` | Nist80053Rev5, FedRampHigh, FedRampModerate, DoDIL5 |
| `BaselineLevel` | High, Moderate, Low |
| `ScanType` | ResourceBased, PolicyBased, Combined |
| `AssessmentStatus` | Running, Completed, Failed, Cancelled |
| `Severity` | Critical, High, Medium, Low |
| `FindingStatus` | Failing, Passing, NotApplicable, Error |
| `TaskStatus` | Backlog, ToDo, InProgress, InReview, Blocked, Done |
| `DocumentType` | SSP, SAR, POAM |
| `TemplateMethod` | TemplateGenerator, AiGenerated, BicepAcr |
| `GitSyncStatus` | NotConfigured, Synced, OutOfSync, SyncFailed |
| `DriftCategory` | BaselineDrift, PolicyDrift, ComplianceStateDrift, SecureScoreDrop |
| `AlertState` | New, Acknowledged, InProgress, Resolved, Dismissed |
| `AuditOutcome` | Success, Failure, Denied, Cancelled |
| `HealthStatus` | Healthy, Degraded, Unavailable |
| `MessageRole` | User, Assistant, System |
| `MonitoringAction` | Status, Scan, Alerts, Trend |

---

## Indexes

| Entity | Index | Columns | Type |
|--------|-------|---------|------|
| User | `IX_User_CacSubjectDN` | CacSubjectDN | Unique |
| Configuration | `IX_Configuration_UserId` | UserId | Unique |
| ComplianceAssessment | `IX_Assessment_UserId_CreatedAt` | UserId, CreatedAt DESC | Non-unique |
| ComplianceAssessment | `IX_Assessment_SubscriptionId` | SubscriptionId | Non-unique |
| ComplianceFinding | `IX_Finding_AssessmentId` | AssessmentId | Non-unique |
| ComplianceFinding | `IX_Finding_ControlFamily_Severity` | ControlFamily, Severity | Non-unique |
| RemediationTask | `IX_Task_BoardId_Status` | BoardId, Status | Non-unique |
| RemediationTask | `IX_Task_AssigneeUserId` | AssigneeUserId | Non-unique |
| RemediationTask | `IX_Task_DisplayId` | DisplayId | Unique |
| EvidencePackage | `IX_Evidence_ControlId` | ControlId | Non-unique |
| AuditLogEntry | `IX_Audit_UserId_Timestamp` | UserId, Timestamp DESC | Non-unique |
| AuditLogEntry | `IX_Audit_CorrelationId` | CorrelationId | Non-unique |
| AuditLogEntry | `IX_Audit_Timestamp` | Timestamp | Non-unique (partition key) |
| Alert | `IX_Alert_Severity_State` | Severity, LifecycleState | Non-unique |
| Alert | `IX_Alert_GroupingKey_CreatedAt` | GroupingKey, CreatedAt | Non-unique |
| ChatMessage | `IX_Message_ConversationId_Timestamp` | ConversationId, Timestamp | Non-unique |

---

## Retention Policy Summary

| Data Category | Minimum Retention | Mechanism | FR |
|---------------|-------------------|-----------|-----|
| Compliance Assessments | 3 years | Soft-delete + background archival | FR-072 |
| Evidence Packages | 3 years | Soft-delete + background archival | FR-072 |
| Compliance Documents | 3 years | Soft-delete + background archival | FR-072 |
| Audit Logs | 7 years (immutable) | Append-only, DB-level DENY, partition by year | FR-073 |
| IaC Templates | 30 minutes | Auto-cleanup background service | — |
| Service Templates | Permanent | Versioned, approval workflow | — |
| Chat History | Session-based | No mandated retention; org-configurable | FR-074 |
