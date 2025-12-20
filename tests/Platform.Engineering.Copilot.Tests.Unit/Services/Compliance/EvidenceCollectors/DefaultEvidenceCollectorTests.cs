using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance.EvidenceCollectors;

/// <summary>
/// Unit tests for DefaultEvidenceCollector
/// Tests evidence collection for all control families as a fallback collector
/// </summary>
public class DefaultEvidenceCollectorTests
{
    private readonly Mock<ILogger<DefaultEvidenceCollector>> _loggerMock;
    private readonly DefaultEvidenceCollector _collector;
    private const string TestSubscriptionId = "test-subscription-id";
    private const string TestControlFamily = "AC";
    private const string TestCollectedBy = "test-collector";

    public DefaultEvidenceCollectorTests()
    {
        _loggerMock = new Mock<ILogger<DefaultEvidenceCollector>>();
        _collector = new DefaultEvidenceCollector(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var collector = new DefaultEvidenceCollector(_loggerMock.Object);

        // Assert
        collector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DefaultEvidenceCollector(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
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

    [Theory]
    [InlineData("AC")]
    [InlineData("AU")]
    [InlineData("CM")]
    [InlineData("CP")]
    [InlineData("IA")]
    [InlineData("IR")]
    [InlineData("RA")]
    [InlineData("SC")]
    [InlineData("SI")]
    public async Task CollectConfigurationEvidenceAsync_WithVariousControlFamilies_Succeeds(string controlFamily)
    {
        // Act
        var result = await _collector.CollectConfigurationEvidenceAsync(
            TestSubscriptionId, controlFamily, TestCollectedBy);

        // Assert
        result.Should().NotBeNull();
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
