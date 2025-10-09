using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.Mcp.Server;
using Platform.Engineering.Copilot.Mcp.Services;
using Platform.Engineering.Copilot.Mcp.Tools;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;

namespace Platform.Engineering.Copilot.Mcp;

/// <summary>
/// MCP server host service
/// </summary>
public class McpHostService : BackgroundService
{
    private readonly McpServer _mcpServer;
    private readonly ILogger<McpHostService> _logger;

    public McpHostService(McpServer mcpServer, ILogger<McpHostService> logger)
    {
        _mcpServer = mcpServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _mcpServer.StartAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MCP server failed");
            throw;
        }
    }
}

/// <summary>
/// Program entry point
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        // Configure Serilog for MCP server (write to stderr to avoid interfering with stdout)
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(standardErrorFromLevel: Serilog.Events.LogEventLevel.Verbose)
            .CreateLogger();

        try
        {
            var builder = Host.CreateApplicationBuilder(args);

            // Configure services
            builder.Services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSerilog();
            });

            // Configure HTTP client for Platform.API communication
            builder.Services.AddHttpClient<PlatformApiClient>(client =>
            {
                var apiBaseUrl = builder.Configuration["PlatformApi:BaseUrl"] ?? "http://localhost:7001";
                client.BaseAddress = new Uri(apiBaseUrl);
                client.DefaultRequestHeaders.Add("User-Agent", "Platform-MCP-Server/1.0");
                client.Timeout = TimeSpan.FromMinutes(5); // Allow longer running operations
            });

            // Register MCP services
            builder.Services.AddSingleton<PlatformTools>();
            builder.Services.AddSingleton<McpServer>();
            builder.Services.AddHostedService<McpHostService>();

            var host = builder.Build();

            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "MCP server terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}