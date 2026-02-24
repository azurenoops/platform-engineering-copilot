using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

public class ProvisionedEnvironmentServiceTests : IAsyncDisposable
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ProvisionedEnvironmentService _service;

    public ProvisionedEnvironmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<PlatformEngineeringCopilotContext>()
            .UseInMemoryDatabase(databaseName: $"EnvTestDb_{Guid.NewGuid()}")
            .Options;
        _context = new PlatformEngineeringCopilotContext(options);

        var factory = new DeployerFactory(Mock.Of<ILogger<DeployerFactory>>());
        var activityService = new EnvironmentActivityService(_context, Mock.Of<ILogger<EnvironmentActivityService>>());
        _service = new ProvisionedEnvironmentService(_context, factory, activityService,
            Mock.Of<ILogger<ProvisionedEnvironmentService>>());
    }

    public async ValueTask DisposeAsync() => await _context.DisposeAsync();

    private ServiceTemplate SeedPublishedTemplate(string name = "test-template")
    {
        var template = new ServiceTemplate
        {
            TemplateId = Guid.NewGuid(),
            Name = name,
            Version = "1.0.0",
            Category = "Compute",
            Format = TemplateFormat.Bicep,
            Status = TemplateStatus.Published,
            Content = "param location string",
            ParametersJson = "{}",
            CreatedBy = "test",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.ServiceTemplates.Add(template);
        _context.SaveChanges();
        return template;
    }

    private ProvisionedEnvironment CreateEnv(Guid templateId, string name = "test-env",
        EnvironmentStatus status = EnvironmentStatus.Running) => new()
    {
        Name = name,
        TemplateId = templateId,
        SubscriptionId = "sub-001",
        ResourceGroup = "rg-test",
        Location = "usgovvirginia",
        Status = status,
        RequestedBy = "test-user"
    };

    [Fact]
    public async Task CreateAsync_WithPublishedTemplate_CreatesEnvironment()
    {
        var template = SeedPublishedTemplate();
        var env = CreateEnv(template.TemplateId);

        var result = await _service.CreateAsync(env);

        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.Status.Should().Be(EnvironmentStatus.Provisioning);
        result.DeploymentId.Should().NotBeNullOrEmpty();
        result.TemplateName.Should().Be("test-template");
    }

    [Fact]
    public async Task CreateAsync_WithDraftTemplate_ThrowsInvalidOperation()
    {
        var template = new ServiceTemplate
        {
            TemplateId = Guid.NewGuid(), Name = "draft", Version = "1.0.0", Category = "Compute",
            Format = TemplateFormat.Bicep, Status = TemplateStatus.Draft, Content = "x",
            ParametersJson = "{}", CreatedBy = "test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.ServiceTemplates.Add(template);
        await _context.SaveChangesAsync();

        var env = CreateEnv(template.TemplateId);
        var act = () => _service.CreateAsync(env);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Published*");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsFilteredResults()
    {
        var template = SeedPublishedTemplate();
        await _service.CreateAsync(CreateEnv(template.TemplateId, "env-1"));
        await _service.CreateAsync(CreateEnv(template.TemplateId, "env-2"));

        var (items, totalCount) = await _service.GetAllAsync();

        totalCount.Should().Be(2);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_FilterBySubscription_ReturnsMatching()
    {
        var template = SeedPublishedTemplate();
        await _service.CreateAsync(CreateEnv(template.TemplateId, "env-1"));

        var (items, totalCount) = await _service.GetAllAsync(subscriptionId: "sub-001");

        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingEnv_ReturnsEnv()
    {
        var template = SeedPublishedTemplate();
        var created = await _service.CreateAsync(CreateEnv(template.TemplateId));

        var result = await _service.GetByIdAsync(created.Id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("test-env");
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesEnvironment()
    {
        var template = SeedPublishedTemplate();
        var created = await _service.CreateAsync(CreateEnv(template.TemplateId));

        await _service.DeleteAsync(created.Id, "admin-user");

        var result = await _service.GetByIdAsync(created.Id);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDeletedAsync_ReturnsOnlySoftDeleted()
    {
        var template = SeedPublishedTemplate();
        var e1 = await _service.CreateAsync(CreateEnv(template.TemplateId, "active"));
        var e2 = await _service.CreateAsync(CreateEnv(template.TemplateId, "deleted"));
        await _service.DeleteAsync(e2.Id, "admin");

        var deleted = await _service.GetDeletedAsync();

        deleted.Should().HaveCount(1);
        deleted.First().Name.Should().Be("deleted");
    }

    [Fact]
    public async Task PurgeAsync_PermanentlyRemoves()
    {
        var template = SeedPublishedTemplate();
        var created = await _service.CreateAsync(CreateEnv(template.TemplateId));
        await _service.DeleteAsync(created.Id, "admin");

        await _service.PurgeAsync(created.Id);

        var all = await _context.ProvisionedEnvironments.IgnoreQueryFilters().ToListAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task CloneAsync_CreatesNewEnvironment()
    {
        var template = SeedPublishedTemplate();
        var source = await _service.CreateAsync(CreateEnv(template.TemplateId, "source-env"));

        var clone = await _service.CloneAsync(source.Id, "cloned-env");

        clone.Should().NotBeNull();
        clone.Name.Should().Be("cloned-env");
        clone.Id.Should().NotBe(source.Id);
        clone.TemplateId.Should().Be(source.TemplateId);
        clone.Status.Should().Be(EnvironmentStatus.Provisioning);
    }

    [Fact]
    public async Task ReprovisionAsync_FailedEnv_ChangesToProvisioning()
    {
        var template = SeedPublishedTemplate();
        var env = await _service.CreateAsync(CreateEnv(template.TemplateId));
        env.Status = EnvironmentStatus.Failed;
        await _context.SaveChangesAsync();

        var result = await _service.ReprovisionAsync(env.Id);

        result.Status.Should().Be(EnvironmentStatus.Provisioning);
    }

    [Fact]
    public async Task ReprovisionAsync_RunningEnv_Throws()
    {
        var template = SeedPublishedTemplate();
        var env = await _service.CreateAsync(CreateEnv(template.TemplateId));
        env.Status = EnvironmentStatus.Running;
        await _context.SaveChangesAsync();

        var act = () => _service.ReprovisionAsync(env.Id);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Failed*");
    }

    [Fact]
    public async Task ScaleAsync_RunningEnv_SetsScaling()
    {
        var template = SeedPublishedTemplate();
        var env = await _service.CreateAsync(CreateEnv(template.TemplateId));
        env.Status = EnvironmentStatus.Running;
        await _context.SaveChangesAsync();

        var result = await _service.ScaleAsync(env.Id, nodeCount: 5, replicaCount: null, sku: null, tier: null);

        result.Should().NotBeNull();
        var refreshedEnv = await _service.GetByIdAsync(env.Id);
        refreshedEnv!.Status.Should().Be(EnvironmentStatus.Scaling);
    }

    [Fact]
    public async Task GetExpiringAsync_ReturnsOnlyExpiring()
    {
        var template = SeedPublishedTemplate();
        var e1 = await _service.CreateAsync(CreateEnv(template.TemplateId, "expiring"));
        e1.ExpiresAt = DateTimeOffset.UtcNow.AddDays(3);
        var e2 = await _service.CreateAsync(CreateEnv(template.TemplateId, "not-expiring"));
        e2.ExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
        await _context.SaveChangesAsync();

        var result = await _service.GetExpiringAsync(withinDays: 7);

        result.Should().HaveCount(1);
        result.First().Name.Should().Be("expiring");
    }

    [Fact]
    public async Task ExtendExpirationAsync_UpdatesExpiration()
    {
        var template = SeedPublishedTemplate();
        var env = await _service.CreateAsync(CreateEnv(template.TemplateId));
        var newExpires = DateTimeOffset.UtcNow.AddDays(30);

        var result = await _service.ExtendExpirationAsync(env.Id, newExpires);

        result.ExpiresAt.Should().Be(newExpires);
    }

    [Fact]
    public async Task UpdateStatusAsync_ValidStatus_Updates()
    {
        var template = SeedPublishedTemplate();
        var env = await _service.CreateAsync(CreateEnv(template.TemplateId));

        var result = await _service.UpdateStatusAsync(env.Id, "Running", "Deployment complete");

        result.Status.Should().Be(EnvironmentStatus.Running);
        result.StatusMessage.Should().Be("Deployment complete");
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidStatus_Throws()
    {
        var template = SeedPublishedTemplate();
        var env = await _service.CreateAsync(CreateEnv(template.TemplateId));

        var act = () => _service.UpdateStatusAsync(env.Id, "InvalidStatus");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Invalid status*");
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsAggregatedData()
    {
        var template = SeedPublishedTemplate();
        await _service.CreateAsync(CreateEnv(template.TemplateId, "e1"));
        await _service.CreateAsync(CreateEnv(template.TemplateId, "e2"));

        var result = await _service.GetSummaryAsync();

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task PurgeAllAsync_RemovesAllSoftDeleted()
    {
        var template = SeedPublishedTemplate();
        var e1 = await _service.CreateAsync(CreateEnv(template.TemplateId, "del1"));
        var e2 = await _service.CreateAsync(CreateEnv(template.TemplateId, "del2"));
        await _service.DeleteAsync(e1.Id, "admin");
        await _service.DeleteAsync(e2.Id, "admin");

        var purgedCount = await _service.PurgeAllAsync();

        purgedCount.Should().Be(2);
    }
}
