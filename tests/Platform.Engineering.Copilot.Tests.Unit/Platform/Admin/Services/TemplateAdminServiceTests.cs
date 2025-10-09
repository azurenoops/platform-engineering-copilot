using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Admin.Models;
using Platform.Engineering.Copilot.Admin.Services;
using Xunit;
using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;
using EnvironmentTemplate = Platform.Engineering.Copilot.Core.Models.EnvironmentTemplate;

namespace Platform.Engineering.Copilot.Tests.Unit.Platform.Admin.Services;

public class TemplateAdminServiceTests
{
    private readonly Mock<ILogger<TemplateAdminService>> _mockLogger;
    private readonly Mock<IDynamicTemplateGenerator> _mockTemplateGenerator;
    private readonly Mock<ITemplateStorageService> _mockTemplateStorage;
    private readonly Mock<IEnvironmentManagementEngine> _mockEnvironmentEngine;
    private readonly TemplateAdminService _service;

    public TemplateAdminServiceTests()
    {
        _mockLogger = new Mock<ILogger<TemplateAdminService>>();
        _mockTemplateGenerator = new Mock<IDynamicTemplateGenerator>();
        _mockTemplateStorage = new Mock<ITemplateStorageService>();
        _mockEnvironmentEngine = new Mock<IEnvironmentManagementEngine>();

        _service = new TemplateAdminService(
            _mockLogger.Object,
            _mockTemplateGenerator.Object,
            _mockTemplateStorage.Object,
            _mockEnvironmentEngine.Object
        );
    }

    #region CreateTemplateAsync Tests

    [Fact]
    public async Task CreateTemplateAsync_WithValidRequest_CreatesTemplate()
    {
        // Arrange
        var request = new CreateTemplateRequest
        {
            TemplateName = "test-template",
            ServiceName = "test-service",
            Description = "Test template",
            Version = "1.0.0",
            CreatedBy = "admin@test.com",
            IsPublic = true,
            TemplateType = "microservice"
        };

        var generationResult = new TemplateGenerationResult
        {
            Success = true,
            Files = new Dictionary<string, string>
            {
                { "infra/main.bicep", "// Bicep template content" },
                { "app/app.yaml", "# App config" }
            },
            Summary = "Template generated successfully",
            GeneratedComponents = new List<string> { "bicep", "kubernetes" }
        };

        var savedTemplate = new EnvironmentTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.TemplateName,
            Description = request.Description,
            Version = request.Version
        };

        _mockTemplateGenerator
            .Setup(g => g.GenerateTemplateAsync(It.IsAny<TemplateGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generationResult);

        _mockTemplateStorage
            .Setup(s => s.StoreTemplateAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTemplate);

        // Act
        var result = await _service.CreateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TemplateId.Should().Be(savedTemplate.Id.ToString());
        result.TemplateName.Should().Be(request.TemplateName);
        result.GeneratedFiles.Should().HaveCount(2);
        result.ComponentsGenerated.Should().Contain("bicep");
    }

