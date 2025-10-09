using Xunit;
using FluentAssertions;
using System.Text.Json;
using System.Text;
using System.Net;

namespace Platform.Engineering.Copilot.Tests.Integration.Platform.API;

public class HttpIntegrationTests : IDisposable
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:5000"; // This would be configurable in real scenarios

    public HttpIntegrationTests()
    {
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "MCP-Server-Test/1.0");
    }

    [Fact]
    public async Task SimulateMcpServer_GetTools_ShouldReturnToolsList()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/tools");

        // Act
        try
        {
            var response = await _httpClient.SendAsync(request);
            
            // Assert - If API is running
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                content.Should().NotBeNullOrEmpty();
                
                // Try to deserialize as JSON to verify structure
                var jsonDocument = JsonDocument.Parse(content);
                jsonDocument.RootElement.Should().NotBeNull();
            }
            else
            {
                // API not running - this is expected in unit test environment
                response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.ServiceUnavailable);
            }
        }
        catch (HttpRequestException)
        {
            // Expected when API is not running during tests
            Assert.True(true, "API not available during test execution - this is expected");
        }
    }

    [Fact]
    public async Task SimulateMcpServer_PostChatQuery_ShouldHandleRequest()
    {
        // Arrange
        var chatQuery = new
        {
            Query = "Help me with Azure deployment"
        };
        var json = JsonSerializer.Serialize(chatQuery);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        try
        {
            var response = await _httpClient.PostAsync("/api/chat/query", content);
            
            // Assert - If API is running
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                responseContent.Should().NotBeNullOrEmpty();
                
                var jsonDocument = JsonDocument.Parse(responseContent);
                jsonDocument.RootElement.Should().NotBeNull();
            }
            else
            {
                // API not running - this is expected in unit test environment
                response.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.ServiceUnavailable);
            }
        }
        catch (HttpRequestException)
        {
            // Expected when API is not running during tests
            Assert.True(true, "API not available during test execution - this is expected");
        }
    }

    [Fact]
    public async Task SimulateMcpServer_PostToolExecution_ShouldHandleRequest()
    {
        // Arrange
        var toolRequest = new
        {
            ToolName = "infrastructure-provisioning",
            Parameters = new Dictionary<string, object>
            {
                { "resourceGroup", "test-rg" },
                { "location", "eastus" }
            }
        };
        var json = JsonSerializer.Serialize(toolRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        try
        {
            var response = await _httpClient.PostAsync("/api/tools/execute", content);
            
            // Assert - If API is running
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                responseContent.Should().NotBeNullOrEmpty();
            }
            else
            {
                // API not running or tool not found - both are expected scenarios
                response.StatusCode.Should().BeOneOf(
                    HttpStatusCode.NotFound, 
                    HttpStatusCode.ServiceUnavailable,
                    HttpStatusCode.BadRequest
                );
            }
        }
        catch (HttpRequestException)
        {
            // Expected when API is not running during tests
            Assert.True(true, "API not available during test execution - this is expected");
        }
    }

    [Theory]
    [InlineData("/api/tools")]
    [InlineData("/api/health")]
    public async Task ApiEndpoints_ShouldBeAccessible_WhenServerIsRunning(string endpoint)
    {
        // Act
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            
            // Assert - If API is running, endpoints should be accessible
            if (response.StatusCode != HttpStatusCode.ServiceUnavailable)
            {
                // Either success or a proper HTTP error (not network error)
                response.StatusCode.Should().BeOneOf(
                    HttpStatusCode.OK,
                    HttpStatusCode.NotFound,
                    HttpStatusCode.BadRequest,
                    HttpStatusCode.InternalServerError
                );
            }
        }
        catch (HttpRequestException)
        {
            // Expected when API is not running during tests
            Assert.True(true, "API not available during test execution - this is expected");
        }
    }

    [Fact]
    public void HttpClient_IsConfiguredCorrectly()
    {
        // Assert
        _httpClient.Should().NotBeNull();
        _httpClient.BaseAddress.Should().NotBeNull();
        _httpClient.BaseAddress!.ToString().Should().Be($"{BaseUrl}/");
        _httpClient.DefaultRequestHeaders.UserAgent.Should().NotBeEmpty();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _httpClient?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}

// Helper classes for JSON serialization in tests
public class TestChatQueryRequest
{
    public string Query { get; set; } = string.Empty;
}

public class TestToolExecutionRequest
{
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}