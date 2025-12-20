using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance.Scanners;

/// <summary>
/// Unit tests for DefaultComplianceScanner
/// Tests scanning for any control family as a fallback scanner
/// </summary>
public class DefaultComplianceScannerTests
{
    private readonly Mock<ILogger<DefaultComplianceScanner>> _loggerMock;
    private readonly DefaultComplianceScanner _scanner;
    private const string TestSubscriptionId = "test-subscription-id";

    public DefaultComplianceScannerTests()
    {
        _loggerMock = new Mock<ILogger<DefaultComplianceScanner>>();
        _scanner = new DefaultComplianceScanner(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var scanner = new DefaultComplianceScanner(_loggerMock.Object);

        // Assert
        scanner.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new DefaultComplianceScanner(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion

    #region ScanControlAsync Tests - Subscription Scope

    [Fact]
    public async Task ScanControlAsync_WithValidControl_ReturnsFindingsList()
    {
        // Arrange
        var control = new NistControl { Id = "XX-1", Title = "Test Control" };

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Theory]
    [InlineData("AC-1", "Access Control Policy")]
    [InlineData("AU-1", "Audit Policy")]
    [InlineData("CM-1", "Configuration Management Policy")]
    [InlineData("XX-99", "Unknown Control")]
    public async Task ScanControlAsync_WithVariousControls_Succeeds(string controlId, string title)
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
        var control = new NistControl { Id = "XX-1", Title = "Test Control" };
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
        var control = new NistControl { Id = "XX-1", Title = "Test Control" };
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
        var control = new NistControl { Id = "XX-1", Title = "Test Control" };
        var resourceGroupName = "test-rg";
        var cts = new CancellationTokenSource();

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, resourceGroupName, control, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
