using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Core.Services.Validation;
using Platform.Engineering.Copilot.Admin.Models;
using Platform.Engineering.Copilot.Admin.Services;

namespace Platform.Engineering.Copilot.Admin.Controllers;

/// <summary>
/// Admin API for platform engineers to manage service templates
/// </summary>
[ApiController]
[Route("api/admin/templates")]
[Produces("application/json")]
public class TemplateAdminController : ControllerBase
{
    private readonly ILogger<TemplateAdminController> _logger;
    private readonly ITemplateAdminService _templateAdminService;
    private readonly ConfigurationValidationService _validationService;

    public TemplateAdminController(
        ILogger<TemplateAdminController> logger,
        ITemplateAdminService templateAdminService,
        ConfigurationValidationService validationService)
    {
        _logger = logger;
        _templateAdminService = templateAdminService;
        _validationService = validationService;
    }

    /// <summary>
    /// Create a new service template
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(TemplateCreationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TemplateCreationResponse>> CreateTemplate(
        [FromBody] CreateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Creating template {TemplateName}", request.TemplateName);

        if (string.IsNullOrWhiteSpace(request.TemplateName))
        {
            return BadRequest(new { error = "Template name is required" });
        }

        if (string.IsNullOrWhiteSpace(request.ServiceName))
        {
            return BadRequest(new { error = "Service name is required" });
        }

        var result = await _templateAdminService.CreateTemplateAsync(request, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return CreatedAtAction(
            nameof(GetTemplate), 
            new { templateId = result.TemplateId }, 
            result);
    }

    /// <summary>
    /// Update an existing template
    /// </summary>
    [HttpPut("{templateId}")]
    [ProducesResponseType(typeof(TemplateCreationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateCreationResponse>> UpdateTemplate(
        string templateId,
        [FromBody] UpdateTemplateRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Updating template {TemplateId}. Request: TemplateType={TemplateType}, Format={Format}, Infrastructure.Format={InfraFormat}", 
            templateId, request.TemplateType, request.Format, request.Infrastructure?.Format);

        var result = await _templateAdminService.UpdateTemplateAsync(templateId, request, cancellationToken);

        if (!result.Success)
        {
            if (result.ErrorMessage?.Contains("not found") == true)
            {
                return NotFound(result);
            }
            return BadRequest(result);
        }

        return Ok(result);
    }

    /// <summary>
    /// List all templates with optional search
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<Core.Models.EnvironmentTemplate>), StatusCodes.Status200OK)]
    public async Task<ActionResult> ListTemplates(
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Admin API: Listing templates. Search: {Search}", search ?? "none");

        var templates = await _templateAdminService.ListTemplatesAsync(search, cancellationToken);
        
        return Ok(new 
        { 
            count = templates.Count,
            templates 
        });
    }

    /// <summary>
    /// Get a specific template by ID
    /// </summary>
    [HttpGet("{templateId}")]
    [ProducesResponseType(typeof(Core.Models.EnvironmentTemplate), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetTemplate(
        string templateId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Getting template {TemplateId}", templateId);

        var template = await _templateAdminService.GetTemplateAsync(templateId, cancellationToken);

        if (template == null)
        {
            return NotFound(new { error = $"Template {templateId} not found" });
        }

        return Ok(template);
    }

    /// <summary>
    /// Delete a template
    /// </summary>
    [HttpDelete("{templateId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteTemplate(
        string templateId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Deleting template {TemplateId}", templateId);

        var result = await _templateAdminService.DeleteTemplateAsync(templateId, cancellationToken);

        if (!result)
        {
            return NotFound(new { error = $"Template {templateId} not found" });
        }

        return NoContent();
    }

    /// <summary>
    /// Validate a template configuration before creating it
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(TemplateValidationResponse), StatusCodes.Status200OK)]
    public ActionResult<TemplateValidationResponse> ValidateTemplate(
        [FromBody] ValidateTemplateRequest request)
    {
        _logger.LogInformation("Admin API: Validating template configuration for {ServiceName}", request.ServiceName);

        // Convert Admin model to Core model
        var coreRequest = new Core.Models.TemplateGenerationRequest
        {
            ServiceName = request.ServiceName,
            Application = request.Application,
            Databases = request.Databases ?? new(),
            Infrastructure = request.Infrastructure ?? new(),
            Deployment = request.Deployment ?? new(),
            Security = request.Security ?? new(),
            Observability = request.Observability ?? new()
        };

        // Perform validation
        var validationResult = _validationService.ValidateRequest(coreRequest);

        // Convert Core ValidationResult to Admin DTO
        var response = new TemplateValidationResponse
        {
            IsValid = validationResult.IsValid,
            Platform = validationResult.Platform,
            ValidationTimeMs = validationResult.ValidationTimeMs,
            Errors = validationResult.Errors.Select(e => new ValidationErrorDto
            {
                Field = e.Field,
                Message = e.Message,
                Code = e.Code,
                CurrentValue = e.CurrentValue,
                ExpectedValue = e.ExpectedValue,
                DocumentationUrl = e.DocumentationUrl
            }).ToList(),
            Warnings = validationResult.Warnings.Select(w => new ValidationWarningDto
            {
                Field = w.Field,
                Message = w.Message,
                Code = w.Code,
                Severity = w.Severity.ToString(),
                Impact = w.Impact
            }).ToList(),
            Recommendations = validationResult.Recommendations.Select(r => new ValidationRecommendationDto
            {
                Field = r.Field,
                Message = r.Message,
                Code = r.Code,
                CurrentValue = r.CurrentValue,
                RecommendedValue = r.RecommendedValue,
                Reason = r.Reason,
                Benefit = r.Benefit
            }).ToList()
        };

        _logger.LogInformation(
            "Validation completed: IsValid={IsValid}, Errors={ErrorCount}, Warnings={WarningCount}, Recommendations={RecommendationCount}",
            response.IsValid, response.Errors.Count, response.Warnings.Count, response.Recommendations.Count);

        return Ok(response);
    }

    /// <summary>
    /// Get template statistics and metrics
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTemplateStats(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Getting template statistics");

        var templates = await _templateAdminService.ListTemplatesAsync(null, cancellationToken);

        var stats = new
        {
            totalTemplates = templates.Count,
            activeTemplates = templates.Count(t => t.IsActive),
            inactiveTemplates = templates.Count(t => !t.IsActive),
            publicTemplates = templates.Count(t => t.IsPublic),
            privateTemplates = templates.Count(t => !t.IsPublic),
            byType = templates.GroupBy(t => t.TemplateType)
                .Select(g => new { type = g.Key, count = g.Count() })
                .OrderByDescending(x => x.count),
            byCloudProvider = templates.GroupBy(t => t.CloudProvider)
                .Select(g => new { provider = g.Key, count = g.Count() }),
            byFormat = templates.GroupBy(t => t.Format)
                .Select(g => new { format = g.Key, count = g.Count() }),
            recentlyCreated = templates
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .Select(t => new { t.Name, t.CreatedAt, t.CreatedBy })
        };

        return Ok(stats);
    }

    /// <summary>
    /// Bulk activate/deactivate templates
    /// </summary>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkOperationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BulkOperationResponse>> BulkOperation(
        [FromBody] BulkTemplateOperationRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Bulk operation {Operation} on {Count} templates", 
            request.Operation, request.TemplateIds.Count);

        var response = new BulkOperationResponse
        {
            TotalRequested = request.TemplateIds.Count,
            Succeeded = 0,
            Failed = 0
        };

        foreach (var templateId in request.TemplateIds)
        {
            try
            {
                switch (request.Operation.ToLowerInvariant())
                {
                    case "delete":
                        var deleted = await _templateAdminService.DeleteTemplateAsync(templateId, cancellationToken);
                        if (deleted)
                            response.Succeeded++;
                        else
                        {
                            response.Failed++;
                            response.FailedTemplateIds.Add(templateId);
                            response.Errors[templateId] = "Not found or already deleted";
                        }
                        break;

                    case "activate":
                    case "deactivate":
                        var updateRequest = new UpdateTemplateRequest
                        {
                            IsActive = request.Operation.ToLowerInvariant() == "activate"
                        };
                        var result = await _templateAdminService.UpdateTemplateAsync(templateId, updateRequest, cancellationToken);
                        if (result.Success)
                            response.Succeeded++;
                        else
                        {
                            response.Failed++;
                            response.FailedTemplateIds.Add(templateId);
                            response.Errors[templateId] = result.ErrorMessage ?? "Update failed";
                        }
                        break;

                    default:
                        response.Failed++;
                        response.FailedTemplateIds.Add(templateId);
                        response.Errors[templateId] = $"Unknown operation: {request.Operation}";
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in bulk operation for template {TemplateId}", templateId);
                response.Failed++;
                response.FailedTemplateIds.Add(templateId);
                response.Errors[templateId] = ex.Message;
            }
        }

        response.Success = response.Failed == 0;

        return Ok(response);
    }

    /// <summary>
    /// Get all files for a specific template
    /// </summary>
    [HttpGet("{templateId}/files")]
    [ProducesResponseType(typeof(TemplateFilesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TemplateFilesResponse>> GetTemplateFiles(
        string templateId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Getting files for template {TemplateId}", templateId);

        try
        {
            var template = await _templateAdminService.GetTemplateAsync(templateId, cancellationToken);
            
            if (template == null)
            {
                _logger.LogWarning("Template {TemplateId} not found", templateId);
                return NotFound(new { error = $"Template {templateId} not found" });
            }

            _logger.LogInformation("Template loaded: {TemplateName}, Files collection is null: {IsNull}, Files count: {Count}", 
                template.Name, 
                template.Files == null, 
                template.Files?.Count() ?? 0);

            var files = template.Files?.Select(f => new TemplateFileDto
            {
                FileName = f.FileName,
                Content = f.Content,
                FileType = f.FileType,
                IsEntryPoint = f.IsEntryPoint,
                Order = f.Order,
                Size = f.Content.Length
            }).OrderBy(f => f.Order).ToList() ?? new List<TemplateFileDto>();

            var response = new TemplateFilesResponse
            {
                TemplateId = template.Id.ToString(),
                TemplateName = template.Name,
                FilesCount = files.Count,
                Files = files
            };

            _logger.LogInformation("Retrieved {Count} files for template {TemplateId}", files.Count, templateId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting files for template {TemplateId}", templateId);
            return BadRequest(new { error = ex.Message, details = ex.ToString() });
        }
    }

    /// <summary>
    /// Update a specific file in a template
    /// </summary>
    [HttpPut("{templateId}/files/{**fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateTemplateFile(
        string templateId,
        string fileName,
        [FromBody] UpdateTemplateFileRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin API: Updating file {FileName} in template {TemplateId}", fileName, templateId);

        try
        {
            var result = await _templateAdminService.UpdateTemplateFileAsync(
                templateId, 
                fileName, 
                request.Content, 
                cancellationToken);

            if (!result)
            {
                return NotFound(new { error = $"Template {templateId} or file {fileName} not found" });
            }

            return Ok(new { message = "File updated successfully", fileName, templateId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating file {FileName} in template {TemplateId}", fileName, templateId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get cost estimate for a resource configuration
    /// </summary>
    [HttpPost("cost-estimate")]
    [ProducesResponseType(typeof(AzureResourceCostResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AzureResourceCostResponse>> GetCostEstimate(
        [FromBody] AzureResourceCostRequest request,
        [FromServices] Platform.Engineering.Copilot.Core.Services.Cost.IAzurePricingService pricingService,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Calculating cost estimate for {ServiceFamily} in {Region}",
                request.ServiceFamily, request.Region);

            var specs = new Platform.Engineering.Copilot.Core.Services.Cost.ResourceSpecification
            {
                ServiceFamily = request.ServiceFamily,
                SkuName = request.SkuName,
                ProductName = request.ProductName,
                Quantity = request.Quantity,
                HoursPerMonth = request.HoursPerMonth
            };

            var monthlyCost = await pricingService.CalculateMonthlyCostAsync(
                request.ServiceFamily,
                request.Region,
                specs,
                cancellationToken);

            var response = new AzureResourceCostResponse
            {
                MonthlyCost = monthlyCost,
                Currency = "USD",
                ServiceFamily = request.ServiceFamily,
                Region = request.Region,
                Quantity = request.Quantity,
                HoursPerMonth = request.HoursPerMonth
            };

            _logger.LogInformation(
                "Cost estimate calculated: ${Cost:N2}/month for {ServiceFamily}",
                monthlyCost, request.ServiceFamily);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating cost estimate");
            return BadRequest(new { error = ex.Message });
        }
    }
}
