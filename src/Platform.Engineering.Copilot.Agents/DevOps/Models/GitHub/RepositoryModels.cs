namespace Platform.Engineering.Copilot.Agents.DevOps.Models.GitHub;

/// <summary>
/// Parameters for creating a GitHub repository
/// </summary>
public class CreateRepositoryParameters
{
    public string? Organization { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Private { get; set; } = true;
    public string? Template { get; set; }
    public string[]? Topics { get; set; }
    public bool AutoInit { get; set; } = true;
    public string? GitignoreTemplate { get; set; }
    public string? License { get; set; }
    public bool BranchProtection { get; set; } = true;
    public bool RequireCodeOwners { get; set; } = false;
}

/// <summary>
/// Repository information
/// </summary>
public class RepositoryInfo
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string HtmlUrl { get; set; } = string.Empty;
    public string CloneUrl { get; set; } = string.Empty;
    public bool Private { get; set; }
    public string DefaultBranch { get; set; } = "main";
    public string[]? Topics { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
