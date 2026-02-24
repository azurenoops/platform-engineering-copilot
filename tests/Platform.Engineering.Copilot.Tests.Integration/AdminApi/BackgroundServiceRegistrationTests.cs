using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Engineering.Copilot.Core.BackgroundServices;
using Platform.Engineering.Copilot.Core.Data;

namespace Platform.Engineering.Copilot.Tests.Integration.AdminApi;

/// <summary>
/// Verifies that background services are properly registered in the DI container
/// and can be resolved without errors. These tests use a minimal factory that does
/// NOT remove hosted services, validating that the service registration code is correct.
/// </summary>
public class BackgroundServiceRegistrationTests
{
    [Fact]
    public void GitTemplateSyncBackgroundService_IsRegisteredAsHostedService()
    {
        using var factory = new BackgroundServiceRegistrationFactory();
        using var scope = factory.Services.CreateScope();

        var hostedServices = factory.Services.GetServices<IHostedService>();

        hostedServices.Should().Contain(s => s is GitTemplateSyncBackgroundService);
    }

    [Fact]
    public void DeploymentStatusPollingBackgroundService_IsRegisteredAsHostedService()
    {
        using var factory = new BackgroundServiceRegistrationFactory();
        using var scope = factory.Services.CreateScope();

        var hostedServices = factory.Services.GetServices<IHostedService>();

        hostedServices.Should().Contain(s => s is DeploymentStatusPollingBackgroundService);
    }

    [Fact]
    public void SoftDeletePurgeBackgroundService_IsRegisteredAsHostedService()
    {
        using var factory = new BackgroundServiceRegistrationFactory();
        using var scope = factory.Services.CreateScope();

        var hostedServices = factory.Services.GetServices<IHostedService>();

        hostedServices.Should().Contain(s => s is SoftDeletePurgeBackgroundService);
    }
}

/// <summary>
/// Minimal WebApplicationFactory that keeps background services registered (unlike
/// <see cref="AdminApiWebApplicationFactory"/>) so DI registration can be verified.
/// The host is never started, so background services do not run.
/// </summary>
internal sealed class BackgroundServiceRegistrationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace the real database with InMemory so no SQL Server is required
            var dbDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PlatformEngineeringCopilotContext>));
            if (dbDescriptor != null)
                services.Remove(dbDescriptor);

            services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
                options.UseInMemoryDatabase($"BgSvcRegTestDb_{Guid.NewGuid():N}"));
        });
    }
}
