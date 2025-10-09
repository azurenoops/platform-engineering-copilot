# Platform Engineering Copilot - Architecture Documentation

**Last Updated:** October 9, 2025  
**Version:** 2.0 (Post-Refactoring)  
**Namespace:** `Platform.Engineering.Copilot.*`

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Architecture](#system-architecture)
3. [Project Structure](#project-structure)
4. [Dependency Graph](#dependency-graph)
5. [Core Components](#core-components)
6. [Data Flow](#data-flow)
7. [Plugin Architecture](#plugin-architecture)
8. [Technology Stack](#technology-stack)
9. [Security & Governance](#security--governance)
10. [Development Guidelines](#development-guidelines)

---

## Executive Summary

The Platform Engineering Copilot is an AI-powered infrastructure provisioning and governance platform built on .NET 9.0 and Microsoft Semantic Kernel. The system provides natural language interfaces for cloud infrastructure management, compliance monitoring, cost optimization, and environment onboarding.

### Key Capabilities

- **AI-Powered Infrastructure Provisioning** (Option C architecture)
- **Natural Language Query Processing** via Semantic Kernel
- **Multi-Cloud Support** (Azure, AWS focus areas)
- **Compliance & Governance Automation**
- **Document Intelligence Processing**
- **Real-Time Chat Interface** for user onboarding
- **Admin Console** for platform management
- **Model Context Protocol (MCP) Server** for AI agent integration

### Architecture Philosophy

- **Domain-Driven Design**: Services organized by business domains
- **Separation of Concerns**: Clear boundaries between layers
- **Data Layer Isolation**: No dependencies in Data project
- **AI-First Design**: Natural language interfaces over rigid APIs
- **Plugin-Based Extensibility**: Semantic Kernel plugins for modularity

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         CLIENT LAYER                                 │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐             │
│  │  Chat.App    │  │   Admin      │  │     MCP      │             │
│  │   (React)    │  │   Client     │  │   Clients    │             │
│  │   :3000      │  │   (React)    │  │              │             │
│  │              │  │   :3001      │  │              │             │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘             │
│         │                 │                  │                      │
│         │                 │                  │                      │
├─────────┼─────────────────┼──────────────────┼──────────────────────┤
│         │    API LAYER    │                  │                      │
├─────────┴─────────────────┴──────────────────┴──────────────────────┤
│                                                                      │
│  ┌──────▼──────────┐  ┌────────▼─────────┐  ┌────────▼─────────┐  │
│  │   Chat.App      │  │   Admin.API      │  │   Mcp.Server     │  │
│  │   Backend       │  │   Backend        │  │   (Console)      │  │
│  │   (SignalR)     │  │   :7002          │  │                  │  │
│  │   ASP.NET       │  │   ASP.NET        │  │                  │  │
│  └──────┬──────────┘  └────────┬─────────┘  └────────┬─────────┘  │
│         │                      │                      │             │
│         └──────────────────────┼──────────────────────┘             │
│                                │                                     │
│  ┌─────────────────────────────▼─────────────────────────────────┐ │
│  │                        API                                     │ │
│  │                   (Main REST API)                              │ │
│  │                        :7001                                   │ │
│  └─────────────────────────────┬─────────────────────────────────┘ │
│                                │                                     │
├────────────────────────────────┼─────────────────────────────────────┤
│         BUSINESS LOGIC LAYER   │                                     │
├────────────────────────────────┴─────────────────────────────────────┤
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                          CORE                                 │  │
│  │                                                               │  │
│  │  ┌────────────────────────────────────────────────────────┐  │  │
│  │  │         Semantic Kernel Plugins (8)                    │  │  │
│  │  │  • Compliance (AI-powered, Option C)                   │  │  │
│  │  │  • Infrastructure (AI-powered, Option C)               │  │  │
│  │  │  • Cost Management                                     │  │  │
│  │  │  • Document Processing                                 │  │  │
│  │  │  • Onboarding                                          │  │  │
│  │  │  • Resource Discovery                                  │  │  │
│  │  │  • Security                                            │  │  │
│  │  └────────────────────────────────────────────────────────┘  │  │
│  │                                                               │  │
│  │  ┌────────────────────────────────────────────────────────┐  │  │
│  │  │         Domain Services                                │  │  │
│  │  │  • Infrastructure Provisioning (AI-powered)            │  │  │
│  │  │  • Compliance Service (AI-powered)                     │  │  │
│  │  │  • Cost Management                                     │  │  │
│  │  │  • Deployment Orchestration                            │  │  │
│  │  │  • Template Generation (ARM, Bicep, Terraform, K8s)    │  │  │
│  │  │  • Onboarding Workflows                                │  │  │
│  │  │  • Chat Service (AI conversations)                     │  │  │
│  │  │  • Notification Service                                │  │  │
│  │  │  • Validation Services                                 │  │  │
│  │  └────────────────────────────────────────────────────────┘  │  │
│  │                                                               │  │
│  └────────┬──────────────────────┬──────────────────────────────┘  │
│           │                      │                                  │
│  ┌────────▼─────────┐  ┌─────────▼──────────┐                     │
│  │   Governance     │  │  DocumentProcessing │                     │
│  │   (Policy &      │  │  (Form Recognizer,  │                     │
│  │   Compliance)    │  │   AI Search)        │                     │
│  └──────────────────┘  └─────────────────────┘                     │
│                                                                      │
├──────────────────────────────────────────────────────────────────────┤
│                      DATA ACCESS LAYER                               │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                          Data                                 │  │
│  │                                                               │  │
│  │  • Entity Framework Core 9.0                                 │  │
│  │  • EnvironmentManagementContext (20+ DbSets)                 │  │
│  │  • Entities: OnboardingRequest, EnvironmentTemplate, etc.    │  │
│  │  • Migrations, Seeding, Factories                            │  │
│  │  • Supports: SQL Server, SQLite, In-Memory                   │  │
│  │                                                               │  │
│  │  **NO DEPENDENCIES** (Proper Isolation)                      │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

### Overview

The platform consists of **10 main projects** organized into logical layers:

| Project | Type | Port | Purpose |
|---------|------|------|---------|
| **Platform.Engineering.Copilot.Core** | Library | - | Business logic, plugins, services |
| **Platform.Engineering.Copilot.Data** | Console | - | EF Core, entities, migrations |
| **Platform.Engineering.Copilot.Governance** | Library | - | Policy, compliance services |
| **Platform.Engineering.Copilot.DocumentProcessing** | Library | - | Document AI, Form Recognizer |
| **Platform.Engineering.Copilot.API** | Web API | 7001 | Main REST API |
| **Platform.Engineering.Copilot.Chat.App** | Web + SPA | 3000 | Chat interface (React + ASP.NET) |
| **Platform.Engineering.Copilot.Admin.API** | Web API | 7002 | Admin backend API |
| **Platform.Engineering.Copilot.Admin.Client** | Web + SPA | 3001 | Admin console (React + ASP.NET) |
| **Platform.Engineering.Copilot.Mcp** | Console | - | Model Context Protocol server |
| **Platform.Engineering.Copilot.Tests.*** | Test | - | Unit & integration tests |

---

## Dependency Graph

```
┌────────────────────────────────────────────────────────────────┐
│                     DEPENDENCY HIERARCHY                        │
└────────────────────────────────────────────────────────────────┘

Level 0 (Foundation):
├── Data (NO DEPENDENCIES - Isolated)
│
Level 1 (Core Business Logic):
├── Core ──→ Data
│
Level 2 (Domain Extensions):
├── Governance ──→ Core ──→ Data
├── DocumentProcessing ──→ Core, Governance
│
Level 3 (Execution Layer):
├── Mcp ──→ Core
│
Level 4 (API Layer):
├── API ──→ Core, Data, Governance, DocumentProcessing
├── Chat.App ──→ Core, Data, Governance
├── Admin.API ──→ Core, Data

┌────────────────────────────────────────────────────────────────┐
│                     DETAILED DEPENDENCIES                       │
└────────────────────────────────────────────────────────────────┘

Mcp
  └── Core
       └── Data

Chat.App
  ├── Core
  ├── Data
  └── Governance
       └── Core
            └── Data

Admin.API
  ├── Core
  └── Data

API
  ├── Core
  ├── Data
  ├── Governance
  └── DocumentProcessing
       ├── Core
       └── Governance

DocumentProcessing
  ├── Core
  └── Governance

Governance
  └── Core
       └── Data

Core
  └── Data

Data
  (NO DEPENDENCIES)
```

### Dependency Rules

1. **Data Layer Isolation**: Data project has zero dependencies (proper data layer pattern)
2. **No Circular Dependencies**: All dependencies flow downward in hierarchy
3. **Core as Hub**: Core is the central library referenced by all higher layers
4. **Governance Extension**: Governance extends Core with policy/compliance features
5. **API Layer Independence**: Frontend APIs (Chat.App, Admin.API) don't depend on each other

---

## Core Components

### 1. Platform.Engineering.Copilot.Core

**Purpose**: Central business logic library containing all services, plugins, and domain models.

**Key Directories**:

```
Core/
├── Configuration/          # Options classes (GovernanceOptions, AzureOptions, etc.)
├── Contracts/             # Shared contracts (IMcpToolHandler, IToolResult)
├── Extensions/            # DI registration (ServiceCollectionExtensions)
├── Interfaces/            # Service interfaces (40+ interfaces)
│   ├── IInfrastructureProvisioningService
│   ├── IOnboardingService
│   ├── IComplianceService
│   ├── ICostManagementService
│   └── [35+ more interfaces]
├── Models/                # DTOs, requests, responses
│   ├── Compliance/        # ComplianceCheckRequest, ComplianceReport
│   ├── Cost/             # CostEstimate, CostBreakdown
│   ├── EnvironmentManagement/  # EnvironmentRequest, DeploymentStatus
│   ├── Notifications/    # NotificationRequest, EmailTemplate
│   └── Onboarding/       # OnboardingFlowRequest, StepDefinition
├── Plugins/              # Semantic Kernel plugins (8)
│   ├── BaseSupervisorPlugin.cs       # Base class for all plugins
│   ├── CompliancePlugin.cs           # AI-powered (Option C)
│   ├── CostManagementPlugin.cs
│   ├── DocumentPlugin.cs
│   ├── InfrastructurePlugin.cs       # AI-powered (Option C)
│   ├── OnboardingPlugin.cs
│   ├── ResourceDiscoveryPlugin.cs
│   └── SecurityPlugin.cs
└── Services/             # Domain-organized services
    ├── Cache/            # Redis caching services
    ├── Chat/             # AI chat services
    ├── Compliance/       # ComplianceService (AI-powered)
    ├── Cost/             # Cost management services
    ├── Deployment/       # DeploymentOrchestrationService
    ├── Gateway/          # Service gateway implementations
    ├── Generators/       # Template generators
    │   ├── ARM/         # Azure Resource Manager
    │   ├── Bicep/       # Bicep templates
    │   ├── Terraform/   # Terraform configs
    │   └── Kubernetes/  # K8s manifests
    ├── Infrastructure/   # InfrastructureProvisioningService (AI-powered)
    ├── Notifications/    # Email, Teams notifications
    ├── Onboarding/       # User onboarding workflows
    ├── Security/         # Security scanning, RBAC
    ├── TemplateGeneration/  # Template orchestration
    └── Validation/       # Input validation services
```

**Key Technologies**:
- **Microsoft.SemanticKernel** 1.26.0 - AI orchestration
- **Azure SDK** (11 packages) - Azure resource management
- **Entity Framework Core** 9.0.0 - Data access
- **KubernetesClient** 15.0.1 - K8s operations
- **Octokit** 15.1.0 - GitHub API

---

### 2. Platform.Engineering.Copilot.Data

**Purpose**: Data access layer with Entity Framework Core, providing database contexts, entities, and migrations.

**Key Features**:
- **Executable Project**: Configured as console app for EF Core tooling (migrations, seeding)
- **Zero Dependencies**: Proper data layer isolation (no references to other projects)
- **Multi-Database Support**: SQL Server, SQLite, In-Memory

**Key Directories**:

```
Data/
├── Context/
│   └── EnvironmentManagementContext.cs  # Main DbContext (20+ DbSets)
├── Entities/                            # Database entities
│   ├── OnboardingRequest.cs             # Complete entity (343 lines)
│   ├── OnboardingStatus.cs              # Enum (Draft → Completed)
│   ├── EnvironmentTemplate.cs
│   ├── DeploymentRecord.cs
│   ├── ChatSession.cs
│   ├── CostEstimate.cs
│   ├── ComplianceReport.cs
│   ├── SecurityAssessment.cs
│   └── [15+ more entities]
├── Extensions/                          # EF configuration extensions
├── Factories/                           # DbContext factories (design-time)
├── Migrations/                          # EF Core migrations
├── Seed/                                # Database seeding
├── Services/                            # Data access services
└── Program.cs                           # Migration runner
```

**Key Entities**:

| Entity | Description | Key Properties |
|--------|-------------|----------------|
| **OnboardingRequest** | User onboarding workflow | Status (9 states), OrganizationName, RequestedEnvironments |
| **EnvironmentTemplate** | Infrastructure templates | TemplateName, ResourceTypes, Configuration JSON |
| **DeploymentRecord** | Deployment history | Status, ResourceGroup, DeployedBy, Timestamp |
| **ChatSession** | AI chat conversations | SessionId, Messages, Context |
| **CostEstimate** | Cost projections | MonthlyEstimate, ResourceBreakdown |
| **ComplianceReport** | Compliance checks | Score, Findings, Recommendations |
| **SecurityAssessment** | Security scans | Severity, Vulnerabilities, Remediation |

**OnboardingStatus Enum**:
```csharp
public enum OnboardingStatus
{
    Draft,              // Initial state
    PendingReview,      // Submitted for review
    UnderReview,        // Being reviewed
    Approved,           // Approved, ready for provisioning
    Provisioning,       // Infrastructure being created
    Completed,          // Successfully completed
    Rejected,           // Rejected by reviewer
    Failed,             // Provisioning failed
    Cancelled           // User cancelled
}
```

**Technologies**:
- **Entity Framework Core** 9.0.0 (SqlServer, Sqlite, InMemory, Design, Tools)

---

### 3. Platform.Engineering.Copilot.Governance

**Purpose**: Azure policy management, compliance checking, and resource governance.

**Key Services**:
- Policy evaluation and enforcement
- Resource compliance checking
- Governance recommendations
- Cost governance rules

**Technologies**:
- **Azure.ResourceManager.PolicyInsights** 1.2.0
- **Microsoft.Azure.Management.ResourceGraph** 4.1.0
- **Azure.ResourceManager.ResourceHealth** 1.1.0

**Note**: Two services temporarily disabled (ResourceRightsizingService, AzureResourceHealthService) due to missing AzureOptions configuration.

---

### 4. Platform.Engineering.Copilot.DocumentProcessing

**Purpose**: AI-powered document intelligence, form recognition, and Azure AI Search integration.

**Capabilities**:
- PDF/Word/Excel document parsing
- Form data extraction (Azure Form Recognizer)
- AI-powered document search
- Architectural diagram analysis

**Technologies**:
- **Azure.AI.FormRecognizer** 4.1.0
- **Azure.AI.OpenAI** 2.1.0-beta.1
- **Azure.Search.Documents** 11.6.0
- **DocumentFormat.OpenXml** 3.2.0
- **iTextSharp** 5.5.13.4
- **Aspose.Words** 24.9.0 (Commercial license)

---

### 5. Platform.Engineering.Copilot.API

**Purpose**: Main REST API for platform operations, infrastructure provisioning, and environment management.

**Port**: 7001

**Key Controllers**:
- Infrastructure management
- Environment lifecycle
- Deployment operations
- Template management

**Technologies**:
- ASP.NET Core 9.0
- Swashbuckle (OpenAPI/Swagger)
- Docker.DotNet, KubernetesClient
- YamlDotNet

---

### 6. Platform.Engineering.Copilot.Chat.App

**Purpose**: Full-stack chat application for user onboarding with AI assistance.

**Architecture**: ASP.NET Core backend + React TypeScript SPA

**Port**: 3000 (React dev server)

**Features**:
- Real-time chat via SignalR
- AI-powered onboarding guidance
- Multi-step workflow management
- Environment provisioning status tracking

**Frontend Stack**:
- React 18.2.0
- TypeScript 4.9.5
- React Router 6.20.0
- Axios 1.6.0

**Backend Stack**:
- ASP.NET Core 9.0
- SignalR 1.1.0 (Real-time)
- Serilog 4.2.0 (Logging)
- EF Core 9.0.0

---

### 7. Platform.Engineering.Copilot.Admin.API

**Purpose**: Admin backend API for platform management and operations.

**Port**: 7002

**Admin Controllers** (7):
- **InfrastructureAdminController** - Infrastructure management (updated for Option C)
- **CostAdminController** - Cost analysis and budgeting
- **DeploymentAdminController** - Deployment operations
- **EnvironmentAdminController** - Environment lifecycle
- **GovernanceAdminController** - Policy and compliance
- **OnboardingAdminController** - User onboarding management
- **TemplateAdminController** - Template CRUD operations

---

### 8. Platform.Engineering.Copilot.Admin.Client

**Purpose**: Full-stack admin console SPA for platform administration.

**Architecture**: ASP.NET Core host + React TypeScript SPA

**Port**: 3001 (React dev server), proxies to Admin.API on 7002

**Features**:
- Infrastructure management dashboard
- Cost analytics and visualizations
- User onboarding approval workflows
- Template library management
- Compliance reporting
- Deployment monitoring

**Frontend Stack**:
- React 18.2.0
- TypeScript 4.9.5
- React Router 6.20.0
- Axios 1.6.0
- **Tailwind CSS** 3.3.6 (Styling)

**Configuration**:
```json
{
  "proxy": "http://localhost:7002"  // Routes API calls to Admin.API
}
```

---

### 9. Platform.Engineering.Copilot.Mcp

**Purpose**: Model Context Protocol server exposing platform capabilities to external AI agents.

**Type**: Console application

**Features**:
- MCP protocol implementation
- Tool execution via Semantic Kernel plugins
- Structured logging with Serilog

**Technologies**:
- Microsoft.Extensions.Hosting 9.0.0
- Serilog 4.2.0
- System.Text.Json 9.0.0

---

## Data Flow

### Onboarding Workflow Example

```
┌──────────┐
│   User   │ Opens Chat.App
└────┬─────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Chat.App (React SPA)                                          │
│  • User describes desired environment in natural language      │
│  • Real-time AI guidance via SignalR                           │
└────┬───────────────────────────────────────────────────────────┘
     │ HTTP POST /api/onboarding
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Chat.App Backend (ASP.NET Core)                               │
│  • Receives onboarding request                                 │
│  • Validates input                                             │
└────┬───────────────────────────────────────────────────────────┘
     │ Calls IOnboardingService
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Core.Services.Onboarding.OnboardingService                    │
│  • Creates OnboardingRequest entity (Status: Draft)            │
│  • Saves to database via EF Core                               │
│  • Returns request ID                                          │
└────┬───────────────────────────────────────────────────────────┘
     │ Saves entity
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Data.Context.EnvironmentManagementContext                     │
│  • Persists OnboardingRequest to database                      │
│  • Triggers audit logging                                      │
└────┬───────────────────────────────────────────────────────────┘
     │ Returns to user
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Chat.App displays confirmation                                │
│  • Status: PendingReview                                       │
│  • Next step: Admin approval                                   │
└────────────────────────────────────────────────────────────────┘
     
     ... Admin reviews in Admin.Client ...
     
┌────────────────────────────────────────────────────────────────┐
│  Admin.Client (React SPA)                                      │
│  • Admin reviews request                                       │
│  • Clicks "Approve"                                            │
└────┬───────────────────────────────────────────────────────────┘
     │ HTTP POST /api/admin/onboarding/{id}/approve
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Admin.API Backend                                             │
│  • Updates OnboardingRequest (Status: Approved)                │
│  • Triggers provisioning workflow                              │
└────┬───────────────────────────────────────────────────────────┘
     │ Calls IInfrastructureProvisioningService
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Core.Services.Infrastructure.InfrastructureProvisioningService│
│  • AI parses natural language requirements                     │
│  • Generates ARM/Bicep templates                               │
│  • Provisions Azure resources                                  │
│  • Updates OnboardingRequest (Status: Provisioning)            │
└────┬───────────────────────────────────────────────────────────┘
     │ Uses Semantic Kernel
     ▼
┌────────────────────────────────────────────────────────────────┐
│  InfrastructurePlugin (Semantic Kernel)                        │
│  • Function: provision_infrastructure                          │
│  • AI-powered query parsing (Option C)                         │
│  • Calls Azure SDK to create resources                         │
└────┬───────────────────────────────────────────────────────────┘
     │ Provisioning complete
     ▼
┌────────────────────────────────────────────────────────────────┐
│  OnboardingService updates status                              │
│  • Status: Completed                                           │
│  • Sends notification to user                                  │
│  • Creates DeploymentRecord                                    │
└────────────────────────────────────────────────────────────────┘
```

### AI Query Processing Flow (Option C)

```
User Query: "Create storage account mydata in eastus with Standard_LRS"
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  InfrastructureProvisioningService.ProvisionInfrastructureAsync│
│  • Receives natural language query                             │
└────┬───────────────────────────────────────────────────────────┘
     │ Calls Semantic Kernel ChatCompletion
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Semantic Kernel AI (GPT-4)                                    │
│  • Parses query into structured intent                         │
│  • Returns JSON:                                               │
│    {                                                           │
│      "resourceType": "storage-account",                        │
│      "resourceName": "mydata",                                 │
│      "location": "eastus",                                     │
│      "parameters": { "sku": "Standard_LRS" }                   │
│    }                                                           │
└────┬───────────────────────────────────────────────────────────┘
     │ Intent extracted
     ▼
┌────────────────────────────────────────────────────────────────┐
│  InfrastructureProvisioningService                             │
│  • Routes to storage account provisioning logic                │
│  • Validates parameters                                        │
│  • Generates ARM template                                      │
└────┬───────────────────────────────────────────────────────────┘
     │ Calls Azure SDK
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Azure.ResourceManager.Storage                                 │
│  • Creates storage account                                     │
│  • Returns resource ID                                         │
└────┬───────────────────────────────────────────────────────────┘
     │ Success
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Returns InfrastructureProvisionResult                         │
│  • Success: true                                               │
│  • ResourceId: /subscriptions/.../mydata                       │
│  • Message: "Storage account created successfully"             │
└────────────────────────────────────────────────────────────────┘
```

---

## Plugin Architecture

### Semantic Kernel Plugins

The platform uses **8 Semantic Kernel plugins** for AI-powered operations. Two plugins have been refactored to the "Option C" architecture (AI-powered query-based interface).

#### Plugin Inventory

| Plugin | Functions | Pattern | Status |
|--------|-----------|---------|--------|
| **BaseSupervisorPlugin** | N/A | Base class | ✅ Base |
| **CompliancePlugin** | 1 (`process_compliance_query`) | Option C | ✅ Refactored |
| **InfrastructurePlugin** | 4 | Option C | ✅ Refactored |
| **CostManagementPlugin** | Multiple | Legacy | ⏳ Pending |
| **DocumentPlugin** | Multiple | Legacy | ⏳ Pending |
| **OnboardingPlugin** | Multiple | Legacy | ⏳ Pending |
| **ResourceDiscoveryPlugin** | Multiple | Legacy | ⏳ Pending |
| **SecurityPlugin** | Multiple | Legacy | ⏳ Pending |

#### Option C Architecture Pattern

**Before (Legacy Pattern)**:
```csharp
// Multiple specific functions
[KernelFunction("provision_virtual_network")]
public async Task<string> ProvisionVirtualNetworkAsync(
    string name, string resourceGroup, string location, 
    string addressSpace, CancellationToken ct)

[KernelFunction("provision_storage_account")]
public async Task<string> ProvisionStorageAccountAsync(
    string name, string resourceGroup, string location, 
    string sku, CancellationToken ct)

// ... 9 more functions (14 total)
```

**After (Option C Pattern)**:
```csharp
// Single AI-powered function
[KernelFunction("provision_infrastructure")]
[Description("Provision any Azure infrastructure resource from natural language description")]
public async Task<InfrastructureProvisionResult> ProvisionInfrastructureAsync(
    [Description("Natural language description of infrastructure to provision")]
    string query,
    CancellationToken ct)
{
    // AI parses query → structured intent → provision resource
    var intent = await _service.ParseQueryAsync(query, ct);
    return await _service.ProvisionAsync(intent, ct);
}
```

**Benefits**:
- **71% method reduction** (14 methods → 4 methods)
- **69% code reduction** (409 lines → 127 lines)
- **Natural language interface** - no rigid parameter signatures
- **Extensible without code changes** - new resource types supported via AI training
- **Better AI integration** - LLM can understand and use single function more easily

#### CompliancePlugin (Option C)

```csharp
[KernelFunction("process_compliance_query")]
[Description("Process compliance-related queries and return compliance reports")]
public async Task<string> ProcessComplianceQueryAsync(
    [Description("Natural language compliance query")]
    string query,
    CancellationToken ct)
{
    // AI-powered compliance checking
    var report = await _complianceService.ProcessQueryAsync(query, ct);
    return JsonSerializer.Serialize(report);
}
```

**Example Queries**:
- "Check compliance for resource group prod-rg"
- "What are the security vulnerabilities in subscription sub-123?"
- "Generate compliance report for all resources tagged with env=prod"

#### InfrastructurePlugin (Option C)

```csharp
[KernelFunction("provision_infrastructure")]
[Description("Provision any Azure infrastructure resource from natural language")]
public async Task<InfrastructureProvisionResult> ProvisionInfrastructureAsync(
    string query, CancellationToken ct)

[KernelFunction("estimate_infrastructure_cost")]
[Description("Estimate cost of infrastructure from natural language description")]
public async Task<InfrastructureCostEstimate> EstimateCostAsync(
    string query, CancellationToken ct)

[KernelFunction("list_resource_groups")]
[Description("List all resource groups in subscription")]
public async Task<List<string>> ListResourceGroupsAsync(
    CancellationToken ct)

[KernelFunction("delete_resource_group")]
[Description("Delete a resource group and all contained resources")]
public async Task<bool> DeleteResourceGroupAsync(
    string resourceGroupName, CancellationToken ct)
```

**Supported Resource Types** (11):
- storage-account, vnet, subnet, keyvault, nsg
- managed-identity, log-analytics, app-insights
- load-balancer, blob-container, file-share

**Example Queries**:
- "Create storage account mydata in eastus with Standard_LRS"
- "Provision vnet prod-vnet with address space 10.0.0.0/16 in westus2"
- "How much will a Standard D4s_v3 VM cost in eastus for one month?"

---

## Technology Stack

### Backend Technologies

| Technology | Version | Purpose |
|------------|---------|---------|
| **.NET** | 9.0 | Application framework |
| **C#** | 12.0 | Programming language |
| **Microsoft.SemanticKernel** | 1.26.0 | AI orchestration |
| **Entity Framework Core** | 9.0.0 | ORM, data access |
| **ASP.NET Core** | 9.0 | Web framework |
| **SignalR** | 1.1.0 | Real-time communication |
| **Serilog** | 4.2.0+ | Structured logging |
| **Swashbuckle** | 9.0.5 | OpenAPI/Swagger |

### Azure SDK

| Package | Version | Purpose |
|---------|---------|---------|
| **Azure.ResourceManager.Resources** | 1.9.0 | ARM deployments |
| **Azure.ResourceManager.PolicyInsights** | 1.2.0 | Policy compliance |
| **Azure.ResourceManager.Network** | 1.9.0 | Networking resources |
| **Azure.ResourceManager.ContainerService** | 1.2.2 | AKS/containers |
| **Azure.ResourceManager.AppService** | 1.2.0 | Web apps |
| **Azure.ResourceManager.Storage** | 1.3.0 | Storage accounts |
| **Azure.Monitor.Query** | 1.5.0 | Metrics, logs |
| **Azure.Communication.Email** | 1.1.0 | Email services |
| **Azure.AI.FormRecognizer** | 4.1.0 | Document intelligence |
| **Azure.AI.OpenAI** | 2.1.0-beta.1 | GPT models |
| **Azure.Search.Documents** | 11.6.0 | AI Search |

### Infrastructure & DevOps

| Technology | Version | Purpose |
|------------|---------|---------|
| **Docker.DotNet** | 3.125.15 | Docker management |
| **KubernetesClient** | 15.0.1 | Kubernetes operations |
| **YamlDotNet** | 16.2.1 | YAML parsing |
| **Octokit** | 15.1.0 | GitHub API |

### Frontend Technologies

| Technology | Version | Purpose |
|------------|---------|---------|
| **React** | 18.2.0 | UI framework |
| **TypeScript** | 4.9.5 | Type-safe JavaScript |
| **React Router** | 6.20.0 | SPA routing |
| **Axios** | 1.6.0 | HTTP client |
| **Tailwind CSS** | 3.3.6 | Utility-first CSS |

### Database Support

- **SQL Server** (Production)
- **SQLite** (Development, lightweight)
- **In-Memory** (Testing)

### Document Processing

| Package | Version | Purpose |
|---------|---------|---------|
| **DocumentFormat.OpenXml** | 3.2.0 | Word/Excel parsing |
| **iTextSharp** | 5.5.13.4 | PDF processing |
| **Aspose.Words** | 24.9.0 | Advanced document features |

---

## Security & Governance

### Authentication & Authorization

- **Azure AD Integration**: Support for Azure Active Directory authentication
- **Role-Based Access Control (RBAC)**: Role-based permissions for admin operations
- **API Key Authentication**: Secure API access for external integrations

### Governance Features

1. **Policy Enforcement**
   - Azure Policy integration via PolicyInsights SDK
   - Automated compliance checking
   - Policy violation detection and reporting

2. **Cost Governance**
   - Budget tracking and alerts
   - Cost estimation before provisioning
   - Resource rightsizing recommendations (planned)

3. **Security Scanning**
   - Vulnerability detection
   - Security assessment reporting
   - Remediation recommendations

4. **Audit Logging**
   - All operations logged to database
   - Immutable audit trail
   - Compliance reporting

### Data Protection

- **Encryption at Rest**: Database encryption enabled
- **Encryption in Transit**: HTTPS/TLS for all API communication
- **Secrets Management**: Azure Key Vault integration for credentials
- **Data Retention**: Configurable retention policies

---

## Development Guidelines

### Adding a New Service

1. **Create Interface** in `Core/Interfaces/`
   ```csharp
   public interface IMyNewService
   {
       Task<Result> DoSomethingAsync(CancellationToken ct);
   }
   ```

2. **Implement Service** in `Core/Services/MyDomain/`
   ```csharp
   public class MyNewService : IMyNewService
   {
       private readonly ILogger<MyNewService> _logger;
       
       public MyNewService(ILogger<MyNewService> logger)
       {
           _logger = logger;
       }
       
       public async Task<Result> DoSomethingAsync(CancellationToken ct)
       {
           // Implementation
       }
   }
   ```

3. **Register in DI** in `Core/Extensions/ServiceCollectionExtensions.cs`
   ```csharp
   services.AddScoped<IMyNewService, MyNewService>();
   ```

4. **Add Unit Tests** in `Tests.Unit/Services/MyDomain/`

### Adding a New Semantic Kernel Plugin

1. **Inherit from BaseSupervisorPlugin**
   ```csharp
   public class MyNewPlugin : BaseSupervisorPlugin
   {
       public MyNewPlugin(ILogger<MyNewPlugin> logger, Kernel kernel)
           : base(logger, kernel)
       {
       }
   }
   ```

2. **Add Kernel Functions**
   ```csharp
   [KernelFunction("my_function")]
   [Description("Description of what this function does")]
   public async Task<string> MyFunctionAsync(
       [Description("Parameter description")] string input,
       CancellationToken ct)
   {
       try
       {
           // Implementation
           return result;
       }
       catch (Exception ex)
       {
           return await HandleErrorAsync("MyFunction", ex);
       }
   }
   ```

3. **Register Plugin** in `ServiceCollectionExtensions.cs`
   ```csharp
   var myPlugin = new MyNewPlugin(
       serviceProvider.GetRequiredService<ILogger<MyNewPlugin>>(),
       kernel);
   kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(myPlugin));
   ```

### Adding a New Entity

1. **Create Entity** in `Data/Entities/`
   ```csharp
   public class MyEntity
   {
       public int Id { get; set; }
       public string Name { get; set; } = string.Empty;
       public DateTime CreatedAt { get; set; }
   }
   ```

2. **Add DbSet** to `EnvironmentManagementContext`
   ```csharp
   public DbSet<MyEntity> MyEntities { get; set; }
   ```

3. **Create Migration**
   ```bash
   cd src/Platform.Engineering.Copilot.Data
   dotnet ef migrations add AddMyEntity
   ```

4. **Update Database**
   ```bash
   dotnet ef database update
   ```

### Coding Standards

- **Nullable Reference Types**: Enabled (`<Nullable>enable</Nullable>`)
- **Implicit Usings**: Enabled (`<ImplicitUsings>enable</ImplicitUsings>`)
- **Async/Await**: Use async methods for I/O operations
- **CancellationToken**: Pass `CancellationToken` to all async methods
- **Logging**: Use `ILogger<T>` for structured logging
- **Error Handling**: Use try-catch with proper error messages
- **Dependency Injection**: Use constructor injection
- **Configuration**: Use Options pattern (`IOptions<T>`)

### Testing Strategy

1. **Unit Tests** (`Tests.Unit/`)
   - Test individual methods in isolation
   - Mock dependencies with Moq
   - Use xUnit, FluentAssertions, AutoFixture

2. **Integration Tests** (`Tests.Integration/`)
   - Test full API endpoints
   - Use in-memory database
   - Test service interactions

3. **Load Tests**
   - NBomber for performance testing
   - Test infrastructure provisioning at scale

---

## Performance Considerations

### Caching Strategy

- **Redis Cache**: Distributed caching for session data, frequently accessed data
- **Memory Cache**: In-process caching for configuration, templates
- **Cache Invalidation**: Event-driven invalidation on data changes

### Database Optimization

- **Connection Pooling**: Enabled by default in EF Core
- **Query Optimization**: Use compiled queries for frequently executed queries
- **Indexing**: Add indexes on foreign keys, frequently queried columns
- **Pagination**: Implement pagination for large result sets

### API Performance

- **Response Compression**: Enable Gzip compression for API responses
- **Async Operations**: Use async/await throughout for non-blocking I/O
- **Rate Limiting**: Implement rate limiting to prevent abuse
- **CDN**: Use CDN for static assets in SPAs

---

## Monitoring & Observability

### Logging

- **Serilog**: Structured logging throughout the application
- **Log Sinks**: Console, File, Application Insights (planned)
- **Log Levels**: Debug, Information, Warning, Error, Critical
- **Correlation IDs**: Track requests across services

### Metrics

- **Azure Monitor**: Metrics collection via Azure SDK
- **Performance Counters**: CPU, memory, request duration
- **Custom Metrics**: Infrastructure provisioning success rate, cost savings

### Health Checks

- **ASP.NET Core Health Checks**: `/health` endpoint
- **Database Health**: Check EF Core connection
- **External Services**: Check Azure SDK connectivity
- **Liveness**: Service is running
- **Readiness**: Service is ready to handle requests

---

## Deployment Architecture

### Container Support

- **Docker**: Dockerfile and docker-compose configurations
- **Kubernetes**: K8s manifest generation via Generators
- **Azure Container Apps**: Deployment target (planned)

### CI/CD Pipeline

- **Build**: `dotnet build` across all projects
- **Test**: `dotnet test` for unit and integration tests
- **Package**: Docker image build
- **Deploy**: Azure DevOps / GitHub Actions (planned)

### Environment Configuration

- **Development**: SQLite, local services, debug logging
- **Staging**: SQL Server, Azure services, info logging
- **Production**: SQL Server, Azure services, warning+ logging, monitoring

---

## Migration Notes

### Recent Refactoring (October 2025)

1. **Namespace Change**: `Supervisor.*` → `Platform.Engineering.Copilot.*`
2. **Option C Implementation**: CompliancePlugin and InfrastructurePlugin refactored to AI-powered query-based interface
3. **Circular Dependency Fix**: Moved `GovernanceOptions` from Governance to Core.Configuration
4. **Entity Consolidation**: `OnboardingRequest` consolidated to Data.Entities (single source of truth)
5. **EF Core Upgrade**: Data project upgraded from EF Core 8.0 → 9.0
6. **Gateway References Removed**: Cleaned up non-existent Gateway project references

### Breaking Changes

- **Namespace**: Update all `using Supervisor.*` statements to `using Platform.Engineering.Copilot.*`
- **InfrastructureProvisioningService Interface**: Changed from 14 specific methods to 4 query-based methods
- **OnboardingRequest Location**: Moved from `Core.Models.Onboarding` to `Data.Entities`
- **GovernanceOptions Location**: Moved from `Governance.Configuration` to `Core.Configuration`

---

## Future Roadmap

### Planned Features

1. **Multi-Cloud Support**
   - AWS provisioning via Terraform
   - GCP support for hybrid scenarios

2. **Advanced AI Capabilities**
   - Architectural diagram generation
   - Cost optimization recommendations via AI
   - Security vulnerability prediction

3. **Enhanced Governance**
   - Real-time policy enforcement
   - Automated remediation workflows
   - Compliance dashboard with visualizations

4. **Improved Observability**
   - Application Insights integration
   - Distributed tracing
   - Real-time dashboards

5. **Plugin Marketplace**
   - Community-contributed plugins
   - Plugin discovery and installation
   - Versioning and compatibility checking

6. **Complete Option C Migration**
   - Refactor remaining 5 plugins to Option C pattern
   - Unified AI-powered interface across all domains

---

## Support & Documentation

### Additional Documentation

- **README.md** - Getting started guide
- **DEPLOYMENT.md** - Deployment instructions
- **DEVELOPMENT.md** - Development setup
- **DOCUMENTATION.md** - User guides
- **docs/** - 18+ refactoring and feature documentation files

### Key Documentation Files in `/docs`

- **OPTION-C-IMPLEMENTATION-COMPLETE.md** - Option C refactoring details
- **SEMANTIC-KERNEL-V2-MIGRATION.md** - SK migration guide
- **PHASE-9-PLUGIN-REFACTORING-PLAN.md** - Plugin refactoring roadmap
- **REFACTORING-ARCHITECTURE-DIAGRAM.md** - Visual architecture diagrams

### Contact & Contributing

- **Repository**: github.com/jrspinella/platform-mcp-supervisor
- **Issues**: Submit bugs and feature requests via GitHub Issues
- **Contributing**: See CONTRIBUTING.md for guidelines

---

## Appendix

### Project File Sizes (Estimated)

| Project | Lines of Code | Key Metrics |
|---------|---------------|-------------|
| Core | ~15,000 | 40+ interfaces, 8 plugins, 60+ services |
| Data | ~3,000 | 20+ entities, 50+ migrations |
| Governance | ~2,000 | 10+ services |
| DocumentProcessing | ~1,500 | Document AI integration |
| API | ~2,000 | REST endpoints |
| Chat.App | ~3,000 | Backend + React frontend |
| Admin.API | ~1,500 | Admin endpoints |
| Admin.Client | ~4,000 | React SPA |
| Mcp | ~500 | MCP server |
| **Total** | **~32,500** | Production code (excluding tests) |

### Version History

| Version | Date | Changes |
|---------|------|---------|
| 2.0 | October 9, 2025 | Namespace refactoring, Option C implementation, EF Core 9.0 upgrade |
| 1.5 | September 2024 | Plugin architecture refactoring |
| 1.0 | June 2024 | Initial release |

---

**Document Status**: ✅ Complete  
**Last Reviewed**: October 9, 2025  
**Next Review**: January 2026

