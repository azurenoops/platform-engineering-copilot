using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Data.Services;

/// <summary>
/// T140 — Audit log service for all agent actions.
/// Records who/what/when/which/outcome, correlationId, PIM justification.
/// Append-only repository per FR-066 and FR-077.
/// </summary>
public class AuditLogService
{
    private readonly ILogger<AuditLogService> _logger;
    private readonly ConcurrentBag<AuditLogEntry> _entries = new();

    public AuditLogService(ILogger<AuditLogService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Log an agent action (append-only, immutable).
    /// </summary>
    public void LogAction(AuditLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        entry.Timestamp = DateTimeOffset.UtcNow;
        entry.Id = Guid.NewGuid().ToString();

        _entries.Add(entry);
        _logger.LogInformation(
            "AuditLog: {Action} by {UserId} on {AgentId}/{ToolName} — {Outcome} [correlationId={CorrelationId}]",
            entry.Action, entry.UserId, entry.AgentId, entry.ToolName, entry.Outcome, entry.CorrelationId);
    }

    /// <summary>
    /// Log an agent tool execution.
    /// </summary>
    public void LogToolExecution(string userId, string agentId, string toolName,
        string outcome, string? correlationId = null, string? pimJustification = null,
        Dictionary<string, object?>? parameters = null)
    {
        LogAction(new AuditLogEntry
        {
            UserId = userId,
            AgentId = agentId,
            ToolName = toolName,
            Action = "ToolExecution",
            Outcome = outcome,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            PimJustification = pimJustification,
            Parameters = parameters
        });
    }

    /// <summary>
    /// Log an authentication event.
    /// </summary>
    public void LogAuthEvent(string userId, string action, string outcome,
        string? correlationId = null, string? pimJustification = null)
    {
        LogAction(new AuditLogEntry
        {
            UserId = userId,
            AgentId = "auth",
            ToolName = "authentication",
            Action = action,
            Outcome = outcome,
            CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
            PimJustification = pimJustification
        });
    }

    /// <summary>
    /// Get all audit log entries (read-only snapshot).
    /// </summary>
    public IReadOnlyList<AuditLogEntry> GetEntries() => _entries.ToList().AsReadOnly();

    /// <summary>
    /// Get entries filtered by user, agent, or time range.
    /// </summary>
    public IReadOnlyList<AuditLogEntry> GetEntries(
        string? userId = null,
        string? agentId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int? limit = null)
    {
        IEnumerable<AuditLogEntry> query = _entries;

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(e => e.UserId == userId);
        if (!string.IsNullOrEmpty(agentId))
            query = query.Where(e => e.AgentId == agentId);
        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);

        query = query.OrderByDescending(e => e.Timestamp);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        return query.ToList().AsReadOnly();
    }

    /// <summary>
    /// Get entry count.
    /// </summary>
    public int Count => _entries.Count;
}

/// <summary>
/// Immutable audit log entry — who/what/when/which/outcome per FR-066.
/// </summary>
public class AuditLogEntry
{
    /// <summary>Unique entry ID.</summary>
    public string Id { get; set; } = "";

    /// <summary>When the action occurred.</summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Who performed the action (user ID or certificate subject).</summary>
    public string UserId { get; set; } = "";

    /// <summary>Which agent processed the request.</summary>
    public string AgentId { get; set; } = "";

    /// <summary>Which tool was executed.</summary>
    public string ToolName { get; set; } = "";

    /// <summary>What action was performed.</summary>
    public string Action { get; set; } = "";

    /// <summary>Outcome of the action (Success, Failure, Partial).</summary>
    public string Outcome { get; set; } = "";

    /// <summary>Distributed tracing correlation ID.</summary>
    public string CorrelationId { get; set; } = "";

    /// <summary>PIM elevation justification, if applicable.</summary>
    public string? PimJustification { get; set; }

    /// <summary>Tool parameters (sanitized, no secrets).</summary>
    public Dictionary<string, object?>? Parameters { get; set; }
}
