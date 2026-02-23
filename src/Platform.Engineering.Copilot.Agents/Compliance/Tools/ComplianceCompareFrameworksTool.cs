using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.Compliance.Tools;

/// <summary>
/// compliance_compare_frameworks — Compare frameworks via INistService.CompareFrameworks.
/// No auth required.
/// </summary>
public class ComplianceCompareFrameworksTool : BaseTool
{
    private readonly INistService _nistService;

    public ComplianceCompareFrameworksTool(INistService nistService, ILogger<ComplianceCompareFrameworksTool> logger)
        : base(logger)
    {
        _nistService = nistService;
    }

    public override string Name => "compliance_compare_frameworks";
    public override string Description => "Compare two compliance frameworks showing overlap and differences";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "framework1": { "type": "string", "description": "First framework (e.g., 'FedRAMPHigh')." },
        "framework2": { "type": "string", "description": "Second framework (e.g., 'DoDIL5')." }
      },
      "required": ["framework1", "framework2"]
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
        var framework1Str = GetRequired<string>(parameters, "framework1");
        var framework2Str = GetRequired<string>(parameters, "framework2");

        if (!Enum.TryParse<ComplianceFramework>(framework1Str, true, out var fw1))
        {
            sw.Stop();
            var err = new { status = "error", error = new { errorCode = "INVALID_FRAMEWORK", message = $"Unknown framework '{framework1Str}'." }, metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTimeOffset.UtcNow.ToString("o") } };
            return Task.FromResult(JsonSerializer.Serialize(err, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        if (!Enum.TryParse<ComplianceFramework>(framework2Str, true, out var fw2))
        {
            sw.Stop();
            var err = new { status = "error", error = new { errorCode = "INVALID_FRAMEWORK", message = $"Unknown framework '{framework2Str}'." }, metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTimeOffset.UtcNow.ToString("o") } };
            return Task.FromResult(JsonSerializer.Serialize(err, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }

        var comparison = _nistService.CompareFrameworks(fw1, fw2);

        var commonIds = comparison.Common.Select(c => c.ControlId).ToList();
        var uniqueToA = comparison.UniqueToA.Select(c => c.ControlId).ToList();
        var uniqueToB = comparison.UniqueToB.Select(c => c.ControlId).ToList();

        var data = new
        {
            framework1 = comparison.FrameworkA.ToString(),
            framework2 = comparison.FrameworkB.ToString(),
            commonControlIds = commonIds,
            onlyInFramework1 = uniqueToA,
            onlyInFramework2 = uniqueToB,
            commonCount = commonIds.Count,
            onlyIn1Count = uniqueToA.Count,
            onlyIn2Count = uniqueToB.Count,
            overlapPercentage = commonIds.Count > 0
                ? Math.Round(commonIds.Count * 100.0 /
                    (commonIds.Count + uniqueToA.Count + uniqueToB.Count), 1)
                : 0.0
        };

        sw.Stop();
        var envelope = new { status = "success", data, metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTimeOffset.UtcNow.ToString("o") } };
        return Task.FromResult(JsonSerializer.Serialize(envelope, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
