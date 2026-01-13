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
/// Uses EF Core for persistence with optional Git sync for source of truth.
/// </summary>
public class ServiceTemplateCatalogService : IServiceTemplateCatalogService
{
    private readonly ILogger<ServiceTemplateCatalogService> _logger;
    private readonly IServiceTemplateRepository _repository;
    private static bool _initialized = false;
    private static readonly object _initLock = new();

    public ServiceTemplateCatalogService(
        ILogger<ServiceTemplateCatalogService> logger,
        IServiceTemplateRepository repository)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        
        // Seed default templates on first initialization (thread-safe)
        lock (_initLock)
        {
            if (!_initialized)
            {
                InitializeDefaultTemplatesAsync().GetAwaiter().GetResult();
                _initialized = true;
            }
        }
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

    #region Git Sync

    public async Task<ServiceTemplate> SyncFromGitAsync(string templateId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        if (entity == null)
            throw new InvalidOperationException($"Template {templateId} not found");

        if (string.IsNullOrEmpty(entity.GitRepositoryUrl))
            throw new InvalidOperationException("Template does not have a Git source configured");

        // TODO: Implement actual Git sync
        // 1. Clone/pull repository
        // 2. Read template files from GitSource.Path
        // 3. Update template content
        // 4. Update GitCommitSha and LastSyncedFromGit

        await _repository.UpdateGitSyncTimestampAsync(entity.Id, null, cancellationToken);
        await AddAuditEntryAsync(entity.Id, entity.Name, "SyncedFromGit", "system", 
            "Template synced from Git repository", cancellationToken);

        _logger.LogInformation("🔄 Template {Name} synced from Git", entity.Name);

        // Re-fetch to get updated timestamp
        entity = await _repository.GetByIdAsync(templateId, cancellationToken);
        return entity!.ToModel();
    }

    public async Task<int> SyncAllFromGitAsync(CancellationToken cancellationToken = default)
    {
        var templatesNeedingSync = await _repository.GetTemplatesNeedingSyncAsync(cancellationToken);

        var syncedCount = 0;
        foreach (var entity in templatesNeedingSync)
        {
            try
            {
                await SyncFromGitAsync(entity.Id.ToString(), cancellationToken);
                syncedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync template {Name} from Git", entity.Name);
            }
        }

        return syncedCount;
    }

    public async Task<ServiceTemplate> ImportFromGitAsync(GitSourceInfo gitSource, string importedBy, CancellationToken cancellationToken = default)
    {
        // TODO: Implement actual Git import
        // 1. Clone repository
        // 2. Read template files
        // 3. Parse Bicep/Terraform/ARM to extract parameters
        // 4. Create ServiceTemplate entity

        var template = new ServiceTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = $"imported-{DateTime.UtcNow:yyyyMMddHHmmss}",
            DisplayName = "Imported Template",
            GitSource = gitSource,
            CreatedBy = importedBy,
            CreatedAt = DateTime.UtcNow,
            Status = TemplateStatus.Draft
        };

        var entity = template.ToEntity();
        await _repository.CreateAsync(entity, cancellationToken);
        await AddAuditEntryAsync(entity.Id, template.Name, "ImportedFromGit", importedBy, 
            $"Imported from {gitSource.RepositoryUrl}", cancellationToken);

        _logger.LogInformation("📥 Template imported from Git by {ImportedBy}: {Url}",
            importedBy, gitSource.RepositoryUrl);

        return template;
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

