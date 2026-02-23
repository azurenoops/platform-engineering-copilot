# Platform Engineering Copilot - Database Architecture

## Overview

The Platform Engineering Copilot uses Entity Framework Core 9.0 with **SQL Server** (Azure SQL Edge in containers, Azure SQL in production) as the primary database. The database stores infrastructure templates, deployment tracking, compliance assessments, audit logs, and agent configurations.

## Database Contexts

The platform uses two separate database contexts:

| Context | Purpose | Project |
|---------|---------|---------|
| `PlatformEngineeringCopilotContext` | Main platform database - templates, deployments, compliance, audit | `Platform.Engineering.Copilot.Core` |
| `ChatDbContext` | Chat conversation history and attachments | `Platform.Engineering.Copilot.Chat` |

---

## PlatformEngineeringCopilotContext Schema

### Two Template Systems - Critical Architecture

The platform has **two distinct template systems** that serve different purposes:

#### 1. Infrastructure Templates (AI-Generated, Temporary)

| Entity | Table | Purpose |
|--------|-------|---------|
| `InfrastructureTemplate` | `InfrastructureTemplates` | AI-generated Bicep/ARM/Terraform templates |
| `TemplateVersion` | `TemplateVersions` | Version history for IaC templates |
| `TemplateFile` | `TemplateFiles` | Individual files within multi-file templates |

**Characteristics:**
- **Temporary** - 30-minute default expiry, auto-cleaned
- Created by Infrastructure Agent from natural language requests
- Example: "Create a storage account with private endpoints"
- Flow: User request → AI generates → Store temporarily → User deploys → Expires

#### 2. Service Templates (Pre-Approved, Permanent)

| Entity | Table | Purpose |
|--------|-------|---------|
| `ServiceTemplateEntity` | `ServiceTemplates` | Pre-approved infrastructure catalog |
| `ServiceTemplateAuditEntity` | `ServiceTemplateAuditLog` | Template change history |
| `ProvisionedEnvironmentEntity` | `ProvisionedEnvironments` | Deployed instances from templates |
| `DeployedResourceEntity` | `DeployedResources` | Azure resources within environments |
| `DriftItemEntity` | `DriftItems` | Configuration drift detection |
| `EnvironmentActivityEntity` | `EnvironmentActivities` | Activity logs for environments |
| `EnvironmentAuditEntity` | `EnvironmentAuditLog` | Audit trail for environment changes |

**Characteristics:**
- **Permanent** - Versioned catalog with approval workflow
- Status lifecycle: `Draft` → `PendingApproval` → `Published` → `Deprecated` → `Archived`
- Git sync support for pulling from approved repositories
- Guardrails and compliance enforcement
- Drift detection against template definitions

---

### Entity Reference

#### ServiceTemplateEntity (`ServiceTemplates`)

Pre-approved infrastructure patterns for self-service provisioning.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | uniqueidentifier | Primary key |
| `Name` | nvarchar(100) | Unique template name |
| `DisplayName` | nvarchar(200) | Human-readable name |
| `Description` | nvarchar(2000) | Template description |
| `Version` | nvarchar(20) | Semantic version (e.g., "1.0.0") |
| `Category` | nvarchar(50) | Category (Compute, Storage, Networking, etc.) |
| `Format` | nvarchar(20) | Template format (Bicep, ARM, Terraform, Pulumi) |
| `MainTemplateContent` | nvarchar(max) | Main template file content |
| `AdditionalFilesJson` | nvarchar(max) | JSON array of additional files |
| `GitRepositoryUrl` | nvarchar(max) | Git source URL |
| `GitBranch` | nvarchar(max) | Git branch |
| `GitPath` | nvarchar(max) | Path within repository |
| `GitAutoSync` | bit | Auto-sync enabled flag |
| `GitSyncIntervalMinutes` | int | Sync interval (default: 15) |
| `ParametersJson` | nvarchar(max) | JSON array of template parameters |
| `GuardrailsJson` | nvarchar(max) | JSON array of guardrails |
| `Status` | nvarchar(20) | Lifecycle status |
| `RequiresApproval` | bit | Approval workflow required |
| `ComplianceFrameworks` | nvarchar(500) | Comma-separated frameworks |
| `CreatedAt` | datetime2 | Creation timestamp |
| `CreatedBy` | nvarchar(200) | Creator identity |

