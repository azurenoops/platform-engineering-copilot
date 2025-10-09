namespace Platform.Engineering.Copilot.Core.Models.Onboarding;

/// <summary>
/// Record of a notification sent for an onboarding request
/// </summary>
public class NotificationRecord
{
    /// <summary>
    /// Type of notification (Approval, Rejection, ProvisioningComplete, ProvisioningFailed)
    /// </summary>
    public string Type { get; set; } = string.Empty;
    
    /// <summary>
    /// Channel used (Email, Slack)
    /// </summary>
    public string Channel { get; set; } = string.Empty;
    
    /// <summary>
    /// Recipient of the notification
    /// </summary>
    public string Recipient { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the notification was sent successfully
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Error message if notification failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// When the notification was sent
    /// </summary>
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Message ID or tracking reference from the notification service
    /// </summary>
    public string? MessageId { get; set; }
}
