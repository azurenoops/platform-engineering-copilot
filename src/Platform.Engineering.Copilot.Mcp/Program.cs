using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Core.Observability;
using Serilog;

// ─── MCP Server with dual transport: HTTP (default, port 5100) + stdio (--stdio flag)
// Per FR-007 and research.md §2: identical tool capabilities across both transports.
// HTTP: ASP.NET Core Kestrel with SSE-based MCP transport for web clients.
// stdio: JSON-RPC over stdin/stdout for AI clients (GitHub Copilot, Claude Desktop).

var isStdio = args.Contains("--stdio", StringComparer.OrdinalIgnoreCase);

if (isStdio)
{
    // ─── stdio transport mode ───
    // Uses HostApplicationBuilder (non-web) with stdin/stdout JSON-RPC.
    var builder = Host.CreateApplicationBuilder(args);

    builder.Logging.ClearProviders();
    builder.Logging.AddSerilog(new LoggerConfiguration()
        .MinimumLevel.Warning()
        .WriteTo.File("logs/mcp-stdio-.log", rollingInterval: RollingInterval.Day)
        .CreateLogger());

    builder.Services.AddPlatformCopilotServices(builder.Configuration);

    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "Platform Engineering Copilot MCP Server",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

    var host = builder.Build();
    await host.RunAsync();
}
else
{
    // ─── HTTP transport mode (default) ───
    // ASP.NET Core Kestrel on port 5100 with SSE-based MCP transport.
    var builder = WebApplication.CreateBuilder(args);

    builder.WebHost.UseUrls("http://0.0.0.0:5100");
    builder.Host.UseSerilog(SerilogConfig.Configure);

    builder.Services.AddPlatformCopilotServices(builder.Configuration);

    builder.Services.AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "Platform Engineering Copilot MCP Server",
            Version = "1.0.0"
        };
    })
    .WithHttpTransport()
    .WithToolsFromAssembly();

    builder.Services.AddHealthChecks()
        .AddCheck<PlatformHealthCheck>("platform-health");

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    // Correlation ID middleware for distributed tracing
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Map MCP endpoints (SSE-based JSON-RPC)
    app.MapMcp();

    // Health check endpoint
    app.MapHealthChecks("/health");

    // Info endpoint
    app.MapGet("/", () => new
    {
        service = "Platform Engineering Copilot MCP Server",
        version = "1.0.0",
        transport = "HTTP (SSE)",
        port = 5100,
        tools = "Use MCP protocol to list tools"
    });

    app.Run();
}
