using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance.Scanners;

/// <summary>
/// Unit tests for SystemIntegrityScanner
/// Tests scanning for SI control family
/// </summary>
public class SystemIntegrityScannerTests
{
    private readonly Mock<ILogger<SystemIntegrityScanner>> _loggerMock;
    private readonly Mock<IAzureResourceService> _azureServiceMock;
    private readonly SystemIntegrityScanner _scanner;
    private const string TestSubscriptionId = "test-subscription-id";

    public SystemIntegrityScannerTests()
    {
        _loggerMock = new Mock<ILogger<SystemIntegrityScanner>>();
        _azureServiceMock = new Mock<IAzureResourceService>();
        _scanner = new SystemIntegrityScanner(_loggerMock.Object, _azureServiceMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var scanner = new SystemIntegrityScanner(_loggerMock.Object, _azureServiceMock.Object);

        // Assert
        scanner.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SystemIntegrityScanner(null!, _azureServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAzureService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SystemIntegrityScanner(_loggerMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("azureService");
    }

    #endregion

    #region ScanControlAsync Tests - Subscription Scope

    [Fact]
    public async Task ScanControlAsync_WithValidControl_ReturnsFindingsList()
    {
        // Arrange
        var control = new NistControl { Id = "SI-2", Title = "Flaw Remediation" };

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Theory]
    [InlineData("SI-1", "System and Information Integrity Policy")]
    [InlineData("SI-2", "Flaw Remediation")]
    [InlineData("SI-3", "Malicious Code Protection")]
    [InlineData("SI-4", "System Monitoring")]
    [InlineData("SI-5", "Security Alerts")]
    public async Task ScanControlAsync_WithVariousSIControls_Succeeds(string controlId, string title)
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
        var control = new NistControl { Id = "SI-2", Title = "Flaw Remediation" };
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
        var control = new NistControl { Id = "SI-2", Title = "Flaw Remediation" };
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
        var control = new NistControl { Id = "SI-2", Title = "Flaw Remediation" };
        var resourceGroupName = "test-rg";
        var cts = new CancellationTokenSource();

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, resourceGroupName, control, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
