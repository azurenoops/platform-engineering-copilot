using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Compliance.Tools;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.Compliance;

/// <summary>
/// T079 — ComplianceAuditLogTool tests:
/// no auth, paginated, default 7 days, actionType filter,
/// response envelope per compliance-tools.md.
/// </summary>
public class ComplianceAuditLogToolTests
{
    private readonly ComplianceAuditLogTool _tool;

    public ComplianceAuditLogToolTests()
    {
        var logger = new Mock<ILogger<ComplianceAuditLogTool>>().Object;
        _tool = new ComplianceAuditLogTool(logger);
    }

    [Fact]
    public void Name_IsCorrect()
        => _tool.Name.Should().Be("compliance_audit_log");

    [Fact]
    public void RequiresAuthentication_IsFalse()
        => _tool.RequiresAuthentication.Should().BeFalse();

    [Fact]
    public void PimTierRequired_IsNone()
        => _tool.PimTierRequired.Should().Be(PimTier.None);

    [Fact]
    public async Task Execute_DefaultDays_Is7()
    {
        var parameters = new Dictionary<string, object?>();

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("days").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task Execute_CustomDays_IsRespected()
    {
        var parameters = new Dictionary<string, object?> { ["days"] = 30 };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("days").GetInt32().Should().Be(30);
    }

    [Fact]
    public async Task Execute_ZeroDays_DefaultsTo7()
    {
        var parameters = new Dictionary<string, object?> { ["days"] = 0 };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("days").GetInt32().Should().Be(7);
    }

    [Fact]
    public async Task Execute_ActionTypeFilter_FiltersResults()
    {
        var parameters = new Dictionary<string, object?> { ["actionType"] = "Assessment" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var entries = doc.RootElement.GetProperty("data").GetProperty("entries");
        foreach (var entry in entries.EnumerateArray())
        {
            entry.GetProperty("action").GetString().Should().Be("Assessment");
        }
    }

    [Fact]
    public async Task Execute_Paginated_DefaultsApplied()
    {
        var parameters = new Dictionary<string, object?>();

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var pagination = doc.RootElement.GetProperty("data").GetProperty("pagination");
        pagination.GetProperty("page").GetInt32().Should().Be(1);
        pagination.GetProperty("pageSize").GetInt32().Should().Be(25);
    }

    [Fact]
    public async Task Execute_Paginated_MaxPageSizeCapped()
    {
        var parameters = new Dictionary<string, object?> { ["pageSize"] = 500 };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("pagination")
            .GetProperty("pageSize").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task Execute_Paginated_HasNextPage()
    {
        var parameters = new Dictionary<string, object?> { ["pageSize"] = 2, ["page"] = 1 };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var pagination = doc.RootElement.GetProperty("data").GetProperty("pagination");
        // With 8 sample entries and page size 2, should have more pages
        var totalItems = pagination.GetProperty("totalItems").GetInt32();
        if (totalItems > 2)
        {
            pagination.GetProperty("hasNextPage").GetBoolean().Should().BeTrue();
        }
    }

    [Fact]
    public async Task Execute_EntriesHaveRequiredFields()
    {
        var parameters = new Dictionary<string, object?>();

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var entries = doc.RootElement.GetProperty("data").GetProperty("entries");
        entries.GetArrayLength().Should().BeGreaterThan(0);

        var firstEntry = entries[0];
        firstEntry.TryGetProperty("entryId", out _).Should().BeTrue();
        firstEntry.TryGetProperty("action", out _).Should().BeTrue();
        firstEntry.TryGetProperty("timestamp", out _).Should().BeTrue();
        firstEntry.TryGetProperty("userId", out _).Should().BeTrue();
        firstEntry.TryGetProperty("details", out _).Should().BeTrue();
        firstEntry.TryGetProperty("correlationId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Execute_ReturnsResponseEnvelope()
    {
        var parameters = new Dictionary<string, object?>();

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.TryGetProperty("data", out _).Should().BeTrue();
        root.TryGetProperty("metadata", out _).Should().BeTrue();
        root.GetProperty("metadata").GetProperty("toolName").GetString()
            .Should().Be("compliance_audit_log");
    }

    [Fact]
    public async Task Execute_UserIdsMasked()
    {
        var parameters = new Dictionary<string, object?>();

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var entries = doc.RootElement.GetProperty("data").GetProperty("entries");
        foreach (var entry in entries.EnumerateArray())
        {
            var userId = entry.GetProperty("userId").GetString();
            userId.Should().StartWith("****", "User IDs should be masked for privacy");
        }
    }
}
