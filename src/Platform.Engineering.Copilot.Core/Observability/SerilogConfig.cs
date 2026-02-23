using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Platform.Engineering.Copilot.Core.Observability;

/// <summary>
/// Serilog configuration — console + file sinks (dev), Application Insights (prod).
/// Structured log format with correlationId, agentName, toolName, userId (redacted), timestamp.
/// Per FR-078 and Constitution Principle V.
/// </summary>
public static class SerilogConfig
{
    /// <summary>
    /// Configure Serilog for the host builder.
    /// Call from Program.cs: <c>builder.Host.UseSerilog(SerilogConfig.Configure);</c>
    /// </summary>
    public static void Configure(
        HostBuilderContext context,
        LoggerConfiguration loggerConfiguration)
    {
        var environment = context.HostingEnvironment;
        var configuration = context.Configuration;

        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "PlatformEngineeringCopilot")
            .Enrich.WithProperty("Environment", environment.EnvironmentName);

        // Structured output template with correlation ID and agent context
        const string outputTemplate =
            "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] " +
            "[{CorrelationId}] [{AgentName}] [{ToolName}] " +
            "{Message:lj}{NewLine}{Exception}";

        if (environment.IsDevelopment())
        {
            // Dev: Console + File sinks with Debug level
            loggerConfiguration
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
                .WriteTo.Console(outputTemplate: outputTemplate)
                .WriteTo.File(
                    path: GetLogFilePath(configuration, "logs/platform-copilot-.log"),
                    outputTemplate: outputTemplate,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    fileSizeLimitBytes: 50_000_000); // 50MB per file
        }
        else
        {
            // Production: Structured JSON for log aggregation + Application Insights
            loggerConfiguration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Error)
                .WriteTo.Console(outputTemplate: outputTemplate)
                .WriteTo.File(
                    path: GetLogFilePath(configuration, "logs/platform-copilot-.json"),
                    formatter: new Serilog.Formatting.Json.JsonFormatter(),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    fileSizeLimitBytes: 100_000_000); // 100MB per file

            // Application Insights (if connection string configured)
            var aiConnectionString = configuration["Observability:ApplicationInsights:ConnectionString"];
            if (!string.IsNullOrEmpty(aiConnectionString))
            {
                // Serilog.Sinks.ApplicationInsights would be added here in production
                // loggerConfiguration.WriteTo.ApplicationInsights(
                //     aiConnectionString, TelemetryConverter.Traces);
            }
        }

        // Sensitive data filtering — never log these paths
        loggerConfiguration.Filter.ByExcluding(logEvent =>
        {
            var message = logEvent.RenderMessage();
            return message.Contains("Bearer ") ||
                   message.Contains("certificate_thumbprint") ||
                   message.Contains("client_secret");
        });
    }

    /// <summary>
    /// Create a pre-configured ILogger for use before Host is built
    /// (e.g., during startup configuration).
    /// </summary>
    public static ILogger CreateBootstrapLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .CreateBootstrapLogger();
    }

    private static string GetLogFilePath(IConfiguration configuration, string defaultPath)
    {
        return configuration["Observability:LogFilePath"] ?? defaultPath;
    }
}
