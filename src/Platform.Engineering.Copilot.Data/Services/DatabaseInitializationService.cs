using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Data.Context;
using Platform.Engineering.Copilot.Data.Seed;

namespace Platform.Engineering.Copilot.Data.Services;

/// <summary>
/// Database initialization service that handles database migrations, schema updates, and initial data seeding for the platform.
/// This hosted service ensures the database is properly configured and contains required seed data on application startup.
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
        var context = scope.ServiceProvider.GetRequiredService<EnvironmentManagementContext>();

        try
        {
            _logger.LogInformation("Starting database initialization...");

            // Apply pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (pendingMigrations.Any())
            {
                _logger.LogInformation("Applying {Count} pending migrations: {Migrations}", 
                    pendingMigrations.Count(), string.Join(", ", pendingMigrations));
                
                await context.Database.MigrateAsync(cancellationToken);
                _logger.LogInformation("Database migrations applied successfully");
            }
            else
            {
                _logger.LogInformation("Database is up to date, no migrations needed");
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