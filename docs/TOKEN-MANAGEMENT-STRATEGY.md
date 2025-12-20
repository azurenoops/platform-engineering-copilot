# Token Management Strategy for Agent Projects

## Executive Summary

The Platform Engineering Copilot Core project has a **robust token management system** that isn't being fully leveraged by agent projects. This plan outlines how agents can implement intelligent token optimization to:

- ✅ Improve response quality (more relevant context)
- ✅ Reduce API costs (fewer tokens = lower costs)
- ✅ Enable longer conversations (better history management)
- ✅ Handle large documents (RAG context optimization)
- ✅ Prevent token limit errors (proactive optimization)

---

## Current Architecture

### Token Management Components

```
TokenManagement/
├── Interfaces/
│   ├── ITokenCounter              → Count tokens using SharpToken
│   ├── IPromptOptimizer           → Optimize complete prompts
│   └── IRagContextOptimizer       → Rank/trim RAG results
├── Services/
│   ├── TokenCounter               → Token encoding/decoding
│   ├── PromptOptimizer            → Prompt fitting algorithm
│   ├── RagContextOptimizer        → RAG ranking and trimming
│   └── TokenManagementHelper      → Integration helper
└── Models/
    ├── TokenEstimate             → Token breakdown
    ├── OptimizedPrompt           → Fitted prompt output
    ├── RankedSearchResult        → Scored RAG result
    └── PromptOptimizationOptions → Configuration
```

### Current Usage

- ✅ Registered as singletons in Core DI container
- ✅ Used in `SemanticKernelService` (limited)
- ✅ Used in `ChatBuilder` (chat history optimization)
- ⚠️ **NOT actively used in Agent projects** ← **This is the gap**

---

## The Problem: Why Agents Need Token Management

### Scenario 1: Infrastructure Agent Generating Large Templates

```
User: "Generate a Bicep template for enterprise AKS deployment"

Without Token Management:
- System prompt: ~500 tokens
- Agent context: ~2000 tokens
- User message: ~200 tokens
- RAG results (all 10): ~3000 tokens
→ Total: ~5700 tokens → Partial response due to 4096 limit

With Token Management:
- Optimizer detects 1700-token overage
- Removes low-relevance RAG results (score < 0.5)
- Trims remaining results intelligently
→ Total: ~3900 tokens → Complete response
```

### Scenario 2: Compliance Agent with History

```
User asking 5th question in compliance session

Without Token Management:
- Full chat history: ~8000 tokens
- System prompt: ~800 tokens
- New question: ~150 tokens
→ Total: ~8950 tokens → EXCEEDS LIMIT, errors

With Token Management:
- System prompt always kept (~800)
- Recent 3 messages kept (~3000)
- Oldest messages pruned
→ Total: ~3950 tokens → Works fine
```

### Scenario 3: Knowledge Base Plugin with Many Results

```
User: "Search Azure docs for security best practices"

Without Token Management:
- All 50 search results included (10000+ tokens)
- Parser crashes or returns garbled response

With Token Management:
- Ranked by relevance score
- Top 5 high-relevance results kept (800 tokens)
- Irrelevant results excluded
→ Quality response with precise info
```

---

## Implementation Plan

### Phase 1: Inject Token Management into Agents (Week 1)

**Goal**: Make token management available to all agents

#### 1.1 Update Agent Constructors

Add three interfaces to each agent constructor:

```csharp
// Infrastructure Agent Example
public InfrastructureAgent(
    ISemanticKernelService semanticKernelService,
    ITokenCounter tokenCounter,              // ← ADD
    IPromptOptimizer promptOptimizer,        // ← ADD
    IRagContextOptimizer ragContextOptimizer, // ← ADD
    ILogger<InfrastructureAgent> logger,
    IOptions<AgentOptions> options)
{
    _tokenCounter = tokenCounter;
    _promptOptimizer = promptOptimizer;
    _ragContextOptimizer = ragContextOptimizer;
    // ... existing code
}
```

**Files to Update**:
- `Infrastructure.Agent/Services/Agents/InfrastructureAgent.cs`
- `Compliance.Agent/Services/Agents/ComplianceAgent.cs`
- `CostManagement.Agent/Services/Agents/CostManagementAgent.cs`
- `Discovery.Agent/Services/Agents/DiscoveryAgent.cs`
- `KnowledgeBase.Agent/Services/Agents/KnowledgeBaseAgent.cs`
- `Security.Agent/Services/Agents/SecurityAgent.cs`
- `Environment.Agent/Services/Agents/EnvironmentAgent.cs`

