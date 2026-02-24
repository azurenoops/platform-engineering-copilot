using System.ClientModel;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Factory for creating <see cref="IChatClient"/> instances backed by Azure OpenAI.
/// <para>
/// Supports API key and managed identity authentication, plus Azure Government
/// endpoint detection (FR-002). Returns <c>null</c> when configuration is missing
/// or incomplete (FR-003) — enabling graceful degradation.
/// </para>
/// </summary>
public class AzureOpenAIChatClientFactory
{
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<AzureOpenAIChatClientFactory> _logger;

    public AzureOpenAIChatClientFactory(
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAIChatClientFactory> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Create an <see cref="IChatClient"/> from configuration.
    /// Returns <c>null</c> if Endpoint is empty or not configured.
    /// </summary>
    public IChatClient? CreateChatClient()
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint))
        {
            _logger.LogInformation(
                "Azure OpenAI Endpoint not configured — IChatClient will be null (fallback mode)");
            return null;
        }

        if (string.IsNullOrWhiteSpace(_options.DeploymentName))
        {
            _logger.LogWarning(
                "Azure OpenAI DeploymentName is empty — IChatClient will be null (fallback mode)");
            return null;
        }

        try
        {
            var endpointUri = new Uri(_options.Endpoint);
            var isGov = IsAzureGovernment(endpointUri);

            AzureOpenAIClient azureClient;

            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                // API key authentication
                _logger.LogInformation(
                    "Creating Azure OpenAI client with API key auth (Gov: {IsGov}, Deployment: {Deployment})",
                    isGov, _options.DeploymentName);

                var clientOptions = new AzureOpenAIClientOptions();
                if (isGov)
                {
                    clientOptions.Audience = AzureOpenAIAudience.AzureGovernment;
                }

                azureClient = new AzureOpenAIClient(
                    endpointUri,
                    new ApiKeyCredential(_options.ApiKey),
                    clientOptions);
            }
            else
            {
                // Managed identity authentication (DefaultAzureCredential)
                _logger.LogInformation(
                    "Creating Azure OpenAI client with managed identity auth (Gov: {IsGov}, Deployment: {Deployment})",
                    isGov, _options.DeploymentName);

                var clientOptions = new AzureOpenAIClientOptions();
                if (isGov)
                {
                    clientOptions.Audience = AzureOpenAIAudience.AzureGovernment;
                }

                azureClient = new AzureOpenAIClient(
                    endpointUri,
                    new DefaultAzureCredential(),
                    clientOptions);
            }

            // Get the OpenAI ChatClient for the deployment, then bridge to IChatClient
            ChatClient chatClient = azureClient.GetChatClient(_options.DeploymentName);
            IChatClient bridgedClient = chatClient.AsIChatClient();

            _logger.LogInformation(
                "Azure OpenAI IChatClient created successfully (Model: {ModelId})",
                _options.ModelId);

            return bridgedClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create Azure OpenAI IChatClient — falling back to null (Endpoint: {Endpoint})",
                _options.Endpoint);
            return null;
        }
    }

    /// <summary>
    /// Detect Azure Government endpoints by checking for ".us" in the host.
    /// </summary>
    public static bool IsAzureGovernment(Uri endpointUri)
    {
        return endpointUri.Host.Contains(".us", StringComparison.OrdinalIgnoreCase);
    }
}
