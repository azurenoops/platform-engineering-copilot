namespace Platform.Engineering.Copilot.Admin.Client.Models;

/// <summary>Platform-wide environment summary for the dashboard.</summary>
public class EnvironmentSummaryDto
{
    public int TotalCount { get; set; }
    public int HealthyCount { get; set; }
    public int DegradedCount { get; set; }
    public int UnhealthyCount { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public int DriftCount { get; set; }
    public int ExpiringWithin7Days { get; set; }
    public decimal TotalEstimatedMonthlyCost { get; set; }
    public List<TemplateCountDto> ByTemplate { get; set; } = new();
}

/// <summary>Environment count per template.</summary>
public class TemplateCountDto
{
    public string TemplateName { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>Full environment details.</summary>
public class EnvironmentDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? Description { get; set; }
    public Guid TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StatusMessage { get; set; }
    public string? DeploymentId { get; set; }
    public string? ParameterValuesJson { get; set; }
    public string? DeployedResourcesJson { get; set; }
    public string? TagsJson { get; set; }
    public bool HasDrift { get; set; }
    public int DriftCount { get; set; }
    public decimal? EstimatedMonthlyCost { get; set; }
    public string? OwnerEmail { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public bool AutoDelete { get; set; }
    public string? DeploymentScope { get; set; }
    public string? RequestedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Deployed Azure resource.</summary>
public class ResourceDto
{
    public Guid Id { get; set; }
    public string AzureResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Sku { get; set; }
    public string? ProvisioningState { get; set; }
    public DateTimeOffset? DeployedAt { get; set; }
    public string? PortalUrl { get; set; }
    public string? ResourceGroupName { get; set; }
}

/// <summary>Result of scaling an environment.</summary>
public class ScaleResultDto
{
    public Guid EnvironmentId { get; set; }
    public string? PreviousScale { get; set; }
    public string? NewScale { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
}

/// <summary>Result of deleting Azure resources for an environment.</summary>
public class DeleteResourcesResultDto
{
    public int DeletedCount { get; set; }
    public int FailedCount { get; set; }
    public List<ResourceFailureDto> Failures { get; set; } = new();
}

/// <summary>Details of a failed resource operation.</summary>
public class ResourceFailureDto
{
    public string ResourceId { get; set; } = string.Empty;
    public string? ResourceName { get; set; }
    public string Error { get; set; } = string.Empty;
}

/// <summary>Result of refreshing deployment status from Azure.</summary>
public class RefreshDeploymentStatusResultDto
{
    public Guid EnvironmentId { get; set; }
    public string? PreviousStatus { get; set; }
    public string CurrentStatus { get; set; } = string.Empty;
    public int ResourceCount { get; set; }
}
