namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Operational status of a provisioned environment.
/// </summary>
public enum EnvironmentStatus
{
    Provisioning = 0,
    Running = 1,
    Failed = 2,
    Updating = 3,
    Scaling = 4,
    Deleting = 5,
    Deleted = 6,
    Suspended = 7
}