#### ProvisionedEnvironmentEntity (`ProvisionedEnvironments`)

Deployed Azure environments from service templates.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | uniqueidentifier | Primary key |
| `Name` | nvarchar(100) | Environment name |
| `TemplateId` | uniqueidentifier | FK to ServiceTemplates |
| `SubscriptionId` | nvarchar(50) | Azure subscription |
| `ResourceGroup` | nvarchar(100) | Azure resource group |
| `Location` | nvarchar(50) | Azure region |
| `Status` | nvarchar(20) | Provisioning, Running, Failed, etc. |
| `ParameterValuesJson` | nvarchar(max) | Deployment parameter values |
| `DeployedResourcesJson` | nvarchar(max) | List of deployed resources |
| `HasDrift` | bit | Drift detection flag |
| `DriftCount` | int | Number of drift items |
| `OwnerEmail` | nvarchar(200) | Environment owner |
| `ExpiresAt` | datetime2 | Auto-deletion date |
| `AutoDelete` | bit | Auto-delete on expiry |
| `IsDeleted` | bit | Soft-delete flag |

#### InfrastructureDeployment (`InfrastructureDeployments`)

Deployments of AI-generated templates to Azure.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | uniqueidentifier | Primary key |
| `Name` | nvarchar(100) | Deployment name |
| `TemplateId` | uniqueidentifier | FK to InfrastructureTemplates |
| `SubscriptionId` | nvarchar(100) | Azure subscription |
| `ResourceGroupName` | nvarchar(100) | Target resource group |
| `Location` | nvarchar(50) | Azure region |
| `Status` | int | DeploymentStatus enum |
| `Configuration` | nvarchar(max) | JSON deployment config |
| `Parameters` | nvarchar(max) | JSON deployment parameters |
| `DeployedBy` | nvarchar(100) | Deployer identity |
| `IsPollingActive` | bit | Active polling flag |
| `ProgressPercentage` | int | Deployment progress |
| `EstimatedMonthlyCost` | decimal(10,2) | Cost estimate |

#### ComplianceAssessment (`ComplianceAssessments`)

NIST 800-53 compliance scan results.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | nvarchar(100) | Primary key |
| `SubscriptionId` | nvarchar(100) | Azure subscription |
| `AssessmentType` | nvarchar(50) | NIST-800-53, FedRAMP, etc. |
| `Status` | nvarchar(20) | InProgress, Completed, Failed |
| `ComplianceScore` | decimal(5,2) | Compliance percentage |
| `TotalFindings` | int | Total findings count |
| `CriticalFindings` | int | Critical severity count |
| `HighFindings` | int | High severity count |
| `Results` | nvarchar(max) | JSON detailed results |
| `Recommendations` | nvarchar(max) | JSON remediation guidance |

#### AuditLogEntity (`AuditLogs`)

NIST 800-53 compliant audit trail (AU-2, AU-3, AU-9).

| Column | Type | Description |
|--------|------|-------------|
| `EntryId` | nvarchar(50) | Primary key |
| `Timestamp` | datetimeoffset | Event timestamp |
| `EventType` | nvarchar(100) | Event type |
| `Severity` | int | 0=Info, 4=Critical |
| `ActorId` | nvarchar(200) | Actor identity |
| `ResourceId` | nvarchar(500) | Affected resource |
| `Action` | nvarchar(100) | Action performed |
| `Result` | nvarchar(50) | Success, Failed, Partial |
| `CorrelationId` | nvarchar(100) | Request correlation |
| `ChangeDetailsJson` | nvarchar(max) | Before/after changes |
| `IsArchived` | bit | Archive flag |
| `RowVersion` | rowversion | Concurrency token |

#### ApprovalWorkflowEntity (`ApprovalWorkflows`)

Infrastructure provisioning approval requests.

