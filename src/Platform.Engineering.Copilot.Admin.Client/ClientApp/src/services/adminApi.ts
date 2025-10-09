import axios from 'axios';

// In development, use proxy (relative path). In production, use full URL.
const API_BASE_URL = process.env.NODE_ENV === 'production' 
  ? (process.env.REACT_APP_ADMIN_API_URL || 'http://localhost:7002')
  : ''; // Empty string means relative to current origin, which will use the proxy

// Enums
export enum NetworkMode {
  CreateNew = 'CreateNew',
  UseExisting = 'UseExisting'
}

export enum SubnetPurpose {
  Application = 'Application',
  PrivateEndpoints = 'PrivateEndpoints',
  ApplicationGateway = 'ApplicationGateway',
  Database = 'Database',
  Other = 'Other'
}

// Subnet Interfaces
export interface SubnetConfiguration {
  name: string;
  addressPrefix: string;
  delegation?: string;
  enableServiceEndpoints?: boolean;
  serviceEndpoints?: string[];
  purpose: SubnetPurpose;
}

export interface ExistingSubnetReference {
  name: string;
  subnetId: string;
  addressPrefix: string;
  purpose: SubnetPurpose;
}

export interface NetworkSecurityRule {
  name: string;
  priority: number;
  direction: string;
  access: string;
  protocol: string;
  sourcePortRange: string;
  destinationPortRange: string;
  sourceAddressPrefix: string;
  destinationAddressPrefix: string;
  description?: string;
}

// Updated NetworkConfiguration interface
export interface NetworkConfiguration {
  // Network Mode
  mode?: NetworkMode;
  
  // Existing Network References (when mode = UseExisting)
  existingVNetResourceId?: string;
  existingVNetName?: string;
  existingVNetResourceGroup?: string;
  existingSubnets?: ExistingSubnetReference[];
  
  // New Network Configuration (when mode = CreateNew)
  vnetName?: string;
  addressSpace?: string;
  location?: string;
  resourceGroup?: string;
  dnsServers?: string[];
  
  // Subnet Configuration (for new networks)
  subnets?: SubnetConfiguration[];
  
  // Legacy single subnet fields (for backward compatibility)
  subnetName?: string;
  subnetAddressPrefix?: string;
  
  // Security Features
  enableServiceEndpoints?: boolean;
  serviceEndpoints?: string[];
  enablePrivateEndpoint?: boolean;
  
  // Network Security Group
  enableNetworkSecurityGroup?: boolean;
  nsgMode?: 'new' | 'existing';  // Create new or use existing NSG
  nsgName?: string;
  existingNsgResourceId?: string;
  nsgRules?: NetworkSecurityRule[];
  
  // DDoS Protection
  enableDdosProtection?: boolean;
  ddosMode?: 'new' | 'existing';  // Create new or use existing DDoS Plan
  ddosProtectionPlanId?: string;
  
  // Private DNS
  enablePrivateDns?: boolean;
  privateDnsMode?: 'new' | 'existing';  // Create new or use existing Private DNS Zone
  privateDnsZoneName?: string;
  existingPrivateDnsZoneResourceId?: string;
  privateEndpointSubnetName?: string;
  
  // VNet Peering/Links
  enableVNetPeering?: boolean;
  vnetPeerings?: VNetPeering[];
}

export interface VNetPeering {
  name: string;
  remoteVNetResourceId: string;
  remoteVNetName?: string;
  allowVirtualNetworkAccess?: boolean;
  allowForwardedTraffic?: boolean;
  allowGatewayTransit?: boolean;
  useRemoteGateways?: boolean;
}

export interface ComputeConfiguration {
  instanceType?: string;
  minInstances?: number;
  maxInstances?: number;
  enableAutoScaling?: boolean;
  cpuLimit?: string;
  memoryLimit?: string;
  storageSize?: string;
  enableSpotInstances?: boolean;
  containerImage?: string;
  nodePoolName?: string;
}

