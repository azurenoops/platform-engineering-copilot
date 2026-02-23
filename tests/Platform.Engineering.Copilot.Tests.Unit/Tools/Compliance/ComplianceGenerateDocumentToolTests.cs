using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Compliance.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.Compliance;

/// <summary>
/// T078 — ComplianceGenerateDocumentTool tests:
/// documentType required (SSP/SAR/POAM), no auth, max 5MB with truncation,
/// response envelope per compliance-tools.md.
/// </summary>
public class ComplianceGenerateDocumentToolTests
{
    private readonly ComplianceGenerateDocumentTool _tool;

    public ComplianceGenerateDocumentToolTests()
    {
        var logger = new Mock<ILogger<ComplianceGenerateDocumentTool>>().Object;
        _tool = new ComplianceGenerateDocumentTool(logger);
    }

    [Fact]
    public void Name_IsCorrect()
        => _tool.Name.Should().Be("compliance_generate_document");

    [Fact]
    public void RequiresAuthentication_IsFalse()
        => _tool.RequiresAuthentication.Should().BeFalse();

    [Fact]
    public void PimTierRequired_IsNone()
        => _tool.PimTierRequired.Should().Be(PimTier.None);

    [Theory]
    [InlineData("SSP")]
    [InlineData("SAR")]
    [InlineData("POAM")]
    public async Task Execute_ValidDocumentType_ReturnsSuccess(string docType)
    {
        var parameters = new Dictionary<string, object?> { ["documentType"] = docType };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("documentType").GetString()
            .Should().Be(docType);
    }

    [Fact]
    public async Task Execute_InvalidDocumentType_ReturnsError()
    {
        var parameters = new Dictionary<string, object?> { ["documentType"] = "INVALID" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("DOCUMENT_GENERATION_FAILED");
    }

    [Fact]
    public async Task Execute_SSP_HasCorrectSections()
    {
        var parameters = new Dictionary<string, object?> { ["documentType"] = "SSP" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var sections = doc.RootElement.GetProperty("data").GetProperty("sections")
            .EnumerateArray().Select(s => s.GetString()).ToList();

        sections.Should().Contain("System Description");
        sections.Should().Contain("Security Controls");
        sections.Should().Contain("Implementation Status");
    }

    [Fact]
    public async Task Execute_SAR_HasCorrectSections()
    {
        var parameters = new Dictionary<string, object?> { ["documentType"] = "SAR" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var sections = doc.RootElement.GetProperty("data").GetProperty("sections")
            .EnumerateArray().Select(s => s.GetString()).ToList();

        sections.Should().Contain("Assessment Scope");
        sections.Should().Contain("Findings Summary");
        sections.Should().Contain("Recommendations");
    }

    [Fact]
    public async Task Execute_POAM_HasCorrectSections()
    {
        var parameters = new Dictionary<string, object?> { ["documentType"] = "POAM" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var sections = doc.RootElement.GetProperty("data").GetProperty("sections")
            .EnumerateArray().Select(s => s.GetString()).ToList();

        sections.Should().Contain("Finding Details");
        sections.Should().Contain("Milestones");
        sections.Should().Contain("Completion Dates");
    }

    [Fact]
    public async Task Execute_DefaultSystemName_UsesFallback()
    {
        var parameters = new Dictionary<string, object?> { ["documentType"] = "SSP" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("systemName").GetString()
            .Should().Be("Platform Engineering Copilot");
    }

    [Fact]
    public async Task Execute_CustomSystemName_IsUsed()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["documentType"] = "SSP",
            ["systemName"] = "My IL5 System"
        };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("systemName").GetString()
            .Should().Be("My IL5 System");
    }

    [Fact]
    public async Task Execute_IncludesContentSizeBytes()
    {
        var parameters = new Dictionary<string, object?> { ["documentType"] = "SSP" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("contentSizeBytes").GetInt32()
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Execute_NormalDoc_TruncatedIsFalse()
    {
        var parameters = new Dictionary<string, object?> { ["documentType"] = "SSP" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("truncated").GetBoolean()
            .Should().BeFalse();
    }

    [Fact]
    public async Task Execute_IncludesMarkdownContent()
    {
        var parameters = new Dictionary<string, object?> { ["documentType"] = "SSP" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var content = doc.RootElement.GetProperty("data").GetProperty("content").GetString();
        content.Should().Contain("# System Security Plan");
        content.Should().Contain("## System Description");
    }

    [Fact]
    public async Task Execute_ReturnsResponseEnvelope()
    {
        var parameters = new Dictionary<string, object?> { ["documentType"] = "SSP" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.TryGetProperty("data", out _).Should().BeTrue();
        root.TryGetProperty("metadata", out _).Should().BeTrue();
        root.GetProperty("metadata").GetProperty("toolName").GetString()
            .Should().Be("compliance_generate_document");
    }

    [Fact]
    public async Task Execute_IncludesOwnerAndFramework()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["documentType"] = "SSP",
            ["owner"] = "John Doe",
            ["framework"] = "FedRAMP High"
        };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("owner").GetString()
            .Should().Be("John Doe");
        doc.RootElement.GetProperty("data").GetProperty("framework").GetString()
            .Should().Be("FedRAMP High");
    }

    [Fact]
    public void MaxDocumentSizeBytes_Is5MB()
    {
        ComplianceGenerateDocumentTool.MaxDocumentSizeBytes.Should().Be(5 * 1024 * 1024);
    }
}
