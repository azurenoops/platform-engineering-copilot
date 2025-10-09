using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Models.Notifications;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Core.Services.Notifications;

/// <summary>
/// Implementation of Slack notification service using incoming webhooks
/// Sends real-time alerts to NNWC operations team Slack channel
/// </summary>
public class SlackService : ISlackService
{
    private readonly ILogger<SlackService> _logger;
    private readonly SlackConfiguration _config;
    private readonly HttpClient _httpClient;

    public SlackService(
        ILogger<SlackService> logger,
        IOptions<SlackConfiguration> config,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _config = config.Value;
        _httpClient = httpClientFactory.CreateClient("SlackWebhook");
    }

    public async Task<SlackNotificationResult> SendOnboardingApprovedAsync(
        SlackApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending Slack approval notification for mission '{MissionName}'",
            request.MissionName);

        var payload = new SlackWebhookPayload
        {
            Username = _config.BotUsername,
            IconEmoji = _config.BotIconEmoji,
            Blocks = new List<SlackBlock>
            {
                new SlackBlock
                {
                    Type = "header",
                    Text = new SlackText
                    {
                        Type = "plain_text",
                        Text = "✅ Onboarding Request Approved",
                        Emoji = true
                    }
                },
                new SlackBlock
                {
                    Type = "section",
                    Fields = new List<SlackText>
                    {
                        new() { Type = "mrkdwn", Text = $"*Mission:*\n{request.MissionName}" },
                        new() { Type = "mrkdwn", Text = $"*Request ID:*\n`{request.RequestId}`" },
                        new() { Type = "mrkdwn", Text = $"*Mission Owner:*\n{request.MissionOwner}" },
                        new() { Type = "mrkdwn", Text = $"*Approved By:*\n{request.ApprovedBy}" },
                        new() { Type = "mrkdwn", Text = $"*Classification:*\n{request.ClassificationLevel}" },
                        new() { Type = "mrkdwn", Text = $"*ETA:*\n{request.EstimatedCompletionTime}" }
                    }
                },
                new SlackBlock
                {
                    Type = "context",
                    Elements = new List<SlackText>
                    {
                        new() { Type = "mrkdwn", Text = $"Approved at {request.Timestamp:yyyy-MM-dd HH:mm:ss} UTC" }
                    }
                }
            }
        };

