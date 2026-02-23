using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.KnowledgeBase.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// T096 — Integration test for KB flow:
/// explain control → compare frameworks → search → STIG guidance, all without auth.
/// </summary>
public class KnowledgeBaseFlowTests
{
    private readonly Mock<INistService> _nistServiceMock;

    public KnowledgeBaseFlowTests()
    {
        _nistServiceMock = new Mock<INistService>();
        SetupNistService();
    }

    private void SetupNistService()
    {
        var ac2 = new ControlDefinition
        {
            ControlId = "AC-2",
            Family = "AC",
            FamilyName = "Access Control",
            Title = "Account Management",
            Description = "Manage system accounts throughout lifecycle.",
            ImplementationGuidance = "Use Azure AD for account management.",
            Baselines = new BaselineApplicability { High = true, Moderate = true, Low = true },
            Frameworks = new FrameworkApplicability { Nist80053Rev5 = true, FedRampHigh = true, FedRampModerate = true },
            AzureServiceMappings = ["Azure AD", "Azure Policy"],
            Related = ["AC-3", "AC-6"],
            Priority = "P1",
            StigReferences =
            [
                new StigReference { StigId = "V-12345", BenchmarkId = "RHEL8", Severity = "CAT II" }
            ]
        };

        var sc8 = new ControlDefinition
        {
            ControlId = "SC-8",
            Family = "SC",
            FamilyName = "System and Communications Protection",
            Title = "Transmission Confidentiality and Integrity",
            Description = "Protect information during transmission.",
            Baselines = new BaselineApplicability { High = true, Moderate = true },
            Frameworks = new FrameworkApplicability { Nist80053Rev5 = true, FedRampHigh = true },
            AzureServiceMappings = ["Azure Virtual Network", "Azure Firewall"],
            Related = ["SC-7", "SC-13"],
            Priority = "P1"
        };

        _nistServiceMock.Setup(n => n.GetControl("AC-2")).Returns(ac2);
        _nistServiceMock.Setup(n => n.GetControl("SC-8")).Returns(sc8);
        _nistServiceMock.Setup(n => n.SearchControls("encryption", 25))
            .Returns(new List<ControlDefinition> { sc8 });
        _nistServiceMock.Setup(n => n.CompareFrameworks(
            ComplianceFramework.FedRampHigh, ComplianceFramework.FedRampModerate))
            .Returns(new FrameworkComparisonResult
            {
                FrameworkA = ComplianceFramework.FedRampHigh,
                FrameworkB = ComplianceFramework.FedRampModerate,
                Common = new List<ControlDefinition> { ac2 },
                UniqueToA = new List<ControlDefinition> { sc8 },
                UniqueToB = new List<ControlDefinition>()
            });
    }

    [Fact]
    public async Task Flow_ExplainControl_ReturnsExplanation()
    {
        var tool = new ExplainControlTool(
            _nistServiceMock.Object,
            new Mock<ILogger<ExplainControlTool>>().Object);

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("controlId").GetString().Should().Be("AC-2");
        data.GetProperty("azureServiceMappings").GetArrayLength().Should().BeGreaterThan(0);
        data.GetProperty("relatedControls").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Flow_CompareFrameworks_ReturnsSharedAndUniqueControls()
    {
        var tool = new CompareFrameworksTool(
            _nistServiceMock.Object,
            new Mock<ILogger<CompareFrameworksTool>>().Object);

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["frameworkA"] = "FedRampHigh",
            ["frameworkB"] = "FedRampModerate"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("commonCount").GetInt32().Should().Be(1);
        data.GetProperty("uniqueToACount").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Flow_SearchControls_FindsResults()
    {
        var tool = new SearchControlsTool(
            _nistServiceMock.Object,
            new Mock<ILogger<SearchControlsTool>>().Object);

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["query"] = "encryption"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("totalResults").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Flow_GetStigGuidance_ReturnsStigData()
    {
        var tool = new GetStigGuidanceTool(
            _nistServiceMock.Object,
            new Mock<ILogger<GetStigGuidanceTool>>().Object);

        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("hasStigGuidance").GetBoolean().Should().BeTrue();
        data.GetProperty("stigReferences").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task Flow_AllToolsRequireNoAuth()
    {
        var tools = new BaseTool[]
        {
            new ExplainControlTool(_nistServiceMock.Object, new Mock<ILogger<ExplainControlTool>>().Object),
            new CompareFrameworksTool(_nistServiceMock.Object, new Mock<ILogger<CompareFrameworksTool>>().Object),
            new SearchControlsTool(_nistServiceMock.Object, new Mock<ILogger<SearchControlsTool>>().Object),
            new GetStigGuidanceTool(_nistServiceMock.Object, new Mock<ILogger<GetStigGuidanceTool>>().Object),
            new GetAtoChecklistTool(_nistServiceMock.Object, new Mock<ILogger<GetAtoChecklistTool>>().Object),
            new FrameworkSummaryTool(_nistServiceMock.Object, new Mock<ILogger<FrameworkSummaryTool>>().Object),
            new ControlMappingTool(_nistServiceMock.Object, new Mock<ILogger<ControlMappingTool>>().Object),
            new ImplementationExamplesTool(_nistServiceMock.Object, new Mock<ILogger<ImplementationExamplesTool>>().Object)
        };

        foreach (var tool in tools)
        {
            tool.RequiresAuthentication.Should().BeFalse($"{tool.Name} should not require auth (SC-008)");
        }
    }
}
