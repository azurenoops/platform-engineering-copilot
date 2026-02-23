using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;

/// <summary>
/// implementation_examples — Azure implementation examples for a control.
/// Provides Bicep/ARM patterns, Azure service configurations, and best practices.
/// No auth required per SC-008.
/// </summary>
public class ImplementationExamplesTool : BaseTool
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly INistService _nistService;

    public ImplementationExamplesTool(INistService nistService, ILogger<ImplementationExamplesTool> logger)
        : base(logger) => _nistService = nistService;

    public override string Name => "implementation_examples";
    public override string Description => "Get Azure implementation examples for a compliance control";

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

        var examples = BuildExamples(control);

        var result = new
        {
            controlId = control.ControlId,
            controlName = control.Title,
            family = control.FamilyName,
            azureServices = control.AzureServiceMappings,
            examples,
            bestPractices = BuildBestPractices(control)
        };

        sw.Stop();
        var envelope = new { status = "success", data = result, metadata = BuildMetadata(sw) };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static object[] BuildExamples(ControlDefinition control)
    {
        var examples = new List<object>();

        foreach (var service in control.AzureServiceMappings.Take(3))
        {
            examples.Add(new
            {
                service,
                approach = $"Configure {service} to implement {control.ControlId} ({control.Title})",
                pattern = GetPatternForService(service),
                notes = $"Ensure {service} configuration aligns with {control.Family} family requirements."
            });
        }

        if (examples.Count == 0)
        {
            examples.Add(new
            {
                service = "Azure Policy",
                approach = $"Create custom Azure Policy to enforce {control.ControlId}",
                pattern = "policy-definition",
                notes = "Define policy rules that map to control requirements."
            });
        }

        return examples.ToArray();
    }

    private static string GetPatternForService(string service) => service switch
    {
        "Azure AD" or "Entra ID" => "identity-configuration",
        "Azure Policy" => "policy-definition",
        "Azure Key Vault" => "key-vault-configuration",
        "Azure Monitor" => "diagnostic-settings",
        "Microsoft Defender for Cloud" => "defender-configuration",
        "Azure Firewall" => "network-security",
        "Azure Storage" => "storage-encryption",
        "Azure Virtual Network" => "network-segmentation",
        _ => "resource-configuration"
    };

    private static string[] BuildBestPractices(ControlDefinition control)
    {
        var practices = new List<string>
        {
            $"Document {control.ControlId} implementation in your System Security Plan (SSP)",
            "Use Infrastructure as Code (Bicep/ARM) for repeatable deployments",
            "Enable continuous monitoring via Azure Policy and Defender for Cloud"
        };

        if (control.Baselines.High)
        {
            practices.Add("This control is required for FedRAMP High — ensure full implementation");
        }

        if (control.StigReferences is { Length: > 0 })
        {
            practices.Add($"Review {control.StigReferences.Length} STIG reference(s) for detailed implementation guidance");
        }

        return practices.ToArray();
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
