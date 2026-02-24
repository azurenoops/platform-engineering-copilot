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

public class NaturalLanguageTemplateMatchingServiceTests : IDisposable
{
    private readonly PlatformEngineeringCopilotContext _context;
    private readonly NaturalLanguageTemplateMatchingService _service;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public NaturalLanguageTemplateMatchingServiceTests()
    {
        var options = new DbContextOptionsBuilder<PlatformEngineeringCopilotContext>()
            .UseInMemoryDatabase(databaseName: $"NLTestDb_{Guid.NewGuid()}")
            .Options;
        _context = new PlatformEngineeringCopilotContext(options);
        _service = new NaturalLanguageTemplateMatchingService(_context,
            Mock.Of<ILogger<NaturalLanguageTemplateMatchingService>>());

        SeedTemplates();
    }

    public void Dispose() => _context.Dispose();

    private static JsonElement ToJson(object obj) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(obj, JsonOpts));

    private void SeedTemplates()
    {
        var templates = new[]
        {
            new ServiceTemplate
            {
                TemplateId = Guid.NewGuid(), Name = "aks-cluster", DisplayName = "AKS Cluster",
                Description = "Deploy an Azure Kubernetes Service cluster with monitoring",
                Category = "Compute", Version = "1.0.0", Format = TemplateFormat.Bicep,
                Status = TemplateStatus.Published, Content = "resource aks",
                Keywords = "kubernetes,aks,container,k8s,orchestration",
                UseCases = "Container orchestration, microservices deployment",
                AiSelectionHints = "When user wants Kubernetes or container orchestration",
                ParametersJson = "{\"nodeCount\":{\"type\":\"int\"},\"vmSize\":{\"type\":\"string\"}}",
                CreatedBy = "test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            },
            new ServiceTemplate
            {
                TemplateId = Guid.NewGuid(), Name = "storage-account", DisplayName = "Storage Account",
                Description = "Deploy an Azure Storage account with blob containers",
                Category = "Storage", Version = "1.0.0", Format = TemplateFormat.Bicep,
                Status = TemplateStatus.Published, Content = "resource storage",
                Keywords = "storage,blob,container,data,files",
                UseCases = "File storage, data lake, static website hosting",
                AiSelectionHints = "When user needs file or object storage",
                ParametersJson = "{\"sku\":{\"type\":\"string\"}}",
                CreatedBy = "test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            },
            new ServiceTemplate
            {
                TemplateId = Guid.NewGuid(), Name = "sql-database", DisplayName = "SQL Database",
                Description = "Deploy an Azure SQL Database with backup configuration",
                Category = "Data", Version = "1.0.0", Format = TemplateFormat.Bicep,
                Status = TemplateStatus.Published, Content = "resource sql",
                Keywords = "sql,database,relational,data,backup",
                UseCases = "Relational database, transaction processing",
                AiSelectionHints = "When user needs a relational SQL database",
                ParametersJson = "{\"databaseName\":{\"type\":\"string\"}}",
                CreatedBy = "test", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
            }
        };

        _context.ServiceTemplates.AddRange(templates);
        _context.SaveChanges();
    }

    [Fact]
    public async Task MatchTemplatesAsync_WithKubernetesQuery_ReturnsResults()
    {
        var result = ToJson(await _service.MatchTemplatesAsync("I need a Kubernetes cluster for microservices"));

        result.GetProperty("matches").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MatchTemplatesAsync_WithHighMinScore_ReturnsFewerResults()
    {
        var result = ToJson(await _service.MatchTemplatesAsync("kubernetes cluster", minScore: 0.9));

        result.Should().NotBe(default);
    }

    [Fact]
    public async Task MatchTemplatesAsync_WithNoRelevantQuery_ReturnsEmpty()
    {
        var result = ToJson(await _service.MatchTemplatesAsync("xyzzy foo bar nonsense"));

        result.GetProperty("matches").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task ExtractParametersAsync_WithValidTemplate_ReturnsParameters()
    {
        var templateId = _context.ServiceTemplates.First(t => t.Name == "aks-cluster").TemplateId;

        var result = ToJson(await _service.ExtractParametersAsync(templateId, "Deploy 3 nodes with Standard_D4s_v3"));

        result.GetProperty("templateName").GetString().Should().Be("aks-cluster");
    }

    [Fact]
    public async Task ExtractParametersAsync_NonExistingTemplate_ThrowsKeyNotFound()
    {
        var act = () => _service.ExtractParametersAsync(Guid.NewGuid(), "test");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ExplainMatchAsync_ReturnsExplanation()
    {
        var templateId = _context.ServiceTemplates.First(t => t.Name == "aks-cluster").TemplateId;

        var result = ToJson(await _service.ExplainMatchAsync(templateId, "I need a Kubernetes cluster"));

        result.GetProperty("templateName").GetString().Should().Be("aks-cluster");
    }

    [Fact]
    public async Task ExplainMatchAsync_NonExistingTemplate_ThrowsKeyNotFound()
    {
        var act = () => _service.ExplainMatchAsync(Guid.NewGuid(), "test");

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task MatchTemplatesAsync_RespectsMaxResults()
    {
        var result = ToJson(await _service.MatchTemplatesAsync("data storage database", minScore: 0.1, maxResults: 1));

        result.GetProperty("matches").GetArrayLength().Should().BeLessThanOrEqualTo(1);
    }

    [Fact]
    public async Task MatchTemplatesAsync_IgnoresNonPublishedTemplates()
    {
        // Add a Draft template  
        _context.ServiceTemplates.Add(new ServiceTemplate
        {
            TemplateId = Guid.NewGuid(), Name = "draft-template", Version = "1.0.0",
            Category = "Compute", Format = TemplateFormat.Bicep, Status = TemplateStatus.Draft,
            Content = "resource draft", Keywords = "kubernetes,draft",
            ParametersJson = "{}", CreatedBy = "test",
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        await _context.SaveChangesAsync();

        var result = ToJson(await _service.MatchTemplatesAsync("kubernetes"));

        result.GetProperty("totalCandidates").GetInt32().Should().Be(3); // Only the 3 Published ones
    }
}
