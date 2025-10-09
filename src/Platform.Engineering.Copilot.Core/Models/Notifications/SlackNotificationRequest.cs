namespace Platform.Engineering.Copilot.Core.Models.Notifications;

/// <summary>
/// Base class for Slack notification requests
/// </summary>
public abstract class SlackNotificationRequest
{
    public string MissionName { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public string MissionOwner { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Slack notification for onboarding request approval
/// </summary>
public class SlackApprovalRequest : SlackNotificationRequest
{
    public string ApprovedBy { get; set; } = string.Empty;
    public string ClassificationLevel { get; set; } = string.Empty;
    public string EstimatedCompletionTime { get; set; } = "15-30 minutes";
}

/// <summary>
/// Slack notification for onboarding request rejection
/// </summary>
public class SlackRejectionRequest : SlackNotificationRequest
{
    public string RejectedBy { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
}

/// <summary>
/// Slack notification for successful provisioning
/// </summary>
public class SlackProvisioningCompleteRequest : SlackNotificationRequest
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
    public int SubnetCount { get; set; }
    public string ClassificationLevel { get; set; } = string.Empty;
    public TimeSpan ProvisioningDuration { get; set; }
}

/// <summary>
/// Slack notification for provisioning failure
/// </summary>
public class SlackProvisioningFailedRequest : SlackNotificationRequest
{
    public string FailureReason { get; set; } = string.Empty;
    public string FailedStep { get; set; } = string.Empty;
    public bool AutoRollbackCompleted { get; set; }
}

/// <summary>
/// Result of sending a Slack notification
/// </summary>
public class SlackNotificationResult
{
    public bool Success { get; set; }
    public string ResponseMessage { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime SentTimestamp { get; set; } = DateTime.UtcNow;
    public string NotificationType { get; set; } = string.Empty;
}
