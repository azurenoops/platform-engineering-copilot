// Type definitions for Platform Engineering extension

// Platform Engineering Tool Requests
export interface InfrastructureRequest {
    resourceType: 'bicep' | 'terraform';
    cloudProvider: 'azure' | 'aws' | 'gcp';
    template: Record<string, any>;
    subscriptionId?: string;
    resourceGroupName?: string;
}

export interface ContainerDeploymentRequest {
    appName: string;
    namespace: string;
    containerImage: string;
    replicas: number;
    includeService?: boolean;
    includeIngress?: boolean;
}

export interface MonitoringDashboardRequest {
    dashboardType: 'azure-monitor' | 'grafana';
    resourceGroup: string;
    subscriptionId: string;
    includeAlerts?: boolean;
}

export interface SecurityScanRequest {
    scanType: 'container' | 'code' | 'infrastructure';
    target: string;
    severity: 'Critical' | 'High' | 'Medium' | 'Low';
}

export interface AtoComplianceScanRequest {
    resourceGroupName: string;
    scanType?: 'comprehensive' | 'quick' | 'targeted';
    resourceTypes?: string[];
    severityFilter?: 'Critical' | 'High' | 'Medium' | 'Low' | 'All';
}

export interface AtoComplianceFinding {
    findingId: string;
    title: string;
    severity: 'Critical' | 'High' | 'Medium' | 'Low' | 'Info';
    resourceName: string;
    resourceType: string;
    resourceId: string;
    description: string;
    remediation: string;
    nistControl: string;
    complianceStatus: 'Compliant' | 'Non-Compliant' | 'Not-Applicable';
    lastChecked: string;
}

export interface AtoComplianceScanResult {
    scanId: string;
    resourceGroup: string;
    timestamp: string;
    scanType: string;
    resourcesScanned: number;
    totalFindings: number;
    findingsBySeverity: {
        Critical: number;
        High: number;
        Medium: number;
        Low: number;
        Info: number;
    };
    complianceScore: number;
    findings: AtoComplianceFinding[];
    recommendations: string[];
}

export type ResourceType = 
    | 'kubernetes_cluster'
    | 'storage_account'
    | 'key_vault'
    | 'app_service_plan'
    | 'sql_database'
    | 'container_registry'
    | 'virtual_machine'
    | 'resource_group'
    | 'network_infrastructure';

export interface MCPToolRequest {
    name: string;
    arguments?: Record<string, any>;
    [k: string]: unknown;
}

// Resource Tagging Management
export interface ResourceTagRequest {
    operation: 'list' | 'add' | 'update' | 'remove';
    resourceGroupName?: string;
    resourceId?: string;
    tags?: Record<string, string>;
    tagKeys?: string[];
    enforcePolicy?: boolean;
    bulkOperation?: boolean;
}

export interface TagPolicyRule {
    tagKey: string;
    required: boolean;
    allowedValues?: string[];
    pattern?: string;
    description: string;
    enforceOnResourceTypes?: string[];
}

export interface TagOperationResult {
    resourceId: string;
    resourceName: string;
    resourceType: string;
    operation: string;
    status: 'success' | 'failed' | 'no-changes';
    tagsAdded?: string[];
    tagsUpdated?: string[];
    tagsRemoved?: string[];
    finalTags?: Record<string, string>;
    error?: string;
    policyViolations?: TagPolicyViolation[];
}

export interface TagPolicyViolation {
    tagKey: string;
    rule: TagPolicyRule;
    violation: 'missing-required' | 'invalid-value' | 'pattern-mismatch';
    currentValue?: string;
    suggestedValue?: string;
}

export interface BulkTagOperationSummary {
    totalResources: number;
    successfulOperations: number;
    failedOperations: number;
    operationTime: string;
    tagsProcessed: number;
    policyViolations: number;
}

export interface MCPToolResponse {
    content: Array<{
        type: 'text';
        text: string;
    }>;
    success: boolean;
    isError?: boolean;
}

