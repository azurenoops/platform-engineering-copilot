# Implementation Plan: Build Platform Copilot Core

**Branch**: `001-platform-copilot-core` | **Date**: 2026-02-22 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-platform-copilot-core/spec.md`

## Summary

Build the Platform Engineering Copilot — an AI-powered infrastructure and compliance platform for Azure Government. The system provides a multi-agent conversational platform where compliance officers, platform engineers, security leads, and auditors manage Azure Government environments through natural language. Eight specialized agents (Compliance, Infrastructure, Cost Management, Discovery, Environment, Knowledge Base, Configuration, Security) are orchestrated via the **Microsoft Agents SDK** with keyword fast-path routing and LLM fallback. The platform exposes tools via **MCP dual transport** (HTTP + stdio), streams responses via **SignalR**, and enforces **CAC/PIV + PIM** dual-gate authentication for IL5/IL6 compliance. A shared **NistService** provides offline-capable NIST 800-53 control catalog access via dual-source OSCAL (GitHub fetch + embedded fallback).

82 functional requirements (FR-001–FR-082), 13 user stories (US10/US11 deferred, US12/US13 scaffold-only), 149 implementation tasks (T001–T149) across 13 phases.

## Technical Context

**Language/Version**: .NET 9.0 / C# 12
**Primary Dependencies**:

| Package | Version | Purpose |
|---------|---------|---------|
| Microsoft.Agents.Protocols | Latest | Core types: IActivity, ITurnContext, channel protocols |
| Microsoft.Agents.Builder | Latest | AgentApplication, middleware pipeline, state management |
| Microsoft.Agents.Client | Latest | Inter-agent communication |
| Microsoft.SemanticKernel | 1.26.0 | AI orchestration, function calling, prompt templates |
| Microsoft.Extensions.AI | 9.1.0 | IChatClient abstraction, LLM routing fallback |
| ModelContextProtocol | 0.4.0-preview | MCP server (HTTP + stdio dual transport) |
| Microsoft.EntityFrameworkCore | 9.0 | ORM — SQL Server + SQLite fallback |
| Serilog | 4.2+ | Structured logging (console, file, App Insights) |
| Microsoft.AspNetCore.SignalR | 1.1 | Real-time chat streaming |
| Azure.ResourceManager.* | Latest | ARM, Policy, Defender, Cost Management, Resource Graph, Monitor |
| xUnit | 2.9+ | Test framework |
| FluentAssertions | Latest | Test assertions |
| Moq | Latest | Test mocking |

**Storage**: SQL Server (Azure SQL Edge dev, Azure SQL prod) with SQLite fallback. Two DbContexts: `PlatformEngineeringCopilotContext` (16 entities) and `ChatDbContext` (4 entities). 20 enumerations. Non-EF NistService models for OSCAL data.
**Testing**: xUnit 2.9+, FluentAssertions, Moq. Coverage targets: 80%+ unit, 70%+ integration, 95%+ critical paths.
**Target Platform**: Azure Government (usgovvirginia, usgovarizona, usgovtexas). ASP.NET Core web services. Docker containers for dev.
**Project Type**: Multi-project web-service platform (8 source projects + 3 test projects)
**Performance Goals**: ≤60s/500 resources, ≤5min/2K, ≤10min/5K. >5K: best-effort SLA with warning + confirmation. Orchestrator routing: 90%+ accuracy. Progress streaming for all scans >10s.
**Constraints**: IL5/IL6 compliance, CAC+PIM dual-gate auth, FIPS 140-2 Level 2 (Key Vault), 3yr assessment retention, 7yr immutable audit logs, WCAG 2.1 Level AA, US data residency only, offline-capable KB via embedded OSCAL.
**Scale/Scope**: 13 user stories, 82 FRs, 52 MCP tools across 8 agents, 4 compose configurations, dual transport (HTTP:5100 + stdio).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research Gate (Initial)

| # | Principle | Status | Evidence |
|---|-----------|--------|----------|
| I | Documentation as Source of Truth | ✅ PASS | All design in `/specs/001-platform-copilot-core/`. Contracts in `/contracts/`. No invented guidance — all references cite spec sections. |
| II | BaseAgent/BaseTool Architecture (NON-NEGOTIABLE) | ✅ PASS | All 8 agents extend `BaseAgent` (wrapping `Microsoft.Agents.Builder.AgentApplication`). All 52 tools extend `BaseTool`. System prompts externalized in `*.prompt.txt`. Tools registered via `RegisterTool()`. |
| III | Test-First Development (NON-NEGOTIABLE) | ✅ PASS | Tasks.md mandates test-first for every phase. 3 test projects: Unit, Integration, Manual. Coverage targets: 80%+ unit. All tasks include test file paths. |
| IV | Azure Government & Compliance First | ✅ PASS | CAC/PIV + PIM dual-gate (FR-008–FR-016, FR-069–FR-071). Azure Key Vault with managed identity (FR-082). FIPS 140-2 Level 2. US regions only. NIST/FedRAMP/IL5 control mapping. Gov cloud endpoints. |
| V | Observability & Structured Logging | ✅ PASS | Serilog 4.2+ with console+file (dev) and App Insights (prod). Health endpoint (FR-075). Structured metrics per agent/tool (FR-076). Correlation IDs across all calls (FR-077). Audit logging (FR-066). |

### Post-Design Gate (Phase 1 Complete)

| # | Principle | Status | Evidence |
|---|-----------|--------|----------|
| I | Documentation as Source of Truth | ✅ PASS | data-model.md (734 lines), 5 contract files, quickstart.md, research.md — all cross-referenced. |
| II | BaseAgent/BaseTool Architecture | ✅ PASS | data-model.md defines AgentDefinition + ToolDefinition entities. mcp-tools.md catalogs all 52 tools per agent. compliance-tools.md is canonical for 12 Compliance tools. configuration-tools.md defines 5 sub-actions. |
| III | Test-First Development | ✅ PASS | tasks.md (149 tasks) includes test tasks before implementation in every phase. Phase dependencies enforce test-first ordering. |
| IV | Azure Government & Compliance First | ✅ PASS | Auth flow detailed in research.md §3. NistService dual-source OSCAL (FR-080). Key Vault (FR-082). Accessibility WCAG 2.1 AA (FR-081). Section 508 compliance. |
| V | Observability & Structured Logging | ✅ PASS | research.md §5 details SignalR streaming. data-model.md includes AuditLogEntity with correlation IDs. FR-075–FR-078 fully specified. |

**Gate Result**: ✅ ALL PASS — No violations. Proceed to implementation.

## Project Structure

### Documentation (this feature)

```text
specs/001-platform-copilot-core/
├── plan.md                       # This file
├── research.md                   # Phase 0: 6 research topics
├── data-model.md                 # Phase 1: 16 EF entities, 20 enums, NistService models
├── quickstart.md                 # Phase 1: Setup and verification guide
├── contracts/
│   ├── mcp-tools.md              # 8 agents, 52 tools catalog
│   ├── compliance-tools.md       # Canonical 12-tool Compliance Agent contract
│   ├── configuration-tools.md    # Configuration Agent 5 sub-actions
│   ├── signalr-hub.md            # 6 server→client, 4 client→server methods
│   └── admin-api.md              # REST endpoints for Admin Dashboard
└── tasks.md                      # 149 tasks (T001–T149), 13 phases
```

### Source Code (repository root)

```text
Platform.Engineering.Copilot.sln

