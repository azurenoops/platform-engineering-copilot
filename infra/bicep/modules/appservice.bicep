// App Service module for hosting the Platform API and MCP Server
@description('Name of the App Service Plan')
param appServicePlanName string

@description('Name of the App Service for the API')
param apiAppServiceName string

@description('Name of the App Service for the MCP Server')
param mcpAppServiceName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('The pricing tier of the App Service Plan')
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
param skuName string = 'B1'

@description('Subnet ID for VNet integration')
param subnetId string = ''

@description('Application Insights Instrumentation Key')
param appInsightsInstrumentationKey string = ''

@description('Key Vault URI for configuration')
param keyVaultUri string = ''

@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@description('SQL Server connection string')
@secure()
param sqlConnectionString string



// App Service Plan
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  kind: 'linux'
  properties: {
    reserved: true
  }
  sku: {
    name: skuName
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformEngineering'
  }
}

// API App Service
resource apiAppService 'Microsoft.Web/sites@2023-01-01' = {
  name: apiAppServiceName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: skuName != 'F1'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      httpLoggingEnabled: true
      logsDirectorySizeLimit: 35
      detailedErrorLoggingEnabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: 'InstrumentationKey=${appInsightsInstrumentationKey}'
        }
        {
          name: 'DatabaseProvider'
          value: 'SqlServer'
        }
        {
          name: 'ConnectionStrings__SqlServerConnection'
          value: sqlConnectionString
        }
        {
          name: 'KeyVault__Uri'
          value: keyVaultUri
        }
      ]
    }
    httpsOnly: true
    clientAffinityEnabled: false
    virtualNetworkSubnetId: !empty(subnetId) ? subnetId : null
  }
  identity: {
    type: 'SystemAssigned'
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformAPI'
  }
}

// MCP Server App Service
resource mcpAppService 'Microsoft.Web/sites@2023-01-01' = {
  name: mcpAppServiceName
  location: location
  kind: 'app,linux'
  properties: {
    serverFarmId: appServicePlan.id
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|8.0'
      alwaysOn: skuName != 'F1'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      scmMinTlsVersion: '1.2'
      httpLoggingEnabled: true
      logsDirectorySizeLimit: 35
      detailedErrorLoggingEnabled: true
      appSettings: [
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsightsInstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: 'InstrumentationKey=${appInsightsInstrumentationKey}'
        }
        {
          name: 'McpPort'
          value: '8080'
        }
        {
          name: 'ApiBaseUrl'
          value: 'https://${apiAppService.properties.defaultHostName}'
        }
      ]
    }
    httpsOnly: true
    clientAffinityEnabled: false
    virtualNetworkSubnetId: !empty(subnetId) ? subnetId : null
  }
  identity: {
    type: 'SystemAssigned'
  }
  tags: {
    Environment: environment
    Purpose: 'McpServer'
  }
}

// Output values
output apiAppServiceId string = apiAppService.id
output apiAppServiceName string = apiAppService.name
output apiAppServiceHostName string = apiAppService.properties.defaultHostName
output apiAppServicePrincipalId string = apiAppService.identity.principalId

output mcpAppServiceId string = mcpAppService.id
output mcpAppServiceName string = mcpAppService.name
output mcpAppServiceHostName string = mcpAppService.properties.defaultHostName
output mcpAppServicePrincipalId string = mcpAppService.identity.principalId

output appServicePlanId string = appServicePlan.id
