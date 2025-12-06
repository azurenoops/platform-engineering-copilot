using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Audits;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Services.Audits;

namespace Platform.Engineering.Copilot.Compliance.Core.Extensions;

/// <summary>
/// Service collection extensions for Governance and Compliance services
/// </summary>
public static class GovernanceServiceCollectionExtensions
{
    /// <summary>
    /// Add enhanced ATO compliance services with NIST integration
    /// </summary>
    public static IServiceCollection AddEnhancedAtoCompliance(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<NistControlsOptions>(
            configuration.GetSection(NistControlsOptions.SectionName));

        // Validation of configuration
        services.AddOptionsWithValidateOnStart<NistControlsOptions>()
            .Bind(configuration.GetSection(NistControlsOptions.SectionName))
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
        
        // ATO Compliance Engine and supporting services
        services.AddSingleton<IAtoComplianceEngine, AtoComplianceEngine>();
        services.AddSingleton<IRemediationEngine, AtoRemediationEngine>();
        services.AddSingleton<IAuditLoggingService, AuditLoggingService>();
        
        // NOTE: IComplianceService is obsolete - use IAtoComplianceEngine instead
        // ComplianceServiceAdapter removed as it's no longer needed
        
        // Legacy services for backward compatibility (health checks, etc.)
        services.AddSingleton<ComplianceValidationService>();
        
        // Azure Resource Health monitoring service - DISABLED due to missing AzureOptions
        // services.AddSingleton<IAzureResourceHealthService, AzureResourceHealthService>();

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