// Platform-specific interfaces
export interface PlatformConfig {
    mcpServerHost: string;
    mcpServerPort: number;
    azureSubscriptionId?: string;
    azureTenantId?: string;
    azureCloudEnvironment: 'commercial' | 'government';
    kubernetesContext?: string;
    defaultCloudProvider: 'azure' | 'aws' | 'gcp';
}

export interface InfrastructureTemplate {
    templateType: 'bicep' | 'terraform';
    resourceType: string;
    parameters: Record<string, any>;
    outputs?: Record<string, any>;
}

export interface ContainerConfiguration {
    image: string;
    name: string;
    namespace: string;
    replicas: number;
    port?: number;
    environmentVariables?: Record<string, string>;
    resourceLimits?: {
        cpu?: string;
        memory?: string;
    };
}

export interface MonitoringConfiguration {
    type: 'azure-monitor' | 'grafana' | 'prometheus';
    dashboardName: string;
    resourceGroup?: string;
    metrics: string[];
    alertRules?: AlertRule[];
}

export interface AlertRule {
    name: string;
    metric: string;
    threshold: number;
    operator: 'GreaterThan' | 'LessThan' | 'Equal';
    severity: 'Critical' | 'Warning' | 'Informational';
}

export interface SecurityScanConfiguration {
    scanType: 'container' | 'infrastructure' | 'code';
    target: string;
    scanners: string[];
    severityThreshold: 'Critical' | 'High' | 'Medium' | 'Low';
    outputFormat: 'json' | 'sarif' | 'table';
}

// Governance-related types
export interface GovernanceCheckResult {
    isAllowed: boolean;
    requiresApproval: boolean;
    violations: string[];
    warnings?: string[];
    approvalRequest?: ApprovalRequest;
    policyEvaluations?: PolicyEvaluation[];
}

export interface ApprovalRequest {
    id: string;
    toolName: string;
    arguments: Record<string, any>;
    reason: string;
    requestedAt: Date;
    expiresAt: Date;
    requestedBy?: string;
    status: 'pending' | 'approved' | 'denied' | 'expired';
    approvedBy?: string;
    approvedAt?: Date;
    comments?: string;
}

export interface PolicyEvaluation {
    policyName: string;
    result: 'allow' | 'deny' | 'warn';
    reason: string;
    recommendation?: string;
}

export interface GovernanceOptions {
    enforcePolicies: boolean;
    requireApprovals: boolean;
    approvalTimeoutMinutes: number;
    enableAuditLogging: boolean;
}

// Enhanced Infrastructure Provisioning
export interface InfrastructureDiscoveryRequest {
    subscriptionId?: string;
    resourceGroupName?: string;
    includeResources?: boolean;
    includeLocations?: boolean;
    includeQuotas?: boolean;
    resourceTypeFilter?: string[];
}

export interface AzureLocationInfo {
    name: string;
    displayName: string;
    latitude?: number;
    longitude?: number;
    availabilityZoneMappings?: any[];
    metadata?: {
        geographyGroup?: string;
        latitude?: string;
        longitude?: string;
        physicalLocation?: string;
        regionCategory?: string;
        regionType?: string;
    };
}

export interface AzureResourceGroupInfo {
    id: string;
    name: string;
    location: string;
    provisioningState: string;
    tags?: Record<string, string>;
    managedBy?: string;
    resourceCount?: number;
    lastModified?: Date;
}

export interface AzureResourceInfo {
    id: string;
    name: string;
    type: string;
    location: string;
    resourceGroup: string;
    sku?: {
        name?: string;
        tier?: string;
        family?: string;
        capacity?: number;
    };
    tags?: Record<string, string>;
    provisioningState?: string;
    createdTime?: Date;
    changedTime?: Date;
}

export interface EnhancedInfrastructureRequest {
    resourceType: ResourceType;
    resourceName: string;
    resourceGroupName: string;
    location: string;
    subscriptionId?: string;
    azureEnvironment?: 'AzureCloud' | 'AzureUSGovernment' | 'AzureGermanCloud' | 'AzureChinaCloud';
    tags?: Record<string, string>;
    preflightValidation?: boolean;
    autoSelectLocation?: boolean;
    optimizeForCost?: boolean;
    optimizeForPerformance?: boolean;
    environmentProfile?: 'development' | 'staging' | 'production';
    scalingProfile?: 'minimal' | 'standard' | 'premium';
    securityProfile?: 'basic' | 'enhanced' | 'maximum';
    sku?: string;
    enableSecurity?: boolean;
}

