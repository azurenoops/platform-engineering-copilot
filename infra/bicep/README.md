# Bicep Infrastructure as Code

This directory contains Azure Bicep templates for deploying the Platform Engineering Copilot infrastructure.

## Deployment Profiles

The templates support 4 deployment profiles matching Docker Compose configurations:

| Profile | Services | Parameter File | Docker Compose |
|---------|----------|----------------|----------------|
| **MCP Only** | MCP Server | `main.parameters.mcp.json` | `docker-compose.mcp.yml` |
| **MCP + Chat** | MCP, Chat UI | `main.parameters.mcp-chat.json` | `docker-compose.mcp-chat.yml` |
| **MCP + Admin** | MCP, Admin API, Admin Client | `main.parameters.mcp-admin.json` | `docker-compose.mcp-admin.yml` |
| **Full Stack** | MCP, Chat, Admin API, Admin Client | `main.parameters.mcp-chat-admin.json` | `docker-compose.mcp-chat-admin.yml` |

## Service Port Mappings

| Service | Container Port | Description |
|---------|---------------|-------------|
| MCP Server | 5100 | AI Agent MCP Server |
| Chat UI | 5001 | Web Chat Interface |
| Admin API | 5050 | Admin REST API |
| Admin Client | 80 | Admin Blazor WASM UI (Nginx) |

## Directory Structure

```
bicep/
├── main.bicep                      # Main infrastructure template
├── main.parameters.json            # Base parameters template
├── main.parameters.mcp.json        # MCP-only deployment
├── main.parameters.mcp-chat.json   # MCP + Chat deployment
├── main.parameters.mcp-admin.json  # MCP + Admin deployment
├── main.parameters.mcp-chat-admin.json # Full stack deployment
├── main.parameters.aci.json        # ACI deployment (legacy)
├── main.parameters.aks.json        # AKS deployment
├── main.parameters.appservice.json # App Service deployment
├── modules/                        # Reusable Bicep modules
│   ├── aci.bicep                   # Azure Container Instances
│   ├── acr.bicep                   # Azure Container Registry
│   ├── aks.bicep                   # Azure Kubernetes Service
│   ├── app-services.bicep          # App Services
│   ├── keyvault.bicep              # Key Vault
│   ├── monitoring.bicep            # Application Insights
│   ├── network.bicep               # Virtual Network
│   ├── sql.bicep                   # Azure SQL
│   └── storage.bicep               # Storage Account
├── scripts/                        # Deployment scripts
│   ├── deploy-to-aci.sh           # Deploy to ACI
│   ├── deploy-to-azure.sh         # General deployment
│   └── update-mcp-auth.sh         # Update MCP authentication
└── README.md                       # This file
```

## Quick Start

### 1. MCP Only (AI Development)

Best for developing AI agents and MCP integrations:

```bash
az deployment group create \
  --resource-group rg-platform-engineering-dev \
  --template-file main.bicep \
  --parameters main.parameters.mcp.json \
  --parameters sqlAdminPassword='YourSecurePassword123!'
```

### 2. MCP + Chat (End User Demo)

Best for demonstrating AI chat capabilities:

```bash
az deployment group create \
  --resource-group rg-platform-engineering-dev \
  --template-file main.bicep \
  --parameters main.parameters.mcp-chat.json \
  --parameters sqlAdminPassword='YourSecurePassword123!'
```

### 3. MCP + Admin (Platform Administration)

Best for template management and platform configuration:

```bash
az deployment group create \
  --resource-group rg-platform-engineering-dev \
  --template-file main.bicep \
  --parameters main.parameters.mcp-admin.json \
  --parameters sqlAdminPassword='YourSecurePassword123!'
```

### 4. Full Stack (Production)

Complete platform deployment:

```bash
az deployment group create \
  --resource-group rg-platform-engineering-dev \
  --template-file main.bicep \
  --parameters main.parameters.mcp-chat-admin.json \
  --parameters sqlAdminPassword='YourSecurePassword123!'
```

## Deployment Targets

The templates support multiple deployment targets:

| Target | Use Case | Parameter |
|--------|----------|-----------|
| **ACI** | Development, simple deployments | `containerDeploymentTarget: aci` |
| **AKS** | Production, high availability | `containerDeploymentTarget: aks` |
| **App Service** | PaaS, managed services | `containerDeploymentTarget: appservice` |

## Git Sync Configuration

The MCP Server includes Git template synchronization with these environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `GitSync__AutoSyncEnabled` | Enable automatic sync | `true` |
| `GitSync__DefaultSyncIntervalMinutes` | Sync interval | `30` |
| `GitSync__GitHubToken` | GitHub personal access token | (from Key Vault) |
| `GitSync__AzureDevOpsToken` | Azure DevOps PAT | (from Key Vault) |

### Store Git Tokens in Key Vault

```bash
# Store GitHub token
az keyvault secret set \
  --vault-name kv-platform-engineering \
  --name github-token \
  --value "ghp_your_github_token"

# Store Azure DevOps token
az keyvault secret set \
  --vault-name kv-platform-engineering \
  --name azuredevops-token \
  --value "your_azuredevops_pat"
```

## Module Reference

### ACI Module (`modules/aci.bicep`)

Deploys Azure Container Instances with:
- Health probes (liveness/readiness)
- Managed identity for ACR pull
- VNet integration (optional)
- Environment variables
- Logging to Log Analytics

### AKS Module (`modules/aks.bicep`)

Deploys Azure Kubernetes Service with:
- Workload identity
- OIDC issuer
- ACR integration
- Auto-scaling
- Network policies

### App Services Module (`modules/app-services.bicep`)

Deploys App Service Plan and Web Apps with:
- VNet integration
- Private endpoints
- Managed identity
- Deployment slots

## Existing Resource Integration

The templates support using existing resources:

```json
{
  "useExistingNetwork": { "value": true },
  "existingVNetName": { "value": "vnet-existing" },
  "useExistingLogAnalytics": { "value": true },
  "existingLogAnalyticsWorkspaceName": { "value": "log-existing" },
  "useExistingKeyVault": { "value": true },
  "existingKeyVaultName": { "value": "kv-existing" }
}
```

## Outputs

The deployment outputs include:

| Output | Description |
|--------|-------------|
| `aciMcpFqdn` | MCP Container Instance FQDN |
| `aciChatFqdn` | Chat Container Instance FQDN |
| `aciAdminApiFqdn` | Admin API Container Instance FQDN |
| `aciAdminClientFqdn` | Admin Client Container Instance FQDN |
| `acrLoginServer` | Container Registry login server |
| `sqlServerFqdn` | SQL Server FQDN |
| `keyVaultUri` | Key Vault URI |
| `deploymentSummary` | Full deployment summary object |

## Troubleshooting

### Check ACI Status

```bash
az container show \
  --resource-group rg-platform-engineering-dev \
  --name aci-mcp-platform-engineering-dev \
  --query "{state:instanceView.state, events:containers[0].instanceView.events}"
```

### View Container Logs

```bash
az container logs \
  --resource-group rg-platform-engineering-dev \
  --name aci-mcp-platform-engineering-dev
```

### Restart Container

```bash
az container restart \
  --resource-group rg-platform-engineering-dev \
  --name aci-mcp-platform-engineering-dev
```
