using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.DocumentProcessing.Models;
using Platform.Engineering.Copilot.DocumentProcessing.Services;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.DocumentProcessing.Analyzers;

/// <summary>
/// Analyzer for Navy Flankspeed platform compatibility assessment.
/// Evaluates architecture documents for compatibility with Navy's Flankspeed collaboration platform,
/// identifies required patterns, and assesses integration effort for migration to Flankspeed environment.
/// Flankspeed is Navy's Microsoft-based cloud collaboration platform providing secure communication,
/// file sharing, and productivity tools in a government cloud environment.
/// </summary>
public class NavyFlankspeedAnalyzer : INavyFlankspeedAnalyzer
{
    private readonly ILogger<NavyFlankspeedAnalyzer> _logger;
    private readonly IConfiguration _configuration;
    private readonly Dictionary<string, string> _flankspeedPatterns;
    private readonly Dictionary<string, string> _flankspeedControls;

    /// <summary>
    /// Initializes a new instance of the NavyFlankspeedAnalyzer.
    /// Sets up predefined Flankspeed patterns and security controls for compatibility assessment.
    /// </summary>
    /// <param name="logger">Logger for diagnostic and audit trail information</param>
    /// <param name="configuration">Application configuration for customizable analysis parameters</param>
    public NavyFlankspeedAnalyzer(
        ILogger<NavyFlankspeedAnalyzer> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _flankspeedPatterns = InitializeFlankspeedPatterns();
        _flankspeedControls = InitializeFlankspeedControls();
    }

    /// <summary>
    /// Performs comprehensive Flankspeed compatibility analysis on a document.
    /// Evaluates architecture patterns, technology stack alignment, and security controls
    /// to determine how well the documented system would integrate with Navy Flankspeed platform.
    /// </summary>
    /// <param name="documentAnalysis">Parsed document containing architecture and security information</param>
    /// <returns>Compliance analysis result with compatibility findings and recommendations</returns>
    public async Task<ComplianceAnalysisResult> AnalyzeFlankspeedCompatibilityAsync(DocumentAnalysis documentAnalysis)
    {
        _logger.LogInformation("Analyzing Navy Flankspeed compatibility for document {DocumentId}", 
            documentAnalysis.DocumentId);

        var summary = "Navy Flankspeed platform compatibility analysis initiated";

        // Create a compliance analysis result for Flankspeed platform
        var analysisResult = new ComplianceAnalysisResult
        {
            DocumentId = Guid.Parse(documentAnalysis.DocumentId),
            ComplianceScore = 0.0,
            OverallStatus = Platform.Engineering.Copilot.Core.Models.ComplianceStatus.PartiallyCompliant,
            Summary = summary,
            Gaps = new List<Platform.Engineering.Copilot.Core.Models.ComplianceGap>(),
            Recommendations = new List<Platform.Engineering.Copilot.Core.Models.ComplianceRecommendation>(),
            AnalyzedAt = DateTime.UtcNow
        };

        // Identify required controls and patterns
        var requiredControls = await IdentifyRequiredFlankspeedControlsAsync(documentAnalysis);
        var patterns = new List<string>();
        
        if (documentAnalysis.ArchitectureAnalysis != null)
        {
            patterns = await IdentifyFlankspeedPatternsAsync(documentAnalysis.ArchitectureAnalysis);
        }

        // Assess integration effort
        var integrationData = await AssessIntegrationEffortAsync(documentAnalysis);

        // Create gaps and recommendations based on analysis
        analysisResult.Gaps.AddRange(requiredControls.Select(control => new Platform.Engineering.Copilot.Core.Models.ComplianceGap
        {
            ControlId = control.Split(':')[0],
            Description = $"Flankspeed control {control} requires assessment",
            Severity = Platform.Engineering.Copilot.Core.Models.GapSeverity.Medium,
            ImpactAssessment = "Medium impact on Flankspeed integration"
        }));

        analysisResult.Recommendations.AddRange(requiredControls.Select(control => new Platform.Engineering.Copilot.Core.Models.ComplianceRecommendation
        {
            Title = $"Implement {control.Split(':')[0]}",
            Description = "Review implementation of this Flankspeed-specific control",
            Priority = Platform.Engineering.Copilot.Core.Models.RecommendationPriority.Medium
        }));

        return analysisResult;
    }

