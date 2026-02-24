namespace Platform.Engineering.Copilot.Admin.Client.Models;

/// <summary>Environment activity log entry.</summary>
public class ActivityDto
{
    public Guid Id { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string? MetadataJson { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Status { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Paginated activity list.</summary>
public class ActivityListDto
{
    public List<ActivityDto> Activities { get; set; } = new();
    public bool HasMore { get; set; }
}
