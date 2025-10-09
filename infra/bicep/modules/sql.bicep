// SQL Database module for the Platform data storage
@description('Name of the SQL Server')
param sqlServerName string

@description('Name of the SQL Database')
param sqlDatabaseName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Administrator login name for the SQL Server')
param administratorLogin string

@description('Administrator password for the SQL Server')
@secure()
param administratorLoginPassword string

@description('The pricing tier of the SQL Database')
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
param skuName string = 'S0'

@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@description('Azure AD Admin Object ID')
param azureAdAdminObjectId string = ''

@description('Azure AD Admin Login Name')
param azureAdAdminLogin string = ''

@description('Allowed IP addresses for firewall rules')
param allowedIpAddresses array = []

@description('Enable Azure services access')
param allowAzureIps bool = true

// SQL Server
resource sqlServer 'Microsoft.Sql/servers@2023-05-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administratorLogin: administratorLogin
    administratorLoginPassword: administratorLoginPassword
    version: '12.0'
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    administrators: !empty(azureAdAdminObjectId) ? {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: false
      login: azureAdAdminLogin
      sid: azureAdAdminObjectId
      tenantId: subscription().tenantId
    } : null
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformDatabase'
  }
}

// SQL Database
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-05-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: skuName
  }
  properties: {
    collation: 'SQL_Latin1_General_CP1_CI_AS'
    catalogCollation: 'SQL_Latin1_General_CP1_CI_AS'
    maxSizeBytes: skuName == 'Basic' ? 2147483648 : 268435456000 // 2GB for Basic, 250GB for others
    readScale: 'Disabled'
    requestedBackupStorageRedundancy: 'Local'
    isLedgerOn: false
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformDatabase'
  }
}

// Firewall rule for Azure services
resource allowAzureIpsFirewallRule 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = if (allowAzureIps) {
  parent: sqlServer
  name: 'AllowAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// Custom firewall rules
resource customFirewallRules 'Microsoft.Sql/servers/firewallRules@2023-05-01-preview' = [for (ipRange, index) in allowedIpAddresses: {
  parent: sqlServer
  name: 'AllowedIP-${index}'
  properties: {
    startIpAddress: ipRange.start
    endIpAddress: ipRange.end
  }
}]

// Enable Advanced Threat Protection for production
resource advancedThreatProtection 'Microsoft.Sql/servers/securityAlertPolicies@2023-05-01-preview' = if (environment == 'prod') {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    emailAddresses: []
    emailAccountAdmins: true
    retentionDays: 30
  }
}

// Enable auditing for production
resource auditSettings 'Microsoft.Sql/servers/auditingSettings@2023-05-01-preview' = if (environment == 'prod') {
  parent: sqlServer
  name: 'default'
  properties: {
    state: 'Enabled'
    retentionDays: 30
    auditActionsAndGroups: [
      'SUCCESSFUL_DATABASE_AUTHENTICATION_GROUP'
      'FAILED_DATABASE_AUTHENTICATION_GROUP'
      'BATCH_COMPLETED_GROUP'
    ]
    isAzureMonitorTargetEnabled: true
  }
}

// Output values
output sqlServerName string = sqlServer.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output connectionStringTemplate string = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabase.name};Persist Security Info=False;User ID=${administratorLogin};Password=<PASSWORD>;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
