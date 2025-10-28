using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.Data.Context;

namespace Platform.Engineering.Copilot.Data.Extensions;

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
                        sqliteOptions.MigrationsAssembly("Platform.Engineering.Copilot.Data");
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
        services.AddScoped<IEnvironmentTemplateRepository, EnvironmentTemplateRepository>();
        services.AddScoped<IEnvironmentDeploymentRepository, EnvironmentDeploymentRepository>();
        services.AddScoped<IScalingPolicyRepository, ScalingPolicyRepository>();
        services.AddScoped<IEnvironmentMetricsRepository, EnvironmentMetricsRepository>();
        services.AddScoped<IComplianceScanRepository, ComplianceScanRepository>();
        services.AddScoped<ISemanticIntentRepository, SemanticIntentRepository>();

        // Add unit of work pattern
        services.AddScoped<IUnitOfWork, UnitOfWork>();

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
        services.AddScoped<IEnvironmentTemplateRepository, EnvironmentTemplateRepository>();
        services.AddScoped<IEnvironmentDeploymentRepository, EnvironmentDeploymentRepository>();
        services.AddScoped<IScalingPolicyRepository, ScalingPolicyRepository>();
        services.AddScoped<IEnvironmentMetricsRepository, EnvironmentMetricsRepository>();
        services.AddScoped<IComplianceScanRepository, ComplianceScanRepository>();
        services.AddScoped<ISemanticIntentRepository, SemanticIntentRepository>();

        // Add unit of work pattern
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}

// Placeholder interfaces for repositories (to be implemented)
public interface IEnvironmentTemplateRepository { }
public interface IEnvironmentDeploymentRepository { }
public interface IScalingPolicyRepository { }
public interface IEnvironmentMetricsRepository { }
public interface IComplianceScanRepository { }
public interface ISemanticIntentRepository { }
public interface IUnitOfWork { }

// Placeholder implementations (to be expanded)
public class EnvironmentTemplateRepository : IEnvironmentTemplateRepository { }
public class EnvironmentDeploymentRepository : IEnvironmentDeploymentRepository { }
public class ScalingPolicyRepository : IScalingPolicyRepository { }
public class EnvironmentMetricsRepository : IEnvironmentMetricsRepository { }
public class ComplianceScanRepository : IComplianceScanRepository { }
public class SemanticIntentRepository : ISemanticIntentRepository { }
public class UnitOfWork : IUnitOfWork { }