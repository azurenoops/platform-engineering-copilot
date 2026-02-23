namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Supported compliance frameworks for assessments and control mappings.
/// </summary>
public enum ComplianceFramework
{
    /// <summary>NIST SP 800-53 Revision 5 — full catalog.</summary>
    Nist80053Rev5,
    /// <summary>FedRAMP High baseline overlay.</summary>
    FedRampHigh,
    /// <summary>FedRAMP Moderate baseline overlay.</summary>
    FedRampModerate,
    /// <summary>DoD Impact Level 5 overlay.</summary>
    DoDIL5
}
