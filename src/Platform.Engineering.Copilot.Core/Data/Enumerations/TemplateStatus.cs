namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Lifecycle status of a service template in the approval workflow.
/// </summary>
public enum TemplateStatus
{
    Draft = 0,
    PendingApproval = 1,
    Published = 2,
    Deprecated = 3
}
