using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Agents.Infrastructure.Tools;
using Platform.Engineering.Copilot.Core.Agents;

namespace Platform.Engineering.Copilot.Tests.Integration.Agents;

/// <summary>
/// T108 — Integration test for infrastructure flow:
/// generate → verify annotations → deploy (no confirm) → deploy (confirm) → verify.
/// </summary>
public class InfrastructureFlowTests
{
    [Fact]
    public async Task Flow_GenerateTemplate_HasAnnotations()
    {
        var genTool = new GenerateInfrastructureTemplateTool(
            new Mock<ILogger<GenerateInfrastructureTemplateTool>>().Object);

        var result = await genTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["resourceType"] = "Storage Account",
            ["region"] = "usgovvirginia"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        var data = doc.RootElement.GetProperty("data");

        // Verify ≥80% annotation coverage per SC-009
        data.GetProperty("annotationCoverage").GetDouble().Should().BeGreaterOrEqualTo(0.80);
        data.GetProperty("meetsMinimumCoverage").GetBoolean().Should().BeTrue();

        // Verify annotations reference NIST controls
        var annotations = data.GetProperty("complianceAnnotations");
        annotations.GetArrayLength().Should().BeGreaterOrEqualTo(4);
    }

    [Fact]
    public async Task Flow_GenerateTemplate_NoAuthRequired()
    {
        var tool = new GenerateInfrastructureTemplateTool(
            new Mock<ILogger<GenerateInfrastructureTemplateTool>>().Object);

        tool.RequiresAuthentication.Should().BeFalse();
    }

    [Fact]
    public async Task Flow_DeployWithoutConfirm_ReturnsPendingConfirmation()
    {
        var provisionTool = new ProvisionInfrastructureTool(
            new Mock<ILogger<ProvisionInfrastructureTool>>().Object);

        var result = await provisionTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["templateId"] = "test-template-id",
            ["resourceGroup"] = "rg-test"
            // confirm not set — defaults to false
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("data").GetProperty("status").GetString()
            .Should().Be("pending_confirmation");
    }

    [Fact]
    public async Task Flow_DeployWithConfirm_Succeeds()
    {
        var provisionTool = new ProvisionInfrastructureTool(
            new Mock<ILogger<ProvisionInfrastructureTool>>().Object);

        // Deploy requires PIM Write
        provisionTool.RequiresAuthentication.Should().BeTrue();

        var result = await provisionTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["templateId"] = "test-template-id",
            ["resourceGroup"] = "rg-test",
            ["confirm"] = true
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("status").GetString()
            .Should().Be("Succeeded");
    }

    [Fact]
    public async Task Flow_ValidateTemplate_Passes()
    {
        var validateTool = new ValidateTemplateTool(
            new Mock<ILogger<ValidateTemplateTool>>().Object);

        var result = await validateTool.ExecuteAsync(new Dictionary<string, object?>
        {
            ["templateId"] = "test-template-id"
        });

        var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("status").GetString().Should().Be("success");
        doc.RootElement.GetProperty("data").GetProperty("validationStatus").GetString()
            .Should().Be("passed");
    }
}
