using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Documents;

/// <summary>
/// Real implementation of document generation service for ATO compliance documents
/// </summary>
public class DocumentGenerationService : IDocumentGenerationService
{
    private readonly IAtoComplianceEngine _complianceEngine;
    private readonly INistControlsService _nistService;
    private readonly EvidenceStorageService _storageService;
    private readonly ILogger<DocumentGenerationService> _logger;

    public DocumentGenerationService(
        IAtoComplianceEngine complianceEngine,
        INistControlsService nistService,
        EvidenceStorageService storageService,
        ILogger<DocumentGenerationService> logger)
    {
        _complianceEngine = complianceEngine ?? throw new ArgumentNullException(nameof(complianceEngine));
        _nistService = nistService ?? throw new ArgumentNullException(nameof(nistService));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ControlNarrative> GenerateControlNarrativeAsync(
        string controlId,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating control narrative for {ControlId}", controlId);

        // Get NIST control details
        var control = await _nistService.GetControlAsync(controlId, cancellationToken);
        
        // Get compliance assessment if subscription provided
        AtoComplianceAssessment? assessment = null;
        EvidencePackage? evidencePackage = null;
        if (!string.IsNullOrEmpty(subscriptionId))
        {
            // Collect evidence for the specific control family
            var controlFamily = controlId.Split('-')[0]; // e.g., "AC" from "AC-1"
            _logger.LogInformation("Collecting evidence for control family {ControlFamily}", controlFamily);
            
            evidencePackage = await _complianceEngine.CollectComplianceEvidenceAsync(
                subscriptionId,
                controlFamily,
                "System",
                progress: null,
                cancellationToken);
            
            _logger.LogInformation("Collected {EvidenceCount} evidence items for {ControlFamily}", 
                evidencePackage.Evidence.Count, controlFamily);

            // Store evidence
            await _storageService.StoreComplianceEvidencePackageAsync(evidencePackage, cancellationToken);
            
            assessment = await _complianceEngine.GetLatestAssessmentAsync(subscriptionId, cancellationToken);
        }

        var narrative = new ControlNarrative
        {
            ControlId = controlId,
            ControlTitle = control?.Title ?? GetControlTitle(controlId),
            What = "This control is implemented through Azure platform capabilities and organizational procedures.",
            How = GenerateHowNarrative(controlId, control, assessment),
            ImplementationStatus = DetermineImplementationStatus(controlId, assessment),
            ComplianceStatus = GetControlComplianceStatus(controlId, assessment)
        };

        _logger.LogInformation("Control narrative generated for {ControlId}", controlId);
        return narrative;
    }

    public async Task<GeneratedDocument> GenerateSSPAsync(
        string subscriptionId,
        SspParameters parameters,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating SSP for subscription {SubscriptionId}", subscriptionId);

        // Step 1: Collect real-time evidence for all control families
        _logger.LogInformation("Collecting compliance evidence for all control families...");
        var evidencePackage = await _complianceEngine.CollectComplianceEvidenceAsync(
            subscriptionId,
            "All", // Collect evidence for all control families
            parameters.SystemOwner ?? "System",
            progress: null,
            cancellationToken);
        
        _logger.LogInformation("Collected {EvidenceCount} evidence items in {Duration}ms", 
            evidencePackage.Evidence.Count,
            evidencePackage.CollectionDuration.TotalMilliseconds);

        // Step 2: Store evidence package to Azure Blob Storage
        var evidenceUri = await _storageService.StoreComplianceEvidencePackageAsync(
            evidencePackage,
            cancellationToken);
        
        _logger.LogInformation("Evidence package stored at {EvidenceUri}", evidenceUri);

        // Step 3: Retrieve the latest assessment (which used the collected evidence)
        var assessment = await _complianceEngine.GetLatestAssessmentAsync(subscriptionId, cancellationToken);
        var catalog = await _nistService.GetCatalogAsync(cancellationToken);
        
        // Get all findings from control families
        List<AtoFinding> findings;
        if (assessment != null)
        {
            findings = assessment.ControlFamilyResults.Values
                .SelectMany(cf => cf.Findings)
                .ToList();
        }
        else
        {
            findings = new List<AtoFinding>();
        }

        var document = new GeneratedDocument
        {
            DocumentId = Guid.NewGuid().ToString(),
            DocumentType = "SSP",
            Title = $"System Security Plan - {parameters.SystemName}",
            Version = "1.0",
            GeneratedDate = DateTime.UtcNow,
            Classification = parameters.Classification,
            Metadata = new Dictionary<string, string>
            {
                { "SystemName", parameters.SystemName },
                { "SubscriptionId", subscriptionId },
                { "ComplianceScore", assessment?.OverallComplianceScore.ToString("F1") ?? "0" },
                { "EvidencePackageId", evidencePackage.PackageId },
                { "EvidenceCount", evidencePackage.Evidence.Count.ToString() },
                { "EvidenceCollectionDuration", evidencePackage.CollectionDuration.TotalSeconds.ToString("F2") },
                { "EvidenceUri", evidenceUri?.ToString() ?? "" }
            }
        };

        var content = new StringBuilder();
        content.AppendLine($"# System Security Plan");
        content.AppendLine();
        content.AppendLine($"**System Name:** {parameters.SystemName}");
        content.AppendLine($"**Classification:** {parameters.Classification}");
        content.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd}");
        content.AppendLine();

        // Executive Summary
        content.AppendLine("## 1. Executive Summary");
        content.AppendLine();
        content.AppendLine($"This System Security Plan (SSP) documents the security controls implemented for {parameters.SystemName}.");
        content.AppendLine($"The system operates on Azure Government and follows NIST 800-53 Rev 5 controls.");
        if (assessment != null)
        {
            content.AppendLine($"Current compliance score: {assessment.OverallComplianceScore:F1}%");
            content.AppendLine($"Total findings: {findings.Count}");
        }
        content.AppendLine();
        content.AppendLine($"**Evidence Collection:** {evidencePackage.Evidence.Count} evidence items collected in {evidencePackage.CollectionDuration.TotalSeconds:F2} seconds");
        content.AppendLine($"**Evidence Package ID:** {evidencePackage.PackageId}");
        content.AppendLine();

        // System Description
        content.AppendLine("## 2. System Description");
        content.AppendLine();
        content.AppendLine(parameters.SystemDescription);
        content.AppendLine();

        // System Categorization
        content.AppendLine("## 3. System Categorization");
        content.AppendLine();
        content.AppendLine($"**Impact Level:** {parameters.ImpactLevel}");
        content.AppendLine($"**FIPS 199 Categorization:** {parameters.ImpactLevel}");
        content.AppendLine();

        // Security Controls
        content.AppendLine("## 4. Security Control Implementation");
        content.AppendLine();
        
        if (catalog != null)
        {
            content.AppendLine("### 4.1 Access Control (AC)");
            await AppendControlFamilySectionAsync(content, "AC", assessment, cancellationToken);
            
            content.AppendLine("### 4.2 Awareness and Training (AT)");
            await AppendControlFamilySectionAsync(content, "AT", assessment, cancellationToken);
            
            content.AppendLine("### 4.3 Audit and Accountability (AU)");
            await AppendControlFamilySectionAsync(content, "AU", assessment, cancellationToken);
            
            content.AppendLine("### 4.4 Security Assessment and Authorization (CA)");
            await AppendControlFamilySectionAsync(content, "CA", assessment, cancellationToken);
            
            content.AppendLine("### 4.5 Configuration Management (CM)");
            await AppendControlFamilySectionAsync(content, "CM", assessment, cancellationToken);
            
            content.AppendLine("### 4.6 Contingency Planning (CP)");
            await AppendControlFamilySectionAsync(content, "CP", assessment, cancellationToken);
            
            content.AppendLine("### 4.7 Identification and Authentication (IA)");
            await AppendControlFamilySectionAsync(content, "IA", assessment, cancellationToken);
            
            content.AppendLine("### 4.8 Incident Response (IR)");
            await AppendControlFamilySectionAsync(content, "IR", assessment, cancellationToken);
            
            content.AppendLine("### 4.9 Maintenance (MA)");
            await AppendControlFamilySectionAsync(content, "MA", assessment, cancellationToken);
            
            content.AppendLine("### 4.10 Media Protection (MP)");
            await AppendControlFamilySectionAsync(content, "MP", assessment, cancellationToken);
            
            content.AppendLine("### 4.11 Physical and Environmental Protection (PE)");
            await AppendControlFamilySectionAsync(content, "PE", assessment, cancellationToken);
            
            content.AppendLine("### 4.12 Planning (PL)");
            await AppendControlFamilySectionAsync(content, "PL", assessment, cancellationToken);
            
            content.AppendLine("### 4.13 Program Management (PM)");
            await AppendControlFamilySectionAsync(content, "PM", assessment, cancellationToken);
            
            content.AppendLine("### 4.14 Personnel Security (PS)");
            await AppendControlFamilySectionAsync(content, "PS", assessment, cancellationToken);
            
            content.AppendLine("### 4.15 Risk Assessment (RA)");
            await AppendControlFamilySectionAsync(content, "RA", assessment, cancellationToken);
            
            content.AppendLine("### 4.16 System and Services Acquisition (SA)");
            await AppendControlFamilySectionAsync(content, "SA", assessment, cancellationToken);
            
            content.AppendLine("### 4.17 System and Communications Protection (SC)");
            await AppendControlFamilySectionAsync(content, "SC", assessment, cancellationToken);
            
            content.AppendLine("### 4.18 System and Information Integrity (SI)");
            await AppendControlFamilySectionAsync(content, "SI", assessment, cancellationToken);
        }

        document.Content = content.ToString();
        
        _logger.LogInformation("SSP generated for {SubscriptionId}", subscriptionId);
        return document;
    }

