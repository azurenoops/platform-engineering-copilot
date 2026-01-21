using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Seed;

namespace Platform.Engineering.Copilot.Core.Data.Services;

/// <summary>
/// Database initialization service that handles database migrations, schema updates, and initial data seeding for the platform.
/// This hosted service ensures the database is properly configured and contains required seed data on application startup.
/// 
/// Migration Strategy:
/// - SQL Server (production): Uses EF Core migrations (dotnet ef migrations add/database update)
/// - In-Memory (testing): Uses EnsureCreated() since migrations don't apply
/// </summary>
public class DatabaseInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializationService> _logger;

    /// <summary>
    /// Initializes a new instance of the DatabaseInitializationService with dependency injection support.
    /// Sets up database initialization capabilities with migration handling and data seeding.
    /// </summary>
    /// <param name="serviceProvider">Service provider for resolving database context and related services</param>
    /// <param name="logger">Logger for database initialization operations and migration events</param>
    public DatabaseInitializationService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PlatformEngineeringCopilotContext>();

        try
        {
            _logger.LogInformation("Starting database initialization...");

            // Check if we're using a relational database (not in-memory)
            var isRelational = context.Database.IsRelational();
            
            if (isRelational)
            {
                // For relational databases (SQL Server), apply migrations
                _logger.LogInformation("Applying database migrations...");
                
                var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
                var pendingCount = pendingMigrations.Count();
                
                if (pendingCount > 0)
                {
                    _logger.LogInformation("Found {Count} pending migrations to apply", pendingCount);
                    foreach (var migration in pendingMigrations)
                    {
                        _logger.LogInformation("  - {Migration}", migration);
                    }
                    
                    await context.Database.MigrateAsync(cancellationToken);
                    _logger.LogInformation("Database migrations applied successfully");
                }
                else
                {
                    _logger.LogInformation("Database is up to date, no migrations to apply");
                }
            }
            else
            {
                // For in-memory databases, just ensure it's created
                _logger.LogInformation("Using non-relational database, ensuring database is created...");
                await context.Database.EnsureCreatedAsync(cancellationToken);
                _logger.LogInformation("Database created/verified successfully");
            }

            // Seed initial data
            _logger.LogInformation("Seeding database with initial data...");
            await DatabaseSeeder.SeedAsync(context);
            _logger.LogInformation("Database seeding completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during database initialization");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Extension methods for adding database initialization
/// </summary>
public static class DatabaseInitializationExtensions
{
    /// <summary>
    /// Add database initialization service
    /// </summary>
    public static IServiceCollection AddDatabaseInitialization(this IServiceCollection services)
    {
        services.AddHostedService<DatabaseInitializationService>();
        return services;
    }
}