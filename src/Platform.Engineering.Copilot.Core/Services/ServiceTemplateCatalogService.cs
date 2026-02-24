using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Manages the service template catalog: CRUD, filtering, approval workflow, and soft-delete.
/// </summary>
public class ServiceTemplateCatalogService : IServiceTemplateCatalogService
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ILogger<ServiceTemplateCatalogService> _logger;

    public ServiceTemplateCatalogService(PlatformEngineeringCopilotContext context, ILogger<ServiceTemplateCatalogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(IReadOnlyList<ServiceTemplate> Items, int TotalCount)> GetAllAsync(
        string? category = null, string? status = null, string? search = null,
        int skip = 0, int take = 50, CancellationToken cancellationToken = default)
    {
        var query = _context.ServiceTemplates.AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(t => t.Category == category);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TemplateStatus>(status, true, out var statusEnum))
            query = query.Where(t => t.Status == statusEnum);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(t =>
                t.Name.ToLower().Contains(term) ||
                t.Description.ToLower().Contains(term) ||
                (t.Keywords != null && t.Keywords.ToLower().Contains(term)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.UpdatedAt)
            .Skip(skip)
            .Take(Math.Min(take, 100))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<ServiceTemplate?> GetByIdAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .FirstOrDefaultAsync(t => t.TemplateId == templateId, cancellationToken);
    }

    public async Task<ServiceTemplate?> GetByNameAsync(string name, string? version = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ServiceTemplates.Where(t => t.Name == name);

        if (!string.IsNullOrWhiteSpace(version))
            query = query.Where(t => t.Version == version);
        else
            query = query.OrderByDescending(t => t.Version);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ServiceTemplate> CreateAsync(ServiceTemplate template, CancellationToken cancellationToken = default)
    {
        // Check for duplicate name+version
        var exists = await _context.ServiceTemplates
            .AnyAsync(t => t.Name == template.Name && t.Version == template.Version, cancellationToken);

        if (exists)
            throw new InvalidOperationException($"A template with name '{template.Name}' and version '{template.Version}' already exists.");

        template.TemplateId = template.TemplateId == Guid.Empty ? Guid.NewGuid() : template.TemplateId;
        template.Status = TemplateStatus.Draft;
        template.CreatedAt = DateTimeOffset.UtcNow;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        if (string.IsNullOrWhiteSpace(template.Version))
            template.Version = "1.0.0";
        if (string.IsNullOrWhiteSpace(template.Category))
            template.Category = "General";

        _context.ServiceTemplates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created template {TemplateId} '{Name}' v{Version}", template.TemplateId, template.Name, template.Version);
        return template;
    }

    public async Task<ServiceTemplate> UpdateAsync(ServiceTemplate template, CancellationToken cancellationToken = default)
    {
        template.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict updating template {TemplateId}", template.TemplateId);
            throw;
        }

        _logger.LogInformation("Updated template {TemplateId} '{Name}'", template.TemplateId, template.Name);
        return template;
    }

    public async Task DeleteAsync(Guid templateId, string deletedBy, CancellationToken cancellationToken = default)
    {
        var template = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        template.IsDeleted = true;
        template.DeletedAt = DateTimeOffset.UtcNow;
        template.DeletedBy = deletedBy;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Soft-deleted template {TemplateId} by {DeletedBy}", templateId, deletedBy);
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(cancellationToken);
    }

    public async Task<ServiceTemplate> SubmitForApprovalAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var template = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        if (template.Status != TemplateStatus.Draft)
            throw new InvalidOperationException($"Cannot submit template for approval: current status is '{template.Status}', expected 'Draft'.");

        template.Status = TemplateStatus.PendingApproval;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Template {TemplateId} submitted for approval", templateId);
        return template;
    }

    public async Task<ServiceTemplate> ApproveAsync(
        Guid templateId, string approvalSource, string approvedBy,
        string? comments = null, string? externalApprovalId = null, string? externalApprovalUrl = null,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        if (template.Status != TemplateStatus.PendingApproval)
            throw new InvalidOperationException($"Cannot approve template: current status is '{template.Status}', expected 'PendingApproval'.");

        template.Status = TemplateStatus.Published;
        template.ApprovalSource = approvalSource;
        template.ApprovedBy = approvedBy;
        template.ApprovedAt = DateTimeOffset.UtcNow;
        template.ApprovalComments = comments;
        template.ExternalApprovalId = externalApprovalId;
        template.ExternalApprovalUrl = externalApprovalUrl;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Template {TemplateId} approved by {ApprovedBy}", templateId, approvedBy);
        return template;
    }

    public async Task<ServiceTemplate> DeprecateAsync(Guid templateId, string deprecatedBy, string reason,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.ServiceTemplates.FindAsync(new object[] { templateId }, cancellationToken)
            ?? throw new KeyNotFoundException($"Template {templateId} not found.");

        if (template.Status != TemplateStatus.Published)
            throw new InvalidOperationException($"Cannot deprecate template: current status is '{template.Status}', expected 'Published'.");

        template.Status = TemplateStatus.Deprecated;
        template.DeprecatedBy = deprecatedBy;
        template.DeprecatedAt = DateTimeOffset.UtcNow;
        template.DeprecationReason = reason;
        template.UpdatedAt = DateTimeOffset.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Template {TemplateId} deprecated by {DeprecatedBy}: {Reason}", templateId, deprecatedBy, reason);
        return template;
    }

    public async Task<IReadOnlyList<ServiceTemplate>> GetDeletedAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ServiceTemplates
            .IgnoreQueryFilters()
            .Where(t => t.IsDeleted)
            .OrderByDescending(t => t.DeletedAt)
            .ToListAsync(cancellationToken);
    }
}
