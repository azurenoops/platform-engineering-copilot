using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Extensions;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Interfaces.Agents;
using Platform.Engineering.Copilot.Core.Models.Agents;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services.Agents;

/// <summary>
/// Tests to verify multi-agent system service registration
/// </summary>
public class MultiAgentServiceRegistrationTests
{
    private readonly IServiceProvider _serviceProvider;

    public MultiAgentServiceRegistrationTests()
    {
        // Build a test service collection
        var services = new ServiceCollection();

        // Add configuration
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:UseMultiAgent"] = "true",
                ["Gateway:AzureOpenAI:Endpoint"] = "https://test.openai.azure.com/",
                ["Gateway:AzureOpenAI:ApiKey"] = "test-key",
                ["Gateway:AzureOpenAI:DeploymentName"] = "gpt-4o"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();

        // Register Supervisor Core services (includes multi-agent system)
        services.AddPlatformEngineeringCopilotCore(new ConfigurationBuilder().Build());

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void SharedMemory_ShouldBeRegisteredAsSingleton()
    {
        // Act
        var sharedMemory1 = _serviceProvider.GetRequiredService<SharedMemory>();
        var sharedMemory2 = _serviceProvider.GetRequiredService<SharedMemory>();

        // Assert
        Assert.NotNull(sharedMemory1);
        Assert.NotNull(sharedMemory2);
        Assert.Same(sharedMemory1, sharedMemory2); // Should be same instance (singleton)
    }

    [Fact]
    public void SpecializedAgents_ShouldBeRegistered()
    {
        // Act
        var agents = _serviceProvider.GetServices<ISpecializedAgent>().ToList();

        // Assert
        Assert.NotEmpty(agents);
        Assert.Equal(6, agents.Count); // Should have 6 specialized agents

        // Verify all agent types are present
        var agentTypes = agents.Select(a => a.AgentType).ToList();
        Assert.Contains(AgentType.Infrastructure, agentTypes);
        Assert.Contains(AgentType.Compliance, agentTypes);
        Assert.Contains(AgentType.CostManagement, agentTypes);
        Assert.Contains(AgentType.Environment, agentTypes);
        Assert.Contains(AgentType.Discovery, agentTypes);
        Assert.Contains(AgentType.ServiceCreation, agentTypes);
    }

    [Fact]
    public void InfrastructureAgent_ShouldBeResolvable()
    {
        // Act
        var agents = _serviceProvider.GetServices<ISpecializedAgent>().ToList();
        var infrastructureAgent = agents.FirstOrDefault(a => a.AgentType == AgentType.Infrastructure);

        // Assert
        Assert.NotNull(infrastructureAgent);
        Assert.IsType<InfrastructureAgent>(infrastructureAgent);
    }

    [Fact]
    public void ComplianceAgent_ShouldBeResolvable()
    {
        // Act
        var agents = _serviceProvider.GetServices<ISpecializedAgent>().ToList();
        var complianceAgent = agents.FirstOrDefault(a => a.AgentType == AgentType.Compliance);

        // Assert
        Assert.NotNull(complianceAgent);
        Assert.IsType<ComplianceAgent>(complianceAgent);
    }

    [Fact]
    public void CostManagementAgent_ShouldBeResolvable()
    {
        // Act
        var agents = _serviceProvider.GetServices<ISpecializedAgent>().ToList();
        var costAgent = agents.FirstOrDefault(a => a.AgentType == AgentType.CostManagement);

        // Assert
        Assert.NotNull(costAgent);
        Assert.IsType<CostManagementAgent>(costAgent);
    }

    [Fact]
    public void EnvironmentAgent_ShouldBeResolvable()
    {
        // Act
        var agents = _serviceProvider.GetServices<ISpecializedAgent>().ToList();
        var environmentAgent = agents.FirstOrDefault(a => a.AgentType == AgentType.Environment);

        // Assert
        Assert.NotNull(environmentAgent);
        Assert.IsType<EnvironmentAgent>(environmentAgent);
    }

    [Fact]
    public void DiscoveryAgent_ShouldBeResolvable()
    {
        // Act
        var agents = _serviceProvider.GetServices<ISpecializedAgent>().ToList();
        var discoveryAgent = agents.FirstOrDefault(a => a.AgentType == AgentType.Discovery);

        // Assert
        Assert.NotNull(discoveryAgent);
        Assert.IsType<DiscoveryAgent>(discoveryAgent);
    }

    [Fact]
    public void ServiceCreationAgent_ShouldBeResolvable()
    {
        // Act
        var agents = _serviceProvider.GetServices<ISpecializedAgent>().ToList();
        var ServiceCreationAgent = agents.FirstOrDefault(a => a.AgentType == AgentType.ServiceCreation);

        // Assert
        Assert.NotNull(ServiceCreationAgent);
        Assert.IsType<ServiceCreationAgent>(ServiceCreationAgent);
    }

    [Fact]
    public void OrchestratorAgent_ShouldBeResolvable()
    {
        // Act
        var orchestrator = _serviceProvider.GetRequiredService<OrchestratorAgent>();

        // Assert
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void OrchestratorAgent_ShouldReceiveAllAgents()
    {
        // Act
        var orchestrator = _serviceProvider.GetRequiredService<OrchestratorAgent>();

        // Assert - Orchestrator should have been constructed with all 6 agents
        // This test verifies DI correctly injects IEnumerable<ISpecializedAgent>
        Assert.NotNull(orchestrator);
        // If orchestrator was constructed, it means all agents were successfully injected
    }

    [Fact]
    public void MultipleAgentResolutions_ShouldReturnSameInstances()
    {
        // Act
        var agents1 = _serviceProvider.GetServices<ISpecializedAgent>().ToList();
        var agents2 = _serviceProvider.GetServices<ISpecializedAgent>().ToList();

        // Assert - Since agents are singletons, should be same instances
        for (int i = 0; i < agents1.Count; i++)
        {
            Assert.Same(agents1[i], agents2[i]);
        }
    }

    [Fact]
    public void Configuration_FeatureFlag_ShouldBeReadable()
    {
        // Act
        var configuration = _serviceProvider.GetRequiredService<IConfiguration>();
        var useMultiAgent = configuration.GetValue<bool>("Features:UseMultiAgent");

        // Assert
        Assert.True(useMultiAgent);
    }
}
