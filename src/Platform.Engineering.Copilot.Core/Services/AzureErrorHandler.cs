using System.Net;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Plain-language explanations for Azure API failures per FR-067.
/// Provides troubleshooting suggestions, retry options, and exponential backoff for rate limiting.
/// </summary>
public class AzureErrorHandler
{
    private readonly ILogger<AzureErrorHandler> _logger;

    public AzureErrorHandler(ILogger<AzureErrorHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Convert an Azure API exception into a plain-language error with troubleshooting steps.
    /// </summary>
    public AzureErrorInfo HandleError(Exception exception, string operation)
    {
        _logger.LogWarning(exception, "Azure API error during {Operation}", operation);

        return exception switch
        {
            Azure.RequestFailedException rfe => HandleRequestFailed(rfe, operation),
            HttpRequestException hre => HandleHttpError(hre, operation),
            TaskCanceledException => new AzureErrorInfo
            {
                Operation = operation,
                ErrorCode = "TIMEOUT",
                PlainLanguage = $"The operation '{operation}' timed out. Azure may be experiencing high load.",
                TroubleshootingSteps = ["Wait a few minutes and try again", "Check Azure status at status.azure.com", "Try reducing the scope of the operation"],
                IsRetryable = true,
                SuggestedRetryDelayMs = 5000
            },
            UnauthorizedAccessException => new AzureErrorInfo
            {
                Operation = operation,
                ErrorCode = "UNAUTHORIZED",
                PlainLanguage = $"You don't have permission to perform '{operation}'. Your CAC/PIV session or PIM activation may have expired.",
                TroubleshootingSteps = ["Re-authenticate with your CAC/PIV card", "Activate the required PIM role", "Verify your Azure RBAC assignments"],
                IsRetryable = false
            },
            _ => new AzureErrorInfo
            {
                Operation = operation,
                ErrorCode = "UNKNOWN",
                PlainLanguage = $"An unexpected error occurred during '{operation}': {exception.Message}",
                TroubleshootingSteps = ["Check Azure service health", "Review the error details", "Contact your administrator if the problem persists"],
                IsRetryable = false
            }
        };
    }

    private static AzureErrorInfo HandleRequestFailed(Azure.RequestFailedException rfe, string operation)
    {
        var statusCode = (HttpStatusCode)rfe.Status;
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new AzureErrorInfo
            {
                Operation = operation,
                ErrorCode = $"HTTP_{rfe.Status}",
                PlainLanguage = $"Access denied for '{operation}'. Your credentials may have expired or you lack the required permissions.",
                TroubleshootingSteps = ["Re-authenticate with your CAC/PIV card", "Activate the required PIM role (Read or Write)", "Verify RBAC assignments on the target subscription/resource group"],
                IsRetryable = false
            },
            HttpStatusCode.NotFound => new AzureErrorInfo
            {
                Operation = operation,
                ErrorCode = "NOT_FOUND",
                PlainLanguage = $"The Azure resource for '{operation}' was not found. It may have been deleted or the ID is incorrect.",
                TroubleshootingSteps = ["Verify the subscription ID and resource group name", "Check if the resource exists in the Azure Portal", "Ensure you're targeting the correct environment"],
                IsRetryable = false
            },
            HttpStatusCode.TooManyRequests => new AzureErrorInfo
            {
                Operation = operation,
                ErrorCode = "RATE_LIMITED",
                PlainLanguage = $"Azure is rate-limiting requests for '{operation}'. Too many API calls in a short period.",
                TroubleshootingSteps = ["Wait for the suggested retry period", "Reduce the scope of your request", "Consider batching operations"],
                IsRetryable = true,
                SuggestedRetryDelayMs = GetRetryAfterMs(rfe)
            },
            HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable => new AzureErrorInfo
            {
                Operation = operation,
                ErrorCode = $"AZURE_ERROR_{rfe.Status}",
                PlainLanguage = $"Azure is experiencing issues processing '{operation}'. This is typically temporary.",
                TroubleshootingSteps = ["Wait a few minutes and retry", "Check Azure status at status.azure.com", "Try a different Azure region if applicable"],
                IsRetryable = true,
                SuggestedRetryDelayMs = 10000
            },
            _ => new AzureErrorInfo
            {
                Operation = operation,
                ErrorCode = $"HTTP_{rfe.Status}",
                PlainLanguage = $"Azure returned an error ({rfe.Status}) during '{operation}': {rfe.Message}",
                TroubleshootingSteps = ["Review the error code and message", "Check Azure documentation for this error", "Contact support if the issue persists"],
                IsRetryable = rfe.Status >= 500
            }
        };
    }

    private static AzureErrorInfo HandleHttpError(HttpRequestException hre, string operation)
    {
        return new AzureErrorInfo
        {
            Operation = operation,
            ErrorCode = "NETWORK_ERROR",
            PlainLanguage = $"Network error during '{operation}': Unable to reach Azure services.",
            TroubleshootingSteps = ["Check your network connectivity", "Verify proxy/firewall settings allow Azure traffic", "Ensure Azure Government endpoints are reachable"],
            IsRetryable = true,
            SuggestedRetryDelayMs = 3000
        };
    }

    private static int GetRetryAfterMs(Azure.RequestFailedException rfe)
    {
        // Try to extract Retry-After header value
        var response = rfe.GetRawResponse();
        if (response is not null && response.Headers.TryGetValue("Retry-After", out var retryAfter)
            && int.TryParse(retryAfter, out var seconds))
        {
            return seconds * 1000;
        }
        return 30000; // Default 30s for rate limiting
    }

    /// <summary>
    /// Calculate exponential backoff delay for retries.
    /// </summary>
    public static int CalculateBackoffMs(int attempt, int baseDelayMs = 1000, int maxDelayMs = 60000)
    {
        var delay = Math.Min(baseDelayMs * (int)Math.Pow(2, attempt), maxDelayMs);
        // Add jitter (±25%)
        var jitter = Random.Shared.Next(-delay / 4, delay / 4);
        return Math.Max(100, delay + jitter);
    }
}

/// <summary>
/// Structured Azure error information with plain-language explanation.
/// </summary>
public class AzureErrorInfo
{
    public string Operation { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = string.Empty;
    public string PlainLanguage { get; set; } = string.Empty;
    public string[] TroubleshootingSteps { get; set; } = [];
    public bool IsRetryable { get; set; }
    public int? SuggestedRetryDelayMs { get; set; }
}
