using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Governance.Models;

/// <summary>
/// Represents an ATO (Authority to Operate) rule
/// </summary>
public class AtoRule
{
    [JsonPropertyName("ruleId")]
    public required string RuleId { get; set; }

    [JsonPropertyName("control")]
    public required string Control { get; set; }

    [JsonPropertyName("description")]
    public required string Description { get; set; }

    [JsonPropertyName("match")]
    public required AtoRuleMatch Match { get; set; }

    [JsonPropertyName("action")]
    public required string Action { get; set; }
}

/// <summary>
/// Represents the matching criteria for an ATO rule
/// </summary>
public class AtoRuleMatch
{
    [JsonPropertyName("toolName")]
    public string? ToolName { get; set; }

    [JsonPropertyName("operation")]
    public string? Operation { get; set; }

    [JsonPropertyName("args")]
    public Dictionary<string, object>? Args { get; set; }
}

/// <summary>
/// Represents a pending approval request
/// </summary>
public class ApprovalRequest
{
    public required string Id { get; set; }
    public required string ToolName { get; set; }
    public required Dictionary<string, object?> Arguments { get; set; }
    public required string Reason { get; set; }
    public required DateTime RequestedAt { get; set; }
    public required DateTime ExpiresAt { get; set; }
    public string? RequestedBy { get; set; }
    public bool IsApproved { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// Teams webhook notification payload
/// </summary>
public class TeamsNotification
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "MessageCard";

    [JsonPropertyName("@context")]
    public string Context { get; set; } = "http://schema.org/extensions";

    [JsonPropertyName("themeColor")]
    public string ThemeColor { get; set; } = "0076D7";

    [JsonPropertyName("summary")]
    public required string Summary { get; set; }

    [JsonPropertyName("sections")]
    public required List<TeamsSection> Sections { get; set; }

    [JsonPropertyName("potentialAction")]
    public List<TeamsAction>? PotentialActions { get; set; }
}

public class TeamsSection
{
    [JsonPropertyName("activityTitle")]
    public string? ActivityTitle { get; set; }

    [JsonPropertyName("activitySubtitle")]
    public string? ActivitySubtitle { get; set; }

    [JsonPropertyName("facts")]
    public List<TeamsFact>? Facts { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public class TeamsFact
{
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }
}

public class TeamsAction
{
    [JsonPropertyName("@type")]
    public string Type { get; set; } = "OpenUri";

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("targets")]
    public required List<TeamsTarget> Targets { get; set; }
}

public class TeamsTarget
{
    [JsonPropertyName("os")]
    public string Os { get; set; } = "default";

    [JsonPropertyName("uri")]
    public required string Uri { get; set; }
}