export interface CreateTemplateRequest {
  templateName: string;
  serviceName: string;
  templateType?: string;
  description?: string;
  application?: {
    language: string;
    type: string;
    port?: number;
    framework?: string;
  };
  databases?: Array<{
    name: string;
    type: string;
    location: string;
    version?: string;
  }>;
  infrastructure?: {
    resourceType?: string;  // 'Compute' or 'Network'
    format: string;
    computePlatform: string;
    cloudProvider?: string;
    region?: string;
    includeNetworking?: boolean;
    includeStorage?: boolean;
    includeLoadBalancer?: boolean;
    // AKS Zero Trust Security Parameters
    enablePrivateCluster?: boolean;
    authorizedIPRanges?: string;
    enableWorkloadIdentity?: boolean;
    logAnalyticsWorkspaceId?: string;
    enableAzurePolicy?: boolean;
    enableImageCleaner?: boolean;
    diskEncryptionSetId?: string;
    enablePrivateEndpointACR?: boolean;
    // App Service Zero Trust Security Parameters
    httpsOnly?: boolean;
    enableVnetIntegration?: boolean;
    vnetSubnetId?: string;
    enablePrivateEndpoint?: boolean;
    ftpsState?: string;
    minTlsVersion?: string;
    enableManagedIdentity?: boolean;
    enableClientCertificate?: boolean;
    clientCertMode?: string;
    // Container Apps Zero Trust Security Parameters
    enablePrivateEndpointCA?: boolean;
    enableManagedIdentityCA?: boolean;
    enableIPRestrictionsCA?: boolean;
    
    // === AWS TERRAFORM ZERO TRUST PARAMETERS ===
    
    // ECS (AWS Elastic Container Service) - 18 parameters
    enableServiceConnect?: boolean;
    enableECSExec?: boolean;
    enableSecretsManager?: boolean;
    httpsListener?: boolean;
    sslCertificateArn?: string;
    enableWAF?: boolean;
    webAclArn?: string;
    allowedCIDRBlocks?: string;
    enableVPCEndpoints?: boolean;
    enableGuardDuty?: boolean;
    enableCloudTrail?: boolean;
    enableECRScanning?: boolean;
    allowedRegistries?: string;
    enableNetworkIsolation?: boolean;
    enableReadOnlyRootFS?: boolean;
    enableDropCapabilities?: boolean;
    enableKMSEncryption?: boolean;
    kmsKeyId?: string;
    
    // EKS (AWS Elastic Kubernetes Service) - 20 parameters
    enablePrivateEndpointEKS?: boolean;
    enableIRSA?: boolean;
    enablePodSecurity?: boolean;
    enableNetworkPolicies?: boolean;
    enableGuardDutyEKS?: boolean;
    enableKMSEncryptionEKS?: boolean;
    enableVPCCNIEncryption?: boolean;
    allowedAPIBlocks?: string;
    enableFargateProfiles?: boolean;
    enableManagedNodeGroups?: boolean;
    enableSpotInstancesEKS?: boolean;
    enableClusterAutoscaler?: boolean;
    enableALBController?: boolean;
    enableEBSCSI?: boolean;
    enableEFSCSI?: boolean;
    enableContainerInsights?: boolean;
    enablePodIdentity?: boolean;
    enableIMDSv2?: boolean;
    enableECRPrivateEndpoint?: boolean;
    eksKMSKeyId?: string;
    
    // Lambda (AWS Serverless) - 15 parameters
    enableVPCConfig?: boolean;
    enableKMSEncryptionLambda?: boolean;
    enableSecretsManagerLambda?: boolean;
    enableCodeSigning?: boolean;
    enableFunctionURLAuth?: boolean;
    enablePrivateAPI?: boolean;
    enableWAFLambda?: boolean;
    enableAPIKeyRequired?: boolean;
    enableCloudWatchLogsEncryption?: boolean;
    enableGuardDutyLambda?: boolean;
    enableResourceBasedPolicy?: boolean;
    allowedPrincipals?: string;
    enableLayerVersionValidation?: boolean;
    lambdaVPCSubnetIds?: string;
    lambdaKMSKeyId?: string;
    
    // === GCP TERRAFORM ZERO TRUST PARAMETERS ===
    
    // Cloud Run (GCP Serverless Containers) - 18 parameters
    enableVPCConnector?: boolean;
    ingressSettings?: string;
    enableServiceIdentity?: boolean;
    enableBinaryAuthorization?: boolean;
    enableCloudArmor?: boolean;
    enableCMEK?: boolean;
    enableCloudAuditLogs?: boolean;
    enableCloudMonitoring?: boolean;
    maxInstanceConcurrency?: number;
    enableHTTPSOnly?: boolean;
    allowedIngressSources?: string;
    enableVPCEgress?: boolean;
    egressSettings?: string;
    enableExecutionEnvironmentV2?: boolean;
    enableCPUThrottling?: boolean;
    enableStartupCPUBoost?: boolean;
    enableSessionAffinity?: boolean;
    cloudRunKMSKeyId?: string;
    
    // GKE (Google Kubernetes Engine) - 20 parameters
    enablePrivateClusterGKE?: boolean;
    masterIPV4CIDRBlock?: string;
    enableWorkloadIdentityGKE?: boolean;
    enableBinaryAuthorizationGKE?: boolean;
    enableShieldedNodes?: boolean;
    enableGKEAutopilot?: boolean;
    enableNetworkPoliciesGKE?: boolean;
    enableCloudArmorGKE?: boolean;
    enablePodSecurityPolicy?: boolean;
    enableSecureBoot?: boolean;
    enableIntegrityMonitoring?: boolean;
    enableKMSEncryptionGKE?: boolean;
    masterAuthorizedNetworks?: string;
    enableVPCNative?: boolean;
    enablePrivateEndpointGKE?: boolean;
    enableIntranodeVisibility?: boolean;
    enableDataplaneV2?: boolean;
    enableVulnerabilityScanning?: boolean;
    enableSecurityPosture?: boolean;
    gkeKMSKeyId?: string;
    
    // === AZURE TERRAFORM ZERO TRUST PARAMETERS ===
    
    // AKS Terraform (mirrors Bicep) - 21 parameters
    enablePrivateClusterTF?: boolean;
    authorizedIPRangesTF?: string;
    enableWorkloadIdentityTF?: boolean;
    logAnalyticsWorkspaceIdTF?: string;
    enableAzurePolicyTF?: boolean;
    enableImageCleanerTF?: boolean;
    imageCleanerIntervalHours?: number;
    diskEncryptionSetIdTF?: string;
    enableDefender?: boolean;
    enablePrivateEndpointACRTF?: boolean;
    acrSubnetId?: string;
    enableAzureRBAC?: boolean;
    enableOIDCIssuer?: boolean;
    enablePodSecurityPolicyTF?: boolean;
    networkPolicy?: string;
    enableHTTPApplicationRouting?: boolean;
    enableKeyVaultSecretsProvider?: boolean;
    secretRotationPollInterval?: string;
    enableAutoScalingTF?: boolean;
    maintenanceWindow?: string;
    nodePoolSubnetId?: string;
    
    // App Service Terraform - 15 parameters
    enableVnetIntegrationTF?: boolean;
    appServiceSubnetId?: string;
    enablePrivateEndpointTF?: boolean;
    enableManagedIdentityTF?: boolean;
    httpsOnlyTF?: boolean;
    minTlsVersionTF?: string;
    ftpsStateTF?: string;
    enableIPRestrictions?: boolean;
    allowedIPAddresses?: string;
    enableClientCertificateTF?: boolean;
    clientCertModeTF?: string;
    enableAlwaysEncrypted?: boolean;
    keyVaultId?: string;
    enableAppServiceAuth?: boolean;
    enableDefenderAppService?: boolean;
    
    // Container Instances Terraform - 16 parameters
    enableVnetIntegrationCI?: boolean;
    containerInstancesSubnetId?: string;
    enableManagedIdentityCI?: boolean;
    enablePrivateEndpointCI?: boolean;
    enableImageScanning?: boolean;
    enableContentTrust?: boolean;
    enableDefenderCI?: boolean;
    enableZoneRedundancy?: boolean;
    enablePublicNetworkAccess?: boolean;
    allowedIPRangesCI?: string;
    enableEncryptionCMEK?: boolean;
    keyVaultKeyId?: string;
    enableLogAnalyticsCI?: boolean;
    logAnalyticsWorkspaceIdCI?: string;
    enableAzureMonitorCI?: boolean;
    restartPolicy?: string;
    
    // Resource Tags (all platforms)
    tags?: Record<string, string>;
  };
  compute?: ComputeConfiguration;
  network?: NetworkConfiguration;
  deployment?: {
    orchestrator: string;
    cicdPlatform?: string;
  };
  security?: {
    authenticationProvider?: string;
    secrets?: Array<{ name: string; description?: string }>;
  };
  observability?: {
    logging?: boolean;
    metrics?: boolean;
    tracing?: boolean;
    applicationInsights?: boolean;
  };
}

