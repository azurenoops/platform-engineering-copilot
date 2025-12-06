using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Jit;
using Platform.Engineering.Copilot.Core.Models.Jit;

namespace Platform.Engineering.Copilot.Core.Services.Jit;

/// <summary>
/// No-op implementation of IAzurePimService when PIM is disabled.
/// Returns appropriate error responses indicating the service is not enabled.
/// </summary>
public class DisabledAzurePimService : IAzurePimService
{
    private readonly ILogger<DisabledAzurePimService> _logger;
    private const string DisabledMessage = "Azure PIM service is not enabled. Enable it in configuration at AzureAd:AzurePim:Enabled.";

    public DisabledAzurePimService(ILogger<DisabledAzurePimService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<EligiblePimRole>> GetEligibleRolesAsync(
        string userId,
        string? scope = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetEligibleRolesAsync called but Azure PIM is disabled");
        return Task.FromResult(new List<EligiblePimRole>());
    }

    /// <inheritdoc />
    public Task<List<ActivePimRole>> GetActiveRolesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetActiveRolesAsync called but Azure PIM is disabled");
        return Task.FromResult(new List<ActivePimRole>());
    }

    /// <inheritdoc />
    public Task<bool> IsRoleActiveAsync(
        string userId,
        string roleDefinitionId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("IsRoleActiveAsync called but Azure PIM is disabled");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<(string RoleName, string? Description)> GetRoleDefinitionAsync(
        string roleDefinitionId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetRoleDefinitionAsync called but Azure PIM is disabled");
        return Task.FromResult(("Unknown (PIM Disabled)", (string?)null));
    }

    /// <inheritdoc />
    public Task<PimActivationResult> ActivatePimRoleAsync(
        PimActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ActivatePimRoleAsync called but Azure PIM is disabled");
        return Task.FromResult(new PimActivationResult
        {
            Status = PimRequestStatus.Failed,
            ErrorMessage = DisabledMessage
        });
    }

    /// <inheritdoc />
    public Task<PimActivationStatus> GetActivationStatusAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetActivationStatusAsync called but Azure PIM is disabled");
        return Task.FromResult(new PimActivationStatus
        {
            RequestId = requestId,
            Status = PimRequestStatus.Failed
        });
    }

    /// <inheritdoc />
    public Task<bool> DeactivatePimRoleAsync(
        string userId,
        string roleDefinitionId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("DeactivatePimRoleAsync called but Azure PIM is disabled");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<PimActivationResult> ExtendPimRoleAsync(
        string userId,
        string roleDefinitionId,
        string scope,
        TimeSpan additionalDuration,
        string justification,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ExtendPimRoleAsync called but Azure PIM is disabled");
        return Task.FromResult(new PimActivationResult
        {
            Status = PimRequestStatus.Failed,
            ErrorMessage = DisabledMessage
        });
    }

    /// <inheritdoc />
    public Task<JitVmAccessResult> RequestJitVmAccessAsync(
        JitVmAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("RequestJitVmAccessAsync called but Azure PIM is disabled");
        return Task.FromResult(new JitVmAccessResult
        {
            Status = JitVmAccessStatus.Failed,
            ErrorMessage = DisabledMessage
        });
    }

    /// <inheritdoc />
    public Task<List<JitVmAccessPolicy>> GetJitPoliciesForVmAsync(
        string vmResourceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetJitPoliciesForVmAsync called but Azure PIM is disabled");
        return Task.FromResult(new List<JitVmAccessPolicy>());
    }

    /// <inheritdoc />
    public Task<bool> IsJitEnabledForVmAsync(
        string vmResourceId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("IsJitEnabledForVmAsync called but Azure PIM is disabled");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<List<PendingPimApproval>> GetPendingApprovalsAsync(
        string approverId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetPendingApprovalsAsync called but Azure PIM is disabled");
        return Task.FromResult(new List<PendingPimApproval>());
    }

    /// <inheritdoc />
    public Task<bool> ApprovePimRequestAsync(
        string requestId,
        string approverId,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("ApprovePimRequestAsync called but Azure PIM is disabled");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<bool> DenyPimRequestAsync(
        string requestId,
        string approverId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("DenyPimRequestAsync called but Azure PIM is disabled");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public Task<List<PimActivationStatus>> GetActivationHistoryAsync(
        string userId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("GetActivationHistoryAsync called but Azure PIM is disabled");
        return Task.FromResult(new List<PimActivationStatus>());
    }
}
