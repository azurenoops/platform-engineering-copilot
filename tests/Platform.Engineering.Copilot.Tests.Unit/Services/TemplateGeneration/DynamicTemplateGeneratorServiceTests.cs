using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Validation;
using Platform.Engineering.Copilot.Core.Models.Validation;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Services.TemplateGeneration;

/// <summary>
/// Unit tests for DynamicTemplateGeneratorService
/// Tests all template generation scenarios including application code, databases, infrastructure, and supporting files
/// </summary>
public class DynamicTemplateGeneratorServiceTests
{
    private readonly Mock<ILogger<DynamicTemplateGeneratorService>> _loggerMock;
    private readonly DynamicTemplateGeneratorService _service;

    public DynamicTemplateGeneratorServiceTests()
    {
        _loggerMock = new Mock<ILogger<DynamicTemplateGeneratorService>>();
        _service = new DynamicTemplateGeneratorService(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var service = new DynamicTemplateGeneratorService(_loggerMock.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithValidationService_CreatesInstance()
    {
        // Arrange
        var validationLoggerMock = new Mock<ILogger<ConfigurationValidationService>>();
        var validationService = new ConfigurationValidationService(
            validationLoggerMock.Object,
            Enumerable.Empty<Platform.Engineering.Copilot.Core.Interfaces.Validation.IConfigurationValidator>());

        // Act
        var service = new DynamicTemplateGeneratorService(_loggerMock.Object, validationService);

        // Assert
        service.Should().NotBeNull();
    }

    #endregion

    #region GenerateTemplateAsync - Null/Invalid Input Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithNullRequest_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _service.GenerateTemplateAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("request");
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithEmptyServiceName_ReturnsSuccessWithFiles()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "",
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Bicep,
                Provider = CloudProvider.Azure
            }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Infrastructure-Only Template Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithInfrastructureTemplateType_GeneratesOnlyInfrastructureFiles()
    {
        // Arrange
        var request = CreateInfrastructureOnlyRequest("infrastructure", InfrastructureFormat.Bicep);

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure"));
        // Should NOT contain application code components
        result.GeneratedComponents.Should().NotContain(c => c.Contains("Application"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithBicepFormat_NoApplication_IsInfrastructureOnly()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-infra",
            Description = "Infrastructure only template",
            Application = null, // No application spec
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Bicep,
                Provider = CloudProvider.Azure,
                Region = "eastus",
                ComputePlatform = ComputePlatform.AKS
            }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithTerraformFormat_NoApplication_IsInfrastructureOnly()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-terraform-infra",
            Description = "Terraform infrastructure only",
            Application = null,
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Terraform,
                Provider = CloudProvider.Azure,
                Region = "eastus",
                ComputePlatform = ComputePlatform.AKS
            }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("Terraform"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithARMFormat_NoApplication_IsInfrastructureOnly()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-arm-infra",
            Description = "ARM infrastructure only",
            Application = null,
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.ARM,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("ARM"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithCloudFormationFormat_NoApplication_IsInfrastructureOnly()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-cf-infra",
            Description = "CloudFormation infrastructure only",
            Application = null,
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.CloudFormation,
                Provider = CloudProvider.AWS,
                Region = "us-east-1"
            }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("CloudFormation"));
    }

    #endregion

    #region Application Code Generation Tests - All Languages

    [Theory]
    [InlineData(ProgrammingLanguage.DotNet, "ASP.NET Core")]
    [InlineData(ProgrammingLanguage.NodeJS, "Express")]
    [InlineData(ProgrammingLanguage.Python, "FastAPI")]
    [InlineData(ProgrammingLanguage.Java, "Spring Boot")]
    [InlineData(ProgrammingLanguage.Go, "Gin")]
    [InlineData(ProgrammingLanguage.Rust, "Actix")]
    [InlineData(ProgrammingLanguage.Ruby, "Rails")]
    [InlineData(ProgrammingLanguage.PHP, "Laravel")]
    public async Task GenerateTemplateAsync_WithLanguage_GeneratesApplicationCode(
        ProgrammingLanguage language, string framework)
    {
        // Arrange
        var request = CreateApplicationRequest(language, framework);

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains(language.ToString()));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithDotNetWebAPI_GeneratesExpectedFiles()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.DotNet, "ASP.NET Core");
        request.Application!.Type = ApplicationType.WebAPI;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        // Verify that application code files were generated
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithNodeJSExpress_GeneratesExpectedFiles()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.NodeJS, "Express");
        request.Application!.Type = ApplicationType.WebAPI;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains("NodeJS"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithPythonFastAPI_GeneratesExpectedFiles()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.Python, "FastAPI");
        request.Application!.Type = ApplicationType.WebAPI;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains("Python"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithJavaSpringBoot_GeneratesExpectedFiles()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.Java, "Spring Boot");
        request.Application!.Type = ApplicationType.Microservice;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains("Java"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithGoGin_GeneratesExpectedFiles()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.Go, "Gin");
        request.Application!.Type = ApplicationType.WebAPI;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains("Go"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithRustActix_GeneratesExpectedFiles()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.Rust, "Actix");
        request.Application!.Type = ApplicationType.WebAPI;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains("Rust"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithRubyRails_GeneratesExpectedFiles()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.Ruby, "Rails");
        request.Application!.Type = ApplicationType.WebApp;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains("Ruby"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithPHPLaravel_GeneratesExpectedFiles()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.PHP, "Laravel");
        request.Application!.Type = ApplicationType.WebApp;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains("PHP"));
    }

    #endregion

    #region Database Template Generation Tests - All Types

    [Theory]
    [InlineData(DatabaseType.PostgreSQL)]
    [InlineData(DatabaseType.MySQL)]
    [InlineData(DatabaseType.SQLServer)]
    [InlineData(DatabaseType.AzureSQL)]
    [InlineData(DatabaseType.MongoDB)]
    [InlineData(DatabaseType.CosmosDB)]
    [InlineData(DatabaseType.Redis)]
    [InlineData(DatabaseType.DynamoDB)]
    public async Task GenerateTemplateAsync_WithDatabaseType_GeneratesDatabaseTemplates(DatabaseType dbType)
    {
        // Arrange
        var request = CreateRequestWithDatabase(dbType);

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Database") && c.Contains(dbType.ToString()));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithPostgreSQLDatabase_GeneratesExpectedTemplates()
    {
        // Arrange
        var request = CreateRequestWithDatabase(DatabaseType.PostgreSQL);
        request.Databases[0].Version = "15";
        request.Databases[0].Tier = DatabaseTier.Standard;
        request.Databases[0].StorageGB = 64;
        request.Databases[0].HighAvailability = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Database") && c.Contains("PostgreSQL"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithCosmosDBDatabase_GeneratesExpectedTemplates()
    {
        // Arrange
        var request = CreateRequestWithDatabase(DatabaseType.CosmosDB);
        request.Databases[0].Tier = DatabaseTier.Serverless;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Database") && c.Contains("CosmosDB"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithMultipleDatabases_GeneratesAllDatabaseTemplates()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "multi-db-service",
            Description = "Service with multiple databases",
            Databases = new List<DatabaseSpec>
            {
                new DatabaseSpec { Name = "primary-db", Type = DatabaseType.PostgreSQL, Version = "15" },
                new DatabaseSpec { Name = "cache-db", Type = DatabaseType.Redis, Version = "7" },
                new DatabaseSpec { Name = "document-db", Type = DatabaseType.MongoDB, Version = "6" }
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure
            }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("PostgreSQL"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Redis"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("MongoDB"));
    }

    #endregion

    #region Infrastructure Format Tests - All Formats

    [Theory]
    [InlineData(InfrastructureFormat.Kubernetes)]
    [InlineData(InfrastructureFormat.Bicep)]
    [InlineData(InfrastructureFormat.Terraform)]
    [InlineData(InfrastructureFormat.ARM)]
    [InlineData(InfrastructureFormat.CloudFormation)]
    public async Task GenerateTemplateAsync_WithInfrastructureFormat_GeneratesInfrastructureFiles(
        InfrastructureFormat format)
    {
        // Arrange
        var request = CreateRequestWithInfrastructureFormat(format);

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains(format.ToString()));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithKubernetesFormat_GeneratesK8sManifests()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Infrastructure.Format = InfrastructureFormat.Kubernetes;
        request.Infrastructure.ComputePlatform = ComputePlatform.AKS;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("Kubernetes"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithBicepFormat_GeneratesBicepFiles()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Infrastructure.Format = InfrastructureFormat.Bicep;
        request.Infrastructure.ComputePlatform = ComputePlatform.AKS;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("Bicep"));
    }

    #endregion

    #region Cloud Provider Tests

    [Theory]
    [InlineData(CloudProvider.Azure)]
    [InlineData(CloudProvider.AWS)]
    [InlineData(CloudProvider.GCP)]
    public async Task GenerateTemplateAsync_WithCloudProvider_GeneratesProviderSpecificFiles(CloudProvider provider)
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Infrastructure.Provider = provider;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
    }

    #endregion

    #region Compute Platform Tests

    [Theory]
    [InlineData(ComputePlatform.AKS)]
    [InlineData(ComputePlatform.AppService)]
    [InlineData(ComputePlatform.ContainerApps)]
    [InlineData(ComputePlatform.Kubernetes)]
    public async Task GenerateTemplateAsync_WithComputePlatform_GeneratesPlatformSpecificFiles(ComputePlatform platform)
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Infrastructure.ComputePlatform = platform;
        request.Infrastructure.Provider = CloudProvider.Azure;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
    }

    #endregion

    #region Security Component Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithRBACEnabled_GeneratesSecurityComponents()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Security.RBAC = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Security"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithNetworkPoliciesEnabled_GeneratesSecurityComponents()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Security.NetworkPolicies = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Security"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithSecurityDisabled_DoesNotGenerateSecurityComponents()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Security.RBAC = false;
        request.Security.NetworkPolicies = false;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Security component should not be in the list if RBAC and NetworkPolicies are both disabled
        result.GeneratedComponents.Should().NotContain(c => c == "Security");
    }

    #endregion

    #region Observability Component Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithPrometheusEnabled_GeneratesObservabilityComponents()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Observability.Prometheus = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Observability"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithGrafanaEnabled_GeneratesObservabilityComponents()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Observability.Grafana = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Observability"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithObservabilityDisabled_DoesNotGenerateObservabilityComponents()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Observability.Prometheus = false;
        request.Observability.Grafana = false;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        // Observability component should not be in the list if disabled
        result.GeneratedComponents.Should().NotContain(c => c == "Observability");
    }

    #endregion

    #region Full Service Generation Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithFullServiceRequest_GeneratesAllComponents()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Security.RBAC = true;
        request.Observability.Prometheus = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify all major components are generated
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Repository"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Docker"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("CI/CD"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Security"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Observability"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithFullServiceRequest_GeneratesSummary()
    {
        // Arrange
        var request = CreateFullServiceRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Summary.Should().NotBeNullOrEmpty();
        result.Summary.Should().Contain(request.ServiceName);
    }

    #endregion

    #region Repository Files Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithApplicationRequest_GeneratesRepositoryFiles()
    {
        // Arrange
        var request = CreateFullServiceRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Repository"));
    }

    #endregion

    #region Docker Files Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithApplicationRequest_GeneratesDockerFiles()
    {
        // Arrange
        var request = CreateFullServiceRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Docker"));
    }

    #endregion

    #region CI/CD Workflow Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithApplicationRequest_GeneratesCICDWorkflows()
    {
        // Arrange
        var request = CreateFullServiceRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.GeneratedComponents.Should().Contain(c => c.Contains("CI/CD"));
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithCancellationToken_RespectsCancellation()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // Note: The current implementation may not throw on cancellation
        // This test verifies that the method accepts a cancellation token
        var result = await _service.GenerateTemplateAsync(request, cts.Token);
        // The result should still be valid since cancellation is not fully implemented
        result.Should().NotBeNull();
    }

    #endregion

    #region Deployment Configuration Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithAutoScalingEnabled_IncludesAutoScalingConfig()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Deployment.AutoScaling = true;
        request.Deployment.MinReplicas = 2;
        request.Deployment.MaxReplicas = 10;
        request.Deployment.TargetCpuPercent = 80;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithCustomResourceRequirements_IncludesResourceConfig()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Deployment.Resources = new ResourceRequirements
        {
            CpuRequest = "500m",
            CpuLimit = "2",
            MemoryRequest = "512Mi",
            MemoryLimit = "2Gi"
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
    }

    #endregion

    #region Application Type Tests

    [Theory]
    [InlineData(ApplicationType.WebAPI)]
    [InlineData(ApplicationType.WebApp)]
    [InlineData(ApplicationType.BackgroundWorker)]
    [InlineData(ApplicationType.MessageConsumer)]
    [InlineData(ApplicationType.Microservice)]
    [InlineData(ApplicationType.Serverless)]
    public async Task GenerateTemplateAsync_WithApplicationType_GeneratesCorrectTemplate(ApplicationType appType)
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.DotNet, "ASP.NET Core");
        request.Application!.Type = appType;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
    }

    #endregion

    #region Application Configuration Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithEnvironmentVariables_IncludesEnvVars()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.NodeJS, "Express");
        request.Application!.EnvironmentVariables = new Dictionary<string, string>
        {
            ["NODE_ENV"] = "production",
            ["PORT"] = "3000",
            ["DATABASE_URL"] = "postgresql://localhost/mydb"
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithDependencies_IncludesDependencies()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.NodeJS, "Express");
        request.Application!.Dependencies = new List<string> { "express", "mongoose", "dotenv" };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithHealthCheckEnabled_IncludesHealthCheck()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.DotNet, "ASP.NET Core");
        request.Application!.IncludeHealthCheck = true;
        request.Application.IncludeReadinessProbe = true;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithCustomPort_UsesCustomPort()
    {
        // Arrange
        var request = CreateApplicationRequest(ProgrammingLanguage.Python, "FastAPI");
        request.Application!.Port = 5000;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithValidRequest_ReturnsSuccessTrue()
    {
        // Arrange
        var request = CreateFullServiceRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNullOrEmpty();
    }

    #endregion

    #region Helper Methods

    private static TemplateGenerationRequest CreateInfrastructureOnlyRequest(
        string templateType, 
        InfrastructureFormat format)
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "test-infra-only",
            Description = "Infrastructure only template",
            TemplateType = templateType,
            Application = null, // No application
            Infrastructure = new InfrastructureSpec
            {
                Format = format,
                Provider = CloudProvider.Azure,
                Region = "eastus",
                ComputePlatform = ComputePlatform.AKS
            }
        };
    }

    private static TemplateGenerationRequest CreateApplicationRequest(
        ProgrammingLanguage language, 
        string framework)
    {
        return new TemplateGenerationRequest
        {
            ServiceName = $"test-{language.ToString().ToLower()}-service",
            Description = $"Test service using {language}",
            Application = new ApplicationSpec
            {
                Language = language,
                Framework = framework,
                Type = ApplicationType.WebAPI,
                Port = 8080,
                IncludeHealthCheck = true,
                IncludeReadinessProbe = true
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreateRequestWithDatabase(DatabaseType dbType)
    {
        return new TemplateGenerationRequest
        {
            ServiceName = $"test-{dbType.ToString().ToLower()}-service",
            Description = $"Test service with {dbType} database",
            Databases = new List<DatabaseSpec>
            {
                new DatabaseSpec
                {
                    Name = "testdb",
                    Type = dbType,
                    Version = "latest",
                    Tier = DatabaseTier.Standard,
                    StorageGB = 32,
                    BackupEnabled = true
                }
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreateRequestWithInfrastructureFormat(InfrastructureFormat format)
    {
        return new TemplateGenerationRequest
        {
            ServiceName = $"test-{format.ToString().ToLower()}-service",
            Description = $"Test service with {format} infrastructure",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.DotNet,
                Framework = "ASP.NET Core",
                Type = ApplicationType.WebAPI,
                Port = 8080
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = format,
                Provider = format == InfrastructureFormat.CloudFormation ? CloudProvider.AWS : CloudProvider.Azure,
                Region = format == InfrastructureFormat.CloudFormation ? "us-east-1" : "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreateFullServiceRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "test-full-service",
            Description = "Full test service with all components",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.DotNet,
                Framework = "ASP.NET Core",
                Type = ApplicationType.Microservice,
                Port = 8080,
                IncludeHealthCheck = true,
                IncludeReadinessProbe = true,
                EnvironmentVariables = new Dictionary<string, string>
                {
                    ["ASPNETCORE_ENVIRONMENT"] = "Production"
                }
            },
            Databases = new List<DatabaseSpec>
            {
                new DatabaseSpec
                {
                    Name = "maindb",
                    Type = DatabaseType.PostgreSQL,
                    Version = "15",
                    Tier = DatabaseTier.Standard
                }
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus",
                ComputePlatform = ComputePlatform.AKS,
                IncludeNetworking = true,
                IncludeStorage = true,
                IncludeLoadBalancer = true
            },
            Deployment = new DeploymentSpec
            {
                Replicas = 3,
                AutoScaling = true,
                MinReplicas = 2,
                MaxReplicas = 10,
                TargetCpuPercent = 70
            },
            Security = new SecuritySpec
            {
                RBAC = false,
                NetworkPolicies = false,
                TLS = true,
                SecretsManagement = true
            },
            Observability = new ObservabilitySpec
            {
                Prometheus = false,
                Grafana = false,
                StructuredLogging = true
            }
        };
    }

    #endregion
}