export interface UpdateTemplateRequest extends Partial<CreateTemplateRequest> {
  regenerateFiles?: boolean;
}

export interface TemplateResponse {
  success: boolean;
  templateId: string;
  templateName: string;
  generatedFiles: string[];
  componentsGenerated?: string[];
  errorMessage?: string;
  summary?: string;
}

// Validation Types
export interface ValidationError {
  field: string;
  message: string;
  code: string;
  currentValue?: string;
  expectedValue?: string;
  documentationUrl?: string;
}

export interface ValidationWarning {
  field: string;
  message: string;
  code: string;
  severity: string;
  impact?: string;
}

export interface ValidationRecommendation {
  field: string;
  message: string;
  code: string;
  currentValue?: string;
  recommendedValue?: string;
  reason?: string;
  benefit?: string;
}

export interface ValidationResult {
  isValid: boolean;
  errors: ValidationError[];
  warnings: ValidationWarning[];
  recommendations: ValidationRecommendation[];
  platform?: string;
  validationTimeMs: number;
}


export interface Template {
  id: string;
  name: string;
  templateType: string;
  azureService?: string;
  version: string;
  format: string;
  createdAt: string;
  updatedAt: string;
  description?: string;
  content: string;
  createdBy: string;
  isActive: boolean;
  isPublic: boolean;
  deploymentTier?: string;
  filesCount?: number;
  mainFileType?: string;
  // Feature flags
  autoScalingEnabled?: boolean;
  monitoringEnabled?: boolean;
  backupEnabled?: boolean;
  highAvailabilitySupported?: boolean;
  disasterRecoverySupported?: boolean;
  multiRegionSupported?: boolean;
}

export interface TemplateStats {
  totalTemplates: number;
  activeTemplates: number;
  inactiveTemplates: number;
  publicTemplates: number;
  privateTemplates: number;
  byType: Array<{ type: string; count: number }>;
  byCloudProvider: Array<{ provider: string | null; count: number }>;
  byFormat: Array<{ format: string; count: number }>;
  recentlyCreated: Array<{ name: string; createdAt: string; createdBy: string }>;
}

export interface TemplateFile {
  fileName: string;
  content: string;
  fileType: string;
  isEntryPoint: boolean;
  order: number;
  size: number;
}

export interface TemplateFilesResponse {
  templateId: string;
  templateName: string;
  filesCount: number;
  files: TemplateFile[];
}

// Azure Resource Interfaces (for Use Existing Network)
export interface AzureSubscription {
  id: string;
  name: string;
  state: string;
}

export interface AzureResourceGroup {
  id: string;
  name: string;
  location: string;
}

