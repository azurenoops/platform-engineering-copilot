using Xunit;
using Platform.Engineering.Copilot.Core.Services.Generators.Repository;
using Platform.Engineering.Copilot.Core.Models;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.Generators;

public class DockerfileGeneratorTests
{
    [Fact]
    public void GenerateDockerFiles_ShouldReturnAllRequiredFiles()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateDockerFiles(request);

        // Assert
        Assert.NotEmpty(files);
        Assert.Contains("Dockerfile", files.Keys);
        Assert.Contains(".dockerignore", files.Keys);
        Assert.Contains("docker-compose.yml", files.Keys);
        Assert.Contains("docker-compose.dev.yml", files.Keys);
    }

    [Theory]
    [InlineData(ProgrammingLanguage.NodeJS, "node:20-alpine")]
    [InlineData(ProgrammingLanguage.Python, "python:3.11-slim")]
    [InlineData(ProgrammingLanguage.DotNet, "dotnet/sdk:8.0")]
    [InlineData(ProgrammingLanguage.Java, "maven:3.9")]
    [InlineData(ProgrammingLanguage.Go, "golang:1.21")]
    [InlineData(ProgrammingLanguage.Rust, "rust:1.75")]
    public void GenerateDockerfile_ShouldUseCorrectBaseImage(ProgrammingLanguage language, string expectedImage)
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(language);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var dockerfile = files["Dockerfile"];

        // Assert
        Assert.Contains(expectedImage, dockerfile);
    }

    [Theory]
    [InlineData(ProgrammingLanguage.NodeJS)]
    [InlineData(ProgrammingLanguage.Python)]
    [InlineData(ProgrammingLanguage.DotNet)]
    [InlineData(ProgrammingLanguage.Java)]
    [InlineData(ProgrammingLanguage.Go)]
    [InlineData(ProgrammingLanguage.Rust)]
    public void GenerateDockerfile_ShouldUseMultiStage(ProgrammingLanguage language)
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(language);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var dockerfile = files["Dockerfile"];

        // Assert
        Assert.Contains("AS builder", dockerfile);
        Assert.Contains("FROM", dockerfile);
        // Multi-stage should have at least 2 FROM statements
        var fromCount = dockerfile.Split("FROM").Length - 1;
        Assert.True(fromCount >= 2, $"Expected multi-stage build with at least 2 FROM statements, found {fromCount}");
    }

    [Theory]
    [InlineData(ProgrammingLanguage.NodeJS, "USER nodejs")]
    [InlineData(ProgrammingLanguage.Python, "USER appuser")]
    [InlineData(ProgrammingLanguage.DotNet, "USER appuser")]
    [InlineData(ProgrammingLanguage.Java, "USER appuser")]
    [InlineData(ProgrammingLanguage.Go, "USER appuser")]
    [InlineData(ProgrammingLanguage.Rust, "USER appuser")]
    public void GenerateDockerfile_ShouldUseNonRootUser(ProgrammingLanguage language, string expectedUser)
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(language);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var dockerfile = files["Dockerfile"];

        // Assert
        Assert.Contains(expectedUser, dockerfile);
    }

    [Theory]
    [InlineData(ProgrammingLanguage.NodeJS)]
    [InlineData(ProgrammingLanguage.Python)]
    [InlineData(ProgrammingLanguage.DotNet)]
    [InlineData(ProgrammingLanguage.Java)]
    public void GenerateDockerfile_ShouldIncludeHealthCheck(ProgrammingLanguage language)
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(language);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var dockerfile = files["Dockerfile"];

        // Assert
        Assert.Contains("HEALTHCHECK", dockerfile);
    }

    [Fact]
    public void GenerateDockerfile_NodeJS_ShouldInstallProductionDependencies()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var dockerfile = files["Dockerfile"];

        // Assert
        Assert.Contains("npm ci", dockerfile);
        Assert.Contains("npm run build", dockerfile);
        Assert.Contains("node", dockerfile);
    }

    [Theory]
    [InlineData("FastAPI", "uvicorn")]
    [InlineData("Flask", "gunicorn")]
    [InlineData("Django", "gunicorn")]
    public void GenerateDockerfile_Python_ShouldDetectFramework(string framework, string expectedServer)
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.Python);
        request.Application.Framework = framework;

        // Act
        var files = generator.GenerateDockerFiles(request);
        var dockerfile = files["Dockerfile"];

        // Assert
        Assert.Contains(expectedServer, dockerfile);
    }

    [Fact]
    public void GenerateDockerfile_DotNet_ShouldPublishApp()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.DotNet);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var dockerfile = files["Dockerfile"];

        // Assert
        Assert.Contains("dotnet restore", dockerfile);
        Assert.Contains("dotnet build", dockerfile);
        Assert.Contains("dotnet publish", dockerfile);
        Assert.Contains("dotnet/aspnet:8.0", dockerfile);
    }

    [Fact]
    public void GenerateDockerfile_Java_ShouldUseMaven()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.Java);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var dockerfile = files["Dockerfile"];

        // Assert
        Assert.Contains("mvn", dockerfile);
        Assert.Contains("maven", dockerfile, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("temurin", dockerfile, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateDockerfile_Go_ShouldBuildStaticBinary()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.Go);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var dockerfile = files["Dockerfile"];

        // Assert
        Assert.Contains("CGO_ENABLED=0", dockerfile);
        Assert.Contains("go build", dockerfile);
        Assert.Contains("alpine", dockerfile, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(ProgrammingLanguage.NodeJS, "node_modules")]
    [InlineData(ProgrammingLanguage.Python, "__pycache__")]
    [InlineData(ProgrammingLanguage.DotNet, "bin")]
    [InlineData(ProgrammingLanguage.Java, "target")]
    [InlineData(ProgrammingLanguage.Go, "vendor")]
    public void GenerateDockerIgnore_ShouldExcludeLanguageSpecificFiles(ProgrammingLanguage language, string expectedPattern)
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(language);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var dockerignore = files[".dockerignore"];

        // Assert
        Assert.Contains(expectedPattern, dockerignore);
        Assert.Contains(".git", dockerignore);
        Assert.Contains(".env", dockerignore);
    }

    [Fact]
    public void GenerateDockerCompose_ShouldIncludeServiceDefinition()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var compose = files["docker-compose.yml"];

        // Assert
        Assert.Contains("version:", compose);
        Assert.Contains("services:", compose);
        Assert.Contains(request.ServiceName, compose);
        Assert.Contains("ports:", compose);
        Assert.Contains($"{request.Application.Port}:", compose);
    }

    [Theory]
    [InlineData(DatabaseType.PostgreSQL, "postgres:15")]
    [InlineData(DatabaseType.MySQL, "mysql:8")]
    [InlineData(DatabaseType.MongoDB, "mongo:7")]
    [InlineData(DatabaseType.Redis, "redis:7")]
    public void GenerateDockerCompose_WithDatabase_ShouldIncludeDatabaseService(DatabaseType dbType, string expectedImage)
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);
        request.Databases.Add(new DatabaseSpec
        {
            Name = "testdb",
            Type = dbType,
            Version = "15",
            Location = DatabaseLocation.Kubernetes // For local development
        });

        // Act
        var files = generator.GenerateDockerFiles(request);
        var compose = files["docker-compose.yml"];

        // Assert
        Assert.Contains(expectedImage, compose);
        Assert.Contains("volumes:", compose);
    }

    [Fact]
    public void GenerateDockerCompose_ShouldIncludeNetwork()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var compose = files["docker-compose.yml"];

        // Assert
        Assert.Contains("networks:", compose);
    }

    [Fact]
    public void GenerateDockerComposeDev_ShouldOverrideForDevelopment()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var composeDev = files["docker-compose.dev.yml"];

        // Assert
        Assert.Contains("version:", composeDev);
        Assert.Contains("services:", composeDev);
        Assert.Contains("volumes:", composeDev);
        // Should mount source code for live reload
        Assert.Contains("./", composeDev);
    }

    [Fact]
    public void GenerateDockerComposeDev_NodeJS_ShouldExcludeNodeModules()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var composeDev = files["docker-compose.dev.yml"];

        // Assert
        Assert.Contains("node_modules", composeDev);
    }

    [Fact]
    public void GenerateDockerComposeDev_ShouldSetDevelopmentEnvironment()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);

        // Act
        var files = generator.GenerateDockerFiles(request);
        var composeDev = files["docker-compose.dev.yml"];

        // Assert
        Assert.Contains("environment:", composeDev);
        Assert.Contains("development", composeDev, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenerateDockerFiles_WithMultipleDatabases_ShouldIncludeAll()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);
        request.Databases.AddRange(new[]
        {
            new DatabaseSpec { Name = "postgres", Type = DatabaseType.PostgreSQL, Version = "15", Location = DatabaseLocation.Kubernetes },
            new DatabaseSpec { Name = "redis", Type = DatabaseType.Redis, Version = "7", Location = DatabaseLocation.Kubernetes }
        });

        // Act
        var files = generator.GenerateDockerFiles(request);
        var compose = files["docker-compose.yml"];

        // Assert
        Assert.Contains("postgres:15", compose);
        Assert.Contains("redis:7", compose);
        Assert.Contains("postgres-data:", compose);
        Assert.Contains("redis-data:", compose);
    }

    [Fact]
    public void GenerateDockerFiles_WithEnvironmentVariables_ShouldIncludeInCompose()
    {
        // Arrange
        var generator = new DockerfileGenerator();
        var request = CreateSampleRequest(ProgrammingLanguage.NodeJS);
        request.Application.EnvironmentVariables = new Dictionary<string, string>
        {
            { "API_KEY", "secret" },
            { "LOG_LEVEL", "debug" }
        };

        // Act
        var files = generator.GenerateDockerFiles(request);
        var compose = files["docker-compose.yml"];

        // Assert
        Assert.Contains("API_KEY", compose);
        Assert.Contains("LOG_LEVEL", compose);
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
                Framework = "Default",
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
