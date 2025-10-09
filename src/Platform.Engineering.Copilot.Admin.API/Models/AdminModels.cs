using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Admin.Models;

/// <summary>
/// Request to create a new service template
/// </summary>
public class CreateTemplateRequest
{
    public string TemplateName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Version { get; set; }
    public string? CreatedBy { get; set; }
    public bool IsPublic { get; set; } = false;
    public string? TemplateType { get; set; } // microservice, web-app, api, infrastructure, data-pipeline, ml-platform, serverless
    
    // Template generation specifications
    public ApplicationSpec? Application { get; set; }
    public List<DatabaseSpec>? Databases { get; set; }
    public InfrastructureSpec? Infrastructure { get; set; }
    public DeploymentSpec? Deployment { get; set; }
    public SecuritySpec? Security { get; set; }
    public ObservabilitySpec? Observability { get; set; }
    
    // New configuration models for platform-specific settings
    public ComputeConfiguration? Compute { get; set; }
    public NetworkConfiguration? Network { get; set; }
}

/// <summary>
/// Request to update an existing template
/// </summary>
public class UpdateTemplateRequest
{
    public string? TemplateName { get; set; }
    public string? ServiceName { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public bool? IsActive { get; set; }
    public string? TemplateType { get; set; } // microservice, web-app, api, infrastructure, data-pipeline, ml-platform, serverless
    public string? Format { get; set; } // Bicep, Terraform, ARM, etc.
    public bool? IsPublic { get; set; }
    
    // Template generation specifications (optional - for regeneration)
    // Note: Using a simplified infrastructure DTO to avoid enum parsing issues
    public ApplicationSpec? Application { get; set; }
    public List<DatabaseSpec>? Databases { get; set; }
    public UpdateInfrastructureDto? Infrastructure { get; set; }
    public DeploymentSpec? Deployment { get; set; }
    public SecuritySpec? Security { get; set; }
    public ObservabilitySpec? Observability { get; set; }
    public ComputeConfiguration? Compute { get; set; }
    public NetworkConfiguration? Network { get; set; }
    
    // If provided, will regenerate template files
    public TemplateGenerationRequest? TemplateGenerationRequest { get; set; }
}

/// <summary>
/// Request to update a specific file in a template
/// </summary>
public class UpdateTemplateFileRequest
{
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Simplified infrastructure DTO for updates to avoid enum parsing issues
/// </summary>
public class UpdateInfrastructureDto
{
    public string? Format { get; set; }
    public string? ComputePlatform { get; set; }
    public string? CloudProvider { get; set; }
    public string? Region { get; set; }
    public bool? IncludeNetworking { get; set; }
    public bool? IncludeStorage { get; set; }
    public bool? IncludeLoadBalancer { get; set; }
}

/// <summary>
/// Request to validate template configuration
/// </summary>
public class ValidateTemplateRequest
{
    public string ServiceName { get; set; } = string.Empty;
    public ApplicationSpec? Application { get; set; }
    public List<DatabaseSpec>? Databases { get; set; }
    public InfrastructureSpec? Infrastructure { get; set; }
    public DeploymentSpec? Deployment { get; set; }
    public SecuritySpec? Security { get; set; }
    public ObservabilitySpec? Observability { get; set; }
}

/// <summary>
/// Response from template creation/update
/// </summary>
public class TemplateCreationResponse
{
    public bool Success { get; set; }
    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string>? GeneratedFiles { get; set; }
    public List<string>? ComponentsGenerated { get; set; }
    public string? Summary { get; set; }
}

/// <summary>
/// Response from template validation
/// </summary>
public class TemplateValidationResponse
{
    public bool IsValid { get; set; }
    public List<ValidationErrorDto> Errors { get; set; } = new();
    public List<ValidationWarningDto> Warnings { get; set; } = new();
    public List<ValidationRecommendationDto> Recommendations { get; set; } = new();
    public string? Platform { get; set; }
    public long ValidationTimeMs { get; set; }
}

public class ValidationErrorDto
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? CurrentValue { get; set; }
    public string? ExpectedValue { get; set; }
    public string? DocumentationUrl { get; set; }
}

public class ValidationWarningDto
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string? Impact { get; set; }
}

