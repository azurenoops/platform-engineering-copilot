using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Data.Extensions;
using Platform.Engineering.Copilot.Data.Context;
using Platform.Engineering.Copilot.Data.Seed;

namespace Platform.Engineering.Copilot.Data.Tests;

/// <summary>
/// Database validation program
/// </summary>
public class DatabaseValidator
{
    public static async Task ValidateAsync(string[] args)
    {
        var host = CreateHost();
        
        using var scope = host.Services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DatabaseValidator>>();
        var context = scope.ServiceProvider.GetRequiredService<EnvironmentManagementContext>();

        try
        {
            logger.LogInformation("🗄️ Starting database validation...");

            // Debug configuration
            var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            var connectionString = config.GetConnectionString("DefaultConnection");
            var databaseProvider = config.GetValue<string>("DatabaseProvider");
            logger.LogInformation("Connection String: {ConnectionString}", connectionString);
            logger.LogInformation("Database Provider: {Provider}", databaseProvider);

            // Test basic connectivity
            var canConnect = await context.Database.CanConnectAsync();
            logger.LogInformation("Database connectivity: {Status}", canConnect ? "✅ Success" : "❌ Failed");

            // Seed the database
            logger.LogInformation("Seeding database with initial data...");
            await DatabaseSeeder.SeedAsync(context);

            // Validate data
            var templateCount = await context.EnvironmentTemplates.CountAsync();
            var patternCount = await context.IntentPatterns.CountAsync();
            
            logger.LogInformation("📊 Database validation results:");
            logger.LogInformation("  • Environment Templates: {Count}", templateCount);
            logger.LogInformation("  • Intent Patterns: {Count}", patternCount);
            
            if (templateCount > 0 && patternCount > 0)
            {
                logger.LogInformation("✅ Database validation successful!");
                
                // Show sample data
                var sampleTemplate = await context.EnvironmentTemplates.FirstAsync();
                logger.LogInformation("Sample Template: {Name} ({Type})", sampleTemplate.Name, sampleTemplate.TemplateType);
            }
            else
            {
                logger.LogWarning("⚠️ Database validation completed but no data found");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Database validation failed");
            Environment.Exit(1);
        }
    }

    private static IHost CreateHost()
    {
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production";
        
        return Host.CreateDefaultBuilder()
            .UseEnvironment(environment)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory())
                      .AddJsonFile("appsettings.json", optional: false)
                      .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true)
                      .AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddEnvironmentManagementData(context.Configuration);
            })
            .Build();
    }
}