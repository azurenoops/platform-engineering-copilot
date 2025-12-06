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
  'P1mv3' 
  'P1v3' 
  'P2mv3' 
  'P2v3' 
  'P3mv3' 
  'P3v3' 
  'P4mv3' 
  'P5mv3'
])
param appServiceSku string = 'P1v3'

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

@description('Container deployment target (appservice, aks, aci, or none)')
@allowed([
  'appservice'
  'aks'
  'aci'
  'none'
])
param containerDeploymentTarget string = 'appservice'

@description('Deploy Azure Container Registry')
param deployACR bool = false

@description('ACR SKU (Basic, Standard, Premium)')
@allowed([
  'Basic'
  'Standard'
  'Premium'
])
param acrSku string = 'Standard'

@description('Deploy AKS cluster')
param deployAKS bool = false

@description('AKS Kubernetes version')
param aksKubernetesVersion string = '1.28.3'

@description('AKS system node count')
@minValue(1)
@maxValue(10)
param aksSystemNodeCount int = 3

@description('Deploy ACI container instances')
param deployACI bool = false

@description('ACI CPU cores')
@minValue(1)
@maxValue(4)
param aciCpuCores int = 2

@description('ACI memory in GB')
@minValue(1)
@maxValue(16)
param aciMemoryInGB int = 4

// ===============================
// APP SERVICE DEPLOYMENT OPTIONS
// ===============================

@description('Deploy Admin API service')
param deployAdminApi bool = true

@description('Deploy Chat service')
param deployChat bool = true

// ===============================
// EXISTING RESOURCE OPTIONS
// ===============================

@description('Use existing Virtual Network instead of creating new one')
param useExistingNetwork bool = false

@description('Existing Virtual Network name (required if useExistingNetwork is true)')
param existingVnetName string = ''

@description('Existing Virtual Network resource group (defaults to current resource group)')
param existingVnetResourceGroup string = resourceGroup().name

@description('Existing App Service subnet name (required if useExistingNetwork is true)')
param existingAppServiceSubnetName string = ''

@description('Existing Private Endpoint subnet name (required if useExistingNetwork is true)')
param existingPrivateEndpointSubnetName string = ''

@description('Use existing Log Analytics Workspace instead of creating new one')
param useExistingLogAnalytics bool = false

@description('Existing Log Analytics Workspace name (required if useExistingLogAnalytics is true)')
param existingLogAnalyticsWorkspaceName string = ''

@description('Existing Log Analytics Workspace resource group (defaults to current resource group)')
param existingLogAnalyticsResourceGroup string = resourceGroup().name

@description('Use existing Key Vault instead of creating new one')
param useExistingKeyVault bool = false

@description('Existing Key Vault name (required if useExistingKeyVault is true)')
param existingKeyVaultName string = ''

@description('Existing Key Vault resource group (defaults to current resource group)')
param existingKeyVaultResourceGroup string = resourceGroup().name



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
var applicationInsightsName = '${resourcePrefix}-ai'
var logAnalyticsWorkspaceName = '${resourcePrefix}-law'

// Container infrastructure names
var acrName = replace('${resourcePrefix}acr${uniqueSuffix}', '-', '')
var aksClusterName = '${resourcePrefix}-aks'
var aciMcpGroupName = '${resourcePrefix}-mcp-aci'
var aciChatGroupName = '${resourcePrefix}-chat-aci'
var aciAdminApiGroupName = '${resourcePrefix}-admin-api-aci'
var aciAdminClientGroupName = '${resourcePrefix}-admin-client-aci'

// ===============================
// MODULES
// ===============================

// Reference to existing Virtual Network (when useExistingNetwork is true)
resource existingVnet 'Microsoft.Network/virtualNetworks@2023-05-01' existing = if (useExistingNetwork) {
  name: existingVnetName
  scope: resourceGroup(existingVnetResourceGroup)
}

resource existingAppServiceSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-05-01' existing = if (useExistingNetwork) {
  parent: existingVnet
  name: existingAppServiceSubnetName
}

resource existingPrivateEndpointSubnet 'Microsoft.Network/virtualNetworks/subnets@2023-05-01' existing = if (useExistingNetwork) {
  parent: existingVnet
  name: existingPrivateEndpointSubnetName
}