#### 1.2 Update Agent DI Registration

Ensure each agent project explicitly registers token services:

```csharp
// Infrastructure.Agent/Extensions/ServiceCollectionExtensions.cs
public static IServiceCollection AddInfrastructureAgent(
    this IServiceCollection services)
{
    // Token management already registered by Core
    // But explicitly reference it for clarity
    services.AddSingleton<ITokenCounter>();
    services.AddSingleton<IPromptOptimizer>();
    services.AddSingleton<IRagContextOptimizer>();
    
    // Register agent
    services.AddSingleton<ISpecializedAgent>(sp => 
        new InfrastructureAgent(
            sp.GetRequiredService<ISemanticKernelService>(),
            sp.GetRequiredService<ITokenCounter>(),
            sp.GetRequiredService<IPromptOptimizer>(),
            sp.GetRequiredService<IRagContextOptimizer>(),
            sp.GetRequiredService<ILogger<InfrastructureAgent>>(),
            sp.GetRequiredService<IOptions<AgentOptions>>()
        ));
    
    return services;
}
```

---

### Phase 2: Implement RAG Context Optimization (Week 1-2)

**Goal**: Intelligent RAG result filtering before sending to LLM

#### 2.1 Create RAG Optimization Wrapper

```csharp
// Infrastructure.Agent/Services/RagOptimizationService.cs
public class RagOptimizationService
{
    private readonly IRagContextOptimizer _optimizer;
    private readonly ITokenCounter _counter;
    
    /// <summary>
    /// Filter and optimize search results before passing to agent
    /// </summary>
    public async Task<List<string>> OptimizeSearchResultsAsync(
        List<string> searchResults,
        string userQuery,
        int maxContextTokens = 2000)
    {
        // Convert to ranked results
        var rankedResults = searchResults
            .Select((content, i) => new RankedSearchResult
            {
                Content = content,
                RelevanceScore = CalculateRelevance(content, userQuery),
                TokenCount = _counter.CountTokens(content)
            })
            .ToList();
        
        // Optimize using core service
        var options = new RagOptimizationOptions
        {
            MaxTokens = maxContextTokens,
            MinRelevanceScore = 0.3,
            PrioritizeRecency = true
        };
        
        var optimized = _optimizer.OptimizeContext(rankedResults, options);
        
        return optimized.Results
            .Select(r => r.Content)
            .ToList();
    }
    
    private double CalculateRelevance(string content, string query)
    {
        // Keyword matching, semantic similarity, etc.
        var queryWords = query.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        var matches = queryWords.Count(w => 
            content.Contains(w, StringComparison.OrdinalIgnoreCase));
        return Math.Min(1.0, (double)matches / queryWords.Length);
    }
}
```

**Usage in Agent**:

```csharp
// In InfrastructureAgent.ProcessRequestAsync()
var userQuery = agentTask.UserRequest;

// Before passing RAG results to LLM:
var optimizedResults = await _ragOptimizationService
    .OptimizeSearchResultsAsync(rawSearchResults, userQuery);

// Now build prompt with optimized results (shorter, more relevant)
```

#### 2.2 Integrate with Plugin RAG Calls

Update infrastructure plugin to use optimization:

```csharp
[KernelFunction("search_azure_templates")]
public async Task<string> SearchTemplatesAsync(
    [Description("Template type (vm, aks, storage, etc)")] 
    string templateType,
    CancellationToken cancellationToken = default)
{
    // Get raw search results
    var rawResults = await _azureSearchClient.SearchAsync(templateType);
    
    // Optimize before returning
    var optimized = await _ragOptimizationService
        .OptimizeSearchResultsAsync(
            rawResults,
            templateType,
            maxContextTokens: 1500);
    
    return JsonSerializer.Serialize(optimized);
}
```

---

### Phase 3: Implement Prompt Optimization (Week 2)

**Goal**: Automatically fit prompts within token limits

#### 3.1 Create Agent Prompt Optimizer

