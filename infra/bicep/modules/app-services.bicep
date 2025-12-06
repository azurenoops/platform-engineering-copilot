// =============================================================================
// Azure App Services for Platform Engineering Copilot - IL5/IL6 Compliant
// =============================================================================
// Creates multiple App Services with conditional deployment:
// - Admin API (conditional)
// - Chat Service (conditional)
// - MCP Server (always deployed)
// =============================================================================

@description('The name of the App Service Plan')
param appServicePlanName string

@description('Location for resources')
param location string = resourceGroup().location

@description('App Service Plan SKU')
@allowed(['F1','B1','B2','B3','S1','S2','S3','P1v3', 'P2v3', 'P3v3', 'P1mv3', 'P2mv3', 'P3mv3', 'P4mv3', 'P5mv3'])
param sku string = 'P1v3'

@description('Number of worker instances')
@minValue(1)
@maxValue(30)
param capacity int = 3

@description('Enable zone redundancy (requires P1v3 or higher)')
param zoneRedundant bool = true

@description('Deploy Admin API service')
param deployAdminApi bool = true

@description('Deploy Chat service')
param deployChat bool = true

@description('Container Registry login server')
param acrLoginServer string

@description('Admin API container image name and tag')
param adminApiContainerImageName string = 'platform-engineering-copilot-admin-api:latest'

@description('Chat container image name and tag')
param chatContainerImageName string = 'platform-engineering-copilot-chat:latest'

@description('MCP Server container image name and tag')
param mcpContainerImageName string = 'platform-engineering-copilot-mcp:latest'

@description('Enable VNet integration')
param enableVNetIntegration bool = true

@description('VNet integration subnet resource ID')
param vnetIntegrationSubnetId string

@description('Enable private endpoint')
param enablePrivateEndpoint bool = true

@description('Private endpoint subnet resource ID')
param privateEndpointSubnetId string

@description('Enable HTTPS only')
param httpsOnly bool = true

@description('Minimum TLS version')
@allowed(['1.0', '1.1', '1.2', '1.3'])
param minTlsVersion string = '1.2'

@description('Enable Always On')
param alwaysOn bool = true

@description('Log Analytics Workspace ID for monitoring')
param logAnalyticsWorkspaceId string

@description('Application Insights connection string')
param appInsightsConnectionString string = ''

@description('Resource tags for compliance')
param tags object = {
  Environment: 'Production'
  Classification: 'CUI'
  ImpactLevel: 'IL5'
  MissionOwner: 'Platform Engineering'
  Compliance: 'NIST-800-53'
}

@description('SQL connection string for Admin API and MCP')
param sqlConnectionString string = ''

@description('Enable auto-scaling')
param enableAutoScale bool = true

@description('Auto-scale minimum instance count')
param autoScaleMinCount int = 3

@description('Auto-scale maximum instance count')
param autoScaleMaxCount int = 10

@description('Environment name (dev, staging, prod)')
param environment string = 'prod'

// =============================================================================
// App Service Plan (shared by all services)
// =============================================================================
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  tags: tags
  sku: {
    name: sku
    capacity: capacity
  }
  kind: 'linux'
  properties: {
    reserved: true // Required for Linux
    zoneRedundant: zoneRedundant && contains(['P1v3', 'P2v3', 'P3v3', 'P1mv3', 'P2mv3', 'P3mv3', 'P4mv3', 'P5mv3'], sku)
  }
}

