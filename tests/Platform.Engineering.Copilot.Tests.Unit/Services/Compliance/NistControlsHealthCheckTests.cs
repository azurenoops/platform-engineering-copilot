using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Compliance;

/// <summary>
/// Unit tests for NistControlsHealthCheck
/// Tests health check logic for NIST controls service availability
/// </summary>
public class NistControlsHealthCheckTests
{
    private readonly Mock<INistControlsService> _nistControlsServiceMock;
    private readonly Mock<ILogger<NistControlsHealthCheck>> _loggerMock;
    private readonly NistControlsOptions _options;

    public NistControlsHealthCheckTests()
    {
        _nistControlsServiceMock = new Mock<INistControlsService>();
        _loggerMock = new Mock<ILogger<NistControlsHealthCheck>>();
        _options = new NistControlsOptions
        {
            CacheDurationHours = 24,
            EnableOfflineFallback = true
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var options = Options.Create(_options);

        // Act
        var healthCheck = new NistControlsHealthCheck(
            _nistControlsServiceMock.Object,
            _loggerMock.Object,
            options);

        // Assert
        healthCheck.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullNistControlsService_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_options);

        // Act
        var act = () => new NistControlsHealthCheck(
            null!,
            _loggerMock.Object,
            options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("nistControlsService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(_options);

        // Act
        var act = () => new NistControlsHealthCheck(
            _nistControlsServiceMock.Object,
            null!,
            options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new NistControlsHealthCheck(
            _nistControlsServiceMock.Object,
            _loggerMock.Object,
            null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    #endregion

    #region CheckHealthAsync Tests - Healthy

    [Fact]
    public async Task CheckHealthAsync_WhenServiceFullyOperational_ReturnsHealthy()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1.1");

        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();
        var context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("nist", healthCheck, null, null)
        };

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("fully operational");
        result.Data.Should().ContainKey("version");
        result.Data["version"].Should().Be("5.1.1");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenAllControlsValid_IncludesControlCount()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1.1");

        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Data.Should().ContainKey("valid_test_controls");
        result.Data["valid_test_controls"].Should().Be("3/3");
    }

    #endregion

    #region CheckHealthAsync Tests - Degraded

    [Fact]
    public async Task CheckHealthAsync_WhenVersionUnknown_ReturnsDegraded()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Unknown");

        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("version information is unavailable");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenVersionEmpty_ReturnsDegraded()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(string.Empty);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenSomeControlsInvalid_ReturnsDegraded()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1.1");

        // Only first control is valid
        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync("AC-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync("SC-13", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync("AU-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("partially operational");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenTimedOut_ReturnsDegraded()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("timed out");
    }

    #endregion

    #region CheckHealthAsync Tests - Unhealthy

    [Fact]
    public async Task CheckHealthAsync_WhenNoControlsValid_ReturnsUnhealthy()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1.1");

        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not operational");
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_ReturnsUnhealthy()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("failed");
        result.Exception.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenExceptionThrown_IncludesErrorInData()
    {
        // Arrange
        var exception = new HttpRequestException("Network error");
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Data.Should().ContainKey("error");
        result.Data["error"].Should().Be("Network error");
        result.Data.Should().ContainKey("timestamp");
    }

    #endregion

    #region CheckHealthAsync Tests - Data Validation

    [Fact]
    public async Task CheckHealthAsync_IncludesResponseTime()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1.1");

        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Data.Should().ContainKey("response_time_ms");
        ((double)result.Data["response_time_ms"]).Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesTimestamp()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1.1");

        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Data.Should().ContainKey("timestamp");
        ((DateTime)result.Data["timestamp"]).Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesCacheDuration()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1.1");

        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Data.Should().ContainKey("cache_duration_hours");
        result.Data["cache_duration_hours"].Should().Be(24);
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesOfflineFallbackStatus()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1.1");

        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Data.Should().ContainKey("offline_fallback_enabled");
        result.Data["offline_fallback_enabled"].Should().Be(true);
    }

    #endregion

    #region CheckHealthAsync Tests - Control Validation Errors

    [Fact]
    public async Task CheckHealthAsync_WhenControlValidationThrows_ContinuesWithOtherControls()
    {
        // Arrange
        _nistControlsServiceMock
            .Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1.1");

        // First control throws, others succeed
        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync("AC-3", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Validation failed"));
        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync("SC-13", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _nistControlsServiceMock
            .Setup(s => s.ValidateControlIdAsync("AU-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();
        var context = CreateHealthCheckContext(healthCheck);

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert - should still process other controls
        result.Data.Should().ContainKey("valid_test_controls");
        // 2 out of 3 should be valid
        result.Data["valid_test_controls"].Should().Be("2/3");
    }

    #endregion

    #region Helper Methods

    private NistControlsHealthCheck CreateHealthCheck()
    {
        var options = Options.Create(_options);
        return new NistControlsHealthCheck(
            _nistControlsServiceMock.Object,
            _loggerMock.Object,
            options);
    }

    private static HealthCheckContext CreateHealthCheckContext(IHealthCheck healthCheck)
    {
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration("nist-controls", healthCheck, null, null)
        };
    }

    #endregion
}
