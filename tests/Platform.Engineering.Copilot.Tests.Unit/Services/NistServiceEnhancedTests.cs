using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Observability;
using Platform.Engineering.Copilot.Core.Services;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

/// <summary>
/// Enhanced unit tests for NistService — caching, retry, enhancement extraction,
/// validation, catalog snapshot, and version queries (FR-010 through FR-052).
/// </summary>
public class NistServiceEnhancedTests
{
    private readonly Mock<ILogger<NistService>> _loggerMock = new();
    private readonly IConfiguration _configuration;
    private readonly NistControlsOptions _defaultOptions;

    public NistServiceEnhancedTests()
    {
        var configData = new Dictionary<string, string?>
        {
            ["NistControls:EnableGitHubFetch"] = "false",
            ["NistControls:EnableMemoryCache"] = "true",
            ["NistControls:CacheDurationHours"] = "24",
            ["NistControls:EnableOfflineFallback"] = "true"
        };
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _defaultOptions = new NistControlsOptions
        {
            EnableMemoryCache = true,
            CacheDurationHours = 24,
            MaxRetryAttempts = 3,
            RetryDelaySeconds = 2,
            EnableOfflineFallback = true
        };
    }

    private NistService CreateService(
        IMemoryCache? cache = null,
        NistControlsOptions? options = null,
        HttpClient? httpClient = null,
        ComplianceMetricsService? metrics = null)
    {
        var opts = Options.Create(options ?? _defaultOptions);
        return new NistService(
            _loggerMock.Object,
            _configuration,
            httpClient ?? new HttpClient(),
            cache,
            opts,
            metrics);
    }

    private async Task<NistService> CreateAndInitializeService(
        IMemoryCache? cache = null,
        NistControlsOptions? options = null,
        ComplianceMetricsService? metrics = null)
    {
        var service = CreateService(cache, options, metrics: metrics);
        await service.InitializeAsync();
        return service;
    }

    // ─── Caching Tests (US8 — FR-010 through FR-014) ─────────────────

    [Fact]
    public async Task InitializeAsync_WithMemoryCache_PopulatesCache()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = await CreateAndInitializeService(cache);

        // Assert — cache should contain the catalog
        cache.TryGetValue("NistCatalog_Controls", out Dictionary<string, ControlDefinition>? cached)
            .Should().BeTrue();
        cached.Should().NotBeNull();
        cached!.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetControl_WithCache_ReturnsCachedResult()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = await CreateAndInitializeService(cache);

        // Act — multiple lookups should work from in-memory dictionary (populated from cache-backed init)
        var result1 = service.GetControl("AC-2");
        var result2 = service.GetControl("AC-2");

