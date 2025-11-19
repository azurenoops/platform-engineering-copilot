using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Configuration;

namespace Platform.Engineering.Copilot.Core.Services.Azure;

/// <summary>
/// Service to provide Azure credentials based on configuration and HTTP context
/// Supports user token passthrough (On-Behalf-Of) and fallback to default credentials
/// </summary>
public interface IAzureCredentialProvider
{
    /// <summary>
    /// Get Azure credential for the current request
    /// Returns user's credential if token passthrough is enabled and user token is available
    /// Otherwise returns default credential (Managed Identity or Azure CLI)
    /// </summary>
    TokenCredential GetCredential();

    /// <summary>
    /// Get user principal name if available from HTTP context
    /// </summary>
    string? GetUserPrincipal();
}

public class AzureCredentialProvider : IAzureCredentialProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AzureCredentialProvider> _logger;
    private readonly AzureGatewayOptions _gatewayOptions;
    private readonly TokenCredential _defaultCredential;

    public AzureCredentialProvider(
        IHttpContextAccessor httpContextAccessor,
        ILogger<AzureCredentialProvider> logger,
        IOptions<GatewayOptions> gatewayOptions)
    {
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gatewayOptions = gatewayOptions?.Value?.Azure ?? throw new ArgumentNullException(nameof(gatewayOptions));

        // Create default credential (Managed Identity or Azure CLI)
        var credentialOptions = new DefaultAzureCredentialOptions
        {
            ExcludeEnvironmentCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeManagedIdentityCredential = !_gatewayOptions.UseManagedIdentity,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeInteractiveBrowserCredential = true
        };

        // Set authority host for Azure Government
        if (_gatewayOptions.CloudEnvironment?.Equals("AzureGovernment", StringComparison.OrdinalIgnoreCase) == true)
        {
            credentialOptions.AuthorityHost = AzureAuthorityHosts.AzureGovernment;
        }

        _defaultCredential = new DefaultAzureCredential(credentialOptions);

        _logger.LogInformation(
            "Azure credential provider initialized. Cloud: {CloudEnvironment}, ManagedIdentity: {UseManagedIdentity}, UserTokenPassthrough: {EnableUserTokenPassthrough}",
            _gatewayOptions.CloudEnvironment,
            _gatewayOptions.UseManagedIdentity,
            _gatewayOptions.EnableUserTokenPassthrough);
    }

    public TokenCredential GetCredential()
    {
        var httpContext = _httpContextAccessor.HttpContext;

        // If user token passthrough is enabled and we have a user credential, use it
        if (_gatewayOptions.EnableUserTokenPassthrough && 
            httpContext?.Items["AzureCredential"] is TokenCredential userCredential)
        {
            var userPrincipal = httpContext.Items["UserPrincipal"]?.ToString();
            _logger.LogDebug("Using user credential for {UserPrincipal}", userPrincipal);
            return userCredential;
        }

        // Otherwise use default credential
        _logger.LogDebug("Using default Azure credential (Managed Identity or Azure CLI)");
        return _defaultCredential;
    }

    public string? GetUserPrincipal()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Items["UserPrincipal"]?.ToString() 
            ?? httpContext?.Items["UserUpn"]?.ToString()
            ?? httpContext?.User?.Identity?.Name;
    }
}

/// <summary>
/// Extension methods to register Azure credential provider
/// </summary>
public static class AzureCredentialProviderExtensions
{
    public static IServiceCollection AddAzureCredentialProvider(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAzureCredentialProvider, AzureCredentialProvider>();
        return services;
    }
}