    public async Task<GeneratedDocument> GenerateSARAsync(
        string subscriptionId,
        string assessmentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating SAR for assessment {AssessmentId}", assessmentId);

        var assessment = await _complianceEngine.RunComprehensiveAssessmentAsync(subscriptionId, null, cancellationToken);
        
        // Get all findings
        var findings = assessment?.ControlFamilyResults.Values
            .SelectMany(cf => cf.Findings)
            .ToList() ?? new List<AtoFinding>();

        var document = new GeneratedDocument
        {
            DocumentId = Guid.NewGuid().ToString(),
            DocumentType = "SAR",
            Title = $"Security Assessment Report",
            Version = "1.0",
            GeneratedDate = DateTime.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                { "AssessmentId", assessmentId },
                { "SubscriptionId", subscriptionId }
            }
        };

        var content = new StringBuilder();
        content.AppendLine("# Security Assessment Report");
        content.AppendLine();
        content.AppendLine($"**Assessment ID:** {assessmentId}");
        content.AppendLine($"**Subscription ID:** {subscriptionId}");
        content.AppendLine($"**Assessment Date:** {DateTime.UtcNow:yyyy-MM-dd}");
        content.AppendLine();

        // Executive Summary
        content.AppendLine("## Executive Summary");
        content.AppendLine();
        if (assessment != null)
        {
            content.AppendLine($"**Overall Compliance Score:** {assessment.OverallComplianceScore:F1}%");
            content.AppendLine($"**Total Findings:** {findings.Count}");
            content.AppendLine($"**High Severity:** {findings.Count(f => f.Severity == AtoFindingSeverity.High)}");
            content.AppendLine($"**Medium Severity:** {findings.Count(f => f.Severity == AtoFindingSeverity.Medium)}");
            content.AppendLine($"**Low Severity:** {findings.Count(f => f.Severity == AtoFindingSeverity.Low)}");
            content.AppendLine();
        }

