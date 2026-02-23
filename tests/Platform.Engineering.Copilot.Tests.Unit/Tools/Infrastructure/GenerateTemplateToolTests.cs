using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Infrastructure.Tools;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.Infrastructure;

/// <summary>
/// T106 — Unit tests for GenerateInfrastructureTemplateTool.
/// 3 methods, compliance annotations ≥80% (SC-009), no auth, 30-min TTL.
/// </summary>
public class GenerateTemplateToolTests
{
    private readonly GenerateInfrastructureTemplateTool _tool = new(
        new Mock<ILogger<GenerateInfrastructureTemplateTool>>().Object);

    [Fact]
    public void Name_Returns_GenerateInfrastructureTemplate() =>
        _tool.Name.Should().Be("generate_infrastructure_template");

    [Fact]
    public void RequiresAuthentication_IsFalse() =>
        _tool.RequiresAuthentication.Should().BeFalse();

    [Fact]
    public async Task Execute_StorageAccount_GeneratesTemplate()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("templateId").GetString().Should().NotBeNullOrEmpty();
        data.GetProperty("content").GetString().Should().Contain("Storage");
    }

    [Fact]
    public async Task Execute_StorageAccount_HasAnnotations()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("complianceAnnotations").GetArrayLength().Should().BeGreaterOrEqualTo(4);
    }

    [Fact]
    public async Task Execute_MeetsMinimumCoverage()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "AKS cluster"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("annotationCoverage").GetDouble().Should().BeGreaterOrEqualTo(0.80);
        data.GetProperty("meetsMinimumCoverage").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Execute_HasExpiresAt_Within30Minutes()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account"
        });

        var doc = JsonDocument.Parse(result);
        var expiresStr = doc.RootElement.GetProperty("data").GetProperty("expiresAt").GetString()!;
        var expires = DateTimeOffset.Parse(expiresStr);
        expires.Should().BeAfter(DateTimeOffset.UtcNow);
        expires.Should().BeBefore(DateTimeOffset.UtcNow.AddMinutes(35));
    }

    [Fact]
    public async Task Execute_DefaultMethod_IsTemplateGenerator()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("method").GetString()
            .Should().Be("template-generator");
    }

    [Fact]
    public async Task Execute_BicepFormat_Default()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("format").GetString()
            .Should().Be("bicep");
    }

    [Fact]
    public async Task Execute_TerraformFormat()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account",
            ["format"] = "terraform"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("format").GetString().Should().Be("terraform");
        data.GetProperty("content").GetString().Should().Contain("azurerm");
    }

    [Fact]
    public async Task Execute_AKSCluster_GeneratesTemplate()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "AKS cluster"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("content").GetString()
            .Should().Contain("AKS");
    }

    [Fact]
    public async Task Execute_MissingResourceType_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = ""
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("MISSING_RESOURCE_TYPE");
    }

    [Fact]
    public async Task Execute_InvalidMethod_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account",
            ["method"] = "invalid-method"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_METHOD");
    }

    [Fact]
    public async Task Execute_InvalidFormat_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account",
            ["format"] = "yaml"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_FORMAT");
    }

    [Fact]
    public async Task Execute_CustomRegion_Reflected()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account",
            ["region"] = "usgovarizona"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("region").GetString()
            .Should().Be("usgovarizona");
    }

    [Fact]
    public async Task Execute_StorageAnnotations_MapToNistControls()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account"
        });

        var doc = JsonDocument.Parse(result);
        var annotations = doc.RootElement.GetProperty("data").GetProperty("complianceAnnotations");
        var controlIds = new List<string>();
        foreach (var ann in annotations.EnumerateArray())
        {
            controlIds.Add(ann.GetProperty("controlId").GetString()!);
        }
        controlIds.Should().Contain("SC-8");
        controlIds.Should().Contain("SC-7");
        controlIds.Should().Contain("AC-3");
    }
}
