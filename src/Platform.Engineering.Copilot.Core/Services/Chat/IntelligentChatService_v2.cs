using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Services.Cache;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// AI-powered intelligent chat service using Semantic Kernel automatic function calling
/// V2: Simplified architecture using SK plugins instead of manual intent classification
/// </summary>
public class IntelligentChatService_v2 : IIntelligentChatService
{
    private readonly ISemanticKernelService _semanticKernel;
    private readonly ILogger<IntelligentChatService_v2> _logger;
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly IIntelligentChatCacheService? _cacheService;
    
    // In-memory conversation store (replace with distributed cache in production)
    private static readonly Dictionary<string, ConversationContext> _conversations = new();
    private static readonly object _conversationLock = new();

    public IntelligentChatService_v2(
        ISemanticKernelService semanticKernel,
        Kernel kernel,
        ILogger<IntelligentChatService_v2> logger,
        IIntelligentChatCacheService? cacheService = null)
    {
        _semanticKernel = semanticKernel;
        _kernel = kernel;
        _logger = logger;
        _cacheService = cacheService;
        _chatCompletion = kernel.GetRequiredService<IChatCompletionService>();
    }

    /// <summary>
    /// Process a user message with AI-powered automatic function calling
    /// </summary>
    public async Task<IntelligentChatResponse> ProcessMessageAsync(
        string message,
        string conversationId,
        ConversationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Processing message for conversation {ConversationId}", conversationId);

            // Get or create conversation context
            context ??= await GetOrCreateContextAsync(conversationId, cancellationToken: cancellationToken);

            // Build chat history from context
            var chatHistory = BuildChatHistory(context);
            chatHistory.AddUserMessage(message);

            // Configure Semantic Kernel to automatically invoke functions
            var settings = new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 0.7,
                MaxTokens = 2000
            };

            _logger.LogInformation("Invoking Semantic Kernel with {PluginCount} plugins registered", _kernel.Plugins.Count);

