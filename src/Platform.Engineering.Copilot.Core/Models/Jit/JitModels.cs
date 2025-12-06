namespace Platform.Engineering.Copilot.Core.Models.Jit;

/// <summary>
/// Request to activate a PIM (Privileged Identity Management) role.
/// </summary>
public class PimActivationRequest
{
    /// <summary>
    /// The Azure AD object ID of the user requesting activation.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// The Azure role definition ID to activate.
    /// Common roles:
    /// - Owner: 8e3af657-a8ff-443c-a75c-2fe8c4bcb635
    /// - Contributor: b24988ac-6180-42a0-ab88-20f7382dd24c
    /// - Reader: acdd72a7-3385-48ef-bd42-f606fba81ae7
    /// </summary>
    public string RoleDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// The scope for the role assignment.
    /// Examples:
    /// - Subscription: /subscriptions/{subscription-id}
    /// - Resource Group: /subscriptions/{subscription-id}/resourceGroups/{rg-name}
    /// - Resource: /subscriptions/{subscription-id}/resourceGroups/{rg-name}/providers/{provider}/{resource}
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Business justification for the activation (required by PIM).
    /// </summary>
    public string Justification { get; set; } = string.Empty;

    /// <summary>
    /// How long the role should be active. Maximum is typically 8 hours.
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Optional ticket number for tracking (e.g., incident or change request).
    /// </summary>
    public string? TicketNumber { get; set; }

    /// <summary>
    /// Optional ticket system identifier (e.g., "ServiceNow", "Jira").
    /// </summary>
    public string? TicketSystem { get; set; }

    /// <summary>
    /// The conversation ID from the Copilot session for audit correlation.
    /// </summary>
    public string? ConversationId { get; set; }
}

/// <summary>
/// Result of a PIM role activation request.
/// </summary>
public class PimActivationResult
{
    /// <summary>
    /// The Azure PIM request ID for tracking.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the activation request.
    /// </summary>
    public PimRequestStatus Status { get; set; }

    /// <summary>
    /// Whether the activation requires approval from designated approvers.
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Approval steps if approval is required.
    /// </summary>
    public List<PimApprovalStep> ApprovalSteps { get; set; } = new();

    /// <summary>
    /// When the role will expire (if activated).
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// When the activation was requested.
    /// </summary>
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// The role that was requested.
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    /// The scope that was requested.
    /// </summary>
    public string? Scope { get; set; }
}

/// <summary>
/// Approval step in a PIM activation workflow.
/// </summary>
public class PimApprovalStep
{
    /// <summary>
    /// Order of this step in the approval chain.
    /// </summary>
    public int StepNumber { get; set; }

    /// <summary>
    /// Status of this approval step.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Approvers who can approve this step.
    /// </summary>
    public List<PimApprover> Approvers { get; set; } = new();

    /// <summary>
    /// Who approved this step (if approved).
    /// </summary>
    public string? ApprovedBy { get; set; }

    /// <summary>
    /// When this step was approved.
    /// </summary>
    public DateTimeOffset? ApprovedAt { get; set; }

    /// <summary>
    /// Comments from the approver.
    /// </summary>
    public string? ApproverComments { get; set; }
}

/// <summary>
/// A designated approver in PIM.
/// </summary>
public class PimApprover
{
    /// <summary>
    /// Azure AD object ID of the approver.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the approver.
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Email address of the approver.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Type of approver (User, Group, ServicePrincipal).
    /// </summary>
    public string ApproverType { get; set; } = "User";
}

/// <summary>
/// Status of a PIM activation request.
/// </summary>
public class PimActivationStatus
{
    /// <summary>
    /// The Azure PIM request ID.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the request.
    /// </summary>
    public PimRequestStatus Status { get; set; }

    /// <summary>
    /// Whether the role is currently active.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Status of the approval workflow (if applicable).
    /// </summary>
    public string? ApprovalStatus { get; set; }

    /// <summary>
    /// Who approved the request (if approved).
    /// </summary>
    public List<string> ApprovedBy { get; set; } = new();