```csharp
// Core/Services/Chat/AgentPromptOptimizer.cs
public class AgentPromptOptimizer
{
    private readonly IPromptOptimizer _promptOptimizer;
    private readonly ITokenCounter _tokenCounter;
    
    public class OptimizedAgentPrompt
    {
        public string SystemPrompt { get; set; }
        public string UserMessage { get; set; }
        public List<string> RagContext { get; set; }
        public List<string> ConversationHistory { get; set; }
        public TokenEstimate Estimate { get; set; }
        public bool WasOptimized { get; set; }
    }
    
    public OptimizedAgentPrompt OptimizeForAgent(
        string systemPrompt,
        string userMessage,
        List<string> ragContext,
        List<string> conversationHistory,
        string agentType,
        int maxInputTokens = 8000,
        int reservedCompletionTokens = 4000)
    {
        var options = GetOptimizationOptionsForAgent(agentType, maxInputTokens);
        
        var optimized = _promptOptimizer.OptimizePrompt(
            systemPrompt,
            userMessage,
            ragContext,
            conversationHistory,
            options);
        
        return new OptimizedAgentPrompt
        {
            SystemPrompt = optimized.SystemPrompt,
            UserMessage = optimized.UserMessage,
            RagContext = optimized.RagContext,
            ConversationHistory = optimized.ConversationHistory,
            Estimate = optimized.TokenEstimate,
            WasOptimized = optimized.WasOptimized
        };
    }
    
    private PromptOptimizationOptions GetOptimizationOptionsForAgent(
        string agentType, int maxTokens)
    {
        return agentType switch
        {
            "Infrastructure" => new()
            {
                MaxTokens = maxTokens,
                SystemPromptPriority = 1.0,
                UserMessagePriority = 0.9,
                RagContextPriority = 0.8,
                ConversationHistoryPriority = 0.3,
                TruncationStrategy = "intelligent" // Keep most recent + relevant
            },
            "Compliance" => new()
            {
                MaxTokens = maxTokens,
                SystemPromptPriority = 1.0,
                UserMessagePriority = 1.0,
                RagContextPriority = 0.6,
                ConversationHistoryPriority = 0.7, // Keep more history for context
                TruncationStrategy = "recent" // Prioritize recent messages
            },
            "CostManagement" => new()
            {
                MaxTokens = maxTokens,
                SystemPromptPriority = 0.9,
                UserMessagePriority = 1.0,
                RagContextPriority = 0.9, // Cost data is critical
                ConversationHistoryPriority = 0.5,
                TruncationStrategy = "intelligent"
            },
            _ => new()
            {
                MaxTokens = maxTokens,
                SystemPromptPriority = 1.0,
                UserMessagePriority = 0.9,
                RagContextPriority = 0.7,
                ConversationHistoryPriority = 0.5,
                TruncationStrategy = "intelligent"
            }
        };
    }
}
```

#### 3.2 Use in ProcessRequestAsync

```csharp
public override async Task<AgentExecutionResult> ProcessRequestAsync(
    AgentTask agentTask, SharedMemory sharedMemory, 
    CancellationToken cancellationToken = default)
{
    // Get system prompt for this agent type
    var systemPrompt = await _semanticKernelService
        .GetAgentSystemPromptAsync(AgentType.Infrastructure);
    
    // Get RAG context
    var ragContext = await _searchService.SearchAsync(agentTask.UserRequest);
    
    // Get conversation history
    var history = sharedMemory.GetConversationHistory(agentTask.ConversationId);
    
    // Optimize everything at once
    var optimized = _agentPromptOptimizer.OptimizeForAgent(
        systemPrompt,
        agentTask.UserRequest,
        ragContext,
        history.ToStringList(),
        AgentType.Infrastructure.ToString());
    
    // Log optimization results
    _logger.LogInformation(
        "Token optimization: {Original} → {Optimized} tokens, " +
        "Optimized: {WasOptimized}",
        optimized.Estimate.TotalInputTokens + 4000,
        optimized.Estimate.TotalInputTokens + 4000,
        optimized.WasOptimized);
    
    // Build kernel with optimized components
    var kernel = _semanticKernelService.CreateSpecializedKernel(AgentType.Infrastructure);
    
    // Invoke with optimized prompt
    var response = await kernel.InvokeAsync(
        "ProcessInfrastructureRequest",
        new KernelArguments
        {
            ["systemPrompt"] = optimized.SystemPrompt,
            ["userMessage"] = optimized.UserMessage,
            ["ragContext"] = string.Join("\n", optimized.RagContext),
            ["conversationHistory"] = string.Join("\n", optimized.ConversationHistory)
        });
    
    return new AgentExecutionResult { /* ... */ };
}
```

---

### Phase 4: Implement Cost Tracking (Week 2-3)

**Goal**: Monitor and report token usage per agent

