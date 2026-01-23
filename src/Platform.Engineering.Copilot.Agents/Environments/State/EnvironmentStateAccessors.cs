using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.State.Abstractions;

namespace Platform.Engineering.Copilot.Agents.Environments.State;

/// <summary>
/// State accessors for Environment Agent, providing typed access to environment-related state.
/// Tracks subscription context, environment operations, and deployment history.
/// </summary>
public class EnvironmentStateAccessors
{
    private readonly ISharedMemory _sharedMemory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EnvironmentStateAccessors> _logger;
    private readonly ConfigService? _configService;

    private const string CurrentSubscriptionKey = "current_subscription";
    private const string EnvironmentOperationsKey = "environment_operations";
    private const string DeploymentHistoryKey = "deployment_history";

    public EnvironmentStateAccessors(
        ISharedMemory sharedMemory,
        IMemoryCache cache,
        ILogger<EnvironmentStateAccessors> logger,
        ConfigService? configService = null)
    {
        _sharedMemory = sharedMemory ?? throw new ArgumentNullException(nameof(sharedMemory));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configService = configService;
    }

    /// <summary>
    /// Get the current subscription ID from conversation state.
    /// Falls back to persisted config if not in conversation state.
    /// </summary>
    public async Task<string?> GetCurrentSubscriptionAsync(
        string conversationId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _sharedMemory.GetAsync<SubscriptionContext>(
            conversationId, CurrentSubscriptionKey, cancellationToken);
        
        if (!string.IsNullOrEmpty(subscription?.SubscriptionId))
        {
            return subscription.SubscriptionId;
        }
        
        // Fall back to persisted config
        var persistedSub = _configService?.GetDefaultSubscription();
        if (!string.IsNullOrEmpty(persistedSub))
        {
            _logger.LogDebug("Using persisted subscription from config: {SubscriptionId}", persistedSub);
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
    /// Track an environment operation.
    /// </summary>
    public async Task TrackOperationAsync(
        string conversationId,
        string operationType,
        string environmentName,
        string resourceGroup,
        bool success,
        TimeSpan duration,
        CancellationToken cancellationToken = default)
    {
        var operations = await _sharedMemory.GetAsync<List<EnvironmentOperationRecord>>(
            conversationId, EnvironmentOperationsKey, cancellationToken) ?? new List<EnvironmentOperationRecord>();

        operations.Add(new EnvironmentOperationRecord
        {
            OperationType = operationType,
            EnvironmentName = environmentName,
            ResourceGroup = resourceGroup,
            Success = success,
            Duration = duration,
            Timestamp = DateTime.UtcNow
        });

        // Keep only last 50 operations
        if (operations.Count > 50)
        {
            operations = operations.TakeLast(50).ToList();
        }

        await _sharedMemory.SetAsync(conversationId, EnvironmentOperationsKey, operations, cancellationToken);
    }

    /// <summary>
    /// Store deployment result in cache.
    /// </summary>
    public void CacheDeploymentResult(string deploymentId, DeploymentResult result, TimeSpan? expiry = null)
    {
        var cacheKey = $"deployment:{deploymentId}";
        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromHours(24)
        };
        _cache.Set(cacheKey, result, options);
    }

    /// <summary>
    /// Get cached deployment result.
    /// </summary>
    public DeploymentResult? GetCachedDeploymentResult(string deploymentId)
    {
        var cacheKey = $"deployment:{deploymentId}";
        return _cache.TryGetValue<DeploymentResult>(cacheKey, out var result) ? result : null;
    }
}

public class SubscriptionContext
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string? SubscriptionName { get; set; }
    public DateTime SetAt { get; set; }
}

public class EnvironmentOperationRecord
{
    public string OperationType { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = string.Empty;
    public string ResourceGroup { get; set; } = string.Empty;
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime Timestamp { get; set; }
}

public class DeploymentResult
{
    public string DeploymentId { get; set; } = string.Empty;
    public string Strategy { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
}
