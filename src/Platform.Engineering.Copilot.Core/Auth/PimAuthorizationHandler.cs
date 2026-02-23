using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Auth;

/// <summary>
/// PIM authorization handler — enforces PIM tier requirements (None/Read/Write).
/// Validates role claims for tier eligibility and activation status.
/// Logs justification for elevated operations per FR-069–FR-071.
/// <para>
/// PIM tier is checked AFTER CAC authentication succeeds.
/// Certificate details and PIM tokens are never leaked in logs or responses (FR-016).
/// </para>
/// </summary>
public class PimAuthorizationHandler : AuthorizationHandler<PimTierRequirement>
{
    private readonly ILogger<PimAuthorizationHandler> _logger;
    private readonly bool _requirePim;

    /// <summary>Role claim name for PIM tier in JWT.</summary>
    public const string PimTierClaimType = "pim_tier";

    /// <summary>Role claim name for PIM active status in JWT.</summary>
    public const string PimActiveClaimType = "pim_active";

    /// <summary>Role claim name for PIM justification in JWT.</summary>
    public const string PimJustificationClaimType = "pim_justification";

    public PimAuthorizationHandler(
        ILogger<PimAuthorizationHandler> logger,
        bool requirePim = true)
    {
        _logger = logger;
        _requirePim = requirePim;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PimTierRequirement requirement)
    {
        // Dev bypass (FR-015)
        if (!_requirePim)
        {
            _logger.LogDebug("PIM authorization bypassed (RequirePim=false)");
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // No PIM needed
        if (requirement.RequiredTier == PimTier.None)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var principal = context.User;
        var userId = RedactUserId(principal);

        // Check eligibility first — user must be eligible for the tier
        var tierClaim = principal.FindFirst(PimTierClaimType)?.Value;
        if (string.IsNullOrEmpty(tierClaim))
        {
            _logger.LogWarning("PIM authorization failed: No PIM tier claim. UserId: {UserId}", userId);
            context.Fail(new AuthorizationFailureReason(this,
                "PIM elevation required. No PIM tier claim found."));
            return Task.CompletedTask;
        }

        if (!Enum.TryParse<PimTier>(tierClaim, true, out var userTier))
        {
            _logger.LogWarning("PIM authorization failed: Invalid tier claim '{Tier}'. UserId: {UserId}",
                tierClaim, userId);
            context.Fail(new AuthorizationFailureReason(this,
                "Invalid PIM tier claim."));
            return Task.CompletedTask;
        }

        // Check tier level — Write ≥ Read ≥ None
        if (userTier < requirement.RequiredTier)
        {
            _logger.LogWarning(
                "PIM authorization failed: User tier {UserTier} < required {RequiredTier}. UserId: {UserId}",
                userTier, requirement.RequiredTier, userId);
            context.Fail(new AuthorizationFailureReason(this,
                $"PIM elevation to {requirement.RequiredTier} tier required. " +
                $"Current tier: {userTier}. Please activate PIM elevation."));
            return Task.CompletedTask;
        }

        // Check activation status — tier must be currently active
        var isActive = principal.FindFirst(PimActiveClaimType)?.Value;
        if (!string.Equals(isActive, "true", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "PIM authorization failed: Tier {UserTier} not active. UserId: {UserId}",
                userTier, userId);
            context.Fail(new AuthorizationFailureReason(this,
                $"PIM {requirement.RequiredTier} tier is eligible but not active. " +
                "Please activate your PIM elevation with a justification."));
            return Task.CompletedTask;
        }

        // Log justification for audit trail (FR-071)
        var justification = principal.FindFirst(PimJustificationClaimType)?.Value;
        if (!string.IsNullOrEmpty(justification))
        {
            _logger.LogInformation(
                "PIM authorization succeeded: Tier={Tier}, Active=true, Justification present. UserId: {UserId}",
                userTier, userId);
        }
        else
        {
            _logger.LogInformation(
                "PIM authorization succeeded: Tier={Tier}, Active=true. UserId: {UserId}",
                userTier, userId);
        }

        context.Succeed(requirement);
        return Task.CompletedTask;
    }

    private static string RedactUserId(ClaimsPrincipal? principal)
    {
        var oid = principal?.FindFirst("oid")?.Value
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(oid))
            return "[unknown]";

        return oid.Length > 4 ? $"***{oid[^4..]}" : "***";
    }
}

/// <summary>
/// Authorization requirement specifying the minimum PIM tier needed.
/// </summary>
public class PimTierRequirement : IAuthorizationRequirement
{
    public PimTier RequiredTier { get; }

    public PimTierRequirement(PimTier requiredTier)
    {
        RequiredTier = requiredTier;
    }
}

/// <summary>
/// Authorization policy names for PIM tiers.
/// </summary>
public static class PimPolicies
{
    public const string ReadRequired = "PimReadRequired";
    public const string WriteRequired = "PimWriteRequired";
}
