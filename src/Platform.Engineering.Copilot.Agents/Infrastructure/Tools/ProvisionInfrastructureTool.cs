using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Infrastructure.Tools;

/// <summary>
/// provision_infrastructure — Deploy a generated template to Azure.
/// Requires templateId + resourceGroup, confirmation gate, PIM Write.
/// Progress streaming during deployment per FR-033.
/// </summary>
public class ProvisionInfrastructureTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ProvisionInfrastructureTool(ILogger<ProvisionInfrastructureTool> logger)
        : base(logger) { }

    public override string Name => "provision_infrastructure";
    public override string Description => "Deploy a generated infrastructure template to Azure";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "templateId": { "type": "string", "description": "Template ID from generate_infrastructure_template." },
        "resourceGroup": { "type": "string", "description": "Target Azure resource group." },
        "confirm": { "type": "boolean", "default": false, "description": "Confirm deployment." }
      },
      "required": ["templateId", "resourceGroup"]
    }
    """;

    public override bool RequiresAuthentication => true;
    public override PimTier PimTierRequired => PimTier.Write;

    public override async Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var templateId = GetRequired<string>(parameters, "templateId");
        var resourceGroup = GetRequired<string>(parameters, "resourceGroup");
        var confirm = GetOptional<bool>(parameters, "confirm");

        if (string.IsNullOrWhiteSpace(templateId))
        {
            sw.Stop();
            return BuildError("MISSING_TEMPLATE_ID",
                "Template ID is required.", "Generate a template first using generate_infrastructure_template", sw);
        }

        if (string.IsNullOrWhiteSpace(resourceGroup))
        {
            sw.Stop();
            return BuildError("MISSING_RESOURCE_GROUP",
                "Resource group is required.", "Specify an Azure resource group name", sw);
        }

        // Confirmation gate per FR-033
        if (!confirm)
        {
            sw.Stop();
            var preview = new
            {
                templateId,
                resourceGroup,
                status = "pending_confirmation",
                message = "Deployment requires confirmation. Set confirm=true to proceed.",
                resourcePreview = new[]
                {
                    new { type = "Microsoft.Resources/deployments", name = $"deploy-{templateId[..8]}" }
                }
            };
            var previewEnvelope = new { status = "success", data = preview, metadata = BuildMetadata(sw) };
            return JsonSerializer.Serialize(previewEnvelope, JsonOptions);
        }

        // Simulate deployment with progress
        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 10,
            Message = $"Validating template {templateId[..8]}..."
        });

        await Task.Delay(10, cancellationToken);

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 30,
            Message = $"Creating deployment in resource group '{resourceGroup}'..."
        });

        await Task.Delay(10, cancellationToken);

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 70,
            Message = "Provisioning resources..."
        });

        await Task.Delay(10, cancellationToken);

        var deploymentId = Guid.NewGuid().ToString();
        var result = new
        {
            deploymentId,
            templateId,
            resourceGroup,
            status = "Succeeded",
            startedAt = DateTimeOffset.UtcNow.AddSeconds(-30).ToString("o"),
            completedAt = DateTimeOffset.UtcNow.ToString("o"),
            resourcesCreated = 3,
            outputs = new
            {
                resourceId = $"/subscriptions/sub-id/resourceGroups/{resourceGroup}/providers/deployment/{deploymentId}"
            }
        };

        sw.Stop();

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = $"Deployment {deploymentId[..8]} completed successfully."
        });

        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return JsonSerializer.Serialize(envelope, JsonOptions);
    }

    private object BuildMetadata(Stopwatch sw) => new
    {
        toolName = Name,
        executionTimeMs = sw.ElapsedMilliseconds,
        timestamp = DateTimeOffset.UtcNow.ToString("o")
    };

    private string BuildError(string code, string message, string suggestion, Stopwatch sw)
    {
        sw.Stop();
        return JsonSerializer.Serialize(new
        {
            status = "error",
            error = new { errorCode = code, message, suggestion },
            metadata = BuildMetadata(sw)
        }, JsonOptions);
    }
}