    private async Task InitializeDefaultTemplatesAsync()
    {
        try
        {
            // Check if templates already exist
            var existingTemplates = await _repository.GetAllAsync();
            if (existingTemplates.Any())
            {
                _logger.LogInformation("📚 Service templates already initialized ({Count} templates)", existingTemplates.Count);
                return;
            }

            var templates = new List<ServiceTemplate>
            {
                CreateAksTemplate(),
                CreateWebAppTemplate(),
                CreateContainerAppTemplate(),
                CreateMicroserviceTemplate(),
                CreateFedRampTemplate()
            };

            foreach (var template in templates)
            {
                var entity = template.ToEntity();
                await _repository.CreateAsync(entity);
            }

            _logger.LogInformation("📚 Initialized {Count} default service templates", templates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize default templates (database may not be ready)");
        }
    }

    private ServiceTemplate CreateAksTemplate() => new()
    {
        Id = "tpl-aks-standard",
        Name = "aks-standard",
        DisplayName = "Standard AKS Cluster",
        Description = "Production-ready Azure Kubernetes Service cluster with autoscaling, monitoring, and security best practices.",
        Version = "2.0.0",
        Category = "Compute",
        Format = TemplateFormat.Bicep,
        Status = TemplateStatus.Published,
        CreatedBy = "platform-team",
        CreatedAt = DateTime.UtcNow.AddDays(-30),
        Keywords = new List<string> { "kubernetes", "aks", "containers", "k8s", "cluster", "microservices" },
        UseCases = new List<string> { "Microservices", "Container workloads", "API hosting", "Background workers" },
        AiSelectionHint = "Use this template when user needs Kubernetes, AKS, container orchestration, or microservices platform",
        ComplianceFrameworks = new List<string> { "NIST-800-53" },
        Parameters = new List<TemplateParameter>
        {
            new() { Name = "clusterName", DisplayName = "Cluster Name", Description = "Name of the AKS cluster", Type = ParameterType.String, Required = true, MinLength = 3, MaxLength = 63, ValidationRegex = "^[a-z0-9-]+$" },
            new() { Name = "nodeCount", DisplayName = "Initial Node Count", Description = "Initial number of nodes", Type = ParameterType.Number, DefaultValue = 3, MinValue = 1, MaxValue = 100 },
            new() { Name = "nodeSize", DisplayName = "Node Size", Description = "VM size for nodes", Type = ParameterType.Choice, DefaultValue = "Standard_D4s_v5", AllowedValues = new List<string> { "Standard_D2s_v5", "Standard_D4s_v5", "Standard_D8s_v5", "Standard_D16s_v5" } },
            new() { Name = "kubernetesVersion", DisplayName = "Kubernetes Version", Description = "Kubernetes version", Type = ParameterType.Choice, DefaultValue = "1.28", AllowedValues = new List<string> { "1.27", "1.28", "1.29" } },
            new() { Name = "enableAutoScaling", DisplayName = "Enable Auto Scaling", Description = "Enable cluster autoscaler", Type = ParameterType.Boolean, DefaultValue = true },
            new() { Name = "minNodeCount", DisplayName = "Minimum Nodes", Description = "Minimum nodes when autoscaling", Type = ParameterType.Number, DefaultValue = 1, MinValue = 1 },
            new() { Name = "maxNodeCount", DisplayName = "Maximum Nodes", Description = "Maximum nodes when autoscaling", Type = ParameterType.Number, DefaultValue = 10, MaxValue = 100 }
        },
        Guardrails = new List<TemplateGuardrail>
        {
            new() { Id = "g1", Name = "Max Node Limit", Property = "maxNodeCount", Operator = "<=", Value = 50, Action = GuardrailAction.Deny, ErrorMessage = "Maximum node count cannot exceed 50 per cluster" },
            new() { Id = "g2", Name = "Node Size Limit", Property = "nodeSize", Operator = "in", Value = new List<string> { "Standard_D2s_v5", "Standard_D4s_v5", "Standard_D8s_v5" }, Action = GuardrailAction.Deny, ErrorMessage = "Node size exceeds approved sizes for this template" }
        },
        DefaultTags = new Dictionary<string, string>
        {
            ["ManagedBy"] = "PlatformEngineering",
            ["TemplateId"] = "tpl-aks-standard"
        },
        RequiresApproval = false,
        Approval = new ApprovalInfo { ApprovedBy = "platform-team", ApprovedAt = DateTime.UtcNow.AddDays(-30), Source = ApprovalSource.Internal }
    };

    private ServiceTemplate CreateWebAppTemplate() => new()
    {
        Id = "tpl-webapp-standard",
        Name = "webapp-standard",
        DisplayName = "Standard Web Application",
        Description = "Azure Web App with staging slot, Application Insights, and recommended security settings.",
        Version = "1.5.0",
        Category = "Web",
        Format = TemplateFormat.Bicep,
        Status = TemplateStatus.Published,
        CreatedBy = "platform-team",
        CreatedAt = DateTime.UtcNow.AddDays(-60),
        Keywords = new List<string> { "web", "webapp", "app service", "website", "api", "dotnet", "node" },
        UseCases = new List<string> { "Web APIs", "Websites", ".NET applications", "Node.js apps" },
        AiSelectionHint = "Use this for web applications, REST APIs, websites, or when user mentions App Service",
        ComplianceFrameworks = new List<string> { "NIST-800-53" },
        Parameters = new List<TemplateParameter>
        {
            new() { Name = "appName", DisplayName = "Application Name", Description = "Name of the web app", Type = ParameterType.String, Required = true, MinLength = 2, MaxLength = 60 },
            new() { Name = "sku", DisplayName = "App Service Plan SKU", Description = "Pricing tier", Type = ParameterType.Choice, DefaultValue = "P1v3", AllowedValues = new List<string> { "B1", "B2", "S1", "S2", "P1v3", "P2v3" } },
            new() { Name = "runtime", DisplayName = "Runtime Stack", Description = "Application runtime", Type = ParameterType.Choice, DefaultValue = "DOTNET|8.0", AllowedValues = new List<string> { "DOTNET|8.0", "DOTNET|6.0", "NODE|20-lts", "NODE|18-lts", "PYTHON|3.11" } },
            new() { Name = "enableStagingSlot", DisplayName = "Enable Staging Slot", Description = "Create staging deployment slot", Type = ParameterType.Boolean, DefaultValue = true },
            new() { Name = "enableAppInsights", DisplayName = "Enable Application Insights", Description = "Enable monitoring", Type = ParameterType.Boolean, DefaultValue = true }
        },
        DefaultTags = new Dictionary<string, string>
        {
            ["ManagedBy"] = "PlatformEngineering",
            ["TemplateId"] = "tpl-webapp-standard"
        },
        RequiresApproval = false,
        Approval = new ApprovalInfo { ApprovedBy = "platform-team", ApprovedAt = DateTime.UtcNow.AddDays(-60), Source = ApprovalSource.Internal }
    };

    private ServiceTemplate CreateContainerAppTemplate() => new()
    {
        Id = "tpl-containerapp-standard",
        Name = "containerapp-standard",
        DisplayName = "Standard Container App",
        Description = "Azure Container App with autoscaling, ingress, and Dapr integration options.",
        Version = "1.2.0",
        Category = "Containers",
        Format = TemplateFormat.Bicep,
        Status = TemplateStatus.Published,
        CreatedBy = "platform-team",
        CreatedAt = DateTime.UtcNow.AddDays(-45),
        Keywords = new List<string> { "container", "containerapp", "docker", "serverless", "dapr" },
        UseCases = new List<string> { "Containerized apps", "Serverless containers", "Microservices with Dapr" },
        AiSelectionHint = "Use for containerized applications when full Kubernetes is not needed",
        ComplianceFrameworks = new List<string> { "NIST-800-53" },
        Parameters = new List<TemplateParameter>
        {
            new() { Name = "appName", DisplayName = "Container App Name", Description = "Name of the container app", Type = ParameterType.String, Required = true },
            new() { Name = "image", DisplayName = "Container Image", Description = "Container image to deploy", Type = ParameterType.String, Required = true, Placeholder = "myregistry.azurecr.io/myapp:latest" },
            new() { Name = "minReplicas", DisplayName = "Minimum Replicas", Description = "Minimum number of replicas", Type = ParameterType.Number, DefaultValue = 1, MinValue = 0, MaxValue = 30 },
            new() { Name = "maxReplicas", DisplayName = "Maximum Replicas", Description = "Maximum number of replicas", Type = ParameterType.Number, DefaultValue = 10, MinValue = 1, MaxValue = 30 },
            new() { Name = "cpu", DisplayName = "CPU Cores", Description = "CPU cores per replica", Type = ParameterType.Choice, DefaultValue = "0.5", AllowedValues = new List<string> { "0.25", "0.5", "1", "2" } },
            new() { Name = "memory", DisplayName = "Memory", Description = "Memory per replica", Type = ParameterType.Choice, DefaultValue = "1Gi", AllowedValues = new List<string> { "0.5Gi", "1Gi", "2Gi", "4Gi" } },
            new() { Name = "enableIngress", DisplayName = "Enable Ingress", Description = "Enable external access", Type = ParameterType.Boolean, DefaultValue = true },
            new() { Name = "enableDapr", DisplayName = "Enable Dapr", Description = "Enable Dapr sidecar", Type = ParameterType.Boolean, DefaultValue = false }
        },
        DefaultTags = new Dictionary<string, string>
        {
            ["ManagedBy"] = "PlatformEngineering",
            ["TemplateId"] = "tpl-containerapp-standard"
        },
        RequiresApproval = false,
        Approval = new ApprovalInfo { ApprovedBy = "platform-team", ApprovedAt = DateTime.UtcNow.AddDays(-45), Source = ApprovalSource.Internal }
    };

    private ServiceTemplate CreateMicroserviceTemplate() => new()
    {
        Id = "tpl-microservice-full",
        Name = "microservice-fullstack",
        DisplayName = "Full-Stack Microservice",
        Description = "Complete microservice environment with AKS, Azure SQL, Redis Cache, and Service Bus.",
        Version = "1.0.0",
        Category = "Composite",
        Format = TemplateFormat.Bicep,
        Status = TemplateStatus.Published,
        CreatedBy = "platform-team",
        CreatedAt = DateTime.UtcNow.AddDays(-15),
        Keywords = new List<string> { "microservice", "fullstack", "complete", "database", "cache", "messaging" },
        UseCases = new List<string> { "Complete microservice stack", "New applications", "Production workloads" },
        AiSelectionHint = "Use when user needs a complete environment with database, cache, and messaging",
        ComplianceFrameworks = new List<string> { "NIST-800-53" },
        Parameters = new List<TemplateParameter>
        {
            new() { Name = "serviceName", DisplayName = "Service Name", Description = "Name of the microservice", Type = ParameterType.String, Required = true },
            new() { Name = "includeDatabase", DisplayName = "Include Database", Description = "Include Azure SQL Database", Type = ParameterType.Boolean, DefaultValue = true },
            new() { Name = "databaseSku", DisplayName = "Database SKU", Description = "Database tier", Type = ParameterType.Choice, DefaultValue = "S1", AllowedValues = new List<string> { "Basic", "S0", "S1", "S2", "P1" } },
            new() { Name = "includeCache", DisplayName = "Include Redis Cache", Description = "Include Azure Redis Cache", Type = ParameterType.Boolean, DefaultValue = true },
            new() { Name = "includeServiceBus", DisplayName = "Include Service Bus", Description = "Include Azure Service Bus", Type = ParameterType.Boolean, DefaultValue = true },
            new() { Name = "nodeCount", DisplayName = "AKS Node Count", Description = "Number of AKS nodes", Type = ParameterType.Number, DefaultValue = 3, MinValue = 1, MaxValue = 10 }
        },
        DefaultTags = new Dictionary<string, string>
        {
            ["ManagedBy"] = "PlatformEngineering",
            ["TemplateId"] = "tpl-microservice-full"
        },
        RequiresApproval = true
    };

    private ServiceTemplate CreateFedRampTemplate() => new()
    {
        Id = "tpl-fedramp-high",
        Name = "fedramp-high-environment",
        DisplayName = "FedRAMP High Compliant Environment",
        Description = "Environment pre-configured for FedRAMP High compliance with all required security controls.",
        Version = "1.0.0",
        Category = "Compliance",
        Format = TemplateFormat.Bicep,
        Status = TemplateStatus.Published,
        CreatedBy = "security-team",
        CreatedAt = DateTime.UtcNow.AddDays(-20),
        Keywords = new List<string> { "fedramp", "compliance", "government", "security", "high", "nist" },
        UseCases = new List<string> { "Government workloads", "FedRAMP certification", "High security requirements" },
        AiSelectionHint = "Use for government, FedRAMP, or high-security compliance requirements",
        ComplianceFrameworks = new List<string> { "FedRAMP-High", "NIST-800-53" },
        EnforceCompliance = true,
        Parameters = new List<TemplateParameter>
        {
            new() { Name = "environmentName", DisplayName = "Environment Name", Description = "Name of the environment", Type = ParameterType.String, Required = true },
            new() { Name = "systemName", DisplayName = "System Name", Description = "Name of the system for ATO", Type = ParameterType.String, Required = true },
            new() { Name = "dataClassification", DisplayName = "Data Classification", Description = "Data classification level", Type = ParameterType.Choice, DefaultValue = "CUI", AllowedValues = new List<string> { "Public", "CUI", "Controlled" } },
            new() { Name = "enableCmk", DisplayName = "Customer Managed Keys", Description = "Use customer-managed encryption keys", Type = ParameterType.Boolean, DefaultValue = true },
            new() { Name = "enablePrivateEndpoints", DisplayName = "Private Endpoints", Description = "Use private endpoints for all services", Type = ParameterType.Boolean, DefaultValue = true },
            new() { Name = "retentionDays", DisplayName = "Log Retention Days", Description = "Audit log retention period", Type = ParameterType.Number, DefaultValue = 365, MinValue = 90, MaxValue = 730 }
        },
        Guardrails = new List<TemplateGuardrail>
        {
            new() { Id = "g1", Name = "Private Endpoints Required", Property = "enablePrivateEndpoints", Operator = "==", Value = true, Action = GuardrailAction.Deny, ErrorMessage = "FedRAMP High requires private endpoints" },
            new() { Id = "g2", Name = "CMK Required", Property = "enableCmk", Operator = "==", Value = true, Action = GuardrailAction.Deny, ErrorMessage = "FedRAMP High requires customer-managed keys" },
            new() { Id = "g3", Name = "Minimum Retention", Property = "retentionDays", Operator = ">=", Value = 90, Action = GuardrailAction.Deny, ErrorMessage = "Minimum log retention is 90 days" }
        },
        DefaultTags = new Dictionary<string, string>
        {
            ["ManagedBy"] = "PlatformEngineering",
            ["TemplateId"] = "tpl-fedramp-high",
            ["ComplianceFramework"] = "FedRAMP-High"
        },
        RequiresApproval = true,
        Approval = new ApprovalInfo { ApprovedBy = "security-team", ApprovedAt = DateTime.UtcNow.AddDays(-20), Source = ApprovalSource.Internal }
    };

    #endregion
}
