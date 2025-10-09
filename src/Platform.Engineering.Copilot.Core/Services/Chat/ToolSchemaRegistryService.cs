using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models.SemanticParsing;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// In-memory registry for managing tool schemas and their metadata
/// </summary>
public class ToolSchemaRegistry : IToolSchemaRegistry
{
    private readonly ILogger<ToolSchemaRegistry> _logger;
    private readonly ConcurrentDictionary<string, ToolSchema> _tools = new();
    private readonly ConcurrentDictionary<IntentCategory, List<string>> _categoryIndex = new();
    private readonly ConcurrentDictionary<string, List<string>> _keywordIndex = new();

    public ToolSchemaRegistry(ILogger<ToolSchemaRegistry> logger)
    {
        _logger = logger;
    }

    public async Task RegisterToolAsync(ToolSchema toolSchema)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(toolSchema.Name))
            {
                throw new ArgumentException("Tool name cannot be empty", nameof(toolSchema));
            }

            _tools.AddOrUpdate(toolSchema.Name, toolSchema, (key, existing) => toolSchema);

            // Update category index
            _categoryIndex.AddOrUpdate(toolSchema.Category, new List<string> { toolSchema.Name }, 
                (category, existingTools) =>
                {
                    if (!existingTools.Contains(toolSchema.Name))
                    {
                        existingTools.Add(toolSchema.Name);
                    }
                    return existingTools;
                });

            // Update keyword index
            var allKeywords = toolSchema.Keywords.Concat(toolSchema.Aliases).Concat(new[] { toolSchema.Name });
            foreach (var keyword in allKeywords)
            {
                var normalizedKeyword = keyword.ToLowerInvariant();
                _keywordIndex.AddOrUpdate(normalizedKeyword, new List<string> { toolSchema.Name },
                    (key, existingTools) =>
                    {
                        if (!existingTools.Contains(toolSchema.Name))
                        {
                            existingTools.Add(toolSchema.Name);
                        }
                        return existingTools;
                    });
            }

            _logger.LogInformation("Registered tool schema: {ToolName} with {ParameterCount} parameters", 
                toolSchema.Name, toolSchema.Parameters.Count);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering tool schema: {ToolName}", toolSchema.Name);
            throw;
        }
    }

    public async Task<IEnumerable<ToolSchema>> GetAllToolsAsync()
    {
        await Task.CompletedTask;
        return _tools.Values.ToList();
    }

    public async Task<ToolSchema?> GetToolAsync(string toolName)
    {
        await Task.CompletedTask;
        _tools.TryGetValue(toolName, out var tool);
        return tool;
    }

    public async Task<IEnumerable<ToolSchema>> GetToolsByCategoryAsync(IntentCategory category)
    {
        await Task.CompletedTask;
        
        if (!_categoryIndex.TryGetValue(category, out var toolNames))
        {
            return Enumerable.Empty<ToolSchema>();
        }

        return toolNames.Select(name => _tools.TryGetValue(name, out var tool) ? tool : null)
                       .Where(tool => tool != null)
                       .Cast<ToolSchema>();
    }

    public async Task<IEnumerable<ToolSchema>> SearchToolsAsync(params string[] keywords)
    {
        await Task.CompletedTask;
        
        if (keywords == null || keywords.Length == 0)
        {
            return Enumerable.Empty<ToolSchema>();
        }

        var matchingToolNames = new HashSet<string>();

        foreach (var keyword in keywords)
        {
            var normalizedKeyword = keyword.ToLowerInvariant();
            
            // Exact match
            if (_keywordIndex.TryGetValue(normalizedKeyword, out var exactMatches))
            {
                foreach (var toolName in exactMatches)
                {
                    matchingToolNames.Add(toolName);
                }
            }

            // Partial matches
            var partialMatches = _keywordIndex.Keys
                .Where(k => k.Contains(normalizedKeyword) || normalizedKeyword.Contains(k))
                .SelectMany(k => _keywordIndex[k]);

            foreach (var toolName in partialMatches)
            {
                matchingToolNames.Add(toolName);
            }
        }

        return matchingToolNames.Select(name => _tools.TryGetValue(name, out var tool) ? tool : null)
                               .Where(tool => tool != null)
                               .Cast<ToolSchema>();
    }

    public async Task UpdateToolAsync(ToolSchema toolSchema)
    {
        await RegisterToolAsync(toolSchema); // Same logic as registration
    }

    public async Task RemoveToolAsync(string toolName)
    {
        try
        {
            if (_tools.TryRemove(toolName, out var removedTool))
            {
                // Clean up category index
                if (_categoryIndex.TryGetValue(removedTool.Category, out var categoryTools))
                {
                    categoryTools.Remove(toolName);
                    if (categoryTools.Count == 0)
                    {
                        _categoryIndex.TryRemove(removedTool.Category, out _);
                    }
                }

                // Clean up keyword index
                var keysToClean = _keywordIndex.Keys.ToList();
                foreach (var key in keysToClean)
                {
                    if (_keywordIndex.TryGetValue(key, out var keywordTools))
                    {
                        keywordTools.Remove(toolName);
                        if (keywordTools.Count == 0)
                        {
                            _keywordIndex.TryRemove(key, out _);
                        }
                    }
                }

                _logger.LogInformation("Removed tool schema: {ToolName}", toolName);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing tool schema: {ToolName}", toolName);
            throw;
        }
    }

    /// <summary>
    /// Get registry statistics for monitoring
    /// </summary>
    public async Task<object> GetStatisticsAsync()
    {
        await Task.CompletedTask;
        
        var categoryStats = _categoryIndex.ToDictionary(
            kvp => kvp.Key.ToString(), 
            kvp => kvp.Value.Count);

        return new
        {
            TotalTools = _tools.Count,
            TotalKeywords = _keywordIndex.Count,
            ToolsByCategory = categoryStats,
            MostReferencedKeywords = _keywordIndex
                .OrderByDescending(kvp => kvp.Value.Count)
                .Take(10)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Count)
        };
    }

    /// <summary>
    /// Export all tool schemas as JSON
    /// </summary>
    public async Task<string> ExportSchemasAsync()
    {
        var schemas = await GetAllToolsAsync();
        return JsonSerializer.Serialize(schemas, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Import tool schemas from JSON
    /// </summary>
    public async Task ImportSchemasAsync(string jsonSchemas)
    {
        try
        {
            var schemas = JsonSerializer.Deserialize<ToolSchema[]>(jsonSchemas, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (schemas != null)
            {
                foreach (var schema in schemas)
                {
                    await RegisterToolAsync(schema);
                }
            }

            _logger.LogInformation("Imported {Count} tool schemas", schemas?.Length ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing tool schemas from JSON");
            throw;
        }
    }
}