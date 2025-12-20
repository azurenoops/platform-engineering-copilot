using System.Collections.Generic;

namespace Platform.Engineering.Copilot.Core.Models
{
    /// <summary>
    /// Template generation request with full specification
    /// </summary>
    public class TemplateGenerationRequest
    {
        // Core Identifiers
        public string ServiceName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? TemplateType { get; set; } // microservice, web-app, api, infrastructure, data-pipeline, ml-platform, serverless
        
        // Application Layer
        public ApplicationSpec? Application { get; set; }
        
        // Data Layer
        public List<DatabaseSpec> Databases { get; set; } = new();
        
        // Infrastructure Layer
        public InfrastructureSpec Infrastructure { get; set; } = new();
        
        // Deployment Configuration
        public DeploymentSpec Deployment { get; set; } = new();
        
        // Security & Compliance
        public SecuritySpec Security { get; set; } = new();
        
        // Observability
        public ObservabilitySpec Observability { get; set; } = new();
        
        // DoD-Specific Compliance (IL2/IL4/IL5/IL6)
        /// <summary>
        /// DoD-specific compliance metadata for Navy/DoD IL2-IL6 environments
        /// Includes mission sponsor, DoDAAC, Impact Level, and derived security requirements
        /// </summary>
        public DoDComplianceSpec? DoDCompliance { get; set; }
    }

    /// <summary>
    /// Application code specification
    /// </summary>
    public class ApplicationSpec
    {
        public ProgrammingLanguage Language { get; set; }
        public string Framework { get; set; } = string.Empty;
        public ApplicationType Type { get; set; }
        public int Port { get; set; } = 8080;
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
        public List<string> Dependencies { get; set; } = new();
        public bool IncludeHealthCheck { get; set; } = true;
        public bool IncludeReadinessProbe { get; set; } = true;
    }

    /// <summary>
    /// Database specification
    /// </summary>
    public class DatabaseSpec
    {
        public string Name { get; set; } = string.Empty;
        public DatabaseType Type { get; set; }
        public string Version { get; set; } = string.Empty;
        public DatabaseTier Tier { get; set; } = DatabaseTier.Standard;
        public int StorageGB { get; set; } = 32;
        public bool HighAvailability { get; set; }
        public bool BackupEnabled { get; set; } = true;
        public int RetentionDays { get; set; } = 7;
        public DatabaseLocation Location { get; set; } = DatabaseLocation.Cloud;
    }

    /// <summary>
    /// Infrastructure specification
    /// </summary>
    public class InfrastructureSpec
    {
        public InfrastructureFormat Format { get; set; } = InfrastructureFormat.Kubernetes;
        public CloudProvider Provider { get; set; } = CloudProvider.Azure;
        public string Region { get; set; } = "eastus";
        public ComputePlatform ComputePlatform { get; set; } = ComputePlatform.Kubernetes;
        public bool IncludeNetworking { get; set; } = true;
        public NetworkingConfiguration? NetworkConfig { get; set; }
        public bool IncludeStorage { get; set; }
        public bool IncludeLoadBalancer { get; set; }
        
        // Core Infrastructure Identifiers
        public string? SubscriptionId { get; set; }
        public string? ClusterName { get; set; }
        public string? ResourceGroupName { get; set; }
        
        // === AZURE ZERO TRUST PARAMETERS (AKS, App Service, Container Apps, Container Instances) ===
        
        // AKS/Bicep Shared Parameters
        public bool? EnablePrivateCluster { get; set; }
        public string? AuthorizedIPRanges { get; set; }
        public bool? EnableWorkloadIdentity { get; set; }
        public string? LogAnalyticsWorkspaceId { get; set; }
        public bool? EnableAzurePolicy { get; set; }
        public bool? EnableImageCleaner { get; set; }
        public string? DiskEncryptionSetId { get; set; }
        public bool? EnablePrivateEndpointACR { get; set; }
        
