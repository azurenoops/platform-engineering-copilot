# Bicep Infrastructure (Refactored)

This folder contains the Azure Bicep IaC for Platform Engineering Copilot. It was recently refactored to simplify parameters, adopt `.bicepparam` files, and add type safety.

## What Changed
- New typed model in [types/main.bicep](infra/bicep/types/main.bicep) (e.g., `containerDeploymentTarget`, `sqlDatabaseSku`).
- Main orchestrator simplified in [main.bicep](infra/bicep/main.bicep) with clearer params and conditional modules.
- Modern parameter files: `.bicepparam` now used instead of JSON.
- ACR module updated in [modules/acr.bicep](infra/bicep/modules/acr.bicep) to remove deprecated properties and linter warnings.

## Current Layout

```
bicep/
├── main.bicep                     # Orchestrates SQL, KV, Storage, Monitoring, Network, ACR, ACI/AppSvc
├── main.dev.bicepparam            # Dev profile (MCP + Admin)
├── main.prod.bicepparam           # Prod profile (all services)
├── main.mcp-only.bicepparam       # Minimal MCP-only profile
├── modules/
│   ├── aci.bicep                  # Azure Container Instances
│   ├── acr.bicep                  # Azure Container Registry
│   ├── app-services.bicep         # Azure App Services (alternative target)
│   ├── keyvault.bicep             # Azure Key Vault
│   ├── monitoring.bicep           # Log Analytics + App Insights
│   ├── network.bicep              # VNet + subnets
│   ├── sql.bicep                  # Azure SQL Server + DB
│   └── storage.bicep              # Storage Account
└── types/
    └── main.bicep                 # User-defined types used by main.bicep
```

## Services and Ports
- MCP Server: 5100
- Chat UI: 5001
- Admin API: 5050
- Admin Client: 80

## Quick Deploy

Set your Azure cloud and login first (Government vs Commercial):

```bash
# Azure Government
az cloud set --name AzureUSGovernment
az login

# or Azure Commercial
az cloud set --name AzureCloud
az login
```

### Dev (MCP + Admin)

```bash
az deployment group create \
  --resource-group rg-pecop-dev \
  --parameters infra/bicep/main.dev.bicepparam \
  --parameters sqlAdminPassword='YourSecurePassword123!'
```

### MCP-Only

```bash
az deployment group create \
  --resource-group rg-pecop-dev \
  --parameters infra/bicep/main.mcp-only.bicepparam \
  --parameters sqlAdminPassword='YourSecurePassword123!'
```

### Prod (All Services)

```bash
az deployment group create \
  --resource-group rg-pecop-prod \
  --parameters infra/bicep/main.prod.bicepparam \
  --parameters sqlAdminPassword='SetSecurelyFromKeyVaultOrPipeline'
```

## Parameters (Overview)
- projectName, environment, location
- sqlAdminLogin, sqlAdminPassword (secure)
- keyVaultAdminObjectId (AAD Object ID for initial KV access)
- deploymentTarget: `aci | aks | appservice | none`
- deployMcp, deployChat, deployAdminApi, deployAdminClient
- imageTag, cpuCores, memoryGB (for ACI)

Note: `sqlAdminPassword` should be supplied securely (Key Vault or pipeline secret). The `.bicepparam` files include an empty placeholder.

## Validation and Linting

```bash
# Validate template compiles
cd infra/bicep
bicep build main.bicep

# Lint
bicep lint main.bicep

# Validate param files
bicep build-params main.dev.bicepparam
bicep build-params main.prod.bicepparam
bicep build-params main.mcp-only.bicepparam
```

## Notes on Warnings
- You may see BCP318 warnings for conditional module outputs (e.g., ACR, ACI). Access conditions match deployment conditions; builds succeed.
- The ACR module was updated to remove deprecated properties (`anonymousPullEnabled`, `azureADAuthenticationAsArmPolicy`, `softDeletePolicy`).

## Key Vault Secrets

Main secrets written by the deployment:
- SqlConnectionString
- AppInsightsConnectionString

Ensure `keyVaultAdminObjectId` has access initially; subsequent access is managed via RBAC.

## Troubleshooting

```bash
# Show ACI state
az container show \
  --resource-group rg-pecop-dev \
  --name <aci-name> \
  --query "{state:instanceView.state, events:containers[0].instanceView.events}"

# Logs
az container logs \
  --resource-group rg-pecop-dev \
  --name <aci-name>

# Restart
az container restart \
  --resource-group rg-pecop-dev \
  --name <aci-name>
```

## Migration (from JSON params)
- Legacy JSON parameter files remain for reference but are superseded by `.bicepparam`.
- Recommended to switch to: [main.dev.bicepparam](infra/bicep/main.dev.bicepparam), [main.prod.bicepparam](infra/bicep/main.prod.bicepparam), [main.mcp-only.bicepparam](infra/bicep/main.mcp-only.bicepparam).

