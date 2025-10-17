using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using CoreModels = Platform.Engineering.Copilot.Core.Models;
using DocModels = Platform.Engineering.Copilot.DocumentProcessing.Models;
using Platform.Engineering.Copilot.DocumentProcessing.Services;
using Platform.Engineering.Copilot.Services.DocumentProcessing;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.DocumentProcessing.Services
{
    /// <summary>
    /// Integration tests for DocumentProcessingService covering end-to-end document processing workflows
    /// Tests complete pipeline from upload through extraction, analysis, and results
    /// </summary>
    public class DocumentProcessingServiceIntegrationTests : IAsyncLifetime
    {
        private ServiceProvider? _serviceProvider;
        private IDocumentProcessingService? _documentProcessingService;
        private string _testUploadsPath = string.Empty;

        public async Task InitializeAsync()
        {
            // Set up test uploads directory
            _testUploadsPath = Path.Combine(Path.GetTempPath(), $"DocProcessing_Tests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testUploadsPath);

            // Set up services
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

            // Add configuration
            var configData = new Dictionary<string, string>
            {
                { "DocumentProcessing:UploadsPath", _testUploadsPath },
                { "DocumentProcessing:MaxFileSizeMB", "50" },
                { "DocumentProcessing:SupportedFormats", "pdf,docx,doc,txt,md" }
            };
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configData!)
                .Build();
            services.AddSingleton<IConfiguration>(configuration);

            // Mock external dependencies with correct model structures
            var mockDiagramAnalyzer = new Mock<IArchitectureDiagramAnalyzer>();
            mockDiagramAnalyzer
                .Setup(a => a.AnalyzeArchitectureAsync(It.IsAny<DocModels.ExtractedContent>()))
                .ReturnsAsync(new DocModels.ArchitectureAnalysis
                {
                    DetectedPatterns = new List<DocModels.ArchitecturePattern>
                    {
                        new DocModels.ArchitecturePattern { Name = "Microservices", Description = "Distributed services pattern", Confidence = 0.85 }
                    },
                    SystemComponents = new List<DocModels.SystemComponent>
                    {
                        new DocModels.SystemComponent { Name = "Web Server", Type = "Server", Description = "IIS-based web server" },
                        new DocModels.SystemComponent { Name = "Database", Type = "Database", Description = "SQL Server database" }
                    },
                    DataFlows = new List<DocModels.DataFlow>
                    {
                        new DocModels.DataFlow { Source = "Web Server", Target = "Database", DataType = "SQL", Classification = DocModels.SecurityClassification.Confidential }
                    },
                    TechnologyStack = new List<string> { "IIS", "SQL Server", ".NET" },
                    Recommendations = new List<DocModels.ArchitectureRecommendation>
                    {
                        new DocModels.ArchitectureRecommendation { Title = "Add caching layer", Description = "Improve performance", Priority = DocModels.Priority.Medium }
                    }
                });

            mockDiagramAnalyzer
                .Setup(a => a.ExtractDiagramsAsync(It.IsAny<string>()))
                .ReturnsAsync(new List<DocModels.ExtractedDiagram>
                {
                    new DocModels.ExtractedDiagram
                    {
                        DiagramId = Guid.NewGuid().ToString(),
                        Title = "System Architecture",
                        Type = DocModels.DiagramType.SystemArchitecture,
                        Components = new List<DocModels.DiagramComponent>(),
                        Connections = new List<DocModels.DiagramConnection>(),
                        PageNumber = 1,
                        Properties = new Dictionary<string, object>
                        {
                            ["summary"] = "Architecture diagram content"
                        }
                    }
                });

            var mockComplianceEngine = new Mock<IAtoComplianceEngine>();
            mockComplianceEngine
                .Setup(e => e.RunComprehensiveAssessmentAsync(It.IsAny<string>(), It.IsAny<IProgress<CoreModels.AssessmentProgress>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CoreModels.AtoComplianceAssessment
                {
                    AssessmentId = Guid.NewGuid().ToString(),
                    SubscriptionId = "test-subscription",
                    StartTime = DateTimeOffset.UtcNow,
                    EndTime = DateTimeOffset.UtcNow,
                    Duration = TimeSpan.FromMinutes(10),
                    OverallComplianceScore = 85.0,
                    TotalFindings = 1,
                    ControlFamilyResults = new Dictionary<string, CoreModels.ControlFamilyAssessment>
                    {
                        ["AC"] = new CoreModels.ControlFamilyAssessment
                        {
                            ControlFamily = "AC",
                            FamilyName = "Access Control",
                            AssessmentTime = DateTimeOffset.UtcNow,
                            TotalControls = 20,
                            PassedControls = 17,
                            ComplianceScore = 85.0,
                            Findings = new List<CoreModels.AtoFinding>
                            {
                                new CoreModels.AtoFinding
                                {
                                    Id = Guid.NewGuid().ToString(),
                                    Title = "Access Control Policy",
                                    Severity = CoreModels.AtoFindingSeverity.Medium,
                                    ComplianceStatus = CoreModels.AtoComplianceStatus.Compliant,
                                    Description = "Policy documented",
                                    AffectedControls = new List<string> { "AC-1" }
                                }
                            }
                        }
                    }
                });

            var mockRemediationEngine = new Mock<IAtoRemediationEngine>();
            mockRemediationEngine
                .Setup(e => e.GenerateRemediationPlanAsync(
                    It.IsAny<string>(),
                    It.IsAny<List<CoreModels.AtoFinding>>(),
                    It.IsAny<CoreModels.RemediationPlanOptions>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new CoreModels.RemediationPlan
                {
                    PlanId = Guid.NewGuid().ToString(),
                    SubscriptionId = "test-subscription",
                    CreatedAt = DateTimeOffset.UtcNow,
                    TotalFindings = 1,
                    EstimatedEffort = TimeSpan.FromHours(40),
                    RemediationItems = new List<CoreModels.RemediationItem>
                    {
                        new CoreModels.RemediationItem
                        {
                            FindingId = Guid.NewGuid().ToString(),
                            ControlId = "AC-2",
                            Title = "Missing user account management",
                            Priority = "High",
                            EstimatedEffort = TimeSpan.FromHours(20),
                            Notes = "Implement user account management procedures",
                            Status = CoreModels.AtoRemediationStatus.NotStarted
                        }
                    }
                });

            services.AddSingleton(mockDiagramAnalyzer.Object);
            services.AddSingleton(mockComplianceEngine.Object);
            services.AddSingleton(mockRemediationEngine.Object);

            // Add the actual document processing service
            services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();

            _serviceProvider = services.BuildServiceProvider();
            _documentProcessingService = _serviceProvider.GetRequiredService<IDocumentProcessingService>();

            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            // Clean up test uploads directory
            if (Directory.Exists(_testUploadsPath))
            {
                Directory.Delete(_testUploadsPath, true);
            }

            if (_serviceProvider != null)
            {
                await _serviceProvider.DisposeAsync();
            }
        }

        #region Complete Workflow Tests

        [Fact]
    public async Task ProcessDocument_TextFile_CompletesFullWorkflowAsync()
        {
            // Arrange
            var content = "This is a test compliance document.\n\nSection 1: Access Control\nWe implement strict access controls.\n\nSection 2: Encryption\nAll data is encrypted at rest and in transit.";
            var file = CreateMockFormFile("test-document.txt", content);

            // Act
            var result = await _documentProcessingService!.ProcessDocumentAsync(
                file,
                DocModels.DocumentAnalysisType.ComplianceDocument);

            // Assert
            result.Should().NotBeNull();
            result.DocumentId.Should().NotBeNullOrEmpty();
            result.ProcessingStatus.Should().Be(DocModels.ProcessingStatus.Complete);
            result.ExtractedText.Should().NotBeEmpty();
            result.Metadata.Should().NotBeNull();
            result.Metadata.FileType.Should().Be(".txt");
        }

        [Fact]
    public async Task ProcessDocument_WithFullAnalysis_ExtractsAndAnalyzesContentAsync()
        {
            // Arrange
            var content = "System Architecture Document\n\nOur system uses a web server connected to a database.\nSecurity controls include firewalls and encryption.";
            var file = CreateMockFormFile("architecture.md", content);

            // Act
            var result = await _documentProcessingService!.ProcessDocumentAsync(
                file,
                DocModels.DocumentAnalysisType.General);

            // Assert
            result.Should().NotBeNull();
            result.DocumentId.Should().NotBeNullOrEmpty();
            result.ProcessingStatus.Should().Be(DocModels.ProcessingStatus.Complete);
            result.AnalysisPreview.Should().NotBeNullOrEmpty();
            result.ExtractedText.Should().Contain(t => t.Contains("Architecture"));
        }

        #endregion

        #region Content Extraction Tests

        [Fact]
    public async Task ProcessDocument_TextFile_ExtractsFullContentAsync()
        {
            // Arrange
            var expectedContent = "Line 1\nLine 2\nLine 3";
            var file = CreateMockFormFile("simple.txt", expectedContent);

            // Act
            var result = await _documentProcessingService!.ProcessDocumentAsync(
                file,
                DocModels.DocumentAnalysisType.General);

            // Assert
            result.ExtractedText.Should().HaveCount(1);
            result.ExtractedText[0].Should().Contain("Line 1");
            result.ExtractedText[0].Should().Contain("Line 2");
            result.ExtractedText[0].Should().Contain("Line 3");
        }

        [Fact]
    public async Task ProcessDocument_MarkdownFile_PreservesStructureAsync()
        {
            // Arrange
            var markdown = "# Heading 1\n\n## Heading 2\n\nSome content here.\n\n- Item 1\n- Item 2";
            var file = CreateMockFormFile("document.md", markdown);

            // Act
            var result = await _documentProcessingService!.ProcessDocumentAsync(
                file,
                DocModels.DocumentAnalysisType.General);

            // Assert
            result.ExtractedText.Should().NotBeEmpty();
            result.ExtractedText[0].Should().Contain("Heading 1");
            result.ExtractedText[0].Should().Contain("Heading 2");
            result.ExtractedText[0].Should().Contain("Item 1");
        }

        #endregion

        #region Metadata Extraction Tests

        [Fact]
    public async Task ProcessDocument_ExtractsBasicMetadataAsync()
        {
            // Arrange
            var file = CreateMockFormFile("metadata-test.txt", "Test content");

            // Act
            var result = await _documentProcessingService!.ProcessDocumentAsync(
                file,
                DocModels.DocumentAnalysisType.General);

            // Assert
            result.Metadata.Should().NotBeNull();
            result.Metadata.FileType.Should().Be(".txt");
            result.Metadata.FileSize.Should().BeGreaterThan(0);
            result.Metadata.CreatedDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
    public async Task ProcessDocument_MultipleFiles_MaintainsUniqueMetadataAsync()
        {
            // Arrange
            var file1 = CreateMockFormFile("file1.txt", "Content 1");
            var file2 = CreateMockFormFile("file2.txt", "Content 2 with more text");

            // Act
            var result1 = await _documentProcessingService!.ProcessDocumentAsync(file1, DocModels.DocumentAnalysisType.General);
            var result2 = await _documentProcessingService.ProcessDocumentAsync(file2, DocModels.DocumentAnalysisType.General);

            // Assert
            result1.DocumentId.Should().NotBe(result2.DocumentId);
            result1.Metadata.FileSize.Should().NotBe(result2.Metadata.FileSize);
            result1.Metadata.FileSize.Should().BeLessThan(result2.Metadata.FileSize);
        }

        #endregion

        #region Processing Status Tests

        [Fact]
    public async Task GetProcessingStatus_AfterUpload_ReturnsCorrectStatusAsync()
        {
            // Arrange
            var file = CreateMockFormFile("status-test.txt", "Status test content");
            var result = await _documentProcessingService!.ProcessDocumentAsync(file, DocModels.DocumentAnalysisType.General);

            // Act
            var status = await _documentProcessingService.GetProcessingStatusAsync(result.DocumentId);

            // Assert
            status.Should().NotBeNull();
            status.DocumentId.Should().Be(result.DocumentId);
            status.Status.Should().Be(DocModels.ProcessingStatus.Complete);
            status.ProgressPercentage.Should().Be(100);
        }

        [Fact]
    public async Task GetProcessingStatus_InvalidDocumentId_ThrowsExceptionAsync()
        {
            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
                await _documentProcessingService!.GetProcessingStatusAsync("invalid-id-12345"));
        }

        #endregion

        #region Analysis Type Tests

        [Fact]
    public async Task ProcessDocument_SecurityDocument_PerformsSecurityAnalysisAsync()
        {
            // Arrange
            var content = "Security Policy\n\nAccess Control: All users must authenticate.\nEncryption: AES-256 for data at rest.\nAudit Logging: All access logged.";
            var file = CreateMockFormFile("security-policy.txt", content);

            // Act
            var result = await _documentProcessingService!.ProcessDocumentAsync(
                file,
                DocModels.DocumentAnalysisType.SecurityDocument);

            // Assert
            result.Should().NotBeNull();
            result.ProcessingStatus.Should().Be(DocModels.ProcessingStatus.Complete);
            result.AnalysisPreview.Should().NotBeNullOrEmpty();
        }

        [Fact]
    public async Task ProcessDocument_ComplianceDocument_PerformsComplianceAnalysisAsync()
        {
            // Arrange
            var content = "NIST 800-53 Compliance Report\n\nAC-1: Access Control Policy - COMPLIANT\nAC-2: Account Management - COMPLIANT\nAC-3: Access Enforcement - PARTIAL";
            var file = CreateMockFormFile("compliance-report.txt", content);

            // Act
            var result = await _documentProcessingService!.ProcessDocumentAsync(
                file,
                DocModels.DocumentAnalysisType.ComplianceDocument);

            // Assert
            result.Should().NotBeNull();
            result.ProcessingStatus.Should().Be(DocModels.ProcessingStatus.Complete);
            result.ExtractedText.Should().Contain(t => t.Contains("NIST"));
        }

        [Fact]
    public async Task ProcessDocument_ArchitectureDiagram_PerformsArchitectureAnalysisAsync()
        {
            // Arrange
            var content = "System Architecture\n\nComponents:\n- Web Server (IIS)\n- Application Server (ASP.NET)\n- Database (SQL Server)\n\nConnections:\n- Web -> App (HTTPS)\n- App -> DB (TCP/1433)";
            var file = CreateMockFormFile("architecture.txt", content);

            // Act
            var result = await _documentProcessingService!.ProcessDocumentAsync(
                file,
                DocModels.DocumentAnalysisType.ArchitectureDiagram);

            // Assert
            result.Should().NotBeNull();
            result.ProcessingStatus.Should().Be(DocModels.ProcessingStatus.Complete);
            result.AnalysisPreview.Should().Contain("component");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
    public async Task ProcessDocument_EmptyFile_HandlesGracefullyAsync()
        {
            // Arrange
            var file = CreateMockFormFile("empty.txt", string.Empty);

            // Act
            var result = await _documentProcessingService!.ProcessDocumentAsync(
                file,
                DocModels.DocumentAnalysisType.General);

            // Assert
            result.Should().NotBeNull();
            result.DocumentId.Should().NotBeNullOrEmpty();
            // Empty file should still process, just with no extracted text
        }

        [Fact]
    public async Task ProcessDocument_LargeTextFile_ProcessesSuccessfullyAsync()
        {
            // Arrange
            var largeContent = string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"Line {i}: This is test content"));
            var file = CreateMockFormFile("large-file.txt", largeContent);

            // Act
            var result = await _documentProcessingService!.ProcessDocumentAsync(
                file,
                DocModels.DocumentAnalysisType.General);

            // Assert
            result.Should().NotBeNull();
            result.ProcessingStatus.Should().Be(DocModels.ProcessingStatus.Complete);
            result.ExtractedText.Should().NotBeEmpty();
            result.ExtractedText[0].Should().Contain("Line 1:");
            result.ExtractedText[0].Should().Contain("Line 1000:");
        }

        #endregion

        #region Multiple Document Tests

        [Fact]
    public async Task ProcessDocument_MultipleDocuments_ProcessedIndependentlyAsync()
        {
            // Arrange
            var file1 = CreateMockFormFile("doc1.txt", "Document 1 content");
            var file2 = CreateMockFormFile("doc2.txt", "Document 2 content");
            var file3 = CreateMockFormFile("doc3.txt", "Document 3 content");

            // Act
            var results = await Task.WhenAll(
                _documentProcessingService!.ProcessDocumentAsync(file1, DocModels.DocumentAnalysisType.General),
                _documentProcessingService.ProcessDocumentAsync(file2, DocModels.DocumentAnalysisType.General),
                _documentProcessingService.ProcessDocumentAsync(file3, DocModels.DocumentAnalysisType.General)
            );

            // Assert
            results.Should().HaveCount(3);
            results.Select(r => r.DocumentId).Should().OnlyHaveUniqueItems();
            results.All(r => r.ProcessingStatus == DocModels.ProcessingStatus.Complete).Should().BeTrue();
        }

        [Fact]
    public async Task GetDocumentAnalysis_MultipleDocuments_ReturnsCorrectAnalysisAsync()
        {
            // Arrange
            var file1 = CreateMockFormFile("analysis1.txt", "Analysis document 1");
            var file2 = CreateMockFormFile("analysis2.txt", "Analysis document 2");

            var result1 = await _documentProcessingService!.ProcessDocumentAsync(file1, DocModels.DocumentAnalysisType.General);
            var result2 = await _documentProcessingService.ProcessDocumentAsync(file2, DocModels.DocumentAnalysisType.General);

            // Act
            var analysis1 = await _documentProcessingService.GetDocumentAnalysisAsync(result1.DocumentId);
            var analysis2 = await _documentProcessingService.GetDocumentAnalysisAsync(result2.DocumentId);

            // Assert
            analysis1.Should().NotBeNull();
            analysis2.Should().NotBeNull();
            analysis1.DocumentId.Should().Be(result1.DocumentId);
            analysis2.DocumentId.Should().Be(result2.DocumentId);
        }

        #endregion

        #region RMF Compliance Analysis Tests

        [Fact]
    public async Task PerformRmfAnalysis_WithDocumentId_ReturnsComplianceResultAsync()
        {
            // Arrange
            var content = "RMF Compliance Documentation\n\nControl AC-1: Implemented\nControl AC-2: Implemented\nControl AC-3: Partially Implemented";
            var file = CreateMockFormFile("rmf-doc.txt", content);
            var result = await _documentProcessingService!.ProcessDocumentAsync(file, DocModels.DocumentAnalysisType.ComplianceDocument);

            // Act
            var complianceResult = await _documentProcessingService.PerformRmfAnalysisAsync(result.DocumentId, "NIST-800-53");

            // Assert
            complianceResult.Should().NotBeNull();
            complianceResult.ComplianceScore.Should().BeGreaterThan(0);
            complianceResult.OverallStatus.Should().NotBe(default(CoreModels.ComplianceStatus));
        }

        #endregion

        #region Helper Methods

        private IFormFile CreateMockFormFile(string fileName, string content)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);
            
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(bytes.Length);
            mockFile.Setup(f => f.OpenReadStream()).Returns(stream);
            mockFile.Setup(f => f.ContentType).Returns("text/plain");
            mockFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream target, CancellationToken token) =>
                {
                    stream.Position = 0;
                    return stream.CopyToAsync(target, token);
                });

            return mockFile.Object;
        }

        #endregion
    }
}
