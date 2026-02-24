# Research: Admin API

**Feature**: 003-admin-api  
**Date**: 2026-02-23  
**Status**: Complete

## Research Topics

### 1. ServiceTemplate Entity Expansion Strategy

- **Decision**: Expand the existing `ServiceTemplate` entity in-place with new scalar columns and JSON string columns (`nvarchar(max)`) for complex nested data (guardrails, parameters, keywords, use cases, AI hints, additional files).
- **Rationale**: DATABASE.md already prescribes this approach — `GuardrailsJson`, `ParametersJson`, `AdditionalFilesJson` as `nvarchar(max)`. The existing entity has 14 properties; expanding to ~30 is straightforward. EF Core 9.0's `ToJson()` owned-entity mapping has SQL Server limitations (can't filter/sort JSON properties in LINQ-to-SQL), and the codebase already uses manual `JsonSerializer` patterns. Consistency wins.
- **Alternatives Considered**:
  - Separate 1:1 entity: Rejected — unnecessary JOIN overhead, no conceptual separation.
  - EF Core `ToJson()` owned entities: Rejected — SQL Server LINQ limitations, inconsistent with existing patterns.

### 2. ProvisionedEnvironment Entity Design

- **Decision**: Use separate tables with FK relationships for DeployedResource, DriftItem, and EnvironmentActivity. Keep `ParameterValuesJson` as a JSON string column on the parent entity.
- **Rationale**: DATABASE.md explicitly defines this ER hierarchy. Query patterns require it: DeployedResources need per-resource queries and portal links; DriftItems are remediated by individual ID; EnvironmentActivities need SQL-level pagination (skip/take with HasMore). JSON arrays would break these patterns. `ParameterValuesJson` stays as JSON because it's always read/written as a single blob.
- **Alternatives Considered**:
  - All JSON columns: Rejected — breaks pagination, prevents drift item remediation by ID.
  - Mix (activities as JSON, resources as table): Rejected — activities are highest-volume and need pagination most.

### 3. EF Core Concurrency Token Strategy

- **Decision**: Use `[Timestamp]` attribute with a `byte[]` property named `RowVersion`, mapped to SQL Server's `rowversion` type. Surface as ETag header using Base64 encoding.
- **Rationale**: SQL Server's `rowversion` is the native automatic concurrency mechanism. EF Core has first-class support — automatic `WHERE` inclusion on `SaveChanges()`, throws `DbUpdateConcurrencyException` on conflicts. The existing codebase uses this pattern (AuditLogEntity in DATABASE.md has `RowVersion | rowversion | Concurrency token`). Controller catches `DbUpdateConcurrencyException` → returns 409 Conflict.
- **Alternatives Considered**:
  - Application-managed Guid/int version: Rejected — manual increment logic is error-prone.
  - `ConcurrencyCheck` on `UpdatedAt`: Rejected — DateTime precision issues cause false positives.

### 4. Soft-Delete Pattern

- **Decision**: Use EF Core global query filters with `HasQueryFilter(e => !e.IsDeleted)`. Use `IgnoreQueryFilters()` for GET /deleted endpoints.
- **Rationale**: Global query filters automatically exclude deleted records from all LINQ queries, preventing accidental exposure. DATABASE.md already specifies this pattern. The 30-day auto-purge background service queries with `IgnoreQueryFilters().Where(t => t.IsDeleted && t.DeletedAt < cutoff)`.
- **Alternatives Considered**:
  - Manual `Where(!e.IsDeleted)` everywhere: Rejected — one missed filter exposes deleted records.
  - Separate "deleted" table: Rejected — doubles schema, requires transactional record moves.

### 5. Background Service Pattern

