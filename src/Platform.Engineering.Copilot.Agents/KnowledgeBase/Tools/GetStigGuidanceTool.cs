using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;

/// <summary>
/// get_stig_guidance — Return STIG implementation guidance for a control.
/// Pulls StigReferences from the control definition. No auth required per SC-008.
/// </summary>
public class GetStigGuidanceTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly INistService _nistService;

    public GetStigGuidanceTool(INistService nistService, ILogger<GetStigGuidanceTool> logger)
        : base(logger) => _nistService = nistService;

    public override string Name => "get_stig_guidance";
    public override string Description => "Get STIG implementation guidance for a compliance control";

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

        var stigRefs = control.StigReferences ?? [];
        var result = new
        {
            controlId = control.ControlId,
            controlName = control.Title,
            hasStigGuidance = stigRefs.Length > 0,
            stigReferences = stigRefs.Select(s => new
            {
                stigId = s.StigId,
                benchmarkId = s.BenchmarkId,
                severity = s.Severity,
                implementationNote = BuildStigNote(s, control)
            }).ToArray(),
            generalGuidance = control.ImplementationGuidance
                ?? "Refer to NIST 800-53 control statement for implementation details.",
            azureServiceMappings = control.AzureServiceMappings
        };

        sw.Stop();
        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static string BuildStigNote(StigReference stig, ControlDefinition control)
    {
        var severity = stig.Severity switch
        {
            "CAT I" => "Critical — must be addressed immediately",
            "CAT II" => "High — should be addressed in near-term",
            "CAT III" => "Medium — address during normal maintenance cycle",
            _ => "Review applicable STIG benchmark"
        };

        return $"{severity}. See STIG {stig.StigId} ({stig.BenchmarkId}) for detailed implementation requirements. " +
               $"Applicable Azure services: {string.Join(", ", control.AzureServiceMappings.Take(3))}.";
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
