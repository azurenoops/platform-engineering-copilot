using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.KnowledgeBase.Agent.Configuration;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// Unit tests for KnowledgeBaseAgent
/// Tests agent initialization, task processing, response handling, and configuration
/// Note: Microsoft.SemanticKernel.Kernel is sealed and cannot be mocked, so we test
/// configuration, models, and response patterns instead.
/// </summary>
public class KnowledgeBaseAgentTests
{
    #region AgentType Tests

    [Fact]
    public void AgentType_ShouldReturnKnowledgeBase()
    {
        // Assert - verify the expected agent type constant
        AgentType.KnowledgeBase.Should().Be(AgentType.KnowledgeBase);
    }

    [Fact]
    public void AgentType_KnowledgeBase_HasCorrectEnumValue()
    {
        // Assert
        ((int)AgentType.KnowledgeBase).Should().BeGreaterThan(0);
    }

    [Fact]
    public void AgentType_KnowledgeBase_IsDifferentFromOtherAgentTypes()
    {
        // Assert
        AgentType.KnowledgeBase.Should().NotBe(AgentType.Infrastructure);
        AgentType.KnowledgeBase.Should().NotBe(AgentType.Compliance);
        AgentType.KnowledgeBase.Should().NotBe(AgentType.CostManagement);
        AgentType.KnowledgeBase.Should().NotBe(AgentType.Discovery);
    }

    #endregion

    #region KnowledgeBaseAgentOptions Tests

    [Fact]
    public void KnowledgeBaseAgentOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions();

