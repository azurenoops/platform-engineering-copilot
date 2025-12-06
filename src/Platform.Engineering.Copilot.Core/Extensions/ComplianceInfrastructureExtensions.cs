using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Services.Logging;

namespace Platform.Engineering.Copilot.Core.Extensions;

/// <summary>
/// Extension methods for registering compliance infrastructure services.
/// </summary>
public static class ComplianceInfrastructureExtensions
{
    /// <summary>
    /// Adds correlation context services for distributed tracing and structured logging.
    /// </summary>
    public static IServiceCollection AddCorrelationContext(this IServiceCollection services)
    {
        services.AddScoped<ICorrelationContext, CorrelationContext>();
        return services;
    }

    /// <summary>
    /// Adds control family configuration from appsettings.
    /// </summary>
    public static IServiceCollection AddControlFamilyConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var config = new ControlFamilyConfiguration();
        configuration.GetSection(ControlFamilyConfiguration.SectionName).Bind(config);
        
        // If no custom configuration, use defaults
        if (config.Families.Count == 0)
        {
            config = new ControlFamilyConfiguration(); // Uses default values
        }

        services.AddSingleton(config);
        return services;
    }

    /// <summary>
    /// Adds all compliance infrastructure services (correlation, configuration).
    /// </summary>
    public static IServiceCollection AddComplianceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCorrelationContext();
        services.AddControlFamilyConfiguration(configuration);
        return services;
    }
}
