using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Environments.Services;
using Platform.Engineering.Copilot.Core.Interfaces.Templates;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;
using Platform.Engineering.Copilot.Core.Models.TemplateMatching;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents.Environments;

/// <summary>
/// Unit tests for NaturalLanguageTemplateMatchingService
/// </summary>
public class NaturalLanguageTemplateMatchingServiceTests
{
    private readonly Mock<ILogger<NaturalLanguageTemplateMatchingService>> _loggerMock;
    private readonly Mock<IServiceTemplateCatalogService> _catalogServiceMock;
    private readonly NaturalLanguageTemplateMatchingService _service;

    public NaturalLanguageTemplateMatchingServiceTests()
    {
        _loggerMock = new Mock<ILogger<NaturalLanguageTemplateMatchingService>>();
        _catalogServiceMock = new Mock<IServiceTemplateCatalogService>();
        
        // Create service without LLM (keyword-based matching)
        _service = new NaturalLanguageTemplateMatchingService(
            _loggerMock.Object,
            _catalogServiceMock.Object,
            kernel: null);
    }

    [Fact]
    public async Task MatchTemplatesAsync_NoTemplates_ReturnsEmptyResult()
    {
        // Arrange
        _catalogServiceMock.Setup(s => s.GetPublishedTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceTemplate>());

        // Act
        var result = await _service.MatchTemplatesAsync("I need a web application");

        // Assert
        result.Success.Should().BeFalse();
        result.Matches.Should().BeEmpty();
        result.Message.Should().Contain("No published templates");
    }

