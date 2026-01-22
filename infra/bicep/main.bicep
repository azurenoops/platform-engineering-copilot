// =============================================================================
// Platform Engineering Copilot - Main Infrastructure Template
// =============================================================================
// Deploys: MCP Server, Chat, Admin API, Admin Client
// Targets: ACI (default), AKS, or App Service
// =============================================================================
targetScope = 'resourceGroup'

import { containerDeploymentTarget, sqlDatabaseSku } from './types/main.bicep'

// =============================================================================
// PARAMETERS
// =============================================================================

@description('Project name prefix for resources (3-8 chars)')
@minLength(3)
@maxLength(8)
param projectName string = 'pecop'

@description('Environment name')
@allowed(['dev', 'staging', 'prod'])
param environment string = 'dev'

@description('Azure region for deployment')
param location string = resourceGroup().location

// Database Parameters
@description('SQL Server admin login')
param sqlAdminLogin string = 'platformadmin'

@description('SQL Server admin password')
@secure()
param sqlAdminPassword string

@description('SQL Database SKU')
param sqlSku sqlDatabaseSku = 'S0'

// Key Vault Parameters
@description('Object ID for Key Vault admin access')
param keyVaultAdminObjectId string

// Container Deployment Parameters
@description('Container deployment target')
param deploymentTarget containerDeploymentTarget = 'aci'

@description('Deploy MCP Server')
param deployMcp bool = true

@description('Deploy Chat service')
param deployChat bool = false

@description('Deploy Admin API')
param deployAdminApi bool = true

@description('Deploy Admin Client')
param deployAdminClient bool = true

// Container Settings
@description('Container image tag')
param imageTag string = 'latest'

@description('CPU cores per container')
@minValue(1)
@maxValue(4)
param cpuCores int = 2

@description('Memory GB per container')
@minValue(1)
@maxValue(16)
param memoryGB int = 4

// =============================================================================
// VARIABLES
// =============================================================================

var prefix = '${projectName}-${environment}'
var suffix = uniqueString(resourceGroup().id)
var isProduction = environment == 'prod'

// Resource Names
var names = {
  vnet: '${prefix}-vnet'
  sqlServer: '${prefix}-sql-${suffix}'
  sqlDatabase: '${prefix}-db'
  keyVault: take('${prefix}-kv-${suffix}', 24)
  storage: take(replace('${prefix}st${suffix}', '-', ''), 24)
  appInsights: '${prefix}-ai'
  logAnalytics: '${prefix}-law'
  acr: take(replace('${prefix}acr${suffix}', '-', ''), 50)
  appServicePlan: '${prefix}-asp'
}

// Container Names
var containerNames = {
  mcp: '${prefix}-mcp-aci'
  chat: '${prefix}-chat-aci'
  adminApi: '${prefix}-admin-api-aci'
  adminClient: '${prefix}-admin-client-aci'
}

// Computed values from conditional modules (set after module declarations)
var acrServer = (deploymentTarget == 'aci' || deploymentTarget == 'aks') ? acr.outputs.acrLoginServer : ''
var sqlConnString = replace(database.outputs.connectionStringTemplate, '<PASSWORD>', sqlAdminPassword)
var aiConnString = monitoring.outputs.connectionString

// Service Configuration
var services = {
  mcp: {
    name: 'mcp'
    port: 5100
    image: 'platform-engineering-copilot-mcp'
    healthPath: '/health'
  }
  chat: {
    name: 'chat'
    port: 5001
    image: 'platform-engineering-copilot-chat'
    healthPath: '/health'
  }
  adminApi: {
    name: 'admin-api'
    port: 5050
    image: 'platform-engineering-copilot-admin-api'
    healthPath: '/api/health'
  }
  adminClient: {
    name: 'admin-client'
    port: 80
    image: 'platform-engineering-copilot-admin-client'
    healthPath: '/'
  }
}

// =============================================================================
// NETWORKING
// =============================================================================

module network 'modules/network.bicep' = {
  name: 'network'
  params: {
    vnetName: names.vnet
    location: location
    environment: environment
    vnetAddressPrefix: '10.0.0.0/16'
    appServiceSubnetPrefix: '10.0.1.0/24'
    privateEndpointSubnetPrefix: '10.0.2.0/24'
    managementSubnetPrefix: '10.0.3.0/24'
  }
}

