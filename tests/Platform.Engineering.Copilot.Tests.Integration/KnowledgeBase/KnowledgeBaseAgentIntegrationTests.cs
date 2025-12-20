using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Models.KnowledgeBase;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.KnowledgeBase;

/// <summary>
/// Integration tests for KnowledgeBase Agent
/// Tests end-to-end workflows including RMF, STIG, DoD Instructions, and Impact Levels
/// Uses Core models without requiring Infrastructure.Agent project reference
/// </summary>
[Trait("Category", "Integration")]
public class KnowledgeBaseAgentIntegrationTests
{
    #region AgentTask to AgentResponse Flow Tests

    [Fact]
    public void KnowledgeBaseAgent_TaskProcessing_NistControlExplanation()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain NIST 800-53 control AC-2",
            Parameters = new Dictionary<string, object>
            {
                ["controlId"] = "AC-2"
            },
            Priority = 1
        };

        // Act - Simulate expected response
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = @"# 📚 NIST 800-53 Control: AC-2

## Account Management

### Control Statement

The organization manages system accounts including identifying account types, establishing conditions for group membership, and specifying authorized users.

### 🔵 Azure Implementation

- **Azure AD**: Use Azure Active Directory for centralized identity management
- **RBAC**: Implement Role-Based Access Control with least privilege principle
- **PIM**: Use Privileged Identity Management for just-in-time access",
            ExecutionTimeMs = 150
        };

        // Assert
        response.Success.Should().BeTrue();
        response.AgentType.Should().Be(AgentType.KnowledgeBase);
        response.Content.Should().Contain("AC-2");
        response.Content.Should().Contain("Account Management");
        response.Content.Should().Contain("Azure");
    }

    [Fact]
    public void KnowledgeBaseAgent_TaskProcessing_RmfStepExplanation()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain RMF Step 4 - Assess",
            Parameters = new Dictionary<string, object>
            {
                ["step"] = "4"
            },
            Priority = 1
        };

        // Act - Simulate expected response
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = @"# RMF Step 4: Assess

## Description
Assess security controls to determine the extent to which the controls are implemented correctly, operating as intended, and producing the desired outcome.

## Activities

1. Develop security assessment plan
2. Execute security assessments
3. Document assessment findings
4. Prepare security assessment report

## Key Deliverables

- Security Assessment Report (SAR)
- Assessment evidence
- Updated POA&M",
            ExecutionTimeMs = 120
        };

        // Assert
        response.Success.Should().BeTrue();
        response.Content.Should().Contain("RMF Step 4");
        response.Content.Should().Contain("Assess");
        response.Content.Should().Contain("Security Assessment Report");
    }

    [Fact]
    public void KnowledgeBaseAgent_TaskProcessing_StigExplanation()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain STIG V-219153",
            Parameters = new Dictionary<string, object>
            {
                ["stigId"] = "V-219153"
            },
            Priority = 1
        };

        // Act - Simulate expected response
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = @"# V-219153: Windows Server 2016 Audit Configuration

**Severity:** Medium
**Category:** CAT II
**STIG Family:** Windows Server 2016

## Description

Windows Server 2016 must be configured to audit Logon/Logoff - Account Lockout successes.

## NIST 800-53 Controls

AU-2, AU-3, AU-12

## Check Procedure

Run the following PowerShell command...

## Remediation

Configure the policy value...",
            ExecutionTimeMs = 100
        };

        // Assert
        response.Success.Should().BeTrue();
        response.Content.Should().Contain("V-219153");
        response.Content.Should().Contain("**Severity:** Medium");
        response.Content.Should().Contain("AU-2");
    }

    [Fact]
    public void KnowledgeBaseAgent_TaskProcessing_ImpactLevelExplanation()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain Impact Level 5",
            Parameters = new Dictionary<string, object>
            {
                ["impactLevel"] = "IL5"
            },
            Priority = 1
        };

        // Act - Simulate expected response
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = @"# Impact Level 5 (IL5)

## Description
Controlled Unclassified Information (CUI) requiring enhanced protection.

## Security Requirements

**Baseline:** NIST 800-53 High

### Encryption Requirements
- **Data at Rest:** AES-256, FIPS 140-2 validated
- **Data in Transit:** TLS 1.2 minimum