export interface AzureSubnet {
  id: string;
  name: string;
  addressPrefix: string;
  delegation?: string;
  serviceEndpoints?: string[];
}

export interface AzureVNet {
  id: string;
  name: string;
  location: string;
  addressSpace: string[];
  subnets: AzureSubnet[];
}

class AdminApiService {
  private apiClient = axios.create({
    baseURL: API_BASE_URL ? `${API_BASE_URL}/api/admin` : '/api/admin',
    headers: {
      'Content-Type': 'application/json',
    },
  });

  constructor() {
    console.log('AdminApiService initialized with baseURL:', API_BASE_URL ? `${API_BASE_URL}/api/admin` : '/api/admin');
    console.log('NODE_ENV:', process.env.NODE_ENV);
    
    // Add request interceptor to log what's being sent
    this.apiClient.interceptors.request.use((config) => {
      console.log('Axios request config:', {
        url: config.url,
        method: config.method,
        baseURL: config.baseURL,
        headers: config.headers,
        data: config.data
      });
      return config;
    });
  }

  // Template Management
  async createTemplate(request: CreateTemplateRequest): Promise<TemplateResponse> {
    const response = await this.apiClient.post<TemplateResponse>('/templates', request);
    return response.data;
  }

  async updateTemplate(templateId: string, request: UpdateTemplateRequest): Promise<TemplateResponse> {
    const response = await this.apiClient.put<TemplateResponse>(`/templates/${templateId}`, request);
    return response.data;
  }

  async listTemplates(search?: string): Promise<Template[]> {
    const params = search ? { search } : {};
    const response = await this.apiClient.get<{ count: number; templates: Template[] }>('/templates', { params });
    // The API returns { count, templates }, so we need to extract the templates array
    return response.data.templates || [];
  }

  async getTemplate(templateId: string): Promise<Template> {
    const response = await this.apiClient.get<Template>(`/templates/${templateId}`);
    return response.data;
  }

  async deleteTemplate(templateId: string): Promise<void> {
    await this.apiClient.delete(`/templates/${templateId}`);
  }

  async getTemplateFiles(templateId: string): Promise<TemplateFilesResponse> {
    const response = await this.apiClient.get<TemplateFilesResponse>(`/templates/${templateId}/files`);
    return response.data;
  }

  async updateTemplateFile(templateId: string, fileName: string, content: string): Promise<void> {
    // Use the fileName directly - the backend route handles paths with {**fileName}
    await this.apiClient.put(`/templates/${templateId}/files/${fileName}`, { content });
  }

  async validateTemplate(request: CreateTemplateRequest): Promise<ValidationResult> {
    console.log('Validating template with request:', request);
    console.log('Making request to:', '/templates/validate');
    try {
      const response = await this.apiClient.post<ValidationResult>('/templates/validate', request);
      console.log('Validation response:', response.data);
      return response.data;
    } catch (error: any) {
      console.error('Validation error:', error);
      console.error('Error response:', error.response?.data);
      console.error('Error status:', error.response?.status);
      console.error('Error details:', JSON.stringify(error.response?.data?.errors, null, 2));
      throw error;
    }
  }

  async getStats(): Promise<TemplateStats> {
    const response = await this.apiClient.get<TemplateStats>('/templates/stats');
    return response.data;
  }

  async bulkOperation(operation: string, templateIds: string[]): Promise<{ successCount: number; failureCount: number; errors: string[] }> {
    const response = await this.apiClient.post('/templates/bulk', { operation, templateIds });
    return response.data;
  }

  // Infrastructure Management
  async provisionInfrastructure(request: {
    resourceGroupName: string;
    location: string;
    resourceType: string; // 'vnet', 'storage-account', 'key-vault', etc.
    parameters?: Record<string, any>;
    tags?: Record<string, string>;
  }): Promise<{
    success: boolean;
    resourceGroupId?: string;
    deploymentId?: string;
    message?: string;
    errorMessage?: string;
    duration: string;
  }> {
    const response = await this.apiClient.post('/infrastructure/provision', request);
    return response.data;
  }

  async getInfrastructureStatus(resourceGroupName: string): Promise<any> {
    const response = await this.apiClient.get(`/infrastructure/resource-groups/${resourceGroupName}/status`);
    return response.data;
  }

  async listResourceGroupNames(): Promise<string[]> {
    const response = await this.apiClient.get<{ count: number; resourceGroups: string[] }>('/infrastructure/resource-groups');
    return response.data.resourceGroups || [];
  }

  async deleteResourceGroup(resourceGroupName: string): Promise<void> {
    await this.apiClient.delete(`/infrastructure/resource-groups/${resourceGroupName}`);
  }

  async estimateCost(request: {
    resourceType: string;
    location: string;
    parameters?: Record<string, any>;
  }): Promise<{
    estimatedMonthlyCost: number;
    estimatedAnnualCost: number;
    currency: string;
    resourceType: string;
    location: string;
    notes?: string;
    breakdown?: Record<string, number>;
  }> {
    const response = await this.apiClient.post('/infrastructure/cost-estimate', request);
    return response.data;
  }

