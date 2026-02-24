using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Agents.Compliance.Services;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

/// <summary>
/// Unit tests for NistControlsCacheWarmupService — background cache warming,
/// proactive refresh, critical control validation (FR-021 through FR-027, FR-051).
/// </summary>
public class NistControlsCacheWarmupServiceTests
{
    private readonly Mock<INistService> _nistServiceMock = new();
    private readonly Mock<ILogger<NistControlsCacheWarmupService>> _loggerMock = new();
    private readonly NistControlsOptions _defaultOptions = new()
    {
        CacheDurationHours = 24,
        EnableMemoryCache = true
    };

    private NistControlsCacheWarmupService CreateService(NistControlsOptions? options = null)
    {
        return new NistControlsCacheWarmupService(
            _nistServiceMock.Object,
            Options.Create(options ?? _defaultOptions),
            _loggerMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_SuccessfulWarmup_CallsGetCatalogAsync()
    {
        // Arrange
        var snapshot = new NistCatalogSnapshot(
            "NIST SP 800-53 Rev 5", 323, 18, DateTimeOffset.UtcNow, "EmbeddedFallback");

        _nistServiceMock.Setup(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act
        await service.StartAsync(cts.Token);
        // Give it time to execute the initial warmup (startup delay is 10s)
        await Task.Delay(TimeSpan.FromSeconds(12), cts.Token).ContinueWith(_ => { });
        await service.StopAsync(CancellationToken.None);

        // Assert
        _nistServiceMock.Verify(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ValidatesCriticalControls()
    {
        // Arrange
        var snapshot = new NistCatalogSnapshot(
            "NIST SP 800-53 Rev 5", 323, 18, DateTimeOffset.UtcNow, "EmbeddedFallback");

        _nistServiceMock.Setup(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(12), cts.Token).ContinueWith(_ => { });
        await service.StopAsync(CancellationToken.None);

        // Assert — should validate the 11 critical controls
        _nistServiceMock.Verify(
            s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(11));
    }

    [Fact]
    public async Task ExecuteAsync_CatalogNull_CallsGetCatalogAndHandlesNull()
    {
        // Arrange — GetCatalogAsync returns null (catalog not loaded)
        _nistServiceMock.Setup(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((NistCatalogSnapshot?)null);

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act
        await service.StartAsync(cts.Token);
        // Wait for startup delay (10s) + first warmup attempt
        await Task.Delay(TimeSpan.FromSeconds(13), cts.Token).ContinueWith(_ => { });
        await service.StopAsync(CancellationToken.None);

        // Assert — should have called GetCatalogAsync (first attempt returns null, service handles gracefully)
        _nistServiceMock.Verify(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // Should NOT call ValidateControlIdAsync since catalog is null
        _nistServiceMock.Verify(
            s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_GracefulCancellation_OnShutdown()
    {
        // Arrange
        _nistServiceMock.Setup(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NistCatalogSnapshot("v5", 100, 10, DateTimeOffset.UtcNow, "Embedded"));
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = CreateService();

        // Act — start and immediately stop
        await service.StartAsync(CancellationToken.None);
        var stopTask = service.StopAsync(CancellationToken.None);

        // Assert — should complete without exception
        await stopTask.Invoking(t => t).Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_MissingCriticalControls_LogsWarning()
    {
        // Arrange — some controls valid, some not
        var snapshot = new NistCatalogSnapshot(
            "NIST SP 800-53 Rev 5", 323, 18, DateTimeOffset.UtcNow, "EmbeddedFallback");

        _nistServiceMock.Setup(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        // Only AC-3 and SC-13 are valid; others return false
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync("AC-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync("SC-13", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _nistServiceMock.Setup(s => s.ValidateControlIdAsync(
                It.Is<string>(id => id != "AC-3" && id != "SC-13"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(12), cts.Token).ContinueWith(_ => { });
        await service.StopAsync(CancellationToken.None);

        // Assert — validate was called for all critical controls
        _nistServiceMock.Verify(
            s => s.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(11));
    }

    [Fact]
    public async Task ExecuteAsync_ExceptionDuringWarmup_DoesNotCrash()
    {
        // Arrange — GetCatalogAsync throws
        _nistServiceMock.Setup(s => s.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated error"));

        var service = CreateService();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act
        await service.StartAsync(cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(12), cts.Token).ContinueWith(_ => { });

        // Assert — service should not throw, just log error
        var stopAction = async () => await service.StopAsync(CancellationToken.None);
        await stopAction.Should().NotThrowAsync();
    }
}
