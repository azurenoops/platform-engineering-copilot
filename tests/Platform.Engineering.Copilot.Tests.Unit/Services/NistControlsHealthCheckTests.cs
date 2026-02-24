using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Observability;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for NistControlsHealthCheck — health status reporting
/// for Kubernetes liveness probes (FR-028 through FR-033, FR-052).
/// </summary>
public class NistControlsHealthCheckTests
{
    private readonly Mock<INistService> _nistServiceMock = new();
    private readonly Mock<ILogger<NistControlsHealthCheck>> _loggerMock = new();
    private readonly NistControlsOptions _defaultOptions = new()
    {
        CacheDurationHours = 24,
        EnableOfflineFallback = true
    };

    private NistControlsHealthCheck CreateHealthCheck(NistControlsOptions? options = null)
    {
        return new NistControlsHealthCheck(
            _nistServiceMock.Object,
            Options.Create(options ?? _defaultOptions),
            _loggerMock.Object);
    }

    private HealthCheckContext CreateContext()
    {
        return new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                "nist-controls",
                CreateHealthCheck(),
                null,
                ["nist", "ready"])
        };
    }

    [Fact]
    public async Task CheckHealthAsync_AllControlsValid_ReturnsHealthy()
    {
        // Arrange
        _nistServiceMock.Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NIST SP 800-53 Rev 5");
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("healthy");
        result.Data.Should().ContainKey("version");
        result.Data.Should().ContainKey("validControlCount");
        result.Data.Should().ContainKey("responseTimeMs");
        result.Data.Should().ContainKey("timestamp");
        result.Data["validControlCount"].Should().Be(3);
    }

    [Fact]
    public async Task CheckHealthAsync_PartialControlsValid_ReturnsDegraded()
    {
        // Arrange — only AC-3 is valid
        _nistServiceMock.Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NIST SP 800-53 Rev 5");
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync("AC-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(
                It.Is<string>(id => id != "AC-3"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var healthCheck = CreateHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Data["validControlCount"].Should().Be(1);
    }

    [Fact]
    public async Task CheckHealthAsync_VersionUnknown_ReturnsDegraded()
    {
        // Arrange — all controls valid but version is unknown
        _nistServiceMock.Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Unknown");
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("unknown");
    }

    [Fact]
    public async Task CheckHealthAsync_NoControlsValid_ReturnsUnhealthy()
    {
        // Arrange — no controls validate
        _nistServiceMock.Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NIST SP 800-53 Rev 5");
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var healthCheck = CreateHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Data["validControlCount"].Should().Be(0);
    }

    [Fact]
    public async Task CheckHealthAsync_ExceptionThrown_ReturnsUnhealthy()
    {
        // Arrange — service throws exception
        _nistServiceMock.Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        var healthCheck = CreateHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("exception");
        result.Data.Should().ContainKey("error");
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesStructuredData()
    {
        // Arrange
        _nistServiceMock.Setup(s => s.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("NIST SP 800-53 Rev 5");
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var healthCheck = CreateHealthCheck();

        // Act
        var result = await healthCheck.CheckHealthAsync(CreateContext());

        // Assert — all structured data fields present
        result.Data.Should().ContainKey("version");
        result.Data.Should().ContainKey("validControlCount");
        result.Data.Should().ContainKey("responseTimeMs");
        result.Data.Should().ContainKey("timestamp");
        result.Data.Should().ContainKey("cacheDurationHours");
        result.Data.Should().ContainKey("offlineFallbackEnabled");

        result.Data["cacheDurationHours"].Should().Be(24);
        result.Data["offlineFallbackEnabled"].Should().Be(true);
    }
}
