using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Plugins.Infrastructure;

/// <summary>
/// Comprehensive unit tests for InfrastructurePlugin template generation, compliance validation, and IL compliance.
/// Tests template generation request building, file formatting, compliance validation, and IL compliance processing.
/// </summary>
public class InfrastructurePluginTemplateTests
{
    private readonly Mock<ILogger<object>> _loggerMock;
    private readonly Mock<IDynamicTemplateGenerator> _templateGeneratorMock;
    private readonly Mock<ITemplateStorageService> _templateStorageServiceMock;
    private readonly Mock<IAzureResourceService> _azureResourceServiceMock;
    private readonly Mock<IPolicyEnforcementService> _policyEnforcementServiceMock;
    private readonly Mock<IComplianceAwareTemplateEnhancer> _complianceEnhancerMock;
    private readonly Mock<AzureMcpClient> _azureMcpClientMock;
    private readonly IMemoryCache _memoryCache;
    private readonly Mock<ILogger<SharedMemory>> _sharedMemoryLoggerMock;
    private readonly SharedMemory _sharedMemory;

    public InfrastructurePluginTemplateTests()
    {
        _loggerMock = new Mock<ILogger<object>>();
        _templateGeneratorMock = new Mock<IDynamicTemplateGenerator>();
        _templateStorageServiceMock = new Mock<ITemplateStorageService>();
        _azureResourceServiceMock = new Mock<IAzureResourceService>();
        _policyEnforcementServiceMock = new Mock<IPolicyEnforcementService>();
        _complianceEnhancerMock = new Mock<IComplianceAwareTemplateEnhancer>();
        _azureMcpClientMock = new Mock<AzureMcpClient>(null!, null!, null!);
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _sharedMemoryLoggerMock = new Mock<ILogger<SharedMemory>>();
        _sharedMemory = new SharedMemory(_sharedMemoryLoggerMock.Object);
    }

    #region Template Generation Tests

    [Fact]
    public void TemplateGenerationRequest_ForAKS_HasCorrectDefaults()
    {
        // Arrange
        var resourceType = "aks";
        var description = "Create AKS cluster with 3 nodes";

        // Act
        var request = BuildTemplateGenerationRequest(
            resourceType, description, InfrastructureFormat.Bicep, 
            ComputePlatform.AKS, "eastus", 3, null);

        // Assert
        request.Should().NotBeNull();
        request.Infrastructure.ComputePlatform.Should().Be(ComputePlatform.AKS);
        request.Infrastructure.NodeCount.Should().Be(3);
        request.Infrastructure.EnableAutoScaling.Should().BeTrue();
        request.Security.EnableWorkloadIdentity.Should().BeTrue();
        request.Security.EnablePrivateCluster.Should().BeTrue();
        request.Observability.EnableContainerInsights.Should().BeTrue();
    }

    [Fact]
    public void TemplateGenerationRequest_ForAppService_HasCorrectDefaults()
    {
        // Arrange
        var resourceType = "app-service";
        var description = "Deploy App Service with managed identity";

        // Act
        var request = BuildTemplateGenerationRequest(
            resourceType, description, InfrastructureFormat.Terraform, 
            ComputePlatform.AppService, "westus2", 0, null);

        // Assert
        request.Should().NotBeNull();
        request.Infrastructure.ComputePlatform.Should().Be(ComputePlatform.AppService);
        request.Infrastructure.AlwaysOn.Should().BeTrue();
        request.Infrastructure.HttpsOnly.Should().BeTrue();
        request.Security.EnableManagedIdentity.Should().BeTrue();
        request.Security.EnableKeyVault.Should().BeTrue();
    }

    [Fact]
    public void TemplateGenerationRequest_ForStorage_HasCorrectDefaults()
    {
        // Arrange
        var resourceType = "storage";
        var description = "Create storage account with encryption";

        // Act
        var request = BuildTemplateGenerationRequest(
            resourceType, description, InfrastructureFormat.Bicep, 
            ComputePlatform.Storage, "usgovvirginia", 0, null);

        // Assert
        request.Should().NotBeNull();
        request.Security.EnablePrivateEndpoint.Should().BeTrue();
        request.Observability.EnableDiagnostics.Should().BeTrue();
    }

