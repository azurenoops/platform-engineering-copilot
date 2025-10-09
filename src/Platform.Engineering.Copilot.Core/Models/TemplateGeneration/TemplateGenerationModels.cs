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
        
        // Container Apps Parameters
        public bool? EnablePrivateEndpointCA { get; set; }
        public bool? EnableManagedIdentityCA { get; set; }
        public bool? EnableIPRestrictionsCA { get; set; }
        
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
        Application,        // Main application subnet (App Service, AKS nodes, Container Instances)
        PrivateEndpoints,   // For private endpoints
        ApplicationGateway, // For Application Gateway (AKS ingress)
        Database,          // For database resources
        Other              // Custom purpose
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
        public bool NetworkPolicies { get; set; }
        public bool PodSecurityPolicies { get; set; }
        public bool ServiceAccount { get; set; } = true;
        public bool RBAC { get; set; } = true;
        public bool TLS { get; set; } = true;
        public bool SecretsManagement { get; set; } = true;
        public List<string> ComplianceStandards { get; set; } = new();
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
        
        // Traditional Infrastructure
        VirtualMachine,     // Traditional VMs
        
        // Network-Only Infrastructure
        Network,            // Pure network infrastructure (VNet, Subnets, NSG, Peering) without compute
        
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
    }
}
