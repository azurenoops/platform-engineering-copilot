using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

public class AzureResourceServiceTests : IDisposable
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly AzureResourceService _service;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AzureResourceServiceTests()
    {
        var options = new DbContextOptionsBuilder<PlatformEngineeringCopilotContext>()
            .UseInMemoryDatabase(databaseName: $"AzureResTestDb_{Guid.NewGuid()}")
            .Options;
        _context = new PlatformEngineeringCopilotContext(options);
        _service = new AzureResourceService(_context, Mock.Of<ILogger<AzureResourceService>>());
    }

    public void Dispose() => _context.Dispose();

    private static JsonElement ToJson(object obj) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj, JsonOpts));

    private ProvisionedEnvironment SeedEnvironment(EnvironmentStatus status = EnvironmentStatus.Running)
    {
        var template = new ServiceTemplate
        {
            TemplateId = Guid.NewGuid(), Name = "test-template", Version = "1.0.0",
            Category = "Compute", Format = TemplateFormat.Bicep, Status = TemplateStatus.Published,
            Content = "resource x", ParametersJson = "{}",
            CreatedBy = "test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.ServiceTemplates.Add(template);

        var env = new ProvisionedEnvironment
        {
            Id = Guid.NewGuid(), Name = "test-env", TemplateId = template.TemplateId,
            SubscriptionId = "sub-001", ResourceGroup = "rg-test", Location = "usgovvirginia",
            Status = status, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.ProvisionedEnvironments.Add(env);
        _context.SaveChanges();
        return env;
    }

    private void SeedResources(Guid environmentId, int count = 3)
    {
        for (int i = 0; i < count; i++)
        {
            _context.DeployedResources.Add(new DeployedResource
            {
                Id = Guid.NewGuid(),
                EnvironmentId = environmentId,
                AzureResourceId = $"/subscriptions/sub/resourceGroups/rg/providers/Microsoft.Compute/vm-{i}",
                Name = $"resource-{i}",
                Type = "Microsoft.Compute/virtualMachines",
                ProvisioningState = "Succeeded"
            });
        }
        _context.SaveChanges();
    }

    [Fact]
    public async Task GetResourcesAsync_ReturnsResources()
    {
        var env = SeedEnvironment();
        SeedResources(env.Id, 3);

        var result = await _service.GetResourcesAsync(env.Id);

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task SyncResourcesAsync_ReturnsResult()
    {
        var env = SeedEnvironment();

        var result = ToJson(await _service.SyncResourcesAsync(env.Id));

        result.GetProperty("status").GetString().Should().Be("Synced");
    }

    [Fact]
    public async Task SyncResourcesAsync_NonExistingEnv_ThrowsKeyNotFound()
    {
        var act = () => _service.SyncResourcesAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task GetResourceHealthAsync_ReturnsHealthStatus()
    {
        var env = SeedEnvironment();
        SeedResources(env.Id);

        var result = ToJson(await _service.GetResourceHealthAsync(env.Id));

        result.GetProperty("overallStatus").GetString().Should().Be("Healthy");
        result.GetProperty("totalResources").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task DetectDriftAsync_NoDrift_ReportsNoDrift()
    {
        var env = SeedEnvironment();

        var result = ToJson(await _service.DetectDriftAsync(env.Id));

        result.GetProperty("hasDrift").GetBoolean().Should().BeFalse();
        result.GetProperty("driftCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task DetectDriftAsync_WithDrift_ReportsDrift()
    {
        var env = SeedEnvironment();
        _context.DriftItems.Add(new DriftItem
        {
            Id = Guid.NewGuid(), EnvironmentId = env.Id,
            ResourceId = "/sub/rg/vm-0", PropertyPath = "sku",
            ExpectedValue = "Standard_D2s_v3", ActualValue = "Standard_D4s_v3",
            Severity = DriftSeverity.Medium, DetectedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = ToJson(await _service.DetectDriftAsync(env.Id));

        result.GetProperty("hasDrift").GetBoolean().Should().BeTrue();
        result.GetProperty("driftCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task RemediateDriftAsync_RemediatesItems()
    {
        var env = SeedEnvironment();
        _context.DriftItems.Add(new DriftItem
        {
            Id = Guid.NewGuid(), EnvironmentId = env.Id,
            ResourceId = "/sub/rg/vm-0", PropertyPath = "sku",
            ExpectedValue = "D2s", ActualValue = "D4s",
            Severity = DriftSeverity.Low, DetectedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = ToJson(await _service.RemediateDriftAsync(env.Id));

        result.GetProperty("remediatedCount").GetInt32().Should().Be(1);
        result.GetProperty("remainingDriftCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task DeleteResourcesAsync_RemovesResources()
    {
        var env = SeedEnvironment();
        SeedResources(env.Id, 2);

        var result = ToJson(await _service.DeleteResourcesAsync(env.Id));

        result.GetProperty("deletedCount").GetInt32().Should().Be(2);

        var remaining = await _context.DeployedResources.Where(r => r.EnvironmentId == env.Id).ToListAsync();
        remaining.Should().BeEmpty();
    }
}
