namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Severity levels for compliance findings and alerts.
/// SLA mapping (FR-052): Critical 24h, High 7d, Medium 30d, Low 90d.
/// </summary>
public enum Severity
{
    Critical,
    High,
    Medium,
    Low,
    /// <summary>Informational — risk weight = 0 (excluded from risk score formula FR-025).</summary>
    Informational
}
