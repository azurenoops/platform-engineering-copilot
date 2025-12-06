using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Repositories;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Core.Data.Extensions;

/// <summary>
/// Service collection extensions for data layer configuration
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add Environment Management database context and services
    /// </summary>
    public static IServiceCollection AddEnvironmentManagementData(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "DefaultConnection")
    {
        // Add Entity Framework DbContext
        services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(connectionStringName);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{connectionStringName}' not found.");
            }

            var databaseProvider = configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
            
            switch (databaseProvider.ToLower())
            {
                case "sqlite":
                    options.UseSqlite(connectionString, sqliteOptions =>
                    {
                        sqliteOptions.MigrationsAssembly("Platform.Engineering.Copilot.Core");
                    });
                    break;
                    
                case "sqlserver":
                default:
                    options.UseSqlServer(connectionString, sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(30),
                            errorNumbersToAdd: null);
                        
                        sqlOptions.CommandTimeout(60);
                        sqlOptions.MigrationsAssembly("Platform.Engineering.Copilot.Data");
                    });
                    break;
            }

            // Enable sensitive data logging in development
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (environment == "Development")
            {
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            }

            // Enable query tracking optimization
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        // Add repository services
        services.AddScoped<Repositories.IEnvironmentTemplateRepository, Repositories.EnvironmentTemplateRepository>();
        services.AddScoped<Repositories.IEnvironmentDeploymentRepository, Repositories.EnvironmentDeploymentRepository>();
        services.AddScoped<Repositories.IComplianceAssessmentRepository, Repositories.ComplianceAssessmentRepository>();
        
        // Semantic Intent Repository and Service (real implementations)
        services.AddScoped<Platform.Engineering.Copilot.Core.Data.Repositories.ISemanticIntentRepository, 
            Platform.Engineering.Copilot.Core.Data.Repositories.SemanticIntentRepository>();
        services.AddScoped<ISemanticIntentService, SemanticIntentService>();

        return services;
    }

    /// <summary>
    /// Add Environment Management database context with in-memory database (for testing)
    /// </summary>
    public static IServiceCollection AddEnvironmentManagementDataInMemory(
        this IServiceCollection services,
        string databaseName = "EnvironmentManagementTestDb")
    {
        services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
        {
            options.UseInMemoryDatabase(databaseName);
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        // Add repository services
        services.AddScoped<Repositories.IEnvironmentTemplateRepository, Repositories.EnvironmentTemplateRepository>();
        services.AddScoped<Repositories.IEnvironmentDeploymentRepository, Repositories.EnvironmentDeploymentRepository>();
        services.AddScoped<Repositories.IComplianceAssessmentRepository, Repositories.ComplianceAssessmentRepository>();
        
        // Semantic Intent Repository and Service (real implementations)
        services.AddScoped<Platform.Engineering.Copilot.Core.Data.Repositories.ISemanticIntentRepository, 
            Platform.Engineering.Copilot.Core.Data.Repositories.SemanticIntentRepository>();
        services.AddScoped<ISemanticIntentService, SemanticIntentService>();

        return services;
    }
}