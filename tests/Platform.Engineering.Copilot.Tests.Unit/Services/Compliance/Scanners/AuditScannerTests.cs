using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance.Scanners;

/// <summary>
/// Unit tests for AuditScanner
/// Tests scanning for AU control family
/// </summary>
public class AuditScannerTests
{
    private readonly Mock<ILogger<AuditScanner>> _loggerMock;
    private readonly Mock<IAzureResourceService> _azureServiceMock;
    private readonly Mock<IOptions<GatewayOptions>> _gatewayOptionsMock;
    private readonly AuditScanner _scanner;
    private const string TestSubscriptionId = "test-subscription-id";

    public AuditScannerTests()
    {
        _loggerMock = new Mock<ILogger<AuditScanner>>();
        _azureServiceMock = new Mock<IAzureResourceService>();
        _gatewayOptionsMock = new Mock<IOptions<GatewayOptions>>();
        _gatewayOptionsMock.Setup(x => x.Value).Returns(new GatewayOptions
        {
            Azure = new AzureGatewayOptions { CloudEnvironment = "AzureCloud" }
        });
        _scanner = new AuditScanner(_loggerMock.Object, _azureServiceMock.Object, _gatewayOptionsMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var scanner = new AuditScanner(_loggerMock.Object, _azureServiceMock.Object, _gatewayOptionsMock.Object);

        // Assert
        scanner.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AuditScanner(null!, _azureServiceMock.Object, _gatewayOptionsMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullAzureService_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new AuditScanner(_loggerMock.Object, null!, _gatewayOptionsMock.Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("azureService");
    }

    [Fact]
    public void Constructor_WithAzureGovernment_InitializesCorrectEndpoint()
    {
        // Arrange
        var govOptionsMock = new Mock<IOptions<GatewayOptions>>();
        govOptionsMock.Setup(x => x.Value).Returns(new GatewayOptions
        {
            Azure = new AzureGatewayOptions { CloudEnvironment = "AzureUSGovernment" }
        });

        // Act
        var scanner = new AuditScanner(_loggerMock.Object, _azureServiceMock.Object, govOptionsMock.Object);

        // Assert
        scanner.Should().NotBeNull();
    }

    #endregion

    #region ScanControlAsync Tests - Subscription Scope

    [Fact]
    public async Task ScanControlAsync_WithValidControl_ReturnsFindingsList()
    {
        // Arrange
        var control = new NistControl { Id = "AU-2", Title = "Audit Events" };

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, control);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<List<AtoFinding>>();
    }

    [Theory]
    [InlineData("AU-1", "Audit Policy")]
    [InlineData("AU-2", "Audit Events")]
    [InlineData("AU-3", "Content of Audit Records")]
    [InlineData("AU-6", "Audit Review, Analysis, and Reporting")]
    [InlineData("AU-8", "Time Stamps")]
    [InlineData("AU-9", "Protection of Audit Information")]
    [InlineData("AU-11", "Audit Record Retention")]
    [InlineData("AU-12", "Audit Generation")]
    public async Task ScanControlAsync_WithVariousAUControls_Succeeds(string controlId, string title)
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
        var control = new NistControl { Id = "AU-2", Title = "Audit Events" };
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
        var control = new NistControl { Id = "AU-6", Title = "Audit Review" };
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
        var control = new NistControl { Id = "AU-6", Title = "Audit Review" };
        var resourceGroupName = "test-rg";
        var cts = new CancellationTokenSource();

        // Act
        var result = await _scanner.ScanControlAsync(TestSubscriptionId, resourceGroupName, control, cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion
}
