namespace Platform.Engineering.Copilot.Core.Models.Compliance;

/// <summary>
/// Document output format for compliance documents
/// </summary>
public enum ComplianceDocumentFormat
{
    Markdown,
    DOCX,
    PDF,
    HTML
}

/// <summary>
/// Generated compliance document
/// </summary>
public class GeneratedDocument
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty; // SSP, SAR, POAM
    public string Title { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0";
    public DateTime GeneratedDate { get; set; } = DateTime.UtcNow;
    public string Classification { get; set; } = "UNCLASSIFIED";
    public List<DocumentSection> Sections { get; set; } = new();
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string Content { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/markdown";
}

/// <summary>
/// Document section
/// </summary>
public class DocumentSection
{
    public string SectionNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public List<DocumentSection> Subsections { get; set; } = new();
    public List<string> References { get; set; } = new();
}

/// <summary>
/// Control narrative document
/// </summary>
public class ControlNarrative
{
    public string ControlId { get; set; } = string.Empty;
    public string ControlTitle { get; set; } = string.Empty;
    public string What { get; set; } = string.Empty;
    public string How { get; set; } = string.Empty;
    public List<string> CustomerResponsibilities { get; set; } = new();
    public List<string> InheritedFromAzure { get; set; } = new();
    public List<string> EvidenceArtifacts { get; set; } = new();
    public string ImplementationStatus { get; set; } = "Implemented";
    public DateTime LastReviewed { get; set; } = DateTime.UtcNow;
    public string ComplianceStatus { get; set; } = "Compliant";
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// SSP generation parameters
/// </summary>
public class SspParameters
{
    public string SystemName { get; set; } = string.Empty;
    public string SystemOwner { get; set; } = string.Empty;
    public string AuthorizingOfficial { get; set; } = string.Empty;
    public string ImpactLevel { get; set; } = "IL4"; // IL2, IL4, IL5, IL6
    public string Environment { get; set; } = "Azure Government";
    public string Classification { get; set; } = "UNCLASSIFIED";
    public List<string> ComplianceFrameworks { get; set; } = new() { "NIST-800-53-Rev5" };
    public Dictionary<string, string> Contacts { get; set; } = new();
    public string SystemDescription { get; set; } = string.Empty;
    public string SystemPurpose { get; set; } = string.Empty;
}

/// <summary>
/// Compliance document metadata
/// </summary>
public class ComplianceDocumentMetadata
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public int PageCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string StorageUri { get; set; } = string.Empty;
}
