using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Constants;
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
using ClosedXML.Excel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Documents;

/// <summary>
/// AI-Enhanced document generation service for ATO compliance documents (SSP, SAR, POA&M)
/// Uses GPT-4 to generate high-quality control narratives, risk assessments, and remediation plans
/// </summary>
public class DocumentGenerationService : IDocumentGenerationService
{
    private readonly IAtoComplianceEngine _complianceEngine;
    private readonly INistControlsService _nistService;
    private readonly IEvidenceStorageService _storageService;
    private readonly ILogger<DocumentGenerationService> _logger;
    private readonly IChatCompletionService? _chatCompletion;
    private readonly DocumentAgentOptions _options;

    public DocumentGenerationService(
        IAtoComplianceEngine complianceEngine,
        INistControlsService nistService,
        IEvidenceStorageService storageService,
        ILogger<DocumentGenerationService> logger,
        IOptions<DocumentAgentOptions> options,
        Kernel? kernel = null)
    {
        _complianceEngine = complianceEngine ?? throw new ArgumentNullException(nameof(complianceEngine));
        _nistService = nistService ?? throw new ArgumentNullException(nameof(nistService));
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _chatCompletion = kernel?.Services.GetService(typeof(IChatCompletionService)) as IChatCompletionService;
        
        if (_options.EnableAIGeneration && _chatCompletion != null)
        {
            _logger.LogInformation("✅ Document Generation Service initialized with AI-enhanced capabilities");
        }
        else if (!_options.EnableAIGeneration)
        {
            _logger.LogInformation("📄 Document Generation Service initialized with AI generation disabled (template-based only)");
        }
        else
        {
            _logger.LogInformation("📄 Document Generation Service initialized without AI (will use template-based generation)");
        }
        
        _logger.LogInformation("📄 Document configuration: Storage={StorageAccount}/{Container}, MaxSize={MaxDocMB}MB, Templates={TemplatesEnabled}",
            _options.DocumentGeneration.StorageAccount, _options.DocumentGeneration.Container,
            _options.DocumentGeneration.MaxDocumentSizeMB, _options.EnableTemplates);
    }

    public async Task<ControlNarrative> GenerateControlNarrativeAsync(
        string controlId,
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableAIGeneration)
        {
            _logger.LogInformation("AI generation disabled. Returning basic control narrative for {ControlId}", controlId);
            // Return basic control narrative without AI enhancement
            return new ControlNarrative
            {
                ControlId = controlId,
                ControlTitle = controlId,
                What = "Manual narrative required - AI generation disabled",
                ImplementationStatus = "Pending",
                LastReviewed = DateTime.UtcNow
            };
        }

        _logger.LogInformation("Generating control narrative for {ControlId} with AI enhancement", controlId);

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
        