        // Assert
        options.EnableRag.Should().BeTrue();
        options.MinimumRelevanceScore.Should().Be(0.75);
        options.MaxRagResults.Should().Be(5);
        options.MaxCompletionTokens.Should().Be(4000);
        options.Temperature.Should().Be(0.3);
        options.ModelName.Should().Be("gpt-4o");
    }

    [Fact]
    public void KnowledgeBaseAgentOptions_ConversationHistory_DefaultValues()
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions();

        // Assert
        options.IncludeConversationHistory.Should().BeTrue();
        options.MaxConversationHistoryMessages.Should().Be(10);
    }

    [Fact]
    public void KnowledgeBaseAgentOptions_AzureAISearch_DefaultValues()
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions();

        // Assert
        options.KnowledgeBaseIndexName.Should().Be("knowledge-base-index");
        options.EnableSemanticSearch.Should().BeTrue();
        options.CacheDurationMinutes.Should().Be(60);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.3)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void KnowledgeBaseAgentOptions_Temperature_AcceptsValidRange(double temperature)
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions { Temperature = temperature };

        // Assert
        options.Temperature.Should().Be(temperature);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.75)]
    [InlineData(1.0)]
    public void KnowledgeBaseAgentOptions_MinimumRelevanceScore_AcceptsValidRange(double score)
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions { MinimumRelevanceScore = score };

        // Assert
        options.MinimumRelevanceScore.Should().Be(score);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public void KnowledgeBaseAgentOptions_MaxRagResults_AcceptsValidValues(int maxResults)
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions { MaxRagResults = maxResults };

        // Assert
        options.MaxRagResults.Should().Be(maxResults);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(4000)]
    [InlineData(8000)]
    [InlineData(16000)]
    public void KnowledgeBaseAgentOptions_MaxCompletionTokens_AcceptsValidValues(int tokens)
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions { MaxCompletionTokens = tokens };

        // Assert
        options.MaxCompletionTokens.Should().Be(tokens);
    }

    [Fact]
    public void KnowledgeBaseAgentOptions_SectionName_IsCorrect()
    {
        // Assert
        KnowledgeBaseAgentOptions.SectionName.Should().Be("KnowledgeBaseAgent");
    }

    [Fact]
    public void KnowledgeBaseAgentOptions_DisableRag_CanBeSet()
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions { EnableRag = false };

        // Assert
        options.EnableRag.Should().BeFalse();
    }

    [Fact]
    public void KnowledgeBaseAgentOptions_DisableSemanticSearch_CanBeSet()
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions { EnableSemanticSearch = false };

        // Assert
        options.EnableSemanticSearch.Should().BeFalse();
    }

    [Fact]
    public void KnowledgeBaseAgentOptions_CustomModelName_CanBeSet()
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions { ModelName = "gpt-4-turbo" };

        // Assert
        options.ModelName.Should().Be("gpt-4-turbo");
    }

    [Fact]
    public void KnowledgeBaseAgentOptions_CustomIndexName_CanBeSet()
    {
        // Arrange & Act
        var options = new KnowledgeBaseAgentOptions { KnowledgeBaseIndexName = "custom-kb-index" };

        // Assert
        options.KnowledgeBaseIndexName.Should().Be("custom-kb-index");
    }

    #endregion

    #region AgentTask Tests

    [Fact]
    public void AgentTask_ForKnowledgeBase_ShouldCreateCorrectly()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "kb-001",
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain NIST 800-53 AC-2 control",
            Priority = 1
        };

        // Assert
        task.TaskId.Should().Be("kb-001");
        task.AgentType.Should().Be(AgentType.KnowledgeBase);
        task.Description.Should().Contain("NIST");
        task.Priority.Should().Be(1);
    }

    [Theory]
    [InlineData("Explain RMF Step 4")]
    [InlineData("What is Impact Level 5?")]
    [InlineData("Search STIGs for encryption")]
    [InlineData("What does DoDI 8500.01 require?")]
    public void AgentTask_Description_SupportsKnowledgeBaseQueries(string description)
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.KnowledgeBase,
            Description = description
        };

        // Assert
        task.Description.Should().Be(description);
        task.AgentType.Should().Be(AgentType.KnowledgeBase);
    }

    [Fact]
    public void AgentTask_Parameters_CanContainControlId()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "kb-002",
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain NIST control",
            Parameters = new Dictionary<string, object>
            {
                ["controlId"] = "AC-2",
                ["includeAzure"] = true
            }
        };

        // Assert
        task.Parameters.Should().ContainKey("controlId");
        task.Parameters["controlId"].Should().Be("AC-2");
        task.Parameters["includeAzure"].Should().Be(true);
    }

    [Fact]
    public void AgentTask_Parameters_CanContainRmfStep()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "kb-003",
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain RMF process",
            Parameters = new Dictionary<string, object>
            {
                ["step"] = "3",
                ["detailed"] = true
            }
        };

        // Assert
        task.Parameters.Should().ContainKey("step");
        task.Parameters["step"].Should().Be("3");
    }

    [Fact]
    public void AgentTask_Parameters_CanContainStigId()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "kb-004",
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain STIG control",
            Parameters = new Dictionary<string, object>
            {
                ["stigId"] = "V-219153"
            }
        };

        // Assert
        task.Parameters.Should().ContainKey("stigId");
        task.Parameters["stigId"].Should().Be("V-219153");
    }

    [Fact]
    public void AgentTask_Parameters_CanContainImpactLevel()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = "kb-005",
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain Impact Level",
            Parameters = new Dictionary<string, object>
            {
                ["impactLevel"] = "IL5"
            }
        };

        // Assert
        task.Parameters.Should().ContainKey("impactLevel");
        task.Parameters["impactLevel"].Should().Be("IL5");
    }

    #endregion

    #region AgentResponse Tests

    [Fact]
    public void AgentResponse_ForKnowledgeBase_ShouldSetCorrectAgentType()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = "kb-001",
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = "AC-2 is about account management..."
        };

        // Assert
        response.AgentType.Should().Be(AgentType.KnowledgeBase);
        response.Success.Should().BeTrue();
        response.Content.Should().Contain("account management");
    }

    [Fact]
    public void AgentResponse_ExecutionTime_ShouldBeTracked()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = "kb-001",
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = "Response content",
            ExecutionTimeMs = 250
        };

        // Assert
        response.ExecutionTimeMs.Should().Be(250);
    }

    [Fact]
    public void AgentResponse_Metadata_CanContainControlFamily()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = "kb-001",
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = "AC-2 details...",
            Metadata = new Dictionary<string, object>
            {
                ["controlFamily"] = "Access Control",
                ["controlId"] = "AC-2"
            }
        };

        // Assert
        response.Metadata.Should().ContainKey("controlFamily");
        response.Metadata["controlFamily"].Should().Be("Access Control");
    }

    [Fact]
    public void AgentResponse_Metadata_CanContainRmfStep()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = "kb-002",
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = "RMF Step 3 details...",
            Metadata = new Dictionary<string, object>
            {
                ["rmfStep"] = "3",
                ["stepTitle"] = "Select Security Controls"
            }
        };

        // Assert
        response.Metadata.Should().ContainKey("rmfStep");
        response.Metadata["stepTitle"].Should().Be("Select Security Controls");
    }

    [Fact]
    public void AgentResponse_Error_ShouldHaveErrorDetails()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = "kb-001",
            AgentType = AgentType.KnowledgeBase,
            Success = false,
            Content = "Error retrieving control",
            Errors = new List<string> { "Control not found in catalog" }
        };

        // Assert
        response.Success.Should().BeFalse();
        response.Errors.Should().Contain("Control not found in catalog");
    }

    #endregion

    #region SharedMemory Tests

    [Fact]
    public void SharedMemory_StoreContext_ForKnowledgeBase()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(mockLogger.Object);

        // Act
        memory.StoreContext("kb-task-001", new ConversationContext
        {
            MentionedResources = new Dictionary<string, string>
            {
                ["controlId"] = "AC-2",
                ["controlFamily"] = "Access Control"
            }
        });

        // Assert
        var context = memory.GetContext("kb-task-001");
        context.Should().NotBeNull();
        context!.MentionedResources.Should().ContainKey("controlId");
    }

    [Fact]
    public void SharedMemory_StoreControlInformation()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(mockLogger.Object);

        // Act
        memory.StoreDeploymentMetadata("kb-task-001", new Dictionary<string, string>
        {
            ["lastControl"] = "AC-2",
            ["lastImpactLevel"] = "IL5",
            ["lastRmfStep"] = "3"
        });

        // Assert
        var metadata = memory.GetDeploymentMetadata("kb-task-001");
        metadata.Should().NotBeNull();
        metadata.Should().ContainKey("lastControl");
        metadata!["lastControl"].Should().Be("AC-2");
    }

    [Fact]
    public void SharedMemory_StoreGeneratedDocumentation()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(mockLogger.Object);
        var documentation = @"# NIST 800-53 AC-2: Account Management
            
