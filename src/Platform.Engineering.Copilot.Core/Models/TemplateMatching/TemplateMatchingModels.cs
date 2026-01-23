namespace Platform.Engineering.Copilot.Core.Models.TemplateMatching;

#region Template Matching Models

/// <summary>
/// Options for template matching
/// </summary>
public class TemplateMatchOptions
{
    /// <summary>
    /// Minimum score threshold (0.0 to 1.0)
    /// </summary>
    public double MinimumScore { get; set; } = 0.3;
    
    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int MaxResults { get; set; } = 5;
    
    /// <summary>
    /// Whether to include deprecated templates
    /// </summary>
    public bool IncludeDeprecated { get; set; } = false;
    
    /// <summary>
    /// Filter by category
    /// </summary>
    public string? Category { get; set; }
    
    /// <summary>
    /// Required compliance framework
    /// </summary>
    public string? RequiredCompliance { get; set; }
}

/// <summary>
/// Result of template matching operation
/// </summary>
public class TemplateMatchResult
{
    public bool Success { get; set; }
    public string UserRequest { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public List<TemplateMatch> Matches { get; set; } = new();
    public bool UsedLlm { get; set; }
}

/// <summary>
/// A single template match with score and reasoning
/// </summary>
public class TemplateMatch
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public Dictionary<string, object?> SuggestedParameters { get; set; } = new();
}

/// <summary>
/// Result of parameter extraction
/// </summary>
public class ParameterExtractionResult
{
    public bool Success { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public Dictionary<string, ExtractedParameter> ExtractedParameters { get; set; } = new();
}

/// <summary>
/// An extracted parameter value with confidence
/// </summary>
public class ExtractedParameter
{
    public string ParameterName { get; set; } = string.Empty;
    public object? SuggestedValue { get; set; }
    public double Confidence { get; set; }
    public string Source { get; set; } = string.Empty; // "llm", "default", "inferred"
    public string? Reasoning { get; set; }
}

#endregion

#region Git Sync Models

/// <summary>
/// Configuration options for Git sync
/// </summary>
public class GitSyncOptions
{
    /// <summary>
    /// GitHub Personal Access Token for API authentication
    /// </summary>
    public string? GitHubToken { get; set; }
    
    /// <summary>
    /// Azure DevOps Personal Access Token for API authentication
    /// </summary>
    public string? AzureDevOpsToken { get; set; }
    
    /// <summary>
    /// Default interval in minutes for syncing individual templates
    /// </summary>
    public int DefaultSyncIntervalMinutes { get; set; } = 15;
    
    /// <summary>
    /// Interval in minutes for the background service to check for templates needing sync
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 5;
    
    /// <summary>
    /// Enable automatic background sync of templates
    /// </summary>
    public bool AutoSyncEnabled { get; set; } = true;
    
    /// <summary>
    /// Legacy property - use AutoSyncEnabled instead
    /// </summary>
    public bool EnableAutoSync { get => AutoSyncEnabled; set => AutoSyncEnabled = value; }
}

/// <summary>
/// Result of a single template sync operation
/// </summary>
public class GitSyncResult
{
    public bool Success { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public bool WasUpdated { get; set; }
    public string? CommitSha { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of batch sync operation
/// </summary>
public class GitSyncBatchResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Updated { get; set; } = new();
    public List<string> Unchanged { get; set; } = new();
    public List<string> Skipped { get; set; } = new();
    public List<GitSyncFailure> Failed { get; set; } = new();
}

/// <summary>
/// Details of a sync failure
/// </summary>
public class GitSyncFailure
{
    public string TemplateId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Result of importing a template from Git
/// </summary>
public class GitImportResult
{
    public bool Success { get; set; }
    public string? TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? CommitSha { get; set; }
}

/// <summary>
/// Result of checking for Git changes
/// </summary>
public class GitDiffResult
{
    public bool Success { get; set; } = true;
    public bool HasChanges { get; set; }
    public string? CurrentSha { get; set; }
    public string? LatestSha { get; set; }
    public DateTime? LastSynced { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

#endregion
