namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Lifecycle state of a compliance drift alert (FR-059–FR-061).
/// Transitions: New → Acknowledged → InProgress → Resolved | Dismissed.
/// </summary>
public enum AlertState
{
    New,
    Acknowledged,
    InProgress,
    Resolved,
    Dismissed
}
