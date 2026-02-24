# Implementation Plan: Admin API

**Branch**: `003-admin-api` | **Date**: 2026-02-23 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-admin-api/spec.md`

## Summary

Build the Admin API — the REST management plane for the Platform Engineering Copilot that manages Service Templates (infrastructure blueprints) and Provisioned Environments (live Azure deployments). The existing project is a scaffold with 6 stub controllers using in-memory static data and a minimal Program.cs. This plan transforms it into a production-grade ASP.NET Core 9.0 Web API with EF Core persistence, service-layer architecture, Azure AD authentication, Serilog observability, background polling services, Git sync, natural language template matching, and comprehensive test coverage.

## Technical Context

**Language/Version**: C# 12 / .NET 9.0  
**Primary Dependencies**: ASP.NET Core 9.0, EF Core 9.0 (SQL Server + InMemory), Serilog.AspNetCore, Swashbuckle.AspNetCore, Microsoft.AspNetCore.Authentication.JwtBearer, Azure.Identity  
**Storage**: SQL Server (production) / EF Core InMemory (dev/test), existing `PlatformEngineeringCopilotContext`  
**Testing**: xUnit 2.9.2, FluentAssertions 7.0.0, Moq 4.20.72, WebApplicationFactory  
**Target Platform**: Linux (Azure Government ACI) + macOS (local dev), multi-arch (amd64/arm64)  
**Project Type**: web-service (REST API)  
**Performance Goals**: Template CRUD < 2s, list < 1s, environment create < 5s, NL match < 5s (keyword) / < 15s (LLM), 50 concurrent users < 1% error rate  
**Constraints**: Azure Government endpoints (.us), NIST 800-53 compliance, portal.azure.us portal URLs  
**Scale/Scope**: 500 templates, 1000 environments, 50 concurrent users

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Details |
|-----------|--------|---------|
| I. Documentation as Source of Truth | PASS | Plan references DATABASE.md entity definitions; new docs will be updated post-implementation per FR-031/docs guidance |
| II. BaseAgent/BaseTool Architecture | N/A | This feature is a REST API; it does not create new agents or tools. ComplianceController stub (FR-056) is designed for future ComplianceAgent wiring but does not implement agent logic. |
| III. Test-First Development | PASS | Every implementation phase includes corresponding unit and integration tests. WebApplicationFactory-based integration tests planned for all controllers. Target: 80%+ coverage. |
| IV. Azure Government & Compliance First | PASS | Portal URLs use portal.azure.us; Azure service clients use DefaultAzureCredential; Authentication uses login.microsoftonline.us authority; Data residency in US Gov regions. |
| V. Observability & Structured Logging | PASS | Serilog configured with console + rolling file sinks (FR-058); all controller actions wrapped in try/catch with structured logging (FR-065); tool/agent execution logging via existing Core infrastructure. |

**Gate Result**: PASS — no violations. Complexity Tracking not needed.

## Project Structure

### Documentation (this feature)

```text
specs/003-admin-api/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (REST API contracts)
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/Platform.Engineering.Copilot.Admin.API/
├── Platform.Engineering.Copilot.Admin.API.csproj   # NuGet deps, project refs
├── Program.cs                                       # Host config, DI, middleware
├── Extensions/
│   └── ServiceCollectionExtensions.cs               # AddAdminServices()
├── Controllers/
│   ├── TemplatesController.cs                       # /api/templates (rewrite)
│   ├── EnvironmentsController.cs                    # /api/environments (rewrite)
│   ├── ComplianceController.cs                      # /api/compliance (new, stub)
│   └── HealthController.cs                          # /health (rewrite)
├── Models/
│   ├── Dtos.cs                                      # Template + Environment DTOs
│   ├── ComplianceDtos.cs                            # Compliance DTOs
│   └── Requests.cs                                  # Request models with validation
├── appsettings.json                                 # Production config
├── appsettings.Development.json                     # Dev config overrides
└── Dockerfile                                       # Multi-stage, multi-arch

src/Platform.Engineering.Copilot.Core/
├── Data/
│   ├── Entities/
│   │   ├── ServiceTemplate.cs                       # Expand existing entity
│   │   ├── ProvisionedEnvironment.cs                # New entity
│   │   ├── DeployedResource.cs                      # New entity
│   │   ├── DriftItem.cs                             # New entity
│   │   └── EnvironmentActivity.cs                   # New entity
│   ├── Enumerations/
│   │   ├── TemplateStatus.cs                        # New enum
│   │   ├── EnvironmentStatus.cs                     # New enum
│   │   ├── TemplateFormat.cs                        # New enum
│   │   └── DriftSeverity.cs                         # New enum
│   └── PlatformEngineeringCopilotContext.cs         # Add DbSets + config
├── Interfaces/
│   ├── IServiceTemplateCatalogService.cs            # New interface
│   ├── IProvisionedEnvironmentService.cs            # New interface
│   ├── ITemplateDeployer.cs                         # New interface
│   ├── IAzureResourceService.cs                     # New interface
│   ├── INaturalLanguageTemplateMatchingService.cs   # New interface
│   └── IGitTemplateSyncService.cs                   # New interface
├── Services/
│   ├── ServiceTemplateCatalogService.cs             # New implementation
│   ├── ProvisionedEnvironmentService.cs             # New implementation
│   ├── NaturalLanguageTemplateMatchingService.cs    # New (keyword fallback)
│   ├── GitTemplateSyncService.cs                    # New implementation
│   ├── BicepParameterParser.cs                      # New utility
│   ├── DeployerFactory.cs                           # New factory
│   └── EnvironmentActivityService.cs                # New implementation
└── BackgroundServices/
    ├── GitTemplateSyncBackgroundService.cs           # New hosted service
    ├── DeploymentStatusPollingBackgroundService.cs   # New hosted service
    └── SoftDeletePurgeBackgroundService.cs           # New hosted service (daily auto-purge)

