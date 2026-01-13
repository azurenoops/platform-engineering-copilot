using FluentAssertions;
using Platform.Engineering.Copilot.Core.Models.ServiceTemplates;
using System.Text.RegularExpressions;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Agents.Environments;

/// <summary>
/// Unit tests for GitTemplateSyncService helper logic
/// Tests URL parsing and template format detection
/// </summary>
public class GitTemplateSyncServiceTests
{
    #region URL Parsing Tests

    [Theory]
    [InlineData("https://github.com/org/repo/blob/main/path/file.bicep", "org", "repo", "path/file.bicep", "main")]
    [InlineData("https://github.com/myorg/templates/blob/develop/bicep/webapp.bicep", "myorg", "templates", "bicep/webapp.bicep", "develop")]
    public void ParseGitHubBlobUrl_ValidUrls_ParsesCorrectly(
        string url,
        string expectedOwner,
        string expectedRepo,
        string expectedPath,
        string expectedBranch)
    {
        // Act
        var parsed = ParseGitHubUrl(url);

        // Assert
        parsed.owner.Should().Be(expectedOwner);
        parsed.repo.Should().Be(expectedRepo);
        parsed.path.Should().Be(expectedPath);
        parsed.branch.Should().Be(expectedBranch);
    }

    [Theory]
    [InlineData("https://raw.githubusercontent.com/org/repo/main/file.bicep", "org", "repo", "file.bicep", "main")]
    [InlineData("https://raw.githubusercontent.com/azure/bicep-registry/main/modules/compute/vm.bicep", "azure", "bicep-registry", "modules/compute/vm.bicep", "main")]
    public void ParseGitHubRawUrl_ValidUrls_ParsesCorrectly(
        string url,
        string expectedOwner,
        string expectedRepo,
        string expectedPath,
        string expectedBranch)
    {
        // Act
        var parsed = ParseGitHubRawUrl(url);

        // Assert
        parsed.owner.Should().Be(expectedOwner);
        parsed.repo.Should().Be(expectedRepo);
        parsed.path.Should().Be(expectedPath);
        parsed.branch.Should().Be(expectedBranch);
    }

