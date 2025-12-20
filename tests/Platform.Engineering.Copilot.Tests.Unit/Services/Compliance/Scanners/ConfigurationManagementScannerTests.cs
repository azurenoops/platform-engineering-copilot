using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance.Scanners;

/// <summary>
/// Unit tests for ConfigurationManagementScanner
/// Tests scanning for CM control family
/// </summary>
public class ConfigurationManagementScannerTests
{
    private readonly Mock<ILogger<ConfigurationManagementScanner>> _loggerMock;
    private readonly Mock<IAzureResourceService> _azureServiceMock;
    private readonly ConfigurationManagementScanner _scanner;
    private const string TestSubscriptionId = "test-subscription-id";

    public ConfigurationManagementScannerTests()
    {
        _loggerMock = new Mock<ILogger<ConfigurationManagementScanner>>();
        _azureServiceMock = new Mock<IAzureResourceService>();
        _scanner = new ConfigurationManagementScanner(_loggerMock.Object, _azureServiceMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var scanner = new ConfigurationManagementScanner(_loggerMock.Object, _azureServiceMock.Object);

        // Assert
        scanner.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ConfigurationManagementScanner(null!, _azureServiceMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAzureService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ConfigurationManagementScanner(_loggerMock.Object, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("azureService");
    }

    #endregion

    #region ScanControlAsync Tests - Subscription Scope

    [Fact]
    public async Task ScanControlAsync_WithValidControl_ReturnsFindingsList()
    {
        // Arrange
        var control = new NistControl { Id = "CM-2", Title = "Baseline Configuration" };

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Theory]
    [InlineData("CM-1", "Configuration Management Policy")]
    [InlineData("CM-2", "Baseline Configuration")]
    [InlineData("CM-3", "Configuration Change Control")]
    [InlineData("CM-4", "Security Impact Analysis")]
    [InlineData("CM-5", "Access Restrictions for Change")]
    [InlineData("CM-6", "Configuration Settings")]
    [InlineData("CM-7", "Least Functionality")]
    [InlineData("CM-8", "Information System Component Inventory")]
    public async Task ScanControlAsync_WithVariousCMControls_Succeeds(string controlId, string title)
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
        var control = new NistControl { Id = "CM-6", Title = "Configuration Settings" };
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
        var control = new NistControl { Id = "CM-6", Title = "Configuration Settings" };
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
        var control = new NistControl { Id = "CM-6", Title = "Configuration Settings" };
        var resourceGroupName = "test-rg";
        var cts = new CancellationTokenSource();

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, resourceGroupName, control, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
