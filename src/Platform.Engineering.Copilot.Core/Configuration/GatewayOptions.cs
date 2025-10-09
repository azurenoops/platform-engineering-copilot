namespace Platform.Engineering.Copilot.Core.Configuration;

/// <summary>
/// Configuration options for the gateway
/// </summary>
public class GatewayOptions
{
    public const string SectionName = "Gateway";

    /// <summary>
    /// Azure configuration
    /// </summary>
    public AzureGatewayOptions Azure { get; set; } = new();

    /// <summary>
    /// GitHub configuration
    /// </summary>
    public GitHubGatewayOptions GitHub { get; set; } = new();

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Request timeout in seconds
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 120;
}

/// <summary>
/// Azure gateway configuration
/// </summary>
public class AzureGatewayOptions
{
    /// <summary>
    /// Azure subscription ID
    /// </summary>
    public string? SubscriptionId { get; set; }

    /// <summary>
    /// Azure tenant ID
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Whether to use managed identity
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Azure cloud environment (AzureCloud, AzureGovernment, AzureChina, AzureGermany)
    /// </summary>
    public string? CloudEnvironment { get; set; }

    /// <summary>
    /// Whether Azure integration is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// GitHub gateway configuration
/// </summary>
public class GitHubGatewayOptions
{
    /// <summary>
    /// GitHub personal access token
    /// </summary>
    public string? AccessToken { get; set; }

    /// <summary>
    /// GitHub API base URL
    /// </summary>
    public string ApiBaseUrl { get; set; } = "https://api.github.com";

    /// <summary>
    /// Default organization/user
    /// </summary>
    public string? DefaultOwner { get; set; }

    /// <summary>
    /// Whether GitHub integration is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
}