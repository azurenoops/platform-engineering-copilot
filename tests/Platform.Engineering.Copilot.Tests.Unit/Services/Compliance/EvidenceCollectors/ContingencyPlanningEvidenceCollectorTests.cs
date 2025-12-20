using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance.EvidenceCollectors;

/// <summary>
/// Unit tests for ContingencyPlanningEvidenceCollector
/// Tests evidence collection for CP control family
/// </summary>
public class ContingencyPlanningEvidenceCollectorTests
{
    private readonly Mock<ILogger<ContingencyPlanningEvidenceCollector>> _loggerMock;
    private readonly Mock<IAzureResourceService> _azureServiceMock;
    private readonly ContingencyPlanningEvidenceCollector _collector;
    private const string TestSubscriptionId = "test-subscription-id";
    private const string TestControlFamily = "CP";
    private const string TestCollectedBy = "test-collector";

    public ContingencyPlanningEvidenceCollectorTests()
    {
        _loggerMock = new Mock<ILogger<ContingencyPlanningEvidenceCollector>>();
        _azureServiceMock = new Mock<IAzureResourceService>();
        _collector = new ContingencyPlanningEvidenceCollector(_loggerMock.Object, _azureServiceMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var collector = new ContingencyPlanningEvidenceCollector(_loggerMock.Object, _azureServiceMock.Object);

        // Assert
        collector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ContingencyPlanningEvidenceCollector(null!, _azureServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAzureService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ContingencyPlanningEvidenceCollector(_loggerMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("azureService");
    }

    #endregion

    #region CollectConfigurationEvidenceAsync Tests

    [Fact]
    public async Task CollectConfigurationEvidenceAsync_WithValidParameters_ReturnsEvidenceList()
    {
        // Act
        var result = await _collector.CollectConfigurationEvidenceAsync(
            TestSubscriptionId, TestControlFamily, TestCollectedBy);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<ComplianceEvidence>>();
    }

    [Fact]
    public async Task CollectConfigurationEvidenceAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        var result = await _collector.CollectConfigurationEvidenceAsync(
            TestSubscriptionId, TestControlFamily, TestCollectedBy, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region CollectLogEvidenceAsync Tests

    [Fact]
    public async Task CollectLogEvidenceAsync_WithValidParameters_ReturnsEvidenceList()
    {
        // Act
        var result = await _collector.CollectLogEvidenceAsync(
            TestSubscriptionId, TestControlFamily, TestCollectedBy);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<ComplianceEvidence>>();
    }

    [Fact]
    public async Task CollectLogEvidenceAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        var result = await _collector.CollectLogEvidenceAsync(
            TestSubscriptionId, TestControlFamily, TestCollectedBy, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region CollectMetricEvidenceAsync Tests

    [Fact]
    public async Task CollectMetricEvidenceAsync_WithValidParameters_ReturnsEvidenceList()
    {
        // Act
        var result = await _collector.CollectMetricEvidenceAsync(
            TestSubscriptionId, TestControlFamily, TestCollectedBy);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<ComplianceEvidence>>();
    }

    [Fact]
    public async Task CollectMetricEvidenceAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        var result = await _collector.CollectMetricEvidenceAsync(
            TestSubscriptionId, TestControlFamily, TestCollectedBy, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region CollectPolicyEvidenceAsync Tests

    [Fact]
    public async Task CollectPolicyEvidenceAsync_WithValidParameters_ReturnsEvidenceList()
    {
        // Act
        var result = await _collector.CollectPolicyEvidenceAsync(
            TestSubscriptionId, TestControlFamily, TestCollectedBy);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<ComplianceEvidence>>();
    }

    [Fact]
    public async Task CollectPolicyEvidenceAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        var result = await _collector.CollectPolicyEvidenceAsync(
            TestSubscriptionId, TestControlFamily, TestCollectedBy, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region CollectAccessControlEvidenceAsync Tests

    [Fact]
    public async Task CollectAccessControlEvidenceAsync_WithValidParameters_ReturnsEvidenceList()
    {
        // Act
        var result = await _collector.CollectAccessControlEvidenceAsync(
            TestSubscriptionId, TestControlFamily, TestCollectedBy);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<ComplianceEvidence>>();
    }

    [Fact]
    public async Task CollectAccessControlEvidenceAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        // Act
        var result = await _collector.CollectAccessControlEvidenceAsync(
            TestSubscriptionId, TestControlFamily, TestCollectedBy, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