src/
├── Platform.Engineering.Copilot.Core/
│   ├── Agents/
│   │   ├── BaseAgent.cs                    # Abstract base — wraps AgentApplication
│   │   └── PlatformOrchestrator.cs         # Keyword fast-path + LLM fallback routing
│   ├── Tools/
│   │   └── BaseTool.cs                     # Abstract base — Name, Params, ExecuteAsync
│   ├── Data/
│   │   ├── PlatformEngineeringCopilotContext.cs  # Main DbContext (16 entities)
│   │   ├── ChatDbContext.cs                       # Chat DbContext (4 entities)
│   │   ├── Entities/                              # EF entity classes
│   │   ├── Enumerations/                          # 20 enum types
│   │   └── Services/                              # Repositories, retention service
│   ├── Auth/
│   │   ├── CacAuthenticationHandler.cs     # CAC/PIV JWT validation
│   │   ├── PimAuthorizationHandler.cs      # PIM tier enforcement
│   │   └── DevBypassHandler.cs             # Development bypass mode
│   ├── Services/
│   │   ├── NistService.cs                  # Dual-source OSCAL catalog service
│   │   ├── NistData/                       # Embedded OSCAL JSON snapshots
│   │   │   ├── nist-800-53-rev5.json
│   │   │   ├── fedramp-high-overlay.json
│   │   │   ├── fedramp-moderate-overlay.json
│   │   │   ├── dod-il5-overlay.json
│   │   │   ├── stig-mappings.json
│   │   │   └── azure-service-mappings.json
│   │   └── AzureErrorHandler.cs            # Plain-language error explanations
│   ├── Observability/
│   │   ├── CorrelationIdMiddleware.cs       # Distributed tracing
│   │   ├── HealthCheckService.cs            # /health endpoint
│   │   └── MetricsService.cs                # Structured metrics emission
│   └── ResponseEnvelope.cs                  # Platform-wide response envelope (FR-079)
│
├── Platform.Engineering.Copilot.Agents/
│   ├── Orchestrator/
│   │   ├── OrchestratorAgent.cs
│   │   └── orchestrator.prompt.txt
│   ├── Compliance/
│   │   ├── ComplianceAgent.cs               # 12 tools per compliance-tools.md
│   │   ├── compliance.prompt.txt
│   │   └── Tools/
│   │       ├── ComplianceAssessTool.cs      # compliance_assess
│   │       ├── ComplianceRemediateTool.cs   # compliance_remediate
│   │       ├── ComplianceCollectEvidenceTool.cs
│   │       ├── ComplianceGenerateDocumentTool.cs
│   │       ├── ComplianceGetControlFamilyTool.cs
│   │       ├── ComplianceStatusTool.cs
│   │       ├── ComplianceHistoryTool.cs
│   │       ├── ComplianceMapControlsTool.cs
│   │       ├── ComplianceCompareFrameworksTool.cs
│   │       ├── ComplianceExportTool.cs
│   │       ├── ComplianceMonitoringTool.cs  # Lightweight on-demand (not US10)
│   │       └── ComplianceDashboardTool.cs
│   ├── Infrastructure/
│   │   ├── InfrastructureAgent.cs           # 6 tools
│   │   ├── infrastructure.prompt.txt
│   │   └── Tools/
│   ├── CostManagement/
│   │   ├── CostManagementAgent.cs           # 6 tools
│   │   ├── costmanagement.prompt.txt
│   │   └── Tools/
│   ├── Discovery/
│   │   ├── DiscoveryAgent.cs                # 9 tools
│   │   ├── discovery.prompt.txt
│   │   └── Tools/
│   ├── Environment/
│   │   ├── EnvironmentAgent.cs              # 10 tools
│   │   ├── environment.prompt.txt
│   │   └── Tools/
│   ├── KnowledgeBase/
│   │   ├── KnowledgeBaseAgent.cs            # 8 tools (INistService dependency)
│   │   ├── knowledgebase.prompt.txt
│   │   └── Tools/
│   ├── Configuration/
│   │   ├── ConfigurationAgent.cs            # 1 tool (5 sub-actions)
│   │   ├── configuration.prompt.txt
│   │   └── Tools/
│   │       └── ConfigurationManageTool.cs   # IAgentStateManager shared state
│   └── Security/
│       ├── SecurityAgent.cs
│       ├── security.prompt.txt
│       └── Tools/
│
├── Platform.Engineering.Copilot.Mcp/
│   ├── Program.cs                           # Dual transport: HTTP (5100) + stdio
│   ├── McpHttpBridge.cs                     # JSON-RPC over HTTP
│   ├── Dockerfile
│   └── appsettings.json
│
├── Platform.Engineering.Copilot.Chat/
│   ├── Program.cs                           # ASP.NET Core Razor (port 5001)
│   ├── Hubs/
│   │   └── ChatHub.cs                       # SignalR hub per signalr-hub.md
│   ├── Pages/                               # Chat UI (WCAG 2.1 AA compliant)
│   ├── Dockerfile
│   └── appsettings.json
│
├── Platform.Engineering.Copilot.Admin.API/
│   ├── Program.cs                           # REST + Swagger (port 5050)
│   ├── Controllers/                         # per admin-api.md contract
│   ├── Dockerfile
│   └── appsettings.json
│
├── Platform.Engineering.Copilot.Admin.Client/
│   ├── Program.cs                           # Blazor WASM (port 5000)
│   ├── Pages/                               # WCAG 2.1 AA compliant
│   └── wwwroot/
│
├── Platform.Engineering.Copilot.State/      # Scaffold only (future)
│   └── placeholder.md
│
└── Platform.Engineering.Copilot.Channels/   # Scaffold only (US12/US13)
    ├── GitHub/                              # @platform Copilot extension scaffold
    └── M365/                                # Teams bot + Adaptive Cards scaffold

