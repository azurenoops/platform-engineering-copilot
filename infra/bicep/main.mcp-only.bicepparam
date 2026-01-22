// =============================================================================
// MCP-Only Deployment Parameters
// Minimal deployment with just MCP Server for AI client development
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

// Key Vault Admin
param keyVaultAdminObjectId = '00000000-0000-0000-0000-000000000000'

// Container Deployment - MCP Only
param deploymentTarget = 'aci'
param deployMcp = true
param deployChat = false
param deployAdminApi = false
param deployAdminClient = false

// Container Resources (minimal)
param imageTag = 'latest'
param cpuCores = 1
param memoryGB = 2
