namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Types of attachments that can accompany chat messages.
/// </summary>
public enum MessageAttachmentType
{
    /// <summary>Assessment result summary.</summary>
    AssessmentResult,
    /// <summary>Generated compliance document.</summary>
    ComplianceDocument,
    /// <summary>IaC template (Bicep/Terraform).</summary>
    IaCTemplate,
    /// <summary>Evidence package reference.</summary>
    EvidencePackage,
    /// <summary>Remediation board link.</summary>
    RemediationBoard,
    /// <summary>Cost analysis report.</summary>
    CostReport,
    /// <summary>Resource inventory snapshot.</summary>
    ResourceInventory
}