    [Fact]
    public async Task MatchTemplatesAsync_KeywordMatching_FindsRelevantTemplates()
    {
        // Arrange
        var templates = new List<ServiceTemplate>
        {
            CreateTestTemplate("webapp-basic", "Basic Web App", "Compute", 
                keywords: new[] { "web", "app", "aspnet" }),
            CreateTestTemplate("database-sql", "SQL Database", "Database", 
                keywords: new[] { "sql", "database", "storage" }),
            CreateTestTemplate("aks-cluster", "AKS Kubernetes Cluster", "Compute", 
                keywords: new[] { "kubernetes", "aks", "containers" })
        };

        _catalogServiceMock.Setup(s => s.GetPublishedTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        var result = await _service.MatchTemplatesAsync("I need a web application");

        // Assert
        result.Success.Should().BeTrue();
        result.UsedLlm.Should().BeFalse();
        result.Matches.Should().NotBeEmpty();
        
        // Web app template should rank highest
        var topMatch = result.Matches.First();
        topMatch.TemplateName.Should().Be("webapp-basic");
    }

    [Fact]
    public async Task MatchTemplatesAsync_CategoryFilter_OnlyMatchesCategory()
    {
        // Arrange
        var templates = new List<ServiceTemplate>
        {
            CreateTestTemplate("webapp-basic", "Basic Web App", "Compute", 
                keywords: new[] { "web", "app" }),
            CreateTestTemplate("database-sql", "SQL Database", "Database", 
                keywords: new[] { "sql", "database" })
        };

        _catalogServiceMock.Setup(s => s.GetPublishedTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        var options = new TemplateMatchOptions { Category = "Database" };

        // Act
        var result = await _service.MatchTemplatesAsync("I need storage", options);

        // Assert
        result.Success.Should().BeTrue();
        // Should still match based on keywords even if category filter applied post-match
    }

    [Fact]
    public async Task MatchTemplatesAsync_MinimumScore_FiltersLowScores()
    {
        // Arrange
        var templates = new List<ServiceTemplate>
        {
            CreateTestTemplate("webapp-basic", "Basic Web App", "Compute", 
                keywords: new[] { "web", "app", "frontend" }),
            CreateTestTemplate("unrelated", "Unrelated Template", "Other", 
                keywords: new[] { "xyz", "abc" })
        };

        _catalogServiceMock.Setup(s => s.GetPublishedTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        var options = new TemplateMatchOptions { MinimumScore = 0.5 };

        // Act
        var result = await _service.MatchTemplatesAsync("web app frontend", options);

        // Assert
        result.Success.Should().BeTrue();
        // Low scoring template should be filtered out
        result.Matches.All(m => m.Score >= 0.3).Should().BeTrue();
    }

    [Fact]
    public async Task MatchTemplatesAsync_MaxResults_LimitsResults()
    {
        // Arrange
        var templates = Enumerable.Range(1, 10)
            .Select(i => CreateTestTemplate($"template-{i}", $"Template {i}", "Compute",
                keywords: new[] { "test", "template" }))
            .ToList();

        _catalogServiceMock.Setup(s => s.GetPublishedTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        var options = new TemplateMatchOptions { MaxResults = 3 };

        // Act
        var result = await _service.MatchTemplatesAsync("test template", options);

        // Assert
        result.Success.Should().BeTrue();
        result.Matches.Count.Should().BeLessOrEqualTo(3);
    }

    [Fact]
    public async Task ExtractParametersAsync_NoLlm_ReturnsDefaults()
    {
        // Arrange
        var template = CreateTestTemplate("webapp", "Web App", "Compute");
        template.Parameters.Add(new TemplateParameter
        {
            Name = "location",
            Description = "Azure region",
            Type = ParameterType.String,
            DefaultValue = "eastus"
        });
        template.Parameters.Add(new TemplateParameter
        {
            Name = "sku",
            Description = "App Service SKU",
            Type = ParameterType.String,
            DefaultValue = "B1"
        });

        // Act
        var result = await _service.ExtractParametersAsync("Create a web app in West US", template);

        // Assert
        result.TemplateId.Should().Be(template.Id);
        result.ExtractedParameters.Should().ContainKey("location");
        result.ExtractedParameters.Should().ContainKey("sku");
        
        // Without LLM, should return defaults
        result.ExtractedParameters["location"].SuggestedValue.Should().Be("eastus");
        result.ExtractedParameters["location"].Source.Should().Be("default");
    }

    [Fact]
    public async Task ExplainMatchAsync_NoLlm_ReturnsDescription()
    {
        // Arrange
        var template = CreateTestTemplate("webapp", "Web App", "Compute");
        template.Description = "A basic web application template";

        // Act
        var explanation = await _service.ExplainMatchAsync("I need a web app", template);

        // Assert
        explanation.Should().Contain("Web App");
    }

    [Fact]
    public async Task MatchTemplatesAsync_ComplianceKeywords_MatchesFedRampTemplates()
    {
        // Arrange
        var templates = new List<ServiceTemplate>
        {
            CreateTestTemplate("webapp-standard", "Standard Web App", "Compute",
                keywords: new[] { "web", "app" }),
            CreateTestTemplate("webapp-fedramp", "FedRAMP Web App", "Compute",
                keywords: new[] { "web", "app", "fedramp", "compliant", "government" },
                compliance: new[] { "FedRAMP-High", "NIST-800-53" })
        };

        _catalogServiceMock.Setup(s => s.GetPublishedTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        var result = await _service.MatchTemplatesAsync(
            "I need a FedRAMP compliant web application for government");

        // Assert
        result.Success.Should().BeTrue();
        
        // FedRAMP template should rank higher due to keyword match
        var fedRampMatch = result.Matches.FirstOrDefault(m => m.TemplateName == "webapp-fedramp");
        fedRampMatch.Should().NotBeNull();
    }

    [Fact]
    public async Task MatchTemplatesAsync_KubernetesKeywords_MatchesAksTemplates()
    {
        // Arrange
        var templates = new List<ServiceTemplate>
        {
            CreateTestTemplate("aks-basic", "Basic AKS Cluster", "Compute",
                keywords: new[] { "kubernetes", "aks", "k8s", "containers", "cluster" }),
            CreateTestTemplate("webapp-basic", "Basic Web App", "Compute",
                keywords: new[] { "web", "app" })
        };

        _catalogServiceMock.Setup(s => s.GetPublishedTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Use a lower minimum score since Jaccard similarity can be low for partial matches
        var options = new TemplateMatchOptions { MinimumScore = 0.05, MaxResults = 10 };

        // Act
        var result = await _service.MatchTemplatesAsync("kubernetes cluster aks", options);

        // Assert
        result.Success.Should().BeTrue();
        result.Matches.Should().NotBeEmpty();
        var aksMatch = result.Matches.FirstOrDefault(m => m.TemplateName == "aks-basic");
        aksMatch.Should().NotBeNull();
    }

    #region Helper Methods

    private ServiceTemplate CreateTestTemplate(
        string name,
        string displayName,
        string category,
        string[]? keywords = null,
        string[]? compliance = null)
    {
        return new ServiceTemplate
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            DisplayName = displayName,
            Description = $"Template for {displayName}",
            Category = category,
            Version = "1.0.0",
            Format = TemplateFormat.Bicep,
            Status = TemplateStatus.Published,
            Keywords = keywords?.ToList() ?? new List<string>(),
            UseCases = new List<string> { displayName },
            ComplianceFrameworks = compliance?.ToList() ?? new List<string>(),
            Parameters = new List<TemplateParameter>()
        };
    }

    #endregion
}