#### 4.1 Create Token Metrics Service

```csharp
// Core/Services/Metrics/TokenMetricsService.cs
public class TokenMetricsService
{
    private readonly ILogger<TokenMetricsService> _logger;
    private readonly IMetricsCollector _metricsCollector;
    
    public class TokenMetrics
    {
        public string AgentType { get; set; }
        public DateTime Timestamp { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
        public decimal CostUsd { get; set; }
        public bool WasOptimized { get; set; }
        public int TokensSaved { get; set; }
    }
    
    public void RecordTokenUsage(
        string agentType,
        TokenEstimate estimate,
        int outputTokens,
        bool wasOptimized,
        int? tokensSaved = null)
    {
        var costPerToken = 0.00001m; // $0.01 per 1K tokens
        var metrics = new TokenMetrics
        {
            AgentType = agentType,
            Timestamp = DateTime.UtcNow,
            InputTokens = estimate.TotalInputTokens,
            OutputTokens = outputTokens,
            TotalTokens = estimate.TotalInputTokens + outputTokens,
            CostUsd = (estimate.TotalInputTokens + outputTokens) * costPerToken,
            WasOptimized = wasOptimized,
            TokensSaved = tokensSaved ?? 0
        };
        
        _metricsCollector.Record(metrics);
        
        _logger.LogInformation(
            "Agent: {Agent}, Tokens: {Input}→{Output}, " +
            "Cost: ${Cost}, Optimized: {Optimized}, Saved: {Saved} tokens",
            agentType, estimate.TotalInputTokens, outputTokens,
            metrics.CostUsd, wasOptimized, tokensSaved);
    }
}
```

#### 4.2 Dashboard Integration

Create Grafana dashboard to show:
- Tokens per agent per day
- Total cost by agent
- Optimization effectiveness (saved tokens)
- Average tokens per request
- Token limit violations (before optimization)

---

### Phase 5: Conversation History Management (Week 3)

**Goal**: Keep conversations alive longer

#### 5.1 Smart History Pruning

```csharp
// Core/Services/Chat/ConversationHistoryOptimizer.cs
public class ConversationHistoryOptimizer
{
    private readonly ITokenCounter _tokenCounter;
    private readonly ILogger<ConversationHistoryOptimizer> _logger;
    
    public List<ChatMessage> OptimizeHistoryForNewRequest(
        List<ChatMessage> fullHistory,
        string newUserMessage,
        int maxHistoryTokens = 2000)
    {
        var optimized = new List<ChatMessage>();
        
        // Always include system message (if exists)
        var systemMsg = fullHistory.FirstOrDefault(m => m.Role == "system");
        if (systemMsg != null)
            optimized.Add(systemMsg);
        
        // Score messages by relevance to new request
        var scored = fullHistory
            .Where(m => m.Role != "system")
            .Select(m => new
            {
                Message = m,
                Tokens = _tokenCounter.CountTokens(m.Content),
                Relevance = CalculateRelevance(m.Content, newUserMessage),
                Recency = 1.0 / (DateTime.UtcNow - m.Timestamp).TotalHours // Higher = more recent
            })
            .OrderByDescending(x => x.Relevance * 0.6 + x.Recency * 0.4) // Weighted score
            .ToList();
        
        // Add messages until token limit
        var currentTokens = 0;
        foreach (var item in scored)
        {
            if (currentTokens + item.Tokens <= maxHistoryTokens)
            {
                optimized.Add(item.Message);
                currentTokens += item.Tokens;
            }
        }
        
        _logger.LogInformation(
            "History optimization: {Original} messages → {Optimized} messages, " +
            "{Tokens} tokens",
            fullHistory.Count, optimized.Count, currentTokens);
        
        return optimized;
    }
}
```

---

## Implementation Checklist

### ✅ Phase 1: Dependency Injection
- [ ] Infrastructure Agent: Add ITokenCounter, IPromptOptimizer, IRagContextOptimizer
- [ ] Compliance Agent: Add token management dependencies
- [ ] CostManagement Agent: Add token management dependencies
- [ ] Discovery Agent: Add token management dependencies
- [ ] KnowledgeBase Agent: Add token management dependencies
- [ ] Security Agent: Add token management dependencies
- [ ] Environment Agent: Add token management dependencies
- [ ] Update all extension files with explicit registration

### ✅ Phase 2: RAG Optimization
- [ ] Create RagOptimizationService in each agent
- [ ] Integrate with plugin search methods
- [ ] Add relevance scoring
- [ ] Test with large document sets (1000+ results)

