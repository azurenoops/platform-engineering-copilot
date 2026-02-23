using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Environment.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.Environment;

/// <summary>
/// T135 — Unit tests for Environment tools.
/// </summary>
public class EnvironmentToolTests
{
    private readonly CloneEnvironmentTool _cloneTool = new(new Mock<ILogger<CloneEnvironmentTool>>().Object);
    private readonly DetectDriftTool _driftTool = new(new Mock<ILogger<DetectDriftTool>>().Object);
    private readonly CompareEnvironmentsTool _compareTool = new(new Mock<ILogger<CompareEnvironmentsTool>>().Object);
    private readonly PromoteEnvironmentTool _promoteTool = new(new Mock<ILogger<PromoteEnvironmentTool>>().Object);
    private readonly ListEnvironmentsTool _listTool = new(new Mock<ILogger<ListEnvironmentsTool>>().Object);
    private readonly GetEnvironmentStatusTool _statusTool = new(new Mock<ILogger<GetEnvironmentStatusTool>>().Object);
    private readonly CreateEnvironmentTool _createTool = new(new Mock<ILogger<CreateEnvironmentTool>>().Object);
    private readonly DeleteEnvironmentTool _deleteTool = new(new Mock<ILogger<DeleteEnvironmentTool>>().Object);
    private readonly GetEnvironmentHistoryTool _historyTool = new(new Mock<ILogger<GetEnvironmentHistoryTool>>().Object);
    private readonly ValidateEnvironmentTool _validateTool = new(new Mock<ILogger<ValidateEnvironmentTool>>().Object);

    // ─── clone_environment ───
    [Fact]
    public async Task Clone_Returns_Success()
    {
        var result = await _cloneTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["sourceEnvironment"] = "platform-staging",
            ["targetName"] = "platform-qa",
            ["targetTier"] = "staging"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("clonedEnvironment").GetProperty("name").GetString().Should().Be("platform-qa");
    }

    [Fact]
    public async Task Clone_Missing_Source_Returns_Error()
    {
        var result = await _cloneTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["sourceEnvironment"] = "",
            ["targetName"] = "qa",
            ["targetTier"] = "dev"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    [Fact] public void Clone_Requires_PIM_Write() => _cloneTool.PimTierRequired.Should().Be(PimTier.Write);

    // ─── detect_drift ───
    [Fact]
    public async Task DetectDrift_Returns_DriftItems()
    {
        var result = await _driftTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "platform-prod"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("driftStatus").GetString().Should().Be("Drifted");
        doc.RootElement.GetProperty("data").GetProperty("driftItems").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact] public void Drift_Requires_PIM_Read() => _driftTool.PimTierRequired.Should().Be(PimTier.Read);

    // ─── compare_environments ───
    [Fact]
    public async Task Compare_Returns_Differences()
    {
        var result = await _compareTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentA"] = "staging",
            ["environmentB"] = "prod"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("totalDifferences").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Compare_Missing_Env_Returns_Error()
    {
        var result = await _compareTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentA"] = "",
            ["environmentB"] = "prod"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
    }

    // ─── promote_environment ───
    [Fact]
    public async Task Promote_DryRun_Returns_Changes()
    {
        var result = await _promoteTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["sourceEnvironment"] = "staging",
            ["targetEnvironment"] = "prod",
            ["dryRun"] = true
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("promotionStatus").GetString().Should().Be("DryRunComplete");
    }

    [Fact] public void Promote_Requires_PIM_Write() => _promoteTool.PimTierRequired.Should().Be(PimTier.Write);

    // ─── list_environments ───
    [Fact]
    public async Task List_Returns_All_Environments()
    {
        var result = await _listTool.ExecuteAsync(new Dictionary<string, object?> { });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("environmentCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task List_Filters_By_Tier()
    {
        var result = await _listTool.ExecuteAsync(new Dictionary<string, object?> { ["tier"] = "prod" });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("filter").GetString().Should().Be("prod");
    }

    // ─── get_environment_status ───
    [Fact]
    public async Task Status_Returns_Details()
    {
        var result = await _statusTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "platform-prod"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("overallStatus").GetString().Should().Be("Healthy");
    }

    // ─── create_environment ───
    [Fact]
    public async Task Create_Returns_Success()
    {
        var result = await _createTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["name"] = "test-env",
            ["tier"] = "dev"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact] public void Create_Requires_PIM_Write() => _createTool.PimTierRequired.Should().Be(PimTier.Write);

    // ─── delete_environment ───
    [Fact]
    public async Task Delete_Without_Confirm_Returns_Error()
    {
        var result = await _deleteTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "test-env",
            ["confirm"] = false
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString().Should().Be("CONFIRMATION_REQUIRED");
    }

    [Fact]
    public async Task Delete_With_Confirm_Returns_Success()
    {
        var result = await _deleteTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "test-env",
            ["confirm"] = true
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact] public void Delete_Requires_PIM_Write() => _deleteTool.PimTierRequired.Should().Be(PimTier.Write);

    // ─── get_environment_history ───
    [Fact]
    public async Task History_Returns_Entries()
    {
        var result = await _historyTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "platform-prod"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("entries").GetArrayLength().Should().BeGreaterThan(0);
    }

    // ─── validate_environment ───
    [Fact]
    public async Task Validate_Returns_Compliance_Score()
    {
        var result = await _validateTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["environmentName"] = "platform-prod"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("complianceScore").GetDouble().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Validate_Reports_Progress()
    {
        var progress = new List<ProgressUpdate>();
        await _validateTool.ExecuteAsync(
            new Dictionary<string, object?> { ["environmentName"] = "platform-prod" },
            new Progress<ProgressUpdate>(p => progress.Add(p)));
        await Task.Delay(50);
        progress.Should().NotBeEmpty();
    }
}
