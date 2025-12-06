using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Admin.Services;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Core.Services.Azure.Cost;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services.Validation.Validators;
using Platform.Engineering.Copilot.Core.Services.Validation;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Cost;
using Platform.Engineering.Copilot.Infrastructure.Agent.Services.Deployment;
using Platform.Engineering.Copilot.Core.Interfaces.Deployment;
using Platform.Engineering.Copilot.Core.Interfaces.GitHub;
using Platform.Engineering.Copilot.Core.Interfaces.Notifications;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Governance;
using Platform.Engineering.Copilot.Security.Agent.Extensions;
using Platform.Engineering.Copilot.Infrastructure.Core.Services;
using Platform.Engineering.Copilot.Compliance.Core.Extensions;
using Platform.Engineering.Copilot.CostManagement.Core.Extensions;
using Platform.Engineering.Copilot.Environment.Core.Extensions;
using Platform.Engineering.Copilot.Discovery.Core.Extensions;
using Platform.Engineering.Copilot.Infrastructure.Core.Extensions;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Extensions;
using Platform.Engineering.Copilot.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add shared configuration from root directory
var sharedConfigPath = Path.Combine(builder.Environment.ContentRootPath, "../..", "appsettings.json");
if (File.Exists(sharedConfigPath))
{
    builder.Configuration.AddJsonFile(sharedConfigPath, optional: false, reloadOnChange: true);
    Console.WriteLine($"[Admin API] Loaded shared configuration from: {sharedConfigPath}");
}

// Configure to listen on port 5002
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5002);
});

// Add services to the container
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = true;
        // Add enum string converter to allow enum deserialization from strings
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogError("Model validation failed. Errors: {Errors}", 
                string.Join(", ", context.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage + " | " + e.Exception?.Message)));
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(context.ModelState);
        };
    });

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "Supervisor Platform Admin API", 
        Version = "v1",
        Description = "Admin API for platform engineers to manage templates, infrastructure, and platform operations"
    });
});

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Register database context - use SQL Server from connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

Console.WriteLine($"[Admin API] Using database connection");

builder.Services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
    options.UseSqlServer(connectionString));

// Register HTTP client (required by several services)
builder.Services.AddHttpClient();

// Add Gateway services manually (excluding problematic singletons that depend on scoped services)
// Azure Services
builder.Services.AddScoped<IAzureResourceService, AzureResourceService>();
builder.Services.AddSingleton<IAzureMetricsService, AzureMetricsService>();
// Note: AzureResourceHealthService temporarily excluded from Governance project - see Governance.csproj
// builder.Services.AddSingleton<IAzureResourceHealthService, Platform.Engineering.Copilot.Core.Services.StubAzureResourceHealthService>();

// Cost Management Services (now properly scoped to match dependencies)
builder.Services.AddScoped<AzureCostManagementService>();
builder.Services.AddScoped<IAzureCostManagementService>(
    provider => provider.GetRequiredService<AzureCostManagementService>());

// Cost Optimization and Predictive Scaling Engines (now properly scoped)
builder.Services.AddScoped<ICostOptimizationEngine, CostOptimizationEngine>();
builder.Services.AddScoped<IPredictiveScalingEngine, PredictiveScalingEngine>();

// Environment Storage Service (for database persistence)
builder.Services.AddScoped<EnvironmentStorageService>();

// Deployment Orchestration Service
builder.Services.AddScoped<IDeploymentOrchestrationService, DeploymentOrchestrationService>();

// Add services needed by EnvironmentManagementEngine
builder.Services.AddScoped<IGitHubServices, Platform.Engineering.Copilot.Core.Services.GitHubGatewayService>();

// Notification Services (Phase 5)
builder.Services.Configure<Platform.Engineering.Copilot.Core.Configuration.EmailConfiguration>(
    builder.Configuration.GetSection("EmailNotifications"));
builder.Services.Configure<Platform.Engineering.Copilot.Core.Configuration.SlackConfiguration>(
    builder.Configuration.GetSection("SlackNotifications"));
builder.Services.AddHttpClient("SlackWebhook");
builder.Services.AddScoped<IEmailService, 
    Platform.Engineering.Copilot.Core.Services.Notifications.EmailService>();
builder.Services.AddScoped<ISlackService, 
    Platform.Engineering.Copilot.Core.Services.Notifications.SlackService>();

