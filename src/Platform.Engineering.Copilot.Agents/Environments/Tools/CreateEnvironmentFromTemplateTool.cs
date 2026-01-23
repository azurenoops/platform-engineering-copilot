using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Governance;

namespace Platform.Engineering.Copilot.Agents.Environments.Tools;

/// <summary>
/// Tool for creating environments from approved service templates.
/// This is the main provisioning interface for developers.
/// Enforces governance policies before provisioning.
/// </summary>
public class CreateEnvironmentFromTemplateTool : BaseTool
{
    private readonly IServiceTemplateCatalogService _templateCatalog;
    private readonly IProvisionedEnvironmentService _environmentService;
    private readonly ConfigService _configService;
    private readonly IGovernanceValidationService _governanceService;

    public override string Name => "create_environment_from_template";

    public override string Description =>
        "Create a new Azure environment from an approved service template. " +
        "Specify the template ID and provide values for required parameters. " +
        "If subscriptionId is not provided, uses the configured default subscription. " +
        "The platform will validate parameters against guardrails and provision all resources. " +
        "Use 'list_service_templates' and 'get_template_details' first to understand available options.";

    public CreateEnvironmentFromTemplateTool(
        ILogger<CreateEnvironmentFromTemplateTool> logger,
        IServiceTemplateCatalogService templateCatalog,
        IProvisionedEnvironmentService environmentService,
        ConfigService configService,
        IGovernanceValidationService governanceService) : base(logger)
    {
        _templateCatalog = templateCatalog ?? throw new ArgumentNullException(nameof(templateCatalog));
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _governanceService = governanceService ?? throw new ArgumentNullException(nameof(governanceService));

        Parameters.Add(new ToolParameter(
            name: "templateId",
            description: "The ID or name of the service template to use (required)",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "environmentName",
            description: "Name for the new environment (required, must be unique)",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Azure subscription ID for deployment. If not provided, uses the configured default subscription.",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "resourceGroupName",
            description: "Resource group name (required for resource-group scoped templates, optional for subscription-scoped templates that create their own resource groups)",
            required: false));  // Made optional - validation depends on template scope

        Parameters.Add(new ToolParameter(
            name: "location",
            description: "Azure region for deployment (e.g., eastus, westus2). Default: eastus",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "parameters",
            description: "JSON object with template-specific parameters. Use 'get_template_details' to see required parameters.",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "tags",
            description: "Optional JSON object with additional resource tags",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "ownerEmail",
            description: "Email of the environment owner for notifications",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "expirationDays",
            description: "Number of days until environment expires (optional, uses template default)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var templateId = GetOptionalString(arguments, "templateId");
        var environmentName = GetOptionalString(arguments, "environmentName");
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var resourceGroupName = GetOptionalString(arguments, "resourceGroupName");
        var location = GetOptionalString(arguments, "location") ?? "eastus";
        var parametersJson = GetOptionalString(arguments, "parameters");
        var tagsJson = GetOptionalString(arguments, "tags");
        var ownerEmail = GetOptionalString(arguments, "ownerEmail");
        var expirationDays = GetOptionalInt(arguments, "expirationDays");

        // Basic validation
        if (string.IsNullOrWhiteSpace(templateId))
            return ToJson(new { success = false, error = "Template ID is required" });
        if (string.IsNullOrWhiteSpace(environmentName))
            return ToJson(new { success = false, error = "Environment name is required" });
        
        // Auto-fill subscription from configuration if not provided
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            subscriptionId = _configService.GetDefaultSubscription();
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                Logger.LogInformation("Using configured default subscription: {SubscriptionId}", subscriptionId);
            }
        }

        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            return ToJson(new { 
                success = false, 
                error = "Subscription ID is required. Either provide subscriptionId parameter or set a default using 'Set my subscription to <id>'" 
            });
        }

        Logger.LogInformation("🚀 Creating environment {Name} from template {Template}",
            environmentName, templateId);

        try
        {
            // Resolve template FIRST to check deployment scope
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

            // Check if resource group is required based on deployment scope
            var isSubscriptionScoped = template.DeploymentScope?.Equals("subscription", StringComparison.OrdinalIgnoreCase) == true;
            
            if (!isSubscriptionScoped && string.IsNullOrWhiteSpace(resourceGroupName))
            {
                return ToJson(new 
                { 
                    success = false, 
                    error = "Resource group name is required for this template",
                    hint = "This template deploys at resource group scope. Provide a resourceGroupName."
                });
            }

            // For subscription-scoped templates, resource group is not needed (template creates its own)
            if (isSubscriptionScoped)
            {
                Logger.LogInformation("📦 Template {Template} is subscription-scoped - will create its own resource groups",
                    template.Name);
                // Clear any provided resource group name since the template manages this
                resourceGroupName = null;
            }

            // Parse parameters
            var parameters = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(parametersJson))
            {
                try
                {
                    parameters = JsonSerializer.Deserialize<Dictionary<string, object>>(parametersJson)
                        ?? new Dictionary<string, object>();
                }
                catch (JsonException)
                {
                    return ToJson(new { success = false, error = "Invalid JSON format for parameters" });
                }
            }

