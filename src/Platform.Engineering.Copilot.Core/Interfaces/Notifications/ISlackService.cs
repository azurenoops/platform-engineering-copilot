using Platform.Engineering.Copilot.Core.Models.Notifications;

namespace Platform.Engineering.Copilot.Core.Services.Notifications;

/// <summary>
/// Service for sending Slack notifications to NNWC operations team
/// Uses Slack incoming webhooks for real-time team alerts
/// </summary>
public interface ISlackService
{
    /// <summary>
    /// Send Slack notification when onboarding request is approved
    /// </summary>
    /// <param name="request">Approval details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Slack send result</returns>
    Task<SlackNotificationResult> SendOnboardingApprovedAsync(
        SlackApprovalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send Slack notification when onboarding request is rejected
    /// </summary>
    /// <param name="request">Rejection details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Slack send result</returns>
    Task<SlackNotificationResult> SendOnboardingRejectedAsync(
        SlackRejectionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send Slack notification when provisioning completes successfully
    /// </summary>
    /// <param name="request">Provisioning success details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Slack send result</returns>
    Task<SlackNotificationResult> SendProvisioningCompleteAsync(
        SlackProvisioningCompleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send Slack notification when provisioning fails (high priority alert)
    /// </summary>
    /// <param name="request">Provisioning failure details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Slack send result</returns>
    Task<SlackNotificationResult> SendProvisioningFailedAsync(
        SlackProvisioningFailedRequest request,
        CancellationToken cancellationToken = default);
}
