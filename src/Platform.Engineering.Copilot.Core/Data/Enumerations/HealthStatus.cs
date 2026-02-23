namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Agent health status reported by /health endpoint (FR-075).
/// </summary>
public enum HealthStatus
{
    Healthy,
    Degraded,
    Unavailable
}
