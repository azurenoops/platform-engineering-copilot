using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Governance.Configuration;

namespace Platform.Engineering.Copilot.Governance.Services;

/// <summary>
/// Production-ready service for fetching, caching, and managing NIST 800-53 controls catalog and compliance data.
/// This service provides comprehensive access to NIST security controls with caching, retry policies, and metrics collection.
/// </summary>
public class NistControlsService : INistControlsService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<NistControlsService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly NistControlsOptions _options;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ComplianceMetricsService _metricsService;

    private const string CATALOG_CACHE_KEY = "nist_catalog";
    private const string VERSION_CACHE_KEY = "nist_version";
    private const string CATALOG_FILENAME = "NIST_SP-800-53_rev5_catalog.json";

    /// <summary>
    /// Initializes a new instance of the NistControlsService with dependency injection support.
    /// Sets up HTTP client, caching, retry policies, and metrics collection for NIST controls access.
    /// </summary>
    /// <param name="httpClient">HTTP client for fetching NIST controls data from external sources</param>
    /// <param name="cache">Memory cache for storing controls catalog and reducing external API calls</param>
    /// <param name="logger">Logger for NIST controls operations and compliance events</param>
    /// <param name="hostEnvironment">Host environment for determining development vs production configurations</param>
    /// <param name="options">Configuration options for NIST controls caching, URLs, and compliance settings</param>
    /// <param name="metricsService">Service for collecting and reporting compliance metrics and performance data</param>
    public NistControlsService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<NistControlsService> logger,
        IHostEnvironment hostEnvironment,
        IOptions<NistControlsOptions> options,
        ComplianceMetricsService metricsService)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _metricsService = metricsService ?? throw new ArgumentNullException(nameof(metricsService));

        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: _options.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(_options.RetryDelaySeconds, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retrying NIST API call (attempt {RetryCount}/{MaxRetries}) after {Delay}ms",
                        retryCount, _options.MaxRetryAttempts, timespan.TotalMilliseconds);
                });
    }

    private void ConfigureHttpClient()
    {
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            $"PlatformMCP-Supervisor/1.0 (+https://github.com/jrspinella/platform-mcp-supervisor)");
        
        if (_options.EnableDetailedLogging)
        {
            _logger.LogDebug("HTTP client configured with timeout: {Timeout}s", _options.TimeoutSeconds);
        }
    }

    private IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: _options.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(
                    Math.Pow(_options.RetryDelaySeconds, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning("Retry {RetryCount}/{MaxRetries} for NIST API call in {Delay}ms. Reason: {Reason}",
                        retryCount, _options.MaxRetryAttempts, timespan.TotalMilliseconds,
                        outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase ?? "Unknown");
                });
    }

    public async Task<NistCatalog?> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        using var activity = ComplianceActivitySource.StartNistApiActivity("GetCatalog");
        var stopwatch = Stopwatch.StartNew();
        
        var cacheKey = $"{CATALOG_CACHE_KEY}_{_options.TargetVersion ?? "latest"}";
        
        if (_cache.TryGetValue(cacheKey, out NistCatalog? cachedCatalog))
        {
            _logger.LogDebug("Returning cached NIST catalog");
            activity?.SetTag("cache.hit", true);
            _metricsService.RecordNistApiCall("GetCatalog", true, stopwatch.Elapsed);
            return cachedCatalog;
        }

        activity?.SetTag("cache.hit", false);
        
        try
        {
            _logger.LogInformation("Fetching NIST 800-53 catalog from official repository");
            
            var catalog = await FetchCatalogFromRemoteAsync(cancellationToken);
            
            if (catalog != null)
            {
                CacheCatalog(cacheKey, catalog);
                activity?.SetTag("success", true);
                activity?.SetTag("control.count", catalog.Groups?.Sum(g => g.Controls?.Count ?? 0) ?? 0);
                _metricsService.RecordNistApiCall("GetCatalog", true, stopwatch.Elapsed);
                return catalog;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch NIST catalog from remote source");
            activity?.SetTag("success", false);
            activity?.SetTag("error", ex.Message);
        }

        // Fallback to offline version if enabled
        if (_options.EnableOfflineFallback)
        {
            _logger.LogWarning("Attempting to load offline fallback NIST catalog");
            activity?.SetTag("fallback.used", true);
            var fallbackCatalog = await LoadOfflineFallbackAsync(cancellationToken);
            _metricsService.RecordNistApiCall("GetCatalog", fallbackCatalog != null, stopwatch.Elapsed);
            return fallbackCatalog;
        }

        activity?.SetTag("fallback.used", false);
        _logger.LogError("No NIST catalog available - remote fetch failed and offline fallback disabled");
        _metricsService.RecordNistApiCall("GetCatalog", false, stopwatch.Elapsed);
        return null;
    }

    private async Task<NistCatalog?> FetchCatalogFromRemoteAsync(CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl.TrimEnd('/')}/{CATALOG_FILENAME}";
        
        if (_options.EnableDetailedLogging)
        {
            _logger.LogDebug("Fetching NIST catalog from: {Url}", url);
        }

        var response = await _retryPolicy.ExecuteAsync(async () =>
        {
            var httpResponse = await _httpClient.GetAsync(url, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();
            return httpResponse;
        });

        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
        
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        var catalogRoot = JsonSerializer.Deserialize<NistCatalogRoot>(jsonContent, options);
        var catalog = catalogRoot?.Catalog;
        
        if (catalog?.Groups != null)
        {
            var controlCount = catalog.Groups.SelectMany(g => g.Controls ?? []).Count();
            _logger.LogInformation("Successfully loaded NIST catalog with {ControlCount} controls (Version: {Version})",
                controlCount, catalog.Metadata?.Version ?? "Unknown");
        }
        else
        {
            _logger.LogWarning("NIST catalog loaded but contains no groups or controls");
        }

        return catalog;
    }

    private async Task<NistCatalog?> LoadOfflineFallbackAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(_options.OfflineFallbackPath))
        {
            _logger.LogWarning("Offline fallback path not configured");
            return null;
        }

        try
        {
            var fallbackPath = Path.Combine(_hostEnvironment.ContentRootPath, _options.OfflineFallbackPath);
            
            if (!File.Exists(fallbackPath))
            {
                _logger.LogWarning("Offline fallback file not found: {Path}", fallbackPath);
                return null;
            }

            _logger.LogInformation("Loading NIST catalog from offline fallback: {Path}", fallbackPath);
            
            var jsonContent = await File.ReadAllTextAsync(fallbackPath, cancellationToken);
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var catalogRoot = JsonSerializer.Deserialize<NistCatalogRoot>(jsonContent, options);
            var catalog = catalogRoot?.Catalog;
            
            if (catalog != null)
            {
                _logger.LogInformation("Successfully loaded offline NIST catalog (Version: {Version})",
                    catalog.Metadata?.Version ?? "Unknown");
            }

            return catalog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load offline fallback NIST catalog");
            return null;
        }
    }

    private void CacheCatalog(string cacheKey, NistCatalog catalog)
    {
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(_options.CacheDurationHours),
            SlidingExpiration = TimeSpan.FromHours(_options.CacheDurationHours / 4),
            Priority = CacheItemPriority.High
        };

        _cache.Set(cacheKey, catalog, cacheOptions);
        _cache.Set(VERSION_CACHE_KEY, catalog.Metadata?.Version ?? "Unknown", cacheOptions);
        
        _logger.LogDebug("Cached NIST catalog for {Duration} hours", _options.CacheDurationHours);
    }

    public async Task<NistControl?> GetControlAsync(string controlId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(controlId))
        {
            throw new ArgumentException("Control ID cannot be null or empty", nameof(controlId));
        }

        var catalog = await GetCatalogAsync(cancellationToken);
        if (catalog?.Groups == null) return null;

        return catalog.Groups
            .SelectMany(g => g.Controls ?? [])
            .FirstOrDefault(c => string.Equals(c.Id, controlId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<NistControl>> GetControlsByFamilyAsync(string family, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(family))
        {
            throw new ArgumentException("Family cannot be null or empty", nameof(family));
        }

        var catalog = await GetCatalogAsync(cancellationToken);
        if (catalog?.Groups == null) return Array.Empty<NistControl>();

        return catalog.Groups
            .Where(g => g.Id?.StartsWith(family, StringComparison.OrdinalIgnoreCase) == true)
            .SelectMany(g => g.Controls ?? [])
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<NistControl>> SearchControlsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            throw new ArgumentException("Search term cannot be null or empty", nameof(searchTerm));
        }

        var catalog = await GetCatalogAsync(cancellationToken);
        if (catalog?.Groups == null) return Array.Empty<NistControl>();

        return catalog.Groups
            .SelectMany(g => g.Controls ?? [])
            .Where(c => 
                (c.Title?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true) ||
                (c.Id?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true) ||
                (c.Parts?.Any(p => p.Prose?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) == true) == true))
            .ToList()
            .AsReadOnly();
    }

    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(VERSION_CACHE_KEY, out string? cachedVersion) && !string.IsNullOrEmpty(cachedVersion))
        {
            return cachedVersion;
        }

        var catalog = await GetCatalogAsync(cancellationToken);
        return catalog?.Metadata?.Version ?? "Unknown";
    }

    public async Task<ControlEnhancement?> GetControlEnhancementAsync(string controlId, CancellationToken cancellationToken = default)
    {
        var control = await GetControlAsync(controlId, cancellationToken);
        if (control == null) return null;

        var statement = control.Parts?
            .FirstOrDefault(p => string.Equals(p.Name, "statement", StringComparison.OrdinalIgnoreCase))?
            .Prose ?? string.Empty;

        var guidance = control.Parts?
            .FirstOrDefault(p => string.Equals(p.Name, "guidance", StringComparison.OrdinalIgnoreCase))?
            .Prose ?? string.Empty;

        var objectives = control.Parts?
            .Where(p => string.Equals(p.Name, "objective", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Prose ?? string.Empty)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToList() ?? [];

        return new ControlEnhancement
        {
            Id = control.Id ?? controlId,
            Title = control.Title ?? string.Empty,
            Statement = statement,
            Guidance = guidance,
            Objectives = objectives.AsReadOnly(),
            LastUpdated = DateTime.UtcNow
        };
    }

    public async Task<bool> ValidateControlIdAsync(string controlId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(controlId)) return false;

        var control = await GetControlAsync(controlId, cancellationToken);
        return control != null;
    }
}
