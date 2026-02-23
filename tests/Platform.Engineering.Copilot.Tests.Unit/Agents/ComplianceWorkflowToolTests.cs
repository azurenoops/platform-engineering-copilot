using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Compliance.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// T047 — ComplianceStatusTool, ComplianceHistoryTool, ComplianceRemediateTool,
///        ComplianceDashboardTool, ComplianceExportTool, ComplianceMonitoringTool tests.
/// </summary>
public class ComplianceStatusToolTests
{
    private readonly ComplianceStatusTool _tool;

    public ComplianceStatusToolTests()
    {
        _tool = new ComplianceStatusTool(new Mock<ILogger<ComplianceStatusTool>>().Object);
    }

    [Fact]
    public void Tool_HasCorrectName()
    {
        _tool.Name.Should().Be("compliance_status");
    }

    [Fact]
    public void Tool_RequiresReadPim()
    {
        _tool.RequiresAuthentication.Should().BeTrue();
        _tool.PimTierRequired.Should().Be(PimTier.Read);
    }

    [Fact]
    public async Task Execute_ReturnsComplianceScore()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>());

        result.ShouldBeSuccessEnvelope("compliance_status");
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("complianceScore", out _).Should().BeTrue();
        data.TryGetProperty("summary", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_WithSubscriptionId_UsesIt()
    {
        var parameters = new Dictionary<string, object?> { { "subscriptionId", "sub-123" } };
        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("subscriptionId").GetString()
            .Should().Be("sub-123");
    }

    [Fact]
    public async Task Execute_DefaultSubscription()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("subscriptionId").GetString()
            .Should().Be("default");
    }
}

public class ComplianceHistoryToolTests
{
    private readonly ComplianceHistoryTool _tool;

    public ComplianceHistoryToolTests()
    {
        _tool = new ComplianceHistoryTool(new Mock<ILogger<ComplianceHistoryTool>>().Object);
    }

    [Fact]
    public void Tool_HasCorrectName()
    {
        _tool.Name.Should().Be("compliance_history");
    }

    [Fact]
    public void Tool_DoesNotRequireAuth()
    {
        _tool.RequiresAuthentication.Should().BeFalse();
        _tool.PimTierRequired.Should().Be(PimTier.None);
    }

    [Fact]
    public async Task Execute_DefaultPagination()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>());

        result.ShouldBeSuccessEnvelope("compliance_history");
        result.ShouldHavePagination();
    }

    [Fact]
    public async Task Execute_CustomPageSize()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "pageSize", 10 },
            { "days", 5 }
        };

        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldHavePagination();
        var doc = JsonDocument.Parse(result);
        var pagination = doc.RootElement.GetProperty("pagination");
        pagination.GetProperty("pageSize").GetInt32().Should().BeLessOrEqualTo(10);
    }

    [Fact]
    public async Task Execute_MaxPageSizeCapped()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "pageSize", 999 }
        };

        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        var pagination = doc.RootElement.GetProperty("pagination");
        pagination.GetProperty("pageSize").GetInt32().Should().BeLessOrEqualTo(100);
    }

    [Fact]
    public async Task Execute_HasTrendData()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("trend", out var trend).Should().BeTrue();
        trend.TryGetProperty("direction", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_HasAssessmentEntries()
    {
        var parameters = new Dictionary<string, object?> { { "days", 7 } };
        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        var assessments = doc.RootElement.GetProperty("data").GetProperty("assessments");
        assessments.GetArrayLength().Should().BeGreaterThan(0);
    }
}

public class ComplianceRemediateToolTests
{
    private readonly ComplianceRemediateTool _tool;

    public ComplianceRemediateToolTests()
    {
        _tool = new ComplianceRemediateTool(new Mock<ILogger<ComplianceRemediateTool>>().Object);
    }

    [Fact]
    public void Tool_HasCorrectName()
    {
        _tool.Name.Should().Be("compliance_remediate");
    }

    [Fact]
    public void Tool_RequiresWritePim()
    {
        _tool.RequiresAuthentication.Should().BeTrue();
        _tool.PimTierRequired.Should().Be(PimTier.Write);
    }

