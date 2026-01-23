using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Agents.Common;
using Platform.Engineering.Copilot.Agents.Infrastructure.Agents;
using Platform.Engineering.Copilot.Agents.Infrastructure.Configuration;
using Platform.Engineering.Copilot.Agents.Infrastructure.State;
using Platform.Engineering.Copilot.State.Abstractions;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents;

/// <summary>
/// Unit tests for InfrastructureAgent using Microsoft Agent Framework pattern.
/// Tests agent configuration, tool registration, and state management.
/// </summary>
public class InfrastructureAgentAgentFxTests
{
    #region InfrastructureAgentOptions Tests

    [Fact]
    public void InfrastructureAgentOptions_SectionName_IsCorrect()
    {
        // Assert
        InfrastructureAgentOptions.SectionName.Should().Be("AgentConfiguration:InfrastructureAgent");
    }

    [Fact]
    public void InfrastructureAgentOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.Temperature.Should().Be(0.4);
        options.MaxTokens.Should().Be(8000);
        options.DefaultRegion.Should().Be("eastus");
        options.EnableComplianceEnhancement.Should().BeTrue();
        options.DefaultComplianceFramework.Should().Be("FedRAMPHigh");
        options.EnablePredictiveScaling.Should().BeTrue();
        options.EnableNetworkDesign.Should().BeTrue();
        options.EnableAzureArc.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.4)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.0)]
    public void InfrastructureAgentOptions_Temperature_AcceptsValidValues(double temperature)
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions { Temperature = temperature };

        // Assert
        options.Temperature.Should().Be(temperature);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4000)]
    [InlineData(8000)]
    [InlineData(128000)]
    public void InfrastructureAgentOptions_MaxTokens_AcceptsValidValues(int maxTokens)
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions { MaxTokens = maxTokens };

        // Assert
        options.MaxTokens.Should().Be(maxTokens);
    }

    [Theory]
    [InlineData("eastus")]
    [InlineData("westus2")]
    [InlineData("usgovvirginia")]
    [InlineData("centralus")]
    public void InfrastructureAgentOptions_DefaultRegion_AcceptsValidValues(string region)
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions { DefaultRegion = region };

        // Assert
        options.DefaultRegion.Should().Be(region);
    }

    [Theory]
    [InlineData("FedRAMPHigh")]
    [InlineData("DoD IL5")]
    [InlineData("NIST80053")]
    [InlineData("SOC2")]
    [InlineData("GDPR")]
    public void InfrastructureAgentOptions_DefaultComplianceFramework_AcceptsValidValues(string framework)
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions { DefaultComplianceFramework = framework };

        // Assert
        options.DefaultComplianceFramework.Should().Be(framework);
    }

    [Fact]
    public void InfrastructureAgentOptions_WithAllFeaturesDisabled_ConfiguresCorrectly()
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions
        {
            Enabled = false,
            EnableComplianceEnhancement = false,
            EnablePredictiveScaling = false,
            EnableNetworkDesign = false,
            EnableAzureArc = false
        };

        // Assert
        options.Enabled.Should().BeFalse();
        options.EnableComplianceEnhancement.Should().BeFalse();
        options.EnablePredictiveScaling.Should().BeFalse();
        options.EnableNetworkDesign.Should().BeFalse();
        options.EnableAzureArc.Should().BeFalse();
    }

    #endregion

    #region Nested Options Tests

    [Fact]
    public void InfrastructureAgentOptions_TemplateGeneration_DefaultValues()
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions();

        // Assert
        options.TemplateGeneration.Should().NotBeNull();
    }

    [Fact]
    public void InfrastructureAgentOptions_Provisioning_DefaultValues()
    {
        // Arrange & Act
        var options = new InfrastructureAgentOptions();

        // Assert
        options.Provisioning.Should().NotBeNull();
    }

    #endregion

    #region AgentResponse Tests

    [Fact]
    public void AgentResponse_ForInfrastructure_IsCorrectlyStructured()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            Success = true,
            AgentName = "Infrastructure Agent",
            Content = "Bicep template generated successfully",
            RequiresHandoff = false
        };

        // Assert
        response.Success.Should().BeTrue();
        response.AgentName.Should().Be("Infrastructure Agent");
        response.RequiresHandoff.Should().BeFalse();
    }

    [Fact]
    public void AgentResponse_WithTemplateContent_ContainsTemplateData()
    {
        // Arrange & Act
        var templateContent = @"param location string = 'eastus'
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'mystorageaccount'
  location: location
}";

        var response = new AgentResponse
        {
            Success = true,
            AgentName = "Infrastructure Agent",
            Content = templateContent,
            RequiresHandoff = false
        };

        // Assert
        response.Content.Should().Contain("param location");
        response.Content.Should().Contain("storageAccounts");
    }

    [Fact]
    public void AgentResponse_WithHandoffToCompliance_SetsHandoffTarget()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            Success = true,
            AgentName = "Infrastructure Agent",
            Content = "Template generated. Handing off to Compliance Agent for verification.",
            RequiresHandoff = true,
            HandoffTarget = "Compliance Agent"
        };

        // Assert
        response.RequiresHandoff.Should().BeTrue();
        response.HandoffTarget.Should().Be("Compliance Agent");
    }

    [Fact]
    public void AgentResponse_WithFailure_ContainsErrorDetails()
    {
        // Arrange & Act
        var response = new AgentResponse
        {
            Success = false,
            AgentName = "Infrastructure Agent",
            Content = "Failed to generate template: Invalid resource type",
            RequiresHandoff = false
        };

        // Assert
        response.Success.Should().BeFalse();
        response.Content.Should().Contain("Failed");
    }

    #endregion
}
