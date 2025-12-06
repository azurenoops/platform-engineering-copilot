#pragma warning disable SKEXP0010 // ResponseFormat is experimental

using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Services;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services.Agents;

/// <summary>
/// Orchestrator agent that coordinates and plans execution across specialized agents
/// This is the "brain" of the multi-agent system
/// </summary>
public class OrchestratorAgent
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly Dictionary<AgentType, ISpecializedAgent> _agents;
    private readonly SharedMemory _sharedMemory;
    private readonly ExecutionPlanValidator _planValidator;
    private readonly ExecutionPlanCache _planCache;
    private readonly ISemanticIntentService? _intentService;
    private readonly ILogger<OrchestratorAgent> _logger;

    public OrchestratorAgent(
        ISemanticKernelService semanticKernelService,
        IEnumerable<ISpecializedAgent> agents,
        SharedMemory sharedMemory,
        ExecutionPlanValidator planValidator,
        ExecutionPlanCache planCache,
        ILogger<OrchestratorAgent> logger,
        Platform.Engineering.Copilot.Core.Plugins.ConfigurationPlugin configurationPlugin,
        ISemanticIntentService? intentService = null)
    {
        _logger = logger;
        _sharedMemory = sharedMemory;
        _planValidator = planValidator;
        _planCache = planCache;
        _intentService = intentService;

        // Create orchestrator's own kernel
        _kernel = semanticKernelService.CreateSpecializedKernel(AgentType.Orchestrator);
        
        // Register shared configuration plugin (set_azure_subscription, get_azure_subscription, etc.)
        _kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(configurationPlugin, "ConfigurationPlugin"));
        
        // Try to get chat completion service - make it optional
        try
        {
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            _logger.LogInformation("✅ OrchestratorAgent initialized with AI chat completion service");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ OrchestratorAgent initialized without AI chat completion service. AI features will be limited.");
            _chatCompletion = null;
        }

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
            // OPTIMIZATION: Direct answer from context for simple informational queries
            var contextAnswer = TryAnswerFromContext(userMessage, context);
            if (contextAnswer != null)
            {
                _logger.LogInformation("⚡ Context-based answer (no agent needed)");
                var contextTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                return new OrchestratedResponse
                {
                    FinalResponse = contextAnswer,
                    PrimaryIntent = "informational",
                    AgentsInvoked = new List<AgentType>(),
                    ExecutionPattern = ExecutionPattern.Sequential,
                    TotalAgentCalls = 0,
                    ExecutionTimeMs = contextTime,
                    Success = true,
                    RequiresFollowUp = false,
                    Metadata = new Dictionary<string, object>()
                };
            }

            // OPTIMIZATION: Fast-path for unambiguous single-agent requests (skip planning LLM call)
            // Only use when request clearly maps to ONE specific agent with no multi-agent coordination needed
            var fastPathAgent = DetectUnambiguousSingleAgentRequest(userMessage, context);
            if (fastPathAgent.HasValue)
            {
                _logger.LogInformation("⚡ Fast-path detected: Unambiguous {AgentType} request - skipping orchestrator planning", 
                    fastPathAgent.Value);
                
                // Record intent for tracking
                var fastPathIntentId = await RecordIntentAsync(
                    userMessage,
                    intentCategory: fastPathAgent.Value.ToString().ToLowerInvariant(),
                    intentAction: "fast_path",
                    confidence: 0.95m,
                    userId: context.UserId,
                    sessionId: conversationId,
                    cancellationToken);
                
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
                
                // Update intent outcome
                await UpdateIntentOutcomeAsync(
                    fastPathIntentId, 
                    response.Success, 
                    response.Success ? null : string.Join("; ", response.Errors),
                    cancellationToken);
                
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

            // Record intent for tracking (after plan creation so we have the intent category)
            var planIntentId = await RecordIntentAsync(
                userMessage,
                intentCategory: plan.PrimaryIntent ?? "unknown",
                intentAction: plan.ExecutionPattern.ToString().ToLowerInvariant(),
                confidence: 0.8m, // Lower confidence since we used LLM planning
                userId: context.UserId,
                sessionId: conversationId,
                cancellationToken);

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

            // Update intent outcome
            await UpdateIntentOutcomeAsync(
                planIntentId,
                orchestratedResponse.Success,
                orchestratedResponse.Success ? null : string.Join("; ", orchestratedResponse.Errors),
                cancellationToken);

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

        // Include stored subscription ID if available
        var contextualInfo = "";
        if (context.WorkflowState.TryGetValue("lastSubscriptionId", out var lastSub) && lastSub != null)
        {
            contextualInfo = $"\n\nIMPORTANT Context: Last scanned subscription ID: {lastSub}";
            if (context.WorkflowState.TryGetValue("lastScanTimestamp", out var timestamp) && timestamp is DateTime scanTime)
            {
                contextualInfo += $" (scanned {(DateTime.UtcNow - scanTime).TotalMinutes:F0} minutes ago)";
            }
        }

        // OPTIMIZATION: Simplified planning prompt (70% token reduction from 2000 to ~600 tokens)
        var planningPrompt = $@"Available agents: {string.Join(", ", _agents.Keys)}
{conversationHistory}{contextualInfo}

User: ""{userMessage}""

Create JSON execution plan:
{{
  ""primaryIntent"": ""infrastructure|compliance|cost|environment|discovery|ServiceCreation|knowledge|mixed"",
  ""tasks"": [{{ ""agentType"": ""Infrastructure"", ""description"": ""task"", ""priority"": 1, ""isCritical"": true }}],
  ""executionPattern"": ""Sequential|Parallel|Collaborative"",
  ""estimatedTimeSeconds"": 30
}}

CRITICAL Rules (apply in order):
1. **Conversation continuation**: If assistant previously asked questions and user is answering → continue same task
2. **Azure context configuration** (""use subscription""/""set tenant""/""set authentication""/""what subscription""/""azure context"") → Infrastructure agent with task description EXACTLY matching user message (prefix with ""Azure config: ""), primaryIntent: ""infrastructure""
   - CRITICAL: For context config, DO NOT add ""create"", ""deploy"", or ""template"" - just pass the user message
   - Example: User says ""Use subscription 123"" → task description: ""Azure config: Use subscription 123"" (NOT ""Create infrastructure using subscription 123"")
3. **Resource queries** (""details for""/""get resource""/""show resource"" + resource ID/name OR contains ""/subscriptions/"") → Discovery agent ONLY, primaryIntent: ""discovery""
   - CRITICAL: Discovery agent has Resource Graph integration for fast queries with extended properties (SKU, Kind, ProvisioningState)
   - DO NOT route to MCP or other tools for Azure resource details
   - Examples: ""get details for /subscriptions/xxx/resourceGroups/yyy/providers/Microsoft.Web/sites/zzz""
   - Examples: ""show me details about app service web-app-123"", ""resource health for X""
4. **Informational questions about compliance/NIST/STIGs** (""what is""/""explain""/""define""/""describe""/""what controls""/""what nist""/""map nist to stig"") → KnowledgeBase agent ONLY, primaryIntent: ""knowledge""
   - Examples: ""what is CM family"", ""explain RMF"", ""what nist controls map to stigs"", ""show me IL5 requirements""
   - DO NOT route to Compliance agent unless explicitly requesting assessment/scan
5. **MULTI-AGENT: Security + Cost Analysis** (""security issues"" + ""cost to fix""/""cost to remediate"") → Compliance agent THEN CostManagement agent Sequential, primaryIntent: ""mixed""
   - CRITICAL: This is a TWO-STEP workflow: First find issues, then estimate costs
   - Task 1: Compliance agent - ""Run security assessment to find issues""
   - Task 2: CostManagement agent - ""Estimate remediation costs for findings""
   - Example: ""What security issues exist and how much would it cost to fix them?""
6. **MULTI-AGENT: Full status report** (""complete status""/""full picture""/""inventory, compliance, cost"") → Discovery, Compliance, CostManagement Sequential, primaryIntent: ""mixed""
   - Task 1: Discovery - ""Get resource inventory""
   - Task 2: Compliance - ""Run compliance assessment""
   - Task 3: CostManagement - ""Get cost analysis""
7. **Compliance scanning/assessment/recommendations** (""check""/""scan""/""assess""/""security issues""/""compliance recommendations""/""security recommendations"" + subscription/resource) → Compliance agent ONLY, primaryIntent: ""compliance""
   - CRITICAL: If query contains ""compliance recommendations"" or ""security posture"" or ""compliance advice"" → route ONLY to Compliance agent
   - DO NOT route to multiple agents for compliance-related recommendations
   - Examples: ""compliance recommendations"", ""improve security posture"", ""what should I fix for compliance""
8. **Template generation** (""create template""/""generate template""/""I need a new"" + resource type) → Infrastructure agent, primaryIntent: ""infrastructure""
   - ONLY for generating IaC templates (Bicep/Terraform/ARM)
   - NOT for security assessments or cost analysis
9. **Cost analysis** (""cost breakdown""/""how much spending""/""cost optimization"") → Cost Management agent, primaryIntent: ""cost""
10. **Discovery and inventory** (""list""/""find""/""discover""/""inventory"" resources) → Discovery agent, primaryIntent: ""discovery""
11. **Actual provisioning** (""actually provision""/""make it live""/""deploy to Azure"") → All agents Sequential

IMPORTANT: ""fix"" in context of ""cost to fix security issues"" means ESTIMATE COST, not generate templates.
Default: Single agent based on primary keyword. DO NOT default to Infrastructure for security/compliance queries.

Respond ONLY with JSON.";

        try
        {
            if (_chatCompletion == null)
            {
                throw new InvalidOperationException("Chat completion service not available. AI features require proper configuration.");
            }

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
            if (_chatCompletion == null)
            {
                _logger.LogWarning("Chat completion service not available, returning raw agent outputs");
                return agentOutputs;
            }

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
        AgentType.Compliance => "Scan and assess EXISTING resources for compliance (NIST 800-53, FedRAMP, DoD IL5), run security assessments, generate eMASS ATO packages, and perform automated remediation of compliance issues. Use for: checking current compliance status, auditing existing infrastructure, validating security controls, and fixing compliance violations",
        AgentType.CostManagement => "Analyze costs of existing resources, estimate costs for planned deployments, optimize spending, track budgets",
        AgentType.Environment => "Manage environment lifecycle, clone environments, track deployments",
        AgentType.Discovery => "Discover and inventory existing resources, monitor health status, scan subscriptions",
        AgentType.ServiceCreation => "Onboard new missions and teams, gather requirements for new projects",
        AgentType.KnowledgeBase => "Answer INFORMATIONAL questions about NIST 800-53 controls, STIGs, DoD frameworks, RMF processes, Impact Levels, and compliance mappings. Use for: explaining controls, describing frameworks, mapping NIST to STIGs/CCIs, understanding DoD policies. Does NOT perform assessments.",
        AgentType.Orchestrator => "Coordinate and plan execution across specialized agents for complex multi-step operations",
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

        // Azure context management (subscription, tenant, authentication)
        if (lowerMessage.Contains("use subscription") || lowerMessage.Contains("set subscription") ||
            lowerMessage.Contains("set my subscription") || lowerMessage.Contains("use my subscription") ||
            lowerMessage.Contains("my subscription is") || lowerMessage.Contains("configure subscription") ||
            lowerMessage.Contains("use tenant") || lowerMessage.Contains("set tenant") ||
            lowerMessage.Contains("set authentication") || lowerMessage.Contains("what subscription") ||
            lowerMessage.Contains("get azure context") || lowerMessage.Contains("azure context") ||
            lowerMessage.Contains("current subscription") || lowerMessage.Contains("switch subscription") ||
            lowerMessage.Contains("change subscription"))
            return AgentType.Infrastructure;

        if (lowerMessage.Contains("provision") || lowerMessage.Contains("create") ||
            lowerMessage.Contains("deploy") || lowerMessage.Contains("bicep") ||
            lowerMessage.Contains("terraform"))
            return AgentType.Infrastructure;

        if (lowerMessage.Contains("compliance") || lowerMessage.Contains("nist") ||
            lowerMessage.Contains("security") || lowerMessage.Contains("ato") ||
            lowerMessage.Contains("emass") || lowerMessage.Contains("scan") ||
            lowerMessage.Contains("assess") || lowerMessage.Contains("remediat"))
            return AgentType.Compliance;

        // Prioritize Compliance for security posture and compliance recommendations
        if ((lowerMessage.Contains("recommendations") || lowerMessage.Contains("advice") || 
             lowerMessage.Contains("improve") || lowerMessage.Contains("posture")) &&
            (lowerMessage.Contains("security") || lowerMessage.Contains("compliance")))
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
            return AgentType.ServiceCreation;

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
            "servicecreation" => AgentType.ServiceCreation,
            "knowledgebase" => AgentType.KnowledgeBase,
            "orchestrator" => AgentType.Orchestrator,
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
                // Store both prefixed and non-prefixed versions for important context values
                var key = $"{response.AgentType}_{kvp.Key}";
                combinedMetadata[key] = kvp.Value;
                
                // Also store non-prefixed for important context keys that need to be accessed directly
                if (kvp.Key == "subscriptionId" || kvp.Key == "resourceGroup" || kvp.Key == "environment")
                {
                    combinedMetadata[kvp.Key] = kvp.Value;
                }
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
            if (previousMessage.Content.Contains("compliance", StringComparison.OrdinalIgnoreCase))
                return AgentType.Compliance;
        }
        
        // Exclude multi-agent scenarios (these need orchestration)
        var requiresMultipleAgents = 
            lowerMessage.Contains("and then") ||
            lowerMessage.Contains("also") ||
            lowerMessage.Contains("actually provision") ||  // Provision = all 5 agents
            lowerMessage.Contains("make it live") ||
            lowerMessage.Contains("with compliance") ||     // "X with compliance" = 2 agents
            lowerMessage.Contains("and cost") ||            // "X and cost" = 2 agents
            // SECURITY + COST patterns - need Compliance + CostManagement agents
            (lowerMessage.Contains("security") && lowerMessage.Contains("cost")) ||
            (lowerMessage.Contains("issues") && lowerMessage.Contains("cost")) ||
            (lowerMessage.Contains("findings") && lowerMessage.Contains("cost")) ||
            (lowerMessage.Contains("fix") && lowerMessage.Contains("cost")) ||
            // COMPLETE STATUS patterns - need Discovery + Compliance + Cost agents
            lowerMessage.Contains("complete status") ||
            lowerMessage.Contains("full picture") ||
            lowerMessage.Contains("full report") ||
            (lowerMessage.Contains("inventory") && lowerMessage.Contains("compliance") && lowerMessage.Contains("cost"));
        
        if (requiresMultipleAgents)
        {
            _logger.LogInformation("🎯 Multi-agent query detected → Orchestrator planning required");
            return null;
        }
        
        // AZURE CONTEXT CONFIGURATION - Unambiguous Infrastructure agent requests
        // Check this EARLY before other patterns (to avoid false matches)
        var isAzureContextConfig = 
            lowerMessage.Contains("use subscription") || lowerMessage.Contains("set subscription") ||
            lowerMessage.Contains("set my subscription") || lowerMessage.Contains("use my subscription") ||
            lowerMessage.Contains("my subscription is") || lowerMessage.Contains("configure subscription") ||
            lowerMessage.Contains("use tenant") || lowerMessage.Contains("set tenant") ||
            lowerMessage.Contains("set authentication") || 
            (lowerMessage.Contains("what subscription") && (lowerMessage.Contains("using") || lowerMessage.Contains("am i") || lowerMessage.Contains("current"))) ||
            lowerMessage.Contains("get azure context") || lowerMessage.Contains("azure context") ||
            lowerMessage.Contains("current subscription") || lowerMessage.Contains("switch subscription") ||
            lowerMessage.Contains("change subscription");
        
        _logger.LogInformation("🔍 Fast-path Azure context check: isAzureContextConfig={IsConfig}, message={Message}", 
            isAzureContextConfig, lowerMessage.Substring(0, Math.Min(50, lowerMessage.Length)));
        
        if (isAzureContextConfig)
        {
            return AgentType.Infrastructure;
        }
        
        // COMPLIANCE AGENT - Distinguish between informational queries and actual assessments
        var isComplianceRelated = lowerMessage.Contains("compliance") || lowerMessage.Contains("nist") || 
                                  lowerMessage.Contains("fedramp") || lowerMessage.Contains("security") || 
                                  lowerMessage.Contains("control");
        
        // ATO PREPARATION AGENT - Handles ATO package creation and orchestration
        var isAtoPreparation = lowerMessage.Contains("ato package") || lowerMessage.Contains("ato preparation") ||
                               lowerMessage.Contains("poa&m") || lowerMessage.Contains("poam") ||
                               lowerMessage.Contains("plan of action") || lowerMessage.Contains("ato progress") ||
                               lowerMessage.Contains("ato status") ||
                               (lowerMessage.Contains("ato") && !lowerMessage.Contains("what is ato"));
        
        // DOCUMENT AGENT - Handles document generation, formatting, and management
        var isDocumentGeneration = lowerMessage.Contains("generate document") || 
                                   lowerMessage.Contains("create document") ||
                                   lowerMessage.Contains("write narrative") || 
                                   lowerMessage.Contains("control narrative") ||
                                   lowerMessage.Contains("format document") || 
                                   lowerMessage.Contains("export document") ||
                                   lowerMessage.Contains("list documents") ||
                                   lowerMessage.Contains("system security plan") || lowerMessage.Contains("ssp") ||
                                   lowerMessage.Contains("security assessment report") || lowerMessage.Contains("sar") ||
                                   (lowerMessage.Contains("generate") && (lowerMessage.Contains("ssp") || 
                                    lowerMessage.Contains("sar") || lowerMessage.Contains("narrative")));
        
        if (isDocumentGeneration || isAtoPreparation)
        {
            // Document generation and ATO preparation use Compliance agent
            return AgentType.Compliance;
        }
        
        // ============================================================================
        // DISCOVERY AGENT - PRIORITY ROUTING (Check before other agents)
        // Routes queries about specific Azure resources to Discovery Agent which has
        // Resource Graph integration for fast queries with extended properties
        // ============================================================================
        
        // DISCOVERY AGENT - Subscription listing (list all subscriptions)
        // "what subscriptions do I have", "list all subscriptions", "show subscriptions"
        var isSubscriptionListQuery = 
            (lowerMessage.Contains("what") || lowerMessage.Contains("list") || 
             lowerMessage.Contains("show") || lowerMessage.Contains("get")) &&
            (lowerMessage.Contains("subscriptions") || lowerMessage.Contains("subscription")) &&
            (lowerMessage.Contains("access to") || lowerMessage.Contains("have") || 
             lowerMessage.Contains("all") || lowerMessage.Contains("available")) &&
            !lowerMessage.Contains("set") && !lowerMessage.Contains("use") && 
            !lowerMessage.Contains("switch") && !lowerMessage.Contains("change");
        
        if (isSubscriptionListQuery)
        {
            _logger.LogInformation("🎯 Fast-path: Subscription listing query → Discovery Agent");
            return AgentType.Discovery;
        }
        
        // DISCOVERY AGENT - Specific resource by ID (HIGHEST PRIORITY)
        // Patterns: "details for /subscriptions/...", "get resource /sub.../rg.../providers/..."
        // "show /subscriptions/.../Microsoft.Web/sites/myapp"
        var hasResourceId = lowerMessage.Contains("/subscriptions/") || 
                           lowerMessage.Contains("/resourcegroups/") ||
                           lowerMessage.Contains("resourceid:") ||
                           lowerMessage.Contains("resource id:");
        
        if (hasResourceId)
        {
            _logger.LogInformation("🎯 Fast-path: Resource ID detected → Discovery Agent (Resource Graph)");
            return AgentType.Discovery;
        }
        
        // DISCOVERY AGENT - Resource details requests
        // "get details for resource X", "show me details about app service Y"
        // "resource details for web-app-123"
        var isResourceDetailsQuery = 
            (lowerMessage.Contains("details") || lowerMessage.Contains("detail") || 
             lowerMessage.Contains("information") || lowerMessage.Contains("info")) &&
            (lowerMessage.Contains("resource") || lowerMessage.Contains("app service") || 
             lowerMessage.Contains("web app") || lowerMessage.Contains("storage account") ||
             lowerMessage.Contains("vm") || lowerMessage.Contains("virtual machine") ||
             lowerMessage.Contains("aks") || lowerMessage.Contains("kubernetes") ||
             lowerMessage.Contains("sql") || lowerMessage.Contains("database")) &&
            !lowerMessage.Contains("create") && !lowerMessage.Contains("deploy");
        
        if (isResourceDetailsQuery)
        {
            _logger.LogInformation("🎯 Fast-path: Resource details query → Discovery Agent");
            return AgentType.Discovery;
        }
        
        // DISCOVERY AGENT - Tag-based resource search
        // "find resources with tag environment=prod", "search by tag"
        if ((lowerMessage.Contains("tag") || lowerMessage.Contains("tagged")) &&
            (lowerMessage.Contains("find") || lowerMessage.Contains("search") || 
             lowerMessage.Contains("list") || lowerMessage.Contains("show") ||
             lowerMessage.Contains("discover") || lowerMessage.Contains("resources with")))
        {
            _logger.LogInformation("🎯 Fast-path: Tag search → Discovery Agent");
            return AgentType.Discovery;
        }
        
        // DISCOVERY AGENT - General resource discovery
        // "list all resources", "find resources", "discover resources", "show me resources"
        // "inventory of subscription X"
        if ((lowerMessage.Contains("list") || lowerMessage.Contains("find") || 
             lowerMessage.Contains("discover") || lowerMessage.Contains("show") ||
             lowerMessage.Contains("inventory")) &&
            (lowerMessage.Contains("resource") || lowerMessage.Contains("resources")) &&
            !lowerMessage.Contains("create") && !lowerMessage.Contains("deploy") &&
            !lowerMessage.Contains("provision") && !lowerMessage.Contains("compliance"))
        {
            _logger.LogInformation("🎯 Fast-path: Resource discovery → Discovery Agent");
            return AgentType.Discovery;
        }
        
        // DISCOVERY AGENT - Resource health queries
        // "health of resource X", "is resource Y healthy", "resource health status"
        if ((lowerMessage.Contains("health") || lowerMessage.Contains("status") || 
             lowerMessage.Contains("availability")) &&
            lowerMessage.Contains("resource") &&
            !lowerMessage.Contains("create"))
        {
            _logger.LogInformation("🎯 Fast-path: Resource health → Discovery Agent");
            return AgentType.Discovery;
        }
        
        if (isComplianceRelated)
        {
            // INFORMATIONAL QUERIES - Just asking about concepts (no assessment needed)
            var isInformational = 
                (lowerMessage.Contains("what is") || lowerMessage.Contains("what are") || 
                 lowerMessage.Contains("tell me about") || lowerMessage.Contains("explain") ||
                 lowerMessage.Contains("describe") || lowerMessage.Contains("define") ||
                 lowerMessage.Contains("what does") || lowerMessage.Contains("how do i implement") ||
                 lowerMessage.Contains("what evidence") || lowerMessage.Contains("requirements for")) &&
                !lowerMessage.Contains("subscription") && !lowerMessage.Contains("resource group") &&
                !lowerMessage.Contains("in my") && !lowerMessage.Contains("for my");
            
            // COMBINED KNOWLEDGE + ANALYSIS - Requires multi-agent orchestration
            // "What is control X and what services implement it?"
            // "Explain control Y and show compliance status"
            var isKnowledgePlusAnalysis = 
                isInformational && 
                (lowerMessage.Contains("implement") || lowerMessage.Contains("services") ||
                 lowerMessage.Contains("resources") || lowerMessage.Contains("compliance") ||
                 lowerMessage.Contains("status") || lowerMessage.Contains("in my"));
            
            if (isKnowledgePlusAnalysis)
            {
                // Let orchestrator handle multi-agent coordination
                _logger.LogInformation("🎯 Multi-agent query detected: Knowledge + Analysis → Orchestrator planning");
                return null;
            }
            
            // PURE INFORMATIONAL → KnowledgeBase Agent (no scanning)
            // "What is AC-2?", "Explain IA-2", "What does SC-28 require?"
            if (isInformational)
            {
                _logger.LogInformation("🎯 Fast-path: Pure informational query → KnowledgeBase Agent (no scan)");
                return AgentType.KnowledgeBase;
            }
            
            // ASSESSMENT/SCAN REQUESTS - Actually wants to scan/assess resources
            var isAssessment = 
                lowerMessage.Contains("check") || lowerMessage.Contains("scan") || 
                lowerMessage.Contains("assess") || lowerMessage.Contains("audit") || 
                lowerMessage.Contains("validate") || lowerMessage.Contains("run") ||
                lowerMessage.Contains("compliance status") || lowerMessage.Contains("findings");
            
            // ASSESSMENT → Compliance Agent (performs scanning)
            // "Run a compliance scan", "Check my compliance", "Assess security"
            if (isAssessment)
            {
                _logger.LogInformation("🎯 Fast-path: Assessment request → Compliance Agent");
                return AgentType.Compliance;
            }
        }
        
        // No clear single-agent match - use orchestrator for planning
        return null;
    }

    /// <summary>
    /// Try to answer simple informational questions directly from conversation context
    /// without invoking any agents or making LLM calls.
    /// Also detects when user is asking for informational content (vs actual assessment/scan).
    /// </summary>
    private string? TryAnswerFromContext(string userMessage, ConversationContext context)
    {
        var lowerMessage = userMessage.ToLowerInvariant();
        
        // Check for questions about recent scans/activity
        var isAskingAboutRecent = 
            (lowerMessage.Contains("what") || lowerMessage.Contains("which")) &&
            (lowerMessage.Contains("subscription") || lowerMessage.Contains("resource")) &&
            (lowerMessage.Contains("scan") || lowerMessage.Contains("just") || lowerMessage.Contains("last") || 
             lowerMessage.Contains("recent") || lowerMessage.Contains("did i"));
        
        if (isAskingAboutRecent && context.WorkflowState.TryGetValue("lastSubscriptionId", out var lastSub) && lastSub != null)
        {
            var subscriptionId = lastSub.ToString();
            var timeInfo = "";
            
            if (context.WorkflowState.TryGetValue("lastScanTimestamp", out var timestamp) && timestamp is DateTime scanTime)
            {
                var elapsed = DateTime.UtcNow - scanTime;
                if (elapsed.TotalMinutes < 60)
                {
                    timeInfo = $" about {elapsed.TotalMinutes:F0} minutes ago";
                }
                else if (elapsed.TotalHours < 24)
                {
                    timeInfo = $" about {elapsed.TotalHours:F1} hours ago";
                }
                else
                {
                    timeInfo = $" on {scanTime:MMM dd 'at' h:mm tt} UTC";
                }
            }
            
            return $"You recently scanned Azure subscription: **{subscriptionId}**{timeInfo}.";
        }
        
        // NOTE: Informational queries about compliance frameworks/controls should be handled by agents
        // (they have the knowledge base), but we return null here to let orchestrator route them properly.
        // The key is to NOT interpret these as assessment requests.
        
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

    // ==================== Semantic Intent Tracking ====================

    /// <summary>
    /// Record a semantic intent for analytics and improvement
    /// </summary>
    private async Task<Guid?> RecordIntentAsync(
        string userMessage,
        string intentCategory,
        string intentAction,
        decimal confidence,
        string? userId = null,
        string? sessionId = null,
        CancellationToken cancellationToken = default)
    {
        if (_intentService == null)
        {
            return null;
        }

        try
        {
            var intent = await _intentService.RecordIntentAsync(
                userId: userId ?? "anonymous",
                userInput: userMessage,
                intentCategory: intentCategory,
                intentAction: intentAction,
                confidence: confidence,
                sessionId: sessionId,
                cancellationToken: cancellationToken);

            return intent.Id;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record semantic intent - continuing without tracking");
            return null;
        }
    }

    /// <summary>
    /// Update the outcome of a tracked intent
    /// </summary>
    private async Task UpdateIntentOutcomeAsync(
        Guid? intentId,
        bool wasSuccessful,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (_intentService == null || !intentId.HasValue)
        {
            return;
        }

        try
        {
            await _intentService.UpdateIntentOutcomeAsync(
                intentId.Value,
                wasSuccessful,
                errorMessage,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update intent outcome - continuing without tracking");
        }
    }
}
