using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Platform.Engineering.Copilot.Core.Data.Context;

namespace Platform.Engineering.Copilot.Core.Data.Factories;

/// <summary>
/// Design-time factory for creating DbContext instances during migrations.
/// Used by EF Core CLI tools (dotnet ef migrations add, dotnet ef database update).
/// 
/// Default: SQL Server (production target)
/// Override: Set DatabaseProvider=Sqlite in appsettings.json or environment variable
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

        // Get connection string and provider - default to SQL Server for production
        var connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? "Server=localhost,1433;Database=PlatformDb;User Id=sa;Password=YourPassword123!;TrustServerCertificate=True;";
        
        var databaseProvider = configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer";

        switch (databaseProvider.ToLower())
        {
            case "sqlite":
                optionsBuilder.UseSqlite(connectionString, options =>
                {
                    options.MigrationsAssembly("Platform.Engineering.Copilot.Core");
                });
                break;
                
            case "sqlserver":
            default:
                optionsBuilder.UseSqlServer(connectionString, options =>
                {
                    options.MigrationsAssembly("Platform.Engineering.Copilot.Core");
                    options.CommandTimeout(300); // 5 minutes for long-running migrations
                });
                break;
        }

        return new PlatformEngineeringCopilotContext(optionsBuilder.Options);
    }
}