using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Agents.Environments.Tools;

/// <summary>
/// Tool for listing available service templates from the platform catalog.
/// This is the primary entry point for developers to discover what environment types are available.
/// </summary>
public class ServiceTemplateListTool : BaseTool
{
    private readonly IServiceTemplateCatalogService _templateCatalog;

    public override string Name => "list_service_templates";

    public override string Description =>
        "List and BROWSE available service templates from the Platform Engineering catalog (summary view only). " +
        "Returns template names, descriptions, and categories - NOT detailed parameters. " +
        "Use this to discover WHAT templates exist. Filter by category or keywords. " +
        "For DETAILED parameter information about a specific template, use 'get_template_details' instead.";

    public ServiceTemplateListTool(
        ILogger<ServiceTemplateListTool> logger,
        IServiceTemplateCatalogService templateCatalog) : base(logger)
    {
        _templateCatalog = templateCatalog ?? throw new ArgumentNullException(nameof(templateCatalog));

        Parameters.Add(new ToolParameter(
            name: "category",
            description: "Filter by category: Compute, Web, Containers, Composite, Compliance (optional)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "keyword",
            description: "Search keyword to match against template names, descriptions, and tags (optional)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "complianceFramework",
            description: "Filter by compliance framework: NIST-800-53, FedRAMP-High (optional)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var category = GetOptionalString(arguments, "category");
        var keyword = GetOptionalString(arguments, "keyword");
        var complianceFramework = GetOptionalString(arguments, "complianceFramework");

        Logger.LogInformation("📚 Listing service templates (Category: {Category}, Keyword: {Keyword})",
            category ?? "All", keyword ?? "None");

        try
        {
            var criteria = new TemplateSearchCriteria
            {
                Category = category,
                Keyword = keyword,
                ComplianceFramework = complianceFramework,
                IncludeDeprecated = false
            };

            var templates = await _templateCatalog.SearchTemplatesAsync(criteria, cancellationToken);
            var categories = await _templateCatalog.GetCategoriesAsync(cancellationToken);
            var stats = await _templateCatalog.GetCatalogStatsAsync(cancellationToken);

            Logger.LogInformation("✅ Found {Count} templates", templates.Count);

            return ToJson(new
            {
                success = true,
                totalPublished = stats.PublishedTemplates,
                categories = categories,
                count = templates.Count,
                templates = templates.Select(t => new
                {
                    id = t.Id,
                    name = t.Name,
                    displayName = t.DisplayName,
                    description = t.Description,
                    category = t.Category,
                    format = t.Format.ToString(),
                    version = t.Version,
                    keywords = t.Keywords,
                    useCases = t.UseCases,
                    complianceFrameworks = t.ComplianceFrameworks,
                    requiresApproval = t.RequiresApproval,
                    deploymentCount = t.DeploymentCount,
                    parameters = t.Parameters.Select(p => new
                    {
                        name = p.Name,
                        displayName = p.DisplayName,
                        type = p.Type.ToString(),
                        required = p.Required,
                        defaultValue = p.DefaultValue,
                        description = p.Description
                    })
                }),
                hint = "Use 'get_template_details' for full parameter information, then 'create_environment_from_template' to provision an environment."
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Failed to list service templates");
            return ToJson(new { success = false, error = ex.Message });
        }
    }
}
