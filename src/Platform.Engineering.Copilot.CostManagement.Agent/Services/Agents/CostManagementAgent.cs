using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.TokenManagement;
using Platform.Engineering.Copilot.Core.Models.TokenManagement;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services;
using System.Text.RegularExpressions;
using Platform.Engineering.Copilot.CostManagement.Agent.Plugins;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.CostManagement.Core.Configuration;

namespace Platform.Engineering.Copilot.CostManagement.Agent.Services.Agents;

/// <summary>
/// Specialized agent for Azure cost analysis, budget management, and cost optimization
/// </summary>
public class CostManagementAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.CostManagement;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly ILogger<CostManagementAgent> _logger;
    private readonly CostManagementAgentOptions _options;
    private readonly ConfigService _configService;
    private readonly ITokenCounter _tokenCounter;
    private readonly IPromptOptimizer _promptOptimizer;
    private readonly IRagContextOptimizer _ragContextOptimizer;
    private readonly IConversationHistoryOptimizer _conversationHistoryOptimizer;

    public CostManagementAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<CostManagementAgent> logger,
        CostManagementPlugin costManagementPlugin,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin,
        ConfigService configService,
        IOptions<CostManagementAgentOptions> options,
        ITokenCounter tokenCounter,
        IPromptOptimizer promptOptimizer,
        IRagContextOptimizer ragContextOptimizer,
        IConversationHistoryOptimizer conversationHistoryOptimizer)
    {
        _logger = logger;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _promptOptimizer = promptOptimizer ?? throw new ArgumentNullException(nameof(promptOptimizer));
        _ragContextOptimizer = ragContextOptimizer ?? throw new ArgumentNullException(nameof(ragContextOptimizer));
        _conversationHistoryOptimizer = conversationHistoryOptimizer ?? throw new ArgumentNullException(nameof(conversationHistoryOptimizer));
        
        // Create specialized kernel for cost management operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.CostManagement);
        
        // Try to get chat completion service - make it optional for basic functionality
        try
        {
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            _logger.LogInformation("✅ Cost Management Agent initialized with AI chat completion service");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Cost Management Agent initialized without AI chat completion service. AI features will be limited.");
            _chatCompletion = null;
        }

        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        // Register cost management plugin
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(costManagementPlugin, "CostManagementPlugin"));

        _logger.LogInformation("✅ Cost Management Agent initialized with specialized kernel (Temperature: {Temperature}, MaxTokens: {MaxTokens}, Currency: {Currency})",
            _options.Temperature, _options.MaxTokens, _options.DefaultCurrency);
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        _logger.LogInformation("💰 Cost Management Agent processing task: {TaskId}", task.TaskId);

        var startTime = DateTime.UtcNow;
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.CostManagement,
            Success = false
        };

        try
        {
            // Check if AI services are available
            if (_chatCompletion == null)
            {
                _logger.LogWarning("⚠️ AI chat completion service not available. Returning basic response for task: {TaskId}", task.TaskId);
                
                response.Success = true;
                response.Content = "AI services not configured. Basic cost management functionality available through database operations only. " +
                                 "Configure Azure OpenAI to enable full AI-powered cost analysis.";
                response.ExecutionTimeMs = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                return response;
            }

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
                        "Cost Management Agent - Conversation optimization needed: {HealthStatus}", 
                        health.GetHealthSummary());

                    // Manage context window by getting focused message range
                    var managedMessages = await ManageContextWindowAsync(
                        conversationMessages, 
                        _conversationHistoryOptimizer, 
                        conversationMessages.Count - 1,
                        4500);
                    
                    conversationMessages = managedMessages;
                }
            }

            // Get default subscription from config service
            var defaultSubscriptionId = _configService.GetDefaultSubscription();
            
            // Build subscription info if available
            var subscriptionInfo = !string.IsNullOrEmpty(defaultSubscriptionId)
                ? $@"

**🔧 DEFAULT CONFIGURATION:**
- Default Subscription ID: {defaultSubscriptionId}
- When users don't specify a subscription, automatically use the default subscription ID above
- ALWAYS use the default subscription when available unless user explicitly specifies a different one
"
                : "";

            // Build system prompt for cost expertise
            var systemPrompt = BuildSystemPrompt(subscriptionInfo);

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

                    var optimizedHistory = await OptimizeConversationHistoryAsync(conversationMessages, _conversationHistoryOptimizer, 4500);
                    
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
**IMPORTANT: Previous conversation context (optimized for cost analysis):**
{historyText}

