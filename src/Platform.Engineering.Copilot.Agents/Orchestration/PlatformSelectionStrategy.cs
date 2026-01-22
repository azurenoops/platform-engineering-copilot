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
        // FIRST: Check if this is a short follow-up message that should continue with the previous agent
        // This handles "yes", "no", "proceed", providing parameters, confirmations, etc.
        var continuationAgent = DetectConversationContinuation(userMessage, agents, context);
        if (continuationAgent != null)
        {
            _logger.LogInformation("🔗 Continuation selection (follow-up to previous agent): {AgentName}", continuationAgent.Name);
            return continuationAgent;
        }

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

        // Infrastructure patterns - Only for EXPLICIT custom template generation
        // Platform Engineering model: Default to pre-approved templates (Environment Agent)
        // Infrastructure Agent is for: "generate a bicep template", "create custom terraform", "design infrastructure"
        // EXCEPTION: "service template" queries always go to Environment Agent
        if ((lower.Contains("generate") || lower.Contains("create custom") || lower.Contains("design")) && 
            (lower.Contains("template") || lower.Contains("bicep") || lower.Contains("terraform")) &&
            !lower.Contains("service template") && !lower.Contains("from template"))
            return agents.FirstOrDefault(a => a.Name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        
        // Template retrieval/review patterns - route to Infrastructure Agent for generated templates
        // EXCEPTION: "service template" queries go to Environment Agent
        if ((lower.Contains("review") || lower.Contains("display")) &&
            (lower.Contains("bicep") || lower.Contains("terraform") || lower.Contains("generated template")) &&
            !lower.Contains("service template"))
            return agents.FirstOrDefault(a => a.Name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        
        // Explicit IaC keywords - Infrastructure Agent (custom template generation)
        if (lower.Contains("bicep") || lower.Contains("terraform") ||
            lower.Contains("arm template") || lower.Contains("infrastructure as code") ||
            lower.Contains("iac") || lower.Contains("generate template"))
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

        // Environment patterns - Platform Engineering provisioning requests (CHECK BEFORE COMPLIANCE)
        // "I need an environment for FedRAMP" = Environment Agent (template provisioning)
        // "Scan for FedRAMP compliance" = Compliance Agent (assessment)
        // Route "I need X" requests to Environment Agent to find/use pre-approved templates
        bool isEnvironmentRequest = 
            lower.Contains("i need an environment") || lower.Contains("i need a environment") ||
            lower.Contains("create environment") || lower.Contains("provision environment") ||
            lower.Contains("deploy environment") || lower.Contains("landing zone") ||
            lower.Contains("provisioned environment") || lower.Contains("scale environment") ||
            lower.Contains("clone environment") || lower.Contains("drift") ||
            lower.Contains("service template") || lower.Contains("template deployment") ||
            lower.Contains("from template") ||
            (lower.Contains("environment") && !lower.Contains("tagged") && !lower.Contains("tag=") && 
             !lower.Contains("scan") && !lower.Contains("assess") && !lower.Contains("compliance scan"));

        if (isEnvironmentRequest)
            return agents.FirstOrDefault(a => a.Name.Contains("Environment", StringComparison.OrdinalIgnoreCase));

        // Compliance patterns - only if NOT environment provisioning request
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

        // Discovery patterns - for Azure RESOURCES (check BEFORE Environment to handle tag queries)
        // "Find resources tagged with environment=X" is discovery, NOT environment provisioning
        if ((lower.Contains("list") && (lower.Contains("resource") || lower.Contains("vm") || lower.Contains("storage") || 
             lower.Contains("subscription") || lower.Contains("azure subscription"))) ||
            lower.Contains("list subscriptions") || lower.Contains("my subscriptions") ||
            lower.Contains("available subscriptions") || lower.Contains("show subscriptions") ||
            lower.Contains("find resource") || lower.Contains("search resource") ||
            lower.Contains("find all") || lower.Contains("find storage") ||
            lower.Contains("inventory") || lower.Contains("what resources") ||
            lower.Contains("tagged with") || lower.Contains("with tag") || // Tag-based queries = Discovery
            (lower.Contains("show me all") && !lower.Contains("environment")) || 
            (lower.Contains("get all") && !lower.Contains("environment")) ||
            lower.Contains("discover") && !lower.Contains("compliance"))
            return agents.FirstOrDefault(a => a.Name.Contains("Discovery", StringComparison.OrdinalIgnoreCase));

        // Additional "I need X" patterns for Environment Agent (resource provisioning via templates)
        if ((lower.Contains("i need") && (lower.Contains("cluster") || lower.Contains("web app") || 
             lower.Contains("container") || lower.Contains("database") || lower.Contains("microservice"))) ||
            // Resource type requests without explicit "generate/bicep/terraform" go to templates
            ((lower.Contains("kubernetes") || lower.Contains("aks") || lower.Contains("web app") || 
              lower.Contains("container app")) && 
             !lower.Contains("generate") && !lower.Contains("bicep") && !lower.Contains("terraform")))
            return agents.FirstOrDefault(a => a.Name.Contains("Environment", StringComparison.OrdinalIgnoreCase));

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

    /// <summary>
    /// Detect if this is a follow-up message that should continue with the previous agent.
    /// Handles confirmations, parameter inputs, and short contextual responses.
    /// </summary>
    private BaseAgent? DetectConversationContinuation(
        string message,
        IReadOnlyList<BaseAgent> agents,
        AgentConversationContext context)
    {
        // No history = no continuation
        if (!context.MessageHistory.Any() && !context.PreviousResponses.Any())
            return null;

        var lower = message.ToLowerInvariant().Trim();
        var wordCount = message.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        // Detect follow-up patterns:
        // 1. Short confirmations: "yes", "no", "proceed", "confirm", "ok"
        // 2. Short parameter inputs: "fs", "dev-team", "usgovvirginia"
        // 3. Negation/decline patterns: "no customize", "no additional", "none", "skip"
        // 4. Numbered choices: "1", "option 2", "the first one"
        
        bool isFollowUpPattern = 
            // Short confirmations
            lower == "yes" || lower == "no" || lower == "ok" || lower == "okay" ||
            lower == "proceed" || lower == "confirm" || lower == "confirmed" ||
            lower == "continue" || lower == "done" || lower == "next" ||
            lower == "go ahead" || lower == "yes proceed" || lower == "yes please" ||
            lower == "none" || lower == "skip" || lower == "neither" ||
            // Decline/negation patterns
            lower.StartsWith("no ") || lower.StartsWith("not ") ||
            lower.Contains("no additional") || lower.Contains("no customize") ||
            lower.Contains("don't need") || lower.Contains("do not need") ||
            lower.Contains("that's all") || lower.Contains("that is all") ||
            // Numbered choices
            (wordCount <= 3 && (lower.StartsWith("1") || lower.StartsWith("2") || lower.StartsWith("3") ||
             lower.Contains("first") || lower.Contains("second") || lower.Contains("third") ||
             lower.Contains("option"))) ||
            // Short parameter-like inputs (under 5 words, no clear agent keywords)
            (wordCount <= 5 && !ContainsAgentKeywords(lower));

        if (!isFollowUpPattern)
            return null;

        // Try to find the last agent from context
        string? lastAgentName = null;

        // First check PreviousResponses
        if (context.PreviousResponses.Any())
        {
            lastAgentName = context.PreviousResponses.Last().AgentName;
        }

        // If we have a last agent, return it
        if (!string.IsNullOrEmpty(lastAgentName))
        {
            var continuationAgent = agents.FirstOrDefault(a =>
                a.Name.Equals(lastAgentName, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(lastAgentName, StringComparison.OrdinalIgnoreCase));

            if (continuationAgent != null)
            {
                _logger.LogDebug("Detected follow-up message '{Message}' continuing with {Agent}", 
                    message.Substring(0, Math.Min(50, message.Length)), lastAgentName);
                return continuationAgent;
            }
        }

        // Check message history for agent context clues
        if (context.MessageHistory.Any())
        {
            var lastAssistantMessage = context.MessageHistory
                .Where(m => !m.IsUser) // IsUser == false means assistant
                .LastOrDefault();

            if (lastAssistantMessage?.Content != null)
            {
                // Look for agent clues in the last response
                var lastContent = lastAssistantMessage.Content.ToLowerInvariant();
                
                if (lastContent.Contains("environment") || lastContent.Contains("template") || 
                    lastContent.Contains("landing zone") || lastContent.Contains("provisioning") ||
                    lastContent.Contains("delete") || lastContent.Contains("permanently"))
                {
                    _logger.LogInformation("🔗 Continuation selection from message history: Environment Agent");
                    return agents.FirstOrDefault(a => a.Name.Contains("Environment", StringComparison.OrdinalIgnoreCase));
                }
                    return agents.FirstOrDefault(a => a.Name.Contains("Environment", StringComparison.OrdinalIgnoreCase));
                
                if (lastContent.Contains("compliance") || lastContent.Contains("nist") || 
                    lastContent.Contains("fedramp") || lastContent.Contains("assessment"))
                    return agents.FirstOrDefault(a => a.Name.Contains("Compliance", StringComparison.OrdinalIgnoreCase));
                
                if (lastContent.Contains("cost") || lastContent.Contains("budget") || 
                    lastContent.Contains("spending") || lastContent.Contains("optimization"))
                    return agents.FirstOrDefault(a => a.Name.Contains("Cost", StringComparison.OrdinalIgnoreCase));
                
                if (lastContent.Contains("bicep") || lastContent.Contains("terraform") || 
                    lastContent.Contains("infrastructure") || lastContent.Contains("provision"))
                    return agents.FirstOrDefault(a => a.Name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
            }
        }

        return null;
    }

    /// <summary>
    /// Check if the message contains clear agent-routing keywords
    /// </summary>
    private static bool ContainsAgentKeywords(string lower)
    {
        // If the message has clear agent keywords, it's not a simple follow-up
        return lower.Contains("compliance") || lower.Contains("scan") || lower.Contains("assess") ||
               lower.Contains("cost") || lower.Contains("budget") || lower.Contains("spending") ||
               lower.Contains("discover") || lower.Contains("list resource") || lower.Contains("find resource") ||
               lower.Contains("generate") || lower.Contains("bicep") || lower.Contains("terraform") ||
               lower.Contains("template") || lower.Contains("environment") || lower.Contains("provision") ||
               lower.Contains("subscription") || lower.Contains("nist") || lower.Contains("control");
    }
}
