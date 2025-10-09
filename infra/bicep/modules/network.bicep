// Virtual Network module for secure networking
@description('Name of the Virtual Network')
param vnetName string

@description('Location for all resources')
param location string = resourceGroup().location

@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@description('Virtual Network address prefix')
param vnetAddressPrefix string = '10.0.0.0/16'

@description('App Service subnet address prefix')
param appServiceSubnetPrefix string = '10.0.1.0/24'

@description('Private endpoint subnet address prefix')
param privateEndpointSubnetPrefix string = '10.0.2.0/24'

@description('Management subnet address prefix')
param managementSubnetPrefix string = '10.0.3.0/24'

// Network Security Group for App Service subnet
resource appServiceNsg 'Microsoft.Network/networkSecurityGroups@2023-09-01' = {
  name: '${vnetName}-appservice-nsg'
  location: location
  properties: {
    securityRules: [
      {
        name: 'AllowHTTPS'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '443'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 1000
          direction: 'Inbound'
        }
      }
      {
        name: 'AllowHTTP'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '80'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 1001
          direction: 'Inbound'
        }
      }
      {
        name: 'AllowMCP'
        properties: {
          protocol: 'Tcp'
          sourcePortRange: '*'
          destinationPortRange: '8080'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Allow'
          priority: 1002
          direction: 'Inbound'
        }
      }
      {
        name: 'DenyAllInbound'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Deny'
          priority: 4096
          direction: 'Inbound'
        }
      }
    ]
  }
  tags: {
    Environment: environment
    Purpose: 'NetworkSecurity'
  }
}

// Network Security Group for Private Endpoints
resource privateEndpointNsg 'Microsoft.Network/networkSecurityGroups@2023-09-01' = {
  name: '${vnetName}-privateendpoint-nsg'
  location: location
  properties: {
    securityRules: [
      {
        name: 'AllowVnetInbound'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: 'VirtualNetwork'
          destinationAddressPrefix: 'VirtualNetwork'
          access: 'Allow'
          priority: 1000
          direction: 'Inbound'
        }
      }
      {
        name: 'DenyAllInbound'
        properties: {
          protocol: '*'
          sourcePortRange: '*'
          destinationPortRange: '*'
          sourceAddressPrefix: '*'
          destinationAddressPrefix: '*'
          access: 'Deny'
          priority: 4096
          direction: 'Inbound'
        }
      }
    ]
  }
  tags: {
    Environment: environment
    Purpose: 'NetworkSecurity'
  }
}

// Virtual Network
resource virtualNetwork 'Microsoft.Network/virtualNetworks@2023-09-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      {
        name: 'app-service-subnet'
        properties: {
          addressPrefix: appServiceSubnetPrefix
          networkSecurityGroup: {
            id: appServiceNsg.id
          }
          delegations: [
            {
              name: 'Microsoft.Web.serverFarms'
              properties: {
                serviceName: 'Microsoft.Web/serverFarms'
              }
            }
          ]
          serviceEndpoints: [
            {
              service: 'Microsoft.Sql'
            }
            {
              service: 'Microsoft.KeyVault'
            }
            {
              service: 'Microsoft.Storage'
            }
          ]
        }
      }
      {
        name: 'private-endpoint-subnet'
        properties: {
          addressPrefix: privateEndpointSubnetPrefix
          networkSecurityGroup: {
            id: privateEndpointNsg.id
          }
          privateEndpointNetworkPolicies: 'Disabled'
          privateLinkServiceNetworkPolicies: 'Enabled'
        }
      }
      {
        name: 'management-subnet'
        properties: {
          addressPrefix: managementSubnetPrefix
          serviceEndpoints: [
            {
              service: 'Microsoft.Sql'
            }
            {
              service: 'Microsoft.KeyVault'
            }
            {
              service: 'Microsoft.Storage'
            }
          ]
        }
      }
    ]
    enableDdosProtection: false
  }
  tags: {
    Environment: environment
    Purpose: 'PlatformNetworking'
  }
}

// Route Table for App Service subnet
resource appServiceRouteTable 'Microsoft.Network/routeTables@2023-09-01' = {
  name: '${vnetName}-appservice-rt'
  location: location
  properties: {
    routes: [
      {
        name: 'DefaultRoute'
        properties: {
          addressPrefix: '0.0.0.0/0'
          nextHopType: 'Internet'
        }
      }
    ]
    disableBgpRoutePropagation: false
  }
  tags: {
    Environment: environment
    Purpose: 'NetworkRouting'
  }
}

// Output values
output vnetId string = virtualNetwork.id
output vnetName string = virtualNetwork.name
output appServiceSubnetId string = '${virtualNetwork.id}/subnets/app-service-subnet'
output privateEndpointSubnetId string = '${virtualNetwork.id}/subnets/private-endpoint-subnet'
output managementSubnetId string = '${virtualNetwork.id}/subnets/management-subnet'
output appServiceNsgId string = appServiceNsg.id
output privateEndpointNsgId string = privateEndpointNsg.id
