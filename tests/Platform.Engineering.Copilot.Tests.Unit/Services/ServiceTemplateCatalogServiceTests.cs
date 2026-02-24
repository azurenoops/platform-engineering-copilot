using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Data;
using Platform.Engineering.Copilot.Core.Data.Entities;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Services;

public class ServiceTemplateCatalogServiceTests : IDisposable
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly ServiceTemplateCatalogService _service;

    public ServiceTemplateCatalogServiceTests()
    {
        var options = new DbContextOptionsBuilder<PlatformEngineeringCopilotContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;
        _context = new PlatformEngineeringCopilotContext(options);
        var logger = Mock.Of<ILogger<ServiceTemplateCatalogService>>();
        _service = new ServiceTemplateCatalogService(_context, logger);
    }

    public void Dispose() => _context.Dispose();

    private ServiceTemplate CreateTemplate(string name = "test-template", string version = "1.0.0",
        string category = "Compute", TemplateStatus status = TemplateStatus.Draft) => new()
    {
        TemplateId = Guid.NewGuid(),
        Name = name,
        DisplayName = name,
        Description = $"Test template {name}",
        Version = version,
        Category = category,
        Format = TemplateFormat.Bicep,
        Status = status,
        Content = "param location string = 'eastus'",
        ParametersJson = "{}",
        CreatedBy = "test-user",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    [Fact]
    public async Task CreateAsync_WithValidTemplate_ReturnsCreatedTemplate()
    {
        var template = CreateTemplate();
        var result = await _service.CreateAsync(template);

        result.Should().NotBeNull();
        result.TemplateId.Should().NotBeEmpty();
        result.Name.Should().Be("test-template");
        result.Status.Should().Be(TemplateStatus.Draft);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateNameAndVersion_ThrowsInvalidOperation()
    {
        var t1 = CreateTemplate();
        await _service.CreateAsync(t1);

        var t2 = CreateTemplate();

        var act = () => _service.CreateAsync(t2);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsFilteredResults()
    {
        await _service.CreateAsync(CreateTemplate("aks-cluster", category: "Compute"));
        await _service.CreateAsync(CreateTemplate("storage-acct", category: "Storage"));
        await _service.CreateAsync(CreateTemplate("web-app", category: "Compute"));

        var (items, totalCount) = await _service.GetAllAsync(category: "Compute");

        totalCount.Should().Be(2);
        items.Should().HaveCount(2);
        items.Should().AllSatisfy(t => t.Category.Should().Be("Compute"));
    }

    [Fact]
    public async Task GetAllAsync_WithSearchTerm_FiltersResults()
    {
        await _service.CreateAsync(CreateTemplate("aks-cluster"));
        await _service.CreateAsync(CreateTemplate("storage-acct"));

        var (items, totalCount) = await _service.GetAllAsync(search: "aks");

        totalCount.Should().Be(1);
        items.First().Name.Should().Be("aks-cluster");
    }

    [Fact]
    public async Task GetAllAsync_WithPagination_ReturnsCorrectPage()
    {
        for (int i = 0; i < 5; i++)
            await _service.CreateAsync(CreateTemplate($"template-{i}"));

        var (items, totalCount) = await _service.GetAllAsync(skip: 2, take: 2);

        totalCount.Should().Be(5);
        items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingTemplate_ReturnsTemplate()
    {
        var created = await _service.CreateAsync(CreateTemplate());

        var result = await _service.GetByIdAsync(created.TemplateId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("test-template");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingTemplate_ReturnsNull()
    {
        var result = await _service.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByNameAsync_WithoutVersion_ReturnsLatest()
    {
        await _service.CreateAsync(CreateTemplate("my-template", "1.0.0"));
        await _service.CreateAsync(CreateTemplate("my-template", "2.0.0"));

        var result = await _service.GetByNameAsync("my-template");

        result.Should().NotBeNull();
        result!.Name.Should().Be("my-template");
    }

    [Fact]
    public async Task GetByNameAsync_WithVersion_ReturnsSpecificVersion()
    {
        await _service.CreateAsync(CreateTemplate("my-template", "1.0.0"));
        await _service.CreateAsync(CreateTemplate("my-template", "2.0.0"));

        var result = await _service.GetByNameAsync("my-template", "1.0.0");

        result.Should().NotBeNull();
        result!.Version.Should().Be("1.0.0");
    }

    [Fact]
    public async Task UpdateAsync_WithValidChanges_UpdatesTemplate()
    {
        var created = await _service.CreateAsync(CreateTemplate());
        created.Description = "Updated description";

        var result = await _service.UpdateAsync(created);

        result.Description.Should().Be("Updated description");
        result.UpdatedAt.Should().BeAfter(created.CreatedAt);
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletesTemplate()
    {
        var created = await _service.CreateAsync(CreateTemplate());

        await _service.DeleteAsync(created.TemplateId, "admin-user");

        var result = await _service.GetByIdAsync(created.TemplateId);
        result.Should().BeNull(); // Filtered out by global query filter
    }

    [Fact]
    public async Task GetDeletedAsync_ReturnsOnlySoftDeletedTemplates()
    {
        var t1 = await _service.CreateAsync(CreateTemplate("active"));
        var t2 = await _service.CreateAsync(CreateTemplate("deleted"));
        await _service.DeleteAsync(t2.TemplateId, "admin");

        var deleted = await _service.GetDeletedAsync();

        deleted.Should().HaveCount(1);
        deleted.First().Name.Should().Be("deleted");
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsDistinctCategories()
    {
        await _service.CreateAsync(CreateTemplate("t1", category: "Compute"));
        await _service.CreateAsync(CreateTemplate("t2", category: "Storage"));
        await _service.CreateAsync(CreateTemplate("t3", category: "Compute"));

        var categories = await _service.GetCategoriesAsync();

        categories.Should().HaveCount(2);
        categories.Should().Contain("Compute");
        categories.Should().Contain("Storage");
    }

    [Fact]
    public async Task SubmitForApprovalAsync_FromDraft_ChangesToPendingApproval()
    {
        var created = await _service.CreateAsync(CreateTemplate());

        var result = await _service.SubmitForApprovalAsync(created.TemplateId);

        result.Status.Should().Be(TemplateStatus.PendingApproval);
    }

    [Fact]
    public async Task SubmitForApprovalAsync_FromNonDraft_ThrowsInvalidOperation()
    {
        var template = CreateTemplate();
        template.Status = TemplateStatus.Published;
        _context.ServiceTemplates.Add(template);
        await _context.SaveChangesAsync();

        var act = () => _service.SubmitForApprovalAsync(template.TemplateId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ApproveAsync_FromPendingApproval_ChangesToPublished()
    {
        var created = await _service.CreateAsync(CreateTemplate());
        await _service.SubmitForApprovalAsync(created.TemplateId);

        var result = await _service.ApproveAsync(created.TemplateId, "ServiceNow", "admin@mil",
            "Approved", "EXT-001", "https://sn.example.com/EXT-001");

        result.Status.Should().Be(TemplateStatus.Published);
        result.ApprovedBy.Should().Be("admin@mil");
        result.ApprovalSource.Should().Be("ServiceNow");
        result.ApprovalComments.Should().Be("Approved");
        result.ExternalApprovalId.Should().Be("EXT-001");
        result.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeprecateAsync_FromPublished_ChangesToDeprecated()
    {
        var template = CreateTemplate();
        template.Status = TemplateStatus.Published;
        _context.ServiceTemplates.Add(template);
        await _context.SaveChangesAsync();

        var result = await _service.DeprecateAsync(template.TemplateId, "admin@mil", "No longer supported");

        result.Status.Should().Be(TemplateStatus.Deprecated);
        result.DeprecatedBy.Should().Be("admin@mil");
        result.DeprecationReason.Should().Be("No longer supported");
    }

    [Fact]
    public async Task DeprecateAsync_FromNonPublished_ThrowsInvalidOperation()
    {
        var created = await _service.CreateAsync(CreateTemplate());

        var act = () => _service.DeprecateAsync(created.TemplateId, "admin", "reason");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetAllAsync_WithStatusFilter_FiltersCorrectly()
    {
        var t1 = CreateTemplate("published");
        t1.Status = TemplateStatus.Published;
        _context.ServiceTemplates.Add(t1);

        await _service.CreateAsync(CreateTemplate("draft"));
        await _context.SaveChangesAsync();

        var (items, totalCount) = await _service.GetAllAsync(status: "Published");

        totalCount.Should().Be(1);
        items.First().Name.Should().Be("published");
    }
}
