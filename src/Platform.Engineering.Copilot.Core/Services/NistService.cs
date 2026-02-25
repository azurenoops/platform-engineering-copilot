using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Observability;
using Polly;
using Polly.Retry;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Dual-source OSCAL catalog service.
/// Primary: GitHub fetch (usnistgov/oscal-content).
/// Fallback: Embedded OSCAL JSON snapshot.
/// Supports IMemoryCache-based caching with configurable TTL and thundering herd prevention.
/// Logs active source and catalog version (FR-080).
/// </summary>
public class NistService : INistService
{
    private readonly ILogger<NistService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache? _memoryCache;
    private readonly NistControlsOptions _options;
    private readonly ComplianceMetricsService? _metrics;
    private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;

    private readonly ConcurrentDictionary<string, ControlDefinition> _controls = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _familyCodes = [];

    /// <summary>
    /// Semaphore to prevent thundering herd on concurrent cache misses.
    /// Only one thread populates the cache; others wait and read the result.
    /// </summary>
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private const string CatalogCacheKey = "NistCatalog_Controls";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsLoaded { get; private set; }
    public NistDataSourceInfo ActiveSource { get; private set; } = new("None", "Not loaded", DateTimeOffset.MinValue);

    /// <summary>
    /// Primary constructor used by DI — accepts IMemoryCache and IOptions&lt;NistControlsOptions&gt;.
    /// </summary>
    public NistService(
        ILogger<NistService> logger,
        IConfiguration configuration,
        HttpClient httpClient,
        IMemoryCache? memoryCache = null,
        IOptions<NistControlsOptions>? options = null,
        ComplianceMetricsService? metrics = null)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
        _memoryCache = memoryCache;
        _options = options?.Value ?? new NistControlsOptions();
        _metrics = metrics;

