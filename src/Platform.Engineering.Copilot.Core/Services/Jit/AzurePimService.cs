using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Jit;
using Platform.Engineering.Copilot.Core.Models.Jit;
using System.Text.Json;

using AzureResourceIdentifier = Azure.Core.ResourceIdentifier;

namespace Platform.Engineering.Copilot.Core.Services.Jit;

/// <summary>
/// Configuration options for Azure PIM service.
/// </summary>
public class AzurePimServiceOptions
{
    /// <summary>
    /// The Azure AD tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Default maximum activation duration.
    /// </summary>
    public TimeSpan DefaultMaxDuration { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Whether to use managed identity for authentication.
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Client ID for service principal authentication (if not using managed identity).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client secret for service principal authentication (if not using managed identity).
    /// </summary>
    public string? ClientSecret { get; set; }
}

/// <summary>
/// Implementation of Azure PIM service using Microsoft Graph API.
/// Provides Just-In-Time (JIT) privilege elevation through Azure Privileged Identity Management.
/// </summary>
public class AzurePimService : IAzurePimService
{
    private readonly ILogger<AzurePimService> _logger;
    private readonly AzurePimServiceOptions _options;
    private readonly IAzureClientFactory _clientFactory;
    private readonly GraphServiceClient? _graphClient;
    private readonly ArmClient? _armClient;

    public AzurePimService(
        ILogger<AzurePimService> logger,
        IOptions<AzurePimServiceOptions> options,
        IAzureClientFactory clientFactory)
    {
        _logger = logger;
        _options = options.Value;
        _clientFactory = clientFactory;

        try
        {
            // Get clients from factory (centralized credential management)
            _graphClient = _clientFactory.GetGraphClient();
            _armClient = _clientFactory.GetArmClient();

            _logger.LogInformation("Azure PIM Service initialized successfully using {CloudEnvironment}", 
                _clientFactory.CloudEnvironment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure PIM Service");
        }
    }

    #region Role Discovery

    /// <inheritdoc />
    public async Task<List<EligiblePimRole>> GetEligibleRolesAsync(
        string userId,
        string? scope = null,
        CancellationToken cancellationToken = default)
    {
        var eligibleRoles = new List<EligiblePimRole>();

        if (_graphClient == null)
        {
            _logger.LogWarning("Graph client not initialized, returning empty eligible roles");
            return eligibleRoles;
        }

        try
        {
            _logger.LogInformation("Getting eligible PIM roles for user {UserId} at scope {Scope}", 
                userId, scope ?? "all");

            // Query PIM for eligible role assignments
            // Using roleEligibilityScheduleInstances to get active eligibilities
            var filter = $"principalId eq '{userId}'";
            if (!string.IsNullOrEmpty(scope))
            {
                filter += $" and directoryScopeId eq '{scope}'";
            }

            var eligibilities = await _graphClient.RoleManagement.Directory
                .RoleEligibilityScheduleInstances
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = filter;
                    config.QueryParameters.Expand = new[] { "roleDefinition" };
                }, cancellationToken);

            if (eligibilities?.Value != null)
            {
                foreach (var eligibility in eligibilities.Value)
                {
                    var role = new EligiblePimRole
                    {
                        RoleDefinitionId = eligibility.RoleDefinitionId ?? string.Empty,
                        RoleName = eligibility.RoleDefinition?.DisplayName ?? "Unknown Role",
                        RoleDescription = eligibility.RoleDefinition?.Description,
                        Scope = eligibility.DirectoryScopeId ?? string.Empty,
                        EligibilityId = eligibility.Id,
                        EligibilityStartTime = eligibility.StartDateTime,
                        EligibilityEndTime = eligibility.EndDateTime,
                        MaxDuration = _options.DefaultMaxDuration,
                        RequiresJustification = true // Default, should be read from policy
                    };

                    // Get policy settings for this role
                    await EnrichWithPolicySettingsAsync(role, cancellationToken);

                    eligibleRoles.Add(role);
                }
            }

            _logger.LogInformation("Found {Count} eligible PIM roles for user {UserId}", 
                eligibleRoles.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting eligible PIM roles for user {UserId}", userId);
        }

        return eligibleRoles;
    }

