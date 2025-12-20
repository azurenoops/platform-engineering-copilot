using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Infrastructure.Core;

/// <summary>
/// Partial class for intelligent caching of template generation results.
/// Prevents duplicate LLM calls for identical template requests within a conversation.
/// </summary>
public partial class InfrastructurePlugin
{
    /// <summary>
    /// In-memory cache key format: {conversationId}:{resourceType}:{templateFormat}
    /// Stores the generated template response to avoid redundant LLM calls
    /// </summary>
    private static readonly ConcurrentDictionary<string, (string Response, DateTime Timestamp)> _templateCache = 
        new();

    private const int CACHE_TTL_MINUTES = 30; // Cache valid for 30 minutes

    /// <summary>
    /// Get a cached template response if available and not expired
    /// </summary>
    private string? GetCachedTemplate(string conversationId, string resourceType, string templateFormat)
    {
        if (string.IsNullOrEmpty(conversationId))
        {
            return null; // No cache without conversation context
        }

        var cacheKey = $"{conversationId}:{resourceType.ToLowerInvariant()}:{templateFormat.ToLowerInvariant()}";
        
        if (_templateCache.TryGetValue(cacheKey, out var cached))
        {
            // Check if cache is still valid
            if ((DateTime.UtcNow - cached.Timestamp).TotalMinutes < CACHE_TTL_MINUTES)
            {
                _logger.LogInformation(
                    "✅ Cache HIT for {ResourceType} - returning cached template (age: {AgeSeconds}s)",
                    resourceType,
                    (int)(DateTime.UtcNow - cached.Timestamp).TotalSeconds);
                return cached.Response;
            }
            else
            {
                // Cache expired, remove it
                _templateCache.TryRemove(cacheKey, out _);
                _logger.LogInformation(
                    "⏰ Cache EXPIRED for {ResourceType} - will regenerate",
                    resourceType);
            }
        }

        return null; // No cache or expired
    }

    /// <summary>
    /// Store a template response in cache for future reuse
    /// </summary>
    private void CacheTemplate(string conversationId, string resourceType, string templateFormat, string response)
    {
        if (string.IsNullOrEmpty(conversationId))
        {
            return; // Don't cache without conversation context
        }

        var cacheKey = $"{conversationId}:{resourceType.ToLowerInvariant()}:{templateFormat.ToLowerInvariant()}";
        _templateCache.AddOrUpdate(
            cacheKey,
            (response, DateTime.UtcNow),
            (_, _) => (response, DateTime.UtcNow));

        _logger.LogInformation(
            "💾 Cached template for {ResourceType} (cache size: {CacheSize} entries)",
            resourceType,
            _templateCache.Count);
    }

    /// <summary>
    /// Clear expired cache entries (can be called periodically)
    /// </summary>
    private static void ClearExpiredCacheEntries()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _templateCache
            .Where(kvp => (now - kvp.Value.Timestamp).TotalMinutes >= CACHE_TTL_MINUTES)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _templateCache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Get cache statistics for monitoring
    /// </summary>
    public string GetCacheStats()
    {
        ClearExpiredCacheEntries();
        
        var stats = new StringBuilder();
        stats.AppendLine($"📊 **Template Cache Statistics:**");
        stats.AppendLine($"- Total entries: {_templateCache.Count}");
        stats.AppendLine($"- Max TTL: {CACHE_TTL_MINUTES} minutes");
        
        if (_templateCache.Any())
        {
            var oldestAge = _templateCache.Min(kvp => (DateTime.UtcNow - kvp.Value.Timestamp).TotalSeconds);
            var newestAge = _templateCache.Max(kvp => (DateTime.UtcNow - kvp.Value.Timestamp).TotalSeconds);
            stats.AppendLine($"- Age range: {newestAge:F0}s - {oldestAge:F0}s");
        }

        return stats.ToString();
    }
}
