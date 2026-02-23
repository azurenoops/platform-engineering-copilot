namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Categories of compliance drift that trigger alerts (FR-058–FR-061).
/// </summary>
public enum DriftCategory
{
    /// <summary>Resource configuration drifted from established baseline.</summary>
    BaselineDrift,
    /// <summary>Azure Policy assignment changed in a way that reduces compliance.</summary>
    PolicyDrift,
    /// <summary>Overall compliance state degraded (e.g., new failures).</summary>
    ComplianceStateDrift,
    /// <summary>Microsoft Defender Secure Score decreased.</summary>
    SecureScoreDrop
}
