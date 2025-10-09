using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Models.Notifications;
using Azure.Communication.Email;
using System.Text;

namespace Platform.Engineering.Copilot.Core.Services.Notifications;

/// <summary>
/// Implementation of email notification service using Azure Communication Services
/// Supports DoD-compliant email delivery for Navy Flankspeed mission owners
/// </summary>
public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly EmailConfiguration _config;
    private readonly EmailClient? _emailClient;

    public EmailService(
        ILogger<EmailService> logger,
        IOptions<EmailConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;

        // Only create EmailClient if not in mock mode and connection string is provided
        if (!_config.MockMode && !string.IsNullOrWhiteSpace(_config.ConnectionString))
        {
            try
            {
                _emailClient = new EmailClient(_config.ConnectionString);
                _logger.LogInformation("EmailClient initialized with Azure Communication Services");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize EmailClient. Falling back to mock mode.");
                _config.MockMode = true;
            }
        }
        else
        {
            _logger.LogInformation("EmailService running in mock mode (notifications will be logged only)");
        }
    }

    public async Task<EmailNotificationResult> SendApprovalNotificationAsync(
        ApprovalEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending approval notification for mission '{MissionName}' to {Email}",
            request.MissionName,
            request.RecipientEmail);

        var subject = $"Navy Flankspeed Onboarding Approved - {request.MissionName}";
        var htmlContent = GenerateApprovalEmailHtml(request);

        return await SendEmailAsync(
            request.RecipientEmail,
            request.RecipientName,
            subject,
            htmlContent,
            "Approval",
            cancellationToken);
    }

    public async Task<EmailNotificationResult> SendRejectionNotificationAsync(
        RejectionEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending rejection notification for mission '{MissionName}' to {Email}",
            request.MissionName,
            request.RecipientEmail);

        var subject = $"Navy Flankspeed Onboarding Request Update - {request.MissionName}";
        var htmlContent = GenerateRejectionEmailHtml(request);

        return await SendEmailAsync(
            request.RecipientEmail,
            request.RecipientName,
            subject,
            htmlContent,
            "Rejection",
            cancellationToken);
    }

    public async Task<EmailNotificationResult> SendProvisioningCompleteNotificationAsync(
        ProvisioningCompleteEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending provisioning complete notification for mission '{MissionName}' to {Email}",
            request.MissionName,
            request.RecipientEmail);

        var subject = $"Navy Flankspeed Resources Ready - {request.MissionName}";
        var htmlContent = GenerateProvisioningCompleteEmailHtml(request);

        return await SendEmailAsync(
            request.RecipientEmail,
            request.RecipientName,
            subject,
            htmlContent,
            "ProvisioningComplete",
            cancellationToken);
    }

    public async Task<EmailNotificationResult> SendProvisioningFailedNotificationAsync(
        ProvisioningFailedEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogError(
            "Sending provisioning failure notification for mission '{MissionName}' to {Email}. Reason: {Reason}",
            request.MissionName,
            request.RecipientEmail,
            request.FailureReason);

        var subject = $"Navy Flankspeed Provisioning Issue - {request.MissionName}";
        var htmlContent = GenerateProvisioningFailedEmailHtml(request);

        return await SendEmailAsync(
            request.RecipientEmail,
            request.RecipientName,
            subject,
            htmlContent,
            "ProvisioningFailed",
            cancellationToken);
    }

    public async Task<EmailNotificationResult> SendNNWCTeamNotificationAsync(
        string missionName,
        string requestId,
        string status,
        string details,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending NNWC team notification for mission '{MissionName}' with status {Status}",
            missionName,
            status);

        var subject = $"Flankspeed Onboarding Update - {missionName} - {status}";
        var htmlContent = GenerateNNWCTeamEmailHtml(missionName, requestId, status, details);

        return await SendEmailAsync(
            _config.NNWCTeamEmail,
            "NNWC Operations Team",
            subject,
            htmlContent,
            "NNWCTeamNotification",
            cancellationToken);
    }

    /// <summary>
    /// Core email sending method with retry logic and error handling
    /// </summary>
    private async Task<EmailNotificationResult> SendEmailAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string htmlContent,
        string notificationType,
        CancellationToken cancellationToken)
    {
        if (!_config.EnableNotifications)
        {
            _logger.LogInformation("Email notifications disabled. Skipping email send.");
            return new EmailNotificationResult
            {
                Success = true,
                MessageId = "NOTIFICATIONS_DISABLED",
                RecipientEmail = recipientEmail,
                NotificationType = notificationType
            };
        }

        // Mock mode - log email instead of sending
        if (_config.MockMode || _emailClient == null)
        {
            _logger.LogInformation(
                "MOCK EMAIL - To: {Email}, Subject: {Subject}\nContent:\n{Content}",
                recipientEmail,
                subject,
                htmlContent);

            return new EmailNotificationResult
            {
                Success = true,
                MessageId = $"MOCK_{Guid.NewGuid()}",
                RecipientEmail = recipientEmail,
                NotificationType = notificationType
            };
        }

        // Real email send with retry logic
        var retryCount = 0;
        var delay = _config.Retry.InitialDelayMs;

        while (retryCount <= _config.Retry.MaxRetries)
        {
            try
            {
                var emailMessage = new EmailMessage(
                    senderAddress: _config.SenderEmail,
                    recipientAddress: recipientEmail,
                    content: new EmailContent(subject)
                    {
                        Html = htmlContent
                    });

                var sendOperation = await _emailClient.SendAsync(
                    global::Azure.WaitUntil.Started,
                    emailMessage,
                    cancellationToken);

                _logger.LogInformation(
                    "Email sent successfully to {Email}. Message ID: {MessageId}",
                    recipientEmail,
                    sendOperation.Id);

                return new EmailNotificationResult
                {
                    Success = true,
                    MessageId = sendOperation.Id,
                    RecipientEmail = recipientEmail,
                    NotificationType = notificationType
                };
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning(
                    ex,
                    "Failed to send email to {Email} (attempt {Attempt}/{MaxAttempts})",
                    recipientEmail,
                    retryCount,
                    _config.Retry.MaxRetries + 1);

                if (retryCount > _config.Retry.MaxRetries)
                {
                    _logger.LogError(
                        ex,
                        "Failed to send email to {Email} after {Attempts} attempts",
                        recipientEmail,
                        retryCount);

                    return new EmailNotificationResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        RecipientEmail = recipientEmail,
                        NotificationType = notificationType
                    };
                }

                await Task.Delay(delay, cancellationToken);
                delay = (int)(delay * _config.Retry.BackoffMultiplier);
            }
        }

        return new EmailNotificationResult
        {
            Success = false,
            ErrorMessage = "Max retries exceeded",
            RecipientEmail = recipientEmail,
            NotificationType = notificationType
        };
    }

    #region HTML Email Template Generators

    private string GenerateApprovalEmailHtml(ApprovalEmailRequest request)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {_config.Templates.PrimaryColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-left: 4px solid {_config.Templates.SecondaryColor}; }}
        .success-badge {{ background-color: #28a745; color: white; padding: 10px 20px; border-radius: 5px; display: inline-block; margin: 20px 0; }}
        .info-box {{ background-color: white; padding: 15px; margin: 15px 0; border-radius: 5px; border: 1px solid #ddd; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .button {{ background-color: {_config.Templates.PrimaryColor}; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 15px 0; }}
        h1 {{ margin: 0; }}
        h2 {{ color: {_config.Templates.PrimaryColor}; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🎉 Onboarding Request Approved</h1>
        </div>
        <div class=""content"">
            <p>Dear {request.RecipientName},</p>
            
            <div class=""success-badge"">
                ✓ APPROVED
            </div>

            <p>Your Navy Flankspeed onboarding request for <strong>{request.MissionName}</strong> has been approved!</p>

            <div class=""info-box"">
                <h3>Request Details</h3>
                <p><strong>Request ID:</strong> {request.RequestId}</p>
                <p><strong>Mission Name:</strong> {request.MissionName}</p>
                <p><strong>Approved By:</strong> {request.ApprovedBy}</p>
                <p><strong>Approval Date:</strong> {request.Timestamp:yyyy-MM-dd HH:mm:ss} UTC</p>
            </div>

            {(!string.IsNullOrWhiteSpace(request.ApprovalComments) ? $@"
            <div class=""info-box"">
                <h3>Approver Comments</h3>
                <p>{request.ApprovalComments}</p>
            </div>" : "")}

            <h2>What Happens Next?</h2>
            <p>{request.NextSteps}</p>

            <div class=""info-box"">
                <h3>Estimated Timeline</h3>
                <p>⏱️ <strong>{request.EstimatedProvisioningTime}</strong></p>
                <p>You will receive another email notification once your Azure resources are fully provisioned and ready to use.</p>
            </div>

            <p>If you have any questions, please contact the NNWC support team at <a href=""mailto:{_config.Templates.SupportEmail}"">{_config.Templates.SupportEmail}</a>.</p>
        </div>
        <div class=""footer"">
            {_config.Templates.FooterText}
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateRejectionEmailHtml(RejectionEmailRequest request)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {_config.Templates.PrimaryColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-left: 4px solid #dc3545; }}
        .warning-badge {{ background-color: #dc3545; color: white; padding: 10px 20px; border-radius: 5px; display: inline-block; margin: 20px 0; }}
        .info-box {{ background-color: white; padding: 15px; margin: 15px 0; border-radius: 5px; border: 1px solid #ddd; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .button {{ background-color: {_config.Templates.PrimaryColor}; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 15px 0; }}
        h1 {{ margin: 0; }}
        h2 {{ color: {_config.Templates.PrimaryColor}; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Onboarding Request Update</h1>
        </div>
        <div class=""content"">
            <p>Dear {request.RecipientName},</p>
            
            <div class=""warning-badge"">
                ⚠ REQUIRES ATTENTION
            </div>

            <p>Your Navy Flankspeed onboarding request for <strong>{request.MissionName}</strong> requires additional review.</p>

            <div class=""info-box"">
                <h3>Request Details</h3>
                <p><strong>Request ID:</strong> {request.RequestId}</p>
                <p><strong>Mission Name:</strong> {request.MissionName}</p>
                <p><strong>Reviewed By:</strong> {request.RejectedBy}</p>
                <p><strong>Review Date:</strong> {request.Timestamp:yyyy-MM-dd HH:mm:ss} UTC</p>
            </div>

            <div class=""info-box"">
                <h3>Reason for Additional Review</h3>
                <p>{request.RejectionReason}</p>
            </div>

            <h2>Next Steps</h2>
            <p>{request.AppealProcess}</p>

            <p>The NNWC team is here to help ensure your mission has the resources it needs. Please don't hesitate to reach out for clarification or to discuss modifications to your request.</p>

            <a href=""mailto:{_config.Templates.SupportEmail}"" class=""button"">Contact Support Team</a>
        </div>
        <div class=""footer"">
            {_config.Templates.FooterText}
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateProvisioningCompleteEmailHtml(ProvisioningCompleteEmailRequest request)
    {
        var subnetTableRows = new StringBuilder();
        foreach (var subnet in request.Subnets)
        {
            subnetTableRows.AppendLine($@"
                <tr>
                    <td style=""padding: 8px; border: 1px solid #ddd;"">{subnet.Name}</td>
                    <td style=""padding: 8px; border: 1px solid #ddd;"">{subnet.AddressPrefix}</td>
                    <td style=""padding: 8px; border: 1px solid #ddd;"">{subnet.Purpose}</td>
                </tr>");
        }

        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {_config.Templates.PrimaryColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-left: 4px solid {_config.Templates.SecondaryColor}; }}
        .success-badge {{ background-color: #28a745; color: white; padding: 10px 20px; border-radius: 5px; display: inline-block; margin: 20px 0; }}
        .info-box {{ background-color: white; padding: 15px; margin: 15px 0; border-radius: 5px; border: 1px solid #ddd; }}
        .resource-table {{ width: 100%; border-collapse: collapse; margin: 15px 0; }}
        .resource-table th {{ background-color: {_config.Templates.PrimaryColor}; color: white; padding: 10px; text-align: left; }}
        .resource-table td {{ padding: 8px; border: 1px solid #ddd; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .button {{ background-color: {_config.Templates.PrimaryColor}; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 15px 0; }}
        h1 {{ margin: 0; }}
        h2 {{ color: {_config.Templates.PrimaryColor}; }}
        .tag {{ background-color: #f0f0f0; padding: 4px 8px; border-radius: 3px; margin: 2px; display: inline-block; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🚀 Your Azure Resources Are Ready!</h1>
        </div>
        <div class=""content"">
            <p>Dear {request.RecipientName},</p>
            
            <div class=""success-badge"">
                ✓ PROVISIONING COMPLETE
            </div>

            <p>Great news! Your Azure Government resources for <strong>{request.MissionName}</strong> have been successfully provisioned and are ready to use.</p>

            <div class=""info-box"">
                <h3>Subscription Information</h3>
                <p><strong>Subscription ID:</strong> {request.SubscriptionId}</p>
                <p><strong>Subscription Name:</strong> {request.SubscriptionName}</p>
                <p><strong>Classification:</strong> {request.ClassificationLevel}</p>
                {(request.DDoSProtectionEnabled ? "<p><strong>DDoS Protection:</strong> ✓ Enabled</p>" : "")}
            </div>

            <div class=""info-box"">
                <h3>Resource Group</h3>
                <p><strong>Name:</strong> {request.ResourceGroupName}</p>
                <p><strong>Location:</strong> USGov Virginia</p>
            </div>

            <div class=""info-box"">
                <h3>Virtual Network</h3>
                <p><strong>VNet Name:</strong> {request.VirtualNetworkName}</p>
                <p><strong>Address Space:</strong> {request.VirtualNetworkCidr}</p>
                <p><strong>Network Security Group:</strong> {request.NetworkSecurityGroupName}</p>
            </div>

            <div class=""info-box"">
                <h3>Subnets</h3>
                <table class=""resource-table"">
                    <thead>
                        <tr>
                            <th>Subnet Name</th>
                            <th>Address Prefix</th>
                            <th>Purpose</th>
                        </tr>
                    </thead>
                    <tbody>
                        {subnetTableRows}
                    </tbody>
                </table>
            </div>

            {(request.Tags.Any() ? $@"
            <div class=""info-box"">
                <h3>Resource Tags</h3>
                <p>{string.Join(" ", request.Tags.Select(t => $"<span class='tag'>{t.Key}: {t.Value}</span>"))}</p>
            </div>" : "")}

            <h2>Access Your Resources</h2>
            <p>You can now access and manage your resources through the Azure Government Portal:</p>
            <a href=""{request.AzurePortalUrl}"" class=""button"">Open Azure Portal</a>

            <h2>Next Steps</h2>
            <ul>
                <li>Review your resource configuration in the Azure Portal</li>
                <li>Configure additional resources as needed (VMs, storage, databases, etc.)</li>
                <li>Set up role-based access control (RBAC) for your team members</li>
                <li>Configure monitoring and alerts for your resources</li>
            </ul>

            <p>If you need assistance or have questions, contact the NNWC support team at <a href=""mailto:{_config.Templates.SupportEmail}"">{_config.Templates.SupportEmail}</a>.</p>
        </div>
        <div class=""footer"">
            {_config.Templates.FooterText}
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateProvisioningFailedEmailHtml(ProvisioningFailedEmailRequest request)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {_config.Templates.PrimaryColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 30px; border-left: 4px solid #dc3545; }}
        .error-badge {{ background-color: #dc3545; color: white; padding: 10px 20px; border-radius: 5px; display: inline-block; margin: 20px 0; }}
        .info-box {{ background-color: white; padding: 15px; margin: 15px 0; border-radius: 5px; border: 1px solid #ddd; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        .button {{ background-color: {_config.Templates.PrimaryColor}; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 15px 0; }}
        h1 {{ margin: 0; }}
        h2 {{ color: {_config.Templates.PrimaryColor}; }}
        .code-block {{ background-color: #f4f4f4; padding: 10px; border-radius: 5px; font-family: monospace; font-size: 12px; overflow-x: auto; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Provisioning Status Update</h1>
        </div>
        <div class=""content"">
            <p>Dear {request.RecipientName},</p>
            
            <div class=""error-badge"">
                ⚠ PROVISIONING ISSUE DETECTED
            </div>

            <p>We encountered an issue while provisioning Azure resources for <strong>{request.MissionName}</strong>. The NNWC team has been automatically notified and is investigating.</p>

            <div class=""info-box"">
                <h3>Incident Details</h3>
                <p><strong>Request ID:</strong> {request.RequestId}</p>
                <p><strong>Mission Name:</strong> {request.MissionName}</p>
                <p><strong>Failed Step:</strong> {request.FailedStep}</p>
                <p><strong>Failure Time:</strong> {request.FailureTimestamp:yyyy-MM-dd HH:mm:ss} UTC</p>
                {(request.AutoRollbackCompleted ? "<p><strong>Auto-Rollback:</strong> ✓ Completed (no resources left behind)</p>" : "")}
            </div>

            <div class=""info-box"">
                <h3>Issue Summary</h3>
                <p>{request.FailureReason}</p>
            </div>

            {(_config.IncludeDetailedErrors && !string.IsNullOrWhiteSpace(request.ErrorDetails) ? $@"
            <div class=""info-box"">
                <h3>Technical Details</h3>
                <div class=""code-block"">{request.ErrorDetails}</div>
            </div>" : "")}

            <h2>What We're Doing</h2>
            <p>The NNWC operations team has been notified and is actively investigating this issue. Our team will:</p>
            <ul>
                <li>Review the error logs and provisioning details</li>
                <li>Identify the root cause of the failure</li>
                <li>Resolve any configuration or permission issues</li>
                <li>Re-provision your resources once the issue is resolved</li>
            </ul>

            <h2>Next Steps</h2>
            <p>{request.NextSteps}</p>

            <a href=""{request.SupportTicketUrl}"" class=""button"">View Support Ticket</a>

            <p>We apologize for the inconvenience and appreciate your patience as we work to resolve this issue.</p>

            <p>For urgent inquiries, contact the NNWC support team directly at <a href=""mailto:{_config.Templates.SupportEmail}"">{_config.Templates.SupportEmail}</a>.</p>
        </div>
        <div class=""footer"">
            {_config.Templates.FooterText}
        </div>
    </div>
</body>
</html>";
    }

    private string GenerateNNWCTeamEmailHtml(string missionName, string requestId, string status, string details)
    {
        return $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: {_config.Templates.PrimaryColor}; color: white; padding: 20px; text-align: center; }}
        .content {{ background-color: #f9f9f9; padding: 30px; }}
        .info-box {{ background-color: white; padding: 15px; margin: 15px 0; border-radius: 5px; border: 1px solid #ddd; }}
        .footer {{ text-align: center; padding: 20px; color: #666; font-size: 12px; }}
        h1 {{ margin: 0; }}
        h2 {{ color: {_config.Templates.PrimaryColor}; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>Flankspeed Onboarding Update</h1>
        </div>
        <div class=""content"">
            <p>NNWC Operations Team,</p>

            <div class=""info-box"">
                <h3>Status Update</h3>
                <p><strong>Mission:</strong> {missionName}</p>
                <p><strong>Request ID:</strong> {requestId}</p>
                <p><strong>Status:</strong> {status}</p>
                <p><strong>Timestamp:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
            </div>

            <div class=""info-box"">
                <h3>Details</h3>
                <p>{details}</p>
            </div>
        </div>
        <div class=""footer"">
            Internal NNWC notification - Navy Flankspeed Platform
        </div>
    </div>
</body>
</html>";
    }

    #endregion
}