  // Azure Resource Queries (for Use Existing Network feature)
  // Note: These will need corresponding backend endpoints
  async listSubscriptions(): Promise<AzureSubscription[]> {
    try {
      const response = await this.apiClient.get<AzureSubscription[]>('/azure/subscriptions');
      return response.data;
    } catch (error) {
      console.error('Failed to fetch subscriptions:', error);
      // Return mock data for development
      return [
        { id: '/subscriptions/mock-sub-id', name: 'Production Subscription', state: 'Enabled' }
      ];
    }
  }

  async listResourceGroups(subscriptionId?: string): Promise<AzureResourceGroup[]> {
    try {
      const params = subscriptionId ? { subscriptionId } : {};
      const response = await this.apiClient.get<AzureResourceGroup[]>('/azure/resource-groups', { params });
      return response.data;
    } catch (error) {
      console.error('Failed to fetch resource groups:', error);
      // Return mock data for development
      return [
        { id: '/subscriptions/xxx/resourceGroups/rg-networking', name: 'rg-networking', location: 'eastus' },
        { id: '/subscriptions/xxx/resourceGroups/rg-prod', name: 'rg-prod', location: 'eastus' }
      ];
    }
  }

  async listVNets(subscriptionId?: string, resourceGroupName?: string): Promise<AzureVNet[]> {
    try {
      const params: any = {};
      if (subscriptionId) params.subscriptionId = subscriptionId;
      if (resourceGroupName) params.resourceGroupName = resourceGroupName;
      
      const response = await this.apiClient.get<AzureVNet[]>('/azure/vnets', { params });
      return response.data;
    } catch (error) {
      console.error('Failed to fetch VNets:', error);
      // Return mock data for development
      return [
        {
          id: '/subscriptions/xxx/resourceGroups/rg-networking/providers/Microsoft.Network/virtualNetworks/vnet-prod',
          name: 'vnet-prod',
          location: 'eastus',
          addressSpace: ['10.0.0.0/16'],
          subnets: [
            {
              id: '/subscriptions/xxx/.../subnets/appservice-subnet',
              name: 'appservice-subnet',
              addressPrefix: '10.0.1.0/24',
              delegation: 'Microsoft.Web/serverFarms',
              serviceEndpoints: ['Microsoft.Storage']
            },
            {
              id: '/subscriptions/xxx/.../subnets/privateendpoints-subnet',
              name: 'privateendpoints-subnet',
              addressPrefix: '10.0.2.0/24',
              serviceEndpoints: []
            },
            {
              id: '/subscriptions/xxx/.../subnets/aks-subnet',
              name: 'aks-subnet',
              addressPrefix: '10.0.10.0/23',
              serviceEndpoints: ['Microsoft.Storage', 'Microsoft.KeyVault']
            }
          ]
        },
        {
          id: '/subscriptions/xxx/resourceGroups/rg-networking/providers/Microsoft.Network/virtualNetworks/vnet-dev',
          name: 'vnet-dev',
          location: 'eastus',
          addressSpace: ['10.1.0.0/16'],
          subnets: [
            {
              id: '/subscriptions/xxx/.../subnets/default-subnet',
              name: 'default-subnet',
              addressPrefix: '10.1.0.0/24'
            }
          ]
        }
      ];
    }
  }

  // ==================== ENVIRONMENT MANAGEMENT METHODS ====================

  async createEnvironment(request: CreateEnvironmentRequest): Promise<EnvironmentCreationResult> {
    const response = await this.apiClient.post<EnvironmentCreationResult>('/environments', request);
    return response.data;
  }

  async listEnvironments(resourceGroup?: string): Promise<EnvironmentListResponse> {
    const params = resourceGroup ? { resourceGroup } : {};
    const response = await this.apiClient.get<EnvironmentListResponse>('/environments', { params });
    return response.data || { environments: [], totalCount: 0 };
  }

  async getEnvironment(environmentName: string, resourceGroup: string): Promise<EnvironmentDetailResponse> {
    const response = await this.apiClient.get<EnvironmentDetailResponse>(
      `/environments/${environmentName}`,
      { params: { resourceGroup } }
    );
    return response.data;
  }

  async getEnvironmentStatus(environmentName: string, resourceGroup: string): Promise<EnvironmentStatus> {
    const response = await this.apiClient.get<EnvironmentStatus>(
      `/environments/${environmentName}/status`,
      { params: { resourceGroup } }
    );
    return response.data;
  }

  async getEnvironmentMetrics(
    environmentName: string,
    resourceGroup: string,
    durationHours: number = 24
  ): Promise<EnvironmentMetrics> {
    const response = await this.apiClient.get<EnvironmentMetrics>(
      `/environments/${environmentName}/metrics`,
      { params: { resourceGroup, durationHours } }
    );
    return response.data;
  }

  async deleteEnvironment(environmentName: string, resourceGroup: string): Promise<void> {
    await this.apiClient.delete(`/environments/${environmentName}`, {
      params: { resourceGroup }
    });
  }

  async scaleEnvironment(
    environmentName: string,
    resourceGroup: string,
    request: {
      targetReplicas?: number;
      autoScalingEnabled?: boolean;
      minReplicas?: number;
      maxReplicas?: number;
    }
  ): Promise<any> {
    const response = await this.apiClient.post(
      `/environments/${environmentName}/scale`,
      request,
      { params: { resourceGroup } }
    );
    return response.data;
  }

