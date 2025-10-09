using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Platform.Engineering.Copilot.Core.Services.Cache;

/// <summary>
/// Caching service for intelligent chat responses to reduce AI API calls and rate limiting
/// </summary>
public interface IIntelligentChatCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class;
    string GenerateCacheKey(string prefix, params string[] values);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory implementation of intelligent chat cache service
/// </summary>
public class IntelligentChatCacheService : IIntelligentChatCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<IntelligentChatCacheService> _logger;
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(30);

    public IntelligentChatCacheService(
        IMemoryCache cache,
        ILogger<IntelligentChatCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            if (_cache.TryGetValue(key, out T? value))
            {
                _logger.LogDebug("Cache hit for key: {CacheKey}", key);
                return Task.FromResult(value);
            }

            _logger.LogDebug("Cache miss for key: {CacheKey}", key);
            return Task.FromResult<T?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache for key: {CacheKey}", key);
            return Task.FromResult<T?>(null);
        }
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            var cacheExpiration = expiration ?? _defaultExpiration;
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = cacheExpiration,
                SlidingExpiration = TimeSpan.FromMinutes(15) // Refresh if accessed within 15 minutes
            };

            _cache.Set(key, value, cacheEntryOptions);
            _logger.LogDebug("Cached value for key: {CacheKey}, Expiration: {Expiration}", key, cacheExpiration);
            
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache for key: {CacheKey}", key);
            return Task.CompletedTask;
        }
    }

    public string GenerateCacheKey(string prefix, params string[] values)
    {
        try
        {
            // Create a hash of the input values for consistent cache keys
            var combined = string.Join("|", values);
            var hash = ComputeHash(combined);
            
            return $"{prefix}:{hash}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating cache key with prefix: {Prefix}", prefix);
            // Fallback to simple concatenation
            return $"{prefix}:{string.Join(":", values)}";
        }
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            _cache.Remove(key);
            _logger.LogDebug("Removed cache entry for key: {CacheKey}", key);
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache entry for key: {CacheKey}", key);
            return Task.CompletedTask;
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Note: IMemoryCache doesn't have a built-in Clear method
            // In production, consider using IDistributedCache with clear capability
            _logger.LogWarning("Cache clear requested - IMemoryCache doesn't support clearing all entries");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
            return Task.CompletedTask;
        }
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder();
        foreach (var b in bytes)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }
}
