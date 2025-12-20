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
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Discovery.Agent;
using Platform.Engineering.Copilot.Discovery.Core.Configuration;
using Platform.Engineering.Copilot.Discovery.Agent.Plugins;

namespace Platform.Engineering.Copilot.Discovery.Core;

/// <summary>
/// Specialized agent for resource discovery, inventory, and health monitoring
/// Enhanced with Azure MCP Server integration for comprehensive Azure resource discovery
/// </summary>
public class DiscoveryAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.Discovery;

    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly ILogger<DiscoveryAgent> _logger;
    private readonly AzureMcpClient _azureMcpClient;
    private readonly string? _defaultSubscriptionId;
    private readonly DiscoveryAgentOptions _options;
    private readonly ITokenCounter _tokenCounter;
    private readonly IPromptOptimizer _promptOptimizer;
    private readonly IRagContextOptimizer _ragContextOptimizer;
    private readonly IConversationHistoryOptimizer _conversationHistoryOptimizer;

    public DiscoveryAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<DiscoveryAgent> logger,
        AzureResourceDiscoveryPlugin AzureResourceDiscoveryPlugin,
        AzureMcpClient azureMcpClient,
        IOptions<AzureGatewayOptions> azureOptions,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin,
        IOptions<DiscoveryAgentOptions> options,
        ITokenCounter tokenCounter,
        IPromptOptimizer promptOptimizer,
        IRagContextOptimizer ragContextOptimizer,
        IConversationHistoryOptimizer conversationHistoryOptimizer)
    {
        _logger = logger;
        _azureMcpClient = azureMcpClient;
        _defaultSubscriptionId = azureOptions.Value.SubscriptionId;
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _promptOptimizer = promptOptimizer ?? throw new ArgumentNullException(nameof(promptOptimizer));
        _ragContextOptimizer = ragContextOptimizer ?? throw new ArgumentNullException(nameof(ragContextOptimizer));
        _conversationHistoryOptimizer = conversationHistoryOptimizer ?? throw new ArgumentNullException(nameof(conversationHistoryOptimizer));
        
        // Create specialized kernel for discovery operations
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Discovery);
        _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        // Register resource discovery plugin
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(AzureResourceDiscoveryPlugin, "AzureResourceDiscoveryPlugin"));

        // Log plugin registration
        _logger.LogInformation("🔧 Registered plugins: {PluginNames}", string.Join(", ", _kernel.Plugins.Select(p => p.Name)));
        _logger.LogInformation("🔧 Discovery plugin functions: {FunctionNames}", 
            string.Join(", ", _kernel.Plugins.FirstOrDefault(p => p.Name == "AzureResourceDiscoveryPlugin")?.Select(f => f.Name) ?? Array.Empty<string>()));

        _logger.LogInformation("✅ Discovery Agent initialized (Temperature: {Temperature}, MaxTokens: {MaxTokens}, HealthMonitoring: {HealthMonitoring}, DependencyMapping: {DependencyMapping})",
            _options.Temperature, _options.MaxTokens, _options.EnableHealthMonitoring, _options.EnableDependencyMapping);
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        _logger.LogInformation("🔍 Discovery Agent processing task: {TaskId}", task.TaskId);

        var startTime = DateTime.UtcNow;
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.Discovery,
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
                        "Discovery Agent - Conversation optimization needed: {HealthStatus}", 
                        health.GetHealthSummary());

                    // Manage context window by getting focused message range
                    var managedMessages = await ManageContextWindowAsync(
                        conversationMessages, 
                        _conversationHistoryOptimizer, 
                        conversationMessages.Count - 1,
                        3000);
                    
                    conversationMessages = managedMessages;
                }
            }

            // Build system prompt for discovery expertise
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

                    var optimizedHistory = await OptimizeConversationHistoryAsync(conversationMessages, _conversationHistoryOptimizer, 3000);
                    
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
**IMPORTANT: Previous conversation context (optimized for discovery):**
{historyText}

**The current message is a continuation of this conversation. User has ALREADY provided discovery context.**

