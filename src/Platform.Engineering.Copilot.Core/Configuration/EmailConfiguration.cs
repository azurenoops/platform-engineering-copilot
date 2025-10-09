namespace Platform.Engineering.Copilot.Core.Configuration;

/// <summary>
/// Configuration for email notifications using Azure Communication Services
/// </summary>
public class EmailConfiguration
{
    /// <summary>
    /// Azure Communication Services connection string
    /// Format: endpoint=https://<resource-name>.communication.azure.us/;accesskey=<access-key>
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Sender email address (must be verified in Azure Communication Services)
    /// Example: noreply@flankspeed.navy.mil
    /// </summary>
    public string SenderEmail { get; set; } = string.Empty;

    /// <summary>
    /// Sender display name for outgoing emails
    /// </summary>
    public string SenderName { get; set; } = "Navy Flankspeed Platform";

    /// <summary>
    /// NNWC team distribution list email for internal notifications
    /// </summary>
    public string NNWCTeamEmail { get; set; } = string.Empty;

    /// <summary>
    /// Whether to send notifications (can disable for development/testing)
    /// </summary>
    public bool EnableNotifications { get; set; } = true;

    /// <summary>
    /// Mock mode - log email content instead of sending (for development)
    /// </summary>
    public bool MockMode { get; set; } = true;

    /// <summary>
    /// Azure Portal base URL for constructing resource links
    /// Azure Government: https://portal.azure.us
    /// </summary>
    public string AzurePortalBaseUrl { get; set; } = "https://portal.azure.us";

    /// <summary>
    /// Support ticket URL for failed provisioning cases
    /// </summary>
    public string SupportTicketUrl { get; set; } = "https://support.navy.mil/create-ticket";

    /// <summary>
    /// Whether to include detailed error messages in failure notifications
    /// (May want to disable for security in production)
    /// </summary>
    public bool IncludeDetailedErrors { get; set; } = true;

    /// <summary>
    /// Retry settings for failed email sends
    /// </summary>
    public EmailRetryConfiguration Retry { get; set; } = new();

    /// <summary>
    /// Email template customization settings
    /// </summary>
    public EmailTemplateConfiguration Templates { get; set; } = new();
}

/// <summary>
/// Retry configuration for email delivery
/// </summary>
public class EmailRetryConfiguration
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay before first retry (milliseconds)
    /// </summary>
    public int InitialDelayMs { get; set; } = 1000;

    /// <summary>
    /// Exponential backoff multiplier
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Email template customization settings
/// </summary>
public class EmailTemplateConfiguration
{
    /// <summary>
    /// Logo URL for email headers
    /// </summary>
    public string LogoUrl { get; set; } = string.Empty;

    /// <summary>
    /// Primary brand color (hex code)
    /// </summary>
    public string PrimaryColor { get; set; } = "#005288"; // Navy blue

    /// <summary>
    /// Secondary brand color (hex code)
    /// </summary>
    public string SecondaryColor { get; set; } = "#C5AA6A"; // Navy gold

    /// <summary>
    /// Footer text for all emails
    /// </summary>
    public string FooterText { get; set; } = "This is an automated notification from the Navy Flankspeed Platform. Do not reply to this email.";

    /// <summary>
    /// Contact email for support inquiries
    /// </summary>
    public string SupportEmail { get; set; } = "flankspeed-support@navy.mil";
}
