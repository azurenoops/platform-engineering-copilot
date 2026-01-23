using Microsoft.AspNetCore.Mvc;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;
using Platform.Engineering.Copilot.Core.Models.TemplateMatching;
using Platform.Engineering.Copilot.Core.Utilities;
using Platform.Engineering.Copilot.Admin.API.Models;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// API controller for Service Template management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TemplatesController : ControllerBase
{
    private readonly IServiceTemplateCatalogService _catalogService;
    private readonly INaturalLanguageTemplateMatchingService? _nlMatchingService;
    private readonly IGitTemplateSyncService? _gitSyncService;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(
        IServiceTemplateCatalogService catalogService,
        ILogger<TemplatesController> logger,
        INaturalLanguageTemplateMatchingService? nlMatchingService = null,
        IGitTemplateSyncService? gitSyncService = null)
    {
        _catalogService = catalogService;
        _logger = logger;
        _nlMatchingService = nlMatchingService;
        _gitSyncService = gitSyncService;
    }

    /// <summary>
    /// Get all service templates
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ServiceTemplateSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<ServiceTemplateSummaryDto>>> GetTemplates(
        [FromQuery] string? category = null,
        [FromQuery] TemplateStatus? status = null,
        [FromQuery] string? keyword = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var criteria = new TemplateSearchCriteria
            {
                Category = category,
                Status = status,
                Keyword = keyword,
                Skip = skip,
                Take = take
            };

            var templates = await _catalogService.SearchTemplatesAsync(criteria, cancellationToken);
            
            var dtos = templates.Select(t => new ServiceTemplateSummaryDto
            {
                Id = t.Id,
                Name = t.Name,
                DisplayName = t.DisplayName,
                Description = t.Description,
                Version = t.Version,
                Category = t.Category,
                Format = t.Format.ToString(),
                Status = t.Status.ToString(),
                DeploymentScope = t.DeploymentScope ?? "resourceGroup",
                DeploymentCount = t.DeploymentCount,
                CreatedAt = t.CreatedAt,
                CreatedBy = t.CreatedBy,
                // Git Sync properties
                HasGitSource = t.GitSource != null,
                GitRepositoryUrl = t.GitSource?.RepositoryUrl,
                LastSyncedFromGit = t.LastSyncedFromGit,
                GitAutoSync = t.GitSource?.AutoSync ?? false
            }).ToList();

            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving templates");
            return StatusCode(500, new { error = "Failed to retrieve templates" });
        }
    }

    /// <summary>
    /// Get a specific template by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ServiceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceTemplateDto>> GetTemplate(
        string id,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await _catalogService.GetTemplateAsync(id, cancellationToken);
            if (template == null)
                return NotFound(new { error = $"Template {id} not found" });

            return Ok(MapToDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template {TemplateId}", id);
            return StatusCode(500, new { error = "Failed to retrieve template" });
        }
    }

    /// <summary>
    /// Get a template by name
    /// </summary>
    [HttpGet("by-name/{name}")]
    [ProducesResponseType(typeof(ServiceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceTemplateDto>> GetTemplateByName(
        string name,
        [FromQuery] string? version = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await _catalogService.GetTemplateByNameAsync(name, version, cancellationToken);
            if (template == null)
                return NotFound(new { error = $"Template '{name}' not found" });

            return Ok(MapToDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving template by name {TemplateName}", name);
            return StatusCode(500, new { error = "Failed to retrieve template" });
        }
    }

    /// <summary>
    /// Create a new service template
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceTemplateDto>> CreateTemplate(
        [FromBody] CreateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var template = new ServiceTemplate
            {
                Name = request.Name,
                DisplayName = request.DisplayName ?? request.Name,
                Description = request.Description ?? string.Empty,
                Version = request.Version ?? "1.0.0",
                Category = request.Category ?? "General",
                Format = Enum.TryParse<TemplateFormat>(request.Format, out var format) 
                    ? format : TemplateFormat.Bicep,
                MainTemplateContent = request.TemplateContent ?? string.Empty,
                Status = !string.IsNullOrEmpty(request.Status) && Enum.TryParse<TemplateStatus>(request.Status, out var status) 
                    ? status : TemplateStatus.Draft,
                CreatedBy = request.CreatedBy ?? "admin",
                RequiresApproval = request.RequiresApproval,
                EnforceCompliance = request.EnforceCompliance,
                DefaultExpirationDays = request.DefaultExpirationDays,
                ComplianceFrameworks = request.ComplianceFrameworks ?? new List<string>(),
                Keywords = request.Keywords ?? new List<string>(),
                UseCases = request.UseCases ?? new List<string>(),
                AiSelectionHint = request.AiSelectionHint
            };

            // Add parameters
            if (request.Parameters != null)
            {
                foreach (var p in request.Parameters)
                {
                    template.Parameters.Add(new TemplateParameter
                    {
                        Name = p.Name,
                        DisplayName = p.DisplayName ?? p.Name,
                        Description = p.Description ?? string.Empty,
                        Type = Enum.TryParse<ParameterType>(p.Type, out var pType) 
                            ? pType : ParameterType.String,
                        Required = p.Required,
                        DefaultValue = p.DefaultValue,
                        AllowedValues = p.AllowedValues?.Select(v => v?.ToString() ?? "").ToList(),
                        MinValue = p.MinValue is int minInt ? minInt : null,
                        MaxValue = p.MaxValue is int maxInt ? maxInt : null,
                        DisplayOrder = p.DisplayOrder
                    });
                }
            }

            // Add guardrails
            if (request.Guardrails != null)
            {
                foreach (var g in request.Guardrails)
                {
                    template.Guardrails.Add(new TemplateGuardrail
                    {
                        Name = g.Name,
                        Description = g.Description ?? string.Empty,
                        Type = Enum.TryParse<GuardrailType>(g.Type, out var gType) 
                            ? gType : GuardrailType.Limit,
                        Property = g.Property,
                        Operator = g.Operator,
                        Value = g.Value ?? string.Empty,
                        Action = Enum.TryParse<GuardrailAction>(g.Action, out var gAction) 
                            ? gAction : GuardrailAction.Deny,
                        ErrorMessage = g.ErrorMessage ?? string.Empty
                    });
                }
            }

            // Set Git source if provided
            if (request.GitSource != null && !string.IsNullOrEmpty(request.GitSource.RepositoryUrl))
            {
                template.GitSource = new GitSourceInfo
                {
                    RepositoryUrl = request.GitSource.RepositoryUrl,
                    Branch = request.GitSource.Branch ?? "main",
                    Path = request.GitSource.Path ?? "",
                    AutoSync = request.GitSource.AutoSync,
                    SyncIntervalMinutes = request.GitSource.SyncIntervalMinutes
                };
            }

            var created = await _catalogService.CreateTemplateAsync(template, template.CreatedBy, cancellationToken);
            
            // Auto-sync from Git if source is configured
            if (created.GitSource != null && !string.IsNullOrEmpty(created.GitSource.RepositoryUrl) && _gitSyncService != null)
            {
                try
                {
                    _logger.LogInformation("Auto-syncing template {TemplateId} from Git source", created.Id);
                    if (Guid.TryParse(created.Id, out var createdGuid))
                    {
                        await _gitSyncService.SyncTemplateAsync(createdGuid, force: true, cancellationToken);
                    }
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx, "Failed to auto-sync template {TemplateId} from Git. Template created but content may be empty.", created.Id);
                }
            }
            
            _logger.LogInformation("Created template {TemplateId}: {TemplateName}", 
                created.Id, created.Name);

            return CreatedAtAction(
                nameof(GetTemplate), 
                new { id = created.Id }, 
                MapToDto(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template");
            return StatusCode(500, new { error = "Failed to create template" });
        }
    }

    /// <summary>
    /// Update a service template
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(ServiceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceTemplateDto>> UpdateTemplate(
        string id,
        [FromBody] UpdateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _catalogService.GetTemplateAsync(id, cancellationToken);
            if (existing == null)
                return NotFound(new { error = $"Template {id} not found" });

            // Update fields
            if (request.DisplayName != null) existing.DisplayName = request.DisplayName;
            if (request.Description != null) existing.Description = request.Description;
            if (request.Category != null) existing.Category = request.Category;
            if (request.TemplateContent != null) existing.MainTemplateContent = request.TemplateContent;
            if (request.Keywords != null) existing.Keywords = request.Keywords;
            if (request.UseCases != null) existing.UseCases = request.UseCases;
            if (request.AiSelectionHint != null) existing.AiSelectionHint = request.AiSelectionHint;
            if (request.DefaultExpirationDays.HasValue) existing.DefaultExpirationDays = request.DefaultExpirationDays;
            if (!string.IsNullOrEmpty(request.DeploymentScope)) existing.DeploymentScope = request.DeploymentScope;
            
            // Update Git source
            if (request.GitSource != null)
            {
                existing.GitSource = new GitSourceInfo
                {
                    RepositoryUrl = request.GitSource.RepositoryUrl ?? "",
                    Branch = request.GitSource.Branch ?? "main",
                    Path = request.GitSource.Path ?? "",
                    AutoSync = request.GitSource.AutoSync,
                    SyncIntervalMinutes = request.GitSource.SyncIntervalMinutes
                };
            }
            
            // Update guardrails
            if (request.Guardrails != null)
            {
                existing.Guardrails = request.Guardrails.Select(g => new TemplateGuardrail
                {
                    Name = g.Name ?? "",
                    Description = g.Description ?? "",
                    Type = Enum.TryParse<GuardrailType>(g.Type, out var type) ? type : GuardrailType.Limit,
                    Property = g.Property ?? "",
                    Operator = g.Operator ?? "",
                    Value = g.Value ?? "",
                    Action = Enum.TryParse<GuardrailAction>(g.Action, out var action) ? action : GuardrailAction.Deny,
                    ErrorMessage = g.ErrorMessage ?? ""
                }).ToList();
            }
                        // Update Parameters if provided
            if (request.Parameters != null)
            {
                existing.Parameters = request.Parameters.Select(p => new TemplateParameter
                {
                    Name = p.Name,
                    DisplayName = p.DisplayName ?? p.Name,
                    Description = p.Description ?? "",
                    Type = Enum.TryParse<ParameterType>(p.Type, out var type) ? type : ParameterType.String,
                    Required = p.Required,
                    DefaultValue = p.DefaultValue,
                    AllowedValues = p.AllowedValues?.Select(v => v?.ToString() ?? "").ToList(),
                    DisplayOrder = p.DisplayOrder
                }).ToList();
                
                // Mark parameters as overridden so Git sync won't overwrite them
                existing.ParametersOverridden = true;
                _logger.LogInformation("Parameters manually updated for template {TemplateId} - marked as overridden", id);
            }
            
            // Update Status if provided
            if (!string.IsNullOrEmpty(request.Status) && Enum.TryParse<TemplateStatus>(request.Status, out var status))
            {
                existing.Status = status;
            }
            
            // Update approval and compliance settings
            if (request.RequiresApproval.HasValue)
                existing.RequiresApproval = request.RequiresApproval.Value;
                
            if (request.EnforceCompliance.HasValue)
                existing.EnforceCompliance = request.EnforceCompliance.Value;
                
            if (request.ComplianceFrameworks != null)
                existing.ComplianceFrameworks = request.ComplianceFrameworks;
                        existing.UpdatedBy = request.UpdatedBy ?? "admin";

            var updated = await _catalogService.UpdateTemplateAsync(
                existing, 
                existing.UpdatedBy, 
                cancellationToken);

            // Auto-sync from Git if source was added/updated
            if (request.GitSource != null && !string.IsNullOrEmpty(request.GitSource.RepositoryUrl) && _gitSyncService != null)
            {
                try
                {
                    _logger.LogInformation("Auto-syncing template {TemplateId} from Git source after update", id);
                    if (Guid.TryParse(id, out var templateGuid))
                    {
                        await _gitSyncService.SyncTemplateAsync(templateGuid, force: true, cancellationToken);
                        // Refresh the template to get synced content
                        updated = await _catalogService.GetTemplateAsync(id, cancellationToken) ?? updated;
                    }
                }
                catch (Exception syncEx)
                {
                    _logger.LogWarning(syncEx, "Failed to auto-sync template {TemplateId} from Git after update.", id);
                }
            }

            _logger.LogInformation("Updated template {TemplateId}", id);

            return Ok(MapToDto(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {TemplateId}", id);
            return StatusCode(500, new { error = "Failed to update template" });
        }
    }

    /// <summary>
    /// Delete a service template
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteTemplate(
        string id,
        [FromQuery] string deletedBy = "admin",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _catalogService.DeleteTemplateAsync(id, deletedBy, cancellationToken);
            if (!success)
                return NotFound(new { error = $"Template {id} not found" });

            _logger.LogInformation("Deleted template {TemplateId}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {TemplateId}", id);
            return StatusCode(500, new { error = "Failed to delete template" });
        }
    }

    /// <summary>
    /// Submit template for approval
    /// </summary>
    [HttpPost("{id}/submit-for-approval")]
    [ProducesResponseType(typeof(ServiceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceTemplateDto>> SubmitForApproval(
        string id,
        [FromQuery] string submittedBy = "admin",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await _catalogService.SubmitForApprovalAsync(id, submittedBy, cancellationToken);
            if (template == null)
                return NotFound(new { error = $"Template {id} not found" });

            _logger.LogInformation("Template {TemplateId} submitted for approval by {User}", id, submittedBy);
            return Ok(MapToDto(template));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting template {TemplateId} for approval", id);
            return StatusCode(500, new { error = "Failed to submit for approval" });
        }
    }

    /// <summary>
    /// Approve and publish a template
    /// </summary>
    [HttpPost("{id}/approve")]
    [ProducesResponseType(typeof(ServiceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ServiceTemplateDto>> ApproveTemplate(
        string id,
        [FromBody] ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var approvalInfo = new ApprovalInfo
            {
                Source = Enum.TryParse<ApprovalSource>(request.Source, out var source) 
                    ? source : ApprovalSource.Internal,
                ApprovedBy = request.ApprovedBy ?? "admin",
                ApprovedAt = DateTime.UtcNow,
                ApprovalComments = request.Comments,
                ExternalApprovalId = request.ExternalApprovalId,
                ExternalApprovalUrl = request.ExternalApprovalUrl
            };

            var template = await _catalogService.ApproveTemplateAsync(
                id, 
                approvalInfo.ApprovedBy, 
                approvalInfo.ApprovalComments,
                approvalInfo.Source,
                approvalInfo.ExternalApprovalId,
                cancellationToken);
                
            if (template == null)
                return NotFound(new { error = $"Template {id} not found" });

            _logger.LogInformation("Template {TemplateId} approved by {User}", id, approvalInfo.ApprovedBy);
            return Ok(MapToDto(template));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving template {TemplateId}", id);
            return StatusCode(500, new { error = "Failed to approve template" });
        }
    }

    /// <summary>
    /// Deprecate a template
    /// </summary>
    [HttpPost("{id}/deprecate")]
    [ProducesResponseType(typeof(ServiceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceTemplateDto>> DeprecateTemplate(
        string id,
        [FromQuery] string deprecatedBy = "admin",
        [FromQuery] string? reason = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await _catalogService.DeprecateTemplateAsync(
                id, 
                deprecatedBy, 
                reason, 
                cancellationToken);
                
            if (template == null)
                return NotFound(new { error = $"Template {id} not found" });

            _logger.LogInformation("Template {TemplateId} deprecated by {User}", id, deprecatedBy);
            return Ok(MapToDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deprecating template {TemplateId}", id);
            return StatusCode(500, new { error = "Failed to deprecate template" });
        }
    }

    /// <summary>
    /// Get template categories
    /// </summary>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetCategories(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var categories = await _catalogService.GetCategoriesAsync(cancellationToken);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving categories");
            return StatusCode(500, new { error = "Failed to retrieve categories" });
        }
    }

    /// <summary>
    /// Validate template content
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidationResultDto>> ValidateTemplate(
        [FromBody] ValidateTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic validation - more comprehensive validation could be added
            var errors = new List<string>();
            var warnings = new List<string>();

            if (string.IsNullOrWhiteSpace(request.Name))
                errors.Add("Template name is required");

            if (string.IsNullOrWhiteSpace(request.TemplateContent))
                errors.Add("Template content is required");
            else
            {
                // Basic format validation
                var format = Enum.TryParse<TemplateFormat>(request.Format, out var f) ? f : TemplateFormat.Bicep;
                if (format == TemplateFormat.Bicep && !request.TemplateContent.Contains("param") && !request.TemplateContent.Contains("resource"))
                    warnings.Add("Template doesn't appear to contain Bicep syntax (no 'param' or 'resource' declarations found)");
                else if (format == TemplateFormat.ARM && !request.TemplateContent.Contains("$schema"))
                    warnings.Add("Template doesn't appear to be a valid ARM template (no '$schema' found)");
                else if (format == TemplateFormat.Terraform && !request.TemplateContent.Contains("resource") && !request.TemplateContent.Contains("provider"))
                    warnings.Add("Template doesn't appear to contain Terraform syntax");
            }

            await Task.CompletedTask; // Placeholder for async validation
            
            return Ok(new ValidationResultDto
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                Warnings = warnings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating template");
            return StatusCode(500, new { error = "Failed to validate template" });
        }
    }

    #region Natural Language Matching

    /// <summary>
    /// Match templates using natural language description
    /// </summary>
    [HttpPost("match")]
    [ProducesResponseType(typeof(TemplateMatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TemplateMatchResultDto>> MatchTemplates(
        [FromBody] NaturalLanguageMatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_nlMatchingService == null)
        {
            return StatusCode(503, new { error = "Natural language matching service is not configured" });
        }

        try
        {
            _logger.LogInformation("🔍 NL template match request: {Request}", 
                request.Description?.Length > 50 ? request.Description[..50] + "..." : request.Description);

            var options = new TemplateMatchOptions
            {
                MinimumScore = request.MinimumScore ?? 0.3,
                MaxResults = request.MaxResults ?? 5,
                Category = request.Category,
                RequiredCompliance = request.RequiredCompliance
            };

            var result = await _nlMatchingService.MatchTemplatesAsync(
                request.Description ?? "", options, cancellationToken);

            return Ok(new TemplateMatchResultDto
            {
                Success = result.Success,
                UserRequest = result.UserRequest,
                Message = result.Message,
                UsedLlm = result.UsedLlm,
                Matches = result.Matches.Select(m => new TemplateMatchDto
                {
                    TemplateId = m.TemplateId,
                    TemplateName = m.TemplateName,
                    DisplayName = m.DisplayName,
                    Category = m.Category,
                    Score = m.Score,
                    Reasoning = m.Reasoning,
                    SuggestedParameters = m.SuggestedParameters
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error matching templates");
            return StatusCode(500, new { error = "Failed to match templates" });
        }
    }

    /// <summary>
    /// Extract parameter values from natural language request
    /// </summary>
    [HttpPost("{id}/extract-parameters")]
    [ProducesResponseType(typeof(ParameterExtractionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ParameterExtractionResultDto>> ExtractParameters(
        string id,
        [FromBody] ExtractParametersRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_nlMatchingService == null)
        {
            return StatusCode(503, new { error = "Natural language matching service is not configured" });
        }

        try
        {
            var template = await _catalogService.GetTemplateAsync(id, cancellationToken);
            if (template == null)
                return NotFound(new { error = $"Template {id} not found" });

            var result = await _nlMatchingService.ExtractParametersAsync(
                request.UserRequest ?? "", template, cancellationToken);

            return Ok(new ParameterExtractionResultDto
            {
                Success = result.Success,
                TemplateId = result.TemplateId,
                TemplateName = result.TemplateName,
                ExtractedParameters = result.ExtractedParameters.ToDictionary(
                    kvp => kvp.Key,
                    kvp => new ExtractedParameterDto
                    {
                        ParameterName = kvp.Value.ParameterName,
                        SuggestedValue = kvp.Value.SuggestedValue,
                        Confidence = kvp.Value.Confidence,
                        Source = kvp.Value.Source,
                        Reasoning = kvp.Value.Reasoning
                    })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting parameters for template {TemplateId}", id);
            return StatusCode(500, new { error = "Failed to extract parameters" });
        }
    }

    /// <summary>
    /// Explain why a template matches a request
    /// </summary>
    [HttpPost("{id}/explain-match")]
    [ProducesResponseType(typeof(ExplainMatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExplainMatchResultDto>> ExplainMatch(
        string id,
        [FromBody] ExplainMatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_nlMatchingService == null)
        {
            return StatusCode(503, new { error = "Natural language matching service is not configured" });
        }

        try
        {
            var template = await _catalogService.GetTemplateAsync(id, cancellationToken);
            if (template == null)
                return NotFound(new { error = $"Template {id} not found" });

            var explanation = await _nlMatchingService.ExplainMatchAsync(
                request.UserRequest ?? "", template, cancellationToken);

            return Ok(new ExplainMatchResultDto
            {
                TemplateId = id,
                TemplateName = template.Name,
                Explanation = explanation
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining match for template {TemplateId}", id);
            return StatusCode(500, new { error = "Failed to explain match" });
        }
    }

    #endregion

    #region Git Sync

    /// <summary>
    /// Import a template from a Git repository
    /// </summary>
    [HttpPost("import-from-git")]
    [ProducesResponseType(typeof(GitImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GitImportResultDto>> ImportFromGit(
        [FromBody] GitImportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_gitSyncService == null)
        {
            return StatusCode(503, new { error = "Git sync service is not configured" });
        }

        try
        {
            _logger.LogInformation("📥 Importing template from Git: {Url}", request.RepositoryUrl);

            var result = await _gitSyncService.ImportFromGitAsync(
                request.RepositoryUrl ?? "",
                request.Branch ?? "main",
                request.Path ?? "main.bicep",
                request.ImportedBy ?? "api",
                cancellationToken);

            return Ok(new GitImportResultDto
            {
                Success = result.Success,
                TemplateId = result.TemplateId,
                TemplateName = result.TemplateName,
                Message = result.Message,
                CommitSha = result.CommitSha
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing template from Git");
            return StatusCode(500, new { error = "Failed to import template" });
        }
    }

    /// <summary>
    /// Sync a template from its Git source
    /// </summary>
    [HttpPost("{id}/sync")]
    [ProducesResponseType(typeof(GitSyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GitSyncResultDto>> SyncFromGit(
        string id,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (_gitSyncService == null)
        {
            return StatusCode(503, new { error = "Git sync service is not configured" });
        }

        try
        {
            if (!Guid.TryParse(id, out var templateId))
                return BadRequest(new { error = "Invalid template ID format" });

            var result = await _gitSyncService.SyncTemplateAsync(templateId, force, cancellationToken);

            return Ok(new GitSyncResultDto
            {
                Success = result.Success,
                TemplateId = result.TemplateId,
                WasUpdated = result.WasUpdated,
                CommitSha = result.CommitSha,
                Message = result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing template {TemplateId}", id);
            return StatusCode(500, new { error = "Failed to sync template" });
        }
    }

    /// <summary>
    /// Sync all templates from Git
    /// </summary>
    [HttpPost("sync-all")]
    [ProducesResponseType(typeof(GitSyncBatchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GitSyncBatchResultDto>> SyncAllFromGit(
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (_gitSyncService == null)
        {
            return StatusCode(503, new { error = "Git sync service is not configured" });
        }

        try
        {
            _logger.LogInformation("🔄 Syncing all templates from Git (force: {Force})", force);

            var result = await _gitSyncService.SyncAllTemplatesAsync(force, cancellationToken);

            return Ok(new GitSyncBatchResultDto
            {
                Success = result.Success,
                Message = result.Message,
                UpdatedCount = result.Updated.Count,
                UnchangedCount = result.Unchanged.Count,
                SkippedCount = result.Skipped.Count,
                FailedCount = result.Failed.Count,
                Failures = result.Failed.Select(f => new GitSyncFailureDto
                {
                    TemplateId = f.TemplateId,
                    TemplateName = f.TemplateName,
                    Error = f.Error
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing all templates");
            return StatusCode(500, new { error = "Failed to sync templates" });
        }
    }

    /// <summary>
    /// Check if a template has changes in Git
    /// </summary>
    [HttpGet("{id}/git-status")]
    [ProducesResponseType(typeof(GitDiffResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<GitDiffResultDto>> CheckGitStatus(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (_gitSyncService == null)
        {
            return StatusCode(503, new { error = "Git sync service is not configured" });
        }

        try
        {
            if (!Guid.TryParse(id, out var templateId))
                return BadRequest(new { error = "Invalid template ID format" });

            var result = await _gitSyncService.CheckForChangesAsync(templateId, cancellationToken);

            return Ok(new GitDiffResultDto
            {
                HasChanges = result.HasChanges,
                CurrentSha = result.CurrentSha,
                LatestSha = result.LatestSha,
                LastSynced = result.LastSynced,
                Message = result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Git status for template {TemplateId}", id);
            return StatusCode(500, new { error = "Failed to check Git status" });
        }
    }

    /// <summary>
    /// Reset parameter override flag and resync parameters from Git.
    /// Use this when you want to discard manual parameter edits and restore parameters from the Git source.
    /// </summary>
    [HttpPost("{id}/reset-parameters")]
    [ProducesResponseType(typeof(ServiceTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceTemplateDto>> ResetParametersFromGit(
        string id,
        CancellationToken cancellationToken = default)
    {
        if (_gitSyncService == null)
        {
            return StatusCode(503, new { error = "Git sync service is not configured" });
        }

        try
        {
            if (!Guid.TryParse(id, out var templateId))
                return BadRequest(new { error = "Invalid template ID format" });

            var existing = await _catalogService.GetTemplateAsync(id, cancellationToken);
            if (existing == null)
                return NotFound(new { error = $"Template {id} not found" });

            if (existing.GitSource == null || string.IsNullOrEmpty(existing.GitSource.RepositoryUrl))
                return BadRequest(new { error = "Template has no Git source configured" });

            // Reset the override flag
            existing.ParametersOverridden = false;
            existing.UpdatedBy = "ParameterReset";
            await _catalogService.UpdateTemplateAsync(existing, "ParameterReset", cancellationToken);

            // Force sync from Git to restore parameters
            _logger.LogInformation("🔄 Resetting parameters for template {TemplateId} from Git", id);
            var syncResult = await _gitSyncService.SyncTemplateAsync(templateId, force: true, cancellationToken);

            if (!syncResult.Success)
            {
                return StatusCode(500, new { error = $"Failed to sync parameters from Git: {syncResult.Message}" });
            }

            // Get the updated template
            var updated = await _catalogService.GetTemplateAsync(id, cancellationToken);
            if (updated == null)
                return NotFound(new { error = $"Template {id} not found after reset" });

            _logger.LogInformation("✅ Parameters reset for template {TemplateId} from Git", id);

            return Ok(MapToDto(updated));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting parameters for template {TemplateId}", id);
            return StatusCode(500, new { error = "Failed to reset parameters" });
        }
    }

    #endregion

    private static ServiceTemplateDto MapToDto(ServiceTemplate template)
    {
        return new ServiceTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            DisplayName = template.DisplayName,
            Description = template.Description,
            Version = template.Version,
            Category = template.Category,
            Format = template.Format.ToString(),
            TemplateContent = template.MainTemplateContent,
            Status = template.Status.ToString(),
            DeploymentScope = template.DeploymentScope ?? "resourceGroup",
            RequiresApproval = template.RequiresApproval,
            EnforceCompliance = template.EnforceCompliance,
            DefaultExpirationDays = template.DefaultExpirationDays,
            ComplianceFrameworks = template.ComplianceFrameworks,
            Keywords = template.Keywords,
            UseCases = template.UseCases,
            AiSelectionHint = template.AiSelectionHint,
            DeploymentCount = template.DeploymentCount,
            CreatedAt = template.CreatedAt,
            CreatedBy = template.CreatedBy,
            UpdatedAt = template.UpdatedAt,
            UpdatedBy = template.UpdatedBy,
            // Git Sync properties
            HasGitSource = template.GitSource != null,
            GitRepositoryUrl = template.GitSource?.RepositoryUrl,
            GitBranch = template.GitSource?.Branch,
            GitPath = template.GitSource?.Path,
            GitCommitSha = template.GitCommitSha,
            LastSyncedFromGit = template.LastSyncedFromGit,
            GitAutoSync = template.GitSource?.AutoSync ?? false,
            GitSyncIntervalMinutes = template.GitSource?.SyncIntervalMinutes ?? 15,
            ParametersOverridden = template.ParametersOverridden,
            // Additional files (Bicep modules)
            AdditionalFiles = template.AdditionalFiles?.Select(f => new TemplateFileDto
            {
                FileName = f.FileName,
                RelativePath = f.RelativePath,
                Content = f.Content,
                FileType = f.FileType
            }).ToList() ?? new List<TemplateFileDto>(),
            Approval = template.Approval != null ? new ApprovalInfoDto
            {
                Source = template.Approval.Source.ToString(),
                ApprovedBy = template.Approval.ApprovedBy,
                ApprovedAt = template.Approval.ApprovedAt,
                Comments = template.Approval.ApprovalComments
            } : null,
            Parameters = template.Parameters.Select(p => new TemplateParameterDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Type = p.Type.ToString(),
                Required = p.Required,
                DefaultValue = p.DefaultValue,
                AllowedValues = p.AllowedValues?.Cast<object>().ToList(),
                MinValue = p.MinValue,
                MaxValue = p.MaxValue,
                DisplayOrder = p.DisplayOrder
            }).ToList(),
            Guardrails = template.Guardrails.Select(g => new TemplateGuardrailDto
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                Type = g.Type.ToString(),
                Property = g.Property,
                Operator = g.Operator,
                Value = g.Value?.ToString() ?? string.Empty,
                Action = g.Action.ToString(),
                ErrorMessage = g.ErrorMessage
            }).ToList()
        };
    }

    /// <summary>
    /// Parse Bicep template content and extract parameter definitions
    /// </summary>
    [HttpPost("parse-bicep-parameters")]
    [ProducesResponseType(typeof(List<TemplateParameterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<List<TemplateParameterDto>> ParseBicepParameters([FromBody] ParseBicepParametersRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.BicepContent))
            {
                return BadRequest("Bicep content is required");
            }

            var parameters = BicepParameterParser.ParseParameters(request.BicepContent);

            var dtos = parameters.Select((p, index) => new TemplateParameterDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Type = p.Type.ToString(),
                Required = p.Required,
                DefaultValue = p.DefaultValue,
                AllowedValues = p.AllowedValues?.Cast<object>().ToList(),
                MinValue = p.MinValue,
                MaxValue = p.MaxValue,
                DisplayOrder = index
            }).ToList();

            _logger.LogInformation("Parsed {Count} parameters from Bicep template", dtos.Count);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Bicep parameters");
            return BadRequest($"Error parsing Bicep parameters: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse Bicep parameters from a Git repository URL
    /// </summary>
    [HttpPost("parse-bicep-parameters-from-git")]
    [ProducesResponseType(typeof(List<TemplateParameterDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<List<TemplateParameterDto>>> ParseBicepParametersFromGit(
        [FromBody] ParseBicepParametersFromGitRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_gitSyncService == null)
        {
            return StatusCode(503, new { error = "Git sync service is not configured" });
        }

        try
        {
            _logger.LogInformation("📥 Fetching Bicep template from Git: {Url}/{Path}", request.RepositoryUrl, request.Path);

            // Fetch the Bicep content from Git
            var content = await _gitSyncService.FetchFileContentAsync(
                request.RepositoryUrl ?? "",
                request.Branch ?? "main",
                request.Path ?? "",
                cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("Failed to fetch file content from Git repository");
            }

            // Parse parameters from the fetched content
            var parameters = BicepParameterParser.ParseParameters(content);

            var dtos = parameters.Select((p, index) => new TemplateParameterDto
            {
                Name = p.Name,
                DisplayName = p.DisplayName,
                Description = p.Description,
                Type = p.Type.ToString(),
                Required = p.Required,
                DefaultValue = p.DefaultValue,
                AllowedValues = p.AllowedValues?.Cast<object>().ToList(),
                MinValue = p.MinValue,
                MaxValue = p.MaxValue,
                DisplayOrder = index
            }).ToList();

            _logger.LogInformation("Parsed {Count} parameters from Git Bicep template", dtos.Count);
            return Ok(dtos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Bicep parameters from Git");
            return BadRequest($"Error parsing Bicep parameters from Git: {ex.Message}");
        }
    }
}

/// <summary>
/// Request model for parsing Bicep parameters
/// </summary>
public record ParseBicepParametersRequest
{
    public required string BicepContent { get; init; }
}

/// <summary>
/// Request model for parsing Bicep parameters from Git
/// </summary>
public record ParseBicepParametersFromGitRequest
{
    public string? RepositoryUrl { get; init; }
    public string? Branch { get; init; }
    public string? Path { get; init; }
}