        // App Service Parameters
        public bool? HttpsOnly { get; set; }
        public bool? EnableVnetIntegration { get; set; }
        public string? VnetSubnetId { get; set; }
        public string? FtpsState { get; set; }
        public string? MinTlsVersion { get; set; }
        public bool? EnableManagedIdentity { get; set; }
        public bool? EnableClientCertificate { get; set; }
        public string? ClientCertMode { get; set; }
        public string? AppServicePlanSku { get; set; } // B1, B2, B3, S1, S2, S3, P1v3, P2v3, P3v3
        public bool? AlwaysOn { get; set; }
        
        // Container Apps Parameters
        public bool? EnablePrivateEndpointCA { get; set; }
        public bool? EnableManagedIdentityCA { get; set; }
        public bool? EnableIPRestrictionsCA { get; set; }
        public string? ContainerImage { get; set; }
        public int? ContainerPort { get; set; }
        public int? MinReplicas { get; set; }
        public int? MaxReplicas { get; set; }
        public string? CpuCores { get; set; } // e.g., "0.5", "1.0"
        public string? MemorySize { get; set; } // e.g., "1Gi", "2Gi"
        public bool? EnableDapr { get; set; }
        public bool? ExternalIngress { get; set; }
        public bool? AllowInsecure { get; set; }
        
        // AKS Terraform-Specific (additional parameters)
        public bool? EnablePrivateClusterTF { get; set; }
        public string? AuthorizedIPRangesTF { get; set; }
        public bool? EnableWorkloadIdentityTF { get; set; }
        public string? LogAnalyticsWorkspaceIdTF { get; set; }
        public bool? EnableAzurePolicyTF { get; set; }
        public bool? EnableImageCleanerTF { get; set; }
        public int? ImageCleanerIntervalHours { get; set; }
        public string? DiskEncryptionSetIdTF { get; set; }
        public bool? EnableDefender { get; set; }
        public bool? EnablePrivateEndpointACRTF { get; set; }
        public string? AcrSubnetId { get; set; }
        public bool? EnableAzureRBAC { get; set; }
        public bool? EnableOIDCIssuer { get; set; }
        public bool? EnablePodSecurityPolicyTF { get; set; }
        public string? NetworkPolicy { get; set; }
        public bool? EnableHTTPApplicationRouting { get; set; }
        public bool? EnableKeyVaultSecretsProvider { get; set; }
        public bool? EnableSecretRotation { get; set; }
        public string? SecretRotationPollInterval { get; set; }
        public string? RotationPollInterval { get; set; }
        public bool? EnableAutoScalingTF { get; set; }
        public string? MaintenanceWindow { get; set; }
        public bool? EnableMaintenanceWindow { get; set; }
        public string? NodePoolSubnetId { get; set; }
        
        // AKS Cluster Configuration
        public string? KubernetesVersion { get; set; } = "1.30";
        public int NodeCount { get; set; } = 3;
        public string? VmSize { get; set; } = "Standard_D4s_v3";
        public string? Environment { get; set; } = "dev"; // dev, staging, prod
        public string? NodeSize { get; set; } // Alias for VmSize for backward compatibility
        public bool EnableAutoScaling { get; set; } = false;
        
        // AKS Node Pool Configuration
        public int MinNodeCount { get; set; } = 2;
        public int MaxNodeCount { get; set; } = 10;
        public int UserMinNodeCount { get; set; } = 1;
        public int UserMaxNodeCount { get; set; } = 20;
        public int MaxPodsPerNode { get; set; } = 110;
        public string OsSku { get; set; } = "AzureLinux"; // "Ubuntu", "AzureLinux"
        
        // AKS Network Configuration  
        public string ServiceCidr { get; set; } = "10.2.0.0/16";
        public string DnsServiceIP { get; set; } = "10.2.0.10";
        public string NetworkPlugin { get; set; } = "azure"; // "azure", "kubenet"
        public string LoadBalancerSku { get; set; } = "standard";
        public string OutboundType { get; set; } = "loadBalancer";
        
        // AKS SKU Configuration
        public string AksSkuTier { get; set; } = "Standard"; // "Free", "Standard", "Premium"
        public string SupportPlan { get; set; } = "KubernetesOfficial"; // "KubernetesOfficial", "AKSLongTermSupport"
        