    [Theory]
    [InlineData("not-a-valid-url")]
    [InlineData("https://example.com/some/path")]
    [InlineData("https://gitlab.com/org/repo/blob/main/file.bicep")]
    public void ParseGitHubUrl_InvalidUrls_ThrowsException(string url)
    {
        // Act
        var action = () => ParseGitHubUrl(url);

        // Assert
        action.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Template Format Detection Tests

    [Theory]
    [InlineData("webapp.bicep", TemplateFormat.Bicep)]
    [InlineData("WEBAPP.BICEP", TemplateFormat.Bicep)]
    [InlineData("modules/storage.bicep", TemplateFormat.Bicep)]
    public void DetectTemplateFormat_BicepFiles_ReturnsBicep(string filename, TemplateFormat expectedFormat)
    {
        // Act
        var format = DetectFormat(filename);

        // Assert
        format.Should().Be(expectedFormat);
    }

    [Theory]
    [InlineData("main.tf", TemplateFormat.Terraform)]
    [InlineData("variables.tf", TemplateFormat.Terraform)]
    [InlineData("modules/storage/main.TF", TemplateFormat.Terraform)]
    public void DetectTemplateFormat_TerraformFiles_ReturnsTerraform(string filename, TemplateFormat expectedFormat)
    {
        // Act
        var format = DetectFormat(filename);

        // Assert
        format.Should().Be(expectedFormat);
    }

    [Theory]
    [InlineData("azuredeploy.json", TemplateFormat.ARM)]
    [InlineData("mainTemplate.json", TemplateFormat.ARM)]
    [InlineData("arm-template.json", TemplateFormat.ARM)]
    public void DetectTemplateFormat_ArmFiles_ReturnsArm(string filename, TemplateFormat expectedFormat)
    {
        // Act
        var format = DetectFormat(filename);

        // Assert
        format.Should().Be(expectedFormat);
    }

    [Theory]
    [InlineData("unknown.yaml")]
    [InlineData("config.json")]
    [InlineData("random.txt")]
    public void DetectTemplateFormat_UnknownFiles_ReturnsBicepAsDefault(string filename)
    {
        // Act
        var format = DetectFormat(filename);

        // Assert - Bicep is the default fallback
        format.Should().Be(TemplateFormat.Bicep);
    }

    #endregion

    #region Bicep Parameter Extraction Tests

    [Fact]
    public void ExtractBicepParameters_ValidBicep_ExtractsParameters()
    {
        // Arrange
        var bicepContent = @"
@description('The name of the web app')
param webAppName string

@description('The Azure region for resources')
@allowed(['eastus', 'westus', 'centralus'])
param location string = 'eastus'

@description('The App Service plan SKU')
param sku string = 'B1'

resource webApp 'Microsoft.Web/sites@2022-03-01' = {
  name: webAppName
  location: location
  properties: {
    serverFarmId: appServicePlan.id
  }
}
";

        // Act
        var parameters = ExtractBicepParameters(bicepContent);

        // Assert
        parameters.Should().HaveCount(3);
        parameters.Should().Contain(p => p.name == "webAppName" && p.description == "The name of the web app");
        parameters.Should().Contain(p => p.name == "location" && p.defaultValue == "eastus");
        parameters.Should().Contain(p => p.name == "sku" && p.defaultValue == "B1");
    }

    [Fact]
    public void ExtractBicepParameters_NoParameters_ReturnsEmpty()
    {
        // Arrange
        var bicepContent = @"
resource storageAccount 'Microsoft.Storage/storageAccounts@2022-09-01' = {
  name: 'mystorageaccount'
  location: 'eastus'
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}
";

        // Act
        var parameters = ExtractBicepParameters(bicepContent);

        // Assert
        parameters.Should().BeEmpty();
    }

    [Fact]
    public void ExtractBicepParameters_ComplexTypes_ExtractsCorrectly()
    {
        // Arrange
        var bicepContent = @"
@description('Storage account name')
@minLength(3)
@maxLength(24)
param storageAccountName string

@description('Number of instances')
param instanceCount int = 1

@description('Enable diagnostics')
param enableDiagnostics bool = false

param tags object = {}

param allowedIps array = []
";

        // Act
        var parameters = ExtractBicepParameters(bicepContent);

        // Assert
        parameters.Should().HaveCount(5);
        parameters.Should().Contain(p => p.name == "storageAccountName");
        parameters.Should().Contain(p => p.name == "instanceCount");
        parameters.Should().Contain(p => p.name == "enableDiagnostics");
        parameters.Should().Contain(p => p.name == "tags");
        parameters.Should().Contain(p => p.name == "allowedIps");
    }

    #endregion

    #region ARM Template Detection Tests

    [Fact]
    public void IsArmTemplate_ValidArmJson_ReturnsTrue()
    {
        // Arrange
        var armContent = @"{
  ""$schema"": ""https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#"",
  ""contentVersion"": ""1.0.0.0"",
  ""parameters"": {},
  ""resources"": []
}";

        // Act
        var isArm = IsArmTemplate(armContent);

        // Assert
        isArm.Should().BeTrue();
    }

    [Fact]
    public void IsArmTemplate_RegularJson_ReturnsFalse()
    {
        // Arrange
        var regularJson = @"{
  ""name"": ""test"",
  ""value"": 123
}";

        // Act
        var isArm = IsArmTemplate(regularJson);