        return await SendSlackMessageAsync(payload, "OnboardingApproved", cancellationToken);
    }

    public async Task<SlackNotificationResult> SendOnboardingRejectedAsync(
        SlackRejectionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending Slack rejection notification for mission '{MissionName}'",
            request.MissionName);

        var payload = new SlackWebhookPayload
        {
            Username = _config.BotUsername,
            IconEmoji = _config.BotIconEmoji,
            Blocks = new List<SlackBlock>
            {
                new SlackBlock
                {
                    Type = "header",
                    Text = new SlackText
                    {
                        Type = "plain_text",
                        Text = "⚠️ Onboarding Request Needs Review",
                        Emoji = true
                    }
                },
                new SlackBlock
                {
                    Type = "section",
                    Fields = new List<SlackText>
                    {
                        new() { Type = "mrkdwn", Text = $"*Mission:*\n{request.MissionName}" },
                        new() { Type = "mrkdwn", Text = $"*Request ID:*\n`{request.RequestId}`" },
                        new() { Type = "mrkdwn", Text = $"*Mission Owner:*\n{request.MissionOwner}" },
                        new() { Type = "mrkdwn", Text = $"*Reviewed By:*\n{request.RejectedBy}" }
                    }
                },
                new SlackBlock
                {
                    Type = "section",
                    Text = new SlackText
                    {
                        Type = "mrkdwn",
                        Text = $"*Reason:*\n{request.RejectionReason}"
                    }
                },
                new SlackBlock
                {
                    Type = "context",
                    Elements = new List<SlackText>
                    {
                        new() { Type = "mrkdwn", Text = $"Rejected at {request.Timestamp:yyyy-MM-dd HH:mm:ss} UTC" }
                    }
                }
            }
        };

        return await SendSlackMessageAsync(payload, "OnboardingRejected", cancellationToken);
    }

    public async Task<SlackNotificationResult> SendProvisioningCompleteAsync(
        SlackProvisioningCompleteRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Sending Slack provisioning complete notification for mission '{MissionName}'",
            request.MissionName);

        var durationText = $"{request.ProvisioningDuration.TotalMinutes:F1} minutes";
        var portalUrl = $"{_config.AzurePortalBaseUrl}/#@/resource/subscriptions/{request.SubscriptionId}/overview";

        var payload = new SlackWebhookPayload
        {
            Username = _config.BotUsername,
            IconEmoji = _config.BotIconEmoji,
            Blocks = new List<SlackBlock>
            {
                new SlackBlock
                {
                    Type = "header",
                    Text = new SlackText
                    {
                        Type = "plain_text",
                        Text = "🚀 Provisioning Complete",
                        Emoji = true
                    }
                },
                new SlackBlock
                {
                    Type = "section",
                    Fields = new List<SlackText>
                    {
                        new() { Type = "mrkdwn", Text = $"*Mission:*\n{request.MissionName}" },
                        new() { Type = "mrkdwn", Text = $"*Request ID:*\n`{request.RequestId}`" },
                        new() { Type = "mrkdwn", Text = $"*Mission Owner:*\n{request.MissionOwner}" },
                        new() { Type = "mrkdwn", Text = $"*Subscription ID:*\n`{request.SubscriptionId}`" },
                        new() { Type = "mrkdwn", Text = $"*Resource Group:*\n{request.ResourceGroupName}" },
                        new() { Type = "mrkdwn", Text = $"*VNet:*\n{request.VirtualNetworkName}" },
                        new() { Type = "mrkdwn", Text = $"*Subnets:*\n{request.SubnetCount} created" },
                        new() { Type = "mrkdwn", Text = $"*Duration:*\n{durationText}" }
                    }
                },
                new SlackBlock
                {
                    Type = "section",
                    Text = new SlackText
                    {
                        Type = "mrkdwn",
                        Text = $"<{portalUrl}|View in Azure Portal>"
                    }
                },
                new SlackBlock
                {
                    Type = "context",
                    Elements = new List<SlackText>
                    {
                        new() { Type = "mrkdwn", Text = $"Completed at {request.Timestamp:yyyy-MM-dd HH:mm:ss} UTC" }
                    }
                }
            }
        };

        return await SendSlackMessageAsync(payload, "ProvisioningComplete", cancellationToken);
    }

    public async Task<SlackNotificationResult> SendProvisioningFailedAsync(
        SlackProvisioningFailedRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogError(
            "Sending Slack provisioning failure notification for mission '{MissionName}'. Reason: {Reason}",
            request.MissionName,
            request.FailureReason);

        var mentionText = _config.MentionChannelOnFailure ? "<!channel> " : "";
        var rollbackText = request.AutoRollbackCompleted ? "✓ Completed" : "✗ Manual cleanup may be required";

        var payload = new SlackWebhookPayload
        {
            Username = _config.BotUsername,
            IconEmoji = _config.BotIconEmoji,
            Blocks = new List<SlackBlock>
            {
                new SlackBlock
                {
                    Type = "header",
                    Text = new SlackText
                    {
                        Type = "plain_text",
                        Text = $"{mentionText}🚨 Provisioning Failure",
                        Emoji = true
                    }
                },
                new SlackBlock
                {
                    Type = "section",
                    Fields = new List<SlackText>
                    {
                        new() { Type = "mrkdwn", Text = $"*Mission:*\n{request.MissionName}" },
                        new() { Type = "mrkdwn", Text = $"*Request ID:*\n`{request.RequestId}`" },
                        new() { Type = "mrkdwn", Text = $"*Mission Owner:*\n{request.MissionOwner}" },
                        new() { Type = "mrkdwn", Text = $"*Failed Step:*\n{request.FailedStep}" },
                        new() { Type = "mrkdwn", Text = $"*Auto-Rollback:*\n{rollbackText}" }
                    }
                },
                new SlackBlock
                {
                    Type = "section",
                    Text = new SlackText
                    {
                        Type = "mrkdwn",
                        Text = $"*Error:*\n```{request.FailureReason}```"
                    }
                },
                new SlackBlock
                {
                    Type = "context",
                    Elements = new List<SlackText>
                    {
                        new() { Type = "mrkdwn", Text = $"Failed at {request.Timestamp:yyyy-MM-dd HH:mm:ss} UTC | *ACTION REQUIRED*" }
                    }
                }
            }
        };

        return await SendSlackMessageAsync(payload, "ProvisioningFailed", cancellationToken);
    }

    /// <summary>
    /// Core Slack message sending method with retry logic
    /// </summary>
    private async Task<SlackNotificationResult> SendSlackMessageAsync(
        SlackWebhookPayload payload,
        string notificationType,
        CancellationToken cancellationToken)
    {
        if (!_config.EnableNotifications)
        {
            _logger.LogInformation("Slack notifications disabled. Skipping message send.");
            return new SlackNotificationResult
            {
                Success = true,
                ResponseMessage = "NOTIFICATIONS_DISABLED",
                NotificationType = notificationType
            };
        }

        // Mock mode - log message instead of sending
        if (_config.MockMode || string.IsNullOrWhiteSpace(_config.WebhookUrl))
        {
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
            _logger.LogInformation(
                "MOCK SLACK MESSAGE - Channel: {Channel}, Type: {Type}\nPayload:\n{Payload}",
                _config.ChannelName,
                notificationType,
                json);

            return new SlackNotificationResult
            {
                Success = true,
                ResponseMessage = "MOCK_SUCCESS",
                NotificationType = notificationType
            };
        }

        // Real Slack webhook call with retry logic
        var retryCount = 0;
        var delay = _config.Retry.InitialDelayMs;

        while (retryCount <= _config.Retry.MaxRetries)
        {
            try
            {
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(_config.WebhookUrl, content, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Slack message sent successfully to {Channel}. Type: {Type}",
                        _config.ChannelName,
                        notificationType);

                    return new SlackNotificationResult
                    {
                        Success = true,
                        ResponseMessage = responseText,
                        NotificationType = notificationType
                    };
                }
                else
                {
                    _logger.LogWarning(
                        "Slack webhook returned error {StatusCode}: {Response}",
                        response.StatusCode,
                        responseText);

                    throw new HttpRequestException($"Slack webhook failed with status {response.StatusCode}: {responseText}");
                }
            }
            catch (Exception ex)
            {
                retryCount++;
                _logger.LogWarning(
                    ex,
                    "Failed to send Slack message (attempt {Attempt}/{MaxAttempts})",
                    retryCount,
                    _config.Retry.MaxRetries + 1);

                if (retryCount > _config.Retry.MaxRetries)
                {
                    _logger.LogError(
                        ex,
                        "Failed to send Slack message after {Attempts} attempts",
                        retryCount);

                    return new SlackNotificationResult
                    {
                        Success = false,
                        ErrorMessage = ex.Message,
                        NotificationType = notificationType
                    };
                }

                await Task.Delay(delay, cancellationToken);
                delay = (int)(delay * _config.Retry.BackoffMultiplier);
            }
        }

        return new SlackNotificationResult
        {
            Success = false,
            ErrorMessage = "Max retries exceeded",
            NotificationType = notificationType
        };
    }

    #region Slack Webhook Payload Classes

    private class SlackWebhookPayload
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("icon_emoji")]
        public string IconEmoji { get; set; } = string.Empty;

        [JsonPropertyName("blocks")]
        public List<SlackBlock> Blocks { get; set; } = new();
    }

    private class SlackBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SlackText? Text { get; set; }

        [JsonPropertyName("fields")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SlackText>? Fields { get; set; }

        [JsonPropertyName("elements")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<SlackText>? Elements { get; set; }
    }

    private class SlackText
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("emoji")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Emoji { get; set; }
    }

    #endregion
}
