using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;

namespace Platform.Engineering.Copilot.Tests.Integration.AdminApi;

[Collection("AdminApi")]
public class TemplatesApiTests
{
    private readonly HttpClient _client;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public TemplatesApiTests(AdminApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetTemplates_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/templates");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/templates/categories");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateTemplate_ReturnsCreated()
    {
        var request = new
        {
            name = $"integration-test-{Guid.NewGuid():N}",
            displayName = "Integration Test Template",
            category = "Compute",
            version = "1.0.0",
            format = "Bicep",
            content = "param location string\nresource vm 'Microsoft.Compute/virtualMachines@2023-01-01' = {}",
            parametersJson = "{}"
        };

        var response = await _client.PostAsJsonAsync("/api/templates", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateTemplate_ThenGetById_ReturnsTemplate()
    {
        var name = $"get-by-id-{Guid.NewGuid():N}";
        var createResp = await _client.PostAsJsonAsync("/api/templates", new
        {
            name, displayName = name, category = "Storage", version = "1.0.0",
            format = "Bicep", content = "resource sa 'Microsoft.Storage/storageAccounts@2023-01-01' = {}",
            parametersJson = "{}"
        });
        var location = createResp.Headers.Location!;

        var getResp = await _client.GetAsync(location);

        getResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("name").GetString().Should().Be(name);
    }

    [Fact]
    public async Task FullLifecycle_CreateSubmitApproveDeprecateDelete()
    {
        // Step 1: Create
        var name = $"lifecycle-{Guid.NewGuid():N}";
        var createResp = await _client.PostAsJsonAsync("/api/templates", new
        {
            name, displayName = name, category = "Compute", version = "1.0.0",
            format = "Bicep", content = "param x string\nresource r 'Microsoft.Compute/virtualMachines@2023-01-01' = {}",
            parametersJson = "{}"
        });
        createResp.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("templateId").GetString();

        // Step 2: Submit for approval
        var submitResp = await _client.PostAsync($"/api/templates/{id}/submit-for-approval", null);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 3: Approve
        var approveResp = await _client.PostAsJsonAsync($"/api/templates/{id}/approve", new
        {
            approvalSource = "Manual",
            approvedBy = "test-approver",
            comments = "Approved for integration test"
        });
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var approved = await approveResp.Content.ReadFromJsonAsync<JsonElement>();
        approved.GetProperty("status").GetString().Should().Be("Published");

        // Step 4: Deprecate
        var deprecateResp = await _client.PostAsync($"/api/templates/{id}/deprecate?deprecatedBy=test-admin&reason=Integration+test", null);
        deprecateResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Step 5: Delete (soft-delete)
        var deleteResp = await _client.DeleteAsync($"/api/templates/{id}?deletedBy=test-admin");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Step 6: Verify 404 after delete
        var getResp = await _client.GetAsync($"/api/templates/{id}");
        getResp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetTemplate_NonExistent_Returns404()
    {
        var response = await _client.GetAsync($"/api/templates/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ValidateTemplate_WithValidContent_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/templates/validate", new
        {
            name = "test-validate",
            content = "param location string\nresource x 'Microsoft.Compute/virtualMachines@2023-01-01' = {}",
            format = "Bicep"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PostMatch_ReturnsOk()
    {
        var response = await _client.PostAsJsonAsync("/api/templates/match", new
        {
            description = "I need a Kubernetes cluster",
            minScore = 0.1,
            maxResults = 5
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
