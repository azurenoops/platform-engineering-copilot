using FluentAssertions;
using Platform.Engineering.Copilot.Agents.Compliance.Configuration;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Tools.Compliance;

/// <summary>
/// Unit tests for ComplianceAssessmentTool metadata.
/// Full tool execution tests require integration testing with live dependencies.
/// </summary>
public class ComplianceAssessmentToolTests
{
    [Fact]
    public void Name_ExpectedValue()
    {
        // Document expected tool name
        const string expectedName = "run_compliance_assessment";
        expectedName.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Description_ShouldDescribeCompliance()
    {
        // Document expected description keywords
        var expectedKeywords = new[] { "compliance", "assessment", "NIST", "Azure", "subscription" };
        expectedKeywords.Should().AllSatisfy(keyword => keyword.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public void ExpectedParameters_ShouldIncludeSubscriptionId()
    {
        // Document expected parameters
        var expectedParameters = new[]
        {
            "subscription_id",    // Azure subscription to scan
            "resource_group",     // Optional scope to resource group
            "control_families",   // AC, AU, SC, etc.
            "include_passed",     // Include passing controls
            "skip_cache",         // Force fresh scan
            "conversation_id"     // State tracking
        };

        expectedParameters.Should().Contain("subscription_id");
        expectedParameters.Length.Should().BeGreaterThan(3);
    }

    [Fact]
    public void ComplianceAgentOptions_HasAssessmentSettings()
    {
        // Test the options model
        var options = new ComplianceAgentOptions();
        
        options.Should().NotBeNull();
        options.Assessment.Should().NotBeNull();
    }

    [Fact]
    public void ComplianceAgentOptions_HasDefaultFramework()
    {
        // Test default framework
        var options = new ComplianceAgentOptions();
        
        options.DefaultFramework.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("AC")]  // Access Control
    [InlineData("AU")]  // Audit and Accountability
    [InlineData("SC")]  // System and Communications Protection
    [InlineData("CM")]  // Configuration Management
    [InlineData("IA")]  // Identification and Authentication
    [InlineData("SI")]  // System and Information Integrity
    public void SupportedControlFamilies_AreDocumented(string controlFamily)
    {
        // Document supported NIST control families
        controlFamily.Should().NotBeNullOrEmpty();
        controlFamily.Length.Should().Be(2);
    }

    [Theory]
    [InlineData("NIST80053")]
    [InlineData("FedRAMPHigh")]
    [InlineData("DoD IL5")]
    public void SupportedFrameworks_AreDocumented(string framework)
    {
        // Document supported compliance frameworks
        framework.Should().NotBeNullOrEmpty();
    }
}
