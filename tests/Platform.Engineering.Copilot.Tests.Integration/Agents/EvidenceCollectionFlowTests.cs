using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Compliance.Tools;
using Platform.Engineering.Copilot.Core.Auth;
using Platform.Engineering.Copilot.Core.Data.Enumerations;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// T080 — Integration test for evidence collection flow:
/// collect → verify 5 artifact types → browse cached → deny remediation with role message.
/// </summary>
public class EvidenceCollectionFlowTests
{
    [Fact]
    public async Task EvidenceCollectionFlow_CollectAndVerifyArtifactTypes()
    {
        // Step 1: Collect evidence
        var collectTool = new ComplianceCollectEvidenceTool(
            new Mock<ILogger<ComplianceCollectEvidenceTool>>().Object);
        var collectParams = new Dictionary<string, object?> { ["controlId"] = "AC-2" };

        var collectResult = await collectTool.ExecuteAsync(collectParams);
        var collectDoc = JsonDocument.Parse(collectResult);

        collectDoc.RootElement.GetProperty("status").GetString().Should().Be("success");

        // Step 2: Verify 5 artifact types per SC-007
        var evidence = collectDoc.RootElement.GetProperty("data").GetProperty("evidence");
        evidence.GetArrayLength().Should().Be(5);

        var types = evidence.EnumerateArray()
            .Select(e => e.GetProperty("type").GetString())
            .ToHashSet();

        types.Should().Contain("ConfigurationExport");
        types.Should().Contain("PolicySnapshot");
        types.Should().Contain("DefenderRecommendation");
        types.Should().Contain("ActivityLog");
        types.Should().Contain("ResourceInventory");
    }

    [Fact]
    public async Task EvidenceCollectionFlow_BrowseCachedWithoutAuth()
    {
        // Audit log tool doesn't require auth — can browse results
        var auditTool = new ComplianceAuditLogTool(
            new Mock<ILogger<ComplianceAuditLogTool>>().Object);

        auditTool.RequiresAuthentication.Should().BeFalse();
        auditTool.PimTierRequired.Should().Be(PimTier.None);

        var result = await auditTool.ExecuteAsync(new Dictionary<string, object?>());
        var doc = JsonDocument.Parse(result);

        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("entries")
            .GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public void EvidenceCollectionFlow_AuditorDeniedRemediation()
    {
        // Step 3: Auditor attempts remediation → role-based denial per FR-020
        var auditorRoles = new[] { UserRole.Auditor };

        var isAllowed = AuthDenialMessageService.IsRemediationAllowed(auditorRoles);
        isAllowed.Should().BeFalse();

        var denial = AuthDenialMessageService.BuildDenialMessage(
            "remediation",
            AuthDenialMessageService.RemediationAllowedRoles,
            PimTier.Write,
            auditorRoles);

        denial.Denied.Should().BeTrue();
        denial.Message.Should().Contain("Auditor");
        denial.Message.Should().Contain("does not permit remediation");
        denial.RequiredRoles.Should().Contain("ComplianceOfficer");
        denial.RequiredRoles.Should().Contain("PlatformEngineer");
        denial.RequiredPimTier.Should().Be("Write");
        denial.CurrentRoles.Should().Contain("Auditor");
        denial.Suggestion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void EvidenceCollectionFlow_ComplianceOfficerAllowedRemediation()
    {
        var officerRoles = new[] { UserRole.ComplianceOfficer };

        AuthDenialMessageService.IsRemediationAllowed(officerRoles).Should().BeTrue();
    }

    [Fact]
    public void EvidenceCollectionFlow_PlatformEngineerAllowedRemediation()
    {
        var engineerRoles = new[] { UserRole.PlatformEngineer };

        AuthDenialMessageService.IsRemediationAllowed(engineerRoles).Should().BeTrue();
    }
}
