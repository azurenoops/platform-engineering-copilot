using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using Platform.Engineering.Copilot.Chat.App.Data;
using Platform.Engineering.Copilot.Chat.App.Hubs;
using Platform.Engineering.Copilot.Chat.App.Services;
using Platform.Engineering.Copilot.Compliance.Core.Extensions;
using Platform.Engineering.Copilot.Infrastructure.Core.Extensions;
using Platform.Engineering.Copilot.CostManagement.Core.Extensions;
using Platform.Engineering.Copilot.Environment.Core.Extensions;
using Platform.Engineering.Copilot.Discovery.Core.Extensions;
using Platform.Engineering.Copilot.Security.Agent.Extensions;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Extensions;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Interfaces.GitHub;
using Platform.Engineering.Copilot.Infrastructure.Core.Services;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Configure Azure Key Vault for secure secret management
var keyVaultEndpoint = builder.Configuration["KeyVault:Endpoint"];
if (!string.IsNullOrEmpty(keyVaultEndpoint))
{
    try
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultEndpoint),
            new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                // Prioritize Managed Identity for Azure deployments
                ManagedIdentityClientId = builder.Configuration["KeyVault:ManagedIdentityClientId"],
                // Exclude IDE credentials for production security
                ExcludeVisualStudioCredential = true,
                ExcludeVisualStudioCodeCredential = true
            }));
        
        Log.Logger?.Information("✅ Azure Key Vault configured: {KeyVaultEndpoint}", keyVaultEndpoint);
    }
    catch (Exception ex)
    {
        Log.Logger?.Warning(ex, "⚠️  Failed to configure Azure Key Vault. Using local configuration only.");
    }
}
else
{
    Log.Logger?.Warning("⚠️  Key Vault not configured. Using local secrets only. Set 'KeyVault:Endpoint' in appsettings.json for production.");
}

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore.Hosting", LogEventLevel.Information) // Enable request logging
    .MinimumLevel.Override("Microsoft.AspNetCore.Routing", LogEventLevel.Information) // Enable routing logs
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/chat-app-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add Entity Framework - Chat DB
// Support both SQL Server (Docker) and SQLite (local dev)
var chatConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(chatConnectionString) && chatConnectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
{
    // SQL Server (Docker environment)
    Console.WriteLine("[Chat] Using SQL Server for Chat database");
    builder.Services.AddDbContext<ChatDbContext>(options =>
        options.UseSqlServer(chatConnectionString));
}
else
{
    // SQLite (local development)
    var sqliteConnection = chatConnectionString ?? "Data Source=chat.db";
    Console.WriteLine($"[Chat] Using SQLite for Chat database: {sqliteConnection}");
    builder.Services.AddDbContext<ChatDbContext>(options =>
        options.UseSqlite(sqliteConnection));
}

// Add Entity Framework - Platform Management DB (required by agents)
// Use SQL Server in Docker, SQLite locally
var platformConnectionString = builder.Configuration.GetConnectionString("SqlServerConnection");
if (!string.IsNullOrEmpty(platformConnectionString) && platformConnectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
{
    // SQL Server (Docker environment)
    Console.WriteLine("[Chat] Using SQL Server for Platform database");
    builder.Services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
        options.UseSqlServer(platformConnectionString));
}
else
{
    // SQLite (local development)
    var sharedDbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../..", "platform_engineering_copilot_management.db"));
    var sqliteConnectionString = $"Data Source={sharedDbPath}";
    Console.WriteLine($"[Chat] Using SQLite for Platform database: {sharedDbPath}");
    builder.Services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
        options.UseSqlite(sqliteConnectionString));
}

// Add HttpClient for API integration
builder.Services.AddHttpClient();

// Add SignalR with minimal configuration
builder.Services.AddSignalR();

