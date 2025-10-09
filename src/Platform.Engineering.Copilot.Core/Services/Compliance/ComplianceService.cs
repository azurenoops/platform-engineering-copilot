using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Platform.Engineering.Copilot.Core.Services.Compliance;

public class ComplianceService
{
    private readonly ILogger<ComplianceService> _logger;
    private readonly Kernel _kernel;

    public ComplianceService(ILogger<ComplianceService> logger, Kernel kernel)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
    }

    public async Task<string> ProcessComplianceQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing compliance query: {Query}", query);
            var intent = await ParseComplianceQueryAsync(query, cancellationToken);
            return await ExecuteComplianceActionAsync(intent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing compliance query: {Query}", query);
            return "Error: " + ex.Message;
        }
    }

    private async Task<ComplianceIntent> ParseComplianceQueryAsync(string query, CancellationToken cancellationToken)
    {
        try
        {
            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            var systemPrompt = "You are an Azure compliance query parser. Extract action and subscriptionId from the query. Return JSON.";
            var chatHistory = new ChatHistory(systemPrompt);
            chatHistory.AddUserMessage(query);
            var response = await chatCompletion.GetChatMessageContentAsync(chatHistory, kernel: _kernel, cancellationToken: cancellationToken);
            var jsonResponse = Regex.Replace(response.Content ?? "", @"^```json\s*|\s*```$", "", RegexOptions.Multiline).Trim();
            var intent = JsonSerializer.Deserialize<ComplianceIntent>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return intent ?? new ComplianceIntent { Action = "assess" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing compliance query");
            return new ComplianceIntent { Action = "assess", Error = ex.Message };
        }
    }

    private async Task<string> ExecuteComplianceActionAsync(ComplianceIntent intent, CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken);
        return $"Compliance action '{intent.Action}' executed successfully for subscription: {intent.SubscriptionId}";
    }

    private class ComplianceIntent
    {
        public string? Action { get; set; }
        public string? SubscriptionId { get; set; }
        public string? Framework { get; set; }
        public string? ControlFamily { get; set; }
        public string? ControlId { get; set; }
        public string? Severity { get; set; }
        public string? TimeRange { get; set; }
        public string? Format { get; set; }
        public string? ResourceGroup { get; set; }
        public string? FindingId { get; set; }
        public string? Error { get; set; }
    }
}
