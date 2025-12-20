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
/// Service for STIG controls and compliance checking
/// </summary>
public class StigKnowledgeService : IStigKnowledgeService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<StigKnowledgeService> _logger;
    private readonly IDoDInstructionService _dodInstructionService;
    private const string CACHE_KEY = "stig_controls_data";
    private const string WINDOWS_STIG_CACHE_KEY = "windows_stig_data";
    private const string STIG_FILE = "KnowledgeBase/stig-controls.json";
    private const string WINDOWS_STIG_FILE = "KnowledgeBase/windows-server-stig-azure.json";

    public StigKnowledgeService(
        IMemoryCache cache, 
        ILogger<StigKnowledgeService> logger,
        IDoDInstructionService dodInstructionService)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dodInstructionService = dodInstructionService ?? throw new ArgumentNullException(nameof(dodInstructionService));
    }

    public async Task<StigControl?> GetStigControlAsync(string stigId, CancellationToken cancellationToken = default)
    {
        var data = await LoadStigDataAsync(cancellationToken);
        return data?.StigControls?.FirstOrDefault(s => 
            s.StigId.Equals(stigId, StringComparison.OrdinalIgnoreCase) ||
            s.VulnId.Equals(stigId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<StigControl>> GetStigsByNistControlAsync(string nistControlId, CancellationToken cancellationToken = default)
    {
        var data = await LoadStigDataAsync(cancellationToken);
        return data?.StigControls?
            .Where(s => s.NistControls.Any(n => n.Equals(nistControlId, StringComparison.OrdinalIgnoreCase)))
            .ToList()
            .AsReadOnly() ?? Array.Empty<StigControl>().ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<StigControl>> SearchStigsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var data = await LoadStigDataAsync(cancellationToken);
        
        if (data?.StigControls == null)
            return Array.Empty<StigControl>().ToList().AsReadOnly();

        return data.StigControls
            .Where(s => 
                s.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                s.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                s.StigId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
                s.Category.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    public async Task<IReadOnlyList<StigControl>> GetStigsBySeverityAsync(StigSeverity severity, CancellationToken cancellationToken = default)
    {
        var data = await LoadStigDataAsync(cancellationToken);
        return data?.StigControls?
            .Where(s => s.Severity == severity)
            .ToList()
            .AsReadOnly() ?? Array.Empty<StigControl>().ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<StigControl>> GetAllStigsAsync(CancellationToken cancellationToken = default)
    {
        var data = await LoadStigDataAsync(cancellationToken);
        return data?.StigControls?.AsReadOnly() ?? Array.Empty<StigControl>().ToList().AsReadOnly();
    }

    public async Task<IReadOnlyList<StigControl>> GetStigsByServiceTypeAsync(StigServiceType serviceType, CancellationToken cancellationToken = default)
    {
        var data = await LoadStigDataAsync(cancellationToken);
        return data?.StigControls?
            .Where(s => s.ServiceType == serviceType)
            .ToList()
            .AsReadOnly() ?? Array.Empty<StigControl>().ToList().AsReadOnly();
    }

    public async Task<ControlMapping?> GetControlMappingAsync(string nistControlId, CancellationToken cancellationToken = default)
    {
        var data = await LoadStigDataAsync(cancellationToken);
        return data?.ControlMappings?.FirstOrDefault(m => 
            m.NistControlId.Equals(nistControlId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<string> ExplainStigAsync(string stigId, CancellationToken cancellationToken = default)
    {
        var stig = await GetStigControlAsync(stigId, cancellationToken);
        
        if (stig == null)
            return $"STIG {stigId} not found in knowledge base.";

        return $@"# {stig.StigId}: {stig.Title}

**Severity:** {stig.Severity}
**Category:** {stig.Category}
**STIG Family:** {stig.StigFamily}

## Description

{stig.Description}

## NIST 800-53 Controls

{string.Join(", ", stig.NistControls)}

## CCI References

{string.Join(", ", stig.CciRefs)}

## Check Procedure

{stig.CheckText}

## Remediation

{stig.FixText}

## Azure Implementation

**Service:** {stig.AzureImplementation.GetValueOrDefault("service", "N/A")}
**Configuration:** {stig.AzureImplementation.GetValueOrDefault("configuration", "N/A")}
**Azure Policy:** {stig.AzureImplementation.GetValueOrDefault("azurePolicy", "N/A")}

### Automation Command

```bash
{stig.AzureImplementation.GetValueOrDefault("automation", "Manual configuration required")}
```

## Compliance Mapping

- **Rule ID:** {stig.RuleId}
- **Vuln ID:** {stig.VulnId}
- **STIG ID:** {stig.StigId}
";
    }

    public async Task<IReadOnlyList<string>> GetNistControlsForStigAsync(string stigId, CancellationToken cancellationToken = default)
    {
        var stig = await GetStigControlAsync(stigId, cancellationToken);
        
        if (stig == null)
        {
            _logger.LogWarning("STIG {StigId} not found for NIST control lookup", stigId);
            return Array.Empty<string>();
        }

        return stig.NistControls;
    }

    public async Task<IReadOnlyList<StigControl>> GetAzureStigsAsync(string azureService, CancellationToken cancellationToken = default)
    {
        var data = await LoadStigDataAsync(cancellationToken);
        
        if (data?.StigControls == null)
        {
            _logger.LogWarning("No STIG data available for Azure service filtering");
            return Array.Empty<StigControl>();
        }

        var azureStigs = data.StigControls
            .Where(s => s.AzureImplementation != null && 
                       s.AzureImplementation.TryGetValue("service", out var service) &&
                       service.Equals(azureService, StringComparison.OrdinalIgnoreCase))
            .ToList();

        _logger.LogInformation("Found {Count} STIGs for Azure service {Service}", azureStigs.Count, azureService);
        return azureStigs.AsReadOnly();
    }

    public async Task<string> GetStigCrossReferenceAsync(string stigId, CancellationToken cancellationToken = default)
    {
        var stig = await GetStigControlAsync(stigId, cancellationToken);
        
        if (stig == null)
            return $"STIG {stigId} not found in knowledge base.";

        var output = $@"# STIG Cross-Reference: {stig.StigId}

**Title:** {stig.Title}
**Severity:** {stig.Severity}
**Category:** {stig.Category}

## NIST 800-53 Controls

{string.Join(", ", stig.NistControls)}

## CCI References

{string.Join(", ", stig.CciRefs)}

";

        // Add DoD Instruction mappings for each NIST control
        if (stig.NistControls.Any())
        {
            output += "## DoD Instructions\n\n";
            
            foreach (var nistControl in stig.NistControls)
            {
                var dodInstructions = await _dodInstructionService.GetInstructionsByControlAsync(nistControl, cancellationToken);
                
                if (dodInstructions.Any())
                {
                    output += $"### {nistControl}\n\n";
                    foreach (var instruction in dodInstructions)
                    {
                        output += $"- **{instruction.Title}**\n";
                        
                        var mapping = instruction.ControlMappings?
                            .FirstOrDefault(m => m.NistControlId.Equals(nistControl, StringComparison.OrdinalIgnoreCase));
                        
                        if (mapping != null)
                        {
                            output += $"  - Section: {mapping.Section}\n";
                            output += $"  - Requirement: {mapping.Requirement}\n";
                            if (!string.IsNullOrEmpty(mapping.ImpactLevel))
                                output += $"  - Impact Level: {mapping.ImpactLevel}\n";
                        }
                        
                        output += $"  - URL: {instruction.Url}\n\n";
                    }
                }
            }
        }

        // Add Azure implementation details
        if (stig.AzureImplementation != null && stig.AzureImplementation.Any())
        {
            output += "## Azure Implementation\n\n";
            output += $"**Service:** {stig.AzureImplementation.GetValueOrDefault("service", "N/A")}\n";
            output += $"**Configuration:** {stig.AzureImplementation.GetValueOrDefault("configuration", "N/A")}\n";
            
            if (stig.AzureImplementation.TryGetValue("azurePolicy", out var policy))
                output += $"**Azure Policy:** {policy}\n";
            
            if (stig.AzureImplementation.TryGetValue("automation", out var automation))
            {
                output += "\n### Automation\n\n```bash\n";
                output += automation;
                output += "\n```\n";
            }
        }

        return output;
    }

    private async Task<StigData?> LoadStigDataAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CACHE_KEY, out StigData? cachedData))
            return cachedData;

        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, STIG_FILE);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("STIG controls file not found: {FilePath}", filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var data = JsonSerializer.Deserialize<StigData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            });

            _cache.Set(CACHE_KEY, data, TimeSpan.FromHours(24));
            _logger.LogInformation("Loaded STIG data with {ControlCount} controls", data?.StigControls?.Count ?? 0);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading STIG data");
            return null;
        }
    }

    private class StigData
    {
        public List<StigControl> StigControls { get; set; } = new();
        public List<StigFamily> StigFamilies { get; set; } = new();
        public List<ControlMapping> ControlMappings { get; set; } = new();
    }

    private class StigFamily
    {
        public string Family { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
    }

    // Windows Server STIG methods
    public async Task<string> GetWindowsServerStigGuidanceAsync(string stigId, CancellationToken cancellationToken = default)
    {
        var data = await LoadWindowsStigDataAsync(cancellationToken);
        if (data?.StigCategories == null)
            return $"Windows Server STIG {stigId} not found.";

        foreach (var category in data.StigCategories)
        {
            var control = category.StigControls?.FirstOrDefault(c => 
                c.StigId?.Equals(stigId, StringComparison.OrdinalIgnoreCase) == true);
            
            if (control != null)
            {
                return $@"# Windows Server STIG: {control.StigId}

**Title:** {control.Title}
**Severity:** {control.Severity}
**Category:** {category.Category}

## Description
{category.Description}

## NIST Mapping
{string.Join(", ", control.NistMapping ?? new List<string>())}

## Configuration

{(control.RegistryPath != null ? $@"### Registry Setting
- **Path:** `{control.RegistryPath}`
- **Value:** `{control.RegistryValue}`
- **Required:** `{control.RequiredValue}`" : "")}

{(control.GpoPath != null ? $@"### Group Policy Setting
- **Path:** `{control.GpoPath}`
- **Setting:** `{control.SettingName}`
- **Required Value:** `{control.RequiredValue}`" : "")}

## Azure Guest Configuration
{(control.AzureGuestConfiguration != null ? $@"- **Policy:** {control.AzureGuestConfiguration.PolicyName}
- **Status:** {control.AzureGuestConfiguration.ComplianceStatus}
- **Remediation:** {control.AzureGuestConfiguration.Remediation}" : "Not available")}

{(control.AzureMonitorIntegration != null ? $@"
## Azure Monitor Integration
- **Event ID:** {control.AzureMonitorIntegration.EventId}
- **Log Analytics Table:** {control.AzureMonitorIntegration.LogAnalyticsTable}
- **Sentinel Detection:** {control.AzureMonitorIntegration.SentinelDetection}" : "")}
";
            }
        }

        return $"Windows Server STIG '{stigId}' not found. Try searching with 'search windows stigs [term]'.";
    }

    public async Task<IReadOnlyList<string>> SearchWindowsStigsAsync(string searchTerm, CancellationToken cancellationToken = default)
    {
        var data = await LoadWindowsStigDataAsync(cancellationToken);
        if (data?.StigCategories == null)
            return Array.Empty<string>();

        var results = new List<string>();
        var term = searchTerm.ToLowerInvariant();

        foreach (var category in data.StigCategories)
        {
            if (category.StigControls == null) continue;

            foreach (var control in category.StigControls)
            {
                if (control.StigId?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
                    control.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) == true ||
                    category.Category?.Contains(term, StringComparison.OrdinalIgnoreCase) == true)
                {
                    results.Add($"{control.StigId}: {control.Title} [{control.Severity}]");
                }
            }
        }

        return results.AsReadOnly();
    }

    public async Task<string> GetGuestConfigurationPolicyAsync(string stigCategory, CancellationToken cancellationToken = default)
    {
        var data = await LoadWindowsStigDataAsync(cancellationToken);
        if (data?.AzurePolicyInitiatives?.Initiatives == null)
            return "Guest Configuration policy information not available.";

        var output = $@"# Azure Guest Configuration for Windows Server STIG

## Available Policy Initiatives

";
        foreach (var initiative in data.AzurePolicyInitiatives.Initiatives)
        {
            output += $@"### {initiative.Name}
- **Policy ID:** `{initiative.PolicyDefinitionId}`
- **Description:** {initiative.Description}
{(initiative.ControlsCovered != null ? $"- **Controls Covered:** {initiative.ControlsCovered}" : "")}

";
        }

        if (data.ImplementationGuidance?.Steps != null)
        {
            output += "## Implementation Steps\n\n";
            foreach (var step in data.ImplementationGuidance.Steps)
            {
                output += $@"### Step {step.Step}: {step.Title}
{string.Join("\n", step.Actions?.Select(a => $"- {a}") ?? Array.Empty<string>())}
{(step.AzureCliExample != null ? $"\n**Azure CLI:**\n```bash\n{step.AzureCliExample}\n```" : "")}

";
            }
        }

        return output;
    }

    private async Task<WindowsStigData?> LoadWindowsStigDataAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(WINDOWS_STIG_CACHE_KEY, out WindowsStigData? cachedData))
            return cachedData;

        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, WINDOWS_STIG_FILE);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Windows STIG file not found: {FilePath}", filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var data = JsonSerializer.Deserialize<WindowsStigData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _cache.Set(WINDOWS_STIG_CACHE_KEY, data, TimeSpan.FromHours(24));
            _logger.LogInformation("Loaded Windows Server STIG data");

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Windows Server STIG data");
            return null;
        }
    }

    // Windows STIG data models
    private class WindowsStigData
    {
        public List<WindowsStigCategory>? StigCategories { get; set; }
        public AzurePolicyInitiatives? AzurePolicyInitiatives { get; set; }
        public ImplementationGuidance? ImplementationGuidance { get; set; }
    }

    private class WindowsStigCategory
    {
        public string? Category { get; set; }
        public string? Description { get; set; }
        public List<WindowsStigControl>? StigControls { get; set; }
    }

    private class WindowsStigControl
    {
        public string? StigId { get; set; }
        public string? Title { get; set; }
        public string? Severity { get; set; }
        public List<string>? NistMapping { get; set; }
        public string? RegistryPath { get; set; }
        public string? RegistryValue { get; set; }
        public string? RequiredValue { get; set; }
        public string? GpoPath { get; set; }
        public string? SettingName { get; set; }
        public GuestConfigurationInfo? AzureGuestConfiguration { get; set; }
        public AzureMonitorInfo? AzureMonitorIntegration { get; set; }
    }

    private class GuestConfigurationInfo
    {
        public string? PolicyName { get; set; }
        public string? ComplianceStatus { get; set; }
        public string? Remediation { get; set; }
    }

    private class AzureMonitorInfo
    {
        public string? EventId { get; set; }
        public string? LogAnalyticsTable { get; set; }
        public string? SentinelDetection { get; set; }
    }

    private class AzurePolicyInitiatives
    {
        public List<PolicyInitiative>? Initiatives { get; set; }
    }

    private class PolicyInitiative
    {
        public string? Name { get; set; }
        public string? PolicyDefinitionId { get; set; }
        public string? Description { get; set; }
        public string? ControlsCovered { get; set; }
    }

    private class ImplementationGuidance
    {
        public List<ImplementationStep>? Steps { get; set; }
    }

    private class ImplementationStep
    {
        public int Step { get; set; }
        public string? Title { get; set; }
        public List<string>? Actions { get; set; }
        public string? AzureCliExample { get; set; }
    }
}