// Add CORS - allow all origins in production for ACI deployment
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy
                .WithOrigins("http://localhost:3000", "https://localhost:3000", "http://localhost:5001")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            // In production, allow any origin (for ACI deployment)
            // For tighter security, specify exact origins
            policy
                .SetIsOriginAllowed(_ => true)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

// Register services
builder.Services.AddScoped<IChatService, ChatService>();

// Add required services for agents
builder.Services.AddScoped<IGitHubServices, Platform.Engineering.Copilot.Core.Services.GitHubGatewayService>();
builder.Services.AddScoped<IEnvironmentManagementEngine, EnvironmentManagementEngine>();
builder.Services.AddScoped<EnvironmentStorageService>();

// Register Platform.Engineering.Copilot.Core services (includes ConfigurationPlugin, OrchestratorAgent, SemanticKernelService, etc.)
builder.Services.AddPlatformEngineeringCopilotCore(builder.Configuration);

// Configure agent options from nested AgentConfiguration sections
builder.Services.Configure<Platform.Engineering.Copilot.Infrastructure.Agent.Configuration.InfrastructureAgentOptions>(
    builder.Configuration.GetSection("AgentConfiguration:InfrastructureAgent"));
builder.Services.Configure<Platform.Engineering.Copilot.Compliance.Core.Configuration.ComplianceAgentOptions>(
    builder.Configuration.GetSection("AgentConfiguration:ComplianceAgent"));
builder.Services.Configure<Platform.Engineering.Copilot.CostManagement.Core.Configuration.CostManagementAgentOptions>(
    builder.Configuration.GetSection("AgentConfiguration:CostManagementAgent"));
builder.Services.Configure<Platform.Engineering.Copilot.Discovery.Core.Configuration.DiscoveryAgentOptions>(
    builder.Configuration.GetSection("AgentConfiguration:DiscoveryAgent"));

// Add domain-specific agents and plugins
builder.Services.AddComplianceAgent(builder.Configuration);
builder.Services.AddInfrastructureAgent();
builder.Services.AddCostManagementAgent();
builder.Services.AddEnvironmentAgent();
builder.Services.AddDiscoveryAgent();
builder.Services.AddSecurityAgent();
var knowledgeBaseConfig = builder.Configuration.GetSection("AgentConfiguration:KnowledgeBaseAgent");
builder.Services.AddKnowledgeBaseAgent(knowledgeBaseConfig);

// Add SPA services
builder.Services.AddSpaStaticFiles(configuration =>
{
    configuration.RootPath = "wwwroot";
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add request logging middleware to see all incoming requests
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
        diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        diagnosticContext.Set("RemoteIP", httpContext.Connection.RemoteIpAddress?.ToString());
    };
});

app.UseCors();

// Only use HTTPS redirection in production
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseSpaStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

// Configure SPA - but exclude API routes from SPA proxy
app.MapWhen(context => !context.Request.Path.StartsWithSegments("/api") && 
                      !context.Request.Path.StartsWithSegments("/chathub"), 
    subApp =>
    {
        subApp.UseSpa(spa =>
        {
            spa.Options.SourcePath = "wwwroot";
            spa.Options.DefaultPage = "/index.html";

            if (app.Environment.IsDevelopment())
            {
                spa.UseProxyToSpaDevelopmentServer("http://localhost:3000");
            }
        });
    });

// Initialize databases
using (var scope = app.Services.CreateScope())
{
    var chatContext = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    await chatContext.Database.EnsureCreatedAsync();
    Log.Information("✅ Chat database initialized successfully");
    
    var platformContext = scope.ServiceProvider.GetRequiredService<PlatformEngineeringCopilotContext>();
    await platformContext.Database.EnsureCreatedAsync();
    Log.Information("✅ Platform database initialized successfully");
}

Log.Information("🚀 Enhanced Chat Application starting on {Environment}", app.Environment.EnvironmentName);

app.Run();

// Ensure to flush and stop internal timers/threads before application-exit
Log.CloseAndFlush();