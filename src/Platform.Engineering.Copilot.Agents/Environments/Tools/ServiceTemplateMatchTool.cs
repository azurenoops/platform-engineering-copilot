using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;

namespace Platform.Engineering.Copilot.Agents.Environments.Tools;

/// <summary>
/// Tool for finding the best matching service template based on natural language requirements.
/// Uses AI hints and keyword matching to help users find the right template.
/// </summary>
public class ServiceTemplateMatchTool : BaseTool
{
    private readonly IServiceTemplateCatalogService _templateCatalog;

    public override string Name => "find_matching_template";

    public override string Description =>
        "Find the best matching service template based on natural language requirements. " +
        "Describe what you need (e.g., 'kubernetes cluster for microservices', 'web app with database') " +
        "and this tool will suggest the most appropriate templates from the catalog.";

    public ServiceTemplateMatchTool(
        ILogger<ServiceTemplateMatchTool> logger,
        IServiceTemplateCatalogService templateCatalog) : base(logger)
    {
        _templateCatalog = templateCatalog ?? throw new ArgumentNullException(nameof(templateCatalog));

        Parameters.Add(new ToolParameter(
            name: "requirements",
            description: "Natural language description of what you need (e.g., 'I need a kubernetes cluster for running microservices with autoscaling')",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "maxResults",
            description: "Maximum number of template suggestions to return (default: 3)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var requirements = GetOptionalString(arguments, "requirements");
        var maxResultsStr = GetOptionalString(arguments, "maxResults");
        var maxResults = 3;

        if (!string.IsNullOrEmpty(maxResultsStr) && int.TryParse(maxResultsStr, out var parsed))
        {
            maxResults = Math.Min(Math.Max(parsed, 1), 10);
        }

        if (string.IsNullOrWhiteSpace(requirements))
            return ToJson(new { success = false, error = "Requirements description is required" });

        Logger.LogInformation("🔍 Finding matching templates for: {Requirements}", requirements);

        try
        {
            var matches = await _templateCatalog.FindMatchingTemplatesAsync(requirements, maxResults, cancellationToken);

            if (!matches.Any())
            {
                // Fallback to all published templates
                var allTemplates = await _templateCatalog.GetPublishedTemplatesAsync(cancellationToken);
                var summary = await _templateCatalog.GetAllTemplateSummariesForAiAsync(cancellationToken);

                return ToJson(new
                {
                    success = true,
                    message = "No direct matches found. Here are all available templates:",
                    count = allTemplates.Count,
                    templates = allTemplates.Take(5).Select(t => new
                    {
                        id = t.Id,
                        name = t.Name,
                        displayName = t.DisplayName,
                        description = t.Description,
                        category = t.Category
                    }),
                    hint = "Try using more specific keywords or use 'list_service_templates' to browse all templates"
                });
            }

            Logger.LogInformation("✅ Found {Count} matching templates", matches.Count);

            return ToJson(new
            {
                success = true,
                query = requirements,
                matchCount = matches.Count,
                recommendations = matches.Select((t, index) => new
                {
                    rank = index + 1,
                    confidence = index == 0 ? "High" : index == 1 ? "Medium" : "Low",
                    id = t.Id,
                    name = t.Name,
                    displayName = t.DisplayName,
                    description = t.Description,
                    category = t.Category,
                    whyThisTemplate = t.AiSelectionHint,
                    useCases = t.UseCases,
                    keywords = t.Keywords,
                    complianceFrameworks = t.ComplianceFrameworks,
                    requiresApproval = t.RequiresApproval,
                    requiredParameters = t.Parameters.Where(p => p.Required).Select(p => new
                    {
                        name = p.Name,
                        displayName = p.DisplayName,
                        type = p.Type.ToString()
                    })
                }),
                nextSteps = new[]
                {
                    "Use 'get_template_details' with the template ID to see full parameter information",
                    "Use 'create_environment_from_template' to create an environment from the chosen template"
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Failed to find matching templates");
            return ToJson(new { success = false, error = ex.Message });
        }
    }
}
