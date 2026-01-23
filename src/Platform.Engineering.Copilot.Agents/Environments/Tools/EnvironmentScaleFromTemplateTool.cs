using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Agents.Environments.Tools;

/// <summary>
/// Tool for scaling provisioned environments within guardrail limits.
/// </summary>
public class EnvironmentScaleFromTemplateTool : BaseTool
{
    private readonly IProvisionedEnvironmentService _environmentService;
    private readonly IServiceTemplateCatalogService _templateCatalog;

    public override string Name => "scale_provisioned_environment";

    public override string Description =>
        "Scale a provisioned environment by adjusting resource parameters. " +
        "Scaling is constrained by the template's guardrails (e.g., max node count). " +
        "Use for scaling AKS nodes, App Service plans, container replicas, etc.";

    public EnvironmentScaleFromTemplateTool(
        ILogger<EnvironmentScaleFromTemplateTool> logger,
        IProvisionedEnvironmentService environmentService,
        IServiceTemplateCatalogService templateCatalog) : base(logger)
    {
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        _templateCatalog = templateCatalog ?? throw new ArgumentNullException(nameof(templateCatalog));

        Parameters.Add(new ToolParameter(
            name: "environmentId",
            description: "ID or name of the environment to scale (required)",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "scalingParameters",
            description: "JSON object with parameter values to change (e.g., {\"nodeCount\": 5, \"maxNodeCount\": 15})",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "reason",
            description: "Reason for scaling (for audit trail)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var environmentId = GetOptionalString(arguments, "environmentId");
        var scalingParametersJson = GetOptionalString(arguments, "scalingParameters");
        var reason = GetOptionalString(arguments, "reason");

        if (string.IsNullOrWhiteSpace(environmentId))
            return ToJson(new { success = false, error = "Environment ID is required" });
        if (string.IsNullOrWhiteSpace(scalingParametersJson))
            return ToJson(new { success = false, error = "Scaling parameters are required" });

        Logger.LogInformation("📈 Scaling environment {EnvironmentId}", environmentId);

        try
        {
            // Get environment (by ID or name)
            var environment = await _environmentService.GetEnvironmentAsync(environmentId, cancellationToken)
                ?? await _environmentService.GetEnvironmentByNameAsync(environmentId, cancellationToken);

            if (environment == null)
            {
                return ToJson(new
                {
                    success = false,
                    error = $"Environment '{environmentId}' not found",
                    hint = "Use 'list_provisioned_environments' to see available environments"
                });
            }

            // Parse scaling parameters
            Dictionary<string, object> scalingParameters;
            try
            {
                scalingParameters = JsonSerializer.Deserialize<Dictionary<string, object>>(scalingParametersJson)
                    ?? new Dictionary<string, object>();
            }
            catch (JsonException)
            {
                return ToJson(new { success = false, error = "Invalid JSON format for scalingParameters" });
            }

            // Get template to show guardrails
            var template = await _templateCatalog.GetTemplateAsync(environment.TemplateId, cancellationToken);

            // Execute scaling
            var request = new ScaleEnvironmentRequest
            {
                EnvironmentId = environment.Id,
                ScalingParameters = scalingParameters,
                RequestedBy = "environment-agent", // TODO: Get from context
                Reason = reason
            };

            var result = await _environmentService.ScaleEnvironmentAsync(request, cancellationToken);

            if (!result.Success)
            {
                Logger.LogWarning("⚠️ Scaling failed: {Errors}", string.Join("; ", result.Errors));
                return ToJson(new
                {
                    success = false,
                    errors = result.Errors,
                    hint = template != null
                        ? $"Check guardrails for template '{template.Name}': {string.Join(", ", template.Guardrails.Select(g => $"{g.Property} {g.Operator} {g.Value}"))}"
                        : null
                });
            }

            Logger.LogInformation("✅ Environment {Name} scaled successfully", environment.Name);

            return ToJson(new
            {
                success = true,
                message = $"Environment '{environment.Name}' scaled successfully",
                environment = new
                {
                    id = result.Environment!.Id,
                    name = result.Environment.Name,
                    status = result.Environment.Status.ToString(),
                    updatedParameters = scalingParameters,
                    currentParameters = result.Environment.Parameters
                        .Where(p => scalingParameters.ContainsKey(p.Key))
                        .ToDictionary(p => p.Key, p => p.Value)
                },
                audit = new
                {
                    action = "Scaled",
                    reason = reason,
                    timestamp = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Failed to scale environment");
            return ToJson(new { success = false, error = ex.Message });
        }
    }
}
