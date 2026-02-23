using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.KnowledgeBase;

/// <summary>
/// T095 — Unit tests for CompareFrameworksTool.
/// Verifies: INistService.CompareFrameworks, shared/unique controls, no auth.
/// </summary>
public class CompareFrameworksToolTests
{
    private readonly Mock<INistService> _nistServiceMock = new();
    private readonly CompareFrameworksTool _tool;

    public CompareFrameworksToolTests()
    {
        _tool = new CompareFrameworksTool(
            _nistServiceMock.Object,
            new Mock<ILogger<CompareFrameworksTool>>().Object);
    }

    private static FrameworkComparisonResult MakeComparison() => new()
    {
        FrameworkA = ComplianceFramework.FedRampHigh,
        FrameworkB = ComplianceFramework.FedRampModerate,
        Common = new List<ControlDefinition>
        {
            new() { ControlId = "AC-2", Title = "Account Management" },
            new() { ControlId = "AU-2", Title = "Event Logging" }
        },
        UniqueToA = new List<ControlDefinition>
        {
            new() { ControlId = "SC-8", Title = "Transmission Confidentiality" }
        },
        UniqueToB = new List<ControlDefinition>()
    };

    [Fact]
    public void Name_Returns_CompareFrameworks()
    {
        _tool.Name.Should().Be("compare_frameworks");
    }

    [Fact]
    public void RequiresAuthentication_IsFalse()
    {
        _tool.RequiresAuthentication.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_ValidFrameworks_ReturnsComparison()
    {
        _nistServiceMock.Setup(n => n.CompareFrameworks(
            ComplianceFramework.FedRampHigh, ComplianceFramework.FedRampModerate))
            .Returns(MakeComparison());

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["frameworkA"] = "FedRampHigh",
            ["frameworkB"] = "FedRampModerate"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");

        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("commonCount").GetInt32().Should().Be(2);
        data.GetProperty("uniqueToACount").GetInt32().Should().Be(1);
        data.GetProperty("uniqueToBCount").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Execute_ValidFrameworks_ReturnsControlDetails()
    {
        _nistServiceMock.Setup(n => n.CompareFrameworks(
            ComplianceFramework.FedRampHigh, ComplianceFramework.FedRampModerate))
            .Returns(MakeComparison());

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["frameworkA"] = "FedRampHigh",
            ["frameworkB"] = "FedRampModerate"
        });

        var doc = JsonDocument.Parse(result);
        var common = doc.RootElement.GetProperty("data").GetProperty("common");
        common.GetArrayLength().Should().Be(2);
        common[0].GetProperty("controlId").GetString().Should().Be("AC-2");
    }

    [Fact]
    public async Task Execute_InvalidFrameworkA_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["frameworkA"] = "InvalidFramework",
            ["frameworkB"] = "FedRampModerate"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_FRAMEWORK");
    }

    [Fact]
    public async Task Execute_InvalidFrameworkB_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["frameworkA"] = "FedRampHigh",
            ["frameworkB"] = "BadFramework"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_FRAMEWORK");
    }

    [Fact]
    public async Task Execute_ReturnsTotalCounts()
    {
        _nistServiceMock.Setup(n => n.CompareFrameworks(
            ComplianceFramework.FedRampHigh, ComplianceFramework.FedRampModerate))
            .Returns(MakeComparison());

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["frameworkA"] = "FedRampHigh",
            ["frameworkB"] = "FedRampModerate"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("totalA").GetInt32().Should().Be(3); // 2 common + 1 unique
        data.GetProperty("totalB").GetInt32().Should().Be(2); // 2 common + 0 unique
    }

    [Fact]
    public async Task Execute_ReturnsFrameworkNames()
    {
        _nistServiceMock.Setup(n => n.CompareFrameworks(
            ComplianceFramework.FedRampHigh, ComplianceFramework.FedRampModerate))
            .Returns(MakeComparison());

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["frameworkA"] = "FedRampHigh",
            ["frameworkB"] = "FedRampModerate"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("frameworkA").GetString().Should().Be("FedRampHigh");
        data.GetProperty("frameworkB").GetString().Should().Be("FedRampModerate");
    }
}
