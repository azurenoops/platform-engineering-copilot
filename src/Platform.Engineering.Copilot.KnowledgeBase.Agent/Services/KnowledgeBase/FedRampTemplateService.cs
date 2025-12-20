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

namespace Platform.Engineering.Copilot.KnowledgeBase.Agent.Services.KnowledgeBase;

/// <summary>
/// Service for FedRAMP templates and authorization guidance
/// </summary>
public class FedRampTemplateService : IFedRampTemplateService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<FedRampTemplateService> _logger;
    private const string CACHE_KEY = "fedramp_templates_data";
    private const string FEDRAMP_FILE = "KnowledgeBase/fedramp-templates.json";

    public FedRampTemplateService(IMemoryCache cache, ILogger<FedRampTemplateService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> GetSspSectionTemplateAsync(string sectionNumber, CancellationToken cancellationToken = default)
    {
        var data = await LoadFedRampDataAsync(cancellationToken);
        if (data?.SystemSecurityPlan?.Sections == null)
            return "SSP section templates not available.";

        var section = data.SystemSecurityPlan.Sections
            .FirstOrDefault(s => s.Section?.Equals(sectionNumber, StringComparison.OrdinalIgnoreCase) == true);

        if (section == null)
        {
            // List available sections
            var available = string.Join(", ", data.SystemSecurityPlan.Sections.Select(s => $"Section {s.Section}"));
            return $"SSP Section '{sectionNumber}' not found. Available sections: {available}";
        }

        var output = $@"# SSP Section {section.Section}: {section.Title}

## Description
{section.Description}

## Required Elements
{string.Join("\n", section.Content?.RequiredElements?.Select(e => $"- {e}") ?? Array.Empty<string>())}

{(section.Content?.ExampleContent != null ? $@"## Example Content
{(section.Content.ExampleContent is string str ? str : JsonSerializer.Serialize(section.Content.ExampleContent, new JsonSerializerOptions { WriteIndented = true }))}" : "")}

## Azure Mapping
{(section.Content?.AzureMapping != null ? (section.Content.AzureMapping is string azStr ? azStr : JsonSerializer.Serialize(section.Content.AzureMapping, new JsonSerializerOptions { WriteIndented = true })) : "See Azure documentation")}
";

        return output;
    }

    public async Task<string> GetControlNarrativeAsync(string controlId, CancellationToken cancellationToken = default)
    {
        var data = await LoadFedRampDataAsync(cancellationToken);
        if (data?.ControlNarrativeTemplates?.Narratives == null)
            return $"Control narrative for {controlId} not available.";

        var normalizedId = controlId.ToUpperInvariant().Trim();
        var narrative = data.ControlNarrativeTemplates.Narratives
            .FirstOrDefault(n => n.ControlId?.Equals(normalizedId, StringComparison.OrdinalIgnoreCase) == true);

        if (narrative == null)
        {
            var available = string.Join(", ", data.ControlNarrativeTemplates.Narratives.Select(n => n.ControlId));
            return $@"Control narrative for '{controlId}' not found in templates.

**Available Narrative Templates:** {available}

*Tip: These narratives are Azure-specific SSP templates. For general control information, use 'explain NIST control {controlId}'.*";
        }

        return $@"# SSP Control Narrative: {narrative.ControlId}

**Control:** {narrative.ControlTitle}

---

## Narrative Template

{narrative.NarrativeTemplate}

---

*Note: Replace [Organization Name] and bracketed placeholders with your specific information. 
This template is designed for Azure Government environments.*
";
    }

    public async Task<IReadOnlyList<string>> GetAuthorizationPackageChecklistAsync(CancellationToken cancellationToken = default)
    {
        var data = await LoadFedRampDataAsync(cancellationToken);
        if (data?.AuthorizationPackageChecklist?.Documents == null)
            return Array.Empty<string>();

        return data.AuthorizationPackageChecklist.Documents
            .Select(d => $"[{(d.Required == true ? "Required" : d.Required?.ToString() ?? "Optional")}] {d.Document}: {d.Description}")
            .ToList()
            .AsReadOnly();
    }

    public async Task<string> GetPoamTemplateAsync(CancellationToken cancellationToken = default)
    {
        var data = await LoadFedRampDataAsync(cancellationToken);
        if (data?.PoamTemplate?.Fields == null)
            return "POA&M template not available.";

        var output = @"# Plan of Action and Milestones (POA&M) Template

## Required Fields

| Field | Description | Example |
|-------|-------------|---------|
";
        foreach (var field in data.PoamTemplate.Fields)
        {
            output += $"| {field.Field} | {field.Description} | {field.Example ?? field.Values?.FirstOrDefault() ?? "N/A"} |\n";
        }

        if (data.PoamTemplate.AzureIntegration != null)
        {
            output += @"

## Azure Integration

### Discovery Sources
";
            foreach (var source in data.PoamTemplate.AzureIntegration.Discovery ?? new List<string>())
            {
                output += $"- {source}\n";
            }

            output += @"
### Tracking Options
";
            foreach (var tracker in data.PoamTemplate.AzureIntegration.Tracking ?? new List<string>())
            {
                output += $"- {tracker}\n";
            }
        }

        return output;
    }

    public async Task<string> GetContinuousMonitoringRequirementsAsync(CancellationToken cancellationToken = default)
    {
        var data = await LoadFedRampDataAsync(cancellationToken);
        if (data?.ContinuousMonitoring?.Requirements == null)
            return "Continuous monitoring requirements not available.";

        var output = @"# FedRAMP Continuous Monitoring Requirements

## Requirements

";
        foreach (var req in data.ContinuousMonitoring.Requirements)
        {
            output += $@"### {req.Requirement}
**Frequency:** {req.Frequency}

**Azure Implementation:**
{string.Join("\n", req.AzureImplementation?.Select(a => $"- {a}") ?? Array.Empty<string>())}

";
        }

        if (data.ContinuousMonitoring.Deliverables != null)
        {
            output += "## Required Deliverables\n\n";
            foreach (var deliverable in data.ContinuousMonitoring.Deliverables)
            {
                output += $@"### {deliverable.Deliverable}
**Contents:**
{string.Join("\n", deliverable.Contents?.Select(c => $"- {c}") ?? Array.Empty<string>())}

**Azure Source:** {deliverable.AzureSource}

";
            }
        }

        return output;
    }

    public async Task<string> ExplainAuthorizationPathAsync(string path, CancellationToken cancellationToken = default)
    {
        var data = await LoadFedRampDataAsync(cancellationToken);
        if (data?.AuthorizationOverview?.AuthorizationPaths == null)
            return "Authorization path information not available.";

        var normalizedPath = path.ToLowerInvariant().Replace(" ", "");
        var authPath = data.AuthorizationOverview.AuthorizationPaths
            .FirstOrDefault(p => p.Path?.ToLowerInvariant().Replace(" ", "").Contains(normalizedPath) == true);

        if (authPath == null)
        {
            var available = string.Join(", ", data.AuthorizationOverview.AuthorizationPaths.Select(p => p.Path));
            return $"Authorization path '{path}' not found. Available: {available}";
        }

        return $@"# FedRAMP Authorization: {authPath.Path}

## Description
{authPath.Description}

## Timeline
**Typical Duration:** {authPath.Timeline}

## Requirements
{string.Join("\n", authPath.Requirements?.Select(r => $"- {r}") ?? Array.Empty<string>())}

---

{data.AuthorizationOverview.Description}
";
    }

    private async Task<FedRampData?> LoadFedRampDataAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CACHE_KEY, out FedRampData? cachedData))
            return cachedData;

        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, FEDRAMP_FILE);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("FedRAMP templates file not found: {FilePath}", filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var data = JsonSerializer.Deserialize<FedRampData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _cache.Set(CACHE_KEY, data, TimeSpan.FromHours(24));
            _logger.LogInformation("Loaded FedRAMP templates data");

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading FedRAMP templates data");
            return null;
        }
    }

    // Internal data models
    private class FedRampData
    {
        public AuthorizationOverview? AuthorizationOverview { get; set; }
        public SystemSecurityPlan? SystemSecurityPlan { get; set; }
        public ControlNarrativeTemplates? ControlNarrativeTemplates { get; set; }
        public PoamTemplate? PoamTemplate { get; set; }
        public ContinuousMonitoring? ContinuousMonitoring { get; set; }
        public AuthorizationPackageChecklist? AuthorizationPackageChecklist { get; set; }
    }

    private class AuthorizationOverview
    {
        public string? Description { get; set; }
        public List<AuthorizationPath>? AuthorizationPaths { get; set; }
    }

    private class AuthorizationPath
    {
        public string? Path { get; set; }
        public string? Description { get; set; }
        public string? Timeline { get; set; }
        public List<string>? Requirements { get; set; }
    }

    private class SystemSecurityPlan
    {
        public List<SspSection>? Sections { get; set; }
    }

    private class SspSection
    {
        public string? Section { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public SspSectionContent? Content { get; set; }
    }

    private class SspSectionContent
    {
        public List<string>? RequiredElements { get; set; }
        public object? ExampleContent { get; set; }
        public object? AzureMapping { get; set; }
    }

    private class ControlNarrativeTemplates
    {
        public List<ControlNarrative>? Narratives { get; set; }
    }

    private class ControlNarrative
    {
        public string? ControlId { get; set; }
        public string? ControlTitle { get; set; }
        public string? NarrativeTemplate { get; set; }
    }

    private class PoamTemplate
    {
        public List<PoamField>? Fields { get; set; }
        public AzureIntegration? AzureIntegration { get; set; }
    }

    private class PoamField
    {
        public string? Field { get; set; }
        public string? Description { get; set; }
        public string? Example { get; set; }
        public List<string>? Values { get; set; }
    }

    private class AzureIntegration
    {
        public List<string>? Discovery { get; set; }
        public List<string>? Tracking { get; set; }
    }

    private class ContinuousMonitoring
    {
        public List<MonitoringRequirement>? Requirements { get; set; }
        public List<MonitoringDeliverable>? Deliverables { get; set; }
    }

    private class MonitoringRequirement
    {
        public string? Requirement { get; set; }
        public string? Frequency { get; set; }
        public List<string>? AzureImplementation { get; set; }
    }

    private class MonitoringDeliverable
    {
        public string? Deliverable { get; set; }
        public List<string>? Contents { get; set; }
        public string? AzureSource { get; set; }
    }

    private class AuthorizationPackageChecklist
    {
        public List<ChecklistDocument>? Documents { get; set; }
    }

    private class ChecklistDocument
    {
        public string? Document { get; set; }
        public bool? Required { get; set; }
        public string? Description { get; set; }
    }
}
