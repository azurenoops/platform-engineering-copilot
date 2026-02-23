using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;

/// <summary>
/// explain_control — Explain a NIST 800-53 control in plain language
/// with Azure service mappings, implementation guidance, and related controls.
/// No auth required per SC-008. Data from INistService embedded OSCAL.
/// </summary>
public class ExplainControlTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly INistService _nistService;

    public ExplainControlTool(INistService nistService, ILogger<ExplainControlTool> logger)
        : base(logger) => _nistService = nistService;

    public override string Name => "explain_control";
    public override string Description => "Explain a compliance control in plain language with Azure mappings";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "controlId": { "type": "string", "description": "NIST control ID (e.g., 'AC-2', 'SC-8').", "required": true }
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
                "Control ID is required.", "Provide a NIST control ID like AC-2 or SC-8", sw));
        }

        var control = _nistService.GetControl(controlId.Trim().ToUpperInvariant());
        if (control is null)
        {
            sw.Stop();
            return Task.FromResult(BuildError("CONTROL_NOT_FOUND",
                $"Control '{controlId}' was not found in the catalog.",
                "Verify the control ID format (e.g., AC-2, SC-8, AC-2(1))", sw));
        }

        var result = new
        {
            controlId = control.ControlId,
            controlName = control.Title,
            family = control.FamilyName,
            familyCode = control.Family,
            description = control.Description,
            azureServiceMappings = control.AzureServiceMappings.Select(s => new
            {
                service = s,
                capability = MapAzureCapability(s)
            }).ToArray(),
            implementationGuidance = control.ImplementationGuidance
                ?? "Refer to the NIST 800-53 control statement for implementation details.",
            relatedControls = control.Related,
            baselines = new
            {
                high = control.Baselines.High,
                moderate = control.Baselines.Moderate,
                low = control.Baselines.Low
            },
            priority = control.Priority,
            stigReferences = control.StigReferences?.Select(s => new
            {
                stigId = s.StigId,
                benchmarkId = s.BenchmarkId,
                severity = s.Severity
            }).ToArray()
        };

        sw.Stop();
        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static string MapAzureCapability(string service) => service switch
    {
        "Azure AD" or "Entra ID" => "Identity and access management",
        "Azure Policy" => "Policy enforcement and compliance",
        "Azure Key Vault" => "Key and secret management",
        "Azure Monitor" => "Logging and monitoring",
        "Microsoft Defender for Cloud" => "Security posture management",
        "Azure Firewall" => "Network security and filtering",
        "Azure Storage" => "Data storage and encryption",
        "Azure Virtual Network" => "Network segmentation",
        "Azure DDoS Protection" => "DDoS mitigation",
        "Azure Backup" => "Data protection and recovery",
        _ => $"Supports {service} implementation"
    };

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
