using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services.Notifications;

/// <summary>
/// Service for sending notifications to Microsoft Teams via incoming webhooks
/// Formats messages as Adaptive Cards for rich formatting
/// </summary>
public class TeamsNotificationService : ITeamsNotificationService
{
    private readonly ILogger<TeamsNotificationService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string? _webhookUrl;
    private readonly bool _isEnabled;

    public TeamsNotificationService(
        ILogger<TeamsNotificationService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _webhookUrl = configuration["Teams:WebhookUrl"];
        _isEnabled = !string.IsNullOrWhiteSpace(_webhookUrl);

        if (!_isEnabled)
        {
            _logger.LogWarning("Teams notifications are disabled. Configure 'Teams:WebhookUrl' to enable.");
        }
    }

    public async Task SendOnboardingApprovedNotificationAsync(
        string missionName,
        string missionOwner,
        string command,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var facts = new Dictionary<string, string>
        {
            ["Mission Name"] = missionName,
            ["Mission Owner"] = missionOwner,
            ["Command"] = command,
            ["Request ID"] = requestId,
            ["Status"] = "Approved - Generation Starting"
        };

        await SendAdaptiveCardAsync(
            title: "🚀 Onboarding Request Approved",
            text: $"Mission **{missionName}** has been approved and infrastructure generation is starting.",
            color: "good",
            facts: facts,
            cancellationToken: cancellationToken);
    }

    public async Task SendTemplateGenerationStartedNotificationAsync(
        string missionName,
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var facts = new Dictionary<string, string>
        {
            ["Mission Name"] = missionName,
            ["Request ID"] = requestId,
            ["Stage"] = "Template Generation",
            ["Status"] = "In Progress"
        };

        await SendAdaptiveCardAsync(
            title: "⚙️ Template Generation Started",
            text: $"Generating infrastructure templates for **{missionName}**...",
            color: "warning",
            facts: facts,
            cancellationToken: cancellationToken);
    }

    public async Task SendTemplateGenerationCompletedNotificationAsync(
        string missionName,
        string requestId,
        int filesGenerated,
        string summary,
        CancellationToken cancellationToken = default)
    {
        var facts = new Dictionary<string, string>
        {
            ["Mission Name"] = missionName,
            ["Request ID"] = requestId,
            ["Files Generated"] = filesGenerated.ToString(),
            ["Summary"] = summary,
            ["Stage"] = "Template Generation",
            ["Status"] = "Completed"
        };

        await SendAdaptiveCardAsync(
            title: "✅ Template Generation Completed",
            text: $"Successfully generated **{filesGenerated} files** for **{missionName}**.",
            color: "good",
            facts: facts,
            cancellationToken: cancellationToken);
    }

    public async Task SendDeploymentStartedNotificationAsync(
        string missionName,
        string requestId,
        string environment,
        CancellationToken cancellationToken = default)
    {
        var facts = new Dictionary<string, string>
        {
            ["Mission Name"] = missionName,
            ["Request ID"] = requestId,
            ["Environment"] = environment,
            ["Stage"] = "Azure Deployment",
            ["Status"] = "In Progress"
        };

        await SendAdaptiveCardAsync(
            title: "☁️ Infrastructure Deployment Started",
            text: $"Deploying infrastructure for **{missionName}** to Azure...",
            color: "warning",
            facts: facts,
            cancellationToken: cancellationToken);
    }

