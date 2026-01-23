namespace Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;

/// <summary>
/// Activity record for an environment - used in API responses and UI
/// </summary>
public class EnvironmentActivity
{
    public Guid Id { get; set; }
    public Guid EnvironmentId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = "Completed";
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Request to add a new activity record
/// </summary>
public class AddEnvironmentActivityRequest
{
    public Guid EnvironmentId { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
    public string Status { get; set; } = "Completed";
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Paged result for activity history
/// </summary>
public class EnvironmentActivityPagedResult
{
    public IReadOnlyList<EnvironmentActivity> Items { get; set; } = Array.Empty<EnvironmentActivity>();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
    public bool HasMore => Skip + Items.Count < TotalCount;
}
