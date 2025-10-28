#pragma warning disable SKEXP0010 // ResponseFormat is experimental

using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Interfaces;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.Agents;

/// <summary>
/// Orchestrator agent that coordinates and plans execution across specialized agents
/// This is the "brain" of the multi-agent system
/// </summary>
public class OrchestratorAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletion;
    private readonly Dictionary<AgentType, ISpecializedAgent> _agents;
    private readonly SharedMemory _sharedMemory;
    private readonly ExecutionPlanValidator _planValidator;
    private readonly ExecutionPlanCache _planCache;
    private readonly ILogger<OrchestratorAgent> _logger;

    public OrchestratorAgent(
        ISemanticKernelService semanticKernelService,
        IEnumerable<ISpecializedAgent> agents,
        SharedMemory sharedMemory,
        ExecutionPlanValidator planValidator,
        ExecutionPlanCache planCache,
        ILogger<OrchestratorAgent> logger)
    {
        _logger = logger;
        _sharedMemory = sharedMemory;
        _planValidator = planValidator;
        _planCache = planCache;

        // Create orchestrator's own kernel
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Orchestrator);
        _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

        // Build agent registry
        _agents = agents.ToDictionary(a => a.AgentType, a => a);

        _logger.LogInformation("🎼 OrchestratorAgent initialized with {AgentCount} specialized agents",
            _agents.Count);
    }

    /// <summary>
    /// Process a user request by coordinating specialized agents
    /// </summary>
    public async Task<OrchestratedResponse> ProcessRequestAsync(
        string userMessage,
        string conversationId,
        ConversationContext? existingContext = null,  // ACCEPT EXISTING CONTEXT WITH HISTORY
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        _logger.LogInformation("🎼 Orchestrator processing request: {Message} [ConversationId: {ConversationId}]", 
            userMessage, conversationId);

        // Use existing context if provided (preserves message history), otherwise create new
        var context = existingContext ?? new ConversationContext
        {
            ConversationId = conversationId,
            LastActivityAt = DateTime.UtcNow
        };
        
        // Update activity timestamp
        context.LastActivityAt = DateTime.UtcNow;

        // Store context in shared memory so agents can access message history
        _sharedMemory.StoreContext(conversationId, context);

        try
        {
            // OPTIMIZATION: Fast-path for unambiguous single-agent requests (skip planning LLM call)
            // Only use when request clearly maps to ONE specific agent with no multi-agent coordination needed
            var fastPathAgent = DetectUnambiguousSingleAgentRequest(userMessage, context);
            if (fastPathAgent.HasValue)
            {
                _logger.LogInformation("⚡ Fast-path detected: Unambiguous {AgentType} request - skipping orchestrator planning", 
                    fastPathAgent.Value);
                
                var agent = _agents[fastPathAgent.Value];
                var task = new AgentTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    AgentType = fastPathAgent.Value,
                    Description = userMessage,
                    Priority = 1,
                    IsCritical = true,
                    ConversationId = conversationId
                };
                
                var response = await agent.ProcessAsync(task, _sharedMemory);
                
                var fastPathExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                
                return new OrchestratedResponse
                {
                    FinalResponse = response.Content,
                    PrimaryIntent = fastPathAgent.Value.ToString().ToLowerInvariant(),
                    AgentsInvoked = new List<AgentType> { fastPathAgent.Value },
                    ExecutionPattern = ExecutionPattern.Sequential,
                    TotalAgentCalls = 1,
                    ExecutionTimeMs = fastPathExecutionTime,
                    Success = response.Success,
                    RequiresFollowUp = DetermineIfFollowUpNeeded(new List<AgentResponse> { response }),
                    FollowUpPrompt = GenerateFollowUpPrompt(new List<AgentResponse> { response }),
                    MissingFields = ExtractMissingFields(new List<AgentResponse> { response }),
                    QuickReplies = GenerateQuickReplies(fastPathAgent.Value.ToString().ToLowerInvariant(), 
                        new List<AgentResponse> { response }),
                    Metadata = response.Metadata,
                    Errors = response.Errors
                };
            }
            
            // Step 1: Analyze intent and create execution plan
            var plan = await CreateExecutionPlanAsync(userMessage, context);

            _logger.LogInformation("📋 Execution plan created: {Pattern} with {TaskCount} tasks",
                plan.ExecutionPattern, plan.Tasks.Count);

            // Step 2: Execute plan based on pattern
            List<AgentResponse> responses;
            switch (plan.ExecutionPattern)
            {
                case ExecutionPattern.Sequential:
                    responses = await ExecuteSequentialAsync(plan.Tasks, context.ConversationId);
                    break;

                case ExecutionPattern.Parallel:
                    responses = await ExecuteParallelAsync(plan.Tasks, context.ConversationId);
                    break;

                case ExecutionPattern.Collaborative:
                    responses = await ExecuteCollaborativeAsync(userMessage, context.ConversationId, plan);
                    break;

                default:
                    throw new ArgumentException($"Unknown execution pattern: {plan.ExecutionPattern}");
            }

            // Step 3: Synthesize final response (OPTIMIZATION: Skip LLM call for single-agent responses)
            string finalResponse;
            if (responses.Count == 1 && responses[0].Success)
            {
                _logger.LogInformation("⚡ Single agent response - skipping synthesis LLM call");
                finalResponse = responses[0].Content;
            }
            else
            {
                finalResponse = await SynthesizeResponseAsync(userMessage, responses, context);
            }

            var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Build orchestrated response
            var orchestratedResponse = new OrchestratedResponse
            {
                FinalResponse = finalResponse,
                PrimaryIntent = plan.PrimaryIntent,
                AgentsInvoked = responses.Select(r => r.AgentType).Distinct().ToList(),
                ExecutionPattern = plan.ExecutionPattern,
                TotalAgentCalls = responses.Count,
                ExecutionTimeMs = executionTime,
                Success = responses.All(r => r.Success),
                RequiresFollowUp = DetermineIfFollowUpNeeded(responses),
                FollowUpPrompt = GenerateFollowUpPrompt(responses),
                MissingFields = ExtractMissingFields(responses),
                QuickReplies = GenerateQuickReplies(plan.PrimaryIntent, responses),
                Metadata = CombineMetadata(responses),
                Errors = responses.SelectMany(r => r.Errors).ToList()
            };

            _logger.LogInformation("✅ Orchestration complete in {ExecutionTime}ms: {AgentCount} agents invoked",
                executionTime, orchestratedResponse.AgentsInvoked.Count);

            return orchestratedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error in orchestrator processing");

            var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

            return new OrchestratedResponse
            {
                FinalResponse = $"I encountered an error while processing your request: {ex.Message}",
                PrimaryIntent = "error",
                Success = false,
                ExecutionTimeMs = executionTime,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    /// <summary>
    /// Create an execution plan by analyzing the user's request
    /// </summary>
    private async Task<ExecutionPlan> CreateExecutionPlanAsync(
        string userMessage,
        ConversationContext context)
    {
        _logger.LogDebug("📋 Creating execution plan for: {Message}", userMessage);

        // OPTIMIZATION: Try to get cached plan for similar request
        var cachedPlan = _planCache.TryGetCachedPlan(userMessage, context);
        if (cachedPlan != null)
        {
            _logger.LogInformation("♻️  Using cached execution plan - skipping planning LLM call");
            return cachedPlan;
        }

        var availableAgents = string.Join("\n", _agents.Keys.Select(a =>
            $"- {a}: {GetAgentDescription(a)}"));

        // Include recent conversation history for context
        var conversationHistory = context.MessageHistory.Count > 0 
            ? "\n\nRecent conversation history:\n" + string.Join("\n", context.MessageHistory.TakeLast(5).Select(m => $"{m.Role}: {m.Content}"))
            : "";

        // OPTIMIZATION: Simplified planning prompt (70% token reduction from 2000 to ~600 tokens)
        var planningPrompt = $@"Available agents: {string.Join(", ", _agents.Keys)}
{conversationHistory}

User: ""{userMessage}""

Create JSON execution plan:
{{
  ""primaryIntent"": ""infrastructure|compliance|cost|environment|discovery|onboarding|mixed"",
  ""tasks"": [{{ ""agentType"": ""Infrastructure"", ""description"": ""task"", ""priority"": 1, ""isCritical"": true }}],
  ""executionPattern"": ""Sequential|Parallel|Collaborative"",
  ""estimatedTimeSeconds"": 30
}}

CRITICAL Rules (apply in order):
1. **Conversation continuation**: If assistant previously asked questions and user is answering → continue same task
2. **Compliance scanning** (""check""/""scan""/""assess"" + compliance) → Compliance agent, primaryIntent: ""compliance""
3. **Template generation** (""create""/""deploy""/""I need"" infra) → Infrastructure ONLY, primaryIntent: ""infrastructure""
4. **Actual provisioning** (""actually provision""/""make it live"") → All 5 agents Sequential
5. **Informational** (""What are...""/""How do..."") → Single relevant agent

Default: Template generation (Infrastructure only) - safe, no real Azure resources.

Respond ONLY with JSON.";

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are an expert at planning multi-agent execution strategies. Always respond with valid JSON.");
            chatHistory.AddUserMessage(planningPrompt);

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new OpenAIPromptExecutionSettings
                {
                    ResponseFormat = "json_object",
                    Temperature = 0.3,
                    MaxTokens = 500  // OPTIMIZATION: Reduced from 2000 (only need small JSON plan)
                });

            var planJson = result.Content ?? "{}";
            _logger.LogInformation("📋 RAW PLAN JSON FROM LLM: {PlanJson}", planJson);

            var plan = JsonSerializer.Deserialize<ExecutionPlanDto>(planJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (plan == null || plan.Tasks == null || !plan.Tasks.Any())
            {
                _logger.LogWarning("⚠️  Invalid plan generated, falling back to single infrastructure task");
                return CreateFallbackPlan(userMessage);
            }

            // Convert DTO to domain model
            var executionPlan = new ExecutionPlan
            {
                PrimaryIntent = plan.PrimaryIntent ?? "unknown",
                ExecutionPattern = ParseExecutionPattern(plan.ExecutionPattern),
                EstimatedTimeSeconds = plan.EstimatedTimeSeconds ?? 30,
                Tasks = plan.Tasks.Select(t => new AgentTask
                {
                    AgentType = ParseAgentType(t.AgentType),
                    Description = t.Description ?? userMessage,
                    Priority = t.Priority ?? 0,
                    IsCritical = t.IsCritical ?? false,
                    ConversationId = context.ConversationId
                }).ToList()
            };

            _logger.LogInformation("📋 Plan created: {Intent} → {Pattern} → {TaskCount} tasks",
                executionPlan.PrimaryIntent,
                executionPlan.ExecutionPattern,
                executionPlan.Tasks.Count);

            // Validate and potentially correct the plan
            var validatedPlan = _planValidator.ValidateAndCorrect(executionPlan, userMessage, context.ConversationId);

            // OPTIMIZATION: Cache the validated plan for future similar requests
            _planCache.CachePlan(userMessage, validatedPlan);

            return validatedPlan;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error creating execution plan, using fallback");
            return CreateFallbackPlan(userMessage);
        }
    }

    /// <summary>
    /// Execute tasks sequentially (one after another)
    /// </summary>
    private async Task<List<AgentResponse>> ExecuteSequentialAsync(
        List<AgentTask> tasks,
        string conversationId)
    {
        _logger.LogInformation("▶️  Executing {TaskCount} tasks sequentially", tasks.Count);

        var responses = new List<AgentResponse>();
        // Sort by Priority ASCENDING (1, 2, 3, 4, 5) so Priority 1 executes FIRST
        var sortedTasks = tasks.OrderBy(t => t.Priority).ToList();

        foreach (var task in sortedTasks)
        {
            if (!_agents.TryGetValue(task.AgentType, out var agent))
            {
                _logger.LogWarning("⚠️  No agent found for type: {AgentType}", task.AgentType);
                continue;
            }

            _logger.LogInformation("🤖 Executing task with {AgentType}: {Description}",
                task.AgentType, task.Description);

            // Add previous results to shared memory
            if (responses.Any())
            {
                var context = _sharedMemory.GetContext(conversationId);
                context.PreviousResults = responses;
                _sharedMemory.StoreContext(conversationId, context);
            }

            var response = await agent.ProcessAsync(task, _sharedMemory);
            responses.Add(response);

            _logger.LogInformation("✅ Task completed: {AgentType} → Success: {Success}",
                task.AgentType, response.Success);

            // Stop if critical task failed
            if (!response.Success && task.IsCritical)
            {
                _logger.LogWarning("⛔ Critical task failed, stopping execution");
                break;
            }
        }

        return responses;
    }

    /// <summary>
    /// Execute tasks in parallel (simultaneously)
    /// </summary>
    private async Task<List<AgentResponse>> ExecuteParallelAsync(
        List<AgentTask> tasks,
        string conversationId)
    {
        _logger.LogInformation("⚡ Executing {TaskCount} tasks in parallel", tasks.Count);

        var agentTasks = tasks
            .Where(t => _agents.ContainsKey(t.AgentType))
            .Select(async task =>
            {
                var agent = _agents[task.AgentType];
                _logger.LogInformation("🤖 Starting parallel task: {AgentType}", task.AgentType);

                var response = await agent.ProcessAsync(task, _sharedMemory);

                _logger.LogInformation("✅ Parallel task completed: {AgentType} → Success: {Success}",
                    task.AgentType, response.Success);

                return response;
            })
            .ToList();

        var responses = await Task.WhenAll(agentTasks);

        return responses.ToList();
    }

    /// <summary>
    /// Execute tasks collaboratively (agents iterate and refine)
    /// </summary>
    private async Task<List<AgentResponse>> ExecuteCollaborativeAsync(
        string userMessage,
        string conversationId,
        ExecutionPlan plan)
    {
        _logger.LogInformation("🔄 Executing collaborative workflow with {TaskCount} tasks",
            plan.Tasks.Count);

        var responses = new List<AgentResponse>();
        var maxRounds = 3;
        var currentRound = 0;
        var allApproved = false;

        while (!allApproved && currentRound < maxRounds)
        {
            currentRound++;
            _logger.LogInformation("🔄 Collaboration round {Round}/{MaxRounds}", currentRound, maxRounds);

            // Execute all tasks in this round
            foreach (var task in plan.Tasks)
            {
                if (!_agents.TryGetValue(task.AgentType, out var agent))
                    continue;

                // Update task description with previous round feedback
                if (currentRound > 1)
                {
                    task.Description = $"{task.Description}\n\nPrevious feedback:\n{GetFeedbackSummary(responses)}";
                }

                var response = await agent.ProcessAsync(task, _sharedMemory);
                responses.Add(response);

                _logger.LogInformation("🤖 {AgentType} completed round {Round}: Success={Success}",
                    task.AgentType, currentRound, response.Success);
            }

            // Check if all agents are satisfied
            var latestResponses = responses.Skip(responses.Count - plan.Tasks.Count).ToList();
            allApproved = CheckCollaborativeApproval(latestResponses);

            if (allApproved)
            {
                _logger.LogInformation("✅ Collaborative workflow approved after {Round} rounds", currentRound);
            }
            else if (currentRound < maxRounds)
            {
                _logger.LogInformation("🔄 Needs refinement, starting round {NextRound}", currentRound + 1);
            }
        }

        if (!allApproved)
        {
            _logger.LogWarning("⚠️  Collaborative workflow completed {MaxRounds} rounds without full approval", maxRounds);
        }

        return responses;
    }

    /// <summary>
    /// Synthesize a final response from multiple agent responses
    /// </summary>
    private async Task<string> SynthesizeResponseAsync(
        string userMessage,
        List<AgentResponse> responses,
        ConversationContext context)
    {
        _logger.LogDebug("🎨 Synthesizing final response from {ResponseCount} agent responses", responses.Count);

        if (!responses.Any())
        {
            return "I couldn't process your request. Please try rephrasing it.";
        }

        // If only one agent responded, return its content directly
        if (responses.Count == 1)
        {
            return responses[0].Content;
        }

        // Combine multiple agent responses
        var agentOutputs = string.Join("\n\n", responses.Select(r =>
            $"**{r.AgentType} Agent:**\n{r.Content}"));

        var synthesisPrompt = $@"
User asked: ""{userMessage}""

Multiple specialized agents have processed this request. Synthesize their outputs into ONE comprehensive, user-friendly response.

Agent outputs:
{agentOutputs}

Your task:
1. Create a cohesive response that directly answers the user's question
2. Integrate insights from all agents seamlessly (don't list them separately)
3. Highlight key information:
   - Resource IDs and Azure Portal links
   - Compliance scores and security findings
   - Cost estimates and optimization opportunities
   - Any warnings or important notes
4. Use clear formatting (bullet points, sections, emojis for visual clarity)
5. Be concise but complete
6. Suggest logical next steps if appropriate

Important:
- Write in a natural, conversational tone
- Don't say ""Agent X said..."" - integrate the information naturally
- If agents provided conflicting information, reconcile it
- If something failed, explain clearly and suggest alternatives

Synthesized response:";

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are an expert at synthesizing technical information into clear, actionable responses.");
            chatHistory.AddUserMessage(synthesisPrompt);

            var result = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                executionSettings: new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.5,
                    MaxTokens = 2000
                });

            return result.Content ?? agentOutputs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error synthesizing response, returning raw outputs");
            return agentOutputs;
        }
    }

    // ========== Helper Methods ==========

    private string GetAgentDescription(AgentType agentType) => agentType switch
    {
        AgentType.Infrastructure => "Generate Infrastructure-as-Code templates (Bicep/Terraform), design network topology, analyze predictive scaling, optimize auto-scaling. Use for: creating NEW compliant infrastructure templates, designing networks, generating IaC code",
        AgentType.Compliance => "Scan and assess EXISTING resources for compliance (NIST 800-53, FedRAMP, DoD IL5), run security assessments, generate eMASS ATO packages. Use for: checking current compliance status, auditing existing infrastructure, validating security controls",
        AgentType.CostManagement => "Analyze costs of existing resources, estimate costs for planned deployments, optimize spending, track budgets",
        AgentType.Environment => "Manage environment lifecycle, clone environments, track deployments",
        AgentType.Discovery => "Discover and inventory existing resources, monitor health status, scan subscriptions",
        AgentType.Onboarding => "Onboard new missions and teams, gather requirements for new projects",
        _ => "General platform engineering tasks"
    };

    private ExecutionPlan CreateFallbackPlan(string userMessage)
    {
        _logger.LogInformation("📋 Creating fallback plan for: {Message}", userMessage);

        // Simple heuristic to determine agent
        var agentType = DetermineAgentFromMessage(userMessage);

        return new ExecutionPlan
        {
            PrimaryIntent = agentType.ToString().ToLowerInvariant(),
            ExecutionPattern = ExecutionPattern.Sequential,
            EstimatedTimeSeconds = 30,
            Tasks = new List<AgentTask>
            {
                new AgentTask
                {
                    AgentType = agentType,
                    Description = userMessage,
                    Priority = 1,
                    IsCritical = true
                }
            }
        };
    }

    private AgentType DetermineAgentFromMessage(string message)
    {
        var lowerMessage = message.ToLowerInvariant();

        if (lowerMessage.Contains("provision") || lowerMessage.Contains("create") ||
            lowerMessage.Contains("deploy") || lowerMessage.Contains("bicep") ||
            lowerMessage.Contains("terraform"))
            return AgentType.Infrastructure;

        if (lowerMessage.Contains("compliance") || lowerMessage.Contains("nist") ||
            lowerMessage.Contains("security") || lowerMessage.Contains("ato") ||
            lowerMessage.Contains("emass"))
            return AgentType.Compliance;

        if (lowerMessage.Contains("cost") || lowerMessage.Contains("budget") ||
            lowerMessage.Contains("price") || lowerMessage.Contains("optimize"))
            return AgentType.CostManagement;

        if (lowerMessage.Contains("environment") || lowerMessage.Contains("clone") ||
            lowerMessage.Contains("scale"))
            return AgentType.Environment;

        if (lowerMessage.Contains("list") || lowerMessage.Contains("find") ||
            lowerMessage.Contains("discover") || lowerMessage.Contains("inventory"))
            return AgentType.Discovery;

        if (lowerMessage.Contains("onboard") || lowerMessage.Contains("mission") ||
            lowerMessage.Contains("setup"))
            return AgentType.Onboarding;

        // Default to infrastructure for resource-related queries
        return AgentType.Infrastructure;
    }

    private ExecutionPattern ParseExecutionPattern(string? pattern)
    {
        if (string.IsNullOrEmpty(pattern))
            return ExecutionPattern.Sequential;

        return pattern.ToLowerInvariant() switch
        {
            "parallel" => ExecutionPattern.Parallel,
            "collaborative" => ExecutionPattern.Collaborative,
            _ => ExecutionPattern.Sequential
        };
    }

    private AgentType ParseAgentType(string? agentType)
    {
        if (string.IsNullOrEmpty(agentType))
            return AgentType.Infrastructure;

        return agentType.ToLowerInvariant() switch
        {
            "infrastructure" => AgentType.Infrastructure,
            "compliance" => AgentType.Compliance,
            "costmanagement" => AgentType.CostManagement,
            "environment" => AgentType.Environment,
            "discovery" => AgentType.Discovery,
            "onboarding" => AgentType.Onboarding,
            _ => AgentType.Infrastructure
        };
    }

    private bool DetermineIfFollowUpNeeded(List<AgentResponse> responses)
    {
        // Follow-up needed if any agent had warnings or partial success
        return responses.Any(r => r.Warnings.Any() || !r.Success);
    }

    private string? GenerateFollowUpPrompt(List<AgentResponse> responses)
    {
        var failedAgents = responses.Where(r => !r.Success).ToList();
        if (failedAgents.Any())
        {
            return $"Some operations didn't complete successfully. Would you like me to retry or try a different approach?";
        }

        var warnings = responses.SelectMany(r => r.Warnings).ToList();
        if (warnings.Any())
        {
            return $"I completed your request with some warnings. Would you like more details?";
        }

        return null;
    }

    private List<string> ExtractMissingFields(List<AgentResponse> responses)
    {
        var missingFields = new List<string>();

        foreach (var response in responses)
        {
            // Check metadata for missing fields
            if (response.Metadata.TryGetValue("MissingFields", out var fields))
            {
                if (fields is List<string> fieldList)
                {
                    missingFields.AddRange(fieldList);
                }
            }
        }

        return missingFields.Distinct().ToList();
    }

    private List<string> GenerateQuickReplies(string intent, List<AgentResponse> responses)
    {
        var replies = new List<string>();

        // Generate contextual quick replies based on intent and results
        if (intent.Contains("infrastructure") || intent.Contains("provision"))
        {
            replies.Add("Check compliance status");
            replies.Add("Estimate costs");
            replies.Add("View in Azure Portal");
        }
        else if (intent.Contains("compliance"))
        {
            replies.Add("Generate remediation plan");
            replies.Add("Create eMASS package");
            replies.Add("View detailed findings");
        }
        else if (intent.Contains("cost"))
        {
            replies.Add("Show optimization suggestions");
            replies.Add("Set up budget alerts");
            replies.Add("Compare pricing tiers");
        }

        return replies;
    }

    private Dictionary<string, object> CombineMetadata(List<AgentResponse> responses)
    {
        var combinedMetadata = new Dictionary<string, object>();

        foreach (var response in responses)
        {
            foreach (var kvp in response.Metadata)
            {
                var key = $"{response.AgentType}_{kvp.Key}";
                combinedMetadata[key] = kvp.Value;
            }
        }

        return combinedMetadata;
    }

    private string GetFeedbackSummary(List<AgentResponse> responses)
    {
        var recentResponses = responses.TakeLast(3).ToList();
        return string.Join("\n", recentResponses.Select(r =>
            $"- {r.AgentType}: {(r.Success ? "✅ Approved" : "❌ Needs changes")} - {r.Warnings.FirstOrDefault() ?? "No issues"}"));
    }

    private bool CheckCollaborativeApproval(List<AgentResponse> latestResponses)
    {
        // All agents must succeed
        if (!latestResponses.All(r => r.Success))
            return false;

        // Check agent-specific approval criteria
        foreach (var response in latestResponses)
        {
            if (response.AgentType == AgentType.Compliance && response.IsApproved == false)
                return false;

            if (response.AgentType == AgentType.CostManagement && response.IsWithinBudget == false)
                return false;
        }

        return true;
    }

    /// <summary>
    /// OPTIMIZATION: Fast-path detection for UNAMBIGUOUS single-agent requests
    /// Returns the agent type ONLY if request clearly maps to one agent with NO multi-agent coordination needed
    /// Treats ALL agents equally - no bias toward any specific agent type
    /// </summary>
    private AgentType? DetectUnambiguousSingleAgentRequest(string userMessage, ConversationContext context)
    {
        var lowerMessage = userMessage.ToLowerInvariant();
        
        // Check if this is a follow-up answer (user providing details after assistant asked questions)
        var isFollowUpAnswer = context.MessageHistory.Count > 0 &&
            context.MessageHistory.Last().Role == "assistant" &&
            context.MessageHistory.Last().Content.Contains("?");
        
        if (isFollowUpAnswer && context.MessageHistory.Count >= 2)
        {
            // Continue with the agent from previous interaction
            var previousMessage = context.MessageHistory[^2];
            if (previousMessage.Content.Contains("infrastructure", StringComparison.OrdinalIgnoreCase))
                return AgentType.Infrastructure;
            if (previousMessage.Content.Contains("compliance", StringComparison.OrdinalIgnoreCase))
                return AgentType.Compliance;
            if (previousMessage.Content.Contains("cost", StringComparison.OrdinalIgnoreCase))
                return AgentType.CostManagement;
        }
        
        // Exclude multi-agent scenarios (these need orchestration)
        var requiresMultipleAgents = 
            lowerMessage.Contains("and then") ||
            lowerMessage.Contains("also") ||
            lowerMessage.Contains("actually provision") ||  // Provision = all 5 agents
            lowerMessage.Contains("make it live") ||
            lowerMessage.Contains("with compliance") ||     // "X with compliance" = 2 agents
            lowerMessage.Contains("and cost");              // "X and cost" = 2 agents
        
        if (requiresMultipleAgents)
            return null;
        
        // COMPLIANCE AGENT - Unambiguous compliance scanning/assessment
        if ((lowerMessage.Contains("check") || lowerMessage.Contains("scan") || lowerMessage.Contains("assess") || 
             lowerMessage.Contains("audit") || lowerMessage.Contains("validate")) &&
            (lowerMessage.Contains("compliance") || lowerMessage.Contains("nist") || lowerMessage.Contains("fedramp") ||
             lowerMessage.Contains("security") || lowerMessage.Contains("ato")))
        {
            return AgentType.Compliance;
        }
        
        // INFRASTRUCTURE AGENT - Unambiguous template generation (NOT provisioning)
        if ((lowerMessage.Contains("generate") || lowerMessage.Contains("create") || lowerMessage.Contains("deploy")) &&
            (lowerMessage.Contains("template") || lowerMessage.Contains("bicep") || lowerMessage.Contains("terraform")) &&
            !lowerMessage.Contains("provision"))
        {
            return AgentType.Infrastructure;
        }
        
        // COST MANAGEMENT AGENT - Unambiguous cost analysis
        if ((lowerMessage.Contains("cost") || lowerMessage.Contains("price") || lowerMessage.Contains("budget") || 
             lowerMessage.Contains("spend")) &&
            (lowerMessage.Contains("estimate") || lowerMessage.Contains("analyze") || lowerMessage.Contains("optimize") ||
             lowerMessage.Contains("how much")))
        {
            return AgentType.CostManagement;
        }
        
        // DISCOVERY AGENT - Unambiguous resource discovery
        if ((lowerMessage.Contains("list") || lowerMessage.Contains("find") || lowerMessage.Contains("show") || 
             lowerMessage.Contains("discover") || lowerMessage.Contains("inventory")) &&
            (lowerMessage.Contains("resource") || lowerMessage.Contains("vm") || lowerMessage.Contains("storage") ||
             lowerMessage.Contains("subscription") || lowerMessage.Contains("cluster")))
        {
            return AgentType.Discovery;
        }
        
        // ENVIRONMENT AGENT - Unambiguous environment operations
        if ((lowerMessage.Contains("clone") || lowerMessage.Contains("copy") || lowerMessage.Contains("duplicate")) &&
            lowerMessage.Contains("environment"))
        {
            return AgentType.Environment;
        }
        
        // ONBOARDING AGENT - Unambiguous mission onboarding
        if ((lowerMessage.Contains("onboard") || lowerMessage.Contains("setup mission") || 
             lowerMessage.Contains("new mission")) &&
            !lowerMessage.Contains("infrastructure"))
        {
            return AgentType.Onboarding;
        }
        
        // No clear single-agent match - use orchestrator for planning
        return null;
    }

    // DTO for JSON deserialization
    private class ExecutionPlanDto
    {
        public string? PrimaryIntent { get; set; }
        public List<TaskDto>? Tasks { get; set; }
        public string? ExecutionPattern { get; set; }
        public int? EstimatedTimeSeconds { get; set; }
    }

    private class TaskDto
    {
        public string? AgentType { get; set; }
        public string? Description { get; set; }
        public int? Priority { get; set; }
        public bool? IsCritical { get; set; }
    }
}