// =============================================================================
// MONITORING
// =============================================================================

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: {
    applicationInsightsName: names.appInsights
    logAnalyticsWorkspaceName: names.logAnalytics
    location: location
    environment: environment
    retentionInDays: isProduction ? 365 : 30
    dailyDataCapInGB: isProduction ? 100 : 1
    samplingPercentage: isProduction ? 20 : 100
  }
}

// =============================================================================
// KEY VAULT
// =============================================================================

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: {
    keyVaultName: names.keyVault
    location: location
    environment: environment
    principalId: keyVaultAdminObjectId
    enableSoftDelete: isProduction
    enablePurgeProtection: isProduction
    skuName: isProduction ? 'premium' : 'standard'
  }
}

// =============================================================================
// STORAGE
// =============================================================================

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: {
    storageAccountName: names.storage
    location: location
    environment: environment
    skuName: isProduction ? 'Standard_GRS' : 'Standard_LRS'
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
  }
}

// =============================================================================
// SQL DATABASE
// =============================================================================

module database 'modules/sql.bicep' = {
  name: 'database'
  params: {
    sqlServerName: names.sqlServer
    sqlDatabaseName: names.sqlDatabase
    location: location
    environment: environment
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    skuName: sqlSku
    allowAzureIps: true
    allowedIpAddresses: []
  }
}

// =============================================================================
// CONTAINER REGISTRY (for ACI/AKS deployments)
// =============================================================================

module acr 'modules/acr.bicep' = if (deploymentTarget == 'aci' || deploymentTarget == 'aks') {
  name: 'acr'
  params: {
    acrName: names.acr
    location: location
    sku: isProduction ? 'Premium' : 'Standard'
    enableGeoReplication: false
    replicationLocations: []
    enableContentTrust: isProduction
    enableQuarantine: isProduction
    publicNetworkAccess: isProduction ? 'Disabled' : 'Enabled'
    tags: {
      Environment: environment
      Service: 'ContainerRegistry'
    }
  }
}

// =============================================================================
// CONTAINER INSTANCES (ACI)
// =============================================================================

// MCP Server
module aciMcp 'modules/aci.bicep' = if (deploymentTarget == 'aci' && deployMcp) {
  name: 'aci-mcp'
  params: {
    containerGroupName: containerNames.mcp
    location: location
    containerImage: '${acrServer}/${services.mcp.image}:${imageTag}'
    containerName: services.mcp.name
    cpuCores: cpuCores
    memoryInGB: memoryGB
    port: services.mcp.port
    acrLoginServer: acrServer
    useManagedIdentity: true
    enableVNetIntegration: isProduction
    subnetId: isProduction ? network.outputs.privateEndpointSubnetId : ''
    dnsNameLabel: !isProduction ? '${containerNames.mcp}-${suffix}' : ''
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    environmentVariables: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: isProduction ? 'Production' : 'Development' }
      { name: 'ConnectionStrings__DefaultConnection', value: sqlConnString }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: aiConnString }
    ]
    tags: { Service: 'MCP', Environment: environment }
  }
}

// Chat Service
module aciChat 'modules/aci.bicep' = if (deploymentTarget == 'aci' && deployChat) {
  name: 'aci-chat'
  params: {
    containerGroupName: containerNames.chat
    location: location
    containerImage: '${acrServer}/${services.chat.image}:${imageTag}'
    containerName: services.chat.name
    cpuCores: cpuCores
    memoryInGB: memoryGB
    port: services.chat.port
    acrLoginServer: acrServer
    useManagedIdentity: true
    enableVNetIntegration: isProduction
    subnetId: isProduction ? network.outputs.privateEndpointSubnetId : ''
    dnsNameLabel: !isProduction ? '${containerNames.chat}-${suffix}' : ''
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    environmentVariables: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: isProduction ? 'Production' : 'Development' }
      { name: 'ConnectionStrings__DefaultConnection', value: sqlConnString }
      { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: aiConnString }
    ]
    tags: { Service: 'Chat', Environment: environment }
  }
}

// Admin API
module aciAdminApi 'modules/aci.bicep' = if (deploymentTarget == 'aci' && deployAdminApi) {
  name: 'aci-admin-api'
  params: {
    containerGroupName: containerNames.adminApi
    location: location
    containerImage: '${acrServer}/${services.adminApi.image}:${imageTag}'
    containerName: services.adminApi.name
    cpuCores: 1
    memoryInGB: 2
    port: services.adminApi.port
    acrLoginServer: acrServer
    useManagedIdentity: true
    enableVNetIntegration: isProduction
    subnetId: isProduction ? network.outputs.privateEndpointSubnetId : ''
    dnsNameLabel: !isProduction ? '${containerNames.adminApi}-${suffix}' : ''
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    environmentVariables: [
      { name: 'ASPNETCORE_ENVIRONMENT', value: isProduction ? 'Production' : 'Development' }
      { name: 'ConnectionStrings__DefaultConnection', value: sqlConnString }
    ]
    tags: { Service: 'AdminAPI', Environment: environment }
  }
}