| Column | Type | Description |
|--------|------|-------------|
| `Id` | nvarchar(100) | Primary key |
| `ToolCallId` | nvarchar(100) | MCP tool call ID |
| `Status` | nvarchar(50) | Pending, Approved, Rejected |
| `Priority` | int | Request priority |
| `ResourceType` | nvarchar(100) | Resource type |
| `ResourceName` | nvarchar(200) | Resource name |
| `Environment` | nvarchar(50) | Target environment |
| `RequestedBy` | nvarchar(200) | Requester identity |
| `ExpiresAt` | datetime2 | Approval deadline |
| `ApprovedBy` | nvarchar(200) | Approver identity |
| `PolicyViolationsJson` | nvarchar(max) | Policy violations |

#### AgentConfiguration (`AgentConfigurations`)

Runtime configuration for AI agents.

| Column | Type | Description |
|--------|------|-------------|
| `AgentConfigurationId` | int | Primary key (identity) |
| `AgentName` | nvarchar(100) | Unique agent name |
| `DisplayName` | nvarchar(200) | UI display name |
| `IsEnabled` | bit | Agent enabled flag |
| `Category` | nvarchar(50) | Agent category |
| `ConfigurationJson` | nvarchar(max) | Agent-specific config |
| `HealthStatus` | nvarchar(50) | Healthy, Unhealthy, Unknown |

---

## ChatDbContext Schema

Stores chat conversation history for the web UI.

| Entity | Table | Purpose |
|--------|-------|---------|
| `Conversation` | `Conversations` | Chat sessions |
| `ChatMessage` | `Messages` | Individual messages |
| `ConversationContext` | `Contexts` | Conversation context/memory |
| `MessageAttachment` | `Attachments` | File attachments |

---

## Connection Configuration

### Docker Compose (Development)

```yaml
# docker-compose.mcp-chat-admin.yml
services:
  pec-sqlserver:
    image: mcr.microsoft.com/azure-sql-edge
    environment:
      ACCEPT_EULA: "1"
      MSSQL_SA_PASSWORD: "SupervisorDB123!"
    volumes:
      - sqlserver-data:/var/opt/mssql
    ports:
      - "1433:1433"
```

### Connection String

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=pec-sqlserver,1433;Database=PlatformDb;User Id=sa;Password=SupervisorDB123!;TrustServerCertificate=True;MultipleActiveResultSets=True;Encrypt=True;"
  },
  "DatabaseProvider": "SqlServer"
}
```

### Environment Variables

| Variable | Description |
|----------|-------------|
| `ConnectionStrings__DefaultConnection` | Database connection string |
| `DatabaseProvider` | `SqlServer` (default) or `Sqlite` |
| `UseInMemoryDatabase` | Set to `true` for testing |

---

## Migrations

### Location

Migrations are stored in:
```
src/Platform.Engineering.Copilot.Core/Data/Migrations/
```

### Design-Time Factory

The `EnvironmentManagementContextFactory` creates the DbContext for EF Core CLI tools:

```csharp
// Defaults to SQL Server for production compatibility
var databaseProvider = configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
```

### Common Commands

```bash
# Navigate to Core project
cd src/Platform.Engineering.Copilot.Core

# Add a new migration
dotnet ef migrations add MigrationName --context PlatformEngineeringCopilotContext --output-dir Data/Migrations

# Update database
dotnet ef database update --context PlatformEngineeringCopilotContext

# Generate SQL script
dotnet ef migrations script --context PlatformEngineeringCopilotContext --output script.sql

# Remove last migration (if not applied)
dotnet ef migrations remove --context PlatformEngineeringCopilotContext
```

### Migration Strategy

| Environment | Strategy |
|-------------|----------|
| Development (Docker) | Auto-migrate on startup via `DatabaseInitializationService` |
| Testing | In-memory database with `EnsureCreated()` |
| Production | Manual migration scripts reviewed before deployment |

---

## Database Initialization

The `DatabaseInitializationService` runs on application startup as an `IHostedService`:

```csharp
// For SQL Server (relational)
await context.Database.MigrateAsync(cancellationToken);

