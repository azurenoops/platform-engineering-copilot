# Platform Engineering Copilot - Deployment Guide

**Version:** 3.0  
**Last Updated:** January 2026

---

## Overview

The Platform Engineering Copilot can be deployed in three modes:

| Mode | Use Case | Command |
|------|----------|---------|
| **Local** | Development | `dotnet run` |
| **Docker** | Quick start, testing | `docker-compose up` |
| **ACI** | Azure container deployment | Bicep templates |
| **AKS** | Production Kubernetes | Helm charts |

---

## Quick Start (Docker)

### Prerequisites

- Docker & Docker Compose
- Azure CLI (authenticated)
- Azure OpenAI endpoint

### MCP Server Only (Recommended for AI Clients)

```bash
# Start MCP server
docker-compose -f docker-compose.mcp.yml up -d

# Verify
curl http://localhost:5100/health
```

### MCP + Chat (End User Demo)

```bash
# Start MCP + Chat
docker-compose -f docker-compose.mcp-chat.yml up -d

# Access
open http://localhost:5001   # Chat UI
curl http://localhost:5100/health  # MCP Server
```

### MCP + Admin (Platform Administration)

```bash
# Start MCP + Admin
docker-compose -f docker-compose.mcp-admin.yml up -d

# Access
open http://localhost:5000   # Admin Client
curl http://localhost:5050/health  # Admin API
curl http://localhost:5100/health  # MCP Server
```

### Full Platform (All Services)

```bash
# Start all services
docker-compose -f docker-compose.mcp-chat-admin.yml up -d

# Access
open http://localhost:5001   # Chat UI
open http://localhost:5000   # Admin Client
curl http://localhost:5100/health  # MCP Server
```

### Configuration

Copy and configure environment:

```bash
cp .env.example .env
# Edit .env with:
# - AZURE_OPENAI_ENDPOINT
# - AZURE_OPENAI_API_KEY
# - AZURE_TENANT_ID
# - AZURE_SUBSCRIPTION_ID
```

---

## Local Development

```bash
# Build
dotnet build

# Azure authentication
az cloud set --name AzureUSGovernment  # or AzureCloud
az login
export AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)

# Run MCP server (stdio mode for AI clients)
dotnet run --project src/Platform.Engineering.Copilot.Mcp

# Run MCP server (HTTP mode for web apps)
dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http
```

---

## Docker Compose Files

| File | Services | Use Case |
|------|----------|----------|
| `docker-compose.mcp.yml` | MCP, SQL | AI client development |
| `docker-compose.mcp-chat.yml` | MCP, Chat, SQL | End user demo |
| `docker-compose.mcp-admin.yml` | MCP, Admin API, Admin Client, SQL | Platform administration |
| `docker-compose.mcp-chat-admin.yml` | MCP, Chat, Admin API, Admin Client, SQL | Full platform |

### Service Ports

| Service | Port | Description |
|---------|------|-------------|
| MCP Server | 5100 | Model Context Protocol server |
| Chat UI | 5001 | Web chat interface |
| Admin API | 5050 | Admin REST API |
| Admin Client | 5000 | Admin Blazor WASM UI (Nginx) |

---

## Build Container Images

```bash
# Create buildx builder
docker buildx create --name platform-builder --use --bootstrap

# Build images
docker buildx build --load -t platform-engineering-copilot-mcp:latest \
  -f src/Platform.Engineering.Copilot.Mcp/Dockerfile .

docker buildx build --load -t platform-engineering-copilot-chat:latest \
  -f src/Platform.Engineering.Copilot.Chat/Dockerfile .
```

---

## Azure Container Instances (ACI)

### Push to ACR

```bash
ACR_NAME="your-acr-name"

# Login and push
az acr login --name $ACR_NAME
docker tag platform-engineering-copilot-mcp:latest \
  ${ACR_NAME}.azurecr.io/platform-engineering-copilot-mcp:latest
docker push ${ACR_NAME}.azurecr.io/platform-engineering-copilot-mcp:latest
```

