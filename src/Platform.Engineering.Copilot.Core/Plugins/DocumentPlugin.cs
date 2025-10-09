using Microsoft.SemanticKernel;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Contracts;
using Platform.Engineering.Copilot.Core.Models;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for document upload and analysis
/// </summary>
public class DocumentPlugin : BaseSupervisorPlugin
{
    private readonly IMcpToolHandler _documentToolHandler;

    public DocumentPlugin(
        IMcpToolHandler documentToolHandler,
        ILogger<DocumentPlugin> logger,
        Kernel kernel) : base(logger, kernel)
    {
        _documentToolHandler = documentToolHandler ?? throw new ArgumentNullException(nameof(documentToolHandler));
    }

    [KernelFunction("upload_security_document")]
    [Description("Upload and analyze security documents (SSP, POA&M, architecture diagrams, security plans). Extracts information, identifies controls, and analyzes compliance requirements. Use when user wants to: upload document, analyze SSP, review security plan, or extract compliance information.")]
    public async Task<string> UploadSecurityDocumentAsync(
        [Description("Document content or file path to analyze")] string documentContent,
        [Description("Document type (e.g., 'SSP', 'POAM', 'Architecture Diagram', 'Security Plan'). Optional - will be auto-detected if not specified.")] string? documentType = null,
        [Description("Analysis focus area (e.g., 'controls', 'vulnerabilities', 'architecture', 'compliance'). Optional - performs comprehensive analysis if not specified.")] string? analysisFocus = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "document_upload_analyze",
                Arguments = new Dictionary<string, object?>
                {
                    ["document_content"] = documentContent,
                    ["document_type"] = documentType,
                    ["analysis_focus"] = analysisFocus
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _documentToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("upload security document", ex);
        }
    }

    [KernelFunction("extract_security_controls")]
    [Description("Extract security controls from uploaded documents. Identifies NIST 800-53 controls, implementation details, and control families. Use when user wants to: find controls, extract security requirements, or map controls from documents.")]
    public async Task<string> ExtractSecurityControlsAsync(
        [Description("Document ID or content to extract controls from")] string documentSource,
        [Description("Control framework to map to (e.g., 'NIST 800-53', 'NIST 800-171', 'ISO 27001'). Optional - defaults to NIST 800-53.")] string? framework = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "document_upload_analyze",
                Arguments = new Dictionary<string, object?>
                {
                    ["document_source"] = documentSource,
                    ["framework"] = framework,
                    ["operation"] = "extract_controls"
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _documentToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("extract security controls", ex);
        }
    }

    [KernelFunction("analyze_architecture_diagram")]
    [Description("Analyze architecture diagrams to identify components, data flows, security boundaries, and compliance implications. Use when user uploads: architecture diagram, network diagram, system diagram, or wants architecture analysis.")]
    public async Task<string> AnalyzeArchitectureDiagramAsync(
        [Description("Diagram content, image path, or diagram description")] string diagramContent,
        [Description("Analysis focus (e.g., 'security boundaries', 'data flow', 'compliance', 'zero trust'). Optional - performs comprehensive analysis if not specified.")] string? analysisFocus = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "document_upload_analyze",
                Arguments = new Dictionary<string, object?>
                {
                    ["document_content"] = diagramContent,
                    ["document_type"] = "Architecture Diagram",
                    ["analysis_focus"] = analysisFocus
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _documentToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("analyze architecture diagram", ex);
        }
    }

    [KernelFunction("compare_documents")]
    [Description("Compare two security documents to identify differences, gaps, and changes. Shows added/removed controls, modified requirements, and compliance delta. Use when user wants to: compare versions, find differences, check updates, or analyze changes.")]
    public async Task<string> CompareDocumentsAsync(
        [Description("First document ID or content (baseline)")] string document1,
        [Description("Second document ID or content (comparison)")] string document2,
        [Description("Comparison focus (e.g., 'controls', 'requirements', 'compliance', 'all'). Optional - compares all aspects if not specified.")] string? comparisonFocus = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "document_upload_analyze",
                Arguments = new Dictionary<string, object?>
                {
                    ["document_1"] = document1,
                    ["document_2"] = document2,
                    ["comparison_focus"] = comparisonFocus,
                    ["operation"] = "compare"
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _documentToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("compare documents", ex);
        }
    }

    [KernelFunction("generate_compliance_mapping")]
    [Description("Generate compliance mapping from document to specific framework. Maps document content to NIST, FedRAMP, FISMA, or other compliance requirements. Use when user wants to: map to framework, check compliance coverage, or generate gap analysis.")]
    public async Task<string> GenerateComplianceMappingAsync(
        [Description("Document ID or content to map")] string documentSource,
        [Description("Target compliance framework (e.g., 'FedRAMP High', 'NIST 800-53 Rev 5', 'FISMA')")] string targetFramework,
        [Description("Show gaps only (true) or full mapping (false). Optional - defaults to full mapping.")] bool? gapsOnly = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var toolCall = new McpToolCall
            {
                Name = "document_upload_analyze",
                Arguments = new Dictionary<string, object?>
                {
                    ["document_source"] = documentSource,
                    ["target_framework"] = targetFramework,
                    ["gaps_only"] = gapsOnly,
                    ["operation"] = "compliance_mapping"
                },
                RequestId = Guid.NewGuid().ToString()
            };

            var result = await _documentToolHandler.ExecuteToolAsync(toolCall, cancellationToken);
            return FormatToolResult(result);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse("generate compliance mapping", ex);
        }
    }
}