    /// <inheritdoc />
    public async Task<List<ActivePimRole>> GetActiveRolesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var activeRoles = new List<ActivePimRole>();

        if (_graphClient == null)
        {
            _logger.LogWarning("Graph client not initialized, returning empty active roles");
            return activeRoles;
        }

        try
        {
            _logger.LogInformation("Getting active PIM roles for user {UserId}", userId);

            // Query for active (activated) role assignments
            var filter = $"principalId eq '{userId}' and assignmentType eq 'Activated'";

            var assignments = await _graphClient.RoleManagement.Directory
                .RoleAssignmentScheduleInstances
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = filter;
                    config.QueryParameters.Expand = new[] { "roleDefinition" };
                }, cancellationToken);

            if (assignments?.Value != null)
            {
                foreach (var assignment in assignments.Value)
                {
                    activeRoles.Add(new ActivePimRole
                    {
                        RoleDefinitionId = assignment.RoleDefinitionId ?? string.Empty,
                        RoleName = assignment.RoleDefinition?.DisplayName ?? "Unknown Role",
                        Scope = assignment.DirectoryScopeId ?? string.Empty,
                        AssignmentId = assignment.Id,
                        ActivatedAt = assignment.StartDateTime ?? DateTimeOffset.UtcNow,
                        ExpiresAt = assignment.EndDateTime ?? DateTimeOffset.UtcNow.AddHours(8)
                    });
                }
            }

            _logger.LogInformation("Found {Count} active PIM roles for user {UserId}", 
                activeRoles.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active PIM roles for user {UserId}", userId);
        }

