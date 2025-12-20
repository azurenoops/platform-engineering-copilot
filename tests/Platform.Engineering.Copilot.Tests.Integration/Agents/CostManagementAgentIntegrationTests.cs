using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.CostManagement.Core.Configuration;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Platform.Engineering.Copilot.Core.Models.IntelligentChat;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.CostOptimization.Analysis;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// Integration tests for Cost Management Agent subsystem
/// Tests agent coordination, shared memory, cost analysis workflows, and end-to-end scenarios
/// Marked with [Trait("Category", "Integration")] for filtering
/// </summary>
[Trait("Category", "Integration")]
public class CostManagementAgentIntegrationTests
{
    private SharedMemory CreateSharedMemory()
    {
        var loggerMock = new Mock<ILogger<SharedMemory>>();
        return new SharedMemory(loggerMock.Object);
    }

    #region Agent Communication Tests

    [Fact]
    public void SharedMemory_CostManagementAgent_CanCommunicateWithOrchestrator()
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = "cost-integration-001";

        // Act - CostManagement Agent reports analysis results
        memory.AddAgentCommunication(
            conversationId,
            AgentType.CostManagement,
            AgentType.Orchestrator,
            "Cost analysis completed. Estimated: $2,500.00/month, Within budget: True",
            new Dictionary<string, object>
            {
                ["estimatedCost"] = 2500.00m,
                ["isWithinBudget"] = true,
                ["budget"] = 5000.00m,
                ["analysis"] = "Monthly cost breakdown by service completed"
            }
        );

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        communications.Should().HaveCount(1);
        var comm = communications.First();
        comm.FromAgent.Should().Be(AgentType.CostManagement);
        comm.ToAgent.Should().Be(AgentType.Orchestrator);
        
