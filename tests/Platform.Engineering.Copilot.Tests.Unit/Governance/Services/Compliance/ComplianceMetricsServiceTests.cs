using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Governance.Services;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Tests.Unit.Governance.Services.Compliance;

public class ComplianceMetricsServiceTests
{
    private readonly Mock<ILogger<ComplianceMetricsService>> _mockLogger;
    private readonly ComplianceMetricsService _service;

    public ComplianceMetricsServiceTests()
    {
        _mockLogger = new Mock<ILogger<ComplianceMetricsService>>();
        _service = new ComplianceMetricsService(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ComplianceMetricsService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var service = new ComplianceMetricsService(_mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region RecordScanRequest Tests

    [Fact]
    public void RecordScanRequest_WithValidParameters_RecordsMetric()
    {
        // Arrange
        var subscriptionId = "sub-123";
        var resourceGroup = "rg-test";
        var frameworks = new List<string> { "NIST 800-53", "FedRAMP High" };

        // Act
        _service.RecordScanRequest(subscriptionId, resourceGroup, frameworks);

        // Assert - Verify logger was called
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded scan request")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordScanRequest_WithMultipleFrameworks_RecordsAll()
    {
        // Arrange
        var subscriptionId = "sub-123";
        var resourceGroup = "rg-test";
        var frameworks = new List<string> { "NIST 800-53", "FedRAMP High", "PCI DSS" };

        // Act
        _service.RecordScanRequest(subscriptionId, resourceGroup, frameworks);

        // Assert - Should not throw
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("NIST 800-53") && 
                                               v.ToString()!.Contains("FedRAMP High") &&
                                               v.ToString()!.Contains("PCI DSS")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordScanRequest_WithEmptyFrameworks_RecordsMetric()
    {
        // Arrange
        var subscriptionId = "sub-123";
        var resourceGroup = "rg-test";
        var frameworks = new List<string>();

        // Act
        _service.RecordScanRequest(subscriptionId, resourceGroup, frameworks);

        // Assert - Should not throw
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region RecordScanCompletion Tests

    [Fact]
    public void RecordScanCompletion_WithSuccessfulScan_RecordsMetrics()
    {
        // Arrange
        var subscriptionId = "sub-123";
        var resourceGroup = "rg-test";
        var duration = TimeSpan.FromSeconds(5);
        var findingCount = 3;
        var success = true;

        // Act
        _service.RecordScanCompletion(subscriptionId, resourceGroup, duration, findingCount, success);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded scan completion")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordScanCompletion_WithFailedScan_RecordsFailure()
    {
        // Arrange
        var subscriptionId = "sub-123";
        var resourceGroup = "rg-test";
        var duration = TimeSpan.FromSeconds(2);
        var findingCount = 0;
        var success = false;

        // Act
        _service.RecordScanCompletion(subscriptionId, resourceGroup, duration, findingCount, success);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Success: False")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordScanCompletion_WithZeroFindings_RecordsCorrectly()
    {
        // Arrange
        var subscriptionId = "sub-123";
        var resourceGroup = "rg-test";
        var duration = TimeSpan.FromSeconds(3);
        var findingCount = 0;
        var success = true;

        // Act
        _service.RecordScanCompletion(subscriptionId, resourceGroup, duration, findingCount, success);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("0 findings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(50)]
    public void RecordScanCompletion_WithDifferentFindingCounts_RecordsCorrectly(int findingCount)
    {
        // Arrange
        var subscriptionId = "sub-123";
        var resourceGroup = "rg-test";
        var duration = TimeSpan.FromSeconds(5);
        var success = true;

        // Act
        _service.RecordScanCompletion(subscriptionId, resourceGroup, duration, findingCount, success);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"{findingCount} findings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region RecordFindings Tests

    [Fact]
    public void RecordFindings_WithCriticalFindings_RecordsMetric()
    {
        // Arrange
        var findings = new List<AtoFinding>
        {
            new AtoFinding
            {
                Id = "finding-1",
                ResourceType = "VirtualMachine",
                FindingType = AtoFindingType.Configuration,
                Severity = AtoFindingSeverity.Critical,
                IsRemediable = true
            }
        };

        // Act
        _service.RecordFindings(findings);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded 1 findings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordFindings_WithMultipleFindings_RecordsAll()
    {
        // Arrange
        var findings = new List<AtoFinding>
        {
            new AtoFinding { ResourceType = "VirtualMachine", FindingType = AtoFindingType.Configuration, Severity = AtoFindingSeverity.High },
            new AtoFinding { ResourceType = "StorageAccount", FindingType = AtoFindingType.Encryption, Severity = AtoFindingSeverity.Critical },
            new AtoFinding { ResourceType = "Network", FindingType = AtoFindingType.Compliance, Severity = AtoFindingSeverity.Medium }
        };

        // Act
        _service.RecordFindings(findings);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded 3 findings")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region RecordRemediation Tests

    [Fact]
    public void RecordRemediation_WithSuccessfulRemediation_RecordsMetric()
    {
        // Arrange
        var findingId = "finding-123";
        var remediationType = "AutoRemediation";
        var success = true;
        var duration = TimeSpan.FromSeconds(2);

        // Act
        _service.RecordRemediation(findingId, remediationType, success, duration);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded remediation")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordRemediation_WithFailedRemediation_RecordsFailure()
    {
        // Arrange
        var findingId = "finding-456";
        var remediationType = "ManualRemediation";
        var success = false;
        var duration = TimeSpan.FromSeconds(5);

        // Act
        _service.RecordRemediation(findingId, remediationType, success, duration);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Success=False")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region RecordNistApiCall Tests

    [Fact]
    public void RecordNistApiCall_WithSuccessfulCall_RecordsMetric()
    {
        // Arrange
        var operation = "GetCatalog";
        var success = true;
        var duration = TimeSpan.FromMilliseconds(250);

        // Act
        _service.RecordNistApiCall(operation, success, duration);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Recorded NIST API call")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void RecordNistApiCall_WithFailedCall_RecordsFailure()
    {
        // Arrange
        var operation = "GetControl";
        var success = false;
        var duration = TimeSpan.FromMilliseconds(100);

        // Act
        _service.RecordNistApiCall(operation, success, duration);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Success: False")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData("GetCatalog")]
    [InlineData("GetControl")]
    [InlineData("SearchControls")]
    [InlineData("GetControlsByFamily")]
    public void RecordNistApiCall_WithDifferentOperations_RecordsCorrectly(string operation)
    {
        // Arrange
        var success = true;
        var duration = TimeSpan.FromMilliseconds(150);

        // Act
        _service.RecordNistApiCall(operation, success, duration);

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(operation)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion
}

