using Xunit;
using Platform.Engineering.Copilot.Core.Services.Generators.Repository;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Generators;

public class GitHubRepoGeneratorTests
{
    [Fact]
    public void GenerateRepositoryFiles_ShouldReturnAllRequiredFiles()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateRepositoryFiles(request);

        // Assert
        Assert.NotEmpty(files);
        Assert.Contains(".gitignore", files.Keys);
        Assert.Contains("README.md", files.Keys);
        Assert.Contains("LICENSE", files.Keys);
        Assert.Contains("CONTRIBUTING.md", files.Keys);
        Assert.Contains(".github/CODEOWNERS", files.Keys);
        Assert.Contains(".github/PULL_REQUEST_TEMPLATE.md", files.Keys);
        Assert.Contains(".github/dependabot.yml", files.Keys);
    }

    [Theory]
    [InlineData(ProgrammingLanguage.NodeJS, "node_modules/")]
    [InlineData(ProgrammingLanguage.Python, "__pycache__/")]
    [InlineData(ProgrammingLanguage.DotNet, "bin/")]
    [InlineData(ProgrammingLanguage.Java, "target/")]
    [InlineData(ProgrammingLanguage.Go, "vendor/")]
    public void GenerateGitIgnore_ShouldContainLanguageSpecificPatterns(ProgrammingLanguage language, string expectedPattern)
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(language);

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var gitignore = files[".gitignore"];

        // Assert
        Assert.Contains(expectedPattern, gitignore);
        Assert.Contains(".env", gitignore);
        Assert.Contains(".DS_Store", gitignore);
    }

    [Fact]
    public void GenerateReadme_NodeJS_ShouldContainNpmCommands()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var readme = files["README.md"];

        // Assert
        Assert.Contains("npm install", readme);
        Assert.Contains("npm start", readme);
        Assert.Contains("npm test", readme);
        Assert.Contains("Node.js", readme);
    }

    [Fact]
    public void GenerateReadme_Python_ShouldContainPipCommands()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.Python);

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var readme = files["README.md"];

        // Assert
        Assert.Contains("pip install", readme);
        Assert.Contains("python", readme);
        Assert.Contains("pytest", readme);
    }

    [Fact]
    public void GenerateReadme_DotNet_ShouldContainDotNetCommands()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.DotNet);

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var readme = files["README.md"];

        // Assert
        Assert.Contains("dotnet restore", readme);
        Assert.Contains("dotnet run", readme);
        Assert.Contains("dotnet test", readme);
    }

    [Theory]
    [InlineData(CloudProvider.AWS, "aws")]
    [InlineData(CloudProvider.Azure, "az")]
    [InlineData(CloudProvider.GCP, "gcloud")]
    public void GenerateReadme_ShouldContainProviderCLI(CloudProvider provider, string expectedCLI)
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);
        request.Infrastructure.Provider = provider;

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var readme = files["README.md"];

        // Assert
        Assert.Contains(expectedCLI, readme);
    }

    [Fact]
    public void GenerateLicense_ShouldContainMITText()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var license = files["LICENSE"];

        // Assert
        Assert.Contains("MIT License", license);
        Assert.Contains("Permission is hereby granted", license);
        Assert.Contains(DateTime.Now.Year.ToString(), license);
    }

    [Fact]
    public void GenerateContributing_ShouldContainGuidelines()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var contributing = files["CONTRIBUTING.md"];

        // Assert
        Assert.Contains("Contributing", contributing);
        Assert.Contains("Bug Reports", contributing);
        Assert.Contains("Pull Requests", contributing);
    }

    [Fact]
    public void GenerateCodeOwners_ShouldContainOwnershipRules()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);
        // Team property doesn't exist in TemplateGenerationRequest, but CODEOWNERS will still be generated

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var codeowners = files[".github/CODEOWNERS"];

        // Assert
        Assert.Contains("@", codeowners);
    }

    [Fact]
    public void GenerateDependabot_NodeJS_ShouldIncludeNpmAndGitHubActions()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var dependabot = files[".github/dependabot.yml"];

        // Assert
        Assert.Contains("github-actions", dependabot);
        Assert.Contains("npm", dependabot);
        Assert.Contains("/.github/workflows", dependabot);
    }

    [Fact]
    public void GenerateDependabot_Python_ShouldIncludePipAndGitHubActions()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.Python);

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var dependabot = files[".github/dependabot.yml"];

        // Assert
        Assert.Contains("github-actions", dependabot);
        Assert.Contains("pip", dependabot);
    }

    [Fact]
    public void GeneratePullRequestTemplate_ShouldContainChecklist()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var prTemplate = files[".github/PULL_REQUEST_TEMPLATE.md"];

        // Assert
        Assert.Contains("Checklist", prTemplate);
        Assert.Contains("[ ]", prTemplate);
        Assert.Contains("tests", prTemplate, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateIssueTemplates_ShouldIncludeBugAndFeature()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateRepositoryFiles(request);

        // Assert
        Assert.Contains(".github/ISSUE_TEMPLATE/bug_report.md", files.Keys);
        Assert.Contains(".github/ISSUE_TEMPLATE/feature_request.md", files.Keys);
        
        var bugTemplate = files[".github/ISSUE_TEMPLATE/bug_report.md"];
        Assert.Contains("Bug", bugTemplate);
        Assert.Contains("Expected behavior", bugTemplate);
        
        var featureTemplate = files[".github/ISSUE_TEMPLATE/feature_request.md"];
        Assert.Contains("Feature", featureTemplate);
        Assert.Contains("Describe the solution", featureTemplate);
    }

    [Fact]
    public void GenerateRepositoryFiles_WithDatabases_ShouldMentionDatabasesInReadme()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);
        request.Databases.Add(new DatabaseSpec
        {
            Name = "postgres",
            Type = DatabaseType.PostgreSQL,
            Version = "15"
        });

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var readme = files["README.md"];

        // Assert
        Assert.Contains("postgres", readme, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("database", readme, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateRepositoryFiles_WithEnvironmentVariables_ShouldListInReadme()
    {
        // Arrange
        var generator = new GitHubRepoGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);
        request.Application.EnvironmentVariables = new Dictionary<string, string>
        {
            { "NODE_ENV", "production" },
            { "PORT", "3000" }
        };

        // Act
        var files = generator.GenerateRepositoryFiles(request);
        var readme = files["README.md"];

        // Assert
        Assert.Contains("NODE_ENV", readme);
        Assert.Contains("Environment Variables", readme);
    }

    private TemplateGenerationRequest CreateSampleRequest(ProgrammingLanguage language)
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Description = "Test service for unit testing",
            Application = new ApplicationSpec
            {
                Language = language,
                Framework = language == ProgrammingLanguage.NodeJS ? "Express" : "Default",
                Type = ApplicationType.WebAPI,
                Port = 8080,
                EnvironmentVariables = new Dictionary<string, string>(),
                IncludeHealthCheck = true
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Terraform,
                Provider = CloudProvider.AWS,
                Region = "us-east-1",
                ComputePlatform = ComputePlatform.ECS
            },
            Deployment = new DeploymentSpec
            {
                Replicas = 3,
                AutoScaling = true,
                MinReplicas = 2,
                MaxReplicas = 10
            },
            Databases = new List<DatabaseSpec>()
        };
    }
}
