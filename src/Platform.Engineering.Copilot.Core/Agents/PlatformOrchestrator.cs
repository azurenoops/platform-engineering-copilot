using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Agents;

/// <summary>
/// Central orchestrator implementing two-tier routing per FR-001, FR-005, research.md §1:
/// <list type="number">
/// <item>Keyword fast-path — O(1) dictionary lookup for known keywords</item>
/// <item>LLM fallback — IChatClient-based intent classification when no keyword matches</item>
/// </list>
/// Direct targeting via "@agent" syntax bypasses intent analysis entirely.
/// Returns transparent routing explanations per Constitution Principle V.
/// </summary>
public class PlatformOrchestrator
{
    private readonly ILogger<PlatformOrchestrator> _logger;
    private readonly IChatClient? _chatClient;
    private readonly List<BaseAgent> _agents = [];
    private readonly Dictionary<string, BaseAgent> _agentIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BaseAgent> _keywordIndex = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<BaseAgent> Agents => _agents.AsReadOnly();

    public PlatformOrchestrator(
        ILogger<PlatformOrchestrator> logger,
        IChatClient? chatClient = null)
    {
        _logger = logger;
        _chatClient = chatClient;
    }

    /// <summary>
    /// Register an agent with the orchestrator. Indexes the agent's keywords for fast-path routing.
    /// </summary>
    public void RegisterAgent(BaseAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (_agentIndex.ContainsKey(agent.AgentId))
        {
            _logger.LogWarning("Agent '{AgentId}' already registered, skipping duplicate", agent.AgentId);
            return;
        }

        _agents.Add(agent);
        _agentIndex[agent.AgentId] = agent;

        foreach (var keyword in agent.Keywords)
        {
            var lower = keyword.ToLowerInvariant();
            if (!_keywordIndex.TryAdd(lower, agent))
            {
                _logger.LogWarning(
                    "Keyword '{Keyword}' already mapped to agent '{ExistingAgent}', skipping for '{NewAgent}'",
                    lower, _keywordIndex[lower].AgentId, agent.AgentId);
            }
        }

        _logger.LogInformation("Registered agent '{AgentId}' ({AgentName}) with {KeywordCount} keywords",
            agent.AgentId, agent.AgentName, agent.Keywords.Count);
    }