### Deploy with Bicep

Choose a deployment profile matching your needs:

| Profile | Parameter File | Services |
|---------|----------------|----------|
| MCP Only | `main.parameters.mcp.json` | MCP |
| MCP + Chat | `main.parameters.mcp-chat.json` | MCP, Chat |
| MCP + Admin | `main.parameters.mcp-admin.json` | MCP, Admin API, Admin Client |
| Full Stack | `main.parameters.mcp-chat-admin.json` | All services |

```bash
# Deploy MCP-only (recommended for AI development)
az deployment group create \
  --resource-group rg-platform-engineering-dev \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.mcp.json \
  --parameters sqlAdminPassword='YourSecurePassword123!'

# Deploy full stack
az deployment group create \
  --resource-group rg-platform-engineering-dev \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.mcp-chat-admin.json \
  --parameters sqlAdminPassword='YourSecurePassword123!'
```

### ACI Environment Variables

Set in Azure Portal or Bicep:

```
AZURE_OPENAI_ENDPOINT=https://your-openai.openai.azure.us
AZURE_OPENAI_API_KEY=your-key
AZURE_TENANT_ID=your-tenant-id
AZURE_SUBSCRIPTION_ID=your-subscription-id

# Git Sync Configuration (for template syncing)
GitSync__AutoSyncEnabled=true
GitSync__DefaultSyncIntervalMinutes=30
GitSync__GitHubToken=ghp_your_token
GitSync__AzureDevOpsToken=your_pat
```

---

## Azure Kubernetes Service (AKS)

### Deploy AKS Infrastructure

```bash
# Deploy AKS cluster
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters infra/bicep/main.parameters.aks.json \
  --parameters environment=prod containerDeploymentTarget=aks

# Get credentials
az aks get-credentials \
  --resource-group rg-platform-engineering-prod \
  --name aks-platform-engineering-prod
```

### Deploy Application

```bash
# Apply Kubernetes manifests
kubectl apply -f infra/kubernetes/

# Or use Helm
helm install platform-engineering ./infra/helm/platform-engineering
```

### Kubernetes Resources

```
infra/kubernetes/
├── namespace.yaml
├── configmap.yaml
├── secrets.yaml
├── mcp-deployment.yaml
├── mcp-service.yaml
├── chat-deployment.yaml
├── chat-service.yaml
├── ingress.yaml
└── hpa.yaml
```

---

## MCP Client Configuration

### GitHub Copilot

Create `~/.copilot/config.json`:

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

Create `~/Library/Application Support/Claude/claude_desktop_config.json`:

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

### Docker Mode (HTTP)

For containerized deployments:

```json
{
  "mcpServers": {
    "platform-engineering-copilot": {
      "url": "http://localhost:5100"
    }
  }
}
```

---

## Health Checks

```bash
# MCP Server
curl http://localhost:5100/health

# Chat UI
curl http://localhost:5001/health

# Admin API
curl http://localhost:5050/health

# Admin Client (static - just check it loads)
curl http://localhost:5000/
```

---

## Troubleshooting

### Container Logs

```bash
# Docker Compose
docker-compose logs -f mcp

# ACI
az container logs --resource-group rg-platform-engineering --name aci-mcp

# AKS
kubectl logs -f deployment/mcp-deployment
```

### Common Issues

| Issue | Solution |
|-------|----------|
| Auth failure | Run `az login` and set `AZURE_TENANT_ID` |
| OpenAI timeout | Verify `AZURE_OPENAI_ENDPOINT` is correct |
| Port conflict | Change port mapping in docker-compose.yml |
| Build cache errors | Add `--no-cache` to docker build |

---

## Related Documentation

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [GETTING-STARTED.md](./GETTING-STARTED.md) - Quick start
- [DOCKER-COMPOSE-GUIDE.md](../DOCKER-COMPOSE-GUIDE.md) - Docker details