  async getDeploymentStatus(deploymentId: string): Promise<DeploymentStatusResponse> {
    const response = await this.apiClient.get<DeploymentStatusResponse>(
      `/deployments/${deploymentId}/status`
    );
    return response.data;
  }

  async getDeploymentLogs(deploymentId: string): Promise<DeploymentLogsResponse> {
    const response = await this.apiClient.get<DeploymentLogsResponse>(
      `/deployments/${deploymentId}/logs`
    );
    return response.data;
  }

  // ==================== END ENVIRONMENT METHODS ====================

  // ==================== GOVERNANCE & APPROVAL WORKFLOWS ====================

  /**
   * Get all pending approval workflows
   */
  async getPendingApprovals(): Promise<ApprovalWorkflow[]> {
    const response = await this.apiClient.get<ApprovalWorkflow[]>(
      '/governance/approvals/pending'
    );
    return response.data;
  }

  /**
   * Get a specific approval workflow by ID
   */
  async getApprovalWorkflow(workflowId: string): Promise<ApprovalWorkflow> {
    const response = await this.apiClient.get<ApprovalWorkflow>(
      `/governance/approvals/${workflowId}`
    );
    return response.data;
  }

  /**
   * Approve a pending approval workflow
   */
  async approveWorkflow(workflowId: string, approvedBy: string, comments?: string): Promise<{ success: boolean; message: string }> {
    const response = await this.apiClient.post(
      `/governance/approvals/${workflowId}/approve`,
      { approvedBy, comments }
    );
    return response.data;
  }

  /**
   * Reject a pending approval workflow
   */
  async rejectWorkflow(workflowId: string, rejectedBy: string, reason: string): Promise<{ success: boolean; message: string }> {
    const response = await this.apiClient.post(
      `/governance/approvals/${workflowId}/reject`,
      { rejectedBy, reason }
    );
    return response.data;
  }

  /**
   * Get approval workflow statistics
   */
  async getApprovalStats(): Promise<ApprovalStatsResponse> {
    const response = await this.apiClient.get<ApprovalStatsResponse>(
      '/governance/approvals/stats'
    );
    return response.data;
  }

  /**
   * Validate resource naming conventions
   */
  async validateNaming(resourceType: string, resourceName: string, environment: string = 'dev'): Promise<NamingValidationResponse> {
    const response = await this.apiClient.post<NamingValidationResponse>(
      '/governance/validate/naming',
      { resourceType, resourceName, environment }
    );
    return response.data;
  }

  /**
   * Validate region availability
   */
  async validateRegion(location: string, resourceType: string): Promise<RegionValidationResponse> {
    const response = await this.apiClient.post<RegionValidationResponse>(
      '/governance/validate/region',
      { location, resourceType }
    );
    return response.data;
  }

  // ==================== END GOVERNANCE METHODS ====================

  // ==================== NAVY FLANKSPEED ONBOARDING METHODS ====================

  /**
   * Get pending Navy Flankspeed onboarding requests
   */
  async getPendingOnboardingRequests(): Promise<OnboardingRequest[]> {
    const response = await this.apiClient.get<OnboardingRequest[]>(
      '/onboarding/pending'
    );
    return response.data;
  }

  /**
   * Get specific onboarding request by ID
   */
  async getOnboardingRequest(requestId: string): Promise<OnboardingRequest> {
    const response = await this.apiClient.get<OnboardingRequest>(
      `/onboarding/${requestId}`
    );
    return response.data;
  }

  /**
   * Approve a Navy Flankspeed onboarding request
   */
  async approveOnboardingRequest(
    requestId: string, 
    approvedBy: string, 
    comments?: string
  ): Promise<OnboardingApprovalResponse> {
    const response = await this.apiClient.post<OnboardingApprovalResponse>(
      `/onboarding/${requestId}/approve`,
      { approvedBy, comments }
    );
    return response.data;
  }

  /**
   * Reject a Navy Flankspeed onboarding request
   */
  async rejectOnboardingRequest(
    requestId: string, 
    rejectedBy: string, 
    reason: string
  ): Promise<OnboardingApprovalResponse> {
    const response = await this.apiClient.post<OnboardingApprovalResponse>(
      `/onboarding/${requestId}/reject`,
      { rejectedBy, reason }
    );
    return response.data;
  }

  /**
   * Get onboarding requests by mission owner email
   */
  async getOnboardingRequestsByOwner(email: string): Promise<OnboardingRequest[]> {
    const response = await this.apiClient.get<OnboardingRequest[]>(
      `/onboarding/owner/${email}`
    );
    return response.data;
  }

  /**
   * Get onboarding statistics
   */
  async getOnboardingStats(): Promise<any> {
    const response = await this.apiClient.get<any>(
      '/onboarding/stats'
    );
    return response.data;
  }

  // ==================== END NAVY FLANKSPEED ONBOARDING METHODS ====================

  // ==================== CHAT METHODS ====================

  /**
   * Send a chat message to the AI assistant
   * This calls the Platform API (port 7001) chat endpoint
   */
  async sendChatMessage(message: string, context?: string[]): Promise<ChatResponse> {
    // Platform API is on a different port, so we need to use full URL
    const platformApiUrl = process.env.REACT_APP_PLATFORM_API_URL || 'http://localhost:7001';
    
    const response = await axios.post<ChatResponse>(
      `${platformApiUrl}/api/chat/query`,
      {
        Query: message,
        Context: context
      } as ChatMessage
    );
    
    return response.data;
  }

