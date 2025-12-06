using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Data;

namespace Platform.Engineering.Copilot.Compliance.Core.Extensions;

/// <summary>
/// Extension methods for configuring compliance data services
/// </summary>
public static class ComplianceDataServiceExtensions
{
    /// <summary>
    /// Add compliance data services with SQL Server database
    /// </summary>
    public static IServiceCollection AddComplianceData(
        this IServiceCollection services, 
        string connectionString)
    {
        return services.AddComplianceData(options => 
            options.UseSqlServer(connectionString));
    }

    /// <summary>
    /// Add compliance data services with SQLite database
    /// </summary>
    public static IServiceCollection AddComplianceDataSqlite(
        this IServiceCollection services, 
        string connectionString = "Data Source=compliance.db")
    {
        return services.AddComplianceData(options => 
            options.UseSqlite(connectionString));
    }

    /// <summary>
    /// Add compliance data services with in-memory database (for testing)
    /// </summary>
    public static IServiceCollection AddComplianceDataInMemory(
        this IServiceCollection services, 
        string databaseName = "ComplianceTestDb")
    {
        return services.AddComplianceData(options => 
            options.UseInMemoryDatabase(databaseName));
    }

    /// <summary>
    /// Add compliance data services with custom DbContext configuration
    /// </summary>
    public static IServiceCollection AddComplianceData(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureContext)
    {
        // TODO: ComplianceContext database is not yet implemented
        // Add DbContext
        // services.AddDbContext<ComplianceContext>(configureContext);

        // Add data services - Register both interface and implementation
        services.AddScoped<IAssessmentService, AssessmentService>();

        return services;
    }

    /// <summary>
    /// Add compliance data services using configuration
    /// </summary>
    public static IServiceCollection AddComplianceData(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "DefaultConnection")
    {
        var connectionString = configuration.GetConnectionString(connectionStringName);
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{connectionStringName}' not found in configuration.");
        }

        return services.AddComplianceData(connectionString);
    }

    /// <summary>
    /// Ensure database is created and migrations are applied
    /// </summary>
    public static async Task<IServiceProvider> EnsureComplianceDatabaseAsync(this IServiceProvider services)
    {
        // TODO: ComplianceContext database is not yet implemented
        /*
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ComplianceContext>();
        
        await context.Database.EnsureCreatedAsync();
        */
        
        await Task.CompletedTask;
        return services;
    }

    /// <summary>
    /// Apply pending migrations to the compliance database
    /// </summary>
    public static async Task<IServiceProvider> MigrateComplianceDatabaseAsync(this IServiceProvider services)
    {
        // TODO: ComplianceContext database is not yet implemented
        /*
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ComplianceContext>();
        
        await context.Database.MigrateAsync();
        */
        
        await Task.CompletedTask;
        return services;
    }
}