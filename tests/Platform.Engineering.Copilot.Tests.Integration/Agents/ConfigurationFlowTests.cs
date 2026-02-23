using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Configuration.Tools;
using Platform.Engineering.Copilot.Core.Agents;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// T087 — Integration test for configuration flow:
/// set subscription → set framework → show config → attempt assessment without sub → verify error.
/// </summary>
public class ConfigurationFlowTests
{
    [Fact]
    public async Task ConfigurationFlow_SetSubscription_SetFramework_ShowConfig()
    {
        var stateManager = new InMemoryAgentStateManager();
        var tool = new ConfigurationManageTool(
            new Mock<ILogger<ConfigurationManageTool>>().Object,
            stateManager);

        var subId = "12345678-1234-1234-1234-123456789abc";

        // Step 1: Set subscription
        var setSubResult = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "set_subscription",
            ["subscriptionId"] = subId
        });
        var setSubDoc = JsonDocument.Parse(setSubResult);
        setSubDoc.RootElement.GetProperty("status").GetString().Should().Be("success");

        // Step 2: Set framework
        var setFwResult = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "set_framework",
            ["framework"] = "FedRAMPHigh"
        });
        var setFwDoc = JsonDocument.Parse(setFwResult);
        setFwDoc.RootElement.GetProperty("status").GetString().Should().Be("success");

        // Step 3: Show configuration — both should be reflected
        var getResult = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "get_configuration"
        });
        var getDoc = JsonDocument.Parse(getResult);
        var data = getDoc.RootElement.GetProperty("data");

        data.GetProperty("subscriptionId").GetString().Should().Be(subId);
        data.GetProperty("framework").GetString().Should().Be("FedRAMPHigh");
    }

    [Fact]
    public async Task ConfigurationFlow_MissingSubscription_ConfigReturnsNull()
    {
        var stateManager = new InMemoryAgentStateManager();
        var tool = new ConfigurationManageTool(
            new Mock<ILogger<ConfigurationManageTool>>().Object,
            stateManager);

        // No subscription set — get_configuration should show null
        var result = await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "get_configuration"
        });
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        // subscriptionId should be null (omitted with WhenWritingNull)
        var data = doc.RootElement.GetProperty("data");
        if (data.TryGetProperty("subscriptionId", out var subProp))
        {
            subProp.ValueKind.Should().Be(JsonValueKind.Null);
        }
    }

    [Fact]
    public async Task ConfigurationFlow_SetAllPreferences()
    {
        var stateManager = new InMemoryAgentStateManager();
        var tool = new ConfigurationManageTool(
            new Mock<ILogger<ConfigurationManageTool>>().Object,
            stateManager);

        // Set all preferences
        var preferences = new Dictionary<string, string>
        {
            ["dryRunDefault"] = "false",
            ["defaultScanType"] = "resource",
            ["cloudEnvironment"] = "AzureCloud",
            ["region"] = "usgovarizona"
        };

        foreach (var (name, value) in preferences)
        {
            var result = await tool.ExecuteAsync(new Dictionary<string, object?>
            {
                ["action"] = "set_preference",
                ["preferenceName"] = name,
                ["preferenceValue"] = value
            });
            var doc = JsonDocument.Parse(result);
            doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        }

        // Verify all stored
        stateManager.GetString("config:dryRunDefault").Should().Be("false");
        stateManager.GetString("config:defaultScanType").Should().Be("resource");
        stateManager.GetString("config:cloudEnvironment").Should().Be("AzureCloud");
        stateManager.GetString("config:region").Should().Be("usgovarizona");
    }

    [Fact]
    public async Task ConfigurationFlow_OtherAgentsCanReadSharedState()
    {
        var stateManager = new InMemoryAgentStateManager();
        var tool = new ConfigurationManageTool(
            new Mock<ILogger<ConfigurationManageTool>>().Object,
            stateManager);

        // Configuration Agent sets state
        await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "set_subscription",
            ["subscriptionId"] = "abcdefab-abcd-abcd-abcd-abcdefabcdef"
        });

        await tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "set_framework",
            ["framework"] = "NIST80053"
        });

        // Another agent reads the shared state
        stateManager.GetString("config:subscriptionId")
            .Should().Be("abcdefab-abcd-abcd-abcd-abcdefabcdef");
        stateManager.GetString("config:framework")
            .Should().Be("NIST80053");

        // Verify keys with prefix
        stateManager.GetKeysWithPrefix("config:")
            .Should().Contain("config:subscriptionId")
            .And.Contain("config:framework")
            .And.Contain("config:lastUpdated");
    }
}
