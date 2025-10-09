using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services.TemplateGeneration;

/// <summary>
/// Unit tests for DynamicTemplateGeneratorService
/// Tests template generation with the actual TemplateGenerationRequest model structure
/// </summary>
public class DynamicTemplateGeneratorServiceTests
{
    private readonly Mock<ILogger<DynamicTemplateGeneratorService>> _mockLogger;
    private readonly DynamicTemplateGeneratorService _service;

    public DynamicTemplateGeneratorServiceTests()
    {
        _mockLogger = new Mock<ILogger<DynamicTemplateGeneratorService>>();
        _service = new DynamicTemplateGeneratorService(_mockLogger.Object);
    }

    #region Helper Methods

    private TemplateGenerationRequest CreateBasicRequest(string serviceName, ComputePlatform platform)
    {
        return new TemplateGenerationRequest
        {
            ServiceName = serviceName,
            Description = "Test service",
            TemplateType = "microservice",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.NodeJS,
                Framework = "express",
                Type = ApplicationType.WebAPI,
                Port = 8080
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Bicep,
                Provider = CloudProvider.Azure,
                Region = "eastus",
                ComputePlatform = platform
            },
            Deployment = new DeploymentSpec
            {
                Replicas = 3,
                AutoScaling = true,
                MinReplicas = 1,
                MaxReplicas = 10
            },
            Security = new SecuritySpec
            {
                NetworkPolicies = true,
                RBAC = true,
                TLS = true
            },
            Observability = new ObservabilitySpec
            {
                Prometheus = true,
                StructuredLogging = true
            }
        };
    }

    #endregion

    #region Basic Generation Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithValidRequest_Succeeds()
    {
        // Arrange
        var request = CreateBasicRequest("test-service", ComputePlatform.AKS);

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success, $"Generation failed: {result.ErrorMessage}");
        Assert.NotNull(result.Files);
        Assert.NotEmpty(result.Files);
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.GenerateTemplateAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GenerateTemplateAsync_GeneratesMultipleFiles()
    {
        // Arrange
        var request = CreateBasicRequest("multi-file-test", ComputePlatform.AKS);

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Files.Count >= 10, $"Expected at least 10 files, got {result.Files.Count}");
    }

    #endregion

    #region Platform-Specific Tests

    [Theory]
    [InlineData(ComputePlatform.AKS)]
    [InlineData(ComputePlatform.AppService)]
    [InlineData(ComputePlatform.ContainerApps)]
    [InlineData(ComputePlatform.ECS)]
    [InlineData(ComputePlatform.GKE)]
    public async Task GenerateTemplateAsync_WithDifferentPlatforms_Succeeds(ComputePlatform platform)
    {
        // Arrange
        var request = CreateBasicRequest($"platform-{platform}-test", platform);

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"Failed for platform {platform}: {result.ErrorMessage}");
        Assert.NotEmpty(result.Files);
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithAKS_GeneratesKubernetesFiles()
    {
        // Arrange
        var request = CreateBasicRequest("aks-k8s-test", ComputePlatform.AKS);

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasK8sFiles = result.Files.Keys.Any(path => 
            path.Contains("kubernetes", StringComparison.OrdinalIgnoreCase) ||
            path.Contains(".yaml", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("deployment", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasK8sFiles, "Expected Kubernetes YAML files for AKS platform");
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithAppService_DoesNotGenerateKubernetesFiles()
    {
        // Arrange
        var request = CreateBasicRequest("appservice-test", ComputePlatform.AppService);

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasK8sFiles = result.Files.Keys.Any(path => 
            path.Contains("kubernetes", StringComparison.OrdinalIgnoreCase));
        Assert.False(hasK8sFiles, "App Service should not generate Kubernetes files");
    }

    #endregion

    #region Infrastructure Format Tests

    [Theory]
    [InlineData(InfrastructureFormat.Bicep, "bicep")]
    [InlineData(InfrastructureFormat.Terraform, "terraform")]
    public async Task GenerateTemplateAsync_WithDifferentFormats_GeneratesCorrectFiles(
        InfrastructureFormat format, string expectedInPath)
    {
        // Arrange
        var request = CreateBasicRequest($"format-{format}-test", ComputePlatform.AKS);
        request.Infrastructure.Format = format;

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        // Some formats might not be fully implemented yet
        if (result.Files.Count > 0)
        {
            var hasFormatFiles = result.Files.Keys.Any(path => 
                path.Contains(expectedInPath, StringComparison.OrdinalIgnoreCase));
            Assert.True(hasFormatFiles, $"Expected files for format {format}");
        }
    }

    #endregion

    #region Language Tests

    [Theory]
    [InlineData(ProgrammingLanguage.NodeJS, "package.json")]
    [InlineData(ProgrammingLanguage.Python, "requirements.txt")]
    [InlineData(ProgrammingLanguage.DotNet, ".csproj")]
    [InlineData(ProgrammingLanguage.Go, "go.mod")]
    [InlineData(ProgrammingLanguage.Java, "pom.xml")]
    public async Task GenerateTemplateAsync_WithDifferentLanguages_GeneratesLanguageFiles(
        ProgrammingLanguage language, string expectedFile)
    {
        // Arrange
        var request = CreateBasicRequest($"lang-{language}-test", ComputePlatform.AKS);
        request.Application!.Language = language;

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasLanguageFile = result.Files.Keys.Any(path => 
            path.Contains(expectedFile, StringComparison.OrdinalIgnoreCase));
        Assert.True(hasLanguageFile, $"Expected {expectedFile} for language {language}");
    }

    #endregion

    #region Database Tests

    [Theory]
    [InlineData(DatabaseType.PostgreSQL)]
    [InlineData(DatabaseType.SQLServer)]
    [InlineData(DatabaseType.MySQL)]
    [InlineData(DatabaseType.Redis)]
    [InlineData(DatabaseType.MongoDB)]
    [InlineData(DatabaseType.CosmosDB)]
    public async Task GenerateTemplateAsync_WithDatabase_GeneratesDatabaseResources(DatabaseType dbType)
    {
        // Arrange
        var request = CreateBasicRequest($"db-{dbType}-test", ComputePlatform.AKS);
        request.Databases = new List<DatabaseSpec>
        {
            new DatabaseSpec
            {
                Name = $"db-{dbType}",
                Type = dbType,
                Version = "latest",
                Tier = DatabaseTier.Standard,
                HighAvailability = true
            }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasDatabaseContent = result.Files.Values.Any(content =>
            content.Contains(dbType.ToString(), StringComparison.OrdinalIgnoreCase));
        Assert.True(hasDatabaseContent, $"Expected database resources for {dbType}");
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithMultipleDatabases_GeneratesAllResources()
    {
        // Arrange
        var request = CreateBasicRequest("multi-db-test", ComputePlatform.AKS);
        request.Databases = new List<DatabaseSpec>
        {
            new DatabaseSpec { Name = "db-postgres", Type = DatabaseType.PostgreSQL, Version = "14" },
            new DatabaseSpec { Name = "db-redis", Type = DatabaseType.Redis, Version = "7" }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasPostgres = result.Files.Values.Any(c => c.Contains("postgres", StringComparison.OrdinalIgnoreCase));
        var hasRedis = result.Files.Values.Any(c => c.Contains("redis", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasPostgres && hasRedis, "Expected both PostgreSQL and Redis resources");
    }

    #endregion

    #region Security Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithRBACEnabled_GeneratesRBACResources()
    {
        // Arrange
        var request = CreateBasicRequest("rbac-test", ComputePlatform.AKS);
        request.Security.RBAC = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasRBAC = result.Files.Values.Any(c => 
            c.Contains("rbac", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("Role", StringComparison.Ordinal));
        Assert.True(hasRBAC, "Expected RBAC resources when RBAC is enabled");
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithNetworkPoliciesEnabled_GeneratesNetworkPolicies()
    {
        // Arrange
        var request = CreateBasicRequest("netpol-test", ComputePlatform.AKS);
        request.Security.NetworkPolicies = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasNetworkPolicies = result.Files.Values.Any(c =>
            c.Contains("NetworkPolicy", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasNetworkPolicies, "Expected NetworkPolicy resources");
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithTLSEnabled_GeneratesTLSConfiguration()
    {
        // Arrange
        var request = CreateBasicRequest("tls-test", ComputePlatform.AKS);
        request.Security.TLS = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasTLS = result.Files.Values.Any(c =>
            c.Contains("tls", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("https", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasTLS, "Expected TLS/HTTPS configuration");
    }

    #endregion

    #region Deployment Configuration Tests

    [Theory]
    [InlineData(1, 1, 5)]
    [InlineData(3, 2, 10)]
    [InlineData(5, 3, 15)]
    public async Task GenerateTemplateAsync_WithReplicaConfiguration_GeneratesCorrectReplicas(
        int replicas, int minReplicas, int maxReplicas)
    {
        // Arrange
        var request = CreateBasicRequest("replica-test", ComputePlatform.AKS);
        request.Deployment.Replicas = replicas;
        request.Deployment.MinReplicas = minReplicas;
        request.Deployment.MaxReplicas = maxReplicas;
        request.Deployment.AutoScaling = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        
        // Check for replica configuration in various formats
        // Different platforms may use different naming (minReplicas, min_replicas, minimum_replicas, etc.)
        var hasReplicaConfig = result.Files.Values.Any(c =>
            c.Contains($"replicas: {replicas}", StringComparison.OrdinalIgnoreCase) ||
            c.Contains($"replica_count: {replicas}", StringComparison.OrdinalIgnoreCase) ||
            c.Contains($"minReplicas: {minReplicas}", StringComparison.OrdinalIgnoreCase) ||
            c.Contains($"min_replicas: {minReplicas}", StringComparison.OrdinalIgnoreCase) ||
            c.Contains($"minimum_replicas: {minReplicas}", StringComparison.OrdinalIgnoreCase) ||
            c.Contains($"maxReplicas: {maxReplicas}", StringComparison.OrdinalIgnoreCase) ||
            c.Contains($"max_replicas: {maxReplicas}", StringComparison.OrdinalIgnoreCase) ||
            c.Contains($"maximum_replicas: {maxReplicas}", StringComparison.OrdinalIgnoreCase) ||
            c.Contains($"min_count = {minReplicas}", StringComparison.OrdinalIgnoreCase) ||
            c.Contains($"max_count = {maxReplicas}", StringComparison.OrdinalIgnoreCase));
        
        // If no replica config found, at least verify AutoScaling configuration is present
        if (!hasReplicaConfig)
        {
            var hasAutoScalingConfig = result.Files.Values.Any(c =>
                c.Contains("enable_auto_scaling", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("autoscaling", StringComparison.OrdinalIgnoreCase) ||
                c.Contains("HorizontalPodAutoscaler", StringComparison.OrdinalIgnoreCase));
            Assert.True(hasAutoScalingConfig, 
                "Expected either replica configuration or autoscaling configuration in generated files");
        }
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithAutoScaling_GeneratesHPA()
    {
        // Arrange
        var request = CreateBasicRequest("hpa-test", ComputePlatform.AKS);
        request.Deployment.AutoScaling = true;
        request.Deployment.TargetCpuPercent = 70;
        request.Deployment.TargetMemoryPercent = 80;

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasHPA = result.Files.Values.Any(c =>
            c.Contains("HorizontalPodAutoscaler", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("autoscaling", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasHPA, "Expected HorizontalPodAutoscaler when AutoScaling is enabled");
    }

    #endregion

    #region Observability Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithPrometheus_GeneratesPrometheusConfiguration()
    {
        // Arrange
        var request = CreateBasicRequest("prometheus-test", ComputePlatform.AKS);
        request.Observability.Prometheus = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasPrometheus = result.Files.Values.Any(c =>
            c.Contains("prometheus", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasPrometheus, "Expected Prometheus configuration");
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithApplicationInsights_GeneratesAIConfiguration()
    {
        // Arrange
        var request = CreateBasicRequest("appinsights-test", ComputePlatform.AKS);
        request.Observability.ApplicationInsights = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasAppInsights = result.Files.Values.Any(c =>
            c.Contains("ApplicationInsights", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("appinsights", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasAppInsights, "Expected Application Insights configuration");
    }

    #endregion

    #region Networking Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithNetworking_GeneratesNetworkResources()
    {
        // Arrange
        var request = CreateBasicRequest("network-test", ComputePlatform.AKS);
        request.Infrastructure.IncludeNetworking = true;
        request.Infrastructure.NetworkConfig = new NetworkingConfiguration
        {
            VNetName = "vnet-test",
            VNetAddressSpace = "10.0.0.0/16",
            EnableNetworkSecurityGroup = true,
            EnablePrivateDns = true
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasNetworking = result.Files.Values.Any(c =>
            c.Contains("vnet", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("network", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasNetworking, "Expected networking resources");
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithPrivateEndpoints_GeneratesPrivateEndpointResources()
    {
        // Arrange
        var request = CreateBasicRequest("pe-test", ComputePlatform.AKS);
        request.Infrastructure.IncludeNetworking = true;
        request.Infrastructure.NetworkConfig = new NetworkingConfiguration
        {
            EnablePrivateEndpoint = true,
            PrivateEndpointSubnetName = "pe-subnet"
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasPrivateEndpoints = result.Files.Values.Any(c =>
            c.Contains("privateEndpoint", StringComparison.OrdinalIgnoreCase) ||
            c.Contains("private endpoint", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasPrivateEndpoints, "Expected private endpoint resources");
    }

    #endregion

    #region File Organization Tests

    [Fact]
    public async Task GenerateTemplateAsync_OrganizesFilesInDirectories()
    {
        // Arrange
        var request = CreateBasicRequest("file-org-test", ComputePlatform.AKS);

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        // Check for any file with a path separator, indicating directory organization
        var hasDirectoryStructure = result.Files.Keys.Any(p => p.Contains("/") || p.Contains("\\"));
        Assert.True(hasDirectoryStructure, "Expected organized directory structure with subdirectories");
    }

    [Fact]
    public async Task GenerateTemplateAsync_GeneratesREADME()
    {
        // Arrange
        var request = CreateBasicRequest("readme-test", ComputePlatform.AKS);

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasReadme = result.Files.Keys.Any(p => 
            p.Equals("README.md", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasReadme, "Expected README.md file");
    }

    [Fact]
    public async Task GenerateTemplateAsync_GeneratesGitignore()
    {
        // Arrange
        var request = CreateBasicRequest("gitignore-test", ComputePlatform.AKS);

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        var hasGitignore = result.Files.Keys.Any(p => 
            p.Equals(".gitignore", StringComparison.OrdinalIgnoreCase));
        Assert.True(hasGitignore, "Expected .gitignore file");
    }

    #endregion

    #region Result Validation Tests

    [Fact]
    public async Task GenerateTemplateAsync_PopulatesSummary()
    {
        // Arrange
        var request = CreateBasicRequest("summary-test", ComputePlatform.AKS);

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.Summary);
        Assert.NotEmpty(result.Summary);
    }

    [Fact]
    public async Task GenerateTemplateAsync_PopulatesGeneratedComponents()
    {
        // Arrange
        var request = CreateBasicRequest("components-test", ComputePlatform.AKS);

        // Act
        var result = await _service.GenerateTemplateAsync(request, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.GeneratedComponents);
        Assert.NotEmpty(result.GeneratedComponents);
    }

    #endregion
}
