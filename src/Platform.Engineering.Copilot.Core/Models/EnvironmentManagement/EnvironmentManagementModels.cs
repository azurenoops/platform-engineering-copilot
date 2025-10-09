using System;
using System.Collections.Generic;

namespace Platform.Engineering.Copilot.Core.Models.EnvironmentManagement
{
    // ========== ENUMS ==========
    
    public enum EnvironmentType
    {
        AKS,
        WebApp,
        FunctionApp,
        ContainerApp,
        Unknown
    }
    
    public enum HealthStatus
    {
        Healthy,
        Warning,
        Critical,
        Unknown
    }
    
    public enum ScalingAction
    {
        ScaleUp,
        ScaleDown,
        AutoScalingEnabled,
        AutoScalingDisabled,
        NoChange
    }
    
    public enum DeploymentStatus
    {
        Pending,
        InProgress,
        Succeeded,
        Failed,
        Cancelled
    }
    
    public enum ComplianceStandard
    {
        NIST80053,
        ISO27001,
        SOX,
        HIPAA,
        FedRAMP
    }
    
    // ========== CORE REQUEST MODELS ==========
    
    /// <summary>
    /// Request to create a new Azure environment
    /// </summary>
    public class EnvironmentCreationRequest
    {
        public string Name { get; set; } = string.Empty;
        public EnvironmentType Type { get; set; }
        public string ResourceGroup { get; set; } = string.Empty;
        public string Location { get; set; } = "eastus";
        public string? SubscriptionId { get; set; }
        public int? NodeCount { get; set; }
        public ScaleSettings? ScaleSettings { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
        public bool EnableMonitoring { get; set; } = true;
        public bool EnableLogging { get; set; } = true;
        public ComplianceSettings? ComplianceSettings { get; set; }
        public string? Sku { get; set; }
        
        // Service Template Support
        public string? TemplateId { get; set; }
        public string? TemplateName { get; set; }
        public string? TemplateContent { get; set; }
        public Dictionary<string, string>? TemplateParameters { get; set; }
        public List<ServiceTemplateFile>? TemplateFiles { get; set; }
    }
    
    /// <summary>
    /// Request to clone an existing environment
    /// </summary>
    public class EnvironmentCloneRequest
    {
        public string SourceEnvironment { get; set; } = string.Empty;
        public string SourceResourceGroup { get; set; } = string.Empty;
        public List<string> TargetEnvironments { get; set; } = new();
        public string TargetResourceGroup { get; set; } = string.Empty;
        public string TargetLocation { get; set; } = string.Empty;
        public bool PreserveData { get; set; } = false;
        public bool IncludePipelines { get; set; } = true;
        public bool CloneConfiguration { get; set; } = true;
        public Dictionary<string, string>? ConfigurationOverrides { get; set; }
    }
    
    /// <summary>
    /// Request for environment migration
    /// </summary>
    public class MigrationRequest
    {
        public string SourceEnvironment { get; set; } = string.Empty;
        public string SourceResourceGroup { get; set; } = string.Empty;
        public string TargetEnvironment { get; set; } = string.Empty;
        public string TargetResourceGroup { get; set; } = string.Empty;
        public string TargetLocation { get; set; } = string.Empty;
        public string? TargetSubscriptionId { get; set; }
        public bool MigrateData { get; set; } = true;
        public bool ValidateBeforeMigration { get; set; } = true;
    }
    
    /// <summary>
    /// Request for template-based deployment
    /// </summary>
    public class TemplateDeploymentRequest
    {
        public string TemplateId { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public string Location { get; set; } = "eastus";
        public Dictionary<string, object> Parameters { get; set; } = new();
        public Dictionary<string, string> Tags { get; set; } = new();
    }
    
