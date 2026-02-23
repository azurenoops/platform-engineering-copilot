using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Per-user platform settings. One Configuration per User (1:1).
/// See configuration-tools.md for sub-action contract.
/// </summary>
public class Configuration
{
    [Key]
    public Guid ConfigurationId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    /// <summary>Default Azure subscription GUID.</summary>
    [MaxLength(36)]
    public string? DefaultSubscriptionId { get; set; }

    public CloudEnvironment CloudEnvironment { get; set; } = CloudEnvironment.AzureUSGovernment;

    public ComplianceFramework DefaultFramework { get; set; } = ComplianceFramework.Nist80053Rev5;

    public BaselineLevel Baseline { get; set; } = BaselineLevel.High;

    public ScanType DefaultScanType { get; set; } = ScanType.Combined;

    [MaxLength(50)]
    public string DefaultRegion { get; set; } = "usgovvirginia";

    /// <summary>Default to dry-run for remediations.</summary>
    public bool DryRunDefault { get; set; } = true;

    /// <summary>Cached PIM eligibility as JSON: { "read": true, "write": false }.</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? PimRoleEligibility { get; set; }

    /// <summary>Mapping between CAC cert and Azure AD identity.</summary>
    [MaxLength(500)]
    public string? CacCertificateMapping { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
