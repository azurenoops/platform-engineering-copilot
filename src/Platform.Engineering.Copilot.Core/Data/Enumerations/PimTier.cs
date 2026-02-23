namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// PIM (Privileged Identity Management) elevation tiers.
/// Determines the level of access granted after PIM activation (FR-012).
/// </summary>
public enum PimTier
{
    /// <summary>No PIM elevation — Knowledge Base tools only.</summary>
    None,
    /// <summary>Read-only PIM elevation — assessment, discovery, cost queries.</summary>
    Read,
    /// <summary>Write PIM elevation — remediation, infrastructure changes.</summary>
    Write
}
