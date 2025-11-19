namespace Platform.Engineering.Copilot.Core.Configuration;

/// <summary>
/// Configuration for Azure AD authentication with CAC/PIV support
/// Enables client apps to pass user tokens to MCP for Azure operations
/// </summary>
public class AzureAdOptions
{
    public const string SectionName = "AzureAd";

    /// <summary>
    /// Azure AD instance (e.g., https://login.microsoftonline.us/ for Government)
    /// </summary>
    public string Instance { get; set; } = "https://login.microsoftonline.us/";

    /// <summary>
    /// Azure AD tenant ID
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// MCP server app registration client ID
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// MCP server app registration client secret (for On-Behalf-Of flow)
    /// Should be stored in Azure Key Vault in production
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Expected audience in JWT tokens (e.g., api://platform-engineering-copilot)
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Whether to require MFA in token validation
    /// </summary>
    public bool RequireMfa { get; set; } = true;

    /// <summary>
    /// Whether to require CAC/PIV authentication specifically
    /// </summary>
    public bool RequireCac { get; set; } = true;

    /// <summary>
    /// Valid token issuers (Azure Government uses different endpoints)
    /// </summary>
    public List<string> ValidIssuers { get; set; } = new();

    /// <summary>
    /// Enable user token passthrough to Azure services
    /// When true, MCP uses user's identity for all Azure operations
    /// </summary>
    public bool EnableUserTokenPassthrough { get; set; } = true;

    /// <summary>
    /// Authority URL (computed from Instance + TenantId)
    /// </summary>
    public string Authority => $"{Instance.TrimEnd('/')}/{TenantId}";
}
