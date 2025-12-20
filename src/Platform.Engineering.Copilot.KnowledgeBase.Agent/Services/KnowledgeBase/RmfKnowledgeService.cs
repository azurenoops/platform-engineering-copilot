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
/// Service for RMF process and documentation guidance
/// </summary>
public class RmfKnowledgeService : IRmfKnowledgeService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<RmfKnowledgeService> _logger;
    private const string CACHE_KEY = "rmf_process_data";
    private const string ENHANCED_CACHE_KEY = "rmf_enhanced_data";
    private const string RMF_FILE = "KnowledgeBase/rmf-process.json";
    private const string RMF_ENHANCED_FILE = "KnowledgeBase/rmf-process-enhanced.json";

    public RmfKnowledgeService(IMemoryCache cache, ILogger<RmfKnowledgeService> logger)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RmfProcess?> GetRmfStepAsync(string step, CancellationToken cancellationToken = default)
    {
        var data = await LoadRmfDataAsync(cancellationToken);
        return data?.RmfSteps?.FirstOrDefault(s => s.Step == step);
    }

    public async Task<IReadOnlyList<RmfProcess>> GetAllRmfStepsAsync(CancellationToken cancellationToken = default)
    {
        var data = await LoadRmfDataAsync(cancellationToken);
        return data?.RmfSteps?.AsReadOnly() ?? Array.Empty<RmfProcess>().ToList().AsReadOnly();
    }

    public async Task<string> ExplainRmfProcessAsync(string? specificStep = null, CancellationToken cancellationToken = default)
    {
        var data = await LoadRmfDataAsync(cancellationToken);
        
        if (data == null)
            return "RMF process data not available.";

        if (string.IsNullOrWhiteSpace(specificStep))
        {
            // Explain entire RMF process
            return $@"# Risk Management Framework (RMF)

{data.RmfOverview.Description}

**Purpose:** {data.RmfOverview.Purpose}

**Applicability:** {data.RmfOverview.Applicability}

## RMF Steps

{string.Join("\n\n", data.RmfSteps.Select(s => $@"### Step {s.Step}: {s.Title}
{s.Description}

**Key Activities:**
{string.Join("\n", s.Activities.Select(a => $"- {a}"))}

**Deliverables:**
{string.Join("\n", s.Outputs.Select(o => $"- {o}"))}

**Responsible Roles:** {string.Join(", ", s.Roles)}
**Reference:** {s.DodInstruction}"))}

## Key Principles
{string.Join("\n", data.RmfOverview.KeyPrinciples.Select(p => $"- {p}"))}
";
        }
        else
        {
            // Explain specific step
            var step = await GetRmfStepAsync(specificStep, cancellationToken);
            if (step == null)
                return $"RMF Step {specificStep} not found. Valid steps are 1-6.";

            return $@"# RMF Step {step.Step}: {step.Title}

{step.Description}

## Activities

{string.Join("\n", step.Activities.Select((a, i) => $"{i + 1}. {a}"))}

## Key Deliverables

{string.Join("\n", step.Outputs.Select(o => $"- {o}"))}

## Responsible Roles

{string.Join("\n", step.Roles.Select(r => $"- {r}"))}

## DoD Guidance

{step.DodInstruction}

## Next Steps

{(int.Parse(step.Step) < 6 
    ? $"After completing Step {step.Step}, proceed to Step {int.Parse(step.Step) + 1}." 
    : "Step 6 (Monitor) is an ongoing activity throughout the system lifecycle.")}
";
        }
    }

    public async Task<List<string>> GetRmfOutputsForStepAsync(string step, CancellationToken cancellationToken = default)
    {
        var rmfStep = await GetRmfStepAsync(step, cancellationToken);
        return rmfStep?.Outputs ?? new List<string>();
    }

    public async Task<string> GetServiceSpecificGuidanceAsync(string service, CancellationToken cancellationToken = default)
    {
        var data = await LoadEnhancedRmfDataAsync(cancellationToken);
        if (data?.ServiceSpecificGuidance == null)
            return $"Service-specific guidance for '{service}' not available.";

        var normalizedService = service.ToLowerInvariant().Replace(" ", "").Replace(".", "");
        
        // Map common variations to the actual keys
        var serviceKey = normalizedService switch
        {
            "navy" or "usnavy" => "navy",
            "army" or "usarmy" => "army",
            "airforce" or "usaf" or "usairforce" => "airForce",
            "disa" => "disa",
            _ => normalizedService
        };

        if (!data.ServiceSpecificGuidance.TryGetValue(serviceKey, out var guidance))
            return $"No specific RMF guidance found for '{service}'. Available: Navy, Army, Air Force, DISA.";

        var output = $@"# RMF Guidance: {guidance.Service}

**Authorizing Organization:** {guidance.AuthorizingOrganization}

## Key Contacts
{string.Join("\n", guidance.KeyContacts?.Select(c => $"- {c}") ?? Array.Empty<string>())}

## Service-Specific Requirements
{string.Join("\n", guidance.SpecificRequirements?.Select(r => $"- {r}") ?? Array.Empty<string>())}

## Typical Timeline
{guidance.TypicalTimeline}

## Tools
{string.Join("\n", guidance.Tools?.Select(t => $"- {t}") ?? Array.Empty<string>())}
";

        if (guidance.Workflow != null && guidance.Workflow.Any())
        {
            output += "\n## Workflow Steps\n\n";
            foreach (var step in guidance.Workflow)
            {
                output += $@"### Step {step.Step}: {step.Activity}
{step.Description}
**Duration:** {step.Duration}
**Artifacts:** {string.Join(", ", step.Artifacts ?? new List<string>())}

";
            }
        }

        return output;
    }

    public async Task<string> GetRmfTimelineAsync(string authorizationType, CancellationToken cancellationToken = default)
    {
        var data = await LoadEnhancedRmfDataAsync(cancellationToken);
        if (data?.RmfTimelines?.AverageTimelines == null)
            return "RMF timeline information not available.";

        var normalizedType = authorizationType.ToLowerInvariant().Replace(" ", "");
        
        var timelineKey = normalizedType switch
        {
            "newsystem" or "new" => "newSystem",
            "cloudmigration" or "cloud" or "migration" or "fedramp" => "cloudMigration",
            "reciprocity" => "reciprocity",
            _ => normalizedType
        };

        if (!data.RmfTimelines.AverageTimelines.TryGetValue(timelineKey, out var timeline))
            return $"Timeline not found for '{authorizationType}'. Available: New System, Cloud Migration, Reciprocity.";

        var output = $@"# RMF Timeline: {timeline.Category}

**Total Duration:** {timeline.TotalDuration}

{timeline.Note ?? ""}

## Phases
";
        foreach (var phase in timeline.Phases ?? new List<RmfPhase>())
        {
            output += $"- **{phase.Phase}:** {phase.Duration}{(phase.Parallelizable ? " (can run in parallel)" : "")}\n";
        }

        if (data.RmfTimelines.AcceleratedProcesses != null)
        {
            output += "\n## Accelerated Options\n\n";
            foreach (var accel in data.RmfTimelines.AcceleratedProcesses)
            {
                output += $@"### {accel.Name}
- **Duration:** {accel.Duration}
- **Purpose:** {accel.Purpose}
- **Restrictions:** {accel.Restrictions}
- **Max Duration:** {accel.MaxDuration}

";
            }
        }

        return output;
    }

    public async Task<IReadOnlyList<string>> GetRmfArtifactsAsync(bool requiredOnly = false, CancellationToken cancellationToken = default)
    {
        var data = await LoadEnhancedRmfDataAsync(cancellationToken);
        var artifacts = new List<string>();

        if (data?.RmfArtifacts?.RequiredDocuments != null)
        {
            foreach (var doc in data.RmfArtifacts.RequiredDocuments)
            {
                artifacts.Add($"[Required] {doc.Artifact}: {doc.Description}");
            }
        }

        if (!requiredOnly && data?.RmfArtifacts?.SupportingDocuments != null)
        {
            foreach (var doc in data.RmfArtifacts.SupportingDocuments)
            {
                artifacts.Add($"[Supporting] {doc.Artifact}: {doc.Description}");
            }
        }

        return artifacts.AsReadOnly();
    }

    private async Task<RmfData?> LoadRmfDataAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CACHE_KEY, out RmfData? cachedData))
            return cachedData;

        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, RMF_FILE);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("RMF process file not found: {FilePath}", filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var data = JsonSerializer.Deserialize<RmfData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _cache.Set(CACHE_KEY, data, TimeSpan.FromHours(24));
            _logger.LogInformation("Loaded RMF process data with {StepCount} steps", data?.RmfSteps?.Count ?? 0);

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading RMF process data");
            return null;
        }
    }

    private class RmfData
    {
        public List<RmfProcess> RmfSteps { get; set; } = new();
        public RmfOverview RmfOverview { get; set; } = new();
    }

    private class RmfOverview
    {
        public string Description { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Applicability { get; set; } = string.Empty;
        public List<string> KeyPrinciples { get; set; } = new();
    }

    // Enhanced RMF data loader and models
    private async Task<EnhancedRmfData?> LoadEnhancedRmfDataAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(ENHANCED_CACHE_KEY, out EnhancedRmfData? cachedData))
            return cachedData;

        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, RMF_ENHANCED_FILE);
            if (!File.Exists(filePath))
            {
                _logger.LogWarning("Enhanced RMF file not found: {FilePath}", filePath);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var data = JsonSerializer.Deserialize<EnhancedRmfData>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _cache.Set(ENHANCED_CACHE_KEY, data, TimeSpan.FromHours(24));
            _logger.LogInformation("Loaded enhanced RMF data");

            return data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading enhanced RMF data");
            return null;
        }
    }

    private class EnhancedRmfData
    {
        public RmfTimelines? RmfTimelines { get; set; }
        public Dictionary<string, ServiceGuidance>? ServiceSpecificGuidance { get; set; }
        public RmfArtifacts? RmfArtifacts { get; set; }
    }

    private class RmfTimelines
    {
        public Dictionary<string, TimelineInfo>? AverageTimelines { get; set; }
        public List<AcceleratedProcess>? AcceleratedProcesses { get; set; }
    }

    private class TimelineInfo
    {
        public string Category { get; set; } = string.Empty;
        public string TotalDuration { get; set; } = string.Empty;
        public List<RmfPhase>? Phases { get; set; }
        public string? Note { get; set; }
    }

    private class RmfPhase
    {
        public string Phase { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public bool Parallelizable { get; set; }
    }

    private class AcceleratedProcess
    {
        public string Name { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Restrictions { get; set; } = string.Empty;
        public string MaxDuration { get; set; } = string.Empty;
    }

    private class ServiceGuidance
    {
        public string Service { get; set; } = string.Empty;
        public string AuthorizingOrganization { get; set; } = string.Empty;
        public List<string>? KeyContacts { get; set; }
        public List<string>? SpecificRequirements { get; set; }
        public string TypicalTimeline { get; set; } = string.Empty;
        public List<string>? Tools { get; set; }
        public List<WorkflowStepInfo>? Workflow { get; set; }
    }

    private class WorkflowStepInfo
    {
        public int Step { get; set; }
        public string Activity { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public List<string>? Artifacts { get; set; }
    }

    private class RmfArtifacts
    {
        public List<ArtifactInfo>? RequiredDocuments { get; set; }
        public List<ArtifactInfo>? SupportingDocuments { get; set; }
    }

    private class ArtifactInfo
    {
        public string Artifact { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? AzureSupport { get; set; }
    }
}
