using System.Text.Json.Serialization;

namespace Platform.Engineering.Copilot.Core.Models;

/// <summary>
/// Represents the status of a document in the analysis pipeline
/// </summary>
public enum DocumentStatus
{
    Uploaded,
    Processing,
    TextExtracted,
    Analyzing,
    Analyzed,
    Failed
}

/// <summary>
/// Represents the severity of a compliance gap
/// </summary>
public enum GapSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Represents the compliance status of a control
/// </summary>
public enum ControlComplianceStatus
{
    NotImplemented,
    PartiallyImplemented,
    FullyImplemented,
    NotApplicable
}

/// <summary>
/// Represents the type of NIST control
/// </summary>
public enum NistControlType
{
    Administrative,
    Technical,
    Physical,
    Operational
}



/// <summary>
/// Represents the priority level for recommendations
/// </summary>
public enum RecommendationPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

/// <summary>
/// Core document model for RMF compliance analysis
/// </summary>
public class AnalysisDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public byte[]? FileContent { get; set; }
    public string? ExtractedText { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a NIST 800-53 security control
/// </summary>
public class NistStandard
{
    public string Id { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public NistControlType Type { get; set; }
    public string Baseline { get; set; } = string.Empty;
    public string Version { get; set; } = "5.1";
    public List<string> RelatedControls { get; set; } = new();
    public Dictionary<string, string> Parameters { get; set; } = new();
}

/// <summary>
/// Represents the result of RMF compliance analysis
/// </summary>
public class ComplianceAnalysisResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    public double ComplianceScore { get; set; }
    public ComplianceStatus OverallStatus { get; set; }
    public string Summary { get; set; } = string.Empty;
    
    public List<ControlAssessment> ControlAssessments { get; set; } = new();
    public List<ComplianceGap> Gaps { get; set; } = new();
    public List<ComplianceRecommendation> Recommendations { get; set; } = new();
    
    public Dictionary<string, object> AnalysisMetadata { get; set; } = new();
}

/// <summary>
/// Assessment of a specific NIST control
/// </summary>
public class ControlAssessment
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlTitle { get; set; } = string.Empty;
    public string ControlFamily { get; set; } = string.Empty;
    public ControlComplianceStatus Status { get; set; }
    public double ImplementationScore { get; set; }
    public string Assessment { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public List<string> Findings { get; set; } = new();
    public DateTime AssessedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Represents a compliance gap identified during analysis
/// </summary>
public class ComplianceGap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ControlId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public GapSeverity Severity { get; set; }
    public string ImpactAssessment { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public List<string> AffectedSystems { get; set; } = new();
    public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Recommendation for improving compliance
/// </summary>
public class ComplianceRecommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RecommendationPriority Priority { get; set; }
    public string Category { get; set; } = string.Empty;
    public int EstimatedEffort { get; set; } // In hours
    public string ExpectedOutcome { get; set; } = string.Empty;
    public List<string> RelatedControls { get; set; } = new();
    public List<string> Resources { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of architecture analysis for platform integration
/// </summary>
public class ArchitectureAnalysisResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    
    public List<ArchitectureComponent> IdentifiedComponents { get; set; } = new();
    public List<IntegrationPoint> IntegrationPoints { get; set; } = new();
    public List<ArchitectureRecommendation> Recommendations { get; set; } = new();
    public List<ComplianceGap> SecurityGaps { get; set; } = new();
    
    public string Summary { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Architecture component identified in diagrams
/// </summary>
public class ArchitectureComponent
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Technologies { get; set; } = new();
    public List<string> SecurityControls { get; set; } = new();
    public Dictionary<string, string> Properties { get; set; } = new();
}

/// <summary>
/// Integration point between systems
/// </summary>
public class IntegrationPoint
{
    public string Source { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string DataFlow { get; set; } = string.Empty;
    public List<string> SecurityRequirements { get; set; } = new();
}

/// <summary>
/// Architecture-specific recommendation
/// </summary>
public class ArchitectureRecommendation
{
    public string Component { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Rationale { get; set; } = string.Empty;
    public RecommendationPriority Priority { get; set; }
    public List<string> BestPractices { get; set; } = new();
}