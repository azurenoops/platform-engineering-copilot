using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Admin.Client.Models;
using Platform.Engineering.Copilot.Admin.Client.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.AdminClient.Services;

public class TemplateApiServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly Mock<ILogger<TemplateApiService>> _loggerMock = new();

    private (TemplateApiService Service, MockHttpMessageHandler Handler) CreateService()
    {
        var handler = new MockHttpMessageHandler();
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5050/") };
        var service = new TemplateApiService(httpClient, _loggerMock.Object);
        return (service, handler);
    }

    [Fact]
    public async Task GetTemplatesAsync_ReturnsTemplates()
    {
        var (service, handler) = CreateService();
        var templates = new List<TemplateSummaryDto>
        {
            new() { TemplateId = Guid.NewGuid(), Name = "template-1", Status = "Published" }
        };
        handler.SetResponse(JsonSerializer.Serialize(templates, JsonOptions));

        var result = await service.GetTemplatesAsync();

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("template-1");
    }

    [Fact]
    public async Task GetTemplatesAsync_WithFilters_IncludesQueryParams()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("[]");

        await service.GetTemplatesAsync(category: "Compute", status: "Published", search: "vm");

        handler.LastRequestUri.Should().Contain("category=Compute");
        handler.LastRequestUri.Should().Contain("status=Published");
        handler.LastRequestUri.Should().Contain("search=vm");
    }

    [Fact]
    public async Task GetTemplatesAsync_OnError_ReturnsEmptyList()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.InternalServerError);

        var result = await service.GetTemplatesAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTemplateByIdAsync_ReturnsTemplate()
    {
        var (service, handler) = CreateService();
        var id = Guid.NewGuid();
        var template = new TemplateDetailDto { TemplateId = id, Name = "test", Content = "param location string" };
        handler.SetResponse(JsonSerializer.Serialize(template, JsonOptions));

        var result = await service.GetTemplateByIdAsync(id);

        result.Should().NotBeNull();
        result!.Name.Should().Be("test");
    }

    [Fact]
    public async Task GetTemplateByIdAsync_NotFound_ReturnsNull()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.NotFound);

        var result = await service.GetTemplateByIdAsync(Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateTemplateAsync_Success_ReturnsTemplate()
    {
        var (service, handler) = CreateService();
        var template = new TemplateDetailDto { Name = "new-template" };
        handler.SetResponse(JsonSerializer.Serialize(template, JsonOptions), HttpStatusCode.Created);

        var result = await service.CreateTemplateAsync(new CreateTemplateRequest { Name = "new-template", Content = "bicep" });

        result.Should().NotBeNull();
        result!.Name.Should().Be("new-template");
    }

    [Fact]
    public async Task CreateTemplateAsync_ValidationError_ReturnsNull()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("{}", HttpStatusCode.BadRequest);

        var result = await service.CreateTemplateAsync(new CreateTemplateRequest());

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTemplateAsync_WithEtag_SendsIfMatchHeader()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new TemplateDetailDto(), JsonOptions));

        await service.UpdateTemplateAsync(Guid.NewGuid(), new UpdateTemplateRequest(), "etag-123");

        handler.LastRequest!.Headers.Contains("If-Match").Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTemplateAsync_Success_ReturnsTrue()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.NoContent);

        var result = await service.DeleteTemplateAsync(Guid.NewGuid(), "admin");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTemplateAsync_Failure_ReturnsFalse()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.NotFound);

        var result = await service.DeleteTemplateAsync(Guid.NewGuid(), "admin");

        result.Should().BeFalse();
    }

    [Fact]
    public async Task SubmitForApprovalAsync_Success_ReturnsTemplate()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new TemplateDetailDto { Status = "PendingApproval" }, JsonOptions));

        var result = await service.SubmitForApprovalAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.Status.Should().Be("PendingApproval");
    }

    [Fact]
    public async Task ApproveTemplateAsync_Success_ReturnsTemplate()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new TemplateDetailDto { Status = "Published" }, JsonOptions));

        var result = await service.ApproveTemplateAsync(Guid.NewGuid(), new ApprovalRequest { ApprovedBy = "admin" });

        result.Should().NotBeNull();
        result!.Status.Should().Be("Published");
    }

    [Fact]
    public async Task ValidateTemplateAsync_ReturnsResult()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new TemplateValidationResultDto { IsValid = true }, JsonOptions));

        var result = await service.ValidateTemplateAsync(new ValidateTemplateRequest { Content = "bicep" });

        result.Should().NotBeNull();
        result!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ParseBicepParametersAsync_ReturnsParams()
    {
        var (service, handler) = CreateService();
        var parameters = new List<TemplateParameterDto> { new() { Name = "location" } };
        handler.SetResponse(JsonSerializer.Serialize(parameters, JsonOptions));

        var result = await service.ParseBicepParametersAsync(new ParseBicepParametersRequest { BicepContent = "param location string" });

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("location");
    }

    [Fact]
    public async Task ParseBicepParametersAsync_OnError_ReturnsEmptyList()
    {
        var (service, handler) = CreateService();
        handler.SetResponse("", HttpStatusCode.InternalServerError);

        var result = await service.ParseBicepParametersAsync(new ParseBicepParametersRequest());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsCategories()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new List<string> { "Compute", "Networking" }, JsonOptions));

        var result = await service.GetCategoriesAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task ImportFromGitAsync_ReturnsTemplate()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new TemplateDetailDto { Name = "git-template" }, JsonOptions), HttpStatusCode.Created);

        var result = await service.ImportFromGitAsync(new ImportFromGitRequest { GitRepoUrl = "https://github.com/test/repo" });

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task MatchTemplatesAsync_ReturnsMatches()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new TemplateMatchResultDto
        {
            Matches = new List<TemplateMatchDto> { new() { TemplateName = "vm", Score = 0.9 } },
            TotalMatches = 1
        }, JsonOptions));

        var result = await service.MatchTemplatesAsync(new TemplateMatchRequest { Description = "virtual machine" });

        result.Should().NotBeNull();
        result!.Matches.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetGitStatusAsync_ReturnsStatus()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new GitStatusDto { HasChanges = true }, JsonOptions));

        var result = await service.GetGitStatusAsync(Guid.NewGuid());

        result.Should().NotBeNull();
        result!.HasChanges.Should().BeTrue();
    }

    [Fact]
    public async Task DeprecateTemplateAsync_ReturnsTemplate()
    {
        var (service, handler) = CreateService();
        handler.SetResponse(JsonSerializer.Serialize(new TemplateDetailDto { Status = "Deprecated" }, JsonOptions));

        var result = await service.DeprecateTemplateAsync(Guid.NewGuid(), "admin", "obsolete");

        result.Should().NotBeNull();
        result!.Status.Should().Be("Deprecated");
    }
}

/// <summary>Simple mock HTTP handler for unit testing HTTP services.</summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private string _responseContent = "{}";
    private HttpStatusCode _statusCode = HttpStatusCode.OK;

    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestUri => LastRequest?.RequestUri?.ToString();

    public void SetResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseContent = content;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseContent, System.Text.Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
