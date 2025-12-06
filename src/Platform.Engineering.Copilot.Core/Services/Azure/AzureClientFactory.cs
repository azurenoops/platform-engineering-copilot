using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Graph;
using Platform.Engineering.Copilot.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;

namespace Platform.Engineering.Copilot.Core.Services.Azure;

/// <summary>
/// Factory for creating Azure SDK clients with consistent configuration.
/// Provides centralized credential management, cloud-aware endpoint configuration,
/// and client caching for optimal performance.
/// </summary>
public class AzureClientFactory : IAzureClientFactory
{
    private readonly ILogger<AzureClientFactory> _logger;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly AzureGatewayOptions _options;
    
    private readonly object _armClientLock = new();
    private readonly object _graphClientLock = new();
    
    private TokenCredential? _defaultCredential;
    private ArmClient? _armClient;
    private GraphServiceClient? _graphClient;

    /// <inheritdoc />
    public AzureCloudEnvironment CloudEnvironment { get; }

    /// <inheritdoc />
    public string? TenantId => _options.TenantId;

    /// <inheritdoc />
    public bool UseManagedIdentity => _options.UseManagedIdentity;

    /// <inheritdoc />
    public bool EnableUserTokenPassthrough => _options.EnableUserTokenPassthrough;

    public AzureClientFactory(
        ILogger<AzureClientFactory> logger,
        IOptions<GatewayOptions> options,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value.Azure;

        // Parse cloud environment from configuration
        CloudEnvironment = ParseCloudEnvironment(_options.CloudEnvironment);

        _logger.LogInformation(
            "Azure Client Factory initialized. Cloud: {CloudEnvironment}, TenantId: {TenantId}, ManagedIdentity: {UseManagedIdentity}, UserPassthrough: {EnableUserTokenPassthrough}",
            CloudEnvironment,
            string.IsNullOrEmpty(_options.TenantId) ? "(not set)" : _options.TenantId[..Math.Min(8, _options.TenantId.Length)] + "...",
            _options.UseManagedIdentity,
            _options.EnableUserTokenPassthrough);
    }

    /// <inheritdoc />
    public TokenCredential GetCredential()
    {
        // Check for user token passthrough first
        if (_options.EnableUserTokenPassthrough && _httpContextAccessor?.HttpContext != null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext.Items["AzureCredential"] is TokenCredential userCredential)
            {
                var userPrincipal = httpContext.Items["UserPrincipal"]?.ToString() ?? "unknown";
                _logger.LogDebug("Using user credential for {UserPrincipal}", userPrincipal);
                return userCredential;
            }
        }

