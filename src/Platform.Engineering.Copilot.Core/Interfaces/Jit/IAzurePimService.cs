using Platform.Engineering.Copilot.Core.Models.Jit;

namespace Platform.Engineering.Copilot.Core.Interfaces.Jit;

/// <summary>
/// Service interface for Azure Privileged Identity Management (PIM) operations.
/// Provides Just-In-Time (JIT) privilege elevation through Azure native services.
/// </summary>
public interface IAzurePimService
{
    #region Role Discovery

    /// <summary>
    /// Gets all roles the user is eligible to activate via PIM.
    /// </summary>
    /// <param name="userId">The Azure AD object ID of the user.</param>
    /// <param name="scope">Optional scope to filter eligible roles (subscription, resource group, or resource).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of roles the user can activate.</returns>
    Task<List<EligiblePimRole>> GetEligibleRolesAsync(
        string userId,
        string? scope = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all currently active PIM role assignments for a user.
    /// </summary>
    /// <param name="userId">The Azure AD object ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of currently active role assignments.</returns>
    Task<List<ActivePimRole>> GetActiveRolesAsync(
        string userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has a specific role active at a given scope.
    /// </summary>
    /// <param name="userId">The Azure AD object ID of the user.</param>
    /// <param name="roleDefinitionId">The Azure role definition ID.</param>
    /// <param name="scope">The scope to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the role is currently active.</returns>
    Task<bool> IsRoleActiveAsync(
        string userId,
        string roleDefinitionId,
        string scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets role definition details by ID.
    /// </summary>
    /// <param name="roleDefinitionId">The Azure role definition ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Role name and description.</returns>
    Task<(string RoleName, string? Description)> GetRoleDefinitionAsync(
        string roleDefinitionId,
        CancellationToken cancellationToken = default);

    #endregion

    #region PIM Role Activation

    /// <summary>
    /// Requests activation of a PIM-eligible role.
    /// This submits a request to Azure PIM which may require approval.
    /// </summary>
    /// <param name="request">The activation request details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the activation request.</returns>
    Task<PimActivationResult> ActivatePimRoleAsync(
        PimActivationRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a PIM activation request.
    /// </summary>
    /// <param name="requestId">The PIM request ID returned from ActivatePimRoleAsync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current status of the activation.</returns>
    Task<PimActivationStatus> GetActivationStatusAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates (revokes) an active PIM role assignment early.
    /// </summary>
    /// <param name="userId">The Azure AD object ID of the user.</param>
    /// <param name="roleDefinitionId">The role to deactivate.</param>
    /// <param name="scope">The scope of the assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully deactivated.</returns>
    Task<bool> DeactivatePimRoleAsync(
        string userId,
        string roleDefinitionId,
        string scope,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extends the duration of an active PIM role assignment.
    /// </summary>
    /// <param name="userId">The Azure AD object ID of the user.</param>
    /// <param name="roleDefinitionId">The role to extend.</param>
    /// <param name="scope">The scope of the assignment.</param>
    /// <param name="additionalDuration">How much longer to extend the assignment.</param>
    /// <param name="justification">Justification for the extension.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the extension request.</returns>
    Task<PimActivationResult> ExtendPimRoleAsync(
        string userId,
        string roleDefinitionId,
        string scope,
        TimeSpan additionalDuration,
        string justification,
        CancellationToken cancellationToken = default);

    #endregion

    #region JIT VM Access

    /// <summary>
    /// Requests Just-In-Time VM access through Azure Security Center.
    /// </summary>
    /// <param name="request">The JIT access request details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result of the JIT access request.</returns>
    Task<JitVmAccessResult> RequestJitVmAccessAsync(
        JitVmAccessRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets JIT access policies configured for a VM.
    /// </summary>
    /// <param name="vmResourceId">The full Azure resource ID of the VM.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of JIT policies for the VM.</returns>
    Task<List<JitVmAccessPolicy>> GetJitPoliciesForVmAsync(
        string vmResourceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a VM has JIT access enabled.
    /// </summary>
    /// <param name="vmResourceId">The full Azure resource ID of the VM.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if JIT is enabled for the VM.</returns>
    Task<bool> IsJitEnabledForVmAsync(
        string vmResourceId,
        CancellationToken cancellationToken = default);

    #endregion

    #region Approval Management

    /// <summary>
    /// Gets pending PIM approvals where the user is a designated approver.
    /// </summary>
    /// <param name="approverId">The Azure AD object ID of the approver.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of pending approval requests.</returns>
    Task<List<PendingPimApproval>> GetPendingApprovalsAsync(
        string approverId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a pending PIM activation request.
    /// </summary>
    /// <param name="requestId">The approval request ID.</param>
    /// <param name="approverId">The Azure AD object ID of the approver.</param>
    /// <param name="comments">Optional comments for the approval.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully approved.</returns>
    Task<bool> ApprovePimRequestAsync(
        string requestId,
        string approverId,
        string? comments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Denies a pending PIM activation request.
    /// </summary>
    /// <param name="requestId">The approval request ID.</param>
    /// <param name="approverId">The Azure AD object ID of the approver.</param>
    /// <param name="reason">Reason for the denial.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if successfully denied.</returns>
    Task<bool> DenyPimRequestAsync(
        string requestId,
        string approverId,
        string reason,
        CancellationToken cancellationToken = default);

    #endregion

    #region Audit & History

    /// <summary>
    /// Gets the PIM activation history for a user.
    /// </summary>
    /// <param name="userId">The Azure AD object ID of the user.</param>
    /// <param name="startDate">Start of the date range.</param>
    /// <param name="endDate">End of the date range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of historical PIM activations.</returns>
    Task<List<PimActivationStatus>> GetActivationHistoryAsync(
        string userId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default);

    #endregion
}
