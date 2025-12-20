using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual.Agents;

/// <summary>
/// Manual tests for Multi-Agent Orchestration via MCP HTTP API.
/// Tests scenarios requiring coordination between multiple specialized agents.
/// 
/// Prerequisites:
/// - MCP server running in HTTP mode: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 5100
/// - Azure CLI authenticated with appropriate permissions
/// </summary>
public class MultiAgentOrchestrationManualTests : McpHttpTestBase
{

    public MultiAgentOrchestrationManualTests(ITestOutputHelper output) : base(output) { }

    #region Full Environment Assessment

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task FullEnvironmentAssessment_Comprehensive_ShouldInvokeMultipleAgents()
    {
        // Arrange
        var message = "Perform a comprehensive assessment of my Azure environment including security posture, compliance status, cost optimization opportunities, and infrastructure health";

        // Act
        var response = await SendChatRequestAsync(message, "multi-assessment-001");

        // Assert
        AssertSuccessfulResponse(response);
        
        Output.WriteLine($"Agents invoked: {string.Join(", ", response.AgentsInvoked)}");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task FullEnvironmentAssessment_ProductionReadiness_ShouldInvokeMultipleAgents()
    {
        // Arrange
        var message = "Assess production readiness of my environment checking security, compliance, cost efficiency, and high availability configurations";

        // Act
        var response = await SendChatRequestAsync(message, "multi-assessment-prod-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Compliant Infrastructure Generation

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task CompliantInfrastructure_FedRAMPHighWebApp_ShouldCombineAgents()
    {
        // Arrange
        var message = "Generate a Bicep template for a FedRAMP High compliant web application with App Service, SQL Database, and Key Vault that meets all NIST 800-53 controls for SC and AC families";

        // Act
        var response = await SendChatRequestAsync(message, "multi-infra-fedramp-001");

        // Assert
        AssertSuccessfulResponse(response);
        Output.WriteLine($"Agents invoked: {string.Join(", ", response.AgentsInvoked ?? new List<string>())}");
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task CompliantInfrastructure_SecureAKSCluster_ShouldCombineAgents()
    {
        // Arrange
        var message = "Design a secure AKS cluster deployment that meets DoD IL4 requirements with network policies, encryption, and audit logging";

        // Act
        var response = await SendChatRequestAsync(message, "multi-infra-aks-il4-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task CompliantInfrastructure_ZeroTrustNetwork_ShouldCombineAgents()
    {
        // Arrange
        var message = "Generate infrastructure templates for a zero-trust network architecture with Azure Firewall, Private Link, and conditional access policies";

        // Act
        var response = await SendChatRequestAsync(message, "multi-infra-zerotrust-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Cost-Optimized Secure Architecture

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task CostOptimizedSecure_ProductionWorkload_ShouldBalanceCostAndSecurity()
    {
        // Arrange
        var message = "Design a cost-optimized architecture for a production workload that maintains security compliance and high availability with a monthly budget under $5000";

        // Act
        var response = await SendChatRequestAsync(message, "multi-cost-secure-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task CostOptimizedSecure_DevEnvironment_ShouldBalanceCostAndSecurity()
    {
        // Arrange
        var message = "Design a cost-effective dev/test environment that still meets basic security requirements with auto-shutdown capabilities";

        // Act
        var response = await SendChatRequestAsync(message, "multi-cost-secure-dev-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Incident Response Workflow

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task IncidentResponse_CriticalVulnerability_ShouldCoordinateResponse()
    {
        // Arrange
        var message = "A critical vulnerability was found in our production environment. Identify affected resources, assess compliance impact, estimate remediation cost, and generate a remediation plan";

        // Act
        var response = await SendChatRequestAsync(message, "multi-incident-vuln-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task IncidentResponse_DataBreach_ShouldCoordinateResponse()
    {
        // Arrange
        var message = "Suspected data breach detected. Identify potentially affected data stores, check encryption status, review access logs, and generate incident report";

        // Act
        var response = await SendChatRequestAsync(message, "multi-incident-breach-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task IncidentResponse_CostSpike_ShouldCoordinateResponse()
    {
        // Arrange
        var message = "Unexpected 300% cost increase detected. Identify the cause, check for unauthorized resources, assess security implications, and recommend immediate actions";

        // Act
        var response = await SendChatRequestAsync(message, "multi-incident-cost-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Migration Planning

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task MigrationPlanning_OnPremToAzure_ShouldCoordinatePlanning()
    {
        // Arrange
        var message = "Plan a migration from on-premises SQL Server to Azure SQL with compliance requirements, cost projections, and security considerations";

        // Act
        var response = await SendChatRequestAsync(message, "multi-migration-sql-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task MigrationPlanning_VMsToContainers_ShouldCoordinatePlanning()
    {
        // Arrange
        var message = "Plan containerization of VM-based workloads to AKS including cost comparison, security assessment, and compliance validation";

        // Act
        var response = await SendChatRequestAsync(message, "multi-migration-containers-001");

        // Assert
        AssertSuccessfulResponse(response);
    }

    #endregion

    #region Conversation Context Tests

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task ConversationContext_MultiTurnInteraction_ShouldMaintainContext()
    {
        // Arrange
        var conversationId = $"context-test-{Guid.NewGuid():N}";

        // Act - First message
        var response1 = await SendChatRequestAsync(
            "I want to deploy a new web application",
            conversationId);
        
        Output.WriteLine("--- First Response ---");
        AssertSuccessfulResponse(response1);

        // Act - Second message (should use context)
        var response2 = await SendChatRequestAsync(
            "Make it highly available across two regions",
            conversationId);
        
        Output.WriteLine("--- Second Response ---");
        AssertSuccessfulResponse(response2);

        // Act - Third message (should remember both)
        var response3 = await SendChatRequestAsync(
            "Now add compliance controls for FedRAMP Moderate",
            conversationId);
        
        Output.WriteLine("--- Third Response ---");
        AssertSuccessfulResponse(response3);

        // Assert - Final response should reference previous context
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task ConversationContext_FollowUpQuestions_ShouldMaintainContext()
    {
        // Arrange
        var conversationId = $"followup-test-{Guid.NewGuid():N}";

        // Act - Initial request
        var response1 = await SendChatRequestAsync(
            "Analyze my production environment for security issues",
            conversationId);
        
        AssertSuccessfulResponse(response1);

        // Act - Follow-up question
        var response2 = await SendChatRequestAsync(
            "What's the most critical issue to fix first?",
            conversationId);
        
        AssertSuccessfulResponse(response2);

        // Act - Another follow-up
        var response3 = await SendChatRequestAsync(
            "Generate a remediation plan for that issue",
            conversationId);
        
        AssertSuccessfulResponse(response3);
    }

    #endregion

    #region Edge Cases and Error Handling

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task EdgeCase_AmbiguousRequest_ShouldHandleGracefully()
    {
        // Arrange
        var message = "Help me with Azure";

        // Act
        var response = await SendChatRequestAsync(message, "edge-ambiguous-001");

        // Assert - Should either ask for clarification or provide general guidance
        response.Success.Should().BeTrue();
        response.Response.Should().NotBeNullOrEmpty();
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task EdgeCase_ConflictingRequirements_ShouldHandleGracefully()
    {
        // Arrange
        var message = "Create the cheapest possible production environment that has the highest security and compliance levels";

        // Act
        var response = await SendChatRequestAsync(message, "edge-conflicting-001");

        // Assert - Should acknowledge trade-offs
        response.Success.Should().BeTrue();
        response.Response.Should().NotBeNullOrEmpty();
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "MultiAgent")]
    public async Task EdgeCase_VeryLongRequest_ShouldHandleGracefully()
    {
        // Arrange
        var message = "I need to deploy a complex multi-tier application with the following requirements: " +
            "a web front-end using App Service with multiple deployment slots, " +
            "an API layer using Azure Functions with premium plan, " +
            "a caching layer using Redis Cache, " +
            "a database layer using Azure SQL with geo-replication, " +
            "a message queue using Service Bus, " +
            "storage accounts for blobs and files, " +
            "Azure Key Vault for secrets, " +
            "Application Insights for monitoring, " +
            "Log Analytics workspace for centralized logging, " +
            "all resources must be in paired regions for DR, " +
            "must meet FedRAMP Moderate compliance, " +
            "budget should not exceed $8000/month, " +
            "and generate Bicep templates for everything";

        // Act
        var response = await SendChatRequestAsync(message, "edge-longquery-001");

        // Assert
        response.Success.Should().BeTrue();
        response.Response.Should().NotBeNullOrEmpty();
        response.ProcessingTimeMs.Should().BeGreaterThan(0);
        
        Output.WriteLine($"Processing time for complex request: {response.ProcessingTimeMs}ms");
    }

    #endregion
}
