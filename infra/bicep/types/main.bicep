// =============================================================================
// User-Defined Types for Platform Engineering Copilot Infrastructure
// =============================================================================

// =============================================================================
// ENVIRONMENT TYPES
// =============================================================================

@export()
@description('Environment configuration')
type environmentConfig = {
  @description('Environment name')
  name: 'dev' | 'staging' | 'prod'

  @description('Is production environment')
  isProduction: bool

  @description('Resource location')
  location: string
}

// =============================================================================
// CONTAINER DEPLOYMENT TYPES
// =============================================================================

@export()
@description('Container deployment target options')
type containerDeploymentTarget = 'appservice' | 'aks' | 'aci' | 'none'

@export()
@description('Container configuration for a service')
type containerConfig = {
  @description('Container image name')
  imageName: string

  @description('Container image tag')
  imageTag: string

  @description('Number of CPU cores')
  cpuCores: int

  @description('Memory in GB')
  memoryGB: int

  @description('Container port')
  port: int

  @description('Health check path')
  healthPath: string?
}

@export()
@description('Environment variable definition')
type environmentVariable = {
  @description('Variable name')
  name: string

  @description('Variable value')
  value: string
}

// =============================================================================
// DATABASE TYPES
// =============================================================================

@export()
@description('SQL Database SKU options')
type sqlDatabaseSku = 'Basic' | 'S0' | 'S1' | 'S2' | 'S3' | 'P1' | 'P2' | 'P4' | 'P6' | 'P11' | 'P15'

// =============================================================================
// NETWORKING TYPES
// =============================================================================

@export()
@description('Existing resource reference')
type existingResourceRef = {
  @description('Use existing resource')
  useExisting: bool

  @description('Existing resource name')
  name: string?

  @description('Existing resource group')
  resourceGroup: string?
}

// =============================================================================
// CONTAINER REGISTRY TYPES
// =============================================================================

@export()
@description('ACR SKU options')
type acrSku = 'Basic' | 'Standard' | 'Premium'

// =============================================================================
// KEY VAULT TYPES
// =============================================================================

@export()
@description('Key Vault SKU options')
type keyVaultSku = 'standard' | 'premium'

// =============================================================================
// APP SERVICE TYPES
// =============================================================================

@export()
@description('App Service Plan SKU options')
type appServiceSku =
  | 'F1'
  | 'B1'
  | 'B2'
  | 'B3'
  | 'S1'
  | 'S2'
  | 'S3'
  | 'P1v3'
  | 'P2v3'
  | 'P3v3'
  | 'P1mv3'
  | 'P2mv3'
  | 'P3mv3'
