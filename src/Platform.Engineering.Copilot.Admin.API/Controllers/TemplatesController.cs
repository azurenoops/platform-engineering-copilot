using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Admin.API.Models;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Admin.API.Controllers;

/// <summary>
/// Service template catalog: CRUD, approval workflow, validation, NL matching, Git sync.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TemplatesController : ControllerBase
{
    private readonly IServiceTemplateCatalogService _catalogService;
    private readonly INaturalLanguageTemplateMatchingService _nlMatchingService;
    private readonly IGitTemplateSyncService _gitSyncService;
    private readonly BicepParameterParser _bicepParser;
    private readonly ILogger<TemplatesController> _logger;

    public TemplatesController(
        IServiceTemplateCatalogService catalogService,
        INaturalLanguageTemplateMatchingService nlMatchingService,
        IGitTemplateSyncService gitSyncService,
        BicepParameterParser bicepParser,
        ILogger<TemplatesController> logger)
    {
        _catalogService = catalogService;
        _nlMatchingService = nlMatchingService;
        _gitSyncService = gitSyncService;
        _bicepParser = bicepParser;
        _logger = logger;
    }

    // ── CRUD ──

    [HttpGet]
    [Authorize(Policy = "Engineer")]
    public async Task<IActionResult> GetTemplates(
        [FromQuery] string? category, [FromQuery] string? status, [FromQuery] string? search,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var (items, _) = await _catalogService.GetAllAsync(category, status, search, skip, take, cancellationToken);
            return Ok(items.Select(MapToSummaryDto).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing templates");
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Engineer")]
    public async Task<IActionResult> GetTemplate(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var template = await _catalogService.GetByIdAsync(id, cancellationToken);
            if (template is null) return NotFound(new { error = "NotFound", message = "Template not found." });

            Response.Headers["ETag"] = $"\"{Convert.ToBase64String(template.RowVersion ?? [])}\"";
            return Ok(MapToDetailDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template {TemplateId}", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    [HttpGet("by-name/{name}")]
    [Authorize(Policy = "Engineer")]
    public async Task<IActionResult> GetTemplateByName(string name, [FromQuery] string? version, CancellationToken cancellationToken)
    {
        try
        {
            var template = await _catalogService.GetByNameAsync(name, version, cancellationToken);
            if (template is null) return NotFound(new { error = "NotFound", message = "Template not found." });

            Response.Headers["ETag"] = $"\"{Convert.ToBase64String(template.RowVersion ?? [])}\"";
            return Ok(MapToDetailDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting template by name {Name}", name);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    [HttpGet("categories")]
    [Authorize(Policy = "Engineer")]
    public async Task<IActionResult> GetCategories(CancellationToken cancellationToken)
    {
        try
        {
            var categories = await _catalogService.GetCategoriesAsync(cancellationToken);
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting categories");
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var template = new ServiceTemplate
            {
                Name = request.Name,
                DisplayName = request.DisplayName,
                Description = request.Description ?? string.Empty,
                Version = request.Version ?? "1.0.0",
                Category = request.Category ?? "General",
                Format = Enum.TryParse<TemplateFormat>(request.Format, true, out var fmt) ? fmt : TemplateFormat.Bicep,
                Content = request.Content,
                DeploymentScope = request.DeploymentScope,
                ParametersJson = request.ParametersJson,
                GuardrailsJson = request.GuardrailsJson,
                ComplianceFrameworks = request.ComplianceFrameworks,
                Keywords = request.Keywords,
                UseCases = request.UseCases,
                AiSelectionHints = request.AiSelectionHints,
                RequiresApproval = request.RequiresApproval,
                GitRepoUrl = request.GitRepoUrl,
                GitBranch = request.GitBranch,
                GitPath = request.GitPath,
                GitAutoSync = request.GitAutoSync,
                GitSyncIntervalMinutes = request.GitSyncIntervalMinutes ?? 60
            };

            var created = await _catalogService.CreateAsync(template, cancellationToken);
            var dto = MapToDetailDto(created);

            Response.Headers["ETag"] = $"\"{Convert.ToBase64String(created.RowVersion ?? [])}\"";
            return CreatedAtAction(nameof(GetTemplate), new { id = created.TemplateId }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "ValidationError", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating template");
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> UpdateTemplate(Guid id, [FromBody] UpdateTemplateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var template = await _catalogService.GetByIdAsync(id, cancellationToken);
            if (template is null) return NotFound(new { error = "NotFound", message = "Template not found." });

            // Apply If-Match ETag for concurrency
            if (Request.Headers.TryGetValue("If-Match", out var ifMatch))
            {
                var etagValue = ifMatch.ToString().Trim('"');
                template.RowVersion = Convert.FromBase64String(etagValue);
            }

            // Partial update — only non-null fields
            if (request.Name is not null) template.Name = request.Name;
            if (request.DisplayName is not null) template.DisplayName = request.DisplayName;
            if (request.Description is not null) template.Description = request.Description;
            if (request.Version is not null) template.Version = request.Version;
            if (request.Category is not null) template.Category = request.Category;
            if (request.Format is not null && Enum.TryParse<TemplateFormat>(request.Format, true, out var fmt))
                template.Format = fmt;
            if (request.Content is not null) template.Content = request.Content;
            if (request.DeploymentScope is not null) template.DeploymentScope = request.DeploymentScope;
            if (request.ParametersJson is not null)
            {
                template.ParametersJson = request.ParametersJson;
                template.ParametersOverridden = true;
            }
            if (request.GuardrailsJson is not null) template.GuardrailsJson = request.GuardrailsJson;
            if (request.ComplianceFrameworks is not null) template.ComplianceFrameworks = request.ComplianceFrameworks;
            if (request.Keywords is not null) template.Keywords = request.Keywords;
            if (request.UseCases is not null) template.UseCases = request.UseCases;
            if (request.AiSelectionHints is not null) template.AiSelectionHints = request.AiSelectionHints;
            if (request.RequiresApproval.HasValue) template.RequiresApproval = request.RequiresApproval.Value;
            if (request.GitRepoUrl is not null) template.GitRepoUrl = request.GitRepoUrl;
            if (request.GitBranch is not null) template.GitBranch = request.GitBranch;
            if (request.GitPath is not null) template.GitPath = request.GitPath;
            if (request.GitAutoSync.HasValue) template.GitAutoSync = request.GitAutoSync.Value;
            if (request.GitSyncIntervalMinutes.HasValue) template.GitSyncIntervalMinutes = request.GitSyncIntervalMinutes.Value;

            var updated = await _catalogService.UpdateAsync(template, cancellationToken);
            Response.Headers["ETag"] = $"\"{Convert.ToBase64String(updated.RowVersion ?? [])}\"";
            return Ok(MapToDetailDto(updated));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { error = "ConcurrencyConflict", message = "The template was modified by another request. Refresh and retry." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating template {TemplateId}", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> DeleteTemplate(Guid id, [FromQuery] string deletedBy, CancellationToken cancellationToken)
    {
        try
        {
            await _catalogService.DeleteAsync(id, deletedBy, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "NotFound", message = "Template not found." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting template {TemplateId}", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    // ── Approval Workflow ──

    [HttpPost("{id:guid}/submit-for-approval")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> SubmitForApproval(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var template = await _catalogService.SubmitForApprovalAsync(id, cancellationToken);
            return Ok(MapToDetailDto(template));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "NotFound", message = "Template not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "InvalidTransition", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting template {TemplateId} for approval", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApprovalRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var template = await _catalogService.ApproveAsync(
                id, request.ApprovalSource, request.ApprovedBy,
                request.Comments, request.ExternalApprovalId, request.ExternalApprovalUrl,
                cancellationToken);
            return Ok(MapToDetailDto(template));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "NotFound", message = "Template not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "InvalidTransition", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving template {TemplateId}", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    [HttpPost("{id:guid}/deprecate")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Deprecate(Guid id, [FromQuery] string deprecatedBy, [FromQuery] string reason, CancellationToken cancellationToken)
    {
        try
        {
            var template = await _catalogService.DeprecateAsync(id, deprecatedBy, reason, cancellationToken);
            return Ok(MapToDetailDto(template));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "NotFound", message = "Template not found." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = "InvalidTransition", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deprecating template {TemplateId}", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    // ── Validation & Parsing ──

    /// <summary>POST /api/templates/validate — Validate template content without creating.</summary>
    [HttpPost("validate")]
    public async Task<IActionResult> Validate([FromBody] ValidateTemplateRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _bicepParser.ValidateAsync(request.Content ?? "", null, request.Format ?? "Bicep", cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating template");
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    /// <summary>POST /api/templates/parse-bicep-parameters — Extract params from Bicep content.</summary>
    [HttpPost("parse-bicep-parameters")]
    public async Task<IActionResult> ParseBicepParameters([FromBody] ParseBicepParametersRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.BicepContent))
            return BadRequest(new { error = "ValidationError", message = "bicepContent is required." });

        try
        {
            var result = await _bicepParser.ParseParametersAsync(request.BicepContent, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Bicep parameters");
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    /// <summary>POST /api/templates/parse-bicep-parameters-from-git — Extract params from Git repo.</summary>
    [HttpPost("parse-bicep-parameters-from-git")]
    public async Task<IActionResult> ParseBicepParametersFromGit([FromBody] ParseBicepFromGitRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GitRepoUrl))
            return BadRequest(new { error = "ValidationError", message = "gitRepoUrl is required." });

        try
        {
            var result = await _bicepParser.ParseFromGitAsync(request.GitRepoUrl, request.Branch, request.FilePath, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Bicep from Git");
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    // ── Natural Language Matching ──

    /// <summary>POST /api/templates/match — NL template matching.</summary>
    [HttpPost("match")]
    public async Task<IActionResult> Match([FromBody] TemplateMatchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "ValidationError", message = "description is required." });

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var result = await _nlMatchingService.MatchTemplatesAsync(request.Description, request.MinScore, request.MaxResults, cancellationToken);
            sw.Stop();

            return Ok(new { result, usedLlm = false, processingTimeMs = sw.ElapsedMilliseconds });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error matching templates");
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    /// <summary>POST /api/templates/{id}/extract-parameters — Extract param values from NL.</summary>
    [HttpPost("{id:guid}/extract-parameters")]
    public async Task<IActionResult> ExtractParameters(Guid id, [FromBody] ExtractParametersRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "ValidationError", message = "description is required." });

        try
        {
            var result = await _nlMatchingService.ExtractParametersAsync(id, request.Description, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(new { error = "NotFound", message = "Template not found." }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting parameters for template {TemplateId}", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    /// <summary>POST /api/templates/{id}/explain-match — Explain why template matches.</summary>
    [HttpPost("{id:guid}/explain-match")]
    public async Task<IActionResult> ExplainMatch(Guid id, [FromBody] ExplainMatchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest(new { error = "ValidationError", message = "description is required." });

        try
        {
            var result = await _nlMatchingService.ExplainMatchAsync(id, request.Description, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(new { error = "NotFound", message = "Template not found." }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error explaining match for template {TemplateId}", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    // ── Git Sync ──

    /// <summary>POST /api/templates/import-from-git — Import from Git repo.</summary>
    [HttpPost("import-from-git")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> ImportFromGit([FromBody] ImportFromGitRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.GitRepoUrl))
            return BadRequest(new { error = "ValidationError", message = "gitRepoUrl is required." });

        try
        {
            var template = await _gitSyncService.ImportFromGitAsync(
                request.GitRepoUrl, request.Branch, request.FilePath,
                request.Name, request.Category, request.GitAutoSync,
                request.GitSyncIntervalMinutes ?? 60, cancellationToken);

            return CreatedAtAction("GetTemplate", new { id = template.TemplateId }, MapToDetailDto(template));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing template from Git");
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    /// <summary>POST /api/templates/{id}/sync — Sync from Git source.</summary>
    [HttpPost("{id:guid}/sync")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> Sync(Guid id, [FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await _gitSyncService.SyncAsync(id, force, cancellationToken);
            return Ok(MapToDetailDto(template));
        }
        catch (KeyNotFoundException) { return NotFound(new { error = "NotFound", message = "Template not found." }); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = "InvalidOperation", message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing template {TemplateId} from Git", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    /// <summary>POST /api/templates/sync-all — Bulk-sync all Git-sourced templates.</summary>
    [HttpPost("sync-all")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> SyncAll(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _gitSyncService.SyncAllAsync(cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing all Git-sourced templates");
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    /// <summary>GET /api/templates/{id}/git-status — Check Git source status.</summary>
    [HttpGet("{id:guid}/git-status")]
    public async Task<IActionResult> GetGitStatus(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _gitSyncService.GetGitStatusAsync(id, cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException) { return NotFound(new { error = "NotFound", message = "Template not found." }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting Git status for template {TemplateId}", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    /// <summary>POST /api/templates/{id}/reset-parameters — Reset manually-overridden params.</summary>
    [HttpPost("{id:guid}/reset-parameters")]
    [Authorize(Policy = "Admin")]
    public async Task<IActionResult> ResetParameters(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var template = await _gitSyncService.ResetParametersAsync(id, cancellationToken);
            return Ok(MapToDetailDto(template));
        }
        catch (KeyNotFoundException) { return NotFound(new { error = "NotFound", message = "Template not found." }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting parameters for template {TemplateId}", id);
            return StatusCode(500, new { error = "InternalError", message = "An unexpected error occurred." });
        }
    }

    // ── Mapping ──

    private static TemplateSummaryDto MapToSummaryDto(ServiceTemplate t) => new()
    {
        TemplateId = t.TemplateId,
        Name = t.Name,
        DisplayName = t.DisplayName,
        Description = t.Description,
        Version = t.Version,
        Category = t.Category,
        Format = t.Format.ToString(),
        Status = t.Status.ToString(),
        DeploymentScope = t.DeploymentScope,
        HasGitSource = !string.IsNullOrEmpty(t.GitRepoUrl),
        GitRepositoryUrl = t.GitRepoUrl,
        LastSyncedFromGit = t.GitLastSyncAt,
        GitAutoSync = t.GitAutoSync,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };

    private static TemplateDetailDto MapToDetailDto(ServiceTemplate t) => new()
    {
        TemplateId = t.TemplateId,
        Name = t.Name,
        DisplayName = t.DisplayName,
        Description = t.Description,
        Version = t.Version,
        Category = t.Category,
        Format = t.Format.ToString(),
        Status = t.Status.ToString(),
        Content = t.Content,
        DeploymentScope = t.DeploymentScope,
        ParametersJson = t.ParametersJson,
        GuardrailsJson = t.GuardrailsJson,
        ComplianceFrameworks = t.ComplianceFrameworks,
        Keywords = t.Keywords,
        UseCases = t.UseCases,
        AiSelectionHints = t.AiSelectionHints,
        AdditionalFilesJson = t.AdditionalFilesJson,
        ParametersOverridden = t.ParametersOverridden,
        RequiresApproval = t.RequiresApproval,
        ApprovalSource = t.ApprovalSource,
        ApprovedBy = t.ApprovedBy,
        ApprovedAt = t.ApprovedAt,
        ApprovalComments = t.ApprovalComments,
        ExternalApprovalId = t.ExternalApprovalId,
        ExternalApprovalUrl = t.ExternalApprovalUrl,
        DeprecatedBy = t.DeprecatedBy,
        DeprecatedAt = t.DeprecatedAt,
        DeprecationReason = t.DeprecationReason,
        GitRepoUrl = t.GitRepoUrl,
        GitBranch = t.GitBranch,
        GitPath = t.GitPath,
        GitCommitSha = t.GitCommitSha,
        GitAutoSync = t.GitAutoSync,
        GitSyncIntervalMinutes = t.GitSyncIntervalMinutes,
        GitSyncStatus = t.GitSyncStatus.ToString(),
        GitLastSyncAt = t.GitLastSyncAt,
        CreatedAt = t.CreatedAt,
        CreatedBy = t.CreatedBy,
        UpdatedAt = t.UpdatedAt
    };
}
