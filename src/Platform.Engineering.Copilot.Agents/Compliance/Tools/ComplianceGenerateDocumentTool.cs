using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Agents.Compliance.Tools;

/// <summary>
/// compliance_generate_document — Generate SSP, SAR, or POA&M documents.
/// No auth required. Max 5MB with truncation flag per FR-028.
/// </summary>
public class ComplianceGenerateDocumentTool : BaseTool
{
    /// <summary>Maximum document size: 5MB.</summary>
    public const int MaxDocumentSizeBytes = 5 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public ComplianceGenerateDocumentTool(ILogger<ComplianceGenerateDocumentTool> logger) : base(logger) { }

    public override string Name => "compliance_generate_document";
    public override string Description => "Generate a compliance document based on assessment results";

    public override string Parameters => """
    {
      "type": "object",
      "properties": {
        "documentType": { "type": "string", "enum": ["SSP", "SAR", "POAM"], "description": "Type of compliance document to generate." },
        "subscriptionId": { "type": "string", "description": "Azure subscription ID." },
        "framework": { "type": "string", "description": "Compliance framework." },
        "systemName": { "type": "string", "description": "Name of the system for the document header." },
        "owner": { "type": "string", "description": "System owner name." },
        "assessmentId": { "type": "string", "description": "Specific assessment to base the document on. Uses latest if omitted." }
      },
      "required": ["documentType"]
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
        var documentType = GetRequired<string>(parameters, "documentType").ToUpperInvariant();
        var systemName = GetOptional<string>(parameters, "systemName") ?? "Platform Engineering Copilot";
        var owner = GetOptional<string>(parameters, "owner") ?? "System Administrator";
        var framework = GetOptional<string>(parameters, "framework") ?? "NIST 800-53 Rev 5";

        // Validate documentType
        if (documentType is not ("SSP" or "SAR" or "POAM"))
        {
            sw.Stop();
            var errEnv = new
            {
                status = "error",
                data = (object?)null,
                error = new
                {
                    message = $"Invalid document type: '{documentType}'.",
                    errorCode = "DOCUMENT_GENERATION_FAILED",
                    suggestion = "Use one of: SSP, SAR, POAM"
                },
                metadata = new { toolName = Name, executionTimeMs = sw.ElapsedMilliseconds, timestamp = DateTimeOffset.UtcNow.ToString("o") }
            };
            return Task.FromResult(JsonSerializer.Serialize(errEnv, JsonOptions));
        }

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 20,
            Message = $"Generating {documentType} document for {systemName}"
        });

        var sections = GetSections(documentType);
        var content = GenerateMarkdownContent(documentType, systemName, owner, framework, sections);
        var contentBytes = System.Text.Encoding.UTF8.GetByteCount(content);
        var truncated = false;

        // Enforce 5MB limit
        if (contentBytes > MaxDocumentSizeBytes)
        {
            truncated = true;
            content = content[..Math.Min(content.Length, MaxDocumentSizeBytes / 2)] +
                      "\n\n---\n*Document truncated. Full findings available via 'compliance_assess' tool.*\n";
            contentBytes = System.Text.Encoding.UTF8.GetByteCount(content);
        }

        progress?.Report(new ProgressUpdate
        {
            PercentComplete = 100,
            Message = $"{documentType} document generated ({contentBytes:N0} bytes)"
        });

        var data = new
        {
            documentId = Guid.NewGuid().ToString(),
            documentType,
            systemName,
            owner,
            framework,
            generatedAt = DateTimeOffset.UtcNow.ToString("o"),
            contentSizeBytes = contentBytes,
            truncated,
            sections,
            content
        };

        sw.Stop();
        var envelope = new
        {
            status = "success",
            data,
            metadata = new
            {
                toolName = Name,
                executionTimeMs = sw.ElapsedMilliseconds,
                timestamp = DateTimeOffset.UtcNow.ToString("o")
            }
        };
        return Task.FromResult(JsonSerializer.Serialize(envelope, JsonOptions));
    }

    private static string[] GetSections(string documentType) => documentType switch
    {
        "SSP" => ["System Description", "Security Controls", "Implementation Status", "Risk Assessment", "Continuous Monitoring"],
        "SAR" => ["Assessment Scope", "Methodology", "Findings Summary", "Recommendations", "Risk Determination"],
        "POAM" => ["Finding Details", "Milestones", "Resource Requirements", "Completion Dates", "Risk Acceptance"],
        _ => ["Document Content"]
    };

    private static string GenerateMarkdownContent(
        string documentType, string systemName, string owner,
        string framework, string[] sections)
    {
        var title = documentType switch
        {
            "SSP" => "System Security Plan",
            "SAR" => "Security Assessment Report",
            "POAM" => "Plan of Action and Milestones",
            _ => "Compliance Document"
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine($"**System**: {systemName}");
        sb.AppendLine($"**Owner**: {owner}");
        sb.AppendLine($"**Framework**: {framework}");
        sb.AppendLine($"**Generated**: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var section in sections)
        {
            sb.AppendLine($"## {section}");
            sb.AppendLine();
            sb.AppendLine($"*Content for {section} section would be populated from assessment data.*");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