        // Auto-upgrade Configuration
        public string UpgradeChannel { get; set; } = "stable"; // "none", "patch", "stable", "rapid", "node-image"
        public string NodeOsUpgradeChannel { get; set; } = "NodeImage"; // "None", "Unmanaged", "SecurityPatch", "NodeImage"
        
        // Resource Tags
        public Dictionary<string, string>? Tags { get; set; }
    }

    /// <summary>
    /// Comprehensive networking configuration
    /// </summary>
    public class NetworkingConfiguration
    {
        // Network Mode: Create new or use existing
        public NetworkMode Mode { get; set; } = NetworkMode.CreateNew;
        
        // Existing Network References (when Mode = UseExisting)
        public string? ExistingVNetResourceId { get; set; }
        public string? ExistingVNetName { get; set; }
        public string? ExistingVNetResourceGroup { get; set; }
        public List<ExistingSubnetReference> ExistingSubnets { get; set; } = new();
        
        // New Network Configuration (when Mode = CreateNew)
        public string VNetName { get; set; } = "vnet-default";
        public string VNetAddressSpace { get; set; } = "10.0.0.0/16";
        
        // Subnet Configuration (for new networks)
        public List<SubnetConfiguration> Subnets { get; set; } = new();
        
        // Network Security Group
        public bool EnableNetworkSecurityGroup { get; set; } = true;
        public string? NsgMode { get; set; } = "new";  // "new" or "existing"
        public string? NsgName { get; set; }
        public string? ExistingNsgResourceId { get; set; }
        public List<NetworkSecurityRule> NsgRules { get; set; } = new();
        
        // DDoS Protection
        public bool EnableDDoSProtection { get; set; } = false;
        public string? DdosMode { get; set; } = "new";  // "new" or "existing"
        public string? DDoSProtectionPlanId { get; set; }
        
        // Private DNS
        public bool EnablePrivateDns { get; set; } = true;
        public string? PrivateDnsMode { get; set; } = "new";  // "new" or "existing"
        public string? PrivateDnsZoneName { get; set; }
        public string? ExistingPrivateDnsZoneResourceId { get; set; }
        
        // Private Endpoint Configuration
        public bool EnablePrivateEndpoint { get; set; } = true;
        public string PrivateEndpointSubnetName { get; set; } = "privateendpoints-subnet";
        
        // Service Endpoints
        public bool EnableServiceEndpoints { get; set; } = false;
        public List<string> ServiceEndpoints { get; set; } = new(); // e.g., "Microsoft.Storage", "Microsoft.Sql"
        
        // VNet Peering/Links
        public bool EnableVNetPeering { get; set; } = false;
        public List<VNetPeeringConfiguration> VNetPeerings { get; set; } = new();
    }
    
    /// <summary>
    /// VNet Peering configuration
    /// </summary>
    public class VNetPeeringConfiguration
    {
        public string Name { get; set; } = string.Empty;
        public string RemoteVNetResourceId { get; set; } = string.Empty;
        public string? RemoteVNetName { get; set; }
        public bool AllowVirtualNetworkAccess { get; set; } = true;
        public bool AllowForwardedTraffic { get; set; } = false;
        public bool AllowGatewayTransit { get; set; } = false;
        public bool UseRemoteGateways { get; set; } = false;
    }

    /// <summary>
    /// Network mode: Create new infrastructure or use existing
    /// </summary>
    public enum NetworkMode
    {
        CreateNew,      // Generator will create VNet, subnets, NSG, etc.
        UseExisting     // Generator will reference existing VNet and subnets
    }
    
    /// <summary>
    /// Reference to an existing subnet
    /// </summary>
    public class ExistingSubnetReference
    {
        public string Name { get; set; } = "";
        public string SubnetId { get; set; } = "";  // Full ARM resource ID
        public string AddressPrefix { get; set; } = "";  // For validation/documentation
        public SubnetPurpose Purpose { get; set; } = SubnetPurpose.Application;  // What this subnet is used for
    }
    