// Virtual Network (create new only if not using existing)
module network 'modules/network.bicep' = if (!useExistingNetwork) {
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

// Reference to existing Log Analytics Workspace (when useExistingLogAnalytics is true)
resource existingLogAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' existing = if (useExistingLogAnalytics) {
  name: existingLogAnalyticsWorkspaceName
  scope: resourceGroup(existingLogAnalyticsResourceGroup)
}

// Application Insights and Log Analytics (create new only if not using existing)
module monitoring 'modules/monitoring.bicep' = if (!useExistingLogAnalytics) {
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

// Reference to existing Key Vault (when useExistingKeyVault is true)
resource existingKeyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = if (useExistingKeyVault) {
  name: existingKeyVaultName
  scope: resourceGroup(existingKeyVaultResourceGroup)
}

// Key Vault (create new only if not using existing)
module keyVault 'modules/keyvault.bicep' = if (!useExistingKeyVault) {
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
    privateEndpointSubnetId: useExistingNetwork ? existingPrivateEndpointSubnet.id : network!.outputs.privateEndpointSubnetId
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

// App Services (Admin API, Chat, and MCP Server with conditional deployment)
module appServices 'modules/app-services.bicep' = if (containerDeploymentTarget == 'appservice' || containerDeploymentTarget == 'none') {
  name: 'appservices-deployment'
  params: {
    appServicePlanName: appServicePlanName
    location: location
    sku: appServiceSku
    deployAdminApi: deployAdminApi
    deployChat: deployChat
    acrLoginServer: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? acr!.outputs.acrLoginServer : ''
    vnetIntegrationSubnetId: useExistingNetwork ? existingAppServiceSubnet.id : network!.outputs.appServiceSubnetId
    privateEndpointSubnetId: useExistingNetwork ? existingPrivateEndpointSubnet.id : network!.outputs.privateEndpointSubnetId
    logAnalyticsWorkspaceId: useExistingLogAnalytics ? existingLogAnalytics.id : monitoring!.outputs.logAnalyticsWorkspaceId
    appInsightsConnectionString: useExistingLogAnalytics ? '' : monitoring!.outputs.connectionString
    sqlConnectionString: replace(database.outputs.connectionStringTemplate, '<PASSWORD>', sqlAdminPassword)
    environment: environment
  }
}

// ===============================
// CONTAINER INFRASTRUCTURE
// ===============================

// Azure Container Registry
module acr 'modules/acr.bicep' = if (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') {
  name: 'acr-deployment'
  params: {
    acrName: acrName
    location: location
    sku: acrSku
    enableGeoReplication: environment == 'prod' && acrSku == 'Premium'
    replicationLocations: environment == 'prod' ? ['usgovvirginia', 'usgovarizona'] : []
    enableContentTrust: environment == 'prod'
    enableQuarantine: environment == 'prod'
    publicNetworkAccess: environment == 'prod' ? 'Disabled' : 'Enabled'
    tags: union({
      Service: 'Container Registry'
    }, {
      Environment: environment
    })
  }
}

// Azure Kubernetes Service
module aks 'modules/aks.bicep' = if (deployAKS || containerDeploymentTarget == 'aks') {
  name: 'aks-deployment'
  params: {
    clusterName: aksClusterName
    location: location
    dnsPrefix: '${projectName}-${environment}'
    kubernetesVersion: aksKubernetesVersion
    enablePrivateCluster: environment == 'prod'
    systemNodeCount: max(aksSystemNodeCount, 3)
    userNodeCount: 3
    enableAutoScaling: true
    minNodeCount: 3
    maxNodeCount: 10
    vnetId: useExistingNetwork ? existingVnet.id : network!.outputs.vnetId
    subnetId: useExistingNetwork ? existingAppServiceSubnet.id : network!.outputs.appServiceSubnetId
    podSubnetId: '' // Can add dedicated pod subnet if needed
    logAnalyticsWorkspaceId: useExistingLogAnalytics ? existingLogAnalytics.id : monitoring!.outputs.logAnalyticsWorkspaceId
    acrId: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? acr!.outputs.acrId : ''
    tags: {
      Service: 'Kubernetes'
      Environment: environment
    }
  }
}

// Azure Container Instances - MCP Server
module aciMcp 'modules/aci.bicep' = if (deployACI || containerDeploymentTarget == 'aci') {
  name: 'aci-mcp-deployment'
  params: {
    containerGroupName: aciMcpGroupName
    location: location
    containerImage: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? '${acr!.outputs.acrLoginServer}/platform-engineering-copilot-mcp:latest' : 'mcr.microsoft.com/dotnet/samples:aspnetapp'
    containerName: 'mcp-server'
    cpuCores: aciCpuCores
    memoryInGB: aciMemoryInGB
    port: 5100
    acrLoginServer: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? acr!.outputs.acrLoginServer : ''
    useManagedIdentity: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci')
    enableVNetIntegration: environment == 'prod'
    subnetId: environment == 'prod' ? (useExistingNetwork ? existingPrivateEndpointSubnet.id : network!.outputs.privateEndpointSubnetId) : ''
    dnsNameLabel: environment != 'prod' ? '${aciMcpGroupName}-${uniqueSuffix}' : ''
    logAnalyticsWorkspaceId: useExistingLogAnalytics ? existingLogAnalytics.id : monitoring!.outputs.logAnalyticsWorkspaceId
    environmentVariables: [
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: environment == 'prod' ? 'Production' : 'Development'
      }
      {
        name: 'ConnectionStrings__DefaultConnection'
        value: replace(database.outputs.connectionStringTemplate, '<PASSWORD>', sqlAdminPassword)
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: useExistingLogAnalytics ? '' : monitoring!.outputs.connectionString
      }
    ]
    tags: {
      Service: 'MCP Server'
      DeploymentType: 'ACI'
      Environment: environment
    }
  }
}

// Azure Container Instances - Chat Service
module aciChat 'modules/aci.bicep' = if ((deployACI || containerDeploymentTarget == 'aci') && deployChat) {
  name: 'aci-chat-deployment'
  params: {
    containerGroupName: aciChatGroupName
    location: location
    containerImage: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? '${acr!.outputs.acrLoginServer}/platform-engineering-copilot-chat:latest' : 'mcr.microsoft.com/dotnet/samples:aspnetapp'
    containerName: 'chat-service'
    cpuCores: aciCpuCores
    memoryInGB: aciMemoryInGB
    port: 5001
    acrLoginServer: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? acr!.outputs.acrLoginServer : ''
    useManagedIdentity: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci')
    enableVNetIntegration: environment == 'prod'
    subnetId: environment == 'prod' ? (useExistingNetwork ? existingPrivateEndpointSubnet.id : network!.outputs.privateEndpointSubnetId) : ''
    dnsNameLabel: environment != 'prod' ? '${aciChatGroupName}-${uniqueSuffix}' : ''
    logAnalyticsWorkspaceId: useExistingLogAnalytics ? existingLogAnalytics.id : monitoring!.outputs.logAnalyticsWorkspaceId
    environmentVariables: [
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: environment == 'prod' ? 'Production' : 'Development'
      }
      {
        name: 'McpServer__BaseUrl'
        value: 'http://localhost:5100'
      }
      {
        name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
        value: useExistingLogAnalytics ? '' : monitoring!.outputs.connectionString
      }
    ]
    tags: {
      Service: 'Chat'
      DeploymentType: 'ACI'
      Environment: environment
    }
  }
}

