using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Configuration.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.Configuration;

/// <summary>
/// T086 — ConfigurationManageTool tests: all 5 sub-actions, IAgentStateManager writes
/// with config: prefix, validation (GUID format, enum values), all 6 error codes.
/// </summary>
public class ConfigurationManageToolTests
{
    private readonly ConfigurationManageTool _tool;
    private readonly InMemoryAgentStateManager _state;

    public ConfigurationManageToolTests()
    {
        _state = new InMemoryAgentStateManager();
        _tool = new ConfigurationManageTool(
            new Mock<ILogger<ConfigurationManageTool>>().Object,
            _state);
    }

    [Fact]
    public void Name_IsCorrect() => _tool.Name.Should().Be("configuration_manage");

    [Fact]
    public void RequiresAuthentication_IsFalse() => _tool.RequiresAuthentication.Should().BeFalse();

    [Fact]
    public void PimTierRequired_IsNone() => _tool.PimTierRequired.Should().Be(PimTier.None);

    // ─── get_configuration ────────────────────────────────────────────

    [Fact]
    public async Task GetConfiguration_ReturnsDefaults()
    {
        var result = await Execute("get_configuration");
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("cloudEnvironment").GetString().Should().Be("AzureGovernment");
        data.GetProperty("dryRunDefault").GetString().Should().Be("true");
        data.GetProperty("defaultScanType").GetString().Should().Be("combined");
        data.GetProperty("region").GetString().Should().Be("usgovvirginia");
    }

    [Fact]
    public async Task GetConfiguration_AfterSetSubscription_ReturnsIt()
    {
        var subId = "12345678-1234-1234-1234-123456789abc";
        await Execute("set_subscription", ("subscriptionId", subId));

        var result = await Execute("get_configuration");
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("subscriptionId").GetString()
            .Should().Be(subId);
    }

    // ─── set_subscription ─────────────────────────────────────────────

    [Fact]
    public async Task SetSubscription_ValidGuid_Succeeds()
    {
        var subId = "12345678-1234-1234-1234-123456789abc";
        var result = await Execute("set_subscription", ("subscriptionId", subId));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("subscriptionId").GetString()
            .Should().Be(subId);

        _state.GetString("config:subscriptionId").Should().Be(subId);
    }

