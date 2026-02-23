namespace Platform.Engineering.Copilot.Core.Data.Enumerations;

/// <summary>
/// Actions available on the compliance monitoring tool (lightweight on-demand, not US10).
/// </summary>
public enum MonitoringAction
{
    /// <summary>Show current compliance status summary.</summary>
    Status,
    /// <summary>Trigger a new on-demand compliance scan.</summary>
    Scan,
    /// <summary>List active compliance drift alerts.</summary>
    Alerts,
    /// <summary>Show compliance trend over time.</summary>
    Trend
}