    /// <summary>
    /// Identifies Navy Flankspeed-compatible architecture patterns in the analyzed system.
    /// Looks for Microsoft technology stack components, collaboration platforms, secure messaging,
    /// identity management systems, and cloud-first architectures that align with Flankspeed.
    /// </summary>
    /// <param name="architectureAnalysis">Architecture analysis containing system components and technology stack</param>
    /// <returns>List of identified Flankspeed-compatible patterns</returns>
    public async Task<List<string>> IdentifyFlankspeedPatternsAsync(ArchitectureAnalysis architectureAnalysis)
    {
        var identifiedPatterns = new List<string>();

        // Check for Microsoft technology stack (Flankspeed is primarily Microsoft-based)
        var hasMicrosoftStack = architectureAnalysis.TechnologyStack.Any(tech => 
            tech.Contains("Microsoft") || tech.Contains(".NET") || tech.Contains("Azure") || 
            tech.Contains("Office 365") || tech.Contains("SharePoint") || tech.Contains("Teams"));

        if (hasMicrosoftStack)
        {
            identifiedPatterns.Add("Microsoft Technology Stack");
        }

        // Check for SharePoint/collaboration patterns
        var hasCollaboration = architectureAnalysis.SystemComponents.Any(comp =>
            comp.Name.ToLowerInvariant().Contains("sharepoint") ||
            comp.Name.ToLowerInvariant().Contains("teams") ||
            comp.Name.ToLowerInvariant().Contains("collaboration"));

        if (hasCollaboration)
        {
            identifiedPatterns.Add("Collaboration Platform");
        }

        // Check for secure messaging patterns
        var hasSecureMessaging = architectureAnalysis.SystemComponents.Any(comp =>
            comp.Name.ToLowerInvariant().Contains("email") ||
            comp.Name.ToLowerInvariant().Contains("messaging") ||
            comp.Name.ToLowerInvariant().Contains("exchange"));

        if (hasSecureMessaging)
        {
            identifiedPatterns.Add("Secure Messaging");
        }

        // Check for identity management patterns
        var hasIdentityManagement = architectureAnalysis.SystemComponents.Any(comp =>
            comp.Name.ToLowerInvariant().Contains("active directory") ||
            comp.Name.ToLowerInvariant().Contains("identity") ||
            comp.Name.ToLowerInvariant().Contains("authentication"));

        if (hasIdentityManagement)
        {
            identifiedPatterns.Add("Identity Management");
        }

        // Check for cloud-first architecture
        var hasCloudComponents = architectureAnalysis.SystemComponents.Any(comp =>
            comp.Description.ToLowerInvariant().Contains("cloud") ||
            comp.Description.ToLowerInvariant().Contains("azure") ||
            comp.Description.ToLowerInvariant().Contains("saas"));

        if (hasCloudComponents)
        {
            identifiedPatterns.Add("Cloud-First Architecture");
        }

        return identifiedPatterns;
    }

    /// <summary>
    /// Assesses the effort required to integrate the documented system with Navy Flankspeed platform.
    /// Analyzes technology compatibility, security requirements, and architectural alignment
    /// to estimate integration complexity and provide implementation roadmap.
    /// </summary>
    /// <param name="documentAnalysis">Document analysis containing system architecture and security details</param>
    /// <returns>Dictionary containing compatibility issues, integration steps, effort estimates, and overall compatibility rating</returns>
    public async Task<Dictionary<string, object>> AssessIntegrationEffortAsync(DocumentAnalysis documentAnalysis)
    {
        var compatibilityIssues = new List<string>();
        var integrationSteps = new List<string>();
        var effortEstimate = "Medium";

        // Check architecture compatibility
        if (documentAnalysis.ArchitectureAnalysis != null)
        {
            var arch = documentAnalysis.ArchitectureAnalysis;

            // Check for non-Microsoft technologies that may need replacement
            var nonMicrosoftTech = arch.TechnologyStack.Where(tech =>
                !tech.Contains("Microsoft") && !tech.Contains(".NET") && !tech.Contains("Azure") &&
                (tech.Contains("Java") || tech.Contains("Oracle") || tech.Contains("MySQL") || 
                 tech.Contains("PostgreSQL") || tech.Contains("Linux"))).ToList();

            if (nonMicrosoftTech.Any())
            {
                compatibilityIssues.Add($"Non-Microsoft technologies detected: {string.Join(", ", nonMicrosoftTech)}");
                integrationSteps.Add("Evaluate migration path for non-Microsoft components to Flankspeed-compatible alternatives");
                effortEstimate = "High";
            }

            // Check for security boundaries
            if (!arch.SecurityBoundaries.Any())
            {
                compatibilityIssues.Add("No clear security boundaries defined");
                integrationSteps.Add("Define security zones compatible with Flankspeed security architecture");
            }

            // Check for data classification handling
            var hasClassifiedData = arch.DataFlows.Any(df => 
                df.Classification == SecurityClassification.Confidential || 
                df.Classification == SecurityClassification.Secret);

            if (hasClassifiedData)
            {
                integrationSteps.Add("Implement Flankspeed data classification and handling procedures");
                integrationSteps.Add("Configure appropriate security controls for classified data");
                effortEstimate = "High";
            }
        }

        return new Dictionary<string, object>
        {
            ["CompatibilityIssues"] = compatibilityIssues,
            ["IntegrationSteps"] = integrationSteps,
            ["EffortEstimate"] = effortEstimate,
            ["RequiredControls"] = _flankspeedControls.Keys.ToList(),
            ["OverallCompatibility"] = compatibilityIssues.Count == 0 ? "High" : compatibilityIssues.Count < 3 ? "Medium" : "Low"
        };
    }


