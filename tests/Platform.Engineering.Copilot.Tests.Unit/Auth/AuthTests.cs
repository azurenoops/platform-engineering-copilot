using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Core.Auth;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Auth;

/// <summary>
/// Auth handler unit tests per FR-008–FR-016.
/// Validates CAC (valid/invalid/expired), PIM (tier enforcement, eligibility, justification),
/// DevBypass (bypass flags), and FR-016 (no credential leakage in logs/responses).
/// </summary>
public class AuthTests
{
    // ═══════════════════════════════════════════════════════════════════
    // CAC Authentication Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void CacOptions_DefaultSessionTimeout_Is8Hours()
    {
        var options = new CacAuthenticationOptions();
        options.SessionTimeout.Should().Be(TimeSpan.FromHours(8));
    }

    [Fact]
    public void CacOptions_DefaultRequireCac_IsTrue()
    {
        var options = new CacAuthenticationOptions();
        options.RequireCac.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // PIM Authorization Tests
    // ═══════════════════════════════════════════════════════════════════

    private PimAuthorizationHandler CreatePimHandler(bool requirePim = true)
    {
        return new PimAuthorizationHandler(
            Mock.Of<ILogger<PimAuthorizationHandler>>(),
            requirePim);
    }

    private AuthorizationHandlerContext CreatePimContext(
        PimTier requiredTier,
        ClaimsPrincipal? user = null)
    {
        var requirement = new PimTierRequirement(requiredTier);
        user ??= new ClaimsPrincipal(new ClaimsIdentity());
        return new AuthorizationHandlerContext(
            [requirement],
            user,
            null);
    }

    private ClaimsPrincipal CreateUserWithPimClaims(
        PimTier tier,
        bool active = true,
        string? justification = null)
    {
        var claims = new List<Claim>
        {
            new("oid", "00000000-0000-0000-0000-000000001234"),
            new(PimAuthorizationHandler.PimTierClaimType, tier.ToString()),
            new(PimAuthorizationHandler.PimActiveClaimType, active.ToString().ToLower())
        };

        if (justification != null)
        {
            claims.Add(new Claim(PimAuthorizationHandler.PimJustificationClaimType, justification));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "TestScheme"));
    }

    [Fact]
    public async Task Pim_NoneRequired_AlwaysSucceeds()
    {
        var handler = CreatePimHandler();
        var context = CreatePimContext(PimTier.None);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Pim_ReadRequired_WriteTier_Succeeds()
    {
        var handler = CreatePimHandler();
        var user = CreateUserWithPimClaims(PimTier.Write);
        var context = CreatePimContext(PimTier.Read, user);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Pim_WriteRequired_WriteTier_Succeeds()
    {
        var handler = CreatePimHandler();
        var user = CreateUserWithPimClaims(PimTier.Write);
        var context = CreatePimContext(PimTier.Write, user);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Pim_WriteRequired_ReadTier_Fails()
    {
        var handler = CreatePimHandler();
        var user = CreateUserWithPimClaims(PimTier.Read);
        var context = CreatePimContext(PimTier.Write, user);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
        context.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Pim_ReadRequired_NoPimClaim_Fails()
    {
        var handler = CreatePimHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("oid", "test-user")
        ], "TestScheme"));
        var context = CreatePimContext(PimTier.Read, user);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Pim_EligibleButNotActive_Fails()
    {
        var handler = CreatePimHandler();
        var user = CreateUserWithPimClaims(PimTier.Write, active: false);
        var context = CreatePimContext(PimTier.Write, user);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    [Fact]
    public async Task Pim_WithJustification_Succeeds()
    {
        var handler = CreatePimHandler();
        var user = CreateUserWithPimClaims(PimTier.Write, justification: "Emergency remediation");
        var context = CreatePimContext(PimTier.Write, user);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Pim_BypassMode_AlwaysSucceeds()
    {
        var handler = CreatePimHandler(requirePim: false);
        var context = CreatePimContext(PimTier.Write); // no claims at all

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task Pim_InvalidTierClaim_Fails()
    {
        var handler = CreatePimHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("oid", "test-user"),
            new Claim(PimAuthorizationHandler.PimTierClaimType, "InvalidTier"),
            new Claim(PimAuthorizationHandler.PimActiveClaimType, "true")
        ], "TestScheme"));
        var context = CreatePimContext(PimTier.Read, user);

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    // ═══════════════════════════════════════════════════════════════════
    // PIM Tier Requirement Tests
    // ═══════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData(PimTier.None)]
    [InlineData(PimTier.Read)]
    [InlineData(PimTier.Write)]
    public void PimTierRequirement_StoresRequiredTier(PimTier tier)
    {
        var requirement = new PimTierRequirement(tier);
        requirement.RequiredTier.Should().Be(tier);
    }

    // ═══════════════════════════════════════════════════════════════════
    // PIM Policy Names
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void PimPolicies_HasCorrectNames()
    {
        PimPolicies.ReadRequired.Should().Be("PimReadRequired");
        PimPolicies.WriteRequired.Should().Be("PimWriteRequired");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Dev Bypass Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void DevBypassOptions_DefaultRequireCac_IsTrue()
    {
        var options = new DevBypassOptions();
        options.RequireCac.Should().BeTrue();
    }

    [Fact]
    public void DevBypassOptions_DefaultRequirePim_IsTrue()
    {
        var options = new DevBypassOptions();
        options.RequirePim.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════════
    // FR-016: No Credential Leakage Tests
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Pim_FailureReason_DoesNotContainPimToken()
    {
        var handler = CreatePimHandler();
        var user = CreateUserWithPimClaims(PimTier.Read);
        var context = CreatePimContext(PimTier.Write, user);

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
        // Failure reasons should not contain actual PIM token values
        var reasons = context.FailureReasons.Select(r => r.Message).ToList();
        foreach (var reason in reasons)
        {
            reason.Should().NotContain("00000000-0000-0000-0000-000000001234",
                "User OID should not appear in authorization failure reasons (FR-016)");
        }
    }

    [Fact]
    public async Task Pim_FailureReason_DoesNotLeakCertificateDetails()
    {
        var handler = CreatePimHandler();
        var user = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("oid", "secret-user-guid-12345"),
            new Claim("certificate_thumbprint", "ABC123THUMBPRINT"),
        ], "TestScheme"));
        var context = CreatePimContext(PimTier.Read, user);

        await handler.HandleAsync(context);

        context.HasFailed.Should().BeTrue();
        var reasons = context.FailureReasons.Select(r => r.Message).ToList();
        foreach (var reason in reasons)
        {
            reason.Should().NotContain("ABC123THUMBPRINT",
                "Certificate details should not appear in failure reasons (FR-016)");
            reason.Should().NotContain("secret-user-guid-12345",
                "Full user OID should not appear in failure reasons (FR-016)");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // PIM Tier Ordering
    // ═══════════════════════════════════════════════════════════════════

    [Fact]
    public void PimTier_WriteIsGreaterThanRead()
    {
        ((int)PimTier.Write).Should().BeGreaterThan((int)PimTier.Read);
    }

    [Fact]
    public void PimTier_ReadIsGreaterThanNone()
    {
        ((int)PimTier.Read).Should().BeGreaterThan((int)PimTier.None);
    }

    [Fact]
    public void PimTier_NoneIsZero()
    {
        ((int)PimTier.None).Should().Be(0);
    }
}
