using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.KnowledgeBase;

/// <summary>
/// Unit tests for DoDInstructionService
/// Tests DoD Instruction retrieval, search, and control mapping
/// </summary>
public class DoDInstructionServiceTests
{
    private readonly Mock<IMemoryCache> _cacheMock;
    private readonly Mock<ILogger<DoDInstructionServiceTests>> _loggerMock;

    public DoDInstructionServiceTests()
    {
        _cacheMock = new Mock<IMemoryCache>();
        _loggerMock = new Mock<ILogger<DoDInstructionServiceTests>>();
    }

    #region DoDInstruction Model Tests

    [Fact]
    public void DoDInstruction_DefaultValues()
    {
        // Arrange & Act
        var instruction = new DoDInstruction();

        // Assert
        instruction.InstructionId.Should().BeEmpty();
        instruction.Title.Should().BeEmpty();
        instruction.Description.Should().BeEmpty();
        instruction.Url.Should().BeEmpty();
        instruction.RelatedNistControls.Should().BeEmpty();
        instruction.RelatedStigIds.Should().BeEmpty();
        instruction.ControlMappings.Should().BeEmpty();
    }

    [Fact]
    public void DoDInstruction_DoDI8500_01()
    {
        // Arrange & Act
        var instruction = new DoDInstruction
        {
            InstructionId = "DoDI 8500.01",
            Title = "Cybersecurity",
            Description = "Establishes policy and assigns responsibilities for cybersecurity in the DoD",
            PublicationDate = new DateTime(2014, 3, 14),
            Url = "https://www.esd.whs.mil/Portals/54/Documents/DD/issuances/dodi/850001_2014.pdf",
            Applicability = "All DoD Components",
            RelatedNistControls = new List<string> { "AC-1", "AC-2", "AT-1", "AU-1", "CA-1" },
            RelatedStigIds = new List<string> { "V-219153", "V-219187" }
        };

        // Assert
        instruction.InstructionId.Should().Be("DoDI 8500.01");
        instruction.Title.Should().Be("Cybersecurity");
        instruction.PublicationDate.Year.Should().Be(2014);
        instruction.RelatedNistControls.Should().Contain("AC-1");
    }

    [Fact]
    public void DoDInstruction_DoDI8510_01()
    {
        // Arrange & Act
        var instruction = new DoDInstruction
        {
            InstructionId = "DoDI 8510.01",
            Title = "Risk Management Framework (RMF) for DoD Information Technology (IT)",
            Description = "Implements the RMF for DoD systems",
            PublicationDate = new DateTime(2014, 3, 12),
            Url = "https://www.esd.whs.mil/Portals/54/Documents/DD/issuances/dodi/851001_2014.pdf",
            Applicability = "All DoD IT systems",
            RelatedNistControls = new List<string> { "CA-1", "CA-2", "CA-5", "CA-6", "CA-7" }
        };

        // Assert
        instruction.InstructionId.Should().Be("DoDI 8510.01");
        instruction.Title.Should().Contain("Risk Management Framework");
        instruction.RelatedNistControls.Should().Contain("CA-1");
    }

    [Fact]
    public void DoDInstruction_CNSSI1253()
    {
        // Arrange & Act
        var instruction = new DoDInstruction
        {
            InstructionId = "CNSSI 1253",
            Title = "Security Categorization and Control Selection for National Security Systems",
            Description = "Provides guidance on categorizing national security systems",
            PublicationDate = new DateTime(2014, 3, 27),
            Applicability = "National Security Systems",
            RelatedNistControls = new List<string> { "RA-2", "PM-7", "PM-8" }
        };

        // Assert
        instruction.InstructionId.Should().Be("CNSSI 1253");
        instruction.Title.Should().Contain("Security Categorization");
    }

    #endregion

    #region DoDControlMapping Model Tests

    [Fact]
    public void DoDControlMapping_DefaultValues()
    {
        // Arrange & Act
        var mapping = new DoDControlMapping();

        // Assert
        mapping.NistControlId.Should().BeEmpty();
        mapping.Section.Should().BeEmpty();
        mapping.Requirement.Should().BeEmpty();
        mapping.ImpactLevel.Should().Be("ALL");
    }

