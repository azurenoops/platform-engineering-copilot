namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Compliance assessment scan strategies.
/// </summary>
public enum ScanType
{
    /// <summary>Scan individual Azure resources against controls.</summary>
    ResourceBased,
    /// <summary>Evaluate Azure Policy assignment compliance.</summary>
    PolicyBased,
    /// <summary>Both resource-based and policy-based scanning (FR-021).</summary>
    Combined
}