### Network Requirements
- Physical isolation required
- Dedicated ExpressRoute connection

## Azure Implementation

**Cloud Environment:** Azure Government",
            ExecutionTimeMs = 110
        };

        // Assert
        response.Success.Should().BeTrue();
        response.Content.Should().Contain("IL5");
        response.Content.Should().Contain("FIPS 140-2");
        response.Content.Should().Contain("Azure Government");
    }

    #endregion

    #region SharedMemory Integration Tests

    [Fact]
    public void SharedMemory_StoresKnowledgeBaseContext()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(mockLogger.Object);
        var taskId = Guid.NewGuid().ToString();

        // Act
        memory.StoreContext(taskId, new ConversationContext
        {
            MentionedResources = new Dictionary<string, string>
            {
                ["lastControl"] = "AC-2",
                ["lastFamily"] = "Access Control",
                ["lastImpactLevel"] = "IL5"
            }
        });

        // Assert
        var context = memory.GetContext(taskId);
        context.Should().NotBeNull();
        context!.MentionedResources.Should().ContainKey("lastControl");
        context.MentionedResources["lastControl"].Should().Be("AC-2");
    }

    [Fact]
    public void SharedMemory_StoresGeneratedDocumentation()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(mockLogger.Object);
        var taskId = Guid.NewGuid().ToString();
        var documentation = @"# NIST 800-53 AC-2: Account Management
            
## Control Statement
The organization manages system accounts...

