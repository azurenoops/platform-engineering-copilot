using System.ComponentModel.DataAnnotations;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// A detected configuration drift item between expected and actual resource state.
/// </summary>
public class DriftItem
{
    [Key]
    public Guid Id { get; set; }

    public Guid EnvironmentId { get; set; }

    [Required]
    [MaxLength(500)]
    public string ResourceId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ResourceName { get; set; }

    [MaxLength(200)]
    public string? ResourceType { get; set; }

    /// <summary>e.g., "properties.storageProfile.osDisk.diskSizeGB".</summary>
    [Required]
    [MaxLength(500)]
    public string PropertyPath { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? ExpectedValue { get; set; }

    [MaxLength(1000)]
    public string? ActualValue { get; set; }

    /// <summary>e.g., "PropertyChanged", "ResourceAdded", "ResourceRemoved".</summary>
    [MaxLength(100)]
    public string? DriftType { get; set; }

    public DriftSeverity Severity { get; set; } = DriftSeverity.Medium;

    public bool CanAutoRemediate { get; set; }

    public bool IsRemediated { get; set; }

    [Required]
    public DateTimeOffset DetectedAt { get; set; }

    public DateTimeOffset? RemediatedAt { get; set; }

    // Navigation
    public ProvisionedEnvironment Environment { get; set; } = null!;
}