tests/Platform.Engineering.Copilot.Tests.Unit/
├── AdminApi/
│   ├── TemplatesControllerTests.cs                  # New
│   ├── EnvironmentsControllerTests.cs               # New
│   ├── ComplianceControllerTests.cs                 # New
│   ├── ServiceTemplateCatalogServiceTests.cs        # New
│   ├── ProvisionedEnvironmentServiceTests.cs        # New
│   ├── NaturalLanguageMatchingServiceTests.cs       # New
│   ├── GitTemplateSyncServiceTests.cs               # New
│   └── BicepParameterParserTests.cs                 # New

tests/Platform.Engineering.Copilot.Tests.Integration/
├── AdminApi/
│   ├── TemplatesApiTests.cs                         # New (WebApplicationFactory)
│   ├── EnvironmentsApiTests.cs                      # New (WebApplicationFactory)
│   ├── ComplianceApiTests.cs                        # New (WebApplicationFactory)
│   └── BackgroundServiceTests.cs                    # New
```

**Structure Decision**: The Admin API project already exists at `src/Platform.Engineering.Copilot.Admin.API/`. Domain entities, interfaces, and services live in the Core project per existing architecture. The Agents project reference is added for environment deployment services. Controllers are rewritten in-place. CostsController, DeploymentsController, and GovernanceController are removed (their functionality is subsumed by the EnvironmentsController and ComplianceController per the spec). Tests follow the existing `Tests.Unit/` and `Tests.Integration/` convention with an `AdminApi/` subdirectory.

## Phase 0: Research (Complete)

**Output**: [research.md](research.md)

8 architectural decisions documented:

1. **ServiceTemplate expansion** → In-place with JSON string columns
2. **Child entity design** → Separate FK tables (DeployedResource, DriftItem, EnvironmentActivity)
3. **Concurrency** → `[Timestamp]` byte[] RowVersion → ETag Base64 → 409 Conflict
4. **Soft-delete** → EF Core global query filters + `IgnoreQueryFilters()`
5. **Background services** → `BackgroundService` + `PeriodicTimer`
6. **NL matching fallback** → Weighted keyword overlap scoring (0.0–1.0)
7. **Swagger** → Swashbuckle.AspNetCore (replaces built-in OpenApi)
8. **Auth** → JwtBearer with Azure Government `.us` endpoints + role policies

## Phase 1: Design (Complete)

**Outputs**:
- [data-model.md](data-model.md) — 5 entities (1 expanded + 4 new), 4 new enums, DbContext changes, indexes, query filters, state machines
- [contracts/api-contracts.md](contracts/api-contracts.md) — 45+ REST endpoints across 4 controllers with full request/response schemas
- [quickstart.md](quickstart.md) — Prerequisites, build/run, config, Docker, test commands

### Key Design Decisions

| Area | Decision |
|------|----------|
| ServiceTemplate | Expand from 14→35+ properties; rename ContentBicep→Content; replace IsApproved with Status enum |
| ProvisionedEnvironment | New entity with 26 properties, 3 child navigation collections |
| Controllers | Rewrite Templates + Environments + Health; new Compliance (stub); remove Costs/Deployments/Governance |
| DI | Single `AddAdminServices()` extension method registers all services |
| Auth | JwtBearer + Admin/Engineer role policies; DevBypass for local dev |
| Concurrency | ETag in response headers; If-Match required on PUT/PATCH; 409 on conflict |

## Constitution Re-Check (Post-Design)

| Principle | Status | Details |
|-----------|--------|---------|
| I. Documentation as Source of Truth | PASS | data-model.md follows DATABASE.md; contracts document all endpoints |
| II. BaseAgent/BaseTool Architecture | N/A | REST API only; no agents/tools created |
| III. Test-First Development | PASS | Test structure defined in project tree; 8+ unit test files, 4+ integration test files planned |
| IV. Azure Government & Compliance First | PASS | `.us` auth endpoints; `portal.azure.us` portal URLs; US Gov regions; compliance framework tracking in data model |
| V. Observability & Structured Logging | PASS | Serilog console+file sinks; all controller actions have try/catch structured logging |

**Gate Result**: PASS — ready for Phase 2 (/speckit.tasks)