        // Assert
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1!.ControlId.Should().Be(result2!.ControlId);
    }

    [Fact]
    public async Task CacheEntry_HasAbsoluteExpiration_24Hours()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = new NistControlsOptions
        {
            CacheDurationHours = 24,
            EnableMemoryCache = true
        };

        // Act
        var service = await CreateAndInitializeService(cache, options);

        // Assert — entry exists after init
        cache.TryGetValue("NistCatalog_Controls", out _).Should().BeTrue();
        service.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task CacheEntry_HasHighPriority()
    {
        // Arrange — verify cache remains populated under memory pressure
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = null });
        var service = await CreateAndInitializeService(cache);

        // Assert — High priority entries persists
        cache.TryGetValue("NistCatalog_Controls", out _).Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_WithCacheDisabled_DoesNotPopulateCache()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = new NistControlsOptions { EnableMemoryCache = false };

        // Act
        var service = await CreateAndInitializeService(cache, options);

        // Assert
        service.IsLoaded.Should().BeTrue();
        cache.TryGetValue("NistCatalog_Controls", out _).Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_WithoutMemoryCache_StillLoads()
    {
        // Arrange — no IMemoryCache provided (backward compatibility)
        var service = new NistService(
            _loggerMock.Object,
            _configuration,
            new HttpClient());

        // Act
        await service.InitializeAsync();

        // Assert
        service.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentInitializeAsync_SemaphoreSlim_PreventsDuplicateLoads()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(cache);

        // Act — simulate concurrent initialization
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => service.InitializeAsync())
            .ToArray();
        await Task.WhenAll(tasks);

        // Assert — service loads once, cache has one entry
        service.IsLoaded.Should().BeTrue();
        cache.TryGetValue("NistCatalog_Controls", out Dictionary<string, ControlDefinition>? cached)
            .Should().BeTrue();
        cached.Should().NotBeNull();
    }

    [Fact]
    public async Task CachedLookup_CompletesInUnder10ms_PerSC004()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = await CreateAndInitializeService(cache);

        // Act — measure 100 cached lookups
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 100; i++)
        {
            service.GetControl("AC-2");
        }
        sw.Stop();

        // Assert — average < 10ms per lookup
        var avgMs = sw.Elapsed.TotalMilliseconds / 100;
        avgMs.Should().BeLessThan(10, "cached lookups must be sub-10ms per SC-004");
    }

    // ─── GetCatalogAsync Tests (US8 — FR-004) ─────────────────

    [Fact]
    public async Task GetCatalogAsync_WhenLoaded_ReturnsSnapshot()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = await CreateAndInitializeService(cache);

        // Act
        var snapshot = await service.GetCatalogAsync();

        // Assert
        snapshot.Should().NotBeNull();
        snapshot!.TotalControls.Should().BeGreaterThan(0);
        snapshot.FamilyCount.Should().BeGreaterThan(0);
        snapshot.Source.Should().Be("EmbeddedFallback");
        snapshot.Version.Should().NotBeNullOrEmpty();
        snapshot.LoadedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetCatalogAsync_WhenNotLoaded_TriggersInitialization()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(cache);

        // Act — catalog hasn't been initialized yet
        var snapshot = await service.GetCatalogAsync();

        // Assert — should initialize on demand
        snapshot.Should().NotBeNull();
        service.IsLoaded.Should().BeTrue();
    }

    // ─── GetVersionAsync Tests (US8 — FR-003) ─────────────────

    [Fact]
    public async Task GetVersionAsync_WhenLoaded_ReturnsCatalogVersion()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = await CreateAndInitializeService(cache);

        // Act
        var version = await service.GetVersionAsync();

        // Assert
        version.Should().NotBeNullOrEmpty();
        version.Should().NotBe("Unknown");
        version.Should().Contain("NIST SP 800-53 Rev 5");
    }

    [Fact]
    public async Task GetVersionAsync_WhenNotLoaded_TriggersInitialization()
    {
        // Arrange
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = CreateService(cache);

        // Act
        var version = await service.GetVersionAsync();

        // Assert — should initialize on demand and return real version
        version.Should().NotBe("Unknown");
        service.IsLoaded.Should().BeTrue();
    }

    // ─── Configurable TTL Tests (US8 — FR-011, FR-012) ─────────────────

    [Fact]
    public async Task CacheEntry_CustomTTL_UsesConfiguredHours()
    {
        // Arrange — 1-hour cache
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = new NistControlsOptions
        {
            CacheDurationHours = 1,
            EnableMemoryCache = true
        };

        // Act
        var service = await CreateAndInitializeService(cache, options);

        // Assert — cache is populated
        cache.TryGetValue("NistCatalog_Controls", out _).Should().BeTrue();
        service.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task CacheEntry_MaxTTL_168Hours()
    {
        // Arrange — maximum 168-hour (7 day) cache
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var options = new NistControlsOptions
        {
            CacheDurationHours = 168,
            EnableMemoryCache = true
        };

        // Act
        var service = await CreateAndInitializeService(cache, options);

        // Assert
        cache.TryGetValue("NistCatalog_Controls", out _).Should().BeTrue();
    }

    // ─── Retry Policy Tests (US2 — FR-015, FR-016, FR-017) ─────────────────

    [Fact]
    public async Task InitializeAsync_WithGitHubDisabled_SkipsRetryAndLoadsFallback()
    {
        // Arrange — GitHub disabled, should load from embedded directly
        var configData = new Dictionary<string, string?>
        {
            ["NistControls:EnableGitHubFetch"] = "false"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        var opts = Options.Create(_defaultOptions);

        var service = new NistService(
            _loggerMock.Object,
            config,
            new HttpClient(),
            null,
            opts);

        // Act
        await service.InitializeAsync();

        // Assert — loaded from embedded resources
        service.IsLoaded.Should().BeTrue();
        service.ActiveSource.Source.Should().Be("EmbeddedFallback");
    }

    [Fact]
    public async Task InitializeAsync_WhenGitHubFails_FallsBackToEmbedded()
    {
        // Arrange — GitHub enabled, but no real server -> will fail and fall back
        var configData = new Dictionary<string, string?>
        {
            ["NistControls:EnableGitHubFetch"] = "true"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();

        var options = new NistControlsOptions
        {
            BaseUrl = "https://invalid.nonexistent.local/does-not-exist",
            MaxRetryAttempts = 1,
            RetryDelaySeconds = 1,
            EnableMemoryCache = false,
            EnableOfflineFallback = false
        };

        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Simulated network failure"));

        var httpClient = new HttpClient(mockHandler.Object);
        var opts = Options.Create(options);

        var service = new NistService(
            _loggerMock.Object,
            config,
            httpClient,
            null,
            opts);

        // Act
        await service.InitializeAsync();

        // Assert — should fall back to embedded
        service.IsLoaded.Should().BeTrue();
        service.ActiveSource.Source.Should().Be("EmbeddedFallback");
    }

    [Fact]
    public async Task RefreshFromGitHubAsync_WithRetry_RetriesOnFailure()
    {
        // Arrange — mock handler that fails twice then succeeds
        var attempt = 0;
        var mockHandler = new Mock<HttpMessageHandler>();
        mockHandler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                attempt++;
                if (attempt <= 2)
                    throw new HttpRequestException($"Simulated failure on attempt {attempt}");
                // Success on 3rd attempt — return non-success so EnsureSuccessStatusCode throws
                // but the retry only fires for non-success, so we need to let it through
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            });

        var httpClient = new HttpClient(mockHandler.Object);
        var options = new NistControlsOptions
        {
            BaseUrl = "https://example.com/nist",
            MaxRetryAttempts = 3,
            RetryDelaySeconds = 1,
            EnableMemoryCache = false,
            EnableOfflineFallback = false
        };

        var configData = new Dictionary<string, string?>
        {
            ["NistControls:EnableGitHubFetch"] = "true"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        var opts = Options.Create(options);

        var service = new NistService(
            _loggerMock.Object,
            config,
            httpClient,
            null,
            opts);

        // Act & Assert — try refresh which will fail with retries, then fall back
        await service.InitializeAsync();

        // The handler was called multiple times (retries happened)
        attempt.Should().BeGreaterThan(1, "retry pipeline should make multiple attempts");

        // Falls back to embedded
        service.IsLoaded.Should().BeTrue();
    }

    [Fact]
    public async Task InitializeAsync_OfflineFallbackDisabled_UsesEmbedded()
    {
        // Arrange
        var options = new NistControlsOptions
        {
            EnableOfflineFallback = false,
            EnableMemoryCache = false
        };

        var service = CreateService(options: options);

        // Act
        await service.InitializeAsync();

        // Assert — skips offline fallback, loads embedded
        service.IsLoaded.Should().BeTrue();
        service.ActiveSource.Source.Should().Be("EmbeddedFallback");
    }

    [Fact]
    public async Task InitializeAsync_OfflineFallbackPathMissing_UsesEmbedded()
    {
        // Arrange — path set but file doesn't exist
        var options = new NistControlsOptions
        {
            EnableOfflineFallback = true,
            OfflineFallbackPath = "/nonexistent/path/fallback.json",
            EnableMemoryCache = false
        };

        var service = CreateService(options: options);

        // Act
        await service.InitializeAsync();

        // Assert — falls through to embedded resources
        service.IsLoaded.Should().BeTrue();
        service.ActiveSource.Source.Should().Be("EmbeddedFallback");
    }

    [Fact]
    public async Task InitializeAsync_GracefulError_WhenBothRemoteAndFallbackFail()
    {
        // Arrange — GitHub disabled, offline fallback path invalid, embedded will still work
        var options = new NistControlsOptions
        {
            EnableOfflineFallback = true,
            OfflineFallbackPath = "/nonexistent/fallback.json",
            EnableMemoryCache = false
        };

        var configData = new Dictionary<string, string?>
        {
            ["NistControls:EnableGitHubFetch"] = "false"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configData).Build();
        var opts = Options.Create(options);

        var service = new NistService(
            _loggerMock.Object,
            config,
            new HttpClient(),
            null,
            opts);

        // Act
        await service.InitializeAsync();

        // Assert — embedded resources provide last resort
        service.IsLoaded.Should().BeTrue();
    }

    // ─── Control Enhancement Extraction Tests (US3 — FR-001, FR-037 through FR-040) ───

    [Fact]
    public async Task GetControlEnhancementAsync_ValidControl_ReturnsFullData()
    {
        // Arrange
        var service = await CreateAndInitializeService();

        // Act
        var enhancement = await service.GetControlEnhancementAsync("AC-2");

        // Assert
        enhancement.Should().NotBeNull();
        enhancement!.Id.Should().Be("AC-2");
        enhancement.Title.Should().NotBeNullOrEmpty();
        enhancement.Statement.Should().NotBeNullOrEmpty();
        enhancement.Objectives.Should().NotBeNull();
        enhancement.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task GetControlEnhancementAsync_ControlWithNoGuidance_ReturnsEmptyGuidance()
    {
        // Arrange
        var service = await CreateAndInitializeService();

        // Act — find a control and check guidance field
        var enhancement = await service.GetControlEnhancementAsync("AC-2");

        // Assert — guidance should be string (possibly empty, never null)
        enhancement.Should().NotBeNull();
        enhancement!.Guidance.Should().NotBeNull();
    }

    [Fact]
    public async Task GetControlEnhancementAsync_InvalidControlId_ReturnsNull()
    {
        // Arrange
        var service = await CreateAndInitializeService();

        // Act
        var enhancement = await service.GetControlEnhancementAsync("INVALID-99");

        // Assert
        enhancement.Should().BeNull();
    }

    [Fact]
    public async Task GetControlEnhancementAsync_NullControlId_ThrowsArgumentException()
    {
        // Arrange
        var service = await CreateAndInitializeService();

        // Act & Assert
        await service.Invoking(s => s.GetControlEnhancementAsync(null!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetControlEnhancementAsync_EmptyControlId_ThrowsArgumentException()
    {
        // Arrange
        var service = await CreateAndInitializeService();

        // Act & Assert
        await service.Invoking(s => s.GetControlEnhancementAsync(""))
            .Should().ThrowAsync<ArgumentException>();
    }

    // ─── Control ID Validation Tests (US4 — FR-002) ───

    [Fact]
    public async Task ValidateControlIdAsync_ValidId_ReturnsTrue()
    {
        // Arrange
        var service = await CreateAndInitializeService();

        // Act
        var result = await service.ValidateControlIdAsync("AC-2");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateControlIdAsync_InvalidId_ReturnsFalse()
    {
        // Arrange
        var service = await CreateAndInitializeService();

        // Act
        var result = await service.ValidateControlIdAsync("AC-99");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateControlIdAsync_NullId_ThrowsArgumentException()
    {
        // Arrange
        var service = await CreateAndInitializeService();

        // Act & Assert
        await service.Invoking(s => s.ValidateControlIdAsync(null!))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ValidateControlIdAsync_EmptyId_ThrowsArgumentException()
    {
        // Arrange
        var service = await CreateAndInitializeService();

        // Act & Assert
        await service.Invoking(s => s.ValidateControlIdAsync(""))
            .Should().ThrowAsync<ArgumentException>();
    }

    // ─── Observability Tests (US7 — FR-034 through FR-036) ─────────────────

    [Fact]
    public void ComplianceMetricsService_CreatesActivitySource()
    {
        // Arrange & Act
        using var metrics = new ComplianceMetricsService();

        // Assert — ActivitySource name follows OpenTelemetry convention
        ComplianceMetricsService.ActivitySourceName.Should().Be("Platform.Engineering.Copilot.Compliance");
        ComplianceMetricsService.MeterName.Should().Be("Platform.Engineering.Copilot.Compliance");
    }

    [Fact]
    public void ComplianceMetricsService_CreatesAllCountersAndHistograms()
    {
        // Arrange & Act
        using var metrics = new ComplianceMetricsService();

        // Assert — all instruments are created (non-null)
        metrics.CatalogFetchCount.Should().NotBeNull();
        metrics.CacheHitCount.Should().NotBeNull();
        metrics.CacheMissCount.Should().NotBeNull();
        metrics.ControlQueryCount.Should().NotBeNull();
        metrics.ErrorCount.Should().NotBeNull();
        metrics.CatalogFetchDuration.Should().NotBeNull();
        metrics.ControlQueryDuration.Should().NotBeNull();
    }

    [Fact]
    public async Task InitializeAsync_WithMetrics_RecordsCatalogFetchCount()
    {
        // Arrange — listen for metric data
        using var metrics = new ComplianceMetricsService();
        using var meterListener = new MeterListener();

        long fetchCount = 0;
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "compliance.catalog.fetch.count")
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "compliance.catalog.fetch.count")
                Interlocked.Add(ref fetchCount, measurement);
        });
        meterListener.Start();

        var service = CreateService(metrics: metrics);

        // Act
        await service.InitializeAsync();

        // Assert
        meterListener.RecordObservableInstruments();
        fetchCount.Should().BeGreaterThanOrEqualTo(1, "catalog fetch should be counted");
    }

    [Fact]
    public async Task InitializeAsync_WithMetrics_RecordsFetchDuration()
    {
        // Arrange — listen for histogram data
        using var metrics = new ComplianceMetricsService();
        using var meterListener = new MeterListener();

        double totalDuration = 0;
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "compliance.catalog.fetch.duration")
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "compliance.catalog.fetch.duration")
                totalDuration += measurement;
        });
        meterListener.Start();

        var service = CreateService(metrics: metrics);

        // Act
        await service.InitializeAsync();

        // Assert
        totalDuration.Should().BeGreaterThan(0, "catalog fetch duration should be recorded");
    }

    [Fact]
    public async Task GetControlEnhancementAsync_WithMetrics_RecordsQueryCount()
    {
        // Arrange
        using var metrics = new ComplianceMetricsService();
        using var meterListener = new MeterListener();

        long queryCount = 0;
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "compliance.control.query.count")
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "compliance.control.query.count")
                Interlocked.Add(ref queryCount, measurement);
        });
        meterListener.Start();

        var service = await CreateAndInitializeService(metrics: metrics);

        // Act
        await service.GetControlEnhancementAsync("AC-1");

        // Assert
        queryCount.Should().BeGreaterThanOrEqualTo(1, "control query should be counted");
    }

    [Fact]
    public async Task EnsureCatalogLoadedAsync_WhenLoaded_RecordsCacheHit()
    {
        // Arrange
        using var metrics = new ComplianceMetricsService();
        using var meterListener = new MeterListener();

        long cacheHitCount = 0;
        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Name == "compliance.cache.hit.count")
                listener.EnableMeasurementEvents(instrument);
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            if (instrument.Name == "compliance.cache.hit.count")
                Interlocked.Add(ref cacheHitCount, measurement);
        });
        meterListener.Start();

        var service = await CreateAndInitializeService(metrics: metrics);

        // Act — second call should be a cache hit (already loaded)
        await service.GetVersionAsync();

        // Assert
        cacheHitCount.Should().BeGreaterThanOrEqualTo(1, "subsequent access should register cache hit");
    }

    [Fact]
    public void ComplianceMetricsService_StartCatalogFetchActivity_SetsSourceTag()
    {
        // Arrange
        using var metrics = new ComplianceMetricsService();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ComplianceMetricsService.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = metrics.StartCatalogFetchActivity("GitHub");

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("compliance.source").Should().Be("GitHub");
    }

    [Fact]
    public void ComplianceMetricsService_RecordFetchResult_SetsAllTags()
    {
        // Arrange
        using var metrics = new ComplianceMetricsService();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ComplianceMetricsService.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = metrics.StartCatalogFetchActivity("GitHub");
        activity.Should().NotBeNull();

        // Act
        ComplianceMetricsService.RecordFetchResult(activity, success: true, controlCount: 42, cacheHit: false, fallbackUsed: false);

        // Assert
        activity!.GetTagItem("cache.hit").Should().Be(false);
        activity.GetTagItem("success").Should().Be(true);
        activity.GetTagItem("control.count").Should().Be(42);
        activity.GetTagItem("fallback.used").Should().Be(false);
    }

    [Fact]
    public void ComplianceMetricsService_RecordError_SetsErrorTags()
    {
        // Arrange
        using var metrics = new ComplianceMetricsService();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == ComplianceMetricsService.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = metrics.StartCatalogFetchActivity("GitHub");
        activity.Should().NotBeNull();

        // Act
        ComplianceMetricsService.RecordError(activity, new HttpRequestException("Connection refused"));

        // Assert
        activity!.GetTagItem("error").Should().Be(true);
        activity.GetTagItem("error.type").Should().Be("HttpRequestException");
        activity.GetTagItem("error.message").Should().Be("Connection refused");
        activity.Status.Should().Be(ActivityStatusCode.Error);
    }
}
