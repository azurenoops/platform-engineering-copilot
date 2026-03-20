using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Auth;

/// <summary>
/// Generates descriptive denial messages per FR-020 when a user's role
/// is insufficient for an operation. Returns the required role,
/// required PIM tier, and the user's current roles.
/// </summary>
public static class AuthDenialMessageService
{
    /// <summary>
    /// Roles permitted to perform remediation operations.
    /// </summary>
    public static readonly UserRole[] RemediationAllowedRoles =
    [
        UserRole.PlatformEngineer
    ];

    /// <summary>
    /// Build a descriptive denial message for a role/PIM failure.
    /// </summary>
    /// <param name="operation">The operation being denied (e.g., "remediation").</param>
    /// <param name="requiredRoles">Roles that would permit the operation.</param>
    /// <param name="requiredPimTier">Minimum PIM tier required.</param>
    /// <param name="currentRoles">User's current roles.</param>
    /// <returns>Structured denial info.</returns>
    public static AuthDenialInfo BuildDenialMessage(
        string operation,
        UserRole[] requiredRoles,
        PimTier requiredPimTier,
        UserRole[] currentRoles)
    {
        var requiredRoleNames = string.Join(" or ", requiredRoles.Select(r => r.ToString()));
        var currentRoleNames = currentRoles.Length > 0
            ? string.Join(", ", currentRoles.Select(r => r.ToString()))
            : "None";

        return new AuthDenialInfo
        {
            Operation = operation,
            Denied = true,
            Message = $"Your current role ({currentRoleNames}) does not permit {operation}. " +
                      $"Required role: {requiredRoleNames}. Required PIM tier: {requiredPimTier}.",
            RequiredRoles = requiredRoles.Select(r => r.ToString()).ToArray(),
            RequiredPimTier = requiredPimTier.ToString(),
            CurrentRoles = currentRoles.Select(r => r.ToString()).ToArray(),
            Suggestion = $"Contact a {requiredRoleNames} to perform this operation, " +
                         $"or request role elevation through your organization's access management."
        };
    }

    /// <summary>
    /// Check whether a set of roles is permitted for remediation.
    /// </summary>
    public static bool IsRemediationAllowed(UserRole[] userRoles) =>
        userRoles.Any(r => RemediationAllowedRoles.Contains(r));
}

/// <summary>
/// Structured denial information per FR-020.
/// </summary>
public class AuthDenialInfo
{
    public string Operation { get; init; } = string.Empty;
    public bool Denied { get; init; }
    public string Message { get; init; } = string.Empty;
    public string[] RequiredRoles { get; init; } = [];
    public string RequiredPimTier { get; init; } = string.Empty;
    public string[] CurrentRoles { get; init; } = [];
    public string Suggestion { get; init; } = string.Empty;
}
