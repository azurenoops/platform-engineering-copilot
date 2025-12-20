using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Platform.Engineering.Copilot.Tests.Manual.Agents;

/// <summary>
/// Manual tests for Infrastructure Agent via MCP HTTP API.
/// Tests IaC generation, Azure resource management, and template creation.
/// 
/// Prerequisites:
/// - MCP server running in HTTP mode: dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 5100
/// - Azure CLI authenticated (for resource queries)
/// </summary>
public class InfrastructureAgentManualTests : McpHttpTestBase
{
    private static readonly string[] AcceptableIntents = { "infrastructure", "multi_agent", "agent_execution", "orchestrat" };

    public InfrastructureAgentManualTests(ITestOutputHelper output) : base(output) { }

    #region Bicep Template Generation

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task GenerateBicepTemplate_ForAKSCluster_ShouldReturnValidTemplate()
    {
        // Arrange
        var message = "Generate a Bicep template for deploying an Azure Kubernetes Service cluster with 3 nodes in the East US region with managed identity enabled";

        // Act
        var response = await SendChatRequestAsync(message, "infra-bicep-aks-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Template structure validation
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        AssertInfrastructureTemplateStructure(response, "bicep");
        AssertResponseContains(response, "resource", "Microsoft.ContainerService/managedClusters", "identity");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task GenerateBicepTemplate_ForStorageAccount_ShouldReturnValidTemplate()
    {
        // Arrange
        var message = "Generate a Bicep template for an Azure Storage Account with blob encryption and private endpoint";

        // Act
        var response = await SendChatRequestAsync(message, "infra-bicep-storage-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Template structure validation
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        AssertInfrastructureTemplateStructure(response, "bicep");
        AssertResponseContains(response, "resource", "Microsoft.Storage/storageAccounts", "encryption", "privateEndpoint");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task GenerateBicepTemplate_ForVirtualNetwork_ShouldReturnValidTemplate()
    {
        // Arrange
        var message = "Create a Bicep template for a virtual network with 3 subnets: web, app, and data tiers with NSGs";

        // Act
        var response = await SendChatRequestAsync(message, "infra-bicep-vnet-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Template structure validation
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        AssertInfrastructureTemplateStructure(response, "bicep");
        AssertResponseContains(response, "resource", "Microsoft.Network/virtualNetworks", "subnet", "networkSecurityGroup");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion

    #region Terraform Template Generation

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task GenerateTerraformTemplate_ForAzureSQLDatabase_ShouldReturnValidConfiguration()
    {
        // Arrange
        var message = "Create a Terraform configuration for a highly available Azure SQL Database with geo-replication and auto-failover groups";

        // Act
        var response = await SendChatRequestAsync(message, "infra-terraform-sql-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Template structure validation
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        AssertInfrastructureTemplateStructure(response, "terraform");
        AssertResponseContains(response, "resource", "azurerm_mssql", "failover", "replication");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task GenerateTerraformTemplate_ForAKSCluster_ShouldReturnValidConfiguration()
    {
        // Arrange
        var message = "Create Terraform configuration for an AKS cluster with auto-scaling node pools and Azure CNI networking";

        // Act
        var response = await SendChatRequestAsync(message, "infra-terraform-aks-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Template structure validation
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        AssertInfrastructureTemplateStructure(response, "terraform");
        AssertResponseContains(response, "resource", "azurerm_kubernetes_cluster", "node_pool", "auto_scal");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion

    #region Azure Resource Queries

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task QueryResources_ListVirtualMachines_ShouldReturnResults()
    {
        // Arrange
        var message = "List all virtual machines in my subscription that are currently running and have more than 4 CPU cores";

        // Act
        var response = await SendChatRequestAsync(message, "infra-query-vms-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "VM", "virtualMachine", "running", "CPU", "core");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task QueryResources_ListStorageAccounts_ShouldReturnResults()
    {
        // Arrange
        var message = "List all storage accounts in my subscription and show their access tier and redundancy type";

        // Act
        var response = await SendChatRequestAsync(message, "infra-query-storage-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "storage", "tier", "redundancy", "LRS", "GRS", "ZRS");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion

    #region Network Topology Analysis

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task AnalyzeNetworkTopology_IdentifyExposedVMs_ShouldReturnFindings()
    {
        // Arrange
        var message = "Analyze the network topology and identify any virtual machines that are exposed directly to the internet without a network security group";

        // Act
        var response = await SendChatRequestAsync(message, "infra-network-exposed-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "network", "VM", "exposed", "NSG", "security", "internet");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task AnalyzeNetworkTopology_ShowVNetPeerings_ShouldReturnResults()
    {
        // Arrange
        var message = "Show all virtual network peering connections and their status";

        // Act
        var response = await SendChatRequestAsync(message, "infra-network-peering-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 50);
        AssertResponseContains(response, "peering", "VNet", "status", "connected");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion

    #region Multi-Region Deployment

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task GenerateMultiRegionDeployment_WithFrontDoor_ShouldReturnCompleteTemplate()
    {
        // Arrange
        var message = "Generate infrastructure templates for a multi-region deployment with Azure Front Door, App Service in East US and West US, and a shared Azure SQL Database with geo-replication";

        // Act
        var response = await SendChatRequestAsync(message, "infra-multiregion-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        AssertResponseContains(response, "Front Door", "App Service", "SQL", "region", "geo-replication");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 90000);
    }

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task GenerateMultiRegionDeployment_WithTrafficManager_ShouldReturnCompleteTemplate()
    {
        // Arrange
        var message = "Create a disaster recovery infrastructure template with Traffic Manager failover between primary (East US) and secondary (West US) regions";

        // Act
        var response = await SendChatRequestAsync(message, "infra-multiregion-dr-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Content validation
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        AssertResponseContains(response, "Traffic Manager", "failover", "primary", "secondary", "region");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 90000);
    }

    #endregion

    #region ARM Template Operations

    [ManualTestFact]
    [Trait("Category", "Manual")]
    [Trait("Agent", "Infrastructure")]
    public async Task GenerateARMTemplate_ForKeyVault_ShouldReturnValidTemplate()
    {
        // Arrange
        var message = "Generate an ARM template for Azure Key Vault with soft delete, purge protection, and RBAC access";

        // Act
        var response = await SendChatRequestAsync(message, "infra-arm-keyvault-001");

        // Assert - Basic validation
        AssertSuccessfulResponse(response);
        AssertIntentMatches(response, AcceptableIntents);
        
        // Assert - Template structure validation
        AssertResponseHasMeaningfulContent(response, minLength: 100);
        AssertInfrastructureTemplateStructure(response, "arm");
        AssertResponseContains(response, "KeyVault", "softDelete", "purgeProtection", "RBAC", "enableRbacAuthorization");
        
        // Assert - Performance
        AssertPerformance(response, maxMilliseconds: 60000);
    }

    #endregion
}
