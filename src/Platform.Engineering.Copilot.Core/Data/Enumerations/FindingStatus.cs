namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Status of a compliance finding against a specific control.
/// </summary>
public enum FindingStatus
{
    Failing,
    Passing,
    NotApplicable,
    Error
}
