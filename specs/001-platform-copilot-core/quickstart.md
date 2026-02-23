# Quickstart: Build Platform Copilot Core

**Branch**: `001-platform-copilot-core` | **Date**: 2026-02-22

## Prerequisites

| Requirement | Version | Notes |
|-------------|---------|-------|
| .NET SDK | 9.0+ | `dotnet --version` |
| Docker + Compose | Latest | For SQL Server (Azure SQL Edge) |
| Azure CLI | 2.60+ | `az --version` |
| Azure subscription | — | Gov or Commercial with Defender for Cloud |
| Node.js | 20+ | For Blazor WASM tooling (optional) |

## Environment Setup

### 1. Clone and Branch

```bash
git clone <repo-url>
cd platform-engineering-copilot-v2
git checkout 001-platform-copilot-core
```

### 2. Configure Environment

```bash
cp .env.example .env
```

Edit `.env` with your values:

```env
AZURE_SUBSCRIPTION_ID=<your-subscription-id>
AZURE_TENANT_ID=<your-tenant-id>
AZURE_OPENAI_API_KEY=<your-api-key>
AZURE_OPENAI_ENDPOINT=<your-endpoint>
```

### 3. Start Database

```bash
docker compose -f docker-compose.mcp.yml up sql -d
```

This starts Azure SQL Edge on port 1433. The database auto-migrates on first run via `DatabaseInitializationService`.

**SQLite alternative** (no Docker):  
Set `DatabaseProvider: "Sqlite"` in `appsettings.Development.json`.

### 4. Azure Login (Dev Mode)

```bash
az login                          # Commercial
az login --use-device-code        # Government (if needed)
az account set --subscription <id>
```

This enables `DefaultAzureCredential` for local development (CAC/PIM bypass mode).

## Build & Run

### Build Solution

```bash
dotnet restore Platform.Engineering.Copilot.sln
dotnet build Platform.Engineering.Copilot.sln
```

### Run Tests

```bash
# All tests
dotnet test Platform.Engineering.Copilot.sln

# Unit tests only
dotnet test tests/Platform.Engineering.Copilot.Tests.Unit/

# Integration tests (requires SQL)
dotnet test tests/Platform.Engineering.Copilot.Tests.Integration/
```

### Run Individual Services

```bash
# MCP Server (HTTP mode, port 5100)
dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http

# MCP Server (stdio mode, for AI clients)
dotnet run --project src/Platform.Engineering.Copilot.Mcp

# Chat UI (port 5001)
dotnet run --project src/Platform.Engineering.Copilot.Chat

# Admin API (port 5050)
dotnet run --project src/Platform.Engineering.Copilot.Admin.API
```

### Run Full Stack via Docker

```bash
# MCP + Chat + SQL
docker compose -f docker-compose.mcp-chat.yml up --build

# Full stack (MCP + Chat + Admin API + Admin Client + SQL)
docker compose -f docker-compose.mcp-chat-admin.yml up --build
```

## Service Ports

| Service | Port | URL |
|---------|------|-----|
| MCP Server | 5100 | http://localhost:5100 |
| Chat UI | 5001 | http://localhost:5001 |
| Admin Dashboard | 5000 | http://localhost:5000 |
| Admin API | 5050 | http://localhost:5050/swagger |
| SQL Server | 1433 | — |
| SignalR Hub | 5001 | ws://localhost:5001/chathub |

## Verify Setup

### 1. Health Check

```bash
curl http://localhost:5100/health
```

Expected: JSON with `"status": "Healthy"` and per-agent status.

### 2. List MCP Tools

```bash
curl -X POST http://localhost:5100 \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc": "2.0", "method": "tools/list", "id": 1}'
```

Expected: List of 52 tools with auth metadata.

### 3. Knowledge Base Query (no auth)

```bash
curl -X POST http://localhost:5100 \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "method": "tools/call",
    "params": {
      "name": "explain_control",
      "arguments": { "controlId": "AC-2" }
    },
    "id": 2
  }'
```

Expected: Plain-language explanation of NIST AC-2 with Azure service mappings.

### 4. Chat UI

Open http://localhost:5001 and type:

```
What are the FedRAMP High requirements for access control?
```

Expected: Streaming Markdown response from Knowledge Base Agent.

## Dev Bypass Mode

For local development without CAC/PIM hardware, ensure `appsettings.Development.json` contains:

```json
{
  "Authentication": {
    "RequireCac": false,
    "RequirePim": false
  }
}
```

This uses Azure CLI credentials (`DefaultAzureCredential`) for all Azure operations while preserving the auth enforcement flow in code.

## Project Structure Overview

```
Platform.Engineering.Copilot.sln
├── src/
│   ├── Platform.Engineering.Copilot.Core/         # Shared: BaseAgent, BaseTool, EF Core, Auth
│   ├── Platform.Engineering.Copilot.Agents/       # 8 agents + Orchestrator
│   ├── Platform.Engineering.Copilot.Mcp/          # MCP Server (HTTP + stdio)
│   ├── Platform.Engineering.Copilot.Chat/         # Chat UI + SignalR
│   ├── Platform.Engineering.Copilot.Admin.API/    # Admin REST API
│   ├── Platform.Engineering.Copilot.Admin.Client/ # Blazor WASM Dashboard
│   ├── Platform.Engineering.Copilot.State/        # State management
│   └── Platform.Engineering.Copilot.Channels/     # Extension channels (scaffold)
└── tests/
    ├── Platform.Engineering.Copilot.Tests.Unit/
    ├── Platform.Engineering.Copilot.Tests.Integration/
    └── Platform.Engineering.Copilot.Tests.Manual/
```

## Key Documentation

| Document | Path | Purpose |
|----------|------|---------|
| Architecture | `docs/ARCHITECTURE.md` | System design, project relationships |
| Agents | `docs/AGENTS.md` | Agent catalog, tool definitions |
| Authentication | `docs/AUTHENTICATION.md` | CAC/PIM flow, Azure AD config |
| Database | `docs/DATABASE.md` | Schema, migrations, contexts |
| Development | `docs/DEVELOPMENT.md` | Build, test, code standards |
| Deployment | `docs/DEPLOYMENT.md` | Docker, ACI, AKS deployment |
