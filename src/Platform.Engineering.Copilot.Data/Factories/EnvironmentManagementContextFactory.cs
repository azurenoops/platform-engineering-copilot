using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.Data.Context;

namespace Platform.Engineering.Copilot.Data.Factories;

/// <summary>
/// Design-time factory for creating DbContext instances during migrations
/// </summary>
public class EnvironmentManagementContextFactory : IDesignTimeDbContextFactory<PlatformEngineeringCopilotContext>
{
    public PlatformEngineeringCopilotContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlatformEngineeringCopilotContext>();

        // Build configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Get connection string and provider
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "./Data/SupervisorEnvironmentManagement.db";
        
        var databaseProvider = configuration.GetValue<string>("DatabaseProvider") ?? "Sqlite";

        switch (databaseProvider.ToLower())
        {
            case "sqlite":
                optionsBuilder.UseSqlite(connectionString, options =>
                {
                    options.MigrationsAssembly("Platform.Engineering.Copilot.Data");
                });
                break;
                
            case "sqlserver":
            default:
                optionsBuilder.UseSqlServer(connectionString, options =>
                {
                    options.MigrationsAssembly("Platform.Engineering.Copilot.Data");
                    options.CommandTimeout(300); // 5 minutes for long-running migrations
                });
                break;
        }

        return new PlatformEngineeringCopilotContext(optionsBuilder.Options);
    }
}