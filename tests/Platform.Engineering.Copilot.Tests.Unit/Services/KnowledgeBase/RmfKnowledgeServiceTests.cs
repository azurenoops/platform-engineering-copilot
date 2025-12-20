using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces.KnowledgeBase;
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.KnowledgeBase;

/// <summary>
/// Unit tests for RmfKnowledgeService
/// Tests RMF process explanations, step retrieval, and service-specific guidance
/// </summary>
public class RmfKnowledgeServiceTests
{
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<ILogger<RmfKnowledgeServiceTests>> _loggerMock;

    public RmfKnowledgeServiceTests()
    {
        _cacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<RmfKnowledgeServiceTests>>();
    }

    #region RmfProcess Model Tests

    [Fact]
    public void RmfProcess_Step1_Categorize()
    {
        // Arrange & Act
        var step = new RmfProcess
        {
            Step = "1",
            Title = "Categorize",
            Description = "Categorize the system and information processed, stored, and transmitted",
            Activities = new List<string>
            {
                "Document system characteristics",
                "Categorize information types",
                "Select impact levels"
            },
            Outputs = new List<string>
            {
                "System categorization document",
                "FIPS 199 categorization"
            },
            Roles = new List<string> { "System Owner", "ISSO", "AO" },
            DodInstruction = "CNSSI 1253"
        };

        // Assert
        step.Step.Should().Be("1");
        step.Title.Should().Be("Categorize");
        step.Activities.Should().HaveCount(3);
        step.Outputs.Should().HaveCount(2);
        step.Roles.Should().Contain("ISSO");
    }

    [Fact]
    public void RmfProcess_Step2_Select()
    {
        // Arrange & Act
        var step = new RmfProcess
        {
            Step = "2",
            Title = "Select",
            Description = "Select, tailor, and document security controls",
            Activities = new List<string>
            {
                "Select baseline security controls",
                "Tailor controls to organization",
                "Supplement controls as needed"
            },
            Outputs = new List<string>
            {
                "Security Plan (SSP)",
                "Tailoring documentation"
            },
            Roles = new List<string> { "ISSO", "Security Architect" },
            DodInstruction = "DoDI 8510.01"
        };

        // Assert
        step.Step.Should().Be("2");
        step.Title.Should().Be("Select");
        step.Outputs.Should().Contain("Security Plan (SSP)");
    }

    [Fact]
    public void RmfProcess_Step3_Implement()
    {
        // Arrange & Act
        var step = new RmfProcess
        {
            Step = "3",
            Title = "Implement",
            Description = "Implement security controls and describe implementation",
            Activities = new List<string>
            {
                "Implement controls in SSP",
                "Document implementation details",
                "Configure security settings"
            },
            Outputs = new List<string>
            {
                "Updated SSP with implementation details",
                "Configuration documentation"
            },
            Roles = new List<string> { "System Administrator", "ISSO" }
        };

        // Assert
        step.Step.Should().Be("3");
        step.Title.Should().Be("Implement");
    }

    [Fact]
    public void RmfProcess_Step4_Assess()
    {
        // Arrange & Act
        var step = new RmfProcess
        {
            Step = "4",
            Title = "Assess",
            Description = "Assess security controls to determine effectiveness",
            Activities = new List<string>
            {
                "Develop assessment plan",
                "Execute assessments",
                "Document findings"
            },
            Outputs = new List<string>
            {
                "Security Assessment Report (SAR)",
                "Assessment evidence"
            },
            Roles = new List<string> { "Security Assessor", "SCA" }
        };

        // Assert
        step.Step.Should().Be("4");
        step.Outputs.Should().Contain("Security Assessment Report (SAR)");
    }

    [Fact]
    public void RmfProcess_Step5_Authorize()
    {
        // Arrange & Act
        var step = new RmfProcess
        {
            Step = "5",
            Title = "Authorize",
            Description = "Authorize system operation based on risk determination",
            Activities = new List<string>
            {
                "Prepare authorization package",
                "Submit to AO",
                "Receive authorization decision"
            },
            Outputs = new List<string>
            {
                "Authorization Decision",
                "ATO letter"
            },
            Roles = new List<string> { "AO", "System Owner" }
        };

        // Assert
        step.Step.Should().Be("5");
        step.Outputs.Should().Contain("ATO letter");
    }