        // Assert
        isArm.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private (string owner, string repo, string path, string branch) ParseGitHubUrl(string url)
    {
        // Standard blob URL: https://github.com/{owner}/{repo}/blob/{branch}/{path}
        var blobMatch = Regex.Match(url,
            @"github\.com/([^/]+)/([^/]+)/blob/([^/]+(?:/[^/]+)*?)/(.+)");

        if (blobMatch.Success)
        {
            var fullBranchPath = blobMatch.Groups[3].Value;
            var filePath = blobMatch.Groups[4].Value;

            // Handle simple branch names (no slashes)
            if (!fullBranchPath.Contains('/'))
            {
                return (
                    blobMatch.Groups[1].Value,
                    blobMatch.Groups[2].Value,
                    filePath,
                    fullBranchPath
                );
            }

            // Handle branch names with slashes (e.g., feature/new-feature)
            // We need to find where the branch ends and the path begins
            // by looking for common file extensions
            var combined = fullBranchPath + "/" + filePath;
            var simpleMatch = Regex.Match(url,
                @"github\.com/([^/]+)/([^/]+)/blob/([^/]+)/(.+)");
            if (simpleMatch.Success)
            {
                return (
                    simpleMatch.Groups[1].Value,
                    simpleMatch.Groups[2].Value,
                    simpleMatch.Groups[4].Value,
                    simpleMatch.Groups[3].Value
                );
            }
        }

        // Raw URL: https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}
        var rawMatch = Regex.Match(url,
            @"raw\.githubusercontent\.com/([^/]+)/([^/]+)/([^/]+)/(.+)");
        if (rawMatch.Success)
        {
            return (
                rawMatch.Groups[1].Value,
                rawMatch.Groups[2].Value,
                rawMatch.Groups[4].Value,
                rawMatch.Groups[3].Value
            );
        }

        throw new ArgumentException($"Invalid GitHub URL: {url}");
    }

    private (string owner, string repo, string path, string branch) ParseGitHubRawUrl(string url)
    {
        var rawMatch = Regex.Match(url,
            @"raw\.githubusercontent\.com/([^/]+)/([^/]+)/([^/]+)/(.+)");
        if (rawMatch.Success)
        {
            return (
                rawMatch.Groups[1].Value,
                rawMatch.Groups[2].Value,
                rawMatch.Groups[4].Value,
                rawMatch.Groups[3].Value
            );
        }

        throw new ArgumentException($"Invalid GitHub raw URL: {url}");
    }

    private TemplateFormat DetectFormat(string filename)
    {
        var lower = filename.ToLowerInvariant();

        if (lower.EndsWith(".bicep"))
            return TemplateFormat.Bicep;

        if (lower.EndsWith(".tf"))
            return TemplateFormat.Terraform;

        if (lower.EndsWith(".json"))
        {
            // Check for ARM template naming conventions
            if (lower.Contains("azuredeploy") ||
                lower.Contains("arm") ||
                lower.Contains("template"))
            {
                return TemplateFormat.ARM;
            }
        }

        // Default to Bicep
        return TemplateFormat.Bicep;
    }

    private List<(string name, string? description, string? defaultValue)> ExtractBicepParameters(string content)
    {
        var parameters = new List<(string name, string? description, string? defaultValue)>();
        var lines = content.Split('\n');

        string? currentDescription = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Look for @description decorator
            if (trimmedLine.StartsWith("@description("))
            {
                var match = Regex.Match(trimmedLine, @"@description\(['""](.+?)['""]\)");
                if (match.Success)
                {
                    currentDescription = match.Groups[1].Value;
                }
            }
            // Look for param declaration
            else if (trimmedLine.StartsWith("param "))
            {
                var paramMatch = Regex.Match(trimmedLine,
                    @"param\s+(\w+)\s+\w+(?:\s*=\s*['""]?([^'""=\s]+)['""]?)?");
                if (paramMatch.Success)
                {
                    var defaultValue = paramMatch.Groups[2].Success
                        ? paramMatch.Groups[2].Value
                        : null;

                    parameters.Add((
                        paramMatch.Groups[1].Value,
                        currentDescription,
                        defaultValue
                    ));
                    currentDescription = null;
                }
            }
            // Skip other decorators but don't reset description
            else if (!trimmedLine.StartsWith("@"))
            {
                // Reset description if we hit a non-decorator, non-param line
                if (!string.IsNullOrWhiteSpace(trimmedLine) && !trimmedLine.StartsWith("//"))
                {
                    currentDescription = null;
                }
            }
        }

        return parameters;
    }

    private bool IsArmTemplate(string content)
    {
        return content.Contains("schema.management.azure.com") &&
               content.Contains("deploymentTemplate.json");
    }

    #endregion
}
