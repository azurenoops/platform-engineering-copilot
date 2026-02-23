namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Lifecycle status of a compliance assessment.
/// Transitions: Running → Completed | Failed | Cancelled
/// </summary>
public enum AssessmentStatus
{
    Running,
    Completed,
    Failed,
    Cancelled
}
