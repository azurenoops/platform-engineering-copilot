using Microsoft.AspNetCore.SignalR;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Chat.Hubs;

/// <summary>
/// T138 — Real-time bidirectional chat hub per signalr-hub.md.
/// Server→Client: ReceiveMessage, StreamToken, ProgressUpdate, AuthRequired, SessionStatus, ErrorNotification
/// Client→Server: SendMessage, ConfirmAction, CancelAction, UpdateAuth
/// Supports session context retention ≥10 messages (SC-006) and Markdown rendering (FR-048).
/// </summary>
public class ChatHub : Hub
{
    private readonly PlatformOrchestrator _orchestrator;
    private readonly ILogger<ChatHub> _logger;

    // In-memory session store (conversation context per connection)
    private static readonly Dictionary<string, ConversationSession> Sessions = new();
    private static readonly object SessionLock = new();

    public ChatHub(PlatformOrchestrator orchestrator, ILogger<ChatHub> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    /// <summary>
    /// Client→Server: Send a user message for orchestrator routing.
    /// Returns correlationId for matching streaming responses.
    /// </summary>
    public async Task<string> SendMessage(ChatMessage message)
    {
        var correlationId = Guid.NewGuid().ToString();
        var connectionId = Context.ConnectionId;

        _logger.LogInformation("ChatHub.SendMessage: correlationId={CorrelationId}, conversationId={ConversationId}",
            correlationId, message.ConversationId);

        // Create or retrieve conversation session
        var session = GetOrCreateSession(connectionId, message.ConversationId);
        session.AddMessage(new SessionMessage
        {
            Role = "User",
            Content = message.Content,
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = correlationId
        });

        // Route through orchestrator
        try
        {
            var routingResult = await _orchestrator.RouteAsync(message.Content);

            if (routingResult.IsMatch && routingResult.Agent is not null)
            {
                // Execute the agent's first matching tool or use routing explanation
                var agentResponse = routingResult.Explanation;

                // Try to execute via the agent
                try
                {
                    var toolResult = await routingResult.Agent.ExecuteToolAsync(
                        routingResult.Agent.GetToolMetadata().First().Name,
                        new Dictionary<string, object?>(),
                        new Progress<ProgressUpdate>(async p =>
                        {
                            await Clients.Caller.SendAsync("ProgressUpdate", new
                            {
                                correlationId,
                                percentComplete = p.PercentComplete,
                                status = p.PercentComplete >= 100 ? "Completed" : "Running",
                                phase = p.Message
                            });
                        }));
                    if (!string.IsNullOrEmpty(toolResult))
                        agentResponse = toolResult;
                }
                catch
                {
                    // If tool execution fails, use the routing explanation
                }

                // Stream tokens progressively
                var tokens = SplitIntoTokens(agentResponse);
                foreach (var token in tokens)
                {
                    await Clients.Caller.SendAsync("StreamToken", new
                    {
                        correlationId,
                        token,
                        isComplete = false
                    });
                }

                // Send completion token
                var fullMessageId = Guid.NewGuid().ToString();
                await Clients.Caller.SendAsync("StreamToken", new
                {
                    correlationId,
                    token = "",
                    isComplete = true,
                    fullMessageId
                });

                // Send complete message
                await Clients.Caller.SendAsync("ReceiveMessage", new
                {
                    messageId = fullMessageId,
                    agentId = routingResult.Agent.AgentId,
                    agentName = routingResult.Agent.AgentName,
                    content = agentResponse,
                    role = "Assistant",
                    correlationId,
                    timestamp = DateTimeOffset.UtcNow.ToString("o")
                });

                // Store in session
                session.AddMessage(new SessionMessage
                {
                    Role = "Assistant",
                    Content = agentResponse,
                    Timestamp = DateTimeOffset.UtcNow,
                    CorrelationId = correlationId,
                    AgentId = routingResult.Agent.AgentId
                });
            }
            else
            {
                await Clients.Caller.SendAsync("ErrorNotification", new
                {
                    correlationId,
                    code = "NO_AGENT_MATCHED",
                    message = "No agent could handle this request. Try rephrasing or specifying an agent.",
                    troubleshooting = "Use a keyword related to compliance, cost, infrastructure, discovery, environment, security, or configuration.",
                    retryable = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ChatHub.SendMessage failed: correlationId={CorrelationId}", correlationId);
            await Clients.Caller.SendAsync("ErrorNotification", new
            {
                correlationId,
                code = "INTERNAL_ERROR",
                message = "An unexpected error occurred processing your request.",
                troubleshooting = "Try again or contact support if the issue persists.",
                retryable = true
            });
        }

        return correlationId;
    }

    /// <summary>
    /// Client→Server: Confirm a pending action (e.g., remediation, high-risk op).
    /// </summary>
    public async Task ConfirmAction(ConfirmActionRequest request)
    {
        _logger.LogInformation("ChatHub.ConfirmAction: correlationId={CorrelationId}, confirmed={Confirmed}",
            request.CorrelationId, request.Confirmed);

        if (request.Confirmed)
        {
            await Clients.Caller.SendAsync("ReceiveMessage", new
            {
                messageId = Guid.NewGuid().ToString(),
                agentId = "system",
                agentName = "System",
                content = $"Action confirmed. Justification: {request.Justification ?? "None provided"}",
                role = "System",
                correlationId = request.CorrelationId,
                timestamp = DateTimeOffset.UtcNow.ToString("o")
            });
        }
        else
        {
            await Clients.Caller.SendAsync("ReceiveMessage", new
            {
                messageId = Guid.NewGuid().ToString(),
                agentId = "system",
                agentName = "System",
                content = "Action cancelled by user.",
                role = "System",
                correlationId = request.CorrelationId,
                timestamp = DateTimeOffset.UtcNow.ToString("o")
            });
        }
    }

    /// <summary>
    /// Client→Server: Cancel a running or pending action.
    /// </summary>
    public async Task CancelAction(CancelActionRequest request)
    {
        _logger.LogInformation("ChatHub.CancelAction: correlationId={CorrelationId}", request.CorrelationId);

        await Clients.Caller.SendAsync("ReceiveMessage", new
        {
            messageId = Guid.NewGuid().ToString(),
            agentId = "system",
            agentName = "System",
            content = "Operation cancelled.",
            role = "System",
            correlationId = request.CorrelationId,
            timestamp = DateTimeOffset.UtcNow.ToString("o")
        });
    }

    /// <summary>
    /// Client→Server: Update authentication state (CAC token, PIM activation).
    /// </summary>
    public async Task UpdateAuth(UpdateAuthRequest request)
    {
        _logger.LogInformation("ChatHub.UpdateAuth: pimActivated={PimActivated}, pimTier={PimTier}",
            request.PimActivated, request.PimTier);

        // Update session auth state
        var session = GetSessionForConnection(Context.ConnectionId);
        if (session is not null)
        {
            session.CacActive = !string.IsNullOrEmpty(request.CacToken);
            session.PimActive = request.PimActivated;
            session.PimTier = request.PimTier;
        }

        // Send auth status back
        await Clients.Caller.SendAsync("SessionStatus", new
        {
            cacStatus = !string.IsNullOrEmpty(request.CacToken) ? "Active" : "Inactive",
            cacRemainingMinutes = !string.IsNullOrEmpty(request.CacToken) ? 480 : 0,
            pimStatus = request.PimActivated ? "Active" : "Inactive",
            pimTier = request.PimTier ?? "None",
            pimRemainingMinutes = request.PimActivated ? 240 : 0,
            roles = new[] { "ComplianceOfficer" }
        });
    }

    /// <summary>
    /// On client connection — send initial session status.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("ChatHub.OnConnected: connectionId={ConnectionId}", Context.ConnectionId);

        await Clients.Caller.SendAsync("SessionStatus", new
        {
            cacStatus = "Inactive",
            cacRemainingMinutes = 0,
            pimStatus = "Inactive",
            pimTier = "None",
            pimRemainingMinutes = 0,
            roles = Array.Empty<string>()
        });

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// On client disconnect — clean up session.
    /// </summary>
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("ChatHub.OnDisconnected: connectionId={ConnectionId}", Context.ConnectionId);
        RemoveSession(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }

    // ─── Session management (thread-safe) ───

    private ConversationSession GetOrCreateSession(string connectionId, string? conversationId)
    {
        lock (SessionLock)
        {
            var key = conversationId ?? connectionId;
            if (!Sessions.TryGetValue(key, out var session))
            {
                session = new ConversationSession
                {
                    ConversationId = conversationId ?? Guid.NewGuid().ToString(),
                    ConnectionId = connectionId,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                Sessions[key] = session;
            }
            return session;
        }
    }

    private ConversationSession? GetSessionForConnection(string connectionId)
    {
        lock (SessionLock)
        {
            return Sessions.Values.FirstOrDefault(s => s.ConnectionId == connectionId);
        }
    }

    private void RemoveSession(string connectionId)
    {
        lock (SessionLock)
        {
            var keysToRemove = Sessions
                .Where(kvp => kvp.Value.ConnectionId == connectionId)
                .Select(kvp => kvp.Key)
                .ToList();
            foreach (var key in keysToRemove)
                Sessions.Remove(key);
        }
    }

    private static string[] SplitIntoTokens(string content)
    {
        return content.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w + " ")
            .ToArray();
    }
}

// ─── DTOs ───

public class ChatMessage
{
    public string? ConversationId { get; set; }
    public string Content { get; set; } = "";
    public string? TargetAgentId { get; set; }
}

public class ConfirmActionRequest
{
    public string CorrelationId { get; set; } = "";
    public bool Confirmed { get; set; }
    public string? Justification { get; set; }
}

public class CancelActionRequest
{
    public string CorrelationId { get; set; } = "";
}

public class UpdateAuthRequest
{
    public string? CacToken { get; set; }
    public bool PimActivated { get; set; }
    public string? PimTier { get; set; }
    public string? PimJustification { get; set; }
}

public class ConversationSession
{
    public string ConversationId { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public bool CacActive { get; set; }
    public bool PimActive { get; set; }
    public string? PimTier { get; set; }

    private readonly List<SessionMessage> _messages = new();
    private readonly object _lock = new();

    /// <summary>
    /// Messages in this conversation session. Supports ≥10 message retention (SC-006).
    /// </summary>
    public IReadOnlyList<SessionMessage> Messages
    {
        get { lock (_lock) { return _messages.ToList().AsReadOnly(); } }
    }

    public void AddMessage(SessionMessage message)
    {
        lock (_lock) { _messages.Add(message); }
    }

    public int MessageCount
    {
        get { lock (_lock) { return _messages.Count; } }
    }
}

public class SessionMessage
{
    public string Role { get; set; } = "";
    public string Content { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
    public string CorrelationId { get; set; } = "";
    public string? AgentId { get; set; }
}