            // Semantic Kernel automatically:
            // 1. Discovers available functions from registered plugins
            // 2. Determines which function(s) to call based on user message
            // 3. Extracts parameters from natural language
            // 4. Invokes the function(s)
            // 5. Returns the result
            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: settings,
                kernel: _kernel,
                cancellationToken: cancellationToken);

            _logger.LogInformation("SK execution completed. Response length: {Length} chars", result.Content?.Length ?? 0);

            // Update context with assistant response
            var userSnapshot = new MessageSnapshot
            {
                Role = "user",
                Content = message,
                Timestamp = DateTime.UtcNow
            };
            await UpdateContextAsync(context, userSnapshot, cancellationToken);

            var assistantSnapshot = new MessageSnapshot
            {
                Role = "assistant",
                Content = result.Content ?? "No response generated",
                Timestamp = DateTime.UtcNow
            };
            await UpdateContextAsync(context, assistantSnapshot, cancellationToken);

            // Extract metadata from SK execution
            var functionCalls = ExtractFunctionCalls(result);
            var toolExecuted = functionCalls?.Count > 0;
            var toolName = toolExecuted ? GetFirstFunctionName(functionCalls) : null;

            if (toolExecuted)
            {
                _logger.LogInformation("Tool executed: {ToolName}, Function calls: {Count}", toolName, functionCalls?.Count);
            }

            // Generate proactive suggestions
            var suggestions = await GenerateProactiveSuggestionsAsync(
                conversationId, context, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Processed message in {ElapsedMs}ms. Tool executed: {ToolExecuted}, Tool: {ToolName}", 
                stopwatch.ElapsedMilliseconds, 
                toolExecuted,
                toolName ?? "none");

            return new IntelligentChatResponse
            {
                Response = result.Content ?? "No response generated",
                Intent = new IntentClassificationResult
                {
                    IntentType = toolExecuted ? "tool_execution" : "conversational",
                    ToolName = toolName,
                    Confidence = 0.95, // SK has high confidence in its function selection
                    RequiresFollowUp = false
                },
                ConversationId = conversationId,
                ToolExecuted = toolExecuted,
                Suggestions = suggestions,
                Context = context,
                Metadata = new ResponseMetadata
                {
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    ModelUsed = "gpt-4o" // TODO: Extract from kernel config
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message for conversation {ConversationId}", conversationId);
            
            // Return user-friendly error
            return new IntelligentChatResponse
            {
                Response = $"I encountered an error processing your request: {ex.Message}. Please try rephrasing your question or contact support if the issue persists.",
                Intent = new IntentClassificationResult
                {
                    IntentType = "error",
                    Confidence = 1.0
                },
                ConversationId = conversationId,
                ToolExecuted = false,
                Metadata = new ResponseMetadata
                {
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    ModelUsed = "gpt-4"
                }
            };
        }
    }

    /// <summary>
    /// Build ChatHistory from ConversationContext with simplified system prompt
    /// </summary>
    private ChatHistory BuildChatHistory(ConversationContext context)
    {
        var history = new ChatHistory();
        
        // Simplified system prompt - no tool descriptions needed!
        // SK discovers functions automatically from registered plugins
        history.AddSystemMessage(@"You are an expert Azure cloud platform assistant specializing in infrastructure, compliance, security, and cost management.

**Your Capabilities:**
- Infrastructure: Provision resources, generate IaC templates (Terraform/Bicep), validate configurations
- Compliance: Run ATO assessments, collect evidence, analyze security posture
- Cost Management: Analyze spending, optimize costs, configure budget alerts
- Security: Apply hardening, baseline assessments, incident response setup
- Onboarding: Guide Navy Flankspeed mission owners through provisioning
- Resource Discovery: Find and analyze Azure resources
- Document Analysis: Upload and analyze security documents (SSP, POA&M, architecture diagrams)

**Interaction Style:**
- Be proactive - suggest next steps and best practices
- Ask clarifying questions when needed
- Explain technical concepts clearly
- Provide specific, actionable guidance

When users ask questions, use the available functions to help them accomplish their goals. The system will automatically discover and call the appropriate functions based on the user's request.");

        // Add recent conversation history (last 10 messages for context)
        foreach (var msg in context.MessageHistory.TakeLast(10))
        {
            if (msg.Role == "user")
                history.AddUserMessage(msg.Content);
            else if (msg.Role == "assistant")
                history.AddAssistantMessage(msg.Content);
        }

        return history;
    }

    /// <summary>
    /// Extract function calls from SK result metadata
    /// </summary>
    private List<object>? ExtractFunctionCalls(ChatMessageContent result)
    {
        try
        {
            if (result.Metadata == null)
                return null;

            // Try different metadata keys that SK might use
            var possibleKeys = new[] { "FunctionCalls", "ToolCalls", "function_calls", "tool_calls" };
            
            foreach (var key in possibleKeys)
            {
                if (result.Metadata.TryGetValue(key, out var value) && value is IList<object> calls)
                {
                    return calls.ToList();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract function calls from result metadata");
            return null;
        }
    }

    /// <summary>
    /// Extract first function name from function calls metadata
    /// </summary>
    private string? GetFirstFunctionName(IList<object>? functionCalls)
    {
        if (functionCalls == null || functionCalls.Count == 0)
            return null;
        
        try
        {
            var firstCall = functionCalls[0];
            var type = firstCall.GetType();
            
            // Try common property names for function name
            var possibleProps = new[] { "FunctionName", "Name", "name", "function_name", "ToolName", "tool_name" };
            
            foreach (var propName in possibleProps)
            {
                var prop = type.GetProperty(propName);
                if (prop != null)
                {
                    var value = prop.GetValue(firstCall);
                    if (value != null)
                        return value.ToString();
                }
            }

            return firstCall.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract function name");
            return null;
        }
    }

    /// <summary>
    /// Generate proactive suggestions based on conversation context
    /// </summary>
    public async Task<List<ProactiveSuggestion>> GenerateProactiveSuggestionsAsync(
        string conversationId,
        ConversationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            context ??= await GetOrCreateContextAsync(conversationId, cancellationToken: cancellationToken);

            var suggestions = new List<ProactiveSuggestion>();

            // Get recent context
            var recentMessages = context.MessageHistory.TakeLast(5).ToList();
            var recentTools = context.UsedTools.TakeLast(3).ToList();

            // Suggest next steps based on recently used tools
            if (recentTools.Contains("infrastructure_provisioning"))
            {
                suggestions.Add(new ProactiveSuggestion
                {
                    Title = "Run Compliance Assessment",
                    Description = "Assess your newly provisioned infrastructure for ATO compliance",
                    ToolName = "run_compliance_assessment",
                    Priority = "high"
                });

                suggestions.Add(new ProactiveSuggestion
                {
                    Title = "Set Up Cost Monitoring",
                    Description = "Configure budget alerts to monitor spending on new resources",
                    ToolName = "configure_budget_alerts",
                    Priority = "medium"
                });
            }

            if (recentTools.Contains("ato_compliance"))
            {
                suggestions.Add(new ProactiveSuggestion
                {
                    Title = "Apply Security Hardening",
                    Description = "Implement recommended security controls from compliance assessment",
                    ToolName = "apply_security_hardening",
                    Priority = "high"
                });

                suggestions.Add(new ProactiveSuggestion
                {
                    Title = "Collect Compliance Evidence",
                    Description = "Start collecting evidence for ATO documentation",
                    ToolName = "collect_compliance_evidence",
                    Priority = "medium"
                });
            }

            // General helpful suggestions
            if (suggestions.Count == 0)
            {
                suggestions.Add(new ProactiveSuggestion
                {
                    Title = "Discover Azure Resources",
                    Description = "Find and inventory your Azure resources",
                    ToolName = "discover_azure_resources",
                    Priority = "low"
                });

                suggestions.Add(new ProactiveSuggestion
                {
                    Title = "Analyze Costs",
                    Description = "Review your Azure spending and find optimization opportunities",
                    ToolName = "analyze_azure_costs",
                    Priority = "low"
                });
            }

            return suggestions.Take(3).ToList(); // Limit to 3 suggestions
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to generate proactive suggestions");
            return new List<ProactiveSuggestion>();
        }
    }

    /// <summary>
    /// Get or create conversation context
    /// </summary>
    public Task<ConversationContext> GetOrCreateContextAsync(
        string conversationId,
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        lock (_conversationLock)
        {
            if (_conversations.TryGetValue(conversationId, out var context))
            {
                context.LastActivityAt = DateTime.UtcNow;
                return Task.FromResult(context);
            }

            var newContext = new ConversationContext
            {
                ConversationId = conversationId,
                UserId = userId,
                StartedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                IsActive = true,
                MessageHistory = new List<MessageSnapshot>(),
                UsedTools = new List<string>(),
                WorkflowState = new Dictionary<string, object?>()
            };

            _conversations[conversationId] = newContext;
            
            _logger.LogInformation("Created new conversation context: {ConversationId}", conversationId);
            
            return Task.FromResult(newContext);
        }
    }

    /// <summary>
    /// Update conversation context with new message
    /// </summary>
    public Task UpdateContextAsync(
        ConversationContext context,
        MessageSnapshot message,
        CancellationToken cancellationToken = default)
    {
        lock (_conversationLock)
        {
            context.MessageHistory.Add(message);
            context.MessageCount++;
            context.LastActivityAt = DateTime.UtcNow;

            // Keep only last 20 messages to avoid context bloat
            if (context.MessageHistory.Count > 20)
            {
                context.MessageHistory = context.MessageHistory.TakeLast(20).ToList();
            }

            // Track used tools
            if (!string.IsNullOrEmpty(message.ToolExecuted) && !context.UsedTools.Contains(message.ToolExecuted))
            {
                context.UsedTools.Add(message.ToolExecuted);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Invalidate cache for a conversation (if caching is enabled)
    /// </summary>
    public async Task InvalidateCacheAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (_cacheService != null)
        {
            // Remove intent classification cache for this conversation
            var cacheKey = _cacheService.GenerateCacheKey("intent", conversationId);
            await _cacheService.RemoveAsync(cacheKey, cancellationToken);
            _logger.LogInformation("Invalidated cache for conversation {ConversationId}", conversationId);
        }
    }

    /// <summary>
    /// NOTE: ExecuteToolChainAsync is NO LONGER NEEDED in v2
    /// Semantic Kernel handles multi-step operations automatically.
    /// This stub is kept for interface compatibility.
    /// </summary>
    [Obsolete("Use ProcessMessageAsync instead - SK handles tool chains automatically")]
    public Task<ToolChainResult> ExecuteToolChainAsync(
        List<ToolStep> steps,
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ExecuteToolChainAsync called but is obsolete in v2. SK handles multi-step operations automatically.");
        
        return Task.FromResult(new ToolChainResult
        {
            Status = "failed",
            Steps = steps ?? new List<ToolStep>()
        });
    }

    /// <summary>
    /// NOTE: ClassifyIntentAsync is NO LONGER NEEDED in v2
    /// Semantic Kernel handles intent classification automatically via function calling.
    /// This stub is kept for interface compatibility if needed.
    /// </summary>
    [Obsolete("Use ProcessMessageAsync instead - SK handles intent classification automatically")]
    public Task<IntentClassificationResult> ClassifyIntentAsync(
        string message,
        ConversationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ClassifyIntentAsync called but is obsolete in v2. Use ProcessMessageAsync instead.");
        
        return Task.FromResult(new IntentClassificationResult
        {
            IntentType = "conversational",
            Confidence = 0.5
        });
    }
}
