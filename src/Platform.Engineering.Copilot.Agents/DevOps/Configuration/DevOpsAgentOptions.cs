namespace Platform.Engineering.Copilot.Agents.DevOps.Configuration;

/// <summary>
/// Configuration options for the DevOps Agent
/// </summary>
public class DevOpsAgentOptions
{
    public bool Enabled { get; set; } = true;
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set} = 4000;
    
    public GitHubOptions GitHub { get; set; } = new();
    public AzureDevOpsOptions AzureDevOps { get; set; } = new();
}

public class GitHubOptions
{
    public bool Enabled { get; set; } = true;
    public string? DefaultOrg { get; set; }
    public bool RequireBranchProtection { get; set; } = true;
    public bool RequireCodeOwners { get; set; } = false;
    public DefaultTemplates Templates { get; set; } = new();
}

public class AzureDevOpsOptions
{
    public bool Enabled { get; set; } = true;
    public string? DefaultOrganization { get; set; }
    public string? DefaultProject { get; set; }
    public string DefaultProcessTemplate { get; set; } = "Agile";
    public bool RequirePullRequests { get; set; } = true;
}

public class DefaultTemplates
{
    public string Workflow { get; set; } = "dotnet-build";
    public string GitIgnore { get; set} = "VisualStudio";
    public string License { get; set; } = "MIT";
}
