using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;

namespace Platform.Engineering.Copilot.Agents.Environments.Tools;

/// <summary>
/// Tool for detecting configuration drift in provisioned environments.
/// Integrates with Compliance Agent for comprehensive drift analysis.
/// </summary>
public class EnvironmentDriftDetectionTool : BaseTool
{
    private readonly IProvisionedEnvironmentService _environmentService;

    public override string Name => "detect_environment_drift";

    public override string Description =>
        "Detect configuration drift in a provisioned environment by comparing current Azure state " +
        "against the expected template configuration. Returns drift items with severity and remediation options. " +
        "Use 'remediate_environment_drift' to auto-fix detected issues.";

    public EnvironmentDriftDetectionTool(
        ILogger<EnvironmentDriftDetectionTool> logger,
        IProvisionedEnvironmentService environmentService) : base(logger)
    {
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));

        Parameters.Add(new ToolParameter(
            name: "environmentId",
            description: "ID or name of the environment to check for drift (optional, checks all if not specified)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "checkAll",
            description: "Set to 'true' to check all running environments for drift",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var environmentId = GetOptionalString(arguments, "environmentId");
        var checkAllStr = GetOptionalString(arguments, "checkAll");
        var checkAll = string.Equals(checkAllStr, "true", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (checkAll || string.IsNullOrEmpty(environmentId))
            {
                Logger.LogInformation("🔍 Checking all environments for drift");

                var summaries = await _environmentService.DetectAllDriftAsync(cancellationToken);

                var withDrift = summaries.Where(s => s.HasDrift).ToList();
                var noDrift = summaries.Where(s => !s.HasDrift).ToList();

                Logger.LogInformation("✅ Drift check complete: {WithDrift} with drift, {NoDrift} clean",
                    withDrift.Count, noDrift.Count);

                return ToJson(new
                {
                    success = true,
                    summary = new
                    {
                        totalChecked = summaries.Count,
                        environmentsWithDrift = withDrift.Count,
                        cleanEnvironments = noDrift.Count,
                        totalDriftItems = summaries.Sum(s => s.DriftItemCount),
                        criticalDriftItems = summaries.Sum(s => s.CriticalDriftCount)
                    },
                    environmentsWithDrift = withDrift.Select(s => new
                    {
                        environmentId = s.EnvironmentId,
                        environmentName = s.EnvironmentName,
                        driftItemCount = s.DriftItemCount,
                        criticalCount = s.CriticalDriftCount,
                        warningCount = s.WarningDriftCount,
                        lastChecked = s.LastChecked
                    }),
                    hint = withDrift.Any()
                        ? "Use 'detect_environment_drift' with a specific environmentId for detailed drift items, or 'remediate_environment_drift' to fix."
                        : "All environments are in compliance with their templates."
                });
            }

            // Single environment drift check
            Logger.LogInformation("🔍 Checking environment {EnvironmentId} for drift", environmentId);

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

            var result = await _environmentService.DetectDriftAsync(environment.Id, cancellationToken);

            if (!result.Success)
            {
                return ToJson(new
                {
                    success = false,
                    errors = result.Errors
                });
            }

            Logger.LogInformation("✅ Drift check complete for {Name}: {HasDrift}",
                environment.Name, result.HasDrift ? "Drift detected" : "No drift");

            return ToJson(new
            {
                success = true,
                environmentId = environment.Id,
                environmentName = environment.Name,
                templateName = environment.TemplateName,
                templateVersion = environment.TemplateVersion,
                hasDrift = result.HasDrift,
                summary = new
                {
                    totalDriftItems = result.DriftItems.Count,
                    criticalCount = result.DriftItems.Count(d => d.Severity == "Critical"),
                    warningCount = result.DriftItems.Count(d => d.Severity == "Warning"),
                    infoCount = result.DriftItems.Count(d => d.Severity == "Info")
                },
                driftItems = result.DriftItems.Select(d => new
                {
                    id = d.Id,
                    resourceName = d.ResourceName,
                    resourceId = d.ResourceId,
                    property = d.Property,
                    expectedValue = d.ExpectedValue,
                    actualValue = d.ActualValue,
                    driftType = d.DriftType,
                    severity = d.Severity,
                    detectedAt = d.DetectedAt
                }),
                lastDriftCheck = environment.LastDriftCheck,
                nextSteps = result.HasDrift
                    ? new[]
                    {
                        "Review drift items above to understand configuration changes",
                        "Use 'remediate_environment_drift' to auto-remediate drift items",
                        "Contact platform team if drift is intentional (may need template update)"
                    }
                    : new[] { "Environment configuration matches expected template state" }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Failed to detect drift");
            return ToJson(new { success = false, error = ex.Message });
        }
    }
}
