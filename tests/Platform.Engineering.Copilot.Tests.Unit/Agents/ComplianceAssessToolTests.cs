using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Compliance.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T045 — ComplianceAssessTool tests: parameters, combined scan, progress streaming, envelope.
/// </summary>
public class ComplianceAssessToolTests
{
    private readonly Mock<INistService> _nistServiceMock = new();
    private readonly ComplianceAssessTool _tool;

    public ComplianceAssessToolTests()
    {
        var logger = new Mock<ILogger<ComplianceAssessTool>>().Object;
        _tool = new ComplianceAssessTool(_nistServiceMock.Object, logger);

        // Default setup: return some controls
        _nistServiceMock.Setup(s => s.GetControlsByFamily(It.IsAny<string>()))
            .Returns(new List<ControlDefinition>
            {
                new() { ControlId = "AC-1", Family = "AC", FamilyName = "Access Control", Title = "Policy and Procedures" },
                new() { ControlId = "AC-2", Family = "AC", FamilyName = "Access Control", Title = "Account Management" },
            });

        _nistServiceMock.Setup(s => s.SearchControls(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(new List<ControlDefinition>
            {
                new() { ControlId = "AC-1", Family = "AC", FamilyName = "Access Control", Title = "Policy and Procedures" },
            });

        _nistServiceMock.Setup(s => s.GetFamilyCodes())
            .Returns(new List<string> { "AC", "AU" });
    }

    [Fact]
    public void Tool_HasCorrectName()
    {
        _tool.Name.Should().Be("compliance_assess");
    }

    [Fact]
    public void Tool_HasCorrectDescription()
    {
        _tool.Description.Should().NotBeNullOrWhiteSpace();
        _tool.Description.Should().Contain("compliance");
    }

    [Fact]
    public void Tool_RequiresAuthentication()
    {
        _tool.RequiresAuthentication.Should().BeTrue();
    }

    [Fact]
    public void Tool_RequiresReadPim()
    {
        _tool.PimTierRequired.Should().Be(PimTier.Read);
    }

    [Fact]
    public void Tool_HasValidParameterSchema()
    {
        var schema = _tool.Parameters;
        schema.Should().NotBeNullOrWhiteSpace();
        var doc = JsonDocument.Parse(schema);
        doc.RootElement.GetProperty("type").GetString().Should().Be("object");
        doc.RootElement.TryGetProperty("properties", out var props).Should().BeTrue();
        props.TryGetProperty("subscriptionId", out _).Should().BeTrue();
        props.TryGetProperty("framework", out _).Should().BeTrue();
        props.TryGetProperty("scanType", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_DefaultParameters_ReturnsSuccessEnvelope()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>());

        result.ShouldBeSuccessEnvelope("compliance_assess");
        result.ShouldHaveReasonableExecutionTime();
    }

    [Fact]
    public async Task Execute_DefaultParameters_ContainsComplianceScore()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("complianceScore", out _).Should().BeTrue();
        data.TryGetProperty("summary", out _).Should().BeTrue();
        data.TryGetProperty("assessmentId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_InvalidFramework_ReturnsErrorEnvelope()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "framework", "INVALID_FRAMEWORK" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeErrorEnvelope("INVALID_FRAMEWORK");
    }

    [Fact]
    public async Task Execute_ValidFramework_Succeeds()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "framework", "FedRAMPHigh" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope("compliance_assess");
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("framework").GetString().Should().Be("FedRAMPHigh");
    }

    [Fact]
    public async Task Execute_WithControlFamilies_FiltersControls()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "controlFamilies", "AC,AU" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope("compliance_assess");
        _nistServiceMock.Verify(s => s.GetControlsByFamily("AC"), Times.AtLeastOnce());
        _nistServiceMock.Verify(s => s.GetControlsByFamily("AU"), Times.AtLeastOnce());
    }

    [Fact]
    public async Task Execute_CombinedScan_ReportsProgress()
    {
        var progressUpdates = new List<ProgressUpdate>();
        var progress = new Progress<ProgressUpdate>(u => progressUpdates.Add(u));

        var parameters = new Dictionary<string, object?>
        {
            { "scanType", "combined" }
        };

        await _tool.ExecuteAsync(parameters, progress);

        // Allow async progress report delivery
        await Task.Delay(100);

        progressUpdates.Should().NotBeEmpty();
        progressUpdates.Should().Contain(u => u.PercentComplete == 100);
        progressUpdates.First().PercentComplete.Should().BeLessThan(100);
    }

    [Fact]
    public async Task Execute_PolicyScanType_ReportsePolicyPhase()
    {
        var progressUpdates = new List<ProgressUpdate>();
        var progress = new Progress<ProgressUpdate>(u => progressUpdates.Add(u));

        var parameters = new Dictionary<string, object?>
        {
            { "scanType", "policy" }
        };

        await _tool.ExecuteAsync(parameters, progress);
        await Task.Delay(100);

        progressUpdates.Should().Contain(u => u.Message != null && u.Message.Contains("Policy"));
    }

    [Fact]
    public async Task Execute_Result_HasFindingsByFamily()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("findingsByFamily", out var findings).Should().BeTrue();
        findings.GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Execute_WithSubscriptionId_UsesIt()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "subscriptionId", "sub-123" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("subscriptionId").GetString()
            .Should().Be("sub-123");
    }

    [Fact]
    public void Tool_IsBaseTool()
    {
        _tool.Should().BeAssignableTo<BaseTool>();
    }
}
