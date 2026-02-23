# Platform Engineering Copilot Documentation

**Version:** 3.1  
**Last Updated:** January 2026

---

## Documentation Overview

Welcome to the Platform Engineering Copilot documentation. This AI-powered platform helps teams manage Azure Government infrastructure, ensure NIST 800-53 compliance, and optimize cloud costs through natural language interactions.

### What is Platform Engineering Copilot?

An MCP-centric multi-agent platform built on .NET 9.0 that orchestrates **7 specialized AI agents** for:

- **Compliance**: NIST 800-53 assessments, FedRAMP, remediation, and ATO documentation
- **Infrastructure**: Bicep/Terraform generation, Azure provisioning, scaling analysis
- **Cost Management**: Cost analysis, optimization, budgets, and forecasting
- **Discovery**: Resource inventory, health monitoring, dependency mapping
- **Environments**: Platform Engineering templates, lifecycle management, drift detection
- **Knowledge Base**: Compliance education, NIST controls, STIG, RMF guidance
- **Configuration**: Azure subscription and settings management

---

## Quick Start

```bash
# 1. Clone and authenticate
git clone https://github.com/azurenoops/platform-engineering-copilot.git
cd platform-engineering-copilot
az cloud set --name AzureUSGovernment  # or AzureCloud
az login

# 2. Configure environment
cp .env.example .env
# Edit .env with Azure OpenAI credentials

# 3. Start MCP Server
docker-compose -f docker-compose.mcp.yml up -d
curl http://localhost:5100/health
```

**Service Ports:**
| Port | Service | Description |
|------|---------|-------------|
| 5100 | MCP Server | Main orchestration hub (HTTP + stdio) |
| 5001 | Chat UI | Web-based chat interface |
| 5050 | Admin API | RESTful administration API |
| 5000 | Admin Client | Blazor WebAssembly dashboard |

---

## Documentation Index

### Getting Started
| Document | Description |
|----------|-------------|
| [GETTING-STARTED.md](./GETTING-STARTED.md) | Quick start guide, prerequisites, first run |
| [AUTHENTICATION.md](./AUTHENTICATION.md) | Azure CLI, Managed Identity, CAC/PIV setup |

### Architecture & Design
| Document | Description |
|----------|-------------|
| [ARCHITECTURE.md](./ARCHITECTURE.md) | System architecture, BaseAgent/BaseTool patterns |
| [AGENTS.md](./AGENTS.md) | All 7 agents with complete tool catalogs |
| [DATABASE.md](./DATABASE.md) | Entity Framework schema, migrations |

### Deployment
| Document | Description |
|----------|-------------|
| [DEPLOYMENT.md](./DEPLOYMENT.md) | Docker, ACI, AKS deployment options |
| [DEVELOPMENT.md](./DEVELOPMENT.md) | Local development, contributing guidelines |
| [ENABLE-REAL-DEPLOYMENTS.md](./ENABLE-REAL-DEPLOYMENTS.md) | Enable live Azure provisioning |

### Azure Integration
| Document | Description |
|----------|-------------|
| [AZURE-ARC.md](./AZURE-ARC.md) | Hybrid infrastructure with Azure Arc |

### Testing
| Document | Description |
|----------|-------------|
| [test cases/AGENT-TEST-CASES.md](./test%20cases/AGENT-TEST-CASES.md) | Agent validation test scenarios |

---

## Architecture at a Glance

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLIENTS                                  │
│  ┌─────────────┐  ┌─────────────┐  ┌────────────────────────┐   │
│  │ Chat UI     │  │ Admin UI    │  │ AI Clients             │   │
│  │ :5001       │  │ :5000       │  │ (Copilot, Claude)      │   │
│  └──────┬──────┘  └──────┬──────┘  └───────────┬────────────┘   │
└─────────┼────────────────┼─────────────────────┼────────────────┘
          │ HTTP           │ HTTP                │ stdio
          ▼                ▼                     ▼
┌─────────────────────────────────────────────────────────────────┐
│                    MCP SERVER (:5100)                            │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │             PlatformAgentGroupChat                          ││
│  │  ├─ PlatformSelectionStrategy (intent routing)             ││
│  │  └─ 7 Specialized Agents                                    ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                  │
│  ┌───────────┐ ┌───────────┐ ┌───────────┐ ┌───────────┐       │
│  │Compliance │ │Infrastructure│ │   Cost  │ │ Discovery │       │
│  │  12 tools │ │   6 tools  │ │  6 tools │ │  9 tools  │       │
│  └───────────┘ └───────────┘ └───────────┘ └───────────┘       │
│  ┌───────────┐ ┌───────────┐ ┌───────────┐                     │
│  │Environment│ │KnowledgeBase│ │  Config  │                     │
│  │  10 tools │ │   8 tools  │ │  1 tool  │                     │
│  └───────────┘ └───────────┘ └───────────┘                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    AZURE SERVICES                                │
│  Azure Resource Manager │ Defender for Cloud │ Cost Management  │
│  Azure Policy │ Resource Graph │ Key Vault │ Azure Arc          │
└─────────────────────────────────────────────────────────────────┘
```

---

## Example Queries

**Compliance:**
```
"Run NIST 800-53 compliance scan"
"Start remediation for high-priority findings"
"Generate SSP document for my subscription"
```

**Infrastructure:**
```
"Generate Bicep for an AKS cluster in usgovvirginia"
"Create a storage account with private endpoints"
"Analyze scaling needs for my VMs"
```

**Cost:**
```
"Show cost analysis for last 30 days"
"What are my optimization opportunities?"
"Detect cost anomalies in my subscription"
```

**Discovery:**
```
"List all VMs in my subscription"
"Map dependencies for my web app"
"Show unhealthy resources"
```

---

## Configuration

All configuration is in `appsettings.json` at the repository root:

```json
{
  "AgentConfiguration": {
    "ComplianceAgent": { "Enabled": true, "Temperature": 0.2 },
    "InfrastructureAgent": { "Enabled": true, "Temperature": 0.4 },
    "CostManagementAgent": { "Enabled": true, "Temperature": 0.3 },
    "DiscoveryAgent": { "Enabled": true, "Temperature": 0.3 },
    "EnvironmentAgent": { "Enabled": true, "Temperature": 0.3 },
    "KnowledgeBaseAgent": { "Enabled": true, "Temperature": 0.2 }
  }
}
```

---

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Run tests: `dotnet test`
4. Submit a pull request

See [DEVELOPMENT.md](./DEVELOPMENT.md) for detailed guidelines.

