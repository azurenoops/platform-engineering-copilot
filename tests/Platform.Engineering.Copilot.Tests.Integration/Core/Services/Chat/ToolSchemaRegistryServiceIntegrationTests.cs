using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models.SemanticParsing;
using Platform.Engineering.Copilot.Core.Services;

namespace Platform.Engineering.Copilot.Tests.Integration.Core.Services.Chat;

public class ToolSchemaRegistryServiceIntegrationTests
{
    private readonly Mock<ILogger<ToolSchemaRegistry>> _mockLogger;
    private readonly ToolSchemaRegistry _service;

    public ToolSchemaRegistryServiceIntegrationTests()
    {
        _mockLogger = new Mock<ILogger<ToolSchemaRegistry>>();
        _service = new ToolSchemaRegistry(_mockLogger.Object);
    }

    [Fact]
    public async Task CompleteWorkflow_RegisterSearchUpdateRemove_WorksCorrectly()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "workflow_test",
            Description = "Original description",
            Category = IntentCategory.Deployment,
            Keywords = new List<string> { "workflow", "integration" }
        };

        // Act & Assert - Register
        await _service.RegisterToolAsync(tool);
        var registered = await _service.GetToolAsync("workflow_test");
        registered.Should().NotBeNull();

        // Act & Assert - Search
        var searchResults = await _service.SearchToolsAsync("workflow");
        searchResults.Should().Contain(t => t.Name == "workflow_test");

        // Act & Assert - Update
        tool.Description = "Updated description";
        await _service.UpdateToolAsync(tool);
        var updated = await _service.GetToolAsync("workflow_test");
        updated!.Description.Should().Be("Updated description");

        // Act & Assert - Remove
        await _service.RemoveToolAsync("workflow_test");
        var removed = await _service.GetToolAsync("workflow_test");
        removed.Should().BeNull();
    }

    [Fact]
    public async Task ExportImportWorkflow_PreservesAllData()
    {
        // Arrange - Register tools
        var tool1 = new ToolSchema
        {
            Name = "export_import_1",
            Description = "Test tool 1",
            Category = IntentCategory.Infrastructure,
            Keywords = new List<string> { "test", "export" },
            Aliases = new List<string> { "ei1" },
            Parameters = new Dictionary<string, ParameterSchema>
            {
                ["param1"] = new ParameterSchema { Name = "param1", Type = "string", Required = true }
            }
        };
        var tool2 = new ToolSchema
        {
            Name = "export_import_2",
            Description = "Test tool 2",
            Category = IntentCategory.Security,
            Keywords = new List<string> { "security", "test" }
        };

        await _service.RegisterToolAsync(tool1);
        await _service.RegisterToolAsync(tool2);

        // Act - Export
        var exported = await _service.ExportSchemasAsync();

        // Clear registry and import
        await _service.RemoveToolAsync("export_import_1");
        await _service.RemoveToolAsync("export_import_2");

        await _service.ImportSchemasAsync(exported);

        // Assert
        var importedTool1 = await _service.GetToolAsync("export_import_1");
        var importedTool2 = await _service.GetToolAsync("export_import_2");

        importedTool1.Should().NotBeNull();
        importedTool1!.Description.Should().Be("Test tool 1");
        importedTool1.Keywords.Should().Contain("test");
        importedTool1.Parameters.Should().ContainKey("param1");

        importedTool2.Should().NotBeNull();
        importedTool2!.Description.Should().Be("Test tool 2");
        importedTool2.Category.Should().Be(IntentCategory.Security);
    }

    [Fact]
    public async Task MultipleCategories_IndexAndSearchCorrectly()
    {
        // Arrange
        var infraTool = new ToolSchema { Name = "infra", Category = IntentCategory.Infrastructure, Keywords = new List<string> { "infra" } };
        var secTool = new ToolSchema { Name = "sec", Category = IntentCategory.Security, Keywords = new List<string> { "security" } };
        var monTool = new ToolSchema { Name = "mon", Category = IntentCategory.Monitoring, Keywords = new List<string> { "monitor" } };
        var costTool = new ToolSchema { Name = "cost", Category = IntentCategory.Cost, Keywords = new List<string> { "cost" } };

        await _service.RegisterToolAsync(infraTool);
        await _service.RegisterToolAsync(secTool);
        await _service.RegisterToolAsync(monTool);
        await _service.RegisterToolAsync(costTool);

        // Act
        var infraTools = await _service.GetToolsByCategoryAsync(IntentCategory.Infrastructure);
        var secTools = await _service.GetToolsByCategoryAsync(IntentCategory.Security);
        var searchInfra = await _service.SearchToolsAsync("infra");
        var searchCost = await _service.SearchToolsAsync("cost");

        // Assert
        infraTools.Should().HaveCount(1);
        infraTools.Should().Contain(t => t.Name == "infra");

        secTools.Should().HaveCount(1);
        secTools.Should().Contain(t => t.Name == "sec");

        searchInfra.Should().Contain(t => t.Name == "infra");
        searchCost.Should().Contain(t => t.Name == "cost");

        var allTools = await _service.GetAllToolsAsync();
        allTools.Should().HaveCount(4);
    }
}
