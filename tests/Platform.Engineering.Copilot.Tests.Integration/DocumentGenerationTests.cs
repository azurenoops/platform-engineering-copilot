using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Integration;

/// <summary>
/// Integration tests for document generation service
/// Run these against a real Azure subscription to verify document generation
/// </summary>
public class DocumentGenerationTests
{
    private readonly ITestOutputHelper _output;

    public DocumentGenerationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Manual test - requires real Azure subscription")]
    public async Task GenerateControlNarrative_WithValidControl_ReturnsNarrative()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        
        // Note: This would need full DI setup from ServiceCollectionExtensions
        // For now, this is a template for manual testing
        
        var serviceProvider = services.BuildServiceProvider();
        var documentService = serviceProvider.GetRequiredService<IDocumentGenerationService>();

        // Act
        var narrative = await documentService.GenerateControlNarrativeAsync("AC-2");

        // Assert
        Assert.NotNull(narrative);
        Assert.Equal("AC-2", narrative.ControlId);
        Assert.NotEmpty(narrative.What);
        Assert.NotEmpty(narrative.How);
        
        _output.WriteLine($"Generated narrative for {narrative.ControlId}:");
        _output.WriteLine($"Title: {narrative.ControlTitle}");
        _output.WriteLine($"Status: {narrative.ImplementationStatus}");
    }

    [Fact(Skip = "Manual test - requires real Azure subscription")]
    public async Task GenerateSSP_WithValidParameters_ReturnsDocument()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        var serviceProvider = services.BuildServiceProvider();
        var documentService = serviceProvider.GetRequiredService<IDocumentGenerationService>();

        var sspParams = new SspParameters
        {
            SystemName = "Test Azure Government System",
            SystemDescription = "Test system for document generation validation",
            ImpactLevel = "IL4",
            SystemOwner = "Test Owner",
            AuthorizingOfficial = "Test AO",
            Classification = "UNCLASSIFIED"
        };

        // Act
        var document = await documentService.GenerateSSPAsync(
            "test-subscription-id", 
            sspParams);

        // Assert
        Assert.NotNull(document);
        Assert.Equal("SSP", document.DocumentType);
        Assert.NotEmpty(document.Content);
        Assert.Contains("System Security Plan", document.Title);
        
        _output.WriteLine($"Generated SSP:");
        _output.WriteLine($"Document ID: {document.DocumentId}");
        _output.WriteLine($"Title: {document.Title}");
        _output.WriteLine($"Generated: {document.GeneratedDate}");
        _output.WriteLine($"Content length: {document.Content.Length} chars");
    }

    [Fact(Skip = "Manual test - requires real Azure subscription")]
    public async Task GenerateSAR_WithValidSubscription_ReturnsReport()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        var serviceProvider = services.BuildServiceProvider();
        var documentService = serviceProvider.GetRequiredService<IDocumentGenerationService>();

        // Act
        var document = await documentService.GenerateSARAsync(
            "test-subscription-id",
            "test-assessment-id");

        // Assert
        Assert.NotNull(document);
        Assert.Equal("SAR", document.DocumentType);
        Assert.NotEmpty(document.Content);
        Assert.Contains("Security Assessment Report", document.Title);
        
        _output.WriteLine($"Generated SAR:");
        _output.WriteLine($"Document ID: {document.DocumentId}");
        _output.WriteLine($"Title: {document.Title}");
    }

    [Fact(Skip = "Manual test - requires real Azure subscription")]
    public async Task GeneratePOAM_WithValidSubscription_ReturnsDocument()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        var serviceProvider = services.BuildServiceProvider();
        var documentService = serviceProvider.GetRequiredService<IDocumentGenerationService>();

        // Act
        var document = await documentService.GeneratePOAMAsync("test-subscription-id");

        // Assert
        Assert.NotNull(document);
        Assert.Equal("POAM", document.DocumentType);
        Assert.NotEmpty(document.Content);
        Assert.Contains("Plan of Action", document.Title);
        
        _output.WriteLine($"Generated POA&M:");
        _output.WriteLine($"Document ID: {document.DocumentId}");
        _output.WriteLine($"Title: {document.Title}");
    }

    [Fact(Skip = "Manual test - requires real Azure subscription")]
    public async Task ExportDocument_AsMarkdown_ReturnsBytes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        var serviceProvider = services.BuildServiceProvider();
        var documentService = serviceProvider.GetRequiredService<IDocumentGenerationService>();

        // Act
        var bytes = await documentService.ExportDocumentAsync(
            "test-doc-id",
            ComplianceDocumentFormat.Markdown);

        // Assert
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        
        var content = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.NotEmpty(content);
        
        _output.WriteLine($"Exported document:");
        _output.WriteLine($"Size: {bytes.Length} bytes");
        _output.WriteLine($"Preview: {content.Substring(0, Math.Min(200, content.Length))}...");
    }

    [Fact(Skip = "Manual test - requires real Azure subscription")]
    public async Task ExportDocument_AsHtml_ReturnsBytes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        var serviceProvider = services.BuildServiceProvider();
        var documentService = serviceProvider.GetRequiredService<IDocumentGenerationService>();

        // Act
        var bytes = await documentService.ExportDocumentAsync(
            "test-doc-id",
            ComplianceDocumentFormat.HTML);

        // Assert
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        
        var content = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("<!DOCTYPE html>", content);
        Assert.Contains("</html>", content);
        
        _output.WriteLine($"Exported HTML document:");
        _output.WriteLine($"Size: {bytes.Length} bytes");
    }

    [Fact(Skip = "Manual test - will throw NotImplementedException")]
    public async Task ExportDocument_AsDocx_ThrowsNotImplementedException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        var serviceProvider = services.BuildServiceProvider();
        var documentService = serviceProvider.GetRequiredService<IDocumentGenerationService>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
        {
            await documentService.ExportDocumentAsync(
                "test-doc-id",
                ComplianceDocumentFormat.DOCX);
        });
    }

    [Fact(Skip = "Manual test - will throw NotImplementedException")]
    public async Task ExportDocument_AsPdf_ThrowsNotImplementedException()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        var serviceProvider = services.BuildServiceProvider();
        var documentService = serviceProvider.GetRequiredService<IDocumentGenerationService>();

        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(async () =>
        {
            await documentService.ExportDocumentAsync(
                "test-doc-id",
                ComplianceDocumentFormat.PDF);
        });
    }

    [Fact(Skip = "Manual test - requires real Azure subscription")]
    public async Task FormatDocument_WithNistStandard_ReturnsFormattedDocument()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        var serviceProvider = services.BuildServiceProvider();
        var documentService = serviceProvider.GetRequiredService<IDocumentGenerationService>();

        var document = new GeneratedDocument
        {
            DocumentId = "test-doc",
            Title = "Test Document",
            Content = "# Test\n\nThis is a test document."
        };

        // Act
        var formattedDoc = await documentService.FormatDocumentAsync(
            document,
            FormattingStandard.NIST);

        // Assert
        Assert.NotNull(formattedDoc);
        Assert.Equal("NIST SP 800-53 Rev 5", formattedDoc.Metadata.GetValueOrDefault("FormattingStandard"));
        
        _output.WriteLine($"Formatted document:");
        _output.WriteLine($"Standard: {formattedDoc.Metadata.GetValueOrDefault("FormattingStandard")}");
    }

    [Fact(Skip = "Manual test - requires real Azure subscription")]
    public async Task ListDocuments_WithPackageId_ReturnsDocuments()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        var serviceProvider = services.BuildServiceProvider();
        var documentService = serviceProvider.GetRequiredService<IDocumentGenerationService>();

        // Act
        var documents = await documentService.ListDocumentsAsync("test-package-id");

        // Assert
        Assert.NotNull(documents);
        Assert.NotEmpty(documents);
        
        foreach (var doc in documents)
        {
            _output.WriteLine($"Document: {doc.Title} ({doc.DocumentType})");
            _output.WriteLine($"  ID: {doc.DocumentId}");
            _output.WriteLine($"  Status: {doc.Status}");
            _output.WriteLine($"  Version: {doc.Version}");
        }
    }
}