        // Assessment Results
        content.AppendLine("## Assessment Results");
        content.AppendLine();
        
        if (assessment?.ControlFamilyResults != null)
        {
            foreach (var familyResult in assessment.ControlFamilyResults.OrderBy(kvp => kvp.Key))
            {
                content.AppendLine($"### {familyResult.Key} - {familyResult.Value.FamilyName}");
                content.AppendLine();
                content.AppendLine($"**Compliance Score:** {familyResult.Value.ComplianceScore:F1}%");
                content.AppendLine($"**Passed Controls:** {familyResult.Value.PassedControls}/{familyResult.Value.TotalControls}");
                content.AppendLine($"**Findings:** {familyResult.Value.Findings.Count}");
                content.AppendLine();
            }
        }

        // Detailed Findings
        content.AppendLine("## Detailed Findings");
        content.AppendLine();
        
        if (findings.Any())
        {
            foreach (var finding in findings.OrderByDescending(f => f.Severity).Take(20))
            {
                content.AppendLine($"### {finding.Title}");
                content.AppendLine();
                content.AppendLine($"**Finding ID:** {finding.Id}");
                content.AppendLine($"**Severity:** {finding.Severity}");
                content.AppendLine($"**Resource:** {finding.ResourceName} ({finding.ResourceType})");
                content.AppendLine($"**Affected Controls:** {string.Join(", ", finding.AffectedNistControls)}");
                content.AppendLine();
                content.AppendLine($"**Description:** {finding.Description}");
                content.AppendLine();
                content.AppendLine($"**Recommendation:** {finding.Recommendation}");
                content.AppendLine();
            }
        }

        document.Content = content.ToString();
        
