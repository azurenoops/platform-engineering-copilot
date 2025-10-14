using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iTextSharp.text.pdf;
using Platform.Engineering.Copilot.DocumentProcessing.Models;
using Platform.Engineering.Copilot.DocumentProcessing.Analyzers;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.DocumentProcessing.Services;

/// <summary>
/// Core service for processing and analyzing documents for compliance and architecture assessment.
/// Handles document upload, content extraction from multiple formats (PDF, Word, PowerPoint, Visio),
/// architecture diagram analysis, and RMF compliance evaluation using the unified compliance service.
/// Supports asynchronous processing with status tracking for large document analysis operations.
/// </summary>
public class DocumentProcessingService : IDocumentProcessingService
{
    private readonly ILogger<DocumentProcessingService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IArchitectureDiagramAnalyzer _diagramAnalyzer;
    private readonly IAtoComplianceEngine _atoComplianceEngine;
    private readonly IAtoRemediationEngine _atoRemediationEngine;
    private readonly string _uploadsPath;
    private readonly Dictionary<string, DocumentProcessingStatus> _processingStatuses;

    /// <summary>
    /// Initializes a new instance of the DocumentProcessingService.
    /// Sets up document processing pipeline with compliance analysis integration.
    /// </summary>
    /// <param name="logger">Logger for processing diagnostics and audit trail</param>
    /// <param name="configuration">Application configuration including upload paths and processing options</param>
    /// <param name="diagramAnalyzer">Analyzer for extracting and analyzing architecture diagrams</param>
    /// <param name="atoComplianceEngine">ATO compliance engine for RMF and compliance analysis from Core</param>
    /// <param name="atoRemediationEngine">ATO remediation engine for generating remediation recommendations from Core</param>
    public DocumentProcessingService(
        ILogger<DocumentProcessingService> logger,
        IConfiguration configuration,
        IArchitectureDiagramAnalyzer diagramAnalyzer,
        IAtoComplianceEngine atoComplianceEngine,
        IAtoRemediationEngine atoRemediationEngine)
    {
        _logger = logger;
        _configuration = configuration;
        _diagramAnalyzer = diagramAnalyzer;
        _atoComplianceEngine = atoComplianceEngine;
        _atoRemediationEngine = atoRemediationEngine;
        _uploadsPath = configuration["DocumentProcessing:UploadsPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _processingStatuses = new Dictionary<string, DocumentProcessingStatus>();
        
        Directory.CreateDirectory(_uploadsPath);
    }

    /// <summary>
    /// Processes an uploaded document through the complete analysis pipeline.
    /// Handles file validation, content extraction, architecture analysis, and compliance assessment.
    /// Supports various document formats and provides real-time processing status updates.
    /// </summary>
    /// <param name="file">Uploaded document file (PDF, Word, PowerPoint, Visio, images)</param>
    /// <param name="analysisType">Type of analysis to perform (Architecture, Compliance, Security, or Full)</param>
    /// <param name="conversationId">Optional conversation ID for chat integration tracking</param>
    /// <returns>Processing result with document ID and initial analysis summary</returns>
    public async Task<DocumentProcessingResult> ProcessDocumentAsync(IFormFile file, DocumentAnalysisType analysisType, string? conversationId = null)
    {
        var documentId = Guid.NewGuid().ToString();
        var fileName = file.FileName;
        var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
        
        _logger.LogInformation("Starting document processing for {FileName} ({DocumentId})", fileName, documentId);

        // Initialize processing status
        var status = new DocumentProcessingStatus
        {
            DocumentId = documentId,
            Status = ProcessingStatus.Processing,
            ProgressPercentage = 0,
            CurrentStep = "Uploading file",
            CompletedSteps = new List<string>(),
            LastUpdated = DateTime.UtcNow
        };
        _processingStatuses[documentId] = status;

        try
        {
            // Save uploaded file
            var filePath = Path.Combine(_uploadsPath, $"{documentId}_{fileName}");
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            UpdateProcessingStatus(documentId, ProcessingStatus.Processing, 20, "File uploaded, extracting content");

            // Extract content based on file type
            var extractedContent = await ExtractContentAsync(filePath, fileExtension);
            
            UpdateProcessingStatus(documentId, ProcessingStatus.Processing, 40, "Content extracted, analyzing metadata");

            // Extract metadata
            var metadata = await ExtractMetadataAsync(filePath, fileExtension);

            UpdateProcessingStatus(documentId, ProcessingStatus.Analyzing, 60, "Performing document analysis");

            // Perform analysis based on type
            var analysis = await PerformAnalysisAsync(extractedContent, metadata, analysisType);

            UpdateProcessingStatus(documentId, ProcessingStatus.Complete, 100, "Analysis complete");

            // Save analysis results
            await SaveAnalysisResultsAsync(documentId, fileName, analysisType, metadata, extractedContent, analysis);

            return new DocumentProcessingResult
            {
                DocumentId = documentId,
                ProcessingStatus = ProcessingStatus.Complete,
                AnalysisPreview = GenerateAnalysisPreview(analysis),
                EstimatedProcessingTime = TimeSpan.FromMinutes(2),
                ExtractedText = extractedContent.Sections.Select(s => s.Content).ToList(),
                ExtractedImages = extractedContent.Images.Select(i => i.Description).ToList(),
                Metadata = metadata
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document {DocumentId}", documentId);
            UpdateProcessingStatus(documentId, ProcessingStatus.Error, 0, "Processing failed", ex.Message);
            throw;
        }
    }

    public async Task<DocumentProcessingStatus> GetProcessingStatusAsync(string documentId)
    {
        if (_processingStatuses.TryGetValue(documentId, out var status))
        {
            return status;
        }
        
        throw new KeyNotFoundException($"Document processing status not found for ID: {documentId}");
    }

    public async Task<DocumentAnalysis> GetDocumentAnalysisAsync(string documentId)
    {
        var analysisFilePath = Path.Combine(_uploadsPath, $"{documentId}_analysis.json");
        
        if (!File.Exists(analysisFilePath))
        {
            throw new FileNotFoundException($"Document analysis not found for ID: {documentId}");
        }

        var json = await File.ReadAllTextAsync(analysisFilePath);
        var analysis = JsonSerializer.Deserialize<DocumentAnalysis>(json);
        
        return analysis ?? throw new InvalidOperationException("Failed to deserialize document analysis");
    }

    public async Task<ComplianceAnalysisResult> PerformRmfAnalysisAsync(string documentId, string frameworkType = "NIST-800-53")
    {
        var documentAnalysis = await GetDocumentAnalysisAsync(documentId);
        
        _logger.LogInformation("Performing RMF analysis for document {DocumentId} using {Framework} framework", 
            documentId, frameworkType);

        // Convert document analysis to ATO findings for compliance assessment
        var findings = ConvertDocumentAnalysisToFindings(documentAnalysis);

        // Use remediation engine to analyze document-based findings and generate recommendations
        var remediationPlan = await _atoRemediationEngine.GenerateRemediationPlanAsync(
            documentId, // Use documentId as subscription for document-based analysis
            findings,
            new RemediationPlanOptions
            {
                MinimumSeverity = AtoFindingSeverity.Low,
                IncludeOnlyAutomatable = false
            },
            CancellationToken.None);

        // Map remediation plan to ComplianceAnalysisResult
        return MapRemediationPlanToComplianceResult(documentId, documentAnalysis, remediationPlan, frameworkType);
    }

    /// <summary>
    /// Converts DocumentAnalysis security findings into AtoFinding objects for compliance assessment
    /// </summary>
    private List<AtoFinding> ConvertDocumentAnalysisToFindings(DocumentAnalysis documentAnalysis)
    {
        var findings = new List<AtoFinding>();

        // Convert security analysis findings if available
        if (documentAnalysis.SecurityAnalysis != null)
        {
            // Convert compliance gaps to findings
            foreach (var gap in documentAnalysis.SecurityAnalysis.ComplianceGaps)
            {
                var controlId = ExtractControlIdFromGap(gap.Description);
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    RuleId = controlId,
                    Severity = MapRiskLevelToSeverity(gap.RiskLevel),
                    Title = gap.Description,
                    Description = gap.Remediation,
                    ResourceId = documentAnalysis.DocumentId,
                    ResourceType = "Document",
                    FindingType = AtoFindingType.Configuration,
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    DetectedAt = documentAnalysis.AnalyzedAt,
                    Evidence = $"DocumentFileName: {documentAnalysis.FileName}, RiskLevel: {gap.RiskLevel}",
                    AffectedNistControls = new List<string> { controlId },
                    Metadata = new Dictionary<string, object>
                    {
                        ["AnalysisType"] = documentAnalysis.AnalysisType.ToString(),
                        ["RiskLevel"] = gap.RiskLevel.ToString()
                    }
                });
            }

            // Convert security risks to findings
            foreach (var risk in documentAnalysis.SecurityAnalysis.IdentifiedRisks)
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    RuleId = "DOC-RISK",
                    Severity = MapRiskLevelToSeverity(risk.Level),
                    Title = risk.Name,
                    Description = risk.Description,
                    ResourceId = documentAnalysis.DocumentId,
                    ResourceType = "Document",
                    FindingType = AtoFindingType.Security,
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    DetectedAt = documentAnalysis.AnalyzedAt,
                    Evidence = $"DocumentFileName: {documentAnalysis.FileName}, RiskLevel: {risk.Level}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["RiskLevel"] = risk.Level.ToString()
                    }
                });
            }

            // Convert unimplemented controls to findings
            foreach (var control in documentAnalysis.SecurityAnalysis.IdentifiedControls
                .Where(c => c.Status != ControlImplementationStatus.Implemented))
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    RuleId = control.ControlId,
                    Severity = control.Status == ControlImplementationStatus.NotImplemented 
                        ? AtoFindingSeverity.High 
                        : AtoFindingSeverity.Medium,
                    Title = $"Control {control.Name} - {control.Status}",
                    Description = $"Security control implementation status: {control.Status}",
                    ResourceId = documentAnalysis.DocumentId,
                    ResourceType = "Document",
                    FindingType = AtoFindingType.Compliance,
                    ComplianceStatus = AtoComplianceStatus.PartiallyCompliant,
                    DetectedAt = documentAnalysis.AnalyzedAt,
                    Evidence = $"DocumentFileName: {documentAnalysis.FileName}, ImplementationStatus: {control.Status}",
                    AffectedNistControls = new List<string> { control.ControlId },
                    Metadata = new Dictionary<string, object>
                    {
                        ["ImplementationStatus"] = control.Status.ToString()
                    }
                });
            }
        }

        // If no findings from security analysis, create a general finding
        if (!findings.Any())
        {
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                RuleId = "DOC-REVIEW",
                Severity = AtoFindingSeverity.Informational,
                Title = "Document requires manual compliance review",
                Description = $"Document '{documentAnalysis.FileName}' has been processed but requires manual review for compliance assessment.",
                ResourceId = documentAnalysis.DocumentId,
                ResourceType = "Document",
                FindingType = AtoFindingType.Configuration,
                ComplianceStatus = AtoComplianceStatus.Unknown,
                DetectedAt = documentAnalysis.AnalyzedAt,
                Evidence = $"DocumentFileName: {documentAnalysis.FileName}, AnalysisType: {documentAnalysis.AnalysisType}",
                Metadata = new Dictionary<string, object>
                {
                    ["DocumentFileName"] = documentAnalysis.FileName,
                    ["AnalysisType"] = documentAnalysis.AnalysisType.ToString()
                }
            });
        }

        return findings;
    }

    /// <summary>
    /// Maps RemediationPlan to ComplianceAnalysisResult for backward compatibility
    /// </summary>
    private ComplianceAnalysisResult MapRemediationPlanToComplianceResult(
        string documentId,
        DocumentAnalysis documentAnalysis,
        RemediationPlan remediationPlan,
        string frameworkType)
    {
        var result = new ComplianceAnalysisResult
        {
            Id = Guid.NewGuid(),
            DocumentId = Guid.Parse(documentId),
            AnalyzedAt = DateTime.UtcNow,
            ComplianceScore = CalculateDocumentComplianceScore(documentAnalysis),
            OverallStatus = DetermineComplianceStatus(documentAnalysis),
            Summary = $"Document compliance analysis for '{documentAnalysis.FileName}' using {frameworkType}. " +
                     $"Found {remediationPlan.RemediationItems.Count} findings requiring attention.",
            AnalysisMetadata = new Dictionary<string, object>
            {
                ["Framework"] = frameworkType,
                ["DocumentFileName"] = documentAnalysis.FileName,
                ["AnalysisType"] = documentAnalysis.AnalysisType.ToString(),
                ["TotalFindings"] = remediationPlan.RemediationItems.Count
            }
        };

        // Convert remediation items to control assessments
        foreach (var item in remediationPlan.RemediationItems)
        {
            result.ControlAssessments.Add(new ControlAssessment
            {
                ControlId = item.ControlId ?? "UNKNOWN",
                ControlTitle = item.Title,
                ControlFamily = ExtractControlFamily(item.ControlId ?? "UNKNOWN"),
                Status = ControlComplianceStatus.NotImplemented,
                ImplementationScore = 0.0,
                Assessment = item.Title,
                Evidence = item.Notes ?? string.Empty,
                Findings = new List<string> { item.FindingId },
                AssessedAt = DateTime.UtcNow
            });

            // Add as compliance gap - use priority to determine severity
            var severity = MapPriorityToGapSeverity(item.Priority);
            result.Gaps.Add(new Platform.Engineering.Copilot.Core.Models.ComplianceGap
            {
                ControlId = item.ControlId ?? "UNKNOWN",
                Title = item.Title,
                Description = item.Notes ?? item.Title,
                Severity = severity,
                ImpactAssessment = $"Priority: {item.Priority ?? "Medium"}",
                RecommendedAction = item.Title,
                AffectedSystems = new List<string> { documentAnalysis.FileName }
            });
        }

        // Add general recommendations for document compliance
        if (!string.IsNullOrEmpty(remediationPlan.ExecutiveSummary))
        {
            result.Recommendations.Add(new ComplianceRecommendation
            {
                Title = "Document Compliance Review",
                Description = remediationPlan.ExecutiveSummary,
                Priority = RecommendationPriority.High,
                Category = "Document Compliance"
            });
        }

        return result;
    }

    private string ExtractControlIdFromGap(string description)
    {
        // Try to extract NIST control ID from description (e.g., AC-1, SC-7)
        var match = System.Text.RegularExpressions.Regex.Match(description, @"\b([A-Z]{2}-\d+)\b");
        return match.Success ? match.Value : "GENERAL";
    }

    private string ExtractControlFamily(string controlId)
    {
        // Extract family from control ID (e.g., "AC" from "AC-1")
        var parts = controlId.Split('-');
        return parts.Length > 0 ? parts[0] : "GENERAL";
    }

    private AtoFindingSeverity MapRiskLevelToSeverity(Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel riskLevel)
    {
        return riskLevel switch
        {
            Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.Critical => AtoFindingSeverity.Critical,
            Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.High => AtoFindingSeverity.High,
            Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.Medium => AtoFindingSeverity.Medium,
            Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.Low => AtoFindingSeverity.Low,
            _ => AtoFindingSeverity.Informational
        };
    }

    private GapSeverity MapPriorityToGapSeverity(string? priority)
    {
        return priority?.ToLower() switch
        {
            "p0 - immediate" => GapSeverity.Critical,
            "p1 - within 24 hours" => GapSeverity.High,
            "p2 - within 7 days" => GapSeverity.Medium,
            "p3 - within 30 days" => GapSeverity.Low,
            _ => GapSeverity.Medium
        };
    }

    private double CalculateDocumentComplianceScore(DocumentAnalysis documentAnalysis)
    {
        if (documentAnalysis.SecurityAnalysis == null) return 100.0;

        var totalIssues = documentAnalysis.SecurityAnalysis.ComplianceGaps.Count +
                         documentAnalysis.SecurityAnalysis.IdentifiedRisks.Count;

        if (totalIssues == 0) return 100.0;

        // Calculate score based on risk levels in document analysis
        var criticalCount = documentAnalysis.SecurityAnalysis.ComplianceGaps.Count(g => g.RiskLevel == Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.Critical) +
                           documentAnalysis.SecurityAnalysis.IdentifiedRisks.Count(r => r.Level == Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.Critical);
        var highCount = documentAnalysis.SecurityAnalysis.ComplianceGaps.Count(g => g.RiskLevel == Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.High) +
                       documentAnalysis.SecurityAnalysis.IdentifiedRisks.Count(r => r.Level == Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.High);
        var mediumCount = documentAnalysis.SecurityAnalysis.ComplianceGaps.Count(g => g.RiskLevel == Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.Medium) +
                         documentAnalysis.SecurityAnalysis.IdentifiedRisks.Count(r => r.Level == Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.Medium);

        var weightedScore = (criticalCount * 4 + highCount * 3 + mediumCount * 2);
        var maxPossibleScore = totalIssues * 4;

        return maxPossibleScore > 0 ? Math.Max(0, 100.0 - (weightedScore * 100.0 / maxPossibleScore)) : 100.0;
    }

    private ComplianceStatus DetermineComplianceStatus(DocumentAnalysis documentAnalysis)
    {
        if (documentAnalysis.SecurityAnalysis == null) return ComplianceStatus.Compliant;

        var hasCritical = documentAnalysis.SecurityAnalysis.ComplianceGaps.Any(g => g.RiskLevel == Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.Critical) ||
                         documentAnalysis.SecurityAnalysis.IdentifiedRisks.Any(r => r.Level == Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.Critical);
        var hasHigh = documentAnalysis.SecurityAnalysis.ComplianceGaps.Any(g => g.RiskLevel == Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.High) ||
                     documentAnalysis.SecurityAnalysis.IdentifiedRisks.Any(r => r.Level == Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.High);

        if (hasCritical) return ComplianceStatus.NonCompliant;
        if (hasHigh) return ComplianceStatus.PartiallyCompliant;

        var totalIssues = documentAnalysis.SecurityAnalysis.ComplianceGaps.Count +
                         documentAnalysis.SecurityAnalysis.IdentifiedRisks.Count;

        return totalIssues > 5 ? ComplianceStatus.PartiallyCompliant : ComplianceStatus.Compliant;
    }

    private async Task<ExtractedContent> ExtractContentAsync(string filePath, string fileExtension)
    {
        var content = new ExtractedContent();

        switch (fileExtension)
        {
            case ".pdf":
                content = await ExtractPdfContentAsync(filePath);
                break;
            case ".docx":
            case ".doc":
                content = await ExtractWordContentAsync(filePath);
                break;
            case ".vsdx":
            case ".vsd":
                content = await ExtractVisioContentAsync(filePath);
                break;
            case ".pptx":
            case ".ppt":
                content = await ExtractPowerPointContentAsync(filePath);
                break;
            case ".txt":
            case ".md":
                content = await ExtractTextContentAsync(filePath);
                break;
            default:
                _logger.LogWarning("Unsupported file type for content extraction: {FileExtension}", fileExtension);
                break;
        }

        return content;
    }

    private async Task<ExtractedContent> ExtractPdfContentAsync(string filePath)
    {
        var content = new ExtractedContent();
        var sections = new List<TextSection>();

        using (var reader = new PdfReader(filePath))
        {
            for (int page = 1; page <= reader.NumberOfPages; page++)
            {
                // Simple text extraction - for production use, consider using a more robust PDF library
                var pageText = $"[Page {page} content - PDF text extraction requires advanced parsing]";
                sections.Add(new TextSection
                {
                    Title = $"Page {page}",
                    Content = pageText,
                    PageNumber = page,
                    SectionType = "Page"
                });
            }
        }

        content.Sections = sections;
        content.FullText = string.Join("\n\n", sections.Select(s => s.Content));
        
        return content;
    }

    private async Task<ExtractedContent> ExtractWordContentAsync(string filePath)
    {
        var content = new ExtractedContent();
        var sections = new List<TextSection>();

        using (var document = WordprocessingDocument.Open(filePath, false))
        {
            var body = document.MainDocumentPart?.Document.Body;
            if (body != null)
            {
                var paragraphs = body.Elements<Paragraph>();
                var sectionNumber = 1;

                foreach (var paragraph in paragraphs)
                {
                    var text = paragraph.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sections.Add(new TextSection
                        {
                            Title = $"Section {sectionNumber}",
                            Content = text,
                            PageNumber = 1,
                            SectionType = "Paragraph"
                        });
                        sectionNumber++;
                    }
                }
            }
        }

        content.Sections = sections;
        content.FullText = string.Join("\n\n", sections.Select(s => s.Content));
        
        return content;
    }

    private async Task<ExtractedContent> ExtractVisioContentAsync(string filePath)
    {
        var content = new ExtractedContent();
        
        _logger.LogInformation("Processing Visio diagram: {FilePath}", filePath);
        
        // Extract diagram information using the architecture analyzer
        var diagrams = await _diagramAnalyzer.ExtractDiagramsAsync(filePath);
        content.Diagrams = diagrams;
        
        // Generate text description of diagrams
        var descriptions = diagrams.Select(d => $"Diagram: {d.Title} - {d.Components.Count} components, {d.Connections.Count} connections");
        content.FullText = string.Join("\n", descriptions);
        
        return content;
    }

    private async Task<ExtractedContent> ExtractPowerPointContentAsync(string filePath)
    {
        var content = new ExtractedContent();
        // Placeholder for PowerPoint extraction
        content.FullText = "PowerPoint content extraction not yet implemented";
        return content;
    }

    private async Task<ExtractedContent> ExtractTextContentAsync(string filePath)
    {
        var content = new ExtractedContent();
        var text = await File.ReadAllTextAsync(filePath);
        
        content.FullText = text;
        content.Sections = new List<TextSection>
        {
            new TextSection
            {
                Title = "Full Document",
                Content = text,
                PageNumber = 1,
                SectionType = "Text"
            }
        };
        
        return content;
    }

    private async Task<DocumentMetadata> ExtractMetadataAsync(string filePath, string fileExtension)
    {
        var fileInfo = new FileInfo(filePath);
        var metadata = new DocumentMetadata
        {
            FileType = fileExtension,
            FileSize = fileInfo.Length,
            CreatedDate = fileInfo.CreationTime,
            ModifiedDate = fileInfo.LastWriteTime
        };

        // Extract additional metadata based on file type
        switch (fileExtension)
        {
            case ".docx":
                await ExtractWordMetadataAsync(filePath, metadata);
                break;
            case ".pdf":
                await ExtractPdfMetadataAsync(filePath, metadata);
                break;
        }

        return metadata;
    }

    private async Task ExtractWordMetadataAsync(string filePath, DocumentMetadata metadata)
    {
        try
        {
            using (var document = WordprocessingDocument.Open(filePath, false))
            {
                var coreProps = document.PackageProperties;
                metadata.Title = coreProps.Title ?? string.Empty;
                metadata.Author = coreProps.Creator ?? string.Empty;
                metadata.Subject = coreProps.Subject ?? string.Empty;
                metadata.Keywords = coreProps.Keywords?.Split(',').Select(k => k.Trim()).ToList() ?? new List<string>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract Word metadata from {FilePath}", filePath);
        }
    }

    private async Task ExtractPdfMetadataAsync(string filePath, DocumentMetadata metadata)
    {
        try
        {
            using (var reader = new PdfReader(filePath))
            {
                var info = reader.Info;
                metadata.Title = info.ContainsKey("Title") ? info["Title"] : string.Empty;
                metadata.Author = info.ContainsKey("Author") ? info["Author"] : string.Empty;
                metadata.Subject = info.ContainsKey("Subject") ? info["Subject"] : string.Empty;
                metadata.PageCount = reader.NumberOfPages;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract PDF metadata from {FilePath}", filePath);
        }
    }

    private async Task<(ArchitectureAnalysis?, SecurityAnalysis?)> PerformAnalysisAsync(
        ExtractedContent content, 
        DocumentMetadata metadata, 
        DocumentAnalysisType analysisType)
    {
        ArchitectureAnalysis? architectureAnalysis = null;
        SecurityAnalysis? securityAnalysis = null;

        switch (analysisType)
        {
            case DocumentAnalysisType.ArchitectureDiagram:
            case DocumentAnalysisType.NetworkDiagram:
            case DocumentAnalysisType.SystemDesign:
                architectureAnalysis = await _diagramAnalyzer.AnalyzeArchitectureAsync(content);
                break;
            
            case DocumentAnalysisType.SecurityDocument:
            case DocumentAnalysisType.ComplianceDocument:
                securityAnalysis = await AnalyzeSecurityContentAsync(content);
                break;
            
            case DocumentAnalysisType.General:
                architectureAnalysis = await _diagramAnalyzer.AnalyzeArchitectureAsync(content);
                securityAnalysis = await AnalyzeSecurityContentAsync(content);
                break;
        }

        return (architectureAnalysis, securityAnalysis);
    }

    private async Task<SecurityAnalysis> AnalyzeSecurityContentAsync(ExtractedContent content)
    {
        var securityAnalysis = new SecurityAnalysis();
        
        var securityKeywords = new[] { "security", "encryption", "authentication", "authorization", "firewall", "vpn", "ssl", "tls" };
        var complianceKeywords = new[] { "nist", "fisma", "fedramp", "ato", "control", "compliance" };
        
        foreach (var section in content.Sections)
        {
            var lowerContent = section.Content.ToLowerInvariant();
            
            foreach (var keyword in securityKeywords)
            {
                if (lowerContent.Contains(keyword))
                {
                    securityAnalysis.IdentifiedControls.Add(new SecurityControl
                    {
                        ControlId = keyword.ToUpperInvariant(),
                        Name = keyword,
                        Status = ControlImplementationStatus.PartiallyImplemented
                    });
                }
            }
            
            foreach (var keyword in complianceKeywords)
            {
                if (lowerContent.Contains(keyword))
                {
                    securityAnalysis.ComplianceGaps.Add(new Platform.Engineering.Copilot.DocumentProcessing.Models.ComplianceGap
                    {
                        Description = $"Compliance reference found: {keyword}",
                        RiskLevel = Platform.Engineering.Copilot.DocumentProcessing.Models.RiskLevel.Medium,
                        Remediation = "Verify implementation details"
                    });
                }
            }
        }
        
        return securityAnalysis;
    }

    private async Task SaveAnalysisResultsAsync(
        string documentId, 
        string fileName, 
        DocumentAnalysisType analysisType,
        DocumentMetadata metadata,
        ExtractedContent content,
        (ArchitectureAnalysis?, SecurityAnalysis?) analysis)
    {
        var documentAnalysis = new DocumentAnalysis
        {
            DocumentId = documentId,
            FileName = fileName,
            AnalysisType = analysisType,
            Metadata = metadata,
            Content = content,
            ArchitectureAnalysis = analysis.Item1,
            SecurityAnalysis = analysis.Item2,
            AnalyzedAt = DateTime.UtcNow
        };

        documentAnalysis.Recommendations = GenerateRecommendations(analysis.Item1, analysis.Item2);

        var analysisFilePath = Path.Combine(_uploadsPath, $"{documentId}_analysis.json");
        var json = JsonSerializer.Serialize(documentAnalysis, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(analysisFilePath, json);
    }

    private List<string> GenerateRecommendations(ArchitectureAnalysis? architectureAnalysis, SecurityAnalysis? securityAnalysis)
    {
        var recommendations = new List<string>();

        if (architectureAnalysis != null)
        {
            recommendations.AddRange(architectureAnalysis.Recommendations.Select(r => r.Description));
        }

        if (securityAnalysis != null)
        {
            recommendations.AddRange(securityAnalysis.Recommendations.Select(r => r.Description));
        }

        if (!recommendations.Any())
        {
            recommendations.Add("Document processed successfully. Consider running RMF analysis for compliance assessment.");
        }

        return recommendations;
    }

    private string GenerateAnalysisPreview((ArchitectureAnalysis?, SecurityAnalysis?) analysis)
    {
        var preview = "Document processing complete. ";
        
        if (analysis.Item1 != null)
        {
            preview += $"Found {analysis.Item1.SystemComponents.Count} system components and {analysis.Item1.DataFlows.Count} data flows. ";
        }
        
        if (analysis.Item2 != null)
        {
            preview += $"Identified {analysis.Item2.IdentifiedControls.Count} security controls and {analysis.Item2.ComplianceGaps.Count} compliance items.";
        }
        
        return preview;
    }

    private void UpdateProcessingStatus(string documentId, ProcessingStatus status, int progress, string currentStep, string? errorMessage = null)
    {
        if (_processingStatuses.TryGetValue(documentId, out var existingStatus))
        {
            existingStatus.Status = status;
            existingStatus.ProgressPercentage = progress;
            existingStatus.CurrentStep = currentStep;
            existingStatus.ErrorMessage = errorMessage;
            existingStatus.LastUpdated = DateTime.UtcNow;
            
            if (!existingStatus.CompletedSteps.Contains(currentStep) && status != ProcessingStatus.Error)
            {
                existingStatus.CompletedSteps.Add(currentStep);
            }
        }
    }
}