// Azure Container Instances - Admin API
module aciAdminApi 'modules/aci.bicep' = if ((deployACI || containerDeploymentTarget == 'aci') && deployAdminApi) {
  name: 'aci-admin-api-deployment'
  params: {
    containerGroupName: aciAdminApiGroupName
    location: location
    containerImage: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? '${acr!.outputs.acrLoginServer}/platform-engineering-copilot-admin-api:latest' : 'mcr.microsoft.com/dotnet/samples:aspnetapp'
    containerName: 'admin-api'
    cpuCores: 1
    memoryInGB: 2
    port: 5002
    acrLoginServer: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? acr!.outputs.acrLoginServer : ''
    useManagedIdentity: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci')
    enableVNetIntegration: environment == 'prod'
    subnetId: environment == 'prod' ? (useExistingNetwork ? existingPrivateEndpointSubnet.id : network!.outputs.privateEndpointSubnetId) : ''
    dnsNameLabel: environment != 'prod' ? '${aciAdminApiGroupName}-${uniqueSuffix}' : ''
    logAnalyticsWorkspaceId: useExistingLogAnalytics ? existingLogAnalytics.id : monitoring!.outputs.logAnalyticsWorkspaceId
    environmentVariables: [
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: environment == 'prod' ? 'Production' : 'Development'
      }
      {
        name: 'ConnectionStrings__DefaultConnection'
        value: replace(database.outputs.connectionStringTemplate, '<PASSWORD>', sqlAdminPassword)
      }
    ]
    tags: {
      Service: 'Admin API'
      DeploymentType: 'ACI'
      Environment: environment
    }
  }
}

