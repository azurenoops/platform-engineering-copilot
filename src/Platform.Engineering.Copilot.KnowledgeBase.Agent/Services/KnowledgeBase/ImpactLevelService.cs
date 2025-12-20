using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase;
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;

namespace Platform.Engineering.Copilot.KnowledgeBase.Agent.Services.KnowledgeBase;

/// <summary>
/// Service for DoD Impact Level requirements and guidance
/// </summary>
public class ImpactLevelService : IImpactLevelService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<ImpactLevelService> _logger;
    private const string CACHE_KEY = "impact_levels_data";
    private const string IMPACT_LEVELS_FILE = "KnowledgeBase/impact-levels.json";

    public ImpactLevelService(IMemoryCache cache, ILogger<ImpactLevelService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ImpactLevel?> GetImpactLevelAsync(string level, CancellationToken cancellationToken = default)
    {
        var data = await LoadImpactLevelDataAsync(cancellationToken);
        if (data?.ImpactLevels == null) return null;

        var normalizedLevel = NormalizeLevel(level);
        if (!data.ImpactLevels.TryGetValue(normalizedLevel, out var ilData))
            return null;

        return new ImpactLevel
        {
            Level = normalizedLevel,
            Name = ilData.Name ?? normalizedLevel,
            Description = ilData.Description ?? "",
            Requirements = ilData.SecurityRequirements?.AdditionalControls ?? new List<string>(),
            NistBaseline = new List<string> { ilData.SecurityRequirements?.Baseline ?? "" },
            MandatoryControls = ilData.SecurityRequirements?.AdditionalControls ?? new List<string>(),
            AzureConfigurations = ilData.AzureImplementation?.Services?.Recommended?
                .ToDictionary(s => s, s => "Recommended") ?? new Dictionary<string, string>()
        };
    }

    public async Task<IReadOnlyList<ImpactLevel>> GetAllImpactLevelsAsync(CancellationToken cancellationToken = default)
    {
        var levels = new List<ImpactLevel>();
        foreach (var level in new[] { "IL2", "IL4", "IL5", "IL6" })
        {
            var il = await GetImpactLevelAsync(level, cancellationToken);
            if (il != null) levels.Add(il);
        }
        return levels.AsReadOnly();
    }

    public async Task<BoundaryProtectionRequirement?> GetBoundaryRequirementsAsync(string impactLevel, CancellationToken cancellationToken = default)
    {
        var data = await LoadImpactLevelDataAsync(cancellationToken);
        if (data?.ImpactLevels == null) return null;

        var normalizedLevel = NormalizeLevel(impactLevel);
        if (!data.ImpactLevels.TryGetValue(normalizedLevel, out var ilData))
            return null;

        return new BoundaryProtectionRequirement
        {
            ImpactLevel = normalizedLevel,
            Description = ilData.Description ?? "",
            NetworkRequirements = ilData.SecurityRequirements?.NetworkRequirements != null
                ? new List<string>
                {
                    $"Connectivity: {ilData.SecurityRequirements.NetworkRequirements.Connectivity}",
                    $"CAP Required: {ilData.SecurityRequirements.NetworkRequirements.CapRequired}",
                    $"Dedicated Connection: {ilData.SecurityRequirements.NetworkRequirements.DedicatedConnection}"
                }
                : new List<string>(),
            EncryptionRequirements = ilData.SecurityRequirements?.EncryptionRequirements != null
                ? new List<string>
                {
                    $"Data at Rest: {ilData.SecurityRequirements.EncryptionRequirements.DataAtRest}",
                    $"Data in Transit: {ilData.SecurityRequirements.EncryptionRequirements.DataInTransit}",
                    $"FIPS: {ilData.SecurityRequirements.EncryptionRequirements.FipsRequirement}"
                }
                : new List<string>()
        };
    }

    public async Task<string> ExplainImpactLevelAsync(string level, CancellationToken cancellationToken = default)
    {
        var data = await LoadImpactLevelDataAsync(cancellationToken);
        if (data?.ImpactLevels == null)
            return $"Impact Level information not available.";

        var normalizedLevel = NormalizeLevel(level);
        if (!data.ImpactLevels.TryGetValue(normalizedLevel, out var ilData))
            return $"Impact Level '{level}' not found. Valid levels: IL2, IL4, IL5, IL6.";

        var output = $@"# {ilData.Name} ({normalizedLevel})

## Description
{ilData.Description}

## Data Classification

### Authorized Data Types
{string.Join("\n", ilData.DataClassification?.Authorized?.Select(d => $"- {d}") ?? Array.Empty<string>())}

### NOT Authorized
{string.Join("\n", ilData.DataClassification?.NotAuthorized?.Select(d => $"- ⚠️ {d}") ?? Array.Empty<string>())}

## Security Requirements

**Baseline:** {ilData.SecurityRequirements?.Baseline}

### Encryption Requirements
- **Data at Rest:** {ilData.SecurityRequirements?.EncryptionRequirements?.DataAtRest}
- **Data in Transit:** {ilData.SecurityRequirements?.EncryptionRequirements?.DataInTransit}
- **FIPS Requirement:** {ilData.SecurityRequirements?.EncryptionRequirements?.FipsRequirement}

### Network Requirements
- **Connectivity:** {ilData.SecurityRequirements?.NetworkRequirements?.Connectivity}
- **CAP Required:** {ilData.SecurityRequirements?.NetworkRequirements?.CapRequired}
- **Dedicated Connection:** {ilData.SecurityRequirements?.NetworkRequirements?.DedicatedConnection}

### Personnel Requirements
- **Clearance:** {ilData.SecurityRequirements?.PersonnelRequirements?.ClearanceRequired}
- **Citizenship:** {(ilData.SecurityRequirements?.PersonnelRequirements?.CitizenshipRequired == true ? "US Citizen Required" : "Not Required")}
- **Background Check:** {ilData.SecurityRequirements?.PersonnelRequirements?.BackgroundCheck}

## Azure Implementation

**Cloud Environment:** {ilData.AzureImplementation?.CloudEnvironment}

### Approved Regions
{string.Join("\n", ilData.AzureImplementation?.Regions?.Select(r => $"- {r}") ?? Array.Empty<string>())}

### Required Services
{string.Join("\n", ilData.AzureImplementation?.Services?.Required?.Select(s => $"- {s}") ?? ilData.AzureImplementation?.Services?.Recommended?.Select(s => $"- {s}") ?? Array.Empty<string>())}

### Compliance Checklist
{string.Join("\n", ilData.AzureImplementation?.ComplianceChecklist?.Select((c, i) => $"{i + 1}. {c}") ?? Array.Empty<string>())}
";

        if (ilData.SecurityRequirements?.AdditionalControls?.Any() == true)
        {
            output += $@"

## Additional Required Controls
{string.Join("\n", ilData.SecurityRequirements.AdditionalControls.Select(c => $"- {c}"))}
";
        }

        return output;
    }

    public async Task<string> GetImpactLevelComparisonAsync(CancellationToken cancellationToken = default)
    {
        var data = await LoadImpactLevelDataAsync(cancellationToken);
        if (data?.ComparisonMatrix?.Categories == null)
            return "Impact Level comparison not available.";

        var output = "# DoD Impact Level Comparison\n\n";
        output += "| Category | IL2 | IL4 | IL5 | IL6 |\n";
        output += "|----------|-----|-----|-----|-----|\n";

        foreach (var category in data.ComparisonMatrix.Categories)
        {
            output += $"| {category.Category} | {category.IL2} | {category.IL4} | {category.IL5} | {category.IL6} |\n";
        }

        return output;
    }

    public async Task<string> GetMigrationGuidanceAsync(string fromLevel, string toLevel, CancellationToken cancellationToken = default)
    {
        var data = await LoadImpactLevelDataAsync(cancellationToken);
        if (data?.MigrationGuidance == null)
            return "Migration guidance not available.";

        var normalizedFrom = NormalizeLevel(fromLevel);
        var normalizedTo = NormalizeLevel(toLevel);
        var key = $"{normalizedFrom}to{normalizedTo}";

        if (!data.MigrationGuidance.TryGetValue(key, out var guidance))
            return $"No migration guidance available for {fromLevel} to {toLevel}. Available: IL2 to IL4, IL4 to IL5.";

        var output = $@"# Migration: {normalizedFrom} → {normalizedTo}

## Overview
{guidance.Overview}

## Steps
";
        foreach (var step in guidance.Steps ?? new List<MigrationStep>())
        {
            output += $@"
### Step {step.Step}: {step.Title}
{step.Description}

**Tasks:**
{string.Join("\n", step.Tasks?.Select(t => $"- {t}") ?? Array.Empty<string>())}
";
        }

        return output;
    }

    public async Task<string> GetAzureImplementationAsync(string level, CancellationToken cancellationToken = default)
    {
        var data = await LoadImpactLevelDataAsync(cancellationToken);
        if (data?.ImpactLevels == null)
            return "Azure implementation guidance not available.";

        var normalizedLevel = NormalizeLevel(level);
        if (!data.ImpactLevels.TryGetValue(normalizedLevel, out var ilData))
            return $"Impact Level '{level}' not found.";

        var azure = ilData.AzureImplementation;
        if (azure == null)
            return $"Azure implementation for {level} not available.";

        var output = $@"# Azure Implementation for {normalizedLevel}

**Cloud Environment:** {azure.CloudEnvironment}

## Approved Regions
{string.Join("\n", azure.Regions?.Select(r => $"- {r}") ?? Array.Empty<string>())}

## Required/Recommended Services
{string.Join("\n", azure.Services?.Required?.Select(s => $"- ✅ {s}") ?? azure.Services?.Recommended?.Select(s => $"- {s}") ?? Array.Empty<string>())}

## Encryption Configuration
- **At Rest:** {azure.Services?.Encryption?.AtRest}
- **In Transit:** {azure.Services?.Encryption?.InTransit}
- **Key Management:** {azure.Services?.Encryption?.KeyManagement}

## Compliance Checklist
{string.Join("\n", azure.ComplianceChecklist?.Select((c, i) => $"{i + 1}. {c}") ?? Array.Empty<string>())}
";

        if (azure.NetworkArchitecture != null)
        {
            output += $@"

## Network Architecture
**Pattern:** {azure.NetworkArchitecture.Pattern}

**Components:**
{string.Join("\n", azure.NetworkArchitecture.Components?.Select(c => $"- {c}") ?? Array.Empty<string>())}
";
        }

        return output;
    }

    private string NormalizeLevel(string level)
    {
        var normalized = level.ToUpperInvariant().Replace(" ", "").Replace("-", "");
        return normalized switch
        {
            "IL2" or "2" or "LEVEL2" or "IMPACTLEVEL2" => "IL2",
            "IL4" or "4" or "LEVEL4" or "IMPACTLEVEL4" => "IL4",
            "IL5" or "5" or "LEVEL5" or "IMPACTLEVEL5" => "IL5",
            "IL6" or "6" or "LEVEL6" or "IMPACTLEVEL6" => "IL6",
            _ => normalized
        };
    }

    private async Task<ImpactLevelData?> LoadImpactLevelDataAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CACHE_KEY, out ImpactLevelData? cachedData))
            return cachedData;

        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, IMPACT_LEVELS_FILE);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Impact levels file not found: {FilePath}", filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var data = JsonSerializer.Deserialize<ImpactLevelData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _cache.Set(CACHE_KEY, data, TimeSpan.FromHours(24));
            _logger.LogInformation("Loaded Impact Level data");

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Impact Level data");
            return null;
        }
    }

    // Internal data models for JSON deserialization
    private class ImpactLevelData
    {
        public Dictionary<string, ImpactLevelInfo>? ImpactLevels { get; set; }
        public ComparisonMatrix? ComparisonMatrix { get; set; }
        public Dictionary<string, MigrationGuidanceInfo>? MigrationGuidance { get; set; }
    }

    private class ImpactLevelInfo
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DataClassificationInfo? DataClassification { get; set; }
        public SecurityRequirementsInfo? SecurityRequirements { get; set; }
        public AzureImplementationInfo? AzureImplementation { get; set; }
    }

    private class DataClassificationInfo
    {
        public List<string>? Authorized { get; set; }
        public List<string>? NotAuthorized { get; set; }
    }

    private class SecurityRequirementsInfo
    {
        public string? Baseline { get; set; }
        public List<string>? AdditionalControls { get; set; }
        public EncryptionRequirementsInfo? EncryptionRequirements { get; set; }
        public NetworkRequirementsInfo? NetworkRequirements { get; set; }
        public PersonnelRequirementsInfo? PersonnelRequirements { get; set; }
    }

    private class EncryptionRequirementsInfo
    {
        public string? DataAtRest { get; set; }
        public string? DataInTransit { get; set; }
        public string? FipsRequirement { get; set; }
    }

    private class NetworkRequirementsInfo
    {
        public string? Connectivity { get; set; }
        public bool CapRequired { get; set; }
        public string? DedicatedConnection { get; set; }
    }

    private class PersonnelRequirementsInfo
    {
        public string? ClearanceRequired { get; set; }
        public bool CitizenshipRequired { get; set; }
        public string? BackgroundCheck { get; set; }
    }

    private class AzureImplementationInfo
    {
        public string? CloudEnvironment { get; set; }
        public List<string>? Regions { get; set; }
        public ServicesInfo? Services { get; set; }
        public List<string>? ComplianceChecklist { get; set; }
        public NetworkArchitectureInfo? NetworkArchitecture { get; set; }
    }

    private class ServicesInfo
    {
        public List<string>? Required { get; set; }
        public List<string>? Recommended { get; set; }
        public EncryptionConfigInfo? Encryption { get; set; }
    }

    private class EncryptionConfigInfo
    {
        public string? AtRest { get; set; }
        public string? InTransit { get; set; }
        public string? KeyManagement { get; set; }
    }

    private class NetworkArchitectureInfo
    {
        public string? Pattern { get; set; }
        public List<string>? Components { get; set; }
    }

    private class ComparisonMatrix
    {
        public List<ComparisonCategory>? Categories { get; set; }
    }

    private class ComparisonCategory
    {
        public string Category { get; set; } = string.Empty;
        public string IL2 { get; set; } = string.Empty;
        public string IL4 { get; set; } = string.Empty;
        public string IL5 { get; set; } = string.Empty;
        public string IL6 { get; set; } = string.Empty;
    }

    private class MigrationGuidanceInfo
    {
        public string? Overview { get; set; }
        public List<MigrationStep>? Steps { get; set; }
    }

    private class MigrationStep
    {
        public int Step { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<string>? Tasks { get; set; }
    }
}