  // ==================== END CHAT METHODS ====================

  // ==================== DEPLOYMENT PROGRESS METHODS ====================

  /**
   * Get all active deployments
   */
  async getActiveDeployments(): Promise<DeploymentStatusResponse[]> {
    const response = await this.apiClient.get<DeploymentStatusResponse[]>(
      '/deployments/active'
    );
    return response.data;
  }

  /**
   * Cancel a running deployment
   */
  async cancelDeployment(deploymentId: string): Promise<void> {
    await this.apiClient.post(`/deployments/${deploymentId}/cancel`);
  }

  // ==================== END DEPLOYMENT PROGRESS METHODS ====================

  // ==================== COST MANAGEMENT METHODS ====================

  /**
   * Get cost data for a specific environment
   */
  async getEnvironmentCost(envId: string, days: number = 30): Promise<EnvironmentCostData> {
    const response = await this.apiClient.get<EnvironmentCostData>(
      `/costs/environment/${envId}`,
      { params: { days } }
    );
    return response.data;
  }

  /**
   * Get cost summary across all environments
   */
  async getCostSummary(days: number = 30): Promise<CostSummary> {
    const response = await this.apiClient.get<CostSummary>(
      '/costs/summary',
      { params: { days } }
    );
    return response.data;
  }

  /**
   * Get cost forecast for the next N days
   */
  async getCostForecast(days: number = 30): Promise<CostForecast> {
    const response = await this.apiClient.get<CostForecast>(
      '/costs/forecast',
      { params: { days } }
    );
    return response.data;
  }

  /**
   * Get cost optimization recommendations
   */
  async getOptimizationRecommendations(): Promise<CostOptimizationRecommendation[]> {
    const response = await this.apiClient.get<CostOptimizationRecommendation[]>(
      '/costs/recommendations'
    );
    return response.data;
  }

  // ==================== END COST MANAGEMENT METHODS ====================
}

// ==================== ENVIRONMENT MANAGEMENT TYPES ====================

export interface CreateEnvironmentRequest {
  environmentName: string;
  environmentType: string;
  resourceGroup: string;
  location?: string;
  subscriptionId?: string;
  tags?: Record<string, string>;
  computeConfiguration?: object;
  networkConfiguration?: object;
  securityConfiguration?: object;
  monitoringConfiguration?: object;
  scalingConfiguration?: object;
  templateId?: string;
  parameters?: Record<string, string>;
  enableMonitoring?: boolean;
  enableLogging?: boolean;
}

export interface EnvironmentResponse {
  id: string;
  name: string;
  templateId?: string;
  resourceGroup: string;
  status: string;
  environmentType?: string;
  location?: string;
  createdAt: string;
  tags?: Record<string, string>;
}

export interface EnvironmentDetailResponse extends EnvironmentResponse {
  history?: DeploymentHistoryItem[];
  metrics?: EnvironmentMetricsSummary;
}

export interface DeploymentHistoryItem {
  action: string;
  status: string;
  startedAt: string;
  completedAt?: string;
  duration?: string;
  triggeredBy?: string;
  errorMessage?: string;
}

export interface EnvironmentMetricsSummary {
  metricsByType: Record<string, MetricStatistics>;
}

export interface MetricStatistics {
  count: number;
  avgValue: number;
  minValue: number;
  maxValue: number;
  lastTimestamp: string;
}

export interface EnvironmentListResponse {
  environments: EnvironmentResponse[];
  totalCount: number;
  filteredBy?: Record<string, string>;
}

export interface DeploymentStatusResponse {
  deploymentId: string;
  deploymentName?: string;
  state: string; // 'Pending' | 'Running' | 'Succeeded' | 'Failed' | 'Cancelled'
  progressPercentage?: number;
  percentComplete?: number; // Alias for progressPercentage
  currentOperation?: string;
  steps?: DeploymentStep[];
  resources?: ResourceStatus[];
  resourcesCreated?: string[];
  startTime?: string;
  endTime?: string;
  estimatedCompletion?: string;
  errorMessage?: string;
}

export interface ResourceStatus {
  resourceType: string;
  resourceName: string;
  status: string;
  errorMessage?: string;
}

export interface DeploymentLogsResponse {
  entries: LogEntry[];
}

export interface LogEntry {
  timestamp: string;
  level: string;
  message: string;
  source?: string;
}

export interface EnvironmentCreationResult {
  success: boolean;
  environmentId: string;
  environmentName: string;
  resourceGroup: string;
  deploymentId?: string;
  createdResources?: string[];
  errorMessage?: string;
}

export interface EnvironmentStatus {
  environmentName: string;
  status: string;
  health: string;
  resourceCount: number;
  resources?: any[];
}

export interface EnvironmentMetrics {
  environmentName: string;
  timeRange: string;
  metrics: Array<{
    name: string;
    values: Array<{
      timestamp: string;
      value: number;
    }>;
  }>;
}

// ==================== END ENVIRONMENT TYPES ====================

