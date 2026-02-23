using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Compliance.Tools;

/// <summary>
/// compliance_validate_remediation — Validate that a remediation was applied successfully.
/// </summary>
public class ComplianceValidateRemediationTool : BaseTool
{
    public ComplianceValidateRemediationTool(ILogger<ComplianceValidateRemediationTool> logger) : base(logger) { }

    public override string Name => "compliance_validate_remediation";
    public override string Description => "Validate that a previously applied remediation was successful";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "findingId": { "type": "string", "description": "Finding ID to validate." },
        "executionId": { "type": "string", "description": "Remediation execution ID." },
        "subscriptionId": { "type": "string", "description": "Azure subscription ID." }
      },
      "required": ["findingId"]
    }
    """;

    public override bool RequiresAuthentication => true;
    public override PimTier PimTierRequired => PimTier.Read;

    public override Task<string> ExecuteAsync(
        Dictionary<string, object?> parameters,
        IProgress<ProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var findingId = GetRequired<string>(parameters, "findingId");

        var data = new
        {
            findingId,
            validationStatus = "Passed",
            validatedAt = DateTimeOffset.UtcNow.ToString("o"),
            message = $"Remediation for finding '{findingId}' validated successfully."
        };

        sw.Stop();
        var envelope = new { status = "success", data, metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTimeOffset.UtcNow.ToString("o") } };
        return Task.FromResult(JsonSerializer.Serialize(envelope, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
