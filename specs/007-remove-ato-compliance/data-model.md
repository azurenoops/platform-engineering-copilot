# Feature 007 — Data Model (Entity Removal Mapping)

This document maps the compliance entities being removed, their relationships, and the database impact.

## Entities Removed

### ComplianceAssessment

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| SubscriptionId | string | Azure subscription being assessed |
| Framework | ComplianceFramework (enum) | NIST, FedRAMP, etc. |
| Status | AssessmentStatus (enum) | Running, Completed, Failed |
| StartedAt | DateTimeOffset | Assessment start time |
| CompletedAt | DateTimeOffset? | Nullable completion time |
| OverallScore | double | 0.0–1.0 compliance score |
| TotalControls | int | Controls evaluated |
| PassedControls | int | Controls passing |
| CreatedBy | string | Initiating user |

**Relationships**: One-to-many → ComplianceFinding, ComplianceDocument

### ComplianceFinding

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| AssessmentId | Guid | FK → ComplianceAssessment |
| ControlId | string | NIST control ID (e.g., AC-2) |
| ControlFamily | string | Control family name |
| Status | FindingStatus (enum) | Compliant, NonCompliant, NotApplicable |
| Severity | Severity (enum) | **Shared enum — KEEP** |
| Description | string | Finding details |
| RemediationGuidance | string | Suggested fix |
| Evidence | string? | JSON evidence blob |

**Relationships**: Many-to-one → ComplianceAssessment

### ComplianceDocument

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| AssessmentId | Guid | FK → ComplianceAssessment |
| DocumentType | DocumentType (enum) | SSP, SAR, POAM |
| Title | string | Document title |
| Content | string | Generated document content |
| GeneratedAt | DateTimeOffset | Generation timestamp |

**Relationships**: Many-to-one → ComplianceAssessment

### EvidencePackage

| Field | Type | Notes |
|-------|------|-------|
| Id | Guid | PK |
| AssessmentId | Guid | FK → ComplianceAssessment |
| ControlId | string | NIST control ID |
| BlobUrl | string | Azure Blob Storage URL |
| CollectedAt | DateTimeOffset | Collection timestamp |
| Collector | string | Evidence collector class name |

**Relationships**: Many-to-one → ComplianceAssessment

## Enumerations Removed

| Enum | Values | Used By (all removed) |
|------|--------|-----------------------|
| ComplianceFramework | Nist80053Rev5, FedRampHigh, FedRampModerate, DodIl5 | ComplianceAssessment |
| ScanType | Rbac, Encryption, Network, Logging, Policy, MultiPillar | Scanners |
| AssessmentStatus | Running, Completed, Failed, Cancelled | ComplianceAssessment |
| FindingStatus | Compliant, NonCompliant, NotApplicable, Error | ComplianceFinding |
| DocumentType | SSP, SAR, POAM | ComplianceDocument |
| BaselineLevel | Low, Moderate, High | Configuration entity (edit to remove) |

## Enumerations Kept

| Enum | Used By (non-compliance) |
|------|--------------------------|
| Severity | RemediationTask, DriftItem |
| AuditOutcome | AuditLogEntry |
| DriftCategory | Alert |
| DriftSeverity | DriftItem |
| AlertState | Alert |
| HealthStatus | HealthCheckService |
| MonitoringAction | Monitoring tools |

## DbContext Changes

### DbSets Removed

```csharp
// REMOVE from PlatformEngineeringCopilotContext:
public DbSet<ComplianceAssessment> ComplianceAssessments { get; set; }
public DbSet<ComplianceFinding> ComplianceFindings { get; set; }
public DbSet<ComplianceDocument> ComplianceDocuments { get; set; }
public DbSet<EvidencePackage> EvidencePackages { get; set; }
```

### OnModelCreating Configurations Removed

All `entity.HasIndex()`, `entity.Property().HasConversion()`, and relationship configurations for the 4 compliance entities are removed from `OnModelCreating`.

## Database Migration

**Strategy**: Hand-written SQL script (no EF migration infrastructure exists in the project).

**Script**: `specs/007-remove-ato-compliance/scripts/drop-compliance-tables.sql`

```sql
-- Idempotent drop script for ATO compliance tables
-- Feature 007: Remove ATO Compliance Engine
-- Apply AFTER deploying the code changes

IF OBJECT_ID('dbo.EvidencePackages', 'U') IS NOT NULL DROP TABLE dbo.EvidencePackages;
IF OBJECT_ID('dbo.ComplianceDocuments', 'U') IS NOT NULL DROP TABLE dbo.ComplianceDocuments;
IF OBJECT_ID('dbo.ComplianceFindings', 'U') IS NOT NULL DROP TABLE dbo.ComplianceFindings;
IF OBJECT_ID('dbo.ComplianceAssessments', 'U') IS NOT NULL DROP TABLE dbo.ComplianceAssessments;
```

**Order**: Child tables first (EvidencePackages, ComplianceDocuments, ComplianceFindings), then parent (ComplianceAssessments) — respects FK constraints.
