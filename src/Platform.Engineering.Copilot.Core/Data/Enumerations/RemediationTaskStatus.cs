namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Kanban workflow status for remediation tasks (FR-051–FR-053).
/// Transitions: Backlog → ToDo → InProgress → InReview → Done | Blocked (from any except Done).
/// </summary>
public enum RemediationTaskStatus
{
    Backlog,
    ToDo,
    InProgress,
    InReview,
    Blocked,
    Done
}