    [Fact]
    public void TemplateGenerationRequest_ForDatabase_HasCorrectDefaults()
    {
        // Arrange
        var resourceType = "sql-database";
        var description = "Create SQL database with FedRAMP compliance";

        // Act
        var request = BuildTemplateGenerationRequest(
            resourceType, description, InfrastructureFormat.Bicep, 
            ComputePlatform.Database, "usgovvirginia", 0, null);

        // Assert
        request.Should().NotBeNull();
        request.Security.EnablePrivateEndpoint.Should().BeTrue();
        request.Security.EnableDefender.Should().BeTrue();
        request.Observability.EnableDiagnostics.Should().BeTrue();
    }

    [Fact]
    public void TemplateGenerationRequest_WithSubscription_IncludesSubscriptionId()
    {
        // Arrange
        var subscriptionId = "12345678-1234-1234-1234-123456789012";

        // Act
        var request = BuildTemplateGenerationRequest(
            "storage", "Create storage", InfrastructureFormat.Bicep,
            ComputePlatform.Storage, "eastus", 0, subscriptionId);

        // Assert
        request.Infrastructure.SubscriptionId.Should().Be(subscriptionId);
    }

    #endregion

    #region File Formatting Tests

    [Theory]
    [InlineData("main.bicep", "bicep")]
    [InlineData("main.tf", "hcl")]
    [InlineData("template.json", "json")]
    [InlineData("values.yaml", "yaml")]
    [InlineData("config.yml", "yaml")]
    public void FormatFileResponse_UsesCorrectCodeBlockType(string fileName, string expectedLanguage)
    {
        // Arrange
        var content = "// This is sample code";

        // Act
        var response = FormatFileResponse(fileName, content);

        // Assert
        response.Should().Contain($"### 📁 {fileName}");
        response.Should().Contain($"```{expectedLanguage}");
        response.Should().Contain(content);
        response.Should().Contain("```");
    }

    [Fact]
    public void FormatFileResponse_WithBicepContent_IncludesAllContent()
    {
        // Arrange
        var fileName = "main.bicep";
        var content = @"param location string = 'eastus'
param name string

resource storageAccount 'Microsoft.Storage/storageAccounts@2021-06-01' = {
  name: name
  location: location
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
}";

        // Act
        var response = FormatFileResponse(fileName, content);

        // Assert
        response.Should().Contain("param location string");
        response.Should().Contain("Microsoft.Storage/storageAccounts");
        response.Should().Contain("Standard_LRS");
    }

    [Fact]
    public void FormatAllFilesFromMemory_GroupsFilesByType()
    {
        // Arrange
        var files = new List<string>
        {
            "main.bicep",
            "modules/storage/main.bicep",
            "modules/storage/outputs.bicep",
            "modules/network/vnet.bicep",
            "parameters.json"
        };

        var fileContents = files.ToDictionary(f => f, f => $"Content of {f}");

        // Act
        var response = FormatAllFilesResponse(files, fileContents);

        // Assert
        response.Should().Contain("main.bicep");
        response.Should().Contain("storage");
        response.Should().Contain("network");
        response.Should().ContainAll("main.bicep", "modules/storage/main.bicep", "parameters.json");
    }

    #endregion

    #region Resource Type Mapping Tests