// ==================== GOVERNANCE & APPROVAL WORKFLOWS ====================

export interface ApprovalWorkflow {
  id: string;
  resourceType: string;
  resourceName: string;
  resourceGroupName: string;
  location: string;
  environment: string;
  status: string;
  requestedBy: string;
  requestedAt: string;
  expiresAt: string;
  reason: string;
  policyViolations: string[];
  requiredApprovers: string[];
  approvedBy?: string;
  approvedAt?: string;
  rejectedBy?: string;
  rejectedAt?: string;
  rejectionReason?: string;
  approvalComments?: string;
}

export interface ApprovalStatsResponse {
  totalPending: number;
  expiringSoon: number;
  byEnvironment: Record<string, number>;
  byResourceType: Record<string, number>;
}

// ==================== NAVY FLANKSPEED ONBOARDING TYPES ====================
export interface OnboardingRequest {
  id: string;
  // Mission Details
  missionName: string;
  missionOwner: string;
  missionOwnerEmail: string;
  missionOwnerRank: string;
  command: string;
  classificationLevel: string;
  // Technical Requirements
  requestedSubscriptionName: string;
  requestedVNetCidr: string;
  requiredServices: string[];
  region: string;
  estimatedUserCount: number;
  dataResidency: string;
  estimatedDataVolumeTB: number;
  // Compliance & Security
  requiresPki: boolean;
  requiresCac: boolean;
  requiresAto: boolean;
  securityContactEmail: string;
  complianceFrameworks: string[];
  // Business Justification
  businessJustification: string;
  useCase: string;
  requestedStartDate: string;
  fundingSource: string;
  estimatedMonthlyCost: number;
  missionDurationMonths: number;
  // Workflow State
  status: string;
  createdAt: string;
  reviewedAt?: string;
  provisionedAt?: string;
  completedAt?: string;
  lastUpdatedAt: string;
  // Approval Workflow
  approvedBy?: string;
  approvalComments?: string;
  rejectedBy?: string;
  rejectionReason?: string;
  priority: number;
  // Provisioned Resources
  provisionedSubscriptionId?: string;
  provisionedVNetId?: string;
  provisionedResourceGroupId?: string;
  provisionedResources?: Record<string, string>;
  provisioningJobId?: string;
  provisioningError?: string;
  // Notifications
  notificationSent: boolean;
  notificationSentAt?: string;
  notificationHistory?: string[];
}

export interface OnboardingApprovalResponse {
  success: boolean;
  message: string;
  provisioningJobId?: string;
}
// ==================== END NAVY FLANKSPEED ONBOARDING TYPES ====================

export interface NamingValidationResponse {
  isValid: boolean;
  errors: string[];
  warnings: string[];
  suggestedName?: string;
}

export interface RegionValidationResponse {
  isAvailable: boolean;
  isApproved: boolean;
  unavailableServices: string[];
  reasonUnavailable?: string;
  alternativeRegions: string[];
}

// ==================== END GOVERNANCE TYPES ====================

// ==================== CHAT API TYPES ====================
export interface ChatMessage {
  Query: string;
  Context?: string[];
}

export interface ChatResponse {
  Success: boolean;
  Response?: string;
  Error?: string;
  SuggestedActions?: string[];
  RecommendedTools?: any[];
}
// ==================== END CHAT API TYPES ====================

// ==================== DEPLOYMENT PROGRESS TYPES ====================
export interface DeploymentStep {
  name: string;
  status: 'Pending' | 'InProgress' | 'Completed' | 'Failed';
  description?: string;
  duration?: string;
  startTime?: string;
  endTime?: string;
}
// ==================== END DEPLOYMENT PROGRESS TYPES ====================

// ==================== COST MANAGEMENT TYPES ====================
export interface EnvironmentCostData {
  environmentId: string;
  environmentName: string;
  totalCost: number;
  currency: string;
  period: CostPeriod;
  dailyCosts: DailyCost[];
  serviceCosts: ServiceCost[];
  recommendations: string[];
}

export interface CostSummary {
  totalCost: number;
  currency: string;
  period: CostPeriod;
  trendPercentage: number;
  dailyCosts: DailyCost[];
  topServices: ServiceCost[];
  potentialSavings: number;
  recommendationCount: number;
  anomalyCount: number;
  budgetStatus?: BudgetStatusSummary;
}

export interface CostPeriod {
  startDate: string;
  endDate: string;
  days: number;
}

export interface DailyCost {
  date: string;
  cost: number;
  currency: string;
}

export interface ServiceCost {
  serviceName: string;
  cost: number;
  currency: string;
  resourceCount: number;
}

export interface BudgetStatusSummary {
  budgetName: string;
  budgetAmount: number;
  currentSpend: number;
  percentageUsed: number;
  status: string;
}

export interface CostForecast {
  forecastedCost: number;
  currency: string;
  startDate: string;
  endDate: string;
  forecastDays: number;
}

export interface CostOptimizationRecommendation {
  recommendationId: string;
  title: string;
  description: string;
  potentialMonthlySavings: number;
  potentialAnnualSavings: number;
  priority: string;
  category: string;
  affectedResources: string[];
}
// ==================== END COST MANAGEMENT TYPES ====================

export default new AdminApiService();
