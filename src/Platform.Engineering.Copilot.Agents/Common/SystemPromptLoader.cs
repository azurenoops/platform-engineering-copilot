using System.Text.RegularExpressions;

namespace Platform.Engineering.Copilot.Agents.Common;

/// <summary>
/// Utility for loading and caching system prompts from embedded resources or files.
/// Supports template variable substitution, async loading, and prompt composition.
/// </summary>
public static class SystemPromptLoader
{
    private static readonly Dictionary<string, string> _cache = new();
    private static readonly object _lock = new();
    private static readonly SemaphoreSlim _asyncLock = new(1, 1);
    
    // Regex for include directives: {{include:path/to/file.txt}}
    private static readonly Regex IncludePattern = new(@"\{\{include:([^}]+)\}\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    #region Synchronous Loading

    /// <summary>
    /// Load a system prompt from an embedded resource.
    /// </summary>
    /// <param name="resourceName">Resource name suffix (e.g., "ComplianceAgent.SystemPrompt.txt")</param>
    /// <param name="assembly">Assembly containing the resource (defaults to calling assembly)</param>
    /// <returns>Prompt content or null if not found</returns>
    public static string? LoadFromResource(string resourceName, System.Reflection.Assembly? assembly = null)
    {
        lock (_lock)
        {
            var cacheKey = $"resource:{resourceName}";
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            assembly ??= System.Reflection.Assembly.GetCallingAssembly();

            var fullName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(resourceName, StringComparison.OrdinalIgnoreCase));

            if (fullName == null)
                return null;

            using var stream = assembly.GetManifestResourceStream(fullName);
            if (stream == null)
                return null;

            using var reader = new StreamReader(stream);
            var content = reader.ReadToEnd();
            _cache[cacheKey] = content;
            return content;
        }
    }

    /// <summary>
    /// Load a system prompt from an embedded resource associated with a specific type.
    /// Looks for resources in the same namespace as the type.
    /// </summary>
    /// <typeparam name="T">Type to use for resource lookup</typeparam>
    /// <param name="resourceName">Resource name (e.g., "SystemPrompt.txt")</param>
    /// <returns>Prompt content or null if not found</returns>
    public static string? LoadFromType<T>(string resourceName)
    {
        var assembly = typeof(T).Assembly;
        var ns = typeof(T).Namespace;
        var fullResourceName = $"{ns}.{resourceName}";
        
        lock (_lock)
        {
            var cacheKey = $"type:{typeof(T).FullName}:{resourceName}";
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            // Try exact match first
            using var stream = assembly.GetManifestResourceStream(fullResourceName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var content = reader.ReadToEnd();
                _cache[cacheKey] = content;
                return content;
            }

            // Fall back to suffix match
            return LoadFromResource(resourceName, assembly);
        }
    }

    /// <summary>
    /// Load a system prompt from a file path.
    /// </summary>
    /// <param name="filePath">Path to the prompt file</param>
    /// <param name="useCache">Whether to cache the result (default true)</param>
    /// <returns>Prompt content or null if not found</returns>
    public static string? LoadFromFile(string filePath, bool useCache = true)
    {
        lock (_lock)
        {
            var cacheKey = $"file:{filePath}";
            if (useCache && _cache.TryGetValue(cacheKey, out var cached))
                return cached;

            if (!File.Exists(filePath))
                return null;

            var content = File.ReadAllText(filePath);
            if (useCache)
                _cache[cacheKey] = content;
            return content;
        }
    }

    /// <summary>
    /// Load a prompt with a fallback default value.
    /// </summary>
    /// <param name="filePath">Path to the prompt file</param>
    /// <param name="defaultPrompt">Default prompt to return if file not found</param>
    /// <param name="useCache">Whether to cache the result</param>
    /// <returns>Prompt content or default</returns>
    public static string LoadOrDefault(string filePath, string defaultPrompt, bool useCache = true)
    {
        return LoadFromFile(filePath, useCache) ?? defaultPrompt;
    }

    #endregion

    #region Asynchronous Loading

    /// <summary>
    /// Asynchronously load a system prompt from a file path.
    /// </summary>
    /// <param name="filePath">Path to the prompt file</param>
    /// <param name="useCache">Whether to cache the result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Prompt content or null if not found</returns>
    public static async Task<string?> LoadFromFileAsync(string filePath, bool useCache = true, CancellationToken cancellationToken = default)
    {
        await _asyncLock.WaitAsync(cancellationToken);
        try
        {
            var cacheKey = $"file:{filePath}";
            if (useCache && _cache.TryGetValue(cacheKey, out var cached))
                return cached;

            if (!File.Exists(filePath))
                return null;

            var content = await File.ReadAllTextAsync(filePath, cancellationToken);
            if (useCache)
            {
                lock (_lock)
                {
                    _cache[cacheKey] = content;
                }
            }
            return content;
        }
        finally
        {
            _asyncLock.Release();
        }
    }

    /// <summary>
    /// Asynchronously load a prompt with a fallback default value.
    /// </summary>
    public static async Task<string> LoadOrDefaultAsync(string filePath, string defaultPrompt, bool useCache = true, CancellationToken cancellationToken = default)
    {
        return await LoadFromFileAsync(filePath, useCache, cancellationToken) ?? defaultPrompt;
    }

    #endregion

    #region Variable Substitution

