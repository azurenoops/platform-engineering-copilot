using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Governance.Services;
using Platform.Engineering.Copilot.Governance.Models;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Tests.Integration.Governance.Services.Compliance;

public class ComplianceMetricsServiceIntegrationTests
{
    private readonly Mock<ILogger<ComplianceMetricsService>> _mockLogger;
    private readonly ComplianceMetricsService _service;

    public ComplianceMetricsServiceIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<ComplianceMetricsService>>();
        _service = new ComplianceMetricsService(_mockLogger.Object);
    }

    [Fact]
    public void CompleteWorkflow_RecordsAllMetrics()
    {
        // Arrange
        var subscriptionId = "sub-123";
        var resourceGroup = "rg-test";
        var frameworks = new List<string> { "NIST 800-53" };
        var controlId = "AC-1";
        var severity = "High";
        var resourceType = "VirtualMachine";
        var resourceId = "resource-123";
        var duration = TimeSpan.FromSeconds(5);

        // Act - Simulate complete workflow
        _service.RecordScanRequest(subscriptionId, resourceGroup, frameworks);
        _service.RecordNistApiCall("GetCatalog", true, TimeSpan.FromMilliseconds(200));
        
        var findings = new List<AtoFinding>
        {
            new AtoFinding { ResourceType = resourceType, FindingType = AtoFindingType.Configuration, Severity = AtoFindingSeverity.High }
        };
        _service.RecordFindings(findings);
        
        _service.RecordRemediation(controlId, "AutoRemediation", true, TimeSpan.FromSeconds(1));
        _service.RecordScanCompletion(subscriptionId, resourceGroup, duration, 1, true);

        // Assert - Verify all logs were recorded
        _mockLogger.Verify(
            x => x.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(5));
    }

    [Fact]
    public void Dispose_DisposesResources()
    {
        // Arrange
        var service = new ComplianceMetricsService(_mockLogger.Object);

        // Act
        service.Dispose();

        // Assert - Should not throw
        service.Should().NotBeNull();
    }
}
