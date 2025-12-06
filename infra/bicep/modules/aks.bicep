// =============================================================================
// Azure Kubernetes Service (AKS) - IL5/IL6 Compliant
// =============================================================================
// Creates an AKS cluster with:
// - Private cluster (no public API endpoint)
// - Azure Policy for Pod Security
// - Azure AD integration with RBAC
// - Managed identity for Azure resource access
// - Network policies and CNI
// - Microsoft Defender for Containers
// - Azure Key Vault integration
// - Monitoring and logging
// =============================================================================

@description('The name of the AKS cluster')
param clusterName string

@description('Location for the AKS cluster')
param location string = resourceGroup().location

@description('Kubernetes version')
param kubernetesVersion string = '1.28.3'

@description('DNS prefix for the cluster')
param dnsPrefix string = '${clusterName}-dns'

@description('Enable private cluster (required for IL5/IL6)')
param enablePrivateCluster bool = true

@description('System node pool VM size')
param systemNodeVmSize string = 'Standard_D4s_v5'

@description('System node pool count')
@minValue(3)
@maxValue(10)
param systemNodeCount int = 3

@description('User node pool VM size')
param userNodeVmSize string = 'Standard_D8s_v5'

@description('User node pool count')
@minValue(3)
@maxValue(100)
param userNodeCount int = 3

@description('Enable auto-scaling for user node pool')
param enableAutoScaling bool = true

@description('Minimum node count for auto-scaling')
param minNodeCount int = 3

@description('Maximum node count for auto-scaling')
param maxNodeCount int = 10

@description('Virtual Network resource ID for AKS')
param vnetId string

@description('Subnet resource ID for AKS nodes')
param subnetId string

@description('Subnet resource ID for pods (CNI overlay)')
param podSubnetId string = ''

@description('Enable Azure Policy for Kubernetes')
param enableAzurePolicy bool = true

@description('Enable Microsoft Defender for Containers')
param enableDefender bool = true

@description('Enable Azure Monitor for containers')
param enableMonitoring bool = true

@description('Log Analytics Workspace ID for monitoring')
param logAnalyticsWorkspaceId string

@description('Enable Azure AD integration')
param enableAzureAD bool = true

@description('Azure AD admin group object IDs')
param aadAdminGroupObjectIds array = []

@description('Enable Azure RBAC for Kubernetes authorization')
param enableAzureRBAC bool = true

@description('Network plugin (azure or kubenet)')
@allowed(['azure', 'kubenet'])
param networkPlugin string = 'azure'

@description('Network policy (azure, calico, or none)')
@allowed(['azure', 'calico', 'none'])
param networkPolicy string = 'azure'

@description('Service CIDR for Kubernetes services')
param serviceCidr string = '10.0.0.0/16'

@description('DNS service IP')
param dnsServiceIP string = '10.0.0.10'

@description('Resource tags for compliance')
param tags object = {
  Environment: 'Production'
  Classification: 'CUI'
  ImpactLevel: 'IL5'
  MissionOwner: 'Platform Engineering'
  Compliance: 'NIST-800-53'
}

@description('Enable encryption at host')
param enableEncryptionAtHost bool = true

@description('Enable FIPS for nodes')
param enableFIPS bool = true

@description('Enable workload identity')
param enableWorkloadIdentity bool = true

@description('Enable OIDC issuer')
param enableOIDCIssuer bool = true

@description('Enable image cleaner')
param enableImageCleaner bool = true

@description('Container Registry resource ID for ACR pull')
param acrId string

@description('Enable node public IP (false for IL5/IL6)')
param enableNodePublicIP bool = false

@description('Outbound type (loadBalancer or userDefinedRouting)')
@allowed(['loadBalancer', 'userDefinedRouting'])
param outboundType string = 'userDefinedRouting'

