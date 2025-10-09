using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Data.Context;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.API.Services;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Governance.Extensions;
using Platform.Engineering.Copilot.DocumentProcessing.Extensions;

namespace Platform.Engineering.Copilot.API;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add shared configuration from root directory
        var sharedConfigPath = Path.Combine(builder.Environment.ContentRootPath, "../..", "appsettings.json");
        if (File.Exists(sharedConfigPath))
        {
            builder.Configuration.AddJsonFile(sharedConfigPath, optional: false, reloadOnChange: true);
            Console.WriteLine($"Loaded shared configuration from: {sharedConfigPath}");
        }

        // Configure to listen on port 7001
        builder.WebHost.UseUrls("http://localhost:7001");

        // Add services to the container
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new() { Title = "Platform Engineering Copilot API", Version = "v1" });
        });

        // Configure CORS
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyMethod()
                      .AllowAnyHeader();
            });
        });

        // Configure options from appsettings.json
        builder.Services.Configure<Platform.Engineering.Copilot.Core.Configuration.GatewayOptions>(
            builder.Configuration.GetSection("Gateway"));

        // Register HttpClient
        builder.Services.AddHttpClient();

        // Register Database Context
        var databaseProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
        var connectionString = databaseProvider.ToLower() switch
        {
            "sqlserver" => builder.Configuration.GetConnectionString("SqlServerConnection"),
            _ => builder.Configuration.GetConnectionString("DefaultConnection")
        } ?? "Data Source=environment_management.db";

        if (databaseProvider.ToLower() == "sqlserver")
        {
            builder.Services.AddDbContext<EnvironmentManagementContext>(options =>
                options.UseSqlServer(connectionString));
        }
        else
        {
            builder.Services.AddDbContext<EnvironmentManagementContext>(options =>
                options.UseSqlite(connectionString));
        }

        // Register semantic processing services
        builder.Services.AddSupervisorCore();

        // Register production-ready governance services with NIST integration
        builder.Services.AddEnhancedAtoCompliance(builder.Configuration);
        
        // Register Gateway Services (includes ComplianceIntegrationService)
        // Note: Gateway project was removed during refactoring - these services may need to be registered individually
        // builder.Services.AddGatewayServices(builder.Configuration);

        // Register Document Processing Services
        builder.Services.AddDocumentProcessing(builder.Configuration);

        // Register other core services
        builder.Services.AddSingleton<GitHubGatewayService>();
        builder.Services.AddSingleton<IGitHubServices>(provider => provider.GetRequiredService<GitHubGatewayService>());
        builder.Services.AddSingleton<AzurePolicyService>();

        // NOTE: Dynamic PluginSystem removed - using Semantic Kernel plugins instead
        // NOTE: All legacy tool registrations removed - using SK plugins in IntelligentChatService_v2

        // Register template storage service
        builder.Services.AddScoped<ITemplateStorageService, Platform.Engineering.Copilot.Core.Data.Services.TemplateStorageService>();

        // Register infrastructure provisioning service (required by InfrastructureProvisioningTool)
        builder.Services.AddScoped<IInfrastructureProvisioningService, Platform.Engineering.Copilot.Core.Services.Infrastructure.InfrastructureProvisioningService>();

        // Register Azure Pricing Service for real-time cost estimates
        builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.Cost.IAzurePricingService, Platform.Engineering.Copilot.Core.Services.Cost.AzurePricingService>();

        // NOTE: DynamicTemplateGenerator is registered in AddSupervisorCore() extension method
        // NOTE: All legacy IMcpToolHandler tool registrations removed - functionality now provided by SK plugins

        // Register notification configuration and services
        builder.Services.Configure<Platform.Engineering.Copilot.Core.Configuration.EmailConfiguration>(
            builder.Configuration.GetSection("EmailNotifications"));
        builder.Services.Configure<Platform.Engineering.Copilot.Core.Configuration.SlackConfiguration>(
            builder.Configuration.GetSection("SlackNotifications"));
        builder.Services.AddHttpClient("SlackWebhook");
        builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.Notifications.IEmailService, Platform.Engineering.Copilot.Core.Services.Notifications.EmailService>();
        builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Services.Notifications.ISlackService, Platform.Engineering.Copilot.Core.Services.Notifications.SlackService>();

        // Register Teams notification service
        builder.Services.AddHttpClient(); // Required for TeamsNotificationService
        builder.Services.AddSingleton<Platform.Engineering.Copilot.Core.Interfaces.ITeamsNotificationService, Platform.Engineering.Copilot.Core.Services.Notifications.TeamsNotificationService>();
        
        // Register Flankspeed onboarding services and tool
        builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IOnboardingService, Platform.Engineering.Copilot.Core.Services.Onboarding.FlankspeedOnboardingService>();
        
        // Register generic onboarding adapter (wraps IOnboardingService to provide IGenericOnboardingService<OnboardingRequest>)
        builder.Services.AddScoped<Platform.Engineering.Copilot.Core.Interfaces.IGenericOnboardingService<Platform.Engineering.Copilot.Data.Entities.OnboardingRequest>, 
            Platform.Engineering.Copilot.Core.Services.Onboarding.FlankspeedGenericOnboardingAdapter>();
        
        // NOTE: FlankspeedOnboardingTool removed - functionality provided by OnboardingPlugin

        // Register API services
        builder.Services.AddScoped<PlatformToolService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Platform Engineering API v1"));
        }

        app.UseCors("AllowAll");
        
        // Add audit logging middleware
        app.UseMiddleware<Platform.Engineering.Copilot.API.Middleware.AuditLoggingMiddleware>();
        
        app.UseAuthorization();
        app.MapControllers();

        // Initialize database
        using (var scope = app.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<EnvironmentManagementContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            try
            {
                context.Database.EnsureCreated();
                logger.LogInformation("✅ Database initialized successfully");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "⚠️ Database initialization failed, continuing with in-memory fallback");
            }
        }

        app.Run();
    }
}