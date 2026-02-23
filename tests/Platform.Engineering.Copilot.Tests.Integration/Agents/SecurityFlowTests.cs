using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Security;
using Platform.Engineering.Copilot.Agents.Security.Tools;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// T136 — Integration tests for Security agent multi-step flows.
/// </summary>
public class SecurityFlowTests
{
    private readonly GetSecureScoreTool _secureScore = new(new Mock<ILogger<GetSecureScoreTool>>().Object);
    private readonly GetSecurityRecommendationsTool _recommendations = new(new Mock<ILogger<GetSecurityRecommendationsTool>>().Object);
    private readonly ManageSecurityPolicyTool _policy = new(new Mock<ILogger<ManageSecurityPolicyTool>>().Object);

    [Fact]
    public async Task SecureScore_Then_Recommendations_Then_Policy_Flow()
    {
        // Step 1: Get secure score overview
        var scoreResult = await _secureScore.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001",
            ["includeControls"] = true
        });
        var score = JsonDocument.Parse(scoreResult);
        score.RootElement.GetProperty("status").GetString().Should().Be("success");
        score.RootElement.GetProperty("data").GetProperty("overallScore").GetDouble().Should().BeGreaterThan(0);
        score.RootElement.GetProperty("data").GetProperty("controls").GetArrayLength().Should().BeGreaterThan(0);

        // Step 2: Get security recommendations
        var recsResult = await _recommendations.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001",
            ["severity"] = "High"
        });
        var recs = JsonDocument.Parse(recsResult);
        recs.RootElement.GetProperty("status").GetString().Should().Be("success");
        recs.RootElement.GetProperty("data").GetProperty("recommendations").GetArrayLength().Should().BeGreaterThan(0);

        // Step 3: View current security policies
        var policyResult = await _policy.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "view",
            ["subscriptionId"] = "sub-001"
        });
        var policies = JsonDocument.Parse(policyResult);
        policies.RootElement.GetProperty("status").GetString().Should().Be("success");
        policies.RootElement.GetProperty("data").GetProperty("policies").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Policy_Enable_Then_Disable_Flow()
    {
        // Step 1: Enable a policy
        var enableResult = await _policy.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "enable",
            ["subscriptionId"] = "sub-001",
            ["policyName"] = "AzureDefenderForServers"
        });
        var enabled = JsonDocument.Parse(enableResult);
        enabled.RootElement.GetProperty("status").GetString().Should().Be("success");

        // Step 2: Disable the policy
        var disableResult = await _policy.ExecuteAsync(new Dictionary<string, object?>
        {
            ["action"] = "disable",
            ["subscriptionId"] = "sub-001",
            ["policyName"] = "AzureDefenderForServers"
        });
        var disabled = JsonDocument.Parse(disableResult);
        disabled.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task Recommendations_Map_To_NIST_Controls()
    {
        var result = await _recommendations.ExecuteAsync(new Dictionary<string, object?>
        {
            ["subscriptionId"] = "sub-001"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");

        var recs = doc.RootElement.GetProperty("data").GetProperty("recommendations");
        recs.GetArrayLength().Should().BeGreaterThan(0);

        // Verify NIST framework mapping present
        foreach (var rec in recs.EnumerateArray())
        {
            rec.TryGetProperty("framework", out _).Should().BeTrue(
                "each recommendation should include NIST framework mappings");
        }
    }

    [Fact]
    public async Task Agent_Registers_All_Three_Tools()
    {
        var agent = new SecurityAgent(
            new Mock<ILogger<SecurityAgent>>().Object,
            _secureScore, _recommendations, _policy);

        var tools = agent.GetToolMetadata();
        tools.Should().HaveCount(3);
        tools.Select(t => t.Name).Should().Contain("get_secure_score");
        tools.Select(t => t.Name).Should().Contain("get_security_recommendations");
        tools.Select(t => t.Name).Should().Contain("manage_security_policy");
    }
}