        // Return cached or create default credential
        return _defaultCredential ??= CreateDefaultCredential();
    }

    /// <inheritdoc />
    public ArmClient GetArmClient(string? subscriptionId = null)
    {
        if (_armClient != null)
            return _armClient;

        lock (_armClientLock)
        {
            if (_armClient != null)
                return _armClient;

            _logger.LogInformation("Creating ARM client for {CloudEnvironment}...", CloudEnvironment);

            var armOptions = new ArmClientOptions
            {
                Environment = GetArmEnvironment()
            };

            var effectiveSubscriptionId = subscriptionId ?? _options.SubscriptionId;
            
            _armClient = new ArmClient(
                GetCredential(),
                string.IsNullOrEmpty(effectiveSubscriptionId) ? null : effectiveSubscriptionId,
                armOptions);

            _logger.LogInformation("✅ ARM client created successfully for {Environment}", armOptions.Environment);
            return _armClient;
        }
    }

    /// <inheritdoc />
    public GraphServiceClient GetGraphClient()
    {
        if (_graphClient != null)
            return _graphClient;

        lock (_graphClientLock)
        {
            if (_graphClient != null)
                return _graphClient;

            _logger.LogInformation("Creating Graph client for {CloudEnvironment}...", CloudEnvironment);

            var scopes = new[] { GetGraphScope() };
            var baseUrl = GetGraphBaseUrl();

            _graphClient = new GraphServiceClient(GetCredential(), scopes, baseUrl);

            _logger.LogInformation("✅ Graph client created successfully with base URL {BaseUrl}", baseUrl);
            return _graphClient;
        }
    }

    /// <inheritdoc />
    public Uri GetAuthorityHost()
    {
        return CloudEnvironment switch
        {
            AzureCloudEnvironment.AzureGovernment => AzureAuthorityHosts.AzureGovernment,
            AzureCloudEnvironment.AzureChina => AzureAuthorityHosts.AzureChina,
            _ => AzureAuthorityHosts.AzurePublicCloud
        };
    }

    /// <inheritdoc />
    public ArmEnvironment GetArmEnvironment()
    {
        return CloudEnvironment switch
        {
            AzureCloudEnvironment.AzureGovernment => ArmEnvironment.AzureGovernment,
            AzureCloudEnvironment.AzureChina => ArmEnvironment.AzureChina,
            _ => ArmEnvironment.AzurePublicCloud
        };
    }

    /// <inheritdoc />
    public string? GetUserPrincipal()
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        return httpContext?.Items["UserPrincipal"]?.ToString()
            ?? httpContext?.Items["UserUpn"]?.ToString()
            ?? httpContext?.User?.Identity?.Name;
    }

    /// <inheritdoc />
    public string GetGraphBaseUrl()
    {
        return CloudEnvironment switch
        {
            AzureCloudEnvironment.AzureGovernment => "https://graph.microsoft.us/v1.0",
            AzureCloudEnvironment.AzureChina => "https://microsoftgraph.chinacloudapi.cn/v1.0",
            _ => "https://graph.microsoft.com/v1.0"
        };
    }

    /// <inheritdoc />
    public string GetGraphScope()
    {
        return CloudEnvironment switch
        {
            AzureCloudEnvironment.AzureGovernment => "https://graph.microsoft.us/.default",
            AzureCloudEnvironment.AzureChina => "https://microsoftgraph.chinacloudapi.cn/.default",
            _ => "https://graph.microsoft.com/.default"
        };
    }

    /// <inheritdoc />
    public string GetManagementScope()
    {
        return CloudEnvironment switch
        {
            AzureCloudEnvironment.AzureGovernment => "https://management.usgovcloudapi.net/.default",
            AzureCloudEnvironment.AzureChina => "https://management.chinacloudapi.cn/.default",
            _ => "https://management.azure.com/.default"
        };
    }

    #region Private Methods

    private TokenCredential CreateDefaultCredential()
    {
        _logger.LogInformation("Creating default Azure credential for {CloudEnvironment}...", CloudEnvironment);

        var credentialOptions = new DefaultAzureCredentialOptions
        {
            AuthorityHost = GetAuthorityHost(),
            ExcludeEnvironmentCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeManagedIdentityCredential = !_options.UseManagedIdentity,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeInteractiveBrowserCredential = true
        };

        if (!string.IsNullOrEmpty(_options.TenantId))
        {
            credentialOptions.TenantId = _options.TenantId;
        }

        // If service principal credentials are provided, use ClientSecretCredential
        if (!_options.UseManagedIdentity &&
            !string.IsNullOrEmpty(_options.ClientId) &&
            !string.IsNullOrEmpty(_options.ClientSecret) &&
            !string.IsNullOrEmpty(_options.TenantId))
        {
            _logger.LogInformation(
                "🔐 Using Service Principal credential (ClientId: {ClientId}) for {CloudEnvironment}",
                _options.ClientId[..Math.Min(8, _options.ClientId.Length)] + "...",
                CloudEnvironment);

            return new ClientSecretCredential(
                _options.TenantId,
                _options.ClientId,
                _options.ClientSecret,
                new ClientSecretCredentialOptions
                {
                    AuthorityHost = GetAuthorityHost()
                });
        }

        _logger.LogInformation(
            "🔐 Using DefaultAzureCredential (ManagedIdentity: {UseManagedIdentity}) for {CloudEnvironment}",
            _options.UseManagedIdentity,
            CloudEnvironment);

        return new DefaultAzureCredential(credentialOptions);
    }

    private static AzureCloudEnvironment ParseCloudEnvironment(string? cloudEnvironment)
    {
        if (string.IsNullOrEmpty(cloudEnvironment))
            return AzureCloudEnvironment.AzureGovernment; // Default to Government for this project

        return cloudEnvironment.ToLowerInvariant() switch
        {
            "azuregovernment" or "usgovernment" or "government" or "usgov" => AzureCloudEnvironment.AzureGovernment,
            "azurechina" or "china" or "chinacloud" => AzureCloudEnvironment.AzureChina,
            "azurepublic" or "public" or "commercial" or "azurecloud" => AzureCloudEnvironment.AzurePublic,
            _ => AzureCloudEnvironment.AzureGovernment // Default to Government
        };
    }

    #endregion
}
