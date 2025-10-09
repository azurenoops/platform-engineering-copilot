using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models.Onboarding;
using Platform.Engineering.Copilot.Data.Entities;

namespace Platform.Engineering.Copilot.Admin.Controllers;

/// <summary>
/// API controller for managing Navy Flankspeed onboarding requests
/// </summary>
[ApiController]
[Route("api/admin/onboarding")]
[Produces("application/json")]
public class OnboardingAdminController : ControllerBase
{
    private readonly IOnboardingService _onboardingService;
    private readonly ILogger<OnboardingAdminController> _logger;

    public OnboardingAdminController(
        IOnboardingService onboardingService,
        ILogger<OnboardingAdminController> logger)
    {
        _onboardingService = onboardingService;
        _logger = logger;
    }

    /// <summary>
    /// Gets all pending onboarding requests awaiting NNWC review
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<OnboardingRequest>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OnboardingRequest>>> GetPendingRequests(
        CancellationToken cancellationToken)
    {
        try
        {
            var requests = await _onboardingService.GetPendingRequestsAsync(cancellationToken);
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending onboarding requests");
            return StatusCode(500, new { error = "Failed to retrieve pending requests" });
        }
    }

    /// <summary>
    /// Gets a specific onboarding request by ID
    /// </summary>
    [HttpGet("{requestId}")]
    [ProducesResponseType(typeof(OnboardingRequest), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnboardingRequest>> GetRequest(
        string requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            var request = await _onboardingService.GetRequestAsync(requestId, cancellationToken);
            
            if (request == null)
            {
                return NotFound(new { error = $"Request {requestId} not found" });
            }

            return Ok(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving onboarding request {RequestId}", requestId);
            return StatusCode(500, new { error = "Failed to retrieve request" });
        }
    }

    /// <summary>
    /// Gets all onboarding requests for a specific mission owner
    /// </summary>
    [HttpGet("owner/{email}")]
    [ProducesResponseType(typeof(List<OnboardingRequest>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OnboardingRequest>>> GetRequestsByOwner(
        string email,
        CancellationToken cancellationToken)
    {
        try
        {
            var requests = await _onboardingService.GetRequestsByOwnerAsync(email, cancellationToken);
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving requests for owner {Email}", email);
            return StatusCode(500, new { error = "Failed to retrieve requests" });
        }
    }

    /// <summary>
    /// Approves an onboarding request and triggers automated provisioning
    /// </summary>
    [HttpPost("{requestId}/approve")]
    [ProducesResponseType(typeof(OnboardingApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnboardingApprovalResponse>> ApproveRequest(
        string requestId,
        [FromBody] OnboardingApprovalRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Approving onboarding request {RequestId} by {ApprovedBy}",
                requestId, request.ApprovedBy);

            var result = await _onboardingService.ApproveRequestAsync(
                requestId,
                request.ApprovedBy,
                request.Comments,
                cancellationToken);

            if (!result.Success)
            {
                return BadRequest(new OnboardingApprovalResponse
                {
                    Success = false,
                    Message = result.Message ?? "Failed to approve request",
                    RequestId = requestId
                });
            }

            return Ok(new OnboardingApprovalResponse
            {
                Success = true,
                Message = "Onboarding request approved. Provisioning will begin shortly.",
                RequestId = requestId,
                ProvisioningJobId = result.JobId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving onboarding request {RequestId}", requestId);
            return StatusCode(500, new OnboardingApprovalResponse
            {
                Success = false,
                Message = $"Error approving request: {ex.Message}",
                RequestId = requestId
            });
        }
    }

    /// <summary>
    /// Rejects an onboarding request
    /// </summary>
    [HttpPost("{requestId}/reject")]
    [ProducesResponseType(typeof(OnboardingApprovalResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OnboardingApprovalResponse>> RejectRequest(
        string requestId,
        [FromBody] OnboardingRejectionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Rejecting onboarding request {RequestId} by {RejectedBy}",
                requestId, request.RejectedBy);

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return BadRequest(new OnboardingApprovalResponse
                {
                    Success = false,
                    Message = "Rejection reason is required",
                    RequestId = requestId
                });
            }

            var success = await _onboardingService.RejectRequestAsync(
                requestId,
                request.RejectedBy,
                request.Reason,
                cancellationToken);

            if (!success)
            {
                return BadRequest(new OnboardingApprovalResponse
                {
                    Success = false,
                    Message = "Failed to reject request. It may not exist or may already be processed.",
                    RequestId = requestId
                });
            }

            return Ok(new OnboardingApprovalResponse
            {
                Success = true,
                Message = "Onboarding request rejected. Mission owner will be notified.",
                RequestId = requestId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting onboarding request {RequestId}", requestId);
            return StatusCode(500, new OnboardingApprovalResponse
            {
                Success = false,
                Message = $"Error rejecting request: {ex.Message}",
                RequestId = requestId
            });
        }
    }

    /// <summary>
    /// Gets the status of a provisioning job
    /// </summary>
    [HttpGet("provisioning/{jobId}")]
    [ProducesResponseType(typeof(ProvisioningStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProvisioningStatus>> GetProvisioningStatus(
        string jobId,
        CancellationToken cancellationToken)
    {
        try
        {
            var status = await _onboardingService.GetProvisioningStatusAsync(jobId, cancellationToken);
            
            if (status.Status == "NotFound")
            {
                return NotFound(new { error = $"Provisioning job {jobId} not found" });
            }

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provisioning status for job {JobId}", jobId);
            return StatusCode(500, new { error = "Failed to retrieve provisioning status" });
        }
    }

    /// <summary>
    /// Gets all requests currently being provisioned
    /// </summary>
    [HttpGet("provisioning")]
    [ProducesResponseType(typeof(List<OnboardingRequest>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OnboardingRequest>>> GetProvisioningRequests(
        CancellationToken cancellationToken)
    {
        try
        {
            var requests = await _onboardingService.GetProvisioningRequestsAsync(cancellationToken);
            return Ok(requests);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving provisioning requests");
            return StatusCode(500, new { error = "Failed to retrieve provisioning requests" });
        }
    }

    /// <summary>
    /// Gets onboarding statistics for the dashboard
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(OnboardingStats), StatusCodes.Status200OK)]
    public async Task<ActionResult<OnboardingStats>> GetStats(
        CancellationToken cancellationToken)
    {
        try
        {
            var stats = await _onboardingService.GetStatsAsync(cancellationToken);
            return Ok(stats);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving onboarding statistics");
            return StatusCode(500, new { error = "Failed to retrieve statistics" });
        }
    }

    /// <summary>
    /// Gets onboarding history for a specific time period
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(List<OnboardingRequest>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OnboardingRequest>>> GetHistory(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        try
        {
            var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
            var end = endDate ?? DateTime.UtcNow;

            var history = await _onboardingService.GetHistoryAsync(start, end, cancellationToken);
            return Ok(history);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving onboarding history");
            return StatusCode(500, new { error = "Failed to retrieve history" });
        }
    }
}

#region Request/Response DTOs

/// <summary>
/// Request to approve an onboarding
/// </summary>
public class OnboardingApprovalRequest
{
    /// <summary>
    /// Name/ID of the person approving
    /// </summary>
    public string ApprovedBy { get; set; } = string.Empty;

    /// <summary>
    /// Optional comments for the approval
    /// </summary>
    public string? Comments { get; set; }
}

/// <summary>
/// Request to reject an onboarding
/// </summary>
public class OnboardingRejectionRequest
{
    /// <summary>
    /// Name/ID of the person rejecting
    /// </summary>
    public string RejectedBy { get; set; } = string.Empty;

    /// <summary>
    /// Reason for rejection (required)
    /// </summary>
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Response from approval/rejection action
/// </summary>
public class OnboardingApprovalResponse
{
    /// <summary>
    /// Whether the operation succeeded
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The request ID
    /// </summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>
    /// Provisioning job ID (if approval triggered provisioning)
    /// </summary>
    public string? ProvisioningJobId { get; set; }
}

#endregion
