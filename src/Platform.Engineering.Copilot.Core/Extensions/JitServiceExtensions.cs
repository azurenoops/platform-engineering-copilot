using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

/// <summary>
/// Configuration options for Azure PIM service.
/// </summary>
public class AzurePimServiceOptions
{
    /// <summary>
    /// Whether Azure PIM service is enabled.
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// The Azure AD tenant ID.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use managed identity for authentication.
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Client ID for app registration (if not using managed identity).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Client secret for app registration (if not using managed identity).
    /// Not recommended for production - use managed identity or certificate.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Certificate thumbprint for app registration authentication.
    /// </summary>
    public string? CertificateThumbprint { get; set; }

    /// <summary>
    /// Azure cloud environment (AzurePublic, AzureGovernment, AzureChina).
    /// </summary>
    public string CloudEnvironment { get; set; } = "AzureGovernment";

    /// <summary>
    /// Default duration in minutes for role activations.
    /// </summary>
    public int DefaultActivationDurationMinutes { get; set; } = 480; // 8 hours

    /// <summary>
    /// Maximum allowed duration in minutes for role activations.
    /// </summary>
    public int MaxActivationDurationMinutes { get; set; } = 480;

    /// <summary>
    /// Whether to require a ticket number for activations.
    /// </summary>
    public bool RequireTicketNumber { get; set; } = true;

    /// <summary>
    /// List of approved ticket systems.
    /// </summary>
    public List<string> ApprovedTicketSystems { get; set; } = new()
    {
        "ServiceNow",
        "Jira",
        "Remedy",
        "AzureDevOps"
    };

    /// <summary>
    /// Minimum justification length required.
    /// </summary>
    public int MinJustificationLength { get; set; } = 20;

    /// <summary>
    /// Default ports for JIT VM SSH access.
    /// </summary>
    public int DefaultSshPort { get; set; } = 22;

    /// <summary>
    /// Default ports for JIT VM RDP access.
    /// </summary>
    public int DefaultRdpPort { get; set; } = 3389;

    /// <summary>
    /// Default JIT VM access duration in hours.
    /// </summary>
    public int DefaultVmAccessDurationHours { get; set; } = 3;

    /// <summary>
    /// Maximum JIT VM access duration in hours.
    /// </summary>
    public int MaxVmAccessDurationHours { get; set; } = 24;

    /// <summary>
    /// Whether to enable audit logging.
    /// </summary>
    public bool EnableAuditLogging { get; set; } = true;

    /// <summary>
    /// Whether to send notifications on activation.
    /// </summary>
    public bool SendNotifications { get; set; } = true;

    /// <summary>
    /// Email addresses to notify on privileged role activations.
    /// </summary>
    public List<string> NotificationRecipients { get; set; } = new();

    /// <summary>
    /// Roles that should trigger additional scrutiny/notifications.
    /// </summary>
    public List<string> HighPrivilegeRoles { get; set; } = new()
    {
        "Owner",
        "Contributor",
        "User Access Administrator",
        "Global Administrator",
        "Privileged Role Administrator"
    };
}
