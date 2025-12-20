# Token Management Quick Reference

## 3-Tier Architecture

```
┌─────────────────────────────────────────────────────────┐
│  Agent Layer (Infrastructure, Compliance, etc.)         │
│  ├─ Inject: ITokenCounter, IPromptOptimizer             │
│  ├─ Use: OptimizeSearchResults(), OptimizeForAgent()    │
│  └─ Track: RecordTokenUsage()                           │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│  Core Services (TokenManagement)                        │
│  ├─ TokenCounter: Count/encode/decode tokens           │
│  ├─ PromptOptimizer: Fit prompts in limits             │
│  ├─ RagContextOptimizer: Rank/trim search results      │
│  └─ TokenManagementHelper: Conversation optimization   │
└─────────────────────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────┐
│  Foundation: SharpToken + Azure OpenAI API              │
│  └─ Accurate token counting for gpt-4o, gpt-4, etc.    │
└─────────────────────────────────────────────────────────┘
```

## Integration Points

### 1. RAG Context Optimization Flow

```
Search Results (1000 items, 10,000+ tokens)
         ↓
    Rank by relevance score
         ↓
    Filter (min score 0.3)
         ↓
    Trim large items to fit tokens
         ↓
    Return top N high-quality results (800 tokens)
```

### 2. Prompt Optimization Flow

```
System Prompt (500 tokens)
User Message (200 tokens)
RAG Context (3000 tokens) ← Over limit!
Conversation History (2000 tokens)
     ↓
  Detect: 5700 total > 4000 reserved
     ↓
  Prioritize: System > User > RAG > History
     ↓
  Trim RAG Context (3000 → 1500)
  Keep History Partial (2000 → 1000)
     ↓
  Result: System (500) + User (200) + RAG (1500) + History (1000) = 3200 ✓
```

### 3. Conversation History Management

```
Old Messages (least relevant)
     ↓
Message 1 (0.2 relevance score)  ← Remove
Message 2 (0.3 relevance score)  ← Remove
Message 3 (0.7 relevance score)  ← Keep
Message 4 (0.9 relevance score)  ← Keep (recent)
Message 5 (1.0 relevance score)  ← Keep (most recent)
     ↓
Result: Keep only high-relevance + recent messages
```

## Core Components

### ITokenCounter
```csharp
int CountTokens(string text, string modelName)         // How many tokens?
List<int> EncodeText(string text, string modelName)    // Token IDs
string DecodeTokens(List<int> tokens, string modelName) // Back to text
TokenEstimate EstimateTokens(...)                       // Full breakdown
```

### IPromptOptimizer
```csharp
OptimizedPrompt OptimizePrompt(...)                     // Fit in limit
bool NeedsOptimization(...)                             // Check needed?
Dictionary<string, int> CalculateTokenDistribution(..) // Allocate budget
```

### IRagContextOptimizer
```csharp
OptimizedRagContext OptimizeContext(...)                // Rank/trim results
List<RankedSearchResult> RankAndFilter(...)             // Filter by score
RankedSearchResult TrimResult(...)                      // Shorten item
```

## Usage Examples

### Example 1: Infrastructure Agent with RAG

```csharp
// In InfrastructureAgent.cs
public InfrastructureAgent(
    ISemanticKernelService semanticKernelService,
    ITokenCounter tokenCounter,              // ← Injected
    IPromptOptimizer promptOptimizer,        // ← Injected
    IRagContextOptimizer ragContextOptimizer, // ← Injected
    ILogger<InfrastructureAgent> logger,
    IOptions<AgentOptions> options)
{
    _tokenCounter = tokenCounter;
    _promptOptimizer = promptOptimizer;
    _ragContextOptimizer = ragContextOptimizer;
}

public override async Task<AgentExecutionResult> ProcessRequestAsync(...)
{
    // 1. Get system prompt
    var systemPrompt = "You are an Infrastructure Agent...";
    
    // 2. Get RAG results
    var searchResults = await _azureSearchClient.SearchAsync(
        query: agentTask.UserRequest);
    
    // 3. Optimize RAG results BEFORE using them
    var rankedResults = searchResults
        .Select(r => new RankedSearchResult
        {
            Content = r,
            RelevanceScore = CalculateScore(r, agentTask.UserRequest)
        })
        .ToList();
    
    var optimized = _ragContextOptimizer.OptimizeContext(
        rankedResults,
        new RagOptimizationOptions { MaxTokens = 1500 });
    
    // 4. Optimize entire prompt
    var prompt = _promptOptimizer.OptimizePrompt(
        systemPrompt,
        agentTask.UserRequest,
        optimized.Results.Select(r => r.Content).ToList(),
        history,
        new PromptOptimizationOptions
        {
            MaxTokens = 8000,
            ReservedCompletionTokens = 4000
        });
    
    // 5. Send to LLM with confidence it fits
    var response = await kernel.InvokeAsync(...);
    
    // 6. Track token usage
    _metricsService.RecordTokenUsage(
        "Infrastructure",
        prompt.TokenEstimate,
        outputTokens,
        prompt.WasOptimized);
}
```

