using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Auth;

/// <summary>
/// T143 — Session expiration mid-operation handler per FR-014.
/// Graceful stop on CAC/PIM expiration, partial result preservation,
/// re-auth prompt for only the expired component, resume from checkpoint.
/// </summary>
public class SessionExpirationHandler
{
    private readonly ILogger<SessionExpirationHandler> _logger;

    public SessionExpirationHandler(ILogger<SessionExpirationHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Details of a user's current session status.
    /// </summary>
    public SessionState CheckSessionState(SessionInfo session)
    {
        var now = DateTimeOffset.UtcNow;
        var cacExpired = session.CacExpiresAt.HasValue && now > session.CacExpiresAt.Value;
        var pimExpired = session.PimExpiresAt.HasValue && now > session.PimExpiresAt.Value;

        var cacNearExpiry = !cacExpired && session.CacExpiresAt.HasValue
            && session.CacExpiresAt.Value - now < TimeSpan.FromMinutes(5);
        var pimNearExpiry = !pimExpired && session.PimExpiresAt.HasValue
            && session.PimExpiresAt.Value - now < TimeSpan.FromMinutes(5);

        return new SessionState
        {
            CacActive = !cacExpired && session.CacExpiresAt.HasValue,
            CacExpired = cacExpired,
            CacNearExpiry = cacNearExpiry,
            CacRemainingMinutes = session.CacExpiresAt.HasValue
                ? Math.Max(0, (int)(session.CacExpiresAt.Value - now).TotalMinutes)
                : 0,
            PimActive = !pimExpired && session.PimExpiresAt.HasValue,
            PimExpired = pimExpired,
            PimNearExpiry = pimNearExpiry,
            PimTier = session.PimTier,
            PimRemainingMinutes = session.PimExpiresAt.HasValue
                ? Math.Max(0, (int)(session.PimExpiresAt.Value - now).TotalMinutes)
                : 0
        };
    }

    /// <summary>
    /// Check if a tool can proceed given current session state.
    /// Returns null if OK, or a ReauthRequest if re-auth is needed.
    /// </summary>
    public ReauthRequest? ValidateForTool(SessionInfo session, bool requiresAuth, PimTier requiredPimTier)
    {
        if (!requiresAuth && requiredPimTier == PimTier.None)
            return null;

        var state = CheckSessionState(session);
        var expiredComponents = new List<string>();
        var message = new List<string>();

        if (requiresAuth && state.CacExpired)
        {
            expiredComponents.Add("CAC");
            message.Add("Your CAC/PIV session has expired.");
        }

        if (requiredPimTier != PimTier.None)
        {
            if (state.PimExpired)
            {
                expiredComponents.Add("PIM");
                message.Add($"Your PIM {requiredPimTier} elevation has expired.");
            }
            else if (!state.PimActive)
            {
                expiredComponents.Add("PIM");
                message.Add($"PIM {requiredPimTier} elevation is required but not active.");
            }
            else if (requiredPimTier == PimTier.Write && state.PimTier == PimTier.Read)
            {
                expiredComponents.Add("PIM");
                message.Add("This operation requires PIM Write elevation, but you only have Read.");
            }
        }

        if (expiredComponents.Count == 0)
            return null;

        _logger.LogWarning("Session validation failed: expired={Components}", string.Join(",", expiredComponents));

        return new ReauthRequest
        {
            RequiredComponents = expiredComponents.ToArray(),
            PimTier = requiredPimTier.ToString(),
            Message = string.Join(" ", message) + " Please re-authenticate to continue.",
            RetryAction = "resume_from_checkpoint"
        };
    }

    /// <summary>
    /// Create a checkpoint for partial result preservation during mid-operation expiration.
    /// </summary>
    public OperationCheckpoint CreateCheckpoint(string operationId, string toolName,
        int completedSteps, int totalSteps, object? partialResult)
    {
        var checkpoint = new OperationCheckpoint
        {
            CheckpointId = Guid.NewGuid().ToString(),
            OperationId = operationId,
            ToolName = toolName,
            CompletedSteps = completedSteps,
            TotalSteps = totalSteps,
            PartialResult = partialResult,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };

        _logger.LogInformation(
            "Checkpoint created: {CheckpointId} for {ToolName} at step {Completed}/{Total}",
            checkpoint.CheckpointId, toolName, completedSteps, totalSteps);

        return checkpoint;
    }
}

/// <summary>Session info for validation.</summary>
public class SessionInfo
{
    public string UserId { get; set; } = "";
    public DateTimeOffset? CacExpiresAt { get; set; }
    public DateTimeOffset? PimExpiresAt { get; set; }
    public PimTier PimTier { get; set; } = PimTier.None;
}

/// <summary>Current session state snapshot.</summary>
public class SessionState
{
    public bool CacActive { get; set; }
    public bool CacExpired { get; set; }
    public bool CacNearExpiry { get; set; }
    public int CacRemainingMinutes { get; set; }
    public bool PimActive { get; set; }
    public bool PimExpired { get; set; }
    public bool PimNearExpiry { get; set; }
    public PimTier PimTier { get; set; }
    public int PimRemainingMinutes { get; set; }
}

/// <summary>Re-authentication request per signalr-hub.md AuthRequired.</summary>
public class ReauthRequest
{
    public string[] RequiredComponents { get; set; } = [];
    public string PimTier { get; set; } = "";
    public string Message { get; set; } = "";
    public string RetryAction { get; set; } = "";
}

/// <summary>Operation checkpoint for resuming after re-auth.</summary>
public class OperationCheckpoint
{
    public string CheckpointId { get; set; } = "";
    public string OperationId { get; set; } = "";
    public string ToolName { get; set; } = "";
    public int CompletedSteps { get; set; }
    public int TotalSteps { get; set; }
    public object? PartialResult { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
