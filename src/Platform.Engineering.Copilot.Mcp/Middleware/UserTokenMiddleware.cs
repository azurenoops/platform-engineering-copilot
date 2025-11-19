using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Configuration;
using System.Security.Claims;

namespace Platform.Engineering.Copilot.Mcp.Middleware;

/// <summary>
/// Middleware to extract user's Azure AD token from Authorization header
/// and create Azure credentials for On-Behalf-Of (OBO) flow.
/// Enables MCP to perform Azure operations using the user's CAC identity.
/// </summary>
public class UserTokenMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<UserTokenMiddleware> _logger;
    private readonly AzureAdOptions _azureAdOptions;
    private readonly AzureGatewayOptions _azureGatewayOptions;

    public UserTokenMiddleware(
        RequestDelegate next,
        ILogger<UserTokenMiddleware> logger,
        IOptions<AzureAdOptions> azureAdOptions,
        IOptions<GatewayOptions> gatewayOptions)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _azureAdOptions = azureAdOptions?.Value ?? throw new ArgumentNullException(nameof(azureAdOptions));
        _azureGatewayOptions = gatewayOptions?.Value?.Azure ?? throw new ArgumentNullException(nameof(gatewayOptions));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only process if user token passthrough is enabled
        if (!_azureGatewayOptions.EnableUserTokenPassthrough)
        {
            _logger.LogDebug("User token passthrough disabled, using default credentials");
            await _next(context);
            return;
        }

        // Extract JWT token from Authorization header
        var authHeader = context.Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("No Bearer token found in Authorization header");
            await _next(context);
            return;
        }

        try
        {
            var userToken = authHeader.Substring("Bearer ".Length).Trim();

            // Extract user information from claims
            var userPrincipal = context.User.Identity?.Name ?? "Unknown";
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? context.User.FindFirst("oid")?.Value 
                ?? context.User.FindFirst("sub")?.Value;
            var tenantId = context.User.FindFirst("tid")?.Value ?? _azureAdOptions.TenantId;
            var upn = context.User.FindFirst("upn")?.Value ?? context.User.FindFirst(ClaimTypes.Upn)?.Value;

            _logger.LogInformation(
                "Processing request for user: {UserPrincipal} (UPN: {Upn}, ObjectId: {UserId})",
                userPrincipal, upn, userId);

            // Store user information in HttpContext for logging and audit
            context.Items["UserToken"] = userToken;
            context.Items["UserPrincipal"] = userPrincipal;
            context.Items["UserUpn"] = upn;
            context.Items["UserId"] = userId;
            context.Items["TenantId"] = tenantId;

            // Create On-Behalf-Of (OBO) credential to access Azure using user's identity
            // This exchanges the user's token for an Azure Resource Manager token
            if (!string.IsNullOrEmpty(_azureAdOptions.ClientSecret))
            {
                var credential = new OnBehalfOfCredential(
                    tenantId: tenantId,
                    clientId: _azureAdOptions.ClientId,
                    clientSecret: _azureAdOptions.ClientSecret,
                    userAssertion: userToken,
                    options: new OnBehalfOfCredentialOptions
                    {
                        AuthorityHost = new Uri(_azureAdOptions.Instance)
                    }
                );

                context.Items["AzureCredential"] = credential;
                
                _logger.LogDebug("Created On-Behalf-Of credential for user {UserPrincipal}", userPrincipal);
            }
            else
            {
                _logger.LogWarning("ClientSecret not configured - cannot create On-Behalf-Of credential. " +
                    "Azure operations will use default credentials instead of user identity.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user token");
            // Don't fail the request - fall back to default credentials
        }

        await _next(context);
    }
}

/// <summary>
/// Extension methods to register UserTokenMiddleware
/// </summary>
public static class UserTokenMiddlewareExtensions
{
    public static IApplicationBuilder UseUserTokenAuthentication(this IApplicationBuilder app)
    {
        return app.UseMiddleware<UserTokenMiddleware>();
    }
}
