using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Graph;

namespace Platform.Engineering.Copilot.Core.Interfaces.Azure;

/// <summary>
/// Azure cloud environment enumeration.
/// </summary>
public enum AzureCloudEnvironment
{
    /// <summary>
    /// Azure Public Cloud (Commercial).
    /// </summary>
    AzurePublic,

    /// <summary>
    /// Azure Government Cloud (US Gov).
    /// </summary>
    AzureGovernment,

    /// <summary>
    /// Azure China Cloud (21Vianet).
    /// </summary>
    AzureChina
}

/// <summary>
/// Factory interface for creating Azure SDK clients with consistent configuration.
/// Provides centralized credential management and cloud-aware endpoint configuration.
/// </summary>
public interface IAzureClientFactory
{
    /// <summary>
    /// Gets the configured Azure cloud environment.
    /// </summary>
    AzureCloudEnvironment CloudEnvironment { get; }

    /// <summary>
    /// Gets a TokenCredential for Azure authentication.
    /// Supports managed identity, service principal, and user token passthrough.
    /// </summary>
    /// <returns>A configured TokenCredential.</returns>
    TokenCredential GetCredential();

    /// <summary>
    /// Gets or creates an ARM client for Azure Resource Manager operations.
    /// Client is cached for reuse.
    /// </summary>
    /// <param name="subscriptionId">Optional default subscription ID.</param>
    /// <returns>A configured ArmClient.</returns>
    ArmClient GetArmClient(string? subscriptionId = null);

    /// <summary>
    /// Gets or creates a Microsoft Graph client.
    /// Client is cached for reuse.
    /// </summary>
    /// <returns>A configured GraphServiceClient.</returns>
    GraphServiceClient GetGraphClient();

    /// <summary>
    /// Gets the Azure AD authority host URL for the configured cloud environment.
    /// </summary>
    /// <returns>The authority host URI.</returns>
    Uri GetAuthorityHost();

    /// <summary>
    /// Gets the ARM environment for the configured cloud.
    /// </summary>
    /// <returns>The ArmEnvironment.</returns>
    ArmEnvironment GetArmEnvironment();

    /// <summary>
    /// Gets the Microsoft Graph API base URL for the configured cloud environment.
    /// </summary>
    /// <returns>The Graph API base URL.</returns>
    string GetGraphBaseUrl();

    /// <summary>
    /// Gets the Microsoft Graph API scope for the configured cloud environment.
    /// </summary>
    /// <returns>The Graph API scope (e.g., "https://graph.microsoft.us/.default").</returns>
    string GetGraphScope();

    /// <summary>
    /// Gets the Azure Management API scope for the configured cloud environment.
    /// </summary>
    /// <returns>The management API scope.</returns>
    string GetManagementScope();

    /// <summary>
    /// Gets the configured tenant ID.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Gets whether managed identity is being used.
    /// </summary>
    bool UseManagedIdentity { get; }

    /// <summary>
    /// Gets whether user token passthrough is enabled.
    /// </summary>
    bool EnableUserTokenPassthrough { get; }

    /// <summary>
    /// Gets the current user principal name from HTTP context if available.
    /// </summary>
    /// <returns>The user principal name (email/UPN), or null if not available.</returns>
    string? GetUserPrincipal();
}