// Teams Notification Service (Phase 4)
builder.Services.AddHttpClient(); // Required for TeamsNotificationService
builder.Services.AddSingleton<ITeamsNotificationService, 
    Platform.Engineering.Copilot.Core.Services.Notifications.TeamsNotificationService>();

// Add Memory Cache (needed by NistControlsService)
builder.Services.AddMemoryCache();

// Register Platform.Engineering.Copilot.Core services (includes OrchestratorAgent, SemanticKernelService, etc.)
builder.Services.AddPlatformEngineeringCopilotCore(builder.Configuration);

// Add ComplianceMetricsService (needed by NistControlsService)
builder.Services.AddScoped<ComplianceMetricsService>();

// Add services needed by AtoComplianceEngine
builder.Services.AddScoped<INistControlsService, NistControlsService>();

// Environment Management Engine (for environment lifecycle operations)
builder.Services.AddScoped<IEnvironmentManagementEngine, EnvironmentManagementEngine>();

// Infrastructure Provisioning Service (for foundational infrastructure resources)
builder.Services.AddScoped<IInfrastructureProvisioningService, InfrastructureProvisioningService>();

// Governance Engine (for policy enforcement and approval workflows)
builder.Services.AddScoped<IGovernanceEngine, GovernanceEngine>();

// Azure Policy Service (for Azure policy evaluation)
builder.Services.AddScoped<IAzurePolicyService, AzurePolicyEngine>();

// ATO Compliance Engine (for security scanning) - Will be registered as optional for now
// builder.Services.AddScoped<IAtoComplianceEngine, Platform.Engineering.Copilot.Core.Services.Compliance.AtoComplianceEngine>();

// Dynamic Template Generator (core service for template creation)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.IDynamicTemplateGenerator, Platform.Engineering.Copilot.Core.Services.DynamicTemplateGeneratorService>();

// Template Storage Service (for saving/loading templates)
builder.Services.AddScoped<ITemplateStorageService, Platform.Engineering.Copilot.Core.Data.Services.TemplateStorageService>();

// Note: Configuration Validators are already registered by AddPlatformEngineeringCopilotCore()

// Register Configuration Validation Service (orchestrator)
builder.Services.AddScoped<ConfigurationValidationService>();

// Register Admin services
builder.Services.AddScoped<ITemplateAdminService, TemplateAdminService>();
builder.Services.AddScoped<AgentConfigurationService>();

// Register Azure Pricing Service for cost estimates
builder.Services.AddScoped<IAzurePricingService, AzurePricingService>();

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

// NOTE: DeploymentPollingService removed - legacy service from Extensions project

var app = builder.Build();

// Initialize database - ensure database is created
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<PlatformEngineeringCopilotContext>();
        Console.WriteLine("[Admin API] Ensuring database is created...");
        context.Database.EnsureCreated();
        Console.WriteLine("[Admin API] Database initialized successfully");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while initializing the database");
        throw;
    }
}

// Add middleware to log request bodies for debugging
app.Use(async (context, next) =>
{
    if (context.Request.Method == "PUT" && context.Request.Path.StartsWithSegments("/api/admin/templates"))
    {
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;
        
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("PUT Request Body: {Body}", body);
    }
    await next();
});

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Supervisor Platform Admin API v1");
        c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    });
}

app.UseCors();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Initialize database and agent configurations on startup
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PlatformEngineeringCopilotContext>();
    var agentService = scope.ServiceProvider.GetRequiredService<AgentConfigurationService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Ensure database is created and migrations are applied
        logger.LogInformation("Ensuring database is created and applying migrations...");
        await dbContext.Database.MigrateAsync();
        logger.LogInformation("Database migrations completed successfully");
        
        // Seed agent configurations
        logger.LogInformation("Seeding agent configurations from appsettings.json...");
        await agentService.SeedFromConfigurationAsync();
        logger.LogInformation("Agent configuration seeding completed");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to initialize database or seed agent configurations on startup");
    }
}

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new 
{ 
    status = "healthy",
    service = "Platform Engineering Copilot Admin API",
    version = "1.0.0"
}));

Console.WriteLine("==============================================");
Console.WriteLine("Supervisor Platform Admin API");
Console.WriteLine("==============================================");
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"Listening on: http://localhost:5002");
Console.WriteLine($"Swagger UI: http://localhost:5002");
Console.WriteLine("==============================================");

app.Run();