export interface InfrastructurePreflightResult {
    validationPassed: boolean;
    recommendedLocation?: string;
    alternativeLocations?: string[];
    quotaWarnings?: string[];
    costEstimate?: {
        monthly: number;
        currency: string;
        breakdown: Array<{
            component: string;
            cost: number;
            description: string;
        }>;
    };
    securityRecommendations?: string[];
    governanceRequirements?: string[];
    conflicts?: Array<{
        resource: string;
        issue: string;
        recommendation: string;
    }>;
}

export interface InfrastructureProvisioningResult {
    success: boolean;
    resourceId?: string;
    resourceName: string;
    resourceType: string;
    location: string;
    deploymentTime?: number;
    cost?: {
        setup: number;
        estimatedMonthly: number;
        currency: string;
    };
    endpoints?: Array<{
        name: string;
        url: string;
        type: 'management' | 'api' | 'ui' | 'ssh' | 'rdp';
    }>;
    nextSteps?: string[];
    error?: string;
    warnings?: string[];
    networkConfiguration?: NetworkConfiguration;
}

// Network Infrastructure Types
export interface NetworkConfiguration {
    virtualNetwork?: VirtualNetworkConfig;
    subnets?: SubnetConfig[];
    networkSecurityGroups?: NetworkSecurityGroupConfig[];
    publicIpAddresses?: PublicIpConfig[];
    loadBalancers?: LoadBalancerConfig[];
    applicationGateways?: ApplicationGatewayConfig[];
}

export interface VirtualNetworkConfig {
    name: string;
    resourceId?: string;
    addressSpace: string[];
    location: string;
    dnsServers?: string[];
    tags?: Record<string, string>;
    enableDdosProtection?: boolean;
    enableVmProtection?: boolean;
}

export interface SubnetConfig {
    name: string;
    resourceId?: string;
    addressPrefix: string;
    virtualNetworkName: string;
    networkSecurityGroupName?: string;
    routeTableName?: string;
    serviceEndpoints?: string[];
    delegations?: SubnetDelegation[];
    privateEndpointNetworkPolicies?: 'Enabled' | 'Disabled';
    privateLinkServiceNetworkPolicies?: 'Enabled' | 'Disabled';
}

export interface SubnetDelegation {
    name: string;
    serviceName: string;
    actions?: string[];
}

export interface NetworkSecurityGroupConfig {
    name: string;
    resourceId?: string;
    location: string;
    securityRules: SecurityRuleConfig[];
    tags?: Record<string, string>;
}

export interface SecurityRuleConfig {
    name: string;
    priority: number;
    direction: 'Inbound' | 'Outbound';
    access: 'Allow' | 'Deny';
    protocol: 'Tcp' | 'Udp' | 'Icmp' | '*';
    sourcePortRange?: string;
    destinationPortRange?: string;
    sourceAddressPrefix?: string;
    destinationAddressPrefix?: string;
    sourcePortRanges?: string[];
    destinationPortRanges?: string[];
    sourceAddressPrefixes?: string[];
    destinationAddressPrefixes?: string[];
    description?: string;
}

export interface PublicIpConfig {
    name: string;
    resourceId?: string;
    location: string;
    allocationMethod: 'Static' | 'Dynamic';
    sku: 'Basic' | 'Standard';
    ipVersion: 'IPv4' | 'IPv6';
    domainNameLabel?: string;
    idleTimeoutInMinutes?: number;
    tags?: Record<string, string>;
}

export interface LoadBalancerConfig {
    name: string;
    resourceId?: string;
    location: string;
    sku: 'Basic' | 'Standard' | 'Gateway';
    type: 'Public' | 'Internal';
    frontendIpConfigurations: FrontendIpConfig[];
    backendAddressPools: BackendAddressPoolConfig[];
    loadBalancingRules: LoadBalancingRuleConfig[];
    probes: ProbeConfig[];
    tags?: Record<string, string>;
}