    [Fact]
    public async Task SetSubscription_InvalidGuid_ReturnsError()
    {
        var result = await Execute("set_subscription", ("subscriptionId", "not-a-guid"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_SUBSCRIPTION_ID");
    }

    [Fact]
    public async Task SetSubscription_Missing_ReturnsError()
    {
        var result = await Execute("set_subscription");
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("MISSING_REQUIRED_PARAM");
    }

    [Fact]
    public async Task SetSubscription_TracksPreviousValue()
    {
        var first = "11111111-1111-1111-1111-111111111111";
        var second = "22222222-2222-2222-2222-222222222222";

        await Execute("set_subscription", ("subscriptionId", first));
        var result = await Execute("set_subscription", ("subscriptionId", second));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("previousValue").GetString()
            .Should().Be(first);
    }

    // ─── set_framework ────────────────────────────────────────────────

    [Theory]
    [InlineData("NIST80053")]
    [InlineData("FedRAMPHigh")]
    [InlineData("FedRAMPModerate")]
    [InlineData("DoDIL5")]
    public async Task SetFramework_ValidValues_Succeeds(string framework)
    {
        var result = await Execute("set_framework", ("framework", framework));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        _state.GetString("config:framework").Should().Be(framework);
    }

    [Fact]
    public async Task SetFramework_CaseInsensitive()
    {
        var result = await Execute("set_framework", ("framework", "nist80053"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        _state.GetString("config:framework").Should().Be("NIST80053");
    }

    [Fact]
    public async Task SetFramework_Invalid_ReturnsError()
    {
        var result = await Execute("set_framework", ("framework", "INVALID"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_FRAMEWORK");
    }

    [Fact]
    public async Task SetFramework_Missing_ReturnsError()
    {
        var result = await Execute("set_framework");
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("MISSING_REQUIRED_PARAM");
    }

    // ─── set_baseline ─────────────────────────────────────────────────

    [Theory]
    [InlineData("High")]
    [InlineData("Moderate")]
    [InlineData("Low")]
    public async Task SetBaseline_ValidValues_Succeeds(string baseline)
    {
        var result = await Execute("set_baseline", ("baseline", baseline));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        _state.GetString("config:baseline").Should().Be(baseline);
    }

    [Fact]
    public async Task SetBaseline_Invalid_ReturnsError()
    {
        var result = await Execute("set_baseline", ("baseline", "Ultra"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_BASELINE");
    }

    // ─── set_preference ───────────────────────────────────────────────

    [Fact]
    public async Task SetPreference_DryRunDefault_Succeeds()
    {
        var result = await Execute("set_preference",
            ("preferenceName", "dryRunDefault"), ("preferenceValue", "false"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        _state.GetString("config:dryRunDefault").Should().Be("false");
    }

    [Fact]
    public async Task SetPreference_DefaultScanType_Succeeds()
    {
        var result = await Execute("set_preference",
            ("preferenceName", "defaultScanType"), ("preferenceValue", "resource"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task SetPreference_CloudEnvironment_Succeeds()
    {
        var result = await Execute("set_preference",
            ("preferenceName", "cloudEnvironment"), ("preferenceValue", "AzureCloud"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task SetPreference_Region_AcceptsAnyString()
    {
        var result = await Execute("set_preference",
            ("preferenceName", "region"), ("preferenceValue", "usgovarizona"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task SetPreference_InvalidName_ReturnsError()
    {
        var result = await Execute("set_preference",
            ("preferenceName", "unknownPref"), ("preferenceValue", "value"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_PREFERENCE_NAME");
    }

    [Fact]
    public async Task SetPreference_InvalidValue_ReturnsError()
    {
        var result = await Execute("set_preference",
            ("preferenceName", "dryRunDefault"), ("preferenceValue", "maybe"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_PREFERENCE_VALUE");
    }

    [Fact]
    public async Task SetPreference_MissingName_ReturnsError()
    {
        var result = await Execute("set_preference", ("preferenceValue", "true"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("MISSING_REQUIRED_PARAM");
    }

    [Fact]
    public async Task SetPreference_MissingValue_ReturnsError()
    {
        var result = await Execute("set_preference", ("preferenceName", "dryRunDefault"));
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("MISSING_REQUIRED_PARAM");
    }

    // ─── Response envelope ────────────────────────────────────────────

    [Fact]
    public async Task Execute_ReturnsResponseEnvelope()
    {
        var result = await Execute("get_configuration");
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.TryGetProperty("status", out _).Should().BeTrue();
        root.TryGetProperty("data", out _).Should().BeTrue();
        root.TryGetProperty("metadata", out _).Should().BeTrue();
        root.GetProperty("metadata").GetProperty("toolName").GetString()
            .Should().Be("configuration_manage");
    }

    // ─── State manager writes ─────────────────────────────────────────

    [Fact]
    public async Task SetSubscription_WritesWithConfigPrefix()
    {
        await Execute("set_subscription",
            ("subscriptionId", "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));

        _state.ContainsKey("config:subscriptionId").Should().BeTrue();
        _state.ContainsKey("config:lastUpdated").Should().BeTrue();
    }

    [Fact]
    public async Task SetFramework_WritesWithConfigPrefix()
    {
        await Execute("set_framework", ("framework", "DoDIL5"));

        _state.ContainsKey("config:framework").Should().BeTrue();
    }

    [Fact]
    public async Task SetBaseline_WritesWithConfigPrefix()
    {
        await Execute("set_baseline", ("baseline", "High"));

        _state.ContainsKey("config:baseline").Should().BeTrue();
    }

    // ─── Helper ───────────────────────────────────────────────────────

    private Task<string> Execute(string action, params (string key, object value)[] extraParams)
    {
        var parameters = new Dictionary<string, object?> { ["action"] = action };
        foreach (var (key, value) in extraParams)
        {
            parameters[key] = value;
        }
        return _tool.ExecuteAsync(parameters);
    }
}
