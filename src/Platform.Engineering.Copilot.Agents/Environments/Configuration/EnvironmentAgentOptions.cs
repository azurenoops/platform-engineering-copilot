using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Agents.Environments.Configuration;

/// <summary>
/// Configuration options for the Environment Agent.
/// </summary>
public class EnvironmentAgentOptions
{
    public const string SectionName = "AgentConfiguration:EnvironmentAgent";

    /// <summary>
    /// Whether this agent is enabled. When false, the agent will not be registered.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Temperature setting for AI model (0.0-1.0). Lower = more deterministic.
    /// Environment operations typically prefer precise, deterministic responses.
    /// </summary>
    [Range(0.0, 1.0)]
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Maximum tokens for AI model responses.
    /// </summary>
    [Range(100, 128000)]
    public int MaxTokens { get; set; } = 4000;

    /// <summary>
    /// Enable drift detection for deployed environments.
    /// </summary>
    public bool EnableDriftDetection { get; set; } = true;

    /// <summary>
    /// Enable health monitoring for environments.
    /// </summary>
    public bool EnableHealthMonitoring { get; set; } = true;

    /// <summary>
    /// Default Azure subscription ID to use when not specified.
    /// </summary>
    public string? DefaultSubscriptionId { get; set; }

    /// <summary>
    /// Default scaling factor for environment scaling operations.
    /// </summary>
    [Range(0.1, 10.0)]
    public double DefaultScalingFactor { get; set; } = 1.0;

    /// <summary>
    /// Environment management settings.
    /// </summary>
    public EnvironmentManagementOptions Management { get; set; } = new();

    /// <summary>
    /// Deployment strategy settings.
    /// </summary>
    public DeploymentStrategyOptions DeploymentStrategies { get; set; } = new();
}

/// <summary>
/// Environment management-specific options.
/// </summary>
public class EnvironmentManagementOptions
{
    /// <summary>
    /// Naming convention pattern for environments.
    /// Supports: {app}, {env}, {region}, {seq}
    /// </summary>
    public string NamingConvention { get; set; } = "{app}-{env}-{region}";

    /// <summary>
    /// Default tags to apply to all environments.
    /// </summary>
    public Dictionary<string, string> DefaultTags { get; set; } = new()
    {
        ["ManagedBy"] = "PlatformEngineeringCopilot",
        ["CreatedBy"] = "EnvironmentAgent"
    };

    /// <summary>
    /// Whether to clone data by default during environment cloning.
    /// </summary>
    public bool CloneDataByDefault { get; set; } = false;

    /// <summary>
    /// Hours to preserve blue environment during blue-green deployments.
    /// </summary>
    public int PreserveBlueEnvironmentHours { get; set; } = 24;
}

/// <summary>
/// Deployment strategy configuration options.
/// </summary>
public class DeploymentStrategyOptions
{
    /// <summary>
    /// Canary deployment settings.
    /// </summary>
    public CanaryDeploymentOptions Canary { get; set; } = new();

    /// <summary>
    /// Blue-green deployment settings.
    /// </summary>
    public BlueGreenDeploymentOptions BlueGreen { get; set; } = new();

    /// <summary>
    /// Rolling update settings.
    /// </summary>
    public RollingUpdateOptions RollingUpdate { get; set; } = new();
}

/// <summary>
/// Canary deployment configuration.
/// </summary>
public class CanaryDeploymentOptions
{
    /// <summary>
    /// Traffic percentages for each phase.
    /// </summary>
    public int[] Phases { get; set; } = [10, 25, 50, 100];

    /// <summary>
    /// Duration of each phase in minutes.
    /// </summary>
    public int PhaseDurationMinutes { get; set; } = 30;

    /// <summary>
    /// Error rate threshold to trigger rollback.
    /// </summary>
    public double ErrorRateThreshold { get; set; } = 1.0;

    /// <summary>
    /// Latency threshold in milliseconds to trigger rollback.
    /// </summary>
    public int LatencyThresholdMs { get; set; } = 500;
}

/// <summary>
/// Blue-green deployment configuration.
/// </summary>
public class BlueGreenDeploymentOptions
{
    /// <summary>
    /// Duration for smoke tests before full cutover.
    /// </summary>
    public int SmokeTestDurationMinutes { get; set; } = 10;

    /// <summary>
    /// Enable automatic cleanup of inactive environment.
    /// </summary>
    public bool AutoCleanup { get; set; } = true;
}

/// <summary>
/// Rolling update configuration.
/// </summary>
public class RollingUpdateOptions
{
    /// <summary>
    /// Maximum number of instances that can be unavailable during update.
    /// </summary>
    public int MaxUnavailable { get; set; } = 1;

    /// <summary>
    /// Maximum number of additional instances during update.
    /// </summary>
    public int MaxSurge { get; set; } = 1;
}