    [Fact]
    public async Task Execute_DefaultDryRun()
    {
        var parameters = new Dictionary<string, object?> { { "findingId", "FIND-001" } };
        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope("compliance_remediate");
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("mode").GetString().Should().Be("dry-run");
        data.GetProperty("confirmationRequired").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Execute_MutuallyExclusive_ReturnsError()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "findingId", "FIND-001" },
            { "controlFamily", "AC" }
        };

        var result = await _tool.ExecuteAsync(parameters);
        result.ShouldBeErrorEnvelope("VALIDATION_ERROR");
    }

    [Fact]
    public async Task Execute_DryRunFalse_AppliesRemediation()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "findingId", "FIND-001" },
            { "dryRun", false }
        };

        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("mode").GetString().Should().Be("applied");
    }

    [Fact]
    public async Task Execute_HasRemediationPlanId()
    {
        var parameters = new Dictionary<string, object?> { { "findingId", "FIND-001" } };
        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("remediationPlanId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_HighRiskFamily_ShowsWarning()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "controlFamily", "AC" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("isHighRisk").GetBoolean().Should().BeTrue();
        data.TryGetProperty("highRiskWarning", out var warning).Should().BeTrue();
        warning.GetString().Should().Contain("high-risk");
    }

    [Fact]
    public async Task Execute_NonHighRiskFamily_NoWarning()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "controlFamily", "AU" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("isHighRisk").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Execute_BatchRemediation_GroupsBySeverity()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "controlFamily", "SC" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("batchGrouping", out var batch).Should().BeTrue();
        batch.TryGetProperty("severityGroups", out _).Should().BeTrue();
        batch.TryGetProperty("totalEstimatedMinutes", out _).Should().BeTrue();
        data.GetProperty("totalFindings").GetInt32().Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task Execute_HighRiskFamily_IA_ShowsWarning()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "controlFamily", "IA" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("isHighRisk").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Execute_FindingWithACPrefix_HighRisk()
    {
        var parameters = new Dictionary<string, object?>
        {
            { "findingId", "AC-2-FIND-001" }
        };

        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("isHighRisk").GetBoolean().Should().BeTrue();
    }
}

public class ComplianceDashboardToolTests
{
    private readonly ComplianceDashboardTool _tool;

    public ComplianceDashboardToolTests()
    {
        _tool = new ComplianceDashboardTool(new Mock<ILogger<ComplianceDashboardTool>>().Object);
    }

    [Fact]
    public void Tool_HasCorrectName()
    {
        _tool.Name.Should().Be("compliance_dashboard");
    }

    [Fact]
    public void Tool_RequiresReadPim()
    {
        _tool.RequiresAuthentication.Should().BeTrue();
        _tool.PimTierRequired.Should().Be(PimTier.Read);
    }

    [Fact]
    public async Task Execute_ReturnsAggregatedData()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>());

        result.ShouldBeSuccessEnvelope("compliance_dashboard");
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("overallScore", out _).Should().BeTrue();
        data.TryGetProperty("frameworks", out _).Should().BeTrue();
        data.TryGetProperty("criticalFindings", out _).Should().BeTrue();
    }
}

public class ComplianceExportToolTests
{
    private readonly ComplianceExportTool _tool;

    public ComplianceExportToolTests()
    {
        _tool = new ComplianceExportTool(new Mock<ILogger<ComplianceExportTool>>().Object);
    }

    [Fact]
    public void Tool_HasCorrectName()
    {
        _tool.Name.Should().Be("compliance_export");
    }

    [Fact]
    public void Tool_DoesNotRequireAuth()
    {
        _tool.RequiresAuthentication.Should().BeFalse();
        _tool.PimTierRequired.Should().Be(PimTier.None);
    }

    [Fact]
    public async Task Execute_JsonFormat()
    {
        var parameters = new Dictionary<string, object?> { { "format", "json" } };
        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope("compliance_export");
        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("format").GetString().Should().Be("json");
    }

    [Fact]
    public async Task Execute_CsvFormat()
    {
        var parameters = new Dictionary<string, object?> { { "format", "csv" } };
        var result = await _tool.ExecuteAsync(parameters);

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("format").GetString().Should().Be("csv");
    }

    [Fact]
    public async Task Execute_HasDownloadUrl()
    {
        var result = await _tool.ExecuteAsync(new Dictionary<string, object?>());

        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("downloadUrl", out _).Should().BeTrue();
        data.TryGetProperty("exportId", out _).Should().BeTrue();
    }
}

public class ComplianceMonitoringToolTests
{
    private readonly ComplianceMonitoringTool _tool;

    public ComplianceMonitoringToolTests()
    {
        _tool = new ComplianceMonitoringTool(new Mock<ILogger<ComplianceMonitoringTool>>().Object);
    }

    [Fact]
    public void Tool_HasCorrectName()
    {
        _tool.Name.Should().Be("compliance_monitoring");
    }

    [Fact]
    public void Tool_RequiresReadPim()
    {
        _tool.RequiresAuthentication.Should().BeTrue();
        _tool.PimTierRequired.Should().Be(PimTier.Read);
    }

    [Fact]
    public async Task Execute_StatusAction()
    {
        var parameters = new Dictionary<string, object?> { { "action", "status" } };
        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope("compliance_monitoring");
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("monitoringStatus", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_AlertsAction()
    {
        var parameters = new Dictionary<string, object?> { { "action", "alerts" } };
        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope();
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("alertCount", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_TrendAction()
    {
        var parameters = new Dictionary<string, object?> { { "action", "trend" } };
        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope();
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("trendDirection", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_MissingAction_Throws()
    {
        Func<Task> act = () => _tool.ExecuteAsync(new Dictionary<string, object?>());
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Execute_ScanAction()
    {
        var parameters = new Dictionary<string, object?> { { "action", "scan" } };
        var result = await _tool.ExecuteAsync(parameters);

        result.ShouldBeSuccessEnvelope();
        var doc = JsonDocument.Parse(result);
        var data = doc.RootElement.GetProperty("data");
        data.TryGetProperty("scanId", out _).Should().BeTrue();
    }
}
