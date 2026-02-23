using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Data.Services;

/// <summary>
/// Hosted service that initializes both databases on startup.
/// SQL Server: applies EF migrations. SQLite: uses EnsureCreated.
/// Provider selected via "DatabaseProvider" config key (per research.md §4).
/// </summary>
public class DatabaseInitializationService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseInitializationService> _logger;

    public DatabaseInitializationService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DatabaseInitializationService> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var provider = _configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
        _logger.LogInformation("Initializing databases with provider: {Provider}", provider);

        using var scope = _serviceProvider.CreateScope();

        await InitializeContextAsync<PlatformEngineeringCopilotContext>(
            scope, provider, "PlatformEngineeringCopilot", cancellationToken);

        await InitializeContextAsync<ChatDbContext>(
            scope, provider, "Chat", cancellationToken);

        _logger.LogInformation("Database initialization complete");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task InitializeContextAsync<TContext>(
        IServiceScope scope, string provider, string contextName, CancellationToken cancellationToken)
        where TContext : DbContext
    {
        try
        {
            var context = scope.ServiceProvider.GetRequiredService<TContext>();

            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Using EnsureCreated for {Context} (SQLite)", contextName);
                await context.Database.EnsureCreatedAsync(cancellationToken);
            }
            else
            {
                _logger.LogInformation("Applying migrations for {Context} (SQL Server)", contextName);
                await context.Database.MigrateAsync(cancellationToken);
            }

            _logger.LogInformation("{Context} database initialized successfully", contextName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize {Context} database", contextName);
            throw;
        }
    }
}

/// <summary>
/// Extension methods for registering database services.
/// </summary>
public static class DatabaseServiceExtensions
{
    /// <summary>
    /// Registers both DbContexts and the DatabaseInitializationService.
    /// Reads "DatabaseProvider" and "ConnectionStrings:DefaultConnection" from configuration.
    /// </summary>
    public static IServiceCollection AddPlatformDatabase(
        this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required");

        if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
                options.UseSqlite(connectionString));

            services.AddDbContext<ChatDbContext>(options =>
                options.UseSqlite(connectionString));
        }
        else
        {
            services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddDbContext<ChatDbContext>(options =>
                options.UseSqlServer(connectionString));
        }

        services.AddHostedService<DatabaseInitializationService>();

        return services;
    }
}
