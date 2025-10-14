using Microsoft.AspNetCore.Http;
using Platform.Engineering.Copilot.DocumentProcessing.Models;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.DocumentProcessing.Services;

/// <summary>
/// Service interface for document processing and compliance analysis operations.
/// Provides document upload, content extraction, and RMF compliance assessment capabilities
/// integrated with the unified compliance service from the Governance project.
/// </summary>
public interface IDocumentProcessingService
{
    /// <summary>
    /// Processes an uploaded document through the complete analysis pipeline.
    /// </summary>
    /// <param name="file">Document file to process</param>
    /// <param name="analysisType">Type of analysis to perform</param>
    /// <param name="conversationId">Optional conversation tracking ID</param>
    /// <returns>Processing result with document ID and status</returns>
    Task<DocumentProcessingResult> ProcessDocumentAsync(IFormFile file, DocumentAnalysisType analysisType, string? conversationId = null);
    
    /// <summary>
    /// Retrieves the current processing status for a document.
    /// </summary>
    /// <param name="documentId">Unique document identifier</param>
    /// <returns>Current processing status and progress information</returns>
    Task<DocumentProcessingStatus> GetProcessingStatusAsync(string documentId);
    
    /// <summary>
    /// Gets the complete analysis results for a processed document.
    /// </summary>
    /// <param name="documentId">Unique document identifier</param>
    /// <returns>Detailed document analysis including content and architecture information</returns>
    Task<DocumentAnalysis> GetDocumentAnalysisAsync(string documentId);
    
    /// <summary>
    /// Performs RMF (Risk Management Framework) compliance analysis on a processed document
    /// using the IAtoComplianceEngine and IAtoRemediationEngine from Core.
    /// Converts document security findings into compliance assessments and generates remediation recommendations.
    /// </summary>
    /// <param name="documentId">Unique document identifier</param>
    /// <param name="frameworkType">Compliance framework to use (default: NIST-800-53)</param>
    /// <returns>Compliance analysis results with findings and recommendations</returns>
    Task<ComplianceAnalysisResult> PerformRmfAnalysisAsync(string documentId, string frameworkType = "NIST-800-53");
}

/// <summary>
/// Interface for analyzing architecture diagrams from various document formats.
/// Extracts visual representations and converts them into structured architecture data.
/// </summary>
public interface IArchitectureDiagramAnalyzer
{
    /// <summary>
    /// Extracts architecture diagrams from document files.
    /// </summary>
    /// <param name="filePath">Path to the document containing diagrams</param>
    /// <returns>List of extracted diagrams with metadata</returns>
    Task<List<ExtractedDiagram>> ExtractDiagramsAsync(string filePath);
    
    /// <summary>
    /// Analyzes extracted content to identify architecture patterns and components.
    /// </summary>
    /// <param name="content">Extracted document content including diagrams</param>
    /// <returns>Structured architecture analysis results</returns>
    Task<ArchitectureAnalysis> AnalyzeArchitectureAsync(ExtractedContent content);
}

/// <summary>
/// Interface for Navy Flankspeed platform compatibility analysis.
/// Evaluates system architecture for compatibility with Navy's Microsoft-based collaboration platform.
/// Note: RMF compliance analysis is handled through IAtoComplianceEngine and IAtoRemediationEngine from Core.
/// </summary>
public interface INavyFlankspeedAnalyzer
{
    /// <summary>
    /// Analyzes document for compatibility with Navy Flankspeed platform.
    /// </summary>
    /// <param name="documentAnalysis">Document analysis containing system architecture</param>
    /// <returns>Compliance analysis result with Flankspeed-specific findings</returns>
    Task<ComplianceAnalysisResult> AnalyzeFlankspeedCompatibilityAsync(DocumentAnalysis documentAnalysis);
    
    /// <summary>
    /// Identifies Flankspeed-compatible architecture patterns in the system.
    /// </summary>
    /// <param name="architectureAnalysis">Architecture analysis with system components</param>
    /// <returns>List of identified Flankspeed patterns</returns>
    Task<List<string>> IdentifyFlankspeedPatternsAsync(ArchitectureAnalysis architectureAnalysis);
    
    /// <summary>
    /// Assesses effort required to integrate with Flankspeed platform.
    /// </summary>
    /// <param name="documentAnalysis">Document analysis with system details</param>
    /// <returns>Integration assessment with effort estimates and compatibility issues</returns>
    Task<Dictionary<string, object>> AssessIntegrationEffortAsync(DocumentAnalysis documentAnalysis);
}