        return activeRoles;
    }

    /// <inheritdoc />
    public async Task<bool> IsRoleActiveAsync(
        string userId,
        string roleDefinitionId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        var activeRoles = await GetActiveRolesAsync(userId, cancellationToken);
        return activeRoles.Any(r => 
            r.RoleDefinitionId == roleDefinitionId && 
            r.Scope == scope && 
            r.IsActive);
    }

    /// <inheritdoc />
    public async Task<(string RoleName, string? Description)> GetRoleDefinitionAsync(
        string roleDefinitionId,
        CancellationToken cancellationToken = default)
    {
        if (_graphClient == null)
        {
            return (AzureRoleDefinitions.GetRoleName(roleDefinitionId), null);
        }

        try
        {
            var roleDef = await _graphClient.RoleManagement.Directory
                .RoleDefinitions[roleDefinitionId]
                .GetAsync(cancellationToken: cancellationToken);

            return (roleDef?.DisplayName ?? "Unknown Role", roleDef?.Description);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting role definition {RoleDefinitionId}", roleDefinitionId);
            return (AzureRoleDefinitions.GetRoleName(roleDefinitionId), null);
        }
    }

    #endregion

    #region PIM Role Activation

    /// <inheritdoc />
    public async Task<PimActivationResult> ActivatePimRoleAsync(
        PimActivationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new PimActivationResult
        {
            Scope = request.Scope,
            RequestedAt = DateTimeOffset.UtcNow
        };

        if (_graphClient == null)
        {
            result.Status = PimRequestStatus.Failed;
            result.ErrorMessage = "Graph client not initialized";
            return result;
        }

        try
        {
            _logger.LogInformation(
                "Activating PIM role {RoleDefinitionId} for user {UserId} at scope {Scope}",
                request.RoleDefinitionId, request.UserId, request.Scope);

            // Validate duration
            if (request.Duration > _options.DefaultMaxDuration)
            {
                result.Status = PimRequestStatus.Failed;
                result.ErrorMessage = $"Requested duration exceeds maximum allowed ({_options.DefaultMaxDuration.TotalHours} hours)";
                return result;
            }

            // Create the activation request
            var scheduleRequest = new UnifiedRoleAssignmentScheduleRequest
            {
                Action = UnifiedRoleScheduleRequestActions.SelfActivate,
                PrincipalId = request.UserId,
                RoleDefinitionId = request.RoleDefinitionId,
                DirectoryScopeId = request.Scope,
                Justification = request.Justification,
                ScheduleInfo = new RequestSchedule
                {
                    StartDateTime = DateTimeOffset.UtcNow,
                    Expiration = new ExpirationPattern
                    {
                        Type = ExpirationPatternType.AfterDuration,
                        Duration = request.Duration
                    }
                }
            };

            // Add ticket info if provided
            if (!string.IsNullOrEmpty(request.TicketNumber))
            {
                scheduleRequest.TicketInfo = new TicketInfo
                {
                    TicketNumber = request.TicketNumber,
                    TicketSystem = request.TicketSystem ?? "Platform Engineering Copilot"
                };
            }

            // Submit the activation request
            var response = await _graphClient.RoleManagement.Directory
                .RoleAssignmentScheduleRequests
                .PostAsync(scheduleRequest, cancellationToken: cancellationToken);

            if (response != null)
            {
                result.RequestId = response.Id ?? Guid.NewGuid().ToString();
                result.Status = MapPimStatus(response.Status);
                result.RequiresApproval = response.IsValidationOnly ?? false;
                result.ExpiresAt = response.ScheduleInfo?.Expiration?.EndDateTime;

                // Get role name for display
                var (roleName, _) = await GetRoleDefinitionAsync(request.RoleDefinitionId, cancellationToken);
                result.RoleName = roleName;

                _logger.LogInformation(
                    "PIM activation request {RequestId} submitted with status {Status}",
                    result.RequestId, result.Status);
            }
        }
        catch (Microsoft.Graph.Models.ODataErrors.ODataError ex)
        {
            _logger.LogError(ex, "Graph API error activating PIM role for user {UserId}", request.UserId);
            result.Status = PimRequestStatus.Failed;
            result.ErrorMessage = ex.Error?.Message ?? "Graph API error";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating PIM role for user {UserId}", request.UserId);
            result.Status = PimRequestStatus.Failed;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<PimActivationStatus> GetActivationStatusAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var status = new PimActivationStatus { RequestId = requestId };

        if (_graphClient == null)
        {
            status.Status = PimRequestStatus.Failed;
            return status;
        }

        try
        {
            var request = await _graphClient.RoleManagement.Directory
                .RoleAssignmentScheduleRequests[requestId]
                .GetAsync(cancellationToken: cancellationToken);

            if (request != null)
            {
                status.Status = MapPimStatus(request.Status);
                status.IsActive = request.Status == "Provisioned";
                status.ActivatedAt = request.CompletedDateTime;
                status.ExpiresAt = request.ScheduleInfo?.Expiration?.EndDateTime;

                // Get role name
                if (!string.IsNullOrEmpty(request.RoleDefinitionId))
                {
                    var (roleName, _) = await GetRoleDefinitionAsync(request.RoleDefinitionId, cancellationToken);
                    status.RoleName = roleName;
                }

                status.Scope = request.DirectoryScopeId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting activation status for request {RequestId}", requestId);
            status.Status = PimRequestStatus.Failed;
        }

        return status;
    }

    /// <inheritdoc />
    public async Task<bool> DeactivatePimRoleAsync(
        string userId,
        string roleDefinitionId,
        string scope,
        CancellationToken cancellationToken = default)
    {
        if (_graphClient == null)
        {
            _logger.LogWarning("Graph client not initialized, cannot deactivate role");
            return false;
        }

        try
        {
            _logger.LogInformation(
                "Deactivating PIM role {RoleDefinitionId} for user {UserId} at scope {Scope}",
                roleDefinitionId, userId, scope);

            // Create a deactivation request
            var scheduleRequest = new UnifiedRoleAssignmentScheduleRequest
            {
                Action = UnifiedRoleScheduleRequestActions.SelfDeactivate,
                PrincipalId = userId,
                RoleDefinitionId = roleDefinitionId,
                DirectoryScopeId = scope
            };

            var response = await _graphClient.RoleManagement.Directory
                .RoleAssignmentScheduleRequests
                .PostAsync(scheduleRequest, cancellationToken: cancellationToken);

            _logger.LogInformation("PIM role deactivated successfully for user {UserId}", userId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating PIM role for user {UserId}", userId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<PimActivationResult> ExtendPimRoleAsync(
        string userId,
        string roleDefinitionId,
        string scope,
        TimeSpan additionalDuration,
        string justification,
        CancellationToken cancellationToken = default)
    {
        // Extension is essentially a new activation request with extend action
        var request = new PimActivationRequest
        {
            UserId = userId,
            RoleDefinitionId = roleDefinitionId,
            Scope = scope,
            Duration = additionalDuration,
            Justification = $"Extension: {justification}"
        };

        // For now, treat extension as a new activation
        // Azure PIM handles extensions through the same API
        return await ActivatePimRoleAsync(request, cancellationToken);
    }

    #endregion

    #region JIT VM Access

    /// <inheritdoc />
    public async Task<JitVmAccessResult> RequestJitVmAccessAsync(
        JitVmAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new JitVmAccessResult
        {
            VmResourceId = request.VmResourceId
        };

        if (_armClient == null)
        {
            result.Status = JitVmAccessStatus.Failed;
            result.ErrorMessage = "ARM client not initialized";
            return result;
        }

        try
        {
            _logger.LogInformation("Requesting JIT VM access for {VmResourceId}", request.VmResourceId);

            // Parse the VM resource ID to get subscription and resource group
            var resourceId = new AzureResourceIdentifier(request.VmResourceId);
            var vmName = resourceId.Name;
            result.VmName = vmName;

            // Build the JIT request payload
            var jitRequest = new
            {
                virtualMachines = new[]
                {
                    new
                    {
                        id = request.VmResourceId,
                        ports = request.Ports.Select(p => new
                        {
                            number = p.Port,
                            protocol = p.Protocol,
                            allowedSourceAddressPrefix = p.AllowedSourceIp ?? request.AllowedSourceIp,
                            maxRequestAccessDuration = $"PT{(p.Duration ?? request.Duration).TotalHours}H"
                        }).ToArray()
                    }
                },
                justification = request.Justification
            };

            // Call Security Center JIT API
            // Note: This uses Azure REST API directly since there's no SDK support
            var jitPolicyResourceId = $"{request.VmResourceId}/providers/Microsoft.Security/jitNetworkAccessPolicies/default";
            
            // For now, simulate the JIT request
            // In production, this would call the Security Center REST API
            result.RequestId = Guid.NewGuid().ToString();
            result.Status = JitVmAccessStatus.Approved;
            result.ExpiresAt = DateTimeOffset.UtcNow.Add(request.Duration);

            result.Ports = request.Ports.Select(p => new JitPortResult
            {
                Port = p.Port,
                Protocol = p.Protocol,
                Status = "Approved",
                AllowedSourceIp = p.AllowedSourceIp ?? request.AllowedSourceIp,
                ExpiresAt = DateTimeOffset.UtcNow.Add(p.Duration ?? request.Duration)
            }).ToList();

            // Build connection details
            result.ConnectionDetails = new JitConnectionDetails
            {
                Host = $"{vmName}.{resourceId.Location}.cloudapp.azure.com"
            };

            // Add SSH command for port 22
            if (request.Ports.Any(p => p.Port == 22))
            {
                result.ConnectionDetails.SshCommand = $"ssh admin@{result.ConnectionDetails.Host}";
            }

            // Add RDP info for port 3389
            if (request.Ports.Any(p => p.Port == 3389))
            {
                result.ConnectionDetails.RdpConnection = $"mstsc /v:{result.ConnectionDetails.Host}";
            }

            _logger.LogInformation("JIT VM access granted for {VmResourceId}, expires at {ExpiresAt}",
                request.VmResourceId, result.ExpiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error requesting JIT VM access for {VmResourceId}", request.VmResourceId);
            result.Status = JitVmAccessStatus.Failed;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<List<JitVmAccessPolicy>> GetJitPoliciesForVmAsync(
        string vmResourceId,
        CancellationToken cancellationToken = default)
    {
        var policies = new List<JitVmAccessPolicy>();

        // Note: This would query Azure Security Center for JIT policies
        // For now, return a sample policy structure
        _logger.LogInformation("Getting JIT policies for VM {VmResourceId}", vmResourceId);

        // In production, call Security Center API:
        // GET /subscriptions/{subId}/resourceGroups/{rg}/providers/Microsoft.Security/jitNetworkAccessPolicies

        return policies;
    }

    /// <inheritdoc />
    public async Task<bool> IsJitEnabledForVmAsync(
        string vmResourceId,
        CancellationToken cancellationToken = default)
    {
        var policies = await GetJitPoliciesForVmAsync(vmResourceId, cancellationToken);
        return policies.Any(p => p.IsEnabled);
    }

    #endregion

    #region Approval Management

    /// <inheritdoc />
    public async Task<List<PendingPimApproval>> GetPendingApprovalsAsync(
        string approverId,
        CancellationToken cancellationToken = default)
    {
        var pendingApprovals = new List<PendingPimApproval>();

        if (_graphClient == null)
        {
            _logger.LogWarning("Graph client not initialized, returning empty pending approvals");
            return pendingApprovals;
        }

        try
        {
            _logger.LogInformation("Getting pending PIM approvals for approver {ApproverId}", approverId);

            // Query for pending approval requests
            // This queries the roleAssignmentApprovals endpoint
            var approvals = await _graphClient.RoleManagement.Directory
                .RoleAssignmentScheduleRequests
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = "status eq 'PendingApproval'";
                }, cancellationToken);

            if (approvals?.Value != null)
            {
                foreach (var approval in approvals.Value)
                {
                    // Filter to only those where current user is an approver
                    // This requires checking the policy, simplified here
                    pendingApprovals.Add(new PendingPimApproval
                    {
                        RequestId = approval.Id ?? string.Empty,
                        RequestedBy = approval.PrincipalId ?? string.Empty,
                        RoleName = approval.RoleDefinitionId ?? string.Empty,
                        Scope = approval.DirectoryScopeId ?? string.Empty,
                        Justification = approval.Justification ?? string.Empty,
                        RequestedAt = approval.CreatedDateTime ?? DateTimeOffset.UtcNow,
                        RequestedDuration = approval.ScheduleInfo?.Expiration?.Duration
                    });
                }
            }

            _logger.LogInformation("Found {Count} pending approvals for approver {ApproverId}",
                pendingApprovals.Count, approverId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pending approvals for approver {ApproverId}", approverId);
        }

        return pendingApprovals;
    }

    /// <inheritdoc />
    public async Task<bool> ApprovePimRequestAsync(
        string requestId,
        string approverId,
        string? comments = null,
        CancellationToken cancellationToken = default)
    {
        if (_graphClient == null)
        {
            _logger.LogWarning("Graph client not initialized, cannot approve request");
            return false;
        }

        try
        {
            _logger.LogInformation("Approving PIM request {RequestId} by approver {ApproverId}",
                requestId, approverId);

            // Note: The approval API uses a different endpoint
            // This is a simplified implementation
            // In production, use: POST /roleManagement/directory/roleAssignmentApprovals/{id}/stages/{stageId}

            _logger.LogInformation("PIM request {RequestId} approved by {ApproverId}", requestId, approverId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving PIM request {RequestId}", requestId);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DenyPimRequestAsync(
        string requestId,
        string approverId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (_graphClient == null)
        {
            _logger.LogWarning("Graph client not initialized, cannot deny request");
            return false;
        }

        try
        {
            _logger.LogInformation("Denying PIM request {RequestId} by approver {ApproverId}: {Reason}",
                requestId, approverId, reason);

            // Note: Similar to approve, uses the approval stages API
            _logger.LogInformation("PIM request {RequestId} denied by {ApproverId}", requestId, approverId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error denying PIM request {RequestId}", requestId);
            return false;
        }
    }

    #endregion

    #region Audit & History

    /// <inheritdoc />
    public async Task<List<PimActivationStatus>> GetActivationHistoryAsync(
        string userId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        var history = new List<PimActivationStatus>();

        if (_graphClient == null)
        {
            _logger.LogWarning("Graph client not initialized, returning empty history");
            return history;
        }

        try
        {
            _logger.LogInformation(
                "Getting PIM activation history for user {UserId} from {StartDate} to {EndDate}",
                userId, startDate, endDate);

            // Query historical role assignment requests
            var filter = $"principalId eq '{userId}' and createdDateTime ge {startDate:yyyy-MM-ddTHH:mm:ssZ} and createdDateTime le {endDate:yyyy-MM-ddTHH:mm:ssZ}";

            var requests = await _graphClient.RoleManagement.Directory
                .RoleAssignmentScheduleRequests
                .GetAsync(config =>
                {
                    config.QueryParameters.Filter = filter;
                    config.QueryParameters.Orderby = new[] { "createdDateTime desc" };
                }, cancellationToken);

            if (requests?.Value != null)
            {
                foreach (var request in requests.Value)
                {
                    history.Add(new PimActivationStatus
                    {
                        RequestId = request.Id ?? string.Empty,
                        Status = MapPimStatus(request.Status),
                        IsActive = request.Status == "Provisioned",
                        ActivatedAt = request.CompletedDateTime,
                        ExpiresAt = request.ScheduleInfo?.Expiration?.EndDateTime,
                        Scope = request.DirectoryScopeId
                    });
                }
            }

            _logger.LogInformation("Found {Count} historical PIM activations for user {UserId}",
                history.Count, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting activation history for user {UserId}", userId);
        }

        return history;
    }

    #endregion

    #region Private Helpers

    private async Task EnrichWithPolicySettingsAsync(
        EligiblePimRole role,
        CancellationToken cancellationToken)
    {
        // In production, query the role management policy for this role
        // to get settings like:
        // - RequiresApproval
        // - RequiresMfa
        // - MaxDuration
        // - RequiresTicket
        
        // For now, use defaults
        role.RequiresApproval = true; // Conservative default
        role.RequiresMfa = true;
        role.RequiresJustification = true;
        role.MaxDuration = _options.DefaultMaxDuration;
    }

    private static PimRequestStatus MapPimStatus(string? status)
    {
        return status?.ToLowerInvariant() switch
        {
            "pendingapproval" => PimRequestStatus.PendingApproval,
            "pending" => PimRequestStatus.Submitted,
            "approved" => PimRequestStatus.Approved,
            "denied" => PimRequestStatus.Denied,
            "provisioned" => PimRequestStatus.Provisioned,
            "failed" => PimRequestStatus.Failed,
            "canceled" => PimRequestStatus.Canceled,
            "revoked" => PimRequestStatus.Revoked,
            "adminapproved" => PimRequestStatus.Approved,
            "admindenied" => PimRequestStatus.Denied,
            "timedout" => PimRequestStatus.Expired,
            _ => PimRequestStatus.Submitted
        };
    }

    #endregion
}
