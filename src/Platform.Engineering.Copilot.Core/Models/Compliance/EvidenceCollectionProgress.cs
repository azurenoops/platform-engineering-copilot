using System;

namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// Represents the progress of evidence collection for a control family
/// </summary>
public class EvidenceCollectionProgress
{
    /// <summary>
    /// The control family for which evidence is being collected
    /// </summary>
    public string ControlFamily { get; set; } = string.Empty;

    /// <summary>
    /// Total number of evidence items to collect
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Number of evidence items collected so far
    /// </summary>
    public int CollectedItems { get; set; }

    /// <summary>
    /// Type of evidence currently being collected
    /// </summary>
    public string CurrentEvidenceType { get; set; } = string.Empty;

    /// <summary>
    /// Percentage of evidence collection completed (0-100)
    /// </summary>
    public double PercentComplete => TotalItems > 0 
        ? Math.Round((double)CollectedItems / TotalItems * 100, 2) 
        : 0;

    /// <summary>
    /// Timestamp when this progress update was created
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional message about the current operation
    /// </summary>
    public string? Message { get; set; }
}
