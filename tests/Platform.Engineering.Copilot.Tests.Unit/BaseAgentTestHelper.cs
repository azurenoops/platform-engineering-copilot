using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit;

/// <summary>
/// Helper for constructing test agents with mocked dependencies.
/// Provides a concrete TestAgent implementation of BaseAgent for testing.
/// </summary>
public static class BaseAgentTestHelper
{
    /// <summary>
    /// Create a TestAgent with default configuration.
    /// </summary>
    public static TestAgent CreateAgent(
        string agentId = "test-agent",
        string agentName = "Test Agent",
        string description = "A test agent",
        string[]? keywords = null,
        PimTier requiredPimTier = PimTier.None)
    {
        var logger = new Mock<ILogger<TestAgent>>().Object;
        return new TestAgent(agentId, agentName, description, keywords ?? ["test"], requiredPimTier, logger);
    }

    /// <summary>
    /// Create a TestAgent with a specific tool pre-registered.
    /// </summary>
    public static TestAgent CreateAgentWithTool(
        BaseTool tool,
        string agentId = "test-agent",
        string agentName = "Test Agent")
    {
        var agent = CreateAgent(agentId, agentName);
        agent.RegisterTool(tool);
        return agent;
    }

    /// <summary>
    /// Create a TestAgent with multiple tools pre-registered.
    /// </summary>
    public static TestAgent CreateAgentWithTools(
        IEnumerable<BaseTool> tools,
        string agentId = "test-agent",
        string agentName = "Test Agent",
        string[]? keywords = null)
    {
        var agent = CreateAgent(agentId, agentName, keywords: keywords);
        foreach (var tool in tools)
        {
            agent.RegisterTool(tool);
        }
        return agent;
    }
}

/// <summary>
/// Concrete implementation of BaseAgent for testing purposes.
/// </summary>
public class TestAgent : BaseAgent
{
    public TestAgent(
        string agentId,
        string agentName,
        string description,
        string[] keywords,
        PimTier requiredPimTier,
        ILogger logger)
        : base(logger)
    {
        AgentId = agentId;
        AgentName = agentName;
        Description = description;
        Keywords = keywords;
        RequiredPimTier = requiredPimTier;
    }

    public override string AgentId { get; }
    public override string AgentName { get; }
    public override string Description { get; }
    public override string[] Keywords { get; }
    public override PimTier RequiredPimTier { get; }

    public override string GetSystemPrompt() => $"You are {AgentName}, a test agent.";
}