export interface FrontendIpConfig {
    name: string;
    publicIpAddressName?: string;
    subnetName?: string;
    privateIpAddress?: string;
    privateIpAddressAllocation?: 'Static' | 'Dynamic';
}

export interface BackendAddressPoolConfig {
    name: string;
    loadBalancerBackendAddresses?: BackendAddressConfig[];
}

export interface BackendAddressConfig {
    name: string;
    ipAddress: string;
    virtualNetworkName?: string;
}

export interface LoadBalancingRuleConfig {
    name: string;
    frontendIpConfigurationName: string;
    backendAddressPoolName: string;
    probeName: string;
    protocol: 'Tcp' | 'Udp' | 'All';
    frontendPort: number;
    backendPort: number;
    idleTimeoutInMinutes?: number;
    enableFloatingIp?: boolean;
    enableTcpReset?: boolean;
}

export interface ProbeConfig {
    name: string;
    protocol: 'Http' | 'Https' | 'Tcp';
    port: number;
    requestPath?: string;
    intervalInSeconds?: number;
    numberOfProbes?: number;
    probeThreshold?: number;
}

export interface ApplicationGatewayConfig {
    name: string;
    resourceId?: string;
    location: string;
    sku: ApplicationGatewaySku;
    gatewayIpConfigurations: GatewayIpConfigurationConfig[];
    frontendIpConfigurations: ApplicationGatewayFrontendIpConfig[];
    frontendPorts: FrontendPortConfig[];
    backendAddressPools: ApplicationGatewayBackendAddressPoolConfig[];
    backendHttpSettingsCollection: BackendHttpSettingsConfig[];
    httpListeners: HttpListenerConfig[];
    requestRoutingRules: RequestRoutingRuleConfig[];
    tags?: Record<string, string>;
}

export interface ApplicationGatewaySku {
    name: 'Standard_Small' | 'Standard_Medium' | 'Standard_Large' | 'Standard_v2' | 'WAF_Medium' | 'WAF_Large' | 'WAF_v2';
    tier: 'Standard' | 'Standard_v2' | 'WAF' | 'WAF_v2';
    capacity?: number;
}

export interface GatewayIpConfigurationConfig {
    name: string;
    subnetName: string;
}

export interface ApplicationGatewayFrontendIpConfig {
    name: string;
    publicIpAddressName?: string;
    subnetName?: string;
    privateIpAddress?: string;
    privateIpAddressAllocation?: 'Static' | 'Dynamic';
}

export interface FrontendPortConfig {
    name: string;
    port: number;
}

export interface ApplicationGatewayBackendAddressPoolConfig {
    name: string;
    backendAddresses?: ApplicationGatewayBackendAddressConfig[];
}

export interface ApplicationGatewayBackendAddressConfig {
    ipAddress?: string;
    fqdn?: string;
}

export interface BackendHttpSettingsConfig {
    name: string;
    port: number;
    protocol: 'Http' | 'Https';
    cookieBasedAffinity: 'Enabled' | 'Disabled';
    requestTimeout?: number;
    probeName?: string;
    hostName?: string;
    pickHostNameFromBackendAddress?: boolean;
}

export interface HttpListenerConfig {
    name: string;
    frontendIpConfigurationName: string;
    frontendPortName: string;
    protocol: 'Http' | 'Https';
    hostName?: string;
    requireServerNameIndication?: boolean;
    sslCertificateName?: string;
}

export interface RequestRoutingRuleConfig {
    name: string;
    ruleType: 'Basic' | 'PathBasedRouting';
    httpListenerName: string;
    backendAddressPoolName?: string;
    backendHttpSettingsName?: string;
    urlPathMapName?: string;
    redirectConfigurationName?: string;
    rewriteRuleSetName?: string;
    priority?: number;
}

// Network Provisioning Request
export interface NetworkProvisioningRequest extends EnhancedInfrastructureRequest {
    networkProfile?: 'basic' | 'standard' | 'advanced' | 'enterprise';
    networkTopology?: 'single-subnet' | 'multi-subnet' | 'hub-spoke' | 'mesh';
    enableNetworkWatcher?: boolean;
    enableFlowLogs?: boolean;
    enableDdosProtection?: boolean;
    connectivityRequirements?: ConnectivityRequirement[];
}

