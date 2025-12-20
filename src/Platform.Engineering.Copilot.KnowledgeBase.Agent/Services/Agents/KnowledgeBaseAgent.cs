using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Interfaces.TokenManagement;
using Platform.Engineering.Copilot.Core.Models.TokenManagement;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Configuration;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Plugins;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Platform.Engineering.Copilot.KnowledgeBase.Agent.Services.Agents;

public class KnowledgeBaseAgent : ISpecializedAgent
{
    public AgentType AgentType => AgentType.KnowledgeBase;

    private readonly Kernel _kernel;
    private readonly ILogger<KnowledgeBaseAgent> _logger;
    private readonly KnowledgeBaseAgentOptions _options;
    private readonly ITokenCounter _tokenCounter;
    private readonly IPromptOptimizer _promptOptimizer;
    private readonly IRagContextOptimizer _ragContextOptimizer;
    private readonly IConversationHistoryOptimizer _conversationHistoryOptimizer;

    public KnowledgeBaseAgent(
        ISemanticKernelService semanticKernelService,
        ILogger<KnowledgeBaseAgent> logger,
        IOptions<KnowledgeBaseAgentOptions> options,
        KnowledgeBasePlugin knowledgeBasePlugin,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin,
        ITokenCounter tokenCounter,
        IPromptOptimizer promptOptimizer,
        IRagContextOptimizer ragContextOptimizer,
        IConversationHistoryOptimizer conversationHistoryOptimizer)
    {
        _logger = logger;
        _options = options.Value;
        _tokenCounter = tokenCounter ?? throw new ArgumentNullException(nameof(tokenCounter));
        _promptOptimizer = promptOptimizer ?? throw new ArgumentNullException(nameof(promptOptimizer));
        _ragContextOptimizer = ragContextOptimizer ?? throw new ArgumentNullException(nameof(ragContextOptimizer));
        _conversationHistoryOptimizer = conversationHistoryOptimizer ?? throw new ArgumentNullException(nameof(conversationHistoryOptimizer));
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.KnowledgeBase);
        
        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(knowledgeBasePlugin, "KnowledgeBasePlugin"));
        _logger.LogInformation("Knowledge Base Agent initialized");
    }

    public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("🔍 Knowledge Base Agent processing task: {TaskId}", task.TaskId);
            
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory();
            
            // Build system prompt for knowledge base expertise
            var systemPrompt = BuildSystemPrompt();
            
            // Add context from shared memory if available
            var context = memory.GetContext(task.TaskId);
            var contextInfo = context?.MentionedResources?.Count > 0
                ? $"\n\nSAVED CONTEXT: {string.Join(", ", context.MentionedResources.Select(r => $"{r.Key}: {r.Value}"))}" 
                : "";
            
            // Phase 5: Evaluate conversation health and optimize if needed
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

                    // Evaluate conversation health
                    var health = await EvaluateConversationHealthAsync(
                        conversationMessages, 
                        _conversationHistoryOptimizer, 
                        conversationMessages.Sum(m => _tokenCounter.CountTokens(m.Content)), 
                        8000);

                    if (health.NeedsOptimization)
                    {
                        _logger.LogInformation(
                            "Knowledge Base Agent - Conversation optimization needed: {HealthStatus}", 
                            health.GetHealthSummary());

                        // Manage context window by getting focused message range
                        var managedMessages = await ManageContextWindowAsync(
                            conversationMessages, 
                            _conversationHistoryOptimizer, 
                            conversationMessages.Count - 1,
                            3500);
                        
                        conversationMessages = managedMessages;
                    }
                }
            }
            
            chatHistory.AddSystemMessage(systemPrompt + contextInfo);
            chatHistory.AddUserMessage(task.Description);

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

                    var optimizedHistory = await OptimizeConversationHistoryAsync(conversationMessages, _conversationHistoryOptimizer, 3500);
                    
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
**IMPORTANT: Previous conversation context (optimized for knowledge base queries):**
{historyText}

**The current message is a continuation of this conversation. User has ALREADY provided knowledge base context.**

