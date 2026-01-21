using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Configuration;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.State;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;
using Platform.Engineering.Copilot.Channels.Abstractions;
using Platform.Engineering.Copilot.State.Abstractions;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase.Agents;

/// <summary>
/// Main Knowledge Base Agent for RMF/STIG/DoD compliance knowledge queries.
/// Provides educational and informational content about compliance frameworks.
/// Enhanced with State and Channels integration for cross-agent coordination.
/// </summary>
public class KnowledgeBaseAgent : BaseAgent
{
    public override string AgentId => "knowledgebase";
    public override string AgentName => "KnowledgeBase Agent";
    public override string Description =>
        "Provides educational and informational content about NIST 800-53 controls, STIGs, " +
        "RMF process, FedRAMP requirements, and DoD impact levels. " +
        "Use for questions about what controls mean, how to implement them, and compliance guidance. " +
        "This is advisory-only - no environment scanning or changes are made.";

    protected override float Temperature => (float)_options.Temperature;
    protected override int MaxTokens => _options.MaxTokens;

    private readonly KnowledgeBaseStateAccessors _stateAccessors;
    private readonly KnowledgeBaseAgentOptions _options;
    private readonly IChannelManager? _channelManager;
    private readonly IStreamingHandler? _streamingHandler;

    public KnowledgeBaseAgent(
        IChatClient chatClient,
        ILogger<KnowledgeBaseAgent> logger,
        IOptions<KnowledgeBaseAgentOptions> options,
        KnowledgeBaseStateAccessors stateAccessors,
        NistControlExplainerTool nistControlExplainerTool,
        NistControlSearchTool nistControlSearchTool,
        StigExplainerTool stigExplainerTool,
        StigSearchTool stigSearchTool,
        RmfExplainerTool rmfExplainerTool,
        ImpactLevelTool impactLevelTool,
        FedRampTemplateTool fedRampTemplateTool,
        IAgentStateManager? agentStateManager = null,
        ISharedMemory? sharedMemory = null,
        IChannelManager? channelManager = null,
        IStreamingHandler? streamingHandler = null)
        : base(chatClient, logger, agentStateManager, sharedMemory)
    {
        _stateAccessors = stateAccessors ?? throw new ArgumentNullException(nameof(stateAccessors));
        _options = options?.Value ?? new KnowledgeBaseAgentOptions();
        _channelManager = channelManager;
        _streamingHandler = streamingHandler;

        // Register tools
        RegisterTool(nistControlExplainerTool);
        RegisterTool(nistControlSearchTool);
        RegisterTool(stigExplainerTool);
        RegisterTool(stigSearchTool);
        RegisterTool(rmfExplainerTool);
        RegisterTool(impactLevelTool);
        RegisterTool(fedRampTemplateTool);

        Logger.LogInformation("✅ Knowledge Base Agent initialized (Temperature: {Temperature}, MaxTokens: {MaxTokens}, " +
            "RAG: {EnableRag}, SemanticSearch: {SemanticSearch}, Tools: {ToolCount})",
            _options.Temperature, _options.MaxTokens,
            _options.EnableRag, _options.EnableSemanticSearch, RegisteredTools.Count);
    }