            // Apply defaults for missing optional parameters
            foreach (var param in template.Parameters.Where(p => !p.Required && p.DefaultValue != null))
            {
                if (!parameters.ContainsKey(param.Name))
                {
                    parameters[param.Name] = param.DefaultValue!;
                }
            }

            // Parse tags
            var tags = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(tagsJson))
            {
                try
                {
                    tags = JsonSerializer.Deserialize<Dictionary<string, string>>(tagsJson)
                        ?? new Dictionary<string, string>();
                }
                catch (JsonException)
                {
                    return ToJson(new { success = false, error = "Invalid JSON format for tags" });
                }
            }

            // ═══════════════════════════════════════════════════════════════════
            // GOVERNANCE VALIDATION - Runtime enforcement of platform policies
            // ═══════════════════════════════════════════════════════════════════
            Logger.LogInformation("🛡️ Validating governance policies before provisioning...");

            var governanceRequest = new GovernanceValidationRequest
            {
                Location = location,
                EnvironmentName = environmentName,
                ResourceGroupName = resourceGroupName,
                Tags = tags,
                TemplateId = templateId,
                Parameters = parameters,
                RequestedBy = ownerEmail ?? "environment-agent"
            };

            var governanceResult = await _governanceService.ValidateAsync(governanceRequest, cancellationToken);

            if (!governanceResult.IsValid)
            {
                Logger.LogWarning("❌ Governance validation failed: {Errors}", 
                    string.Join("; ", governanceResult.Errors));

                return ToJson(new
                {
                    success = false,
                    error = "Governance policy validation failed",
                    governanceViolations = governanceResult.Violations.Select(v => new
                    {
                        policyType = v.PolicyType.ToString(),
                        message = v.Message,
                        property = v.Property,
                        providedValue = v.ProvidedValue,
                        allowedValue = v.AllowedValue,
                        severity = v.Severity.ToString(),
                        nistControls = v.NistControls
                    }),
                    hint = "Correct the violations and try again. Use approved regions and naming conventions."
                });
            }

            // Log any warnings
            if (governanceResult.Warnings.Any())
            {
                Logger.LogWarning("⚠️ Governance warnings: {Warnings}", 
                    string.Join("; ", governanceResult.Warnings));
            }
            // ═══════════════════════════════════════════════════════════════════

            // Create request
            var request = new CreateEnvironmentFromTemplateRequest
            {
                TemplateId = template.Id,
                EnvironmentName = environmentName,
                SubscriptionId = subscriptionId,
                ResourceGroupName = resourceGroupName,
                Location = location,
                Parameters = parameters,
                Tags = tags,
                OwnerEmail = ownerEmail,
                RequestedBy = ownerEmail ?? "environment-agent",
                ExpiresAt = expirationDays.HasValue
                    ? DateTime.UtcNow.AddDays(expirationDays.Value)
                    : null
            };

            // Create environment
            var result = await _environmentService.CreateFromTemplateAsync(request, cancellationToken);

            if (!result.Success)
            {
                Logger.LogWarning("⚠️ Environment creation failed: {Errors}", string.Join("; ", result.Errors));
                return ToJson(new
                {
                    success = false,
                    errors = result.Errors,
                    guardrailViolations = result.GuardrailViolations?.Select(v => new
                    {
                        guardrail = v.GuardrailName,
                        property = v.Property,
                        providedValue = v.ProvidedValue,
                        requiredValue = v.RequiredValue,
                        message = v.Message
                    })
                });
            }

            Logger.LogInformation("✅ Environment {Name} created successfully (ID: {Id})",
                result.Environment!.Name, result.Environment.Id);

            return ToJson(new
            {
                success = true,
                message = $"Environment '{environmentName}' created successfully from template '{template.DisplayName}'",
                environment = new
                {
                    id = result.Environment!.Id,
                    name = result.Environment.Name,
                    templateName = result.Environment.TemplateName,
                    templateVersion = result.Environment.TemplateVersion,
                    subscriptionId = result.Environment.SubscriptionId,
                    resourceGroupName = result.Environment.ResourceGroupName,
                    location = result.Environment.Location,
                    status = result.Environment.Status.ToString(),
                    createdAt = result.Environment.CreatedAt,
                    expiresAt = result.Environment.ExpiresAt,
                    deployedResources = result.Environment.DeployedResources?.Select(r => new
                    {
                        name = r.Name,
                        type = r.Type,
                        resourceId = r.ResourceId,
                        provisioningState = r.ProvisioningState
                    }) ?? Enumerable.Empty<object>(),
                    tags = result.Environment.Tags
                },
                deploymentId = result.DeploymentId,
                warnings = result.GuardrailViolations?.Where(v => v.Action != GuardrailAction.Deny)
                    .Select(v => v.Message).ToList(),
                nextSteps = new[]
                {
                    "Use 'list_provisioned_environments' to view all environments",
                    "Use 'get_environment_health' to check environment status",
                    "Use 'detect_environment_drift' to monitor configuration drift"
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Failed to create environment from template");
            return ToJson(new { success = false, error = ex.Message });
        }
    }
}
