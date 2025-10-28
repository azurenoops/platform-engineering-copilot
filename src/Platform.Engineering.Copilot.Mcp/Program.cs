using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Mcp.Server;
using Platform.Engineering.Copilot.Mcp.Tools;
using Platform.Engineering.Copilot.Mcp.Middleware;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace Platform.Engineering.Copilot.Mcp;

/// <summary>
/// MCP server host service (stdio mode for GitHub Copilot/Claude)
/// </summary>
public class McpStdioService : BackgroundService
{
    private readonly McpServer _mcpServer;
    private readonly ILogger<McpStdioService> _logger;

    public McpStdioService(McpServer mcpServer, ILogger<McpStdioService> logger)
    {
        _mcpServer = mcpServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("📡 MCP stdio server starting...");
            await _mcpServer.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP stdio server failed");
            throw;
        }
    }
}

/// <summary>
/// Program entry point - Dual-mode MCP server
/// Supports BOTH stdio (for GitHub Copilot/Claude) and HTTP (for Chat web app)
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        // Check if running in HTTP mode (--http flag)
        var httpMode = args.Contains("--http");
        var httpPort = GetHttpPort(args);

        // Configure Serilog for MCP server (write to stderr to avoid interfering with stdout)
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
            .CreateLogger();

        try
        {
            if (httpMode)
            {
                await RunHttpModeAsync(httpPort);
            }
            else
            {
                await RunStdioModeAsync();
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "MCP server terminated unexpectedly");
        }
        finally
        {
            await Log.CloseAndFlushAsync();
        }
    }

    /// <summary>
    /// Run in stdio mode for external AI tools (GitHub Copilot, Claude Desktop, Cline)
    /// </summary>
    static async Task RunStdioModeAsync()
    {
        Log.Information("🚀 Starting MCP server in STDIO mode (for GitHub Copilot/Claude)");

        var builder = Host.CreateApplicationBuilder();

        // Configure services
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog();
        });

        // Add Core services (Multi-Agent Orchestrator, Plugins, etc.)
        builder.Services.AddPlatformEngineeringCopilotCore();

        // Register MCP Chat Tool (exposes orchestrator through MCP)
        builder.Services.AddSingleton<PlatformEngineeringCopilotTools>();

        // Register MCP server for stdio
        builder.Services.AddSingleton<McpServer>();
        builder.Services.AddHostedService<McpStdioService>();

        var host = builder.Build();
        await host.RunAsync();
    }

    /// <summary>
    /// Run in HTTP mode for web apps (Chat client)
    /// </summary>
    static async Task RunHttpModeAsync(int port)
    {
        Log.Information("🌐 Starting MCP server in HTTP mode on port {Port} (for Chat web app)", port);

        var builder = WebApplication.CreateBuilder();

        // Configure logging
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddSerilog();
        });

        // Add Core services (Multi-Agent Orchestrator, Plugins, etc.)
        builder.Services.AddPlatformEngineeringCopilotCore();

        // Register MCP Chat Tool
        builder.Services.AddSingleton<PlatformEngineeringCopilotTools>();

        // Register HTTP bridge
        builder.Services.AddSingleton<McpHttpBridge>();

        var app = builder.Build();

        // Configure URLs
        app.Urls.Add($"http://0.0.0.0:{port}");

        // Add audit logging middleware for HTTP requests
        app.UseMiddleware<AuditLoggingMiddleware>();

        // Map HTTP endpoints
        var httpBridge = app.Services.GetRequiredService<McpHttpBridge>();
        httpBridge.MapHttpEndpoints(app);

        Log.Information("✅ MCP HTTP server ready on http://localhost:{Port}", port);
        await app.RunAsync();
    }

    /// <summary>
    /// Get HTTP port from args (default: 5100)
    /// </summary>
    static int GetHttpPort(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--port" && int.TryParse(args[i + 1], out int port))
            {
                return port;
            }
        }
        return 5100; // Default port
    }
}