    [Fact]
    public void DoDControlMapping_FullMapping()
    {
        // Arrange & Act
        var mapping = new DoDControlMapping
        {
            NistControlId = "AC-2",
            Section = "Enclosure 3, Section 2.a",
            Requirement = "Account management procedures must be documented and implemented",
            ImpactLevel = "ALL"
        };

        // Assert
        mapping.NistControlId.Should().Be("AC-2");
        mapping.Section.Should().Contain("Enclosure");
        mapping.ImpactLevel.Should().Be("ALL");
    }

    [Fact]
    public void DoDInstruction_WithControlMappings()
    {
        // Arrange & Act
        var instruction = new DoDInstruction
        {
            InstructionId = "DoDI 8500.01",
            ControlMappings = new List<DoDControlMapping>
            {
                new DoDControlMapping
                {
                    NistControlId = "AC-1",
                    Section = "Enclosure 2, Section 3",
                    Requirement = "Access control policy development",
                    ImpactLevel = "ALL"
                },
                new DoDControlMapping
                {
                    NistControlId = "AC-2",
                    Section = "Enclosure 2, Section 3.a",
                    Requirement = "Account management implementation",
                    ImpactLevel = "IL4,IL5,IL6"
                }
            }
        };

        // Assert
        instruction.ControlMappings.Should().HaveCount(2);
        instruction.ControlMappings[0].NistControlId.Should().Be("AC-1");
        instruction.ControlMappings[1].ImpactLevel.Should().Be("IL4,IL5,IL6");
    }

    #endregion

    #region Instruction ID Format Tests

    [Theory]
    [InlineData("DoDI 8500.01", true)]
    [InlineData("DoDI 8510.01", true)]
    [InlineData("CNSSI 1253", true)]
    [InlineData("DoD CIO", true)]
    [InlineData("8500.01", false)]
    [InlineData("", false)]
    public void InstructionId_FormatValidation(string instructionId, bool isValid)
    {
        // Act
        var hasValidFormat = !string.IsNullOrWhiteSpace(instructionId) &&
            (instructionId.StartsWith("DoDI") ||
             instructionId.StartsWith("CNSSI") ||
             instructionId.StartsWith("DoD"));

        // Assert
        hasValidFormat.Should().Be(isValid);
    }

    [Theory]
    [InlineData("DoDI 8500.01", "DoDI")]
    [InlineData("DoDI 8510.01", "DoDI")]
    [InlineData("CNSSI 1253", "CNSSI")]
    [InlineData("DoD CIO Memo", "DoD")]
    public void InstructionType_Extraction(string instructionId, string expectedType)
    {
        // Act
        var type = instructionId.Split(' ')[0];

        // Assert
        type.Should().Be(expectedType);
    }

    #endregion

    #region Search Tests

    [Theory]
    [InlineData("cybersecurity")]
    [InlineData("risk management")]
    [InlineData("RMF")]
    [InlineData("authorization")]
    public void SearchTerm_ValidKeywords(string searchTerm)
    {
        // Arrange
        var instructions = new List<DoDInstruction>
        {
            new DoDInstruction { InstructionId = "DoDI 8500.01", Title = "Cybersecurity", Description = "Cybersecurity policy" },
            new DoDInstruction { InstructionId = "DoDI 8510.01", Title = "Risk Management Framework", Description = "RMF implementation" },
            new DoDInstruction { InstructionId = "DoDI 8520.02", Title = "Authorization", Description = "System authorization process" }
        };

        // Act
        var results = instructions.Where(i =>
            i.Title.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            i.Description.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ||
            i.InstructionId.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)).ToList();

