using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Governance.Services;

/// <summary>
/// Central governance engine that orchestrates policy enforcement, approval workflows,
/// and compliance validation for infrastructure provisioning and platform operations
/// </summary>
public interface IGovernanceEngine
{
    /// <summary>
    /// Performs pre-flight policy checks before infrastructure provisioning
    /// Validates naming conventions, policy compliance, budget constraints, and security requirements
    /// </summary>
    /// <param name="request">Infrastructure provisioning request with resource details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Pre-flight governance result with approval decision</returns>
    Task<PreFlightGovernanceResult> EvaluatePreFlightChecksAsync(
        InfrastructureProvisioningRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs post-flight validation after infrastructure provisioning
    /// Verifies resource creation, compliance tagging, security configuration, and audit logging
    /// </summary>
    /// <param name="request">Original provisioning request</param>
    /// <param name="result">Provisioning result with resource details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Post-flight governance result with compliance status</returns>
    Task<PostFlightGovernanceResult> EvaluatePostFlightChecksAsync(
        InfrastructureProvisioningRequest request,
        InfrastructureProvisionResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates resource naming against organizational conventions
    /// </summary>
    /// <param name="resourceType">Type of Azure resource</param>
    /// <param name="resourceName">Proposed resource name</param>
    /// <param name="environment">Target environment (dev/staging/prod)</param>
    /// <returns>Validation result with naming errors if any</returns>
    Task<NamingValidationResult> ValidateResourceNamingAsync(
        string resourceType,
        string resourceName,
        string environment);

    /// <summary>
    /// Validates that the specified Azure region is available and approved for use
    /// </summary>
    /// <param name="location">Azure region name</param>
    /// <param name="resourceType">Type of resource to provision</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Region validation result</returns>
    Task<RegionValidationResult> ValidateRegionAvailabilityAsync(
        string location,
        string resourceType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an approval workflow for infrastructure requests requiring manual review
    /// </summary>
    /// <param name="request">Infrastructure provisioning request</param>
    /// <param name="reason">Reason approval is required</param>
    /// <param name="violations">Policy violations that triggered approval</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval workflow with workflow ID</returns>
    Task<ApprovalWorkflow> CreateApprovalWorkflowAsync(
        InfrastructureProvisioningRequest request,
        string reason,
        List<string> violations,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the status of a pending approval workflow
    /// </summary>
    /// <param name="workflowId">Workflow identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Approval workflow status</returns>
    Task<ApprovalWorkflow?> GetApprovalWorkflowAsync(
        string workflowId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all pending approval workflows
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of pending approval workflows</returns>
    Task<List<ApprovalWorkflow>> ListPendingApprovalsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approves a pending approval workflow
    /// </summary>
    /// <param name="workflowId">Workflow identifier</param>
    /// <param name="approvedBy">User approving the request</param>
    /// <param name="comments">Optional approval comments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if approved successfully</returns>
    Task<bool> ApproveWorkflowAsync(
        string workflowId,
        string approvedBy,
        string? comments = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rejects a pending approval workflow
    /// </summary>
    /// <param name="workflowId">Workflow identifier</param>
    /// <param name="rejectedBy">User rejecting the request</param>
    /// <param name="reason">Reason for rejection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if rejected successfully</returns>
    Task<bool> RejectWorkflowAsync(
        string workflowId,
        string rejectedBy,
        string reason,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Infrastructure provisioning request for governance evaluation
/// </summary>
public class InfrastructureProvisioningRequest
{
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceGroupName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Environment { get; set; } = "dev";
    public string SubscriptionId { get; set; } = string.Empty;
    public Dictionary<string, object>? Parameters { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public string RequestedBy { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of naming validation
/// </summary>
public class NamingValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? SuggestedName { get; set; }
}

/// <summary>
/// Result of region availability validation
/// </summary>
public class RegionValidationResult
{
    public bool IsAvailable { get; set; }
    public bool IsApproved { get; set; }
    public List<string> UnavailableServices { get; set; } = new();
    public string? ReasonUnavailable { get; set; }
    public List<string> AlternativeRegions { get; set; } = new();
}

/// <summary>
/// Result of post-flight governance checks
/// </summary>
public class PostFlightGovernanceResult
{
    public bool IsCompliant { get; set; }
    public List<string> ComplianceIssues { get; set; } = new();
    public List<string> SecurityFindings { get; set; } = new();
    public bool TaggingCompliant { get; set; }
    public bool SecurityCompliant { get; set; }
    public bool AuditLogged { get; set; }
    public string? RemediationRequired { get; set; }
}
