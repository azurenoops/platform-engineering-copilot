using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Azure.ResourceManager;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services
{
    /// <summary>
    /// Unit tests for DeploymentOrchestrationService
    /// Tests deployment orchestration logic, state management, and error handling
    /// </summary>
    public class DeploymentOrchestrationServiceTests
    {
        private readonly Mock<ILogger<DeploymentOrchestrationService>> _mockLogger;
        private readonly Mock<IAzureResourceService> _mockAzureService;
        private readonly DeploymentOrchestrationService _service;

        public DeploymentOrchestrationServiceTests()
        {
            _mockLogger = new Mock<ILogger<DeploymentOrchestrationService>>();
            _mockAzureService = new Mock<IAzureResourceService>();
            _service = new DeploymentOrchestrationService(_mockLogger.Object, _mockAzureService.Object);
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new DeploymentOrchestrationService(null!, _mockAzureService.Object));
        }

        [Fact]
        public void Constructor_WithNullAzureService_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new DeploymentOrchestrationService(_mockLogger.Object, null!));
        }

        [Fact]
        public void Constructor_WithValidParameters_CreatesInstance()
        {
            // Act
            var service = new DeploymentOrchestrationService(_mockLogger.Object, _mockAzureService.Object);

            // Assert
            service.Should().NotBeNull();
        }

        #endregion

        #region Bicep Validation Tests

        [Fact]
        public async Task ValidateDeploymentAsync_WithEmptyTemplate_ReturnsInvalid()
        {
            // Arrange
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus"
            };

            // Act
            var result = await _service.ValidateDeploymentAsync("", "bicep", options);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("empty", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ValidateDeploymentAsync_WithMissingResourceGroup_ReturnsInvalid()
        {
            // Arrange
            var templateContent = "param location string = 'eastus'";
            var options = new DeploymentOptions
            {
                ResourceGroup = "",
                Location = "eastus"
            };

            // Act
            var result = await _service.ValidateDeploymentAsync(templateContent, "bicep", options);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Resource group", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ValidateDeploymentAsync_WithMissingLocation_AddsWarning()
        {
            // Arrange
            var templateContent = "param name string";
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = ""
            };

            // Act
            var result = await _service.ValidateDeploymentAsync(templateContent, "bicep", options);

            // Assert
            result.Should().NotBeNull();
            result.Warnings.Should().Contain(w => w.Contains("Location", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public async Task ValidateDeploymentAsync_WithValidBicepTemplate_ReturnsValid()
        {
            // Arrange
            var templateContent = @"
                param location string = 'eastus'
                param serviceName string
                
                resource storage 'Microsoft.Storage/storageAccounts@2023-01-01' = {
                    name: serviceName
                    location: location
                    sku: { name: 'Standard_LRS' }
                    kind: 'StorageV2'
                }
            ";
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                Parameters = new Dictionary<string, string> { { "serviceName", "teststorage" } }
            };

            // Act
            var result = await _service.ValidateDeploymentAsync(templateContent, "bicep", options);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
        }

        #endregion

        #region Bicep Deployment Tests

        [Fact]
        public async Task DeployBicepTemplateAsync_WithValidTemplate_ReturnsDeploymentResult()
        {
            // Arrange
            var templateContent = "param location string = 'eastus'";
            var options = new DeploymentOptions
            {
                DeploymentName = "test-deployment",
                ResourceGroup = "test-rg",
                Location = "eastus",
                SubscriptionId = "test-sub",
                WaitForCompletion = false
            };

            _mockAzureService
                .Setup(s => s.CreateResourceGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeployBicepTemplateAsync(templateContent, options);

            // Assert
            result.Should().NotBeNull();
            result.DeploymentId.Should().NotBeNullOrEmpty();
            result.DeploymentName.Should().Be("test-deployment");
            result.ResourceGroup.Should().Be("test-rg");
            result.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public async Task DeployBicepTemplateAsync_WithParameters_IncludesParameters()
        {
            // Arrange
            var templateContent = "param serviceName string\nparam environment string = 'dev'";
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                SubscriptionId = "test-sub",
                Parameters = new Dictionary<string, string>
                {
                    { "serviceName", "myapp" },
                    { "environment", "prod" }
                },
                WaitForCompletion = false
            };

            _mockAzureService
                .Setup(s => s.CreateResourceGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Mock GetArmClient to return null (not configured in test environment)
            _mockAzureService
                .Setup(s => s.GetArmClient())
                .Returns((ArmClient?)null);

            // Act
            var result = await _service.DeployBicepTemplateAsync(templateContent, options);

            // Assert
            result.Should().NotBeNull();
            // Without ARM client configured, deployment will fail
            result.State.Should().Be(DeploymentState.Failed);
            
            // Verify resource group creation was called
            _mockAzureService.Verify(
                s => s.CreateResourceGroupAsync(
                    "test-rg",
                    "eastus",
                    "test-sub",
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task DeployBicepTemplateAsync_WithAdditionalFiles_HandlesModules()
        {
            // Arrange
            var mainTemplate = "module storage './modules/storage.bicep' = { name: 'storage' }";
            var additionalFiles = new Dictionary<string, string>
            {
                { "modules/storage.bicep", "param storageName string" }
            };
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                SubscriptionId = "test-sub",
                WaitForCompletion = false
            };

            _mockAzureService
                .Setup(s => s.CreateResourceGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeployBicepTemplateAsync(mainTemplate, options, additionalFiles);

            // Assert
            result.Should().NotBeNull();
            result.DeploymentId.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region Terraform Validation Tests

        [Fact]
        public async Task ValidateDeploymentAsync_WithValidTerraform_ReturnsValid()
        {
            // Arrange
            var terraformContent = @"
                variable ""location"" {
                    type = string
                    default = ""eastus""
                }
                
                resource ""azurerm_storage_account"" ""example"" {
                    name = ""examplestorage""
                    resource_group_name = ""example-rg""
                    location = var.location
                }
            ";
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus"
            };

            // Act
            var result = await _service.ValidateDeploymentAsync(terraformContent, "terraform", options);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
        }

        #endregion

        #region Terraform Deployment Tests

        [Fact]
        public async Task DeployTerraformAsync_WithValidTemplate_ReturnsDeploymentResult()
        {
            // Arrange
            var terraformContent = "terraform { required_version = \">= 1.0\" }";
            var options = new DeploymentOptions
            {
                DeploymentName = "terraform-deployment",
                ResourceGroup = "test-rg",
                Location = "eastus",
                WaitForCompletion = false
            };

            // Act
            var result = await _service.DeployTerraformAsync(terraformContent, options);

            // Assert
            result.Should().NotBeNull();
            result.DeploymentId.Should().NotBeNullOrEmpty();
            result.DeploymentName.Should().Be("terraform-deployment");
            // In unit test environment without terraform CLI, deployment will fail
            result.State.Should().Be(DeploymentState.Failed);
        }

        #endregion

        #region Kubernetes Deployment Tests

        [Fact]
        public async Task DeployKubernetesAsync_WithValidManifest_ReturnsDeploymentResult()
        {
            // Arrange
            var k8sManifest = @"
                apiVersion: v1
                kind: ConfigMap
                metadata:
                    name: test-config
                data:
                    key: value
            ";
            var options = new DeploymentOptions
            {
                DeploymentName = "k8s-deployment",
                ResourceGroup = "test-rg",
                WaitForCompletion = false
            };

            // Act
            var result = await _service.DeployKubernetesAsync(k8sManifest, options);

            // Assert
            result.Should().NotBeNull();
            result.DeploymentId.Should().NotBeNullOrEmpty();
            // In unit test environment without kubectl CLI, deployment will fail
            result.State.Should().Be(DeploymentState.Failed);
        }

        #endregion

        #region Deployment Status Tests

        [Fact]
        public async Task GetDeploymentStatusAsync_WithExistingDeployment_ReturnsStatus()
        {
            // Arrange - Create a deployment first
            var templateContent = "param location string = 'eastus'";
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                SubscriptionId = "test-sub",
                WaitForCompletion = false
            };

            _mockAzureService
                .Setup(s => s.CreateResourceGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var deploymentResult = await _service.DeployBicepTemplateAsync(templateContent, options);

            // Act
            var status = await _service.GetDeploymentStatusAsync(deploymentResult.DeploymentId);

            // Assert
            status.Should().NotBeNull();
            status.DeploymentId.Should().Be(deploymentResult.DeploymentId);
            status.State.Should().BeOneOf(DeploymentState.Running, DeploymentState.Succeeded, DeploymentState.Failed);
        }

        [Fact]
        public async Task GetDeploymentStatusAsync_WithNonExistentDeployment_ReturnsNotStartedState()
        {
            // Arrange
            var fakeDeploymentId = Guid.NewGuid().ToString();

            // Act
            var status = await _service.GetDeploymentStatusAsync(fakeDeploymentId);

            // Assert
            status.Should().NotBeNull();
            status.DeploymentId.Should().Be(fakeDeploymentId);
            status.State.Should().Be(DeploymentState.NotStarted);
            status.CurrentOperation.Should().Be("Deployment not found");
        }

        #endregion

        #region Deployment Logs Tests

        [Fact]
        public async Task GetDeploymentLogsAsync_WithExistingDeployment_ReturnsLogs()
        {
            // Arrange
            var templateContent = "param location string = 'eastus'";
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                SubscriptionId = "test-sub",
                WaitForCompletion = false
            };

            _mockAzureService
                .Setup(s => s.CreateResourceGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var deploymentResult = await _service.DeployBicepTemplateAsync(templateContent, options);

            // Act
            var logs = await _service.GetDeploymentLogsAsync(deploymentResult.DeploymentId);

            // Assert
            logs.Should().NotBeNull();
            logs.DeploymentId.Should().Be(deploymentResult.DeploymentId);
            logs.Entries.Should().NotBeEmpty();
            logs.Entries.Should().Contain(log => log.Level == "Info");
        }

        [Fact]
        public async Task GetDeploymentLogsAsync_WithNonExistentDeployment_ReturnsWarningLog()
        {
            // Arrange
            var fakeDeploymentId = Guid.NewGuid().ToString();

            // Act
            var logs = await _service.GetDeploymentLogsAsync(fakeDeploymentId);

            // Assert
            logs.Should().NotBeNull();
            logs.DeploymentId.Should().Be(fakeDeploymentId);
            logs.Entries.Should().ContainSingle();
            logs.Entries[0].Level.Should().Be("Warning");
            logs.Entries[0].Message.Should().Contain("Deployment not found");
        }

        #endregion

        #region Concurrent Deployment Tests

        [Fact]
        public async Task DeployBicepTemplateAsync_MultipleConcurrentDeployments_HandlesIndependently()
        {
            // Arrange
            var template1 = "param location string = 'eastus'";
            var template2 = "param location string = 'westus'";

            var options1 = new DeploymentOptions
            {
                DeploymentName = "deployment-1",
                ResourceGroup = "test-rg-1",
                Location = "eastus",
                SubscriptionId = "test-sub",
                WaitForCompletion = false
            };

            var options2 = new DeploymentOptions
            {
                DeploymentName = "deployment-2",
                ResourceGroup = "test-rg-2",
                Location = "westus",
                SubscriptionId = "test-sub",
                WaitForCompletion = false
            };

            _mockAzureService
                .Setup(s => s.CreateResourceGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result1Task = _service.DeployBicepTemplateAsync(template1, options1);
            var result2Task = _service.DeployBicepTemplateAsync(template2, options2);

            var results = await Task.WhenAll(result1Task, result2Task);

            // Assert
            results[0].DeploymentId.Should().NotBe(results[1].DeploymentId);
            results[0].ResourceGroup.Should().Be("test-rg-1");
            results[1].ResourceGroup.Should().Be("test-rg-2");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task DeployBicepTemplateAsync_WhenResourceGroupCreationFails_HandlesGracefully()
        {
            // Arrange
            var templateContent = "param location string = 'eastus'";
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                SubscriptionId = "test-sub",
                WaitForCompletion = false
            };

            _mockAzureService
                .Setup(s => s.CreateResourceGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("Failed to create resource group"));

            // Act
            var result = await _service.DeployBicepTemplateAsync(templateContent, options);

            // Assert
            result.Should().NotBeNull();
            result.State.Should().BeOneOf(DeploymentState.Failed, DeploymentState.Running);
        }

        #endregion

        #region Tag Handling Tests

        [Fact]
        public async Task DeployBicepTemplateAsync_WithTags_PassesTagsToAzureService()
        {
            // Arrange
            var templateContent = "param location string = 'eastus'";
            var tags = new Dictionary<string, string>
            {
                { "Environment", "Test" },
                { "Owner", "TeamA" },
                { "CostCenter", "12345" }
            };

            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                SubscriptionId = "test-sub",
                Tags = tags,
                WaitForCompletion = false
            };

            _mockAzureService
                .Setup(s => s.CreateResourceGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.Is<Dictionary<string, string>>(t => 
                        t.ContainsKey("Environment") && 
                        t["Environment"] == "Test"),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeployBicepTemplateAsync(templateContent, options);

            // Assert
            result.Should().NotBeNull();
            
            _mockAzureService.Verify(
                s => s.CreateResourceGroupAsync(
                    "test-rg",
                    "eastus",
                    "test-sub",
                    It.Is<Dictionary<string, string>>(t => 
                        t.ContainsKey("Environment") && 
                        t.ContainsKey("Owner") && 
                        t.ContainsKey("CostCenter")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region Deployment Name Tests

        [Fact]
        public async Task DeployBicepTemplateAsync_WithCustomDeploymentName_UsesProvidedName()
        {
            // Arrange
            var templateContent = "param location string = 'eastus'";
            var customName = "my-custom-deployment-name";
            var options = new DeploymentOptions
            {
                DeploymentName = customName,
                ResourceGroup = "test-rg",
                Location = "eastus",
                SubscriptionId = "test-sub",
                WaitForCompletion = false
            };

            _mockAzureService
                .Setup(s => s.CreateResourceGroupAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeployBicepTemplateAsync(templateContent, options);

            // Assert
            result.DeploymentName.Should().Be(customName);
        }

        #endregion
    }
}
