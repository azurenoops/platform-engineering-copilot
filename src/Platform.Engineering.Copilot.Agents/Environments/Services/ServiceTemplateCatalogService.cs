using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Mappers;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Agents.Environments.Services;

/// <summary>
/// Database-backed implementation of the Service Template Catalog.
/// Uses EF Core for persistence. Git sync is handled by the dedicated GitTemplateSyncService.
/// </summary>
public class ServiceTemplateCatalogService : IServiceTemplateCatalogService
{
    private readonly ILogger<ServiceTemplateCatalogService> _logger;
    private readonly IServiceTemplateRepository _repository;

    public ServiceTemplateCatalogService(
        ILogger<ServiceTemplateCatalogService> logger,
        IServiceTemplateRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        // Note: Service template seeding is now handled by DatabaseSeeder at app startup
    }

    #region Service Template CRUD

    public async Task<ServiceTemplate?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        return entity?.ToModel();
    }

    public async Task<ServiceTemplate?> GetTemplateByNameAsync(string name, string? version = null, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByNameAsync(name, version, cancellationToken);
        return entity?.ToModel();
    }

    public async Task<List<ServiceTemplate>> SearchTemplatesAsync(TemplateSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var entities = await _repository.SearchAsync(
            keyword: criteria.Keyword,
            category: criteria.Category,
            status: criteria.Status?.ToString(),
            format: criteria.Format?.ToString(),
            includeDeprecated: criteria.IncludeDeprecated,
            skip: criteria.Skip,
            take: criteria.Take,
            cancellationToken: cancellationToken);

        var results = entities.ToModels().ToList();

        // Apply additional filters not supported by repository
        if (!string.IsNullOrEmpty(criteria.ComplianceFramework))
        {
            results = results
                .Where(t => t.ComplianceFrameworks.Contains(criteria.ComplianceFramework))
                .ToList();
        }

        return results;
    }

    public async Task<List<ServiceTemplate>> GetPublishedTemplatesAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _repository.GetPublishedAsync(cancellationToken);
        return entities.ToModels().ToList();
    }

    public async Task<ServiceTemplate> CreateTemplateAsync(ServiceTemplate template, string createdBy, CancellationToken cancellationToken = default)
    {
        template.Id = Guid.NewGuid().ToString();
        template.CreatedBy = createdBy;
        template.CreatedAt = DateTime.UtcNow;
        template.Status = TemplateStatus.Draft;

        var entity = template.ToEntity();
        await _repository.CreateAsync(entity, cancellationToken);
        
        await AddAuditEntryAsync(entity.Id, template.Name, "Created", createdBy, "Template created as draft", cancellationToken);

        _logger.LogInformation("📝 Created template {Name} (ID: {Id}) by {CreatedBy}",
            template.Name, template.Id, createdBy);

        return template;
    }

    public async Task<ServiceTemplate> UpdateTemplateAsync(ServiceTemplate template, string updatedBy, CancellationToken cancellationToken = default)
    {
        var existingEntity = await _repository.GetByIdAsync(template.Id, cancellationToken);
        if (existingEntity == null)
            throw new InvalidOperationException($"Template {template.Id} not found");

        var existingModel = existingEntity.ToModel();
        
        // If published, create new version and reset to draft
        if (existingModel.Status == TemplateStatus.Published)
        {
            template.VersionHistory.Add(new TemplateVersionInfo
            {
                Version = existingModel.Version,
                ChangedBy = updatedBy,
                ChangedAt = DateTime.UtcNow,
                ChangeDescription = "Updated to new version"
            });
            template.Status = TemplateStatus.Draft;
        }

        template.UpdatedBy = updatedBy;
        template.UpdatedAt = DateTime.UtcNow;

        var entity = template.ToEntity();
        await _repository.UpdateAsync(entity, cancellationToken);
        
        await AddAuditEntryAsync(entity.Id, template.Name, "Updated", updatedBy, "Template updated", cancellationToken);

        _logger.LogInformation("📝 Updated template {Name} by {UpdatedBy}", template.Name, updatedBy);

        return template;
    }

    public async Task<bool> DeleteTemplateAsync(string templateId, string deletedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        if (entity == null)
            return false;

        // Soft delete - archive the template (Status is used, no IsArchived property)
        entity.Status = TemplateStatus.Archived.ToString();
        entity.UpdatedBy = deletedBy;
        entity.UpdatedAt = DateTime.UtcNow;
        
        await _repository.UpdateAsync(entity, cancellationToken);
        await AddAuditEntryAsync(entity.Id, entity.Name, "Archived", deletedBy, "Template archived (soft delete)", cancellationToken);

        _logger.LogInformation("🗑️ Archived template {Name} by {DeletedBy}", entity.Name, deletedBy);

        return true;
    }

    #endregion

    #region Template Lifecycle

    public async Task<ServiceTemplate> SubmitForApprovalAsync(string templateId, string submittedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException($"Template {templateId} not found");

        entity.Status = TemplateStatus.PendingApproval.ToString();
        entity.UpdatedBy = submittedBy;
        entity.UpdatedAt = DateTime.UtcNow;
        
        await _repository.UpdateAsync(entity, cancellationToken);
        await AddAuditEntryAsync(entity.Id, entity.Name, "SubmittedForApproval", submittedBy, "Template submitted for approval", cancellationToken);

        _logger.LogInformation("📤 Template {Name} submitted for approval by {SubmittedBy}", entity.Name, submittedBy);

        return entity.ToModel();
    }

    public async Task<ServiceTemplate> ApproveTemplateAsync(string templateId, string approvedBy, string? comments = null,
        ApprovalSource source = ApprovalSource.Internal, string? externalApprovalId = null, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException($"Template {templateId} not found");

        entity.ApprovalSource = source.ToString();
        entity.ApprovedBy = approvedBy;
        entity.ApprovedAt = DateTime.UtcNow;
        entity.ApprovalComments = comments;
        entity.ExternalApprovalId = externalApprovalId;
        entity.UpdatedBy = approvedBy;
        entity.UpdatedAt = DateTime.UtcNow;
        
        await _repository.UpdateAsync(entity, cancellationToken);
        await AddAuditEntryAsync(entity.Id, entity.Name, "Approved", approvedBy, 
            $"Template approved via {source}. {comments}", cancellationToken);

        _logger.LogInformation("✅ Template {Name} approved by {ApprovedBy} via {Source}",
            entity.Name, approvedBy, source);

        return entity.ToModel();
    }

    public async Task<ServiceTemplate> RejectTemplateAsync(string templateId, string rejectedBy, string reason, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException($"Template {templateId} not found");

        entity.Status = TemplateStatus.Draft.ToString();  // Back to draft for revision
        entity.UpdatedBy = rejectedBy;
        entity.UpdatedAt = DateTime.UtcNow;
        
        await _repository.UpdateAsync(entity, cancellationToken);
        await AddAuditEntryAsync(entity.Id, entity.Name, "Rejected", rejectedBy, 
            $"Approval rejected: {reason}", cancellationToken);

        _logger.LogInformation("❌ Template {Name} rejected by {RejectedBy}: {Reason}",
            entity.Name, rejectedBy, reason);

        return entity.ToModel();
    }

    public async Task<ServiceTemplate> PublishTemplateAsync(string templateId, string publishedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException($"Template {templateId} not found");

        if (entity.RequiresApproval && string.IsNullOrEmpty(entity.ApprovedBy))
            throw new InvalidOperationException("Template requires approval before publishing");

        entity.Status = TemplateStatus.Published.ToString();
        entity.UpdatedBy = publishedBy;
        entity.UpdatedAt = DateTime.UtcNow;
        
        await _repository.UpdateAsync(entity, cancellationToken);
        await AddAuditEntryAsync(entity.Id, entity.Name, "Published", publishedBy, 
            "Template published and available for use", cancellationToken);

        _logger.LogInformation("🚀 Template {Name} published by {PublishedBy}", entity.Name, publishedBy);

        return entity.ToModel();
    }

    public async Task<ServiceTemplate> DeprecateTemplateAsync(string templateId, string deprecatedBy, string? reason = null, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException($"Template {templateId} not found");

        entity.Status = TemplateStatus.Deprecated.ToString();
        entity.UpdatedBy = deprecatedBy;
        entity.UpdatedAt = DateTime.UtcNow;
        
        await _repository.UpdateAsync(entity, cancellationToken);
        await AddAuditEntryAsync(entity.Id, entity.Name, "Deprecated", deprecatedBy, 
            $"Template deprecated. {reason}", cancellationToken);

        _logger.LogWarning("⚠️ Template {Name} deprecated by {DeprecatedBy}: {Reason}",
            entity.Name, deprecatedBy, reason);

        return entity.ToModel();
    }

    public async Task<ServiceTemplate> CloneTemplateAsync(string templateId, string newName, string clonedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException($"Template {templateId} not found");

        var original = entity.ToModel();
        
        // Deep clone using JSON serialization
        var cloned = JsonSerializer.Deserialize<ServiceTemplate>(
            JsonSerializer.Serialize(original))!;

        cloned.Id = Guid.NewGuid().ToString();
        cloned.Name = newName;
        cloned.DisplayName = $"{original.DisplayName} (Clone)";
        cloned.Status = TemplateStatus.Draft;
        cloned.CreatedBy = clonedBy;
        cloned.CreatedAt = DateTime.UtcNow;
        cloned.UpdatedBy = null;
        cloned.UpdatedAt = null;
        cloned.Approval = null;
        cloned.DeploymentCount = 0;
        cloned.VersionHistory = new List<TemplateVersionInfo>
        {
            new() { Version = "1.0.0", ChangedBy = clonedBy, ChangedAt = DateTime.UtcNow, ChangeDescription = $"Cloned from {original.Name}" }
        };

        var clonedEntity = cloned.ToEntity();
        await _repository.CreateAsync(clonedEntity, cancellationToken);
        await AddAuditEntryAsync(clonedEntity.Id, cloned.Name, "Created", clonedBy, 
            $"Cloned from template {original.Name} ({templateId})", cancellationToken);

        _logger.LogInformation("📋 Template {SourceName} cloned to {NewName} by {ClonedBy}",
            original.Name, newName, clonedBy);

        return cloned;
    }

    #endregion

    #region Validation

    public async Task<List<string>> ValidateParametersAsync(string templateId, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        var template = await GetTemplateAsync(templateId, cancellationToken);
        if (template == null)
        {
            errors.Add($"Template {templateId} not found");
            return errors;
        }

        foreach (var param in template.Parameters)
        {
            var hasValue = parameters.TryGetValue(param.Name, out var value);

            // Required check
            if (param.Required && (!hasValue || value == null || string.IsNullOrEmpty(value.ToString())))
            {
                errors.Add($"Parameter '{param.DisplayName}' is required");
                continue;
            }

            if (!hasValue || value == null) continue;

            // Type-specific validation
            switch (param.Type)
            {
                case ParameterType.Number:
                    if (!double.TryParse(value.ToString(), out var numValue))
                    {
                        errors.Add($"Parameter '{param.DisplayName}' must be a number");
                    }
                    else
                    {
                        if (param.MinValue.HasValue && numValue < param.MinValue)
                            errors.Add($"Parameter '{param.DisplayName}' must be at least {param.MinValue}");
                        if (param.MaxValue.HasValue && numValue > param.MaxValue)
                            errors.Add($"Parameter '{param.DisplayName}' must be at most {param.MaxValue}");
                    }
                    break;

                case ParameterType.Choice:
                    if (param.AllowedValues != null && !param.AllowedValues.Contains(value.ToString()))
                        errors.Add($"Parameter '{param.DisplayName}' must be one of: {string.Join(", ", param.AllowedValues)}");
                    break;

                case ParameterType.String:
                    var strValue = value.ToString() ?? "";
                    if (param.MinLength.HasValue && strValue.Length < param.MinLength)
                        errors.Add($"Parameter '{param.DisplayName}' must be at least {param.MinLength} characters");
                    if (param.MaxLength.HasValue && strValue.Length > param.MaxLength)
                        errors.Add($"Parameter '{param.DisplayName}' must be at most {param.MaxLength} characters");
                    if (!string.IsNullOrEmpty(param.ValidationRegex))
                    {
                        if (!System.Text.RegularExpressions.Regex.IsMatch(strValue, param.ValidationRegex))
                            errors.Add($"Parameter '{param.DisplayName}' has invalid format");
                    }
                    break;
            }
        }

        return errors;
    }

    public async Task<List<GuardrailViolation>> CheckGuardrailsAsync(string templateId, Dictionary<string, object> parameters, CancellationToken cancellationToken = default)
    {
        var violations = new List<GuardrailViolation>();

        var template = await GetTemplateAsync(templateId, cancellationToken);
        if (template == null)
            return violations;

        foreach (var guardrail in template.Guardrails.Where(g => g.Enabled))
        {
            if (!parameters.TryGetValue(guardrail.Property, out var value))
                continue;

            var isViolation = CheckGuardrailViolation(guardrail, value);
            if (isViolation)
            {
                violations.Add(new GuardrailViolation
                {
                    GuardrailId = guardrail.Id,
                    GuardrailName = guardrail.Name,
                    Property = guardrail.Property,
                    ProvidedValue = value,
                    RequiredValue = guardrail.Value,
                    Action = guardrail.Action,
                    Message = guardrail.ErrorMessage
                });
            }
        }

        return violations;
    }

    private bool CheckGuardrailViolation(TemplateGuardrail guardrail, object value)
    {
        var strValue = value.ToString();
        var guardValue = guardrail.Value.ToString();

        return guardrail.Operator.ToLowerInvariant() switch
        {
            "==" or "equals" => strValue != guardValue,
            "!=" or "notequals" => strValue == guardValue,
            "<=" or "lte" when double.TryParse(strValue, out var v) && double.TryParse(guardValue, out var g) => v > g,
            ">=" or "gte" when double.TryParse(strValue, out var v) && double.TryParse(guardValue, out var g) => v < g,
            "<" or "lt" when double.TryParse(strValue, out var v) && double.TryParse(guardValue, out var g) => v >= g,
            ">" or "gt" when double.TryParse(strValue, out var v) && double.TryParse(guardValue, out var g) => v <= g,
            "in" when guardrail.Value is IEnumerable<string> list => !list.Contains(strValue),
            "notin" when guardrail.Value is IEnumerable<string> list => list.Contains(strValue),
            _ => false
        };
    }

    #endregion

    #region AI/LLM Support

    public async Task<List<ServiceTemplate>> FindMatchingTemplatesAsync(string userRequest, int maxResults = 5, CancellationToken cancellationToken = default)
    {
        var templates = await GetPublishedTemplatesAsync(cancellationToken);
        
        var requestLower = userRequest.ToLowerInvariant();
        var keywords = requestLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var scored = templates
            .Select(t => new
            {
                Template = t,
                Score = CalculateMatchScore(t, keywords)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Template)
            .ToList();

        return scored;
    }

    private int CalculateMatchScore(ServiceTemplate template, string[] keywords)
    {
        var score = 0;

        foreach (var keyword in keywords)
        {
            // Name match (highest weight)
            if (template.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score += 10;

            // DisplayName match
            if (template.DisplayName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score += 8;

            // Category match
            if (template.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score += 7;

            // Keywords match
            if (template.Keywords.Any(k => k.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                score += 5;

            // UseCases match
            if (template.UseCases.Any(u => u.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                score += 5;

            // Description match
            if (template.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                score += 2;
        }

        return score;
    }

    public async Task<string> GetTemplateSummaryForAiAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var template = await GetTemplateAsync(templateId, cancellationToken);
        if (template == null)
            return $"Template {templateId} not found";

        var sb = new StringBuilder();
        sb.AppendLine($"**{template.DisplayName}** (ID: {template.Id})");
        sb.AppendLine($"Category: {template.Category} | Format: {template.Format}");
        sb.AppendLine($"Description: {template.Description}");
        
        if (template.Parameters.Any())
        {
            sb.AppendLine("Parameters:");
            foreach (var param in template.Parameters.OrderBy(p => p.DisplayOrder))
            {
                var required = param.Required ? "*" : "";
                sb.AppendLine($"  - {param.Name}{required}: {param.Description}");
            }
        }

        if (template.ComplianceFrameworks.Any())
            sb.AppendLine($"Compliance: {string.Join(", ", template.ComplianceFrameworks)}");

        return sb.ToString();
    }

    public async Task<string> GetAllTemplateSummariesForAiAsync(CancellationToken cancellationToken = default)
    {
        var templates = await GetPublishedTemplatesAsync(cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("# Available Service Templates\n");

        foreach (var category in templates.GroupBy(t => t.Category))
        {
            sb.AppendLine($"## {category.Key}\n");
            foreach (var template in category)
            {
                sb.AppendLine($"- **{template.DisplayName}** ({template.Id}): {template.Description}");
                sb.AppendLine($"  Keywords: {string.Join(", ", template.Keywords)}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    #endregion

    #region Categories & Metadata

    public async Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _repository.GetCategoriesAsync(cancellationToken);
        return categories.ToList();
    }

    public async Task<TemplateCatalogStats> GetCatalogStatsAsync(CancellationToken cancellationToken = default)
    {
        var countByStatus = await _repository.GetCountByStatusAsync(cancellationToken);
        var countByCategory = await _repository.GetCountByCategoryAsync(cancellationToken);
        var countByFormat = await _repository.GetCountByFormatAsync(cancellationToken);

        return new TemplateCatalogStats
        {
            TotalTemplates = countByStatus.Values.Sum(),
            PublishedTemplates = countByStatus.GetValueOrDefault("Published", 0),
            DraftTemplates = countByStatus.GetValueOrDefault("Draft", 0),
            PendingApprovalTemplates = countByStatus.GetValueOrDefault("PendingApproval", 0),
            DeprecatedTemplates = countByStatus.GetValueOrDefault("Deprecated", 0),
            TotalDeployments = 0, // Would need separate query
            TemplatesByCategory = countByCategory,
            TemplatesByFormat = countByFormat
        };
    }

    #endregion

    #region Private Helpers

    private async Task AddAuditEntryAsync(Guid entityId, string entityName, string action, string performedBy, 
        string? details = null, CancellationToken cancellationToken = default)
    {
        var entry = ServiceTemplateMapper.CreateAuditEntry(entityId, entityName, action, performedBy, details);
        await _repository.AddAuditEntryAsync(entry, cancellationToken);
    }

    #endregion
}
