using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using Platform.Engineering.Copilot.Agents.Compliance;
using Platform.Engineering.Copilot.Agents.Compliance.Tools;
using Platform.Engineering.Copilot.Agents.Configuration;
using Platform.Engineering.Copilot.Agents.Configuration.Tools;
using Platform.Engineering.Copilot.Agents.CostManagement;
using Platform.Engineering.Copilot.Agents.CostManagement.Tools;
using Platform.Engineering.Copilot.Agents.Infrastructure;
using Platform.Engineering.Copilot.Agents.Infrastructure.Tools;
using Platform.Engineering.Copilot.Agents.KnowledgeBase;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;
using Platform.Engineering.Copilot.Agents.Discovery;
using Platform.Engineering.Copilot.Agents.Discovery.Tools;
using Platform.Engineering.Copilot.Agents.Environment;
using Platform.Engineering.Copilot.Agents.Environment.Tools;
using Platform.Engineering.Copilot.Agents.Security;
using Platform.Engineering.Copilot.Agents.Security.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Services;
using Platform.Engineering.Copilot.Core.Observability;
using Platform.Engineering.Copilot.Core.Services;
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

    ConfigureSharedServices(builder.Services, builder.Configuration);

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

    ConfigureSharedServices(builder.Services, builder.Configuration);

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