    /// <summary>
    /// Purpose of a subnet in the architecture
    /// </summary>
    public enum SubnetPurpose
    {
        // Basic/Legacy purposes
        Application,        // Main application subnet (App Service, AKS nodes, Container Instances)
        PrivateEndpoints,   // For private endpoints
        ApplicationGateway, // For Application Gateway (AKS ingress)
        Database,           // For database resources
        Other,              // Custom purpose
        
        // 3-Tier Architecture purposes
        WebTier,            // Web/presentation tier - public-facing
        ApplicationTier,    // Application/business logic tier
        DataTier,           // Data/persistence tier
        
        // AKS-specific purposes
        AksSystemNodePool,  // AKS system node pool
        AksUserNodePool,    // AKS user/workload node pool
        AksIngress,         // AKS ingress controller subnet
        
        // Landing Zone purposes
        Management,         // Management subnet for bastion, jump boxes
        SharedServices,     // Shared services - DNS, AD, monitoring
        Workload,           // Primary workload subnet
        
        // Security/Infrastructure purposes
        Firewall,           // Azure Firewall subnet
        Bastion,            // Azure Bastion subnet
        Gateway             // VPN/ExpressRoute Gateway subnet
    }

    /// <summary>
    /// Subnet configuration for VNet
    /// </summary>
    public class SubnetConfiguration
    {
        public string Name { get; set; } = "";
        public string AddressPrefix { get; set; } = "";
        public string? Delegation { get; set; } // e.g., "Microsoft.Web/serverFarms"
        public bool EnableServiceEndpoints { get; set; } = false;
        public List<string> ServiceEndpoints { get; set; } = new();
        public SubnetPurpose Purpose { get; set; } = SubnetPurpose.Application;  // Purpose of this subnet
    }

    /// <summary>
    /// Network Security Group rule
    /// </summary>
    public class NetworkSecurityRule
    {
        public string Name { get; set; } = "";
        public int Priority { get; set; } = 100;
        public string Direction { get; set; } = "Inbound"; // Inbound or Outbound
        public string Access { get; set; } = "Allow"; // Allow or Deny
        public string Protocol { get; set; } = "Tcp"; // Tcp, Udp, Icmp, Esp, Ah, or *
        public string SourcePortRange { get; set; } = "*";
        public string DestinationPortRange { get; set; } = "*";
        public string SourceAddressPrefix { get; set; } = "*";
        public string DestinationAddressPrefix { get; set; } = "*";
        public string Description { get; set; } = "";
    }

    /// <summary>
    /// Deployment configuration
    /// </summary>
    public class DeploymentSpec
    {
        public int Replicas { get; set; } = 3;
        public bool AutoScaling { get; set; } = true;
        public int MinReplicas { get; set; } = 1;
        public int MaxReplicas { get; set; } = 10;
        public int TargetCpuPercent { get; set; } = 70;
        public int TargetMemoryPercent { get; set; } = 80;
        public ResourceRequirements Resources { get; set; } = new();
        public RollingUpdateStrategy UpdateStrategy { get; set; } = new();
    }

    /// <summary>
    /// Security specification
    /// </summary>
    public class SecuritySpec
    {
        // General Security Settings
        public bool NetworkPolicies { get; set; }
        public bool PodSecurityPolicies { get; set; }
        public bool ServiceAccount { get; set; } = true;
        public bool RBAC { get; set; } = true;
        public bool TLS { get; set; } = true;
        public bool SecretsManagement { get; set; } = true;
        public List<string> ComplianceStandards { get; set; } = new();
        
        // AKS-Specific Security Settings
        public bool EnablePrivateCluster { get; set; } = true;
        public bool EnableWorkloadIdentity { get; set; } = true;
        public bool EnableAzurePolicy { get; set; } = true;
        public bool EnableImageCleaner { get; set; } = true;
        public bool EnableDefender { get; set; } = true;
        public bool EnableKeyVault { get; set; } = true;
        public bool DisableLocalAccounts { get; set; } = true;
        public bool EnablePrivateEndpoint { get; set; } = true;
        public List<string> AuthorizedIPRanges { get; set; } = new();
        public string? DiskEncryptionSetId { get; set; }
        
