using Platform.Engineering.Copilot.Core.Models.Notifications;

namespace Platform.Engineering.Copilot.Core.Services.Notifications;

/// <summary>
/// Service for sending email notifications to mission owners and NNWC team
/// Uses Azure Communication Services for DoD-compliant email delivery
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send email notification when onboarding request is approved
    /// </summary>
    /// <param name="request">Approval email details including mission info and approver</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Email send result with message ID and status</returns>
    Task<EmailNotificationResult> SendApprovalNotificationAsync(
        ApprovalEmailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send email notification when onboarding request is rejected
    /// </summary>
    /// <param name="request">Rejection email details including reason and appeal process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Email send result with message ID and status</returns>
    Task<EmailNotificationResult> SendRejectionNotificationAsync(
        RejectionEmailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send email notification when Azure resource provisioning completes successfully
    /// </summary>
    /// <param name="request">Provisioning complete email with all resource details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Email send result with message ID and status</returns>
    Task<EmailNotificationResult> SendProvisioningCompleteNotificationAsync(
        ProvisioningCompleteEmailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send email notification when Azure resource provisioning fails
    /// </summary>
    /// <param name="request">Provisioning failure email with error details and next steps</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Email send result with message ID and status</returns>
    Task<EmailNotificationResult> SendProvisioningFailedNotificationAsync(
        ProvisioningFailedEmailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send email to NNWC team with onboarding request summary (for internal tracking)
    /// </summary>
    /// <param name="missionName">Mission name</param>
    /// <param name="requestId">Request ID</param>
    /// <param name="status">Current status</param>
    /// <param name="details">Additional details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Email send result</returns>
    Task<EmailNotificationResult> SendNNWCTeamNotificationAsync(
        string missionName,
        string requestId,
        string status,
        string details,
        CancellationToken cancellationToken = default);
}