// Multi-Cloud Resource Discovery Types
export interface AzureSubscription {
    subscriptionId: string;
    displayName: string;
    state: 'Enabled' | 'Disabled' | 'Warned' | 'PastDue' | 'Deleted';
    tenantId: string;
    environmentName?: 'AzureCloud' | 'AzureUSGovernment' | 'AzureGermanCloud' | 'AzureChinaCloud';
    quotaId?: string;
    spendingLimit?: 'On' | 'Off' | 'CurrentPeriodOff';
    managementGroupIds?: string[];
    tags?: Record<string, string>;
}

export interface AzureLocation {
    name: string;
    displayName: string;
    regionalDisplayName?: string;
    metadata?: {
        regionType?: string;
        regionCategory?: string;
        geography?: string;
        geographyGroup?: string;
        longitude?: string;
        latitude?: string;
        physicalLocation?: string;
        pairedRegion?: {
            name: string;
            id: string;
        }[];
    };
    availabilityZoneMappings?: {
        logicalZone: string;
        physicalZone: string;
    }[];
    zones?: string[];
}

export interface ResourceDiscoveryFilter {
    subscriptionIds?: string[];
    resourceGroupNames?: string[];
    resourceTypes?: string[];
    locations?: string[];
    tags?: Record<string, string>;
    states?: string[];
    createdAfter?: Date;
    createdBefore?: Date;
    modifiedAfter?: Date;
    modifiedBefore?: Date;
    includeDeleted?: boolean;
    includeCosts?: boolean;
    includeMetrics?: boolean;
    includeHealth?: boolean;
}

export interface DiscoveredResource {
    id: string;
    name: string;
    type: string;
    resourceGroup: string;
    subscription: string;
    location: string;
    tags?: Record<string, string>;
    properties?: Record<string, any>;
    sku?: {
        name: string;
        tier?: string;
        size?: string;
        family?: string;
        capacity?: number;
    };
    identity?: {
        type: string;
        principalId?: string;
        tenantId?: string;
    };
    plan?: {
        name: string;
        product: string;
        publisher: string;
        version?: string;
    };
    kind?: string;
    managedBy?: string;
    createdTime?: Date;
    changedTime?: Date;
    provisioningState?: string;
    // Cost and utilization data (if requested)
    costAnalysis?: {
        dailyCost: number;
        monthlyCost: number;
        currency: string;
        costTrend: 'increasing' | 'decreasing' | 'stable';
        utilizationScore?: number;
    };
    // Health and performance data (if requested)
    health?: {
        status: 'Healthy' | 'Warning' | 'Critical' | 'Unknown';
        issues?: string[];
        recommendations?: string[];
        lastChecked?: Date;
    };
    // Dependencies and relationships
    dependencies?: {
        dependsOn: string[];
        dependents: string[];
    };
}

export interface ResourceDiscoveryRequest {
    filter?: ResourceDiscoveryFilter;
    includeAnalytics?: boolean;
    groupBy?: 'subscription' | 'resourceGroup' | 'location' | 'resourceType' | 'tags';
    sortBy?: 'name' | 'type' | 'location' | 'cost' | 'createdTime' | 'changedTime';
    sortOrder?: 'asc' | 'desc';
    maxResults?: number;
    continueToken?: string;
}

export interface ResourceDiscoveryResult {
    resources: DiscoveredResource[];
    summary: {
        totalResources: number;
        resourcesByType: Record<string, number>;
        resourcesByLocation: Record<string, number>;
        resourcesBySubscription: Record<string, number>;
        totalCostDaily?: number;
        totalCostMonthly?: number;
        currency?: string;
        healthSummary?: {
            healthy: number;
            warning: number;
            critical: number;
            unknown: number;
        };
    };
    continueToken?: string;
    recommendations?: {
        costOptimization: string[];
        securityImprovements: string[];
        performanceOptimization: string[];
        governance: string[];
    };
}

