using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;

namespace Platform.Engineering.Copilot.Agents.Environments.Tools;

/// <summary>
/// Tool for remediating configuration drift by re-applying template configuration.
/// </summary>
public class EnvironmentDriftRemediationTool : BaseTool
{
    private readonly IProvisionedEnvironmentService _environmentService;

    public override string Name => "remediate_environment_drift";

    public override string Description =>
        "Remediate configuration drift in a provisioned environment by re-applying the template configuration. " +
        "Can remediate all drift items or specific items. " +
        "Use 'detect_environment_drift' first to identify drift.";

    public EnvironmentDriftRemediationTool(
        ILogger<EnvironmentDriftRemediationTool> logger,
        IProvisionedEnvironmentService environmentService) : base(logger)
    {
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));

        Parameters.Add(new ToolParameter(
            name: "environmentId",
            description: "ID or name of the environment to remediate (required)",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "driftItemIds",
            description: "Optional JSON array of specific drift item IDs to remediate. If not provided, remediates all drift.",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "dryRun",
            description: "Set to 'true' to preview remediation without applying changes",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var environmentId = GetOptionalString(arguments, "environmentId");
        var driftItemIdsJson = GetOptionalString(arguments, "driftItemIds");
        var dryRunStr = GetOptionalString(arguments, "dryRun");
        var dryRun = string.Equals(dryRunStr, "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(environmentId))
            return ToJson(new { success = false, error = "Environment ID is required" });

        Logger.LogInformation("🔧 Remediating drift for environment {EnvironmentId} (DryRun: {DryRun})",
            environmentId, dryRun);

        try
        {
            // Get environment
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

            // Check if there's drift to remediate
            if (!environment.HasDrift || !environment.DriftItems.Any())
            {
                return ToJson(new
                {
                    success = true,
                    message = "No drift detected in this environment. Nothing to remediate.",
                    environmentId = environment.Id,
                    environmentName = environment.Name
                });
            }

            // Parse drift item IDs if provided
            List<string>? driftItemIds = null;
            if (!string.IsNullOrEmpty(driftItemIdsJson))
            {
                try
                {
                    driftItemIds = JsonSerializer.Deserialize<List<string>>(driftItemIdsJson);
                }
                catch (JsonException)
                {
                    return ToJson(new { success = false, error = "Invalid JSON format for driftItemIds" });
                }
            }

            // Preview mode
            if (dryRun)
            {
                var itemsToRemediate = driftItemIds == null
                    ? environment.DriftItems.ToList()
                    : environment.DriftItems.Where(d => driftItemIds.Contains(d.Id)).ToList();

                return ToJson(new
                {
                    success = true,
                    dryRun = true,
                    message = "Preview of remediation (no changes applied)",
                    environmentId = environment.Id,
                    environmentName = environment.Name,
                    itemsToRemediate = itemsToRemediate.Count,
                    remediationPlan = itemsToRemediate.Select(d => new
                    {
                        driftItemId = d.Id,
                        resourceName = d.ResourceName,
                        property = d.Property,
                        action = $"Revert '{d.Property}' from '{d.ActualValue}' to '{d.ExpectedValue}'"
                    }),
                    hint = "Remove 'dryRun' parameter to apply these changes"
                });
            }

            // Execute remediation
            var result = await _environmentService.RemediateDriftAsync(
                environment.Id,
                driftItemIds,
                "environment-agent", // TODO: Get from context
                cancellationToken);

            if (!result.Success)
            {
                return ToJson(new
                {
                    success = false,
                    errors = result.Errors
                });
            }

            Logger.LogInformation("✅ Remediated {Count} drift items for environment {Name}",
                result.RemediatedItems.Count, environment.Name);

            return ToJson(new
            {
                success = true,
                message = $"Successfully remediated {result.RemediatedItems.Count} drift items",
                environmentId = environment.Id,
                environmentName = environment.Name,
                remediatedCount = result.RemediatedItems.Count,
                failedCount = result.FailedItems.Count,
                remainingDriftCount = result.RemainingDriftCount,
                remediatedItems = result.RemediatedItems,
                failedItems = result.FailedItems.Any() ? result.FailedItems : null,
                nextSteps = result.RemainingDriftCount > 0
                    ? new[] { "Some drift items could not be remediated automatically. Review failed items and remediate manually." }
                    : new[] { "All drift items have been remediated. Environment is now in compliance." }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Failed to remediate drift");
            return ToJson(new { success = false, error = ex.Message });
        }
    }
}
