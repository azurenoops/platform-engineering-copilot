// =============================================================================
// Azure App Service for Containers - IL5/IL6 Compliant
// =============================================================================
// Creates an App Service with:
// - Linux container support
// - VNet integration for private networking
// - Private endpoints for inbound traffic
// - Managed identity for Azure resource access
// - Always-on and auto-scaling
// - Monitoring and logging
// - SSL/TLS enforcement
// =============================================================================

@description('The name of the App Service Plan')
param appServicePlanName string

@description('The name of the App Service (Web App)')
param appServiceName string

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

@description('Container Registry login server')
param acrLoginServer string

@description('Container image name and tag')
param containerImageName string

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

@description('Enable client certificate mode')
@allowed(['Required', 'Optional', 'OptionalInteractiveUser'])
param clientCertMode string = 'Optional'

@description('Resource tags for compliance')
param tags object = {
  Environment: 'Production'
  Classification: 'CUI'
  ImpactLevel: 'IL5'
  MissionOwner: 'Platform Engineering'
  Compliance: 'NIST-800-53'
}

@description('Environment variables for the container')
param environmentVariables array = []

@description('Enable auto-scaling')
param enableAutoScale bool = true

@description('Auto-scale minimum instance count')
param autoScaleMinCount int = 3

@description('Auto-scale maximum instance count')
param autoScaleMaxCount int = 10

@description('Enable FTP (false for IL5/IL6)')
param ftpsState string = 'Disabled'

@description('Enable remote debugging (false for production)')
param remoteDebuggingEnabled bool = false

@description('Enable detailed error logging')
param detailedErrorLoggingEnabled bool = true

@description('Enable HTTP logging')
param httpLoggingEnabled bool = true

@description('Enable request tracing')
param requestTracingEnabled bool = true

// =============================================================================
// App Service Plan
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
// App Service (Web App for Containers)
// =============================================================================
resource appService 'Microsoft.Web/sites@2023-01-01' = {
  name: appServiceName
  location: location
  tags: tags
  kind: 'app,linux,container'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: httpsOnly
    clientCertEnabled: clientCertMode != 'Optional'
    clientCertMode: clientCertMode

    siteConfig: {
      // Container configuration
      linuxFxVersion: 'DOCKER|${acrLoginServer}/${containerImageName}'
      acrUseManagedIdentityCreds: true

      // Always On for production
      alwaysOn: alwaysOn

      // HTTP settings
      http20Enabled: true
      minTlsVersion: minTlsVersion
      ftpsState: ftpsState

      // Logging
      detailedErrorLoggingEnabled: detailedErrorLoggingEnabled
      httpLoggingEnabled: httpLoggingEnabled
      requestTracingEnabled: requestTracingEnabled

      // Security headers
      ipSecurityRestrictions: [
        {
          action: 'Allow'
          priority: 100
          name: 'Allow all'
          description: 'Allow all traffic (use private endpoint for restriction)'
          ipAddress: 'Any'
        }
      ]

      // Disable remote debugging in production
      remoteDebuggingEnabled: remoteDebuggingEnabled

      // Health check
      healthCheckPath: '/health'

      // Environment variables
      appSettings: union(
        [
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
        ],
        environmentVariables
      )
    }

    // VNet integration
    virtualNetworkSubnetId: enableVNetIntegration ? vnetIntegrationSubnetId : null

    // VNet route all (force all outbound through VNet)
    vnetRouteAllEnabled: enableVNetIntegration

    // Public network access (disabled when using private endpoint)
    publicNetworkAccess: enablePrivateEndpoint ? 'Disabled' : 'Enabled'
  }
}

// =============================================================================
// Private Endpoint for App Service
// =============================================================================
resource privateEndpoint 'Microsoft.Network/privateEndpoints@2023-05-01' = if (enablePrivateEndpoint) {
  name: '${appServiceName}-pe'
  location: location
  tags: tags
  properties: {
    subnet: {
      id: privateEndpointSubnetId
    }
    privateLinkServiceConnections: [
      {
        name: '${appServiceName}-pe-connection'
        properties: {
          privateLinkServiceId: appService.id
          groupIds: [
            'sites'
          ]
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
          // Scale out when CPU > 70%
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
          // Scale in when CPU < 30%
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
          // Scale out when Memory > 80%
          {
            metricTrigger: {
              metricName: 'MemoryPercentage'
              metricResourceUri: appServicePlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT5M'
              timeAggregation: 'Average'
              operator: 'GreaterThan'
              threshold: 80
            }
            scaleAction: {
              direction: 'Increase'
              type: 'ChangeCount'
              value: '1'
              cooldown: 'PT5M'
            }
          }
          // Scale in when Memory < 40%
          {
            metricTrigger: {
              metricName: 'MemoryPercentage'
              metricResourceUri: appServicePlan.id
              timeGrain: 'PT1M'
              statistic: 'Average'
              timeWindow: 'PT10M'
              timeAggregation: 'Average'
              operator: 'LessThan'
              threshold: 40
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
    notifications: []
  }
}

// =============================================================================
// Diagnostic Settings
// =============================================================================
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${appServiceName}-diagnostics'
  scope: appService
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'AppServiceHTTPLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'AppServiceConsoleLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'AppServiceAppLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'AppServiceAuditLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'AppServicePlatformLogs'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
    ]
  }
}

// =============================================================================
// Outputs
// =============================================================================
output appServiceId string = appService.id
output appServiceName string = appService.name
output appServicePlanId string = appServicePlan.id
output appServiceDefaultHostname string = appService.properties.defaultHostName
output appServicePrincipalId string = appService.identity.principalId
output privateEndpointId string = enablePrivateEndpoint ? privateEndpoint.id : ''