// =============================================================================
// Admin API App Service (Conditional)
// =============================================================================
resource adminApiAppService 'Microsoft.Web/sites@2023-01-01' = if (deployAdminApi) {
  name: '${appServicePlanName}-admin-api'
  location: location
  tags: union(tags, {
    Service: 'Admin API'
  })
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: httpsOnly
    clientCertEnabled: false
    clientCertMode: 'Optional'

    siteConfig: {
      linuxFxVersion: 'DOCKER|${acrLoginServer}/${adminApiContainerImageName}'
      acrUseManagedIdentityCreds: true
      alwaysOn: alwaysOn
      http20Enabled: true
      minTlsVersion: minTlsVersion
      ftpsState: 'Disabled'
      detailedErrorLoggingEnabled: true
      httpLoggingEnabled: true
      requestTracingEnabled: true
      remoteDebuggingEnabled: false
      healthCheckPath: '/health'

      ipSecurityRestrictions: [
        {
          action: 'Allow'
          priority: 100
          name: 'Allow all'
          ipAddress: 'Any'
        }
      ]

      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'WEBSITES_PORT'
          value: '5002'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acrLoginServer}'
        }
        {
          name: 'DOCKER_ENABLE_CI'
          value: 'true'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: sqlConnectionString
        }
      ]
    }

    virtualNetworkSubnetId: enableVNetIntegration ? vnetIntegrationSubnetId : null
    vnetRouteAllEnabled: enableVNetIntegration
    publicNetworkAccess: enablePrivateEndpoint ? 'Disabled' : 'Enabled'
  }
}

// =============================================================================
// Chat App Service (Conditional)
// =============================================================================
resource chatAppService 'Microsoft.Web/sites@2023-01-01' = if (deployChat) {
  name: '${appServicePlanName}-chat'
  location: location
  tags: union(tags, {
    Service: 'Chat'
  })
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: httpsOnly
    clientCertEnabled: false
    clientCertMode: 'Optional'

    siteConfig: {
      linuxFxVersion: 'DOCKER|${acrLoginServer}/${chatContainerImageName}'
      acrUseManagedIdentityCreds: true
      alwaysOn: alwaysOn
      http20Enabled: true
      minTlsVersion: minTlsVersion
      ftpsState: 'Disabled'
      detailedErrorLoggingEnabled: true
      httpLoggingEnabled: true
      requestTracingEnabled: true
      remoteDebuggingEnabled: false
      healthCheckPath: '/health'

      ipSecurityRestrictions: [
        {
          action: 'Allow'
          priority: 100
          name: 'Allow all'
          ipAddress: 'Any'
        }
      ]

      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'WEBSITES_PORT'
          value: '5001'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acrLoginServer}'
        }
        {
          name: 'DOCKER_ENABLE_CI'
          value: 'true'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'McpServer__BaseUrl'
          value: 'https://${appServicePlanName}-mcp.azurewebsites.net'
        }
      ]
    }

    virtualNetworkSubnetId: enableVNetIntegration ? vnetIntegrationSubnetId : null
    vnetRouteAllEnabled: enableVNetIntegration
    publicNetworkAccess: enablePrivateEndpoint ? 'Disabled' : 'Enabled'
  }
}

// =============================================================================
// MCP Server App Service (Always Deployed)
// =============================================================================
resource mcpAppService 'Microsoft.Web/sites@2023-01-01' = {
  name: '${appServicePlanName}-mcp'
  location: location
  tags: union(tags, {
    Service: 'MCP Server'
  })
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: httpsOnly
    clientCertEnabled: false
    clientCertMode: 'Optional'

    siteConfig: {
      linuxFxVersion: 'DOCKER|${acrLoginServer}/${mcpContainerImageName}'
      acrUseManagedIdentityCreds: true
      alwaysOn: alwaysOn
      http20Enabled: true
      minTlsVersion: minTlsVersion
      ftpsState: 'Disabled'
      detailedErrorLoggingEnabled: true
      httpLoggingEnabled: true
      requestTracingEnabled: true
      remoteDebuggingEnabled: false
      healthCheckPath: '/health'

      ipSecurityRestrictions: [
        {
          action: 'Allow'
          priority: 100
          name: 'Allow all'
          ipAddress: 'Any'
        }
      ]

      appSettings: [
        {
          name: 'WEBSITES_ENABLE_APP_SERVICE_STORAGE'
          value: 'false'
        }
        {
          name: 'WEBSITES_PORT'
          value: '5100'
        }
        {
          name: 'DOCKER_REGISTRY_SERVER_URL'
          value: 'https://${acrLoginServer}'
        }
        {
          name: 'DOCKER_ENABLE_CI'
          value: 'true'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsightsConnectionString
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environment == 'prod' ? 'Production' : 'Development'
        }
        {
          name: 'ConnectionStrings__DefaultConnection'
          value: sqlConnectionString
        }
      ]
    }

    virtualNetworkSubnetId: enableVNetIntegration ? vnetIntegrationSubnetId : null
    vnetRouteAllEnabled: enableVNetIntegration
    publicNetworkAccess: enablePrivateEndpoint ? 'Disabled' : 'Enabled'
  }
}

