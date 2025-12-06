using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.DocumentProcessing;
using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Compliance.Agent.Plugins.Compliance;

/// <summary>
/// Semantic Kernel plugin for document upload and analysis.
/// Integrates with IDocumentProcessingService for production-ready document processing,
/// including PDF/Word/PowerPoint/Visio extraction, architecture diagram analysis,
/// and RMF compliance evaluation.
/// </summary>
public class DocumentPlugin : BaseSupervisorPlugin
{
    private readonly IDocumentProcessingService _documentProcessingService;

    public DocumentPlugin(
        IDocumentProcessingService documentProcessingService,
        ILogger<DocumentPlugin> logger,
        Kernel kernel) : base(logger, kernel)
    {
        _documentProcessingService = documentProcessingService ?? throw new ArgumentNullException(nameof(documentProcessingService));
    }


    [KernelFunction("upload_security_document")]
    [Description("Upload and analyze security documents (SSP, POA&M, architecture diagrams, security plans). Extracts information, identifies controls, and analyzes compliance requirements. Use when user wants to: upload document, analyze SSP, review security plan, or extract compliance information.")]
    public async Task<string> UploadSecurityDocumentAsync(
        [Description("File path to the document to analyze (must be accessible file path)")] string filePath,
        [Description("Document type (e.g., 'SSP', 'POAM', 'Architecture Diagram', 'Security Plan'). Optional - will be auto-detected if not specified.")] string? documentType = null,
        [Description("Analysis focus area (e.g., 'controls', 'vulnerabilities', 'architecture', 'compliance'). Optional - performs comprehensive analysis if not specified.")] string? analysisFocus = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate file path
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return CreateErrorResponse("upload security document", new ArgumentException("File path is required"));
            }

            if (!File.Exists(filePath))
            {
                return CreateErrorResponse("upload security document", new FileNotFoundException($"File not found: {filePath}"));
            }

            // Determine analysis type from documentType
            var analysisType = DetermineAnalysisType(documentType, analysisFocus);

            // Create IFormFile from file path
            var formFile = await CreateFormFileFromPathAsync(filePath);

            _logger.LogInformation("Processing document: {FilePath}, Type: {AnalysisType}", filePath, analysisType);

            // Process document using the service
            var result = await _documentProcessingService.ProcessDocumentAsync(formFile, analysisType, null);

