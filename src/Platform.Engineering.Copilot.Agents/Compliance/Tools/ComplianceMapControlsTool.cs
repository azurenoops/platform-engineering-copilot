using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.Compliance.Tools;

/// <summary>
/// compliance_map_controls — Map resources to controls via INistService.
/// </summary>
public class ComplianceMapControlsTool : BaseTool
{
    private readonly INistService _nistService;

    public ComplianceMapControlsTool(INistService nistService, ILogger<ComplianceMapControlsTool> logger)
        : base(logger)
    {
        _nistService = nistService;
    }

    public override string Name => "compliance_map_controls";
    public override string Description => "Map Azure resources to NIST 800-53 controls";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "resourceType": { "type": "string", "description": "Azure resource type (e.g., 'microsoft.storage/storageaccounts')." },
        "controlId": { "type": "string", "description": "NIST control ID to map resources for." },
        "framework": { "type": "string", "description": "Compliance framework context." }
      },
      "required": []
    }
    """;

    public override bool RequiresAuthentication => false;
    public override PimTier PimTierRequired => PimTier.None;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var controlId = GetOptional<string>(parameters, "controlId");
        var resourceType = GetOptional<string>(parameters, "resourceType");

        object data;
        if (!string.IsNullOrEmpty(controlId))
        {
            var control = _nistService.GetControl(controlId);
            data = new
            {
                controlId,
                controlTitle = control?.Title ?? "Unknown",
                azureServices = control?.AzureServiceMappings ?? [],
                mappingCount = control?.AzureServiceMappings?.Length ?? 0
            };
        }
        else
        {
            var families = _nistService.GetFamilyCodes().ToList();
            data = new
            {
                availableFamilies = families,
                totalFamilies = families.Count,
                message = "Specify controlId for detailed resource mapping."
            };
        }

        sw.Stop();
        var envelope = new { status = "success", data, metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTimeOffset.UtcNow.ToString("o") } };
        return Task.FromResult(JsonSerializer.Serialize(envelope, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