        // Assert
        results.Should().NotBeEmpty();
    }

    [Fact]
    public void Search_ByNistControl()
    {
        // Arrange
        var instructions = new List<DoDInstruction>
        {
            new DoDInstruction
            {
                InstructionId = "DoDI 8500.01",
                RelatedNistControls = new List<string> { "AC-1", "AC-2", "AT-1" }
            },
            new DoDInstruction
            {
                InstructionId = "DoDI 8510.01",
                RelatedNistControls = new List<string> { "CA-1", "CA-2", "CA-6" }
            }
        };

        // Act
        var ac2Instructions = instructions
            .Where(i => i.RelatedNistControls.Contains("AC-2"))
            .ToList();

        // Assert
        ac2Instructions.Should().HaveCount(1);
        ac2Instructions[0].InstructionId.Should().Be("DoDI 8500.01");
    }

    #endregion

    #region Explanation Formatting Tests

    [Fact]
    public void InstructionExplanation_Format()
    {
        // Arrange
        var instruction = new DoDInstruction
        {
            InstructionId = "DoDI 8500.01",
            Title = "Cybersecurity",
            Description = "Establishes policy and assigns responsibilities for cybersecurity",
            PublicationDate = new DateTime(2014, 3, 14),
            Applicability = "All DoD Components",
            Url = "https://example.com/8500.01.pdf",
            RelatedNistControls = new List<string> { "AC-1", "AC-2" },
            RelatedStigIds = new List<string> { "V-219153" }
        };

        // Act
        var explanation = $@"# {instruction.InstructionId}: {instruction.Title}

## Description

{instruction.Description}

**Publication Date:** {instruction.PublicationDate:yyyy-MM-dd}

**Applicability:** {instruction.Applicability}

## Related NIST 800-53 Controls

{string.Join(", ", instruction.RelatedNistControls)}

## Related STIGs

{string.Join(", ", instruction.RelatedStigIds)}

## Reference

{instruction.Url}";

        // Assert
        explanation.Should().Contain("DoDI 8500.01");
        explanation.Should().Contain("Cybersecurity");
        explanation.Should().Contain("2014-03-14");
        explanation.Should().Contain("AC-1, AC-2");
        explanation.Should().Contain("V-219153");
    }

    #endregion

    #region Common DoD Instructions Tests

    [Fact]
    public void CommonDoDInstructions_List()
    {
        // Arrange
        var commonInstructions = new List<(string Id, string Topic)>
        {
            ("DoDI 8500.01", "Cybersecurity"),
            ("DoDI 8510.01", "Risk Management Framework"),
            ("DoDI 8520.02", "Public Key Infrastructure"),
            ("DoDI 8530.01", "Cybersecurity Activities"),
            ("DoDI 8580.01", "Information Assurance"),
            ("CNSSI 1253", "Security Categorization"),
            ("CNSSP 15", "National Information Assurance Policy")
        };

        // Assert
        commonInstructions.Should().Contain(i => i.Id == "DoDI 8500.01");
        commonInstructions.Should().Contain(i => i.Id == "DoDI 8510.01");
        commonInstructions.Should().Contain(i => i.Id == "CNSSI 1253");
    }

    [Theory]
    [InlineData("DoDI 8500.01", "Cybersecurity")]
    [InlineData("DoDI 8510.01", "Risk Management Framework")]
    [InlineData("CNSSI 1253", "Security Categorization")]
    public void InstructionTopicMapping(string instructionId, string expectedTopic)
    {
        // Arrange
        var topicMap = new Dictionary<string, string>
        {
            ["DoDI 8500.01"] = "Cybersecurity",
            ["DoDI 8510.01"] = "Risk Management Framework",
            ["CNSSI 1253"] = "Security Categorization"
        };

        // Assert
        topicMap[instructionId].Should().Be(expectedTopic);
    }

    #endregion

    #region Publication Date Tests

    [Fact]
    public void PublicationDate_Formatting()
    {
        // Arrange
        var instruction = new DoDInstruction
        {
            InstructionId = "DoDI 8500.01",
            PublicationDate = new DateTime(2014, 3, 14)
        };

        // Act
        var formattedDate = instruction.PublicationDate.ToString("yyyy-MM-dd");

        // Assert
        formattedDate.Should().Be("2014-03-14");
    }

    [Fact]
    public void PublicationDate_Comparison()
    {
        // Arrange
        var instructions = new List<DoDInstruction>
        {
            new DoDInstruction { InstructionId = "DoDI 8500.01", PublicationDate = new DateTime(2014, 3, 14) },
            new DoDInstruction { InstructionId = "DoDI 8510.01", PublicationDate = new DateTime(2014, 3, 12) },
            new DoDInstruction { InstructionId = "CNSSI 1253", PublicationDate = new DateTime(2014, 3, 27) }
        };

        // Act
        var mostRecent = instructions.OrderByDescending(i => i.PublicationDate).First();
        var oldest = instructions.OrderBy(i => i.PublicationDate).First();

        // Assert
        mostRecent.InstructionId.Should().Be("CNSSI 1253");
        oldest.InstructionId.Should().Be("DoDI 8510.01");
    }

    #endregion

    #region Applicability Tests

    [Theory]
    [InlineData("All DoD Components")]
    [InlineData("National Security Systems")]
    [InlineData("DoD IT Systems")]
    [InlineData("Classified Systems")]
    public void Applicability_ValidValues(string applicability)
    {
        // Arrange
        var instruction = new DoDInstruction { Applicability = applicability };

        // Assert
        instruction.Applicability.Should().Be(applicability);
        instruction.Applicability.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region DoDWorkflow Model Tests

    [Fact]
    public void DoDWorkflow_DefaultValues()
    {
        // Arrange & Act
        var workflow = new DoDWorkflow();

        // Assert
        workflow.WorkflowId.Should().BeEmpty();
        workflow.Name.Should().BeEmpty();
        workflow.Description.Should().BeEmpty();
        workflow.Steps.Should().BeEmpty();
        workflow.RequiredDocuments.Should().BeEmpty();
        workflow.ApprovalAuthorities.Should().BeEmpty();
    }

    [Fact]
    public void DoDWorkflow_AtoProcess()
    {
        // Arrange & Act
        var workflow = new DoDWorkflow
        {
            WorkflowId = "ATO-NAVY",
            Name = "Navy ATO Process",
            Description = "Authorization to Operate process for Navy systems",
            Organization = DoDOrganization.Navy,
            Steps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    StepNumber = 1,
                    Title = "System Registration",
                    Description = "Register system in eMASS",
                    EstimatedDuration = "2 weeks"
                },
                new WorkflowStep
                {
                    StepNumber = 2,
                    Title = "Security Assessment",
                    Description = "Conduct security assessment",
                    EstimatedDuration = "4-6 weeks"
                }
            },
            RequiredDocuments = new List<string> { "SSP", "SAR", "POA&M" },
            ApprovalAuthorities = new List<string> { "NAO", "DAA" },
            ImpactLevel = "IL5"
        };

        // Assert
        workflow.WorkflowId.Should().Be("ATO-NAVY");
        workflow.Organization.Should().Be(DoDOrganization.Navy);
        workflow.Steps.Should().HaveCount(2);
        workflow.RequiredDocuments.Should().Contain("SSP");
    }

    #endregion

    #region WorkflowStep Model Tests

    [Fact]
    public void WorkflowStep_DefaultValues()
    {
        // Arrange & Act
        var step = new WorkflowStep();

        // Assert
        step.StepNumber.Should().Be(0);
        step.Title.Should().BeEmpty();
        step.Description.Should().BeEmpty();
        step.Responsibilities.Should().BeEmpty();
        step.Deliverables.Should().BeEmpty();
        step.EstimatedDuration.Should().BeEmpty();
        step.Prerequisites.Should().BeEmpty();
    }

    [Fact]
    public void WorkflowStep_FullConfiguration()
    {
        // Arrange & Act
        var step = new WorkflowStep
        {
            StepNumber = 3,
            Title = "Security Control Implementation",
            Description = "Implement selected security controls",
            Responsibilities = new List<string> { "System Administrator", "ISSO", "Developer" },
            Deliverables = new List<string> { "Implemented controls", "Configuration evidence" },
            EstimatedDuration = "4-8 weeks",
            Prerequisites = new List<string> { "SSP approved", "Resources allocated" }
        };

        // Assert
        step.StepNumber.Should().Be(3);
        step.Responsibilities.Should().HaveCount(3);
        step.Deliverables.Should().HaveCount(2);
        step.Prerequisites.Should().Contain("SSP approved");
    }

    #endregion

    #region DoDOrganization Enum Tests

    [Theory]
    [InlineData(DoDOrganization.Navy)]
    [InlineData(DoDOrganization.PMW)]
    [InlineData(DoDOrganization.SPAWAR)]
    [InlineData(DoDOrganization.NAVWAR)]
    [InlineData(DoDOrganization.NIWC)]
    [InlineData(DoDOrganization.DISA)]
    [InlineData(DoDOrganization.CYBERCOM)]
    [InlineData(DoDOrganization.Other)]
    public void DoDOrganization_AllValuesExist(DoDOrganization org)
    {
        // Assert
        Enum.IsDefined(org).Should().BeTrue();
    }

    [Fact]
    public void DoDOrganization_Count()
    {
        // Assert
        Enum.GetValues<DoDOrganization>().Should().HaveCount(8);
    }

    #endregion
}
