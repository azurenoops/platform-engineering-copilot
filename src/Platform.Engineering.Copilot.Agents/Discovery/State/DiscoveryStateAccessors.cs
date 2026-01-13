using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.State.Abstractions;

namespace Platform.Engineering.Copilot.Agents.Discovery.State;

/// <summary>
/// State accessors for Discovery Agent, providing typed access to discovery-related state.
/// Tracks discovered resources, subscription context, and cache state.
/// </summary>
public class DiscoveryStateAccessors
{
    private readonly IAgentStateManager _stateManager;
    private readonly ISharedMemory _sharedMemory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DiscoveryStateAccessors> _logger;
    private readonly ConfigService? _configService;

    private const string AgentType = "discovery";
    private const string CurrentSubscriptionKey = "current_subscription";
    private const string DiscoveredResourcesCachePrefix = "discovered_resources";
    private const string LastDiscoveryResultsKey = "last_discovery_results";

    public DiscoveryStateAccessors(
        IAgentStateManager stateManager,
        ISharedMemory sharedMemory,
        IMemoryCache cache,
        ILogger<DiscoveryStateAccessors> logger,
        ConfigService? configService = null)
    {
        _stateManager = stateManager ?? throw new ArgumentNullException(nameof(stateManager));
        _sharedMemory = sharedMemory ?? throw new ArgumentNullException(nameof(sharedMemory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configService = configService;
    }

    /// <summary>
    /// Get the current subscription ID from conversation state.
    /// Falls back to persisted config (~/.platform-copilot/config.json) if not in conversation state.
    /// </summary>
    public async Task<string?> GetCurrentSubscriptionAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        // 1. First check conversation state
        var subscription = await _sharedMemory.GetAsync<SubscriptionContext>(
            conversationId, CurrentSubscriptionKey, cancellationToken);
        
        if (!string.IsNullOrEmpty(subscription?.SubscriptionId))
        {
            return subscription.SubscriptionId;
        }
        
        // 2. Fall back to persisted config (survives across sessions)
        var persistedSub = _configService?.GetDefaultSubscription();
        if (!string.IsNullOrEmpty(persistedSub))
        {
            _logger.LogDebug("Using persisted subscription from config: {SubscriptionId}", persistedSub);
            // Store in conversation state for efficiency in subsequent calls
            await SetCurrentSubscriptionAsync(conversationId, persistedSub, null, cancellationToken);
            return persistedSub;
        }
        
        return null;
    }

    /// <summary>
    /// Set the current subscription ID in conversation state.
    /// </summary>
    public async Task SetCurrentSubscriptionAsync(
        string conversationId,
        string subscriptionId,
        string? subscriptionName = null,
        CancellationToken cancellationToken = default)
    {
        var context = new SubscriptionContext
        {
            SubscriptionId = subscriptionId,
            SubscriptionName = subscriptionName,
            SetAt = DateTime.UtcNow
        };

        await _sharedMemory.SetAsync(conversationId, CurrentSubscriptionKey, context, cancellationToken);
        _logger.LogDebug("Set current subscription for {ConversationId}: {SubscriptionId}", 
            conversationId, subscriptionId);
    }

    /// <summary>
    /// Get cached discovered resources for a subscription.
    /// </summary>
    public DiscoveredResourcesCache? GetCachedResources(string subscriptionId)
    {
        var cacheKey = $"{DiscoveredResourcesCachePrefix}:{subscriptionId}";
        if (_cache.TryGetValue<DiscoveredResourcesCache>(cacheKey, out var cached))
        {
            return cached;
        }
        return null;
    }

    /// <summary>
    /// Cache discovered resources for a subscription.
    /// </summary>
    public void CacheDiscoveredResources(
        string subscriptionId,
        List<DiscoveredResourceSummary> resources,
        TimeSpan? expiration = null)
    {
        var cacheKey = $"{DiscoveredResourcesCachePrefix}:{subscriptionId}";
        var cache = new DiscoveredResourcesCache
        {
            SubscriptionId = subscriptionId,
            Resources = resources,
            DiscoveredAt = DateTime.UtcNow,
            ResourceCount = resources.Count
        };

        var cacheExpiration = expiration ?? TimeSpan.FromMinutes(15);
        _cache.Set(cacheKey, cache, cacheExpiration);
        _logger.LogDebug("Cached {Count} resources for subscription {SubscriptionId}, expires in {Minutes} minutes",
            resources.Count, subscriptionId, cacheExpiration.TotalMinutes);
    }

    /// <summary>
    /// Invalidate cached resources for a subscription.
    /// </summary>
    public void InvalidateCache(string subscriptionId)
    {
        var cacheKey = $"{DiscoveredResourcesCachePrefix}:{subscriptionId}";
        _cache.Remove(cacheKey);
        _logger.LogDebug("Invalidated cache for subscription {SubscriptionId}", subscriptionId);
    }

    /// <summary>
    /// Store discovery results in shared memory for cross-agent access.
    /// </summary>
    public async Task ShareDiscoveryResultsAsync(
        string conversationId,
        DiscoveryResultSummary summary,
        CancellationToken cancellationToken = default)
    {
        await _sharedMemory.SetAsync(conversationId, LastDiscoveryResultsKey, summary, cancellationToken);
        await _sharedMemory.PublishEventAsync(conversationId, "discovery_completed", summary, cancellationToken);
    }

    /// <summary>
    /// Get shared discovery results from another agent's operation.
    /// </summary>
    public async Task<DiscoveryResultSummary?> GetSharedDiscoveryResultsAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _sharedMemory.GetAsync<DiscoveryResultSummary>(
            conversationId, LastDiscoveryResultsKey, cancellationToken);
    }

