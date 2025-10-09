namespace Platform.Engineering.Copilot.Core.Models.Notifications;

/// <summary>
/// Base class for email notification requests
/// </summary>
public abstract class EmailNotificationRequest
{
    public string RecipientEmail { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
    public string MissionName { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Email notification for onboarding request approval
/// </summary>
public class ApprovalEmailRequest : EmailNotificationRequest
{
    public string ApprovedBy { get; set; } = string.Empty;
    public string ApprovalComments { get; set; } = string.Empty;
    public string EstimatedProvisioningTime { get; set; } = "15-30 minutes";
    public string NextSteps { get; set; } = "Your Azure resources are now being provisioned. You will receive another email when provisioning is complete.";
}

/// <summary>
/// Email notification for onboarding request rejection
/// </summary>
public class RejectionEmailRequest : EmailNotificationRequest
{
    public string RejectedBy { get; set; } = string.Empty;
    public string RejectionReason { get; set; } = string.Empty;
    public string AppealProcess { get; set; } = "Contact the NNWC team at nnwc-support@navy.mil to discuss your request.";
}

/// <summary>
/// Email notification for successful Azure resource provisioning
/// </summary>
public class ProvisioningCompleteEmailRequest : EmailNotificationRequest
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string SubscriptionName { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string VirtualNetworkName { get; set; } = string.Empty;
    public string VirtualNetworkCidr { get; set; } = string.Empty;
    public List<SubnetInfo> Subnets { get; set; } = new();
    public string NetworkSecurityGroupName { get; set; } = string.Empty;
    public string AzurePortalUrl { get; set; } = string.Empty;
    public string ClassificationLevel { get; set; } = string.Empty;
    public bool DDoSProtectionEnabled { get; set; }
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Email notification for failed Azure resource provisioning
/// </summary>
public class ProvisioningFailedEmailRequest : EmailNotificationRequest
{
    public string FailureReason { get; set; } = string.Empty;
    public string ErrorDetails { get; set; } = string.Empty;
    public string FailedStep { get; set; } = string.Empty;
    public DateTime FailureTimestamp { get; set; } = DateTime.UtcNow;
    public string SupportTicketUrl { get; set; } = string.Empty;
    public string NextSteps { get; set; } = "The NNWC team has been notified and will investigate. You will be contacted within 24 hours.";
    public bool AutoRollbackCompleted { get; set; }
}

/// <summary>
/// Subnet information for provisioning complete emails
/// </summary>
public class SubnetInfo
{
    public string Name { get; set; } = string.Empty;
    public string AddressPrefix { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
}

/// <summary>
/// Result of sending an email notification
/// </summary>
public class EmailNotificationResult
{
    public bool Success { get; set; }
    public string MessageId { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime SentTimestamp { get; set; } = DateTime.UtcNow;
    public string RecipientEmail { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
}
