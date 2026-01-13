using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;

namespace Platform.Engineering.Copilot.Agents.Environments.Tools;

/// <summary>
/// Tool for listing provisioned environments with filtering and status information.
/// </summary>
public class ProvisionedEnvironmentListTool : BaseTool
{
    private readonly IProvisionedEnvironmentService _environmentService;

    public override string Name => "list_provisioned_environments";

    public override string Description =>
        "List provisioned environments created from service templates. " +
        "Shows status, template used, drift detection results, and expiration dates. " +
        "Filter by subscription, template, status, owner, or drift state.";

    public ProvisionedEnvironmentListTool(
        ILogger<ProvisionedEnvironmentListTool> logger,
        IProvisionedEnvironmentService environmentService) : base(logger)
    {
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));

        Parameters.Add(new ToolParameter(
            name: "subscriptionId",
            description: "Filter by Azure subscription ID (optional)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "templateName",
            description: "Filter by template name (optional)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "status",
            description: "Filter by status: Running, Provisioning, Updating, Failed, Stopped (optional)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "createdBy",
            description: "Filter by creator (optional)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "hasDrift",
            description: "Filter by drift status: true or false (optional)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetOptionalString(arguments, "subscriptionId");
        var templateName = GetOptionalString(arguments, "templateName");
        var statusStr = GetOptionalString(arguments, "status");
        var createdBy = GetOptionalString(arguments, "createdBy");
        var hasDriftStr = GetOptionalString(arguments, "hasDrift");

        Logger.LogInformation("📋 Listing provisioned environments");

        try
        {
            // Build search criteria
            var criteria = new EnvironmentSearchCriteria
            {
                SubscriptionId = subscriptionId,
                CreatedBy = createdBy,
                IncludeDeleted = false
            };

            if (!string.IsNullOrEmpty(statusStr) && Enum.TryParse<EnvironmentStatus>(statusStr, true, out var status))
            {
                criteria.Status = status;
            }

            if (!string.IsNullOrEmpty(hasDriftStr) && bool.TryParse(hasDriftStr, out var hasDrift))
            {
                criteria.HasDrift = hasDrift;
            }

            var environments = await _environmentService.SearchEnvironmentsAsync(criteria, cancellationToken);

            // Apply template name filter (post-query since it's on TemplateName)
            if (!string.IsNullOrEmpty(templateName))
            {
                environments = environments
                    .Where(e => e.TemplateName.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            var statusSummary = await _environmentService.GetStatusSummaryAsync(cancellationToken);

            Logger.LogInformation("✅ Found {Count} environments", environments.Count);

            return ToJson(new
            {
                success = true,
                summary = new
                {
                    total = statusSummary.TotalEnvironments,
                    running = statusSummary.RunningEnvironments,
                    provisioning = statusSummary.ProvisioningEnvironments,
                    failed = statusSummary.FailedEnvironments,
                    withDrift = statusSummary.EnvironmentsWithDrift,
                    expiringWithin7Days = statusSummary.ExpiringWithin7Days
                },
                count = environments.Count,
                environments = environments.Select(e => new
                {
                    id = e.Id,
                    name = e.Name,
                    description = e.Description,
                    templateName = e.TemplateName,
                    templateVersion = e.TemplateVersion,
                    subscriptionId = e.SubscriptionId,
                    resourceGroupName = e.ResourceGroupName,
                    location = e.Location,
                    status = e.Status.ToString(),
                    hasDrift = e.HasDrift,
                    driftItemCount = e.DriftItems.Count,
                    lastDriftCheck = e.LastDriftCheck,
                    createdBy = e.CreatedBy,
                    createdAt = e.CreatedAt,
                    expiresAt = e.ExpiresAt,
                    resourceCount = e.DeployedResources.Count,
                    tags = e.Tags.Take(5) // Limit tags in list view
                }),
                hint = environments.Any(e => e.HasDrift)
                    ? "⚠️ Some environments have configuration drift. Use 'detect_environment_drift' for details."
                    : null
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Failed to list provisioned environments");
            return ToJson(new { success = false, error = ex.Message });
        }
    }
}