export interface SubscriptionDiscoveryRequest {
    includeDisabled?: boolean;
    includeQuotaInfo?: boolean;
    includeSpendingInfo?: boolean;
    includeCostAnalysis?: boolean;
    includeResourceCounts?: boolean;
    filterByManagementGroup?: string;
    filterByEnvironment?: 'AzureCloud' | 'AzureUSGovernment' | 'AzureGermanCloud' | 'AzureChinaCloud';
}

export interface SubscriptionDiscoveryResult {
    subscriptions: (AzureSubscription & {
        resourceCount?: number;
        resourceGroupCount?: number;
        dailyCost?: number;
        monthlyCost?: number;
        currency?: string;
        quotaInfo?: {
            cores: { used: number; limit: number };
            storage: { used: number; limit: number };
            networks: { used: number; limit: number };
        };
    })[];
    summary: {
        totalSubscriptions: number;
        enabledSubscriptions: number;
        totalResources?: number;
        totalCostDaily?: number;
        totalCostMonthly?: number;
        currency?: string;
    };
    recommendations?: {
        unusedSubscriptions: string[];
        highCostSubscriptions: string[];
        complianceIssues: string[];
    };
}

export interface LocationDiscoveryRequest {
    subscriptionId?: string;
    includeCapacityInfo?: boolean;
    includeServiceAvailability?: boolean;
    includePricingInfo?: boolean;
    filterByResourceType?: string;
    filterByGeography?: string;
    excludePairedRegions?: boolean;
}

export interface LocationDiscoveryResult {
    locations: (AzureLocation & {
        serviceAvailability?: {
            available: string[];
            preview: string[];
            unavailable: string[];
        };
        capacityInfo?: {
            vmSizes: string[];
            availabilityZones: string[];
            maxVMs?: number;
        };
        pricingTier?: 'low' | 'medium' | 'high';
        latencyScore?: number;
        recommendationScore?: number;
    })[];
    recommendations?: {
        costOptimal: string[];
        performanceOptimal: string[];
        complianceRecommended: string[];
        disasterRecoveryPairs: Array<{ primary: string; secondary: string }>;
    };
}

// Enhanced Interactivity Types
export interface ChatMessage {
    role: 'user' | 'assistant' | 'system';
    content: string;
    timestamp: Date;
    metadata?: {
        action?: string;
        confidence?: number;
        resources?: string[];
        cost?: number;
    };
}

export interface UserPreferences {
    defaultSubscription?: string;
    defaultResourceGroup?: string;
    defaultLocation?: string;
    preferredEnvironment?: 'development' | 'staging' | 'production';
    costBudget?: number;
    securityProfile?: 'basic' | 'enhanced' | 'maximum';
    notificationChannels?: string[];
    autoApprove?: boolean;
    favoriteCommands?: string[];
}

export interface UserContext {
    sessionId: string;
    preferences: UserPreferences;
    recentResources: string[];
    activeSubscription?: string;
    currentWorkspace?: string;
    conversationHistory: ChatMessage[];
}

export interface IntentAnalysis {
    action: string;
    confidence: number;
    parameters: Record<string, any>;
    requiresApproval: boolean;
    estimatedCost?: number;
    riskLevel: 'low' | 'medium' | 'high';
    suggestions: string[];
    followUpQuestions: string[];
}

export interface InteractiveStep {
    id: string;
    type: 'input' | 'selection' | 'confirmation' | 'approval';
    title: string;
    description: string;
    options?: string[];
    defaultValue?: string;
    required: boolean;
    validation?: (value: string) => boolean;
}

export interface InteractiveWorkflow {
    id: string;
    title: string;
    description: string;
    steps: InteractiveStep[];
    currentStep: number;
    completed: boolean;
    result?: any;
}

export interface SmartSuggestion {
    id: string;
    title: string;
    description: string;
    command: string;
    confidence: number;
    reasoning: string;
    estimatedTime?: string;
    estimatedCost?: number;
    riskLevel: 'low' | 'medium' | 'high';
}

export interface ConnectivityRequirement {
    type: 'internet' | 'vpn' | 'expressroute' | 'peering' | 'private-endpoint';
    target?: string;
    bandwidth?: string;
    encryption?: boolean;
    redundancy?: boolean;
}