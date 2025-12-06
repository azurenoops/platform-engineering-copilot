using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Audits;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Services.Audits;
using Platform.Engineering.Copilot.Core.Interfaces.Services.DiagramGeneration;
using Platform.Engineering.Copilot.Core.Services.DocumentProcessing;
using Platform.Engineering.Copilot.Core.Services.DiagramGeneration;
using Platform.Engineering.Copilot.Core.Services.Analyzers;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins.Compliance;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins.Code;

namespace Platform.Engineering.Copilot.Compliance.Agent.Extensions;

/// <summary>
/// Service collection extensions for Governance and Compliance services
/// </summary>
public static class ComplianceAgentCollectionExtensions
{
    /// <summary>
    /// Add enhanced ATO compliance services with NIST integration
    /// </summary>
    public static IServiceCollection AddEnhancedAtoCompliance(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<Platform.Engineering.Copilot.Core.Configuration.NistControlsOptions>(
            configuration.GetSection(Platform.Engineering.Copilot.Core.Configuration.NistControlsOptions.SectionName));

        // Validation of configuration
        services.AddOptionsWithValidateOnStart<Platform.Engineering.Copilot.Core.Configuration.NistControlsOptions>()
            .Bind(configuration.GetSection(Platform.Engineering.Copilot.Core.Configuration.NistControlsOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.BaseUrl), 
                "NIST controls base URL must be configured");

        // HTTP client for NIST controls with retry policy
        services.AddHttpClient<INistControlsService, NistControlsService>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", 
                "PlatformMCP-Supervisor/1.0 (+https://github.com/jrspinella/platform-mcp-supervisor)");
        })
        .AddPolicyHandler(GetRetryPolicy())
        .AddPolicyHandler(GetCircuitBreakerPolicy());

        // Core services - using Singleton to match Platform expectations
        services.AddSingleton<INistControlsService, NistControlsService>();
        services.AddSingleton<ComplianceMetricsService>();
        
        // Knowledge Base Services (used by both KnowledgeBase.Agent plugin and AtoComplianceEngine)
        services.AddSingleton<Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase.IRmfKnowledgeService, Platform.Engineering.Copilot.KnowledgeBase.Agent.Services.KnowledgeBase.RmfKnowledgeService>();
        services.AddSingleton<Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase.IStigKnowledgeService, Platform.Engineering.Copilot.KnowledgeBase.Agent.Services.KnowledgeBase.StigKnowledgeService>();
        services.AddSingleton<Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase.IDoDInstructionService, Platform.Engineering.Copilot.KnowledgeBase.Agent.Services.KnowledgeBase.DoDInstructionService>();
        services.AddSingleton<Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase.IDoDWorkflowService, Platform.Engineering.Copilot.KnowledgeBase.Agent.Services.KnowledgeBase.DoDWorkflowService>();
        
        // Knowledge Base Plugin is now in KnowledgeBase.Agent - use AddKnowledgeBaseAgent() to register
        
        // NOTE: ATO Compliance/Remediation Engines are now registered as Scoped in AddComplianceAgent()
        // DO NOT register them here as Singleton to avoid lifetime conflicts
        
        services.AddSingleton<IAuditLoggingService, AuditLoggingService>();

        // NOTE: IComplianceService is obsolete - use IAtoComplianceEngine instead
        // ComplianceServiceAdapter removed as it's no longer needed

        // Legacy services for backward compatibility (health checks, etc.)
        services.AddSingleton<ComplianceValidationService>();
        
        // Defender for Cloud integration (optional - disabled by default)
        var dfcEnabled = configuration.GetValue<bool>("ComplianceAgent:DefenderForCloud:Enabled", false);
        if (dfcEnabled)
        {
            services.Configure<DefenderForCloudOptions>(
                configuration.GetSection("ComplianceAgent:DefenderForCloud"));
            
            services.AddSingleton<IDefenderForCloudService, DefenderForCloudService>();
        }
        else
        {
            // Register null implementation for graceful degradation
            services.AddSingleton<IDefenderForCloudService, NullDefenderForCloudService>();
        }
        
        // Azure Resource Health monitoring service - DISABLED due to missing AzureOptions
        // services.AddSingleton<IAzureResourceHealthService, AzureResourceHealthService>();
        
        // Register Architecture Diagram Analyzer
        services.AddScoped<IArchitectureDiagramAnalyzer, ArchitectureDiagramAnalyzer>();
        
        // Register Document Processing Service
        services.AddScoped<IDocumentProcessingService, DocumentProcessingService>();
        
        // Register Diagram Generation Services (Week 1)
        services.AddScoped<IMermaidDiagramService, MermaidDiagramService>();
        services.AddSingleton<IDiagramRenderService, DiagramRenderService>(); // Singleton for browser reuse
        
        // Register Plugins
        services.AddScoped<DocumentPlugin>();
        services.AddScoped<DiagramGenerationPlugin>();

        // Memory caching for NIST controls
        services.AddMemoryCache();

        // Health checks
        services.AddHealthChecks()
            .AddCheck<NistControlsHealthCheck>(
                "nist-controls",
                HealthStatus.Degraded,
                tags: new[] { "nist", "compliance", "external" },
                timeout: TimeSpan.FromSeconds(30));

        // Logging enhancement
        services.AddLogging(builder =>
        {
            builder.AddFilter("Platform.Engineering.Copilot.Compliance", LogLevel.Information);
            builder.AddFilter("System.Net.Http.HttpClient.INistControlsService", LogLevel.Warning);
        });

        return services;
    }

    /// <summary>
    /// Add ATO compliance background services
    /// </summary>
    public static IServiceCollection AddAtoComplianceBackgroundServices(
        this IServiceCollection services)
    {
        // Background service for cache warming and validation
        services.AddHostedService<NistControlsCacheWarmupService>();
        
        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => !msg.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var logger = context.GetLogger();
                    logger?.LogWarning("Retry {RetryCount}/3 for NIST API call in {Delay}ms. Reason: {Reason}",
                        retryCount, timespan.TotalMilliseconds,
                        outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase ?? "Unknown");
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (exception, duration) =>
                {
                    // Could log circuit breaker activation
                },
                onReset: () =>
                {
                    // Could log circuit breaker reset
                });
    }
}

/// <summary>
/// Polly context extensions for logging
/// </summary>
internal static class ContextExtensions
{
    private const string LoggerKey = "ILogger";

    public static Context WithLogger(this Context context, ILogger logger)
    {
        context[LoggerKey] = logger;
        return context;
    }

    public static ILogger? GetLogger(this Context context)
    {
        return context.TryGetValue(LoggerKey, out var logger) ? logger as ILogger : null;
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