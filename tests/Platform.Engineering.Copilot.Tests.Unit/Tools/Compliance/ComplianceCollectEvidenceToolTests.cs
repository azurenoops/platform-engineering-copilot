using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Compliance.Tools;
using Platform.Engineering.Copilot.Core.Agents;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.Compliance;

/// <summary>
/// T077 — ComplianceCollectEvidenceTool tests:
/// controlId required, append default (immutable records), replace opt-in,
/// previousEvidenceCount in response, Read PIM, paginated, 5 artifact types (SC-007),
/// response envelope per compliance-tools.md.
/// </summary>
public class ComplianceCollectEvidenceToolTests
{
    private readonly ComplianceCollectEvidenceTool _tool;

    public ComplianceCollectEvidenceToolTests()
    {
        var logger = new Mock<ILogger<ComplianceCollectEvidenceTool>>().Object;
        _tool = new ComplianceCollectEvidenceTool(logger);
    }

    [Fact]
    public void Name_IsCorrect()
        => _tool.Name.Should().Be("compliance_collect_evidence");

    [Fact]
    public void RequiresAuthentication_IsTrue()
        => _tool.RequiresAuthentication.Should().BeTrue();

    [Fact]
    public void PimTierRequired_IsRead()
        => _tool.PimTierRequired.Should().Be(PimTier.Read);

    [Fact]
    public async Task Execute_ControlIdRequired_ReturnsEvidence()
    {
        var parameters = new Dictionary<string, object?> { ["controlId"] = "AC-2" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.GetProperty("data").GetProperty("controlId").GetString().Should().Be("AC-2");
        root.GetProperty("data").GetProperty("evidenceCount").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Execute_DefaultMode_IsAppend()
    {
        var parameters = new Dictionary<string, object?> { ["controlId"] = "AC-2" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("mode").GetString().Should().Be("append");
    }

    [Fact]
    public async Task Execute_AppendMode_IncludesPreviousEvidenceCount()
    {
        var parameters = new Dictionary<string, object?> { ["controlId"] = "AC-2" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("previousEvidenceCount").GetInt32()
            .Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Execute_ReplaceMode_SetsPreviousEvidenceCountToZero()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2",
            ["replace"] = true
        };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("mode").GetString().Should().Be("replace");
        doc.RootElement.GetProperty("data").GetProperty("previousEvidenceCount").GetInt32()
            .Should().Be(0);
    }

    [Fact]
    public async Task Execute_Returns5ArtifactTypes()
    {
        var parameters = new Dictionary<string, object?> { ["controlId"] = "SC-7" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var evidence = doc.RootElement.GetProperty("data").GetProperty("evidence");
        evidence.GetArrayLength().Should().Be(5);

        var types = evidence.EnumerateArray()
            .Select(e => e.GetProperty("type").GetString())
            .ToList();

        types.Should().Contain("ConfigurationExport");
        types.Should().Contain("PolicySnapshot");
        types.Should().Contain("DefenderRecommendation");
        types.Should().Contain("ActivityLog");
        types.Should().Contain("ResourceInventory");
    }

    [Fact]
    public async Task Execute_Paginated_DefaultPage()
    {
        var parameters = new Dictionary<string, object?> { ["controlId"] = "AC-2" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var pagination = doc.RootElement.GetProperty("data").GetProperty("pagination");
        pagination.GetProperty("page").GetInt32().Should().Be(1);
        pagination.GetProperty("pageSize").GetInt32().Should().Be(25);
    }

    [Fact]
    public async Task Execute_Paginated_CustomPageSize()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2",
            ["pageSize"] = 2,
            ["page"] = 1
        };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var pagination = doc.RootElement.GetProperty("data").GetProperty("pagination");
        pagination.GetProperty("pageSize").GetInt32().Should().Be(2);
        var evidence = doc.RootElement.GetProperty("data").GetProperty("evidence");
        evidence.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Execute_Paginated_MaxPageSizeCapped()
    {
        var parameters = new Dictionary<string, object?>
        {
            ["controlId"] = "AC-2",
            ["pageSize"] = 200
        };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("data").GetProperty("pagination")
            .GetProperty("pageSize").GetInt32().Should().Be(100);
    }

    [Fact]
    public async Task Execute_ReturnsResponseEnvelope()
    {
        var parameters = new Dictionary<string, object?> { ["controlId"] = "AC-2" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        root.GetProperty("status").GetString().Should().Be("success");
        root.TryGetProperty("data", out _).Should().BeTrue();
        root.TryGetProperty("metadata", out _).Should().BeTrue();
        root.GetProperty("metadata").GetProperty("toolName").GetString()
            .Should().Be("compliance_collect_evidence");
    }

    [Fact]
    public async Task Execute_ProgressIsStreamed()
    {
        var updates = new List<ProgressUpdate>();
        var progress = new Progress<ProgressUpdate>(u => updates.Add(u));
        var parameters = new Dictionary<string, object?> { ["controlId"] = "AC-2" };

        await _tool.ExecuteAsync(parameters, progress);
        // Allow async progress to fire
        await Task.Delay(50);

        updates.Should().NotBeEmpty();
        updates.Last().PercentComplete.Should().Be(100);
    }

    [Fact]
    public async Task Execute_EvidenceHasRequiredFields()
    {
        var parameters = new Dictionary<string, object?> { ["controlId"] = "AC-2" };

        var result = await _tool.ExecuteAsync(parameters);
        var doc = JsonDocument.Parse(result);

        var firstEvidence = doc.RootElement.GetProperty("data")
            .GetProperty("evidence")[0];

        firstEvidence.TryGetProperty("evidenceId", out _).Should().BeTrue();
        firstEvidence.TryGetProperty("type", out _).Should().BeTrue();
        firstEvidence.TryGetProperty("category", out _).Should().BeTrue();
        firstEvidence.TryGetProperty("description", out _).Should().BeTrue();
        firstEvidence.TryGetProperty("collectedAt", out _).Should().BeTrue();
        firstEvidence.TryGetProperty("contentSizeBytes", out _).Should().BeTrue();
    }
}
