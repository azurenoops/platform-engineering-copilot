using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Platform.Engineering.Copilot.Core.Auth;

/// <summary>
/// CAC/PIV authentication handler — validates JWT <c>amr</c> claim for MFA + RSA/smartcard,
/// <c>aud</c>/<c>iss</c> validation for Gov tenant, configurable 8hr timeout.
/// Per FR-008–FR-012 and research.md §3.
/// <para>
/// The handler does NOT cache CAC tokens (FR-016). Certificate details are
/// never written to logs or included in error responses.
/// </para>
/// </summary>
public class CacAuthenticationHandler : AuthenticationHandler<CacAuthenticationOptions>
{
    public const string SchemeName = "CacAuthentication";

    public CacAuthenticationHandler(
        IOptionsMonitor<CacAuthenticationOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
        : base(options, loggerFactory, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Check if CAC authentication is bypassed (dev mode)
        if (!Options.RequireCac)
        {
            Logger.LogDebug("CAC authentication bypassed (RequireCac=false)");
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        // Extract Authorization header
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing Authorization header."));
        }

        var bearerToken = authHeader.ToString();
        if (!bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid authorization scheme. Expected Bearer."));
        }

        // In production, JWT is validated by the JwtBearer middleware upstream.
        // This handler validates CAC-specific claims (amr) on the already-authenticated principal.
        var principal = Context.User;
        if (principal?.Identity == null || !principal.Identity.IsAuthenticated)
        {
            return Task.FromResult(AuthenticateResult.Fail("User is not authenticated."));
        }

        // Validate amr claim — must contain "mfa" AND ("rsa" OR "smartcard")
        var amrClaims = principal.FindAll("amr").Select(c => c.Value).ToList();

        if (!amrClaims.Contains("mfa"))
        {
            // Do NOT log which auth methods were present (FR-016)
            Logger.LogWarning("CAC validation failed: MFA not in amr claims. UserId: {UserId}",
                RedactUserId(principal));
            return Task.FromResult(AuthenticateResult.Fail(
                "CAC authentication requires multi-factor authentication."));
        }

        if (!amrClaims.Contains("rsa") && !amrClaims.Contains("smartcard"))
        {
            Logger.LogWarning("CAC validation failed: No RSA/smartcard in amr claims. UserId: {UserId}",
                RedactUserId(principal));
            return Task.FromResult(AuthenticateResult.Fail(
                "CAC authentication requires RSA or smartcard factor."));
        }

        // Validate audience
        if (!string.IsNullOrEmpty(Options.ExpectedAudience))
        {
            var aud = principal.FindFirst("aud")?.Value;
            if (aud != Options.ExpectedAudience)
            {
                Logger.LogWarning("CAC validation failed: Invalid audience. UserId: {UserId}",
                    RedactUserId(principal));
                return Task.FromResult(AuthenticateResult.Fail("Invalid token audience."));
            }
        }

        // Validate issuer (Gov tenant)
        if (!string.IsNullOrEmpty(Options.ExpectedIssuer))
        {
            var iss = principal.FindFirst("iss")?.Value;
            if (iss != Options.ExpectedIssuer)
            {
                Logger.LogWarning("CAC validation failed: Invalid issuer. UserId: {UserId}",
                    RedactUserId(principal));
                return Task.FromResult(AuthenticateResult.Fail("Invalid token issuer."));
            }
        }

        // Validate token age (configurable timeout, default 8h)
        var iatClaim = principal.FindFirst("iat")?.Value;
        if (iatClaim != null && long.TryParse(iatClaim, out var iat))
        {
            var issuedAt = DateTimeOffset.FromUnixTimeSeconds(iat);
            if (DateTimeOffset.UtcNow - issuedAt > Options.SessionTimeout)
            {
                Logger.LogWarning("CAC validation failed: Session expired. UserId: {UserId}",
                    RedactUserId(principal));
                return Task.FromResult(AuthenticateResult.Fail(
                    "CAC session has expired. Please re-authenticate."));
            }
        }

        Logger.LogDebug("CAC authentication succeeded. UserId: {UserId}", RedactUserId(principal));

        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    /// <summary>
    /// Redact user ID for logs — show only last 4 chars of OID (FR-016).
    /// Never log certificate details, PIM tokens, or full user identifiers.
    /// </summary>
    private static string RedactUserId(ClaimsPrincipal? principal)
    {
        var oid = principal?.FindFirst("oid")?.Value
            ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(oid))
            return "[unknown]";

        return oid.Length > 4 ? $"***{oid[^4..]}" : "***";
    }
}

/// <summary>
/// Configuration options for CAC authentication.
/// </summary>
public class CacAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>Whether CAC authentication is required. Set to false for dev bypass (FR-015).</summary>
    public bool RequireCac { get; set; } = true;

    /// <summary>Expected JWT audience (e.g., "api://platform-engineering-copilot").</summary>
    public string? ExpectedAudience { get; set; }

    /// <summary>Expected JWT issuer (Azure Gov tenant URL).</summary>
    public string? ExpectedIssuer { get; set; }

    /// <summary>Maximum session duration before re-authentication required. Default 8 hours.</summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromHours(8);
}
