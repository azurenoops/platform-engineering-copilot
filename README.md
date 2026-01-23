# Platform Engineering Copilot

> **AI-Powered Infrastructure & Compliance Platform for Azure Government**

Built on .NET 9.0, and Model Context Protocol (MCP). Uses the **Microsoft Agent Framework** architecture pattern with 8+ specialized AI agents for infrastructure, compliance, cost management, and more.

---

## Quick Start

```bash
# Clone and build
git clone https://github.com/azurenoops/platform-engineering-copilot.git
cd platform-engineering-copilot
dotnet build

# Azure authentication
az cloud set --name AzureUSGovernment  # or AzureCloud
az login
export AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)

# Configure
cp .env.example .env
# Edit .env with Azure OpenAI and subscription details

# Run MCP server only (Docker)
docker-compose -f docker-compose.mcp.yml up -d
curl http://localhost:5100/health

# Run MCP + Chat UI
docker-compose -f docker-compose.mcp-chat.yml up -d
open http://localhost:5001

# Run full platform (MCP + Chat + Admin)
docker-compose -f docker-compose.mcp-chat-admin.yml up -d
open http://localhost:5000  # Admin Client
```

---

## Architecture

The platform uses **Microsoft Agent Framework** with `PlatformAgentGroupChat` for multi-agent orchestration.

```
┌─────────────────────────────────────────────────────────────────┐
│                    MCP SERVER (:5100)                            │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │              PlatformAgentGroupChat                         ││
│  │  ├─ PlatformSelectionStrategy (intent-based routing)       ││
│  │  ├─ PlatformTerminationStrategy                            ││
│  │  └─ 8 Specialized Agents                                    ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                  │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐               │
│  │ Compliance  │ │Infrastructure│ │    Cost     │               │
│  │   Agent     │ │    Agent    │ │   Agent     │               │
│  └─────────────┘ └─────────────┘ └─────────────┘               │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐               │
│  │  Discovery  │ │ Environment │ │Configuration│               │
│  │   Agent     │ │   Agent     │ │   Agent     │               │
│  └─────────────┘ └─────────────┘ └─────────────┘               │
│  ┌─────────────┐ ┌─────────────┐                               │
│  │ Knowledge   │ │  Security   │                               │
│  │ Base Agent  │ │   Agent     │                               │
│  └─────────────┘ └─────────────┘                               │
└─────────────────────────────────────────────────────────────────┘
        │                    │                    │
        ▼                    ▼                    ▼
┌─────────────┐      ┌─────────────┐      ┌─────────────┐
│ Chat UI     │      │ Admin API   │      │Admin Client │
│   :5001     │      │   :5050     │      │   :5000     │
└─────────────┘      └─────────────┘      └─────────────┘
```

### Service Ports

| Service | Port | Description |
|---------|------|-------------|
| **MCP Server** | 5100 | Dual-mode (HTTP + stdio) orchestration hub |
| **Chat UI** | 5001 | SignalR-based web chat interface |
| **Admin API** | 5050 | RESTful admin operations (Swagger) |
| **Admin Client** | 5000 | Blazor WebAssembly dashboard |

### Agents

| Agent | Domain | Key Capabilities |
|-------|--------|------------------|
| **Compliance** | Governance | NIST 800-53, FedRAMP, Defender for Cloud, remediation |
| **Infrastructure** | Provisioning | Azure resources, Bicep/Terraform generation |
| **Cost** | FinOps | Cost analysis, optimization, trend forecasting |
| **Discovery** | Inventory | Resource discovery, health, dependency mapping |
| **Environment** | Lifecycle | Environment provisioning, template management, Git sync |
| **Configuration** | Settings | Azure configuration, Key Vault, App Config |
| **KnowledgeBase** | Documentation | ATO docs, SSP generation, policy lookup |
| **Security** | Protection | Vulnerability scanning, secure score, policy |

---

## Example Queries

```
"Run NIST 800-53 compliance scan on my subscription"
"Create storage account data001 in rg-dr with encryption"
"Show cost analysis for last 30 days grouped by resource type"
"What's my secure score and top recommendations?"
"List all VMs in my subscription with their health status"
"Generate Bicep for an AKS cluster in usgovvirginia"
"Clone environment dev to staging"
"What are the FedRAMP High requirements for access control?"
```

---

## MCP Client Configuration

### GitHub Copilot

Create `~/.vscode/mcp.json`:
```json
{
  "mcpServers": {
    "platform-engineering-copilot": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/src/Platform.Engineering.Copilot.Mcp"]
    }
  }
}
```

### Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json`:
```json
{
  "mcpServers": {
    "platform-engineering-copilot": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/src/Platform.Engineering.Copilot.Mcp"]
    }
  }
}
```

---

## Project Structure