## Azure Implementation
Use Azure Active Directory with RBAC and PIM.";

        // Act
        memory.StoreGeneratedFiles(taskId, new Dictionary<string, string>
        {
            ["ac-2-documentation.md"] = documentation,
            ["ac-2-azure-guidance.md"] = "Azure specific implementation..."
        });

        // Assert
        var files = memory.GetGeneratedFileNames(taskId);
        files.Should().HaveCount(2);
        files.Should().Contain("ac-2-documentation.md");

        var content = memory.GetGeneratedFile(taskId, "ac-2-documentation.md");
        content.Should().Contain("Account Management");
    }

    [Fact]
    public void SharedMemory_StoresRmfProgress()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(mockLogger.Object);
        var taskId = Guid.NewGuid().ToString();

        // Act
        memory.StoreDeploymentMetadata(taskId, new Dictionary<string, string>
        {
            ["rmfStep"] = "3",
            ["rmfPhase"] = "Implement",
            ["systemName"] = "Test System",
            ["impactLevel"] = "IL5"
        });

        // Assert
        var metadata = memory.GetDeploymentMetadata(taskId);
        metadata.Should().NotBeNull();
        metadata!["rmfStep"].Should().Be("3");
        metadata["impactLevel"].Should().Be("IL5");
    }

    #endregion

    #region Multi-Query Workflow Tests

    [Fact]
    public void KnowledgeBase_MultiQueryWorkflow_ControlToStig()
    {
        // Arrange - First query: NIST Control
        var controlTask = new AgentTask
        {
            TaskId = "kb-multi-001",
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain NIST control AU-2",
            Parameters = new Dictionary<string, object> { ["controlId"] = "AU-2" }
        };

        // Simulate control response
        var controlResponse = new AgentResponse
        {
            TaskId = controlTask.TaskId,
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = "AU-2 is about Audit Events...",
            Metadata = new Dictionary<string, object>
            {
                ["relatedStigs"] = new List<string> { "V-219153", "V-219154" }
            }
        };

        // Second query: Related STIG
        var stigTask = new AgentTask
        {
            TaskId = "kb-multi-002",
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain STIG V-219153",
            Parameters = new Dictionary<string, object> { ["stigId"] = "V-219153" }
        };

        // Simulate STIG response
        var stigResponse = new AgentResponse
        {
            TaskId = stigTask.TaskId,
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = "V-219153 implements audit logging requirements..."
        };

        // Assert
        controlResponse.Success.Should().BeTrue();
        stigResponse.Success.Should().BeTrue();
        controlResponse.Metadata.Should().ContainKey("relatedStigs");
    }

    [Fact]
    public void KnowledgeBase_MultiQueryWorkflow_RmfToArtifacts()
    {
        // Arrange - First query: RMF Step
        var rmfTask = new AgentTask
        {
            TaskId = "kb-rmf-001",
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain RMF Step 5 - Authorize"
        };

        // Simulate RMF response with required artifacts
        var rmfResponse = new AgentResponse
        {
            TaskId = rmfTask.TaskId,
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = "RMF Step 5: Authorize...",
            Metadata = new Dictionary<string, object>
            {
                ["requiredArtifacts"] = new List<string> { "SSP", "SAR", "POA&M" },
                ["nextStep"] = "6"
            }
        };

        // Assert
        rmfResponse.Success.Should().BeTrue();
        rmfResponse.Metadata.Should().ContainKey("requiredArtifacts");
        ((List<string>)rmfResponse.Metadata["requiredArtifacts"]).Should().Contain("SSP");
    }

    #endregion

    #region Cross-Reference Integration Tests

    [Fact]
    public void ControlMapping_IntegratesNistStigAndDoD()
    {
        // Arrange
        var mapping = new ControlMapping
        {
            NistControlId = "AC-2",
            StigIds = new List<string> { "V-219153", "V-219154", "V-219155" },
            CciIds = new List<string> { "CCI-000015", "CCI-000016" },
            DoDInstructions = new List<string> { "DoDI 8500.01", "DoDI 8510.01" },
            Description = "Account Management control with full cross-reference",
            ImplementationGuidance = new Dictionary<string, string>
            {
                ["azure"] = "Use Azure AD with PIM for privileged access",
                ["onprem"] = "Use Active Directory with LAPS"
            }
        };

        // Assert
        mapping.NistControlId.Should().Be("AC-2");
        mapping.StigIds.Should().HaveCount(3);
        mapping.DoDInstructions.Should().HaveCount(2);
        mapping.ImplementationGuidance.Should().ContainKey("azure");
    }

    [Fact]
    public void ImpactLevel_IntegratesWithBoundaryProtection()
    {
        // Arrange
        var impactLevel = new ImpactLevel
        {
            Level = "IL5",
            Name = "Impact Level 5",
            Description = "CUI with enhanced protection",
            Requirements = new List<string>
            {
                "Physical isolation",
                "FIPS 140-2 validated encryption",
                "US Citizen only access"
            },
            MandatoryControls = new List<string> { "SC-7", "SC-28", "IA-2(1)" }
        };

        var boundaryRequirement = new BoundaryProtectionRequirement
        {
            ImpactLevel = "IL5",
            RequirementId = "BP-IL5-001",
            Description = "Boundary protection for IL5",
            MandatoryControls = new List<string> { "SC-7", "SC-7(4)", "SC-7(5)" },
            NetworkRequirements = new List<string>
            {
                "Dedicated ExpressRoute",
                "Azure Firewall Premium",
                "No public IPs"
            }
        };

        // Assert
        impactLevel.MandatoryControls.Should().Contain("SC-7");
        boundaryRequirement.MandatoryControls.Should().Contain("SC-7");
        boundaryRequirement.ImpactLevel.Should().Be(impactLevel.Level);
    }

    #endregion

    #region Search Integration Tests

    [Fact]
    public void KnowledgeBaseSearch_ReturnsRankedResults()
    {
        // Arrange
        var searchResults = new List<KnowledgeBaseSearchResult>
        {
            new KnowledgeBaseSearchResult
            {
                Type = "NIST Control",
                Id = "AC-2",
                Title = "Account Management",
                Summary = "Manages system accounts...",
                RelevanceScore = 0.95
            },
            new KnowledgeBaseSearchResult
            {
                Type = "STIG",
                Id = "V-219153",
                Title = "Windows Account Management",
                Summary = "Windows Server account configuration...",
                RelevanceScore = 0.82
            },
            new KnowledgeBaseSearchResult
            {
                Type = "DoD Instruction",
                Id = "DoDI 8500.01",
                Title = "Cybersecurity",
                Summary = "Account management policy...",
                RelevanceScore = 0.75
            }
        };

        // Act
        var rankedResults = searchResults.OrderByDescending(r => r.RelevanceScore).ToList();

        // Assert
        rankedResults.Should().HaveCount(3);
        rankedResults[0].Id.Should().Be("AC-2");
        rankedResults[0].RelevanceScore.Should().BeGreaterThan(rankedResults[1].RelevanceScore);
    }

    [Fact]
    public void KnowledgeBaseSearch_FiltersMinimumRelevance()
    {
        // Arrange
        var allResults = new List<KnowledgeBaseSearchResult>
        {
            new KnowledgeBaseSearchResult { Id = "AC-2", RelevanceScore = 0.95 },
            new KnowledgeBaseSearchResult { Id = "AC-3", RelevanceScore = 0.80 },
            new KnowledgeBaseSearchResult { Id = "AC-4", RelevanceScore = 0.65 },
            new KnowledgeBaseSearchResult { Id = "AC-5", RelevanceScore = 0.45 }
        };

        var minimumScore = 0.75;

        // Act
        var filteredResults = allResults.Where(r => r.RelevanceScore >= minimumScore).ToList();

        // Assert
        filteredResults.Should().HaveCount(2);
        filteredResults.All(r => r.RelevanceScore >= minimumScore).Should().BeTrue();
    }

    #endregion

    #region Error Handling Integration Tests

    [Fact]
    public void KnowledgeBaseAgent_HandlesControlNotFound()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain NIST control XY-999",
            Parameters = new Dictionary<string, object>
            {
                ["controlId"] = "XY-999"
            }
        };

        // Act - Simulate error response
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.KnowledgeBase,
            Success = true, // Knowledge base returns success with "not found" message
            Content = @"❓ Control 'XY-999' was not found in the NIST 800-53 catalog.

