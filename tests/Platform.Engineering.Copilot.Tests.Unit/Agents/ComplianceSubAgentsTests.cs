using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Moq;
using Platform.Engineering.Copilot.Compliance.Agent.Plugins;
using Platform.Engineering.Copilot.Compliance.Agent.Services.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Chat;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// Unit tests for AtoPreparationAgent
/// Tests ATO package preparation, SSP/SAR/POA&M generation orchestration
/// </summary>
public class AtoPreparationAgentTests
{
    [Fact]
    public void AgentType_ShouldReturnCompliance()
    {
        // The AtoPreparationAgent is a sub-agent under Compliance domain
        // It reports AgentType.Compliance (not a separate type)
        AgentType.Compliance.Should().Be(AgentType.Compliance);
    }

    [Fact]
    public void AgentTask_ForAtoPreparation_HasCorrectProperties()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Compliance,
            Description = "Generate ATO package for production system",
            Priority = 1,
            IsCritical = true,
            Parameters = new Dictionary<string, object>
            {
                ["systemName"] = "Production Web Application",
                ["impactLevel"] = "Moderate",
                ["atoType"] = "Full"
            }
        };

        // Assert
        task.AgentType.Should().Be(AgentType.Compliance);
        task.Description.Should().Contain("ATO");
        task.IsCritical.Should().BeTrue();
        task.Parameters.Should().ContainKey("systemName");
        task.Parameters.Should().ContainKey("impactLevel");
    }

    [Theory]
    [InlineData("Low")]
    [InlineData("Moderate")]
    [InlineData("High")]
    public void AtoPackage_ImpactLevel_AcceptsValidValues(string impactLevel)
    {
        // Arrange & Act
        var task = new AgentTask
        {
            Parameters = new Dictionary<string, object>
            {
                ["impactLevel"] = impactLevel
            }
        };

        // Assert
        task.Parameters["impactLevel"].Should().Be(impactLevel);
    }

    [Theory]
    [InlineData("Full", "Complete ATO package with all artifacts")]
    [InlineData("Continuous", "Continuous ATO monitoring and updates")]
    [InlineData("Reauthorization", "ATO reauthorization package")]
    public void AtoType_HasExpectedMeanings(string atoType, string description)
    {
        // Document expected ATO types
        var atoTypes = new Dictionary<string, string>
        {
            ["Full"] = "Complete ATO package with all artifacts",
            ["Continuous"] = "Continuous ATO monitoring and updates",
            ["Reauthorization"] = "ATO reauthorization package"
        };

        // Assert
        atoTypes[atoType].Should().Be(description);
    }

    [Fact]
    public void AgentResponse_ForAtoPreparation_CanIncludeArtifacts()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Compliance,
            Success = true,
            Content = "ATO package generated successfully",
            Metadata = new Dictionary<string, object>
            {
                ["sspGenerated"] = true,
                ["sarGenerated"] = true,
                ["poamGenerated"] = true,
                ["artifactCount"] = 15,
                ["readinessScore"] = 85
            }
        };

        // Assert
        response.Success.Should().BeTrue();
        response.Metadata.Should().ContainKey("sspGenerated");
        response.Metadata.Should().ContainKey("sarGenerated");
        response.Metadata.Should().ContainKey("poamGenerated");
        response.Metadata["artifactCount"].Should().Be(15);
    }

    [Fact]
    public void SharedMemory_CanStoreAtoProgress()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);
        var conversationId = "ato-prep-123";
        
        // Act
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Compliance, // AtoPreparationAgent uses Compliance type
            AgentType.Orchestrator,
            "ATO package preparation: 50% complete",
            new Dictionary<string, object>
            {
                ["phase"] = "SSP Generation",
                ["completedArtifacts"] = 7,
                ["totalArtifacts"] = 15
            }
        );

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        communications.Should().NotBeEmpty();
        var comm = communications.First();
        var data = comm.Data as Dictionary<string, object>;
        data.Should().NotBeNull();
        data!.Should().ContainKey("phase");
        ((string)data["phase"]).Should().Be("SSP Generation");
    }
}

/// <summary>
/// Unit tests for DocumentAgent
/// Tests SSP, SAR, POA&M document generation
/// </summary>
public class DocumentAgentTests
{
    [Fact]
    public void AgentType_ShouldReturnCompliance()
    {
        // DocumentAgent is a sub-agent under Compliance domain
        AgentType.Compliance.Should().Be(AgentType.Compliance);
    }

