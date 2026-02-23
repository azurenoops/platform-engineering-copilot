using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;

/// <summary>
/// compare_frameworks — Compare controls across two compliance frameworks.
/// Shows common, unique-to-A, and unique-to-B controls.
/// No auth required per SC-008.
/// </summary>
public class CompareFrameworksTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly INistService _nistService;

    public CompareFrameworksTool(INistService nistService, ILogger<CompareFrameworksTool> logger)
        : base(logger) => _nistService = nistService;

    public override string Name => "compare_frameworks";
    public override string Description => "Compare controls across two compliance frameworks";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "frameworkA": { "type": "string", "enum": ["Nist80053Rev5", "FedRampHigh", "FedRampModerate", "DoDIL5"], "description": "First framework." },
        "frameworkB": { "type": "string", "enum": ["Nist80053Rev5", "FedRampHigh", "FedRampModerate", "DoDIL5"], "description": "Second framework." }
      },
      "required": ["frameworkA", "frameworkB"]
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
        var frameworkAStr = GetRequired<string>(parameters, "frameworkA");
        var frameworkBStr = GetRequired<string>(parameters, "frameworkB");

        if (!TryParseFramework(frameworkAStr, out var frameworkA))
        {
            sw.Stop();
            return Task.FromResult(BuildError("INVALID_FRAMEWORK",
                $"Framework '{frameworkAStr}' is not recognized.",
                "Use one of: Nist80053Rev5, FedRampHigh, FedRampModerate, DoDIL5", sw));
        }

        if (!TryParseFramework(frameworkBStr, out var frameworkB))
        {
            sw.Stop();
            return Task.FromResult(BuildError("INVALID_FRAMEWORK",
                $"Framework '{frameworkBStr}' is not recognized.",
                "Use one of: Nist80053Rev5, FedRampHigh, FedRampModerate, DoDIL5", sw));
        }

        var comparison = _nistService.CompareFrameworks(frameworkA, frameworkB);

        var result = new
        {
            frameworkA = frameworkA.ToString(),
            frameworkB = frameworkB.ToString(),
            totalA = comparison.TotalA,
            totalB = comparison.TotalB,
            commonCount = comparison.Common.Count,
            uniqueToACount = comparison.UniqueToA.Count,
            uniqueToBCount = comparison.UniqueToB.Count,
            common = comparison.Common.Take(10).Select(c => new { c.ControlId, c.Title }).ToArray(),
            uniqueToA = comparison.UniqueToA.Take(10).Select(c => new { c.ControlId, c.Title }).ToArray(),
            uniqueToB = comparison.UniqueToB.Take(10).Select(c => new { c.ControlId, c.Title }).ToArray()
        };

        sw.Stop();
        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static bool TryParseFramework(string value, out ComplianceFramework framework)
    {
        return Enum.TryParse(value, ignoreCase: true, out framework);
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
