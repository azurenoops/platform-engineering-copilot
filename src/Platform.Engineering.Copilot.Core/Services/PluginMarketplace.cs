using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Plugin marketplace service for discovering, downloading, and managing community plugins
/// </summary>
public interface IPluginMarketplace
{
    /// <summary>
    /// Search for plugins in the marketplace
    /// </summary>
    Task<IEnumerable<PluginManifest>> SearchPluginsAsync(string? query = null, PluginCategory? category = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get plugin details by ID
    /// </summary>
    Task<PluginManifest?> GetPluginDetailsAsync(string pluginId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Download and install a plugin
    /// </summary>
    Task<bool> InstallPluginAsync(string pluginId, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an installed plugin
    /// </summary>
    Task<bool> UpdatePluginAsync(string pluginId, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available updates for installed plugins
    /// </summary>
    Task<IEnumerable<PluginUpdate>> GetAvailableUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rate a plugin
    /// </summary>
    Task<bool> RatePluginAsync(string pluginId, int rating, string? review = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Plugin update information
/// </summary>
public class PluginUpdate
{
    public string PluginId { get; set; } = string.Empty;
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; }
    public bool IsSecurityUpdate { get; set; }
}

/// <summary>
/// Plugin marketplace configuration
/// </summary>
public class PluginMarketplaceOptions
{
    public string MarketplaceUrl { get; set; } = "https://plugins.platform-mcp.io";
    public string ApiKey { get; set; } = string.Empty;
    public bool EnableAutoUpdate { get; set; } = false;
    public bool RequireSignedPlugins { get; set; } = true;
    public string PluginInstallDirectory { get; set; } = "./plugins/installed";
}

/// <summary>
/// Default implementation of plugin marketplace
/// </summary>
public class PluginMarketplace : IPluginMarketplace
{
    private readonly ILogger<PluginMarketplace> _logger;
    private readonly PluginMarketplaceOptions _options;
    private readonly HttpClient _httpClient;
    private readonly IPluginValidator _validator;

    public PluginMarketplace(
        ILogger<PluginMarketplace> logger,
        IOptions<PluginMarketplaceOptions> options,
        IHttpClientFactory httpClientFactory,
        IPluginValidator validator)
    {
        _logger = logger;
        _options = options.Value;
        _httpClient = httpClientFactory.CreateClient("PluginMarketplace");
        _validator = validator;
    }

    public async Task<IEnumerable<PluginManifest>> SearchPluginsAsync(string? query = null, PluginCategory? category = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_options.MarketplaceUrl}/api/plugins/search";
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(query))
                queryParams.Add($"q={Uri.EscapeDataString(query)}");

            if (category.HasValue)
                queryParams.Add($"category={category.Value}");

            if (queryParams.Any())
                url += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var plugins = JsonSerializer.Deserialize<List<PluginManifest>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return plugins ?? new List<PluginManifest>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search plugins in marketplace");
            return new List<PluginManifest>();
        }
    }

    public async Task<PluginManifest?> GetPluginDetailsAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_options.MarketplaceUrl}/api/plugins/{Uri.EscapeDataString(pluginId)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Plugin {PluginId} not found in marketplace", pluginId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get plugin details for {PluginId}", pluginId);
            return null;
        }
    }

    public async Task<bool> InstallPluginAsync(string pluginId, string version, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Installing plugin {PluginId} version {Version}", pluginId, version);

            // Get plugin manifest
            var manifest = await GetPluginDetailsAsync(pluginId, cancellationToken);
            if (manifest == null)
            {
                _logger.LogError("Plugin {PluginId} not found in marketplace", pluginId);
                return false;
            }

            // Download plugin package
            var packageUrl = $"{_options.MarketplaceUrl}/api/plugins/{pluginId}/download/{version}";
            var response = await _httpClient.GetAsync(packageUrl, cancellationToken);
            response.EnsureSuccessStatusCode();

            var packageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            // Validate plugin package
            if (_options.RequireSignedPlugins)
            {
                var isValid = await _validator.ValidatePluginPackageAsync(packageBytes, manifest, cancellationToken);
                if (!isValid)
                {
                    _logger.LogError("Plugin {PluginId} failed validation", pluginId);
                    return false;
                }
            }

            // Extract and install plugin
            var installPath = Path.Combine(_options.PluginInstallDirectory, pluginId, version);
            Directory.CreateDirectory(installPath);

            // Save package to disk
            var packagePath = Path.Combine(installPath, $"{pluginId}.zip");
            await File.WriteAllBytesAsync(packagePath, packageBytes, cancellationToken);

            // Extract package
            System.IO.Compression.ZipFile.ExtractToDirectory(packagePath, installPath, true);

            // Save manifest
            var manifestPath = Path.Combine(installPath, "plugin.manifest.json");
            var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(manifestPath, manifestJson, cancellationToken);

            _logger.LogInformation("Successfully installed plugin {PluginId} version {Version} to {Path}", 
                pluginId, version, installPath);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install plugin {PluginId} version {Version}", pluginId, version);
            return false;
        }
    }

    public async Task<bool> UpdatePluginAsync(string pluginId, string version, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Updating plugin {PluginId} to version {Version}", pluginId, version);

            // Check if plugin is installed
            var installedPath = Path.Combine(_options.PluginInstallDirectory, pluginId);
            if (!Directory.Exists(installedPath))
            {
                _logger.LogError("Plugin {PluginId} is not installed", pluginId);
                return false;
            }

            // Install new version
            var result = await InstallPluginAsync(pluginId, version, cancellationToken);

            if (result)
            {
                // Clean up old versions (keep last 2)
                var versions = Directory.GetDirectories(installedPath)
                    .OrderByDescending(d => d)
                    .Skip(2)
                    .ToList();

                foreach (var oldVersion in versions)
                {
                    try
                    {
                        Directory.Delete(oldVersion, true);
                        _logger.LogInformation("Removed old plugin version {Path}", oldVersion);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to remove old plugin version {Path}", oldVersion);
                    }
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update plugin {PluginId} to version {Version}", pluginId, version);
            return false;
        }
    }

    public async Task<IEnumerable<PluginUpdate>> GetAvailableUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var updates = new List<PluginUpdate>();

        try
        {
            // Get all installed plugins
            var installedPlugins = GetInstalledPlugins();

            // Check each plugin for updates
            foreach (var (pluginId, currentVersion) in installedPlugins)
            {
                var latestManifest = await GetPluginDetailsAsync(pluginId, cancellationToken);
                if (latestManifest != null && IsNewerVersion(currentVersion, latestManifest.Version))
                {
                    updates.Add(new PluginUpdate
                    {
                        PluginId = pluginId,
                        CurrentVersion = currentVersion,
                        LatestVersion = latestManifest.Version,
                        ReleaseNotes = $"Update {pluginId} from {currentVersion} to {latestManifest.Version}",
                        ReleaseDate = DateTime.UtcNow,
                        IsSecurityUpdate = false
                    });
                }
            }

            return updates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for plugin updates");
            return updates;
        }
    }

    public async Task<bool> RatePluginAsync(string pluginId, int rating, string? review = null, CancellationToken cancellationToken = default)
    {
        try
        {
            if (rating < 1 || rating > 5)
            {
                _logger.LogWarning("Invalid rating {Rating} for plugin {PluginId}", rating, pluginId);
                return false;
            }

            var url = $"{_options.MarketplaceUrl}/api/plugins/{Uri.EscapeDataString(pluginId)}/rate";
            var content = new
            {
                rating,
                review,
                timestamp = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(content);
            var response = await _httpClient.PostAsync(url, new StringContent(json, System.Text.Encoding.UTF8, "application/json"), cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully rated plugin {PluginId} with {Rating} stars", pluginId, rating);
                return true;
            }

            _logger.LogWarning("Failed to rate plugin {PluginId}: {StatusCode}", pluginId, response.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rate plugin {PluginId}", pluginId);
            return false;
        }
    }

    private Dictionary<string, string> GetInstalledPlugins()
    {
        var installedPlugins = new Dictionary<string, string>();

        if (!Directory.Exists(_options.PluginInstallDirectory))
            return installedPlugins;

        foreach (var pluginDir in Directory.GetDirectories(_options.PluginInstallDirectory))
        {
            var pluginId = Path.GetFileName(pluginDir);
            var versionDirs = Directory.GetDirectories(pluginDir).OrderByDescending(d => d).ToList();

            if (versionDirs.Any())
            {
                var latestVersion = Path.GetFileName(versionDirs.First());
                installedPlugins[pluginId] = latestVersion;
            }
        }

        return installedPlugins;
    }

    private bool IsNewerVersion(string current, string latest)
    {
        try
        {
            var currentVersion = Version.Parse(current);
            var latestVersion = Version.Parse(latest);
            return latestVersion > currentVersion;
        }
        catch
        {
            // Fallback to string comparison
            return string.Compare(latest, current, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}

/// <summary>
/// Plugin validator interface
/// </summary>
public interface IPluginValidator
{
    Task<bool> ValidatePluginPackageAsync(byte[] packageBytes, PluginManifest manifest, CancellationToken cancellationToken = default);
}

/// <summary>
/// Basic plugin validator implementation
/// </summary>
public class PluginValidator : IPluginValidator
{
    private readonly ILogger<PluginValidator> _logger;

    public PluginValidator(ILogger<PluginValidator> logger)
    {
        _logger = logger;
    }

    public Task<bool> ValidatePluginPackageAsync(byte[] packageBytes, PluginManifest manifest, CancellationToken cancellationToken = default)
    {
        try
        {
            // TODO: Implement actual validation logic
            // - Check digital signature
            // - Verify checksum
            // - Scan for malicious code
            // - Validate manifest integrity

            _logger.LogInformation("Validating plugin package for {PluginId}", manifest.Id);

            // For now, just check basic things
            if (packageBytes.Length == 0)
                return Task.FromResult(false);

            if (string.IsNullOrEmpty(manifest.Id) || string.IsNullOrEmpty(manifest.EntryAssembly))
                return Task.FromResult(false);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate plugin package");
            return Task.FromResult(false);
        }
    }
}