    /// <summary>
    /// Request for multi-region deployment
    /// </summary>
    public class MultiRegionRequest
    {
        public string EnvironmentBaseName { get; set; } = string.Empty;
        public List<string> TargetRegions { get; set; } = new();
        public EnvironmentType Type { get; set; }
        public bool EnableTrafficManager { get; set; } = true;
        public bool EnableGeoReplication { get; set; } = false;
        public Dictionary<string, string> Tags { get; set; } = new();
    }
    
    /// <summary>
    /// Request for environment metrics
    /// </summary>
    public class MetricsRequest
    {
        public string TimeRange { get; set; } = "1h";
        public List<string> MetricTypes { get; set; } = new() { "cpu", "memory", "requests" };
        public string? Aggregation { get; set; }
    }
    
    // ========== CORE RESULT MODELS ==========
    
    /// <summary>
    /// Result of environment creation operation
    /// </summary>
    public class EnvironmentCreationResult
    {
        public bool Success { get; set; }
        public string EnvironmentId { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public EnvironmentType Type { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Endpoint { get; set; } = string.Empty;
        public Dictionary<string, string> Configuration { get; set; } = new();
        public List<string> CreatedResources { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public string? DeploymentId { get; set; } // For async deployment tracking
        public string? Message { get; set; } // User-friendly status message
    }
    
    /// <summary>
    /// Result of environment cloning operation
    /// </summary>
    public class EnvironmentCloneResult
    {
        public bool Success { get; set; }
        public string SourceEnvironment { get; set; } = string.Empty;
        public List<ClonedEnvironment> ClonedEnvironments { get; set; } = new();
        public int TotalCloned { get; set; }
        public int TotalFailed { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> Warnings { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Individual cloned environment details
    /// </summary>
    public class ClonedEnvironment
    {
        public string Name { get; set; } = string.Empty;
        public string ResourceId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool DataCloned { get; set; }
        public bool PipelinesCloned { get; set; }
    }
    
    /// <summary>
    /// Result of environment deletion
    /// </summary>
    public class EnvironmentDeletionResult
    {
        public bool Success { get; set; }
        public string EnvironmentName { get; set; } = string.Empty;
        public string ResourceGroup { get; set; } = string.Empty;
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
        public bool BackupCreated { get; set; }
        public string? BackupLocation { get; set; }
        public List<string> DeletedResources { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Result of bulk operation
    /// </summary>
    public class BulkOperationResult
    {
        public bool Success { get; set; }
        public int TotalProcessed { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> SuccessfulOperations { get; set; } = new();
        public List<string> FailedOperations { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }
    
    /// <summary>
    /// Result of scaling operation
    /// </summary>
    public class ScalingResult
    {
        public bool Success { get; set; }
        public string EnvironmentName { get; set; } = string.Empty;
        public int PreviousReplicas { get; set; }
        public int NewReplicas { get; set; }
        public ScalingAction Action { get; set; }
        public DateTime ScaledAt { get; set; } = DateTime.UtcNow;
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Result of migration operation
    /// </summary>
    public class MigrationResult
    {
        public bool Success { get; set; }
        public string SourceEnvironment { get; set; } = string.Empty;
        public string TargetEnvironment { get; set; } = string.Empty;
        public string TargetLocation { get; set; } = string.Empty;
        public bool DataMigrated { get; set; }
        public TimeSpan Duration { get; set; }
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Result of template deployment
    /// </summary>
    public class TemplateDeploymentResult
    {
        public bool Success { get; set; }
        public string TemplateId { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public List<string> DeployedResources { get; set; } = new();
        public DeploymentStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Result of compliance setup
    /// </summary>
    public class ComplianceSetupResult
    {
        public bool Success { get; set; }
        public string EnvironmentName { get; set; } = string.Empty;
        public ComplianceStandard Standard { get; set; }
        public List<string> AppliedControls { get; set; } = new();
        public int TotalControls { get; set; }
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Result of multi-region deployment
    /// </summary>
    public class MultiRegionDeploymentResult
    {
        public bool Success { get; set; }
        public string EnvironmentBaseName { get; set; } = string.Empty;
        public List<RegionalDeployment> RegionalDeployments { get; set; } = new();
        public bool TrafficManagerConfigured { get; set; }
        public string? ErrorMessage { get; set; }
    }
    
    /// <summary>
    /// Regional deployment details
    /// </summary>
    public class RegionalDeployment
    {
        public string Region { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Endpoint { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Result of cleanup operation
    /// </summary>
    public class CleanupResult
    {
        public bool Success { get; set; }
        public int EnvironmentsAnalyzed { get; set; }
        public int EnvironmentsDeleted { get; set; }
        public List<string> DeletedEnvironments { get; set; } = new();
        public decimal EstimatedMonthlySavings { get; set; }
    }
    
    // ========== CONFIGURATION MODELS ==========
    
    /// <summary>
    /// Scaling configuration for environment
    /// </summary>
    public class ScaleSettings
    {
        public int? MinReplicas { get; set; }
        public int? MaxReplicas { get; set; }
        public int? TargetReplicas { get; set; }
        public bool AutoScaling { get; set; } = false;
        public int? TargetCpuUtilization { get; set; }
        public int? TargetMemoryUtilization { get; set; }
        public bool CostOptimization { get; set; } = false;
        public bool TrafficBasedScaling { get; set; } = false;
    }
    
    /// <summary>
    /// Compliance settings for environment
    /// </summary>
    public class ComplianceSettings
    {
        public ComplianceStandard Standard { get; set; }
        public bool GovernmentCloud { get; set; } = false;
        public bool EncryptionAtRest { get; set; } = true;
        public bool EncryptionInTransit { get; set; } = true;
        public bool AuditLogging { get; set; } = true;
        public bool SecurityMonitoring { get; set; } = true;
    }
    
    /// <summary>
    /// Filter for environment queries
    /// </summary>
    public class EnvironmentFilter
    {
        public List<string>? Locations { get; set; }
        public List<string>? ResourceGroups { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
        public int? AgeInDays { get; set; }
        public string? Status { get; set; }
        public EnvironmentType? Type { get; set; }
        public string? SubscriptionId { get; set; }
    }
    
    /// <summary>
    /// Bulk operation safety settings
    /// </summary>
    public class BulkOperationSettings
    {
        public bool SafetyChecks { get; set; } = true;
        public bool CreateBackup { get; set; } = true;
        public bool ConfirmationRequired { get; set; } = true;
        public int MaxResources { get; set; } = 10;
    }
    
    /// <summary>
    /// Cleanup policy
    /// </summary>
    public class CleanupPolicy
    {
        public int MinimumAgeInDays { get; set; } = 30;
        public bool OnlyUnusedEnvironments { get; set; } = true;
        public bool CreateBackup { get; set; } = true;
        public List<string>? ExcludedResourceGroups { get; set; }
        public List<string>? ExcludedTags { get; set; }
    }
    
    /// <summary>
    /// Service template file for multi-file templates
    /// </summary>
    public class ServiceTemplateFile
    {
        public string FileName { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public bool IsEntryPoint { get; set; } = false;
        public int Order { get; set; } = 0;
    }
    
    /// <summary>
    /// Service template definition for infrastructure deployment
    /// </summary>
    public class ServiceTemplate
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TemplateType { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty; // Bicep, ARM, Terraform
        public string DeploymentTier { get; set; } = string.Empty;
        public bool MultiRegionSupported { get; set; }
        public bool DisasterRecoverySupported { get; set; }
        public bool HighAvailabilitySupported { get; set; }
        public Dictionary<string, string>? Parameters { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
        public string? AzureService { get; set; }
        public bool AutoScalingEnabled { get; set; }
        public bool MonitoringEnabled { get; set; }
        public bool BackupEnabled { get; set; }
        public int FilesCount { get; set; }
        public string? MainFileType { get; set; }
        public List<ServiceTemplateFile> Files { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
