using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Environment;
using Platform.Engineering.Copilot.Agents.Environment.Tools;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// T135 — Integration tests for Environment agent multi-step flows.
/// </summary>
public class EnvironmentFlowTests
{
    private readonly CloneEnvironmentTool _clone = new(new Mock<ILogger<CloneEnvironmentTool>>().Object);
    private readonly DetectDriftTool _drift = new(new Mock<ILogger<DetectDriftTool>>().Object);
    private readonly CompareEnvironmentsTool _compare = new(new Mock<ILogger<CompareEnvironmentsTool>>().Object);
    private readonly PromoteEnvironmentTool _promote = new(new Mock<ILogger<PromoteEnvironmentTool>>().Object);
    private readonly ListEnvironmentsTool _list = new(new Mock<ILogger<ListEnvironmentsTool>>().Object);
    private readonly GetEnvironmentStatusTool _status = new(new Mock<ILogger<GetEnvironmentStatusTool>>().Object);
    private readonly CreateEnvironmentTool _create = new(new Mock<ILogger<CreateEnvironmentTool>>().Object);
    private readonly DeleteEnvironmentTool _delete = new(new Mock<ILogger<DeleteEnvironmentTool>>().Object);
    private readonly GetEnvironmentHistoryTool _history = new(new Mock<ILogger<GetEnvironmentHistoryTool>>().Object);
    private readonly ValidateEnvironmentTool _validate = new(new Mock<ILogger<ValidateEnvironmentTool>>().Object);

    [Fact]
    public async Task Create_Then_Validate_Then_Promote_Flow()
    {
        // Step 1: Create new environment
        var createResult = await _create.ExecuteAsync(new Dictionary<string, object?>
        {
            ["name"] = "platform-qa",
            ["tier"] = "staging",
            ["templateName"] = "enterprise-web"
        });
        var created = JsonDocument.Parse(createResult);
        created.RootElement.GetProperty("status").GetString().Should().Be("success");

        // Step 2: Validate it
        var validateResult = await _validate.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "platform-qa"
        });
        var validated = JsonDocument.Parse(validateResult);
        validated.RootElement.GetProperty("status").GetString().Should().Be("success");
        validated.RootElement.GetProperty("data").GetProperty("complianceScore").GetDouble().Should().BeGreaterThan(0);

        // Step 3: Promote (dry run)
        var promoteResult = await _promote.ExecuteAsync(new Dictionary<string, object?>
        {
            ["sourceEnvironment"] = "platform-qa",
            ["targetEnvironment"] = "platform-prod",
            ["dryRun"] = true
        });
        var promoted = JsonDocument.Parse(promoteResult);
        promoted.RootElement.GetProperty("status").GetString().Should().Be("success");
        promoted.RootElement.GetProperty("data").GetProperty("promotionStatus").GetString().Should().Be("DryRunComplete");
    }

    [Fact]
    public async Task Compare_Then_Drift_Detection_Flow()
    {
        // Step 1: Compare staging vs prod
        var compareResult = await _compare.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentA"] = "staging",
            ["environmentB"] = "prod"
        });
        var compared = JsonDocument.Parse(compareResult);
        compared.RootElement.GetProperty("status").GetString().Should().Be("success");
        compared.RootElement.GetProperty("data").GetProperty("totalDifferences").GetInt32().Should().BeGreaterThan(0);

        // Step 2: Detect drift in prod
        var driftResult = await _drift.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "platform-prod"
        });
        var drifted = JsonDocument.Parse(driftResult);
        drifted.RootElement.GetProperty("status").GetString().Should().Be("success");
        drifted.RootElement.GetProperty("data").GetProperty("driftStatus").GetString().Should().Be("Drifted");
    }

    [Fact]
    public async Task List_Then_Status_Then_History_Flow()
    {
        // Step 1: List environments
        var listResult = await _list.ExecuteAsync(new Dictionary<string, object?> { });
        var listed = JsonDocument.Parse(listResult);
        listed.RootElement.GetProperty("status").GetString().Should().Be("success");
        listed.RootElement.GetProperty("data").GetProperty("environmentCount").GetInt32().Should().BeGreaterThan(0);

        // Step 2: Get status of prod
        var statusResult = await _status.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "platform-prod"
        });
        var status = JsonDocument.Parse(statusResult);
        status.RootElement.GetProperty("status").GetString().Should().Be("success");
        status.RootElement.GetProperty("data").GetProperty("overallStatus").GetString().Should().Be("Healthy");

        // Step 3: View deployment history
        var historyResult = await _history.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "platform-prod",
            ["limit"] = 5
        });
        var history = JsonDocument.Parse(historyResult);
        history.RootElement.GetProperty("status").GetString().Should().Be("success");
        history.RootElement.GetProperty("data").GetProperty("entries").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Clone_Then_Delete_Flow()
    {
        // Step 1: Clone environment
        var cloneResult = await _clone.ExecuteAsync(new Dictionary<string, object?>
        {
            ["sourceEnvironment"] = "platform-prod",
            ["targetName"] = "platform-hotfix",
            ["targetTier"] = "staging"
        });
        var cloned = JsonDocument.Parse(cloneResult);
        cloned.RootElement.GetProperty("status").GetString().Should().Be("success");
        cloned.RootElement.GetProperty("data").GetProperty("clonedEnvironment").GetProperty("name").GetString().Should().Be("platform-hotfix");

        // Step 2: Delete without confirm should fail
        var deleteResult = await _delete.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "platform-hotfix",
            ["confirm"] = false
        });
        var rejected = JsonDocument.Parse(deleteResult);
        rejected.RootElement.GetProperty("status").GetString().Should().Be("error");

        // Step 3: Delete with confirm
        var confirmDelete = await _delete.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "platform-hotfix",
            ["confirm"] = true
        });
        var deleted = JsonDocument.Parse(confirmDelete);
        deleted.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task Agent_Registers_All_Ten_Tools()
    {
        var agent = new EnvironmentAgent(
            new Mock<ILogger<EnvironmentAgent>>().Object,
            _clone, _drift, _compare, _promote, _list, _status, _create, _delete, _history, _validate);

        var tools = agent.GetToolMetadata();
        tools.Should().HaveCount(10);
        tools.Select(t => t.Name).Should().Contain("clone_environment");
        tools.Select(t => t.Name).Should().Contain("promote_environment");
        tools.Select(t => t.Name).Should().Contain("validate_environment");
    }
}