            // Format and return result
            return FormatDocumentProcessingResult(result, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading security document from path: {FilePath}", filePath);
            return CreateErrorResponse("upload security document", ex);
        }
    }

    [KernelFunction("extract_security_controls")]
    [Description("Extract security controls from uploaded documents. Identifies NIST 800-53 controls, implementation details, and control families. Use when user wants to: find controls, extract security requirements, or map controls from documents.")]
    public async Task<string> ExtractSecurityControlsAsync(
        [Description("Document ID from previous upload")] string documentId,
        [Description("Control framework to map to (e.g., 'NIST 800-53', 'NIST 800-171', 'ISO 27001'). Optional - defaults to NIST 800-53.")] string? framework = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate document ID
            if (string.IsNullOrWhiteSpace(documentId))
            {
                return CreateErrorResponse("extract security controls", new ArgumentException("Document ID is required"));
            }

            _logger.LogInformation("Extracting security controls from document: {DocumentId}, Framework: {Framework}", documentId, framework ?? "NIST 800-53");

            // Get document analysis
            var analysis = await _documentProcessingService.GetDocumentAnalysisAsync(documentId);

            if (analysis.SecurityAnalysis == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    documentId,
                    message = "No security analysis available for this document. Document may not contain security-related content.",
                    controls = Array.Empty<object>()
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Perform RMF analysis for compliance mapping
            var complianceResult = await _documentProcessingService.PerformRmfAnalysisAsync(documentId, framework ?? "NIST-800-53");

            // Format controls with implementation status
            var controls = analysis.SecurityAnalysis.IdentifiedControls.Select(c => new
            {
                controlId = c.ControlId,
                name = c.Name,
                status = c.Status.ToString(),
                framework = framework ?? "NIST 800-53"
            }).ToList();

            var response = new
            {
                success = true,
                documentId,
                framework = framework ?? "NIST 800-53",
                totalControls = controls.Count,
                implemented = controls.Count(c => c.status == "Implemented"),
                partiallyImplemented = controls.Count(c => c.status == "PartiallyImplemented"),
                notImplemented = controls.Count(c => c.status == "NotImplemented"),
                controls,
                complianceScore = complianceResult.ComplianceScore,
                overallStatus = complianceResult.OverallStatus.ToString()
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found: {DocumentId}", documentId);
            return CreateErrorResponse("extract security controls", new Exception($"Document not found: {documentId}. Please upload the document first."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting security controls from document: {DocumentId}", documentId);
            return CreateErrorResponse("extract security controls", ex);
        }
    }

    [KernelFunction("analyze_architecture_diagram")]
    [Description("Analyze architecture diagrams to identify components, data flows, security boundaries, and compliance implications. Use when user uploads: architecture diagram, network diagram, system diagram, or wants architecture analysis.")]
    public async Task<string> AnalyzeArchitectureDiagramAsync(
        [Description("Document ID from previous upload OR file path to diagram (Visio .vsdx, image, or PDF)")] string documentIdOrPath,
        [Description("Analysis focus (e.g., 'security boundaries', 'data flow', 'compliance', 'zero trust'). Optional - performs comprehensive analysis if not specified.")] string? analysisFocus = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string documentId;
            
            // Check if it's a file path or document ID
            if (File.Exists(documentIdOrPath))
            {
                _logger.LogInformation("Processing new architecture diagram from file: {FilePath}", documentIdOrPath);
                
                // Upload new diagram
                var formFile = await CreateFormFileFromPathAsync(documentIdOrPath);
                var uploadResult = await _documentProcessingService.ProcessDocumentAsync(
                    formFile, 
                    DocumentAnalysisType.ArchitectureDiagram,
                    null);
                
                documentId = uploadResult.DocumentId;
            }
            else
            {
                // Assume it's a document ID
                documentId = documentIdOrPath;
            }

            _logger.LogInformation("Analyzing architecture diagram: {DocumentId}", documentId);

            // Get document analysis
            var analysis = await _documentProcessingService.GetDocumentAnalysisAsync(documentId);

            if (analysis.ArchitectureAnalysis == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    documentId,
                    message = "No architecture analysis available. Document may not contain diagrams or architectural information.",
                    diagrams = Array.Empty<object>()
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var arch = analysis.ArchitectureAnalysis;

            var response = new
            {
                success = true,
                documentId,
                analysisFocus = analysisFocus ?? "comprehensive",
                summary = new
                {
                    totalComponents = arch.SystemComponents.Count,
                    totalDataFlows = arch.DataFlows.Count,
                    securityBoundaries = arch.SecurityBoundaries.Count,
                    detectedPatterns = arch.DetectedPatterns.Count
                },
                components = arch.SystemComponents.Select(c => new
                {
                    name = c.Name,
                    type = c.Type,
                    description = c.Description
                }).ToList(),
                dataFlows = arch.DataFlows.Select(df => new
                {
                    source = df.Source,
                    target = df.Target,
                    dataType = df.DataType,
                    classification = df.Classification.ToString()
                }).ToList(),
                securityBoundaries = arch.SecurityBoundaries.Select(sb => new
                {
                    name = sb.Name,
                    type = sb.Type,
                    components = sb.Components
                }).ToList(),
                recommendations = arch.Recommendations.Select(r => new
                {
                    title = r.Title,
                    description = r.Description,
                    priority = r.Priority.ToString()
                }).ToList()
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found: {DocumentId}", documentIdOrPath);
            return CreateErrorResponse("analyze architecture diagram", new Exception($"Document not found: {documentIdOrPath}. Please provide a valid file path or document ID."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing architecture diagram: {DocumentIdOrPath}", documentIdOrPath);
            return CreateErrorResponse("analyze architecture diagram", ex);
        }
    }

    [KernelFunction("compare_documents")]
    [Description("Compare two security documents to identify differences, gaps, and changes. Shows added/removed controls, modified requirements, and compliance delta. Use when user wants to: compare versions, find differences, check updates, or analyze changes.")]
    public async Task<string> CompareDocumentsAsync(
        [Description("First document ID (baseline)")] string document1Id,
        [Description("Second document ID (comparison)")] string document2Id,
        [Description("Comparison focus (e.g., 'controls', 'requirements', 'compliance', 'all'). Optional - compares all aspects if not specified.")] string? comparisonFocus = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(document1Id) || string.IsNullOrWhiteSpace(document2Id))
            {
                return CreateErrorResponse("compare documents", new ArgumentException("Both document IDs are required"));
            }

            _logger.LogInformation("Comparing documents: {Doc1} vs {Doc2}", document1Id, document2Id);

            // Get both document analyses
            var analysis1 = await _documentProcessingService.GetDocumentAnalysisAsync(document1Id);
            var analysis2 = await _documentProcessingService.GetDocumentAnalysisAsync(document2Id);

            // Compare security controls
            var controlsComparison = CompareSecurityControls(analysis1, analysis2);
            
            // Compare compliance gaps
            var gapsComparison = CompareComplianceGaps(analysis1, analysis2);
            
            // Compare architecture (if both have architecture analysis)
            var architectureComparison = CompareArchitecture(analysis1, analysis2);

            // Calculate total changes from comparison results
            int totalChanges = 0;
            if (controlsComparison != null)
            {
                var cc = JsonSerializer.Deserialize<Dictionary<string, object>>(JsonSerializer.Serialize(controlsComparison));
                if (cc != null)
                {
                    totalChanges += Convert.ToInt32(cc.GetValueOrDefault("added", 0));
                    totalChanges += Convert.ToInt32(cc.GetValueOrDefault("removed", 0));
                    totalChanges += Convert.ToInt32(cc.GetValueOrDefault("modified", 0));
                }
            }

            var response = new
            {
                success = true,
                comparison = new
                {
                    baseline = new { documentId = document1Id, fileName = analysis1.FileName },
                    target = new { documentId = document2Id, fileName = analysis2.FileName },
                    comparisonFocus = comparisonFocus ?? "all",
                    analyzedAt = DateTime.UtcNow
                },
                controls = controlsComparison,
                complianceGaps = gapsComparison,
                architecture = architectureComparison,
                summary = new
                {
                    totalChanges,
                    controlChanges = controlsComparison != null,
                    complianceChanges = gapsComparison != null,
                    architectureChanges = architectureComparison != null
                }
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found during comparison");
            return CreateErrorResponse("compare documents", new Exception($"One or both documents not found. Please verify document IDs: {document1Id}, {document2Id}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comparing documents: {Doc1} vs {Doc2}", document1Id, document2Id);
            return CreateErrorResponse("compare documents", ex);
        }
    }

    [KernelFunction("generate_compliance_mapping")]
    [Description("Generate compliance mapping from document to specific framework. Maps document content to NIST, FedRAMP, FISMA, or other compliance requirements. Use when user wants to: map to framework, check compliance coverage, or generate gap analysis.")]
    public async Task<string> GenerateComplianceMappingAsync(
        [Description("Document ID to map")] string documentId,
        [Description("Target compliance framework (e.g., 'FedRAMP High', 'NIST 800-53 Rev 5', 'FISMA', 'NIST-800-53')")] string targetFramework,
        [Description("Show gaps only (true) or full mapping (false). Optional - defaults to full mapping.")] bool? gapsOnly = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(documentId))
            {
                return CreateErrorResponse("generate compliance mapping", new ArgumentException("Document ID is required"));
            }

            if (string.IsNullOrWhiteSpace(targetFramework))
            {
                return CreateErrorResponse("generate compliance mapping", new ArgumentException("Target framework is required"));
            }

            _logger.LogInformation("Generating compliance mapping for document: {DocumentId}, Framework: {Framework}", documentId, targetFramework);

            // Perform RMF analysis
            var complianceResult = await _documentProcessingService.PerformRmfAnalysisAsync(documentId, targetFramework);

            // Filter based on gapsOnly flag
            var mappings = gapsOnly == true
                ? complianceResult.ControlAssessments.Where(ca => ca.Status != ControlComplianceStatus.FullyImplemented).ToList()
                : complianceResult.ControlAssessments.ToList();

            var response = new
            {
                success = true,
                documentId,
                framework = targetFramework,
                gapsOnly = gapsOnly ?? false,
                analyzedAt = complianceResult.AnalyzedAt,
                complianceScore = complianceResult.ComplianceScore,
                overallStatus = complianceResult.OverallStatus.ToString(),
                summary = new
                {
                    totalControls = mappings.Count,
                    fullyImplemented = mappings.Count(m => m.Status == ControlComplianceStatus.FullyImplemented),
                    partiallyImplemented = mappings.Count(m => m.Status == ControlComplianceStatus.PartiallyImplemented),
                    notImplemented = mappings.Count(m => m.Status == ControlComplianceStatus.NotImplemented),
                    notApplicable = mappings.Count(m => m.Status == ControlComplianceStatus.NotApplicable)
                },
                controlMappings = mappings.Select(ca => new
                {
                    controlId = ca.ControlId,
                    controlTitle = ca.ControlTitle,
                    controlFamily = ca.ControlFamily,
                    status = ca.Status.ToString(),
                    implementationScore = ca.ImplementationScore,
                    assessment = ca.Assessment,
                    evidence = ca.Evidence,
                    assessedAt = ca.AssessedAt
                }).ToList(),
                gaps = complianceResult.Gaps.Select(g => new
                {
                    controlId = g.ControlId,
                    title = g.Title,
                    description = g.Description,
                    severity = g.Severity.ToString(),
                    impactAssessment = g.ImpactAssessment,
                    recommendedAction = g.RecommendedAction,
                    affectedSystems = g.AffectedSystems
                }).ToList(),
                recommendations = complianceResult.Recommendations.Select(r => new
                {
                    title = r.Title,
                    description = r.Description,
                    priority = r.Priority.ToString(),
                    category = r.Category
                }).ToList()
            };

            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Document not found: {DocumentId}", documentId);
            return CreateErrorResponse("generate compliance mapping", new Exception($"Document not found: {documentId}. Please upload the document first."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliance mapping for document: {DocumentId}", documentId);
            return CreateErrorResponse("generate compliance mapping", ex);
        }
    }

    #region Helper Methods

    /// <summary>
    /// Determine analysis type from document type and focus
    /// </summary>
    private DocumentAnalysisType DetermineAnalysisType(string? documentType, string? analysisFocus)
    {
        var lowerType = documentType?.ToLowerInvariant() ?? "";
        var lowerFocus = analysisFocus?.ToLowerInvariant() ?? "";

        if (lowerType.Contains("architecture") || lowerType.Contains("diagram") || lowerFocus.Contains("architecture"))
            return DocumentAnalysisType.ArchitectureDiagram;

        if (lowerType.Contains("network") || lowerFocus.Contains("network"))
            return DocumentAnalysisType.NetworkDiagram;

        if (lowerType.Contains("system") || lowerFocus.Contains("system"))
            return DocumentAnalysisType.SystemDesign;

        if (lowerType.Contains("security") || lowerType.Contains("ssp") || lowerFocus.Contains("security"))
            return DocumentAnalysisType.SecurityDocument;

        if (lowerType.Contains("compliance") || lowerType.Contains("poam") || lowerFocus.Contains("compliance"))
            return DocumentAnalysisType.ComplianceDocument;

        return DocumentAnalysisType.General;
    }

    /// <summary>
    /// Create IFormFile from file path
    /// </summary>
    private async Task<IFormFile> CreateFormFileFromPathAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        var fileStream = fileInfo.OpenRead();
        var fileName = fileInfo.Name;
        var contentType = GetContentType(fileInfo.Extension);

        return new Microsoft.AspNetCore.Http.Internal.FormFile(fileStream, 0, fileStream.Length, fileName, fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    /// <summary>
    /// Get content type from file extension
    /// </summary>
    private string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".doc" => "application/msword",
            ".vsdx" => "application/vnd.visio",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".txt" => "text/plain",
            ".md" => "text/markdown",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Format document processing result
    /// </summary>
    private string FormatDocumentProcessingResult(DocumentProcessingResult result, string originalPath)
    {
        var response = new
        {
            success = true,
            documentId = result.DocumentId,
            originalPath,
            status = result.ProcessingStatus.ToString(),
            preview = result.AnalysisPreview,
            estimatedTime = result.EstimatedProcessingTime.ToString(),
            metadata = new
            {
                fileType = result.Metadata.FileType,
                fileSize = result.Metadata.FileSize,
                title = result.Metadata.Title,
                author = result.Metadata.Author,
                pageCount = result.Metadata.PageCount
            },
            message = "Document uploaded and processing started. Use the document ID for further analysis."
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Compare security controls between two documents
    /// </summary>
    private object? CompareSecurityControls(DocumentAnalysis doc1, DocumentAnalysis doc2)
    {
        if (doc1.SecurityAnalysis == null || doc2.SecurityAnalysis == null)
            return null;

        var controls1 = doc1.SecurityAnalysis.IdentifiedControls.Select(c => c.ControlId).ToHashSet();
        var controls2 = doc2.SecurityAnalysis.IdentifiedControls.Select(c => c.ControlId).ToHashSet();

        var added = controls2.Except(controls1).ToList();
        var removed = controls1.Except(controls2).ToList();
        var common = controls1.Intersect(controls2).ToList();

        var modified = common.Where(controlId =>
        {
            var c1 = doc1.SecurityAnalysis.IdentifiedControls.First(c => c.ControlId == controlId);
            var c2 = doc2.SecurityAnalysis.IdentifiedControls.First(c => c.ControlId == controlId);
            return c1.Status != c2.Status;
        }).ToList();

        return new
        {
            added = added.Count,
            removed = removed.Count,
            modified = modified.Count,
            unchanged = common.Count - modified.Count,
            details = new
            {
                addedControls = added,
                removedControls = removed,
                modifiedControls = modified
            }
        };
    }

    /// <summary>
    /// Compare compliance gaps between two documents
    /// </summary>
    private object? CompareComplianceGaps(DocumentAnalysis doc1, DocumentAnalysis doc2)
    {
        if (doc1.SecurityAnalysis == null || doc2.SecurityAnalysis == null)
            return null;

        var gaps1Count = doc1.SecurityAnalysis.ComplianceGaps.Count;
        var gaps2Count = doc2.SecurityAnalysis.ComplianceGaps.Count;

        return new
        {
            baseline = gaps1Count,
            target = gaps2Count,
            delta = gaps2Count - gaps1Count,
            improvement = gaps1Count > gaps2Count,
            percentageChange = gaps1Count > 0 ? ((gaps2Count - gaps1Count) / (double)gaps1Count * 100) : 0
        };
    }

    /// <summary>
    /// Compare architecture between two documents
    /// </summary>
    private object? CompareArchitecture(DocumentAnalysis doc1, DocumentAnalysis doc2)
    {
        if (doc1.ArchitectureAnalysis == null || doc2.ArchitectureAnalysis == null)
            return null;

        var arch1 = doc1.ArchitectureAnalysis;
        var arch2 = doc2.ArchitectureAnalysis;

        return new
        {
            components = new
            {
                baseline = arch1.SystemComponents.Count,
                target = arch2.SystemComponents.Count,
                delta = arch2.SystemComponents.Count - arch1.SystemComponents.Count
            },
            dataFlows = new
            {
                baseline = arch1.DataFlows.Count,
                target = arch2.DataFlows.Count,
                delta = arch2.DataFlows.Count - arch1.DataFlows.Count
            },
            securityBoundaries = new
            {
                baseline = arch1.SecurityBoundaries.Count,
                target = arch2.SecurityBoundaries.Count,
                delta = arch2.SecurityBoundaries.Count - arch1.SecurityBoundaries.Count
            }
        };
    }

    #endregion
}
