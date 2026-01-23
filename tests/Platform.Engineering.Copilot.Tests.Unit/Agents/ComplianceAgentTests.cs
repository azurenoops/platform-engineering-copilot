using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Compliance.Agents;
using Platform.Engineering.Copilot.Agents.Compliance.Configuration;
using Platform.Engineering.Copilot.Agents.Compliance.State;
using Platform.Engineering.Copilot.Agents.Compliance.Tools;
using Platform.Engineering.Copilot.Agents.Configuration.Tools;
using Platform.Engineering.Copilot.State.Abstractions;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// Unit tests for ComplianceAgent using Microsoft Agent Framework pattern.
/// Tests agent initialization, tool registration, and configuration.
/// </summary>
public class ComplianceAgentTests
{
    #region Configuration Tests

    [Fact]
    public void ComplianceAgentOptions_SectionName_IsCorrect()
    {
        // Assert
        ComplianceAgentOptions.SectionName.Should().Be("AgentConfiguration:ComplianceAgent");
    }

    [Fact]
    public void ComplianceAgentOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.Temperature.Should().Be(0.2);
        options.MaxTokens.Should().Be(6000);
        options.EnableAutomatedRemediation.Should().BeTrue();
        options.DefaultFramework.Should().Be("NIST80053");
        options.DefaultBaseline.Should().Be("FedRAMPHigh");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void ComplianceAgentOptions_Temperature_AcceptsValidRange(double temperature)
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions { Temperature = temperature };

        // Assert
        options.Temperature.Should().Be(temperature);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6000)]
    [InlineData(128000)]
    public void ComplianceAgentOptions_MaxTokens_AcceptsValidRange(int maxTokens)
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions { MaxTokens = maxTokens };

        // Assert
        options.MaxTokens.Should().Be(maxTokens);
    }

    [Theory]
    [InlineData("NIST80053")]
    [InlineData("FedRAMPHigh")]
    [InlineData("DoD IL5")]
    [InlineData("SOC2")]
    [InlineData("GDPR")]
    public void ComplianceAgentOptions_DefaultFramework_AcceptsValidFrameworks(string framework)
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions { DefaultFramework = framework };

        // Assert
        options.DefaultFramework.Should().Be(framework);
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComplianceAgentOptions_EnableAutomatedRemediation_CanBeToggled(bool enabled)
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions { EnableAutomatedRemediation = enabled };

        // Assert
        options.EnableAutomatedRemediation.Should().Be(enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComplianceAgentOptions_Enabled_CanBeToggled(bool enabled)
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions { Enabled = enabled };

        // Assert
        options.Enabled.Should().Be(enabled);
    }

    #endregion

    #region Nested Options Tests

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

    [Fact]
    public void ComplianceAgentOptions_DefenderForCloud_DefaultValues()
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions();

        // Assert
        options.DefenderForCloud.Should().NotBeNull();
        options.DefenderForCloud.Enabled.Should().BeFalse(); // Disabled by default
        options.DefenderForCloud.IncludeSecureScore.Should().BeTrue();
    }

    [Fact]
    public void ComplianceAgentOptions_Assessment_DefaultValues()
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions();

        // Assert
        options.Assessment.Should().NotBeNull();
    }

    [Fact]
    public void ComplianceAgentOptions_Evidence_DefaultValues()
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions();

        // Assert
        options.Evidence.Should().NotBeNull();
    }

    [Fact]
    public void ComplianceAgentOptions_Remediation_DefaultValues()
    {
        // Arrange & Act
        var options = new ComplianceAgentOptions();

        // Assert
        options.Remediation.Should().NotBeNull();
    }

    #endregion

    #region AgentResponse Tests

    [Fact]
    public void AgentResponse_WithComplianceContext_IsCorrectlySet()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            Success = true,
            AgentName = "Compliance Agent",
            Content = "Assessment completed with 85% compliance score",
            RequiresHandoff = false
        };

        // Assert
        response.Success.Should().BeTrue();
        response.AgentName.Should().Be("Compliance Agent");
        response.RequiresHandoff.Should().BeFalse();
    }

    [Fact]
    public void AgentResponse_WithHandoff_SetsHandoffTarget()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            Success = true,
            AgentName = "Compliance Agent",
            Content = "Found issues requiring infrastructure changes",
            RequiresHandoff = true,
            HandoffTarget = "Infrastructure Agent"
        };

        // Assert
        response.RequiresHandoff.Should().BeTrue();
        response.HandoffTarget.Should().Be("Infrastructure Agent");
    }

    #endregion
}