    /// <summary>
    /// When the role was activated.
    /// </summary>
    public DateTimeOffset? ActivatedAt { get; set; }

    /// <summary>
    /// When the role will expire.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Time remaining until expiration.
    /// </summary>
    public TimeSpan? TimeRemaining => ExpiresAt.HasValue 
        ? ExpiresAt.Value - DateTimeOffset.UtcNow 
        : null;

    /// <summary>
    /// The role that was requested.
    /// </summary>
    public string? RoleName { get; set; }

    /// <summary>
    /// The scope that was requested.
    /// </summary>
    public string? Scope { get; set; }
}

/// <summary>
/// A currently active PIM role assignment.
/// </summary>
public class ActivePimRole
{
    /// <summary>
    /// The Azure role definition ID.
    /// </summary>
    public string RoleDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the role.
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// The scope where the role is active.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name for the scope (e.g., resource group name).
    /// </summary>
    public string? ScopeName { get; set; }

    /// <summary>
    /// When the role was activated.
    /// </summary>
    public DateTimeOffset ActivatedAt { get; set; }

    /// <summary>
    /// When the role will expire.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Time remaining until expiration.
    /// </summary>
    public TimeSpan TimeRemaining => ExpiresAt - DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether the role is still active.
    /// </summary>
    public bool IsActive => ExpiresAt > DateTimeOffset.UtcNow;

    /// <summary>
    /// The assignment instance ID.
    /// </summary>
    public string? AssignmentId { get; set; }
}

/// <summary>
/// An eligible (but not currently active) PIM role.
/// </summary>
public class EligiblePimRole
{
    /// <summary>
    /// The Azure role definition ID.
    /// </summary>
    public string RoleDefinitionId { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the role.
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Description of the role.
    /// </summary>
    public string? RoleDescription { get; set; }

    /// <summary>
    /// The scope where the role can be activated.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name for the scope.
    /// </summary>
    public string? ScopeName { get; set; }

    /// <summary>
    /// Maximum duration the role can be activated for.
    /// </summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// Whether activation requires approval.
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Whether activation requires MFA.
    /// </summary>
    public bool RequiresMfa { get; set; }

    /// <summary>
    /// Whether justification is required for activation.
    /// </summary>
    public bool RequiresJustification { get; set; } = true;

    /// <summary>
    /// Whether a ticket number is required for activation.
    /// </summary>
    public bool RequiresTicket { get; set; }

    /// <summary>
    /// The eligibility assignment ID.
    /// </summary>
    public string? EligibilityId { get; set; }

    /// <summary>
    /// When the eligibility starts.
    /// </summary>
    public DateTimeOffset? EligibilityStartTime { get; set; }

    /// <summary>
    /// When the eligibility ends.
    /// </summary>
    public DateTimeOffset? EligibilityEndTime { get; set; }
}

/// <summary>
/// Request for JIT (Just-In-Time) VM access via Azure Security Center.
/// </summary>
public class JitVmAccessRequest
{
    /// <summary>
    /// The full Azure resource ID of the VM.
    /// </summary>
    public string VmResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Ports to request access to.
    /// </summary>
    public List<JitPortRequest> Ports { get; set; } = new();

    /// <summary>
    /// How long access should be granted.
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.FromHours(3);

    /// <summary>
    /// Business justification for the access.
    /// </summary>
    public string Justification { get; set; } = string.Empty;

    /// <summary>
    /// Source IP address allowed to connect. Use "*" for any.
    /// </summary>
    public string AllowedSourceIp { get; set; } = "*";

    /// <summary>
    /// The conversation ID from the Copilot session for audit correlation.
    /// </summary>
    public string? ConversationId { get; set; }
}

/// <summary>
/// Port configuration for JIT VM access.
/// </summary>
public class JitPortRequest
{
    /// <summary>
    /// The port number (e.g., 22 for SSH, 3389 for RDP).
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Protocol (TCP or UDP).
    /// </summary>
    public string Protocol { get; set; } = "TCP";

