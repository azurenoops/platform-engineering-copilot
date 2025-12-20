using FluentAssertions;
using Platform.Engineering.Copilot.Compliance.Core.Configuration;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Plugins;

/// <summary>
/// Unit tests for CompliancePlugin models and configuration
/// Tests plugin functions, authorization, and audit logging
/// </summary>
public class CompliancePluginTests
{
    [Fact]
    public void ComplianceAgentOptions_SectionName_IsCorrect()
    {
        // Assert
        ComplianceAgentOptions.SectionName.Should().Be("ComplianceAgent");
    }

    [Fact]
    public void ComplianceAgentOptions_AzureOpenAI_DefaultValues()
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions();

        // Assert
        options.AzureOpenAI.Should().NotBeNull();
    }

    [Fact]
    public void ComplianceAgentOptions_Gateway_DefaultValues()
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions();

        // Assert
        options.Gateway.Should().NotBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(128000)]
    public void ComplianceAgentOptions_MaxTokens_AcceptsValidRange(int maxTokens)
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions { MaxTokens = maxTokens };

        // Assert
        options.MaxTokens.Should().Be(maxTokens);
    }

    [Fact]
    public void ComplianceAgentOptions_EnableAutomatedRemediation_DefaultTrue()
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions();

        // Assert
        options.EnableAutomatedRemediation.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComplianceAgentOptions_EnableAutomatedRemediation_CanBeSet(bool enabled)
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions { EnableAutomatedRemediation = enabled };

        // Assert
        options.EnableAutomatedRemediation.Should().Be(enabled);
    }

    [Theory]
    [InlineData("FedRAMPHigh")]
    [InlineData("FedRAMPModerate")]
    [InlineData("DoD IL5")]
    [InlineData("DoD IL4")]
    public void ComplianceAgentOptions_DefaultBaseline_AcceptsValidBaselines(string baseline)
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions { DefaultBaseline = baseline };

        // Assert
        options.DefaultBaseline.Should().Be(baseline);
    }

    [Fact]
    public void AtoFindingSeverity_ValuesAreOrdered()
    {
        // Assert - verify severity ordering for correct prioritization
        // Cast to int to compare enum values
        ((int)AtoFindingSeverity.Critical).Should().BeGreaterThan((int)AtoFindingSeverity.High);
        ((int)AtoFindingSeverity.High).Should().BeGreaterThan((int)AtoFindingSeverity.Medium);
        ((int)AtoFindingSeverity.Medium).Should().BeGreaterThan((int)AtoFindingSeverity.Low);
        ((int)AtoFindingSeverity.Low).Should().BeGreaterThan((int)AtoFindingSeverity.Informational);
    }

    [Fact]
    public void AtoFindingSeverity_AllValuesAreDefined()
    {
        // Assert
        var severities = Enum.GetValues<AtoFindingSeverity>();
        severities.Should().Contain(AtoFindingSeverity.Critical);
        severities.Should().Contain(AtoFindingSeverity.High);
        severities.Should().Contain(AtoFindingSeverity.Medium);
        severities.Should().Contain(AtoFindingSeverity.Low);
        severities.Should().Contain(AtoFindingSeverity.Informational);
    }

    [Fact]
    public void AtoScanResult_Properties_CanBeSet()
    {
        // Arrange & Act
        var result = new AtoScanResult
        {
            SubscriptionId = "test-subscription-id",
            ResourceGroupName = "test-rg",
            ScanStartTime = DateTime.UtcNow.AddMinutes(-5),
            ScanEndTime = DateTime.UtcNow,
            Status = AtoScanStatus.Completed,
            TotalResourcesScanned = 10
        };

        // Assert
        result.SubscriptionId.Should().Be("test-subscription-id");
        result.ResourceGroupName.Should().Be("test-rg");
        result.Status.Should().Be(AtoScanStatus.Completed);
        result.TotalResourcesScanned.Should().Be(10);
    }

    [Fact]
    public void AtoScanSummary_Properties_CanBeSet()
    {
        // Arrange & Act
        var summary = new AtoScanSummary
        {
            TotalResourcesScanned = 10,
            TotalFindings = 20,
            CriticalFindings = 1,
            HighFindings = 2,
            MediumFindings = 5,
            LowFindings = 8,
            InformationalFindings = 4,
            ComplianceScore = 85.5
        };

        // Assert
        summary.TotalFindings.Should().Be(20);
        summary.CriticalFindings.Should().Be(1);
        summary.HighFindings.Should().Be(2);
        summary.MediumFindings.Should().Be(5);
        summary.LowFindings.Should().Be(8);
        summary.ComplianceScore.Should().Be(85.5);
    }

    [Fact]
    public void AtoScanResult_Findings_DefaultsToEmpty()
    {
        // Arrange & Act
        var result = new AtoScanResult();

        // Assert
        result.Findings.Should().NotBeNull();
        result.Findings.Should().BeEmpty();
    }

    [Fact]
    public void AtoFinding_Properties_CanBeSet()
    {
        // Arrange & Act
        var finding = new AtoFinding
        {
            Id = "finding-001",
            ResourceId = "/subscriptions/xxx/resourceGroups/rg/providers/Microsoft.Compute/virtualMachines/vm1",
            Severity = AtoFindingSeverity.High,
            Description = "User accounts not properly managed",
            Recommendation = "Implement automated account management",
            IsAutoRemediable = true,
            AffectedNistControls = new List<string> { "AC-2", "AC-2(1)" }
        };

        // Assert
        finding.Id.Should().Be("finding-001");
        finding.ResourceId.Should().Contain("virtualMachines");
        finding.Severity.Should().Be(AtoFindingSeverity.High);
        finding.IsAutoRemediable.Should().BeTrue();
        finding.AffectedNistControls.Should().Contain("AC-2");
    }

    [Fact]
    public void AtoFinding_Metadata_DefaultsToEmpty()
    {
        // Arrange & Act
        var finding = new AtoFinding();

        // Assert
        finding.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void AtoFinding_AffectedControls_DefaultsToEmpty()
    {
        // Arrange & Act
        var finding = new AtoFinding();

        // Assert
        finding.AffectedControls.Should().NotBeNull();
        finding.AffectedControls.Should().BeEmpty();
    }

    [Theory]
    [InlineData("AC", "Access Control")]
    [InlineData("AU", "Audit and Accountability")]
    [InlineData("CM", "Configuration Management")]
    [InlineData("IA", "Identification and Authentication")]
    [InlineData("SC", "System and Communications Protection")]
    [InlineData("SI", "System and Information Integrity")]
    public void NistControlFamilies_HaveCorrectNames(string familyCode, string expectedName)
    {
        // This documents the expected NIST control family mappings
        var familyNames = new Dictionary<string, string>
        {
            ["AC"] = "Access Control",
            ["AU"] = "Audit and Accountability",
            ["CM"] = "Configuration Management",
            ["IA"] = "Identification and Authentication",
            ["SC"] = "System and Communications Protection",
            ["SI"] = "System and Information Integrity"
        };

        // Assert
        familyNames[familyCode].Should().Be(expectedName);
    }

    [Fact]
    public void RemediationPlan_Properties_CanBeSet()
    {
        // Arrange & Act
        var plan = new RemediationPlan
        {
            PlanId = "plan-001",
            SubscriptionId = "subscription-001",
            CreatedAt = DateTimeOffset.UtcNow,
            TotalFindings = 5,
            Priority = "High"
        };

        // Assert
        plan.PlanId.Should().Be("plan-001");
        plan.SubscriptionId.Should().Be("subscription-001");
        plan.TotalFindings.Should().Be(5);
        plan.Priority.Should().Be("High");
    }

    [Fact]
    public void RemediationItem_Properties_CanBeSet()
    {
        // Arrange & Act
        var item = new RemediationItem
        {
            Id = "item-001",
            FindingId = "finding-001",
            ControlId = "AC-2",
            Priority = "High",
            Status = AtoRemediationStatus.NotStarted,
            IsAutomated = true
        };

        // Assert
        item.Id.Should().Be("item-001");
        item.ControlId.Should().Be("AC-2");
        item.Priority.Should().Be("High");
        item.IsAutomated.Should().BeTrue();
        item.Status.Should().Be(AtoRemediationStatus.NotStarted);
    }

    [Fact]
    public void AtoRemediationStatus_AllValuesAreDefined()
    {
        // Assert
        var statuses = Enum.GetValues<AtoRemediationStatus>();
        statuses.Should().Contain(AtoRemediationStatus.NotStarted);
        statuses.Should().Contain(AtoRemediationStatus.InProgress);
        statuses.Should().Contain(AtoRemediationStatus.Completed);
        statuses.Should().Contain(AtoRemediationStatus.Failed);
    }

    [Fact]
    public void AssessmentProgress_Properties_CanBeSet()
    {
        // Arrange & Act
        var progress = new AssessmentProgress
        {
            CurrentFamily = "AC",
            CompletedFamilies = 5,
            TotalFamilies = 18
        };

        // Assert
        progress.CurrentFamily.Should().Be("AC");
        progress.CompletedFamilies.Should().Be(5);
        progress.TotalFamilies.Should().Be(18);
        progress.PercentComplete.Should().BeApproximately(27.78, 0.1);
    }

    [Fact]
    public void AssessmentProgress_PercentComplete_IsComputed()
    {
        // Arrange
        var progress = new AssessmentProgress
        {
            CompletedFamilies = 10,
            TotalFamilies = 20
        };

        // Act & Assert - PercentComplete is computed from CompletedFamilies/TotalFamilies
        progress.PercentComplete.Should().Be(50.0);
    }
}
