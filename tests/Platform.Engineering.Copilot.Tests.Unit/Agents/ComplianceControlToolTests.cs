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
/// T046 — ComplianceGetControlFamilyTool tests + ComplianceCompareFrameworksTool tests.
/// </summary>
public class ComplianceGetControlFamilyToolTests
{
    private readonly Mock<INistService> _nistServiceMock = new();
    private readonly ComplianceGetControlFamilyTool _tool;

    public ComplianceGetControlFamilyToolTests()
    {
        var logger = new Mock<ILogger<ComplianceGetControlFamilyTool>>().Object;
        _tool = new ComplianceGetControlFamilyTool(_nistServiceMock.Object, logger);
    }

    [Fact]
    public void Tool_HasCorrectName()
    {
        _tool.Name.Should().Be("compliance_get_control_family");
    }

    [Fact]
    public void Tool_RequiresAuthentication()
    {
        _tool.RequiresAuthentication.Should().BeTrue();
        _tool.PimTierRequired.Should().Be(PimTier.Read);
    }

    [Fact]
    public async Task Execute_ValidFamily_ReturnsControls()
    {
        _nistServiceMock.Setup(s => s.GetControlsByFamily("AC"))
            .Returns(new List<ControlDefinition>
            {
                new() { ControlId = "AC-1", Family = "AC", FamilyName = "Access Control", Title = "Policy",
                    Baselines = new BaselineApplicability { High = true, Moderate = true, Low = true } },
                new() { ControlId = "AC-2", Family = "AC", FamilyName = "Access Control", Title = "Account Management",
                    Baselines = new BaselineApplicability { High = true, Moderate = true, Low = false } },
            });

        var parameters = new Dictionary<string, object?> { { "familyId", "AC" } };
        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope("compliance_get_control_family");
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("family").GetString().Should().Be("AC");
        data.GetProperty("totalControls").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Execute_InvalidFamily_ReturnsError()
    {
        _nistServiceMock.Setup(s => s.GetControlsByFamily("ZZ"))
            .Returns(new List<ControlDefinition>());

        var parameters = new Dictionary<string, object?> { { "familyId", "ZZ" } };
        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeErrorEnvelope("INVALID_CONTROL_ID");
    }

    [Fact]
    public async Task Execute_IncludeControlsFalse_OmitsControlsList()
    {
        _nistServiceMock.Setup(s => s.GetControlsByFamily("AC"))
            .Returns(new List<ControlDefinition>
            {
                new() { ControlId = "AC-1", Family = "AC", FamilyName = "Access Control", Title = "Policy",
                    Baselines = new BaselineApplicability { High = true } }
            });

        var parameters = new Dictionary<string, object?>
        {
            { "familyId", "AC" },
            { "includeControls", false }
        };

        var result = await _tool.ExecuteAsync(parameters);
        result.ShouldBeSuccessEnvelope();
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        // Controls should be omitted (WhenWritingNull) or null when includeControls is false
        if (data.TryGetProperty("controls", out var controls))
        {
            controls.ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task Execute_BaselineApplicability_ConvertsToList()
    {
        _nistServiceMock.Setup(s => s.GetControlsByFamily("SC"))
            .Returns(new List<ControlDefinition>
            {
                new() { ControlId = "SC-1", Family = "SC", FamilyName = "System and Communications Protection", Title = "Policy",
                    Baselines = new BaselineApplicability { High = true, Moderate = true, Low = false } }
            });

        var parameters = new Dictionary<string, object?> { { "familyId", "SC" } };
        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        var controls = doc.RootElement.GetProperty("data").GetProperty("controls");
        var firstControl = controls[0];
        var baselines = firstControl.GetProperty("baselines");
        baselines.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Execute_CaseInsensitiveFamilyId()
    {
        _nistServiceMock.Setup(s => s.GetControlsByFamily("AC"))
            .Returns(new List<ControlDefinition>
            {
                new() { ControlId = "AC-1", Family = "AC", FamilyName = "Access Control", Title = "Policy" }
            });

        var parameters = new Dictionary<string, object?> { { "familyId", "ac" } };
        var result = await _tool.ExecuteAsync(parameters);

        // Should uppercase the familyId
        result.ShouldBeSuccessEnvelope();
        _nistServiceMock.Verify(s => s.GetControlsByFamily("AC"), Times.Once());
    }
}

/// <summary>
/// ComplianceCompareFrameworksTool tests.
/// </summary>
public class ComplianceCompareFrameworksToolTests
{
    private readonly Mock<INistService> _nistServiceMock = new();
    private readonly ComplianceCompareFrameworksTool _tool;

    public ComplianceCompareFrameworksToolTests()
    {
        var logger = new Mock<ILogger<ComplianceCompareFrameworksTool>>().Object;
        _tool = new ComplianceCompareFrameworksTool(_nistServiceMock.Object, logger);
    }

    [Fact]
    public void Tool_HasCorrectName()
    {
        _tool.Name.Should().Be("compliance_compare_frameworks");
    }

    [Fact]
    public void Tool_DoesNotRequireAuthentication()
    {
        _tool.RequiresAuthentication.Should().BeFalse();
        _tool.PimTierRequired.Should().Be(PimTier.None);
    }

    [Fact]
    public async Task Execute_ValidFrameworks_ReturnsComparison()
    {
        _nistServiceMock.Setup(s => s.CompareFrameworks(ComplianceFramework.FedRampHigh, ComplianceFramework.DoDIL5))
            .Returns(new FrameworkComparisonResult
            {
                FrameworkA = ComplianceFramework.FedRampHigh,
                FrameworkB = ComplianceFramework.DoDIL5,
                Common = new List<ControlDefinition>
                {
                    new() { ControlId = "AC-1" },
                    new() { ControlId = "AU-1" }
                },
                UniqueToA = new List<ControlDefinition> { new() { ControlId = "AC-99" } },
                UniqueToB = new List<ControlDefinition>()
            });

        var parameters = new Dictionary<string, object?>
        {
            { "framework1", "FedRampHigh" },
            { "framework2", "DoDIL5" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope("compliance_compare_frameworks");
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("commonCount").GetInt32().Should().Be(2);
        data.GetProperty("onlyIn1Count").GetInt32().Should().Be(1);
        data.GetProperty("onlyIn2Count").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Execute_InvalidFramework_ReturnsError()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "framework1", "INVALID" },
            { "framework2", "DoDIL5" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeErrorEnvelope("INVALID_FRAMEWORK");
    }

    [Fact]
    public async Task Execute_HasOverlapPercentage()
    {
        _nistServiceMock.Setup(s => s.CompareFrameworks(It.IsAny<ComplianceFramework>(), It.IsAny<ComplianceFramework>()))
            .Returns(new FrameworkComparisonResult
            {
                FrameworkA = ComplianceFramework.Nist80053Rev5,
                FrameworkB = ComplianceFramework.FedRampHigh,
                Common = new List<ControlDefinition> { new() { ControlId = "AC-1" } },
                UniqueToA = new List<ControlDefinition>(),
                UniqueToB = new List<ControlDefinition>()
            });

        var parameters = new Dictionary<string, object?>
        {
            { "framework1", "Nist80053Rev5" },
            { "framework2", "FedRampHigh" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("overlapPercentage", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_MissingRequiredParam_Throws()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "framework1", "NIST80053" }
            // Missing framework2
        };

        Func<Task> act = () => _tool.ExecuteAsync(parameters);
        await act.Should().ThrowAsync<ArgumentException>();
    }
}

/// <summary>
/// ComplianceMapControlsTool tests.
/// </summary>
public class ComplianceMapControlsToolTests
{
    private readonly Mock<INistService> _nistServiceMock = new();
    private readonly ComplianceMapControlsTool _tool;

    public ComplianceMapControlsToolTests()
    {
        var logger = new Mock<ILogger<ComplianceMapControlsTool>>().Object;
        _tool = new ComplianceMapControlsTool(_nistServiceMock.Object, logger);
    }

    [Fact]
    public void Tool_HasCorrectName()
    {
        _tool.Name.Should().Be("compliance_map_controls");
    }

    [Fact]
    public void Tool_DoesNotRequireAuth()
    {
        _tool.RequiresAuthentication.Should().BeFalse();
        _tool.PimTierRequired.Should().Be(PimTier.None);
    }

    [Fact]
    public async Task Execute_WithControlId_ReturnsMapping()
    {
        _nistServiceMock.Setup(s => s.GetControl("AC-1"))
            .Returns(new ControlDefinition
            {
                ControlId = "AC-1",
                Title = "Policy and Procedures",
                AzureServiceMappings = ["Azure AD", "Azure Policy"]
            });

        var parameters = new Dictionary<string, object?> { { "controlId", "AC-1" } };
        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope("compliance_map_controls");
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("mappingCount").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Execute_NoParams_ReturnsAvailableFamilies()
    {
        _nistServiceMock.Setup(s => s.GetFamilyCodes())
            .Returns(new List<string> { "AC", "AU", "SC" });

        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>());

        result.ShouldBeSuccessEnvelope();
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("totalFamilies").GetInt32().Should().Be(3);
    }
}