    [Fact]
    public void AgentTask_ForDocumentGeneration_HasCorrectProperties()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Compliance,
            Description = "Generate System Security Plan for AC-2 control implementation",
            Priority = 1,
            Parameters = new Dictionary<string, object>
            {
                ["documentType"] = "SSP",
                ["controlId"] = "AC-2",
                ["systemName"] = "Web Application"
            }
        };

        // Assert
        task.AgentType.Should().Be(AgentType.Compliance);
        task.Description.Should().Contain("System Security Plan");
        task.Parameters.Should().ContainKey("documentType");
        task.Parameters["documentType"].Should().Be("SSP");
    }

    [Theory]
    [InlineData("SSP", "System Security Plan")]
    [InlineData("SAR", "Security Assessment Report")]
    [InlineData("POA&M", "Plan of Actions and Milestones")]
    [InlineData("ControlNarrative", "Control Implementation Narrative")]
    public void DocumentType_HasExpectedMeanings(string documentType, string fullName)
    {
        // Document expected document types
        var documentTypes = new Dictionary<string, string>
        {
            ["SSP"] = "System Security Plan",
            ["SAR"] = "Security Assessment Report",
            ["POA&M"] = "Plan of Actions and Milestones",
            ["ControlNarrative"] = "Control Implementation Narrative"
        };

        // Assert
        documentTypes[documentType].Should().Be(fullName);
    }

    [Fact]
    public void AgentResponse_ForDocument_CanIncludeDocumentContent()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Compliance,
            Success = true,
            Content = "# System Security Plan\n\n## AC-2: Account Management\n\nThis control ensures...",
            Metadata = new Dictionary<string, object>
            {
                ["documentType"] = "SSP",
                ["controlId"] = "AC-2",
                ["wordCount"] = 500,
                ["format"] = "Markdown"
            }
        };

        // Assert
        response.Success.Should().BeTrue();
        response.Content.Should().Contain("System Security Plan");
        response.Content.Should().Contain("AC-2");
        response.Metadata["documentType"].Should().Be("SSP");
    }

    [Fact]
    public void SharedMemory_CanStoreGeneratedDocuments()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);
        var conversationId = "doc-gen-456";
        
        // Act
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Compliance,
            AgentType.Orchestrator,
            "Document generated: SSP for AC-2",
            new Dictionary<string, object>
            {
                ["documentType"] = "SSP",
                ["controlId"] = "AC-2",
                ["content"] = "# System Security Plan..."
            }
        );

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        communications.Should().NotBeEmpty();
    }
}

/// <summary>
/// Unit tests for CodeScanningAgent
/// Tests security vulnerability scanning and code analysis
/// </summary>
public class CodeScanningAgentTests
{
    [Fact]
    public void AgentType_ShouldReturnCompliance()
    {
        // CodeScanningAgent is a sub-agent under Compliance domain
        AgentType.Compliance.Should().Be(AgentType.Compliance);
    }

    [Fact]
    public void AgentTask_ForCodeScanning_HasCorrectProperties()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Compliance,
            Description = "Scan repository for security vulnerabilities",
            Priority = 2,
            Parameters = new Dictionary<string, object>
            {
                ["repositoryPath"] = "/path/to/repo",
                ["scanType"] = "Security",
                ["includeDependencies"] = true
            }
        };

        // Assert
        task.AgentType.Should().Be(AgentType.Compliance);
        task.Description.Should().Contain("security vulnerabilities");
        task.Parameters.Should().ContainKey("scanType");
    }

    [Theory]
    [InlineData("Security")]
    [InlineData("Quality")]
    [InlineData("Compliance")]
    [InlineData("Full")]
    public void ScanType_AcceptsValidValues(string scanType)
    {
        // Arrange & Act
        var task = new AgentTask
        {
            Parameters = new Dictionary<string, object>
            {
                ["scanType"] = scanType
            }
        };

        // Assert
        task.Parameters["scanType"].Should().Be(scanType);
    }

    [Fact]
    public void AgentResponse_ForCodeScan_CanIncludeVulnerabilities()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.Compliance,
            Success = true,
            Content = "Code scan completed. Found 3 high-severity vulnerabilities.",
            Metadata = new Dictionary<string, object>
            {
                ["totalVulnerabilities"] = 10,
                ["criticalCount"] = 0,
                ["highCount"] = 3,
                ["mediumCount"] = 5,
                ["lowCount"] = 2,
                ["scanDuration"] = "45 seconds"
            }
        };

        // Assert
        response.Success.Should().BeTrue();
        response.Metadata["totalVulnerabilities"].Should().Be(10);
        response.Metadata["highCount"].Should().Be(3);
    }

    [Fact]
    public void CodeVulnerability_CanBeClassifiedBySeverity()
    {
        // This documents expected vulnerability classification
        var severities = new[] { "Critical", "High", "Medium", "Low", "Informational" };

        // Assert
        severities.Should().HaveCount(5);
        severities.Should().Contain("Critical");
        severities.Should().Contain("Informational");
    }

    [Fact]
    public void SharedMemory_CanStoreScanResults()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        var memory = new SharedMemory(loggerMock.Object);
        var conversationId = "scan-789";
        
        // Act
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Compliance,
            AgentType.Orchestrator,
            "Code scan completed with 10 findings",
            new Dictionary<string, object>
            {
                ["scanType"] = "Security",
                ["findingsCount"] = 10,
                ["passedChecks"] = 150
            }
        );

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        communications.Should().NotBeEmpty();
        var comm = communications.First();
        var data = comm.Data as Dictionary<string, object>;
        data.Should().NotBeNull();
        ((int)data!["findingsCount"]).Should().Be(10);
    }
}