    public async Task SendDeploymentCompletedNotificationAsync(
        string missionName,
        string requestId,
        string environment,
        string resourceGroupName,
        string subscriptionId,
        bool success,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var facts = new Dictionary<string, string>
        {
            ["Mission Name"] = missionName,
            ["Request ID"] = requestId,
            ["Environment"] = environment,
            ["Resource Group"] = resourceGroupName,
            ["Subscription ID"] = subscriptionId,
            ["Status"] = success ? "Completed Successfully" : "Failed"
        };

        if (!success && !string.IsNullOrWhiteSpace(errorMessage))
        {
            facts["Error"] = errorMessage;
        }

        await SendAdaptiveCardAsync(
            title: success ? "✅ Infrastructure Deployment Completed" : "❌ Infrastructure Deployment Failed",
            text: success
                ? $"Successfully deployed infrastructure for **{missionName}** to Azure."
                : $"Deployment failed for **{missionName}**. Check logs for details.",
            color: success ? "good" : "attention",
            facts: facts,
            cancellationToken: cancellationToken);
    }

    public async Task SendDeploymentFailedNotificationAsync(
        string missionName,
        string requestId,
        string stage,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var facts = new Dictionary<string, string>
        {
            ["Mission Name"] = missionName,
            ["Request ID"] = requestId,
            ["Failed Stage"] = stage,
            ["Error"] = errorMessage,
            ["Status"] = "Failed"
        };

        await SendAdaptiveCardAsync(
            title: "❌ Deployment Failed",
            text: $"Deployment failed for **{missionName}** during **{stage}** stage.",
            color: "attention",
            facts: facts,
            cancellationToken: cancellationToken);
    }

    public async Task SendCustomNotificationAsync(
        string title,
        string message,
        string? color = null,
        Dictionary<string, string>? facts = null,
        CancellationToken cancellationToken = default)
    {
        await SendAdaptiveCardAsync(
            title: title,
            text: message,
            color: color ?? "default",
            facts: facts,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Send an Adaptive Card to Teams webhook
    /// </summary>
    private async Task SendAdaptiveCardAsync(
        string title,
        string text,
        string color,
        Dictionary<string, string>? facts = null,
        CancellationToken cancellationToken = default)
    {
        if (!_isEnabled)
        {
            _logger.LogDebug("Teams notifications disabled, skipping: {Title}", title);
            return;
        }

        try
        {
            var card = BuildAdaptiveCard(title, text, color, facts);
            var payload = new
            {
                type = "message",
                attachments = new[]
                {
                    new
                    {
                        contentType = "application/vnd.microsoft.card.adaptive",
                        content = card
                    }
                }
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var httpClient = _httpClientFactory.CreateClient();
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(_webhookUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Teams notification sent successfully: {Title}", title);
            }
            else
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Teams notification failed with status {StatusCode}: {Response}",
                    response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Teams notification: {Title}", title);
        }
    }

    /// <summary>
    /// Build Adaptive Card JSON structure
    /// </summary>
    private object BuildAdaptiveCard(
        string title,
        string text,
        string color,
        Dictionary<string, string>? facts = null)
    {
        var bodyElements = new List<object>
        {
            new
            {
                type = "TextBlock",
                text = title,
                weight = "Bolder",
                size = "Large",
                wrap = true
            },
            new
            {
                type = "TextBlock",
                text = text,
                wrap = true,
                spacing = "Medium"
            }
        };

        // Add facts if provided
        if (facts != null && facts.Count > 0)
        {
            var factSet = new List<object>();
            foreach (var fact in facts)
            {
                factSet.Add(new
                {
                    title = fact.Key,
                    value = fact.Value
                });
            }

            bodyElements.Add(new
            {
                type = "FactSet",
                facts = factSet,
                spacing = "Medium"
            });
        }

        // Add timestamp
        bodyElements.Add(new
        {
            type = "TextBlock",
            text = $"🕐 {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
            size = "Small",
            color = "Accent",
            spacing = "Medium"
        });

        return new
        {
            type = "AdaptiveCard",
            version = "1.4",
            schema = "http://adaptivecards.io/schemas/adaptive-card.json",
            body = bodyElements,
            msteams = new
            {
                width = "Full"
            },
            // Add color accent bar
            style = color switch
            {
                "good" => "emphasis",
                "attention" => "attention",
                "warning" => "warning",
                _ => "default"
            }
        };
    }
}
