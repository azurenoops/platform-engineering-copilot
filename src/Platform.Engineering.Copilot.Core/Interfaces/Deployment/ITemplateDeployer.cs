using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Core.Interfaces.Deployment;

/// <summary>
/// Interface for deploying infrastructure templates (Bicep, ARM, Terraform)
/// </summary>
public interface ITemplateDeployer
{
    /// <summary>
    /// The template format this deployer handles
    /// </summary>
    string Format { get; }

    /// <summary>
    /// Deploy the template to Azure
    /// </summary>
    Task<TemplateDeploymentResult> DeployAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the status of a running deployment
    /// </summary>
    Task<DeploymentStatusResult> GetDeploymentStatusAsync(
        string subscriptionId,
        string deploymentName,
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate the template before deployment
    /// </summary>
    Task<ValidationResult> ValidateAsync(
        string templateContent,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if this deployer can handle the given format
    /// </summary>
    bool CanHandle(string format);
}

/// <summary>
/// Request to deploy a template
/// </summary>
public class DeploymentRequest
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateContent { get; set; } = string.Empty;
    public string Format { get; set; } = "Bicep";
    public string EnvironmentName { get; set; } = string.Empty;
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Location { get; set; } = "eastus";
    public Dictionary<string, object> Parameters { get; set; } = new();
    public Dictionary<string, string> Tags { get; set; } = new();
    public string DeployedBy { get; set; } = "system";
    
    /// <summary>
    /// Additional files required for deployment (e.g., Bicep modules).
    /// Key = relative path (e.g., "modules/security.bicep"), Value = file content.
    /// </summary>
    public Dictionary<string, string> AdditionalFiles { get; set; } = new();
    
    /// <summary>
    /// For Terraform: backend configuration (state storage)
    /// </summary>
    public TerraformBackendConfig? TerraformBackend { get; set; }
    
    /// <summary>
    /// Whether to run in dry-run/what-if mode
    /// </summary>
    public bool WhatIf { get; set; }
}

/// <summary>
/// Result of a template deployment operation
/// </summary>
public class TemplateDeploymentResult
{
    public bool Success { get; set; }
    public string DeploymentId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? CorrelationId { get; set; }
    public List<DeployedResourceInfo> Resources { get; set; } = new();
    public List<string> Outputs { get; set; } = new();
    public Dictionary<string, object> OutputValues { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public TimeSpan Duration { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? RawOutput { get; set; }
}

/// <summary>
/// Information about a deployed resource
/// </summary>
public class DeployedResourceInfo
{
    public string ResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ProvisioningState { get; set; } = string.Empty;
}

/// <summary>
/// Validation result for a template
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of checking deployment status from Azure
/// </summary>
public class DeploymentStatusResult
{
    public string DeploymentName { get; set; } = string.Empty;
    public string ProvisioningState { get; set; } = string.Empty;  // Running, Succeeded, Failed, Canceled
    public string? CorrelationId { get; set; }
    public DateTime? Timestamp { get; set; }
    public TimeSpan? Duration { get; set; }
    public List<DeployedResourceInfo> Resources { get; set; } = new();
    public Dictionary<string, object> Outputs { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public List<string> Errors { get; set; } = new();
    public bool IsComplete => ProvisioningState is "Succeeded" or "Failed" or "Canceled";
    public bool IsSuccessful => ProvisioningState == "Succeeded";
}

/// <summary>
/// Terraform backend configuration for state storage
/// </summary>
public class TerraformBackendConfig
{
    public string Type { get; set; } = "azurerm"; // azurerm, s3, gcs, etc.
    public string StorageAccountName { get; set; } = string.Empty;
    public string ContainerName { get; set; } = "tfstate";
    public string Key { get; set; } = string.Empty; // state file name
    public string ResourceGroupName { get; set; } = string.Empty;
}
