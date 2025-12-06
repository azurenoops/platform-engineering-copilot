using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Platform.Engineering.Copilot.Core.Data.Entities;

/// <summary>
/// Represents an agent configuration stored in the database
/// </summary>
[Table("AgentConfigurations")]
public class AgentConfiguration
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int AgentConfigurationId { get; set; }

    /// <summary>
    /// Internal name of the agent (e.g., "InfrastructureAgent", "ComplianceAgent")
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the UI (e.g., "Infrastructure Agent")
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the agent does
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// Whether the agent is currently enabled
    /// </summary>
    [Required]
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Category for grouping agents (e.g., "Core", "Compliance", "Cost", "Discovery")
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Icon name for UI display (e.g., "🏗️", "✓", "💰")
    /// </summary>
    [MaxLength(50)]
    public string? IconName { get; set; }

    /// <summary>
    /// Agent-specific configuration as JSON blob
    /// </summary>
    public string? ConfigurationJson { get; set; }

    /// <summary>
    /// Order for display in the UI
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// When the agent configuration was created
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the agent configuration was last updated
    /// </summary>
    [Required]
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// User who last modified the configuration
    /// </summary>
    [MaxLength(200)]
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Optional comma-separated list of agent names this agent depends on
    /// </summary>
    [MaxLength(500)]
    public string? Dependencies { get; set; }

    /// <summary>
    /// Last time the agent was executed (if tracked)
    /// </summary>
    public DateTime? LastExecutedAt { get; set; }

    /// <summary>
    /// Health status of the agent (e.g., "Healthy", "Unhealthy", "Unknown")
    /// </summary>
    [MaxLength(50)]
    public string? HealthStatus { get; set; }
}