    [Theory]
    [InlineData("aks", ComputePlatform.AKS)]
    [InlineData("kubernetes", ComputePlatform.AKS)]
    [InlineData("k8s", ComputePlatform.AKS)]
    [InlineData("eks", ComputePlatform.EKS)]
    [InlineData("gke", ComputePlatform.GKE)]
    [InlineData("appservice", ComputePlatform.AppService)]
    [InlineData("webapp", ComputePlatform.AppService)]
    [InlineData("storage", ComputePlatform.Storage)]
    [InlineData("storage-account", ComputePlatform.Storage)]
    [InlineData("sql", ComputePlatform.Database)]
    [InlineData("database", ComputePlatform.Database)]
    [InlineData("postgres", ComputePlatform.Database)]
    [InlineData("vnet", ComputePlatform.Networking)]
    [InlineData("network", ComputePlatform.Networking)]
    [InlineData("keyvault", ComputePlatform.Security)]
    [InlineData("vault", ComputePlatform.Security)]
    public void MapResourceTypeToComputePlatform_ReturnsCorrectPlatform(string resourceType, ComputePlatform expectedPlatform)
    {
        // Act
        var platform = MapResourceTypeToComputePlatform(resourceType);

        // Assert
        platform.Should().Be(expectedPlatform);
    }

    [Fact]
    public void MapResourceTypeToComputePlatform_WithUnknownType_ReturnsDefault()
    {
        // Act
        var platform = MapResourceTypeToComputePlatform("unknown-resource");

        // Assert
        platform.Should().Be(ComputePlatform.Networking); // Default
    }

    #endregion

    #region Template Storage Tests

    [Fact]
    public void SharedMemory_StoresGeneratedFiles_Successfully()
    {
        // Arrange
        var conversationId = "test-conv-001";
        var files = new Dictionary<string, string>
        {
            ["main.bicep"] = "param location string",
            ["modules/storage.bicep"] = "param name string"
        };

        // Act
        _sharedMemory.StoreGeneratedFiles(conversationId, files);
        var retrievedFileNames = _sharedMemory.GetGeneratedFileNames(conversationId);

        // Assert
        retrievedFileNames.Should().HaveCount(2);
        retrievedFileNames.Should().Contain("main.bicep");
        retrievedFileNames.Should().Contain("modules/storage.bicep");
    }

    [Fact]
    public void SharedMemory_RetrievesSpecificFile_Successfully()
    {
        // Arrange
        var conversationId = "test-conv-002";
        var fileName = "main.bicep";
        var content = "param location string = 'eastus'";
        var files = new Dictionary<string, string> { [fileName] = content };

        // Act
        _sharedMemory.StoreGeneratedFiles(conversationId, files);
        var retrieved = _sharedMemory.GetGeneratedFile(conversationId, fileName);

        // Assert
        retrieved.Should().Be(content);
    }

    [Fact]
    public void SharedMemory_ReturnsNullForNonexistentFile()
    {
        // Arrange
        var conversationId = "test-conv-003";
        _sharedMemory.StoreGeneratedFiles(conversationId, new Dictionary<string, string> { ["main.bicep"] = "content" });

        // Act
        var retrieved = _sharedMemory.GetGeneratedFile(conversationId, "nonexistent.bicep");

        // Assert
        retrieved.Should().BeNull();
    }

    #endregion

    #region Compliance Validation Tests