    /// <summary>
    /// Route a user message to the appropriate agent.
    /// Returns the routing result with the selected agent and explanation.
    /// </summary>
    public async Task<RoutingResult> RouteAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return RoutingResult.NoMatch("Empty message received.");
        }

        // 1. Direct targeting: @agentId or @agentName
        var directTarget = TryDirectTarget(message);
        if (directTarget != null)
        {
            return directTarget;
        }

        // 2. Keyword fast-path: O(1) lookup
        var keywordMatch = TryKeywordMatch(message);
        if (keywordMatch != null)
        {
            return keywordMatch;
        }

        // 3. LLM fallback: classify intent via IChatClient
        if (_chatClient != null)
        {
            var llmResult = await TryLlmClassification(message, cancellationToken);
            if (llmResult != null)
            {
                return llmResult;
            }
        }

        return RoutingResult.NoMatch(BuildUnrecognizedIntentMessage(message));
    }

    /// <summary>
    /// Build a helpful message when no agent matches the user's intent.
    /// Lists all available agents with descriptions and example commands (FR-001, T076).
    /// </summary>
    private string BuildUnrecognizedIntentMessage(string message)
    {
        if (_agents.Count == 0)
            return "No agents are currently registered. Please contact your administrator.";

        var agentList = string.Join("\n", _agents.Select(a =>
            $"  - **{a.AgentName}** (@{a.AgentId}): {a.Description}"));

        return $"I wasn't able to determine which agent should handle your request. " +
               $"Here are the available agents:\n{agentList}\n\n" +
               $"You can also use direct targeting by prefixing your message with @agentid " +
               $"(e.g., '@compliance run assessment'). " +
               $"Try rephrasing your request or specify the agent directly.";
    }

    /// <summary>
    /// Try to match a direct targeting pattern (@agent) in the message.
    /// FR-005: When user explicitly names an agent, bypass intent analysis.
    /// </summary>
    private RoutingResult? TryDirectTarget(string message)
    {
        // Look for @agentId or @agentName pattern at the start
        if (!message.TrimStart().StartsWith('@'))
            return null;

        var firstSpace = message.IndexOf(' ', message.IndexOf('@'));
        var targetName = firstSpace >= 0
            ? message[1..firstSpace].Trim()
            : message[1..].Trim();

        if (string.IsNullOrEmpty(targetName))
            return null;

        // Match against agent ID
        if (_agentIndex.TryGetValue(targetName, out var agentById))
        {
            _logger.LogInformation("Direct targeting: routing to '{AgentId}' via @{Target}",
                agentById.AgentId, targetName);
            return RoutingResult.DirectTarget(agentById,
                $"Routing directly to {agentById.AgentName} via @{targetName}.");
        }

        // Match against agent name (case-insensitive)
        var byName = _agents.FirstOrDefault(a =>
            a.AgentName.Equals(targetName, StringComparison.OrdinalIgnoreCase) ||
            a.AgentId.Replace("-", "").Equals(targetName.Replace("-", ""), StringComparison.OrdinalIgnoreCase));

        if (byName != null)
        {
            _logger.LogInformation("Direct targeting: routing to '{AgentId}' via @{Target}",
                byName.AgentId, targetName);
            return RoutingResult.DirectTarget(byName,
                $"Routing directly to {byName.AgentName} via @{targetName}.");
        }

        _logger.LogDebug("Direct targeting failed: no agent matches @{Target}", targetName);
        return null;
    }

    /// <summary>
    /// Try O(1) keyword lookup against the message words.
    /// Returns the first matched agent. If multiple agents match,
    /// selects the one with the most keyword hits.
    /// </summary>
    private RoutingResult? TryKeywordMatch(string message)
    {
        var words = message.ToLowerInvariant()
            .Split([' ', ',', '.', '?', '!', ':', ';', '\n', '\r', '\t'],
                StringSplitOptions.RemoveEmptyEntries);

        // Count keyword hits per agent
        var hitCounts = new Dictionary<BaseAgent, (int count, string firstKeyword)>();

        foreach (var word in words)
        {
            if (_keywordIndex.TryGetValue(word, out var agent))
            {
                if (hitCounts.TryGetValue(agent, out var existing))
                {
                    hitCounts[agent] = (existing.count + 1, existing.firstKeyword);
                }
                else
                {
                    hitCounts[agent] = (1, word);
                }
            }
        }

        if (hitCounts.Count == 0)
            return null;

        // Select agent with most hits; ties broken by registration order
        var best = hitCounts
            .OrderByDescending(kv => kv.Value.count)
            .First();

        var explanation = hitCounts.Count > 1
            ? $"Routing to {best.Key.AgentName} based on '{best.Value.firstKeyword}' keyword " +
              $"({best.Value.count} keyword matches). " +
              $"Other candidates: {string.Join(", ", hitCounts.Where(kv => kv.Key != best.Key).Select(kv => $"{kv.Key.AgentName} ({kv.Value.count} matches)"))}"
            : $"Routing to {best.Key.AgentName} based on '{best.Value.firstKeyword}' keyword.";

        _logger.LogInformation("Keyword routing: {Explanation}", explanation);

        return RoutingResult.KeywordMatch(best.Key, explanation);
    }

    /// <summary>
    /// Use IChatClient to classify the user's intent against agent descriptions.
    /// LLM fallback for messages that don't match any keyword.
    /// </summary>
    private async Task<RoutingResult?> TryLlmClassification(
        string message, CancellationToken cancellationToken)
    {
        try
        {
            var agentDescriptions = string.Join("\n", _agents.Select(a =>
                $"- {a.AgentId}: {a.Description}"));

            var classificationPrompt =
                $"""
                You are a routing classifier. Given a user message, determine which agent should handle it.
                
                Available agents:
                {agentDescriptions}
                
                User message: "{message}"
                
                Respond with ONLY the agent ID (e.g., "compliance") that best matches the user's intent.
                If no agent matches, respond with "none".
                """;

            var chatMessages = new List<ChatMessage>
            {
                new(ChatRole.User, classificationPrompt)
            };

            var response = await _chatClient!.GetResponseAsync(chatMessages, cancellationToken: cancellationToken);

            var agentId = response.Messages.LastOrDefault()?.Text?.Trim().Trim('"').ToLowerInvariant();

            if (agentId != null && agentId != "none" && _agentIndex.TryGetValue(agentId, out var agent))
            {
                _logger.LogInformation("LLM routing: classified message to agent '{AgentId}'", agent.AgentId);
                return RoutingResult.LlmClassification(agent,
                    $"Routing to {agent.AgentName} based on intent classification.");
            }

            _logger.LogDebug("LLM routing: no agent matched (response: '{Response}')", agentId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM classification failed, no routing result");
            return null;
        }
    }
}

/// <summary>
/// Result of the orchestrator's routing decision.
/// </summary>
public class RoutingResult
{
    /// <summary>The selected agent, if any.</summary>
    public BaseAgent? Agent { get; init; }

    /// <summary>Whether routing succeeded.</summary>
    public bool IsMatch => Agent != null;

    /// <summary>Human-readable explanation of the routing decision (transparent routing).</summary>
    public string Explanation { get; init; } = string.Empty;

    /// <summary>How the route was determined.</summary>
    public RoutingMethod Method { get; init; }

    public static RoutingResult DirectTarget(BaseAgent agent, string explanation)
        => new() { Agent = agent, Method = RoutingMethod.DirectTarget, Explanation = explanation };

    public static RoutingResult KeywordMatch(BaseAgent agent, string explanation)
        => new() { Agent = agent, Method = RoutingMethod.KeywordMatch, Explanation = explanation };

    public static RoutingResult LlmClassification(BaseAgent agent, string explanation)
        => new() { Agent = agent, Method = RoutingMethod.LlmClassification, Explanation = explanation };

    public static RoutingResult NoMatch(string explanation)
        => new() { Agent = null, Method = RoutingMethod.None, Explanation = explanation };
}

/// <summary>
/// How the orchestrator determined the route.
/// </summary>
public enum RoutingMethod
{
    /// <summary>No match found.</summary>
    None,

    /// <summary>User explicitly targeted an agent via @agent syntax.</summary>
    DirectTarget,

    /// <summary>Keyword fast-path O(1) dictionary lookup.</summary>
    KeywordMatch,

    /// <summary>IChatClient LLM-based intent classification.</summary>
    LlmClassification
}