        // Build Polly retry pipeline with exponential backoff (FR-015, FR-016, FR-017)
        _retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = _options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(_options.RetryDelaySeconds),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .HandleResult(r => !r.IsSuccessStatusCode),
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        "NIST catalog fetch retry {Attempt}/{MaxRetries} after {Delay}s",
                        args.AttemptNumber + 1,
                        _options.MaxRetryAttempts,
                        args.RetryDelay.TotalSeconds);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Initializes the catalog. Called at startup by NistServiceHostedService.
    /// Populates IMemoryCache when enabled (FR-010 through FR-014).
    /// Uses SemaphoreSlim double-check pattern to prevent thundering herd.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Use options first, fall back to raw config for backward compatibility
        var enableGitHub = _configuration.GetValue<bool?>("NistControls:EnableGitHubFetch")
            ?? _configuration.GetValue<bool>("NistData:EnableGitHubFetch", true);

        var fetchActivity = _metrics?.StartCatalogFetchActivity(enableGitHub ? "GitHub" : "Fallback");
        var sw = Stopwatch.StartNew();

        try
        {
            if (enableGitHub)
            {
                try
                {
                    await RefreshFromGitHubAsync(cancellationToken);
                    if (IsLoaded)
                    {
                        SetCatalogCache();
                        sw.Stop();
                        _metrics?.CatalogFetchCount.Add(1, new KeyValuePair<string, object?>("source", "github"));
                        _metrics?.CatalogFetchDuration.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("source", "github"));
                        ComplianceMetricsService.RecordFetchResult(fetchActivity, true, _controls.Count, false, false);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GitHub fetch failed after {MaxRetries} retries, attempting fallback",
                        _options.MaxRetryAttempts);
                    _metrics?.ErrorCount.Add(1, new KeyValuePair<string, object?>("operation", "github_fetch"));
                }
            }

            // Try offline fallback file before embedded resources (FR-018, FR-019)
            if (TryLoadFromOfflineFallback())
            {
                SetCatalogCache();
                sw.Stop();
                _metrics?.CatalogFetchCount.Add(1, new KeyValuePair<string, object?>("source", "offline_fallback"));
                _metrics?.CatalogFetchDuration.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("source", "offline_fallback"));
                ComplianceMetricsService.RecordFetchResult(fetchActivity, true, _controls.Count, false, true);
                return;
            }

            LoadFromEmbeddedResources();
            SetCatalogCache();
            sw.Stop();
            _metrics?.CatalogFetchCount.Add(1, new KeyValuePair<string, object?>("source", "embedded"));
            _metrics?.CatalogFetchDuration.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("source", "embedded"));
            ComplianceMetricsService.RecordFetchResult(fetchActivity, IsLoaded, _controls.Count, false, true);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _metrics?.ErrorCount.Add(1, new KeyValuePair<string, object?>("operation", "catalog_fetch"));
            ComplianceMetricsService.RecordError(fetchActivity, ex);
            throw;
        }
        finally
        {
            fetchActivity?.Dispose();
        }
    }

    /// <summary>
    /// Sets the catalog data into IMemoryCache with configurable absolute and sliding expiration.
    /// Absolute expiration defaults to 24h; sliding expiration is 25% of absolute (default 6h).
    /// Cache priority is set to High to resist eviction pressure.
    /// </summary>
    private void SetCatalogCache()
    {
        if (_memoryCache == null || !_options.EnableMemoryCache || !IsLoaded) return;

        var absoluteExpiration = TimeSpan.FromHours(_options.CacheDurationHours);
        var slidingExpiration = TimeSpan.FromHours(_options.CacheDurationHours / 4.0);

        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration,
            SlidingExpiration = slidingExpiration,
            Priority = CacheItemPriority.High
        };

        // Cache a snapshot of the controls dictionary
        var snapshot = new Dictionary<string, ControlDefinition>(_controls, StringComparer.OrdinalIgnoreCase);
        _memoryCache.Set(CatalogCacheKey, snapshot, cacheOptions);

        _logger.LogInformation(
            "NIST catalog cached with absolute expiration {AbsoluteHours}h, sliding expiration {SlidingHours}h",
            _options.CacheDurationHours, _options.CacheDurationHours / 4.0);
    }

    /// <summary>
    /// Ensures the catalog is loaded, using cache-first with thundering herd prevention.
    /// </summary>
    private async Task EnsureCatalogLoadedAsync(CancellationToken cancellationToken)
    {
        // Fast path: already loaded in-memory
        if (IsLoaded)
        {
            _metrics?.CacheHitCount.Add(1);
            return;
        }

        // Check cache
        if (_memoryCache != null && _options.EnableMemoryCache &&
            _memoryCache.TryGetValue(CatalogCacheKey, out Dictionary<string, ControlDefinition>? cached) &&
            cached != null)
        {
            foreach (var kvp in cached)
                _controls.TryAdd(kvp.Key, kvp.Value);
            IsLoaded = true;
            _metrics?.CacheHitCount.Add(1);
            return;
        }

        _metrics?.CacheMissCount.Add(1);

        // Thundering herd prevention: only one thread initializes
        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (IsLoaded) return;

            await InitializeAsync(cancellationToken);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public ControlDefinition? GetControl(string controlId)
    {
        _controls.TryGetValue(controlId, out var control);
        return control;
    }

    public IReadOnlyList<ControlDefinition> GetControlsByFamily(string familyCode)
    {
        return _controls.Values
            .Where(c => c.Family.Equals(familyCode, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.ControlId)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<ControlDefinition> SearchControls(string query, int maxResults = 25)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var lowerQuery = query.ToLowerInvariant();

        return _controls.Values
            .Where(c =>
                c.ControlId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.FamilyName.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.ControlId.Equals(query, StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(c => c.ControlId)
            .Take(maxResults)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<ControlDefinition> GetControlsByBaseline(BaselineLevel baseline)
    {
        return _controls.Values
            .Where(c => baseline switch
            {
                BaselineLevel.High => c.Baselines.High,
                BaselineLevel.Moderate => c.Baselines.Moderate,
                BaselineLevel.Low => c.Baselines.Low,
                _ => false
            })
            .OrderBy(c => c.ControlId)
            .ToList()
            .AsReadOnly();
    }

    public IReadOnlyList<ControlDefinition> GetControlsByFramework(ComplianceFramework framework)
    {
        return _controls.Values
            .Where(c => framework switch
            {
                ComplianceFramework.Nist80053Rev5 => c.Frameworks.Nist80053Rev5,
                ComplianceFramework.FedRampHigh => c.Frameworks.FedRampHigh,
                ComplianceFramework.FedRampModerate => c.Frameworks.FedRampModerate,
                ComplianceFramework.DoDIL5 => c.Frameworks.DoDIL5,
                _ => false
            })
            .OrderBy(c => c.ControlId)
            .ToList()
            .AsReadOnly();
    }

    public FrameworkComparisonResult CompareFrameworks(ComplianceFramework a, ComplianceFramework b)
    {
        var controlsA = GetControlsByFramework(a).ToDictionary(c => c.ControlId);
        var controlsB = GetControlsByFramework(b).ToDictionary(c => c.ControlId);

        var common = controlsA.Values.Where(c => controlsB.ContainsKey(c.ControlId)).ToList();
        var uniqueToA = controlsA.Values.Where(c => !controlsB.ContainsKey(c.ControlId)).ToList();
        var uniqueToB = controlsB.Values.Where(c => !controlsA.ContainsKey(c.ControlId)).ToList();

        return new FrameworkComparisonResult
        {
            FrameworkA = a,
            FrameworkB = b,
            Common = common.AsReadOnly(),
            UniqueToA = uniqueToA.AsReadOnly(),
            UniqueToB = uniqueToB.AsReadOnly()
        };
    }

    public IReadOnlyList<string> GetFamilyCodes()
    {
        return _familyCodes.AsReadOnly();
    }

    public async Task RefreshFromGitHubAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _options.BaseUrl
            ?? _configuration.GetValue<string>("NistData:GitHubBaseUrl")
            ?? "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json";

        _logger.LogInformation("Attempting to fetch NIST catalog from GitHub: {Url}", baseUrl);

        // Fetch the catalog JSON with Polly retry pipeline (FR-015, FR-016, FR-017)
        var catalogUrl = baseUrl.TrimEnd('/') + "/NIST_SP-800-53_rev5_catalog.json";
        var response = await _retryPipeline.ExecuteAsync(
            async ct => await _httpClient.GetAsync(catalogUrl, ct),
            cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var catalog = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

        LoadFromCatalogJson(catalog);

        ActiveSource = new NistDataSourceInfo(
            "GitHub",
            $"NIST SP 800-53 Rev 5 — fetched {DateTimeOffset.UtcNow:yyyy-MM-dd}",
            DateTimeOffset.UtcNow);

        IsLoaded = true;
        _logger.LogInformation("NIST catalog loaded from GitHub. Controls: {Count}, Source: {Source}",
            _controls.Count, ActiveSource.Source);
    }

    /// <summary>
    /// Tries to load the catalog from the offline fallback JSON file (FR-018, FR-019, FR-020).
    /// Returns true if load succeeded, false if the file is missing or parsing failed.
    /// </summary>
    private bool TryLoadFromOfflineFallback()
    {
        if (!_options.EnableOfflineFallback || string.IsNullOrEmpty(_options.OfflineFallbackPath))
        {
            _logger.LogDebug("Offline fallback disabled or path not configured");
            return false;
        }

        // Resolve path relative to content root or as absolute
        var fallbackPath = _options.OfflineFallbackPath;
        var contentRoot = _configuration.GetValue<string>("ContentRootPath");
        if (contentRoot != null && !Path.IsPathRooted(fallbackPath))
        {
            fallbackPath = Path.Combine(contentRoot, fallbackPath);
        }

        if (!File.Exists(fallbackPath))
        {
            _logger.LogWarning("Offline fallback file not found at {Path}", fallbackPath);
            return false;
        }

        try
        {
            _logger.LogInformation("Loading NIST catalog from offline fallback: {Path}", fallbackPath);

            var json = File.ReadAllText(fallbackPath);
            var catalog = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);

            LoadFromCatalogJson(catalog);

            ActiveSource = new NistDataSourceInfo(
                "OfflineFallback",
                "NIST SP 800-53 Rev 5 — offline fallback",
                DateTimeOffset.UtcNow);

            IsLoaded = true;
            _logger.LogInformation("NIST catalog loaded from offline fallback. Controls: {Count}", _controls.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load offline fallback from {Path}", fallbackPath);
            return false;
        }
    }

    private void LoadFromEmbeddedResources()
    {
        _logger.LogInformation("Loading NIST catalog from embedded OSCAL snapshots");

        var assembly = Assembly.GetExecutingAssembly();
        var catalogResourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("nist-800-53-rev5.json", StringComparison.OrdinalIgnoreCase));

        if (catalogResourceName == null)
        {
            _logger.LogError("Embedded NIST catalog resource not found");
            return;
        }

        using var stream = assembly.GetManifestResourceStream(catalogResourceName)!;
        var catalog = JsonSerializer.Deserialize<JsonElement>(stream, JsonOptions);

        LoadFromCatalogJson(catalog);
        LoadOverlays(assembly);
        LoadStigMappings(assembly);
        LoadAzureServiceMappings(assembly);

        ActiveSource = new NistDataSourceInfo(
            "EmbeddedFallback",
            "NIST SP 800-53 Rev 5 — embedded snapshot",
            DateTimeOffset.UtcNow);

        IsLoaded = true;
        _logger.LogInformation("NIST catalog loaded from embedded resources. Controls: {Count}, Source: {Source}",
            _controls.Count, ActiveSource.Source);
    }

    private void LoadFromCatalogJson(JsonElement root)
    {
        _controls.Clear();
        _familyCodes.Clear();

        if (!root.TryGetProperty("catalog", out var catalog)) return;
        if (!catalog.TryGetProperty("groups", out var groups)) return;

        foreach (var group in groups.EnumerateArray())
        {
            var familyCode = group.GetProperty("id").GetString()?.ToUpperInvariant() ?? "";
            var familyName = group.GetProperty("title").GetString() ?? "";

            if (!_familyCodes.Contains(familyCode))
                _familyCodes.Add(familyCode);

            if (!group.TryGetProperty("controls", out var controls)) continue;

            foreach (var control in controls.EnumerateArray())
            {
                var def = ParseControl(control, familyCode, familyName);
                _controls.TryAdd(def.ControlId, def);
            }
        }

        _familyCodes.Sort();
    }

    private static ControlDefinition ParseControl(JsonElement control, string familyCode, string familyName)
    {
        var def = new ControlDefinition
        {
            ControlId = control.GetProperty("id").GetString() ?? "",
            Family = familyCode,
            FamilyName = familyName,
            Title = control.GetProperty("title").GetString() ?? "",
            Description = control.GetProperty("description").GetString() ?? ""
        };

        if (control.TryGetProperty("guidance", out var guidance))
            def.ImplementationGuidance = guidance.GetString();

        if (control.TryGetProperty("priority", out var priority))
            def.Priority = priority.GetString();

        if (control.TryGetProperty("baselines", out var baselines))
        {
            def.Baselines = new BaselineApplicability
            {
                High = baselines.TryGetProperty("high", out var h) && h.GetBoolean(),
                Moderate = baselines.TryGetProperty("moderate", out var m) && m.GetBoolean(),
                Low = baselines.TryGetProperty("low", out var l) && l.GetBoolean()
            };
        }

        // All controls are inherently NIST 800-53 Rev 5
        def.Frameworks = new FrameworkApplicability { Nist80053Rev5 = true };

        if (control.TryGetProperty("related", out var related))
        {
            def.Related = related.EnumerateArray()
                .Select(r => r.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }

        return def;
    }

    private void LoadOverlays(Assembly assembly)
    {
        LoadOverlay(assembly, "fedramp-high-overlay.json", (c) => c.Frameworks.FedRampHigh = true);
        LoadOverlay(assembly, "fedramp-moderate-overlay.json", (c) => c.Frameworks.FedRampModerate = true);
        LoadOverlay(assembly, "dod-il5-overlay.json", (c) => c.Frameworks.DoDIL5 = true);
    }

    private void LoadOverlay(Assembly assembly, string fileName, Action<ControlDefinition> applyFlag)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName == null) return;

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        var doc = JsonSerializer.Deserialize<JsonElement>(stream, JsonOptions);

        if (!doc.TryGetProperty("overlay", out var overlay)) return;
        if (!overlay.TryGetProperty("controls", out var controls)) return;

        foreach (var controlId in controls.EnumerateArray())
        {
            var id = controlId.GetString();
            if (id != null && _controls.TryGetValue(id, out var control))
            {
                applyFlag(control);
            }
        }
    }

    private void LoadStigMappings(Assembly assembly)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("stig-mappings.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null) return;

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        var doc = JsonSerializer.Deserialize<JsonElement>(stream, JsonOptions);

        if (!doc.TryGetProperty("stigMappings", out var mappings)) return;

        foreach (var mapping in mappings.EnumerateArray())
        {
            var controlId = mapping.GetProperty("controlId").GetString();
            if (controlId == null || !_controls.TryGetValue(controlId, out var control)) continue;

            if (!mapping.TryGetProperty("stigs", out var stigs)) continue;

            control.StigReferences = stigs.EnumerateArray()
                .Select(s => new StigReference
                {
                    StigId = s.GetProperty("stigId").GetString() ?? "",
                    BenchmarkId = s.GetProperty("benchmarkId").GetString() ?? "",
                    Severity = s.GetProperty("severity").GetString() ?? ""
                })
                .ToArray();
        }
    }

    private void LoadAzureServiceMappings(Assembly assembly)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("azure-service-mappings.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName == null) return;

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        var doc = JsonSerializer.Deserialize<JsonElement>(stream, JsonOptions);

        if (!doc.TryGetProperty("serviceMappings", out var mappings)) return;

        foreach (var mapping in mappings.EnumerateArray())
        {
            var controlId = mapping.GetProperty("controlId").GetString();
            if (controlId == null || !_controls.TryGetValue(controlId, out var control)) continue;

            if (!mapping.TryGetProperty("services", out var services)) continue;

            control.AzureServiceMappings = services.EnumerateArray()
                .Select(s => s.GetString() ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .ToArray();
        }
    }

    /// <inheritdoc />
    public async Task<ControlEnhancement?> GetControlEnhancementAsync(string controlId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(controlId);

        using var activity = _metrics?.StartControlQueryActivity("GetControlEnhancement");
        activity?.SetTag("control.id", controlId);
        var sw = Stopwatch.StartNew();

        await EnsureCatalogLoadedAsync(cancellationToken);

        _metrics?.ControlQueryCount.Add(1, new KeyValuePair<string, object?>("operation", "GetControlEnhancement"));

        if (!_controls.TryGetValue(controlId, out var control))
        {
            sw.Stop();
            _metrics?.ControlQueryDuration.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "GetControlEnhancement"));
            return null;
        }

        sw.Stop();
        _metrics?.ControlQueryDuration.Record(sw.Elapsed.TotalMilliseconds, new KeyValuePair<string, object?>("operation", "GetControlEnhancement"));

        return new ControlEnhancement(
            Id: control.ControlId,
            Title: control.Title,
            Statement: control.Description,
            Guidance: control.ImplementationGuidance ?? string.Empty,
            Objectives: Array.Empty<string>(),
            LastUpdated: DateTime.UtcNow);
    }

    /// <inheritdoc />
    public async Task<bool> ValidateControlIdAsync(string controlId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(controlId);

        using var activity = _metrics?.StartControlQueryActivity("ValidateControlId");
        activity?.SetTag("control.id", controlId);

        await EnsureCatalogLoadedAsync(cancellationToken);

        _metrics?.ControlQueryCount.Add(1, new KeyValuePair<string, object?>("operation", "ValidateControlId"));

        if (!IsLoaded)
            return false;

        return _controls.ContainsKey(controlId);
    }

    /// <inheritdoc />
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _metrics?.StartControlQueryActivity("GetVersion");

        await EnsureCatalogLoadedAsync(cancellationToken);

        _metrics?.ControlQueryCount.Add(1, new KeyValuePair<string, object?>("operation", "GetVersion"));

        if (!IsLoaded)
            return "Unknown";

        return ActiveSource.CatalogVersion;
    }

    /// <inheritdoc />
    public async Task<NistCatalogSnapshot?> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        using var activity = _metrics?.StartControlQueryActivity("GetCatalog");

        await EnsureCatalogLoadedAsync(cancellationToken);

        _metrics?.ControlQueryCount.Add(1, new KeyValuePair<string, object?>("operation", "GetCatalog"));

        if (!IsLoaded)
            return null;

        var familyCount = _controls.Values
            .Select(c => c.Family)
            .Distinct()
            .Count();

        return new NistCatalogSnapshot(
            Version: ActiveSource.CatalogVersion,
            TotalControls: _controls.Count,
            FamilyCount: familyCount,
            LoadedAt: ActiveSource.LoadedAt,
            Source: ActiveSource.Source);
    }
}
