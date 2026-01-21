using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Core.Interfaces.Templates;

/// <summary>
/// Service for managing the Service Template catalog.
/// Git sync is handled by the dedicated IGitTemplateSyncService.
/// </summary>
public interface IServiceTemplateCatalogService
{
    #region Template CRUD Operations

    /// <summary>
    /// Get a template by ID
    /// </summary>
    Task<ServiceTemplate?> GetTemplateAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a template by name and optional version
    /// </summary>
    Task<ServiceTemplate?> GetTemplateByNameAsync(string name, string? version = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search templates by criteria
    /// </summary>
    Task<List<ServiceTemplate>> SearchTemplatesAsync(TemplateSearchCriteria criteria, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all published templates (for LLM selection)
    /// </summary>
    Task<List<ServiceTemplate>> GetPublishedTemplatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new template (as draft)
    /// </summary>
    Task<ServiceTemplate> CreateTemplateAsync(ServiceTemplate template, string createdBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing template
    /// </summary>
    Task<ServiceTemplate> UpdateTemplateAsync(ServiceTemplate template, string updatedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a template (soft delete - archives it)
    /// </summary>
    Task<bool> DeleteTemplateAsync(string templateId, string deletedBy, CancellationToken cancellationToken = default);

    #endregion

    #region Template Lifecycle

    /// <summary>
    /// Submit template for approval
    /// </summary>
    Task<ServiceTemplate> SubmitForApprovalAsync(string templateId, string submittedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Approve a template
    /// </summary>
    Task<ServiceTemplate> ApproveTemplateAsync(string templateId, string approvedBy, string? comments = null, ApprovalSource source = ApprovalSource.Internal, string? externalApprovalId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reject a template approval
    /// </summary>
    Task<ServiceTemplate> RejectTemplateAsync(string templateId, string rejectedBy, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publish a template (must be approved first)
    /// </summary>
    Task<ServiceTemplate> PublishTemplateAsync(string templateId, string publishedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deprecate a template
    /// </summary>
    Task<ServiceTemplate> DeprecateTemplateAsync(string templateId, string deprecatedBy, string? reason = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clone a template as a new draft
    /// </summary>
    Task<ServiceTemplate> CloneTemplateAsync(string templateId, string newName, string clonedBy, CancellationToken cancellationToken = default);

    #endregion

    #region Validation

    /// <summary>
    /// Validate template parameters against a set of provided values
    /// </summary>
    Task<List<string>> ValidateParametersAsync(string templateId, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check guardrails and return any violations
    /// </summary>
    Task<List<GuardrailViolation>> CheckGuardrailsAsync(string templateId, Dictionary<string, object> parameters, CancellationToken cancellationToken = default);

    #endregion

    #region AI/LLM Support

    /// <summary>
    /// Find best matching templates for a natural language request
    /// </summary>
    Task<List<ServiceTemplate>> FindMatchingTemplatesAsync(string userRequest, int maxResults = 5, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get template summary for LLM context (condensed format)
    /// </summary>
    Task<string> GetTemplateSummaryForAiAsync(string templateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all template summaries for LLM context
    /// </summary>
    Task<string> GetAllTemplateSummariesForAiAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Categories & Metadata

    /// <summary>
    /// Get all template categories
    /// </summary>
    Task<List<string>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get template statistics
    /// </summary>
    Task<TemplateCatalogStats> GetCatalogStatsAsync(CancellationToken cancellationToken = default);

    #endregion
}

/// <summary>
/// Statistics about the template catalog
/// </summary>
public class TemplateCatalogStats
{
    public int TotalTemplates { get; set; }
    public int PublishedTemplates { get; set; }
    public int DraftTemplates { get; set; }
    public int PendingApprovalTemplates { get; set; }
    public int DeprecatedTemplates { get; set; }
    public int TotalDeployments { get; set; }
    public Dictionary<string, int> TemplatesByCategory { get; set; } = new();
    public Dictionary<string, int> TemplatesByFormat { get; set; } = new();
}
