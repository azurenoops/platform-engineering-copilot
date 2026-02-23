using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Metadata for tools registered to agents.
/// </summary>
public class ToolDefinition
{
    /// <summary>e.g. "run_compliance_assessment".</summary>
    [Key]
    [MaxLength(100)]
    public string ToolId { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string AgentId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>JSON Schema for parameters.</summary>
    [Required]
    [Column(TypeName = "nvarchar(max)")]
    public string ParameterSchema { get; set; } = "{}";

    /// <summary>Whether the tool requires authentication (FR-010).</summary>
    [Required]
    public bool RequiresAuthentication { get; set; }

    /// <summary>Minimum PIM tier required to invoke this tool.</summary>
    [Required]
    public PimTier PimTierRequired { get; set; } = PimTier.None;

    public bool IsEnabled { get; set; } = true;

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    [ForeignKey(nameof(AgentId))]
    public AgentDefinition Agent { get; set; } = null!;
}
