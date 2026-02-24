using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Admin.Client.Models;
using Platform.Engineering.Copilot.Admin.Client.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.AdminClient.Services;

public class EnvironmentApiServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly Mock<ILogger<EnvironmentApiService>> _loggerMock = new();

    private (EnvironmentApiService Service, MockHttpMessageHandler Handler) CreateService()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5050/") };
        var service = new EnvironmentApiService(httpClient, _loggerMock.Object);
        return (service, handler);
    }

    [Fact]
    public async Task GetEnvironmentsAsync_ReturnsItemsAndCount()
    {
        var (service, handler) = CreateService();
        var response = new { items = new[] { new EnvironmentDetailDto { Name = "dev-env" } }, totalCount = 1, skip = 0, take = 10 };
        handler.SetResponse(JsonSerializer.Serialize(response, JsonOptions));

        var (items, totalCount) = await service.GetEnvironmentsAsync();

        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetEnvironmentsAsync_WithFilters_IncludesQueryParams()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new { items = Array.Empty<object>(), totalCount = 0 }, JsonOptions));

        await service.GetEnvironmentsAsync(status: "Running", hasDrift: true, skip: 10, take: 25);

        handler.LastRequestUri.Should().Contain("status=Running");
        handler.LastRequestUri.Should().Contain("hasDrift=True");
        handler.LastRequestUri.Should().Contain("skip=10");
        handler.LastRequestUri.Should().Contain("take=25");
    }

    [Fact]
    public async Task GetEnvironmentsAsync_OnError_ReturnsEmpty()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.InternalServerError);

        var (items, totalCount) = await service.GetEnvironmentsAsync();

        items.Should().BeEmpty();
        totalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetEnvironmentAsync_ReturnsEnvironment()
    {
        var (service, handler) = CreateService();
        var id = Guid.NewGuid();
        handler.SetResponse(JsonSerializer.Serialize(new EnvironmentDetailDto { Id = id, Name = "test-env" }, JsonOptions));

        var result = await service.GetEnvironmentAsync(id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("test-env");
    }

    [Fact]
    public async Task GetEnvironmentAsync_NotFound_ReturnsNull()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.NotFound);

        var result = await service.GetEnvironmentAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsSummary()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new EnvironmentSummaryDto { TotalCount = 10, HealthyCount = 8 }, JsonOptions));

        var result = await service.GetSummaryAsync();

        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(10);
        result.HealthyCount.Should().Be(8);
    }

    [Fact]
    public async Task CreateEnvironmentAsync_ReturnsEnvironment()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new EnvironmentDetailDto { Name = "new-env" }, JsonOptions), HttpStatusCode.Created);

        var result = await service.CreateEnvironmentAsync(new CreateEnvironmentRequest
        {
            EnvironmentName = "new-env",
            TemplateId = Guid.NewGuid(),
            ResourceGroup = "rg-test",
            SubscriptionId = "sub-123"
        });

        result.Should().NotBeNull();
        result!.Name.Should().Be("new-env");
    }

    [Fact]
    public async Task ScaleEnvironmentAsync_ReturnsResult()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new ScaleResultDto { Status = "Succeeded" }, JsonOptions));

        var result = await service.ScaleEnvironmentAsync(Guid.NewGuid(), new ScaleEnvironmentRequest { Sku = "Standard_D4s_v3" });

        result.Should().NotBeNull();
        result!.Status.Should().Be("Succeeded");
    }

    [Fact]
    public async Task CloneEnvironmentAsync_ReturnsClone()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new EnvironmentDetailDto { Name = "clone-env" }, JsonOptions), HttpStatusCode.Created);

        var result = await service.CloneEnvironmentAsync(Guid.NewGuid(), new CloneEnvironmentRequest { NewName = "clone-env" });

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteEnvironmentAsync_Success_ReturnsTrue()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.NoContent);

        var result = await service.DeleteEnvironmentAsync(Guid.NewGuid(), "admin");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteEnvironmentAsync_Failure_ReturnsFalse()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.InternalServerError);

        var result = await service.DeleteEnvironmentAsync(Guid.NewGuid(), "admin");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAzureResourcesAsync_ReturnsResult()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new DeleteResourcesResultDto { DeletedCount = 3 }, JsonOptions));

        var result = await service.DeleteAzureResourcesAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.DeletedCount.Should().Be(3);
    }

    [Fact]
    public async Task GetDeletedEnvironmentsAsync_ReturnsItems()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new { items = new[] { new EnvironmentDetailDto { Name = "deleted" } }, totalCount = 1 }, JsonOptions));

        var (items, totalCount) = await service.GetDeletedEnvironmentsAsync();

        items.Should().HaveCount(1);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task PurgeEnvironmentAsync_ReturnsTrue()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.NoContent);

        var result = await service.PurgeEnvironmentAsync(Guid.NewGuid());

        result.Should().BeTrue();
    }

    [Fact]
    public async Task PurgeAllDeletedAsync_ReturnsPurgedCount()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new { purgedCount = 5 }, JsonOptions));

        var result = await service.PurgeAllDeletedAsync();

        result.Should().Be(5);
    }

    [Fact]
    public async Task GetResourcesAsync_ReturnsResources()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new { resources = new[] { new ResourceDto { Name = "res-1" } }, totalCount = 1 }, JsonOptions));

        var (resources, totalCount) = await service.GetResourcesAsync(Guid.NewGuid());

        resources.Should().HaveCount(1);
        totalCount.Should().Be(1);
    }

    [Fact]
    public async Task GetHealthAsync_ReturnsHealth()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new EnvironmentHealthDto { OverallStatus = "Healthy" }, JsonOptions));

        var result = await service.GetHealthAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.OverallStatus.Should().Be("Healthy");
    }

    [Fact]
    public async Task GetActivitiesAsync_ReturnsActivities()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new ActivityListDto
        {
            Activities = new List<ActivityDto> { new() { ActivityType = "Created" } },
            HasMore = false
        }, JsonOptions));

        var result = await service.GetActivitiesAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.Activities.Should().HaveCount(1);
    }

    [Fact]
    public async Task DetectDriftAsync_ReturnsResult()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new DriftDetectionResultDto { TotalDriftCount = 2 }, JsonOptions));

        var result = await service.DetectDriftAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.TotalDriftCount.Should().Be(2);
    }

    [Fact]
    public async Task RemediateDriftAsync_ReturnsResult()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new RemediateDriftResultDto { RemediatedCount = 1 }, JsonOptions));

        var result = await service.RemediateDriftAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.RemediatedCount.Should().Be(1);
    }

    [Fact]
    public async Task ExtendExpirationAsync_ReturnsEnvironment()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new EnvironmentDetailDto { Name = "extended" }, JsonOptions));

        var result = await service.ExtendExpirationAsync(Guid.NewGuid(), new ExtendExpirationRequest { NewExpiresAt = DateTimeOffset.UtcNow.AddDays(30) });

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task ReprovisionEnvironmentAsync_ReturnsEnvironment()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new EnvironmentDetailDto { Name = "reprovisioned" }, JsonOptions));

        var result = await service.ReprovisionEnvironmentAsync(Guid.NewGuid());

        result.Should().NotBeNull();
    }
}
