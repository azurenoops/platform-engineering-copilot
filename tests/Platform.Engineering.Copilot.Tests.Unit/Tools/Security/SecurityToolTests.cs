using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Security.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.Security;

/// <summary>
/// T136 — Unit tests for Security tools.
/// </summary>
public class SecurityToolTests
{
    private readonly GetSecureScoreTool _scoreTool = new(new Mock<ILogger<GetSecureScoreTool>>().Object);
    private readonly GetSecurityRecommendationsTool _recsTool = new(new Mock<ILogger<GetSecurityRecommendationsTool>>().Object);
    private readonly ManageSecurityPolicyTool _policyTool = new(new Mock<ILogger<ManageSecurityPolicyTool>>().Object);

    // ─── get_secure_score ───
    [Fact]
    public async Task SecureScore_Returns_Score()
    {
        var result = await _scoreTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("overallScore").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SecureScore_Includes_Controls()
    {
        var result = await _scoreTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001",
            ["includeControls"] = true
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("controls").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SecureScore_Missing_SubscriptionId_Returns_Error()
    {
        var result = await _scoreTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = ""
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    [Fact] public void SecureScore_PIM_Read() => _scoreTool.PimTierRequired.Should().Be(PimTier.Read);

    // ─── get_security_recommendations ───
    [Fact]
    public async Task Recommendations_Returns_Items()
    {
        var result = await _recsTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("recommendations").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Recommendations_Filters_By_Severity()
    {
        var result = await _recsTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001",
            ["severity"] = "High"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("severityFilter").GetString().Should().Be("High");
    }

    [Fact]
    public async Task Recommendations_Reports_Progress()
    {
        var progress = new List<ProgressUpdate>();
        await _recsTool.ExecuteAsync(
            new Dictionary<string, object?> { ["subscriptionId"] = "sub-001" },
            new Progress<ProgressUpdate>(p => progress.Add(p)));
        await Task.Delay(50);
        progress.Should().NotBeEmpty();
    }

    // ─── manage_security_policy ───
    [Fact]
    public async Task Policy_View_Returns_Policies()
    {
        var result = await _policyTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "view",
            ["subscriptionId"] = "sub-001"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("policies").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Policy_Enable_Returns_Success()
    {
        var result = await _policyTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "enable",
            ["subscriptionId"] = "sub-001",
            ["policyName"] = "AzureDefenderForServers"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task Policy_Disable_Returns_Success()
    {
        var result = await _policyTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "disable",
            ["subscriptionId"] = "sub-001",
            ["policyName"] = "AzureDefenderForServers"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task Policy_UnknownAction_Still_Returns_Response()
    {
        var result = await _policyTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "delete",
            ["subscriptionId"] = "sub-001"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task Policy_Enable_Without_PolicyName_Returns_Error()
    {
        var result = await _policyTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "enable",
            ["subscriptionId"] = "sub-001"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }
}