tests/
├── Platform.Engineering.Copilot.Tests.Unit/
│   ├── Agents/                              # Per-agent unit tests
│   ├── Tools/                               # Per-tool unit tests
│   ├── Services/                            # NistService, retention, audit tests
│   └── Auth/                                # CAC/PIM handler tests
│
├── Platform.Engineering.Copilot.Tests.Integration/
│   ├── Agents/                              # WebApplicationFactory-based
│   ├── Data/                                # EF Core migration + query tests
│   └── Mcp/                                 # MCP tool invocation tests
│
└── Platform.Engineering.Copilot.Tests.Manual/
    └── Scenarios/                           # Documented verification scenarios

docker-compose.mcp.yml                       # MCP server only
docker-compose.mcp-chat.yml                  # MCP + Chat
docker-compose.mcp-admin.yml                 # MCP + Admin API + Admin Client
docker-compose.mcp-chat-admin.yml            # All services
```

**Structure Decision**: Multi-project solution with 8 source projects + 3 test projects. Core abstractions (BaseAgent, BaseTool, DbContexts, Auth, NistService, Observability) isolated in `Core`. Agent implementations separated by domain in `Agents`. Frontend/API split matches distinct hosting concerns (MCP:5100, Chat:5001, Admin API:5050, Admin Client:5000). Test projects mirror source structure. Channels project scaffolded for US12/US13 extensions (deferred).

## Complexity Tracking

> No constitution violations to justify — all 5 principles pass at both gates.

| Aspect | Justification |
|--------|---------------|
| 8 source projects (exceeds typical 1–3) | Each serves a distinct deployment target with independent scaling concerns. MCP, Chat, Admin API, and Admin Client are separately deployed services. Core and Agents are shared libraries. State and Channels are scaffold-only. |
| 20 enumerations | Compliance domain requires fine-grained status/type modeling (severity, scan type, finding status, PIM tier, etc.). All enums map to spec FRs. |
| Dual DbContext | Chat history has different retention (purge-friendly) vs. compliance data (3–7 year retention). Separate contexts enable independent scaling and backup. |
| NistService dual-source | Air-gapped IL5/IL6 environments cannot reach GitHub. Embedded fallback ensures offline capability (FR-080). |
| Microsoft Agents SDK + Semantic Kernel | Microsoft Agents SDK provides activity pipeline, state management, and future M365 integration. Semantic Kernel provides AI orchestration and function calling. Both are needed — neither alone covers all requirements. |
