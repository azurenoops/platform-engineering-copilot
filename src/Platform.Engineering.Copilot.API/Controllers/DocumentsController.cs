using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
using Platform.Engineering.Copilot.DocumentProcessing.Services;
using Platform.Engineering.Copilot.DocumentProcessing.Models;

namespace Platform.Engineering.Copilot.API.Controllers;

/// <summary>
/// REST API controller for document processing and analysis operations.
/// Provides endpoints for document upload, processing status tracking, analysis retrieval,
/// and RMF compliance assessment. Supports multiple document formats including PDF, Word,
/// PowerPoint, Visio, Excel, and image files for architecture diagram analysis.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly ILogger<DocumentsController> _logger;
    private readonly IConfiguration _configuration;

    // Supported file types for document analysis
    private static readonly string[] SupportedFileTypes = {
        ".pdf", ".docx", ".doc", ".vsdx", ".vsd", ".pptx", ".ppt", 
        ".xlsx", ".xls", ".txt", ".md", ".png", ".jpg", ".jpeg"
    };

    private const long MaxFileSize = 50 * 1024 * 1024; // 50MB

    /// <summary>
    /// Initializes a new instance of the DocumentsController.
    /// </summary>
    /// <param name="documentProcessingService">Service for document processing operations</param>
    /// <param name="logger">Logger for API operation diagnostics</param>
    /// <param name="configuration">Application configuration for processing parameters</param>
    public DocumentsController(
        IDocumentProcessingService documentProcessingService,
        ILogger<DocumentsController> logger,
        IConfiguration configuration)
    {
        _documentProcessingService = documentProcessingService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Uploads a document for processing and analysis.
    /// </summary>
    /// <param name="request">Document upload request containing file and analysis parameters</param>
    /// <returns>Document upload response with processing details</returns>
    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocument([FromForm] DocumentUploadRequest request)
    {
        try
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("No file provided");

            if (request.File.Length > MaxFileSize)
                return BadRequest($"File size exceeds maximum limit of {MaxFileSize / (1024 * 1024)}MB");

            var fileExtension = Path.GetExtension(request.File.FileName).ToLowerInvariant();
            if (!SupportedFileTypes.Contains(fileExtension))
                return BadRequest($"Unsupported file type: {fileExtension}. Supported types: {string.Join(", ", SupportedFileTypes)}");

            _logger.LogInformation("Processing document upload: {FileName} ({FileSize} bytes)", 
                request.File.FileName, request.File.Length);

            // Process the uploaded document
            var result = await _documentProcessingService.ProcessDocumentAsync(
                request.File, 
                request.AnalysisType, 
                request.ConversationId);

            return Ok(new DocumentUploadResponse
            {
                DocumentId = result.DocumentId,
                FileName = request.File.FileName,
                FileSize = request.File.Length,
                ProcessingStatus = result.ProcessingStatus,
                AnalysisPreview = result.AnalysisPreview,
                EstimatedProcessingTime = result.EstimatedProcessingTime
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document upload");
            return StatusCode(500, "Internal server error processing document");
        }
    }

    /// <summary>
    /// Retrieves the current processing status for a document.
    /// </summary>
    /// <param name="documentId">Unique identifier of the document</param>
    /// <returns>Current processing status and progress information</returns>
    [HttpGet("{documentId}/status")]
    public async Task<IActionResult> GetProcessingStatus(string documentId)
    {
        try
        {
            var status = await _documentProcessingService.GetProcessingStatusAsync(documentId);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document processing status for {DocumentId}", documentId);
            return NotFound("Document not found");
        }
    }

    /// <summary>
    /// Retrieves the complete analysis results for a processed document.
    /// </summary>
    /// <param name="documentId">Unique identifier of the document</param>
    /// <returns>Comprehensive document analysis including extracted content and insights</returns>
    [HttpGet("{documentId}/analysis")]
    public async Task<IActionResult> GetDocumentAnalysis(string documentId)
    {
        try
        {
            var analysis = await _documentProcessingService.GetDocumentAnalysisAsync(documentId);
            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving document analysis for {DocumentId}", documentId);
            return NotFound("Document analysis not found");
        }
    }

    /// <summary>
    /// Performs RMF (Risk Management Framework) compliance analysis on a processed document.
    /// </summary>
    /// <param name="documentId">Unique identifier of the document</param>
    /// <param name="request">RMF analysis parameters including framework and compliance level</param>
    /// <returns>RMF compliance analysis results with control assessments and recommendations</returns>
    [HttpPost("{documentId}/rmf-analysis")]
    public async Task<IActionResult> PerformRmfAnalysis(string documentId, [FromBody] RmfAnalysisRequest request)
    {
        try
        {
            var rmfAnalysis = await _documentProcessingService.PerformRmfAnalysisAsync(
                documentId, 
                request.Framework.ToString());

            return Ok(rmfAnalysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing RMF analysis for document {DocumentId}", documentId);
            return StatusCode(500, "Error performing RMF analysis");
        }
    }
}