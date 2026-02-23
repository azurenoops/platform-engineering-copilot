using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.KnowledgeBase;

/// <summary>
/// T094 — Unit tests for ExplainControlTool.
/// Verifies: controlId required, INistService.GetControl, Azure mappings, related controls, no auth.
/// </summary>
public class ExplainControlToolTests
{
    private readonly Mock<INistService> _nistServiceMock = new();
    private readonly ExplainControlTool _tool;

    public ExplainControlToolTests()
    {
        _tool = new ExplainControlTool(
            _nistServiceMock.Object,
            new Mock<ILogger<ExplainControlTool>>().Object);
    }

    private static ControlDefinition MakeControl(string controlId = "AC-2") => new()
    {
        ControlId = controlId,
        Family = "AC",
        FamilyName = "Access Control",
        Title = "Account Management",
        Description = "Manage system accounts.",
        ImplementationGuidance = "Configure Azure AD for account lifecycle.",
        Baselines = new BaselineApplicability { High = true, Moderate = true, Low = false },
        Frameworks = new FrameworkApplicability { Nist80053Rev5 = true, FedRampHigh = true },
        AzureServiceMappings = ["Azure AD", "Azure Policy"],
        Related = ["AC-3", "AC-6", "IA-2"],
        Priority = "P1",
        StigReferences =
        [
            new StigReference { StigId = "V-12345", BenchmarkId = "RHEL8", Severity = "CAT II" }
        ]
    };

    [Fact]
    public void Name_Returns_ExplainControl()
    {
        _tool.Name.Should().Be("explain_control");
    }

    [Fact]
    public void RequiresAuthentication_IsFalse()
    {
        _tool.RequiresAuthentication.Should().BeFalse();
    }

    [Fact]
    public async Task Execute_ValidControlId_ReturnsExplanation()
    {
        var control = MakeControl();
        _nistServiceMock.Setup(n => n.GetControl("AC-2")).Returns(control);

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");

        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("controlId").GetString().Should().Be("AC-2");
        data.GetProperty("controlName").GetString().Should().Be("Account Management");
        data.GetProperty("family").GetString().Should().Be("Access Control");
    }

    [Fact]
    public async Task Execute_ValidControl_IncludesAzureServiceMappings()
    {
        _nistServiceMock.Setup(n => n.GetControl("AC-2")).Returns(MakeControl());

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2"
        });

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        var mappings = data.GetProperty("azureServiceMappings");
        mappings.GetArrayLength().Should().Be(2);
        mappings[0].GetProperty("service").GetString().Should().Be("Azure AD");
    }

    [Fact]
    public async Task Execute_ValidControl_IncludesRelatedControls()
    {
        _nistServiceMock.Setup(n => n.GetControl("AC-2")).Returns(MakeControl());

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2"
        });

        var doc = JsonDocument.Parse(result);
        var related = doc.RootElement.GetProperty("data").GetProperty("relatedControls");
        related.GetArrayLength().Should().Be(3);
    }

    [Fact]
    public async Task Execute_ValidControl_IncludesStigReferences()
    {
        _nistServiceMock.Setup(n => n.GetControl("AC-2")).Returns(MakeControl());

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2"
        });

        var doc = JsonDocument.Parse(result);
        var stig = doc.RootElement.GetProperty("data").GetProperty("stigReferences");
        stig.GetArrayLength().Should().Be(1);
        stig[0].GetProperty("stigId").GetString().Should().Be("V-12345");
    }

    [Fact]
    public async Task Execute_ValidControl_IncludesBaselines()
    {
        _nistServiceMock.Setup(n => n.GetControl("AC-2")).Returns(MakeControl());

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2"
        });

        var doc = JsonDocument.Parse(result);
        var baselines = doc.RootElement.GetProperty("data").GetProperty("baselines");
        baselines.GetProperty("high").GetBoolean().Should().BeTrue();
        baselines.GetProperty("moderate").GetBoolean().Should().BeTrue();
        baselines.GetProperty("low").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Execute_ControlNotFound_ReturnsError()
    {
        _nistServiceMock.Setup(n => n.GetControl("ZZ-99")).Returns((ControlDefinition?)null);

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = "ZZ-99"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("CONTROL_NOT_FOUND");
    }

    [Fact]
    public async Task Execute_EmptyControlId_ReturnsError()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = ""
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("MISSING_CONTROL_ID");
    }

    [Fact]
    public async Task Execute_CaseInsensitive_ControlId()
    {
        _nistServiceMock.Setup(n => n.GetControl("AC-2")).Returns(MakeControl());

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = "ac-2"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task Execute_NoGuidance_ReturnsFallback()
    {
        var control = MakeControl();
        control.ImplementationGuidance = null;
        _nistServiceMock.Setup(n => n.GetControl("AC-2")).Returns(control);

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2"
        });

        var doc = JsonDocument.Parse(result);
        var guidance = doc.RootElement.GetProperty("data")
            .GetProperty("implementationGuidance").GetString();
        guidance.Should().Contain("Refer to the NIST 800-53");
    }
}
