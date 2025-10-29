# Platform Engineering Copilot - Architecture Documentation

**Last Updated:** October 29, 2025  
**Version:** 2.1 (MCP Server Release)  
**Namespace:** `Platform.Engineering.Copilot.*`

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [System Architecture](#system-architecture)
3. [MCP Server Architecture](#mcp-server-architecture)
4. [Multi-Agent Orchestration](#multi-agent-orchestration)
5. [Project Structure](#project-structure)
6. [Dependency Graph](#dependency-graph)
7. [Core Components](#core-components)
8. [Data Flow](#data-flow)
9. [Plugin Architecture](#plugin-architecture)
10. [Technology Stack](#technology-stack)
11. [Security & Governance](#security--governance)
12. [Development Guidelines](#development-guidelines)

---

## Executive Summary

The Platform Engineering Copilot is an AI-powered infrastructure provisioning and governance platform built on .NET 9.0 and Microsoft Semantic Kernel. The system is centered around a **Model Context Protocol (MCP) Server** that orchestrates 6 specialized AI agents to provide comprehensive cloud infrastructure management, compliance monitoring, cost optimization, and ATO preparation.

### Key Capabilities

- **Multi-Agent Orchestration**: 6 specialized AI agents working in concert
- **Dual-Mode MCP Server**: HTTP (for web apps) and stdio (for AI clients)
- **AI-Powered Infrastructure Provisioning**: Natural language to Azure resources
- **NIST 800-53 Rev 5 Compliance**: Complete control family implementation with gap analysis
- **Cost Intelligence**: Real-time analysis, optimization, and cost overview dashboards
- **Security & Policy Enforcement**: Azure Policy integration with vulnerability scanning
- **ATO Preparation**: Automated documentation generation and submission workflows
- **Multi-Cloud Support**: Azure (primary), AWS, GCP

### Architecture Philosophy

- **MCP-First Design**: Model Context Protocol server as the primary interface
- **Agent-Based Architecture**: Specialized agents for different domains
- **Domain-Driven Design**: Services organized by business capabilities
- **Data Layer Isolation**: No dependencies in Data project
- **AI-First Interfaces**: Natural language over rigid APIs
- **Plugin-Based Extensibility**: Semantic Kernel plugins for modularity
- **Dual-Mode Operation**: HTTP and stdio for maximum flexibility

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                         CLIENT LAYER                                │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────────┐  │
│  │Platform Chat │  │Admin Console │  │   AI Clients             │  │
│  │  (Web UI)    │  │  (Web UI)    │  │  • GitHub Copilot        │  │
│  │  :5001       │  │  :5003       │  │  • Claude Desktop        │  │
│  │              │  │              │  │  • Custom MCP Clients    │  │
│  └──────┬───────┘  └──────┬───────┘  └────────┬─────────────────┘  │
│         │                 │                    │                    │
│         │ HTTP            │ HTTP               │ stdio              │
│         │                 │                    │                    │
├─────────┴─────────────────┴────────────────────┴────────────────────┤
│                      MCP SERVER LAYER                               │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │            MCP Server (Port 5100)                            │  │
│  │          Platform.Engineering.Copilot.Mcp                    │  │
│  │                                                               │  │
│  │  ┌────────────────────────────────────────────────────────┐  │  │
│  │  │              Dual-Mode Operation                       │  │  │
│  │  │  ┌──────────────────┐    ┌──────────────────┐         │  │  │
│  │  │  │  HTTP Mode       │    │  stdio Mode      │         │  │  │
│  │  │  │  (Port 5100)     │    │  (stdin/stdout)  │         │  │  │
│  │  │  │  - REST API      │    │  - AI Tools      │         │  │  │
│  │  │  │  - Web Apps      │    │  - MCP Protocol  │         │  │  │
│  │  │  │  - Health Checks │    │  - Direct Exec   │         │  │  │
│  │  │  └──────────────────┘    └──────────────────┘         │  │  │
│  │  └────────────────────────────────────────────────────────┘  │  │
│  │                                                               │  │
│  │  ┌────────────────────────────────────────────────────────┐  │  │
│  │  │         Multi-Agent Orchestrator                       │  │  │
│  │  │                                                         │  │  │
│  │  │  ┌──────────────────────────────────────────────────┐  │  │  │
│  │  │  │  1. Infrastructure Agent                         │  │  │  │
│  │  │  │     • Resource provisioning & management         │  │  │  │
│  │  │  │     • IaC generation (Terraform, Bicep, K8s)     │  │  │  │
│  │  │  │     • Template validation & deployment           │  │  │  │
│  │  │  └──────────────────────────────────────────────────┘  │  │  │
│  │  │                                                         │  │  │
│  │  │  ┌──────────────────────────────────────────────────┐  │  │  │
│  │  │  │  2. Cost Optimization Agent                      │  │  │  │
│  │  │  │     • Cost analysis & dashboards                 │  │  │  │
│  │  │  │     • Budget management & forecasting            │  │  │  │
│  │  │  │     • Savings recommendations                    │  │  │  │
│  │  │  └──────────────────────────────────────────────────┘  │  │  │
│  │  │                                                         │  │  │
│  │  │  ┌──────────────────────────────────────────────────┐  │  │  │
│  │  │  │  3. Compliance Agent                             │  │  │  │
│  │  │  │     • NIST 800-53 Rev 5 assessment               │  │  │  │
│  │  │  │     • Gap analysis & remediation                 │  │  │  │
│  │  │  │     • FedRAMP baseline validation                │  │  │  │
│  │  │  └──────────────────────────────────────────────────┘  │  │  │
│  │  │                                                         │  │  │
│  │  │  ┌──────────────────────────────────────────────────┐  │  │  │
│  │  │  │  4. Security Agent                               │  │  │  │
│  │  │  │     • Security scanning & assessment             │  │  │  │
│  │  │  │     • Azure Policy integration                   │  │  │  │
│  │  │  │     • Vulnerability detection                    │  │  │  │
│  │  │  └──────────────────────────────────────────────────┘  │  │  │
│  │  │                                                         │  │  │
│  │  │  ┌──────────────────────────────────────────────────┐  │  │  │
│  │  │  │  5. Document Agent                               │  │  │  │
│  │  │  │     • ATO documentation generation               │  │  │  │
│  │  │  │     • Evidence collection & organization         │  │  │  │
│  │  │  │     • Template management                        │  │  │  │
│  │  │  └──────────────────────────────────────────────────┘  │  │  │
│  │  │                                                         │  │  │
│  │  │  ┌──────────────────────────────────────────────────┐  │  │  │
│  │  │  │  6. ATO Preparation Agent                        │  │  │  │
│  │  │  │     • End-to-end ATO orchestration               │  │  │  │
│  │  │  │     • Submission workflow management             │  │  │  │
│  │  │  │     • Stakeholder coordination                   │  │  │  │
│  │  │  └──────────────────────────────────────────────────┘  │  │  │
│  │  │                                                         │  │  │
│  │  └─────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                                                                     │
│  ┌──────────────────┐  ┌──────────────────┐                       │
│  │   Admin API      │  │  Admin Client    │                       │
│  │   :5002          │◄─┤  :5003           │                       │
│  └──────────────────┘  └──────────────────┘                       │
│                                                                     │
├─────────────────────────────────────────────────────────────────────┤
│                   BUSINESS LOGIC LAYER                              │
├─────────────────────────────────────────────────────────────────────┤
│                                                                     │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                          CORE                                 │  │
│  │          Platform.Engineering.Copilot.Core                    │  │
│  │                                                               │  │
│  │  ┌────────────────────────────────────────────────────────┐  │  │
│  │  │         Semantic Kernel Plugins                        │  │  │
│  │  │  • Infrastructure Plugin (Azure ARM)                   │  │  │
│  │  │  • Compliance Plugin (NIST 800-53)                     │  │  │
│  │  │  • Cost Management Plugin                             │  │  │
│  │  │  • Security Plugin (Policy & Scanning)                │  │  │
│  │  │  • Document Plugin (ATO Docs)                         │  │  │
│  │  └────────────────────────────────────────────────────────┘  │  │
│  │                                                               │  │
│  │  ┌────────────────────────────────────────────────────────┐  │  │
│  │  │         Domain Services                                │  │  │
│  │  │  • InfrastructureProvisioningService                   │  │  │
│  │  │  • RmfComplianceEngine (NIST 800-53)                   │  │  │
│  │  │  • CostOptimizationEngine                              │  │  │
│  │  │  • SecurityScanningService                             │  │  │
│  │  │  • AtoDocumentationService                             │  │  │
│  │  │  • Template Generation (Terraform, Bicep, K8s)         │  │  │
│  │  │  • Deployment Orchestration                            │  │  │
│  │  │  • Notification Services                               │  │  │
│  │  └────────────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────────────┘  │
│                                                                      │
├──────────────────────────────────────────────────────────────────────┤
│                      DATA ACCESS LAYER                               │
├──────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                          Data                                 │  │
│  │          Platform.Engineering.Copilot.Data                    │  │
│  │                                                               │  │
│  │  • Entity Framework Core 9.0                                 │  │
│  │  • Contexts: McpContext, ChatContext, AdminContext           │  │
│  │  • Databases: McpDb, ChatDb, AdminDb                         │  │
│  │  • Entities: Sessions, Agents, Templates, Assessments        │  │
│  │  • Migrations, Seeding, Factories                            │  │
│  │  • Supports: SQL Server, SQLite, In-Memory                   │  │
│  │                                                               │  │
│  │  **NO DEPENDENCIES** (Proper Isolation)                      │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
                                 ▼
┌──────────────────────────────────────────────────────────────────────┐
│                       EXTERNAL SERVICES                              │
├──────────────────────────────────────────────────────────────────────┤
│  • Azure Resource Manager (Azure Government/Commercial)              │
│  • Azure Policy Insights API                                         │
│  • Azure Cost Management API                                         │
│  • Azure OpenAI Service (gpt-4o deployment)                          │
│  • Azure Storage, Key Vault, SQL Database                            │
│  • NIST 800-53 OSCAL Content Repository                              │
└──────────────────────────────────────────────────────────────────────┘
```

---

## MCP Server Architecture

### Dual-Mode Operation

The MCP Server operates in two modes simultaneously, providing maximum flexibility for different client types:

```
┌──────────────────────────────────────────────────────┐
│              MCP Server Process                      │
│      Platform.Engineering.Copilot.Mcp                │
├──────────────────────────────────────────────────────┤
│                                                      │
│  ┌────────────────────────────────────────────────┐ │
│  │           HTTP Mode (Port 5100)                │ │
│  │                                                │ │
│  │  • REST API endpoints                          │ │
│  │  • Health checks (/health)                     │ │
│  │  • Chat endpoints (/api/chat/message)          │ │
│  │  • Agent status and metrics                    │ │
│  │  • Session management                          │ │
│  │                                                │ │
│  │  Connected Clients:                            │ │
│  │  - Platform Chat (Web UI)                      │ │
│  │  - Custom web applications                     │ │
│  │  - HTTP-based integrations                     │ │
│  └────────────────────────────────────────────────┘ │
│                                                      │
│  ┌────────────────────────────────────────────────┐ │
│  │           stdio Mode (stdin/stdout)            │ │
│  │                                                │ │
│  │  • Model Context Protocol                      │ │
│  │  • Direct tool execution                       │ │
│  │  • Streaming responses                         │ │
│  │  • Resource access                             │ │
│  │  • Prompt templates                            │ │
│  │                                                │ │
│  │  Connected Clients:                            │ │
│  │  - GitHub Copilot                              │ │
│  │  - Claude Desktop                              │ │
│  │  - Custom MCP clients                          │ │
│  │  - AI agent frameworks                         │ │
│  └────────────────────────────────────────────────┘ │
│                                                      │
│  ┌────────────────────────────────────────────────┐ │
│  │         Shared Agent Core                      │ │
│  │                                                │ │
│  │  All 6 agents available to both modes:         │ │
│  │  1. Infrastructure Agent                       │ │
│  │  2. Cost Optimization Agent                    │ │
│  │  3. Compliance Agent                           │ │
│  │  4. Security Agent                             │ │
│  │  5. Document Agent                             │ │
│  │  6. ATO Preparation Agent                      │ │
│  └────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────┘
```

### Agent Orchestration

The MCP Server coordinates multiple specialized agents, each responsible for a specific domain:

**1. Infrastructure Agent**
- **Purpose**: Azure resource provisioning and infrastructure as code
- **Capabilities**:
  - Create and manage Azure resources via ARM API
  - Generate Terraform, Bicep, and Kubernetes templates
  - Template validation and deployment
  - Resource discovery and inventory
  - Multi-cloud support (Azure primary, AWS/GCP secondary)
- **Key Services**: InfrastructureProvisioningService, TemplateGenerationService
- **Plugin**: InfrastructurePlugin

**2. Cost Optimization Agent**
- **Purpose**: Cost analysis, optimization, and budget management
- **Capabilities**:
  - **Cost Overview Dashboard**: Total spend, top services, daily trends
  - Real-time cost analysis with multi-dimensional breakdowns
  - Budget management and forecasting
  - Savings recommendations (right-sizing, reserved instances)
  - Cost anomaly detection
  - Resource utilization analysis
- **Key Services**: CostOptimizationEngine, AzureCostManagementService
- **Plugin**: CostManagementPlugin

**3. Compliance Agent**
- **Purpose**: NIST 800-53 Rev 5 compliance and RMF framework support
- **Capabilities**:
  - Complete NIST 800-53 Rev 5 control assessment (18 families, 1000+ controls)
  - **Gap Analysis**: Automated compliance gap identification
  - FedRAMP High/Moderate/Low baseline validation
  - Remediation plan generation with prioritized actions
  - POAM (Plan of Action & Milestones) creation
  - Risk assessment and scoring
  - Control mapping and evidence collection
- **Key Services**: RmfComplianceEngine, ComplianceAssessmentService
- **Plugin**: CompliancePlugin

**4. Security Agent**
- **Purpose**: Security scanning, policy enforcement, and vulnerability management
- **Capabilities**:
  - Security vulnerability scanning
  - Azure Policy evaluation and enforcement
  - Threat detection and mitigation
  - Security posture assessment
  - Compliance violation identification
  - Policy exception workflows
  - Security best practices validation
- **Key Services**: SecurityScanningService, AzurePolicyEngine
- **Plugin**: SecurityPlugin

**5. Document Agent**
- **Purpose**: ATO documentation generation and compliance artifact management
- **Capabilities**:
  - System Security Plan (SSP) generation
  - Security Assessment Report (SAR) creation
  - POAM documentation
  - Control Implementation Summary
  - Evidence collection and organization
  - Document versioning and templates
  - Artifact management
- **Key Services**: AtoDocumentationService, DocumentProcessingService
- **Plugin**: DocumentPlugin

**6. ATO Preparation Agent**
- **Purpose**: End-to-end ATO package orchestration and submission
- **Capabilities**:
  - ATO package orchestration
  - Submission workflow management
  - Stakeholder coordination
  - Timeline and milestone tracking
  - eMASS integration preparation
  - Cross-agent coordination for complete ATO packages
- **Key Services**: AtoPreparationService, WorkflowOrchestrationService
- **Plugin**: AtoPreparationPlugin

### Agent Communication

```
User Query → MCP Server → Intent Classification
                              ↓
                    ┌─────────┴──────────┐
                    │  Agent Router      │
                    └─────────┬──────────┘
                              ↓
              ┌───────────────┼───────────────┐
              │               │               │
              ▼               ▼               ▼
    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    │Infrastructure│  │  Compliance │  │    Cost     │
    │    Agent    │  │    Agent    │  │    Agent    │
    └─────────────┘  └─────────────┘  └─────────────┘
              │               │               │
              ▼               ▼               ▼
    ┌─────────────┐  ┌─────────────┐  ┌─────────────┐
    │  Security   │  │  Document   │  │     ATO     │
    │    Agent    │  │    Agent    │  │    Agent    │
    └─────────────┘  └─────────────┘  └─────────────┘
              │               │               │
              └───────────────┼───────────────┘
                              ↓
                     Unified Response
                              ↓
                    MCP Server → Client
```

**Agent Handoff Scenarios**:

1. **Infrastructure Provisioning + Compliance**:
   - User: "Create a NIST-compliant storage account"
   - Infrastructure Agent provisions resource
   - Compliance Agent validates against NIST controls
   - Security Agent checks Azure policies

2. **Cost Analysis + Recommendations**:
   - User: "Show cost overview and optimization opportunities"
   - Cost Optimization Agent provides cost dashboard
   - Infrastructure Agent identifies underutilized resources
   - Recommendations synthesized from both agents

3. **ATO Preparation + Documentation**:
   - User: "Prepare ATO package for production environment"
   - ATO Preparation Agent orchestrates overall workflow
   - Compliance Agent runs full assessment
   - Document Agent generates SSP, SAR, POAM
   - Security Agent provides security scan results

---

## Multi-Agent Orchestration

### Agent Lifecycle

```
┌─────────────────────────────────────────────────────┐
│              Agent Initialization                    │
├─────────────────────────────────────────────────────┤
│  1. Load agent configuration                         │
│  2. Initialize Semantic Kernel                       │
│  3. Register plugins and tools                       │
│  4. Establish Azure connections                      │
│  5. Load prompt templates                            │
│  6. Set up session management                        │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│              Request Processing                      │
├─────────────────────────────────────────────────────┤
│  1. Receive user query (HTTP or stdio)               │
│  2. Intent classification via Semantic Kernel        │
│  3. Route to appropriate agent(s)                    │
│  4. Execute agent-specific logic                     │
│  5. Invoke Azure APIs or services                    │
│  6. Aggregate results from multiple agents           │
│  7. Format response for client mode                  │
└─────────────────────────────────────────────────────┘
                       ↓
┌─────────────────────────────────────────────────────┐
│              Session Management                      │
├─────────────────────────────────────────────────────┤
│  • Context preservation across queries               │
│  • Conversation history tracking                     │
│  • Multi-turn dialogue support                       │
│  • Agent state persistence                           │
└─────────────────────────────────────────────────────┘
```

### Shared Context and Memory

All agents share a common context pool for seamless handoffs:

- **Session Context**: User identity, subscription, tenant
- **Conversation History**: Previous queries and responses
- **Agent State**: Current workflow stage, pending actions
- **Resource Context**: Azure resources being managed
- **Compliance State**: Current compliance posture
- **Cost Context**: Budget allocations, spending patterns

---

## Project Structure

### Overview

The platform consists of **6 main projects** organized into logical layers:

| Project | Type | Port | Purpose |
|---------|------|------|---------|
| **Platform.Engineering.Copilot.Data** | Library | - | EF Core, entities, migrations (NO DEPENDENCIES) |
| **Platform.Engineering.Copilot.Core** | Library | - | Business logic, plugins, services |
| **Platform.Engineering.Copilot.Mcp** | Console/Web | 5100 | MCP Server (HTTP + stdio modes) |
| **Platform.Engineering.Copilot.Chat** | Web + SPA | 5001 | Chat interface (connects to MCP) |
| **Platform.Engineering.Copilot.Admin.API** | Web API | 5002 | Admin backend API |
| **Platform.Engineering.Copilot.Admin.Client** | Web + SPA | 5003 | Admin console (React SPA) |
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
Level 2 (MCP Server):
├── Mcp ──→ Core ──→ Data
│
Level 3 (Web Applications):
├── Chat ──→ Core, Data (connects to MCP via HTTP)
├── Admin.API ──→ Core, Data
├── Admin.Client (NO PROJECT DEPENDENCIES - SPA only)

┌────────────────────────────────────────────────────────────────┐
│                     DETAILED DEPENDENCIES                       │
└────────────────────────────────────────────────────────────────┘

Admin.Client
  (NO PROJECT DEPENDENCIES)
  └── Connects to Admin.API via HTTP

Chat
  ├── Core
  ├── Data
  └── Connects to MCP Server via HTTP (http://platform-mcp:5100)

Admin.API
  ├── Core
  └── Data

Mcp (MCP Server)
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
3. **Core as Hub**: Core is the central library referenced by all services
4. **Clean Architecture**: Services depend on Core and Data, not on each other
5. **MCP Server as Primary Interface**: MCP Server is the primary orchestration layer
6. **HTTP Communication**: Chat and other web apps communicate with MCP via HTTP
7. **Minimal Dependencies**: Each project only references what it absolutely needs

### Communication Patterns

```
┌─────────────────────────────────────────────────────────┐
│               Inter-Service Communication                │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  Platform Chat ──HTTP──→ MCP Server (Port 5100)         │
│                                                         │
│  Admin Client ──HTTP──→ Admin API (Port 5002)           │
│                                                         │
│  GitHub Copilot ──stdio──→ MCP Server                   │
│                                                         │
│  Claude Desktop ──stdio──→ MCP Server                   │
│                                                         │
│  All Services ──→ SQL Server (Port 1433)                │
│    • McpDb (MCP Server data)                            │
│    • ChatDb (Chat sessions)                             │
│    • AdminDb (Admin data)                               │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

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
- **Microsoft.SemanticKernel** 1.26.0+ - AI orchestration
- **Azure SDK** (11 packages) - Azure resource management
  - Azure.ResourceManager.*
  - Azure.AI.OpenAI
  - Azure.Identity
  - Azure.CostManagement
  - Azure.PolicyInsights
- **Entity Framework Core** 9.0.0 - Data access
- **KubernetesClient** 15.0.1+ - K8s operations
- **Octokit** 15.1.0+ - GitHub API

**Key Services**:
- **InfrastructureProvisioningService**: Azure resource creation and management
- **RmfComplianceEngine**: NIST 800-53 Rev 5 compliance assessment
- **CostOptimizationEngine**: Cost analysis and optimization
- **SecurityScanningService**: Security vulnerability assessment
- **AtoDocumentationService**: ATO document generation
- **TemplateGenerationService**: IaC template creation
- **DeploymentOrchestrationService**: Multi-step deployment coordination

---

### 2. Platform.Engineering.Copilot.Mcp

**Purpose**: Model Context Protocol server providing dual-mode operation (HTTP + stdio) for AI-powered infrastructure management.

**Key Features**:
- **Dual-Mode Operation**: HTTP server and stdio MCP server in single process
- **Multi-Agent Orchestration**: Coordinates 6 specialized agents
- **Semantic Kernel Integration**: AI-powered query processing
- **Session Management**: Multi-turn conversation support
- **Health Monitoring**: Endpoint for service health checks

**Project Structure**:

```
Mcp/
├── Program.cs                 # Entry point, dual-mode initialization
├── Agents/                    # Agent implementations
│   ├── InfrastructureAgent.cs
│   ├── CostOptimizationAgent.cs
│   ├── ComplianceAgent.cs
│   ├── SecurityAgent.cs
│   ├── DocumentAgent.cs
│   └── AtoPreparationAgent.cs
├── Controllers/               # HTTP mode endpoints
│   ├── ChatController.cs      # /api/chat/*
│   └── HealthController.cs    # /health
├── Mcp/                       # stdio mode handlers
│   ├── McpServer.cs           # MCP protocol implementation
│   ├── Tools/                 # MCP tool definitions
│   └── Resources/             # MCP resource handlers
├── Services/
│   ├── AgentOrchestrator.cs   # Agent routing and coordination
│   ├── SessionManager.cs      # Conversation state management
│   └── IntentClassifier.cs    # Query intent determination
└── appsettings.json           # Configuration

```

**Startup Modes**:
```bash
# HTTP Mode (default for Docker)
dotnet run --http

# stdio Mode (for AI clients)
dotnet run

# Both modes (advanced)
dotnet run --dual-mode
```

**HTTP Endpoints**:
- `GET /health` - Health check
- `POST /api/chat/message` - Process user message
- `GET /api/chat/sessions` - List active sessions
- `DELETE /api/chat/sessions/{id}` - Clear session

**stdio MCP Tools**:
- `provision_infrastructure` - Create Azure resources
- `assess_compliance` - Run NIST compliance check
- `analyze_costs` - Get cost analysis
- `scan_security` - Security vulnerability scan
- `generate_documentation` - Create ATO docs
- `prepare_ato` - Orchestrate ATO package

---

### 3. Platform.Engineering.Copilot.Data

**Purpose**: Data access layer with Entity Framework Core, providing database contexts, entities, and migrations.

**Key Features**:
- **Executable Project**: Configured as console app for EF Core tooling (migrations, seeding)
- **Zero Dependencies**: Proper data layer isolation (no references to other projects)
- **Multi-Database Support**: SQL Server, SQLite, In-Memory
- **Multiple Contexts**: Separate contexts for MCP, Chat, and Admin data

**Key Directories**:

```
Data/
├── Context/
│   ├── McpContext.cs          # MCP Server data (agents, sessions, artifacts)
│   ├── ChatContext.cs         # Chat history and sessions
│   └── AdminContext.cs        # Admin and configuration data
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
| **AgentSession** | MCP agent sessions | SessionId, AgentType, Context, History |
| **InfrastructureArtifact** | Generated IaC artifacts | TemplateType, Content, Metadata |
| **ComplianceAssessment** | NIST 800-53 assessments | Score, Gaps, Findings, Recommendations |
| **CostAnalysis** | Cost analysis results | TotalCost, Breakdown, Trends, Recommendations |
| **SecurityScan** | Security scan results | Severity, Vulnerabilities, Remediation |
| **AtoDocument** | ATO documentation | DocumentType (SSP/SAR/POAM), Content, Version |
| **ChatSession** | Chat conversations | SessionId, Messages, Context |
| **EnvironmentTemplate** | Infrastructure templates | TemplateName, ResourceTypes, Configuration |
| **DeploymentRecord** | Deployment history | Status, ResourceGroup, DeployedBy, Timestamp |

**Database Separation**:
- **McpDb**: Agent sessions, artifacts, assessments, scans
- **ChatDb**: Chat sessions, message history
- **AdminDb**: Templates, deployments, configuration

**Technologies**:
- **Entity Framework Core** 9.0.0 (SqlServer, Sqlite, InMemory, Design, Tools)

---

### 4. Platform.Engineering.Copilot.Chat

**Purpose**: Web-based chat application that connects to MCP Server for AI-powered infrastructure conversations.

**Architecture**: ASP.NET Core backend + React TypeScript SPA

**Port**: 5001

**Connection**: Connects to MCP Server via HTTP (`http://platform-mcp:5100`)

**Features**:
- Real-time chat interface
- AI-powered infrastructure assistance
- Multi-agent capabilities via MCP
- Session persistence
- Conversation history

**Frontend Stack**:
- React 18.2.0+
- TypeScript 4.9.5+
- React Router 6.20.0+
- Axios 1.6.0+

**Backend Stack**:
- ASP.NET Core 9.0
- HTTP Client to MCP Server
- Session management
- Serilog 4.2.0 (Logging)
- EF Core 9.0.0

---

### 5. Platform.Engineering.Copilot.Admin.API

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

### 6. Platform.Engineering.Copilot.Admin.Client

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

### 7. Platform.Engineering.Copilot.Mcp

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

### MCP Server Query Processing Flow

```
User Query: "Create a NIST-compliant storage account and show me the cost analysis"
     │
     ▼ (via HTTP or stdio)
┌────────────────────────────────────────────────────────────────┐
│  MCP Server (Platform.Engineering.Copilot.Mcp)                 │
│  • Receives query in appropriate mode                          │
│  • HTTP: POST /api/chat/message                                │
│  • stdio: MCP protocol message                                 │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  AgentOrchestrator.ProcessQueryAsync                           │
│  • Intent classification via Semantic Kernel                   │
│  • Determines: Infrastructure + Compliance + Cost agents needed│
└────┬───────────────────────────────────────────────────────────┘
     │
     ├──────────────────┬──────────────────┬───────────────────┐
     ▼                  ▼                  ▼                   │
┌─────────────┐  ┌─────────────┐  ┌─────────────┐            │
│Infrastructure│  │ Compliance  │  │    Cost     │            │
│   Agent     │  │   Agent     │  │   Agent     │            │
└──────┬──────┘  └──────┬──────┘  └──────┬──────┘            │
       │                │                │                     │
       ▼                ▼                ▼                     │
┌────────────────────────────────────────────────────┐        │
│  Execute Agent-Specific Logic                      │        │
├────────────────────────────────────────────────────┤        │
│                                                    │        │
│  Infrastructure Agent:                             │        │
│  ┌──────────────────────────────────────────────┐ │        │
│  │ 1. Parse: "storage account with NIST config" │ │        │
│  │ 2. Generate: Bicep/ARM template              │ │        │
│  │ 3. Apply: Security defaults                  │ │        │
│  │ 4. Call: Azure.ResourceManager.Storage       │ │        │
│  │ 5. Return: Resource ID                       │ │        │
│  └──────────────────────────────────────────────┘ │        │
│                                                    │        │
│  Compliance Agent:                                 │        │
│  ┌──────────────────────────────────────────────┐ │        │
│  │ 1. Run: NIST 800-53 controls check           │ │        │
│  │ 2. Validate: Encryption, access controls     │ │        │
│  │ 3. Identify: Compliance gaps                 │ │        │
│  │ 4. Generate: Remediation plan                │ │        │
│  │ 5. Return: Compliance report                 │ │        │
│  └──────────────────────────────────────────────┘ │        │
│                                                    │        │
│  Cost Optimization Agent:                          │        │
│  ┌──────────────────────────────────────────────┐ │        │
│  │ 1. Call: Azure Cost Management API           │ │        │
│  │ 2. Calculate: Estimated monthly cost         │ │        │
│  │ 3. Analyze: Similar resource costs           │ │        │
│  │ 4. Recommend: Cost optimizations             │ │        │
│  │ 5. Return: Cost dashboard                    │ │        │
│  └──────────────────────────────────────────────┘ │        │
└────────────────────────────────────────────────────┘        │
       │                │                │                     │
       └────────────────┴────────────────┴─────────────────────┘
                                ▼
┌────────────────────────────────────────────────────────────────┐
│  AgentOrchestrator.AggregateResults                            │
│  • Synthesize responses from all agents                        │
│  • Format unified response                                     │
│  • Include: Resource details, compliance status, cost info     │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  MCP Server Response                                           │
│  • HTTP mode: JSON response                                    │
│  • stdio mode: MCP protocol response                           │
│  • Includes: All agent results, session context               │
└────────────────────────────────────────────────────────────────┘
```

### Infrastructure Provisioning Flow

```
User: "Create storage account data001 in rg-dr in usgovvirginia"
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Infrastructure Agent (via MCP Server)                         │
│  • Parses natural language query                               │
│  • Extracts: resource type, name, location, resource group     │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  InfrastructureProvisioningService                             │
│  • Validates Azure subscription access                         │
│  • Checks if resource group exists                             │
│  • If not exists: Creates resource group with tags             │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  InfrastructurePlugin (Semantic Kernel)                        │
│  • Function: provision_infrastructure                          │
│  • AI extracts structured parameters                           │
│  • Applies security defaults:                                  │
│    - HTTPS-only traffic                                        │
│    - TLS 1.2 minimum                                           │
│    - Encryption at rest enabled                                │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Azure.ResourceManager.Storage                                 │
│  • Creates storage account in Azure Government                 │
│  • Waits for provisioning completion                           │
│  • Returns: Resource ID, provisioning status                   │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  DeploymentRecord saved to McpDb                               │
│  • Status: Completed                                           │
│  • Resource ID, deployment timestamp                           │
│  • Deployed by user information                                │
└────────────────────────────────────────────────────────────────┘
```

### Compliance Assessment Flow

```
User: "Run NIST 800-53 compliance assessment"
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Compliance Agent (via MCP Server)                             │
│  • Routes to RmfComplianceEngine                               │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  RmfComplianceEngine.AssessSubscriptionAsync                   │
│  • Loads NIST 800-53 Rev 5 controls from OSCAL repository      │
│  • 18 control families, 1000+ controls                         │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  For each control family:                                      │
│  • AC (Access Control) - 25 controls                           │
│  • AU (Audit and Accountability) - 16 controls                 │
│  • CM (Configuration Management) - 14 controls                 │
│  • ... (15 more families)                                      │
│                                                                │
│  For each control:                                             │
│  1. Evaluate against Azure resources                           │
│  2. Check Azure Policy compliance                              │
│  3. Identify gaps and missing controls                         │
│  4. Generate remediation recommendations                       │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  ComplianceAssessment saved to McpDb                           │
│  • Overall compliance score (e.g., 75.3%)                      │
│  • Critical/High/Medium/Low findings                           │
│  • Gap analysis with missing controls                          │
│  • Remediation roadmap with priorities                         │
│  • POAM generation                                             │
└────────────────────────────────────────────────────────────────┘
```

### Cost Analysis Flow

```
User: "Show cost overview for last 30 days"
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Cost Optimization Agent (via MCP Server)                      │
│  • Routes to CostOptimizationEngine                            │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  CostManagementPlugin.get_cost_dashboard                       │
│  • Calls Azure Cost Management API                             │
│  • Retrieves cost data for date range                          │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  Cost Dashboard Generated:                                     │
│  • Total monthly spend: $12,345.67                             │
│  • Month-over-month change: +5.2%                              │
│  • Top 5 services by cost:                                     │
│    1. Virtual Machines: $4,567 (37%)                           │
│    2. Storage: $2,345 (19%)                                    │
│    3. SQL Database: $1,890 (15%)                               │
│    4. App Services: $1,234 (10%)                               │
│    5. Key Vault: $876 (7%)                                     │
│  • Daily cost trends (chart data)                              │
│  • Breakdowns by: resource group, location, tags               │
│                                                                │
│  Optimization Recommendations:                                 │
│  • Right-size VM instances: Save $890/month                    │
│  • Reserved instances for SQL: Save $450/month                 │
│  • Delete idle resources: Save $230/month                      │
└────┬───────────────────────────────────────────────────────────┘
     │
     ▼
┌────────────────────────────────────────────────────────────────┐
│  CostAnalysis saved to McpDb                                   │
│  • Total cost, breakdowns, trends                              │
│  • Recommendations with savings estimates                      │
└────────────────────────────────────────────────────────────────┘
```

---

## Plugin Architecture

### Semantic Kernel Plugins

The platform uses **Semantic Kernel plugins exclusively** for AI-powered operations. All legacy plugin systems (IPlugin, IMcpToolHandler) have been removed in favor of Semantic Kernel's native plugin architecture.

#### Plugin System Modernization (Oct 11, 2025)

**Removed**:
- ❌ `IPlugin`, `IToolPlugin`, `IResourcePlugin`, `IGatewayPlugin` interfaces
- ❌ `IMcpToolHandler`, `IMcpResourceHandler` interfaces
- ❌ `PlatformToolService` (obsolete)
- ❌ `Contracts/` directory (empty after cleanup)

**Current Architecture**:
- ✅ All plugins inherit from `BaseSupervisorPlugin`
- ✅ Registered via `IntelligentChatService.EnsurePluginsRegistered()`
- ✅ Use Semantic Kernel's `[KernelFunction]` and `[Description]` attributes
- ✅ Support automatic function calling via Azure OpenAI

#### Active Plugin Inventory

| Plugin | Functions | Pattern | Status |
|--------|-----------|---------|--------|
| **BaseSupervisorPlugin** | N/A | Base class | ✅ Base |
| **CompliancePlugin** | 1 (`process_compliance_query`) | Option C | ✅ Refactored |
| **InfrastructurePlugin** | 4 | Option C | ✅ Refactored |
| **OnboardingPlugin** | Multiple | Legacy | ✅ Active |
| **CostManagementPlugin** | Multiple | Legacy | ✅ Active |
| **EnvironmentManagementPlugin** | Multiple | Legacy | ✅ Active |
| **ResourceDiscoveryPlugin** | Multiple | Legacy | ✅ Active |
| **DocumentPlugin** | 5 | Direct Service | ✅ Active |

**Note**: SecurityPlugin was removed (stub with no implementation).

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

2. **Add DbSet** to `PlatformEngineeringCopilotContext`
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

### Recent Refactoring (October 29, 2025)

1. **MCP-Centric Architecture**: Platform.API (port 7001) removed; MCP Server (port 5100) is now primary interface
2. **Multi-Agent Orchestration**: 6 specialized AI agents (Infrastructure, Cost Optimization, Compliance, Security, Document, ATO Preparation)
3. **Dual-Mode MCP Server**: Supports both HTTP (for web apps) and stdio (for AI clients like GitHub Copilot, Claude Desktop)
4. **Database Separation**: McpDb, ChatDb, AdminDb for clearer domain boundaries
5. **Docker Compose Configurations**: Multiple deployment options (essentials, full, dev, prod)
6. **Gap Analysis Feature**: Compliance Agent identifies missing NIST controls and generates remediation plans
7. **Cost Overview Dashboard**: Cost Optimization Agent provides real-time cost analysis and optimization recommendations

### Breaking Changes

- **Platform API Removed**: All functionality migrated to MCP Server (port 5100)
- **Port Changes**: Chat moved from 3000 to 5001, Admin from default to 5002/5003
- **Database Names**: SupervisorPlatformDb → McpDb
- **Network Name**: supervisor-network → plaform-engineering-copilot-network
- **Connection Strings**: Update Chat and Admin to connect to MCP Server instead of Platform API

---

## Future Roadmap

### Planned Features

1. **Enhanced Multi-Agent Capabilities**
   - Agent learning and adaptation based on user feedback
   - Cross-agent collaboration for complex workflows
   - Customizable agent personas and expertise domains

2. **Advanced Compliance Features**
   - Real-time compliance monitoring dashboard
   - Automated POAM generation and tracking
   - FedRAMP package generation
   - Continuous ATO support

3. **Cost Intelligence**
   - Predictive cost modeling via AI
   - Automated cost anomaly detection
   - Cost allocation and chargeback reporting
   - Multi-cloud cost comparison

4. **Security Enhancements**
   - Zero Trust architecture validation
   - Automated security remediation workflows
   - Threat modeling and risk assessment
   - Security posture scoring

5. **Developer Experience**
   - VS Code extension for MCP integration
   - GitHub Copilot advanced prompt examples
   - Natural language IaC generation improvements
   - Interactive compliance guidance in IDEs

6. **Multi-Cloud Support**
   - AWS provisioning via Terraform
   - GCP support for hybrid scenarios
   - Multi-cloud cost comparison

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
| Core | ~15,000 | 40+ interfaces, 8 plugins, 60+ services (includes governance & document processing) |
| Data | ~3,000 | 20+ entities, 50+ migrations |
| API | ~2,000 | REST endpoints |
| Chat | ~3,000 | Backend + React frontend |
| Admin.API | ~1,500 | Admin endpoints |
| Admin.Client | ~4,000 | React SPA |
| Mcp | ~500 | MCP server |
| **Total** | **~29,000** | Production code (excluding tests) |

### Version History

| Version | Date | Changes |
|---------|------|---------|
| 2.1 | October 29, 2025 | MCP-centric architecture, 6 AI agents, dual-mode MCP Server, Gap Analysis, Cost Overview Dashboard |
| 2.0 | October 9, 2025 | Namespace refactoring, Option C implementation, EF Core 9.0 upgrade |
| 1.5 | September 2024 | Plugin architecture refactoring |
| 1.0 | June 2024 | Initial release |

---

**Document Status**: ✅ Complete  
**Last Reviewed**: October 29, 2025  
**Next Review**: January 2026

