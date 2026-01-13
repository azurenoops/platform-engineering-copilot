using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;

namespace Platform.Engineering.Copilot.Agents.Environments.Tools;

/// <summary>
/// Tool for cloning an existing provisioned environment.
/// Creates a new environment using the same template and parameters.
/// </summary>
public class EnvironmentCloneFromTemplateTool : BaseTool
{
    private readonly IProvisionedEnvironmentService _environmentService;

    public override string Name => "clone_provisioned_environment";

    public override string Description =>
        "Clone an existing provisioned environment to create a copy with the same template and configuration. " +
        "Useful for creating test/staging copies of production environments or duplicating successful configurations.";

    public EnvironmentCloneFromTemplateTool(
        ILogger<EnvironmentCloneFromTemplateTool> logger,
        IProvisionedEnvironmentService environmentService) : base(logger)
    {
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));

        Parameters.Add(new ToolParameter(
            name: "sourceEnvironmentId",
            description: "ID or name of the environment to clone (required)",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "newEnvironmentName",
            description: "Name for the cloned environment (required)",
            required: true));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var sourceEnvironmentId = GetOptionalString(arguments, "sourceEnvironmentId");
        var newEnvironmentName = GetOptionalString(arguments, "newEnvironmentName");

        if (string.IsNullOrWhiteSpace(sourceEnvironmentId))
            return ToJson(new { success = false, error = "Source environment ID is required" });
        if (string.IsNullOrWhiteSpace(newEnvironmentName))
            return ToJson(new { success = false, error = "New environment name is required" });

        Logger.LogInformation("📋 Cloning environment {Source} to {New}",
            sourceEnvironmentId, newEnvironmentName);

        try
        {
            // Get source environment
            var sourceEnv = await _environmentService.GetEnvironmentAsync(sourceEnvironmentId, cancellationToken)
                ?? await _environmentService.GetEnvironmentByNameAsync(sourceEnvironmentId, cancellationToken);

            if (sourceEnv == null)
            {
                return ToJson(new
                {
                    success = false,
                    error = $"Source environment '{sourceEnvironmentId}' not found",
                    hint = "Use 'list_provisioned_environments' to see available environments"
                });
            }

            // Clone the environment
            var result = await _environmentService.CloneEnvironmentAsync(
                sourceEnv.Id,
                newEnvironmentName,
                "environment-agent", // TODO: Get from context
                cancellationToken);

            if (!result.Success)
            {
                Logger.LogWarning("⚠️ Clone failed: {Errors}", string.Join("; ", result.Errors));
                return ToJson(new
                {
                    success = false,
                    errors = result.Errors
                });
            }

            Logger.LogInformation("✅ Environment cloned: {SourceName} → {NewName}",
                sourceEnv.Name, result.Environment!.Name);

            return ToJson(new
            {
                success = true,
                message = $"Environment '{sourceEnv.Name}' cloned to '{result.Environment!.Name}'",
                source = new
                {
                    id = sourceEnv.Id,
                    name = sourceEnv.Name,
                    templateName = sourceEnv.TemplateName,
                    templateVersion = sourceEnv.TemplateVersion
                },
                clonedEnvironment = new
                {
                    id = result.Environment.Id,
                    name = result.Environment.Name,
                    description = result.Environment.Description,
                    templateName = result.Environment.TemplateName,
                    templateVersion = result.Environment.TemplateVersion,
                    subscriptionId = result.Environment.SubscriptionId,
                    resourceGroupName = result.Environment.ResourceGroupName,
                    location = result.Environment.Location,
                    status = result.Environment.Status.ToString(),
                    createdAt = result.Environment.CreatedAt,
                    clonedFromId = result.Environment.ClonedFromId,
                    resourceCount = result.Environment.DeployedResources.Count
                },
                nextSteps = new[]
                {
                    "The cloned environment has the same configuration as the source",
                    "Use 'scale_provisioned_environment' if you need to adjust resource sizing",
                    "Use 'list_provisioned_environments' to see both environments"
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Failed to clone environment");
            return ToJson(new { success = false, error = ex.Message });
        }
    }
}