    /// <summary>
    /// Duration for this specific port (overrides request duration if set).
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Source IP for this specific port (overrides request IP if set).
    /// </summary>
    public string? AllowedSourceIp { get; set; }
}

/// <summary>
/// Result of a JIT VM access request.
/// </summary>
public class JitVmAccessResult
{
    /// <summary>
    /// The JIT request ID.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Status of the request.
    /// </summary>
    public JitVmAccessStatus Status { get; set; }

    /// <summary>
    /// The VM resource ID.
    /// </summary>
    public string VmResourceId { get; set; } = string.Empty;

    /// <summary>
    /// VM name for display.
    /// </summary>
    public string? VmName { get; set; }

    /// <summary>
    /// Ports that were granted access.
    /// </summary>
    public List<JitPortResult> Ports { get; set; } = new();

    /// <summary>
    /// When the access expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// Connection details for the user.
    /// </summary>
    public JitConnectionDetails? ConnectionDetails { get; set; }

    /// <summary>
    /// Error message if the request failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result for a specific port in JIT access.
/// </summary>
public class JitPortResult
{
    /// <summary>
    /// The port number.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Protocol.
    /// </summary>
    public string Protocol { get; set; } = "TCP";

    /// <summary>
    /// Status of this port's access.
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Allowed source IP for this port.
    /// </summary>
    public string AllowedSourceIp { get; set; } = string.Empty;

    /// <summary>
    /// When this port's access expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>
/// Connection details for JIT VM access.
/// </summary>
public class JitConnectionDetails
{
    /// <summary>
    /// The hostname or IP to connect to.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Example SSH command (for Linux VMs).
    /// </summary>
    public string? SshCommand { get; set; }

    /// <summary>
    /// Example RDP connection string (for Windows VMs).
    /// </summary>
    public string? RdpConnection { get; set; }

    /// <summary>
    /// Public IP address of the VM.
    /// </summary>
    public string? PublicIpAddress { get; set; }

    /// <summary>
    /// Private IP address of the VM.
    /// </summary>
    public string? PrivateIpAddress { get; set; }
}

/// <summary>
/// JIT network access policy for a VM.
/// </summary>
public class JitVmAccessPolicy
{
    /// <summary>
    /// The policy ID.
    /// </summary>
    public string PolicyId { get; set; } = string.Empty;

    /// <summary>
    /// The VM resource ID this policy applies to.
    /// </summary>
    public string VmResourceId { get; set; } = string.Empty;

    /// <summary>
    /// Ports configured in this policy.
    /// </summary>
    public List<JitPolicyPort> Ports { get; set; } = new();

    /// <summary>
    /// Whether the policy is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Port configuration in a JIT policy.
/// </summary>
public class JitPolicyPort
{
    /// <summary>
    /// The port number.
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Protocol.
    /// </summary>
    public string Protocol { get; set; } = "TCP";

    /// <summary>
    /// Maximum duration allowed for this port.
    /// </summary>
    public TimeSpan MaxDuration { get; set; }

    /// <summary>
    /// Allowed source address prefixes.
    /// </summary>
    public List<string> AllowedSourceAddresses { get; set; } = new();
}

/// <summary>
/// A pending PIM approval for the current user.
/// </summary>
public class PendingPimApproval
{
    /// <summary>
    /// The approval request ID.
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Who requested the activation.
    /// </summary>
    public string RequestedBy { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the requester.
    /// </summary>
    public string? RequestedByName { get; set; }

    /// <summary>
    /// The role being requested.
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// The scope being requested.
    /// </summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>
    /// Justification provided by the requester.
    /// </summary>
    public string Justification { get; set; } = string.Empty;

    /// <summary>
    /// When the request was submitted.
    /// </summary>
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>
    /// When the request expires.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Duration requested.
    /// </summary>
    public TimeSpan? RequestedDuration { get; set; }
}

/// <summary>
/// Status values for PIM requests.
/// </summary>
public enum PimRequestStatus
{
    /// <summary>
    /// Request has been submitted.
    /// </summary>
    Submitted,

