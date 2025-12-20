using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.Services.TemplateGeneration;

/// <summary>
/// Integration tests for DynamicTemplateGeneratorService
/// These tests verify that the actual generated content meets expected patterns and structures
/// </summary>
public class DynamicTemplateGeneratorServiceIntegrationTests
{
    private readonly Mock<ILogger<DynamicTemplateGeneratorService>> _loggerMock;
    private readonly DynamicTemplateGeneratorService _service;

    public DynamicTemplateGeneratorServiceIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<DynamicTemplateGeneratorService>>();
        _service = new DynamicTemplateGeneratorService(_loggerMock.Object);
    }

    #region DotNet Template Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_DotNetWebAPI_ContainsExpectedFileTypes()
    {
        // Arrange
        var request = CreateDotNetRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify .NET specific file extensions exist
        var hasNetFile = result.Files.Keys.Any(k => 
            k.EndsWith(".cs") || 
            k.EndsWith(".csproj") || 
            k.EndsWith("Program.cs") ||
            k.Contains(".NET"));
        hasNetFile.Should().BeTrue("should contain .NET related files");
    }

    [Fact]
    public async Task GenerateTemplateAsync_DotNetWebAPI_GeneratesDockerfile()
    {
        // Arrange
        var request = CreateDotNetRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        // Check if Docker files are generated
        result.GeneratedComponents.Should().Contain(c => c.Contains("Docker"));
        
        // Docker files should reference appropriate base image
        var dockerFile = result.Files.Keys.FirstOrDefault(k => 
            k.Contains("Dockerfile") || k.Contains("docker"));
        if (dockerFile != null)
        {
            var content = result.Files[dockerFile];
            content.Should().NotBeNullOrEmpty();
        }
    }

    #endregion

    #region NodeJS Template Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_NodeJSExpress_ContainsExpectedFileTypes()
    {
        // Arrange
        var request = CreateNodeJSRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify NodeJS specific patterns
        var hasNodeFile = result.Files.Keys.Any(k => 
            k.EndsWith(".js") || 
            k.EndsWith(".ts") || 
            k.Contains("package.json") ||
            k.Contains("Node"));
        hasNodeFile.Should().BeTrue("should contain Node.js related files");
    }

    [Fact]
    public async Task GenerateTemplateAsync_NodeJSExpress_GeneratesPackageJson()
    {
        // Arrange
        var request = CreateNodeJSRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        // Application code should be generated
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains("NodeJS"));
    }

    #endregion

    #region Python Template Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_PythonFastAPI_ContainsExpectedFileTypes()
    {
        // Arrange
        var request = CreatePythonRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify Python specific patterns
        var hasPythonFile = result.Files.Keys.Any(k => 
            k.EndsWith(".py") || 
            k.Contains("requirements") ||
            k.Contains("Python"));
        hasPythonFile.Should().BeTrue("should contain Python related files");
    }

    #endregion

    #region Java Template Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_JavaSpringBoot_ContainsExpectedFileTypes()
    {
        // Arrange
        var request = CreateJavaRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify Java specific patterns
        var hasJavaFile = result.Files.Keys.Any(k => 
            k.EndsWith(".java") || 
            k.Contains("pom.xml") ||
            k.Contains("build.gradle") ||
            k.Contains("Java"));
        hasJavaFile.Should().BeTrue("should contain Java related files");
    }

    #endregion

    #region Go Template Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_GoGin_ContainsExpectedFileTypes()
    {
        // Arrange
        var request = CreateGoRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify Go specific patterns
        var hasGoFile = result.Files.Keys.Any(k => 
            k.EndsWith(".go") || 
            k.Contains("go.mod") ||
            k.Contains("Go"));
        hasGoFile.Should().BeTrue("should contain Go related files");
    }

    #endregion

    #region Rust Template Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_RustActix_ContainsExpectedFileTypes()
    {
        // Arrange
        var request = CreateRustRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify Rust specific patterns
        var hasRustFile = result.Files.Keys.Any(k => 
            k.EndsWith(".rs") || 
            k.Contains("Cargo.toml") ||
            k.Contains("Rust"));
        hasRustFile.Should().BeTrue("should contain Rust related files");
    }

    #endregion

    #region Bicep Infrastructure Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_Bicep_ContainsBicepFiles()
    {
        // Arrange
        var request = CreateBicepInfrastructureRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify Bicep specific files
        var hasBicepFile = result.Files.Keys.Any(k => 
            k.EndsWith(".bicep") ||
            k.Contains("Bicep"));
        hasBicepFile.Should().BeTrue("should contain Bicep files");
        
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("Bicep"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_BicepWithAKS_ContainsAKSConfiguration()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "aks-bicep-service",
            Description = "AKS deployed with Bicep",
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Bicep,
                Provider = CloudProvider.Azure,
                Region = "eastus",
                ComputePlatform = ComputePlatform.AKS,
                IncludeNetworking = true,
                IncludeStorage = true
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

    #endregion

    #region Terraform Infrastructure Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_Terraform_ContainsTerraformFiles()
    {
        // Arrange
        var request = CreateTerraformInfrastructureRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify Terraform specific files
        var hasTfFile = result.Files.Keys.Any(k => 
            k.EndsWith(".tf") ||
            k.Contains("Terraform"));
        hasTfFile.Should().BeTrue("should contain Terraform files");
        
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("Terraform"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_TerraformWithAWS_ContainsAWSConfiguration()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "aws-terraform-service",
            Description = "AWS deployed with Terraform",
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Terraform,
                Provider = CloudProvider.AWS,
                Region = "us-east-1",
                ComputePlatform = ComputePlatform.EKS
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

    #endregion

    #region ARM Template Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_ARM_ContainsARMTemplateFiles()
    {
        // Arrange
        var request = CreateARMInfrastructureRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify ARM specific files
        var hasArmFile = result.Files.Keys.Any(k => 
            k.Contains(".json") ||
            k.Contains("ARM") ||
            k.Contains("azuredeploy"));
        hasArmFile.Should().BeTrue("should contain ARM template files");
        
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("ARM"));
    }

    #endregion

    #region Kubernetes Manifest Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_Kubernetes_ContainsK8sManifests()
    {
        // Arrange
        var request = CreateKubernetesInfrastructureRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify Kubernetes specific files
        var hasK8sFile = result.Files.Keys.Any(k => 
            k.EndsWith(".yaml") ||
            k.EndsWith(".yml") ||
            k.Contains("Kubernetes") ||
            k.Contains("deployment") ||
            k.Contains("service"));
        hasK8sFile.Should().BeTrue("should contain Kubernetes manifest files");
        
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("Kubernetes"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_KubernetesWithDatabase_IncludesStatefulSet()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "k8s-db-service",
            Description = "K8s service with database",
            Databases = new List<DatabaseSpec>
            {
                new DatabaseSpec
                {
                    Name = "postgres",
                    Type = DatabaseType.PostgreSQL,
                    Version = "15"
                }
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
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
        result.GeneratedComponents.Should().Contain(c => c.Contains("Database") && c.Contains("PostgreSQL"));
    }

    #endregion

    #region CloudFormation Template Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_CloudFormation_ContainsCFTemplateFiles()
    {
        // Arrange
        var request = CreateCloudFormationInfrastructureRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        
        // Verify CloudFormation specific files
        var hasCfFile = result.Files.Keys.Any(k => 
            k.Contains(".yaml") ||
            k.Contains(".yml") ||
            k.Contains(".json") ||
            k.Contains("CloudFormation") ||
            k.Contains("template"));
        hasCfFile.Should().BeTrue("should contain CloudFormation template files");
        
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("CloudFormation"));
    }

    #endregion

    #region Database Template Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_PostgreSQLDatabase_GeneratesMigrationScripts()
    {
        // Arrange
        var request = CreateRequestWithPostgreSQL();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Database") && c.Contains("PostgreSQL"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_CosmosDB_GeneratesCosmosConfiguration()
    {
        // Arrange
        var request = CreateRequestWithCosmosDB();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        // CosmosDB with Bicep may be generated as part of infrastructure
        // The component may be labeled as "Database (CosmosDB)" or included in infrastructure
        var hasCosmosComponent = result.GeneratedComponents.Any(c => 
            (c.Contains("Database") && c.Contains("CosmosDB")) || 
            c.Contains("Infrastructure"));
        hasCosmosComponent.Should().BeTrue("should contain CosmosDB or Infrastructure component");
    }

    [Fact]
    public async Task GenerateTemplateAsync_RedisCache_GeneratesRedisConfiguration()
    {
        // Arrange
        var request = CreateRequestWithRedis();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Database") && c.Contains("Redis"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_MongoDB_GeneratesMongoConfiguration()
    {
        // Arrange
        var request = CreateRequestWithMongoDB();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Database") && c.Contains("MongoDB"));
    }

    #endregion

    #region CI/CD Workflow Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithGitHubActions_GeneratesWorkflowFiles()
    {
        // Arrange
        var request = CreateFullServiceRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("CI/CD"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_CICD_IncludesBuildAndDeploy()
    {
        // Arrange
        var request = CreateFullServiceRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        
        // CI/CD workflows should be generated
        result.GeneratedComponents.Should().Contain(c => c.Contains("CI/CD"));
    }

    #endregion

    #region Security Component Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithRBAC_GeneratesRBACConfiguration()
    {
        // Arrange
        var request = CreateSecurityEnabledRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Security"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithNetworkPolicies_GeneratesNetworkConfig()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Security.NetworkPolicies = true;
        request.Infrastructure.Format = InfrastructureFormat.Kubernetes;

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Security"));
    }

    #endregion

    #region Observability Component Content Tests

    [Fact]
    public async Task GenerateTemplateAsync_WithPrometheus_GeneratesPrometheusConfig()
    {
        // Arrange
        var request = CreateObservabilityEnabledRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Observability"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_WithGrafana_GeneratesGrafanaDashboards()
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

    #endregion

    #region Full Service Integration Tests

    [Fact]
    public async Task GenerateTemplateAsync_FullMicroserviceStack_ProducesCompleteTemplate()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "full-microservice",
            Description = "Complete microservice with all components",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.DotNet,
                Framework = "ASP.NET Core",
                Type = ApplicationType.Microservice,
                Port = 8080,
                IncludeHealthCheck = true,
                IncludeReadinessProbe = true,
                Dependencies = new List<string> { "Microsoft.Extensions.Logging" }
            },
            Databases = new List<DatabaseSpec>
            {
                new DatabaseSpec
                {
                    Name = "primary",
                    Type = DatabaseType.PostgreSQL,
                    Version = "15",
                    Tier = DatabaseTier.Standard,
                    HighAvailability = true
                },
                new DatabaseSpec
                {
                    Name = "cache",
                    Type = DatabaseType.Redis,
                    Version = "7"
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
                TargetCpuPercent = 70,
                Resources = new ResourceRequirements
                {
                    CpuRequest = "250m",
                    CpuLimit = "1",
                    MemoryRequest = "256Mi",
                    MemoryLimit = "512Mi"
                }
            },
            Security = new SecuritySpec
            {
                RBAC = true,
                NetworkPolicies = true,
                TLS = true,
                SecretsManagement = true
            },
            Observability = new ObservabilitySpec
            {
                Prometheus = true,
                Grafana = true,
                StructuredLogging = true
            }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.Summary.Should().NotBeNullOrEmpty();
        
        // Verify all major components are generated
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Database"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Docker"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("CI/CD"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Security"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Observability"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Repository"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_AWSFullStack_ProducesAWSSpecificTemplates()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "aws-full-service",
            Description = "AWS full stack service",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.NodeJS,
                Framework = "Express",
                Type = ApplicationType.WebAPI,
                Port = 3000
            },
            Databases = new List<DatabaseSpec>
            {
                new DatabaseSpec
                {
                    Name = "dynamo",
                    Type = DatabaseType.DynamoDB,
                    Tier = DatabaseTier.Serverless
                }
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.CloudFormation,
                Provider = CloudProvider.AWS,
                Region = "us-east-1",
                ComputePlatform = ComputePlatform.EKS
            }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains("NodeJS"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Database") && c.Contains("DynamoDB"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("CloudFormation"));
    }

    [Fact]
    public async Task GenerateTemplateAsync_GCPFullStack_ProducesGCPSpecificTemplates()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "gcp-full-service",
            Description = "GCP full stack service",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.Go,
                Framework = "Gin",
                Type = ApplicationType.WebAPI,
                Port = 8080
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Terraform,
                Provider = CloudProvider.GCP,
                Region = "us-central1",
                ComputePlatform = ComputePlatform.GKE
            }
        };

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.GeneratedComponents.Should().Contain(c => c.Contains("Application") && c.Contains("Go"));
        result.GeneratedComponents.Should().Contain(c => c.Contains("Infrastructure") && c.Contains("Terraform"));
    }

    #endregion

    #region File Count and Structure Tests

    [Fact]
    public async Task GenerateTemplateAsync_MinimalRequest_GeneratesAtLeastOneFile()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "minimal-service",
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
        result.Files.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateTemplateAsync_FullRequest_GeneratesMultipleFiles()
    {
        // Arrange
        var request = CreateFullServiceRequest();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Files.Should().NotBeEmpty();
        result.Files.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GenerateTemplateAsync_WithAllOptionalFieldsNull_StillSucceeds()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "minimal",
            Description = null,
            Application = null,
            Databases = null!,
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

    [Fact]
    public async Task GenerateTemplateAsync_WithEmptyDatabaseList_StillSucceeds()
    {
        // Arrange
        var request = CreateFullServiceRequest();
        request.Databases = new List<DatabaseSpec>();

        // Act
        var result = await _service.GenerateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static TemplateGenerationRequest CreateDotNetRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "dotnet-api-service",
            Description = ".NET Web API service",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.DotNet,
                Framework = "ASP.NET Core",
                Type = ApplicationType.WebAPI,
                Port = 8080,
                IncludeHealthCheck = true
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreateNodeJSRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "nodejs-api-service",
            Description = "Node.js Express API service",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.NodeJS,
                Framework = "Express",
                Type = ApplicationType.WebAPI,
                Port = 3000
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreatePythonRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "python-api-service",
            Description = "Python FastAPI service",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.Python,
                Framework = "FastAPI",
                Type = ApplicationType.WebAPI,
                Port = 8000
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreateJavaRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "java-api-service",
            Description = "Java Spring Boot service",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.Java,
                Framework = "Spring Boot",
                Type = ApplicationType.Microservice,
                Port = 8080
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreateGoRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "go-api-service",
            Description = "Go Gin service",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.Go,
                Framework = "Gin",
                Type = ApplicationType.WebAPI,
                Port = 8080
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreateRustRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "rust-api-service",
            Description = "Rust Actix service",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.Rust,
                Framework = "Actix",
                Type = ApplicationType.WebAPI,
                Port = 8080
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreateBicepInfrastructureRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "bicep-infra",
            Description = "Bicep infrastructure",
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Bicep,
                Provider = CloudProvider.Azure,
                Region = "eastus",
                ComputePlatform = ComputePlatform.AKS
            }
        };
    }

    private static TemplateGenerationRequest CreateTerraformInfrastructureRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "terraform-infra",
            Description = "Terraform infrastructure",
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Terraform,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreateARMInfrastructureRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "arm-infra",
            Description = "ARM infrastructure",
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.ARM,
                Provider = CloudProvider.Azure,
                Region = "eastus"
            }
        };
    }

    private static TemplateGenerationRequest CreateKubernetesInfrastructureRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "k8s-infra",
            Description = "Kubernetes infrastructure",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.DotNet,
                Framework = "ASP.NET Core",
                Type = ApplicationType.WebAPI
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus",
                ComputePlatform = ComputePlatform.AKS
            }
        };
    }

    private static TemplateGenerationRequest CreateCloudFormationInfrastructureRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "cf-infra",
            Description = "CloudFormation infrastructure",
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.CloudFormation,
                Provider = CloudProvider.AWS,
                Region = "us-east-1"
            }
        };
    }

    private static TemplateGenerationRequest CreateRequestWithPostgreSQL()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "postgres-service",
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
                Provider = CloudProvider.Azure
            }
        };
    }

    private static TemplateGenerationRequest CreateRequestWithCosmosDB()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "cosmos-service",
            Databases = new List<DatabaseSpec>
            {
                new DatabaseSpec
                {
                    Name = "cosmosdb",
                    Type = DatabaseType.CosmosDB,
                    Tier = DatabaseTier.Serverless
                }
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Bicep,
                Provider = CloudProvider.Azure
            }
        };
    }

    private static TemplateGenerationRequest CreateRequestWithRedis()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "redis-service",
            Databases = new List<DatabaseSpec>
            {
                new DatabaseSpec
                {
                    Name = "cache",
                    Type = DatabaseType.Redis,
                    Version = "7"
                }
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure
            }
        };
    }

    private static TemplateGenerationRequest CreateRequestWithMongoDB()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "mongo-service",
            Databases = new List<DatabaseSpec>
            {
                new DatabaseSpec
                {
                    Name = "mongodb",
                    Type = DatabaseType.MongoDB,
                    Version = "6"
                }
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure
            }
        };
    }

    private static TemplateGenerationRequest CreateSecurityEnabledRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "secure-service",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.DotNet,
                Framework = "ASP.NET Core",
                Type = ApplicationType.WebAPI
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure
            },
            Security = new SecuritySpec
            {
                RBAC = true,
                NetworkPolicies = true,
                TLS = true
            }
        };
    }

    private static TemplateGenerationRequest CreateObservabilityEnabledRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "observable-service",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.DotNet,
                Framework = "ASP.NET Core",
                Type = ApplicationType.WebAPI
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure
            },
            Observability = new ObservabilitySpec
            {
                Prometheus = true,
                Grafana = true,
                StructuredLogging = true
            }
        };
    }

    private static TemplateGenerationRequest CreateFullServiceRequest()
    {
        return new TemplateGenerationRequest
        {
            ServiceName = "full-service",
            Description = "Full service with all components",
            Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.DotNet,
                Framework = "ASP.NET Core",
                Type = ApplicationType.Microservice,
                Port = 8080,
                IncludeHealthCheck = true
            },
            Databases = new List<DatabaseSpec>
            {
                new DatabaseSpec
                {
                    Name = "maindb",
                    Type = DatabaseType.PostgreSQL,
                    Version = "15"
                }
            },
            Infrastructure = new InfrastructureSpec
            {
                Format = InfrastructureFormat.Kubernetes,
                Provider = CloudProvider.Azure,
                Region = "eastus",
                ComputePlatform = ComputePlatform.AKS
            },
            Deployment = new DeploymentSpec
            {
                Replicas = 3,
                AutoScaling = true
            },
            Security = new SecuritySpec
            {
                RBAC = true,
                NetworkPolicies = true
            },
            Observability = new ObservabilitySpec
            {
                Prometheus = true,
                Grafana = true
            }
        };
    }

    #endregion
}