        // AI Enhancement: Generate professional control narrative
        if (_chatCompletion != null && control != null)
        {
            try
            {
                _logger.LogInformation("Enhancing narrative with AI for {ControlId}", controlId);
                var aiNarrative = await GenerateAiControlNarrativeAsync(control, evidencePackage, assessment, cancellationToken);
                
                if (aiNarrative != null)
                {
                    narrative.What = aiNarrative.What ?? narrative.What;
                    narrative.How = aiNarrative.How ?? narrative.How;
                    narrative.Evidence = aiNarrative.Evidence;
                    narrative.Gaps = aiNarrative.Gaps;
                    narrative.ResponsibleParty = aiNarrative.ResponsibleParty;
                    narrative.ImplementationDetails = aiNarrative.ImplementationDetails;
                    
                    _logger.LogInformation("AI-enhanced narrative generated for {ControlId}", controlId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate AI narrative for {ControlId}, using template-based narrative", controlId);
            }
        }

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

        // Executive Summary (AI-Enhanced)
        content.AppendLine("## 1. Executive Summary");
        content.AppendLine();
        
        if (_chatCompletion != null && assessment != null)
        {
            _logger.LogInformation("Generating AI-enhanced executive summary");
            var aiSummary = await GenerateAiExecutiveSummaryAsync(assessment, parameters, cancellationToken);
            content.AppendLine(aiSummary);
        }
        else
        {
            content.AppendLine($"This System Security Plan (SSP) documents the security controls implemented for {parameters.SystemName}.");
            content.AppendLine($"The system operates on Azure Government and follows NIST 800-53 Rev 5 controls.");
            if (assessment != null)
            {
                content.AppendLine($"Current compliance score: {assessment.OverallComplianceScore:F1}%");
                content.AppendLine($"Total findings: {findings.Count}");
            }
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

        // Generate Excel workbook
        _logger.LogInformation("Creating Excel POA&M workbook with {Count} findings", findings.Count);
        var excelBytes = GeneratePoamExcelWorkbook(subscriptionId, findings);

        // Store Excel to blob storage
        var blobUri = await StoreDocumentAsync(document, excelBytes, ComplianceDocumentFormat.DOCX, cancellationToken);
        
        document.Content = $"POA&M Excel workbook generated with {findings.Count} findings. Download: {blobUri}";
        document.Metadata["ExcelUri"] = blobUri;
        
        _logger.LogInformation("POA&M generated for {SubscriptionId}", subscriptionId);
        return document;
    }

    /// <summary>
    /// Generate AI-Enhanced POA&M as Excel workbook using ClosedXML
    /// </summary>
    private byte[] GeneratePoamExcelWorkbook(string subscriptionId, List<AtoFinding> findings)
    {
        using var workbook = new XLWorkbook();
        
        // Create POA&M Items worksheet
        var worksheet = workbook.Worksheets.Add("POA&M Items");
        
        // Set up headers
        var headers = new[]
        {
            "POA&M ID", "Weakness Description", "Risk Narrative", "Affected Controls", "Severity", 
            "Remediation Plan", "Resources Required", "Responsible Party", 
            "Scheduled Completion Date", "Milestones", "Status", "Comments"
        };
        
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = worksheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightBlue;
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;
        }
        
        // Populate finding rows (AI-Enhanced)
        int row = 2;
        foreach (var finding in findings.OrderByDescending(f => f.Severity))
        {
            // Generate AI-powered content for this finding (synchronous wrapper)
            string riskNarrative = "";
            string milestones = "";
            
            if (_chatCompletion != null)
            {
                try
                {
                    // Get AI-enhanced risk narrative
                    var narrativeTask = GenerateAiRiskNarrativeAsync(finding, null, CancellationToken.None);
                    riskNarrative = narrativeTask.GetAwaiter().GetResult();
                    
                    // Get AI-enhanced milestones
                    var milestonesTask = GenerateAiPoamMilestonesAsync(finding, CancellationToken.None);
                    var milestonesList = milestonesTask.GetAwaiter().GetResult();
                    milestones = string.Join("; ", milestonesList);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate AI content for finding {FindingId}, using templates", finding.Id);
                    riskNarrative = GenerateTemplateRiskNarrative(finding);
                    milestones = $"{GenerateMilestone1(finding)}; {GenerateMilestone2(finding)}";
                }
            }
            else
            {
                riskNarrative = GenerateTemplateRiskNarrative(finding);
                milestones = $"{GenerateMilestone1(finding)}; {GenerateMilestone2(finding)}";
            }
            
            worksheet.Cell(row, 1).Value = $"POAM-{row - 1:D3}";
            worksheet.Cell(row, 2).Value = finding.Title;
            worksheet.Cell(row, 3).Value = riskNarrative;
            worksheet.Cell(row, 4).Value = string.Join(", ", finding.AffectedNistControls.Take(3));
            worksheet.Cell(row, 5).Value = finding.Severity.ToString();
            worksheet.Cell(row, 6).Value = finding.RemediationGuidance ?? finding.Recommendation;
            worksheet.Cell(row, 7).Value = DetermineResourcesRequired(finding);
            worksheet.Cell(row, 8).Value = DetermineResponsibleParty(finding);
            worksheet.Cell(row, 9).Value = CalculateTargetDate(finding.Severity);
            worksheet.Cell(row, 10).Value = milestones;
            worksheet.Cell(row, 11).Value = "Open";
            worksheet.Cell(row, 12).Value = "";
            
            // Color-code by severity
            var rowRange = worksheet.Range(row, 1, row, 12);
            rowRange.Style.Fill.BackgroundColor = finding.Severity switch
            {
                AtoFindingSeverity.Critical => XLColor.Red,
                AtoFindingSeverity.High => XLColor.LightPink,
                AtoFindingSeverity.Medium => XLColor.LightYellow,
                _ => XLColor.White
            };
            
            row++;
        }
        
        // Auto-fit columns
        worksheet.Columns().AdjustToContents();
        
        // Add summary worksheet
        var summarySheet = workbook.Worksheets.Add("Summary");
        summarySheet.Cell("A1").Value = "POA&M Summary";
        summarySheet.Cell("A1").Style.Font.Bold = true;
        summarySheet.Cell("A1").Style.Font.FontSize = 16;
        
        summarySheet.Cell("A3").Value = "Subscription ID:";
        summarySheet.Cell("B3").Value = subscriptionId;
        summarySheet.Cell("A4").Value = "Generated Date:";
        summarySheet.Cell("B4").Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
        summarySheet.Cell("A5").Value = "Total Items:";
        summarySheet.Cell("B5").Value = findings.Count;
        
        summarySheet.Cell("A7").Value = "By Severity:";
        summarySheet.Cell("A7").Style.Font.Bold = true;
        summarySheet.Cell("A8").Value = "Critical:";
        summarySheet.Cell("B8").Value = findings.Count(f => f.Severity == AtoFindingSeverity.Critical);
        summarySheet.Cell("A9").Value = "High:";
        summarySheet.Cell("B9").Value = findings.Count(f => f.Severity == AtoFindingSeverity.High);
        summarySheet.Cell("A10").Value = "Medium:";
        summarySheet.Cell("B10").Value = findings.Count(f => f.Severity == AtoFindingSeverity.Medium);
        summarySheet.Cell("A11").Value = "Low:";
        summarySheet.Cell("B11").Value = findings.Count(f => f.Severity == AtoFindingSeverity.Low);
        
        summarySheet.Columns().AdjustToContents();
        
        // Save to byte array
        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return memoryStream.ToArray();
    }

    private string DetermineResourcesRequired(AtoFinding finding)
    {
        return finding.Severity switch
        {
            AtoFindingSeverity.Critical or AtoFindingSeverity.High => "Security Team, Platform Engineering",
            AtoFindingSeverity.Medium => "Platform Engineering",
            _ => "Development Team"
        };
    }

    private string DetermineResponsibleParty(AtoFinding finding)
    {
        var family = finding.AffectedNistControls.FirstOrDefault()?.Split('-')[0] ?? "General";
        return family switch
        {
            ComplianceConstants.ControlFamilies.AccessControl => "IAM Team",
            ComplianceConstants.ControlFamilies.AuditAccountability => "Security Operations",
            ComplianceConstants.ControlFamilies.SystemCommunications => "Network Security Team",
            ComplianceConstants.ControlFamilies.SystemInformationIntegrity => "Security Operations",
            ComplianceConstants.ControlFamilies.ConfigurationManagement => "Platform Engineering",
            ComplianceConstants.ControlFamilies.IdentificationAuthentication => "IAM Team",
            _ => "Security Team"
        };
    }

    private DateTime CalculateTargetDate(AtoFindingSeverity severity)
    {
        var daysToAdd = severity switch
        {
            AtoFindingSeverity.Critical => 15,
            AtoFindingSeverity.High => 30,
            AtoFindingSeverity.Medium => 90,
            AtoFindingSeverity.Low => 180,
            _ => 90
        };
        return DateTime.UtcNow.AddDays(daysToAdd);
    }

    private string GenerateMilestone1(AtoFinding finding)
    {
        return finding.Severity switch
        {
            AtoFindingSeverity.Critical or AtoFindingSeverity.High => "Initial assessment and remediation planning (+7 days)",
            AtoFindingSeverity.Medium => "Remediation planning (+30 days)",
            _ => "Remediation planning (+60 days)"
        };
    }

    private string GenerateMilestone2(AtoFinding finding)
    {
        return finding.Severity switch
        {
            AtoFindingSeverity.Critical or AtoFindingSeverity.High => "Remediation implementation (+15 days)",
            AtoFindingSeverity.Medium => "Remediation implementation (+60 days)",
            _ => "Remediation implementation (+120 days)"
        };
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
    
    #region AI-Enhanced Document Generation Methods
    
    /// <summary>
    /// Generate AI-powered control narrative with evidence synthesis
    /// </summary>
    private async Task<ControlNarrative?> GenerateAiControlNarrativeAsync(
        NistControl control,
        EvidencePackage? evidencePackage,
        AtoComplianceAssessment? assessment,
        CancellationToken cancellationToken)
    {
        if (_chatCompletion == null)
            return null;
            
        try
        {
            // Build context from evidence and assessment
            var evidenceContext = BuildEvidenceContext(control.Id, evidencePackage);
            var findingsContext = BuildFindingsContext(control.Id, assessment);
            
            var systemPrompt = @"You are an expert compliance officer specializing in NIST 800-53 security controls and ATO documentation. 
Your task is to generate professional, detailed control implementation narratives for System Security Plans (SSP).

Write in a clear, authoritative tone suitable for government security assessments. Focus on:
1. WHAT the control requires (control objective)
2. HOW the system implements it (implementation details)
3. Evidence of implementation (automated evidence from Azure)
4. Any gaps or weaknesses identified
5. Responsible parties and implementation details

Be specific about Azure services, policies, and configurations. Use technical detail appropriate for security auditors.";

            var controlDescription = control.Parts?.FirstOrDefault()?.Prose ?? control.Title ?? "Security control";
            var controlFamily = control.Props?.FirstOrDefault(p => p.Name == "family")?.Value ?? "Security";

            var userPrompt = $@"Generate a comprehensive control narrative for:

**Control ID:** {control.Id}
**Control Title:** {control.Title}
**Control Description:** {controlDescription}
**Control Family:** {controlFamily}

**Current Evidence:**
{evidenceContext}

**Compliance Findings:**
{findingsContext}

Generate a JSON response with:
{{
  ""what"": ""What the control requires (control objective)"",
  ""how"": ""How the system implements this control using Azure services"",
  ""evidence"": ""Summary of automated evidence collected"",
  ""gaps"": ""Any identified gaps or weaknesses"",
  ""responsibleParty"": ""Who is responsible for this control"",
  ""implementationDetails"": ""Specific Azure configurations and policies""
}}";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);
            
            var response = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);
            
            var responseText = response.Content ?? "";
            
            // Parse JSON response
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                responseText, 
                @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            
            if (jsonMatch.Success)
            {
                var json = JsonSerializer.Deserialize<AiNarrativeResponse>(jsonMatch.Value, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                if (json != null)
                {
                    return new ControlNarrative
                    {
                        ControlId = control.Id,
                        ControlTitle = control.Title,
                        What = json.What,
                        How = json.How,
                        Evidence = json.Evidence,
                        Gaps = json.Gaps,
                        ResponsibleParty = json.ResponsibleParty,
                        ImplementationDetails = json.ImplementationDetails,
                        ImplementationStatus = string.IsNullOrEmpty(json.Gaps) ? "Implemented" : "Partially Implemented",
                        ComplianceStatus = string.IsNullOrEmpty(json.Gaps) ? "Compliant" : "Non-Compliant"
                    };
                }
            }
            
            _logger.LogWarning("Failed to parse AI response for control {ControlId}", control.Id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI narrative for control {ControlId}", control.Id);
            return null;
        }
    }
    
    /// <summary>
    /// Generate AI-powered SSP executive summary
    /// </summary>
    private async Task<string> GenerateAiExecutiveSummaryAsync(
        AtoComplianceAssessment assessment,
        SspParameters parameters,
        CancellationToken cancellationToken)
    {
        if (_chatCompletion == null)
            return GenerateTemplateExecutiveSummary(assessment, parameters);
            
        try
        {
            var systemPrompt = @"You are an expert technical writer specializing in government security documentation and ATO packages.
Generate professional executive summaries for System Security Plans that are clear, concise, and suitable for executive leadership review.";

            var userPrompt = $@"Generate an executive summary for a System Security Plan with the following details:

**System Information:**
- System Name: {parameters.SystemName}
- System Owner: {parameters.SystemOwner}
- Classification: {parameters.Classification}
- Environment: {parameters.Environment}

**Compliance Assessment:**
- Overall Compliance Score: {assessment.OverallComplianceScore:F1}%
- Total Findings: {assessment.TotalFindings}
- Critical Findings: {assessment.CriticalFindings}
- High Findings: {assessment.HighFindings}
- Control Families Assessed: {assessment.ControlFamilyResults.Count}

**System Purpose:**
{parameters.SystemDescription}

Write a 3-4 paragraph executive summary that:
1. Describes the system and its purpose
2. Summarizes the security posture and compliance status
3. Highlights key security controls implemented
4. Notes any significant findings or risks
5. Provides confidence in the system's security";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);
            
            var response = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);
            
            return response.Content ?? GenerateTemplateExecutiveSummary(assessment, parameters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI executive summary");
            return GenerateTemplateExecutiveSummary(assessment, parameters);
        }
    }
    
    /// <summary>
    /// Generate AI-powered risk narrative for findings
    /// </summary>
    private async Task<string> GenerateAiRiskNarrativeAsync(
        AtoFinding finding,
        NistControl? control,
        CancellationToken cancellationToken)
    {
        if (_chatCompletion == null)
            return GenerateTemplateRiskNarrative(finding);
            
        try
        {
            var systemPrompt = @"You are a cybersecurity risk analyst specializing in cloud infrastructure and compliance.
Generate clear, concise risk narratives that explain the business impact of security findings in non-technical terms.";

            var userPrompt = $@"Generate a risk narrative for the following security finding:

**Finding ID:** {finding.Id}
**Control:** {control?.Id} - {control?.Title}
**Severity:** {finding.Severity}
**Resource:** {finding.ResourceName} ({finding.ResourceType})
**Description:** {finding.Description}
**Remediation:** {finding.RemediationGuidance}

Write a 2-3 sentence risk narrative that explains:
1. What the vulnerability is
2. What could happen if exploited (business impact)
3. Why remediation is important

Use clear language suitable for management review.";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);
            
            var response = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);
            
            return response.Content ?? GenerateTemplateRiskNarrative(finding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI risk narrative for finding {FindingId}", finding.Id);
            return GenerateTemplateRiskNarrative(finding);
        }
    }
    
    /// <summary>
    /// Generate AI-powered POA&M milestones and timelines
    /// </summary>
    private async Task<List<string>> GenerateAiPoamMilestonesAsync(
        AtoFinding finding,
        CancellationToken cancellationToken)
    {
        if (_chatCompletion == null)
            return GenerateTemplatePoamMilestones(finding);
            
        try
        {
            var systemPrompt = @"You are a project manager specializing in security remediation and compliance.
Generate realistic, actionable milestones for security remediation plans.";

            var userPrompt = $@"Generate remediation milestones for the following security finding:

**Finding:** {finding.Id}
**Severity:** {finding.Severity}
**Resource:** {finding.ResourceName}
**Issue:** {finding.Description}
**Remediation:** {finding.RemediationGuidance}

Generate 3-5 specific, actionable milestones with estimated timeframes. 
Return as a JSON array of strings, e.g.: [""Week 1: Review and approve remediation plan"", ""Week 2: Implement changes...""]";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(userPrompt);
            
            var response = await _chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);
            
            var responseText = response.Content ?? "";
            
            // Try to parse JSON array
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(
                responseText,
                @"\[.*?\]",
                System.Text.RegularExpressions.RegexOptions.Singleline);
            
            if (jsonMatch.Success)
            {
                var milestones = JsonSerializer.Deserialize<List<string>>(jsonMatch.Value);
                if (milestones != null && milestones.Count > 0)
                    return milestones;
            }
            
            return GenerateTemplatePoamMilestones(finding);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating AI milestones for finding {FindingId}", finding.Id);
            return GenerateTemplatePoamMilestones(finding);
        }
    }
    
    #endregion
    
    #region Helper Methods
    
    private string BuildEvidenceContext(string controlId, EvidencePackage? evidencePackage)
    {
        if (evidencePackage == null || !evidencePackage.Evidence.Any())
            return "No automated evidence available.";
        
        var relevantEvidence = evidencePackage.Evidence
            .Where(e => e.ControlId == controlId || e.ControlId.StartsWith(controlId.Split('-')[0]))
            .Take(5)
            .ToList();
        
        if (!relevantEvidence.Any())
            return "No specific evidence for this control.";
        
        var sb = new StringBuilder();
        foreach (var evidence in relevantEvidence)
        {
            sb.AppendLine($"- {evidence.EvidenceType}");
            sb.AppendLine($"  Collected: {evidence.CollectedAt:yyyy-MM-dd HH:mm}");
        }
        
        return sb.ToString();
    }
    
    private string BuildFindingsContext(string controlId, AtoComplianceAssessment? assessment)
    {
        if (assessment == null)
            return "No compliance assessment available.";
        
        var findings = assessment.ControlFamilyResults.Values
            .SelectMany(cf => cf.Findings)
            .Where(f => f.AffectedNistControls.Contains(controlId))
            .ToList();
        
        if (!findings.Any())
            return "No findings for this control - control is compliant.";
        
        var sb = new StringBuilder();
        foreach (var finding in findings.Take(3))
        {
            sb.AppendLine($"- {finding.Severity}: {finding.Description}");
            sb.AppendLine($"  Resource: {finding.ResourceName}");
        }
        
        if (findings.Count > 3)
            sb.AppendLine($"... and {findings.Count - 3} more findings");
        
        return sb.ToString();
    }
    
    private string GenerateTemplateExecutiveSummary(AtoComplianceAssessment assessment, SspParameters parameters)
    {
        return $@"## Executive Summary

The {parameters.SystemName} is a {parameters.Classification} system operating in the {parameters.Environment} environment. 
{parameters.SystemDescription}

This System Security Plan documents the security controls implemented to protect the system and its data. 
The system has been assessed against NIST 800-53 security controls with an overall compliance score of {assessment.OverallComplianceScore:F1}%.

Key security features include:
- Azure Security Center for continuous monitoring
- Role-Based Access Control (RBAC) for access management  
- Azure Policy for automated compliance enforcement
- Comprehensive logging and monitoring via Azure Monitor

Total findings identified: {assessment.TotalFindings} ({assessment.CriticalFindings} Critical, {assessment.HighFindings} High, {assessment.MediumFindings} Medium, {assessment.LowFindings} Low)";
    }
    
    private string GenerateTemplateRiskNarrative(AtoFinding finding)
    {
        return $@"This {finding.Severity.ToString().ToLower()} severity finding indicates {finding.Description.ToLower()}. 
If not addressed, this could lead to {GetRiskImpact(finding.Severity)}. 
Remediation is {GetRemediationUrgency(finding.Severity)} to maintain system security posture.";
    }
    
    private string GetRiskImpact(AtoFindingSeverity severity)
    {
        return severity switch
        {
            AtoFindingSeverity.Critical => "immediate security breach, data loss, or system compromise",
            AtoFindingSeverity.High => "significant security vulnerabilities and potential unauthorized access",
            AtoFindingSeverity.Medium => "security gaps that could be exploited in combination with other vulnerabilities",
            AtoFindingSeverity.Low => "minor security concerns that should be addressed as part of continuous improvement",
            _ => "potential security issues"
        };
    }
    
    private string GetRemediationUrgency(AtoFindingSeverity severity)
    {
        return severity switch
        {
            AtoFindingSeverity.Critical => "required immediately",
            AtoFindingSeverity.High => "highly recommended within 30 days",
            AtoFindingSeverity.Medium => "recommended within 90 days",
            AtoFindingSeverity.Low => "recommended for the next maintenance cycle",
            _ => "recommended"
        };
    }
    
    private List<string> GenerateTemplatePoamMilestones(AtoFinding finding)
    {
        var milestones = new List<string>
        {
            "Week 1: Review and approve remediation plan",
            "Week 2: Implement technical controls and configuration changes",
            "Week 3: Validate remediation and run compliance scan",
            "Week 4: Document changes and update control narratives"
        };
        
        if (finding.Severity == AtoFindingSeverity.Critical)
        {
            milestones.Insert(0, "Day 1: Emergency response and immediate mitigation");
        }
        
        return milestones;
    }
    
    #endregion
    
    #region AI Response Models
    
    private class AiNarrativeResponse
    {
        public string? What { get; set; }
        public string? How { get; set; }
        public string? Evidence { get; set; }
        public string? Gaps { get; set; }
        public string? ResponsibleParty { get; set; }
        public string? ImplementationDetails { get; set; }
    }
    
    #endregion
}
