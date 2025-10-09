namespace Platform.Engineering.Copilot.Core.Configuration;

/// <summary>
/// Configuration options for governance
/// </summary>
public class GovernanceOptions
{
    public const string SectionName = "Governance";

    /// <summary>
    /// Path to ATO rules configuration file
    /// </summary>
    public string AtoRulesPath { get; set; } = "ato-rules.json";

    /// <summary>
    /// Azure subscription ID for policy checks
    /// </summary>
    public string? AzureSubscriptionId { get; set; }

    /// <summary>
    /// Teams webhook URL for approval notifications
    /// </summary>
    public string? TeamsWebhookUrl { get; set; }

    /// <summary>
    /// Timeout for approval requests in minutes
    /// </summary>
    public int ApprovalTimeoutMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to enforce policy checks
    /// </summary>
    public bool EnforcePolicies { get; set; } = true;

    /// <summary>
    /// Whether to require approvals for flagged operations
    /// </summary>
    public bool RequireApprovals { get; set; } = true;
}
