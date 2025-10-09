using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Models.SemanticParsing;
using Platform.Engineering.Copilot.Core.Services;
using System.Text.Json;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services.Chat;

public class ToolSchemaRegistryServiceTests
{
    private readonly Mock<ILogger<ToolSchemaRegistry>> _mockLogger;
    private readonly ToolSchemaRegistry _service;

    public ToolSchemaRegistryServiceTests()
    {
        _mockLogger = new Mock<ILogger<ToolSchemaRegistry>>();
        _service = new ToolSchemaRegistry(_mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidLogger_CreatesService()
    {
        // Arrange & Act
        var service = new ToolSchemaRegistry(_mockLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region RegisterToolAsync Tests

    [Fact]
    public async Task RegisterToolAsync_WithValidTool_RegistersSuccessfully()
    {
        // Arrange
        var toolSchema = new ToolSchema
        {
            Name = "deploy_vm",
            Description = "Deploy a virtual machine",
            Category = IntentCategory.Infrastructure,
            Keywords = new List<string> { "deploy", "vm", "virtual machine" },
            Aliases = new List<string> { "create_vm" },
            Parameters = new Dictionary<string, ParameterSchema>
            {
                ["vmName"] = new ParameterSchema { Name = "vmName", Type = "string", Required = true }
            }
        };

        // Act
        await _service.RegisterToolAsync(toolSchema);

        // Assert
        var retrieved = await _service.GetToolAsync("deploy_vm");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("deploy_vm");
        retrieved.Description.Should().Be("Deploy a virtual machine");
        retrieved.Category.Should().Be(IntentCategory.Infrastructure);
    }

    [Fact]
    public async Task RegisterToolAsync_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var toolSchema = new ToolSchema
        {
            Name = "",
            Description = "Test tool"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.RegisterToolAsync(toolSchema));
    }

    [Fact]
    public async Task RegisterToolAsync_WithNullName_ThrowsArgumentException()
    {
        // Arrange
        var toolSchema = new ToolSchema
        {
            Name = null!,
            Description = "Test tool"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            async () => await _service.RegisterToolAsync(toolSchema));
    }

    [Fact]
    public async Task RegisterToolAsync_DuplicateTool_OverwritesExisting()
    {
        // Arrange
        var originalTool = new ToolSchema
        {
            Name = "test_tool",
            Description = "Original description",
            Category = IntentCategory.Infrastructure
        };

        var updatedTool = new ToolSchema
        {
            Name = "test_tool",
            Description = "Updated description",
            Category = IntentCategory.Security
        };

        // Act
        await _service.RegisterToolAsync(originalTool);
        await _service.RegisterToolAsync(updatedTool);

        // Assert
        var retrieved = await _service.GetToolAsync("test_tool");
        retrieved.Should().NotBeNull();
        retrieved!.Description.Should().Be("Updated description");
        retrieved.Category.Should().Be(IntentCategory.Security);
    }

    [Fact]
    public async Task RegisterToolAsync_WithKeywordsAndAliases_IndexesBoth()
    {
        // Arrange
        var toolSchema = new ToolSchema
        {
            Name = "scan_security",
            Description = "Security scan",
            Category = IntentCategory.Security,
            Keywords = new List<string> { "scan", "audit" },
            Aliases = new List<string> { "security_audit", "vulnerability_scan" }
        };

        // Act
        await _service.RegisterToolAsync(toolSchema);

        // Assert - Should be findable by keyword
        var byKeyword = await _service.SearchToolsAsync("scan");
        byKeyword.Should().Contain(t => t.Name == "scan_security");

        // Should be findable by alias
        var byAlias = await _service.SearchToolsAsync("security_audit");
        byAlias.Should().Contain(t => t.Name == "scan_security");

        // Should be findable by tool name
        var byName = await _service.SearchToolsAsync("scan_security");
        byName.Should().Contain(t => t.Name == "scan_security");
    }

    #endregion

    #region GetAllToolsAsync Tests

    [Fact]
    public async Task GetAllToolsAsync_WithNoTools_ReturnsEmptyList()
    {
        // Act
        var tools = await _service.GetAllToolsAsync();

        // Assert
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllToolsAsync_WithMultipleTools_ReturnsAll()
    {
        // Arrange
        var tool1 = new ToolSchema { Name = "tool1", Description = "Tool 1", Category = IntentCategory.Infrastructure };
        var tool2 = new ToolSchema { Name = "tool2", Description = "Tool 2", Category = IntentCategory.Security };
        var tool3 = new ToolSchema { Name = "tool3", Description = "Tool 3", Category = IntentCategory.Monitoring };

        await _service.RegisterToolAsync(tool1);
        await _service.RegisterToolAsync(tool2);
        await _service.RegisterToolAsync(tool3);

        // Act
        var tools = await _service.GetAllToolsAsync();

        // Assert
        tools.Should().HaveCount(3);
        tools.Should().Contain(t => t.Name == "tool1");
        tools.Should().Contain(t => t.Name == "tool2");
        tools.Should().Contain(t => t.Name == "tool3");
    }

    #endregion

    #region GetToolAsync Tests

    [Fact]
    public async Task GetToolAsync_WithExistingTool_ReturnsTool()
    {
        // Arrange
        var toolSchema = new ToolSchema
        {
            Name = "existing_tool",
            Description = "Test tool",
            Category = IntentCategory.Deployment
        };
        await _service.RegisterToolAsync(toolSchema);

        // Act
        var retrieved = await _service.GetToolAsync("existing_tool");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("existing_tool");
        retrieved.Description.Should().Be("Test tool");
    }

    [Fact]
    public async Task GetToolAsync_WithNonExistingTool_ReturnsNull()
    {
        // Act
        var retrieved = await _service.GetToolAsync("nonexistent_tool");

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region GetToolsByCategoryAsync Tests

    [Fact]
    public async Task GetToolsByCategoryAsync_WithMatchingCategory_ReturnsTools()
    {
        // Arrange
        var infraTool1 = new ToolSchema { Name = "infra1", Category = IntentCategory.Infrastructure };
        var infraTool2 = new ToolSchema { Name = "infra2", Category = IntentCategory.Infrastructure };
        var securityTool = new ToolSchema { Name = "sec1", Category = IntentCategory.Security };

        await _service.RegisterToolAsync(infraTool1);
        await _service.RegisterToolAsync(infraTool2);
        await _service.RegisterToolAsync(securityTool);

        // Act
        var infraTools = await _service.GetToolsByCategoryAsync(IntentCategory.Infrastructure);

        // Assert
        infraTools.Should().HaveCount(2);
        infraTools.Should().Contain(t => t.Name == "infra1");
        infraTools.Should().Contain(t => t.Name == "infra2");
        infraTools.Should().NotContain(t => t.Name == "sec1");
    }

    [Fact]
    public async Task GetToolsByCategoryAsync_WithNoMatchingCategory_ReturnsEmpty()
    {
        // Arrange
        var infraTool = new ToolSchema { Name = "infra1", Category = IntentCategory.Infrastructure };
        await _service.RegisterToolAsync(infraTool);

        // Act
        var costTools = await _service.GetToolsByCategoryAsync(IntentCategory.Cost);

        // Assert
        costTools.Should().BeEmpty();
    }

    [Theory]
    [InlineData(IntentCategory.Infrastructure)]
    [InlineData(IntentCategory.Security)]
    [InlineData(IntentCategory.Monitoring)]
    [InlineData(IntentCategory.Cost)]
    [InlineData(IntentCategory.Deployment)]
    [InlineData(IntentCategory.Configuration)]
    public async Task GetToolsByCategoryAsync_WithVariousCategories_FiltersCorrectly(IntentCategory category)
    {
        // Arrange
        var tool = new ToolSchema { Name = $"tool_{category}", Category = category };
        await _service.RegisterToolAsync(tool);

        // Act
        var tools = await _service.GetToolsByCategoryAsync(category);

        // Assert
        tools.Should().HaveCount(1);
        tools.Should().Contain(t => t.Category == category);
    }

    #endregion

    #region SearchToolsAsync Tests

    [Fact]
    public async Task SearchToolsAsync_WithNullKeywords_ReturnsEmpty()
    {
        // Act
        var results = await _service.SearchToolsAsync(null!);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchToolsAsync_WithEmptyKeywords_ReturnsEmpty()
    {
        // Act
        var results = await _service.SearchToolsAsync();

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchToolsAsync_WithExactKeywordMatch_ReturnsTool()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "deploy_vm",
            Keywords = new List<string> { "deploy", "virtual", "machine" }
        };
        await _service.RegisterToolAsync(tool);

        // Act
        var results = await _service.SearchToolsAsync("deploy");

        // Assert
        results.Should().HaveCount(1);
        results.Should().Contain(t => t.Name == "deploy_vm");
    }

    [Fact]
    public async Task SearchToolsAsync_WithPartialKeywordMatch_ReturnsTool()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "kubernetes_deploy",
            Keywords = new List<string> { "kubernetes", "k8s", "container" }
        };
        await _service.RegisterToolAsync(tool);

        // Act
        var results = await _service.SearchToolsAsync("kube");

        // Assert
        results.Should().Contain(t => t.Name == "kubernetes_deploy");
    }

    [Fact]
    public async Task SearchToolsAsync_WithMultipleKeywords_ReturnsAllMatches()
    {
        // Arrange
        var vmTool = new ToolSchema { Name = "deploy_vm", Keywords = new List<string> { "vm", "virtual" } };
        var containerTool = new ToolSchema { Name = "deploy_container", Keywords = new List<string> { "container", "docker" } };
        var dbTool = new ToolSchema { Name = "deploy_db", Keywords = new List<string> { "database", "sql" } };

        await _service.RegisterToolAsync(vmTool);
        await _service.RegisterToolAsync(containerTool);
        await _service.RegisterToolAsync(dbTool);

        // Act
        var results = await _service.SearchToolsAsync("vm", "container");

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(t => t.Name == "deploy_vm");
        results.Should().Contain(t => t.Name == "deploy_container");
        results.Should().NotContain(t => t.Name == "deploy_db");
    }

    [Fact]
    public async Task SearchToolsAsync_CaseInsensitive_FindsMatches()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "Deploy_VM",
            Keywords = new List<string> { "DEPLOY", "Virtual", "Machine" }
        };
        await _service.RegisterToolAsync(tool);

        // Act
        var results = await _service.SearchToolsAsync("deploy", "VIRTUAL");

        // Assert
        results.Should().Contain(t => t.Name == "Deploy_VM");
    }

    [Fact]
    public async Task SearchToolsAsync_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "deploy_vm",
            Keywords = new List<string> { "vm", "virtual" }
        };
        await _service.RegisterToolAsync(tool);