// ACI - Admin Client
module aciAdminClient 'modules/aci.bicep' = if ((deployACI || containerDeploymentTarget == 'aci') && deployAdminApi) {
  name: 'aci-admin-client-deployment'
  params: {
    containerGroupName: aciAdminClientGroupName
    location: location
    containerImage: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? '${acr!.outputs.acrLoginServer}/platform-engineering-copilot-admin-client:latest' : 'mcr.microsoft.com/dotnet/samples:aspnetapp'
    containerName: 'admin-client'
    cpuCores: 1
    memoryInGB: 2
    port: 5003
    acrLoginServer: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? acr!.outputs.acrLoginServer : ''
    useManagedIdentity: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci')
    enableVNetIntegration: environment == 'prod'
    subnetId: environment == 'prod' ? (useExistingNetwork ? existingPrivateEndpointSubnet.id : network!.outputs.privateEndpointSubnetId) : ''
    dnsNameLabel: environment != 'prod' ? '${aciAdminClientGroupName}-${uniqueSuffix}' : ''
    logAnalyticsWorkspaceId: useExistingLogAnalytics ? existingLogAnalytics.id : monitoring!.outputs.logAnalyticsWorkspaceId
    environmentVariables: [
      {
        name: 'ASPNETCORE_ENVIRONMENT'
        value: environment == 'prod' ? 'Production' : 'Development'
      }
    ]
    tags: {
      Service: 'Admin Client'
      DeploymentType: 'ACI'
      Environment: environment
    }
  }
}

// ===============================
// KEY VAULT SECRETS
// ===============================

// Helper variable for Key Vault name (existing or new)
var actualKeyVaultName = useExistingKeyVault ? existingKeyVaultName : keyVaultName

// Store SQL connection string in Key Vault
resource sqlConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: '${actualKeyVaultName}/SqlConnectionString'
  properties: {
    value: replace(database.outputs.connectionStringTemplate, '<PASSWORD>', sqlAdminPassword)
    contentType: 'SQL Connection String'
    attributes: {
      enabled: true
    }
  }
}

// Store Application Insights instrumentation key
resource appInsightsKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!useExistingLogAnalytics) {
  name: '${actualKeyVaultName}/ApplicationInsightsInstrumentationKey'
  properties: {
    value: monitoring!.outputs.instrumentationKey
    contentType: 'Application Insights Instrumentation Key'
    attributes: {
      enabled: true
    }
  }
}

// Store Application Insights connection string
resource appInsightsConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!useExistingLogAnalytics) {
  name: '${actualKeyVaultName}/ApplicationInsightsConnectionString'
  properties: {
    value: monitoring!.outputs.connectionString
    contentType: 'Application Insights Connection String'
    attributes: {
      enabled: true
    }
  }
}

// ===============================
// RBAC ASSIGNMENTS
// ===============================

// Grant App Services access to Key Vault (if using App Service)
resource apiKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = if ((containerDeploymentTarget == 'appservice' || containerDeploymentTarget == 'none') && deployAdminApi) {
  name: guid(resourceGroup().id, appServicePlanName, 'admin-api', 'KeyVaultSecretsUser')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: appServices!.outputs.adminApiPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource chatKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = if ((containerDeploymentTarget == 'appservice' || containerDeploymentTarget == 'none') && deployChat) {
  name: guid(resourceGroup().id, appServicePlanName, 'chat', 'KeyVaultSecretsUser')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: appServices!.outputs.chatPrincipalId
    principalType: 'ServicePrincipal'
  }
}

