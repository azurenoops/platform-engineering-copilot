// Key Vault module for secure storage of secrets and configuration
@description('Name of the Key Vault')
param keyVaultName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@description('Object ID of the user or service principal to grant access')
param principalId string



@description('Additional access policies for applications')
param accessPolicies array = []

@description('Enable soft delete (recommended for production)')
param enableSoftDelete bool = environment == 'prod'

@description('Soft delete retention days')
param softDeleteRetentionInDays int = 90

@description('Enable purge protection (recommended for production)')
param enablePurgeProtection bool = environment == 'prod'

@description('SKU name for Key Vault')
@allowed([
  'standard'
  'premium'
])
param skuName string = 'standard'

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: skuName
    }
    tenantId: subscription().tenantId
    enableSoftDelete: enableSoftDelete
    softDeleteRetentionInDays: enableSoftDelete ? softDeleteRetentionInDays : null
    enablePurgeProtection: enablePurgeProtection ? true : null
    enableRbacAuthorization: false
    enabledForDeployment: false
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: false
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    accessPolicies: concat([
      {
        tenantId: subscription().tenantId
        objectId: principalId
        permissions: {
          keys: [
            'Get'
            'List'
            'Update'
            'Create'
            'Import'
            'Delete'
            'Recover'
            'Backup'
            'Restore'
          ]
          secrets: [
            'Get'
            'List'
            'Set'
            'Delete'
            'Recover'
            'Backup'
            'Restore'
          ]
          certificates: [
            'Get'
            'List'
            'Update'
            'Create'
            'Import'
            'Delete'
            'Recover'
            'Backup'
            'Restore'
            'ManageContacts'
            'ManageIssuers'
            'GetIssuers'
            'ListIssuers'
            'SetIssuers'
            'DeleteIssuers'
          ]
        }
      }
    ], accessPolicies)
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformSecrets'
  }
}

// Diagnostic settings for audit logging
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${keyVaultName}-diagnostics'
  scope: keyVault
  properties: {
    logs: [
      {
        categoryGroup: 'audit'
        enabled: true
        retentionPolicy: {
          enabled: environment == 'prod'
          days: environment == 'prod' ? 365 : 30
        }
      }
    ]
    metrics: [
      {
        category: 'AllMetrics'
        enabled: true
        retentionPolicy: {
          enabled: environment == 'prod'
          days: environment == 'prod' ? 365 : 30
        }
      }
    ]
  }
}

// Output values
output keyVaultId string = keyVault.id
output keyVaultName string = keyVault.name
output keyVaultUri string = keyVault.properties.vaultUri