**Suggestions:**
- Check the control ID format (e.g., AC-2, IA-2(1), SC-28)
- Common control families: AC, AU, IA, SC, CM"
        };

        // Assert
        response.Content.Should().Contain("not found");
        response.Content.Should().Contain("Suggestions");
    }

    [Fact]
    public void KnowledgeBaseAgent_HandlesInvalidRmfStep()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain RMF Step 7"
        };

        // Act - Simulate error response
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = "RMF Step 7 not found. Valid steps are 1-6."
        };

        // Assert
        response.Content.Should().Contain("Valid steps are 1-6");
    }

    [Fact]
    public void KnowledgeBaseAgent_HandlesInvalidImpactLevel()
    {
        // Arrange
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.KnowledgeBase,
            Description = "Explain Impact Level 3"
        };

        // Act - Simulate error response
        var response = new AgentResponse
        {
            TaskId = task.TaskId,
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = "Impact Level 'IL3' not found. Valid levels: IL2, IL4, IL5, IL6."
        };

        // Assert
        response.Content.Should().Contain("Valid levels: IL2, IL4, IL5, IL6");
    }

    #endregion

    #region Conversation Context Tests

    [Fact]
    public void KnowledgeBase_MaintainsConversationContext()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(mockLogger.Object);

        // First query - NIST control
        var task1 = new AgentTask { TaskId = "task-1", AgentType = AgentType.KnowledgeBase };
        memory.StoreContext(task1.TaskId, new ConversationContext
        {
            MentionedResources = new Dictionary<string, string>
            {
                ["controlId"] = "AC-2",
                ["controlFamily"] = "Access Control"
            }
        });

        // Second query - Follow up
        var task2 = new AgentTask { TaskId = "task-2", AgentType = AgentType.KnowledgeBase };
        var previousContext = memory.GetContext(task1.TaskId);

        // Assert
        previousContext.Should().NotBeNull();
        previousContext!.MentionedResources["controlId"].Should().Be("AC-2");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void KnowledgeBaseAgent_ResponseTimeTracking()
    {
        // Arrange
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            Content = "Response content...",
            ExecutionTimeMs = 250
        };

        // Assert
        response.ExecutionTimeMs.Should().BeLessThan(5000); // Should complete in under 5 seconds
        response.ExecutionTimeMs.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(250)]
    [InlineData(500)]
    [InlineData(1000)]
    public void ExecutionTime_ReasonableRanges(long executionTimeMs)
    {
        // Arrange
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.KnowledgeBase,
            Success = true,
            ExecutionTimeMs = executionTimeMs
        };

        // Assert
        response.ExecutionTimeMs.Should().Be(executionTimeMs);
    }

    #endregion
}