    /// <summary>
    /// Track discovery operation in agent state.
    /// </summary>
    public async Task TrackDiscoveryOperationAsync(
        string conversationId,
        string operationType,
        string subscriptionId,
        int resourceCount,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var agentState = await _stateManager.GetAgentStateAsync(conversationId, AgentType, cancellationToken);
        
        agentState.SetData("lastOperationType", operationType);
        agentState.SetData("lastSubscriptionId", subscriptionId);
        agentState.SetData("lastResourceCount", resourceCount);
        agentState.SetData("lastOperationTime", DateTime.UtcNow);
        agentState.SetData("lastOperationDurationMs", (int)duration.TotalMilliseconds);

        // Track operation count
        var opCount = agentState.GetData<int>("operationCount");
        agentState.SetData("operationCount", opCount + 1);

        await _stateManager.SaveAgentStateAsync(conversationId, AgentType, agentState, cancellationToken);
    }

    /// <summary>
    /// Store the last inventory summary for follow-up queries.
    /// </summary>
    public async Task SetLastInventoryAsync(
        object inventorySummary,
        CancellationToken cancellationToken = default)
    {
        // Store in memory cache for quick retrieval within session
        _cache.Set("last_inventory_summary", inventorySummary, TimeSpan.FromMinutes(30));
        _logger.LogDebug("Stored last inventory summary in cache");
        await Task.CompletedTask;
    }

    /// <summary>
    /// Get the last inventory summary.
    /// </summary>
    public Task<object?> GetLastInventoryAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue<object>("last_inventory_summary", out var summary))
        {
            return Task.FromResult<object?>(summary);
        }
        return Task.FromResult<object?>(null);
    }
}

#region State Models

/// <summary>
/// Subscription context stored in shared memory.
/// </summary>
public class SubscriptionContext
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string? SubscriptionName { get; set; }
    public DateTime SetAt { get; set; }
}

/// <summary>
/// Cached discovered resources.
/// </summary>
public class DiscoveredResourcesCache
{
    public string SubscriptionId { get; set; } = string.Empty;
    public List<DiscoveredResourceSummary> Resources { get; set; } = new();
    public DateTime DiscoveredAt { get; set; }
    public int ResourceCount { get; set; }
}

/// <summary>
/// Summary of a discovered resource.
/// </summary>
public class DiscoveredResourceSummary
{
    public string ResourceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public Dictionary<string, string>? Tags { get; set; }
}

/// <summary>
/// Summary of discovery results for cross-agent sharing.
/// </summary>
public class DiscoveryResultSummary
{
    public string SubscriptionId { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public Dictionary<string, int> ByType { get; set; } = new();
    public Dictionary<string, int> ByLocation { get; set; } = new();
    public Dictionary<string, int> ByResourceGroup { get; set; } = new();
    public DateTime DiscoveredAt { get; set; }
    public string? Query { get; set; }
}

#endregion