**DO NOT ask for information the user already provided above. Instead, USE the information for your discovery operations!**
");
                    }
                }
            }

            // Determine if this is a specific resource query (FORCE function call via prompt)
            var isSpecificResourceQuery = task.Description.Contains("/subscriptions/") || 
                                         task.Description.Contains("resource ID", StringComparison.OrdinalIgnoreCase) ||
                                         (task.Description.Contains("details", StringComparison.OrdinalIgnoreCase) && 
                                          task.Description.Contains("resource", StringComparison.OrdinalIgnoreCase));

            // Phase 5: Optimize prompt to fit token budget before sending to LLM
            var systemPromptText = chatHistory.FirstOrDefault(m => m.Role == Microsoft.SemanticKernel.ChatCompletion.AuthorRole.System)?.Content ?? "";
            var userMessageText = task.Description;
            if (!string.IsNullOrEmpty(systemPromptText) && !string.IsNullOrEmpty(userMessageText))
            {
                var optimizedPrompt = FitPromptInTokenBudget(systemPromptText, userMessageText);
                if (optimizedPrompt.WasOptimized)
                {
                    _logger.LogInformation(
                        "Discovery Agent - Prompt optimized before LLM call: {Strategy}, Tokens saved: {Saved}",
                        optimizedPrompt.OptimizationStrategy, optimizedPrompt.TokensSaved);
                }
            }

            // Execute with configured temperature for discovery operations
            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = _options.Temperature,
                MaxTokens = _options.MaxTokens,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            _logger.LogInformation("🤖 Calling LLM with {PluginCount} plugins, ToolCallBehavior: AutoInvokeKernelFunctions", _kernel.Plugins.Count);

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel);

            _logger.LogInformation("🤖 LLM returned. Metadata items: {MetadataCount}", result.Metadata?.Count ?? 0);
            if (result.Metadata != null)
            {
                foreach (var meta in result.Metadata)
                {
                    _logger.LogInformation("🤖 Metadata: {Key} = {Value}", meta.Key, meta.Value);
                }
            }

            response.Content = result.Content ?? "";
            response.Success = true;

            // Extract metadata
            response.Metadata = ExtractMetadata(result, task);

            // Store result in shared memory for other agents
            memory.AddAgentCommunication(
                task.ConversationId ?? "default",
                AgentType.Discovery,
                AgentType.Orchestrator,
                $"Discovery operation completed: {task.Description}",
                new Dictionary<string, object>
                {
                    ["result"] = result.Content ?? ""
                }
            );

            _logger.LogInformation("✅ Discovery Agent completed task: {TaskId}", task.TaskId);
            
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
            _logger.LogError(ex, "❌ Discovery Agent failed on task: {TaskId}", task.TaskId);
            response.Success = false;
            response.Errors = new List<string> { ex.Message };
        }

        response.ExecutionTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
        return response;
    }

    private string BuildSystemPrompt()
    {
        var subscriptionInfo = !string.IsNullOrEmpty(_defaultSubscriptionId)
            ? $@"

**🔧 DEFAULT CONFIGURATION:**
- Default Subscription ID: {_defaultSubscriptionId}
- When users don't specify a subscription, automatically use the default subscription ID above
- ALWAYS use the default subscription when available unless user explicitly specifies a different one"
            : "";

        return $@"You are a specialized Resource Discovery and Inventory expert with deep expertise in:
{subscriptionInfo}

**Azure Resource Discovery:**
- Comprehensive resource inventory across subscriptions
- Resource hierarchy mapping (Management Groups, Subscriptions, Resource Groups)
- Resource tagging analysis and validation
- Resource dependency mapping and visualization
- Orphaned and unused resource identification

**Health and Performance Monitoring:**
- Resource health status assessment
- Performance metrics collection and analysis
- Availability and uptime tracking
- Alert and incident correlation
- Capacity planning based on usage trends

**Discovery Operations:**
- Discover all resources in subscription/resource group
- Find resources by type, tag, location, or name
- Map resource dependencies (VNets, NICs, Disks, etc.)
- Identify security misconfigurations
- Detect cost optimization opportunities

**Reporting Capabilities:**
- Resource inventory reports (JSON, CSV, Excel)
- Compliance and tagging reports
- Cost allocation by tag/resource group
- Resource lifecycle analysis (creation date, modification date)
- Dependency diagrams and architecture views

**🤖 Conversational Requirements Gathering**

When a user asks about resources, inventory, or discovery, use a conversational approach to gather context:

**For Resource Discovery Requests, ask about:**
- **Scope**: ""What resources would you like me to discover?""
  - All resources in a subscription
  - Resources in a specific resource group
  - Specific resource types (VMs, AKS, Storage, Databases, etc.)
  - Resources with specific tags
  - Resources in a specific location
- **Subscription ID**: Use the default subscription ID unless user specifies a different one
- **Output Format**: ""How would you like the results?""
  - Summary (count by type)
  - Detailed list with properties
  - Inventory report (JSON/CSV)
  - Dependency map

**For Resource Search Requests, ask about:**
- **Search Criteria**: ""What are you looking for?""
  - Resource name pattern (e.g., ""*-prod-*"")
  - Resource type (e.g., ""all AKS clusters"")
  - Tag key/value (e.g., ""Environment=Production"")
  - Location (e.g., ""usgovvirginia"")
- **Search Scope**: ""Where should I search?""
  - Specific subscription
  - All subscriptions (if multi-subscription access)
  - Specific resource groups

**For Dependency Mapping Requests, ask about:**
- **Root Resource**: ""Which resource should I start from?""
  - Resource ID
  - Resource name and resource group
- **Depth**: ""How deep should I map dependencies?""
  - Direct dependencies only
  - Full dependency tree
  - Up to N levels deep

**For Orphaned Resource Detection, ask about:**
- **Resource Types**: ""Which types of resources should I check?""
  - Unattached disks
  - Unused NICs
  - Empty NSGs
  - Idle VMs
  - All of the above
- **Retention Period**: ""How long should a resource be unused before flagging?""
  - 7 days
  - 30 days
  - 90 days

**For Tagging Analysis, ask about:**
- **Required Tags**: ""Which tags are required in your organization?""
  - Common: Environment, Owner, CostCenter, Application
  - Custom tags specific to organization
- **Scope**: ""What should I analyze?""
  - All resources in subscription
  - Specific resource types
  - Specific resource groups

**Example Conversation Flow:**

User: ""What Azure subscriptions do I have access to?"" or ""List all subscriptions"" or ""Show subscriptions""
You: **[CRITICAL: ONLY call list_subscriptions function - STOP AFTER THIS]**
**[DO NOT call discover_azure_resources, discover_resources_with_guidance, or any other discovery function]**
**[Return ONLY the subscription list with: subscription IDs, names, states, tenant IDs]**
**[The user is asking about SUBSCRIPTIONS, not RESOURCES - these are different things]**

User: ""What resources do I have running?"" or ""List all Azure resources"" or ""Discover resources""
You: **[IMMEDIATELY call discover_azure_resources with the default subscription ID - DO NOT ask for subscription if default is configured]**

User: ""Discover resources in subscription 453c...""
You: **[IMMEDIATELY call discover_azure_resources with the specified subscription ID]**

**CRITICAL: Use Available Tools Proactively!**
- If default subscription is configured, USE IT immediately - don't ask
- Call discovery functions directly when you have enough information
- Only ask for clarification on ambiguous requests (e.g., ""which resource type?"")
- DO NOT ask ""Should I proceed?"" or ""Let me know your preferences!"" when you have subscription ID
- DO NOT repeat questions - use smart defaults for minor missing details
- **DO NOT call multiple functions unless explicitly needed - one function call should answer most questions**
- **When listing subscriptions, STOP after list_subscriptions - do not discover resources**

**🎯 CRITICAL: Tool Selection for Resource Queries**

When getting details about a specific Azure resource:

**ALWAYS use get_resource_details for:**
- Normal resource detail queries (""show me details for resource..."", ""get details about..."")
- Resource inventory and discovery operations
- Resource metadata and configuration inspection
- Standard resource information requests
- This function uses Azure Resource Graph for fast, comprehensive data retrieval

**ONLY use get_resource_with_diagnostics when:**
- User EXPLICITLY asks for troubleshooting (""troubleshoot"", ""diagnose"", ""what's wrong with"")
- User EXPLICITLY asks for diagnostics or AppLens data
- User asks about resource health issues or problems
- User needs deep analysis for a malfunctioning resource

**Default behavior:** Use get_resource_details unless user explicitly requests diagnostics/troubleshooting.

**🎯 CRITICAL: Presenting Best Practices and Guidance**

When you call discover_resources_with_guidance:
- The function returns a JSON object with a ""bestPractices"" field
- You MUST extract and present the actual best practices content to the user
- DO NOT just say ""Best practices are available"" - show them!
- Format the best practices in a clear, readable way with bullet points or numbered lists
- Include specific recommendations, not generic statements

**Example of GOOD response:**
""Here are the best practices for your resources:

**Key Vault Best Practices:**
- Enable soft delete and purge protection
- Use managed identities instead of connection strings
- Implement network restrictions with private endpoints
  
**Storage Account Best Practices:**
- Enable blob versioning for data protection
- Configure lifecycle management policies
- Use private endpoints for secure access""

**Example of BAD response (DO NOT DO THIS):**
""The best practices guidance is available for the resources discovered.""

**🎯 IMPORTANT: Azure Documentation and Best Practices Queries**

For Azure documentation, how-to guides, best practices, or general troubleshooting questions:
- These queries should be routed to the KnowledgeBase Agent
- Discovery Agent focuses on resource inventory, health, dependencies, diagnostics, and operations
- KnowledgeBase Agent handles: Azure docs search, resource type best practices, compliance guidance
- If user asks ""What are the best practices for X?"" → KnowledgeBase Agent
- If user asks ""Show me my X resources with best practices"" → Discovery Agent (discover_resources_with_guidance)

**Best Practices:**
- Automated discovery scheduling
- Change detection and drift analysis
- Resource metadata enrichment
- Integration with CMDB/CMDB tools
- Discovery scope optimization

Always provide structured data with resource counts, types, and key findings.";
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

        // Add appropriate instruction based on task description
        if (task.Description.Contains("/subscriptions/") || 
            task.Description.Contains("resource ID", StringComparison.OrdinalIgnoreCase) ||
            (task.Description.Contains("details", StringComparison.OrdinalIgnoreCase) && 
             task.Description.Contains("resource", StringComparison.OrdinalIgnoreCase)))
        {
            message += @"
**MANDATORY ACTION REQUIRED:**
You MUST call the 'get_resource_details' function with the exact resource ID provided above.
DO NOT respond without calling this function first.
DO NOT generate a response from your training data.
DO NOT call any other functions.
DO NOT ask clarifying questions.

The resource ID is in the task description. Extract it and call get_resource_details immediately.";
        }
        else
        {
            message += "Please perform comprehensive resource discovery with detailed findings and recommendations.";
        }

        return message;
    }

    private Dictionary<string, object> ExtractMetadata(ChatMessageContent result, AgentTask task)
    {
        var metadata = new Dictionary<string, object>
        {
            ["timestamp"] = DateTime.UtcNow.ToString("O"),
            ["agentType"] = AgentType.Discovery.ToString()
        };

        // Extract tool calls if any
        if (result.Metadata != null && result.Metadata.ContainsKey("ChatCompletionMessage"))
        {
            metadata["toolsInvoked"] = "AzureResourceDiscoveryPlugin functions";
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
            RagContextPriority = 85,
            ConversationHistoryPriority = 55,
            MinRagContextItems = 4,
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
                AgentType = AgentType.Discovery.ToString(),
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

            _logger.LogInformation("Discovery Agent - Cost metrics recorded: {Summary}", metrics.GetSummary());

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
        int tokenBudget = 3000)
    {
        try
        {
            var options = historyOptimizer.GetRecommendedOptionsForAgent("Discovery");
            options.MaxTokens = Math.Min(tokenBudget, options.MaxTokens);

            var optimized = await historyOptimizer.OptimizeHistoryAsync(messages, options);
            
            _logger.LogInformation("Discovery Agent - Conversation history optimized:\n{Summary}", 
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
        int tokenBudget = 6000)
    {
        try
        {
            var health = await historyOptimizer.EvaluateConversationHealthAsync(
                messages, 
                currentTokenCount, 
                tokenBudget);

            _logger.LogDebug("Discovery Agent - Conversation health evaluated:\n{Summary}", 
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
        int maxTokens = 3000)
    {
        try
        {
            var contextWindow = await historyOptimizer.GetContextWindowAsync(
                messages,
                maxTokens,
                targetMessageIndex);

            _logger.LogDebug("Discovery Agent - Context window managed: {TargetIndex} → {WindowSize} messages", 
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