// =============================================================================
// Private Endpoints
// =============================================================================
resource adminApiPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = if (deployAdminApi && enablePrivateEndpoint) {
  name: '${appServicePlanName}-admin-api-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${appServicePlanName}-admin-api-pe-connection'
        properties: {
          privateLinkServiceId: adminApiAppService.id
          groupIds: ['sites']
        }
      }
    ]
  }
}

resource chatPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = if (deployChat && enablePrivateEndpoint) {
  name: '${appServicePlanName}-chat-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${appServicePlanName}-chat-pe-connection'
        properties: {
          privateLinkServiceId: chatAppService.id
          groupIds: ['sites']
        }
      }
    ]
  }
}

resource mcpPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = if (enablePrivateEndpoint) {
  name: '${appServicePlanName}-mcp-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${appServicePlanName}-mcp-pe-connection'
        properties: {
          privateLinkServiceId: mcpAppService.id
          groupIds: ['sites']
        }
      }
    ]
  }
}

// =============================================================================
// Auto-scale Settings
// =============================================================================
resource autoScaleSettings 'Microsoft.Insights/autoscalesettings@2022-10-01' = if (enableAutoScale) {
  name: '${appServicePlanName}-autoscale'
  location: location
  tags: tags
  properties: {
    enabled: true
    targetResourceUri: appServicePlan.id
    profiles: [
      {
        name: 'Auto scale based on CPU and memory'
        capacity: {
          minimum: string(autoScaleMinCount)
          maximum: string(autoScaleMaxCount)
          default: string(capacity)
        }
        rules: [
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricResourceUri: appServicePlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT5M'
              timeAggregation: 'Average'
              operator: 'GreaterThan'
              threshold: 70
            }
            scaleAction: {
              direction: 'Increase'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT5M'
            }
          }
          {
            metricTrigger: {
              metricName: 'CpuPercentage'
              metricResourceUri: appServicePlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT10M'
              timeAggregation: 'Average'
              operator: 'LessThan'
              threshold: 30
            }
            scaleAction: {
              direction: 'Decrease'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT10M'
            }
          }
        ]
      }
    ]
  }
}

// =============================================================================
// Diagnostic Settings
// =============================================================================
resource adminApiDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (deployAdminApi) {
  name: 'admin-api-diagnostics'
  scope: adminApiAppService
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource chatDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = if (deployChat) {
  name: 'chat-diagnostics'
  scope: chatAppService
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

resource mcpDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'mcp-diagnostics'
  scope: mcpAppService
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
      }
    ]
  }
}

// =============================================================================
// Outputs
// =============================================================================
output appServicePlanId string = appServicePlan.id
output appServicePlanName string = appServicePlan.name

// Admin API outputs (conditional)
output adminApiAppServiceId string = deployAdminApi ? adminApiAppService!.id : ''
output adminApiAppServiceName string = deployAdminApi ? adminApiAppService!.name : ''
output adminApiDefaultHostname string = deployAdminApi ? adminApiAppService!.properties.defaultHostName : ''
output adminApiPrincipalId string = deployAdminApi ? adminApiAppService!.identity.principalId : ''

// Chat outputs (conditional)
output chatAppServiceId string = deployChat ? chatAppService!.id : ''
output chatAppServiceName string = deployChat ? chatAppService!.name : ''
output chatDefaultHostname string = deployChat ? chatAppService!.properties.defaultHostName : ''
output chatPrincipalId string = deployChat ? chatAppService!.identity.principalId : ''

// MCP outputs (always available)
output mcpAppServiceId string = mcpAppService.id
output mcpAppServiceName string = mcpAppService.name
output mcpDefaultHostname string = mcpAppService.properties.defaultHostName
output mcpPrincipalId string = mcpAppService.identity.principalId