        // Key Vault Configuration
        public bool EnableKeyVaultSecretsProvider { get; set; } = true;
        public bool EnableSecretRotation { get; set; } = true;
        public string SecretRotationPollInterval { get; set; } = "2m";
        public bool EnablePurgeProtection { get; set; } = true;
        public bool EnableForDeployment { get; set; } = true;
        public bool EnableForDiskEncryption { get; set; } = true;
        public bool EnableForTemplateDeployment { get; set; } = true;
        
        // TLS/SSL Configuration
        public string? TLSVersion { get; set; } = "1.2";
        
        // Firewall Configuration
        public bool EnableFirewall { get; set; } = true;
        
        // Network Security
        public bool EnableNetworkPolicy { get; set; } = true;
        public string NetworkPolicyProvider { get; set; } = "azure"; // "azure", "calico", "cilium"
        public string? NetworkPolicy { get; set; } // For backward compatibility
        
        // Identity and Access
        public bool? EnableManagedIdentity { get; set; }
        public bool? EnableAADIntegration { get; set; }
        public bool? EnableAzureRBAC { get; set; }
        public bool? EnableSecretStore { get; set; }
        
        // App Service / Container Apps Specific
        public bool? HttpsOnly { get; set; }
        public bool? AllowInsecure { get; set; }
    }

    /// <summary>
    /// Observability specification
    /// </summary>
    public class ObservabilitySpec
    {
        public bool Prometheus { get; set; } = true;
        public bool Grafana { get; set; }
        public bool ApplicationInsights { get; set; }
        public bool CloudWatch { get; set; }
        public bool StructuredLogging { get; set; } = true;
        public bool DistributedTracing { get; set; }
        
        // Azure-specific monitoring
        public bool? EnableContainerInsights { get; set; }
        public bool? EnablePrometheus { get; set; } // For Azure Monitor Workspace
        public bool? EnableDiagnostics { get; set; }
    }

    public class ResourceRequirements
    {
        public string CpuRequest { get; set; } = "100m";
        public string CpuLimit { get; set; } = "500m";
        public string MemoryRequest { get; set; } = "128Mi";
        public string MemoryLimit { get; set; } = "512Mi";
    }

    public class RollingUpdateStrategy
    {
        public int MaxSurge { get; set; } = 1;
        public int MaxUnavailable { get; set; } = 0;
    }

    // ========== ENUMS ==========

    public enum ProgrammingLanguage
    {
        DotNet,
        NodeJS,
        Python,
        Java,
        Go,
        Rust,
        Ruby,
        PHP
    }

    public enum ApplicationType
    {
        WebAPI,
        WebApp,
        BackgroundWorker,
        MessageConsumer,
        Microservice,
        Serverless
    }

    public enum DatabaseType
    {
        // Relational
        PostgreSQL,
        MySQL,
        SQLServer,
        AzureSQL,
        
        // NoSQL
        MongoDB,
        CosmosDB,
        DynamoDB,
        Redis,
        
        // Time-series
        InfluxDB,
        TimescaleDB,
        
        // Graph
        Neo4j,
        
        // Search
        Elasticsearch
    }

    public enum DatabaseTier
    {
        Basic,
        Standard,
        Premium,
        Serverless
    }

    public enum DatabaseLocation
    {
        Cloud,          // Managed cloud service
        Kubernetes,     // In-cluster deployment
        External        // External connection
    }

    public enum InfrastructureFormat
    {
        Kubernetes,     // K8s YAML
        Bicep,          // Azure Bicep
        Terraform,      // Terraform HCL
        ARM,            // Azure Resource Manager JSON
        CloudFormation, // AWS CloudFormation
        Pulumi          // Pulumi (various languages)
    }

    public enum CloudProvider
    {
        Azure,
        AWS,
        GCP,
        OnPremises
    }