// For In-Memory (testing)
await context.Database.EnsureCreatedAsync(cancellationToken);

// Seed initial data
await DatabaseSeeder.SeedAsync(context);
```

### Seeded Data

The `DatabaseSeeder` populates:

1. **Agent Configurations** - Default agent settings
2. **Service Templates** - Sample templates (webapp-standard, aks-production, etc.)

---

## Key Indexes

### Performance Indexes

| Table | Index | Purpose |
|-------|-------|---------|
| `ServiceTemplates` | `(Name, Version)` UNIQUE | Template lookup |
| `ServiceTemplates` | `(Category, Status)` | Catalog filtering |
| `ProvisionedEnvironments` | `(SubscriptionId, ResourceGroup)` | Azure resource lookup |
| `ProvisionedEnvironments` | `(Status, HasDrift)` | Status monitoring |
| `AuditLogs` | `(Timestamp, Severity)` | Compliance queries |
| `AuditLogs` | `(ActorId, Timestamp)` | User activity audit |
| `ComplianceAssessments` | `(SubscriptionId, AssessmentType)` | Assessment lookup |

### Query Filters

| Table | Filter | Purpose |
|-------|--------|---------|
| `InfrastructureDeployments` | `!IsDeleted` | Soft delete |
| `ProvisionedEnvironments` | `!IsDeleted` | Soft delete |

---

## Relationships

```
ServiceTemplateEntity
    ├── ProvisionedEnvironmentEntity (1:N)
    │       ├── DeployedResourceEntity (1:N)
    │       ├── DriftItemEntity (1:N)
    │       └── EnvironmentActivityEntity (1:N)
    └── ServiceTemplateAuditEntity (1:N)

InfrastructureTemplate
    ├── TemplateVersion (1:N)
    ├── TemplateFile (1:N)
    └── InfrastructureDeployment (1:N)
            └── DeploymentHistory (1:N)

ComplianceAssessment
    └── ComplianceFinding (1:N)
```

---

## Best Practices

### 1. Use Async Operations

```csharp
// Good
var template = await _context.ServiceTemplates
    .FirstOrDefaultAsync(t => t.Id == id);

// Avoid
var template = _context.ServiceTemplates
    .FirstOrDefault(t => t.Id == id);
```

### 2. Track vs. No-Tracking

The context is configured with `NoTracking` by default for performance:

```csharp
options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
```

For updates, explicitly track entities:

```csharp
_context.Entry(entity).State = EntityState.Modified;
```

### 3. Soft Deletes

Use soft deletes for audit compliance:

```csharp
entity.IsDeleted = true;
entity.DeletedAt = DateTime.UtcNow;
entity.DeletedBy = userId;
```

### 4. JSON Columns

Store complex objects as JSON in `nvarchar(max)` columns:

```csharp
public string? ParametersJson { get; set; }

// Serialize
entity.ParametersJson = JsonSerializer.Serialize(parameters);

// Deserialize
var parameters = JsonSerializer.Deserialize<List<TemplateParameter>>(entity.ParametersJson);
```

---

## Troubleshooting

### Common Issues

| Issue | Solution |
|-------|----------|
| Migration fails with "TEXT" type | Entity has SQLite column type annotation. Change to `nvarchar(max)` |
| Connection refused | Ensure SQL Server container is running and healthy |
| Login failed | Check `MSSQL_SA_PASSWORD` environment variable |
| Pending model changes warning | Run `dotnet ef migrations add` or suppress warning |

### Docker Database Reset

```bash
# Stop containers
docker compose -f docker-compose.mcp-chat-admin.yml down

# Remove volume
docker volume rm platform-engineering-copilot_sqlserver-data

# Start fresh
docker compose -f docker-compose.mcp-chat-admin.yml up -d
```

---

## Future Considerations

1. **Read Replicas** - Add read replica support for reporting queries
2. **Partitioning** - Partition AuditLogs by date for performance
3. **Azure SQL** - Production deployment on Azure SQL with geo-replication
4. **Cosmos DB** - Consider Cosmos DB for global distribution of templates
