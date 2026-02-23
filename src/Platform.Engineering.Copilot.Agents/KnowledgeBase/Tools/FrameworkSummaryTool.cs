using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;

/// <summary>
/// framework_summary — Provide summary statistics for a compliance framework.
/// Shows total controls, family breakdown, baseline coverage.
/// No auth required per SC-008.
/// </summary>
public class FrameworkSummaryTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly INistService _nistService;

    public FrameworkSummaryTool(INistService nistService, ILogger<FrameworkSummaryTool> logger)
        : base(logger) => _nistService = nistService;

    public override string Name => "framework_summary";
    public override string Description => "Get summary statistics for a compliance framework";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "framework": { "type": "string", "enum": ["Nist80053Rev5", "FedRampHigh", "FedRampModerate", "DoDIL5"], "default": "Nist80053Rev5" }
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
        var frameworkStr = GetOptional<string>(parameters, "framework") ?? "Nist80053Rev5";

        if (!Enum.TryParse<ComplianceFramework>(frameworkStr, ignoreCase: true, out var framework))
        {
            sw.Stop();
            return Task.FromResult(BuildError("INVALID_FRAMEWORK",
                $"Framework '{frameworkStr}' is not recognized.",
                "Use one of: Nist80053Rev5, FedRampHigh, FedRampModerate, DoDIL5", sw));
        }

        var controls = _nistService.GetControlsByFramework(framework);
        var familyCodes = _nistService.GetFamilyCodes();

        var familyBreakdown = familyCodes
            .Select(f =>
            {
                var familyControls = controls.Where(c => c.Family == f).ToList();
                return new
                {
                    familyCode = f,
                    familyName = familyControls.FirstOrDefault()?.FamilyName ?? f,
                    controlCount = familyControls.Count
                };
            })
            .Where(f => f.controlCount > 0)
            .OrderByDescending(f => f.controlCount)
            .ToArray();

        var result = new
        {
            framework = framework.ToString(),
            totalControls = controls.Count,
            totalFamilies = familyBreakdown.Length,
            baselineCoverage = new
            {
                high = controls.Count(c => c.Baselines.High),
                moderate = controls.Count(c => c.Baselines.Moderate),
                low = controls.Count(c => c.Baselines.Low)
            },
            priorityDistribution = new
            {
                p1 = controls.Count(c => c.Priority == "P1"),
                p2 = controls.Count(c => c.Priority == "P2"),
                p3 = controls.Count(c => c.Priority == "P3"),
                unassigned = controls.Count(c => string.IsNullOrEmpty(c.Priority))
            },
            familyBreakdown,
            dataSource = _nistService.ActiveSource.Source
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