        // Act
        var results = await _service.SearchToolsAsync("nonexistent", "keyword");

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task SearchToolsAsync_ByToolName_FindsTool()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "security_scanner",
            Keywords = new List<string> { "scan" }
        };
        await _service.RegisterToolAsync(tool);

        // Act
        var results = await _service.SearchToolsAsync("security_scanner");

        // Assert
        results.Should().Contain(t => t.Name == "security_scanner");
    }

    #endregion

    #region UpdateToolAsync Tests

    [Fact]
    public async Task UpdateToolAsync_WithExistingTool_UpdatesSuccessfully()
    {
        // Arrange
        var originalTool = new ToolSchema
        {
            Name = "update_test",
            Description = "Original",
            Category = IntentCategory.Infrastructure
        };

        var updatedTool = new ToolSchema
        {
            Name = "update_test",
            Description = "Updated",
            Category = IntentCategory.Security
        };

        await _service.RegisterToolAsync(originalTool);

        // Act
        await _service.UpdateToolAsync(updatedTool);

        // Assert
        var retrieved = await _service.GetToolAsync("update_test");
        retrieved.Should().NotBeNull();
        retrieved!.Description.Should().Be("Updated");
        retrieved.Category.Should().Be(IntentCategory.Security);
    }

    [Fact]
    public async Task UpdateToolAsync_WithNewTool_RegistersAsNew()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "new_tool",
            Description = "New tool",
            Category = IntentCategory.Monitoring
        };

        // Act
        await _service.UpdateToolAsync(tool);

        // Assert
        var retrieved = await _service.GetToolAsync("new_tool");
        retrieved.Should().NotBeNull();
        retrieved!.Name.Should().Be("new_tool");
    }

    #endregion

    #region RemoveToolAsync Tests

    [Fact]
    public async Task RemoveToolAsync_WithExistingTool_RemovesSuccessfully()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "remove_test",
            Description = "To be removed",
            Category = IntentCategory.Infrastructure,
            Keywords = new List<string> { "remove", "test" }
        };
        await _service.RegisterToolAsync(tool);

        // Act
        await _service.RemoveToolAsync("remove_test");

        // Assert
        var retrieved = await _service.GetToolAsync("remove_test");
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task RemoveToolAsync_RemovesCategoryIndex()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "category_test",
            Category = IntentCategory.Compliance
        };
        await _service.RegisterToolAsync(tool);

        // Act
        await _service.RemoveToolAsync("category_test");

        // Assert
        var categoryTools = await _service.GetToolsByCategoryAsync(IntentCategory.Compliance);
        categoryTools.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveToolAsync_RemovesKeywordIndex()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "keyword_test",
            Keywords = new List<string> { "unique_keyword_12345" }
        };
        await _service.RegisterToolAsync(tool);

        // Act
        await _service.RemoveToolAsync("keyword_test");

        // Assert
        var searchResults = await _service.SearchToolsAsync("unique_keyword_12345");
        searchResults.Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveToolAsync_WithNonExistingTool_DoesNotThrow()
    {
        // Act & Assert
        await _service.RemoveToolAsync("nonexistent_tool");
        // Should not throw
    }

    [Fact]
    public async Task RemoveToolAsync_WithMultipleToolsInCategory_OnlyRemovesOne()
    {
        // Arrange
        var tool1 = new ToolSchema { Name = "infra1", Category = IntentCategory.Infrastructure };
        var tool2 = new ToolSchema { Name = "infra2", Category = IntentCategory.Infrastructure };

        await _service.RegisterToolAsync(tool1);
        await _service.RegisterToolAsync(tool2);

        // Act
        await _service.RemoveToolAsync("infra1");

        // Assert
        var retrieved1 = await _service.GetToolAsync("infra1");
        var retrieved2 = await _service.GetToolAsync("infra2");
        
        retrieved1.Should().BeNull();
        retrieved2.Should().NotBeNull();

        var categoryTools = await _service.GetToolsByCategoryAsync(IntentCategory.Infrastructure);
        categoryTools.Should().HaveCount(1);
        categoryTools.Should().Contain(t => t.Name == "infra2");
    }

    #endregion

    #region GetStatisticsAsync Tests

    [Fact]
    public async Task GetStatisticsAsync_WithNoTools_ReturnsZeroCounts()
    {
        // Act
        var stats = await _service.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        
        // Use reflection to access anonymous type properties
        var totalToolsProperty = stats.GetType().GetProperty("TotalTools");
        var totalKeywordsProperty = stats.GetType().GetProperty("TotalKeywords");
        
        totalToolsProperty.Should().NotBeNull();
        totalKeywordsProperty.Should().NotBeNull();
        
        var totalTools = (int)totalToolsProperty!.GetValue(stats)!;
        var totalKeywords = (int)totalKeywordsProperty!.GetValue(stats)!;
        
        totalTools.Should().Be(0);
        totalKeywords.Should().Be(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_WithMultipleTools_ReturnsCorrectCounts()
    {
        // Arrange
        var tool1 = new ToolSchema
        {
            Name = "tool1",
            Category = IntentCategory.Infrastructure,
            Keywords = new List<string> { "k1", "k2" }
        };
        var tool2 = new ToolSchema
        {
            Name = "tool2",
            Category = IntentCategory.Infrastructure,
            Keywords = new List<string> { "k3" }
        };
        var tool3 = new ToolSchema
        {
            Name = "tool3",
            Category = IntentCategory.Security,
            Keywords = new List<string> { "k4", "k5" }
        };

        await _service.RegisterToolAsync(tool1);
        await _service.RegisterToolAsync(tool2);
        await _service.RegisterToolAsync(tool3);

        // Act
        var stats = await _service.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        
        // Use reflection to access anonymous type properties
        var totalToolsProperty = stats.GetType().GetProperty("TotalTools");
        var totalKeywordsProperty = stats.GetType().GetProperty("TotalKeywords");
        
        totalToolsProperty.Should().NotBeNull();
        totalKeywordsProperty.Should().NotBeNull();
        
        var totalTools = (int)totalToolsProperty!.GetValue(stats)!;
        var totalKeywords = (int)totalKeywordsProperty!.GetValue(stats)!;
        
        totalTools.Should().Be(3);
        totalKeywords.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetStatisticsAsync_IncludesCategoryBreakdown()
    {
        // Arrange
        var infraTool1 = new ToolSchema { Name = "infra1", Category = IntentCategory.Infrastructure };
        var infraTool2 = new ToolSchema { Name = "infra2", Category = IntentCategory.Infrastructure };
        var securityTool = new ToolSchema { Name = "sec1", Category = IntentCategory.Security };

        await _service.RegisterToolAsync(infraTool1);
        await _service.RegisterToolAsync(infraTool2);
        await _service.RegisterToolAsync(securityTool);

        // Act
        var stats = await _service.GetStatisticsAsync();

        // Assert
        stats.Should().NotBeNull();
        
        // Use reflection to access anonymous type properties
        var toolsByCategoryProperty = stats.GetType().GetProperty("ToolsByCategory");
        toolsByCategoryProperty.Should().NotBeNull();
        
        var toolsByCategory = toolsByCategoryProperty!.GetValue(stats) as IDictionary<string, int>;
        
        toolsByCategory.Should().NotBeNull();
        toolsByCategory.Should().ContainKey("Infrastructure");
        toolsByCategory.Should().ContainKey("Security");
        toolsByCategory!["Infrastructure"].Should().Be(2);
        toolsByCategory["Security"].Should().Be(1);
    }

    #endregion

    #region ExportSchemasAsync Tests

    [Fact]
    public async Task ExportSchemasAsync_WithNoTools_ReturnsEmptyJsonArray()
    {
        // Act
        var json = await _service.ExportSchemasAsync();

        // Assert
        json.Should().NotBeNullOrEmpty();
        var schemas = JsonSerializer.Deserialize<ToolSchema[]>(json);
        schemas.Should().BeEmpty();
    }

    [Fact]
    public async Task ExportSchemasAsync_WithTools_ReturnsValidJson()
    {
        // Arrange
        var tool = new ToolSchema
        {
            Name = "export_test",
            Description = "Test export",
            Category = IntentCategory.Deployment,
            Keywords = new List<string> { "export", "test" },
            Parameters = new Dictionary<string, ParameterSchema>
            {
                ["param1"] = new ParameterSchema { Name = "param1", Type = "string", Required = true }
            }
        };
        await _service.RegisterToolAsync(tool);

        // Act
        var json = await _service.ExportSchemasAsync();

        // Assert
        json.Should().NotBeNullOrEmpty();
        var schemas = JsonSerializer.Deserialize<ToolSchema[]>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        schemas.Should().HaveCount(1);
        schemas![0].Name.Should().Be("export_test");
        schemas[0].Description.Should().Be("Test export");
        schemas[0].Parameters.Should().ContainKey("param1");
    }

    [Fact]
    public async Task ExportSchemasAsync_ProducesIndentedJson()
    {
        // Arrange
        var tool = new ToolSchema { Name = "test", Description = "Test" };
        await _service.RegisterToolAsync(tool);

        // Act
        var json = await _service.ExportSchemasAsync();

        // Assert
        json.Should().Contain("\n"); // Indented JSON has newlines
        json.Should().Contain("  "); // Indented JSON has spaces
    }

    #endregion

    #region ImportSchemasAsync Tests

    [Fact]
    public async Task ImportSchemasAsync_WithValidJson_ImportsTools()
    {
        // Arrange
        var schemas = new[]
        {
            new ToolSchema
            {
                Name = "import_test1",
                Description = "Import test 1",
                Category = IntentCategory.Infrastructure
            },
            new ToolSchema
            {
                Name = "import_test2",
                Description = "Import test 2",
                Category = IntentCategory.Security
            }
        };

        var json = JsonSerializer.Serialize(schemas, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        await _service.ImportSchemasAsync(json);

        // Assert
        var allTools = await _service.GetAllToolsAsync();
        allTools.Should().HaveCount(2);
        allTools.Should().Contain(t => t.Name == "import_test1");
        allTools.Should().Contain(t => t.Name == "import_test2");
    }

    [Fact]
    public async Task ImportSchemasAsync_WithEmptyJson_DoesNotThrow()
    {
        // Arrange
        var json = "[]";

        // Act & Assert
        await _service.ImportSchemasAsync(json);
        
        var allTools = await _service.GetAllToolsAsync();
        allTools.Should().BeEmpty();
    }

    [Fact]
    public async Task ImportSchemasAsync_WithInvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{ invalid json }";

        // Act & Assert
        await Assert.ThrowsAsync<JsonException>(
            async () => await _service.ImportSchemasAsync(invalidJson));
    }

    [Fact]
    public async Task ImportSchemasAsync_OverwritesExistingTools()
    {
        // Arrange
        var existingTool = new ToolSchema
        {
            Name = "overwrite_test",
            Description = "Original",
            Category = IntentCategory.Infrastructure
        };
        await _service.RegisterToolAsync(existingTool);

        var importSchemas = new[]
        {
            new ToolSchema
            {
                Name = "overwrite_test",
                Description = "Imported",
                Category = IntentCategory.Security
            }
        };

        var json = JsonSerializer.Serialize(importSchemas, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Act
        await _service.ImportSchemasAsync(json);

        // Assert
        var retrieved = await _service.GetToolAsync("overwrite_test");
        retrieved.Should().NotBeNull();
        retrieved!.Description.Should().Be("Imported");
        retrieved.Category.Should().Be(IntentCategory.Security);
    }

    #endregion
}
