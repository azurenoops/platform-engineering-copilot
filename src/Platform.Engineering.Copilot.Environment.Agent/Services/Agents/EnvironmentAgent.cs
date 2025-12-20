using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Interfaces.TokenManagement;
using Platform.Engineering.Copilot.Core.Models.TokenManagement;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Environment.Agent.Plugins;

namespace Platform.Engineering.Copilot.Environment.Agent.Services.Agents;

/// <summary>
/// Specialized agent for environment management (lifecycle, cloning, scaling)
/// </summary>
public class EnvironmentAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.Environment;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly ILogger<EnvironmentAgent> _logger;
    private readonly EnvironmentManagementPlugin _environmentPlugin;
    private readonly ITokenCounter _tokenCounter;
    private readonly IPromptOptimizer _promptOptimizer;
    private readonly IRagContextOptimizer _ragContextOptimizer;
    private readonly IConversationHistoryOptimizer _conversationHistoryOptimizer;

    public EnvironmentAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<EnvironmentAgent> logger,
        EnvironmentManagementPlugin environmentPlugin,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin,
        ITokenCounter tokenCounter,
        IPromptOptimizer promptOptimizer,
        IRagContextOptimizer ragContextOptimizer,
        IConversationHistoryOptimizer conversationHistoryOptimizer)
    {
        _logger = logger;
        _environmentPlugin = environmentPlugin;
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _promptOptimizer = promptOptimizer ?? throw new ArgumentNullException(nameof(promptOptimizer));
        _ragContextOptimizer = ragContextOptimizer ?? throw new ArgumentNullException(nameof(ragContextOptimizer));
        _conversationHistoryOptimizer = conversationHistoryOptimizer ?? throw new ArgumentNullException(nameof(conversationHistoryOptimizer));
        
        // Create specialized kernel for environment operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Environment);
        _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        // Register environment management plugin
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(environmentPlugin, "EnvironmentManagementPlugin"));

        _logger.LogInformation("✅ Environment Agent initialized with specialized kernel");
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        _logger.LogInformation("🌍 Environment Agent processing task: {TaskId}", task.TaskId);

        var startTime = DateTime.UtcNow;
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.Environment,
            Success = false
        };

        try
        {
            // Get conversation context from shared memory
            var context = memory.GetContext(task.ConversationId ?? "default");
            var previousResults = context?.PreviousResults ?? new List<AgentResponse>();

            // Phase 5: Evaluate conversation health and optimize if needed
            if (context.MessageHistory != null && context.MessageHistory.Any())
            {
                var conversationMessages = context.MessageHistory
                    .Select(m => new ConversationMessage
                    {
                        Role = m.Role,
                        Content = m.Content,
                        Timestamp = m.Timestamp
                    })
                    .ToList();

                // Evaluate conversation health
                var health = await EvaluateConversationHealthAsync(
                    conversationMessages, 
                    _conversationHistoryOptimizer, 
                    conversationMessages.Sum(m => _tokenCounter.CountTokens(m.Content)), 
                    8000);

                if (health.NeedsOptimization)
                {
                    _logger.LogInformation(
                        "Environment Agent - Conversation optimization needed: {HealthStatus}", 
                        health.GetHealthSummary());

                    // Manage context window by getting focused message range
                    var managedMessages = await ManageContextWindowAsync(
                        conversationMessages, 
                        _conversationHistoryOptimizer, 
                        conversationMessages.Count - 1,
                        3250);
                    
                    conversationMessages = managedMessages;
                }
            }

            // Build system prompt for environment management expertise
            var systemPrompt = BuildSystemPrompt();

            // Build user message with context
            var userMessage = BuildUserMessage(task, previousResults);

            // Create chat history
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userMessage);

            // Phase 5: Optimize conversation history before using it
            if (memory.HasContext(task.ConversationId ?? "default"))
            {
                var conversationContext = memory.GetContext(task.ConversationId ?? "default");
                
                if (conversationContext.MessageHistory != null && conversationContext.MessageHistory.Any())
                {
                    var conversationMessages = conversationContext.MessageHistory
                        .Select(m => new ConversationMessage
                        {
                            Role = m.Role,
                            Content = m.Content,
                            Timestamp = m.Timestamp
                        })
                        .ToList();

                    var optimizedHistory = await OptimizeConversationHistoryAsync(conversationMessages, _conversationHistoryOptimizer, 3250);
                    
                    // Use optimized messages for context
                    var recentHistory = optimizedHistory.Messages
                        .OrderBy(m => m.Timestamp)
                        .TakeLast(Math.Min(5, optimizedHistory.Messages.Count))
                        .ToList();

                    if (recentHistory.Any())
                    {
                        var historyText = string.Join("\n", recentHistory.Select(h => 
                            $"{h.Role}: {h.Content}"));
                        
                        chatHistory.AddUserMessage($@"
**IMPORTANT: Previous conversation context (optimized for environment management):**
{historyText}

**The current message is a continuation of this conversation. User has ALREADY provided environment context.**

**DO NOT ask for information the user already provided above. Instead, USE the information for environment operations!**
");
                    }
                }
            }

            // 🔥 Set conversation ID in plugin so it can retrieve files from SharedMemory
            // This enables EnvironmentAgent to retrieve Bicep templates generated by InfrastructureAgent
            _environmentPlugin.SetConversationId(task.ConversationId ?? "default");
            _logger.LogInformation(
                "🔗 EnvironmentAgent: ConversationId set to {ConversationId} for SharedMemory file retrieval",
                task.ConversationId ?? "default");

            // Phase 5: Optimize prompt to fit token budget before sending to LLM
            var systemPromptText = chatHistory.FirstOrDefault(m => m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System)?.Content ?? "";
            var userMessageText = task.Description;
            if (!string.IsNullOrEmpty(systemPromptText) && !string.IsNullOrEmpty(userMessageText))
            {
                var optimizedPrompt = FitPromptInTokenBudget(systemPromptText, userMessageText);
                if (optimizedPrompt.WasOptimized)
                {
                    _logger.LogInformation(
                        "Environment Agent - Prompt optimized before LLM call: {Strategy}, Tokens saved: {Saved}",
                        optimizedPrompt.OptimizationStrategy, optimizedPrompt.TokensSaved);
                }
            }

            // Execute with moderate temperature for environment operations
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.3, // Moderate temperature for precise operations
                MaxTokens = 4000,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            // 🔍 DIAGNOSTIC: Log what the LLM actually did
            _logger.LogInformation("🔍 EnvironmentAgent DIAGNOSTIC:");
            _logger.LogInformation("   - Result Content Length: {Length} characters", result.Content?.Length ?? 0);
            _logger.LogInformation("   - Result Role: {Role}", result.Role);
            _logger.LogInformation("   - Result Metadata Keys: {Keys}", result.Metadata?.Keys != null ? string.Join(", ", result.Metadata.Keys) : "null");
            
            // Check if any functions were called
            if (result.Items != null && result.Items.Any())
            {
                _logger.LogInformation("   - Result Items Count: {Count}", result.Items.Count);
                foreach (var item in result.Items)
                {
                    _logger.LogInformation("     - Item Type: {Type}", item?.GetType().Name ?? "null");
                }
            }
            else
            {
                _logger.LogWarning("   ⚠️  NO FUNCTION CALLS DETECTED - LLM returned text response only!");
                var preview = string.IsNullOrEmpty(result.Content) ? "empty" : result.Content.Substring(0, Math.Min(200, result.Content.Length));
                _logger.LogWarning("   📝 Response preview: {Preview}", preview);
            }

            response.Content = result.Content ?? "";
            response.Success = true;

            // Extract metadata
            response.Metadata = ExtractMetadata(result, task);

            // Store result in shared memory for other agents
            memory.AddAgentCommunication(
                task.ConversationId ?? "default",
                AgentType.Environment,
                AgentType.Orchestrator,
                $"Environment operation completed: {task.Description}",
                new Dictionary<string, object>
                {
                    ["result"] = result.Content ?? ""
                }
            );

            _logger.LogInformation("✅ Environment Agent completed task: {TaskId}", task.TaskId);
            
            // Phase 5: Record agent cost metrics for this operation
            try
            {
                var completionTokens = _tokenCounter.CountTokens(result.Content ?? "");
                var costPrompt = new OptimizedPrompt
                {
                    WasOptimized = false,
                    RagContext = new List<string>(),
                    ConversationHistory = new List<string>()
                };
                await RecordAgentCostAsync(costPrompt, completionTokens, task.TaskId, task.ConversationId ?? "default");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record cost metrics for task {TaskId}", task.TaskId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Environment Agent failed on task: {TaskId}", task.TaskId);
            response.Success = false;
            response.Errors = new List<string> { ex.Message };
        }

        response.ExecutionTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return response;
    }

    private string BuildSystemPrompt()
    {
        return @"You are a specialized Environment Management expert with deep expertise in:

**Environment Lifecycle Management:**
- Environment provisioning and de-provisioning
- Environment cloning and replication
- Environment scaling (up/down, in/out)
- Environment health monitoring
- Environment state management

**Environment Types:**
- Development, Test, Staging, Production
- Sandbox and demo environments
- CI/CD pipeline environments
- Training and disaster recovery environments

**Best Practices:**
- Environment isolation and security boundaries
- Configuration management across environments
- Secrets and credentials management
- Environment naming conventions
- Resource tagging strategies

**Operations:**
- Clone existing environments with data/configuration
- Scale environments based on load and requirements
- Refresh environments with production data (sanitized)
- Environment status checks and health validation
- Environment cleanup and resource optimization

**CRITICAL: ALWAYS CALL FUNCTIONS - NEVER JUST RESPOND CONVERSATIONALLY!**

When your task description includes ANY of these keywords:
- ""Manage the environment lifecycle""
- ""Track the deployment""  
- ""Deploy"" or ""Provision""
- ""Create environment""
- ""Scale environment""
- ""Clone environment""

You MUST call the appropriate function! DO NOT just write a conversational response.

**ACTUAL DEPLOYMENT WORKFLOW:**
When your task is to ""Manage the environment lifecycle and track the deployment of the new resources"":

1. **YOU MUST CALL create_environment** - This is not optional!
2. The InfrastructureAgent already generated the Bicep templates in the previous step
3. Those templates are stored in SharedMemory and will be retrieved automatically
4. Your job is to ACTUALLY DEPLOY them by calling create_environment

**REQUIRED PARAMETERS FOR create_environment:**
- environmentName: Extract from task description (e.g., ""dev-aks"", ""staging-cluster"")
- environmentType: Use ""aks"" for Kubernetes, ""appservice"" for web apps, etc.
- resourceGroup: Extract from task or generate name (e.g., ""rg-dev-aks"")
- location: **CRITICAL - Azure Government ONLY regions**: usgovvirginia, usgovarizona, usgovtexas, usgoviowa, usdodeast, usdodcentral
  - ❌ WRONG: eastus, westus, centralus (commercial Azure regions - will FAIL in Azure Government!)
  - ✅ CORRECT: usgovvirginia (default), usgovarizona
- subscriptionId: **ALWAYS REQUIRED** - Extract from task description or conversation

**CRITICAL: Subscription ID Requirement**
When creating environments with the create_environment function, you MUST ALWAYS provide the subscriptionId parameter.
- Extract the subscription ID from the user's request or previous conversation
- The subscription ID should be a valid Azure subscription GUID (format: 00000000-0000-0000-0000-000000000000)
- Never use placeholder values like 'default-subscription'
- The subscription ID will be visible in your task description or parameters

**Example - WRONG (Don't do this):**
Task: ""Manage the environment lifecycle and track the deployment of the new AKS cluster""
Response: ""I will manage the environment lifecycle and track the deployment...""  ❌ WRONG - No function call!

**Example - CORRECT (Do this):**
Task: ""Manage the environment lifecycle and track the deployment of the new AKS cluster in subscription 453c...""
Response: [Calls create_environment with environmentName=""dev-aks"", environmentType=""aks"", resourceGroup=""rg-dev-aks"", location=""usgovvirginia"", subscriptionId=""00000000-0000-0000-0000-000000000000""] ✅ CORRECT

**DO NOT:**
- Write conversational responses without calling functions
- Say ""I will track..."" or ""I will manage..."" without actually calling create_environment
- Ask for more information if the subscription ID and basic details are in the task description
- Respond with status updates without first calling the deployment function

**DO:**
- Call create_environment IMMEDIATELY when your task is about deployment/provisioning
- Extract all necessary parameters from the task description and previous conversation context
- Trust that the Bicep templates are already in SharedMemory from InfrastructureAgent
- Actually execute the deployment by calling the function

Always provide clear operational steps and validate prerequisites before operations.

**🤖 Conversational Requirements Gathering**

When a user asks about environments, configurations, or deployments, use a conversational approach to gather context:

**For Environment Creation/Setup Requests, ask about:**
- **Environment Purpose**: ""What type of environment are you setting up?""
  - Development
  - Testing/QA
  - Staging
  - Production
  - Disaster Recovery
- **Naming Convention**: ""What naming pattern should I use?""
  - Standard: {app}-{env}-{region} (e.g., webapp-dev-eastus)
  - Custom pattern
  - User will provide full name
- **Location**: ""Which Azure region should I use?""
  - usgovvirginia (Azure Government)
  - usgovarizona (Azure Government)
  - Other Azure Government regions only
- **Subscription**: ""Which subscription should host this environment?""
  - Subscription ID or name
- **Resource Group**: ""Should I create a new resource group or use existing?""
  - Create new (suggest name based on environment)
  - Use existing (ask for name)

**For Environment Configuration Requests, ask about:**
- **Configuration Scope**: ""What would you like me to configure?""
  - Networking (VNets, subnets, NSGs)
  - Security (RBAC, managed identities, Key Vault)
  - Monitoring (Application Insights, Log Analytics)
  - Scaling (auto-scale rules, instance counts)
  - All of the above
- **Environment Type**: ""Which environment am I configuring?""
  - Environment name or ID
  - Check SharedMemory for recent deployments

**For Environment Validation Requests, ask about:**
- **Validation Level**: ""How thorough should the validation be?""
  - Basic (naming, tagging, RBAC, networking) - 4 checks
  - Standard (+ security, monitoring, backup, cost) - 9 checks
  - Comprehensive (+ compliance, DR, scaling, docs) - 16 checks
- **Environment ID**: ""Which environment should I validate?""
  - Environment name
  - Subscription + resource group combination
  - Check SharedMemory for recent deployments

**For Environment Comparison Requests, ask about:**
- **Environments to Compare**: ""Which two environments should I compare?""
  - Source environment (e.g., dev or staging)
  - Target environment (e.g., production)
- **Comparison Aspects**: ""What should I compare?""
  - Resource configuration (SKUs, sizes)
  - Network settings
  - Security configuration
  - Scaling settings
  - All of the above

**Example Conversation Flow:**

User: ""Set up a production environment for my new web app""
You: ""I'd be happy to help set up your production environment! To ensure everything is configured correctly, I need a few details:

1. What's the application name? (I'll use this for naming: {app}-prod-{region})
2. Which Azure region? (usgovvirginia, usgovarizona)
3. Which subscription should I use? (name or subscription ID)
4. What configuration level do you need?
   - Basic (compute + storage)
   - Standard (+ networking + security)
   - Enterprise (+ monitoring + compliance)

Let me know your preferences!""

User: ""webapp-api, usgovvirginia, subscription 453c..., enterprise""
You: **[IMMEDIATELY call create_environment function - DO NOT ask for confirmation]**

**CRITICAL: One Question Cycle Only!**
- First message: User asks to set up environment → Ask for missing critical info
- Second message: User provides answers → **IMMEDIATELY call the appropriate environment function**
- DO NOT ask ""Should I proceed?"" or ""Any adjustments needed?""
- DO NOT repeat questions - use smart defaults for minor missing details

**CRITICAL: Check SharedMemory First!**
Before asking for environment details, ALWAYS check SharedMemory for:
- Recently created environments
- Deployment metadata (resource group, subscription, location)
- If found, confirm with user: ""I found environment '{name}' from a recent deployment. Is this the one you want to configure/validate?""
";
    }

    private string BuildUserMessage(AgentTask task, List<AgentResponse> previousResults)
    {
        var message = $"Task: {task.Description}\n\n";

        // Add parameters if provided
        if (task.Parameters != null && task.Parameters.Any())
        {
            message += "Parameters:\n";
            foreach (var param in task.Parameters)
            {
                message += $"- {param.Key}: {param.Value}\n";
            }
            message += "\n";
        }

        // Add context from previous agent results
        if (previousResults.Any())
        {
            message += "Context from other agents:\n";
            foreach (var prevResult in previousResults.TakeLast(3))
            {
                var contentLength = prevResult.Content?.Length ?? 0;
                if (contentLength > 0)
                {
                    message += $"- {prevResult.AgentType}: {prevResult.Content?.Substring(0, Math.Min(200, contentLength))}...\n";
                }
            }
            message += "\n";
        }

        message += "Please perform the environment management operation with detailed status updates.";

        return message;
    }

    private Dictionary<string, object> ExtractMetadata(ChatMessageContent result, AgentTask task)
    {
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["agentType"] = AgentType.Environment.ToString()
        };

        // Extract tool calls if any
        if (result.Metadata != null && result.Metadata.ContainsKey("ChatCompletionMessage"))
        {
            metadata["toolsInvoked"] = "EnvironmentManagementPlugin functions";
        }

        return metadata;
    }

    /// <summary>
    /// Helper method to optimize search results for RAG context
    /// </summary>
    private OptimizedRagContext OptimizeSearchResults(
        List<string> results,
        string query,
        int maxTokens = 1500)
    {
        var ranked = new List<RankedSearchResult>();

        foreach (var result in results)
        {
            if (string.IsNullOrEmpty(result))
                continue;

            var relevanceScore = CalculateRelevanceScore(query, result);
            ranked.Add(new RankedSearchResult
            {
                Content = result,
                RelevanceScore = relevanceScore,
                TokenCount = 0,
                Metadata = new Dictionary<string, object> { { "query", query } }
            });
        }

        var options = new RagOptimizationOptions
        {
            MaxRagTokens = maxTokens,
            MinRelevanceScore = 0.3,
            MaxResults = 10,
            TrimLargeResults = true
        };

        return _ragContextOptimizer.OptimizeContext(ranked, options);
    }

    /// <summary>
    /// Calculate relevance score based on keyword matching
    /// </summary>
    private double CalculateRelevanceScore(string query, string content, string? title = null)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(content))
            return 0.0;

        var queryWords = query.ToLower()
            .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .ToList();

        if (queryWords.Count == 0)
            return 0.0;

        var contentLower = content.ToLower();
        var titleLower = title?.ToLower() ?? string.Empty;

        int contentMatches = queryWords.Count(w => contentLower.Contains(w));
        double contentScore = (double)contentMatches / queryWords.Count;

        double titleScore = 0.0;
        if (!string.IsNullOrEmpty(titleLower))
        {
            int titleMatches = queryWords.Count(w => titleLower.Contains(w));
            titleScore = (titleMatches * 0.15);
        }

        double score = contentScore + titleScore;
        return Math.Min(1.0, Math.Max(0.0, score));
    }

    /// <summary>
    /// Helper method to fit a prompt within token budget using optimization
    /// </summary>
    private OptimizedPrompt FitPromptInTokenBudget(
        string systemPrompt,
        string userMessage,
        List<string>? ragContext = null,
        List<string>? conversationHistory = null)
    {
        ragContext ??= new List<string>();
        conversationHistory ??= new List<string>();

        var options = BuildPromptOptimizationOptions();
        var optimized = _promptOptimizer.OptimizePrompt(
            systemPrompt,
            userMessage,
            ragContext,
            conversationHistory,
            options);

        if (optimized.WasOptimized)
        {
            _logger.LogInformation("Prompt optimization applied: {Strategy}", optimized.OptimizationStrategy);
            _logger.LogInformation("Tokens saved: {TokensSaved}", optimized.TokensSaved);
        }

        return optimized;
    }

    /// <summary>
    /// Helper method to build prompt optimization options
    /// </summary>
    private PromptOptimizationOptions BuildPromptOptimizationOptions()
    {
        return new PromptOptimizationOptions
        {
            ModelName = "gpt-4o",
            MaxContextWindow = 128000,
            TargetTokenCount = 0,
            ReservedCompletionTokens = 4000,
            SystemPromptPriority = 100,
            UserMessagePriority = 100,
            RagContextPriority = 70,
            ConversationHistoryPriority = 65,
            MinRagContextItems = 2,
            MinConversationHistoryMessages = 2,
            SafetyBufferPercentage = 10,
            UseSummarization = false
        };
    }

    /// <summary>
    /// Helper method to calculate and record agent cost metrics
    /// </summary>
    private async Task RecordAgentCostAsync(
        OptimizedPrompt optimizedPrompt,
        int completionTokens,
        string taskId,
        string conversationId)
    {
        try
        {
            var metrics = new AgentCostMetrics
            {
                AgentType = AgentType.Environment.ToString(),
                TaskId = taskId,
                ConversationId = conversationId,
                Timestamp = DateTime.UtcNow,
                OriginalPromptTokens = optimizedPrompt.OriginalEstimate?.TotalInputTokens ?? 0,
                OptimizedPromptTokens = optimizedPrompt.OptimizedEstimate?.TotalInputTokens ?? 0,
                TokensSaved = optimizedPrompt.TokensSaved,
                OptimizationPercentage = optimizedPrompt.OriginalEstimate?.TotalInputTokens > 0
                    ? (optimizedPrompt.TokensSaved * 100.0 / optimizedPrompt.OriginalEstimate.TotalInputTokens)
                    : 0,
                CompletionTokens = completionTokens,
                TotalTokens = (optimizedPrompt.OptimizedEstimate?.TotalInputTokens ?? 0) + completionTokens,
                Model = "gpt-4o",
                WasOptimized = optimizedPrompt.WasOptimized,
                OptimizationStrategy = optimizedPrompt.OptimizationStrategy,
                RagContextItems = optimizedPrompt.OriginalEstimate?.RagContextItemTokens.Count ?? 0,
                RagContextItemsAfterOptimization = optimizedPrompt.RagContext.Count,
                ConversationHistoryMessages = optimizedPrompt.ConversationHistory.Count
            };

            // Calculate cost (GPT-4o pricing: ~$0.03 per 1K prompt tokens, ~$0.06 per 1K completion tokens)
            var promptCost = (metrics.OptimizedPromptTokens / 1000.0) * 0.03;
            var completionCost = (completionTokens / 1000.0) * 0.06;
            metrics.EstimatedCost = promptCost + completionCost;

            // Calculate original cost for comparison
            var originalPromptCost = (metrics.OriginalPromptTokens / 1000.0) * 0.03;
            metrics.CostSaved = originalPromptCost - promptCost;

            _logger.LogInformation("Environment Agent - Cost metrics recorded: {Summary}", metrics.GetSummary());

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record agent cost metrics");
        }
    }

    /// <summary>
    /// Helper method to optimize conversation history for context window management
    /// </summary>
    private async Task<OptimizedConversationHistory> OptimizeConversationHistoryAsync(
        List<ConversationMessage> messages,
        IConversationHistoryOptimizer historyOptimizer,
        int tokenBudget = 3250)
    {
        try
        {
            var options = historyOptimizer.GetRecommendedOptionsForAgent("Environment");
            options.MaxTokens = Math.Min(tokenBudget, options.MaxTokens);

            var optimized = await historyOptimizer.OptimizeHistoryAsync(messages, options);
            
            _logger.LogInformation("Environment Agent - Conversation history optimized:\n{Summary}", 
                optimized.GetSummary());

            return optimized;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to optimize conversation history");
            return new OptimizedConversationHistory 
            { 
                Messages = messages,
                OriginalMessageCount = messages.Count
            };
        }
    }

    /// <summary>
    /// Helper method to evaluate conversation health and determine if pruning is needed
    /// </summary>
    private async Task<ConversationHealthMetrics> EvaluateConversationHealthAsync(
        List<ConversationMessage> messages,
        IConversationHistoryOptimizer historyOptimizer,
        int currentTokenCount,
        int tokenBudget = 6500)
    {
        try
        {
            var health = await historyOptimizer.EvaluateConversationHealthAsync(
                messages, 
                currentTokenCount, 
                tokenBudget);

            _logger.LogDebug("Environment Agent - Conversation health evaluated:\n{Summary}", 
                health.GetHealthSummary());

            return health;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to evaluate conversation health");
            return new ConversationHealthMetrics { TotalMessages = messages.Count };
        }
    }

    /// <summary>
    /// Helper method to manage context window for long-running conversations
    /// </summary>
    private async Task<List<ConversationMessage>> ManageContextWindowAsync(
        List<ConversationMessage> messages,
        IConversationHistoryOptimizer historyOptimizer,
        int targetMessageIndex,
        int maxTokens = 3250)
    {
        try
        {
            var contextWindow = await historyOptimizer.GetContextWindowAsync(
                messages,
                maxTokens,
                targetMessageIndex);

            _logger.LogDebug("Environment Agent - Context window managed: {TargetIndex} → {WindowSize} messages", 
                targetMessageIndex, contextWindow.Count);

            return contextWindow;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to manage context window");
            return messages;
        }
    }
}