    public enum ComputePlatform
    {
        // Kubernetes Platforms
        AKS,                // Azure Kubernetes Service
        EKS,                // AWS Elastic Kubernetes Service
        GKE,                // Google Kubernetes Engine
        
        // Container Platforms
        ECS,                // AWS Elastic Container Service
        CloudRun,           // GCP Cloud Run
        ContainerApps,      // Azure Container Apps
        
        // Platform-as-a-Service
        AppService,         // Azure App Service (Web Apps, API Apps)
        
        // Serverless
        Lambda,             // AWS Lambda
        Functions,          // Azure Functions
        
        // Traditional Infrastructure
        VirtualMachine,     // Traditional VMs (singular)
        VirtualMachines,    // Traditional VMs (plural - for consistency)
        Fargate,            // AWS Fargate (serverless containers)
        
        // Network-Only Infrastructure
        Network,            // Pure network infrastructure (VNet, Subnets, NSG, Peering) without compute
        Networking,         // Network infrastructure (alternative naming)
        
        // Storage Infrastructure
        Storage,            // Storage accounts, blobs, file shares
        
        // Database Infrastructure
        Database,           // Managed database services
        
        // Security Infrastructure
        Security,           // Key Vault, secrets management, certificate management
        
        // Legacy/Generic
        Kubernetes          // Generic Kubernetes (kept for backward compatibility)
    }

    /// <summary>
    /// Result of template generation
    /// </summary>
    public class TemplateGenerationResult
    {
        public bool Success { get; set; }
        public Dictionary<string, string> Files { get; set; } = new();
        public List<string> GeneratedComponents { get; set; } = new();
        public string Summary { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
    }

    // ======================================================================
    // COMPOSITE INFRASTRUCTURE MODELS - Multi-Resource Orchestration
    // ======================================================================

    /// <summary>
    /// Architecture patterns for pre-built infrastructure compositions
    /// </summary>
    public enum ArchitecturePattern
    {
        /// <summary>Custom composition - no pre-defined pattern, user specifies all resources</summary>
        Custom,
        
        /// <summary>Classic 3-tier: VNet with web/app/data subnets + tiered NSGs</summary>
        ThreeTier,
        
        /// <summary>Azure Landing Zone: Hub-spoke VNet, AKS, Key Vault, Log Analytics</summary>
        LandingZone,
        
        /// <summary>AKS with supporting infrastructure: VNet, ACR, Key Vault, Managed Identity</summary>
        AksWithVNet,
        
        /// <summary>Microservices: AKS + Container Registry + Service Mesh + Observability</summary>
        Microservices,
        
        /// <summary>Serverless: Azure Functions + Storage + Event Grid + App Insights</summary>
        Serverless,
        
        /// <summary>Data Platform: Storage + SQL + Cosmos DB + Data Factory</summary>
        DataPlatform,
        
        /// <summary>SCCA-compliant: 3-tier VNet with CAP/VDSS zones + Firewall + Log Analytics</summary>
        SccaCompliant
    }

    /// <summary>
    /// Request for composite infrastructure generation with multiple resources
    /// </summary>
    public class CompositeInfrastructureRequest
    {
        /// <summary>Base name for the deployment (used as prefix for resources)</summary>
        public string ServiceName { get; set; } = string.Empty;
        
        /// <summary>Human-readable description of the infrastructure</summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>Pre-defined architecture pattern (or Custom for manual specification)</summary>
        public ArchitecturePattern Pattern { get; set; } = ArchitecturePattern.Custom;
        
        /// <summary>Output format for generated templates</summary>
        public InfrastructureFormat Format { get; set; } = InfrastructureFormat.Bicep;
        
        /// <summary>Target cloud provider</summary>
        public CloudProvider Provider { get; set; } = CloudProvider.Azure;
        
        /// <summary>Azure region/location for all resources</summary>
        public string Region { get; set; } = "eastus";
        
        /// <summary>Environment tier (dev, staging, prod) - affects sizing and redundancy</summary>
        public string Environment { get; set; } = "dev";
        
