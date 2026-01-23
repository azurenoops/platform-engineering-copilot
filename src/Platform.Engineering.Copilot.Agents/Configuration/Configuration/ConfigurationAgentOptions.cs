using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Agents.Configuration.Configuration;

/// <summary>
/// Configuration options for the Configuration Agent.
/// </summary>
public class ConfigurationAgentOptions
{
    /// <summary>
    /// Configuration section name matching the pattern used by other agents.
    /// </summary>
    public const string SectionName = "AgentConfiguration:ConfigurationAgent";

    /// <summary>
    /// Whether the agent is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Temperature for LLM responses (0.0 = deterministic, 1.0 = creative).
    /// Configuration agent uses low temperature for consistent responses.
    /// </summary>
    [Range(0.0, 2.0)]
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    /// Maximum tokens for LLM responses.
    /// Configuration responses are typically short.
    /// </summary>
    [Range(100, 128000)]
    public int MaxTokens { get; set; } = 1000;
}
