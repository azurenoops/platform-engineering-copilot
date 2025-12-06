using Microsoft.Extensions.DependencyInjection;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;

namespace Platform.Engineering.Copilot.Core.Extensions;

/// <summary>
/// Extension methods for registering Azure client factory services.
/// </summary>
public static class AzureClientFactoryExtensions
{
    /// <summary>
    /// Adds the Azure client factory to the service collection.
    /// This provides centralized credential and client management for all Azure SDK operations.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAzureClientFactory(this IServiceCollection services)
    {
        // Ensure HTTP context accessor is available for user token passthrough
        services.AddHttpContextAccessor();

        // Register the factory as singleton (clients are cached internally)
        services.AddSingleton<IAzureClientFactory, Services.Azure.AzureClientFactory>();

        return services;
    }
}
