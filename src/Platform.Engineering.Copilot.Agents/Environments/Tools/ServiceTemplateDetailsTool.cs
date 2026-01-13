using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;

namespace Platform.Engineering.Copilot.Agents.Environments.Tools;

/// <summary>
/// Tool for getting detailed information about a specific service template.
/// Provides full parameter definitions, guardrails, and compliance information.
/// </summary>
public class ServiceTemplateDetailsTool : BaseTool
{
    private readonly IServiceTemplateCatalogService _templateCatalog;

    public override string Name => "get_template_details";

    public override string Description =>
        "Get detailed information about a specific service template including all parameters, " +
        "validation rules, guardrails (constraints), compliance requirements, and usage guidance. " +
        "Use this before creating an environment to understand all required and optional parameters.";

    public ServiceTemplateDetailsTool(
        ILogger<ServiceTemplateDetailsTool> logger,
        IServiceTemplateCatalogService templateCatalog) : base(logger)
    {
        _templateCatalog = templateCatalog ?? throw new ArgumentNullException(nameof(templateCatalog));

        Parameters.Add(new ToolParameter(
            name: "templateId",
            description: "The ID or name of the template to get details for (required)",
            required: true));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var templateId = GetOptionalString(arguments, "templateId");

        if (string.IsNullOrWhiteSpace(templateId))
        {
            return ToJson(new { success = false, error = "Template ID is required" });
        }

        Logger.LogInformation("🔍 Getting details for template {TemplateId}", templateId);

        try
        {
            // Try by ID first, then by name
            var template = await _templateCatalog.GetTemplateAsync(templateId, cancellationToken)
                ?? await _templateCatalog.GetTemplateByNameAsync(templateId, cancellationToken: cancellationToken);

            if (template == null)
            {
                return ToJson(new
                {
                    success = false,
                    error = $"Template '{templateId}' not found",
                    hint = "Use 'list_service_templates' to see available templates"
                });
            }

            Logger.LogInformation("✅ Found template {Name}", template.DisplayName);

            return ToJson(new
            {
                success = true,
                template = new
                {
                    id = template.Id,
                    name = template.Name,
                    displayName = template.DisplayName,
                    description = template.Description,
                    category = template.Category,
                    format = template.Format.ToString(),
                    version = template.Version,
                    status = template.Status.ToString(),

                    // AI hints
                    useCases = template.UseCases,
                    keywords = template.Keywords,
                    aiSelectionHint = template.AiSelectionHint,

                    // Compliance
                    complianceFrameworks = template.ComplianceFrameworks,
                    enforceCompliance = template.EnforceCompliance,

                    // Parameters with full details
                    parameters = template.Parameters.OrderBy(p => p.DisplayOrder).Select(p => new
                    {
                        name = p.Name,
                        displayName = p.DisplayName,
                        description = p.Description,
                        type = p.Type.ToString(),
                        required = p.Required,
                        defaultValue = p.DefaultValue,
                        placeholder = p.Placeholder,
                        helpText = p.HelpText,

                        // Validation rules
                        allowedValues = p.AllowedValues,
                        minValue = p.MinValue,
                        maxValue = p.MaxValue,
                        minLength = p.MinLength,
                        maxLength = p.MaxLength,
                        validationRegex = p.ValidationRegex
                    }),

                    // Guardrails (constraints)
                    guardrails = template.Guardrails.Select(g => new
                    {
                        name = g.Name,
                        description = g.Description,
                        property = g.Property,
                        @operator = g.Operator,
                        value = g.Value,
                        action = g.Action.ToString(),
                        errorMessage = g.ErrorMessage
                    }),

                    // Metadata
                    requiresApproval = template.RequiresApproval,
                    defaultExpirationDays = template.DefaultExpirationDays,
                    defaultTags = template.DefaultTags,
                    deploymentCount = template.DeploymentCount,
                    createdBy = template.CreatedBy,
                    createdAt = template.CreatedAt
                },
                nextSteps = new[]
                {
                    "Review the parameters above and gather required values",
                    "Use 'create_environment_from_template' with the templateId and parameters to create an environment"
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Failed to get template details for {TemplateId}", templateId);
            return ToJson(new { success = false, error = ex.Message });
        }
    }
}