```
src/
├── Platform.Engineering.Copilot.Mcp/        # MCP Server (HTTP:5100 + stdio)
│   ├── Server/                              # HTTP bridge endpoints
│   ├── Tools/                               # MCP tool definitions
│   └── Prompts/                             # Agent system prompts
├── Platform.Engineering.Copilot.Agents/     # All agents (consolidated)
│   ├── Common/                              # Shared abstractions
│   ├── Orchestration/                       # PlatformAgentGroupChat, strategies
│   ├── Compliance/                          # Compliance Agent
│   ├── Infrastructure/                      # Infrastructure Agent
│   ├── CostManagement/                      # Cost Agent
│   ├── Discovery/                           # Discovery Agent
│   ├── Environments/                        # Environment Agent
│   ├── Configuration/                       # Configuration Agent
│   ├── KnowledgeBase/                       # Knowledge Base Agent
│   └── Extensions/                          # DI registration
├── Platform.Engineering.Copilot.Core/       # Shared core library
│   ├── Data/                                # EF Core context, migrations
│   ├── Services/                            # Azure SDK integrations
│   ├── Models/                              # Domain models
│   └── Interfaces/                          # Service contracts
├── Platform.Engineering.Copilot.State/      # State management
├── Platform.Engineering.Copilot.Channels/   # Communication channels
├── Platform.Engineering.Copilot.Chat/       # Web Chat UI (:5001)
├── Platform.Engineering.Copilot.Admin.API/  # Admin REST API (:5050)
└── Platform.Engineering.Copilot.Admin.Client/ # Blazor WASM (:5000)
```

---

## Docker Compose Profiles

| File | Services | Use Case |
|------|----------|----------|
| `docker-compose.mcp.yml` | MCP only | AI client development |
| `docker-compose.mcp-chat.yml` | MCP + Chat | Web chat interface |
| `docker-compose.mcp-admin.yml` | MCP + Admin | Admin dashboard |
| `docker-compose.mcp-chat-admin.yml` | Full platform | Production deployment |

---

## Infrastructure as Code (Bicep)

Refactored Bicep templates live under [infra/bicep](infra/bicep). They use modern `.bicepparam` files, typed parameters, and simplified orchestration.

Quick steps:

```bash
# Set cloud and authenticate
az cloud set --name AzureUSGovernment   # or AzureCloud
az login

# Dev: MCP + Admin
az deployment group create \
  --resource-group rg-pecop-dev \
  --parameters infra/bicep/main.dev.bicepparam \
  --parameters sqlAdminPassword='YourSecurePassword123!'

# MCP-only
az deployment group create \
  --resource-group rg-pecop-dev \
  --parameters infra/bicep/main.mcp-only.bicepparam \
  --parameters sqlAdminPassword='YourSecurePassword123!'

# Prod: all services
az deployment group create \
  --resource-group rg-pecop-prod \
  --parameters infra/bicep/main.prod.bicepparam \
  --parameters sqlAdminPassword='SetSecurelyFromKeyVaultOrPipeline'
```

More details and parameters: [infra/bicep/README.md](infra/bicep/README.md)

---

## Configuration

All configuration in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=platform_engineering_copilot_management.db"
  },
  "Gateway": {
    "AzureOpenAI": {
      "Endpoint": "https://your-openai.openai.azure.us/",
      "ApiKey": "<key>",
      "DeploymentName": "gpt-4o"
    }
  },
  "AgentConfiguration": {
    "ComplianceAgent": { "Enabled": true, "Temperature": 0.2 },
    "InfrastructureAgent": { "Enabled": true, "DefaultRegion": "usgovvirginia" }
  },
  "GitSync": {
    "AutoSyncEnabled": true,
    "DefaultSyncIntervalMinutes": 30
  }
}
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | System architecture, agent framework |
| [docs/AGENTS.md](docs/AGENTS.md) | All agents with capabilities |
| [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md) | Docker, ACI, AKS deployment |
| [docs/GETTING-STARTED.md](docs/GETTING-STARTED.md) | Complete setup guide |
| [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md) | Development guide, contributing |
| [docs/AUTHENTICATION.md](docs/AUTHENTICATION.md) | Azure authentication, CAC/PIV |
| [docs/AGENT-REMEDIATION-BOUNDARIES.md](docs/AGENT-REMEDIATION-BOUNDARIES.md) | Agent responsibility boundaries |

---

## Technology Stack

| Component | Technology |
|-----------|------------|
| Runtime | .NET 9.0 / C# 12 |
| AI Framework | Microsoft Semantic Kernel 1.26.0 |
| MCP | ModelContextProtocol 0.4.0-preview |
| Azure SDK | Azure.ResourceManager.* |
| Database | SQLite (default), SQL Server (optional) |
| Frontend | Blazor WebAssembly, ASP.NET Core Razor |
| Real-time | SignalR |

---

## Development

```bash
# Build
dotnet build Platform.Engineering.Copilot.sln

# Test
dotnet test Platform.Engineering.Copilot.sln

# Run MCP server (stdio mode for AI clients)
dotnet run --project src/Platform.Engineering.Copilot.Mcp

# Run MCP server (HTTP mode for web clients)
dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http

# Run Chat UI
dotnet run --project src/Platform.Engineering.Copilot.Chat --urls http://0.0.0.0:5001

# Run Admin services
dotnet run --project src/Platform.Engineering.Copilot.Admin.API --urls http://0.0.0.0:5050
dotnet run --project src/Platform.Engineering.Copilot.Admin.Client --urls http://0.0.0.0:5000
```

---

## License

MIT License - see [LICENSE](LICENSE)
