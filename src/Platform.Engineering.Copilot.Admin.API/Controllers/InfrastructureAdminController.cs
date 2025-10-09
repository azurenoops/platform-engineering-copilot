using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Admin.Models;

namespace Platform.Engineering.Copilot.Admin.Controllers;

/// <summary>
/// Admin API for infrastructure provisioning and management
/// </summary>
[ApiController]
[Route("api/admin/infrastructure")]
[Produces("application/json")]
public class InfrastructureAdminController : ControllerBase
{
    private readonly ILogger<InfrastructureAdminController> _logger;
    private readonly IAzureResourceService _azureResourceService;
    private readonly IEnvironmentManagementEngine _environmentEngine;
    private readonly IInfrastructureProvisioningService _infrastructureProvisioning;

    public InfrastructureAdminController(
        ILogger<InfrastructureAdminController> logger,
        IAzureResourceService azureResourceService,
        IEnvironmentManagementEngine environmentEngine,
        IInfrastructureProvisioningService infrastructureProvisioning)
    {
        _logger = logger;
        _azureResourceService = azureResourceService;
        _environmentEngine = environmentEngine;
        _infrastructureProvisioning = infrastructureProvisioning;
    }

    /// <summary>
    /// Provision infrastructure resources
    /// </summary>
    [HttpPost("provision")]
    [ProducesResponseType(typeof(ProvisionInfrastructureResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProvisionInfrastructureResponse>> ProvisionInfrastructure(
        [FromBody] ProvisionInfrastructureRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Provisioning infrastructure - ResourceType: {ResourceType}, RG: {ResourceGroup}", 
            request.ResourceType, request.ResourceGroupName);

        var startTime = DateTime.UtcNow;

        try
        {
            // Build natural language query from request parameters
            var query = BuildQueryFromRequest(request);
            
            // Use the new AI-powered InfrastructureProvisioningService
            var result = await _infrastructureProvisioning.ProvisionInfrastructureAsync(
                query,
                cancellationToken);

            var response = new ProvisionInfrastructureResponse
            {
                Success = result.Success,
                ResourceGroupId = result.ResourceId,
                DeploymentId = result.ResourceId,
                Message = result.Message,
                ErrorMessage = result.ErrorDetails,
                Duration = DateTime.UtcNow - startTime
            };

            if (result.Success)
            {
                return CreatedAtAction(nameof(GetResourceGroupStatus), 
                    new { resourceGroup = request.ResourceGroupName }, 
                    response);
            }

            return BadRequest(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error provisioning infrastructure");
            return BadRequest(new ProvisionInfrastructureResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            });
        }
    }

    /// <summary>
    /// Get resource group status
    /// </summary>
    [HttpGet("resource-groups/{resourceGroup}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetResourceGroupStatus(
        string resourceGroup,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Getting status for resource group: {ResourceGroup}", resourceGroup);

        try
        {
            // Get resource group info from Azure
            var status = new
            {
                name = resourceGroup,
                status = "Active",
                location = "eastus",
                resourceCount = 0,
                lastUpdated = DateTime.UtcNow
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource group status");
            return NotFound(new { error = $"Resource group {resourceGroup} not found" });
        }
    }

    /// <summary>
    /// List all resource groups
    /// </summary>
    [HttpGet("resource-groups")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> ListResourceGroups(
        [FromQuery] string? subscription = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin API: Listing resource groups");

        try
        {
            // Use the new InfrastructureProvisioningService
            var resourceGroups = await _infrastructureProvisioning.ListResourceGroupsAsync(cancellationToken);

            return Ok(new { count = resourceGroups.Count, resourceGroups });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing resource groups");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Delete infrastructure resources
    /// </summary>
    [HttpDelete("resource-groups/{resourceGroup}")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteResourceGroup(
        string resourceGroup,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin API: Deleting resource group: {ResourceGroup}, Force: {Force}", 
            resourceGroup, force);

        try
        {
            // Use the new InfrastructureProvisioningService
            var success = await _infrastructureProvisioning.DeleteResourceGroupAsync(resourceGroup, cancellationToken);

            if (success)
            {
                return Accepted(new 
                { 
                    message = $"Resource group {resourceGroup} deletion initiated",
                    estimatedCompletionTime = DateTime.UtcNow.AddMinutes(10)
                });
            }

            return StatusCode(500, new { error = "Failed to delete resource group" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting resource group");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get infrastructure cost estimates
    /// </summary>
    [HttpPost("cost-estimate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetCostEstimate(
        [FromBody] CostEstimateRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Getting cost estimate for {ResourceType} in {Location}", 
            request.ResourceType, request.Location);

        try
        {
            // Build natural language query from request parameters
            var query = BuildQueryFromRequest(request);
            
            // Use the new AI-powered InfrastructureProvisioningService
            var estimate = await _infrastructureProvisioning.EstimateCostAsync(
                query,
                cancellationToken);

            var response = new
            {
                estimatedMonthlyCost = estimate.MonthlyEstimate,
                estimatedAnnualCost = estimate.AnnualEstimate,
                currency = estimate.Currency,
                resourceType = estimate.ResourceType,
                location = estimate.Location,
                notes = estimate.Notes,
                breakdown = estimate.CostBreakdown
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cost estimate");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // Helper methods to convert structured requests to natural language queries
    private string BuildQueryFromRequest(ProvisionInfrastructureRequest request)
    {
        var resourceType = request.ResourceType ?? request.TemplateId ?? request.InfrastructureType ?? "resource";
        var location = request.Location ?? "eastus";
        
        // Try to extract resource name from parameters or generate one
        string? resourceName = null;
        if (request.Parameters?.TryGetValue("name", out var nameObj) == true)
        {
            resourceName = nameObj?.ToString();
        }
        else if (request.Parameters?.TryGetValue("resourceName", out var resNameObj) == true)
        {
            resourceName = resNameObj?.ToString();
        }
        
        // Build the query
        var query = $"Create {resourceType}";
        
        if (!string.IsNullOrEmpty(resourceName))
        {
            query += $" named {resourceName}";
        }
        
        if (!string.IsNullOrEmpty(request.ResourceGroupName))
        {
            query += $" in resource group {request.ResourceGroupName}";
        }
        
        query += $" in {location}";
        
        // Add other parameters if provided (excluding name since it's already part of the query)
        if (request.Parameters != null && request.Parameters.Any())
        {
            var otherParams = request.Parameters
                .Where(p => p.Key != "name" && p.Key != "resourceName")
                .Select(p => $"{p.Key}={p.Value}");
            
            if (otherParams.Any())
            {
                query += $" with {string.Join(", ", otherParams)}";
            }
        }
        
        // Add tags if provided
        if (request.Tags != null && request.Tags.Any())
        {
            var tagList = request.Tags.Select(t => $"{t.Key}={t.Value}");
            query += $" and tags {string.Join(", ", tagList)}";
        }
        
        return query;
    }

    private string BuildQueryFromRequest(CostEstimateRequest request)
    {
        var resourceType = request.ResourceType ?? request.TemplateId ?? "resource";
        var location = request.Location ?? request.Region ?? "eastus";
        
        var query = $"Estimate cost for {resourceType} in {location}";
        
        // Add parameters if provided for more accurate cost estimation
        if (request.Parameters != null && request.Parameters.Any())
        {
            var paramList = request.Parameters.Select(p => $"{p.Key}={p.Value}");
            query += $" with {string.Join(", ", paramList)}";
        }
        
        return query;
    }
}

// Request models
public class CostEstimateRequest
{
    public string? ResourceType { get; set; }
    public string? TemplateId { get; set; } // For backwards compatibility
    public string? Location { get; set; }
    public string? Region { get; set; } // For backwards compatibility
    public Dictionary<string, object>? Parameters { get; set; }
}
