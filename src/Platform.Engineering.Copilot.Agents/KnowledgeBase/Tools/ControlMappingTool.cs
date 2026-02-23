using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;

/// <summary>
/// control_mapping — Map a control across frameworks showing applicability.
/// Shows whether the control is included in each framework and baseline.
/// No auth required per SC-008.
/// </summary>
public class ControlMappingTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly INistService _nistService;

    public ControlMappingTool(INistService nistService, ILogger<ControlMappingTool> logger)
        : base(logger) => _nistService = nistService;

    public override string Name => "control_mapping";
    public override string Description => "Map a control across frameworks showing applicability";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "controlId": { "type": "string", "description": "NIST control ID (e.g., 'AC-2')." }
      },
      "required": ["controlId"]
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
        var controlId = GetRequired<string>(parameters, "controlId");

        if (string.IsNullOrWhiteSpace(controlId))
        {
            sw.Stop();
            return Task.FromResult(BuildError("MISSING_CONTROL_ID",
                "Control ID is required.", "Provide a NIST control ID like AC-2", sw));
        }

        var control = _nistService.GetControl(controlId.Trim().ToUpperInvariant());
        if (control is null)
        {
            sw.Stop();
            return Task.FromResult(BuildError("CONTROL_NOT_FOUND",
                $"Control '{controlId}' was not found in the catalog.",
                "Verify the control ID format (e.g., AC-2, SC-8)", sw));
        }

        var result = new
        {
            controlId = control.ControlId,
            controlName = control.Title,
            family = control.FamilyName,
            frameworkApplicability = new
            {
                nist80053Rev5 = control.Frameworks.Nist80053Rev5,
                fedRampHigh = control.Frameworks.FedRampHigh,
                fedRampModerate = control.Frameworks.FedRampModerate,
                doDIL5 = control.Frameworks.DoDIL5
            },
            baselineApplicability = new
            {
                high = control.Baselines.High,
                moderate = control.Baselines.Moderate,
                low = control.Baselines.Low
            },
            priority = control.Priority,
            relatedControls = control.Related
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
