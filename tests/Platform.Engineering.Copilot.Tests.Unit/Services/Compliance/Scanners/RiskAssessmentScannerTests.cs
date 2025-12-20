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
/// Unit tests for RiskAssessmentScanner
/// Tests scanning for RA control family
/// </summary>
public class RiskAssessmentScannerTests
{
    private readonly Mock<ILogger<RiskAssessmentScanner>> _loggerMock;
    private readonly Mock<IAzureResourceService> _azureServiceMock;
    private readonly Mock<IDefenderForCloudService> _defenderServiceMock;
    private readonly RiskAssessmentScanner _scanner;
    private const string TestSubscriptionId = "test-subscription-id";

    public RiskAssessmentScannerTests()
    {
        _loggerMock = new Mock<ILogger<RiskAssessmentScanner>>();
        _azureServiceMock = new Mock<IAzureResourceService>();
        _defenderServiceMock = new Mock<IDefenderForCloudService>();
        _scanner = new RiskAssessmentScanner(_loggerMock.Object, _azureServiceMock.Object, _defenderServiceMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var scanner = new RiskAssessmentScanner(_loggerMock.Object, _azureServiceMock.Object, _defenderServiceMock.Object);

        // Assert
        scanner.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RiskAssessmentScanner(null!, _azureServiceMock.Object, _defenderServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAzureService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RiskAssessmentScanner(_loggerMock.Object, null!, _defenderServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("azureService");
    }

    [Fact]
    public void Constructor_WithNullDefenderService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RiskAssessmentScanner(_loggerMock.Object, _azureServiceMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("defenderService");
    }

    #endregion

    #region ScanControlAsync Tests - Subscription Scope

    [Fact]
    public async Task ScanControlAsync_WithValidControl_ReturnsFindingsList()
    {
        // Arrange
        var control = new NistControl { Id = "RA-5", Title = "Vulnerability Scanning" };

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Theory]
    [InlineData("RA-1", "Risk Assessment Policy")]
    [InlineData("RA-2", "Security Categorization")]
    [InlineData("RA-3", "Risk Assessment")]
    [InlineData("RA-5", "Vulnerability Scanning")]
    public async Task ScanControlAsync_WithVariousRAControls_Succeeds(string controlId, string title)
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
        var control = new NistControl { Id = "RA-5", Title = "Vulnerability Scanning" };
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
        var control = new NistControl { Id = "RA-5", Title = "Vulnerability Scanning" };
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
        var control = new NistControl { Id = "RA-5", Title = "Vulnerability Scanning" };
        var resourceGroupName = "test-rg";
        var cts = new CancellationTokenSource();

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, resourceGroupName, control, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