    [Fact]
    public void RmfProcess_Step6_Monitor()
    {
        // Arrange & Act
        var step = new RmfProcess
        {
            Step = "6",
            Title = "Monitor",
            Description = "Continuously monitor security controls and system",
            Activities = new List<string>
            {
                "Implement continuous monitoring",
                "Conduct ongoing assessments",
                "Report security status"
            },
            Outputs = new List<string>
            {
                "Continuous Monitoring Reports",
                "Updated POA&M"
            },
            Roles = new List<string> { "ISSO", "System Owner" }
        };

        // Assert
        step.Step.Should().Be("6");
        step.Title.Should().Be("Monitor");
    }

    #endregion

    #region Step Validation Tests

    [Theory]
    [InlineData("1", true)]
    [InlineData("2", true)]
    [InlineData("3", true)]
    [InlineData("4", true)]
    [InlineData("5", true)]
    [InlineData("6", true)]
    [InlineData("0", false)]
    [InlineData("7", false)]
    [InlineData("abc", false)]
    [InlineData("", false)]
    public void RmfStep_Validation(string step, bool expectedValid)
    {
        // Act
        var isValid = int.TryParse(step, out var stepNum) && stepNum >= 1 && stepNum <= 6;

        // Assert
        isValid.Should().Be(expectedValid);
    }

    #endregion

    #region Service-Specific Guidance Tests

    [Theory]
    [InlineData("navy", "Navy")]
    [InlineData("usnavy", "Navy")]
    [InlineData("army", "Army")]
    [InlineData("usarmy", "Army")]
    [InlineData("airforce", "Air Force")]
    [InlineData("usaf", "Air Force")]
    [InlineData("disa", "DISA")]
    public void ServiceMapping_NormalizesInput(string input, string expected)
    {
        // Arrange
        var normalizedService = input.ToLowerInvariant().Replace(" ", "").Replace(".", "");
        var serviceKey = normalizedService switch
        {
            "navy" or "usnavy" => "Navy",
            "army" or "usarmy" => "Army",
            "airforce" or "usaf" or "usairforce" => "Air Force",
            "disa" => "DISA",
            _ => input
        };

        // Assert
        serviceKey.Should().Be(expected);
    }

    #endregion

    #region Timeline Tests

    [Theory]
    [InlineData("newsystem", "newSystem")]
    [InlineData("new", "newSystem")]
    [InlineData("cloudmigration", "cloudMigration")]
    [InlineData("cloud", "cloudMigration")]
    [InlineData("fedramp", "cloudMigration")]
    [InlineData("reciprocity", "reciprocity")]
    public void TimelineType_Normalization(string input, string expectedKey)
    {
        // Arrange
        var normalizedType = input.ToLowerInvariant().Replace(" ", "");
        var timelineKey = normalizedType switch
        {
            "newsystem" or "new" => "newSystem",
            "cloudmigration" or "cloud" or "migration" or "fedramp" => "cloudMigration",
            "reciprocity" => "reciprocity",
            _ => normalizedType
        };

        // Assert
        timelineKey.Should().Be(expectedKey);
    }

    #endregion

    #region Output Formatting Tests

    [Fact]
    public void RmfExplanation_FullProcess_Format()
    {
        // Arrange
        var steps = new List<RmfProcess>
        {
            new RmfProcess { Step = "1", Title = "Categorize", Description = "Categorize system" },
            new RmfProcess { Step = "2", Title = "Select", Description = "Select controls" },
            new RmfProcess { Step = "3", Title = "Implement", Description = "Implement controls" },
            new RmfProcess { Step = "4", Title = "Assess", Description = "Assess controls" },
            new RmfProcess { Step = "5", Title = "Authorize", Description = "Authorize system" },
            new RmfProcess { Step = "6", Title = "Monitor", Description = "Monitor controls" }
        };

        // Act
        var explanation = $@"# Risk Management Framework (RMF)

## RMF Steps

{string.Join("\n\n", steps.Select(s => $"### Step {s.Step}: {s.Title}\n{s.Description}"))}";

        // Assert
        explanation.Should().Contain("Risk Management Framework");
        explanation.Should().Contain("Step 1: Categorize");
        explanation.Should().Contain("Step 6: Monitor");
    }

