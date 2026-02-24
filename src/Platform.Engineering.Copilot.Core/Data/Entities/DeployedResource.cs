using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// An Azure resource deployed as part of a provisioned environment.
/// </summary>
public class DeployedResource
{
    [Key]
    public Guid Id { get; set; }

    public Guid EnvironmentId { get; set; }

    [Required]
    [MaxLength(500)]
    public string AzureResourceId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>e.g., "Microsoft.ContainerService/managedClusters".</summary>
    [Required]
    [MaxLength(200)]
    public string Type { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Location { get; set; }

    [MaxLength(100)]
    public string? Sku { get; set; }

    [MaxLength(50)]
    public string? ProvisioningState { get; set; }

    public DateTimeOffset? DeployedAt { get; set; }

    /// <summary>Computed portal.azure.us URL.</summary>
    [MaxLength(500)]
    public string? PortalUrl { get; set; }

    /// <summary>Extracted from AzureResourceId.</summary>
    [MaxLength(200)]
    public string? ResourceGroupName { get; set; }

    // Navigation
    public ProvisionedEnvironment Environment { get; set; } = null!;
}
