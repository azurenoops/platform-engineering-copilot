using System.ComponentModel.DataAnnotations;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Strongly-typed configuration for Azure OpenAI integration.
/// Binds to the "AzureOpenAI" section in appsettings.json.
/// <para>
/// Feature flag <see cref="AgentAIEnabled"/> defaults to <c>false</c> — system behaves
/// identically to pre-feature behavior until explicitly enabled.
/// </para>
/// </summary>
public class AzureOpenAIOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AzureOpenAI";

    /// <summary>Azure OpenAI endpoint URL (e.g., https://your-resource.openai.azure.com).</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>API key for Azure OpenAI. If empty, managed identity (DefaultAzureCredential) is used.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Deployment name (model deployment) for chat completions.</summary>
    public string DeploymentName { get; set; } = "gpt-4o";

    /// <summary>Model identifier for tracking and logging.</summary>
    public string ModelId { get; set; } = "gpt-4o";

    /// <summary>
    /// Feature flag: when <c>true</c>, agents send messages to Azure OpenAI for
    /// natural-language processing. When <c>false</c>, agents fall back to direct
    /// tool execution (pre-feature behavior).
    /// </summary>
    public bool AgentAIEnabled { get; set; } = false;

    /// <summary>
    /// Maximum number of tool-call rounds per request. The FunctionInvokingChatClient
    /// will stop after this many iterations, preventing runaway tool loops.
    /// </summary>
    [Range(1, 20, ErrorMessage = "MaxToolCallRounds must be between 1 and 20.")]
    public int MaxToolCallRounds { get; set; } = 5;

    /// <summary>
    /// LLM generation temperature (0.0 = deterministic, 2.0 = creative).
    /// Lower values produce more consistent, factual responses for compliance/security use cases.
    /// </summary>
    [Range(0.0, 2.0, ErrorMessage = "Temperature must be between 0.0 and 2.0.")]
    public float Temperature { get; set; } = 0.3f;

    /// <summary>
    /// Validates that DeploymentName is non-empty when Endpoint is set.
    /// Called during service registration.
    /// </summary>
    public IEnumerable<ValidationResult> Validate()
    {
        if (!string.IsNullOrWhiteSpace(Endpoint) && string.IsNullOrWhiteSpace(DeploymentName))
        {
            yield return new ValidationResult(
                "DeploymentName must be specified when Endpoint is set.",
                [nameof(DeploymentName)]);
        }

        if (MaxToolCallRounds < 1 || MaxToolCallRounds > 20)
        {
            yield return new ValidationResult(
                "MaxToolCallRounds must be between 1 and 20.",
                [nameof(MaxToolCallRounds)]);
        }

        if (Temperature < 0.0f || Temperature > 2.0f)
        {
            yield return new ValidationResult(
                "Temperature must be between 0.0 and 2.0.",
                [nameof(Temperature)]);
        }
    }
}
