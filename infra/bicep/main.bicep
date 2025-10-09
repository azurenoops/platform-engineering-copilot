// Main Bicep template for Platform Engineering Copilot infrastructure
targetScope = 'resourceGroup'

// ===============================
// PARAMETERS
// ===============================

@description('Project name that will be used as prefix for resource names')
@minLength(3)
@maxLength(8)
param projectName string = 'platsup'

@description('Environment name (dev, staging, prod)')
@allowed([
  'dev'
  'staging'  
  'prod'
])
param environment string = 'dev'

@description('Location for all resources')
param location string = resourceGroup().location

@description('SQL Server administrator login')
param sqlAdminLogin string = 'platformadmin'

@description('SQL Server administrator password')
@secure()
param sqlAdminPassword string

@description('Your Object ID for Key Vault access')
param keyVaultAdminObjectId string

@description('Azure AD Admin Object ID for SQL Server')
param azureAdAdminObjectId string = ''

@description('Azure AD Admin Login Name for SQL Server')
param azureAdAdminLogin string = ''

@description('App Service Plan SKU')
@allowed([
  'F1'
  'B1'
  'B2'
  'B3'
  'S1'
  'S2'
  'S3'
  'P1V2'
  'P2V2'
  'P3V2'
])
param appServiceSku string = 'B1'

@description('SQL Database SKU')
@allowed([
  'Basic'
  'S0'
  'S1'  
  'S2'
  'S3'
  'P1'
  'P2'
  'P4'
  'P6'
  'P11'
  'P15'
])
param sqlDatabaseSku string = 'S0'



// ===============================
// VARIABLES
// ===============================

var resourcePrefix = '${projectName}-${environment}'
var uniqueSuffix = uniqueString(resourceGroup().id)

// Resource names
var vnetName = '${resourcePrefix}-vnet'
var sqlServerName = '${resourcePrefix}-sql-${uniqueSuffix}'
var sqlDatabaseName = '${resourcePrefix}-db'
var keyVaultName = '${resourcePrefix}-kv-${uniqueSuffix}'
var storageAccountName = replace('${resourcePrefix}st${uniqueSuffix}', '-', '')
var appServicePlanName = '${resourcePrefix}-asp'
var apiAppServiceName = '${resourcePrefix}-api'
var mcpAppServiceName = '${resourcePrefix}-mcp'
var applicationInsightsName = '${resourcePrefix}-ai'
var logAnalyticsWorkspaceName = '${resourcePrefix}-law'

// ===============================
// MODULES
// ===============================

// Virtual Network
module network 'modules/network.bicep' = {
  name: 'network-deployment'
  params: {
    vnetName: vnetName
    location: location
    environment: environment
    vnetAddressPrefix: '10.0.0.0/16'
    appServiceSubnetPrefix: '10.0.1.0/24'
    privateEndpointSubnetPrefix: '10.0.2.0/24'
    managementSubnetPrefix: '10.0.3.0/24'
  }
}

// Application Insights and Log Analytics
module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring-deployment'
  params: {
    applicationInsightsName: applicationInsightsName
    logAnalyticsWorkspaceName: logAnalyticsWorkspaceName
    location: location
    environment: environment
    retentionInDays: environment == 'prod' ? 365 : 90
    dailyDataCapInGB: environment == 'prod' ? 100 : 1
    samplingPercentage: environment == 'prod' ? 20 : 100
  }
}

// Key Vault
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault-deployment'
  params: {
    keyVaultName: keyVaultName
    location: location
    environment: environment
    principalId: keyVaultAdminObjectId
    enableSoftDelete: environment == 'prod'
    enablePurgeProtection: environment == 'prod'
    skuName: environment == 'prod' ? 'premium' : 'standard'
  }
}

// Storage Account
module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    storageAccountName: storageAccountName
    location: location
    environment: environment
    skuName: environment == 'prod' ? 'Standard_GRS' : 'Standard_LRS'
    privateEndpointSubnetId: network.outputs.privateEndpointSubnetId
  }
}

