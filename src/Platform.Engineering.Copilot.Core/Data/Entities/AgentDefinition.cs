using System.ComponentModel.DataAnnotations;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Runtime configuration for a specialized agent.
/// </summary>
public class AgentDefinition
{
    /// <summary>e.g. "compliance", "infrastructure".</summary>
    [Key]
    [MaxLength(50)]
    public string AgentId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string AgentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Path to .prompt.txt file.</summary>
    [Required]
    [MaxLength(500)]
    public string SystemPromptPath { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    public HealthStatus HealthStatus { get; set; } = HealthStatus.Healthy;

    public DateTimeOffset? LastHealthCheck { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation
    public ICollection<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();
}