    /// <summary>
    /// Request is pending approval.
    /// </summary>
    PendingApproval,

    /// <summary>
    /// Request has been approved.
    /// </summary>
    Approved,

    /// <summary>
    /// Request has been denied.
    /// </summary>
    Denied,

    /// <summary>
    /// Role has been provisioned/activated.
    /// </summary>
    Provisioned,

    /// <summary>
    /// Role activation failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Request was canceled.
    /// </summary>
    Canceled,

    /// <summary>
    /// Request has expired.
    /// </summary>
    Expired,

    /// <summary>
    /// Role has been revoked/deactivated.
    /// </summary>
    Revoked
}

/// <summary>
/// Status values for JIT VM access.
/// </summary>
public enum JitVmAccessStatus
{
    /// <summary>
    /// Request is pending.
    /// </summary>
    Pending,

    /// <summary>
    /// Access has been approved and is active.
    /// </summary>
    Approved,

    /// <summary>
    /// Access was denied.
    /// </summary>
    Denied,

    /// <summary>
    /// Access has expired.
    /// </summary>
    Expired,

    /// <summary>
    /// Request failed.
    /// </summary>
    Failed
}

/// <summary>
/// Common Azure RBAC role definition IDs.
/// </summary>
public static class AzureRoleDefinitions
{
    /// <summary>
    /// Owner - Full access to all resources, including the ability to delegate access.
    /// </summary>
    public const string Owner = "8e3af657-a8ff-443c-a75c-2fe8c4bcb635";

    /// <summary>
    /// Contributor - Create and manage all resources, but cannot grant access.
    /// </summary>
    public const string Contributor = "b24988ac-6180-42a0-ab88-20f7382dd24c";

    /// <summary>
    /// Reader - View all resources, but cannot make changes.
    /// </summary>
    public const string Reader = "acdd72a7-3385-48ef-bd42-f606fba81ae7";

    /// <summary>
    /// User Access Administrator - Manage user access to Azure resources.
    /// </summary>
    public const string UserAccessAdministrator = "18d7d88d-d35e-4fb5-a5c3-7773c20a72d9";

    /// <summary>
    /// Virtual Machine Administrator Login - View VMs and login as administrator.
    /// </summary>
    public const string VirtualMachineAdministratorLogin = "1c0163c0-47e6-4577-8991-ea5c82e286e4";

    /// <summary>
    /// Virtual Machine User Login - View VMs and login as a regular user.
    /// </summary>
    public const string VirtualMachineUserLogin = "fb879df8-f326-4884-b1cf-06f3ad86be52";

    /// <summary>
    /// Key Vault Administrator - Perform all operations on keys, secrets, and certificates.
    /// </summary>
    public const string KeyVaultAdministrator = "00482a5a-887f-4fb3-b363-3b7fe8e74483";

    /// <summary>
    /// Storage Blob Data Contributor - Read, write, and delete blob containers and data.
    /// </summary>
    public const string StorageBlobDataContributor = "ba92f5b4-2d11-453d-a403-e96b0029c9fe";

    /// <summary>
    /// AKS Cluster Admin - Manage AKS clusters.
    /// </summary>
    public const string AksClusterAdmin = "0ab0b1a8-8aac-4efd-b8c2-3ee1fb270be8";

    /// <summary>
    /// Get role name from definition ID.
    /// </summary>
    public static string GetRoleName(string roleDefinitionId)
    {
        return roleDefinitionId switch
        {
            Owner => "Owner",
            Contributor => "Contributor",
            Reader => "Reader",
            UserAccessAdministrator => "User Access Administrator",
            VirtualMachineAdministratorLogin => "Virtual Machine Administrator Login",
            VirtualMachineUserLogin => "Virtual Machine User Login",
            KeyVaultAdministrator => "Key Vault Administrator",
            StorageBlobDataContributor => "Storage Blob Data Contributor",
            AksClusterAdmin => "Azure Kubernetes Service Cluster Admin",
            _ => "Unknown Role"
        };
    }
}
