using System;

namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// Represents the progress of an ATO compliance assessment
/// </summary>
public class AssessmentProgress
{
    /// <summary>
    /// Total number of control families to assess
    /// </summary>
    public int TotalFamilies { get; set; }

    /// <summary>
    /// Number of control families completed
    /// </summary>
    public int CompletedFamilies { get; set; }

    /// <summary>
    /// The control family currently being assessed
    /// </summary>
    public string CurrentFamily { get; set; } = string.Empty;

    /// <summary>
    /// Percentage of assessment completed (0-100)
    /// </summary>
    public double PercentComplete => TotalFamilies > 0 
        ? Math.Round((double)CompletedFamilies / TotalFamilies * 100, 2) 
        : 0;

    /// <summary>
    /// Estimated time remaining in milliseconds (optional)
    /// </summary>
    public long? EstimatedTimeRemainingMs { get; set; }

    /// <summary>
    /// Timestamp when this progress update was created
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Additional message about the current operation
    /// </summary>
    public string? Message { get; set; }
}
