using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Agents.Orchestration;

/// <summary>
/// Strategy for selecting which agent should handle a request.
/// Uses fast-path detection for unambiguous requests, falls back to LLM for complex cases.
/// </summary>
public class PlatformSelectionStrategy
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<PlatformSelectionStrategy> _logger;

    public PlatformSelectionStrategy(
        IChatClient chatClient,
        ILogger<PlatformSelectionStrategy> logger)
    {
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Select the best agent for the given request
    /// </summary>
    public async Task<BaseAgent?> SelectAgentAsync(
        IReadOnlyList<BaseAgent> agents,
        string userMessage,
        AgentConversationContext context,
        CancellationToken cancellationToken = default)
    {
        // Fast-path: Check for unambiguous patterns
        var fastPathAgent = DetectFastPathAgent(userMessage, agents);
        if (fastPathAgent != null)
        {
            _logger.LogInformation("⚡ Fast-path selection: {AgentName}", fastPathAgent.Name);
            return fastPathAgent;
        }

        // Check if we have a handoff target from previous response
        var lastResponse = context.PreviousResponses.LastOrDefault();
        if (lastResponse?.RequiresHandoff == true && !string.IsNullOrEmpty(lastResponse.HandoffTarget))
        {
            var handoffAgent = agents.FirstOrDefault(a =>
                a.Name.Equals(lastResponse.HandoffTarget, StringComparison.OrdinalIgnoreCase));
            if (handoffAgent != null)
            {
                _logger.LogInformation("🔄 Handoff selection: {AgentName}", handoffAgent.Name);
                return handoffAgent;
            }
        }

        // LLM-based selection for complex requests
        var llmSelectedAgent = await SelectWithLLMAsync(userMessage, agents, context, cancellationToken);
        if (llmSelectedAgent != null)
        {
            _logger.LogInformation("🤖 LLM selection: {AgentName}", llmSelectedAgent.Name);
            return llmSelectedAgent;
        }

        // Default fallback
        var defaultAgent = agents.FirstOrDefault();
        _logger.LogWarning("⚠️ Fallback to default agent: {AgentName}", defaultAgent?.Name ?? "none");
        return defaultAgent;
    }

    /// <summary>
    /// Fast-path detection for unambiguous single-agent requests
    /// </summary>
    private BaseAgent? DetectFastPathAgent(string message, IReadOnlyList<BaseAgent> agents)
    {
        var lower = message.ToLowerInvariant();

        // Discovery subscription patterns - CHECK FIRST before Configuration
        // "List subscriptions", "my subscriptions", "show subscriptions" = DISCOVERY (listing resources)
        // "Set my subscription", "use subscription X" = CONFIGURATION (changing settings)
        if (lower.Contains("list") && lower.Contains("subscription"))
            return agents.FirstOrDefault(a => a.Name.Contains("Discovery", StringComparison.OrdinalIgnoreCase));
        if (lower.Contains("my subscriptions") || lower.Contains("show subscriptions") ||
            lower.Contains("available subscriptions") || lower.Contains("all subscriptions") ||
            (lower.Contains("what") && lower.Contains("subscriptions")))
            return agents.FirstOrDefault(a => a.Name.Contains("Discovery", StringComparison.OrdinalIgnoreCase));

        // Configuration patterns - route to Configuration Agent (handles subscription settings)
        // These are SETTING operations, not listing operations
        if (lower.Contains("set my subscription") || lower.Contains("set subscription to") ||
            lower.Contains("use subscription") || lower.Contains("configure subscription") ||
            lower.Contains("my subscription is") || lower.Contains("switch to subscription") ||
            lower.Contains("change subscription") || lower.Contains("default subscription") ||
            lower.Contains("show my config") || lower.Contains("current subscription") ||
            lower.Contains("what is my subscription") || lower.Contains("what's my subscription"))
            return agents.FirstOrDefault(a => a.Name.Contains("Configuration", StringComparison.OrdinalIgnoreCase));

        // Infrastructure patterns - CHECK FIRST to prioritize template generation over compliance keywords
        // When user asks for "template with compliance" or "AKS with NIST", the PRIMARY intent is infrastructure
        if (lower.Contains("generate") && (lower.Contains("template") || lower.Contains("bicep") || lower.Contains("terraform")))
            return agents.FirstOrDefault(a => a.Name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        
        // Template retrieval/review patterns - route to Infrastructure Agent
        if ((lower.Contains("review") || lower.Contains("show") || lower.Contains("display") || lower.Contains("get")) &&
            (lower.Contains("template") || lower.Contains("file") || lower.Contains("bicep") || lower.Contains("generated")))
            return agents.FirstOrDefault(a => a.Name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        
        if (lower.Contains("aks") || lower.Contains("kubernetes") || lower.Contains("container service"))
            return agents.FirstOrDefault(a => a.Name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        
        if (lower.Contains("bicep") || lower.Contains("terraform") ||
            lower.Contains("arm template") || lower.Contains("deploy resource") ||
            lower.Contains("create resource") || lower.Contains("delete resource") ||
            lower.Contains("generate template") || lower.Contains("infrastructure as code") ||
            lower.Contains("network design") || lower.Contains("vnet") ||
            lower.Contains("virtual network") || lower.Contains("production template") ||
            lower.Contains("best practices") && (lower.Contains("template") || lower.Contains("aks") || lower.Contains("infrastructure")))
            return agents.FirstOrDefault(a => a.Name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));

        // Knowledge patterns - CHECK BEFORE COMPLIANCE to prioritize educational queries
        // When user asks "What is NIST control X?" they want explanation, not compliance scan
        // Use specific patterns to avoid matching cost/spending questions like "What is my monthly spending?"
        if (lower.Contains("explain") || lower.Contains("tell me about") ||
            lower.Contains("stig") || lower.Contains("cci") ||
            (lower.Contains("what is") && (lower.Contains("nist") || lower.Contains("control") || lower.Contains("rmf") || 
             lower.Contains("stig") || lower.Contains("cci") || lower.Contains("framework"))) ||
            (lower.Contains("what does") && (lower.Contains("control") || lower.Contains("family"))) ||
            (lower.Contains("how does") && (lower.Contains("compliance") || lower.Contains("nist") || lower.Contains("control"))) ||
            (lower.Contains("nist") && (lower.Contains("control") || lower.Contains("family"))) ||
            (lower.Contains("family") && lower.Contains("control")) ||
            lower.Contains("rmf") || lower.Contains("risk management framework"))
            return agents.FirstOrDefault(a => a.Name.Contains("Knowledge", StringComparison.OrdinalIgnoreCase));

        // Compliance patterns - only if NOT infrastructure-related and NOT educational query
        if (lower.Contains("compliance scan") || lower.Contains("scan for compliance") ||
            lower.Contains("ssp") || lower.Contains("sar") || lower.Contains("poa&m") ||
            lower.Contains("poam") || lower.Contains("fedramp") ||
            lower.Contains("scan for security") || lower.Contains("ato ") ||
            lower.Contains("authority to operate") || lower.Contains("security assessment") ||
            lower.Contains("remediate finding") || lower.Contains("remediation") ||
            (lower.Contains("nist") && !lower.Contains("template") && !lower.Contains("generate") && !lower.Contains("aks")) ||
            (lower.Contains("compliance") && !lower.Contains("template") && !lower.Contains("generate") && !lower.Contains("aks")))
            return agents.FirstOrDefault(a => a.Name.Contains("Compliance", StringComparison.OrdinalIgnoreCase));

        // Cost patterns
        if (lower.Contains("cost") || lower.Contains("budget") ||
            lower.Contains("spending") || lower.Contains("price") ||
            lower.Contains("expense") || lower.Contains("billing") ||
            lower.Contains("cost optimization") || lower.Contains("save money"))
            return agents.FirstOrDefault(a => a.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase));

        // Discovery patterns
        if ((lower.Contains("list") && (lower.Contains("resource") || lower.Contains("vm") || lower.Contains("storage") || 
             lower.Contains("subscription") || lower.Contains("azure subscription"))) ||
            lower.Contains("list subscriptions") || lower.Contains("my subscriptions") ||
            lower.Contains("available subscriptions") || lower.Contains("show subscriptions") ||
            lower.Contains("find resource") || lower.Contains("search resource") ||
            lower.Contains("find all") || lower.Contains("find storage") ||
            lower.Contains("inventory") || lower.Contains("what resources") ||
            lower.Contains("show me all") || lower.Contains("get all") ||
            lower.Contains("discover") && !lower.Contains("compliance"))
            return agents.FirstOrDefault(a => a.Name.Contains("Discovery", StringComparison.OrdinalIgnoreCase));

        return null;
    }

    /// <summary>
    /// Use LLM to select the appropriate agent for complex requests
    /// </summary>
    private async Task<BaseAgent?> SelectWithLLMAsync(
        string message,
        IReadOnlyList<BaseAgent> agents,
        AgentConversationContext context,
        CancellationToken cancellationToken)
    {
        if (!agents.Any()) return null;

        var agentDescriptions = string.Join("\n", agents.Select(a =>
            $"- {a.Name}: {a.Description}"));

        var contextInfo = "";
        if (context.PreviousResponses.Any())
        {
            var lastAgent = context.PreviousResponses.Last().AgentName;
            contextInfo = $"\n\nLast agent used: {lastAgent}";
        }

        var prompt = $"""
            Select the most appropriate agent for this request. Respond with ONLY the agent name, nothing else.
            
            User request: {message}
            {contextInfo}
            
            Available agents:
            {agentDescriptions}
            
            Agent name:
            """;

        try
        {
            var response = await _chatClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt)],
                new ChatOptions { Temperature = 0.1f },
                cancellationToken);

            // ChatResponse.Text gets the combined text from all messages
            var selectedName = response.Text?.Trim();
            if (string.IsNullOrEmpty(selectedName)) return null;

            // Clean up the response (remove quotes, punctuation, etc.)
            selectedName = selectedName.Trim('"', '\'', '.', ' ');

            return agents.FirstOrDefault(a =>
                a.Name.Equals(selectedName, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(selectedName, StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LLM agent selection failed, using fallback");
            return null;
        }
    }
}
