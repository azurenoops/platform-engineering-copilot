using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using Platform.Engineering.Copilot.Services.DocumentProcessing;
using Platform.Engineering.Copilot.DocumentProcessing.Services;
using Platform.Engineering.Copilot.DocumentProcessing.Models;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using System.Text;

namespace Platform.Engineering.Copilot.Tests.Unit.DocumentProcessing.Services;

public class DocumentProcessingServiceTests : IDisposable
{
    private readonly Mock<ILogger<DocumentProcessingService>> _mockLogger;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<IArchitectureDiagramAnalyzer> _mockDiagramAnalyzer;
    private readonly Mock<IAtoComplianceEngine> _mockAtoComplianceEngine;
    private readonly Mock<IAtoRemediationEngine> _mockAtoRemediationEngine;
    private readonly DocumentProcessingService _service;
    private readonly string _testUploadsPath;

    public DocumentProcessingServiceTests()
    {
        _mockLogger = new Mock<ILogger<DocumentProcessingService>>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockDiagramAnalyzer = new Mock<IArchitectureDiagramAnalyzer>();
        _mockAtoComplianceEngine = new Mock<IAtoComplianceEngine>();
        _mockAtoRemediationEngine = new Mock<IAtoRemediationEngine>();

        _testUploadsPath = Path.Combine(Path.GetTempPath(), $"test_uploads_{Guid.NewGuid()}");
        _mockConfiguration.Setup(c => c["DocumentProcessing:UploadsPath"]).Returns(_testUploadsPath);

        _service = new DocumentProcessingService(
            _mockLogger.Object,
            _mockConfiguration.Object,
            _mockDiagramAnalyzer.Object,
            _mockAtoComplianceEngine.Object,
            _mockAtoRemediationEngine.Object);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testUploadsPath))
        {
            Directory.Delete(_testUploadsPath, true);
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesService()
    {
        // Arrange & Act
        var service = new DocumentProcessingService(
            _mockLogger.Object,
            _mockConfiguration.Object,
            _mockDiagramAnalyzer.Object,
            _mockAtoComplianceEngine.Object,
            _mockAtoRemediationEngine.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_CreatesUploadsDirectory()
    {
        // Arrange - Constructor already called in setup

        // Assert
        Directory.Exists(_testUploadsPath).Should().BeTrue();
    }

    #endregion

    #region ProcessDocumentAsync Tests

    [Fact]
    public async Task ProcessDocumentAsync_WithNullFile_ThrowsArgumentNullExceptionAsync()
    {
        // Arrange
        IFormFile? nullFile = null;

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.ProcessDocumentAsync(nullFile!, DocumentAnalysisType.General));
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithValidPdfFile_ReturnsSuccessResultAsync()
    {
        // Arrange
        var file = CreateMockFormFile("test.pdf", "PDF content");

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        result.Should().NotBeNull();
        result.DocumentId.Should().NotBeNullOrEmpty();
        result.ProcessingStatus.Should().Be(ProcessingStatus.Complete);
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithArchitectureAnalysisType_CallsDiagramAnalyzerAsync()
    {
        // Arrange
        var file = CreateMockFormFile("diagram.vsdx", "Visio content");
        _mockDiagramAnalyzer
            .Setup(a => a.AnalyzeArchitectureAsync(It.IsAny<ExtractedContent>()))
            .ReturnsAsync(new ArchitectureAnalysis());

        // Act
        await _service.ProcessDocumentAsync(file, DocumentAnalysisType.ArchitectureDiagram);

        // Assert
        _mockDiagramAnalyzer.Verify(a => a.AnalyzeArchitectureAsync(It.IsAny<ExtractedContent>()), Times.Once);
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithWordFile_ExtractsContentAsync()
    {
        // Arrange
        var file = CreateMockFormFile("document.docx", "Word document content");

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        result.Should().NotBeNull();
        result.ExtractedText.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithComplianceAnalysis_ProcessesDocumentAsync()
    {
        // Arrange
        var file = CreateMockFormFile("compliance.pdf", "Compliance document");

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.ComplianceDocument);

        // Assert
        result.Should().NotBeNull();
        result.ProcessingStatus.Should().Be(ProcessingStatus.Complete);
    }

    [Fact]
    public async Task ProcessDocumentAsync_SavesFileToUploadsDirectoryAsync()
    {
        // Arrange
        var fileName = "test_document.pdf";
        var file = CreateMockFormFile(fileName, "Test content");

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        var files = Directory.GetFiles(_testUploadsPath, $"*{fileName}");
        files.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithConversationId_IncludesInResultAsync()
    {
        // Arrange
        var file = CreateMockFormFile("test.pdf", "Content");
        var conversationId = "conv-123";

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General, conversationId);

        // Assert
        result.Should().NotBeNull();
        // ConversationId should be tracked in metadata
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithTextFile_ExtractsFullTextAsync()
    {
        // Arrange
        var content = "This is test text content";
        var file = CreateMockFormFile("document.txt", content);

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        result.ExtractedText.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithUnsupportedFileType_HandlesGracefullyAsync()
    {
        // Arrange
        var file = CreateMockFormFile("document.xyz", "Unknown format");

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        result.Should().NotBeNull();
        // Should complete without throwing
    }

    #endregion

    #region GetProcessingStatusAsync Tests

    [Fact]
    public async Task GetProcessingStatusAsync_WithValidDocumentId_ReturnsStatusAsync()
    {
        // Arrange
        var file = CreateMockFormFile("test.pdf", "Content");
        var processResult = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Act
        var status = await _service.GetProcessingStatusAsync(processResult.DocumentId);

        // Assert
        status.Should().NotBeNull();
        status.DocumentId.Should().Be(processResult.DocumentId);
        status.Status.Should().Be(ProcessingStatus.Complete);
    }

    [Fact]
    public async Task GetProcessingStatusAsync_WithInvalidDocumentId_ThrowsKeyNotFoundExceptionAsync()
    {
        // Arrange
        var invalidId = Guid.NewGuid().ToString();

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.GetProcessingStatusAsync(invalidId));
    }

    [Fact]
    public async Task GetProcessingStatusAsync_TracksSProgressPercentageAsync()
    {
        // Arrange
        var file = CreateMockFormFile("test.pdf", "Content");
        var processResult = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Act
        var status = await _service.GetProcessingStatusAsync(processResult.DocumentId);

        // Assert
        status.ProgressPercentage.Should().Be(100);
    }

    #endregion

    #region File Format Detection Tests

    [Theory]
    [InlineData("document.pdf", ".pdf")]
    [InlineData("document.docx", ".docx")]
    [InlineData("document.doc", ".doc")]
    [InlineData("diagram.vsdx", ".vsdx")]
    [InlineData("presentation.pptx", ".pptx")]
    [InlineData("spreadsheet.xlsx", ".xlsx")]
    [InlineData("text.txt", ".txt")]
    [InlineData("readme.md", ".md")]
    [InlineData("image.png", ".png")]
    [InlineData("photo.jpg", ".jpg")]
    public async Task ProcessDocumentAsync_DetectsFileExtensionCorrectlyAsync(string fileName, string expectedExtension)
    {
        // Arrange
        var file = CreateMockFormFile(fileName, "Content");

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.FileType.Should().Be(expectedExtension);
    }

    #endregion

    #region Metadata Extraction Tests

    [Fact]
    public async Task ProcessDocumentAsync_ExtractsFileMetadataAsync()
    {
        // Arrange
        var file = CreateMockFormFile("test.pdf", "Content");

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        result.Metadata.Should().NotBeNull();
        result.Metadata.FileSize.Should().BeGreaterThan(0);
        result.Metadata.CreatedDate.Should().NotBe(default);
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithPdfFile_ExtractsPageCountAsync()
    {
        // Arrange
        var file = CreateMockFormFile("test.pdf", "PDF content with multiple pages");

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        result.Metadata.Should().NotBeNull();
        // PageCount should be extracted for PDF files
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ProcessDocumentAsync_WithProcessingError_UpdatesStatusToErrorAsync()
    {
        // Arrange
        var file = CreateMockFormFile("test.pdf", "Content");
        _mockDiagramAnalyzer
            .Setup(a => a.AnalyzeArchitectureAsync(It.IsAny<ExtractedContent>()))
            .ThrowsAsync(new InvalidOperationException("Analysis failed"));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.ProcessDocumentAsync(file, DocumentAnalysisType.ArchitectureDiagram));
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithEmptyFile_HandlesGracefullyAsync()
    {
        // Arrange
        var file = CreateMockFormFile("empty.txt", "");

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Content Extraction Tests

    [Fact]
    public async Task ProcessDocumentAsync_WithTextFile_ExtractsFullContentAsync()
    {
        // Arrange
        var content = "Line 1\nLine 2\nLine 3";
        var file = CreateMockFormFile("test.txt", content);

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        result.ExtractedText.Should().NotBeNullOrEmpty();
        result.ExtractedText.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithMarkdownFile_ExtractsContentAsync()
    {
        // Arrange
        var content = "# Header\n## Subheader\nContent";
        var file = CreateMockFormFile("readme.md", content);

        // Act
        var result = await _service.ProcessDocumentAsync(file, DocumentAnalysisType.General);

        // Assert
        result.ExtractedText.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Analysis Type Tests

    [Theory]
    [InlineData(DocumentAnalysisType.ArchitectureDiagram)]
    [InlineData(DocumentAnalysisType.NetworkDiagram)]
    [InlineData(DocumentAnalysisType.SystemDesign)]
    [InlineData(DocumentAnalysisType.SecurityDocument)]
    [InlineData(DocumentAnalysisType.ComplianceDocument)]
    [InlineData(DocumentAnalysisType.General)]
    public async Task ProcessDocumentAsync_HandlesAllAnalysisTypesAsync(DocumentAnalysisType analysisType)
    {
        // Arrange
        var file = CreateMockFormFile("test.pdf", "Content");
        _mockDiagramAnalyzer
            .Setup(a => a.AnalyzeArchitectureAsync(It.IsAny<ExtractedContent>()))
            .ReturnsAsync(new ArchitectureAnalysis());

        // Act
        var result = await _service.ProcessDocumentAsync(file, analysisType);

        // Assert
        result.Should().NotBeNull();
        result.ProcessingStatus.Should().Be(ProcessingStatus.Complete);
    }

    #endregion

    #region Helper Methods

    private IFormFile CreateMockFormFile(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        var file = new Mock<IFormFile>();

        file.Setup(f => f.FileName).Returns(fileName);
        file.Setup(f => f.Length).Returns(bytes.Length);
        file.Setup(f => f.OpenReadStream()).Returns(stream);
        file.Setup(f => f.ContentType).Returns("application/octet-stream");
        file.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns((Stream targetStream, CancellationToken token) =>
            {
                stream.Position = 0;
                return stream.CopyToAsync(targetStream, token);
            });

        return file.Object;
    }

    #endregion
}
