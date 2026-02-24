using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Admin.Client.Models;
using Platform.Engineering.Copilot.Admin.Client.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.AdminClient.Services;

public class ComplianceApiServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly Mock<ILogger<ComplianceApiService>> _loggerMock = new();

    private (ComplianceApiService Service, MockHttpMessageHandler Handler) CreateService()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5050/") };
        var service = new ComplianceApiService(httpClient, _loggerMock.Object);
        return (service, handler);
    }

    [Fact]
    public async Task GetSummaryAsync_ReturnsSummary()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new ComplianceSummaryDto { OverallScore = 85.0 }, JsonOptions));

        var result = await service.GetSummaryAsync();

        result.Should().NotBeNull();
        result!.OverallScore.Should().Be(85.0);
    }

    [Fact]
    public async Task GetSummaryAsync_OnError_ReturnsNull()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.InternalServerError);

        var result = await service.GetSummaryAsync();

        result.Should().BeNull();
    }

    [Fact]
    public async Task TriggerScanAsync_WithoutEnvironmentId_ReturnsTrue()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("{}", HttpStatusCode.Accepted);

        var result = await service.TriggerScanAsync();

        result.Should().BeTrue();
        handler.LastRequestUri.Should().NotContain("environmentId");
    }

    [Fact]
    public async Task TriggerScanAsync_WithEnvironmentId_IncludesParam()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("{}", HttpStatusCode.Accepted);
        var envId = Guid.NewGuid();

        var result = await service.TriggerScanAsync(envId);

        result.Should().BeTrue();
        handler.LastRequestUri.Should().Contain($"environmentId={envId}");
    }

    [Fact]
    public async Task TriggerScanAsync_OnError_ReturnsFalse()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.InternalServerError);

        var result = await service.TriggerScanAsync();

        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetEnvironmentComplianceAsync_ReturnsCompliance()
    {
        var (service, handler) = CreateService();
        var envId = Guid.NewGuid();
        handler.SetResponse(JsonSerializer.Serialize(new EnvironmentComplianceDto
        {
            EnvironmentId = envId,
            EnvironmentName = "dev-env",
            OverallScore = 92.5,
            FrameworkResults = new List<FrameworkResultDto>
            {
                new() { Framework = "NIST 800-53", Score = 95.0 }
            }
        }, JsonOptions));

        var result = await service.GetEnvironmentComplianceAsync(envId);

        result.Should().NotBeNull();
        result!.OverallScore.Should().Be(92.5);
        result.FrameworkResults.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetEnvironmentComplianceAsync_OnError_ReturnsNull()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.InternalServerError);

        var result = await service.GetEnvironmentComplianceAsync(Guid.NewGuid());

        result.Should().BeNull();
    }
}