### ✅ Phase 3: Prompt Optimization
- [ ] Create AgentPromptOptimizer in Core
- [ ] Update each agent's ProcessRequestAsync
- [ ] Add agent-specific optimization strategies
- [ ] Add logging for optimization metrics

### ✅ Phase 4: Cost Tracking
- [ ] Create TokenMetricsService
- [ ] Instrument all agents
- [ ] Create dashboard in monitoring system
- [ ] Add daily/weekly cost reports

### ✅ Phase 5: History Management
- [ ] Create ConversationHistoryOptimizer
- [ ] Integrate with IntelligentChatService
- [ ] Test multi-turn conversations (20+ turns)

---

## Expected Benefits

### Cost Reduction
- **Current**: ~50,000 tokens/day × $0.00001 = **$0.50/day** per agent (~$15/month)
- **After Optimization**: ~30,000 tokens/day × $0.00001 = **$0.30/day** per agent (~$9/month)
- **Savings**: **40% reduction** = **$18/month** across all 5 active agents

### Quality Improvement
- **Better Context**: Only high-relevance RAG results included
- **Longer Conversations**: Chat history stays relevant longer
- **Fewer Errors**: Proactive token limit avoidance

### Operational Visibility
- **Token Tracking**: Know exactly what each agent costs
- **Optimization Metrics**: See how much is being saved
- **Performance Insights**: Identify high-token requests for optimization

---

## Configuration

Add to `appsettings.json`:

```json
{
  "TokenManagement": {
    "Enabled": true,
    "DefaultModel": "gpt-4o",
    "ConversationHistory": {
      "MaxTokens": 2000,
      "MaxMessages": 20,
      "PriorityRecentMessages": true
    },
    "RagContext": {
      "MaxTokens": 1500,
      "MinRelevanceScore": 0.3,
      "MaxResults": 10
    },
    "Optimization": {
      "AgentDefaults": {
        "Infrastructure": {
          "MaxInputTokens": 8000,
          "ReservedCompletionTokens": 4000,
          "RagContextPriority": 0.8
        },
        "Compliance": {
          "MaxInputTokens": 8000,
          "ReservedCompletionTokens": 4000,
          "ConversationHistoryPriority": 0.8
        },
        "CostManagement": {
          "MaxInputTokens": 6000,
          "ReservedCompletionTokens": 2000,
          "RagContextPriority": 0.9
        }
      }
    },
    "Monitoring": {
      "LogMetrics": true,
      "SendToMetricsCollector": true,
      "MetricsInterval": "PT1H"
    }
  }
}
```

---

## Testing Strategy

### Unit Tests
- Token counting accuracy (compare with OpenAI API)
- Optimization algorithm correctness
- History pruning logic

### Integration Tests
- End-to-end agent requests with token tracking
- Multi-turn conversations with pruning
- RAG result filtering

### Performance Tests
- Token optimization overhead (should be < 50ms)
- Memory usage with large history
- Concurrent token counting

### Cost Tests
- Verify cost calculations match Azure billing
- Track savings over 1-week pilot

---

## Timeline

| Phase | Tasks | Duration | Effort |
|-------|-------|----------|--------|
| 1 | DI Setup | 1 day | 8 hours |
| 2 | RAG Optimization | 3 days | 24 hours |
| 3 | Prompt Optimization | 3 days | 24 hours |
| 4 | Cost Tracking | 2 days | 16 hours |
| 5 | History Mgmt | 2 days | 16 hours |
| Testing | E2E, Perf, Cost | 3 days | 24 hours |
| **Total** | | **2 weeks** | **112 hours** |

---

## Risk Mitigation

| Risk | Impact | Mitigation |
|------|--------|-----------|
| Optimization too aggressive | Lost context | Phased rollout, tunable thresholds |
| Performance overhead | Latency increase | Monitor optimization timing, cache results |
| Token counting inaccuracy | Budget overruns | Validate against OpenAI API |
| Agent-specific issues | Uneven implementation | Template pattern, peer review |

---

## Success Metrics

✅ **Implementation Complete When**:
1. All agents can access token management services
2. RAG results filtered intelligently in 90% of requests
3. Prompts automatically optimized before sending
4. Token metrics tracked in dashboard
5. Conversation history stays optimized for 20+ turns
6. Cost reduced by 30-40% without quality loss
7. All agents tested and certified

