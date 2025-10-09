using Microsoft.AspNetCore.Http;

namespace Platform.Engineering.Copilot.DocumentProcessing.Models;

public enum DocumentAnalysisType
{
    General,
    ArchitectureDiagram,
    SecurityDocument,
    ComplianceDocument,
    NetworkDiagram,
    SystemDesign
}

public enum ProcessingStatus
{
    Uploading,
    Processing,
    Analyzing,
    Complete,
    Error
}

public enum RmfFramework
{
    NIST80053,
    NIST80063,
    FedRAMP,
    FISMA
}

public enum ComplianceLevel
{
    Low,
    Moderate,
    High
}

public enum PlatformType
{
    General,
    NavyFlankspeed,
    AzureGovernment,
    AWS_GovCloud,
    Microsoft365_GCC
}

public enum ImageType
{
    Diagram,
    Screenshot,
    Chart,
    Photo,
    Logo,
    Other
}

public enum DiagramType
{
    NetworkDiagram,
    SystemArchitecture,
    DataFlow,
    ProcessFlow,
    OrganizationalChart,
    Other
}

public enum SecurityClassification
{
    Unclassified,
    Confidential,
    Secret,
    TopSecret
}

public enum ControlImplementationStatus
{
    Implemented,
    PartiallyImplemented,
    NotImplemented,
    NotApplicable
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    Critical
}

public enum RecommendationType
{
    Security,
    Compliance,
    Architecture,
    Performance,
    Cost
}

public enum Priority
{
    Low,
    Medium,
    High,
    Critical
}

// Request/Response Models
public class DocumentUploadRequest
{
    public IFormFile File { get; set; } = null!;
    public string? ConversationId { get; set; }
    public DocumentAnalysisType AnalysisType { get; set; } = DocumentAnalysisType.General;
}

public class DocumentUploadResponse
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; }
    public string AnalysisPreview { get; set; } = string.Empty;
    public TimeSpan EstimatedProcessingTime { get; set; }
}

public class RmfAnalysisRequest
{
    public RmfFramework Framework { get; set; } = RmfFramework.NIST80053;
    public ComplianceLevel ComplianceLevel { get; set; } = ComplianceLevel.Moderate;
    public PlatformType PlatformType { get; set; } = PlatformType.General;
}

// Processing Results
public class DocumentProcessingResult
{
    public string DocumentId { get; set; } = string.Empty;
    public ProcessingStatus ProcessingStatus { get; set; }
    public string AnalysisPreview { get; set; } = string.Empty;
    public TimeSpan EstimatedProcessingTime { get; set; }
    public List<string> ExtractedText { get; set; } = new();
    public List<string> ExtractedImages { get; set; } = new();
    public DocumentMetadata Metadata { get; set; } = new();
}

public class DocumentProcessingStatus
{
    public string DocumentId { get; set; } = string.Empty;
    public ProcessingStatus Status { get; set; }
    public int ProgressPercentage { get; set; }
    public string CurrentStep { get; set; } = string.Empty;
    public List<string> CompletedSteps { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public DateTime LastUpdated { get; set; }
}

// Document Analysis
public class DocumentAnalysis
{
    public string DocumentId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DocumentAnalysisType AnalysisType { get; set; }
    public DocumentMetadata Metadata { get; set; } = new();
    public ExtractedContent Content { get; set; } = new();
    public ArchitectureAnalysis? ArchitectureAnalysis { get; set; }
    public SecurityAnalysis? SecurityAnalysis { get; set; }
    public List<string> Recommendations { get; set; } = new();
    public DateTime AnalyzedAt { get; set; }
}

public class DocumentMetadata
{
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public List<string> Keywords { get; set; } = new();
    public int PageCount { get; set; }
    public Dictionary<string, object> CustomProperties { get; set; } = new();
}

public class ExtractedContent
{
    public string FullText { get; set; } = string.Empty;
    public List<TextSection> Sections { get; set; } = new();
    public List<ExtractedImage> Images { get; set; } = new();
    public List<ExtractedTable> Tables { get; set; } = new();
    public List<ExtractedDiagram> Diagrams { get; set; } = new();
}

public class TextSection
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public string SectionType { get; set; } = string.Empty;
}

public class ExtractedImage
{
    public string ImageId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PageNumber { get; set; }
    public ImageType Type { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public class ExtractedTable
{
    public string TableId { get; set; } = string.Empty;
    public List<List<string>> Rows { get; set; } = new();
    public List<string> Headers { get; set; } = new();
    public int PageNumber { get; set; }
    public string Caption { get; set; } = string.Empty;
}

public class ExtractedDiagram
{
    public string DiagramId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DiagramType Type { get; set; }
    public List<DiagramComponent> Components { get; set; } = new();
    public List<DiagramConnection> Connections { get; set; } = new();
    public int PageNumber { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

public class DiagramComponent
{
    public string ComponentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Properties { get; set; } = new();
    public SecurityClassification? SecurityLevel { get; set; }
}

public class DiagramConnection
{
    public string ConnectionId { get; set; } = string.Empty;
    public string SourceComponentId { get; set; } = string.Empty;
    public string TargetComponentId { get; set; } = string.Empty;
    public string ConnectionType { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public bool IsSecure { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}