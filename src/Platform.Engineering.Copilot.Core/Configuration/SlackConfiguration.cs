namespace Platform.Engineering.Copilot.Core.Configuration;

/// <summary>
/// Configuration for Slack notifications to NNWC operations team
/// </summary>
public class SlackConfiguration
{
    /// <summary>
    /// Slack incoming webhook URL for NNWC operations channel
    /// Format: https://hooks.slack.com/services/T00000000/B00000000/XXXXXXXXXXXXXXXXXXXX
    /// </summary>
    public string WebhookUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether to send Slack notifications (can disable for development/testing)
    /// </summary>
    public bool EnableNotifications { get; set; } = true;

    /// <summary>
    /// Mock mode - log Slack messages instead of sending (for development)
    /// </summary>
    public bool MockMode { get; set; } = true;

    /// <summary>
    /// Slack channel name for notifications (for display purposes)
    /// </summary>
    public string ChannelName { get; set; } = "#flankspeed-ops";

    /// <summary>
    /// Bot username to display in Slack
    /// </summary>
    public string BotUsername { get; set; } = "Flankspeed Platform";

    /// <summary>
    /// Bot icon emoji for Slack messages
    /// </summary>
    public string BotIconEmoji { get; set; } = ":anchor:";

    /// <summary>
    /// Whether to mention @channel for high-priority alerts (failures)
    /// </summary>
    public bool MentionChannelOnFailure { get; set; } = true;

    /// <summary>
    /// Azure Portal base URL for constructing resource links
    /// </summary>
    public string AzurePortalBaseUrl { get; set; } = "https://portal.azure.us";

    /// <summary>
    /// Retry settings for failed Slack sends
    /// </summary>
    public SlackRetryConfiguration Retry { get; set; } = new();
}

/// <summary>
/// Retry configuration for Slack webhook calls
/// </summary>
public class SlackRetryConfiguration
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay before first retry (milliseconds)
    /// </summary>
    public int InitialDelayMs { get; set; } = 500;

    /// <summary>
    /// Exponential backoff multiplier
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}
