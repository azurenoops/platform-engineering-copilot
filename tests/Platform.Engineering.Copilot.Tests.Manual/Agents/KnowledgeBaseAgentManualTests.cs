using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual.Agents;

/// <summary>
/// Manual tests for Knowledge Base Agent via MCP HTTP API.
/// Tests documentation search, best practices retrieval, and knowledge management.
/// 
/// Prerequisites:
/// - MCP server running in HTTP mode: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 5100
/// </summary>
public class KnowledgeBaseAgentManualTests : McpHttpTestBase
{
    private static readonly string[] AcceptableIntents = { "knowledgebase", "knowledge", "multi_agent", "agent_execution", "orchestrat" };

    public KnowledgeBaseAgentManualTests(ITestOutputHelper output) : base(output) { }

    #region Documentation Search

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task SearchDocumentation_KeyVaultManagedIdentity_ShouldReturnResults()
    {
        // Arrange
        var message = "Find documentation about configuring Azure Key Vault with managed identities for our standard deployment patterns";

        // Act
        var response = await SendChatRequestAsync(message, "kb-search-keyvault-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "Key Vault", "managed identity", "configuration", "access");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task SearchDocumentation_NetworkingPatterns_ShouldReturnResults()
    {
        // Arrange
        var message = "Find documentation about hub-spoke network architecture patterns";

        // Act
        var response = await SendChatRequestAsync(message, "kb-search-network-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "hub", "spoke", "network", "VNet", "architecture");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task SearchDocumentation_ComplianceGuidelines_ShouldReturnResults()
    {
        // Arrange
        var message = "Search for FedRAMP compliance documentation and guidelines";

        // Act
        var response = await SendChatRequestAsync(message, "kb-search-compliance-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "FedRAMP", "compliance", "control", "authorization");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task SearchDocumentation_DisasterRecovery_ShouldReturnResults()
    {
        // Arrange
        var message = "Find disaster recovery procedures and runbooks";

        // Act
        var response = await SendChatRequestAsync(message, "kb-search-dr-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "disaster recovery", "DR", "failover", "recovery", "RTO", "RPO");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion

    #region Best Practices

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetBestPractices_AKSDeployment_ShouldReturnGuidance()
    {
        // Arrange
        var message = "What are our organization best practices for deploying containerized applications to AKS?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-bestpractice-aks-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "AKS", "Kubernetes", "container", "deployment", "pod");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetBestPractices_SecurityHardening_ShouldReturnGuidance()
    {
        // Arrange
        var message = "What are the security hardening best practices for Azure VMs?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-bestpractice-security-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "security", "VM", "hardening", "NSG", "encryption", "antimalware");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetBestPractices_CostOptimization_ShouldReturnGuidance()
    {
        // Arrange
        var message = "What are our recommended best practices for cost optimization in Azure?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-bestpractice-cost-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "cost", "optimization", "savings", "reserved", "right-siz");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetBestPractices_DatabaseDesign_ShouldReturnGuidance()
    {
        // Arrange
        var message = "What are our database design best practices for high-availability workloads?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-bestpractice-db-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "database", "availability", "replication", "failover", "backup");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion

    #region Architecture Guidance

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetArchitectureGuidance_Microservices_ShouldReturnPatterns()
    {
        // Arrange
        var message = "Explain our recommended microservices architecture patterns for Azure";

        // Act
        var response = await SendChatRequestAsync(message, "kb-arch-microservices-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "microservice", "API", "gateway", "container", "service");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetArchitectureGuidance_EventDriven_ShouldReturnPatterns()
    {
        // Arrange
        var message = "What are the recommended event-driven architecture patterns using Azure Service Bus and Event Grid?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-arch-events-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "event", "Service Bus", "Event Grid", "message", "queue", "topic");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetArchitectureGuidance_DataPipelines_ShouldReturnPatterns()
    {
        // Arrange
        var message = "Explain our standard data pipeline architecture using Azure Data Factory and Synapse";

        // Act
        var response = await SendChatRequestAsync(message, "kb-arch-data-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "Data Factory", "Synapse", "pipeline", "ETL", "data");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion

    #region Troubleshooting

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetTroubleshootingGuide_NetworkConnectivity_ShouldReturnSteps()
    {
        // Arrange
        var message = "How do I troubleshoot network connectivity issues between VNets?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-troubleshoot-network-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "network", "VNet", "connectivity", "troubleshoot", "peering", "NSG");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetTroubleshootingGuide_AKSPodFailures_ShouldReturnSteps()
    {
        // Arrange
        var message = "How to troubleshoot AKS pod failures and crash loops?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-troubleshoot-aks-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "pod", "crash", "kubectl", "log", "describe", "restart");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetTroubleshootingGuide_DatabasePerformance_ShouldReturnSteps()
    {
        // Arrange
        var message = "How to troubleshoot Azure SQL Database performance issues?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-troubleshoot-sql-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "SQL", "performance", "query", "DTU", "index", "monitor");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion

    #region Standards and Policies

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetStandards_NamingConventions_ShouldReturnPolicy()
    {
        // Arrange
        var message = "What are our Azure resource naming conventions and standards?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-standards-naming-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "naming", "convention", "prefix", "resource", "standard");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetStandards_TaggingPolicy_ShouldReturnPolicy()
    {
        // Arrange
        var message = "What are the required tags and tagging policy for Azure resources?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-standards-tagging-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "tag", "required", "policy", "environment", "owner", "cost center");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "KnowledgeBase")]
    public async Task GetStandards_SecurityBaseline_ShouldReturnPolicy()
    {
        // Arrange
        var message = "What is our security baseline configuration for Azure resources?";

        // Act
        var response = await SendChatRequestAsync(message, "kb-standards-security-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "security", "baseline", "encryption", "access", "policy");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion
}
