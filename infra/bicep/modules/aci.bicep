// =============================================================================
// Azure Container Instances (ACI) - IL5/IL6 Compliant
// =============================================================================
// Creates container instances for:
// - Lightweight container deployments
// - Development/testing environments
// - Background jobs and scheduled tasks
// - Sidecar containers
// =============================================================================

@description('Container group name')
param containerGroupName string

@description('Location for the container group')
param location string = resourceGroup().location

@description('Container image (from ACR)')
param containerImage string

@description('Container name')
param containerName string

@description('Number of CPU cores')
@minValue(1)
@maxValue(4)
param cpuCores int = 2

@description('Memory in GB')
@minValue(1)
@maxValue(16)
param memoryInGB int = 4

@description('Container port')
param port int = 5100

@description('OS type')
@allowed(['Linux', 'Windows'])
param osType string = 'Linux'

@description('Restart policy')
@allowed(['Always', 'OnFailure', 'Never'])
param restartPolicy string = 'Always'

@description('Environment variables')
param environmentVariables array = []

@description('Secure environment variables (from Key Vault)')
param secureEnvironmentVariables array = []

@description('ACR login server')
param acrLoginServer string

@description('ACR username (use managed identity instead for production)')
param acrUsername string = ''

@description('ACR password')
@secure()
param acrPassword string = ''

@description('Use managed identity for ACR pull')
param useManagedIdentity bool = true

@description('Subnet ID for VNet integration')
param subnetId string = ''

@description('Enable VNet integration (private IP)')
param enableVNetIntegration bool = false

@description('DNS name label for public IP (leave empty for private only)')
param dnsNameLabel string = ''

@description('Log Analytics Workspace ID for monitoring')
param logAnalyticsWorkspaceId string = ''

@description('Log Analytics Workspace Key')
@secure()
param logAnalyticsWorkspaceKey string = ''

@description('Resource tags')
param tags object = {
  Environment: 'Development'
  ManagedBy: 'Bicep'
}

@description('Enable GPU')
param enableGpu bool = false

@description('GPU count')
param gpuCount int = 0

@description('GPU SKU')
@allowed(['K80', 'P100', 'V100'])
param gpuSku string = 'K80'

@description('Command to override container entrypoint')
param command array = []

@description('Volume mounts')
param volumes array = []

@description('Init containers')
param initContainers array = []

// =============================================================================
// Container Group
// =============================================================================
resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2023-05-01' = {
  name: containerGroupName
  location: location
  tags: tags
  identity: useManagedIdentity ? {
    type: 'SystemAssigned'
  } : null
  properties: {
    // OS type
    osType: osType
    
    // Restart policy
    restartPolicy: restartPolicy
    
    // Image registry credentials (use managed identity or credentials)
    imageRegistryCredentials: useManagedIdentity ? [] : [
      {
        server: acrLoginServer
        username: acrUsername
        password: acrPassword
      }
    ]
    
    // Containers
    containers: [
      {
        name: containerName
        properties: {
          image: containerImage
          
          // Resource requests
          resources: {
            requests: {
              cpu: cpuCores
              memoryInGB: memoryInGB
              gpu: enableGpu ? {
                count: gpuCount
                sku: gpuSku
              } : null
            }
          }
          
          // Ports
          ports: [
            {
              port: port
              protocol: 'TCP'
            }
          ]
          
          // Environment variables
          environmentVariables: concat(
            environmentVariables,
            secureEnvironmentVariables
          )
          
          // Command override
          command: !empty(command) ? command : null
          
          // Volume mounts
          volumeMounts: volumes
          
          // Liveness probe
          livenessProbe: {
            httpGet: {
              path: '/health'
              port: port
              scheme: 'HTTP'
            }
            initialDelaySeconds: 30
            periodSeconds: 10
            failureThreshold: 3
            successThreshold: 1
            timeoutSeconds: 5
          }
          
          // Readiness probe
          readinessProbe: {
            httpGet: {
              path: '/health'
              port: port
              scheme: 'HTTP'
            }
            initialDelaySeconds: 10
            periodSeconds: 5
            failureThreshold: 3
            successThreshold: 1
            timeoutSeconds: 3
          }
        }
      }
    ]
    
    // Init containers
    initContainers: initContainers
    
    // IP address configuration
    ipAddress: enableVNetIntegration ? {
      type: 'Private'
      ports: [
        {
          port: port
          protocol: 'TCP'
        }
      ]
    } : !empty(dnsNameLabel) ? {
      type: 'Public'
      dnsNameLabel: dnsNameLabel
      ports: [
        {
          port: port
          protocol: 'TCP'
        }
      ]
    } : null
    
    // Subnet for VNet integration
    subnetIds: enableVNetIntegration && !empty(subnetId) ? [
      {
        id: subnetId
      }
    ] : null
    
    // Diagnostics - Log Analytics integration
    diagnostics: !empty(logAnalyticsWorkspaceId) ? {
      logAnalytics: {
        workspaceId: logAnalyticsWorkspaceId
        workspaceKey: logAnalyticsWorkspaceKey
      }
    } : null
  }
}

// =============================================================================
// ACR Pull Role Assignment (if using managed identity)
// =============================================================================
resource acrPullRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (useManagedIdentity && !empty(acrLoginServer)) {
  name: guid(containerGroup.id, acrLoginServer, 'AcrPull')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') // AcrPull
    principalId: containerGroup.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// =============================================================================
// Outputs
// =============================================================================
output containerGroupId string = containerGroup.id
output containerGroupName string = containerGroup.name
output containerGroupFqdn string = !empty(dnsNameLabel) ? containerGroup.properties.ipAddress.fqdn : ''
output containerGroupIp string = containerGroup.properties.ipAddress.ip
output containerGroupPrincipalId string = useManagedIdentity ? containerGroup.identity.principalId : ''
