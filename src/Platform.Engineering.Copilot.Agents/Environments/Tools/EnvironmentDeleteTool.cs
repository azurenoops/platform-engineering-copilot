using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;

namespace Platform.Engineering.Copilot.Agents.Environments.Tools;

/// <summary>
/// Tool for deleting a provisioned environment.
/// Includes safety checks and audit trail.
/// </summary>
public class EnvironmentDeleteTool : BaseTool
{
    private readonly IProvisionedEnvironmentService _environmentService;

    public override string Name => "delete_provisioned_environment";

    public override string Description =>
        "Delete a provisioned environment and all its Azure resources. " +
        "This is a destructive operation. Use 'forceDelete' to skip confirmation prompts. " +
        "The operation is logged for audit purposes.";

    public EnvironmentDeleteTool(
        ILogger<EnvironmentDeleteTool> logger,
        IProvisionedEnvironmentService environmentService) : base(logger)
    {
        _environmentService = environmentService ?? throw new ArgumentNullException(nameof(environmentService));

        Parameters.Add(new ToolParameter(
            name: "environmentId",
            description: "ID or name of the environment to delete (required)",
            required: true));

        Parameters.Add(new ToolParameter(
            name: "forceDelete",
            description: "Set to 'true' to force deletion without additional checks (default: false)",
            required: false));

        Parameters.Add(new ToolParameter(
            name: "reason",
            description: "Reason for deletion (for audit trail)",
            required: false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var environmentId = GetOptionalString(arguments, "environmentId");
        var forceDeleteStr = GetOptionalString(arguments, "forceDelete");
        var reason = GetOptionalString(arguments, "reason");
        var forceDelete = string.Equals(forceDeleteStr, "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(environmentId))
            return ToJson(new { success = false, error = "Environment ID is required" });

        Logger.LogInformation("🗑️ Deleting environment {EnvironmentId} (Force: {Force})",
            environmentId, forceDelete);

        try
        {
            // Get environment details
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

            // Deletion preview (unless force)
            if (!forceDelete)
            {
                return ToJson(new
                {
                    success = true,
                    pendingAction = "delete",
                    message = "⚠️ This will permanently delete the environment and all resources. Please confirm.",
                    environment = new
                    {
                        id = environment.Id,
                        name = environment.Name,
                        templateName = environment.TemplateName,
                        resourceGroup = environment.ResourceGroupName,
                        subscriptionId = environment.SubscriptionId,
                        status = environment.Status.ToString(),
                        createdAt = environment.CreatedAt,
                        createdBy = environment.CreatedBy,
                        resourceCount = environment.DeployedResources.Count,
                        resources = environment.DeployedResources.Select(r => new
                        {
                            name = r.Name,
                            type = r.Type
                        })
                    },
                    instruction = "To confirm deletion, call this tool again with 'forceDelete' set to 'true'"
                });
            }

            // Execute deletion
            var deleted = await _environmentService.DeleteEnvironmentAsync(
                environment.Id,
                "environment-agent", // TODO: Get from context
                forceDelete,
                cancellationToken);

            if (!deleted)
            {
                return ToJson(new
                {
                    success = false,
                    error = "Failed to delete environment"
                });
            }

            Logger.LogInformation("✅ Environment {Name} deleted successfully", environment.Name);

            return ToJson(new
            {
                success = true,
                message = $"Environment '{environment.Name}' has been deleted",
                deletedEnvironment = new
                {
                    id = environment.Id,
                    name = environment.Name,
                    templateName = environment.TemplateName,
                    resourceGroup = environment.ResourceGroupName
                },
                audit = new
                {
                    action = "Deleted",
                    reason = reason,
                    timestamp = DateTime.UtcNow,
                    deletedBy = "environment-agent"
                },
                resourcesDeleted = environment.DeployedResources.Count
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "❌ Failed to delete environment");
            return ToJson(new { success = false, error = ex.Message });
        }
    }
}
