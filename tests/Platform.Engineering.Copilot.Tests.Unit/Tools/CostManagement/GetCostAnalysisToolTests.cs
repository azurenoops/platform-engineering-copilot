using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.CostManagement.Tools;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.CostManagement;

/// <summary>
/// T116 — Unit tests for GetCostAnalysisTool.
/// Timeframe options, groupBy options, Read PIM, response format.
/// </summary>
public class GetCostAnalysisToolTests
{
    private readonly GetCostAnalysisTool _tool = new(
        new Mock<ILogger<GetCostAnalysisTool>>().Object);

    [Fact] public void Name_Is_Correct() => _tool.Name.Should().Be("get_cost_analysis");
    [Fact] public void RequiresAuthentication_Is_True() => _tool.RequiresAuthentication.Should().BeTrue();
    [Fact] public void PimTier_Is_Read() => _tool.PimTierRequired.Should().Be(PimTier.Read);

    [Fact]
    public async Task Default_Timeframe_Is_30d()
    {
        var result = await _tool.ExecuteAsync([]);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("timeframe").GetString()
            .Should().Be("30d");
    }

    [Theory]
    [InlineData("7d")]
    [InlineData("30d")]
    [InlineData("90d")]
    public async Task Valid_Timeframes_Return_Success(string timeframe)
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["timeframe"] = timeframe
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("timeframe").GetString()
            .Should().Be(timeframe);
    }

    [Fact]
    public async Task Invalid_Timeframe_Returns_Error()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["timeframe"] = "999d"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_TIMEFRAME");
    }

    [Theory]
    [InlineData("resourceType")]
    [InlineData("resourceGroup")]
    [InlineData("service")]
    [InlineData("tag")]
    public async Task Valid_GroupBy_Returns_Success(string groupBy)
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["groupBy"] = groupBy
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("groupBy").GetString()
            .Should().Be(groupBy);
    }

    [Fact]
    public async Task Invalid_GroupBy_Returns_Error()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["groupBy"] = "invalid"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("INVALID_GROUP_BY");
    }

    [Fact]
    public async Task Custom_Timeframe_Without_Dates_Returns_Error()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["timeframe"] = "custom"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("error");
        doc.RootElement.GetProperty("error").GetProperty("errorCode").GetString()
            .Should().Be("MISSING_DATE_RANGE");
    }

    [Fact]
    public async Task Custom_Timeframe_With_Dates_Returns_Success()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["timeframe"] = "custom",
            ["startDate"] = "2025-01-01",
            ["endDate"] = "2025-01-15"
        });
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
    }

    [Fact]
    public async Task Response_Contains_CostBreakdown()
    {
        var result = await _tool.ExecuteAsync([]);
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("costBreakdown").GetArrayLength().Should().BeGreaterThan(0);
        data.GetProperty("totalCost").GetDouble().Should().BeGreaterThan(0);
        data.GetProperty("currency").GetString().Should().Be("USD");
    }

    [Fact]
    public async Task Response_Contains_ChangePercent_And_Trend()
    {
        var result = await _tool.ExecuteAsync([]);
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("changePercent", out _).Should().BeTrue();
        data.GetProperty("trend").GetString().Should().BeOneOf("increasing", "decreasing");
    }

    [Fact]
    public async Task CostBreakdown_Items_Have_PercentOfTotal()
    {
        var result = await _tool.ExecuteAsync([]);
        var doc = JsonDocument.Parse(result);
        var breakdown = doc.RootElement.GetProperty("data").GetProperty("costBreakdown");
        foreach (var item in breakdown.EnumerateArray())
        {
            item.TryGetProperty("percentOfTotal", out var pct).Should().BeTrue();
            pct.GetDouble().Should().BeGreaterOrEqualTo(0);
        }
    }

    [Fact]
    public async Task Metadata_Contains_ToolName()
    {
        var result = await _tool.ExecuteAsync([]);
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("metadata").GetProperty("toolName").GetString()
            .Should().Be("get_cost_analysis");
    }

    [Fact]
    public async Task Progress_Reports_Are_Sent()
    {
        var updates = new List<string>();
        var progress = new Progress<Platform.Engineering.Copilot.Core.Agents.ProgressUpdate>(u =>
            updates.Add(u.Message));

        await _tool.ExecuteAsync([], progress);
        updates.Should().HaveCountGreaterOrEqualTo(2);
    }
}