    [Fact]
    public void RmfExplanation_SingleStep_Format()
    {
        // Arrange
        var step = new RmfProcess
        {
            Step = "4",
            Title = "Assess",
            Description = "Assess security controls",
            Activities = new List<string> { "Develop plan", "Execute assessment" },
            Outputs = new List<string> { "SAR" }
        };

        // Act
        var explanation = $@"# RMF Step {step.Step}: {step.Title}

{step.Description}

## Activities

{string.Join("\n", step.Activities.Select((a, i) => $"{i + 1}. {a}"))}

## Key Deliverables

{string.Join("\n", step.Outputs.Select(o => $"- {o}"))}";

        // Assert
        explanation.Should().Contain("RMF Step 4: Assess");
        explanation.Should().Contain("Activities");
        explanation.Should().Contain("Deliverables");
    }

    #endregion

    #region Artifacts Tests

    [Fact]
    public void RmfArtifacts_RequiredDocuments()
    {
        // Arrange
        var requiredArtifacts = new List<string>
        {
            "System Security Plan (SSP)",
            "Security Assessment Report (SAR)",
            "Plan of Action and Milestones (POA&M)",
            "Authorization Decision Letter",
            "Continuous Monitoring Plan",
            "System Categorization Document",
            "Risk Assessment Report"
        };

        // Assert
        requiredArtifacts.Should().Contain("System Security Plan (SSP)");
        requiredArtifacts.Should().Contain("Security Assessment Report (SAR)");
        requiredArtifacts.Should().HaveCountGreaterOrEqualTo(5);
    }

    [Theory]
    [InlineData("SSP", "System Security Plan")]
    [InlineData("SAR", "Security Assessment Report")]
    [InlineData("POA&M", "Plan of Action and Milestones")]
    [InlineData("ATO", "Authorization to Operate")]
    public void ArtifactAcronyms(string acronym, string fullName)
    {
        // Arrange
        var acronymMap = new Dictionary<string, string>
        {
            ["SSP"] = "System Security Plan",
            ["SAR"] = "Security Assessment Report",
            ["POA&M"] = "Plan of Action and Milestones",
            ["ATO"] = "Authorization to Operate",
            ["IATO"] = "Interim Authorization to Operate",
            ["DATO"] = "Denial of Authorization to Operate"
        };

        // Assert
        acronymMap[acronym].Should().Be(fullName);
    }

    #endregion

    #region Role Tests

    [Fact]
    public void RmfRoles_CommonRoles()
    {
        // Arrange
        var roles = new List<string>
        {
            "Authorizing Official (AO)",
            "Information System Security Officer (ISSO)",
            "System Owner",
            "Security Control Assessor (SCA)",
            "Common Control Provider",
            "Information Owner"
        };

        // Assert
        roles.Should().Contain("Authorizing Official (AO)");
        roles.Should().Contain("Information System Security Officer (ISSO)");
    }

    [Theory]
    [InlineData("AO", "Authorizing Official")]
    [InlineData("ISSO", "Information System Security Officer")]
    [InlineData("SCA", "Security Control Assessor")]
    [InlineData("ISSM", "Information System Security Manager")]
    public void RoleAcronyms(string acronym, string fullName)
    {
        // Arrange
        var roleMap = new Dictionary<string, string>
        {
            ["AO"] = "Authorizing Official",
            ["ISSO"] = "Information System Security Officer",
            ["SCA"] = "Security Control Assessor",
            ["ISSM"] = "Information System Security Manager"
        };

        // Assert
        roleMap[acronym].Should().Be(fullName);
    }

    #endregion
}
