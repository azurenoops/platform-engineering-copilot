using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Configuration;
using Platform.Engineering.Copilot.Agents.Configuration.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T085 — ConfigurationAgent tests: extends BaseAgent, registers configuration_manage tool,
/// loads configuration.prompt.txt.
/// </summary>
public class ConfigurationAgentTests
{
    private readonly ConfigurationAgent _agent;

    public ConfigurationAgentTests()
    {
        var stateManager = new InMemoryAgentStateManager();
        var tool = new ConfigurationManageTool(
            new Mock<ILogger<ConfigurationManageTool>>().Object,
            stateManager);
        _agent = new ConfigurationAgent(
            new Mock<ILogger<ConfigurationAgent>>().Object,
            new BaseTool[] { tool });
    }

    [Fact]
    public void AgentId_IsConfiguration() => _agent.AgentId.Should().Be("configuration");

    [Fact]
    public void AgentName_IsCorrect() => _agent.AgentName.Should().Be("Configuration Agent");

    [Fact]
    public void Description_ContainsSettings() => _agent.Description.Should().Contain("settings");

    [Fact]
    public void RequiredPimTier_IsNone() => _agent.RequiredPimTier.Should().Be(PimTier.None);

    [Fact]
    public void IsBaseAgent() => _agent.Should().BeAssignableTo<BaseAgent>();

    [Fact]
    public void SystemPrompt_IsLoaded()
    {
        var prompt = _agent.GetSystemPrompt();
        prompt.Should().NotBeNullOrWhiteSpace();
        prompt.Should().Contain("Configuration");
    }

    [Fact]
    public void Keywords_ContainsConfigurationTerms()
    {
        _agent.Keywords.Should().Contain("configure");
        _agent.Keywords.Should().Contain("settings");
        _agent.Keywords.Should().Contain("subscription");
    }

    [Fact]
    public void RegistersTool_ConfigurationManage()
    {
        var tools = _agent.GetToolMetadata();
        tools.Should().ContainSingle(t => t.Name == "configuration_manage");
    }

    [Fact]
    public async Task ExecuteToolAsync_ConfigurationManage_Works()
    {
        var result = await _agent.ExecuteToolAsync(
            "configuration_manage",
            new Dictionary<string, object?> { ["action"] = "get_configuration" });

        result.Should().Contain("success");
    }
}