    /// <summary>
    /// Override ProcessAsync to add Knowledge Base-specific behavior with Channels integration.
    /// </summary>
    public override async Task<AgentResponse> ProcessAsync(
        AgentConversationContext context,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Notify via channel that knowledge base query is starting
        await NotifyChannelAsync(context.ConversationId, MessageType.AgentThinking,
            "Searching compliance knowledge base...", cancellationToken);

        try
        {
            // Analyze query to determine the type of knowledge being requested
            var queryType = AnalyzeQueryType(context.MessageHistory.LastOrDefault()?.Content ?? "");

            Logger.LogDebug("Knowledge base query type: {QueryType}", queryType);

            // Store the query for context
            await _stateAccessors.SetLastQueryAsync(context.ConversationId,
                context.MessageHistory.LastOrDefault()?.Content ?? "", queryType, cancellationToken);

            // Notify progress
            await NotifyChannelAsync(context.ConversationId, MessageType.ProgressUpdate,
                $"Looking up {queryType} information...", cancellationToken);

            // Call base implementation for actual processing
            var response = await base.ProcessAsync(context, cancellationToken);

            // Track the operation in state
            var duration = DateTime.UtcNow - startTime;
            await _stateAccessors.TrackKnowledgeBaseOperationAsync(
                context.ConversationId,
                queryType,
                context.MessageHistory.LastOrDefault()?.Content ?? "",
                response.Success,
                duration,
                cancellationToken);

            // Share relevant knowledge with other agents if useful
            if (response.Success && queryType is "nist_control" or "stig")
            {
                await _stateAccessors.ShareKnowledgeAsync(
                    context.ConversationId,
                    $"last_{queryType}",
                    new { query = context.MessageHistory.LastOrDefault()?.Content, result = response.Content },
                    cancellationToken);
            }

            // Notify completion via channel
            await NotifyChannelAsync(context.ConversationId, MessageType.AgentResponse,
                JsonSerializer.Serialize(new
                {
                    agentName = AgentName,
                    queryType,
                    success = response.Success,
                    durationMs = (int)duration.TotalMilliseconds,
                    toolsUsed = response.ToolsExecuted?.Select(t => t.ToolName).ToList() ?? new List<string>()
                }), cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Knowledge Base Agent failed");

            await NotifyChannelAsync(context.ConversationId, MessageType.Error,
                $"Knowledge base query failed: {ex.Message}", cancellationToken);

            return new AgentResponse
            {
                AgentId = AgentId,
                AgentName = AgentName,
                Content = $"Knowledge base query failed: {ex.Message}",
                Success = false
            };
        }
    }

    protected override string GetSystemPrompt()
    {
        var variables = new Dictionary<string, string>
        {
            ["ToolCount"] = RegisteredTools.Count.ToString(),
            ["Temperature"] = _options.Temperature.ToString(),
            ["MaxTokens"] = _options.MaxTokens.ToString(),
            ["RagEnabled"] = _options.EnableRag.ToString(),
            ["SemanticSearchEnabled"] = _options.EnableSemanticSearch.ToString()
        };

        var template = SystemPromptLoader.LoadFromType<KnowledgeBaseAgent>("KnowledgeBaseAgent.prompt.txt") ?? "";
        return SystemPromptLoader.ApplyVariables(template, variables);
    }

    /// <summary>
    /// Analyze the query to determine what type of knowledge is being requested.
    /// </summary>
    private static string AnalyzeQueryType(string query)
    {
        var lowerQuery = query.ToLowerInvariant();

        // NIST control patterns
        if (lowerQuery.Contains("nist") || 
            System.Text.RegularExpressions.Regex.IsMatch(query, @"\b[A-Z]{2}-\d+", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return "nist_control";
        }

        // STIG patterns
        if (lowerQuery.Contains("stig") || lowerQuery.Contains("v-") || 
            System.Text.RegularExpressions.Regex.IsMatch(query, @"\bV-\d+", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            return "stig";
        }

        // RMF patterns
        if (lowerQuery.Contains("rmf") || lowerQuery.Contains("risk management framework") ||
            lowerQuery.Contains("step 1") || lowerQuery.Contains("step 2") ||
            lowerQuery.Contains("ato") || lowerQuery.Contains("authorization"))
        {
            return "rmf";
        }

        // Impact level patterns
        if (lowerQuery.Contains("il2") || lowerQuery.Contains("il4") ||
            lowerQuery.Contains("il5") || lowerQuery.Contains("il6") ||
            lowerQuery.Contains("impact level"))
        {
            return "impact_level";
        }

        // FedRAMP patterns
        if (lowerQuery.Contains("fedramp") || lowerQuery.Contains("ssp") ||
            lowerQuery.Contains("sar") || lowerQuery.Contains("poa&m"))
        {
            return "fedramp";
        }

        return "general_knowledge";
    }

    /// <summary>
    /// Notify via channel with proper null checking.
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
