// =============================================================================
// Development Environment Parameters
// Deploys: MCP, Admin API, Admin Client (no Chat)
// =============================================================================
using 'main.bicep'

// Core Settings
param projectName = 'pecop'
param environment = 'dev'
param location = 'eastus'

// SQL Database
param sqlAdminLogin = 'platformadmin'
// Set at deployment: az deployment group create ... --parameters sqlAdminPassword='<value>'
param sqlAdminPassword = ''

// Key Vault Admin - Replace with: az ad signed-in-user show --query id
param keyVaultAdminObjectId = '00000000-0000-0000-0000-000000000000'

// Container Deployment
param deploymentTarget = 'aci'
param deployMcp = true
param deployChat = false
param deployAdminApi = true
param deployAdminClient = true

// Container Resources (dev - smaller)
param imageTag = 'latest'
param cpuCores = 1
param memoryGB = 2
