namespace Platform.Engineering.Copilot.Core.Agents;

/// <summary>
/// Shared state store for cross-agent configuration (FR-043).
/// Configuration Agent writes settings; other agents read them.
/// Keys are prefixed by agent (e.g., "config:" for Configuration Agent).
/// Thread-safe concurrent dictionary backed.
/// </summary>
public interface IAgentStateManager
{
    /// <summary>Store a value by key.</summary>
    void Set(string key, object value);

    /// <summary>Retrieve a value by key.</summary>
    T? Get<T>(string key) where T : class;

    /// <summary>Retrieve a value by key, returning null if not found.</summary>
    string? GetString(string key);

    /// <summary>Check if a key exists.</summary>
    bool ContainsKey(string key);

    /// <summary>Remove a key.</summary>
    bool Remove(string key);

    /// <summary>Get all keys with a given prefix.</summary>
    IEnumerable<string> GetKeysWithPrefix(string prefix);
}

/// <summary>
/// In-memory implementation of IAgentStateManager using ConcurrentDictionary.
/// </summary>
public class InMemoryAgentStateManager : IAgentStateManager
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object> _state = new();

    public void Set(string key, object value)
    {
        _state[key] = value;
    }

    public T? Get<T>(string key) where T : class
    {
        return _state.TryGetValue(key, out var value) ? value as T : null;
    }

    public string? GetString(string key)
    {
        return _state.TryGetValue(key, out var value) ? value?.ToString() : null;
    }

    public bool ContainsKey(string key)
    {
        return _state.ContainsKey(key);
    }

    public bool Remove(string key)
    {
        return _state.TryRemove(key, out _);
    }

    public IEnumerable<string> GetKeysWithPrefix(string prefix)
    {
        return _state.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