**The current message is a continuation of this conversation. User has ALREADY provided cost context.**

**DO NOT ask for information the user already provided above. Instead, USE the information for your cost analysis!**
");
                    }
                }
            }

            // Execute with configured temperature for analytical cost assessments
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = _options.Temperature,
                MaxTokens = _options.MaxTokens,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            // Phase 5: Optimize prompt to fit token budget before sending to LLM
            var systemPromptText = chatHistory.FirstOrDefault(m => m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System)?.Content ?? "";
            var userMessageText = task.Description;
            if (!string.IsNullOrEmpty(systemPromptText) && !string.IsNullOrEmpty(userMessageText))
            {
                var optimizedPrompt = FitPromptInTokenBudget(systemPromptText, userMessageText);
                if (optimizedPrompt.WasOptimized)
                {
                    _logger.LogInformation(
                        "Cost Management Agent - Prompt optimized before LLM call: {Strategy}, Tokens saved: {Saved}",
                        optimizedPrompt.OptimizationStrategy, optimizedPrompt.TokensSaved);
                }
            }

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            response.Content = result.Content ?? "";
            response.Success = true;

            // Extract cost metadata
            var metadata = ExtractMetadata(result, task);
            response.Metadata = metadata;

            // Extract estimated cost
            response.EstimatedCost = (decimal)ExtractEstimatedCost(result.Content);

            // Check if within budget (extract budget from parameters if provided)
            var budget = ExtractBudget(task.Parameters);
            response.IsWithinBudget = budget == null || response.EstimatedCost <= (decimal)budget.Value;

            // Store result in shared memory for other agents
            memory.AddAgentCommunication(
                task.ConversationId ?? "default",
                AgentType.CostManagement,
                AgentType.Orchestrator,
                $"Cost analysis completed. Estimated: ${response.EstimatedCost:N2}/month, Within budget: {response.IsWithinBudget}",
                new Dictionary<string, object>
                {
                    ["estimatedCost"] = response.EstimatedCost,
                    ["isWithinBudget"] = response.IsWithinBudget,
                    ["budget"] = budget ?? 0,
                    ["analysis"] = result.Content ?? ""
                }
            );

            _logger.LogInformation("✅ Cost Management Agent completed task: {TaskId}. Estimated cost: ${Cost:N2}/month, Within budget: {WithinBudget}",
                task.TaskId, response.EstimatedCost, response.IsWithinBudget);
            
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
            _logger.LogError(ex, "❌ Cost Management Agent failed on task: {TaskId}", task.TaskId);
            response.Success = false;
            response.Errors = new List<string> { ex.Message };
        }

        response.ExecutionTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return response;
    }

    private string BuildSystemPrompt(string subscriptionInfo = "")
    {
        return $@"You are a specialized Azure Cost Management and Optimization expert with deep expertise in:
- **Azure context configuration (subscription, tenant, authentication settings)**
{subscriptionInfo}

**CONFIGURATION vs ANALYSIS:**
- If users say ""Use subscription X"", ""Set tenant Y"", ""Set authentication Z"" → **IMMEDIATELY CALL** `set_azure_subscription`, `set_azure_tenant`, or `set_authentication_method` functions (CONFIGURATION)
  - DO NOT just acknowledge - you MUST call the function to actually configure the Azure context
  - Extract the subscription ID/tenant ID from the user's message and pass it to the function
  - Example: ""Use subscription abc-123"" → Call set_azure_subscription(""abc-123"")
  - **CRITICAL**: After calling these functions, return the EXACT function result - DO NOT paraphrase or add commentary
- If users say ""Show costs"", ""Analyze spending"", ""Recommend savings"" → Use cost analysis functions (ANALYSIS)

**Azure Cost Analysis:**
- Cost estimation for all Azure services (VMs, AKS, Storage, Networking, etc.)
- TCO (Total Cost of Ownership) calculations
- Regional pricing variations
- Reserved instance and savings plan benefits
- License optimization (AHUB, Dev/Test pricing)

**Cost Optimization Strategies:**
- Right-sizing recommendations (VMs, storage, databases)
- Auto-scaling and reserved capacity strategies
- Spot instance opportunities
- Storage tier optimization (Hot, Cool, Archive)
- Network cost reduction techniques

**Budget Management:**
- Budget allocation and tracking
- Cost anomaly detection
- Spending forecasts and trend analysis

**🤖 Conversational Requirements Gathering**

When a user asks about costs, optimization, or budgets, use a conversational approach to gather context:

**For Cost Analysis Requests, ask about:**
- **Scope**: ""What would you like me to analyze?""
  - Entire subscription
  - Specific resource group
  - Particular resource types (AKS, VMs, Storage, etc.)
  - Time period for analysis (last month, last 90 days, year-to-date)
- **Breakdown Preference**: ""How would you like costs broken down?""
  - By service type
  - By resource group
  - By location
  - By tags (cost center, environment, etc.)
- **Subscription ID**: If not provided, ask: ""Which subscription should I analyze?""

**For Cost Optimization Requests, ask about:**
- **Focus Area**: ""What type of optimization are you looking for?""
  - Compute (VMs, AKS nodes, App Service plans)
  - Storage (tier optimization, unused disks)
  - Networking (bandwidth, data transfer)
  - Databases (SKU rightsizing, reserved capacity)
  - All of the above
- **Constraints**: ""Are there any constraints I should know about?""
  - Must maintain current performance
  - Can tolerate some downtime for changes
  - Prefer automated recommendations only
  - Need manual review before changes
- **Savings Target**: ""Do you have a target for cost reduction?""
  - Percentage (e.g., reduce by 20%)
  - Dollar amount (e.g., save $5,000/month)
  - Just show all opportunities

**For Budget Management Requests, ask about:**
- **Budget Amount**: ""What's your monthly budget?""
  - Dollar amount
  - Based on current spending + buffer
- **Alert Thresholds**: ""When should I alert you?""
  - At 50%, 75%, 90%, 100% of budget
  - Custom thresholds
- **Scope**: ""What should this budget cover?""
  - Entire subscription
  - Specific resource groups
  - Tagged resources only
- **Actions**: ""What should happen when budget is exceeded?""
  - Email notifications only
  - Automated cost-cutting actions
  - Both

**Example Conversation Flow:**

**If default subscription IS configured:**
User: ""How much am I spending on Azure?""
You: **[IMMEDIATELY call process_cost_management_query with the default subscription - use last 30 days and breakdown by service as smart defaults]**
- DO NOT ask which subscription - use the default from configuration
- Only ask clarifying questions if user wants something specific (different time period, different breakdown, etc.)

**If NO default subscription is configured:**
User: ""How much am I spending on Azure?""
You: ""I'd be happy to analyze your Azure spending! I don't have a default subscription configured yet.

Please provide a subscription ID, or say 'Set subscription <id>' to configure a default for future queries.""

User: ""Set subscription 453c2549-...""
You: **[IMMEDIATELY call set_azure_subscription function, then proceed with cost analysis]**

**CRITICAL: One Question Cycle Only!**
- First message: User asks about costs → Ask for missing critical info
- Second message: User provides answers → **IMMEDIATELY call the appropriate cost function**
- DO NOT ask ""Should I proceed?"" or ""Any adjustments needed?""
- DO NOT repeat questions - use smart defaults for minor missing details
- Cost allocation by tags and resource groups
- Showback/chargeback reporting

**FinOps Best Practices:**
- Cost visibility and transparency
- Cost allocation tagging strategies
- Reserved instance coverage optimization
- Commitment-based discounts (Azure Reservations, Savings Plans)
- Waste identification (unused resources, orphaned disks, etc.)

**Response Format:**
When analyzing costs:
1. Estimate monthly costs in USD with itemized breakdown
2. Identify cost drivers (top 3-5 services/resources)
3. Compare with budget if provided
4. Recommend optimization opportunities
5. Provide potential savings percentage

Always format costs as currency (e.g., $1,234.56/month) and be specific about resource SKUs and regions.";
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

        // Add context from previous agent results (especially infrastructure details)
        if (previousResults.Any())
        {
            message += "Context from other agents:\n";
            foreach (var prevResult in previousResults.TakeLast(3)) // Last 3 results for context
            {
                if (prevResult.AgentType == AgentType.Infrastructure && prevResult.Metadata != null)
                {
                    message += $"- Infrastructure resources: ";
                    if (prevResult.Metadata.ContainsKey("resourceTypes"))
                    {
                        message += $"{prevResult.Metadata["resourceTypes"]}\n";
                    }
                }
                var contentLength = prevResult.Content?.Length ?? 0;
                if (contentLength > 0)
                {
                    message += $"- {prevResult.AgentType}: {prevResult.Content?.Substring(0, Math.Min(200, contentLength))}...\n";
                }
            }
            message += "\n";
        }

        message += "Please provide a comprehensive cost analysis with itemized breakdown and optimization recommendations.";

        return message;
    }

    private Dictionary<string, object> ExtractMetadata(ChatMessageContent result, AgentTask task)
    {
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["agentType"] = AgentType.CostManagement.ToString()
        };

        // Extract tool calls if any
        if (result.Metadata != null && result.Metadata.ContainsKey("ChatCompletionMessage"))
        {
            metadata["toolsInvoked"] = "CostManagementPlugin functions";
        }

        // Extract services mentioned
        var services = ExtractAzureServices(result.Content);
        if (services.Any())
        {
            metadata["azureServices"] = string.Join(", ", services);
        }

        return metadata;
    }

    private List<string> ExtractAzureServices(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return new List<string>();

        var services = new List<string>();
        var commonServices = new[]
        {
            "Virtual Machine", "VM", "AKS", "Kubernetes", "Storage", "SQL", "Cosmos",
            "App Service", "Function", "Container", "VNet", "Load Balancer", "Application Gateway",
            "Key Vault", "Monitor", "Log Analytics"
        };

        foreach (var service in commonServices)
        {
            if (content.Contains(service, StringComparison.OrdinalIgnoreCase))
            {
                services.Add(service);
            }
        }

        return services.Distinct().ToList();
    }

    private double ExtractEstimatedCost(string? content)
    {
        if (string.IsNullOrEmpty(content))
            return 0.0;

        // Try to extract cost patterns like "$1,234.56/month", "cost: $500", "estimated: $2,500.00", etc.
        var patterns = new[]
        {
            @"\$\s*([\d,]+\.?\d*)\s*(?:/month|per month|monthly)?",
            @"(?:cost|estimated|total)[:\s]+\$\s*([\d,]+\.?\d*)",
            @"([\d,]+\.?\d*)\s*USD",
            @"approximately\s+\$\s*([\d,]+\.?\d*)"
        };

        var costs = new List<double>();

        foreach (var pattern in patterns)
        {
            var matches = Regex.Matches(content, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var costStr = match.Groups[1].Value.Replace(",", "");
                if (double.TryParse(costStr, out var cost))
                {
                    costs.Add(cost);
                }
            }
        }

        // Return the maximum cost found (likely the total)
        return costs.Any() ? costs.Max() : 0.0;
    }

    private double? ExtractBudget(Dictionary<string, object>? parameters)
    {
        if (parameters == null)
            return null;

        // Look for budget-related parameters
        var budgetKeys = new[] { "budget", "maxCost", "max_cost", "costLimit", "cost_limit" };
        
        foreach (var key in budgetKeys)
        {
            if (parameters.TryGetValue(key, out var budgetObj))
            {
                // Convert to string and remove currency symbols and commas
                var budgetStr = budgetObj?.ToString()?.Replace("$", "").Replace(",", "").Trim();
                
                if (!string.IsNullOrEmpty(budgetStr) && double.TryParse(budgetStr, out var budget))
                {
                    return budget;
                }
            }
        }

        return null;
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
            RagContextPriority = 75,
            ConversationHistoryPriority = 70,
            MinRagContextItems = 3,
            MinConversationHistoryMessages = 3,
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
                AgentType = AgentType.CostManagement.ToString(),
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

            _logger.LogInformation("Cost Management Agent - Cost metrics recorded: {Summary}", metrics.GetSummary());

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
        int tokenBudget = 4500)
    {
        try
        {
            var options = historyOptimizer.GetRecommendedOptionsForAgent("CostManagement");
            options.MaxTokens = Math.Min(tokenBudget, options.MaxTokens);

            var optimized = await historyOptimizer.OptimizeHistoryAsync(messages, options);
            
            _logger.LogInformation("Cost Management Agent - Conversation history optimized:\n{Summary}", 
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
        int tokenBudget = 9000)
    {
        try
        {
            var health = await historyOptimizer.EvaluateConversationHealthAsync(
                messages, 
                currentTokenCount, 
                tokenBudget);

            _logger.LogDebug("Cost Management Agent - Conversation health evaluated:\n{Summary}", 
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
        int maxTokens = 4500)
    {
        try
        {
            var contextWindow = await historyOptimizer.GetContextWindowAsync(
                messages,
                maxTokens,
                targetMessageIndex);

            _logger.LogDebug("Cost Management Agent - Context window managed: {TargetIndex} → {WindowSize} messages", 
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