// Admin Client
module aciAdminClient 'modules/aci.bicep' = if (deploymentTarget == 'aci' && deployAdminClient) {
  name: 'aci-admin-client'
  params: {
    containerGroupName: containerNames.adminClient
    location: location
    containerImage: '${acrServer}/${services.adminClient.image}:${imageTag}'
    containerName: services.adminClient.name
    cpuCores: 1
    memoryInGB: 1
    port: services.adminClient.port
    acrLoginServer: acrServer
    useManagedIdentity: true
    enableVNetIntegration: isProduction
    subnetId: isProduction ? network.outputs.privateEndpointSubnetId : ''
    dnsNameLabel: !isProduction ? '${containerNames.adminClient}-${suffix}' : ''
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    environmentVariables: []
    tags: { Service: 'AdminClient', Environment: environment }
  }
}

// =============================================================================
// APP SERVICE (Alternative deployment target)
// =============================================================================

module appServices 'modules/app-services.bicep' = if (deploymentTarget == 'appservice') {
  name: 'appservices'
  params: {
    appServicePlanName: names.appServicePlan
    location: location
    sku: isProduction ? 'P1v3' : 'B1'
    deployAdminApi: deployAdminApi
    deployChat: deployChat
    acrLoginServer: acrServer
    vnetIntegrationSubnetId: network.outputs.appServiceSubnetId
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
    logAnalyticsWorkspaceId: monitoring.outputs.logAnalyticsWorkspaceId
    appInsightsConnectionString: aiConnString
    sqlConnectionString: sqlConnString
    environment: environment
  }
}

// =============================================================================
// KEY VAULT SECRETS
// =============================================================================

resource sqlSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${names.keyVault}/SqlConnectionString'
  properties: {
    value: sqlConnString
    contentType: 'SQL Connection String'
  }
  dependsOn: [keyVault]
}

resource appInsightsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${names.keyVault}/AppInsightsConnectionString'
  properties: {
    value: aiConnString
    contentType: 'Application Insights Connection String'
  }
  dependsOn: [keyVault]
}

// =============================================================================
// OUTPUTS
// =============================================================================

@description('Resource Group name')
output resourceGroup string = resourceGroup().name

@description('SQL Server FQDN')
output sqlServerFqdn string = database.outputs.sqlServerFqdn

@description('Key Vault URI')
output keyVaultUri string = keyVault.outputs.keyVaultUri

@description('Application Insights Connection String')
output appInsightsConnectionString string = aiConnString

@description('ACR Login Server')
output acrLoginServer string = acrServer

// ACI Outputs
@description('MCP Server URL')
output mcpUrl string = deploymentTarget == 'aci' && deployMcp && !isProduction
  ? 'http://${aciMcp.outputs.containerGroupFqdn}:${services.mcp.port}'
  : ''

@description('Chat URL')
output chatUrl string = deploymentTarget == 'aci' && deployChat && !isProduction
  ? 'http://${aciChat.outputs.containerGroupFqdn}:${services.chat.port}'
  : ''

@description('Admin API URL')
output adminApiUrl string = deploymentTarget == 'aci' && deployAdminApi && !isProduction
  ? 'http://${aciAdminApi.outputs.containerGroupFqdn}:${services.adminApi.port}'
  : ''

@description('Admin Client URL')
output adminClientUrl string = deploymentTarget == 'aci' && deployAdminClient && !isProduction
  ? 'http://${aciAdminClient.outputs.containerGroupFqdn}'
  : ''

@description('Deployment summary')
output summary object = {
  project: projectName
  environment: environment
  location: location
  deploymentTarget: deploymentTarget
  services: {
    mcp: deployMcp
    chat: deployChat
    adminApi: deployAdminApi
    adminClient: deployAdminClient
  }
  infrastructure: {
    sqlServer: names.sqlServer
    keyVault: names.keyVault
    storage: names.storage
    acr: (deploymentTarget == 'aci' || deploymentTarget == 'aks') ? names.acr : 'N/A'
  }
}