        /// <summary>Subscription ID (optional, for resource naming)</summary>
        public string? SubscriptionId { get; set; }
        
        /// <summary>Individual resource specifications (for Custom pattern or overrides)</summary>
        public List<ResourceSpec> Resources { get; set; } = new();
        
        /// <summary>Explicit dependencies between resources (auto-detected when possible)</summary>
        public List<ResourceDependency> Dependencies { get; set; } = new();
        
        /// <summary>Custom tags to apply to all resources</summary>
        public Dictionary<string, string> Tags { get; set; } = new();
        
        /// <summary>Network configuration overrides</summary>
        public NetworkingConfiguration? NetworkConfig { get; set; }
        
        /// <summary>Security configuration overrides</summary>
        public SecuritySpec? Security { get; set; }
    }

    /// <summary>
    /// Specification for a single resource within a composite deployment
    /// </summary>
    public class ResourceSpec
    {
        /// <summary>Unique identifier within the composite (e.g., "vnet", "aks", "keyvault")</summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>Display name for the resource</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>Resource type (maps to ComputePlatform or Azure resource type)</summary>
        public string ResourceType { get; set; } = string.Empty;
        
        /// <summary>Compute platform for this resource</summary>
        public ComputePlatform Platform { get; set; }
        
        /// <summary>Parent resource ID (for hierarchical resources like subnets in VNet)</summary>
        public string? ParentResourceId { get; set; }
        
        /// <summary>Resource-specific configuration as key-value pairs</summary>
        public Dictionary<string, object> Configuration { get; set; } = new();
        
        /// <summary>Custom tags specific to this resource</summary>
        public Dictionary<string, string> Tags { get; set; } = new();
        
        /// <summary>Whether this resource should be created (for conditional resources)</summary>
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Dependency relationship between two resources
    /// </summary>
    public class ResourceDependency
    {
        /// <summary>ID of the resource that depends on another</summary>
        public string SourceResourceId { get; set; } = string.Empty;
        
        /// <summary>ID of the resource that must be created first</summary>
        public string TargetResourceId { get; set; } = string.Empty;
        
        /// <summary>Output name from target resource to pass to source</summary>
        public string? OutputName { get; set; }
        
        /// <summary>Input parameter name on source resource to receive the output</summary>
        public string? InputName { get; set; }
        
        /// <summary>Type of dependency</summary>
        public DependencyType Type { get; set; } = DependencyType.CreationOrder;
    }

    /// <summary>
    /// Types of resource dependencies
    /// </summary>
    public enum DependencyType
    {
        /// <summary>Target must be created before source (implicit resource reference)</summary>
        CreationOrder,
        
        /// <summary>Source uses a specific output from target (explicit output-to-input)</summary>
        OutputToInput,
        
        /// <summary>Source is deployed into target (e.g., AKS into VNet)</summary>
        DeployedInto,
        
        /// <summary>Source references target by resource ID</summary>
        ResourceReference
    }

    /// <summary>
    /// Result of composite infrastructure generation
    /// </summary>
    public class CompositeGenerationResult
    {
        public bool Success { get; set; }
        
        /// <summary>All generated files (main.bicep/main.tf + modules)</summary>
        public Dictionary<string, string> Files { get; set; } = new();
        
        /// <summary>Summary of what was generated</summary>
        public string Summary { get; set; } = string.Empty;
        
        /// <summary>Error message if generation failed</summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>Per-resource generation results</summary>
        public List<ResourceGenerationResult> ResourceResults { get; set; } = new();
        
        /// <summary>The generated main orchestrator file path (main.bicep or main.tf)</summary>
        public string MainFilePath { get; set; } = string.Empty;
        
        /// <summary>List of generated module paths</summary>
        public List<string> ModulePaths { get; set; } = new();
    }

    /// <summary>
    /// Generation result for a single resource within a composite
    /// </summary>
    public class ResourceGenerationResult
    {
        public string ResourceId { get; set; } = string.Empty;
        public string ResourceType { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string ModulePath { get; set; } = string.Empty;
        public List<string> OutputNames { get; set; } = new();
    }
}