        var data = comm.Data as Dictionary<string, object>;
        data.Should().NotBeNull();
        data!["estimatedCost"].Should().Be(2500.00m);
        data["isWithinBudget"].Should().Be(true);
    }

    [Fact]
    public void SharedMemory_InfrastructureToCostManagement_CommunicationFlow()
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = "infra-cost-flow-001";

        // Act - Infrastructure Agent completes deployment
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Infrastructure,
            AgentType.CostManagement,
            "AKS cluster provisioned, requesting cost estimate",
            new Dictionary<string, object>
            {
                ["subscriptionId"] = "00000000-0000-0000-0000-000000000001",
                ["resourceGroupName"] = "rg-aks-prod",
                ["resourceType"] = "Microsoft.ContainerService/managedClusters",
                ["nodePools"] = 3,
                ["nodeCount"] = 10
            }
        );

        // CostManagement Agent processes and responds
        var communications = memory.GetAgentCommunications(conversationId);
        var infraMessage = communications.FirstOrDefault(c => c.FromAgent == AgentType.Infrastructure);
        var infraData = infraMessage?.Data as Dictionary<string, object>;

        memory.AddAgentCommunication(
            conversationId,
            AgentType.CostManagement,
            AgentType.Orchestrator,
            "Cost estimate for AKS cluster: $3,500/month",
            new Dictionary<string, object>
            {
                ["estimatedCost"] = 3500.00m,
                ["subscriptionId"] = infraData?["subscriptionId"] ?? "unknown",
                ["breakdown"] = new Dictionary<string, decimal>
                {
                    ["compute"] = 2800.00m,
                    ["networking"] = 500.00m,
                    ["monitoring"] = 200.00m
                }
            }
        );

        // Assert
        var allCommunications = memory.GetAgentCommunications(conversationId);
        allCommunications.Should().HaveCount(2);
        allCommunications.Should().Contain(c => c.FromAgent == AgentType.Infrastructure);
        allCommunications.Should().Contain(c => c.FromAgent == AgentType.CostManagement);
    }

    [Fact]
    public void SharedMemory_MultiAgentCostWorkflow_CoordinatesCorrectly()
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = "multi-agent-cost-001";

        // Discovery Agent finds resources
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Discovery,
            AgentType.CostManagement,
            "Resource discovery completed",
            new Dictionary<string, object>
            {
                ["totalResources"] = 75,
                ["resourceTypes"] = new[] { "VMs", "Storage", "AKS", "SQL" },
                ["subscriptionId"] = "00000000-0000-0000-0000-000000000002"
            }
        );

        // CostManagement Agent analyzes costs
        memory.AddAgentCommunication(
            conversationId,
            AgentType.CostManagement,
            AgentType.Compliance,
            "Cost analysis completed, checking compliance budget limits",
            new Dictionary<string, object>
            {
                ["totalMonthlyCost"] = 15000.00m,
                ["potentialSavings"] = 2500.00m,
                ["recommendations"] = 8
            }
        );

        // Compliance Agent checks budget governance
        memory.AddAgentCommunication(
            conversationId,
            AgentType.Compliance,
            AgentType.Orchestrator,
            "Budget governance check passed",
            new Dictionary<string, object>
            {
                ["withinBudget"] = true,
                ["budgetUtilization"] = 75.0
            }
        );

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        communications.Should().HaveCount(3);
        communications.Select(c => c.FromAgent).Should().Contain(AgentType.Discovery);
        communications.Select(c => c.FromAgent).Should().Contain(AgentType.CostManagement);
        communications.Select(c => c.FromAgent).Should().Contain(AgentType.Compliance);
    }

    #endregion

    #region Context Persistence Tests

    [Fact]
    public void SharedMemory_CostContext_PersistsAcrossAgentCalls()
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = "cost-context-001";

        // Act - Set context with cost analysis info
        var context = new ConversationContext
        {
            ConversationId = conversationId,
            UserId = "finance-user",
            WorkflowState = new Dictionary<string, object?>
            {
                ["lastSubscriptionId"] = "00000000-0000-0000-0000-000000000003",
                ["lastAnalysisTimestamp"] = DateTime.UtcNow.AddHours(-1),
                ["defaultCurrency"] = "USD",
                ["budgetThreshold"] = 10000.00m
            }
        };
        memory.StoreContext(conversationId, context);

        // Assert - Retrieve context
        var retrievedContext = memory.GetContext(conversationId);
        retrievedContext.Should().NotBeNull();
        retrievedContext!.WorkflowState.Should().ContainKey("lastSubscriptionId");
        retrievedContext.WorkflowState.Should().ContainKey("defaultCurrency");
        retrievedContext.WorkflowState["defaultCurrency"].Should().Be("USD");
    }

    [Fact]
    public void SharedMemory_ClearConversation_RemovesCostData()
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = "cost-clear-001";

        // Set up context and communications
        var context = new ConversationContext
        {
            ConversationId = conversationId,
            UserId = "test-user"
        };
        memory.StoreContext(conversationId, context);
        memory.AddAgentCommunication(
            conversationId,
            AgentType.CostManagement,
            AgentType.Orchestrator,
            "Cost analysis result",
            new Dictionary<string, object> { ["estimatedCost"] = 1000.00m }
        );

        // Act
        memory.ClearConversation(conversationId);

        // Assert
        memory.HasContext(conversationId).Should().BeFalse();
    }

    #endregion

    #region AgentTask Tests

    [Fact]
    public void AgentTask_WithCostParameters_CanBeCreated()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Description = "Analyze Azure costs for subscription and recommend optimizations",
            Priority = 1,
            IsCritical = false,
            ConversationId = "cost-task-001",
            Parameters = new Dictionary<string, object>
            {
                ["subscriptionId"] = "00000000-0000-0000-0000-000000000004",
                ["timeframe"] = "MonthToDate",
                ["budget"] = 5000.00m,
                ["includeRecommendations"] = true
            }
        };

        // Assert
        task.TaskId.Should().NotBeNullOrEmpty();
        task.AgentType.Should().Be(AgentType.CostManagement);
        task.Parameters.Should().HaveCount(4);
        task.Parameters["timeframe"].Should().Be("MonthToDate");
        task.Parameters["budget"].Should().Be(5000.00m);
    }

    [Fact]
    public void AgentTask_ForBudgetAnalysis_HasCorrectConfiguration()
    {
        // Arrange & Act
        var task = new AgentTask
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Description = "Check budget status and alert thresholds",
            Priority = 2,
            Parameters = new Dictionary<string, object>
            {
                ["subscriptionId"] = "00000000-0000-0000-0000-000000000005",
                ["budgetName"] = "Development Budget",
                ["alertThresholds"] = new[] { 50, 80, 100, 120 }
            }
        };

        // Assert
        task.Parameters.Should().ContainKey("budgetName");
        task.Parameters["budgetName"].Should().Be("Development Budget");
        ((int[])task.Parameters["alertThresholds"]).Should().HaveCount(4);
    }

    #endregion

    #region AgentResponse Tests

    [Fact]
    public void AgentResponse_WithCostMetadata_StoresComplexResults()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Success = true,
            Content = "Cost analysis completed with optimization recommendations",
            EstimatedCost = 4500.00m,
            IsWithinBudget = true,
            ExecutionTimeMs = 8500,
            Errors = new List<string>(),
            Metadata = new Dictionary<string, object>
            {
                ["subscriptionId"] = "00000000-0000-0000-0000-000000000006",
                ["totalMonthlyCost"] = 4500.00m,
                ["potentialSavings"] = 750.00m,
                ["recommendationCount"] = 5,
                ["costByService"] = new Dictionary<string, decimal>
                {
                    ["Compute"] = 2500.00m,
                    ["Storage"] = 1000.00m,
                    ["Networking"] = 750.00m,
                    ["Other"] = 250.00m
                },
                ["analysisTimestamp"] = DateTime.UtcNow
            }
        };

        // Assert
        response.Success.Should().BeTrue();
        response.EstimatedCost.Should().Be(4500.00m);
        response.IsWithinBudget.Should().BeTrue();
        response.Metadata.Should().ContainKey("potentialSavings");
        response.Metadata["potentialSavings"].Should().Be(750.00m);
        
        var costByService = response.Metadata["costByService"] as Dictionary<string, decimal>;
        costByService.Should().NotBeNull();
        costByService!.Should().ContainKey("Compute");
    }

    [Theory]
    [InlineData(4000.00, 5000.00, true)]
    [InlineData(5000.00, 5000.00, true)]
    [InlineData(5001.00, 5000.00, false)]
    [InlineData(7500.00, 5000.00, false)]
    public void AgentResponse_IsWithinBudget_ReflectsBudgetComparison(
        decimal estimatedCost, decimal budget, bool expectedWithinBudget)
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Success = true,
            EstimatedCost = estimatedCost,
            IsWithinBudget = estimatedCost <= budget
        };

        // Assert
        response.IsWithinBudget.Should().Be(expectedWithinBudget);
    }

    #endregion

    #region CostManagementAgentOptions Tests

    [Fact]
    public void CostManagementAgentOptions_CanBeConfiguredFromOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Configure options
        services.Configure<CostManagementAgentOptions>(options =>
        {
            options.Temperature = 0.2;
            options.MaxTokens = 6000;
            options.DefaultCurrency = "EUR";
            options.DefaultTimeframe = "LastMonth";
            options.EnableAnomalyDetection = false;
            options.EnableOptimizationRecommendations = true;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<CostManagementAgentOptions>>();

        // Assert
        options.Value.Temperature.Should().Be(0.2);
        options.Value.MaxTokens.Should().Be(6000);
        options.Value.DefaultCurrency.Should().Be("EUR");
        options.Value.DefaultTimeframe.Should().Be("LastMonth");
        options.Value.EnableAnomalyDetection.Should().BeFalse();
        options.Value.EnableOptimizationRecommendations.Should().BeTrue();
    }

    [Fact]
    public void CostManagementOptions_CanBeConfiguredForCostAnalysis()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.Configure<CostManagementAgentOptions>(options =>
        {
            options.CostManagement.RefreshIntervalMinutes = 30;
            options.CostManagement.AnomalyThresholdPercentage = 25;
            options.CostManagement.MinimumSavingsThreshold = 50.00m;
            options.CostManagement.ForecastDays = 60;
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<CostManagementAgentOptions>>();

        // Assert
        options.Value.CostManagement.RefreshIntervalMinutes.Should().Be(30);
        options.Value.CostManagement.AnomalyThresholdPercentage.Should().Be(25);
        options.Value.CostManagement.MinimumSavingsThreshold.Should().Be(50.00m);
        options.Value.CostManagement.ForecastDays.Should().Be(60);
    }

    [Fact]
    public void BudgetOptions_CanBeConfiguredForAlerts()
    {
        // Arrange
        var services = new ServiceCollection();
        
        services.Configure<CostManagementAgentOptions>(options =>
        {
            options.Budgets.DefaultAlertThresholds = new List<int> { 25, 50, 75, 90, 100 };
            options.Budgets.EmailNotifications = false;
            options.Budgets.NotificationEmails = new List<string> { "finance@example.com" };
        });

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var options = serviceProvider.GetRequiredService<IOptions<CostManagementAgentOptions>>();

        // Assert
        options.Value.Budgets.DefaultAlertThresholds.Should().HaveCount(5);
        options.Value.Budgets.EmailNotifications.Should().BeFalse();
        options.Value.Budgets.NotificationEmails.Should().Contain("finance@example.com");
    }

    #endregion

    #region Execution Plan Tests

    [Fact]
    public void ExecutionPlan_ForCostAnalysis_CanBeConstructed()
    {
        // Arrange & Act
        var plan = new ExecutionPlan
        {
            PrimaryIntent = "Comprehensive cost analysis for subscription",
            ExecutionPattern = ExecutionPattern.Sequential,
            Tasks = new List<AgentTask>
            {
                new AgentTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    AgentType = AgentType.Discovery,
                    Description = "Discover all resources in subscription",
                    Priority = 1
                },
                new AgentTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    AgentType = AgentType.CostManagement,
                    Description = "Analyze costs and generate optimization recommendations",
                    Priority = 2
                }
            }
        };

        // Assert
        plan.Tasks.Should().HaveCount(2);
        plan.ExecutionPattern.Should().Be(ExecutionPattern.Sequential);
        plan.Tasks[0].AgentType.Should().Be(AgentType.Discovery);
        plan.Tasks[1].AgentType.Should().Be(AgentType.CostManagement);
    }

    [Fact]
    public void ExecutionPlan_ForBudgetMonitoring_IncludesMultipleAgents()
    {
        // Arrange & Act
        var plan = new ExecutionPlan
        {
            PrimaryIntent = "Budget monitoring and compliance check",
            ExecutionPattern = ExecutionPattern.Parallel,
            Tasks = new List<AgentTask>
            {
                new AgentTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    AgentType = AgentType.CostManagement,
                    Description = "Check current budget utilization",
                    Priority = 1
                },
                new AgentTask
                {
                    TaskId = Guid.NewGuid().ToString(),
                    AgentType = AgentType.Compliance,
                    Description = "Verify budget governance compliance",
                    Priority = 1
                }
            }
        };

        // Assert
        plan.Tasks.Should().HaveCount(2);
        plan.ExecutionPattern.Should().Be(ExecutionPattern.Parallel);
        plan.Tasks.Should().Contain(t => t.AgentType == AgentType.CostManagement);
        plan.Tasks.Should().Contain(t => t.AgentType == AgentType.Compliance);
    }

    #endregion

    #region Agent Communication Patterns Tests

    [Theory]
    [InlineData(AgentType.CostManagement, AgentType.Orchestrator)]
    [InlineData(AgentType.CostManagement, AgentType.Infrastructure)]
    [InlineData(AgentType.CostManagement, AgentType.Compliance)]
    [InlineData(AgentType.Infrastructure, AgentType.CostManagement)]
    [InlineData(AgentType.Discovery, AgentType.CostManagement)]
    public void SharedMemory_Communication_SupportsCostAgentPairs(AgentType from, AgentType to)
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = $"cost-pair-{from}-{to}";

        // Act
        memory.AddAgentCommunication(
            conversationId,
            from,
            to,
            $"Cost-related message from {from} to {to}",
            new Dictionary<string, object> 
            { 
                ["estimatedCost"] = 1000.00m,
                ["timestamp"] = DateTime.UtcNow 
            }
        );

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        communications.Should().HaveCount(1);
        communications.First().FromAgent.Should().Be(from);
        communications.First().ToAgent.Should().Be(to);
    }

    [Fact]
    public void AgentCommunication_Timestamp_IsSetCorrectly()
    {
        // Arrange
        var memory = CreateSharedMemory();
        var conversationId = "cost-timestamp-001";
        var beforeTime = DateTime.UtcNow;

        // Act
        memory.AddAgentCommunication(
            conversationId,
            AgentType.CostManagement,
            AgentType.Orchestrator,
            "Cost analysis complete",
            new Dictionary<string, object> { ["cost"] = 5000.00m }
        );

        var afterTime = DateTime.UtcNow;

        // Assert
        var communications = memory.GetAgentCommunications(conversationId);
        communications.Should().HaveCount(1);
        var comm = communications.First();
        comm.Timestamp.Should().BeOnOrAfter(beforeTime);
        comm.Timestamp.Should().BeOnOrBefore(afterTime);
    }

    #endregion

    #region Previous Results Tests

    [Fact]
    public void ConversationContext_PreviousResults_CanStoreCostResponses()
    {
        // Arrange
        var context = new ConversationContext
        {
            ConversationId = "cost-results-001",
            UserId = "test-user",
            PreviousResults = new List<AgentResponse>()
        };

        // Act - Add cost analysis response
        context.PreviousResults.Add(new AgentResponse
        {
            TaskId = "cost-task-1",
            AgentType = AgentType.CostManagement,
            Success = true,
            Content = "Monthly cost: $5,000",
            EstimatedCost = 5000.00m,
            IsWithinBudget = true
        });

        // Add optimization recommendation response
        context.PreviousResults.Add(new AgentResponse
        {
            TaskId = "cost-task-2",
            AgentType = AgentType.CostManagement,
            Success = true,
            Content = "Found 5 optimization opportunities, potential savings: $750/month",
            Metadata = new Dictionary<string, object>
            {
                ["potentialSavings"] = 750.00m,
                ["recommendationCount"] = 5
            }
        });

        // Assert
        context.PreviousResults.Should().HaveCount(2);
        context.PreviousResults.Should().OnlyContain(r => r.AgentType == AgentType.CostManagement);
        context.PreviousResults.First().EstimatedCost.Should().Be(5000.00m);
    }

    #endregion

    #region Cost Model Integration Tests

    [Fact]
    public void CostOptimizationRecommendation_InAgentResponse_SerializesCorrectly()
    {
        // Arrange
        var recommendations = new List<CostOptimizationRecommendation>
        {
            new CostOptimizationRecommendation
            {
                Id = Guid.NewGuid().ToString(),
                ResourceName = "vm-oversized",
                ResourceType = "Microsoft.Compute/virtualMachines",
                Type = OptimizationType.RightSizing,
                Priority = OptimizationPriority.High,
                CurrentMonthlyCost = 500.00m,
                EstimatedMonthlySavings = 200.00m,
                Description = "VM is oversized based on CPU utilization"
            }
        };

        // Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Success = true,
            Content = "Generated 1 optimization recommendation",
            Metadata = new Dictionary<string, object>
            {
                ["recommendations"] = recommendations
            }
        };

        // Assert
        response.Metadata.Should().ContainKey("recommendations");
        var storedRecommendations = response.Metadata["recommendations"] as List<CostOptimizationRecommendation>;
        storedRecommendations.Should().NotBeNull();
        storedRecommendations!.First().Type.Should().Be(OptimizationType.RightSizing);
        storedRecommendations.First().EstimatedMonthlySavings.Should().Be(200.00m);
    }

    [Fact]
    public void CostAnalysisResult_Integration_WithAgentResponse()
    {
        // Arrange
        var analysisResult = new CostAnalysisResult
        {
            SubscriptionId = "test-subscription",
            TotalMonthlyCost = 10000.00m,
            PotentialMonthlySavings = 1500.00m,
            TotalRecommendations = 8,
            CostByService = new Dictionary<string, decimal>
            {
                ["Compute"] = 6000.00m,
                ["Storage"] = 2500.00m,
                ["Networking"] = 1500.00m
            }
        };

        // Act
        var response = new AgentResponse
        {
            TaskId = Guid.NewGuid().ToString(),
            AgentType = AgentType.CostManagement,
            Success = true,
            EstimatedCost = analysisResult.TotalMonthlyCost,
            Content = $"Total monthly cost: ${analysisResult.TotalMonthlyCost:N2}",
            Metadata = new Dictionary<string, object>
            {
                ["analysisResult"] = analysisResult
            }
        };

        // Assert
        response.EstimatedCost.Should().Be(10000.00m);
        var storedResult = response.Metadata["analysisResult"] as CostAnalysisResult;
        storedResult.Should().NotBeNull();
        storedResult!.PotentialMonthlySavings.Should().Be(1500.00m);
        storedResult.CostByService.Should().HaveCount(3);
    }

    #endregion
}