resource mcpKeyVaultAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (containerDeploymentTarget == 'appservice' || containerDeploymentTarget == 'none') {
  name: guid(resourceGroup().id, appServicePlanName, 'mcp', 'KeyVaultSecretsUser')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: appServices!.outputs.mcpPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ===============================
// OUTPUTS
// ===============================

// App Service Outputs (conditional)
@description('Admin API App Service URL')
output adminApiUrl string = (containerDeploymentTarget == 'appservice' || containerDeploymentTarget == 'none') && deployAdminApi ? 'https://${appServices!.outputs.adminApiDefaultHostname}' : ''

@description('Chat App Service URL')
output chatUrl string = (containerDeploymentTarget == 'appservice' || containerDeploymentTarget == 'none') && deployChat ? 'https://${appServices!.outputs.chatDefaultHostname}' : ''

@description('MCP Server URL')
output mcpUrl string = (containerDeploymentTarget == 'appservice' || containerDeploymentTarget == 'none') ? 'https://${appServices!.outputs.mcpDefaultHostname}' : ''

// Container Registry Outputs
@description('Container Registry Login Server')
output acrLoginServer string = (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? acr!.outputs.acrLoginServer : ''

@description('Container Registry Name')
output acrName string = (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? acr!.outputs.acrName : ''

// AKS Outputs
@description('AKS Cluster Name')
output aksClusterName string = (deployAKS || containerDeploymentTarget == 'aks') ? aks!.outputs.aksName : ''

@description('AKS Cluster FQDN')
output aksClusterFqdn string = (deployAKS || containerDeploymentTarget == 'aks') ? aks!.outputs.aksFqdn : ''

@description('AKS OIDC Issuer URL')
output aksOidcIssuerUrl string = (deployAKS || containerDeploymentTarget == 'aks') ? aks!.outputs.aksOidcIssuerUrl : ''

@description('AKS Kubelet Identity Object ID')
output aksKubeletIdentityObjectId string = (deployAKS || containerDeploymentTarget == 'aks') ? aks!.outputs.aksKubeletIdentityObjectId : ''

// ACI Outputs
@description('MCP Container Instance FQDN')
output aciMcpFqdn string = (deployACI || containerDeploymentTarget == 'aci') && environment != 'prod' ? aciMcp!.outputs.containerGroupFqdn : ''

@description('MCP Container Instance IP')
output aciMcpIpAddress string = (deployACI || containerDeploymentTarget == 'aci') ? aciMcp!.outputs.containerGroupIp : ''

@description('Chat Container Instance FQDN')
output aciChatFqdn string = (deployACI || containerDeploymentTarget == 'aci') && deployChat && environment != 'prod' ? aciChat!.outputs.containerGroupFqdn : ''

@description('Chat Container Instance IP')
output aciChatIpAddress string = (deployACI || containerDeploymentTarget == 'aci') && deployChat ? aciChat!.outputs.containerGroupIp : ''

@description('Admin API Container Instance FQDN')
output aciAdminApiFqdn string = (deployACI || containerDeploymentTarget == 'aci') && deployAdminApi && environment != 'prod' ? aciAdminApi!.outputs.containerGroupFqdn : ''

@description('Admin API Container Instance IP')
output aciAdminApiIpAddress string = (deployACI || containerDeploymentTarget == 'aci') && deployAdminApi ? aciAdminApi!.outputs.containerGroupIp : ''

@description('Admin Client Container Instance FQDN')
output aciAdminClientFqdn string = (deployACI || containerDeploymentTarget == 'aci') && deployAdminApi && environment != 'prod' ? aciAdminClient!.outputs.containerGroupFqdn : ''

@description('Admin Client Container Instance IP')
output aciAdminClientIpAddress string = (deployACI || containerDeploymentTarget == 'aci') && deployAdminApi ? aciAdminClient!.outputs.containerGroupIp : ''

// Infrastructure Outputs
@description('SQL Server FQDN')
output sqlServerFqdn string = database.outputs.sqlServerFqdn

@description('Key Vault URI')
output keyVaultUri string = useExistingKeyVault ? existingKeyVault!.properties.vaultUri : keyVault!.outputs.keyVaultUri

@description('Application Insights Instrumentation Key')
output applicationInsightsInstrumentationKey string = useExistingLogAnalytics ? '' : monitoring!.outputs.instrumentationKey

@description('Storage Account Name')
output storageAccountName string = storage.outputs.storageAccountName

@description('Resource Group Name')
output resourceGroupName string = resourceGroup().name

@description('Virtual Network Name')
output virtualNetworkName string = useExistingNetwork ? existingVnetName : network!.outputs.vnetName

@description('Deployment Summary')
output deploymentSummary object = {
  projectName: projectName
  environment: environment
  location: location
  deploymentTarget: containerDeploymentTarget
  
  // Deployment flags
  deployAdminApi: deployAdminApi
  deployChat: deployChat
  useExistingNetwork: useExistingNetwork
  useExistingLogAnalytics: useExistingLogAnalytics
  useExistingKeyVault: useExistingKeyVault
  
  // App Services (if deployed)
  appServices: (containerDeploymentTarget == 'appservice' || containerDeploymentTarget == 'none') ? {
    adminApi: deployAdminApi ? appServices!.outputs.adminApiAppServiceName : 'Not Deployed'
    chat: deployChat ? appServices!.outputs.chatAppServiceName : 'Not Deployed'
    mcp: appServices!.outputs.mcpAppServiceName
  } : null
  
  // Container Infrastructure (if deployed)
  containerRegistry: (deployACR || containerDeploymentTarget == 'aks' || containerDeploymentTarget == 'aci') ? {
    name: acr!.outputs.acrName
    loginServer: acr!.outputs.acrLoginServer
  } : null
  
  // AKS (if deployed)
  aksCluster: (deployAKS || containerDeploymentTarget == 'aks') ? {
    name: aks!.outputs.aksName
    fqdn: aks!.outputs.aksFqdn
    oidcIssuer: aks!.outputs.aksOidcIssuerUrl
  } : null
  
  // ACI (if deployed)
  containerInstances: (deployACI || containerDeploymentTarget == 'aci') ? {
    mcp: {
      name: aciMcp!.outputs.containerGroupName
      ip: aciMcp!.outputs.containerGroupIp
      fqdn: environment != 'prod' ? aciMcp!.outputs.containerGroupFqdn : 'N/A (Private)'
    }
    chat: deployChat ? {
      name: aciChat!.outputs.containerGroupName
      ip: aciChat!.outputs.containerGroupIp
      fqdn: environment != 'prod' ? aciChat!.outputs.containerGroupFqdn : 'N/A (Private)'
    } : 'Not Deployed'
    adminApi: deployAdminApi ? {
      name: aciAdminApi!.outputs.containerGroupName
      ip: aciAdminApi!.outputs.containerGroupIp
      fqdn: environment != 'prod' ? aciAdminApi!.outputs.containerGroupFqdn : 'N/A (Private)'
    } : 'Not Deployed'
    adminClient: deployAdminApi ? {
      name: aciAdminClient!.outputs.containerGroupName
      ip: aciAdminClient!.outputs.containerGroupIp
      fqdn: environment != 'prod' ? aciAdminClient!.outputs.containerGroupFqdn : 'N/A (Private)'
    } : 'Not Deployed'
  } : null
  
  // Core Infrastructure
  sqlServer: database.outputs.sqlServerName
  sqlDatabase: database.outputs.sqlDatabaseName
  keyVault: useExistingKeyVault ? existingKeyVaultName : keyVault!.outputs.keyVaultName
  storageAccount: storage.outputs.storageAccountName
  applicationInsights: useExistingLogAnalytics ? 'Using Existing' : monitoring!.outputs.applicationInsightsName
  virtualNetwork: useExistingNetwork ? existingVnetName : network!.outputs.vnetName
  deployedAt: 'Deployment completed'
}
