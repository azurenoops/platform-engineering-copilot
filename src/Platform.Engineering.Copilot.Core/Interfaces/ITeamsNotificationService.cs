using System.Threading;
using System.Threading.Tasks;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Service for sending notifications to Microsoft Teams via webhooks
/// </summary>
public interface ITeamsNotificationService
{
    /// <summary>
    /// Send notification when onboarding request is approved
    /// </summary>
    Task SendOnboardingApprovedNotificationAsync(
        string missionName,
        string missionOwner,
        string command,
        string requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification when template generation starts
    /// </summary>
    Task SendTemplateGenerationStartedNotificationAsync(
        string missionName,
        string requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification when template generation completes
    /// </summary>
    Task SendTemplateGenerationCompletedNotificationAsync(
        string missionName,
        string requestId,
        int filesGenerated,
        string summary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification when infrastructure deployment starts
    /// </summary>
    Task SendDeploymentStartedNotificationAsync(
        string missionName,
        string requestId,
        string environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification when infrastructure deployment completes
    /// </summary>
    Task SendDeploymentCompletedNotificationAsync(
        string missionName,
        string requestId,
        string environment,
        string resourceGroupName,
        string subscriptionId,
        bool success,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification for deployment failure
    /// </summary>
    Task SendDeploymentFailedNotificationAsync(
        string missionName,
        string requestId,
        string stage,
        string errorMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Send notification with custom adaptive card
    /// </summary>
    Task SendCustomNotificationAsync(
        string title,
        string message,
        string? color = null,
        Dictionary<string, string>? facts = null,
        CancellationToken cancellationToken = default);
}