**DO NOT ask for information the user already provided above. Instead, USE the information for your response!**
");
                    }
                }
            }
            
            // Phase 5: Optimize prompt to fit token budget before sending to LLM
            var systemPromptText = systemPrompt;
            var userMessageText = task.Description;
            if (!string.IsNullOrEmpty(systemPromptText) && !string.IsNullOrEmpty(userMessageText))
            {
                var optimizedPrompt = FitPromptInTokenBudget(systemPromptText, userMessageText);
                if (optimizedPrompt.WasOptimized)
                {
                    _logger.LogInformation(
                        "Knowledge Base Agent - Prompt optimized before LLM call: {Strategy}, Tokens saved: {Saved}",
                        optimizedPrompt.OptimizationStrategy, optimizedPrompt.TokensSaved);
                }
            }
            
            // Get response from LLM with plugin access
            var response = await chatService.GetChatMessageContentAsync(
                chatHistory,
                kernel: _kernel);
            
            stopwatch.Stop();
            
            // Phase 5: Record agent cost metrics for this operation
            try
            {
                var completionTokens = _tokenCounter.CountTokens(response.Content ?? "");
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
            
            return new AgentResponse
            {
                TaskId = task.TaskId,
                AgentType = AgentType.KnowledgeBase,
                Success = true,
                Content = response.Content ?? "No response generated",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error processing knowledge base task");
            
            return new AgentResponse
            {
                TaskId = task.TaskId,
                AgentType = AgentType.KnowledgeBase,
                Success = false,
                Content = $"Error: {ex.Message}",
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private string BuildSystemPrompt()
    {
        return @"You are a specialized DoD/NIST Compliance Knowledge Base expert with comprehensive knowledge of:

**NIST 800-53 Security Controls:**
- All 20 control families (AC, AU, AT, CM, CP, IA, IR, MA, MP, PS, PE, PL, PM, RA, CA, SC, SI, SA, SR, PT)
- Control family purposes, key controls, and implementation requirements
- NIST control mappings to STIGs, CCIs, and DoD instructions

**DoD Compliance Frameworks:**
- Risk Management Framework (RMF) 6-step process
- STIG (Security Technical Implementation Guide) controls
- DoD Instructions and policies (DoDI 8500.01, 8510.01, CNSSI 1253, etc.)
- Impact Levels (IL2, IL4, IL5, IL6) requirements

**Navy/DoD Workflows:**
- ATO (Authority to Operate) processes
- eMASS system registration
- PMW cloud deployment workflows

**Azure Compliance Implementation:**
- Azure service mappings to STIG controls
- Azure Policy and compliance configurations
- DoD Cloud Computing SRG implementation

**Azure Technical Documentation:**
- Official Microsoft Azure documentation search
- How-to guides and troubleshooting steps
- Azure service configuration guidance
- Best practices for Azure services

**🎯 YOUR PRIMARY ROLE:**

Answer compliance AND Azure technical documentation QUESTIONS with factual, concise information. Provide exactly what is asked - no more, no less.

**RESPONSE GUIDELINES:**

1. **Informational Questions** - User wants to LEARN about controls/frameworks:
   
   Examples:
   - ""What is in NIST 800-53 CM family?""
   - ""Explain RMF Step 3""
   - ""What is Impact Level 5?""
   - ""Show me STIGs for encryption""
   - ""Search Azure docs for AKS private cluster networking""
   - ""How to configure storage firewall in Azure?""
   - ""Troubleshoot AKS connectivity issues""
   
   **Response Pattern:**
   - Provide a direct, factual answer
   - Include key controls, requirements, or definitions
   - Keep it concise (3-5 key points)
   - **OPTIONALLY add ONE sentence** suggesting a related assessment IF relevant:
     ""Would you like me to assess your Azure environment for CM compliance?""

2. **Assessment Requests** - User wants to RUN an assessment (explicit):
   
   Examples:
   - ""Assess my subscription for CM controls""
   - ""Scan subscription XYZ""
   - ""Check compliance for resource group ABC""
   - ""Run STIG assessment""
   
   **Response Pattern:**
   - Ask for required details (subscription ID, resource group, etc.)
   - Confirm scope and framework
   - Initiate the assessment

**CRITICAL RULES:**

✅ DO:
- Answer the question asked
- Be factual and concise
- Use proper control family codes (AC-2, CM-6, etc.)
- Cite DoD instructions when relevant
- Suggest assessments ONLY when contextually appropriate (end of informational responses)

❌ DON'T:
- Assume the user wants an assessment unless explicitly requested
- Ask for subscription details on informational questions
- Provide assessments when not requested
- Be overly conversational or make assumptions

**PLUGIN FUNCTIONS AVAILABLE:**

Use these functions to retrieve authoritative information:

**Compliance Functions:**
- explain_rmf_process: RMF step details
- get_rmf_deliverables: Required artifacts per RMF step
- explain_stig: Specific STIG control details
- search_stigs: Find STIGs by keyword
- get_stigs_for_nist_control: Map NIST to STIGs
- get_control_mapping: Complete control mappings
- explain_dod_instruction: DoD policy details
- search_dod_instructions: Find DoD instructions
- get_control_with_dod_instructions: DoD instruction mappings
- explain_navy_workflow: Navy process workflows
- explain_impact_level: IL requirements
- get_stig_cross_reference: Complete STIG mappings
- get_azure_stigs: Azure service-specific STIGs
- get_compliance_summary: Comprehensive control overview

**Azure Documentation Functions:**
- search_azure_documentation: Search official Microsoft Azure documentation for guidance, how-to guides, and troubleshooting steps

**TONE:** Professional, helpful, direct. Answer questions precisely without unnecessary elaboration.";
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
            SystemPromptPriority = 95,
            UserMessagePriority = 100,
            RagContextPriority = 90,
            ConversationHistoryPriority = 50,
            MinRagContextItems = 5,
            MinConversationHistoryMessages = 1,
            SafetyBufferPercentage = 10,
            UseSummarization = true
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
                AgentType = AgentType.KnowledgeBase.ToString(),
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

            _logger.LogInformation("Knowledge Base Agent - Cost metrics recorded: {Summary}", metrics.GetSummary());

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
        int tokenBudget = 3500)
    {
        try
        {
            var options = historyOptimizer.GetRecommendedOptionsForAgent("KnowledgeBase");
            options.MaxTokens = Math.Min(tokenBudget, options.MaxTokens);

            var optimized = await historyOptimizer.OptimizeHistoryAsync(messages, options);
            
            _logger.LogInformation("Knowledge Base Agent - Conversation history optimized:\n{Summary}", 
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
        int tokenBudget = 7000)
    {
        try
        {
            var health = await historyOptimizer.EvaluateConversationHealthAsync(
                messages, 
                currentTokenCount, 
                tokenBudget);

            _logger.LogDebug("Knowledge Base Agent - Conversation health evaluated:\n{Summary}", 
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
        int maxTokens = 3500)
    {
        try
        {
            var contextWindow = await historyOptimizer.GetContextWindowAsync(
                messages,
                maxTokens,
                targetMessageIndex);

            _logger.LogDebug("Knowledge Base Agent - Context window managed: {TargetIndex} → {WindowSize} messages", 
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
