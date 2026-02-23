namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Outcome of an audited action (FR-066).
/// </summary>
public enum AuditOutcome
{
    Success,
    Failure,
    Denied,
    Cancelled
}