    /// <summary>
    /// Identifies Navy Flankspeed-specific security controls required for the documented system.
    /// Analyzes document content for security-sensitive features like classified data handling,
    /// mobile access, and collaboration requirements to determine applicable Flankspeed controls.
    /// </summary>
    /// <param name="documentAnalysis">Document analysis containing system description and security requirements</param>
    /// <returns>List of required Flankspeed security control identifiers and descriptions</returns>
    private async Task<List<string>> IdentifyRequiredFlankspeedControlsAsync(DocumentAnalysis documentAnalysis)
    {
        var requiredControls = new List<string>();

        // Add all standard Flankspeed controls
        requiredControls.AddRange(_flankspeedControls.Keys);

        // Add specific controls based on document content
        var content = documentAnalysis.Content.FullText.ToLowerInvariant();

        // If document mentions classified data, add additional controls
        if (content.Contains("classified") || content.Contains("secret") || content.Contains("confidential"))
        {
            requiredControls.Add("FS-DATA-1: Classified Data Handling");
        }

        // If document mentions mobile access, add mobility controls
        if (content.Contains("mobile") || content.Contains("byod") || content.Contains("remote access"))
        {
            requiredControls.Add("FS-MOB-1: Mobile Device Management");
        }

        return requiredControls.Distinct().ToList();
    }

    /// <summary>
    /// Initializes the predefined Navy Flankspeed architecture patterns for compatibility assessment.
    /// These patterns represent common architectural components and configurations that align
    /// with Flankspeed's Microsoft-based collaboration and productivity platform.
    /// </summary>
    /// <returns>Dictionary mapping pattern keys to their descriptions</returns>
    private Dictionary<string, string> InitializeFlankspeedPatterns()
    {
        return new Dictionary<string, string>
        {
            ["microsoft_stack"] = "Microsoft Technology Stack - Architecture primarily based on Microsoft technologies, compatible with Flankspeed",
            ["collaboration_platform"] = "Collaboration Platform - SharePoint and Teams-based collaboration, core to Flankspeed",
            ["secure_messaging"] = "Secure Messaging - Email and messaging systems compatible with Flankspeed security requirements",
            ["identity_management"] = "Identity Management - Active Directory and Azure AD integration for Flankspeed",
            ["cloud_first"] = "Cloud-First Architecture - Azure-based cloud architecture aligned with Flankspeed strategy"
        };
    }

    /// <summary>
    /// Initializes the Navy Flankspeed-specific security controls for compliance assessment.
    /// These controls represent security requirements and integration points specific to
    /// Flankspeed's government cloud environment and Navy security standards.
    /// </summary>
    /// <returns>Dictionary mapping control identifiers to their implementation descriptions</returns>
    private Dictionary<string, string> InitializeFlankspeedControls()
    {
        return new Dictionary<string, string>
        {
            ["FS-AC-1"] = "Flankspeed Access Control Integration - Integrate with Flankspeed Active Directory and identity management services",
            ["FS-AU-1"] = "Flankspeed Audit Integration - Configure audit logging to Flankspeed central logging infrastructure",
            ["FS-SC-1"] = "Flankspeed Network Security - Align network security controls with Flankspeed security architecture",
            ["FS-SI-1"] = "Flankspeed Security Integration - Integrate with Flankspeed security monitoring and incident response",
            ["FS-CP-1"] = "Flankspeed Continuity Planning - Align business continuity plans with Flankspeed disaster recovery procedures",
            ["FS-IA-1"] = "Flankspeed Identity and Authentication - Implement Flankspeed identity management and authentication requirements"
        };
    }
}