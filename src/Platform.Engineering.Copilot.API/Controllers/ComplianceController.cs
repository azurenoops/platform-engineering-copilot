using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using System.Text;
using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Platform.Engineering.Copilot.API.Controllers;

/// <summary>
/// API Controller for Compliance Evidence Download and eMASS Integration
/// </summary>
[ApiController]
[Route("api/compliance")]
[Produces("application/json")]
public class ComplianceController : ControllerBase
{
    private readonly ILogger<ComplianceController> _logger;
    private readonly IAtoComplianceEngine _complianceEngine;

    public ComplianceController(
        ILogger<ComplianceController> logger,
        IAtoComplianceEngine complianceEngine)
    {
        _logger = logger;
        _complianceEngine = complianceEngine;
    }

    /// <summary>
    /// Download compliance evidence package in various formats
    /// </summary>
    /// <param name="subscriptionIdOrName">Subscription ID or friendly name</param>
    /// <param name="controlFamily">NIST Control Family (AC, AU, CM, etc.)</param>
    /// <param name="format">Download format: json, csv, pdf, or emass</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Evidence package in requested format</returns>
    [HttpGet("evidence/download")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DownloadEvidenceAsync(
        [FromQuery] string subscriptionIdOrName,
        [FromQuery] string controlFamily,
        [FromQuery] string format = "json",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(subscriptionIdOrName) || string.IsNullOrWhiteSpace(controlFamily))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Missing Required Parameters",
                    Detail = "Both 'subscriptionIdOrName' and 'controlFamily' are required",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var normalizedFormat = format.ToLowerInvariant();
            if (!new[] { "json", "csv", "pdf", "emass" }.Contains(normalizedFormat))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Format",
                    Detail = $"Format '{format}' is not supported. Valid formats: json, csv, pdf, emass",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Downloading evidence for subscription {Subscription}, family {Family}, format {Format}",
                subscriptionIdOrName, controlFamily, normalizedFormat);

            // Collect evidence
            var evidencePackage = await _complianceEngine.CollectComplianceEvidenceAsync(
                subscriptionIdOrName,
                controlFamily,
                null,
                cancellationToken);

