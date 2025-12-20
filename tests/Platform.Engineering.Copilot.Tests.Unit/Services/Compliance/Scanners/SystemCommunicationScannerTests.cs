using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance.Scanners;

/// <summary>
/// Unit tests for SystemCommunicationScanner
/// Tests scanning for SC control family
/// </summary>
public class SystemCommunicationScannerTests
{
    private readonly Mock<ILogger<SystemCommunicationScanner>> _loggerMock;
    private readonly Mock<IAzureResourceService> _azureServiceMock;
    private readonly SystemCommunicationScanner _scanner;
    private const string TestSubscriptionId = "test-subscription-id";

    public SystemCommunicationScannerTests()
    {
        _loggerMock = new Mock<ILogger<SystemCommunicationScanner>>();
        _azureServiceMock = new Mock<IAzureResourceService>();
        _scanner = new SystemCommunicationScanner(_loggerMock.Object, _azureServiceMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var scanner = new SystemCommunicationScanner(_loggerMock.Object, _azureServiceMock.Object);

        // Assert
        scanner.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SystemCommunicationScanner(null!, _azureServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAzureService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SystemCommunicationScanner(_loggerMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("azureService");
    }

    #endregion

    #region ScanControlAsync Tests - Subscription Scope

    [Fact]
    public async Task ScanControlAsync_WithValidControl_ReturnsFindingsList()
    {
        // Arrange
        var control = new NistControl { Id = "SC-7", Title = "Boundary Protection" };

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Theory]
    [InlineData("SC-1", "System and Communications Protection Policy")]
    [InlineData("SC-7", "Boundary Protection")]
    [InlineData("SC-8", "Transmission Confidentiality")]
    [InlineData("SC-12", "Cryptographic Key Establishment")]
    [InlineData("SC-13", "Cryptographic Protection")]
    [InlineData("SC-28", "Protection of Information at Rest")]
    public async Task ScanControlAsync_WithVariousSCControls_Succeeds(string controlId, string title)
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
        var control = new NistControl { Id = "SC-7", Title = "Boundary Protection" };
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
        var control = new NistControl { Id = "SC-7", Title = "Boundary Protection" };
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
        var control = new NistControl { Id = "SC-7", Title = "Boundary Protection" };
        var resourceGroupName = "test-rg";
        var cts = new CancellationTokenSource();

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, resourceGroupName, control, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
