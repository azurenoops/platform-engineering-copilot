using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Core.Services;

/// <summary>
/// Dual-source OSCAL catalog service.
/// Primary: GitHub fetch (usnistgov/oscal-content).
/// Fallback: Embedded OSCAL JSON snapshot.
/// Logs active source and catalog version (FR-080).
/// </summary>
public class NistService : INistService
{
    private readonly ILogger<NistService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    private readonly ConcurrentDictionary<string, ControlDefinition> _controls = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _familyCodes = [];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool IsLoaded { get; private set; }
    public NistDataSourceInfo ActiveSource { get; private set; } = new("None", "Not loaded", DateTimeOffset.MinValue);

    public NistService(
        ILogger<NistService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Initializes the catalog. Called at startup by NistServiceHostedService.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var enableGitHub = _configuration.GetValue<bool>("NistData:EnableGitHubFetch", true);

        if (enableGitHub)
        {
            try
            {
                await RefreshFromGitHubAsync(cancellationToken);
                if (IsLoaded) return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GitHub fetch failed, falling back to embedded OSCAL data");
            }
        }

        LoadFromEmbeddedResources();
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
        var baseUrl = _configuration.GetValue<string>("NistData:GitHubBaseUrl")
            ?? "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5";

        _logger.LogInformation("Attempting to fetch NIST catalog from GitHub: {Url}", baseUrl);

        // Fetch the catalog JSON
        var catalogUrl = $"{baseUrl}/json/NIST_SP-800-53_rev5_catalog.json";
        var response = await _httpClient.GetAsync(catalogUrl, cancellationToken);
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
}