/// <summary>
/// Register shared services used by both transport modes.
/// Ensures identical tool capabilities across HTTP and stdio (FR-007).
/// </summary>
static void ConfigureSharedServices(IServiceCollection services, IConfiguration configuration)
{
    // Core services
    services.AddSingleton<INistService, NistService>();
    services.AddSingleton<HealthCheckService>();
    services.AddSingleton<MetricsService>();
    services.AddSingleton<AuditLogService>();
    services.AddSingleton<AzureErrorHandler>();
    services.AddSingleton<IAgentStateManager, InMemoryAgentStateManager>();
    services.AddSingleton<RemediationBoardService>();
    services.AddSingleton<ISecretProvider, KeyVaultSecretProvider>();
    services.AddScoped<CorrelationIdAccessor>();

    // Compliance Agent tools (T060 — agent registration)
    services.AddSingleton<ComplianceAssessTool>();
    services.AddSingleton<ComplianceGetControlFamilyTool>();
    services.AddSingleton<ComplianceStatusTool>();
    services.AddSingleton<ComplianceHistoryTool>();
    services.AddSingleton<ComplianceRemediateTool>();
    services.AddSingleton<ComplianceValidateRemediationTool>();
    services.AddSingleton<ComplianceGeneratePlanTool>();
    services.AddSingleton<ComplianceCollectEvidenceTool>();
    services.AddSingleton<ComplianceGenerateDocumentTool>();
    services.AddSingleton<ComplianceAuditLogTool>();
    services.AddSingleton<ComplianceChatTool>();
    services.AddSingleton<ComplianceMapControlsTool>();
    services.AddSingleton<ComplianceCompareFrameworksTool>();
    services.AddSingleton<ComplianceDashboardTool>();
    services.AddSingleton<ComplianceExportTool>();
    services.AddSingleton<ComplianceMonitoringTool>();

    // Compliance Agent
    services.AddSingleton<ComplianceAgent>(sp =>
    {
        var tools = new BaseTool[]
        {
            sp.GetRequiredService<ComplianceAssessTool>(),
            sp.GetRequiredService<ComplianceGetControlFamilyTool>(),
            sp.GetRequiredService<ComplianceStatusTool>(),
            sp.GetRequiredService<ComplianceHistoryTool>(),
            sp.GetRequiredService<ComplianceRemediateTool>(),
            sp.GetRequiredService<ComplianceValidateRemediationTool>(),
            sp.GetRequiredService<ComplianceGeneratePlanTool>(),
            sp.GetRequiredService<ComplianceCollectEvidenceTool>(),
            sp.GetRequiredService<ComplianceGenerateDocumentTool>(),
            sp.GetRequiredService<ComplianceAuditLogTool>(),
            sp.GetRequiredService<ComplianceChatTool>(),
            sp.GetRequiredService<ComplianceMapControlsTool>(),
            sp.GetRequiredService<ComplianceCompareFrameworksTool>(),
            sp.GetRequiredService<ComplianceDashboardTool>(),
            sp.GetRequiredService<ComplianceExportTool>(),
            sp.GetRequiredService<ComplianceMonitoringTool>()
        };
        return new ComplianceAgent(
            sp.GetRequiredService<ILogger<ComplianceAgent>>(),
            tools);
    });

    // Orchestrator and agent registry
    services.AddSingleton<PlatformOrchestrator>(sp =>
    {
        var orchestrator = new PlatformOrchestrator(
            sp.GetRequiredService<ILogger<PlatformOrchestrator>>());

        // Register all agents with the orchestrator
        orchestrator.RegisterAgent(sp.GetRequiredService<ComplianceAgent>());
        orchestrator.RegisterAgent(sp.GetRequiredService<ConfigurationAgent>());
        orchestrator.RegisterAgent(sp.GetRequiredService<KnowledgeBaseAgent>());
        orchestrator.RegisterAgent(sp.GetRequiredService<InfrastructureAgent>());
        orchestrator.RegisterAgent(sp.GetRequiredService<CostManagementAgent>());
        orchestrator.RegisterAgent(sp.GetRequiredService<DiscoveryAgent>());
        orchestrator.RegisterAgent(sp.GetRequiredService<EnvironmentAgent>());
        orchestrator.RegisterAgent(sp.GetRequiredService<SecurityAgent>());

        return orchestrator;
    });

    // Configuration Agent tool + agent
    services.AddSingleton<ConfigurationManageTool>();
    services.AddSingleton<ConfigurationAgent>(sp =>
    {
        return new ConfigurationAgent(
            sp.GetRequiredService<ILogger<ConfigurationAgent>>(),
            sp.GetRequiredService<ConfigurationManageTool>());
    });

    // Knowledge Base Agent tools + agent (T104 — no auth required, SC-008)
    services.AddSingleton<ExplainControlTool>();
    services.AddSingleton<CompareFrameworksTool>();
    services.AddSingleton<SearchControlsTool>();
    services.AddSingleton<GetStigGuidanceTool>();
    services.AddSingleton<GetAtoChecklistTool>();
    services.AddSingleton<FrameworkSummaryTool>();
    services.AddSingleton<ControlMappingTool>();
    services.AddSingleton<ImplementationExamplesTool>();
    services.AddSingleton<KnowledgeBaseAgent>(sp =>
    {
        return new KnowledgeBaseAgent(
            sp.GetRequiredService<ILogger<KnowledgeBaseAgent>>(),
            sp.GetRequiredService<ExplainControlTool>(),
            sp.GetRequiredService<CompareFrameworksTool>(),
            sp.GetRequiredService<SearchControlsTool>(),
            sp.GetRequiredService<GetStigGuidanceTool>(),
            sp.GetRequiredService<GetAtoChecklistTool>(),
            sp.GetRequiredService<FrameworkSummaryTool>(),
            sp.GetRequiredService<ControlMappingTool>(),
            sp.GetRequiredService<ImplementationExamplesTool>());
    });

    // Infrastructure Agent tools + agent (T114 — template gen no auth, deploy requires PIM Write)
    services.AddSingleton<GenerateInfrastructureTemplateTool>();
    services.AddSingleton<ProvisionInfrastructureTool>();
    services.AddSingleton<ValidateTemplateTool>();
    services.AddSingleton<ListDeploymentsTool>();
    services.AddSingleton<GetDeploymentStatusTool>();
    services.AddSingleton<RollbackDeploymentTool>();
    services.AddSingleton<InfrastructureAgent>(sp =>
    {
        return new InfrastructureAgent(
            sp.GetRequiredService<ILogger<InfrastructureAgent>>(),
            sp.GetRequiredService<GenerateInfrastructureTemplateTool>(),
            sp.GetRequiredService<ProvisionInfrastructureTool>(),
            sp.GetRequiredService<ValidateTemplateTool>(),
            sp.GetRequiredService<ListDeploymentsTool>(),
            sp.GetRequiredService<GetDeploymentStatusTool>(),
            sp.GetRequiredService<RollbackDeploymentTool>());
    });

    // Cost Management Agent tools + agent (T123 — cached reports no auth, live queries require PIM Read)
    services.AddSingleton<GetCostAnalysisTool>();
    services.AddSingleton<GetCostForecastTool>();
    services.AddSingleton<GetOptimizationSuggestionsTool>();
    services.AddSingleton<GetCachedCostReportTool>();
    services.AddSingleton<GetBudgetStatusTool>();
    services.AddSingleton<GetCostAnomaliesTool>();
    services.AddSingleton<CostManagementAgent>(sp =>
    {
        return new CostManagementAgent(
            sp.GetRequiredService<ILogger<CostManagementAgent>>(),
            sp.GetRequiredService<GetCostAnalysisTool>(),
            sp.GetRequiredService<GetCostForecastTool>(),
            sp.GetRequiredService<GetOptimizationSuggestionsTool>(),
            sp.GetRequiredService<GetCachedCostReportTool>(),
            sp.GetRequiredService<GetBudgetStatusTool>(),
            sp.GetRequiredService<GetCostAnomaliesTool>());
    });

    // Configuration
    services.AddOptions();

    // Discovery Agent tools + agent (T134 — resource discovery, PIM Read)
    services.AddSingleton<DiscoverResourcesTool>();
    services.AddSingleton<GetResourceDependenciesTool>();
    services.AddSingleton<CrossSubscriptionQueryTool>();
    services.AddSingleton<GetResourceHealthTool>();
    services.AddSingleton<GetNetworkTopologyTool>();
    services.AddSingleton<AnalyzeTagsTool>();
    services.AddSingleton<GetResourceChangesTool>();
    services.AddSingleton<GetOrphanedResourcesTool>();
    services.AddSingleton<GetResourceMetricsTool>();
    services.AddSingleton<DiscoveryAgent>(sp =>
    {
        return new DiscoveryAgent(
            sp.GetRequiredService<ILogger<DiscoveryAgent>>(),
            sp.GetRequiredService<DiscoverResourcesTool>(),
            sp.GetRequiredService<GetResourceDependenciesTool>(),
            sp.GetRequiredService<CrossSubscriptionQueryTool>(),
            sp.GetRequiredService<GetResourceHealthTool>(),
            sp.GetRequiredService<GetNetworkTopologyTool>(),
            sp.GetRequiredService<AnalyzeTagsTool>(),
            sp.GetRequiredService<GetResourceChangesTool>(),
            sp.GetRequiredService<GetOrphanedResourcesTool>(),
            sp.GetRequiredService<GetResourceMetricsTool>());
    });

    // Environment Agent tools + agent (T135 — lifecycle management, PIM Read/Write)
    services.AddSingleton<CloneEnvironmentTool>();
    services.AddSingleton<DetectDriftTool>();
    services.AddSingleton<CompareEnvironmentsTool>();
    services.AddSingleton<PromoteEnvironmentTool>();
    services.AddSingleton<ListEnvironmentsTool>();
    services.AddSingleton<GetEnvironmentStatusTool>();
    services.AddSingleton<CreateEnvironmentTool>();
    services.AddSingleton<DeleteEnvironmentTool>();
    services.AddSingleton<GetEnvironmentHistoryTool>();
    services.AddSingleton<ValidateEnvironmentTool>();
    services.AddSingleton<EnvironmentAgent>(sp =>
    {
        return new EnvironmentAgent(
            sp.GetRequiredService<ILogger<EnvironmentAgent>>(),
            sp.GetRequiredService<CloneEnvironmentTool>(),
            sp.GetRequiredService<DetectDriftTool>(),
            sp.GetRequiredService<CompareEnvironmentsTool>(),
            sp.GetRequiredService<PromoteEnvironmentTool>(),
            sp.GetRequiredService<ListEnvironmentsTool>(),
            sp.GetRequiredService<GetEnvironmentStatusTool>(),
            sp.GetRequiredService<CreateEnvironmentTool>(),
            sp.GetRequiredService<DeleteEnvironmentTool>(),
            sp.GetRequiredService<GetEnvironmentHistoryTool>(),
            sp.GetRequiredService<ValidateEnvironmentTool>());
    });

    // Security Agent tools + agent (T136 — security posture, PIM Read)
    services.AddSingleton<GetSecureScoreTool>();
    services.AddSingleton<GetSecurityRecommendationsTool>();
    services.AddSingleton<ManageSecurityPolicyTool>();
    services.AddSingleton<SecurityAgent>(sp =>
    {
        return new SecurityAgent(
            sp.GetRequiredService<ILogger<SecurityAgent>>(),
            sp.GetRequiredService<GetSecureScoreTool>(),
            sp.GetRequiredService<GetSecurityRecommendationsTool>(),
            sp.GetRequiredService<ManageSecurityPolicyTool>());
    });
}
