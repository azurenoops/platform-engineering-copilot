using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;

namespace Platform.Engineering.Copilot.Core.Services.Agents;

/// <summary>
/// Shared memory service for agent communication and context sharing
/// </summary>
public class SharedMemory
{
    private readonly ConcurrentDictionary<string, ConversationContext> _contexts;
    private readonly ConcurrentDictionary<string, List<AgentCommunication>> _communications;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _generatedFiles;
    private readonly ILogger<SharedMemory> _logger;

    public SharedMemory(ILogger<SharedMemory> logger)
    {
        _contexts = new ConcurrentDictionary<string, ConversationContext>();
        _communications = new ConcurrentDictionary<string, List<AgentCommunication>>();
        _generatedFiles = new ConcurrentDictionary<string, Dictionary<string, string>>();
        _logger = logger;
    }

    /// <summary>
    /// Store conversation context
    /// </summary>
    public void StoreContext(string conversationId, ConversationContext context)
    {
        _contexts[conversationId] = context;
        _logger.LogDebug("Stored context for conversation: {ConversationId}", conversationId);
    }

    /// <summary>
    /// Retrieve conversation context
    /// </summary>
    public ConversationContext GetContext(string conversationId)
    {
        return _contexts.TryGetValue(conversationId, out var context) 
            ? context 
            : new ConversationContext { ConversationId = conversationId };
    }

    /// <summary>
    /// Check if context exists for conversation
    /// </summary>
    public bool HasContext(string conversationId)
    {
        return _contexts.ContainsKey(conversationId);
    }

    /// <summary>
    /// Add agent communication
    /// </summary>
    public void AddAgentCommunication(
        string conversationId,
        AgentType fromAgent,
        AgentType? toAgent,
        string message,
        object? data = null)
    {
        var communication = new AgentCommunication
        {
            Timestamp = DateTime.UtcNow,
            FromAgent = fromAgent,
            ToAgent = toAgent,
            Message = message,
            Data = data
        };

        _communications.AddOrUpdate(
            conversationId,
            new List<AgentCommunication> { communication },
            (key, existingList) =>
            {
                existingList.Add(communication);
                return existingList;
            });

        _logger.LogDebug(
            "Agent communication: {From} → {To}: {Message}", 
            fromAgent, 
            toAgent?.ToString() ?? "broadcast", 
            message);
    }

    /// <summary>
    /// Get all communications for a conversation
    /// </summary>
    public List<AgentCommunication> GetAgentCommunications(
        string conversationId,
        AgentType? agentType = null)
    {
        if (!_communications.TryGetValue(conversationId, out var comms))
        {
            return new List<AgentCommunication>();
        }

        if (agentType == null)
        {
            return comms.ToList();
        }

        return comms.Where(c => 
            c.FromAgent == agentType || c.ToAgent == agentType)
            .ToList();
    }

    /// <summary>
    /// Get the latest communication from a specific agent
    /// </summary>
    public AgentCommunication? GetLatestCommunication(
        string conversationId,
        AgentType fromAgent)
    {
        if (!_communications.TryGetValue(conversationId, out var comms))
        {
            return null;
        }

        return comms
            .Where(c => c.FromAgent == fromAgent)
            .OrderByDescending(c => c.Timestamp)
            .FirstOrDefault();
    }

    /// <summary>
    /// Clear all data for a conversation (cleanup)
    /// </summary>
    public void ClearConversation(string conversationId)
    {
        _contexts.TryRemove(conversationId, out _);
        _communications.TryRemove(conversationId, out _);
        _generatedFiles.TryRemove(conversationId, out _);
        _logger.LogDebug("Cleared conversation: {ConversationId}", conversationId);
    }

    /// <summary>
    /// Store generated template files for a conversation
    /// </summary>
    public void StoreGeneratedFiles(string conversationId, Dictionary<string, string> files)
    {
        _generatedFiles[conversationId] = files;
        _logger.LogDebug("Stored {Count} generated files for conversation: {ConversationId}", files.Count, conversationId);
    }

    /// <summary>
    /// Retrieve a specific generated file
    /// </summary>
    public string? GetGeneratedFile(string conversationId, string fileName)
    {
        if (_generatedFiles.TryGetValue(conversationId, out var files))
        {
            return files.TryGetValue(fileName, out var content) ? content : null;
        }
        return null;
    }

    /// <summary>
    /// Get list of all generated file names for a conversation
    /// </summary>
    public List<string> GetGeneratedFileNames(string conversationId)
    {
        if (_generatedFiles.TryGetValue(conversationId, out var files))
        {
            return files.Keys.ToList();
        }
        return new List<string>();
    }

    /// <summary>
    /// Get summary of conversation activity
    /// </summary>
    public string GetConversationSummary(string conversationId)
    {
        var context = GetContext(conversationId);
        var comms = GetAgentCommunications(conversationId);

        return $"Conversation {conversationId}: " +
               $"{context.PreviousResults.Count} agent responses, " +
               $"{comms.Count} communications";
    }

    // Deployment metadata storage for agent-to-agent communication
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _deploymentMetadata = new();

    /// <summary>
    /// Store deployment metadata (resource group, subscription ID, etc.) for a conversation
    /// </summary>
    public void StoreDeploymentMetadata(string conversationId, Dictionary<string, string> metadata)
    {
        _deploymentMetadata[conversationId] = metadata;
        _logger.LogDebug("Stored deployment metadata for conversation: {ConversationId}", conversationId);
    }

    /// <summary>
    /// Retrieve deployment metadata for a conversation
    /// </summary>
    public Dictionary<string, string>? GetDeploymentMetadata(string conversationId)
    {
        return _deploymentMetadata.TryGetValue(conversationId, out var metadata) ? metadata : null;
    }
}