    /// <summary>
    /// Apply variable substitution to a prompt template.
    /// Variables are in the format {{variableName}}.
    /// </summary>
    /// <param name="template">Prompt template with {{variables}}</param>
    /// <param name="variables">Dictionary of variable names to values</param>
    /// <returns>Prompt with variables substituted</returns>
    public static string ApplyVariables(string template, IDictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template) || variables == null || variables.Count == 0)
            return template;

        var result = template;
        foreach (var (key, value) in variables)
        {
            result = result.Replace("{{" + key + "}}", value, StringComparison.OrdinalIgnoreCase);
        }
        return result;
    }

    /// <summary>
    /// Apply variable substitution using an anonymous object.
    /// </summary>
    /// <param name="template">Prompt template with {{variables}}</param>
    /// <param name="variables">Anonymous object with properties as variables</param>
    /// <returns>Prompt with variables substituted</returns>
    public static string ApplyVariables(string template, object variables)
    {
        if (string.IsNullOrEmpty(template) || variables == null)
            return template;

        var dict = variables.GetType()
            .GetProperties()
            .ToDictionary(
                p => p.Name,
                p => p.GetValue(variables)?.ToString() ?? ""
            );

        return ApplyVariables(template, dict);
    }

    #endregion

    #region Prompt Composition

    /// <summary>
    /// Process include directives in a prompt template.
    /// Supports {{include:path/to/file.txt}} syntax for file includes.
    /// </summary>
    /// <param name="template">Template with include directives</param>
    /// <param name="basePath">Base path for resolving relative includes</param>
    /// <param name="maxDepth">Maximum include depth to prevent infinite recursion</param>
    /// <returns>Processed template with includes resolved</returns>
    public static string ProcessIncludes(string template, string? basePath = null, int maxDepth = 5)
    {
        if (string.IsNullOrEmpty(template) || maxDepth <= 0)
            return template;

        return IncludePattern.Replace(template, match =>
        {
            var includePath = match.Groups[1].Value.Trim();
            
            // Resolve relative path
            if (!Path.IsPathRooted(includePath) && !string.IsNullOrEmpty(basePath))
            {
                includePath = Path.Combine(basePath, includePath);
            }

            var includeContent = LoadFromFile(includePath, useCache: true);
            if (includeContent == null)
            {
                return $"[INCLUDE NOT FOUND: {includePath}]";
            }

            // Recursively process nested includes
            return ProcessIncludes(includeContent, Path.GetDirectoryName(includePath), maxDepth - 1);
        });
    }

    /// <summary>
    /// Build a prompt from multiple sections with headers.
    /// </summary>
    /// <param name="sections">Tuples of (header, content)</param>
    /// <returns>Combined prompt with markdown headers</returns>
    public static string BuildFromSections(params (string Header, string Content)[] sections)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var (header, content) in sections)
        {
            if (string.IsNullOrEmpty(content))
                continue;

            if (!string.IsNullOrEmpty(header))
            {
                sb.AppendLine($"## {header}");
                sb.AppendLine();
            }
            sb.AppendLine(content);
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Build a prompt by combining a base template with additional context sections.
    /// </summary>
    /// <param name="basePrompt">The main system prompt</param>
    /// <param name="contextSections">Additional context sections to append</param>
    /// <returns>Combined prompt</returns>
    public static string BuildWithContext(string basePrompt, params (string Header, string Content)[] contextSections)
    {
        if (contextSections.Length == 0)
            return basePrompt;

        var contextPart = BuildFromSections(contextSections);
        if (string.IsNullOrEmpty(contextPart))
            return basePrompt;

        return $"{basePrompt}\n\n{contextPart}";
    }

    #endregion

    #region Cache Management

    /// <summary>
    /// Clear the prompt cache.
    /// </summary>
    public static void ClearCache()
    {
        lock (_lock)
        {
            _cache.Clear();
        }
    }

    /// <summary>
    /// Remove a specific entry from the cache.
    /// </summary>
    /// <param name="key">Cache key to remove (file:path or resource:name)</param>
    /// <returns>True if removed, false if not found</returns>
    public static bool RemoveFromCache(string key)
    {
        lock (_lock)
        {
            return _cache.Remove(key);
        }
    }

    /// <summary>
    /// Get cache statistics for debugging.
    /// </summary>
    /// <returns>Cache statistics</returns>
    public static CacheStatistics GetCacheStatistics()
    {
        lock (_lock)
        {
            return new CacheStatistics
            {
                EntryCount = _cache.Count,
                TotalCharacters = _cache.Values.Sum(v => v.Length),
                Keys = _cache.Keys.ToList()
            };
        }
    }

    /// <summary>
    /// Pre-warm the cache with prompts from a directory.
    /// </summary>
    /// <param name="directoryPath">Directory containing prompt files</param>
    /// <param name="searchPattern">File search pattern (default: *.txt)</param>
    /// <param name="recursive">Whether to search subdirectories</param>
    /// <returns>Number of prompts loaded</returns>
    public static int PrewarmCache(string directoryPath, string searchPattern = "*.txt", bool recursive = false)
    {
        if (!Directory.Exists(directoryPath))
            return 0;

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var files = Directory.GetFiles(directoryPath, searchPattern, searchOption);
        
        var count = 0;
        foreach (var file in files)
        {
            if (LoadFromFile(file, useCache: true) != null)
                count++;
        }
        return count;
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// Cache statistics for debugging and monitoring.
    /// </summary>
    public class CacheStatistics
    {
        /// <summary>Number of cached entries</summary>
        public int EntryCount { get; init; }
        
        /// <summary>Total character count across all cached prompts</summary>
        public long TotalCharacters { get; init; }
        
        /// <summary>Estimated memory usage in bytes (approximate)</summary>
        public long EstimatedMemoryBytes => TotalCharacters * sizeof(char);
        
        /// <summary>List of cache keys</summary>
        public IReadOnlyList<string> Keys { get; init; } = Array.Empty<string>();
    }

    #endregion
}