// =============================================================================
// AKS Cluster
// =============================================================================
resource aksCluster 'Microsoft.ContainerService/managedClusters@2023-10-01' = {
  name: clusterName
  location: location
  tags: tags
  identity: {
    type: 'SystemAssigned'
  }
  sku: {
    name: 'Base'
    tier: 'Standard' // Standard tier for production, supports SLA
  }
  properties: {
    kubernetesVersion: kubernetesVersion
    dnsPrefix: dnsPrefix
    
    // Private cluster configuration
    apiServerAccessProfile: enablePrivateCluster ? {
      enablePrivateCluster: true
      enablePrivateClusterPublicFQDN: false
      privateDNSZone: 'system' // Managed private DNS zone
    } : {
      enablePrivateCluster: false
    }
    
    // Azure AD integration
    aadProfile: enableAzureAD ? {
      managed: true
      enableAzureRBAC: enableAzureRBAC
      adminGroupObjectIDs: aadAdminGroupObjectIds
      tenantID: subscription().tenantId
    } : null
    
    // System node pool (required, runs critical system pods)
    agentPoolProfiles: [
      {
        name: 'system'
        count: systemNodeCount
        vmSize: systemNodeVmSize
        osType: 'Linux'
        osSKU: 'Ubuntu'
        mode: 'System'
        type: 'VirtualMachineScaleSets'
        vnetSubnetID: subnetId
        podSubnetID: networkPlugin == 'azure' && !empty(podSubnetId) ? podSubnetId : null
        enableAutoScaling: false
        availabilityZones: [
          '1'
          '2'
          '3'
        ]
        enableEncryptionAtHost: enableEncryptionAtHost
        enableFIPS: enableFIPS
        enableNodePublicIP: enableNodePublicIP
        maxPods: 110
        nodeTaints: [
          'CriticalAddonsOnly=true:NoSchedule'
        ]
        tags: union(tags, {
          NodePool: 'System'
        })
      }
    ]
    
    // Network configuration
    networkProfile: {
      networkPlugin: networkPlugin
      networkPolicy: networkPolicy
      serviceCidr: serviceCidr
      dnsServiceIP: dnsServiceIP
      outboundType: outboundType
      loadBalancerSku: 'standard'
      // Pod CIDR only for kubenet
      podCidr: networkPlugin == 'kubenet' ? '10.244.0.0/16' : null
    }
    
    // Add-ons and features
    addonProfiles: {
      // Azure Policy
      azurepolicy: {
        enabled: enableAzurePolicy
        config: {
          version: 'v2'
        }
      }
      // Azure Monitor
      omsagent: enableMonitoring ? {
        enabled: true
        config: {
          logAnalyticsWorkspaceResourceID: logAnalyticsWorkspaceId
        }
      } : {
        enabled: false
      }
      // Azure Key Vault Secrets Provider
      azureKeyvaultSecretsProvider: {
        enabled: true
        config: {
          enableSecretRotation: 'true'
          rotationPollInterval: '2m'
        }
      }
    }
    
    // Security features
    securityProfile: {
      // Microsoft Defender for Containers
      defender: enableDefender ? {
        logAnalyticsWorkspaceResourceId: logAnalyticsWorkspaceId
        securityMonitoring: {
          enabled: true
        }
      } : null
      
      // Workload identity (OIDC)
      workloadIdentity: enableWorkloadIdentity ? {
        enabled: true
      } : null
      
      // Image cleaner
      imageCleaner: enableImageCleaner ? {
        enabled: true
        intervalHours: 24
      } : null
    }
    
    // OIDC issuer for workload identity
    oidcIssuerProfile: enableOIDCIssuer ? {
      enabled: true
    } : null
    
    // Auto-upgrade channel
    autoUpgradeProfile: {
      upgradeChannel: 'patch' // Auto-upgrade to latest patch version
    }
    
    // Disable local accounts for Azure AD-only authentication
    disableLocalAccounts: enableAzureAD
    
    // Enable RBAC
    enableRBAC: true
    
    // Node resource group
    nodeResourceGroup: '${clusterName}-nodes-rg'
  }
}

// =============================================================================
// User Node Pool (for application workloads)
// =============================================================================
resource userNodePool 'Microsoft.ContainerService/managedClusters/agentPools@2023-10-01' = {
  parent: aksCluster
  name: 'user'
  properties: {
    count: userNodeCount
    vmSize: userNodeVmSize
    osType: 'Linux'
    osSKU: 'Ubuntu'
    mode: 'User'
    type: 'VirtualMachineScaleSets'
    vnetSubnetID: subnetId
    podSubnetID: networkPlugin == 'azure' && !empty(podSubnetId) ? podSubnetId : null
    enableAutoScaling: enableAutoScaling
    minCount: enableAutoScaling ? minNodeCount : null
    maxCount: enableAutoScaling ? maxNodeCount : null
    availabilityZones: [
      '1'
      '2'
      '3'
    ]
    enableEncryptionAtHost: enableEncryptionAtHost
    enableFIPS: enableFIPS
    enableNodePublicIP: enableNodePublicIP
    maxPods: 110
    tags: union(tags, {
      NodePool: 'User'
    })
  }
}

// =============================================================================
// ACR Pull Role Assignment
// =============================================================================
resource acrPullRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(acrId)) {
  name: guid(aksCluster.id, acrId, 'AcrPull')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d') // AcrPull role
    principalId: aksCluster.properties.identityProfile.kubeletidentity.objectId
    principalType: 'ServicePrincipal'
  }
}

// =============================================================================
// Diagnostic Settings
// =============================================================================
resource diagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${clusterName}-diagnostics'
  scope: aksCluster
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        category: 'kube-apiserver'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'kube-controller-manager'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'kube-scheduler'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'kube-audit'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'kube-audit-admin'
        enabled: true
        retentionPolicy: {
          enabled: true
          days: 90
        }
      }
      {
        category: 'guard'
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
output aksId string = aksCluster.id
output aksName string = aksCluster.name
output aksFqdn string = aksCluster.properties.fqdn
output aksPrivateFqdn string = enablePrivateCluster ? aksCluster.properties.privateFQDN : ''
output aksKubeletIdentityObjectId string = aksCluster.properties.identityProfile.kubeletidentity.objectId
output aksOidcIssuerUrl string = enableOIDCIssuer ? aksCluster.properties.oidcIssuerProfile.issuerURL : ''
output aksNodeResourceGroup string = aksCluster.properties.nodeResourceGroup