public class ValidationRecommendationDto
{
    public string Field { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? CurrentValue { get; set; }
    public string? RecommendedValue { get; set; }
    public string? Reason { get; set; }
    public string? Benefit { get; set; }
}

/// <summary>
/// Request for infrastructure provisioning
/// </summary>
public class ProvisionInfrastructureRequest
{
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Location { get; set; } = "eastus";
    public string SubscriptionId { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new();
    
    // Infrastructure resource type (e.g., "vnet", "storage-account", "key-vault")
    public string? ResourceType { get; set; }
    
    // Legacy support
    public string? TemplateId { get; set; } // For backwards compatibility with template-based provisioning
    public string? InfrastructureType { get; set; } // AKS, AppService, FunctionApp, etc. (legacy)
    
    // Resource-specific parameters
    public Dictionary<string, object>? Parameters { get; set; }
    public Dictionary<string, object> Configuration { get; set; } = new();
}

/// <summary>
/// Response from infrastructure provisioning
/// </summary>
public class ProvisionInfrastructureResponse
{
    public bool Success { get; set; }
    public string? ResourceGroupId { get; set; }
    public string? DeploymentId { get; set; }
    public string? Message { get; set; }
    public List<string> ProvisionedResources { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Request for bulk template operations
/// </summary>
public class BulkTemplateOperationRequest
{
    public List<string> TemplateIds { get; set; } = new();
    public string Operation { get; set; } = string.Empty; // "activate", "deactivate", "delete"
}

/// <summary>
/// Response from bulk operations
/// </summary>
public class BulkOperationResponse
{
    public bool Success { get; set; }
    public int TotalRequested { get; set; }
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<string> FailedTemplateIds { get; set; } = new();
    public Dictionary<string, string> Errors { get; set; } = new();
}

/// <summary>
/// Platform-specific compute configuration
/// Supports different compute platforms: AKS, EKS, ECS, App Service, Cloud Run, Lambda
/// </summary>
public class ComputeConfiguration
{
    // Common properties across all platforms
    public string? InstanceType { get; set; }  // VM size, instance type, SKU, or memory tier
    public int? MinInstances { get; set; }
    public int? MaxInstances { get; set; }
    public bool? EnableAutoScaling { get; set; }
    
    // Resource limits (Kubernetes-style or platform-specific)
    public string? CpuLimit { get; set; }      // e.g., "2", "500m", "1024" (ECS units), timeout seconds (Lambda)
    public string? MemoryLimit { get; set; }   // e.g., "4Gi", "512Mi", "1024" (MB for ECS/Lambda)
    public string? StorageSize { get; set; }   // PVC size, EBS volume, ephemeral storage
    
    // Platform-specific properties
    public bool? EnableSpotInstances { get; set; }  // Spot/Preemptible instances (AKS/EKS/ECS)
    public string? ContainerImage { get; set; }     // Container image URI
    public string? NodePoolName { get; set; }       // Node pool/group name (Kubernetes platforms)
    
    // Additional platform-specific metadata stored as JSON
    public Dictionary<string, object>? PlatformSpecificConfig { get; set; }
}

/// <summary>
/// Network configuration for infrastructure templates
/// </summary>
public class NetworkConfiguration
{
    // Virtual Network / VPC
    public string? VNetName { get; set; }
    public string? VNetAddressSpace { get; set; }  // e.g., "10.0.0.0/16"
    
    // Subnets
    public List<SubnetConfig>? Subnets { get; set; }
    
    // Service Endpoints & Private Link
    public List<string>? ServiceEndpoints { get; set; }  // e.g., "Microsoft.Storage", "Microsoft.Sql"
    public bool? EnablePrivateEndpoint { get; set; }
    
    // Network Security Group (NSG)
    public bool? EnableNetworkSecurityGroup { get; set; }
    public string? NsgMode { get; set; }  // "new" or "existing"
    public string? NsgName { get; set; }
    public string? ExistingNsgResourceId { get; set; }
    public List<NsgRule>? NsgRules { get; set; }
    
    // DDoS Protection
    public bool? EnableDDoSProtection { get; set; }
    public string? DdosMode { get; set; }  // "new" or "existing"
    public string? DdosProtectionPlanId { get; set; }
    
    // Private DNS
    public bool? EnablePrivateDns { get; set; }
    public string? PrivateDnsMode { get; set; }  // "new" or "existing"
    public string? DnsZone { get; set; }
    public string? PrivateDnsZoneName { get; set; }
    public string? ExistingPrivateDnsZoneResourceId { get; set; }
    
    // VNet Peering/Links
    public bool? EnableVNetPeering { get; set; }
    public List<VNetPeeringConfig>? VNetPeerings { get; set; }
}

/// <summary>
/// Subnet configuration
/// </summary>
public class SubnetConfig
{
    public string Name { get; set; } = string.Empty;
    public string AddressPrefix { get; set; } = string.Empty;  // e.g., "10.0.1.0/24"
    public bool? EnableServiceEndpoints { get; set; }
    public List<string>? ServiceEndpointTypes { get; set; }
}

/// <summary>
/// Network Security Group rule
/// </summary>
public class NsgRule
{
    public string Name { get; set; } = string.Empty;
    public int Priority { get; set; }
    public string Direction { get; set; } = "Inbound";  // Inbound or Outbound
    public string Access { get; set; } = "Allow";       // Allow or Deny
    public string Protocol { get; set; } = "Tcp";        // Tcp, Udp, or *
    public string? SourceAddressPrefix { get; set; }
    public string? SourcePortRange { get; set; }
    public string? DestinationAddressPrefix { get; set; }
    public string? DestinationPortRange { get; set; }
}

/// <summary>
/// VNet Peering configuration
/// </summary>
public class VNetPeeringConfig
{
    public string Name { get; set; } = string.Empty;
    public string RemoteVNetResourceId { get; set; } = string.Empty;
    public string? RemoteVNetName { get; set; }
    public bool? AllowVirtualNetworkAccess { get; set; } = true;
    public bool? AllowForwardedTraffic { get; set; } = false;
    public bool? AllowGatewayTransit { get; set; } = false;
    public bool? UseRemoteGateways { get; set; } = false;
}

/// <summary>
/// Response containing all files for a template
/// </summary>
public class TemplateFilesResponse
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public int FilesCount { get; set; }
    public List<TemplateFileDto> Files { get; set; } = new();
}

/// <summary>
/// Individual template file DTO
/// </summary>
public class TemplateFileDto
{
    public string FileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public bool IsEntryPoint { get; set; }
    public int Order { get; set; }
    public int Size { get; set; }
}

#region Environment Management DTOs

/// <summary>
/// Request to create a new environment from a template
/// </summary>
public class CreateEnvironmentRequest
{
    public string EnvironmentName { get; set; } = string.Empty;
    public string EnvironmentType { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = "eastus";
    public string? SubscriptionId { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public object? ComputeConfiguration { get; set; }
    public object? NetworkConfiguration { get; set; }
    public object? SecurityConfiguration { get; set; }
    public object? MonitoringConfiguration { get; set; }
    public object? ScalingConfiguration { get; set; }
    
    // Template-based creation properties
    public string? TemplateId { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public bool EnableMonitoring { get; set; } = true;
    public bool EnableLogging { get; set; } = true;
}

/// <summary>
/// Request to clone an environment
/// </summary>
public class CloneEnvironmentRequest
{
    public string SourceResourceGroup { get; set; } = string.Empty;
    public string TargetEnvironmentName { get; set; } = string.Empty;
    public string TargetResourceGroup { get; set; } = string.Empty;
    public string? TargetSubscriptionId { get; set; }
    public bool IncludeData { get; set; } = false;
    public bool MaskSensitiveData { get; set; } = true;
    public bool IncludeSecrets { get; set; } = false;
    public bool CopyNetworkConfiguration { get; set; } = true;
    public bool CopyScalingPolicies { get; set; } = true;
}

/// <summary>
/// Request to scale an environment
/// </summary>
public class ScaleEnvironmentRequest
{
    public int? TargetReplicas { get; set; }
    public string? ScaleType { get; set; }
    public bool AutoScalingEnabled { get; set; } = false;
    public int? MinReplicas { get; set; }
    public int? MaxReplicas { get; set; }
    public int? TargetCpuUtilization { get; set; }
    public int? TargetMemoryUtilization { get; set; }
}

/// <summary>
/// Request to migrate an environment
/// </summary>
public class MigrateEnvironmentRequest
{
    public string SourceEnvironment { get; set; } = string.Empty;
    public string SourceResourceGroup { get; set; } = string.Empty;
    public string TargetEnvironmentName { get; set; } = string.Empty;
    public string TargetResourceGroup { get; set; } = string.Empty;
    public string? TargetSubscriptionId { get; set; }
    public string TargetLocation { get; set; } = string.Empty;
    public string MigrationType { get; set; } = "FullMigration";
    public bool IncludeData { get; set; } = true;
    public bool MinimizeDowntime { get; set; } = false;
    public bool ValidationOnly { get; set; } = false;
}

/// <summary>
/// Request to bulk delete environments
/// </summary>
public class BulkDeleteEnvironmentsRequest
{
    public List<string>? SubscriptionIds { get; set; }
    public List<string>? ResourceGroups { get; set; }
    public List<string>? Locations { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public List<string>? EnvironmentTypes { get; set; }
    public int? MaxAgeInDays { get; set; }
    public string? NamePattern { get; set; }
    public int? MaxConcurrentOperations { get; set; }
    public bool ContinueOnError { get; set; } = false;
    public bool DryRun { get; set; } = true;
    public bool RequireConfirmation { get; set; } = true;
    public bool CreateBackups { get; set; } = true;
}

/// <summary>
/// Request to cleanup old environments
/// </summary>
public class CleanupEnvironmentsRequest
{
    public int MaxAgeInDays { get; set; } = 30;
    public int MaxInactivityDays { get; set; } = 30;
    public bool DeleteIfNoActivity { get; set; } = false;
    public string? RequireTag { get; set; }
    public bool ExcludeTaggedEnvironments { get; set; } = false;
    public List<string>? ExcludeEnvironmentTypes { get; set; }
    public string? ResourceGroupFilter { get; set; }
    public bool DryRun { get; set; } = true;
}

/// <summary>
/// Request to deploy an environment
/// </summary>
public class DeployEnvironmentRequest
{
    public bool WaitForCompletion { get; set; } = true;
    public int TimeoutMinutes { get; set; } = 30;
    public Dictionary<string, string>? OverrideParameters { get; set; }
}

/// <summary>
/// Response from environment creation
/// </summary>
public class EnvironmentResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string DeployedBy { get; set; } = string.Empty;
    public Dictionary<string, string>? Tags { get; set; }
}

/// <summary>
/// Detailed environment response with related data
/// </summary>
public class EnvironmentDetailResponse : EnvironmentResponse
{
    public List<DeploymentHistoryDto> History { get; set; } = new();
    public EnvironmentMetricsSummary? Metrics { get; set; }
    public int HistoryCount { get; set; }
    public int MetricsCount { get; set; }
}

/// <summary>
/// Deployment history entry
/// </summary>
public class DeploymentHistoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    public string InitiatedBy { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Environment metrics summary
/// </summary>
public class EnvironmentMetricsSummary
{
    public Dictionary<string, MetricStatistics> MetricsByType { get; set; } = new();
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Statistics for a metric type
/// </summary>
public class MetricStatistics
{
    public int Count { get; set; }
    public decimal AvgValue { get; set; }
    public decimal MinValue { get; set; }
    public decimal MaxValue { get; set; }
    public DateTime LastTimestamp { get; set; }
}

/// <summary>
/// List of environments response
/// </summary>
public class EnvironmentListResponse
{
    public List<EnvironmentResponse> Environments { get; set; } = new();
    public int TotalCount { get; set; }
    public string? FilteredBy { get; set; }
}

/// <summary>
/// Deployment status response
/// </summary>
public class DeploymentStatusResponse
{
    public string DeploymentId { get; set; } = string.Empty;
    public string EnvironmentId { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public string CurrentOperation { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; }
    public List<ResourceStatusDto> Resources { get; set; } = new();
}

/// <summary>
/// Individual resource status
/// </summary>
public class ResourceStatusDto
{
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Deployment logs response
/// </summary>
public class DeploymentLogsResponse
{
    public string DeploymentId { get; set; } = string.Empty;
    public List<LogEntryDto> Entries { get; set; } = new();
    public int TotalEntries { get; set; }
}

/// <summary>
/// Individual log entry
/// </summary>
public class LogEntryDto
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
}

/// <summary>
/// Environment metrics request
/// </summary>
public class EnvironmentMetricsRequest
{
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? MetricType { get; set; } // cpu, memory, requests, etc.
}

/// <summary>
/// Environment metrics response
/// </summary>
public class EnvironmentMetricsResponse
{
    public string EnvironmentId { get; set; } = string.Empty;
    public List<MetricDataPoint> DataPoints { get; set; } = new();
    public TimeSpan TimeRange { get; set; }
}

/// <summary>
/// Individual metric data point
/// </summary>
public class MetricDataPoint
{
    public DateTime Timestamp { get; set; }
    public string MetricName { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string? Unit { get; set; }
}

#endregion

#region Governance and Approval Workflows

/// <summary>
/// Approval workflow DTO for API responses
/// </summary>
public class ApprovalWorkflowDto
{
    public string Id { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string> PolicyViolations { get; set; } = new();
    public List<string> RequiredApprovers { get; set; } = new();
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? ApprovalComments { get; set; }
}

/// <summary>
/// Request to approve an approval workflow
/// </summary>
public class ApproveWorkflowRequest
{
    public string ApprovedBy { get; set; } = string.Empty;
    public string? Comments { get; set; }
}

/// <summary>
/// Request to reject an approval workflow
/// </summary>
public class RejectWorkflowRequest
{
    public string RejectedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Response for approval/rejection actions
/// </summary>
public class ApprovalActionResponse
{
    public bool Success { get; set; }
    public string? WorkflowId { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Request to validate resource naming
/// </summary>
public class NamingValidationRequest
{
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string Environment { get; set; } = "dev";
}

/// <summary>
/// Response for naming validation
/// </summary>
public class NamingValidationResponse
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? SuggestedName { get; set; }
}

/// <summary>
/// Request to validate region availability
/// </summary>
public class RegionValidationRequest
{
    public string Location { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
}

/// <summary>
/// Response for region validation
/// </summary>
public class RegionValidationResponse
{
    public bool IsAvailable { get; set; }
    public bool IsApproved { get; set; }
    public List<string> UnavailableServices { get; set; } = new();
    public string? ReasonUnavailable { get; set; }
    public List<string> AlternativeRegions { get; set; } = new();
}

/// <summary>
/// Approval workflow statistics
/// </summary>
public class ApprovalStatsResponse
{
    public int TotalPending { get; set; }
    public int ExpiringSoon { get; set; }
    public Dictionary<string, int> ByEnvironment { get; set; } = new();
    public Dictionary<string, int> ByResourceType { get; set; } = new();
}

#endregion

#region Cost Estimation Models

/// <summary>
/// Request for Azure resource cost estimation
/// </summary>
public class AzureResourceCostRequest
{
    public string ServiceFamily { get; set; } = string.Empty;
    public string Region { get; set; } = "eastus";
    public string? SkuName { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; } = 1;
    public int HoursPerMonth { get; set; } = 730; // Default: 24/7
}

/// <summary>
/// Azure resource cost estimate response
/// </summary>
public class AzureResourceCostResponse
{
    public decimal MonthlyCost { get; set; }
    public string Currency { get; set; } = "USD";
    public string ServiceFamily { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int HoursPerMonth { get; set; }
    public string? Details { get; set; }
}

#endregion
