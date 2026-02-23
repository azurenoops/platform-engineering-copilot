using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Platform.Engineering.Copilot.Core.Auth;

/// <summary>
/// Development bypass handler — provides synthetic authentication when
/// both RequireCac and RequirePim are false in configuration.
/// Uses DefaultAzureCredential context instead of CAC/PIV.
/// Per FR-015: Requires ASPNETCORE_ENVIRONMENT=Development AND explicit config flags.
/// </summary>
public class DevBypassHandler : AuthenticationHandler<DevBypassOptions>
{
    public const string SchemeName = "DevBypass";

    private readonly IConfiguration _configuration;

    public DevBypassHandler(
        IOptionsMonitor<DevBypassOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, loggerFactory, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Only allow bypass in Development environment
        var environment = _configuration["ASPNETCORE_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning("Dev bypass attempted outside Development environment");
            return Task.FromResult(AuthenticateResult.Fail(
                "Dev bypass is only available in Development environment."));
        }

        if (Options.RequireCac || Options.RequirePim)
        {
            Logger.LogDebug("Dev bypass not active: RequireCac={RequireCac}, RequirePim={RequirePim}",
                Options.RequireCac, Options.RequirePim);
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        Logger.LogInformation("Dev bypass active: Providing synthetic authentication");

        var claims = new List<Claim>
        {
            new("sub", "dev-user-00000000-0000-0000-0000-000000000000"),
            new("oid", "dev-user-00000000-0000-0000-0000-000000000000"),
            new(ClaimTypes.Name, "Dev User"),
            new(ClaimTypes.Email, "dev@localhost"),
            new("amr", "mfa"),
            new("amr", "rsa"),
            new("aud", Options.SyntheticAudience ?? "api://platform-engineering-copilot"),
            new("iss", Options.SyntheticIssuer ?? "https://login.microsoftonline.us/dev-tenant/v2.0"),
            new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
            // PIM claims — full write access in dev mode
            new(PimAuthorizationHandler.PimTierClaimType, "Write"),
            new(PimAuthorizationHandler.PimActiveClaimType, "true"),
            new(PimAuthorizationHandler.PimJustificationClaimType, "Development bypass")
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>
/// Configuration options for dev bypass authentication.
/// </summary>
public class DevBypassOptions : AuthenticationSchemeOptions
{
    /// <summary>Whether CAC is required. Must be false for bypass to activate.</summary>
    public bool RequireCac { get; set; } = true;

    /// <summary>Whether PIM is required. Must be false for bypass to activate.</summary>
    public bool RequirePim { get; set; } = true;

    /// <summary>Synthetic audience claim value for dev tokens.</summary>
    public string? SyntheticAudience { get; set; }

    /// <summary>Synthetic issuer claim value for dev tokens.</summary>
    public string? SyntheticIssuer { get; set; }
}
