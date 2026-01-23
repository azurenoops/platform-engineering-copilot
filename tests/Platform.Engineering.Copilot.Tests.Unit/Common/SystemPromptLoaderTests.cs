using FluentAssertions;
using Platform.Engineering.Copilot.Agents.Common;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Common;

/// <summary>
/// Unit tests for SystemPromptLoader utility class.
/// </summary>
public class SystemPromptLoaderTests : IDisposable
{
    private readonly string _tempDir;
    
    public SystemPromptLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        SystemPromptLoader.ClearCache();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    #region LoadFromFile Tests

    [Fact]
    public void LoadFromFile_WithExistingFile_ReturnsContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "test-prompt.txt");
        var expectedContent = "You are a helpful assistant.";
        File.WriteAllText(filePath, expectedContent);

        // Act
        var result = SystemPromptLoader.LoadFromFile(filePath);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public void LoadFromFile_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "non-existent.txt");

        // Act
        var result = SystemPromptLoader.LoadFromFile(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void LoadFromFile_WithCaching_ReturnsCachedContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "cached-prompt.txt");
        var originalContent = "Original content";
        File.WriteAllText(filePath, originalContent);

        // First load
        var firstResult = SystemPromptLoader.LoadFromFile(filePath, useCache: true);

        // Modify file
        File.WriteAllText(filePath, "Modified content");

        // Second load (should return cached)
        var secondResult = SystemPromptLoader.LoadFromFile(filePath, useCache: true);

        // Assert
        firstResult.Should().Be(originalContent);
        secondResult.Should().Be(originalContent); // Cached!
    }

    [Fact]
    public void LoadFromFile_WithoutCaching_ReturnsFreshContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "no-cache-prompt.txt");
        var originalContent = "Original content";
        File.WriteAllText(filePath, originalContent);

        // First load
        var firstResult = SystemPromptLoader.LoadFromFile(filePath, useCache: false);

        // Modify file
        var modifiedContent = "Modified content";
        File.WriteAllText(filePath, modifiedContent);

        // Second load (should return fresh)
        var secondResult = SystemPromptLoader.LoadFromFile(filePath, useCache: false);

        // Assert
        firstResult.Should().Be(originalContent);
        secondResult.Should().Be(modifiedContent);
    }

    #endregion

    #region ApplyVariables Tests

    [Fact]
    public void ApplyVariables_WithDictionary_SubstitutesVariables()
    {
        // Arrange
        var template = "Hello {{name}}, you are a {{role}}.";
        var variables = new Dictionary<string, string>
        {
            ["name"] = "Alice",
            ["role"] = "developer"
        };

        // Act
        var result = SystemPromptLoader.ApplyVariables(template, variables);

        // Assert
        result.Should().Be("Hello Alice, you are a developer.");
    }

    [Fact]
    public void ApplyVariables_WithAnonymousObject_SubstitutesVariables()
    {
        // Arrange
        var template = "The {{agentName}} is configured with temperature {{temperature}}.";

        // Act
        var result = SystemPromptLoader.ApplyVariables(template, new 
        { 
            agentName = "ComplianceAgent", 
            temperature = "0.2" 
        });

        // Assert
        result.Should().Be("The ComplianceAgent is configured with temperature 0.2.");
    }

    [Fact]
    public void ApplyVariables_WithEmptyTemplate_ReturnsEmptyString()
    {
        // Act
        var result = SystemPromptLoader.ApplyVariables("", new { name = "test" });

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ApplyVariables_WithNullVariables_ReturnsOriginalTemplate()
    {
        // Arrange
        var template = "Hello {{name}}!";

        // Act
        var result = SystemPromptLoader.ApplyVariables(template, (IDictionary<string, string>)null!);

        // Assert
        result.Should().Be(template);
    }

    [Fact]
    public void ApplyVariables_IgnoresCase_SubstitutesCorrectly()
    {
        // Arrange
        var template = "Hello {{NAME}} and {{Name}} and {{name}}.";
        var variables = new Dictionary<string, string> { ["name"] = "Test" };

        // Act
        var result = SystemPromptLoader.ApplyVariables(template, variables);

        // Assert
        result.Should().Be("Hello Test and Test and Test.");
    }

    #endregion

    #region BuildFromSections Tests

    [Fact]
    public void BuildFromSections_WithMultipleSections_BuildsCorrectly()
    {
        // Act
        var result = SystemPromptLoader.BuildFromSections(
            ("Role", "You are an engineer."),
            ("Rules", "Follow best practices.")
        );

        // Assert
        result.Should().Contain("## Role");
        result.Should().Contain("You are an engineer.");
        result.Should().Contain("## Rules");
        result.Should().Contain("Follow best practices.");
    }

    [Fact]
    public void BuildFromSections_WithEmptyContent_SkipsSection()
    {
        // Act
        var result = SystemPromptLoader.BuildFromSections(
            ("Visible", "This is visible."),
            ("Hidden", ""),
            ("Also Visible", "Also here.")
        );

        // Assert
        result.Should().Contain("## Visible");
        result.Should().NotContain("## Hidden");
        result.Should().Contain("## Also Visible");
    }

    [Fact]
    public void BuildFromSections_WithEmptyHeader_OmitsHeader()
    {
        // Act
        var result = SystemPromptLoader.BuildFromSections(
            ("", "This is a preamble without a header.")
        );

        // Assert
        result.Should().NotStartWith("##");
        result.Should().Contain("This is a preamble");
    }

    #endregion

    #region ClearCache Tests

    [Fact]
    public void ClearCache_AfterLoad_ReturnsFreshContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "clear-cache-test.txt");
        var originalContent = "Original";
        File.WriteAllText(filePath, originalContent);

        // First load
        var firstResult = SystemPromptLoader.LoadFromFile(filePath);

        // Modify file
        var modifiedContent = "Modified";
        File.WriteAllText(filePath, modifiedContent);

        // Clear cache
        SystemPromptLoader.ClearCache();

        // Second load
        var secondResult = SystemPromptLoader.LoadFromFile(filePath);

        // Assert
        firstResult.Should().Be(originalContent);
        secondResult.Should().Be(modifiedContent);
    }

    #endregion

    #region LoadOrDefault Tests

    [Fact]
    public void LoadOrDefault_WithExistingFile_ReturnsFileContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "exists.txt");
        var content = "File content";
        File.WriteAllText(filePath, content);

        // Act
        var result = SystemPromptLoader.LoadOrDefault(filePath, "default");

        // Assert
        result.Should().Be(content);
    }

    [Fact]
    public void LoadOrDefault_WithNonExistentFile_ReturnsDefault()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "does-not-exist.txt");

        // Act
        var result = SystemPromptLoader.LoadOrDefault(filePath, "default prompt");

        // Assert
        result.Should().Be("default prompt");
    }

    #endregion

    #region Async Loading Tests

    [Fact]
    public async Task LoadFromFileAsync_WithExistingFile_ReturnsContent()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "async-test.txt");
        var expectedContent = "Async loaded content";
        await File.WriteAllTextAsync(filePath, expectedContent);

        // Act
        var result = await SystemPromptLoader.LoadFromFileAsync(filePath);

        // Assert
        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task LoadFromFileAsync_WithNonExistentFile_ReturnsNull()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "async-not-found.txt");

        // Act
        var result = await SystemPromptLoader.LoadFromFileAsync(filePath);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task LoadOrDefaultAsync_WithNonExistentFile_ReturnsDefault()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "async-default.txt");

        // Act
        var result = await SystemPromptLoader.LoadOrDefaultAsync(filePath, "async default");

        // Assert
        result.Should().Be("async default");
    }

    #endregion

    #region ProcessIncludes Tests

    [Fact]
    public void ProcessIncludes_WithIncludeDirective_ResolvesInclude()
    {
        // Arrange
        var includeFile = Path.Combine(_tempDir, "included.txt");
        File.WriteAllText(includeFile, "Included content");

        var template = $"Before {{{{include:{includeFile}}}}} After";

        // Act
        var result = SystemPromptLoader.ProcessIncludes(template);

        // Assert
        result.Should().Contain("Before");
        result.Should().Contain("Included content");
        result.Should().Contain("After");
    }

    [Fact]
    public void ProcessIncludes_WithMissingInclude_ReturnsPlaceholder()
    {
        // Arrange
        var template = "Before {{include:missing.txt}} After";

        // Act
        var result = SystemPromptLoader.ProcessIncludes(template, _tempDir);

        // Assert
        result.Should().Contain("[INCLUDE NOT FOUND:");
    }

    [Fact]
    public void ProcessIncludes_WithNestedIncludes_ResolvesRecursively()
    {
        // Arrange
        var innerFile = Path.Combine(_tempDir, "inner.txt");
        File.WriteAllText(innerFile, "Inner content");

        var outerFile = Path.Combine(_tempDir, "outer.txt");
        File.WriteAllText(outerFile, $"Outer [{{{{include:{innerFile}}}}}] Outer");

        var template = $"Main [{{{{include:{outerFile}}}}}] Main";

        // Act
        var result = SystemPromptLoader.ProcessIncludes(template);

        // Assert
        result.Should().Contain("Main");
        result.Should().Contain("Outer");
        result.Should().Contain("Inner content");
    }

    #endregion

    #region BuildWithContext Tests

    [Fact]
    public void BuildWithContext_WithSections_AppendsContext()
    {
        // Arrange
        var basePrompt = "You are a helpful assistant.";

        // Act
        var result = SystemPromptLoader.BuildWithContext(
            basePrompt,
            ("Current Task", "Analyze the code"),
            ("Constraints", "Be concise")
        );

        // Assert
        result.Should().StartWith(basePrompt);
        result.Should().Contain("## Current Task");
        result.Should().Contain("Analyze the code");
        result.Should().Contain("## Constraints");
        result.Should().Contain("Be concise");
    }

    [Fact]
    public void BuildWithContext_WithNoSections_ReturnsBasePrompt()
    {
        // Arrange
        var basePrompt = "You are a helpful assistant.";

        // Act
        var result = SystemPromptLoader.BuildWithContext(basePrompt);

        // Assert
        result.Should().Be(basePrompt);
    }

    #endregion

    #region Cache Statistics Tests

    [Fact]
    public void GetCacheStatistics_AfterLoading_ReturnsCorrectStats()
    {
        // Arrange
        SystemPromptLoader.ClearCache();
        var file1 = Path.Combine(_tempDir, "stat1.txt");
        var file2 = Path.Combine(_tempDir, "stat2.txt");
        File.WriteAllText(file1, "Content 1");
        File.WriteAllText(file2, "Content 2");

        SystemPromptLoader.LoadFromFile(file1);
        SystemPromptLoader.LoadFromFile(file2);

        // Act
        var stats = SystemPromptLoader.GetCacheStatistics();

        // Assert
        stats.EntryCount.Should().BeGreaterThanOrEqualTo(2);
        stats.TotalCharacters.Should().BeGreaterThan(0);
        stats.Keys.Should().Contain(k => k.Contains("stat1.txt"));
    }

    [Fact]
    public void RemoveFromCache_WithExistingKey_ReturnsTrue()
    {
        // Arrange
        var filePath = Path.Combine(_tempDir, "to-remove.txt");
        File.WriteAllText(filePath, "Content");
        SystemPromptLoader.LoadFromFile(filePath);

        var cacheKey = $"file:{filePath}";

        // Act
        var removed = SystemPromptLoader.RemoveFromCache(cacheKey);

        // Assert
        removed.Should().BeTrue();
    }

    [Fact]
    public void RemoveFromCache_WithNonExistentKey_ReturnsFalse()
    {
        // Act
        var removed = SystemPromptLoader.RemoveFromCache("nonexistent:key");

        // Assert
        removed.Should().BeFalse();
    }

    #endregion

    #region PrewarmCache Tests

    [Fact]
    public void PrewarmCache_WithDirectory_LoadsAllFiles()
    {
        // Arrange
        SystemPromptLoader.ClearCache();
        var subDir = Path.Combine(_tempDir, "prompts");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "p1.txt"), "Prompt 1");
        File.WriteAllText(Path.Combine(subDir, "p2.txt"), "Prompt 2");
        File.WriteAllText(Path.Combine(subDir, "other.md"), "Markdown");

        // Act
        var count = SystemPromptLoader.PrewarmCache(subDir, "*.txt");

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void PrewarmCache_WithNonExistentDirectory_ReturnsZero()
    {
        // Act
        var count = SystemPromptLoader.PrewarmCache("/nonexistent/path");

        // Assert
        count.Should().Be(0);
    }

    #endregion
}