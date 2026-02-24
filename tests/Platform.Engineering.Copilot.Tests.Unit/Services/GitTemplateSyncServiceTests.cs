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

public class GitTemplateSyncServiceTests : IDisposable
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly GitTemplateSyncService _service;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public GitTemplateSyncServiceTests()
    {
        var options = new DbContextOptionsBuilder<PlatformEngineeringCopilotContext>()
            .UseInMemoryDatabase(databaseName: $"GitSyncTestDb_{Guid.NewGuid()}")
            .Options;
        _context = new PlatformEngineeringCopilotContext(options);
        _service = new GitTemplateSyncService(_context, Mock.Of<ILogger<GitTemplateSyncService>>());
    }

    public void Dispose() => _context.Dispose();

    private static JsonElement ToJson(object obj) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj, JsonOpts));

    [Fact]
    public async Task ImportFromGitAsync_CreatesNewTemplate()
    {
        var result = await _service.ImportFromGitAsync(
            "https://github.com/org/infra-templates.git",
            branch: "main",
            filePath: "aks/main.bicep",
            name: "aks-from-git",
            category: "Compute");

        result.Should().NotBeNull();
        result.Name.Should().Be("aks-from-git");
        result.Category.Should().Be("Compute");
        result.Status.Should().Be(TemplateStatus.Draft);
        result.GitAutoSync.Should().BeFalse();
    }

    [Fact]
    public async Task ImportFromGitAsync_WithAutoSync_SetsFlags()
    {
        var result = await _service.ImportFromGitAsync(
            "https://github.com/org/templates.git",
            gitAutoSync: true,
            gitSyncIntervalMinutes: 30);

        result.GitAutoSync.Should().BeTrue();
        result.GitSyncIntervalMinutes.Should().Be(30);
    }

    [Fact]
    public async Task ImportFromGitAsync_WithoutName_ExtractsFromPath()
    {
        var result = await _service.ImportFromGitAsync(
            "https://github.com/org/templates.git",
            filePath: "modules/aks-cluster.bicep");

        result.Name.Should().Be("aks-cluster");
    }

    [Fact]
    public async Task ImportFromGitAsync_WithoutNameOrPath_ExtractsFromUrl()
    {
        var result = await _service.ImportFromGitAsync(
            "https://github.com/org/infra-templates.git");

        result.Name.Should().Be("infra-templates");
    }

    [Fact]
    public async Task SyncAsync_ExistingGitTemplate_UpdatesSha()
    {
        var template = await _service.ImportFromGitAsync("https://github.com/org/repo.git", name: "sync-test");

        var result = await _service.SyncAsync(template.TemplateId);

        result.GitCommitSha.Should().StartWith("sync-");
        result.UpdatedAt.Should().BeOnOrAfter(template.UpdatedAt);
    }

    [Fact]
    public async Task SyncAsync_NonGitTemplate_ThrowsInvalidOperation()
    {
        var template = new ServiceTemplate
        {
            TemplateId = Guid.NewGuid(), Name = "no-git", Version = "1.0.0",
            Category = "Compute", Format = TemplateFormat.Bicep, Status = TemplateStatus.Draft,
            Content = "param x string", ParametersJson = "{}",
            CreatedBy = "test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        _context.ServiceTemplates.Add(template);
        await _context.SaveChangesAsync();

        var act = () => _service.SyncAsync(template.TemplateId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not linked*");
    }

    [Fact]
    public async Task SyncAsync_NonExistingTemplate_ThrowsKeyNotFound()
    {
        var act = () => _service.SyncAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task SyncAllAsync_SyncsOnlyAutoSyncTemplates()
    {
        await _service.ImportFromGitAsync("https://github.com/org/t1.git", name: "auto-sync", gitAutoSync: true);
        await _service.ImportFromGitAsync("https://github.com/org/t2.git", name: "manual-sync", gitAutoSync: false);

        var result = ToJson(await _service.SyncAllAsync());

        result.GetProperty("totalCount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetGitStatusAsync_ReturnsStatus()
    {
        var template = await _service.ImportFromGitAsync("https://github.com/org/repo.git", name: "git-status-test");

        var result = ToJson(await _service.GetGitStatusAsync(template.TemplateId));

        result.GetProperty("isGitLinked").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task ResetParametersAsync_ClearsParameters()
    {
        var template = await _service.ImportFromGitAsync("https://github.com/org/repo.git", name: "reset-test");
        template.ParametersJson = "{\"p1\":\"v1\"}";
        template.ParametersOverridden = true;
        await _context.SaveChangesAsync();

        var result = await _service.ResetParametersAsync(template.TemplateId);

        result.ParametersJson.Should().Be("{}");
        result.ParametersOverridden.Should().BeFalse();
    }
}
