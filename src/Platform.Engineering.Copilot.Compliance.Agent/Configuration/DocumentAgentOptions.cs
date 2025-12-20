namespace Platform.Engineering.Copilot.Compliance.Core.Configuration;

/// <summary>
/// Configuration options for the Document Agent
/// Handles ATO document generation, template management, and eMASS export
/// </summary>
public class DocumentAgentOptions
{
    public const string SectionName = "DocumentAgent";

    /// <summary>
    /// Enable AI-powered document generation (requires Azure OpenAI)
    /// </summary>
    public bool EnableAIGeneration { get; set; } = true;

    /// <summary>
    /// Enable template-based document generation
    /// </summary>
    public bool EnableTemplates { get; set; } = true;

    /// <summary>
    /// Temperature for AI responses (0.0 - 2.0)
    /// Lower = more focused and deterministic, Higher = more creative
    /// Default: 0.3 (balanced for technical writing)
    /// </summary>
    public double Temperature { get; set; } = 0.3;

    /// <summary>
    /// Maximum tokens for chat completion requests
    /// Default: 8000 (sufficient for long compliance documents)
    /// </summary>
    public int MaxTokens { get; set; } = 8000;

    /// <summary>
    /// Document generation configuration
    /// </summary>
    public DocumentGenerationOptions DocumentGeneration { get; set; } = new();

    /// <summary>
    /// Template configuration
    /// </summary>
    public TemplatesOptions Templates { get; set; } = new();

    /// <summary>
    /// eMASS export configuration
    /// </summary>
    public EMassOptions EMass { get; set; } = new();
}

/// <summary>
/// Document generation storage and format configuration
/// </summary>
public class DocumentGenerationOptions
{
    /// <summary>
    /// Azure Storage Account name for document storage
    /// </summary>
    public string StorageAccount { get; set; } = "atodocuments";

    /// <summary>
    /// Azure Blob container for generated documents
    /// </summary>
    public string Container { get; set; } = "documents";

    /// <summary>
    /// Azure Blob container for document templates
    /// </summary>
    public string TemplateContainer { get; set; } = "templates";

    /// <summary>
    /// Azure Blob container for compliance evidence artifacts
    /// </summary>
    public string EvidenceContainer { get; set; } = "evidence";

    /// <summary>
    /// Maximum document size in MB
    /// </summary>
    public int MaxDocumentSizeMB { get; set; } = 50;

    /// <summary>
    /// Supported document export formats
    /// </summary>
    public List<string> SupportedFormats { get; set; } = new()
    {
        "DOCX",
        "PDF",
        "MD",
        "HTML",
        "JSON"
    };
}

/// <summary>
/// Document template configuration
/// Maps compliance frameworks to template file paths
/// </summary>
public class TemplatesOptions
{
    /// <summary>
    /// FedRAMP High SSP template path
    /// </summary>
    public string FedRAMPHigh { get; set; } = "templates/fedramp/ssp_high_template.docx";

    /// <summary>
    /// FedRAMP Moderate SSP template path
    /// </summary>
    public string FedRAMPModerate { get; set; } = "templates/fedramp/ssp_moderate_template.docx";

    /// <summary>
    /// DoD IL5 SSP template path
    /// </summary>
    public string DoDIL5 { get; set; } = "templates/dod/ssp_il5_template.docx";

    /// <summary>
    /// ISO 27001 ISMS template path
    /// </summary>
    public string ISO27001 { get; set; } = "templates/iso/isms_template.docx";
}

/// <summary>
/// eMASS (Enterprise Mission Assurance Support Service) export configuration
/// </summary>
public class EMassOptions
{
    /// <summary>
    /// eMASS package export format (ZIP or TAR)
    /// </summary>
    public string PackageFormat { get; set; } = "ZIP";

    /// <summary>
    /// Maximum eMASS package size in GB
    /// </summary>
    public double MaxPackageSizeGB { get; set; } = 5.0;

    /// <summary>
    /// Include source/working files in eMASS export
    /// </summary>
    public bool IncludeSourceFiles { get; set; } = false;

    /// <summary>
    /// Export only PDF format (no source documents)
    /// </summary>
    public bool PDFOnly { get; set; } = true;
}
