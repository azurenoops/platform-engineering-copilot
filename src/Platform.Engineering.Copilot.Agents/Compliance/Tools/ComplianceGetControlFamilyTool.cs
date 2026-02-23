using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.Compliance.Tools;

/// <summary>
/// compliance_get_control_family — Get details for a specific NIST 800-53 control family.
/// </summary>
public class ComplianceGetControlFamilyTool : BaseTool
{
    private readonly INistService _nistService;

    public ComplianceGetControlFamilyTool(INistService nistService, ILogger<ComplianceGetControlFamilyTool> logger)
        : base(logger)
    {
        _nistService = nistService;
    }

    public override string Name => "compliance_get_control_family";
    public override string Description => "Get detailed information about a NIST 800-53 control family and current compliance status";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "familyId": { "type": "string", "description": "Control family abbreviation (e.g., 'AC', 'AU', 'SC')." },
        "includeControls": { "type": "boolean", "default": true, "description": "Include individual controls." }
      },
      "required": ["familyId"]
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

        var familyId = GetRequired<string>(parameters, "familyId").ToUpperInvariant();
        var includeControls = GetOptional<bool>(parameters, "includeControls");
        if (!parameters.ContainsKey("includeControls")) includeControls = true;

        var controls = _nistService.GetControlsByFamily(familyId).ToList();

        if (controls.Count == 0)
        {
            sw.Stop();
            return Task.FromResult(BuildEnvelope("error", null,
                new { errorCode = "INVALID_CONTROL_ID", message = $"No controls found for family '{familyId}'.", suggestion = "Use a valid family code like AC, AU, SC, etc." },
                sw));
        }

        var data = new
        {
            family = familyId,
            familyName = controls.First().FamilyName,
            totalControls = controls.Count,
            controls = includeControls
                ? controls.Select(c => new
                {
                    controlId = c.ControlId,
                    title = c.Title,
                    priority = c.Priority,
                    baselines = GetApplicableBaselines(c.Baselines)
                }).ToList()
                : null
        };

        sw.Stop();
        return Task.FromResult(BuildEnvelope("success", data, null, sw));
    }

    private string BuildEnvelope(string status, object? data, object? error, Stopwatch sw)
    {
        var envelope = new
        {
            status,
            data,
            error,
            metadata = new
            {
                toolName = Name,
                executionTimeMs = sw.ElapsedMilliseconds,
                timestamp = DateTimeOffset.UtcNow.ToString("o")
            }
        };
        return JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
    }

    private static List<string> GetApplicableBaselines(BaselineApplicability baselines)
    {
        var result = new List<string>();
        if (baselines.High) result.Add("High");
        if (baselines.Moderate) result.Add("Moderate");
        if (baselines.Low) result.Add("Low");
        return result;
    }
}