- **Decision**: Use `BackgroundService` base class with `PeriodicTimer` for polling loops.
- **Rationale**: `BackgroundService` integrates with host lifecycle (graceful shutdown via CancellationToken). `PeriodicTimer` (introduced .NET 6) doesn't drift, respects cancellation, prevents overlapping ticks. Key design: `CreateScope()` for each tick (DbContext is scoped), catch-all except `OperationCanceledException` for resilience, configurable intervals via `IOptions<T>`.
- **Alternatives Considered**:
  - `IHostedService` with `System.Threading.Timer`: Rejected — timer callbacks can overlap without manual SemaphoreSlim.
  - `Task.Delay` loop: Rejected — causes interval drift (delay + execution time).
  - Hangfire/Quartz: Rejected — overkill; spec explicitly states ASP.NET Core hosted services.

### 6. Natural Language Matching — Keyword Fallback

- **Decision**: Use weighted keyword overlap scoring across Name (3.0x), Keywords (2.5x), UseCases (2.0x), ComplianceFrameworks (2.0x), Category (1.5x), and Description (1.0x). Tokenize input, remove stopwords, score exact matches at full weight and substring matches at 0.5x weight. Normalize to 0.0–1.0 range.
- **Rationale**: No external dependencies needed. Simple, fast (<5s for 500 templates), produces meaningful ranked results. Stopwords list removes common query noise ("I need a", "deploy", "setup"). Substring matching catches partial hits ("kubernetes" matching "kube").
- **Alternatives Considered**:
  - Full TF-IDF: Rejected — requires building/maintaining corpus index; marginal benefit at 500 templates.
  - Levenshtein distance: Partially incorporated via substring matching. Full Levenshtein expensive and false-positive-prone on short tokens.
  - Lucene.NET / SQL Server full-text: Rejected — heavy dependency for simple use case.

### 7. Swagger/OpenAPI Configuration

- **Decision**: Use Swashbuckle.AspNetCore for both OpenAPI spec generation and Swagger UI. Remove the built-in `Microsoft.AspNetCore.OpenApi`.
- **Rationale**: The built-in OpenAPI in .NET 9 generates a JSON doc but provides no UI. The spec requires Swagger UI at /swagger (FR-059). Swashbuckle handles everything in one package — spec generation, UI, XML comment rendering, auth scheme display, `[ProducesResponseType]` aggregation. Combining both built-in and Swashbuckle creates dual-config risk with no benefit.
- **Alternatives Considered**:
  - Built-in OpenApi + Swashbuckle UI only: Rejected — dual configuration risk.
  - NSwag: Viable but heavier; no client generation needed. Swashbuckle more widely documented.

### 8. Azure AD JWT Auth with Azure Government

- **Decision**: Use `Microsoft.AspNetCore.Authentication.JwtBearer` with Azure Government-specific configuration. Authority set to `https://login.microsoftonline.us/{TenantId}`, audience to `api://platform-copilot`. Map Azure AD app roles to authorization policies (Admin, Engineer).
- **Rationale**: Azure Government uses different identity endpoints (`.us` vs `.com`). The appsettings already has the correct authority URL. Token validation auto-discovers signing keys from the `.us` OIDC metadata endpoint. Role claims from Azure AD app roles map to `[Authorize(Policy = "Admin")]` attributes. Development bypass via `Authentication:DevBypass` config flag for local testing without Azure AD.
- **Alternatives Considered**:
  - `Microsoft.Identity.Web`: Viable and higher-level. But adds a heavy dependency for what is purely an API (no sign-in flow). Raw JwtBearer is sufficient and more transparent.
  - Custom token validation: Rejected — reinventing the wheel.

### Key Design Constraints

| Constraint | Decision |
|-----------|----------|
| Entity expansion | In-place expansion with JSON string columns |
| Child entities | Separate tables with FK (DeployedResource, DriftItem, EnvironmentActivity) |
| Concurrency | `[Timestamp]` byte[] RowVersion → ETag header (Base64) → 409 Conflict |
| Soft-delete | EF Core global query filters + `IgnoreQueryFilters()` for /deleted |
| Background services | `BackgroundService` + `PeriodicTimer` |
| NL matching fallback | Weighted keyword overlap, 0.0–1.0 normalized scoring |
| OpenAPI/Swagger | Swashbuckle.AspNetCore (replaces built-in) |
| Auth | JwtBearer with Azure Government `.us` endpoints + role policies |
