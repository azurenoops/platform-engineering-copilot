// =============================================================================
// Production Environment Parameters
// Deploys: All services with enhanced security
// =============================================================================
using 'main.bicep'

// Core Settings
param projectName = 'pecop'
param environment = 'prod'
param location = 'eastus'

// SQL Database
param sqlAdminLogin = 'platformadmin'
param sqlSku = 'S1'
// Set at deployment: az deployment group create ... --parameters sqlAdminPassword='<value>'
param sqlAdminPassword = ''

// Key Vault Admin - Replace with actual Object ID
param keyVaultAdminObjectId = '00000000-0000-0000-0000-000000000000'

// Container Deployment - All services
param deploymentTarget = 'aci'
param deployMcp = true
param deployChat = true
param deployAdminApi = true
param deployAdminClient = true

// Container Resources (prod - larger)
param imageTag = 'v1.0.0'
param cpuCores = 2
param memoryGB = 4