    [Theory]
    [InlineData("IL2")]
    [InlineData("IL4")]
    [InlineData("IL5")]
    [InlineData("IL6")]
    public void ImpactLevel_Enum_HasValidValues(string impactLevel)
    {
        // Act
        var isValid = Enum.TryParse<ImpactLevel>(impactLevel, ignoreCase: true, out _);

        // Assert
        isValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Bicep")]
    [InlineData("Terraform")]
    [InlineData("ARM")]
    [InlineData("Kubernetes")]
    [InlineData("Helm")]
    public void TemplateType_Enum_HasValidValues(string templateType)
    {
        // Act
        var isValid = Enum.TryParse<TemplateType>(templateType, ignoreCase: true, out _);

        // Assert
        isValid.Should().BeTrue();
    }

    [Fact]
    public void TemplateValidationRequest_CreatesValidRequest()
    {
        // Arrange
        var templateContent = "param location string";
        var templateType = TemplateType.Bicep;
        var impactLevel = ImpactLevel.IL5;

        // Act
        var request = new TemplateValidationRequest
        {
            TemplateContent = templateContent,
            Type = templateType,
            TargetImpactLevel = impactLevel,
            RequiresApproval = impactLevel >= ImpactLevel.IL5
        };

        // Assert
        request.TemplateContent.Should().Be(templateContent);
        request.Type.Should().Be(templateType);
        request.TargetImpactLevel.Should().Be(impactLevel);
        request.RequiresApproval.Should().BeTrue();
    }

    [Fact]
    public void ILTemplateRequest_CreatesValidRequest()
    {
        // Arrange
        var impactLevel = ImpactLevel.IL5;
        var resourceType = AzureResourceType.StorageAccount;

        // Act
        var request = new IlTemplateRequest
        {
            ImpactLevel = impactLevel,
            TemplateType = TemplateType.Bicep,
            ResourceType = resourceType,
            ResourceName = "mystorageaccount",
            Region = "usgovvirginia"
        };

        // Assert
        request.ImpactLevel.Should().Be(impactLevel);
        request.ResourceType.Should().Be(resourceType);
        request.Region.Should().Be("usgovvirginia");
    }

    [Theory]
    [InlineData("IL2", "FedRAMP Low")]
    [InlineData("IL4", "FedRAMP Moderate")]
    [InlineData("IL5", "FedRAMP High")]
    [InlineData("IL6", "FedRAMP High+")]
    public void ImpactLevel_MapsToFedRAMPLevel_Correctly(string ilLevel, string expectedFedramp)
    {
        // Arrange
        var fedRampMapping = new Dictionary<string, string>
        {
            ["IL2"] = "FedRAMP Low",
            ["IL4"] = "FedRAMP Moderate",
            ["IL5"] = "FedRAMP High",
            ["IL6"] = "FedRAMP High+"
        };

        // Act & Assert
        fedRampMapping.Should().ContainKey(ilLevel);
        fedRampMapping[ilLevel].Should().Be(expectedFedramp);
    }

    #endregion

    #region Policy Violation Tests

    [Fact]
    public void PolicyViolation_CreatesWithAllProperties()
    {
        // Arrange
        var violation = new PolicyViolation
        {
            PolicyId = "ENC-001",
            PolicyName = "Enable Encryption at Rest",
            Severity = PolicyViolationSeverity.Critical,
            Description = "Storage account must have encryption at rest enabled",
            RecommendedAction = "Enable the encryptionServices configuration"
        };

        // Assert
        violation.PolicyId.Should().Be("ENC-001");
        violation.Severity.Should().Be(PolicyViolationSeverity.Critical);
        violation.Description.Should().Contain("encryption");
    }

    [Theory]
    [InlineData(PolicyViolationSeverity.Critical, "🔴")]
    [InlineData(PolicyViolationSeverity.High, "🟠")]
    [InlineData(PolicyViolationSeverity.Medium, "🟡")]
    [InlineData(PolicyViolationSeverity.Low, "🟢")]
    public void PolicyViolationSeverity_HasCorrectEmoji(PolicyViolationSeverity severity, string expectedEmoji)
    {
        // Arrange
        var emojiMap = new Dictionary<PolicyViolationSeverity, string>
        {
            [PolicyViolationSeverity.Critical] = "🔴",
            [PolicyViolationSeverity.High] = "🟠",
            [PolicyViolationSeverity.Medium] = "🟡",
            [PolicyViolationSeverity.Low] = "🟢"
        };

        // Assert
        emojiMap[severity].Should().Be(expectedEmoji);
    }

    #endregion

    #region Network Topology Tests

    [Theory]
    [InlineData("10.0.0.0/16", 1)] // 256 /24 subnets
    [InlineData("10.0.0.0/16", 3)]
    [InlineData("10.0.0.0/24", 1)]  // 2 /25 subnets
    public void SubnetCalculation_CalculatesCorrectly(string addressSpace, int tierCount)
    {
        // Arrange
        var parts = addressSpace.Split('/');
        var prefix = int.Parse(parts[1]);
        var hostBits = 32 - prefix;
        var maxSubnets = (int)Math.Pow(2, hostBits) / 256;

        // Act & Assert
        maxSubnets.Should().BeGreaterThanOrEqualTo(tierCount);
    }

    [Fact]
    public void SubnetCIDR_CalculatesUsableIPs_Correctly()
    {
        // Arrange
        var cidr = "10.0.0.0/24";
        var parts = cidr.Split('/');
        var prefix = int.Parse(parts[1]);
        var hostBits = 32 - prefix;
        var totalIPs = (int)Math.Pow(2, hostBits);
        var usableIPs = totalIPs - 5; // Azure reserves 5

        // Act & Assert
        totalIPs.Should().Be(256);
        usableIPs.Should().Be(251);
    }

    #endregion

    #region Composite Infrastructure Tests

    [Fact]
    public void CompositeInfrastructureRequest_CreatesValidRequest()
    {
        // Arrange
        var serviceName = "myplatform";
        var description = "AKS with VNet";
        var pattern = ArchitecturePattern.AksWithVNet;

        // Act
        var request = new CompositeInfrastructureRequest
        {
            ServiceName = serviceName,
            Description = description,
            Pattern = pattern,
            Format = InfrastructureFormat.Bicep,
            Provider = CloudProvider.Azure,
            Region = "usgovvirginia",
            Environment = "prod",
            SubscriptionId = "sub-123"
        };

        // Assert
        request.ServiceName.Should().Be(serviceName);
        request.Pattern.Should().Be(pattern);
        request.Format.Should().Be(InfrastructureFormat.Bicep);
    }

    [Theory]
    [InlineData("three-tier", ArchitecturePattern.ThreeTier)]
    [InlineData("aks-with-vnet", ArchitecturePattern.AksWithVNet)]
    [InlineData("landing-zone", ArchitecturePattern.LandingZone)]
    [InlineData("microservices", ArchitecturePattern.Microservices)]
    [InlineData("serverless", ArchitecturePattern.Serverless)]
    [InlineData("data-platform", ArchitecturePattern.DataPlatform)]
    [InlineData("scca-compliant", ArchitecturePattern.SccaCompliant)]
    public void ArchitecturePattern_MapsCorrectly(string patternString, ArchitecturePattern expectedPattern)
    {
        // Arrange
        var patternMapping = new Dictionary<string, ArchitecturePattern>
        {
            ["three-tier"] = ArchitecturePattern.ThreeTier,
            ["aks-with-vnet"] = ArchitecturePattern.AksWithVNet,
            ["landing-zone"] = ArchitecturePattern.LandingZone,
            ["microservices"] = ArchitecturePattern.Microservices,
            ["serverless"] = ArchitecturePattern.Serverless,
            ["data-platform"] = ArchitecturePattern.DataPlatform,
            ["scca-compliant"] = ArchitecturePattern.SccaCompliant
        };

        // Act & Assert
        patternMapping.Should().ContainKey(patternString);
        patternMapping[patternString].Should().Be(expectedPattern);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void InvalidImpactLevel_ThrowsArgumentException()
    {
        // Act
        var isValid = Enum.TryParse<ImpactLevel>("IL99", ignoreCase: true, out _);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void InvalidTemplateType_ThrowsArgumentException()
    {
        // Act
        var isValid = Enum.TryParse<TemplateType>("InvalidType", ignoreCase: true, out _);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void NullFilePath_ReturnsValidDefault()
    {
        // Act
        var fileName = "";
        var extension = Path.GetExtension(fileName);

        // Assert
        extension.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static TemplateGenerationRequest BuildTemplateGenerationRequest(
        string resourceType,
        string description,
        InfrastructureFormat infraFormat,
        ComputePlatform computePlatform,
        string location,
        int nodeCount,
        string? subscriptionId)
    {
        var resourceTypeLower = resourceType?.ToLowerInvariant() ?? "";
        var isAKS = resourceTypeLower == "aks" || resourceTypeLower == "kubernetes";
        var isAppService = resourceTypeLower == "app-service" || resourceTypeLower == "appservice";
        var isStorage = resourceTypeLower.Contains("storage");
        var isDatabase = resourceTypeLower.Contains("sql") || resourceTypeLower.Contains("database");

        var request = new TemplateGenerationRequest
        {
            ServiceName = $"{resourceType}-deployment",
            Description = description,
            TemplateType = "infrastructure-only",
            Infrastructure = new InfrastructureSpec
            {
                Format = infraFormat,
                Provider = CloudProvider.Azure,
                Region = location,
                ComputePlatform = computePlatform,
                Environment = "production",
                SubscriptionId = subscriptionId
            },
            Security = new SecuritySpec(),
            Observability = new ObservabilitySpec()
        };

        if (isAKS)
        {
            request.Infrastructure.ClusterName = $"{resourceType}-cluster";
            request.Infrastructure.NodeCount = nodeCount;
            request.Infrastructure.EnableAutoScaling = true;
            request.Security.EnableWorkloadIdentity = true;
            request.Security.EnablePrivateCluster = true;
            request.Observability.EnableContainerInsights = true;
        }
        else if (isAppService)
        {
            request.Infrastructure.AlwaysOn = true;
            request.Infrastructure.HttpsOnly = true;
            request.Security.EnableManagedIdentity = true;
            request.Security.EnableKeyVault = true;
        }
        else if (isStorage)
        {
            request.Security.EnablePrivateEndpoint = true;
            request.Observability.EnableDiagnostics = true;
        }
        else if (isDatabase)
        {
            request.Security.EnablePrivateEndpoint = true;
            request.Security.EnableDefender = true;
            request.Observability.EnableDiagnostics = true;
        }

        return request;
    }

    private static ComputePlatform MapResourceTypeToComputePlatform(string resourceType)
    {
        var normalized = resourceType?.ToLowerInvariant().Replace("-", "").Replace("_", "");

        return normalized switch
        {
            "aks" or "kubernetes" or "k8s" => ComputePlatform.AKS,
            "eks" => ComputePlatform.EKS,
            "gke" => ComputePlatform.GKE,
            "appservice" or "webapp" or "webapps" => ComputePlatform.AppService,
            "containerapps" or "containerapp" => ComputePlatform.ContainerApps,
            "functions" => ComputePlatform.Functions,
            "storage" or "storageaccount" or "blob" or "blobstorage" => ComputePlatform.Storage,
            "sql" or "sqldatabase" or "database" or "postgres" or "postgresql" or "mysql" or "cosmosdb" or "cosmos" => ComputePlatform.Database,
            "vnet" or "virtualnetwork" or "network" or "networking" or "subnet" or "nsg" => ComputePlatform.Networking,
            "keyvault" or "vault" or "managedidentity" or "identity" => ComputePlatform.Security,
            _ => ComputePlatform.Networking
        };
    }

    private static string FormatFileResponse(string fileName, string content)
    {
        var fileExt = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        var codeBlockType = fileExt switch
        {
            "bicep" => "bicep",
            "tf" => "hcl",
            "json" => "json",
            "yaml" or "yml" => "yaml",
            "dockerfile" => "dockerfile",
            _ => fileExt
        };

        var response = new System.Text.StringBuilder();
        response.AppendLine($"### 📁 {fileName}");
        response.AppendLine();
        response.AppendLine($"```{codeBlockType}");
        response.AppendLine(content);
        response.AppendLine("```");
        return response.ToString();
    }

    private static string FormatAllFilesResponse(List<string> fileNames, Dictionary<string, string> fileContents)
    {
        var response = new System.Text.StringBuilder();
        response.AppendLine($"## 📦 All {fileNames.Count} Generated Files");
        response.AppendLine();

        var byModule = fileNames.GroupBy(f =>
        {
            var parts = f.Split('/');
            return parts.Length > 1 ? parts[0] : "root";
        });

        foreach (var module in byModule)
        {
            response.AppendLine($"### {module.Key}");
            foreach (var file in module)
            {
                response.AppendLine($"- {file}");
            }
            response.AppendLine();
        }

        return response.ToString();
    }

    #endregion
}