// SQL Server and Database
module database 'modules/sql.bicep' = {
  name: 'database-deployment'
  params: {
    sqlServerName: sqlServerName
    sqlDatabaseName: sqlDatabaseName
    location: location
    environment: environment
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    skuName: sqlDatabaseSku
    azureAdAdminObjectId: azureAdAdminObjectId
    azureAdAdminLogin: azureAdAdminLogin
    allowAzureIps: true
    allowedIpAddresses: []
  }
}

// App Services (API and MCP Server)
module appServices 'modules/appservice.bicep' = {
  name: 'appservices-deployment'
  params: {
    appServicePlanName: appServicePlanName
    apiAppServiceName: apiAppServiceName
    mcpAppServiceName: mcpAppServiceName
    location: location
    environment: environment
    skuName: appServiceSku
    subnetId: network.outputs.appServiceSubnetId
    appInsightsInstrumentationKey: monitoring.outputs.instrumentationKey
    keyVaultUri: keyVault.outputs.keyVaultUri
    sqlConnectionString: replace(database.outputs.connectionStringTemplate, '<PASSWORD>', sqlAdminPassword)
  }
}

// ===============================
// KEY VAULT SECRETS
// ===============================

// Store SQL connection string in Key Vault
resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/SqlConnectionString'
  properties: {
    value: replace(database.outputs.connectionStringTemplate, '<PASSWORD>', sqlAdminPassword)
    contentType: 'SQL Connection String'
    attributes: {
      enabled: true
    }
  }
}

// Store Application Insights instrumentation key
resource appInsightsKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/ApplicationInsightsInstrumentationKey'
  properties: {
    value: monitoring.outputs.instrumentationKey
    contentType: 'Application Insights Instrumentation Key'
    attributes: {
      enabled: true
    }
  }
}

// Store Application Insights connection string
resource appInsightsConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${keyVaultName}/ApplicationInsightsConnectionString'
  properties: {
    value: monitoring.outputs.connectionString
    contentType: 'Application Insights Connection String'
    attributes: {
      enabled: true
    }
  }
}

// ===============================
// RBAC ASSIGNMENTS
// ===============================

// Grant App Services access to Key Vault
resource apiKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, apiAppServiceName, 'KeyVaultSecretsUser')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: appServices.outputs.apiAppServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource mcpKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, mcpAppServiceName, 'KeyVaultSecretsUser')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: appServices.outputs.mcpAppServicePrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ===============================
// OUTPUTS
// ===============================

@description('API App Service URL')
output apiUrl string = 'https://${appServices.outputs.apiAppServiceHostName}'

@description('MCP Server URL')
output mcpUrl string = 'https://${appServices.outputs.mcpAppServiceHostName}'

@description('SQL Server FQDN')
output sqlServerFqdn string = database.outputs.sqlServerFqdn

@description('Key Vault URI')
output keyVaultUri string = keyVault.outputs.keyVaultUri

@description('Application Insights Instrumentation Key')
output applicationInsightsInstrumentationKey string = monitoring.outputs.instrumentationKey

@description('Storage Account Name')
output storageAccountName string = storage.outputs.storageAccountName

@description('Resource Group Name')
output resourceGroupName string = resourceGroup().name

@description('Virtual Network Name')
output virtualNetworkName string = network.outputs.vnetName

@description('Deployment Summary')
output deploymentSummary object = {
  projectName: projectName
  environment: environment
  location: location
  apiAppService: appServices.outputs.apiAppServiceName
  mcpAppService: appServices.outputs.mcpAppServiceName
  sqlServer: database.outputs.sqlServerName
  sqlDatabase: database.outputs.sqlDatabaseName
  keyVault: keyVault.outputs.keyVaultName
  storageAccount: storage.outputs.storageAccountName
  applicationInsights: monitoring.outputs.applicationInsightsName
  virtualNetwork: network.outputs.vnetName
  deployedAt: 'Deployment completed'
}
