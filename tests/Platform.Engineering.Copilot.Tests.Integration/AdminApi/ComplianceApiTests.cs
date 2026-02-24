using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Platform.Engineering.Copilot.Tests.Integration.AdminApi;

[Collection("AdminApi")]
public class ComplianceApiTests
{
    private readonly HttpClient _client;

    public ComplianceApiTests(AdminApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetSummary_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/compliance/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostScan_ReturnsAccepted()
    {
        var response = await _client.PostAsync("/api/compliance/scan", null);

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task GetEnvironmentCompliance_NonExistent_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/compliance/environments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
