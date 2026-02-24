using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// Service for managing the service template catalog: CRUD, filtering, approval workflow, and soft-delete.
/// </summary>
public interface IServiceTemplateCatalogService
{
    Task<(IReadOnlyList<ServiceTemplate> Items, int TotalCount)> GetAllAsync(
        string? category = null,
        string? status = null,
        string? search = null,
        int skip = 0,
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<ServiceTemplate?> GetByIdAsync(Guid templateId, CancellationToken cancellationToken = default);

    Task<ServiceTemplate?> GetByNameAsync(string name, string? version = null, CancellationToken cancellationToken = default);

    Task<ServiceTemplate> CreateAsync(ServiceTemplate template, CancellationToken cancellationToken = default);

    Task<ServiceTemplate> UpdateAsync(ServiceTemplate template, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid templateId, string deletedBy, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<ServiceTemplate> SubmitForApprovalAsync(Guid templateId, CancellationToken cancellationToken = default);

    Task<ServiceTemplate> ApproveAsync(
        Guid templateId,
        string approvalSource,
        string approvedBy,
        string? comments = null,
        string? externalApprovalId = null,
        string? externalApprovalUrl = null,
        CancellationToken cancellationToken = default);

    Task<ServiceTemplate> DeprecateAsync(
        Guid templateId,
        string deprecatedBy,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceTemplate>> GetDeletedAsync(CancellationToken cancellationToken = default);
}
