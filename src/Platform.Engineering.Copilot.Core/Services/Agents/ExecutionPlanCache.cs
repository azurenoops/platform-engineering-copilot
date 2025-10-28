using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;

namespace Platform.Engineering.Copilot.Core.Services.Agents;

/// <summary>
/// OPTIMIZATION: Cache execution plans for similar requests to skip orchestrator planning LLM call
/// Uses semantic hashing to match similar user requests (e.g., "Deploy AKS" vs "Create AKS cluster")
/// </summary>
public class ExecutionPlanCache
{
    private readonly ILogger<ExecutionPlanCache> _logger;
    private readonly Dictionary<string, CachedPlan> _cache = new();
    private readonly object _cacheLock = new();
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(15);
    private readonly int _maxCacheSize = 100;

    public ExecutionPlanCache(ILogger<ExecutionPlanCache> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Try to get a cached execution plan for a similar request
    /// </summary>
    public ExecutionPlan? TryGetCachedPlan(string userMessage, ConversationContext context)
    {
        var intentHash = ComputeIntentHash(userMessage);

        lock (_cacheLock)
        {
            if (_cache.TryGetValue(intentHash, out var cached))
            {
                if (DateTime.UtcNow - cached.Timestamp < _cacheDuration)
                {
                    _logger.LogInformation("♻️  Cache HIT: Using cached execution plan for similar request (age: {Age}s)",
                        (DateTime.UtcNow - cached.Timestamp).TotalSeconds);
                    
                    cached.HitCount++;
                    return cached.Plan;
                }
                else
                {
                    _logger.LogDebug("Cache entry expired, removing: {Hash}", intentHash);
                    _cache.Remove(intentHash);
                }
            }
        }

        _logger.LogDebug("Cache MISS: No cached plan found for request");
        return null;
    }

    /// <summary>
    /// Store an execution plan in the cache
    /// </summary>
    public void CachePlan(string userMessage, ExecutionPlan plan)
    {
        var intentHash = ComputeIntentHash(userMessage);

        lock (_cacheLock)
        {
            // Evict oldest entries if cache is full
            if (_cache.Count >= _maxCacheSize)
            {
                var oldestKey = _cache
                    .OrderBy(kvp => kvp.Value.Timestamp)
                    .First()
                    .Key;
                
                _cache.Remove(oldestKey);
                _logger.LogDebug("Cache full, evicted oldest entry");
            }

            _cache[intentHash] = new CachedPlan
            {
                Plan = plan,
                Timestamp = DateTime.UtcNow,
                OriginalMessage = userMessage,
                HitCount = 0
            };

            _logger.LogDebug("Cached execution plan: {Hash} (cache size: {Size})", intentHash, _cache.Count);
        }
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStatistics GetStatistics()
    {
        lock (_cacheLock)
        {
            return new CacheStatistics
            {
                TotalEntries = _cache.Count,
                TotalHits = _cache.Values.Sum(c => c.HitCount),
                MostHitEntry = _cache.Values.OrderByDescending(c => c.HitCount).FirstOrDefault()?.OriginalMessage ?? "None"
            };
        }
    }

    /// <summary>
    /// Compute a semantic hash for the user's request
    /// Maps similar requests to the same hash regardless of agent type
    /// Treats ALL agents equally - no bias toward infrastructure/templates
    /// </summary>
    private string ComputeIntentHash(string message)
    {
        // Normalize the message to detect similar intent across ALL agent types
        var normalized = message.ToLowerInvariant()
            // Normalize action verbs (all agents)
            .Replace("deploy", "create")
            .Replace("set up", "create")
            .Replace("provision", "create")
            .Replace("i need", "create")
            .Replace("generate", "create")
            .Replace("check", "analyze")
            .Replace("scan", "analyze")
            .Replace("assess", "analyze")
            .Replace("validate", "analyze")
            .Replace("list", "show")
            .Replace("find", "show")
            .Replace("discover", "show")
            // Normalize resource types (infrastructure)
            .Replace("aks cluster", "aks")
            .Replace("kubernetes cluster", "aks")
            .Replace("storage account", "storage")
            .Replace("virtual machine", "vm")
            .Replace("virtual network", "vnet")
            // Normalize compliance frameworks
            .Replace("nist 800-53", "nist")
            .Replace("fedramp high", "fedramp")
            .Replace("dod il5", "dodil5")
            // Normalize locations
            .Replace("us gov virginia", "virginia")
            .Replace("us-gov-virginia", "virginia")
            .Replace("us gov arizona", "arizona")
            // Remove template-specific noise
            .Replace("template", "")
            .Replace("bicep", "")
            .Replace("terraform", "")
            .Trim();

        // Extract key tokens (resource types, actions, modifiers) - treat all equally
        var tokens = normalized
            .Split(new[] { ' ', ',', '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => IsRelevantToken(t))
            .OrderBy(t => t) // Sort for consistency
            .ToList();

        var tokenString = string.Join("|", tokens);

        // Hash the normalized token string
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(tokenString));
        return Convert.ToBase64String(hashBytes)[..16]; // Use first 16 chars
    }

    /// <summary>
    /// Filter out noise words, keep only relevant tokens
    /// </summary>
    private bool IsRelevantToken(string token)
    {
        var noiseWords = new HashSet<string>
        {
            "a", "an", "the", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "should",
            "can", "could", "may", "might", "must", "shall",
            "i", "me", "my", "we", "us", "our", "you", "your",
            "and", "or", "but", "if", "then", "else", "when", "where", "how", "why",
            "for", "to", "in", "on", "at", "by", "with", "from"
        };

        return token.Length > 2 && !noiseWords.Contains(token);
    }

    private class CachedPlan
    {
        public ExecutionPlan Plan { get; set; } = null!;
        public DateTime Timestamp { get; set; }
        public string OriginalMessage { get; set; } = string.Empty;
        public int HitCount { get; set; }
    }
}

public class CacheStatistics
{
    public int TotalEntries { get; set; }
    public int TotalHits { get; set; }
    public string MostHitEntry { get; set; } = string.Empty;
}