        _logger.LogInformation("SAR generated for {AssessmentId}", assessmentId);
        return document;
    }

    public async Task<GeneratedDocument> GeneratePOAMAsync(
        string subscriptionId,
        List<AtoFinding>? findings = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating POA&M for subscription {SubscriptionId}", subscriptionId);

        // Collect real-time evidence for all control families
        _logger.LogInformation("Collecting compliance evidence for POA&M generation...");
        var evidencePackage = await _complianceEngine.CollectComplianceEvidenceAsync(
            subscriptionId,
            "All",
            "System",
            progress: null,
            cancellationToken);
        
        _logger.LogInformation("Collected {EvidenceCount} evidence items", evidencePackage.Evidence.Count);

        // Store evidence package
        var evidenceUri = await _storageService.StoreComplianceEvidencePackageAsync(evidencePackage, cancellationToken);

        // If findings not provided, get them from assessment
        if (findings == null)
        {
            var assessment = await _complianceEngine.GetLatestAssessmentAsync(subscriptionId, cancellationToken);
            findings = assessment?.ControlFamilyResults.Values
                .SelectMany(cf => cf.Findings)
                .ToList() ?? new List<AtoFinding>();
        }

        var document = new GeneratedDocument
        {
            DocumentId = Guid.NewGuid().ToString(),
            DocumentType = "POAM",
            Title = "Plan of Action & Milestones",
            Version = "1.0",
            GeneratedDate = DateTime.UtcNow,
            Metadata = new Dictionary<string, string>
            {
                { "SubscriptionId", subscriptionId },
                { "TotalFindings", findings.Count.ToString() },
                { "EvidencePackageId", evidencePackage.PackageId },
                { "EvidenceCount", evidencePackage.Evidence.Count.ToString() },
                { "EvidenceUri", evidenceUri?.ToString() ?? "" }
            }
        };

        var content = new StringBuilder();
        content.AppendLine("# Plan of Action & Milestones (POA&M)");
        content.AppendLine();
        content.AppendLine($"**Subscription ID:** {subscriptionId}");
        content.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd}");
        content.AppendLine($"**Total Items:** {findings.Count}");
        content.AppendLine();

        // POA&M Table
        content.AppendLine("## Action Items");
        content.AppendLine();
        content.AppendLine("| Finding ID | Affected Controls | Title | Severity | Remediation Action | Target Date | Status |");
        content.AppendLine("|------------|-------------------|-------|----------|-------------------|-------------|--------|");
        
        foreach (var finding in findings.OrderByDescending(f => f.Severity))
        {
            var controlsStr = string.Join(", ", finding.AffectedNistControls.Take(3));
            var targetDate = DateTime.UtcNow.AddDays(finding.Severity == AtoFindingSeverity.High ? 30 : 90);
            content.AppendLine($"| {finding.Id} | {controlsStr} | {finding.Title} | {finding.Severity} | {finding.Recommendation} | {targetDate:yyyy-MM-dd} | Open |");
        }
        
        content.AppendLine();

        // Remediation Priority
        content.AppendLine("## Remediation Priority");
        content.AppendLine();
        content.AppendLine($"1. **Critical/High Findings:** {findings.Count(f => f.Severity == AtoFindingSeverity.High || f.Severity == AtoFindingSeverity.Critical)} items - Target: 30 days");
        content.AppendLine($"2. **Medium Findings:** {findings.Count(f => f.Severity == AtoFindingSeverity.Medium)} items - Target: 90 days");
        content.AppendLine($"3. **Low Findings:** {findings.Count(f => f.Severity == AtoFindingSeverity.Low)} items - Target: 180 days");
        content.AppendLine();

        document.Content = content.ToString();
        
        _logger.LogInformation("POA&M generated for {SubscriptionId}", subscriptionId);
        return document;
    }

    public async Task<List<ComplianceDocumentMetadata>> ListDocumentsAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing documents for package {PackageId}", packageId);

        // For now, return placeholder until storage service is ready
        await Task.CompletedTask;
        
        return new List<ComplianceDocumentMetadata>
        {
            new ComplianceDocumentMetadata
            {
                DocumentId = packageId + "-ssp",
                Title = "System Security Plan",
                DocumentType = "SSP",
                Version = "1.0",
                LastModified = DateTime.UtcNow,
                Status = "Draft"
            }
        };
    }

    public async Task<byte[]> ExportDocumentAsync(
        string documentId,
        ComplianceDocumentFormat format,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Exporting document {DocumentId} to {Format}", documentId, format);

        // Placeholder - generate sample markdown content
        var sampleContent = $"# Document {documentId}\\n\\nThis is a placeholder document.";
        
        await Task.CompletedTask;

        // Convert based on format
        return format switch
        {
            ComplianceDocumentFormat.Markdown => System.Text.Encoding.UTF8.GetBytes(sampleContent),
            ComplianceDocumentFormat.HTML => ConvertMarkdownToHtml(sampleContent),
            ComplianceDocumentFormat.DOCX => await ConvertMarkdownToDocxAsync(sampleContent, cancellationToken),
            ComplianceDocumentFormat.PDF => await ConvertMarkdownToPdfAsync(sampleContent, cancellationToken),
            _ => throw new NotSupportedException($"Format {format} not yet supported")
        };
    }

    public async Task<GeneratedDocument> FormatDocumentAsync(
        GeneratedDocument document,
        FormattingStandard standard,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Formatting document {DocumentId} to {Standard}", document.DocumentId, standard);

        // Apply formatting rules based on standard
        switch (standard)
        {
            case FormattingStandard.NIST:
                ApplyNistFormatting(document);
                break;
            case FormattingStandard.FedRAMP:
                ApplyFedRampFormatting(document);
                break;
            case FormattingStandard.DoD_RMF:
                ApplyDodRmfFormatting(document);
                break;
            case FormattingStandard.FISMA:
                ApplyFismaFormatting(document);
                break;
        }

        return await Task.FromResult(document);
    }

    // Helper methods

    private async Task AppendControlFamilySectionAsync(
        StringBuilder content,
        string familyPrefix,
        AtoComplianceAssessment? assessment,
        CancellationToken cancellationToken)
    {
        var controls = await _nistService.SearchControlsAsync(familyPrefix, cancellationToken);
        
        foreach (var control in controls.Take(5))
        {
            if (control.Id?.StartsWith(familyPrefix) == true)
            {
                content.AppendLine($"#### {control.Id} - {control.Title}");
                content.AppendLine();
                
                var status = GetControlStatus(control.Id, assessment);
                content.AppendLine($"**Implementation Status:** {status}");
                content.AppendLine();
                
                content.AppendLine("**What:** Azure platform provides native capabilities for this control.");
                content.AppendLine();
                content.AppendLine("**How:** Implemented through Azure Security Center, Policy, and RBAC.");
                content.AppendLine();
            }
        }
    }

    private string GetControlTitle(string controlId)
    {
        // Fallback titles for common controls
        return controlId switch
        {
            "AC-1" => "Access Control Policy and Procedures",
            "AC-2" => "Account Management",
            "AU-1" => "Audit and Accountability Policy and Procedures",
            "IA-1" => "Identification and Authentication Policy and Procedures",
            _ => $"Control {controlId}"
        };
    }

    private string GenerateHowNarrative(string controlId, NistControl? control, AtoComplianceAssessment? assessment)
    {
        var sb = new StringBuilder();
        sb.AppendLine("This control is implemented through:");
        sb.AppendLine("- Azure Security Center and Defender for Cloud");
        sb.AppendLine("- Azure Policy for continuous compliance monitoring");
        sb.AppendLine("- Role-Based Access Control (RBAC) for access management");
        sb.AppendLine("- Azure Monitor and Log Analytics for auditing");
        
        if (assessment != null)
        {
            var findings = assessment.ControlFamilyResults.Values
                .SelectMany(cf => cf.Findings)
                .Where(f => f.AffectedNistControls.Contains(controlId))
                .ToList();
            
            if (findings.Any())
            {
                sb.AppendLine();
                sb.AppendLine($"Current compliance findings: {findings.Count}");
            }
        }
        
        return sb.ToString();
    }

    private string DetermineImplementationStatus(string controlId, AtoComplianceAssessment? assessment)
    {
        if (assessment == null) return "Implemented";
        
        var findings = assessment.ControlFamilyResults.Values
            .SelectMany(cf => cf.Findings)
            .Where(f => f.AffectedNistControls.Contains(controlId))
            .ToList();
        
        if (!findings.Any()) return "Fully Implemented";
        if (findings.Any(f => f.Severity == AtoFindingSeverity.High || f.Severity == AtoFindingSeverity.Critical))
            return "Partially Implemented";
        
        return "Substantially Implemented";
    }

    private string GetControlComplianceStatus(string controlId, AtoComplianceAssessment? assessment)
    {
        if (assessment == null) return "Compliant";
        
        var findings = assessment.ControlFamilyResults.Values
            .SelectMany(cf => cf.Findings)
            .Where(f => f.AffectedNistControls.Contains(controlId))
            .ToList();
        
        if (!findings.Any()) return "Compliant";
        if (findings.Any(f => f.Severity == AtoFindingSeverity.High || f.Severity == AtoFindingSeverity.Critical))
            return "Non-Compliant";
        
        return "Partially Compliant";
    }

    private string GetControlStatus(string? controlId, AtoComplianceAssessment? assessment)
    {
        if (string.IsNullOrEmpty(controlId) || assessment == null) return "Implemented";
        
        return DetermineImplementationStatus(controlId, assessment);
    }

    private byte[] ConvertMarkdownToHtml(string markdown)
    {
        // Simple HTML wrapper for now
        var html = $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>Compliance Document</title>
    <style>
        body {{ font-family: Arial, sans-serif; max-width: 800px; margin: 0 auto; padding: 20px; }}
        h1 {{ color: #2c3e50; }}
        h2 {{ color: #34495e; border-bottom: 2px solid #3498db; }}
        table {{ border-collapse: collapse; width: 100%; margin: 20px 0; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #3498db; color: white; }}
    </style>
</head>
<body>
    <pre>{markdown}</pre>
</body>
</html>";
        return System.Text.Encoding.UTF8.GetBytes(html);
    }

    private async Task<byte[]> ConvertMarkdownToDocxAsync(string markdown, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        using var memoryStream = new MemoryStream();
        using (var wordDocument = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document, true))
        {
            // Add main document part
            var mainPart = wordDocument.AddMainDocumentPart();
            mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = mainPart.Document.AppendChild(new Body());

            // Parse markdown and convert to Word paragraphs
            var lines = markdown.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new Run(new Text(""))));
                    continue;
                }

                DocumentFormat.OpenXml.Wordprocessing.Paragraph paragraph;
                
                // Handle headers
                if (line.StartsWith("# "))
                {
                    paragraph = CreateHeading(line.Substring(2), 1);
                }
                else if (line.StartsWith("## "))
                {
                    paragraph = CreateHeading(line.Substring(3), 2);
                }
                else if (line.StartsWith("### "))
                {
                    paragraph = CreateHeading(line.Substring(4), 3);
                }
                else if (line.StartsWith("#### "))
                {
                    paragraph = CreateHeading(line.Substring(5), 4);
                }
                // Handle lists
                else if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
                {
                    var text = line.TrimStart().Substring(2);
                    paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new Run(new Text("• " + text)));
                }
                // Handle numbered lists
                else if (char.IsDigit(line.TrimStart().FirstOrDefault()) && line.Contains(". "))
                {
                    paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new Run(new Text(line.TrimStart())));
                }
                // Handle bold text (simplified)
                else if (line.Contains("**"))
                {
                    paragraph = CreateParagraphWithFormatting(line);
                }
                // Regular paragraph
                else
                {
                    paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new Run(new Text(line)));
                }

                body.AppendChild(paragraph);
            }

            mainPart.Document.Save();
        }

        return memoryStream.ToArray();
    }

    private DocumentFormat.OpenXml.Wordprocessing.Paragraph CreateHeading(string text, int level)
    {
        var paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
        var run = paragraph.AppendChild(new Run(new Text(text)));
        
        var properties = paragraph.AppendChild(new ParagraphProperties());
        properties.ParagraphStyleId = new ParagraphStyleId { Val = $"Heading{level}" };
        
        var runProperties = run.PrependChild(new RunProperties());
        runProperties.Bold = new Bold();
        runProperties.FontSize = new FontSize { Val = (28 - (level * 4)).ToString() };
        
        return paragraph;
    }

    private DocumentFormat.OpenXml.Wordprocessing.Paragraph CreateParagraphWithFormatting(string text)
    {
        var paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
        var parts = text.Split(new[] { "**" }, StringSplitOptions.None);
        
        for (int i = 0; i < parts.Length; i++)
        {
            var run = new Run(new Text(parts[i]));
            if (i % 2 == 1) // Odd indices are bold
            {
                run.RunProperties = new RunProperties(new Bold());
            }
            paragraph.AppendChild(run);
        }
        
        return paragraph;
    }

    private async Task<byte[]> ConvertMarkdownToPdfAsync(string markdown, CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        
        using var memoryStream = new MemoryStream();
        var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.Letter, 50, 50, 50, 50);
        
        try
        {
            PdfWriter.GetInstance(document, memoryStream);
            document.Open();

            // Add document metadata
            document.AddTitle("Compliance Document");
            document.AddAuthor("Platform Engineering Copilot");
            document.AddCreator("ATO Document Generation Service");
            document.AddCreationDate();

            // Define fonts  
            var titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18, BaseColor.Black);
            var heading1Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16, BaseColor.Black);
            var heading2Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14, BaseColor.Black);
            var heading3Font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12, BaseColor.Black);
            var normalFont = FontFactory.GetFont(FontFactory.HELVETICA, 11, BaseColor.Black);
            var boldFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11, BaseColor.Black);

            // Parse markdown and convert to PDF
            var lines = markdown.Split('\n');
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    document.Add(new iTextSharp.text.Paragraph(" ", normalFont));
                    continue;
                }

                iTextSharp.text.Paragraph paragraph;

                // Handle headers
                if (line.StartsWith("# "))
                {
                    paragraph = new iTextSharp.text.Paragraph(line.Substring(2), titleFont);
                    paragraph.SpacingBefore = 10;
                    paragraph.SpacingAfter = 5;
                }
                else if (line.StartsWith("## "))
                {
                    paragraph = new iTextSharp.text.Paragraph(line.Substring(3), heading1Font);
                    paragraph.SpacingBefore = 8;
                    paragraph.SpacingAfter = 4;
                }
                else if (line.StartsWith("### "))
                {
                    paragraph = new iTextSharp.text.Paragraph(line.Substring(4), heading2Font);
                    paragraph.SpacingBefore = 6;
                    paragraph.SpacingAfter = 3;
                }
                else if (line.StartsWith("#### "))
                {
                    paragraph = new iTextSharp.text.Paragraph(line.Substring(5), heading3Font);
                    paragraph.SpacingBefore = 4;
                    paragraph.SpacingAfter = 2;
                }
                // Handle lists
                else if (line.TrimStart().StartsWith("- ") || line.TrimStart().StartsWith("* "))
                {
                    var text = line.TrimStart().Substring(2);
                    paragraph = new iTextSharp.text.Paragraph("• " + text, normalFont);
                    paragraph.IndentationLeft = 20;
                }
                // Handle numbered lists
                else if (char.IsDigit(line.TrimStart().FirstOrDefault()) && line.Contains(". "))
                {
                    paragraph = new iTextSharp.text.Paragraph(line.TrimStart(), normalFont);
                    paragraph.IndentationLeft = 20;
                }
                // Handle bold text (simplified - handles **text**)
                else if (line.Contains("**"))
                {
                    paragraph = CreatePdfParagraphWithFormatting(line, normalFont, boldFont);
                }
                // Regular paragraph
                else
                {
                    paragraph = new iTextSharp.text.Paragraph(line, normalFont);
                }

                document.Add(paragraph);
            }

            document.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating PDF document");
            throw;
        }

        return memoryStream.ToArray();
    }

    private iTextSharp.text.Paragraph CreatePdfParagraphWithFormatting(
        string text, 
        iTextSharp.text.Font normalFont, 
        iTextSharp.text.Font boldFont)
    {
        var paragraph = new iTextSharp.text.Paragraph();
        var parts = text.Split(new[] { "**" }, StringSplitOptions.None);
        
        for (int i = 0; i < parts.Length; i++)
        {
            var chunk = new Chunk(parts[i], i % 2 == 1 ? boldFont : normalFont);
            paragraph.Add(chunk);
        }
        
        return paragraph;
    }

    private void ApplyNistFormatting(GeneratedDocument document)
    {
        // Apply NIST-specific formatting rules
        document.Metadata["FormattingStandard"] = "NIST SP 800-53 Rev 5";
    }

    private void ApplyFedRampFormatting(GeneratedDocument document)
    {
        // Apply FedRAMP-specific formatting rules
        document.Metadata["FormattingStandard"] = "FedRAMP";
    }

    private void ApplyDodRmfFormatting(GeneratedDocument document)
    {
        // Apply DoD RMF-specific formatting rules
        document.Metadata["FormattingStandard"] = "DoD RMF";
    }

    private void ApplyFismaFormatting(GeneratedDocument document)
    {
        // Apply FISMA-specific formatting rules
        document.Metadata["FormattingStandard"] = "FISMA";
    }

    // Blob Storage Persistence Methods

    /// <summary>
    /// Stores a generated document to Azure Blob Storage
    /// </summary>
    public async Task<string> StoreDocumentAsync(
        GeneratedDocument document,
        byte[]? exportedBytes = null,
        ComplianceDocumentFormat format = ComplianceDocumentFormat.Markdown,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Azure Storage connection string not configured. Document not persisted.");
                return document.DocumentId;
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerName = "compliance-documents";
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            // Determine file extension
            var extension = format switch
            {
                ComplianceDocumentFormat.DOCX => "docx",
                ComplianceDocumentFormat.PDF => "pdf",
                ComplianceDocumentFormat.HTML => "html",
                _ => "md"
            };

            // Create blob path: documents/{type}/{year}/{month}/{documentId}.{ext}
            var now = DateTime.UtcNow;
            var blobName = $"documents/{document.DocumentType.ToLower()}/{now:yyyy}/{now:MM}/{document.DocumentId}.{extension}";
            var blobClient = containerClient.GetBlobClient(blobName);

            // Prepare content
            byte[] content;
            string contentType;
            
            if (exportedBytes != null)
            {
                content = exportedBytes;
                contentType = format switch
                {
                    ComplianceDocumentFormat.DOCX => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    ComplianceDocumentFormat.PDF => "application/pdf",
                    ComplianceDocumentFormat.HTML => "text/html",
                    _ => "text/markdown"
                };
            }
            else
            {
                content = Encoding.UTF8.GetBytes(document.Content);
                contentType = "text/markdown";
            }

            // Upload with metadata
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                },
                Metadata = new Dictionary<string, string>
                {
                    { "DocumentId", document.DocumentId },
                    { "DocumentType", document.DocumentType },
                    { "Title", document.Title },
                    { "Version", document.Version },
                    { "GeneratedDate", document.GeneratedDate.ToString("O") },
                    { "Classification", document.Classification },
                    { "Format", format.ToString() }
                }
            };

            await blobClient.UploadAsync(new BinaryData(content), uploadOptions, cancellationToken);

            _logger.LogInformation("Document {DocumentId} stored to blob storage: {BlobName}", 
                document.DocumentId, blobName);

            return blobName;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing document {DocumentId} to blob storage", document.DocumentId);
            throw;
        }
    }

    /// <summary>
    /// Retrieves a document from Azure Blob Storage
    /// </summary>
    public async Task<(byte[] Content, string ContentType)?> RetrieveDocumentAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Azure Storage connection string not configured.");
                return null;
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerName = "compliance-documents";
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                _logger.LogWarning("Document not found in blob storage: {BlobName}", blobName);
                return null;
            }

            var download = await blobClient.DownloadContentAsync(cancellationToken);
            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

            return (download.Value.Content.ToArray(), properties.Value.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document from blob storage: {BlobName}", blobName);
            throw;
        }
    }

    /// <summary>
    /// Lists all documents in blob storage for a specific package/subscription
    /// </summary>
    public async Task<List<ComplianceDocumentMetadata>> ListStoredDocumentsAsync(
        string packageId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Azure Storage connection string not configured.");
                return new List<ComplianceDocumentMetadata>();
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerName = "compliance-documents";
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);

            var documents = new List<ComplianceDocumentMetadata>();

            await foreach (var blobItem in containerClient.GetBlobsAsync(
                prefix: "documents/",
                cancellationToken: cancellationToken))
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);

                if (properties.Value.Metadata.TryGetValue("DocumentId", out var docId) &&
                    docId.Contains(packageId))
                {
                    documents.Add(new ComplianceDocumentMetadata
                    {
                        DocumentId = properties.Value.Metadata.TryGetValue("DocumentId", out var docIdVal) ? docIdVal : "",
                        DocumentType = properties.Value.Metadata.TryGetValue("DocumentType", out var docTypeVal) ? docTypeVal : "",
                        Title = properties.Value.Metadata.TryGetValue("Title", out var titleVal) ? titleVal : "",
                        Version = properties.Value.Metadata.TryGetValue("Version", out var versionVal) ? versionVal : "1.0",
                        LastModified = properties.Value.LastModified.DateTime,
                        Status = "Stored",
                        SizeBytes = blobItem.Properties.ContentLength ?? 0,
                        StorageUri = blobClient.Uri.ToString()
                    });
                }
            }

            return documents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing documents from blob storage for package {PackageId}", packageId);
            throw;
        }
    }

    /// <summary>
    /// Deletes a document from blob storage
    /// </summary>
    public async Task<bool> DeleteDocumentAsync(
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("Azure Storage connection string not configured.");
                return false;
            }

            var blobServiceClient = new BlobServiceClient(connectionString);
            var containerName = "compliance-documents";
            var containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var result = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

            if (result.Value)
            {
                _logger.LogInformation("Document deleted from blob storage: {BlobName}", blobName);
            }

            return result.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting document from blob storage: {BlobName}", blobName);
            throw;
        }
    }
}