    [Fact]
    public async Task CreateTemplateAsync_WhenGenerationFails_ReturnsFailure()
    {
        // Arrange
        var request = new CreateTemplateRequest
        {
            TemplateName = "test-template",
            ServiceName = "test-service"
        };

        var generationResult = new TemplateGenerationResult
        {
            Success = false,
            ErrorMessage = "Template generation failed"
        };

        _mockTemplateGenerator
            .Setup(g => g.GenerateTemplateAsync(It.IsAny<TemplateGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generationResult);

        // Act
        var result = await _service.CreateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Template generation failed");
        result.TemplateName.Should().Be(request.TemplateName);
    }

    [Fact]
    public async Task CreateTemplateAsync_WhenStorageThrowsException_ReturnsFailure()
    {
        // Arrange
        var request = new CreateTemplateRequest
        {
            TemplateName = "test-template",
            ServiceName = "test-service"
        };

        var generationResult = new TemplateGenerationResult
        {
            Success = true,
            Files = new Dictionary<string, string> { { "infra/main.bicep", "content" } }
        };

        _mockTemplateGenerator
            .Setup(g => g.GenerateTemplateAsync(It.IsAny<TemplateGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generationResult);

        _mockTemplateStorage
            .Setup(s => s.StoreTemplateAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage error"));

        // Act
        var result = await _service.CreateTemplateAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Storage error");
    }

    [Fact]
    public async Task CreateTemplateAsync_WithComputeConfiguration_AppliesConfiguration()
    {
        // Arrange
        var request = new CreateTemplateRequest
        {
            TemplateName = "test-template",
            ServiceName = "test-service",
            Compute = new ComputeConfiguration
            {
                InstanceType = "Standard_D2s_v3",
                EnableAutoScaling = true,
                MinInstances = 2,
                MaxInstances = 10,
                CpuLimit = "2",
                MemoryLimit = "4Gi"
            }
        };

        var generationResult = new TemplateGenerationResult
        {
            Success = true,
            Files = new Dictionary<string, string> { { "infra/main.bicep", "content" } }
        };

        var savedTemplate = new EnvironmentTemplate { Id = Guid.NewGuid(), Name = request.TemplateName };

        _mockTemplateGenerator
            .Setup(g => g.GenerateTemplateAsync(It.IsAny<TemplateGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generationResult);

        _mockTemplateStorage
            .Setup(s => s.StoreTemplateAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTemplate);

        // Act
        var result = await _service.CreateTemplateAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        _mockTemplateGenerator.Verify(g => g.GenerateTemplateAsync(
            It.Is<TemplateGenerationRequest>(req =>
                req.Deployment.AutoScaling == true &&
                req.Deployment.MinReplicas == 2 &&
                req.Deployment.MaxReplicas == 10 &&
                req.Deployment.Resources.CpuLimit == "2" &&
                req.Deployment.Resources.MemoryLimit == "4Gi"
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task CreateTemplateAsync_WithNetworkConfiguration_AppliesConfiguration()
    {
        // Arrange
        var request = new CreateTemplateRequest
        {
            TemplateName = "test-template",
            ServiceName = "test-service",
            Network = new NetworkConfiguration
            {
                VNetName = "test-vnet",
                VNetAddressSpace = "10.1.0.0/16",
                EnableNetworkSecurityGroup = true,
                NsgMode = "new",
                Subnets = new List<SubnetConfig>
                {
                    new SubnetConfig
                    {
                        Name = "app-subnet",
                        AddressPrefix = "10.1.1.0/24"
                    }
                }
            }
        };

        var generationResult = new TemplateGenerationResult
        {
            Success = true,
            Files = new Dictionary<string, string> { { "infra/main.bicep", "content" } }
        };

        var savedTemplate = new EnvironmentTemplate { Id = Guid.NewGuid(), Name = request.TemplateName };

        _mockTemplateGenerator
            .Setup(g => g.GenerateTemplateAsync(It.IsAny<TemplateGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generationResult);

        _mockTemplateStorage
            .Setup(s => s.StoreTemplateAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(savedTemplate);

        // Act
        var result = await _service.CreateTemplateAsync(request);

        // Assert
        result.Success.Should().BeTrue();
        _mockTemplateGenerator.Verify(g => g.GenerateTemplateAsync(
            It.Is<TemplateGenerationRequest>(req =>
                req.Infrastructure.IncludeNetworking == true &&
                req.Infrastructure.NetworkConfig.VNetName == "test-vnet" &&
                req.Infrastructure.NetworkConfig.VNetAddressSpace == "10.1.0.0/16" &&
                req.Infrastructure.NetworkConfig.EnableNetworkSecurityGroup == true &&
                req.Infrastructure.NetworkConfig.Subnets.Count == 1
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    #endregion

    #region UpdateTemplateAsync Tests

    [Fact]
    public async Task UpdateTemplateAsync_WithValidRequest_UpdatesTemplate()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();
        var existingTemplate = new EnvironmentTemplate
        {
            Id = Guid.Parse(templateId),
            Name = "existing-template",
            Description = "Old description",
            Version = "1.0.0"
        };

        var updateRequest = new UpdateTemplateRequest
        {
            Description = "Updated description",
            Version = "1.1.0",
            IsActive = true
        };

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        _mockTemplateStorage
            .Setup(s => s.StoreTemplateAsync(It.IsAny<string>(), It.IsAny<EnvironmentTemplate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        // Act
        var result = await _service.UpdateTemplateAsync(templateId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.TemplateId.Should().Be(templateId);
        _mockTemplateStorage.Verify(s => s.StoreTemplateAsync(
            existingTemplate.Name,
            It.Is<EnvironmentTemplate>(t =>
                t.Description == "Updated description" &&
                t.Version == "1.1.0" &&
                t.IsActive == true
            ),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task UpdateTemplateAsync_WithNonExistentTemplate_ReturnsFailure()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();
        var updateRequest = new UpdateTemplateRequest { Description = "Updated" };

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnvironmentTemplate?)null);

        // Act
        var result = await _service.UpdateTemplateAsync(templateId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task UpdateTemplateAsync_WithTemplateGenerationRequest_RegeneratesTemplate()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();
        var existingTemplate = new EnvironmentTemplate
        {
            Id = Guid.Parse(templateId),
            Name = "existing-template",
            Content = "old content"
        };

        var generationRequest = new TemplateGenerationRequest
        {
            ServiceName = "updated-service"
        };

        var updateRequest = new UpdateTemplateRequest
        {
            TemplateGenerationRequest = generationRequest
        };

        var generationResult = new TemplateGenerationResult
        {
            Success = true,
            Files = new Dictionary<string, string> { { "infra/main.bicep", "new content" } },
            Summary = "Template regenerated"
        };

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        _mockTemplateGenerator
            .Setup(g => g.GenerateTemplateAsync(generationRequest, It.IsAny<CancellationToken>()))
            .ReturnsAsync(generationResult);

        _mockTemplateStorage
            .Setup(s => s.StoreTemplateAsync(It.IsAny<string>(), It.IsAny<EnvironmentTemplate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        // Act
        var result = await _service.UpdateTemplateAsync(templateId, updateRequest);

        // Assert
        result.Success.Should().BeTrue();
        _mockTemplateGenerator.Verify(g => g.GenerateTemplateAsync(generationRequest, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTemplateAsync_WhenRegenerationFails_ReturnsFailure()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();
        var existingTemplate = new EnvironmentTemplate { Id = Guid.Parse(templateId), Name = "test" };

        var updateRequest = new UpdateTemplateRequest
        {
            TemplateGenerationRequest = new TemplateGenerationRequest()
        };

        var generationResult = new TemplateGenerationResult
        {
            Success = false,
            ErrorMessage = "Regeneration failed"
        };

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTemplate);

        _mockTemplateGenerator
            .Setup(g => g.GenerateTemplateAsync(It.IsAny<TemplateGenerationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(generationResult);

        // Act
        var result = await _service.UpdateTemplateAsync(templateId, updateRequest);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Regeneration failed");
    }

    #endregion

    #region ListTemplatesAsync Tests

    [Fact]
    public async Task ListTemplatesAsync_WithoutSearchTerm_ReturnsAllTemplates()
    {
        // Arrange
        var templates = new List<EnvironmentTemplate>
        {
            new EnvironmentTemplate { Name = "template1", Description = "Test 1", TemplateType = "microservice" },
            new EnvironmentTemplate { Name = "template2", Description = "Test 2", TemplateType = "web-app" },
            new EnvironmentTemplate { Name = "template3", Description = "Test 3", TemplateType = "api" }
        };

        _mockTemplateStorage
            .Setup(s => s.ListAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        var result = await _service.ListTemplatesAsync();

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeEquivalentTo(templates);
    }

    [Fact]
    public async Task ListTemplatesAsync_WithSearchTerm_FiltersTemplates()
    {
        // Arrange
        var templates = new List<EnvironmentTemplate>
        {
            new EnvironmentTemplate { Name = "microservice-template", Description = "Test 1", TemplateType = "microservice" },
            new EnvironmentTemplate { Name = "web-app-template", Description = "Web application", TemplateType = "web-app" },
            new EnvironmentTemplate { Name = "api-template", Description = "API service", TemplateType = "api" }
        };

        _mockTemplateStorage
            .Setup(s => s.ListAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        var result = await _service.ListTemplatesAsync("microservice");

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("microservice-template");
    }

    [Fact]
    public async Task ListTemplatesAsync_SearchIsCaseInsensitive()
    {
        // Arrange
        var templates = new List<EnvironmentTemplate>
        {
            new EnvironmentTemplate { Name = "MicroService-Template", Description = "Test", TemplateType = "microservice" }
        };

        _mockTemplateStorage
            .Setup(s => s.ListAllTemplatesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(templates);

        // Act
        var result = await _service.ListTemplatesAsync("microservice");

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region GetTemplateAsync Tests

    [Fact]
    public async Task GetTemplateAsync_WithExistingTemplate_ReturnsTemplate()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();
        var template = new EnvironmentTemplate { Id = Guid.Parse(templateId), Name = "test-template" };

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _service.GetTemplateAsync(templateId);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("test-template");
    }

    [Fact]
    public async Task GetTemplateAsync_WithNonExistentTemplate_ReturnsNull()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnvironmentTemplate?)null);

        // Act
        var result = await _service.GetTemplateAsync(templateId);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteTemplateAsync Tests

    [Fact]
    public async Task DeleteTemplateAsync_WithExistingTemplate_DeletesTemplate()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();
        var template = new EnvironmentTemplate { Id = Guid.Parse(templateId), Name = "test-template" };

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _mockTemplateStorage
            .Setup(s => s.DeleteTemplateAsync(template.Name, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _service.DeleteTemplateAsync(templateId);

        // Assert
        result.Should().BeTrue();
        _mockTemplateStorage.Verify(s => s.DeleteTemplateAsync(template.Name, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteTemplateAsync_WithNonExistentTemplate_ReturnsFalse()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnvironmentTemplate?)null);

        // Act
        var result = await _service.DeleteTemplateAsync(templateId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTemplateAsync_WhenStorageThrowsException_ReturnsFalse()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();
        var template = new EnvironmentTemplate { Id = Guid.Parse(templateId), Name = "test-template" };

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _mockTemplateStorage
            .Setup(s => s.DeleteTemplateAsync(template.Name, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage error"));

        // Act
        var result = await _service.DeleteTemplateAsync(templateId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region UpdateTemplateFileAsync Tests

    [Fact]
    public async Task UpdateTemplateFileAsync_WithValidFile_UpdatesFile()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();
        var fileName = "infra/main.bicep";
        var newContent = "// Updated bicep content";

        var template = new EnvironmentTemplate
        {
            Id = Guid.Parse(templateId),
            Name = "test-template",
            Files = new List<ServiceTemplateFile>
            {
                new ServiceTemplateFile { FileName = fileName, Content = "old content" }
            }
        };

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _mockTemplateStorage
            .Setup(s => s.UpdateTemplateAsync(It.IsAny<string>(), It.IsAny<EnvironmentTemplate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _service.UpdateTemplateFileAsync(templateId, fileName, newContent);

        // Assert
        result.Should().BeTrue();
        template.Files.First().Content.Should().Be(newContent);
        _mockTemplateStorage.Verify(s => s.UpdateTemplateAsync(template.Name, template, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateTemplateFileAsync_WithNonExistentTemplate_ReturnsFalse()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnvironmentTemplate?)null);

        // Act
        var result = await _service.UpdateTemplateFileAsync(templateId, "test.bicep", "content");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateTemplateFileAsync_WithNonExistentFile_ReturnsFalse()
    {
        // Arrange
        var templateId = Guid.NewGuid().ToString();
        var template = new EnvironmentTemplate
        {
            Id = Guid.Parse(templateId),
            Name = "test-template",
            Files = new List<ServiceTemplateFile>
            {
                new ServiceTemplateFile { FileName = "other.bicep", Content = "content" }
            }
        };

        _mockTemplateStorage
            .Setup(s => s.GetTemplateByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        // Act
        var result = await _service.UpdateTemplateFileAsync(templateId, "nonexistent.bicep", "new content");

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