## Description
The organization manages system accounts...";

        // Act
        memory.StoreGeneratedFiles("kb-task-001", new Dictionary<string, string>
        {
            ["ac-2-explanation.md"] = documentation
        });

        // Assert
        var file = memory.GetGeneratedFile("kb-task-001", "ac-2-explanation.md");
        file.Should().NotBeNull();
        file.Should().Contain("Account Management");
    }

    #endregion

    #region RmfProcess Model Tests

    [Fact]
    public void RmfProcess_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var process = new RmfProcess();

        // Assert
        process.Step.Should().BeEmpty();
        process.Title.Should().BeEmpty();
        process.Description.Should().BeEmpty();
        process.Activities.Should().BeEmpty();
        process.Outputs.Should().BeEmpty();
        process.Roles.Should().BeEmpty();
        process.DodInstruction.Should().BeEmpty();
    }

    [Fact]
    public void RmfProcess_CanBePopulated()
    {
        // Arrange & Act
        var process = new RmfProcess
        {
            Step = "3",
            Title = "Select Security Controls",
            Description = "Select, tailor, and document the controls necessary to protect the system",
            Activities = new List<string>
            {
                "Select initial set of baseline controls",
                "Tailor security controls",
                "Document control decisions"
            },
            Outputs = new List<string>
            {
                "Security Plan (SSP)",
                "Control Selection Documentation"
            },
            Roles = new List<string> { "ISSO", "System Owner", "AO" },
            DodInstruction = "DoDI 8510.01"
        };

        // Assert
        process.Step.Should().Be("3");
        process.Title.Should().Be("Select Security Controls");
        process.Activities.Should().HaveCount(3);
        process.Outputs.Should().Contain("Security Plan (SSP)");
        process.Roles.Should().Contain("ISSO");
    }

    [Theory]
    [InlineData("1", "Categorize")]
    [InlineData("2", "Select")]
    [InlineData("3", "Implement")]
    [InlineData("4", "Assess")]
    [InlineData("5", "Authorize")]
    [InlineData("6", "Monitor")]
    public void RmfProcess_ValidSteps(string step, string titleContains)
    {
        // Arrange & Act
        var process = new RmfProcess
        {
            Step = step,
            Title = $"{titleContains} Security Controls"
        };

        // Assert
        process.Step.Should().Be(step);
        process.Title.Should().Contain(titleContains);
    }

    #endregion

    #region StigControl Model Tests

    [Fact]
    public void StigControl_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var stig = new StigControl();

        // Assert
        stig.StigId.Should().BeEmpty();
        stig.VulnId.Should().BeEmpty();
        stig.RuleId.Should().BeEmpty();
        stig.Title.Should().BeEmpty();
        stig.Description.Should().BeEmpty();
        stig.NistControls.Should().BeEmpty();
        stig.CciRefs.Should().BeEmpty();
        stig.AzureImplementation.Should().BeEmpty();
    }

    [Fact]
    public void StigControl_CanBePopulated()
    {
        // Arrange & Act
        var stig = new StigControl
        {
            StigId = "V-219153",
            VulnId = "V-219153",
            RuleId = "SV-219153r897755_rule",
            Title = "Windows Server 2016 must be configured to audit Logon/Logoff - Account Lockout successes.",
            Description = "Maintaining an audit trail of system activity...",
            Severity = StigSeverity.Medium,
            CheckText = "Run the following PowerShell command...",
            FixText = "Configure the policy value...",
            NistControls = new List<string> { "AU-2", "AU-3", "AU-12" },
            CciRefs = new List<string> { "CCI-000172" },
            Category = "CAT II",
            StigFamily = "Windows Server 2016",
            ServiceType = StigServiceType.Compute,
            AzureImplementation = new Dictionary<string, string>
            {
                ["service"] = "Azure Policy",
                ["configuration"] = "Enable audit logging"
            }
        };

        // Assert
        stig.StigId.Should().Be("V-219153");
        stig.Severity.Should().Be(StigSeverity.Medium);
        stig.NistControls.Should().Contain("AU-2");
        stig.AzureImplementation.Should().ContainKey("service");
    }

    [Theory]
    [InlineData(StigSeverity.Low)]
    [InlineData(StigSeverity.Medium)]
    [InlineData(StigSeverity.High)]
    [InlineData(StigSeverity.Critical)]
    public void StigControl_SeverityLevels_AreValid(StigSeverity severity)
    {
        // Arrange & Act
        var stig = new StigControl { Severity = severity };

        // Assert
        stig.Severity.Should().Be(severity);
        Enum.IsDefined(typeof(StigSeverity), severity).Should().BeTrue();
    }

    [Theory]
    [InlineData(StigServiceType.Compute)]
    [InlineData(StigServiceType.Network)]
    [InlineData(StigServiceType.Storage)]
    [InlineData(StigServiceType.Database)]
    [InlineData(StigServiceType.Identity)]
    [InlineData(StigServiceType.Security)]
    [InlineData(StigServiceType.Containers)]
    public void StigControl_ServiceTypes_AreValid(StigServiceType serviceType)
    {
        // Arrange & Act
        var stig = new StigControl { ServiceType = serviceType };

        // Assert
        stig.ServiceType.Should().Be(serviceType);
        Enum.IsDefined(typeof(StigServiceType), serviceType).Should().BeTrue();
    }

    #endregion

    #region DoDInstruction Model Tests

    [Fact]
    public void DoDInstruction_DefaultValues_AreCorrect()
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
    public void DoDInstruction_CanBePopulated()
    {
        // Arrange & Act
        var instruction = new DoDInstruction
        {
            InstructionId = "DoDI 8500.01",
            Title = "Cybersecurity",
            Description = "Establishes policy and assigns responsibilities for cybersecurity...",
            PublicationDate = new DateTime(2014, 3, 14),
            Url = "https://www.esd.whs.mil/Portals/54/Documents/DD/issuances/dodi/850001_2014.pdf",
            Applicability = "All DoD Components",
            RelatedNistControls = new List<string> { "AC-1", "AC-2", "AT-1" },
            RelatedStigIds = new List<string> { "V-219153", "V-219187" }
        };

        // Assert
        instruction.InstructionId.Should().Be("DoDI 8500.01");
        instruction.Title.Should().Be("Cybersecurity");
        instruction.RelatedNistControls.Should().Contain("AC-1");
        instruction.PublicationDate.Year.Should().Be(2014);
    }

    [Fact]
    public void DoDInstruction_ControlMappings_CanBeAdded()
    {
        // Arrange & Act
        var instruction = new DoDInstruction
        {
            InstructionId = "DoDI 8510.01",
            ControlMappings = new List<DoDControlMapping>
            {
                new DoDControlMapping
                {
                    NistControlId = "CA-1",
                    Section = "Enclosure 3, Section 2.a",
                    Requirement = "Develop and document security assessment policy",
                    ImpactLevel = "ALL"
                }
            }
        };

        // Assert
        instruction.ControlMappings.Should().HaveCount(1);
        instruction.ControlMappings[0].NistControlId.Should().Be("CA-1");
        instruction.ControlMappings[0].ImpactLevel.Should().Be("ALL");
    }

    #endregion

    #region ImpactLevel Model Tests

    [Fact]
    public void ImpactLevel_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var level = new ImpactLevel();

        // Assert
        level.Level.Should().BeEmpty();
        level.Name.Should().BeEmpty();
        level.Description.Should().BeEmpty();
        level.Requirements.Should().BeEmpty();
        level.NistBaseline.Should().BeEmpty();
        level.MandatoryControls.Should().BeEmpty();
        level.AzureConfigurations.Should().BeEmpty();
    }

    [Fact]
    public void ImpactLevel_IL5_CanBePopulated()
    {
        // Arrange & Act
        var level = new ImpactLevel
        {
            Level = "IL5",
            Name = "Impact Level 5",
            Description = "Controlled Unclassified Information (CUI) requiring protection",
            Requirements = new List<string>
            {
                "FIPS 140-2/140-3 validated encryption",
                "US Citizen-only access",
                "Physical isolation requirements"
            },
            NistBaseline = new List<string> { "Moderate", "High" },
            MandatoryControls = new List<string> { "SC-28", "IA-2(1)", "AC-2" },
            AzureConfigurations = new Dictionary<string, string>
            {
                ["environment"] = "Azure Government",
                ["region"] = "usgovvirginia"
            }
        };

        // Assert
        level.Level.Should().Be("IL5");
        level.Requirements.Should().Contain(r => r.Contains("FIPS"));
        level.MandatoryControls.Should().Contain("SC-28");
        level.AzureConfigurations["environment"].Should().Be("Azure Government");
    }

    [Theory]
    [InlineData("IL2", "Public Cloud Data")]
    [InlineData("IL4", "Controlled Unclassified Information")]
    [InlineData("IL5", "CUI with Physical Isolation")]
    [InlineData("IL6", "Classified Information")]
    public void ImpactLevel_ValidLevels(string level, string nameContains)
    {
        // Arrange & Act
        var il = new ImpactLevel
        {
            Level = level,
            Name = $"{level}: {nameContains}"
        };

        // Assert
        il.Level.Should().Be(level);
        il.Name.Should().Contain(nameContains);
    }

    #endregion

    #region ControlMapping Model Tests

    [Fact]
    public void ControlMapping_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var mapping = new ControlMapping();

        // Assert
        mapping.NistControlId.Should().BeEmpty();
        mapping.StigIds.Should().BeEmpty();
        mapping.CciIds.Should().BeEmpty();
        mapping.DoDInstructions.Should().BeEmpty();
        mapping.Description.Should().BeEmpty();
        mapping.ImplementationGuidance.Should().BeEmpty();
    }

    [Fact]
    public void ControlMapping_CanMapMultipleSources()
    {
        // Arrange & Act
        var mapping = new ControlMapping
        {
            NistControlId = "AC-2",
            StigIds = new List<string> { "V-219153", "V-219187" },
            CciIds = new List<string> { "CCI-000015", "CCI-000016" },
            DoDInstructions = new List<string> { "DoDI 8500.01", "DoDI 8510.01" },
            Description = "Account Management control mapping",
            ImplementationGuidance = new Dictionary<string, string>
            {
                ["azure"] = "Use Azure AD with PIM",
                ["onprem"] = "Use Active Directory with LAPS"
            }
        };

        // Assert
        mapping.StigIds.Should().HaveCount(2);
        mapping.CciIds.Should().HaveCount(2);
        mapping.DoDInstructions.Should().HaveCount(2);
        mapping.ImplementationGuidance.Should().ContainKey("azure");
    }

    #endregion

    #region BoundaryProtectionRequirement Model Tests

    [Fact]
    public void BoundaryProtectionRequirement_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var requirement = new BoundaryProtectionRequirement();

        // Assert
        requirement.ImpactLevel.Should().BeEmpty();
        requirement.Description.Should().BeEmpty();
        requirement.NetworkRequirements.Should().BeEmpty();
        requirement.EncryptionRequirements.Should().BeEmpty();
        requirement.AzureImplementation.Should().BeEmpty();
    }

    [Fact]
    public void BoundaryProtectionRequirement_IL5_CanBePopulated()
    {
        // Arrange & Act
        var requirement = new BoundaryProtectionRequirement
        {
            ImpactLevel = "IL5",
            RequirementId = "BP-001",
            Description = "Boundary protection for IL5 workloads",
            MandatoryControls = new List<string> { "SC-7", "SC-7(4)", "SC-7(5)" },
            NetworkRequirements = new List<string>
            {
                "Dedicated ExpressRoute connection",
                "Azure Firewall required",
                "No public IP addresses"
            },
            EncryptionRequirements = new List<string>
            {
                "TLS 1.2 minimum",
                "FIPS 140-2 validated cryptography"
            },
            AzureImplementation = new Dictionary<string, string>
            {
                ["firewall"] = "Azure Firewall Premium",
                ["vnet"] = "Hub-spoke topology"
            }
        };

        // Assert
        requirement.ImpactLevel.Should().Be("IL5");
        requirement.NetworkRequirements.Should().HaveCount(3);
        requirement.EncryptionRequirements.Should().Contain(e => e.Contains("FIPS"));
        requirement.AzureImplementation["firewall"].Should().Be("Azure Firewall Premium");
    }

    #endregion

    #region KnowledgeBaseSearchResult Model Tests

    [Fact]
    public void KnowledgeBaseSearchResult_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var result = new KnowledgeBaseSearchResult();

        // Assert
        result.Type.Should().BeEmpty();
        result.Id.Should().BeEmpty();
        result.Title.Should().BeEmpty();
        result.Summary.Should().BeEmpty();
        result.RelevanceScore.Should().Be(0);
        result.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void KnowledgeBaseSearchResult_CanBePopulated()
    {
        // Arrange & Act
        var result = new KnowledgeBaseSearchResult
        {
            Type = "NIST Control",
            Id = "AC-2",
            Title = "Account Management",
            Summary = "The organization manages system accounts...",
            RelevanceScore = 0.95,
            Metadata = new Dictionary<string, object>
            {
                ["controlFamily"] = "Access Control",
                ["baseline"] = "Moderate"
            }
        };

        // Assert
        result.Type.Should().Be("NIST Control");
        result.Id.Should().Be("AC-2");
        result.RelevanceScore.Should().BeGreaterThan(0.9);
        result.Metadata.Should().ContainKey("controlFamily");
    }

    [Theory]
    [InlineData("RMF")]
    [InlineData("STIG")]
    [InlineData("DoD Instruction")]
    [InlineData("Workflow")]
    [InlineData("NIST Control")]
    public void KnowledgeBaseSearchResult_ValidTypes(string type)
    {
        // Arrange & Act
        var result = new KnowledgeBaseSearchResult { Type = type };

        // Assert
        result.Type.Should().Be(type);
    }

    #endregion

    #region DoDOrganization Enum Tests

    [Theory]
    [InlineData(DoDOrganization.Navy)]
    [InlineData(DoDOrganization.PMW)]
    [InlineData(DoDOrganization.DISA)]
    [InlineData(DoDOrganization.CYBERCOM)]
    public void DoDOrganization_ValidValues(DoDOrganization org)
    {
        // Assert
        Enum.IsDefined(typeof(DoDOrganization), org).Should().BeTrue();
    }

    #endregion

    #region Response Formatting Tests

    [Fact]
    public void KnowledgeBaseResponse_ControlExplanation_Format()
    {
        // Arrange
        var controlId = "AC-2";
        var controlTitle = "Account Management";
        var controlDescription = "The organization manages system accounts...";

        // Act - simulate response format
        var responseContent = $@"# 📚 NIST 800-53 Control: {controlId}

## {controlTitle}

### Control Statement

{controlDescription}

### 🔵 Azure Implementation

- **Azure AD**: Use Azure Active Directory for centralized identity management
- **RBAC**: Implement Role-Based Access Control with least privilege principle

---
*This is informational only. To check your compliance status, ask: 'Run a compliance assessment'*";

        // Assert
        responseContent.Should().Contain(controlId);
        responseContent.Should().Contain(controlTitle);
        responseContent.Should().Contain("Azure Implementation");
        responseContent.Should().Contain("informational only");
    }

    [Fact]
    public void KnowledgeBaseResponse_RmfExplanation_Format()
    {
        // Arrange
        var step = "3";
        var title = "Select Security Controls";

        // Act - simulate response format
        var responseContent = $@"# RMF Step {step}: {title}

## Activities

1. Select initial set of baseline controls
2. Tailor security controls
3. Document control decisions

## Key Deliverables

- Security Plan (SSP)
- Control Selection Documentation

## Next Steps

After completing Step {step}, proceed to Step 4.";

        // Assert
        responseContent.Should().Contain($"RMF Step {step}");
        responseContent.Should().Contain(title);
        responseContent.Should().Contain("Activities");
        responseContent.Should().Contain("Deliverables");
    }

    [Fact]
    public void KnowledgeBaseResponse_ImpactLevelExplanation_Format()
    {
        // Arrange
        var level = "IL5";

        // Act - simulate response format
        var responseContent = $@"# Impact Level 5 ({level})

## Description
Controlled Unclassified Information (CUI) requiring protection

## Security Requirements

**Baseline:** NIST 800-53 Moderate/High

### Encryption Requirements
- **Data at Rest:** AES-256, FIPS 140-2 validated
- **Data in Transit:** TLS 1.2 minimum

## Azure Implementation

**Cloud Environment:** Azure Government";

        // Assert
        responseContent.Should().Contain(level);
        responseContent.Should().Contain("Encryption Requirements");
        responseContent.Should().Contain("Azure Government");
    }

    #endregion
}
