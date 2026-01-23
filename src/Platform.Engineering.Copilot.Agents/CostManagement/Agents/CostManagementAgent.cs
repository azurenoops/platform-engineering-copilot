using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.CostManagement.Configuration;
using Platform.Engineering.Copilot.Agents.CostManagement.State;
using Platform.Engineering.Copilot.Agents.CostManagement.Tools;
using Platform.Engineering.Copilot.Channels.Abstractions;
using Platform.Engineering.Copilot.State.Abstractions;

namespace Platform.Engineering.Copilot.Agents.CostManagement.Agents;

/// <summary>
/// Main Cost Management Agent for Azure cost analysis, optimization, budgeting, and forecasting.
/// Enhanced with State and Channels integration for cross-agent coordination.
/// </summary>
public class CostManagementAgent : BaseAgent
{
    public override string AgentId => "cost-management";
    public override string AgentName => "Cost Management Agent";
    public override string Description =>
        "Handles Azure cost analysis, optimization recommendations, budget monitoring, " +
        "cost forecasting, anomaly detection, and scenario modeling. " +
        "Can analyze spend, identify savings opportunities, and project future costs.";

    protected override float Temperature => (float)_options.Temperature;
    protected override int MaxTokens => _options.MaxTokens;

    private readonly CostManagementStateAccessors _stateAccessors;
    private readonly CostManagementAgentOptions _options;
    private readonly IChannelManager? _channelManager;
    private readonly IStreamingHandler? _streamingHandler;

    public CostManagementAgent(
        IChatClient chatClient,
        ILogger<CostManagementAgent> logger,
        IOptions<CostManagementAgentOptions> options,
        CostManagementStateAccessors stateAccessors,
        CostAnalysisTool costAnalysisTool,
        CostOptimizationTool costOptimizationTool,
        BudgetManagementTool budgetManagementTool,
        CostForecastTool costForecastTool,
        CostScenarioTool costScenarioTool,
        CostAnomalyTool costAnomalyTool,
        IAgentStateManager? agentStateManager = null,
        ISharedMemory? sharedMemory = null,
        IChannelManager? channelManager = null,
        IStreamingHandler? streamingHandler = null)
        : base(chatClient, logger, agentStateManager, sharedMemory)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _options = options?.Value ?? new CostManagementAgentOptions();
        _channelManager = channelManager;
        _streamingHandler = streamingHandler;

        // Register tools
        RegisterTool(costAnalysisTool);
        RegisterTool(costOptimizationTool);
        RegisterTool(budgetManagementTool);
        RegisterTool(costForecastTool);
        RegisterTool(costScenarioTool);
        RegisterTool(costAnomalyTool);

