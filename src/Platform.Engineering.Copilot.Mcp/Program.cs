using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Mcp.Server;
using Platform.Engineering.Copilot.Mcp.Tools;
using Platform.Engineering.Copilot.Mcp.Middleware;
using Platform.Engineering.Copilot.Compliance.Core.Extensions;
using Platform.Engineering.Copilot.Infrastructure.Core.Extensions;
using Platform.Engineering.Copilot.CostManagement.Core.Extensions;
using Platform.Engineering.Copilot.Environment.Core.Extensions;
using Platform.Engineering.Copilot.Discovery.Core.Extensions;
using Serilog;
using Platform.Engineering.Copilot.Security.Agent.Extensions;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Extensions;

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

        // Register database context (SQLite)
        var dbPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../..", "platform_engineering_copilot_management.db"));
        var connectionString = $"Data Source={dbPath}";
        builder.Services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
            options.UseSqlite(connectionString));

        // Register HttpClient for services that need it (like NistControlsService)
        builder.Services.AddHttpClient();

        // Add Core services (Multi-Agent Orchestrator, Plugins, etc.)
        builder.Services.AddPlatformEngineeringCopilotCore();

        // Register MCP Chat Tool - Scoped to match IIntelligentChatService
        builder.Services.AddScoped<PlatformEngineeringCopilotTools>();

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

        // Register database context - use SQL Server connection from config or env variable
        var configuration = builder.Configuration;
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? configuration.GetConnectionString("SqlServerConnection");
        
        Log.Information("🔧 Database connection string lookup:");
        Log.Information("   - DefaultConnection: {Exists}", configuration.GetConnectionString("DefaultConnection") != null ? "Found" : "Not found");
        Log.Information("   - SqlServerConnection: {Exists}", configuration.GetConnectionString("SqlServerConnection") != null ? "Found" : "Not found");
        
        if (!string.IsNullOrEmpty(connectionString))
        {
            Log.Information("✅ Using SQL Server database");
            // Mask the password in the connection string for logging
            var maskedConnectionString = System.Text.RegularExpressions.Regex.Replace(
                connectionString, @"(Password|Pwd)=[^;]+", "$1=***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            Log.Information("   Connection: {ConnectionString}", maskedConnectionString);
            
            builder.Services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
                options.UseSqlServer(connectionString));
        }
        else
        {
            // Fallback to SQLite
            Log.Warning("⚠️ No SQL Server connection string found, falling back to SQLite");
            var dbPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../..", "platform_engineering_copilot_management.db"));
            var sqliteConnectionString = $"Data Source={dbPath}";
            Log.Information("   SQLite Path: {DbPath}", dbPath);
            builder.Services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
                options.UseSqlite(sqliteConnectionString));
        }

        // Register HttpClient for services that need it (like NistControlsService)
        builder.Services.AddHttpClient();

        // Register HttpContextAccessor for middleware access
        builder.Services.AddHttpContextAccessor();

        // Configure Azure AD authentication options
        builder.Services.Configure<AzureAdOptions>(
            builder.Configuration.GetSection(AzureAdOptions.SectionName));
        builder.Services.Configure<GatewayOptions>(
            builder.Configuration.GetSection(GatewayOptions.SectionName));

        // Add JWT Bearer authentication for CAC token validation
        var azureAdConfig = builder.Configuration.GetSection(AzureAdOptions.SectionName);
        var azureAdOptions = new AzureAdOptions();
        azureAdConfig.Bind(azureAdOptions);

        if (!string.IsNullOrEmpty(azureAdOptions.TenantId) && !string.IsNullOrEmpty(azureAdOptions.Audience))
        {
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.Authority = azureAdOptions.Authority;
                    options.Audience = azureAdOptions.Audience;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuers = azureAdOptions.ValidIssuers.Any() 
                            ? azureAdOptions.ValidIssuers 
                            : new[] { azureAdOptions.Authority },
                        ValidateAudience = true,
                        ValidAudience = azureAdOptions.Audience,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.FromMinutes(5)
                    };

                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILogger<Program>>();

                            // Verify MFA/CAC authentication if required
                            if (azureAdOptions.RequireCac || azureAdOptions.RequireMfa)
                            {
                                var amrClaim = context.Principal?.FindFirst("amr")?.Value;
                                var authMethod = amrClaim ?? "none";

                                if (azureAdOptions.RequireCac)
                                {
                                    // Check for CAC/PIV indicators in authentication method
                                    if (!authMethod.Contains("mfa", StringComparison.OrdinalIgnoreCase) &&
                                        !authMethod.Contains("rsa", StringComparison.OrdinalIgnoreCase) &&
                                        !authMethod.Contains("smartcard", StringComparison.OrdinalIgnoreCase))
                                    {
                                        logger.LogWarning(
                                            "CAC/PIV authentication required but not detected. Auth method: {AuthMethod}",
                                            authMethod);
                                        context.Fail("Multi-factor authentication with CAC/PIV is required");
                                        return Task.CompletedTask;
                                    }
                                }
                                else if (azureAdOptions.RequireMfa)
                                {
                                    if (!authMethod.Contains("mfa", StringComparison.OrdinalIgnoreCase))
                                    {
                                        logger.LogWarning(
                                            "Multi-factor authentication required but not detected. Auth method: {AuthMethod}",
                                            authMethod);
                                        context.Fail("Multi-factor authentication is required");
                                        return Task.CompletedTask;
                                    }
                                }
                            }

                            var userPrincipal = context.Principal?.Identity?.Name ?? "Unknown";
                            logger.LogInformation(
                                "Token validated for user: {UserPrincipal}",
                                userPrincipal);

                            return Task.CompletedTask;
                        },
                        OnAuthenticationFailed = context =>
                        {
                            var logger = context.HttpContext.RequestServices
                                .GetRequiredService<ILogger<Program>>();

                            logger.LogError(
                                context.Exception,
                                "Authentication failed: {ErrorMessage}",
                                context.Exception.Message);

                            return Task.CompletedTask;
                        }
                    };
                });

            Log.Information("✅ JWT Bearer authentication configured for Azure AD tenant: {TenantId}", 
                azureAdOptions.TenantId);
        }
        else
        {
            Log.Warning("⚠️  Azure AD authentication not configured - MCP will not validate user tokens");
        }

        // Add authorization services with compliance RBAC policies
        builder.Services.AddAuthorization(options =>
        {
            // Remediation policies
            options.AddPolicy("CanExecuteRemediation", policy =>
                policy.RequireRole(
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Administrator,
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Analyst));

            options.AddPolicy("CanApproveRemediation", policy =>
                policy.RequireRole(Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Administrator));

            // Evidence and export policies
            options.AddPolicy("CanExportEvidence", policy =>
                policy.RequireRole(
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Administrator,
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Auditor));

            options.AddPolicy("CanCollectEvidence", policy =>
                policy.RequireRole(
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Administrator,
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Auditor,
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Analyst));

            // Assessment policies
            options.AddPolicy("CanDeleteAssessment", policy =>
                policy.RequireRole(Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Administrator));

            options.AddPolicy("CanRunAssessment", policy =>
                policy.RequireRole(
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Administrator,
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Auditor,
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Analyst));

            // Document generation policies
            options.AddPolicy("CanGenerateDocuments", policy =>
                policy.RequireAssertion(context =>
                    context.User.IsInRole(Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Administrator) ||
                    context.User.IsInRole(Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Auditor) ||
                    context.User.HasClaim(c => c.Value == Platform.Engineering.Copilot.Core.Authorization.CompliancePermissions.GenerateDocuments)));

            options.AddPolicy("CanExportDocuments", policy =>
                policy.RequireRole(
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Administrator,
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Auditor));

            // Finding management policies
            options.AddPolicy("CanUpdateFindings", policy =>
                policy.RequireRole(
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Administrator,
                    Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Analyst));

            options.AddPolicy("CanDeleteFindings", policy =>
                policy.RequireRole(Platform.Engineering.Copilot.Core.Authorization.ComplianceRoles.Administrator));

            Log.Information("✅ Compliance authorization policies configured");
        });

        // Add Azure client factory for centralized credential management and user token passthrough
        builder.Services.AddAzureClientFactory();

        // Register user context service for accessing current user information
        builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.IUserContextService, 
            Platform.Engineering.Copilot.Core.Services.UserContextService>();

        // Add Core services (Multi-Agent Orchestrator, Plugins, etc.)
        builder.Services.AddPlatformEngineeringCopilotCore();
        
        // Configure which agents are enabled
        builder.Services.Configure<Platform.Engineering.Copilot.Core.Configuration.AgentConfiguration>(
            builder.Configuration.GetSection(Platform.Engineering.Copilot.Core.Configuration.AgentConfiguration.SectionName));
        
        // Configure individual agent options from nested sections
        builder.Services.Configure<Platform.Engineering.Copilot.Infrastructure.Agent.Configuration.InfrastructureAgentOptions>(
            builder.Configuration.GetSection("AgentConfiguration:InfrastructureAgent"));
        builder.Services.Configure<Platform.Engineering.Copilot.Compliance.Core.Configuration.ComplianceAgentOptions>(
            builder.Configuration.GetSection("AgentConfiguration:ComplianceAgent"));
        builder.Services.Configure<Platform.Engineering.Copilot.CostManagement.Core.Configuration.CostManagementAgentOptions>(
            builder.Configuration.GetSection("AgentConfiguration:CostManagementAgent"));
        builder.Services.Configure<Platform.Engineering.Copilot.Discovery.Core.Configuration.DiscoveryAgentOptions>(
            builder.Configuration.GetSection("AgentConfiguration:DiscoveryAgent"));
        
        // Add domain-specific agents and plugins based on configuration
        var agentConfigSection = builder.Configuration.GetSection(Platform.Engineering.Copilot.Core.Configuration.AgentConfiguration.SectionName);
        var agentConfig = new Platform.Engineering.Copilot.Core.Configuration.AgentConfiguration();
        agentConfig.SetConfiguration(agentConfigSection);

        var logger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger("AgentLoader");
        logger.LogInformation("🔧 Loading agents based on configuration...");

        int enabledCount = 0;

        if (agentConfig.IsAgentEnabled("Compliance"))
        {
            var complianceConfig = builder.Configuration.GetSection("AgentConfiguration:ComplianceAgent");
            builder.Services.AddComplianceAgent(complianceConfig);
            logger.LogInformation("✅ Compliance agent enabled");
            enabledCount++;
        }

        if (agentConfig.IsAgentEnabled("CostManagement"))
        {
            builder.Services.AddCostManagementAgent();
            logger.LogInformation("✅ CostManagement agent enabled");
            enabledCount++;
        }

        if (agentConfig.IsAgentEnabled("Discovery"))
        {
            builder.Services.AddDiscoveryAgent();
            logger.LogInformation("✅ Discovery agent enabled");
            enabledCount++;
        }

        if (agentConfig.IsAgentEnabled("Environment"))
        {
            builder.Services.AddEnvironmentAgent();
            logger.LogInformation("✅ Environment agent enabled");
            enabledCount++;
        }
        
        if (agentConfig.IsAgentEnabled("Infrastructure"))
        {
            builder.Services.AddInfrastructureAgent();
            logger.LogInformation("✅ Infrastructure agent enabled");
            enabledCount++;
        }

        if (agentConfig.IsAgentEnabled("Security"))
        {
            builder.Services.AddSecurityAgent();
            logger.LogInformation("✅ Security agent enabled");
            enabledCount++;
        }

        if (agentConfig.IsAgentEnabled("KnowledgeBase"))
        {
            var knowledgeBaseConfig = builder.Configuration.GetSection("AgentConfiguration:KnowledgeBaseAgent");
            builder.Services.AddKnowledgeBaseAgent(knowledgeBaseConfig);
            logger.LogInformation("✅ Knowledge Base agent enabled");
            enabledCount++;
        }

        logger.LogInformation($"🚀 Loaded {enabledCount} agents");

        // Register MCP Chat Tool - Scoped to match IIntelligentChatService
        builder.Services.AddScoped<PlatformEngineeringCopilotTools>();

        // Register HTTP bridge - Singleton (resolves scoped services per request)
        builder.Services.AddSingleton<McpHttpBridge>();

        var app = builder.Build();

        // Apply database migrations automatically
        using (var scope = app.Services.CreateScope())
        {
            try
            {
                var context = scope.ServiceProvider.GetRequiredService<PlatformEngineeringCopilotContext>();
                Log.Information("🔄 Ensuring database is created...");
                Log.Information("🔍 Database provider: {Provider}", context.Database.ProviderName);
                context.Database.EnsureCreated();
                Log.Information("✅ Database created/verified successfully");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "❌ Failed to create/verify database");
            }
        }

        // Configure URLs
        app.Urls.Add($"http://0.0.0.0:{port}");

        // Add authentication middleware only if Azure AD is configured
        var appAzureAdConfig = app.Configuration.GetSection(AzureAdOptions.SectionName);
        var appAzureAdOptions = new AzureAdOptions();
        appAzureAdConfig.Bind(appAzureAdOptions);
        
        if (!string.IsNullOrEmpty(appAzureAdOptions.TenantId) && !string.IsNullOrEmpty(appAzureAdOptions.Audience))
        {
            // Add authentication middleware (must be before authorization)
            app.UseAuthentication();
            app.UseAuthorization();

            // Add compliance authorization middleware for auditing access to compliance endpoints
            app.UseComplianceAuthorization();

            // Add user token middleware to extract CAC identity and create Azure credentials
            app.UseUserTokenAuthentication();
        }

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