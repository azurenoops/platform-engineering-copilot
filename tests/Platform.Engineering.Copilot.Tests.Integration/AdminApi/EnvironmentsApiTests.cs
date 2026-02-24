using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Platform.Engineering.Copilot.Tests.Integration.AdminApi;

[Collection("AdminApi")]
public class EnvironmentsApiTests
{
    private readonly HttpClient _client;

    public EnvironmentsApiTests(AdminApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> CreatePublishedTemplate()
    {
        var name = $"env-template-{Guid.NewGuid():N}";
        var createResp = await _client.PostAsJsonAsync("/api/templates", new
        {
            name, displayName = name, category = "Compute", version = "1.0.0",
            format = "Bicep", content = "param location string\nresource x 'Microsoft.Compute/virtualMachines@2023-01-01' = {}",
            parametersJson = "{}"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("templateId").GetString()!;

        await _client.PostAsync($"/api/templates/{id}/submit-for-approval", null);
        await _client.PostAsJsonAsync($"/api/templates/{id}/approve", new
        {
            approvalSource = "Manual", approvedBy = "test", comments = "ok"
        });

        return id;
    }

    [Fact]
    public async Task GetEnvironments_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/environments");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateEnvironment_FromPublishedTemplate_ReturnsCreated()
    {
        var templateId = await CreatePublishedTemplate();

        var response = await _client.PostAsJsonAsync("/api/environments", new
        {
            environmentName = $"test-env-{Guid.NewGuid():N}",
            templateId,
            subscriptionId = "sub-001",
            resourceGroup = "rg-test",
            location = "usgovvirginia"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateEnvironment_FromDraftTemplate_Returns400()
    {
        var name = $"draft-tmpl-{Guid.NewGuid():N}";
        var createResp = await _client.PostAsJsonAsync("/api/templates", new
        {
            name, displayName = name, category = "Compute", version = "1.0.0",
            format = "Bicep", content = "param x string\nresource r 'Microsoft.Compute/virtualMachines@2023-01-01' = {}",
            parametersJson = "{}"
        });
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var templateId = created.GetProperty("templateId").GetString()!;

        var response = await _client.PostAsJsonAsync("/api/environments", new
        {
            environmentName = "test-env",
            templateId,
            subscriptionId = "sub-001",
            resourceGroup = "rg-test",
            location = "usgovvirginia"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FullLifecycle_CreateScaleCloneDeletePurge()
    {
        var templateId = await CreatePublishedTemplate();

        // Create
        var createResp = await _client.PostAsJsonAsync("/api/environments", new
        {
            environmentName = $"lifecycle-env-{Guid.NewGuid():N}",
            templateId,
            subscriptionId = "sub-001",
            resourceGroup = "rg-test",
            location = "usgovvirginia"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var envId = created.GetProperty("id").GetString()!;

        // Update status to Running so we can scale
        var patchResp = await _client.PatchAsJsonAsync($"/api/environments/{envId}/status", new
        {
            status = "Running",
            reason = "Deployment complete"
        });
        patchResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Scale
        var scaleResp = await _client.PostAsJsonAsync($"/api/environments/{envId}/scale", new
        {
            nodeCount = 5
        });
        scaleResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Update status back to Running
        await _client.PatchAsJsonAsync($"/api/environments/{envId}/status", new
        {
            status = "Running", reason = "Scale complete"
        });

        // Clone
        var cloneResp = await _client.PostAsJsonAsync($"/api/environments/{envId}/clone", new
        {
            newName = $"cloned-{Guid.NewGuid():N}"
        });
        cloneResp.StatusCode.Should().Be(HttpStatusCode.Created);

        // Delete (soft)
        var deleteResp = await _client.DeleteAsync($"/api/environments/{envId}?deletedBy=test-admin");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Get deleted list
        var deletedResp = await _client.GetAsync("/api/environments/deleted");
        deletedResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Purge
        var purgeResp = await _client.DeleteAsync($"/api/environments/{envId}/purge");
        purgeResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetSummary_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/environments/summary");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetExpiring_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/environments/expiring?withinDays=7");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetEnvironment_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/environments/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
