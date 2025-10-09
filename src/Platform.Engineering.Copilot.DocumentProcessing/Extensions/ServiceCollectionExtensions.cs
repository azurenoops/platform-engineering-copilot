using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.DocumentProcessing.Services;
using Platform.Engineering.Copilot.DocumentProcessing.Analyzers;

namespace Platform.Engineering.Copilot.DocumentProcessing.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentProcessing(this IServiceCollection services, IConfiguration configuration)
    {
        // Register core services
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        
        // Register analyzers  
        services.AddScoped<IArchitectureDiagramAnalyzer, ArchitectureDiagramAnalyzer>();
        services.AddScoped<INavyFlankspeedAnalyzer, NavyFlankspeedAnalyzer>();

        // Configure document processing options
        services.Configure<DocumentProcessingOptions>(configuration.GetSection("DocumentProcessing"));

        // Note: RMF compliance analysis is now handled by IAtoComplianceEngine and IAtoRemediationEngine from Platform.Engineering.Copilot.Core
        // These services are injected into DocumentProcessingService and provide:
        // - Comprehensive compliance assessment for document findings
        // - Automated remediation plan generation
        // - Risk assessment and compliance scoring
        // Ensure that IAtoComplianceEngine and IAtoRemediationEngine are registered in the DI container

        return services;
    }
}

public class DocumentProcessingOptions
{
    public string UploadsPath { get; set; } = "uploads";
    public long MaxFileSizeBytes { get; set; } = 50 * 1024 * 1024; // 50MB
    public int ProcessingTimeoutMinutes { get; set; } = 30;
    public bool EnableAdvancedAnalysis { get; set; } = true;
    public string[] SupportedFileTypes { get; set; } = {
        ".pdf", ".docx", ".doc", ".vsdx", ".vsd", ".pptx", ".ppt", 
        ".xlsx", ".xls", ".txt", ".md", ".png", ".jpg", ".jpeg"
    };
}