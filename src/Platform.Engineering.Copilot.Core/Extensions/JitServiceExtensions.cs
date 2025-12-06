using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Jit;
using Platform.Engineering.Copilot.Core.Services.Jit;

namespace Platform.Engineering.Copilot.Core.Extensions;

/// <summary>
/// Extension methods for registering JIT (Just-In-Time) privilege elevation services.
/// </summary>
public static class JitServiceExtensions
{
    /// <summary>
    /// Adds Azure PIM (Privileged Identity Management) services for JIT privilege elevation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAzurePimServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration options from AzureAd:AzurePim section
        var pimSection = configuration.GetSection("AzureAd:AzurePim");
        services.Configure<AzurePimServiceOptions>(pimSection);

        // Only register the PIM service if enabled
        var enabled = pimSection.GetValue<bool>("Enabled");
        if (enabled)
        {
            services.AddScoped<IAzurePimService, AzurePimService>();
        }
        else
        {
            // Register a disabled/no-op implementation
            services.AddScoped<IAzurePimService, DisabledAzurePimService>();
        }

        return services;
    }

    /// <summary>
    /// Adds Azure PIM services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAzurePimServices(
        this IServiceCollection services,
        Action<AzurePimServiceOptions> configure)
    {
        services.Configure(configure);
        services.AddScoped<IAzurePimService, AzurePimService>();

        return services;
    }

    /// <summary>
    /// Adds all JIT-related services including PIM, VM access, and tracking.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJitServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Add PIM services
        services.AddAzurePimServices(configuration);

        // Additional JIT-related services can be registered here
        // e.g., JIT request tracking service, notification service, etc.

        return services;
    }
}
