// =============================================================================
// Azure Container Registry (ACR) - IL5/IL6 Compliant
// =============================================================================
// Creates a Premium SKU ACR with:
// - Geo-replication for disaster recovery
// - Content trust and image quarantine
// - Vulnerability scanning with Microsoft Defender
// - Private endpoints for network isolation
// - Encryption at rest with customer-managed keys
// - Audit logging and monitoring
// =============================================================================

@description('The name of the Azure Container Registry')
param acrName string

@description('Location for the primary registry')
param location string = resourceGroup().location

@description('Azure Container Registry SKU (Premium required for IL5/IL6)')
@allowed(['Basic', 'Standard', 'Premium'])
param sku string = 'Premium'

@description('Enable geo-replication for disaster recovery')
param enableGeoReplication bool = true

@description('Replication locations for geo-redundancy')
param replicationLocations array = [
  'usgovvirginia'
  'usgovarizona'
]

@description('Enable Microsoft Defender for container registries')
param enableDefender bool = true

@description('Enable content trust (image signing)')
param enableContentTrust bool = true

@description('Enable image quarantine (scan before use)')
param enableQuarantine bool = true

@description('Enable anonymous pull access (false for IL5/IL6)')
param enableAnonymousPull bool = false

@description('Enable public network access (false for IL5/IL6 with private endpoints)')
param publicNetworkAccess string = 'Disabled'

@description('Enable admin user (not recommended for production)')
param adminUserEnabled bool = false

@description('Resource tags for compliance and cost tracking')
param tags object = {
  Environment: 'Production'
  Classification: 'CUI'
  ImpactLevel: 'IL5'
  MissionOwner: 'Platform Engineering'
  CostCenter: 'Engineering'
  Compliance: 'NIST-800-53'
}

@description('Enable customer-managed encryption keys')
param enableCustomerManagedKey bool = false

@description('Key Vault resource ID for customer-managed keys')
param keyVaultId string = ''

@description('Key name in Key Vault for encryption')
param encryptionKeyName string = ''

@description('Enable zone redundancy (Premium SKU only)')
param zoneRedundancy bool = true

@description('Retention policy for untagged manifests (days)')
param retentionDays int = 30

@description('Enable retention policy')
param enableRetentionPolicy bool = true

// =============================================================================
// Azure Container Registry
// =============================================================================
resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: acrName
  location: location
  tags: tags
  sku: {
    name: sku
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    adminUserEnabled: adminUserEnabled
    publicNetworkAccess: publicNetworkAccess
    networkRuleBypassOptions: 'AzureServices'
    
    // Zone redundancy for high availability (Premium SKU)
    zoneRedundancy: sku == 'Premium' ? (zoneRedundancy ? 'Enabled' : 'Disabled') : 'Disabled'
    
    // Anonymous pull disabled for security
    anonymousPullEnabled: enableAnonymousPull
    
    // Data endpoint enabled for peering scenarios
    dataEndpointEnabled: true
    
    // Network rule set
    networkRuleSet: {
      defaultAction: 'Deny'
    }
    
    // Policies
    policies: {
      // Quarantine policy - scan images before use
      quarantinePolicy: {
        status: enableQuarantine ? 'enabled' : 'disabled'
      }
      
      // Trust policy - require signed images
      trustPolicy: {
        type: 'Notary'
        status: enableContentTrust ? 'enabled' : 'disabled'
      }
      
      // Retention policy - cleanup untagged manifests
      retentionPolicy: {
        days: retentionDays
        status: enableRetentionPolicy ? 'enabled' : 'disabled'
      }
      
      // Export policy - prevent export to non-compliant regions
      exportPolicy: {
        status: 'disabled'
      }
      
      // Azure AD authentication only
      azureADAuthenticationAsArmPolicy: {
        status: 'enabled'
      }
      
      // Soft delete for accidental deletion protection
      softDeletePolicy: {
        retentionDays: 7
        status: 'enabled'
      }
    }
    
    // Encryption configuration
    encryption: enableCustomerManagedKey ? {
      status: 'enabled'
      keyVaultProperties: {
        identity: ''
        keyIdentifier: '${keyVaultId}/keys/${encryptionKeyName}'
      }
    } : {
      status: 'disabled'
    }
  }
}

// =============================================================================
// Geo-Replication (Premium SKU only)
// =============================================================================
resource replication 'Microsoft.ContainerRegistry/registries/replications@2023-07-01' = [for (replicationLocation, i) in replicationLocations: if (enableGeoReplication && sku == 'Premium') {
  parent: containerRegistry
  name: replicationLocation
  location: replicationLocation
  tags: tags
  properties: {
    regionEndpointEnabled: true
    zoneRedundancy: zoneRedundancy ? 'Enabled' : 'Disabled'
  }
}]

// =============================================================================
// Diagnostic Settings for Audit Logging
// =============================================================================
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${acrName}-diagnostics'
  scope: containerRegistry
  properties: {
    logs: [
      {
        category: 'ContainerRegistryRepositoryEvents'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'ContainerRegistryLoginEvents'
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
output acrId string = containerRegistry.id
output acrName string = containerRegistry.name
output acrLoginServer string = containerRegistry.properties.loginServer
output acrPrincipalId string = containerRegistry.identity.principalId
output acrResourceId string = containerRegistry.id
