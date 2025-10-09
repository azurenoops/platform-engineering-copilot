using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Governance.Services;
using Platform.Engineering.Copilot.Admin.Models;

namespace Platform.Engineering.Copilot.Admin.Controllers;

/// <summary>
/// API controller for governance operations including approval workflows,
/// policy checks, and compliance validation
/// </summary>
[ApiController]
[Route("api/admin/governance")]
public class GovernanceAdminController : ControllerBase
{
    private readonly ILogger<GovernanceAdminController> _logger;
    private readonly IGovernanceEngine _governanceEngine;

    public GovernanceAdminController(
        ILogger<GovernanceAdminController> logger,
        IGovernanceEngine governanceEngine)
    {
        _logger = logger;
        _governanceEngine = governanceEngine;
    }

    /// <summary>
    /// List all pending approval workflows
    /// </summary>
    [HttpGet("approvals/pending")]
    public async Task<ActionResult<List<ApprovalWorkflowDto>>> GetPendingApprovals(
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Fetching pending approval workflows");

            var workflows = await _governanceEngine.ListPendingApprovalsAsync(cancellationToken);

            var dtos = workflows.Select(w => new ApprovalWorkflowDto
            {
                Id = w.Id,
                ResourceType = w.ResourceType,
                ResourceName = w.ResourceName,
                ResourceGroupName = w.ResourceGroupName,
                Location = w.Location,
                Environment = w.Environment,
                Status = w.Status.ToString(),
                RequestedBy = w.RequestedBy,
                RequestedAt = w.RequestedAt,
                ExpiresAt = w.ExpiresAt,
                Reason = w.Reason,
                PolicyViolations = w.PolicyViolations,
                RequiredApprovers = w.RequiredApprovers,
                ApprovedBy = w.ApprovedBy,
                ApprovedAt = w.ApprovedAt,
                RejectedBy = w.RejectedBy,
                RejectedAt = w.RejectedAt,
                RejectionReason = w.RejectionReason
            }).ToList();

            _logger.LogInformation("Found {Count} pending approval workflows", dtos.Count);

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending approvals");
            return StatusCode(500, new { error = "Failed to fetch pending approvals", message = ex.Message });
        }
    }

    /// <summary>
    /// Get details of a specific approval workflow
    /// </summary>
    [HttpGet("approvals/{workflowId}")]
    public async Task<ActionResult<ApprovalWorkflowDto>> GetApprovalWorkflow(
        string workflowId,
        CancellationToken cancellationToken)
    {
        try
        {
            var workflow = await _governanceEngine.GetApprovalWorkflowAsync(workflowId, cancellationToken);

            if (workflow == null)
            {
                return NotFound(new { error = $"Approval workflow '{workflowId}' not found" });
            }

            var dto = new ApprovalWorkflowDto
            {
                Id = workflow.Id,
                ResourceType = workflow.ResourceType,
                ResourceName = workflow.ResourceName,
                ResourceGroupName = workflow.ResourceGroupName,
                Location = workflow.Location,
                Environment = workflow.Environment,
                Status = workflow.Status.ToString(),
                RequestedBy = workflow.RequestedBy,
                RequestedAt = workflow.RequestedAt,
                ExpiresAt = workflow.ExpiresAt,
                Reason = workflow.Reason,
                PolicyViolations = workflow.PolicyViolations,
                RequiredApprovers = workflow.RequiredApprovers,
                ApprovedBy = workflow.ApprovedBy,
                ApprovedAt = workflow.ApprovedAt,
                RejectedBy = workflow.RejectedBy,
                RejectedAt = workflow.RejectedAt,
                RejectionReason = workflow.RejectionReason,
                ApprovalComments = workflow.ApprovalComments
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching approval workflow {WorkflowId}", workflowId);
            return StatusCode(500, new { error = "Failed to fetch approval workflow", message = ex.Message });
        }
    }

    /// <summary>
    /// Approve a pending approval workflow
    /// </summary>
    [HttpPost("approvals/{workflowId}/approve")]
    public async Task<ActionResult<ApprovalActionResponse>> ApproveWorkflow(
        string workflowId,
        [FromBody] ApproveWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Approving workflow {WorkflowId} by {ApprovedBy}", workflowId, request.ApprovedBy);

            var success = await _governanceEngine.ApproveWorkflowAsync(
                workflowId,
                request.ApprovedBy,
                request.Comments,
                cancellationToken);

            if (!success)
            {
                return BadRequest(new ApprovalActionResponse
                {
                    Success = false,
                    Message = "Failed to approve workflow. It may not exist, be expired, or already processed."
                });
            }

            return Ok(new ApprovalActionResponse
            {
                Success = true,
                WorkflowId = workflowId,
                Message = "Workflow approved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving workflow {WorkflowId}", workflowId);
            return StatusCode(500, new ApprovalActionResponse
            {
                Success = false,
                Message = $"Failed to approve workflow: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Reject a pending approval workflow
    /// </summary>
    [HttpPost("approvals/{workflowId}/reject")]
    public async Task<ActionResult<ApprovalActionResponse>> RejectWorkflow(
        string workflowId,
        [FromBody] RejectWorkflowRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Rejecting workflow {WorkflowId} by {RejectedBy}", workflowId, request.RejectedBy);

            var success = await _governanceEngine.RejectWorkflowAsync(
                workflowId,
                request.RejectedBy,
                request.Reason,
                cancellationToken);

            if (!success)
            {
                return BadRequest(new ApprovalActionResponse
                {
                    Success = false,
                    Message = "Failed to reject workflow. It may not exist or already be processed."
                });
            }

            return Ok(new ApprovalActionResponse
            {
                Success = true,
                WorkflowId = workflowId,
                Message = "Workflow rejected successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting workflow {WorkflowId}", workflowId);
            return StatusCode(500, new ApprovalActionResponse
            {
                Success = false,
                Message = $"Failed to reject workflow: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Validate resource naming conventions
    /// </summary>
    [HttpPost("validate/naming")]
    public async Task<ActionResult<NamingValidationResponse>> ValidateNaming(
        [FromBody] NamingValidationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _governanceEngine.ValidateResourceNamingAsync(
                request.ResourceType,
                request.ResourceName,
                request.Environment);

            return Ok(new NamingValidationResponse
            {
                IsValid = result.IsValid,
                Errors = result.Errors,
                Warnings = result.Warnings,
                SuggestedName = result.SuggestedName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating naming for {ResourceName}", request.ResourceName);
            return StatusCode(500, new { error = "Failed to validate naming", message = ex.Message });
        }
    }

    /// <summary>
    /// Validate region availability
    /// </summary>
    [HttpPost("validate/region")]
    public async Task<ActionResult<RegionValidationResponse>> ValidateRegion(
        [FromBody] RegionValidationRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _governanceEngine.ValidateRegionAvailabilityAsync(
                request.Location,
                request.ResourceType,
                cancellationToken);

            return Ok(new RegionValidationResponse
            {
                IsAvailable = result.IsAvailable,
                IsApproved = result.IsApproved,
                UnavailableServices = result.UnavailableServices,
                ReasonUnavailable = result.ReasonUnavailable,
                AlternativeRegions = result.AlternativeRegions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating region {Location}", request.Location);
            return StatusCode(500, new { error = "Failed to validate region", message = ex.Message });
        }
    }

    /// <summary>
    /// Get approval workflow statistics
    /// </summary>
    [HttpGet("approvals/stats")]
    public async Task<ActionResult<ApprovalStatsResponse>> GetApprovalStats(
        CancellationToken cancellationToken)
    {
        try
        {
            var allWorkflows = await _governanceEngine.ListPendingApprovalsAsync(cancellationToken);

            var stats = new ApprovalStatsResponse
            {
                TotalPending = allWorkflows.Count,
                ExpiringSoon = allWorkflows.Count(w => w.ExpiresAt < DateTime.UtcNow.AddHours(4)),
                ByEnvironment = allWorkflows.GroupBy(w => w.Environment)
                    .ToDictionary(g => g.Key, g => g.Count()),
                ByResourceType = allWorkflows.GroupBy(w => w.ResourceType)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching approval stats");
            return StatusCode(500, new { error = "Failed to fetch approval stats", message = ex.Message });
        }
    }
}
