using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Data.Context;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

// Add shared configuration from root directory
var sharedConfigPath = Path.Combine(builder.Environment.ContentRootPath, "../..", "appsettings.json");
if (File.Exists(sharedConfigPath))
{
    builder.Configuration.AddJsonFile(sharedConfigPath, optional: false, reloadOnChange: true);
    Console.WriteLine($"[Admin API] Loaded shared configuration from: {sharedConfigPath}");
}

// Configure to listen on port 7002
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(7002);
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

// Register database context - use shared database from root
var sharedDbPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../..", "environment_management.db"));
var connectionString = $"Data Source={sharedDbPath}";

Console.WriteLine($"[Admin API] Using database: {sharedDbPath}");

builder.Services.AddDbContext<EnvironmentManagementContext>(options =>
    options.UseSqlite(connectionString));

// Register HTTP client (required by several services)
builder.Services.AddHttpClient();

// Add Gateway services manually (excluding problematic singletons that depend on scoped services)
// Azure Services
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IAzureResourceService, Platform.Engineering.Copilot.Core.Services.AzureServices.AzureResourceService>();
builder.Services.AddSingleton<Platform.Engineering.Copilot.Core.Interfaces.IAzureMetricsService, Platform.Engineering.Copilot.Core.Services.AzureMetricsService>();
// Note: AzureResourceHealthService temporarily excluded from Governance project - see Governance.csproj
// builder.Services.AddSingleton<Platform.Engineering.Copilot.Core.Interfaces.IAzureResourceHealthService, Platform.Engineering.Copilot.Core.Services.StubAzureResourceHealthService>();

// Cost Management Services (now properly scoped to match dependencies)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.AzureCostManagementService>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IAzureCostManagementService>(
    provider => provider.GetRequiredService<Platform.Engineering.Copilot.Core.Services.AzureCostManagementService>());

// Cost Optimization and Predictive Scaling Engines (now properly scoped)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.ICostOptimizationEngine, Platform.Engineering.Copilot.Core.Services.CostOptimizationEngine>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.Infrastructure.IPredictiveScalingEngine, Platform.Engineering.Copilot.Core.Services.Infrastructure.PredictiveScalingEngine>();

// Environment Storage Service (for database persistence)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.Infrastructure.EnvironmentStorageService>();

// Deployment Orchestration Service
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IDeploymentOrchestrationService, Platform.Engineering.Copilot.Core.Services.DeploymentOrchestrationService>();

// Add services needed by EnvironmentManagementEngine
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IGitHubServices, Platform.Engineering.Copilot.Core.Services.GitHubGatewayService>();

// Navy Flankspeed Onboarding Service
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IOnboardingService, Platform.Engineering.Copilot.Core.Services.Onboarding.FlankspeedOnboardingService>();

// Notification Services (Phase 5)
builder.Services.Configure<Platform.Engineering.Copilot.Core.Configuration.EmailConfiguration>(
    builder.Configuration.GetSection("EmailNotifications"));
builder.Services.Configure<Platform.Engineering.Copilot.Core.Configuration.SlackConfiguration>(
    builder.Configuration.GetSection("SlackNotifications"));
builder.Services.AddHttpClient("SlackWebhook");
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.Notifications.IEmailService, 
    Platform.Engineering.Copilot.Core.Services.Notifications.EmailService>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.Notifications.ISlackService, 
    Platform.Engineering.Copilot.Core.Services.Notifications.SlackService>();

// Teams Notification Service (Phase 4)
builder.Services.AddHttpClient(); // Required for TeamsNotificationService
builder.Services.AddSingleton<Platform.Engineering.Copilot.Core.Interfaces.ITeamsNotificationService, 
    Platform.Engineering.Copilot.Core.Services.Notifications.TeamsNotificationService>();

// Add Memory Cache (needed by NistControlsService)
builder.Services.AddMemoryCache();

// Add ComplianceMetricsService (needed by NistControlsService)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.Compliance.ComplianceMetricsService>();

// Add services needed by AtoComplianceEngine
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.INistControlsService, Platform.Engineering.Copilot.Core.Services.Compliance.NistControlsService>();

// Environment Management Engine (for environment lifecycle operations)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IEnvironmentManagementEngine, Platform.Engineering.Copilot.Core.Services.Infrastructure.EnvironmentManagementEngine>();

// Infrastructure Provisioning Service (for foundational infrastructure resources)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IInfrastructureProvisioningService, Platform.Engineering.Copilot.Core.Services.Infrastructure.InfrastructureProvisioningService>();

// Governance Engine (for policy enforcement and approval workflows)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IGovernanceEngine, Platform.Engineering.Copilot.Core.Services.Governance.GovernanceEngine>();

// Azure Policy Service (for Azure policy evaluation)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.IAzurePolicyService, Platform.Engineering.Copilot.Core.Services.AzurePolicyEngine>();

// ATO Compliance Engine (for security scanning) - Will be registered as optional for now
// builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IAtoComplianceEngine, Platform.Engineering.Copilot.Core.Services.Compliance.AtoComplianceEngine>();

// Dynamic Template Generator (core service for template creation)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.IDynamicTemplateGenerator, Platform.Engineering.Copilot.Core.Services.DynamicTemplateGeneratorService>();

// Template Storage Service (for saving/loading templates)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.ITemplateStorageService, Platform.Engineering.Copilot.Core.Data.Services.TemplateStorageService>();

// Register Configuration Validators
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.Validation.IConfigurationValidator, Platform.Engineering.Copilot.Core.Services.Validation.Validators.AKSConfigValidator>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.Validation.IConfigurationValidator, Platform.Engineering.Copilot.Core.Services.Validation.Validators.EKSConfigValidator>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.Validation.IConfigurationValidator, Platform.Engineering.Copilot.Core.Services.Validation.Validators.GKEConfigValidator>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.Validation.IConfigurationValidator, Platform.Engineering.Copilot.Core.Services.Validation.Validators.ECSConfigValidator>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.Validation.IConfigurationValidator, Platform.Engineering.Copilot.Core.Services.Validation.Validators.LambdaConfigValidator>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.Validation.IConfigurationValidator, Platform.Engineering.Copilot.Core.Services.Validation.Validators.CloudRunConfigValidator>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.Validation.IConfigurationValidator, Platform.Engineering.Copilot.Core.Services.Validation.Validators.VMConfigValidator>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.Validation.IConfigurationValidator, Platform.Engineering.Copilot.Core.Services.Validation.AppServiceConfigValidator>();
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.Validation.IConfigurationValidator, Platform.Engineering.Copilot.Core.Services.Validation.ContainerAppsConfigValidator>();

// Register Configuration Validation Service (orchestrator)
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.Validation.ConfigurationValidationService>();

// Register Admin services
builder.Services.AddScoped<ITemplateAdminService, TemplateAdminService>();

// Register Azure Pricing Service for cost estimates
builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.Cost.IAzurePricingService, Platform.Engineering.Copilot.Core.Services.Cost.AzurePricingService>();

// NOTE: DeploymentPollingService removed - legacy service from Extensions project

var app = builder.Build();

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

Console.WriteLine("==============================================");
Console.WriteLine("Supervisor Platform Admin API");
Console.WriteLine("==============================================");
Console.WriteLine($"Environment: {app.Environment.EnvironmentName}");
Console.WriteLine($"Listening on: http://localhost:7002");
Console.WriteLine($"Swagger UI: http://localhost:7002");
Console.WriteLine("==============================================");

app.Run();