### Example 2: Compliance Agent with History

```csharp
// In ComplianceAgent.cs
var fullHistory = await _conversationStore.GetHistoryAsync(conversationId);

// Optimize for new request
var optimized = _historyOptimizer.OptimizeHistoryForNewRequest(
    fullHistory,
    agentTask.UserRequest,
    maxHistoryTokens: 2500); // Compliance needs more history

// Use optimized history
var estimate = _tokenCounter.EstimateTokens(
    systemPrompt,
    agentTask.UserRequest,
    ragContext,
    optimized.Select(m => m.Content).ToList());

_logger.LogInformation(
    "Request tokens: {Input}, History optimized: " +
    "{Original} → {Optimized} messages",
    estimate.TotalInputTokens,
    fullHistory.Count,
    optimized.Count);
```

### Example 3: Cost Dashboard Query

```sql
SELECT 
    AgentType,
    DATE(Timestamp) as Date,
    COUNT(*) as Requests,
    SUM(InputTokens + OutputTokens) as TotalTokens,
    SUM(CostUsd) as DailyCost,
    SUM(CASE WHEN WasOptimized = 1 THEN TokensSaved ELSE 0 END) as TokensSaved,
    ROUND(SUM(CASE WHEN WasOptimized = 1 THEN TokensSaved ELSE 0 END) / 
          NULLIF(SUM(TotalTokens), 0) * 100, 2) as OptimizationRate
FROM TokenMetrics
WHERE Date >= DATEADD(day, -7, GETDATE())
GROUP BY AgentType, DATE(Timestamp)
ORDER BY Date DESC, AgentType;
```

## Configuration Priority

Token management is configured at multiple levels (highest priority wins):

```
1. Runtime Parameter (highest)  → OptimizeForAgent(maxTokens: 5000)
2. Agent-Specific Config        → appsettings.json > AgentDefaults.Infrastructure
3. Model-Specific Config        → appsettings.json > Optimization.Defaults.gpt-4o
4. Global Config (lowest)       → appsettings.json > TokenManagement.Defaults
```

## Performance Characteristics

| Operation | Time | Memory | Notes |
|-----------|------|--------|-------|
| CountTokens(1000 chars) | ~1ms | 10KB | SharpToken is fast |
| EstimateTokens (all) | ~5ms | 50KB | Includes breakdown |
| OptimizePrompt | ~10ms | 100KB | Full optimization |
| OptimizeContext (100 results) | ~50ms | 200KB | Ranking and trimming |
| RankAndFilter | ~30ms | 150KB | Relevance scoring |

## Troubleshooting

### Problem: "Token count exceeds limit"

**Solution**: Enable optimization

```csharp
var needs = _promptOptimizer.NeedsOptimization(
    systemPrompt, userMessage, ragContext, history);

if (needs)
{
    // Optimize before invoking LLM
    var optimized = _promptOptimizer.OptimizePrompt(...);
}
```

### Problem: "RAG results too large"

**Solution**: Trim before using

```csharp
var trimmed = _ragContextOptimizer.RankAndFilter(
    results,
    minRelevanceScore: 0.5,  // Stricter filtering
    maxResults: 5);           // Fewer results
```

### Problem: "Conversation keeps losing context"

**Solution**: Adjust history priority

```csharp
options.ConversationHistoryPriority = 0.8; // Higher = keep more
options.MaxTokens = 3000; // Give more tokens to history
```

### Problem: "Token counting seems wrong"

**Solution**: Verify model name

```csharp
// Must match actual deployment
var tokens = _tokenCounter.CountTokens(text, "gpt-4o");
```

## Monitoring & Alerts

### Key Metrics to Track

- **Optimization Rate**: % of requests optimized
- **Token Efficiency**: Avg tokens per request
- **Cost per Request**: Dollars spent per request
- **Context Relevance**: Avg relevance score of included RAG results
- **History Retention**: % of conversation history kept

### Recommended Alerts

| Alert | Threshold | Action |
|-------|-----------|--------|
| High token usage | > 7000/request | Review RAG filtering |
| Low optimization rate | < 5% | Check if optimization needed |
| Cost spike | > 150% daily avg | Investigate agent behavior |
| Relevance drop | < 0.5 avg score | Adjust search quality |

---

**Last Updated**: December 2025  
**Status**: Ready for Phase 1 Implementation
