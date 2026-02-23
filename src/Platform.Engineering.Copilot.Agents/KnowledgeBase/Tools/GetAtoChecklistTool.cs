using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;

/// <summary>
/// get_ato_checklist — Provide an ATO preparation checklist for a framework and baseline.
/// Generates a phased checklist based on controls in the selected baseline.
/// No auth required per SC-008.
/// </summary>
public class GetAtoChecklistTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly INistService _nistService;

    public GetAtoChecklistTool(INistService nistService, ILogger<GetAtoChecklistTool> logger)
        : base(logger) => _nistService = nistService;

    public override string Name => "get_ato_checklist";
    public override string Description => "Generate an ATO preparation checklist for a framework and baseline";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "framework": { "type": "string", "enum": ["Nist80053Rev5", "FedRampHigh", "FedRampModerate", "DoDIL5"], "default": "FedRampHigh" },
        "baseline": { "type": "string", "enum": ["High", "Moderate", "Low"], "default": "High" }
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
        var baselineStr = GetOptional<string>(parameters, "baseline") ?? "High";

        if (!Enum.TryParse<BaselineLevel>(baselineStr, ignoreCase: true, out var baseline))
        {
            sw.Stop();
            return Task.FromResult(BuildError("INVALID_BASELINE",
                $"Baseline '{baselineStr}' is not recognized.",
                "Use one of: High, Moderate, Low", sw));
        }

        var controls = _nistService.GetControlsByBaseline(baseline);
        var familyCodes = controls.Select(c => c.Family).Distinct().OrderBy(f => f).ToList();

        var phases = new[]
        {
            new
            {
                phase = "Phase 1: Documentation",
                items = new[]
                {
                    "Prepare System Security Plan (SSP)",
                    "Document system boundary and data flow",
                    "Complete Privacy Impact Assessment (PIA)",
                    "Document interconnection agreements (ISA/MOU)"
                }
            },
            new
            {
                phase = "Phase 2: Control Implementation",
                items = familyCodes.Take(5).Select(f =>
                    $"Implement {f} family controls ({controls.Count(c => c.Family == f)} controls)")
                    .ToArray()
            },
            new
            {
                phase = "Phase 3: Assessment",
                items = new[]
                {
                    "Conduct vulnerability scanning (ACAS/Nessus)",
                    "Run STIG compliance checks",
                    "Perform penetration testing",
                    "Complete Security Assessment Report (SAR)"
                }
            },
            new
            {
                phase = "Phase 4: Authorization",
                items = new[]
                {
                    "Compile POA&M for open findings",
                    "Submit authorization package to AO",
                    "Complete risk assessment review",
                    "Obtain ATO decision"
                }
            }
        };

        var result = new
        {
            baseline = baselineStr,
            totalControls = controls.Count,
            totalFamilies = familyCodes.Count,
            phases,
            controlFamilySummary = familyCodes.Select(f => new
            {
                family = f,
                controlCount = controls.Count(c => c.Family == f)
            }).ToArray()
        };

        sw.Stop();
        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
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
