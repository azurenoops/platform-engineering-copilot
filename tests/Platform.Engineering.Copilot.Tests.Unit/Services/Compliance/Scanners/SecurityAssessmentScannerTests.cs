using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance.Scanners;

/// <summary>
/// Unit tests for SecurityAssessmentScanner
/// Tests scanning for CA control family
/// </summary>
public class SecurityAssessmentScannerTests
{
    private readonly Mock<ILogger<SecurityAssessmentScanner>> _loggerMock;
    private readonly Mock<IAzureResourceService> _azureServiceMock;
    private readonly Mock<IDefenderForCloudService> _defenderServiceMock;
    private readonly SecurityAssessmentScanner _scanner;
    private const string TestSubscriptionId = "test-subscription-id";

    public SecurityAssessmentScannerTests()
    {
        _loggerMock = new Mock<ILogger<SecurityAssessmentScanner>>();
        _azureServiceMock = new Mock<IAzureResourceService>();
        _defenderServiceMock = new Mock<IDefenderForCloudService>();
        _scanner = new SecurityAssessmentScanner(_loggerMock.Object, _azureServiceMock.Object, _defenderServiceMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var scanner = new SecurityAssessmentScanner(_loggerMock.Object, _azureServiceMock.Object, _defenderServiceMock.Object);

        // Assert
        scanner.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SecurityAssessmentScanner(null!, _azureServiceMock.Object, _defenderServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAzureService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SecurityAssessmentScanner(_loggerMock.Object, null!, _defenderServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("azureService");
    }

    [Fact]
    public void Constructor_WithNullDefenderService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SecurityAssessmentScanner(_loggerMock.Object, _azureServiceMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("defenderService");
    }

    #endregion

    #region ScanControlAsync Tests - Subscription Scope

    [Fact]
    public async Task ScanControlAsync_WithValidControl_ReturnsFindingsList()
    {
        // Arrange
        var control = new NistControl { Id = "CA-7", Title = "Continuous Monitoring" };

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Theory]
    [InlineData("CA-1", "Security Assessment Policy")]
    [InlineData("CA-2", "Security Assessments")]
    [InlineData("CA-3", "System Interconnections")]
    [InlineData("CA-5", "Plan of Action and Milestones")]
    [InlineData("CA-7", "Continuous Monitoring")]
    [InlineData("CA-9", "Internal System Connections")]
    public async Task ScanControlAsync_WithVariousCAControls_Succeeds(string controlId, string title)
    {
        // Arrange
        var control = new NistControl { Id = controlId, Title = title };

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ScanControlAsync_WithCancellationToken_RespectsToken()
    {
        // Arrange
        var control = new NistControl { Id = "CA-7", Title = "Continuous Monitoring" };
        var cts = new CancellationTokenSource();

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, control, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region ScanControlAsync Tests - Resource Group Scope

    [Fact]
    public async Task ScanControlAsync_WithResourceGroup_ReturnsFindingsList()
    {
        // Arrange
        var control = new NistControl { Id = "CA-7", Title = "Continuous Monitoring" };
        var resourceGroupName = "test-rg";

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, resourceGroupName, control);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ScanControlAsync_WithResourceGroupAndCancellationToken_RespectsToken()
    {
        // Arrange
        var control = new NistControl { Id = "CA-7", Title = "Continuous Monitoring" };
        var resourceGroupName = "test-rg";
        var cts = new CancellationTokenSource();

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, resourceGroupName, control, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