        Logger.LogInformation("✅ Cost Management Agent initialized (Temperature: {Temperature}, MaxTokens: {MaxTokens}, " +
            "Currency: {Currency}, AnomalyDetection: {AnomalyDetection}, BudgetMonitoring: {BudgetMonitoring})",
            _options.Temperature, _options.MaxTokens, _options.DefaultCurrency,
            _options.EnableAnomalyDetection, _options.EnableBudgetMonitoring);
    }

    /// <summary>
    /// Override ProcessAsync to add Cost Management-specific behavior with Channels integration.
    /// </summary>
    public override async Task<AgentResponse> ProcessAsync(
        AgentConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Notify via channel that cost analysis is starting
        await NotifyChannelAsync(context.ConversationId, MessageType.AgentThinking,
            "Analyzing cost management request...", cancellationToken);

        try
        {
            // Check for subscription context in shared memory
            var currentSubscription = await _stateAccessors.GetCurrentSubscriptionAsync(
                context.ConversationId, cancellationToken);

            if (!string.IsNullOrEmpty(currentSubscription))
            {
                Logger.LogDebug("Using subscription from context: {SubscriptionId}", currentSubscription);
            }
            else if (!string.IsNullOrEmpty(_options.DefaultSubscriptionId))
            {
                // Store default subscription in context
                await _stateAccessors.SetCurrentSubscriptionAsync(
                    context.ConversationId, _options.DefaultSubscriptionId, null, cancellationToken);
                Logger.LogDebug("Using default subscription: {SubscriptionId}", _options.DefaultSubscriptionId);
            }

            // Notify progress
            await NotifyChannelAsync(context.ConversationId, MessageType.ProgressUpdate,
                "Executing cost analysis...", cancellationToken);

            // Call base implementation for actual processing
            var response = await base.ProcessAsync(context, cancellationToken);

            // Track the operation in state
            var duration = DateTime.UtcNow - startTime;
            await _stateAccessors.TrackCostAnalysisOperationAsync(
                context.ConversationId,
                "cost-analysis",
                currentSubscription ?? _options.DefaultSubscriptionId ?? "unknown",
                0m, // Total cost from response would go here
                0m, // Potential savings from response would go here
                duration,
                cancellationToken);

            // Notify completion via channel
            await NotifyChannelAsync(context.ConversationId, MessageType.AgentResponse,
                JsonSerializer.Serialize(new
                {
                    agentName = AgentName,
                    success = response.Success,
                    durationMs = (int)duration.TotalMilliseconds,
                    toolsUsed = response.ToolsExecuted?.Select(t => t.ToolName).ToList() ?? new List<string>()
                }), cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Cost Management Agent failed");

            await NotifyChannelAsync(context.ConversationId, MessageType.Error,
                $"Cost analysis failed: {ex.Message}", cancellationToken);

            return new AgentResponse
            {
                AgentId = AgentId,
                AgentName = AgentName,
                Content = $"Cost analysis failed: {ex.Message}",
                Success = false
            };
        }
    }

    protected override string GetSystemPrompt()
    {
        var subscriptionInfo = !string.IsNullOrEmpty(_options.DefaultSubscriptionId)
            ? $@"
## Default Configuration
- **Default Subscription ID**: {_options.DefaultSubscriptionId}
- **Default Currency**: {_options.DefaultCurrency}
- **Default Timeframe**: {_options.DefaultTimeframe}
- When users don't specify a subscription, automatically use the default subscription ID
- ALWAYS use the default subscription when available unless user explicitly specifies a different one"
            : $@"
## Configuration
- **Default Currency**: {_options.DefaultCurrency}
- **Default Timeframe**: {_options.DefaultTimeframe}
- No default subscription is configured
- Ask user for subscription ID when needed";

        var featureInfo = GetFeatureInfo();

        var variables = new Dictionary<string, string>
        {
            ["FeatureInfo"] = featureInfo,
            ["SubscriptionInfo"] = subscriptionInfo,
            ["DefaultCurrency"] = _options.DefaultCurrency,
            ["AnomalyThreshold"] = _options.CostManagement.AnomalyThresholdPercentage.ToString(),
            ["MinimumSavings"] = _options.CostManagement.MinimumSavingsThreshold.ToString(),
            ["RefreshInterval"] = _options.CostManagement.RefreshIntervalMinutes.ToString(),
            ["ForecastDays"] = _options.Forecasting.ForecastDays.ToString()
        };

        var template = SystemPromptLoader.LoadFromType<CostManagementAgent>("CostManagementAgent.prompt.txt") ?? "";
        return SystemPromptLoader.ApplyVariables(template, variables);
    }

    private string GetFeatureInfo()
    {
        var features = new List<string>();

        if (_options.EnableAnomalyDetection)
            features.Add("- **Anomaly Detection**: Enabled (threshold: " +
                $"{_options.CostManagement.AnomalyThresholdPercentage}%)");

        if (_options.EnableOptimizationRecommendations)
            features.Add("- **Optimization Recommendations**: Enabled (min savings: " +
                $"{_options.DefaultCurrency} {_options.CostManagement.MinimumSavingsThreshold})");

        if (_options.EnableBudgetMonitoring)
            features.Add("- **Budget Monitoring**: Enabled (alert thresholds: " +
                $"{string.Join(", ", _options.Budgets.DefaultAlertThresholds)}%)");

        if (_options.EnableCostForecasting)
            features.Add("- **Cost Forecasting**: Enabled (period: " +
                $"{_options.Forecasting.ForecastDays} days, seasonality: " +
                $"{(_options.Forecasting.EnableSeasonalityDetection ? "enabled" : "disabled")})");

        if (!features.Any())
            return "";

        return $@"

## Enabled Features
{string.Join("\n", features)}";
    }

    /// <summary>
    /// Helper to send notifications via channel manager if available.
    /// </summary>
    private async Task NotifyChannelAsync(
        string conversationId,
        MessageType messageType,
        string content,
        CancellationToken cancellationToken)
    {
        if (_channelManager == null) return;

        try
        {
            var message = new ChannelMessage
            {
                ConversationId = conversationId,
                Type = messageType,
                Content = content,
                AgentType = AgentId,
                Timestamp = DateTime.UtcNow
            };
            await _channelManager.SendToConversationAsync(conversationId, message, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to send channel notification: {MessageType}", messageType);
        }
    }
}