            if (evidencePackage == null || !string.IsNullOrEmpty(evidencePackage.Error))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Evidence Collection Failed",
                    Detail = evidencePackage?.Error ?? "Unable to collect evidence for the specified subscription and control family",
                    Status = StatusCodes.Status404NotFound
                });
            }

            // Generate content based on format
            return normalizedFormat switch
            {
                "json" => GenerateJsonDownload(evidencePackage),
                "csv" => GenerateCsvDownload(evidencePackage),
                "pdf" => GeneratePdfDownload(evidencePackage),
                "emass" => GenerateEmassDownload(evidencePackage),
                _ => BadRequest(new ProblemDetails
                {
                    Title = "Unsupported Format",
                    Detail = $"Format '{normalizedFormat}' is not supported",
                    Status = StatusCodes.Status400BadRequest
                })
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading evidence for subscription {Subscription}, family {Family}",
                subscriptionIdOrName, controlFamily);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while generating the evidence package. Please try again later.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Generate eMASS-compatible package for a control family
    /// </summary>
    /// <param name="subscriptionIdOrName">Subscription ID or friendly name</param>
    /// <param name="controlFamily">NIST Control Family (AC, AU, CM, etc.)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>eMASS XML package</returns>
    [HttpPost("emass/generate")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GenerateEmassPackageAsync(
        [FromQuery] string subscriptionIdOrName,
        [FromQuery] string controlFamily,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(subscriptionIdOrName) || string.IsNullOrWhiteSpace(controlFamily))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Missing Required Parameters",
                    Detail = "Both 'subscriptionIdOrName' and 'controlFamily' are required",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Generating eMASS package for subscription {Subscription}, family {Family}",
                subscriptionIdOrName, controlFamily);

            // Collect evidence
            var evidencePackage = await _complianceEngine.CollectComplianceEvidenceAsync(
                subscriptionIdOrName,
                controlFamily,
                null,
                cancellationToken);

            if (evidencePackage == null || !string.IsNullOrEmpty(evidencePackage.Error))
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Evidence Collection Failed",
                    Detail = evidencePackage?.Error ?? "Unable to collect evidence for eMASS package",
                    Status = StatusCodes.Status404NotFound
                });
            }

            return GenerateEmassDownload(evidencePackage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating eMASS package for subscription {Subscription}, family {Family}",
                subscriptionIdOrName, controlFamily);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while generating the eMASS package. Please try again later.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Generate POA&M document
    /// </summary>
    /// <param name="subscriptionIdOrName">Subscription ID or friendly name</param>
    /// <param name="controlFamily">Optional control family to limit scope</param>
    /// <param name="format">Download format: pdf, xlsx, or emass</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>POA&M document in requested format</returns>
    [HttpGet("poam/generate")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GeneratePoamAsync(
        [FromQuery] string subscriptionIdOrName,
        [FromQuery] string? controlFamily = null,
        [FromQuery] string format = "pdf",
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(subscriptionIdOrName))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Missing Required Parameters",
                    Detail = "'subscriptionIdOrName' is required",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            var normalizedFormat = format.ToLowerInvariant();
            if (!new[] { "pdf", "xlsx", "emass" }.Contains(normalizedFormat))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Format",
                    Detail = $"Format '{format}' is not supported. Valid formats: pdf, xlsx, emass",
                    Status = StatusCodes.Status400BadRequest
                });
            }

            _logger.LogInformation("Generating POA&M for subscription {Subscription}, family {Family}, format {Format}",
                subscriptionIdOrName, controlFamily ?? "all", normalizedFormat);

            // Get assessment findings
            var assessment = await _complianceEngine.RunComprehensiveAssessmentAsync(
                subscriptionIdOrName,
                null,
                cancellationToken);

            var findings = assessment.ControlFamilyResults
                .SelectMany(cf => cf.Value.Findings)
                .ToList();

            // Filter by control family if specified
            if (!string.IsNullOrWhiteSpace(controlFamily))
            {
                findings = findings.Where(f =>
                    f.AffectedNistControls.Any(c => c.StartsWith(controlFamily, StringComparison.OrdinalIgnoreCase))
                ).ToList();
            }

            if (!findings.Any())
            {
                return Ok(new
                {
                    success = true,
                    message = "No findings - subscription is compliant!",
                    subscriptionId = subscriptionIdOrName
                });
            }

            // Generate remediation plan
            var plan = await _complianceEngine.GenerateRemediationPlanAsync(
                subscriptionIdOrName,
                findings,
                cancellationToken);

            // Generate POA&M content
            var poamContent = GeneratePoamContent(subscriptionIdOrName, controlFamily, findings, plan);
            var fileName = $"poam-{controlFamily ?? "all"}-{DateTimeOffset.UtcNow:yyyyMMdd}.txt";

            return File(
                Encoding.UTF8.GetBytes(poamContent),
                "text/plain",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating POA&M for subscription {Subscription}",
                subscriptionIdOrName);

            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Internal Server Error",
                Detail = "An error occurred while generating the POA&M. Please try again later.",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    // ========== PRIVATE HELPER METHODS ==========

    private FileContentResult GenerateJsonDownload(EvidencePackage evidencePackage)
    {
        var jsonContent = JsonSerializer.Serialize(evidencePackage, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var fileName = $"evidence-{evidencePackage.ControlFamily}-{evidencePackage.PackageId}.json";

        return File(
            Encoding.UTF8.GetBytes(jsonContent),
            "application/json",
            fileName);
    }

    private FileContentResult GenerateCsvDownload(EvidencePackage evidencePackage)
    {
        var csv = new StringBuilder();

        // CSV Header
        csv.AppendLine("Evidence ID,Control ID,Evidence Type,Resource ID,Collected At,Data Summary");

        // CSV Rows
        foreach (var evidence in evidencePackage.Evidence)
        {
            // Properly serialize the data dictionary
            var dataStr = "";
            if (evidence.Data != null && evidence.Data.Count > 0)
            {
                var dataParts = new List<string>();
                foreach (var kvp in evidence.Data)
                {
                    // Serialize complex objects as JSON
                    var valueStr = kvp.Value switch
                    {
                        string s => s,
                        null => "null",
                        _ => JsonSerializer.Serialize(kvp.Value, new JsonSerializerOptions 
                        { 
                            WriteIndented = false,
                            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                        })
                    };
                    dataParts.Add($"{kvp.Key}={valueStr}");
                }
                dataStr = string.Join("; ", dataParts);
            }

            var dataSummary = dataStr.Replace(",", ";").Replace("\n", " ").Replace("\r", "");
            if (dataSummary.Length > 200)
            {
                dataSummary = dataSummary.Substring(0, 197) + "...";
            }

            csv.AppendLine($"\"{evidence.EvidenceId}\",\"{evidence.ControlId}\",\"{evidence.EvidenceType}\",\"{evidence.ResourceId}\",\"{evidence.CollectedAt:yyyy-MM-dd HH:mm:ss}\",\"{dataSummary}\"");
        }

        var fileName = $"evidence-{evidencePackage.ControlFamily}-{evidencePackage.PackageId}.csv";

        return File(
            Encoding.UTF8.GetBytes(csv.ToString()),
            "text/csv",
            fileName);
    }

    private FileContentResult GeneratePdfDownload(EvidencePackage evidencePackage)
    {
        // Configure QuestPDF license (Community license is free for non-commercial use)
        QuestPDF.Settings.License = LicenseType.Community;

        // Generate PDF using QuestPDF
        var pdfBytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                // Header
                page.Header().Column(column =>
                {
                    column.Item().AlignCenter().Text("COMPLIANCE EVIDENCE REPORT")
                        .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                    
                    column.Item().PaddingTop(10).LineHorizontal(2).LineColor(Colors.Blue.Darken2);
                });

                // Content
                page.Content().Column(column =>
                {
                    // Package Information
                    column.Item().PaddingTop(15).Text("PACKAGE INFORMATION").FontSize(14).Bold();
                    column.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(120);
                            columns.RelativeColumn();
                        });

                        table.Cell().Text("Package ID:").Bold();
                        table.Cell().Text(evidencePackage.PackageId);

                        table.Cell().Text("Subscription:").Bold();
                        table.Cell().Text(evidencePackage.SubscriptionId);

                        table.Cell().Text("Control Family:").Bold();
                        table.Cell().Text(evidencePackage.ControlFamily);

                        table.Cell().Text("Collection Date:").Bold();
                        table.Cell().Text(evidencePackage.CollectionDate.ToString("yyyy-MM-dd HH:mm:ss UTC"));
                    });

                    // Summary
                    column.Item().PaddingTop(15).Text("SUMMARY").FontSize(14).Bold();
                    column.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(120);
                            columns.RelativeColumn();
                        });

                        table.Cell().Text("Total Evidence Items:").Bold();
                        table.Cell().Text(evidencePackage.TotalItems.ToString());

                        table.Cell().Text("Completeness Score:").Bold();
                        table.Cell().Text($"{evidencePackage.CompletenessScore:F1}%");

                        table.Cell().Text("Collection Duration:").Bold();
                        table.Cell().Text(evidencePackage.CollectionDuration.ToString());
                    });

                    // Evidence Items
                    column.Item().PaddingTop(15).Text("EVIDENCE ITEMS").FontSize(14).Bold();
                    column.Item().PaddingTop(5).Text($"Showing {Math.Min(50, evidencePackage.Evidence.Count)} of {evidencePackage.Evidence.Count} items")
                        .FontSize(9).Italic();

                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(3);
                        });

                        // Header row
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("#").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Type").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Control").FontColor(Colors.White).Bold();
                            header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Resource ID").FontColor(Colors.White).Bold();
                        });

                        // Data rows (limit to 50 items)
                        var itemsToShow = evidencePackage.Evidence.Take(50).ToList();
                        for (int i = 0; i < itemsToShow.Count; i++)
                        {
                            var evidence = itemsToShow[i];
                            var bgColor = i % 2 == 0 ? Colors.Grey.Lighten4 : Colors.White;

                            table.Cell().Background(bgColor).Padding(5).Text((i + 1).ToString());
                            table.Cell().Background(bgColor).Padding(5).Text(evidence.EvidenceType).FontSize(9);
                            table.Cell().Background(bgColor).Padding(5).Text(evidence.ControlId).FontSize(9);
                            table.Cell().Background(bgColor).Padding(5).Text(evidence.ResourceId.Length > 60 
                                ? evidence.ResourceId.Substring(0, 57) + "..." 
                                : evidence.ResourceId).FontSize(8);
                        }
                    });

                    if (evidencePackage.Evidence.Count > 50)
                    {
                        column.Item().PaddingTop(5).Text($"... and {evidencePackage.Evidence.Count - 50} more items")
                            .FontSize(9).Italic().FontColor(Colors.Grey.Darken1);
                    }

                    // Attestation
                    column.Item().PaddingTop(15).Text("ATTESTATION").FontSize(14).Bold();
                    column.Item().PaddingTop(5).BorderLeft(3).BorderColor(Colors.Blue.Darken2).PaddingLeft(10)
                        .Text(evidencePackage.AttestationStatement).FontSize(9).Italic();
                });

                // Footer
                page.Footer().Column(column =>
                {
                    column.Item().LineHorizontal(1).LineColor(Colors.Grey.Medium);
                    column.Item().PaddingTop(5).Row(row =>
                    {
                        row.RelativeItem().AlignLeft().Text($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss UTC}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        row.RelativeItem().AlignRight().Text("Platform Engineering Copilot - Compliance Module")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        }).GeneratePdf();

        var fileName = $"evidence-{evidencePackage.ControlFamily}-{evidencePackage.PackageId}.pdf";

        return File(pdfBytes, "application/pdf", fileName);
    }

    private FileContentResult GenerateEmassDownload(EvidencePackage evidencePackage)
    {
        var systemId = $"SYS-{evidencePackage.SubscriptionId[..8].ToUpperInvariant()}";
        var schemaVersion = "6.2";

        var xml = new StringBuilder();
        xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        xml.AppendLine($"<emass-package xmlns=\"https://emass.apps.mil/schema/{schemaVersion}\" version=\"{schemaVersion}\">");
        xml.AppendLine($"  <metadata>");
        xml.AppendLine($"    <system-id>{systemId}</system-id>");
        xml.AppendLine($"    <package-id>{evidencePackage.PackageId}</package-id>");
        xml.AppendLine($"    <submission-date>{DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</submission-date>");
        xml.AppendLine($"    <control-family>{evidencePackage.ControlFamily}</control-family>");
        xml.AppendLine($"    <control-family-name>{GetControlFamilyName(evidencePackage.ControlFamily)}</control-family-name>");
        xml.AppendLine($"  </metadata>");

        xml.AppendLine($"  <artifacts count=\"{evidencePackage.Evidence.Count}\">");
        foreach (var evidence in evidencePackage.Evidence)
        {
            // Properly serialize the data dictionary
            var dataStr = "";
            if (evidence.Data != null && evidence.Data.Count > 0)
            {
                var dataParts = new List<string>();
                foreach (var kvp in evidence.Data)
                {
                    // Serialize complex objects as JSON
                    var valueStr = kvp.Value switch
                    {
                        string s => s,
                        null => "null",
                        _ => JsonSerializer.Serialize(kvp.Value, new JsonSerializerOptions 
                        { 
                            WriteIndented = false,
                            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                        })
                    };
                    dataParts.Add($"{kvp.Key}={valueStr}");
                }
                dataStr = string.Join("; ", dataParts);
            }

            xml.AppendLine($"    <artifact>");
            xml.AppendLine($"      <artifact-id>{evidence.EvidenceId}</artifact-id>");
            xml.AppendLine($"      <control-id>{evidence.ControlId}</control-id>");
            xml.AppendLine($"      <artifact-type>{evidence.EvidenceType}</artifact-type>");
            xml.AppendLine($"      <resource-id><![CDATA[{evidence.ResourceId}]]></resource-id>");
            xml.AppendLine($"      <collection-date>{evidence.CollectedAt:yyyy-MM-ddTHH:mm:ssZ}</collection-date>");
            xml.AppendLine($"      <data><![CDATA[{dataStr}]]></data>");
            xml.AppendLine($"    </artifact>");
        }
        xml.AppendLine($"  </artifacts>");

        xml.AppendLine($"  <attestation>");
        xml.AppendLine($"    <statement><![CDATA[{evidencePackage.AttestationStatement}]]></statement>");
        xml.AppendLine($"    <completeness-score>{evidencePackage.CompletenessScore:F2}</completeness-score>");
        xml.AppendLine($"  </attestation>");

        xml.AppendLine($"</emass-package>");

        var fileName = $"emass-{evidencePackage.ControlFamily}-{evidencePackage.PackageId}.xml";

        return File(
            Encoding.UTF8.GetBytes(xml.ToString()),
            "application/xml",
            fileName);
    }

    private string GeneratePoamContent(
        string subscriptionId,
        string? controlFamily,
        List<AtoFinding> findings,
        RemediationPlan plan)
    {
        var poamId = $"POAM-{DateTimeOffset.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}";

        var content = new StringBuilder();
        content.AppendLine("PLAN OF ACTION & MILESTONES (POA&M)");
        content.AppendLine("=========================================");
        content.AppendLine();
        content.AppendLine($"POA&M ID: {poamId}");
        content.AppendLine($"Subscription: {subscriptionId}");
        content.AppendLine($"Control Family: {controlFamily ?? "All"}");
        content.AppendLine($"Generated: {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss}");
        content.AppendLine($"Priority: {plan.Priority}");
        content.AppendLine($"Estimated Effort: {plan.EstimatedEffort}");
        content.AppendLine();

        content.AppendLine("SUMMARY");
        content.AppendLine("-------");
        content.AppendLine($"Total Findings: {findings.Count}");
        content.AppendLine($"Critical: {findings.Count(f => f.Severity.ToString() == "Critical")}");
        content.AppendLine($"High: {findings.Count(f => f.Severity.ToString() == "High")}");
        content.AppendLine($"Medium: {findings.Count(f => f.Severity.ToString() == "Medium")}");
        content.AppendLine($"Low: {findings.Count(f => f.Severity.ToString() == "Low")}");
        content.AppendLine();

        content.AppendLine("POA&M ITEMS");
        content.AppendLine("-----------");
        for (int i = 0; i < findings.Count; i++)
        {
            var finding = findings[i];
            content.AppendLine($"{i + 1}. {finding.Title}");
            content.AppendLine($"   Control: {finding.AffectedNistControls.FirstOrDefault() ?? "N/A"}");
            content.AppendLine($"   Severity: {finding.Severity}");
            content.AppendLine($"   Resource: {finding.ResourceId}");
            content.AppendLine($"   Remediation: {finding.Recommendation}");
            content.AppendLine($"   Auto-remediable: {(finding.IsAutoRemediable ? "Yes" : "No")}");
            content.AppendLine();
        }

        return content.ToString();
    }

    private string GetControlFamilyName(string familyCode)
    {
        return familyCode switch
        {
            "AC" => "Access Control",
            "AU" => "Audit and Accountability",
            "CM" => "Configuration Management",
            "CP" => "Contingency Planning",
            "IA" => "Identification and Authentication",
            "IR" => "Incident Response",
            "MA" => "Maintenance",
            "MP" => "Media Protection",
            "PE" => "Physical and Environmental Protection",
            "PL" => "Planning",
            "PS" => "Personnel Security",
            "RA" => "Risk Assessment",
            "SA" => "System and Services Acquisition",
            "CA" => "Security Assessment and Authorization",
            "AT" => "Awareness and Training",
            "PM" => "Program Management",
            "SC" => "System and Communications Protection",
            "SI" => "System and Information Integrity",
            _ => familyCode
        };
    }
}
