using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Data;

namespace Platform.Engineering.Copilot.Tests.Integration.AdminApi;

/// <summary>
/// Collection definition so all Admin API test classes share one factory (avoids
/// concurrent HostFactoryResolver listeners that cause "entry point exited" errors).
/// </summary>
[CollectionDefinition("AdminApi")]
public class AdminApiCollection : ICollectionFixture<AdminApiWebApplicationFactory> { }

/// <summary>
/// Shared test fixture for Admin API integration tests.
/// Uses InMemory EF Core and a test auth handler with Admin role.
/// </summary>
public class AdminApiWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"AdminApiTestDb_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext descriptor so InMemory replaces SqlServer
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PlatformEngineeringCopilotContext>));
            if (dbDescriptor != null)
                services.Remove(dbDescriptor);

            services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            // Remove background services — they interfere with test host lifecycle
            var hostedServiceDescriptors = services
                .Where(d => d.ServiceType == typeof(IHostedService))
                .ToList();
            foreach (var d in hostedServiceDescriptors)
                services.Remove(d);

            // Add test authentication scheme
            services.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", _ => { });

            // Override default auth scheme to Test (runs after app's Configure calls)
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = "Test";
                options.DefaultChallengeScheme = "Test";
                options.DefaultScheme = "Test";
            });

            // Override authorization policies to accept Test scheme
            services.PostConfigure<AuthorizationOptions>(options =>
            {
                options.DefaultPolicy = new AuthorizationPolicyBuilder("Test")
                    .RequireAuthenticatedUser()
                    .Build();
                options.AddPolicy("Admin", new AuthorizationPolicyBuilder("Test")
                    .RequireAuthenticatedUser()
                    .RequireRole("Admin")
                    .Build());
                options.AddPolicy("Engineer", new AuthorizationPolicyBuilder("Test")
                    .RequireAuthenticatedUser()
                    .RequireRole("Admin", "Engineer")
                    .Build());
            });
        });
    }
}

/// <summary>
/// Test authentication handler that always returns an authenticated Admin user.
/// </summary>
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "test-admin"),
            new Claim(ClaimTypes.NameIdentifier, "test-admin-id"),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(ClaimTypes.Role, "Engineer"),
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, "Test");
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
