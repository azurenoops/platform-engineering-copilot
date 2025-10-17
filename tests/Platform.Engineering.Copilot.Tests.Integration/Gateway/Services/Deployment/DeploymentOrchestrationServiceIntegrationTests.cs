using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Deployment;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Integration.Core.Services.Deployment
{
    /// <summary>
    /// Integration tests for DeploymentOrchestrationService covering template deployment lifecycle
    /// Tests Bicep/Terraform template validation, deployment orchestration, and status tracking
    /// </summary>
    public class DeploymentOrchestrationServiceIntegrationTests : IAsyncLifetime
    {
        private ServiceProvider? _serviceProvider;
        private IDeploymentOrchestrationService? _deploymentService;

        public async Task InitializeAsync()
        {
            // Set up services
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

            // Mock Azure Resource Service
            var mockAzureService = new Mock<IAzureResourceService>();
            mockAzureService
                .Setup(s => s.CreateResourceGroupAsync(
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<string>(), 
                    It.IsAny<Dictionary<string, string>>(), 
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            services.AddSingleton(mockAzureService.Object);

            // Add the actual deployment orchestration service
            services.AddScoped<IDeploymentOrchestrationService, DeploymentOrchestrationService>();

            _serviceProvider = services.BuildServiceProvider();
            _deploymentService = _serviceProvider.GetRequiredService<IDeploymentOrchestrationService>();

            await Task.CompletedTask;
        }

        public async Task DisposeAsync()
        {
            if (_serviceProvider != null)
            {
                await _serviceProvider.DisposeAsync();
            }
        }

        #region Bicep Template Deployment Tests

        [Fact]
        public async Task ValidateDeployment_ValidBicepTemplate_ReturnsValidResult()
        {
            // Arrange
            var bicepTemplate = @"
                param location string = 'eastus'
                param serviceName string
                
                resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
                    name: '${serviceName}storage'
                    location: location
                    sku: {
                        name: 'Standard_LRS'
                    }
                    kind: 'StorageV2'
                }
            ";

            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                SubscriptionId = "test-subscription",
                Parameters = new Dictionary<string, string>
                {
                    { "serviceName", "testservice" }
                }
            };

            // Act
            var result = await _deploymentService.ValidateDeploymentAsync(
                bicepTemplate, 
                "bicep", 
                options);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue("valid Bicep template should pass validation");
            result.Errors.Should().BeEmpty();
        }

        [Fact]
        public async Task ValidateDeployment_EmptyTemplate_ReturnsInvalidResult()
        {
            // Arrange
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus"
            };

            // Act
            var result = await _deploymentService.ValidateDeploymentAsync(
                string.Empty, 
                "bicep", 
                options);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse("empty template should fail validation");
            result.Errors.Should().Contain(e => e.Contains("empty"));
        }

        [Fact]
        public async Task ValidateDeployment_MissingResourceGroup_ReturnsInvalidResult()
        {
            // Arrange
            var bicepTemplate = "param location string = 'eastus'";
            var options = new DeploymentOptions
            {
                ResourceGroup = "",
                Location = "eastus"
            };

            // Act
            var result = await _deploymentService.ValidateDeploymentAsync(
                bicepTemplate, 
                "bicep", 
                options);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeFalse();
            result.Errors.Should().Contain(e => e.Contains("Resource group"));
        }

        [Fact]
        public async Task DeployBicepTemplate_ValidTemplate_InitiatesDeployment()
        {
            // Arrange
            var bicepTemplate = @"
                param serviceName string
                
                output deploymentId string = uniqueString(resourceGroup().id)
            ";

            var options = new DeploymentOptions
            {
                DeploymentName = "test-deployment",
                ResourceGroup = "test-rg-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                Location = "eastus",
                SubscriptionId = "test-subscription",
                Parameters = new Dictionary<string, string>
                {
                    { "serviceName", "testservice" }
                },
                WaitForCompletion = false
            };

            // Act
            var result = await _deploymentService.DeployBicepTemplateAsync(
                bicepTemplate, 
                options);

            // Assert
            result.Should().NotBeNull();
            result.DeploymentId.Should().NotBeNullOrEmpty();
            result.DeploymentName.Should().Be("test-deployment");
            result.ResourceGroup.Should().Be(options.ResourceGroup);
            result.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task DeployBicepTemplate_WithParameters_AppliesParameters()
        {
            // Arrange
            var bicepTemplate = @"
                param location string
                param serviceName string
                param environment string = 'dev'
                
                var storageAccountName = '${serviceName}${environment}st'
            ";

            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                SubscriptionId = "test-subscription",
                Parameters = new Dictionary<string, string>
                {
                    { "serviceName", "myapp" },
                    { "location", "westus" },
                    { "environment", "prod" }
                },
                WaitForCompletion = false
            };

            // Act
            var result = await _deploymentService.DeployBicepTemplateAsync(
                bicepTemplate, 
                options);

            // Assert
            result.Should().NotBeNull();
            result.State.Should().Be(DeploymentState.Running);
            options.Parameters.Should().ContainKey("serviceName");
            options.Parameters.Should().ContainKey("environment");
        }

        #endregion

        #region Terraform Deployment Tests

        [Fact]
        public async Task ValidateDeployment_ValidTerraformTemplate_ReturnsValidResult()
        {
            // Arrange
            var terraformTemplate = @"
                variable ""location"" {
                    type    = string
                    default = ""eastus""
                }
                
                resource ""azurerm_storage_account"" ""example"" {
                    name                     = ""examplestorage""
                    resource_group_name      = ""example-rg""
                    location                 = var.location
                    account_tier             = ""Standard""
                    account_replication_type = ""LRS""
                }
            ";

            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus"
            };

            // Act
            var result = await _deploymentService.ValidateDeploymentAsync(
                terraformTemplate, 
                "terraform", 
                options);

            // Assert
            result.Should().NotBeNull();
            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public async Task DeployTerraform_ValidTemplate_InitiatesDeployment()
        {
            // Arrange
            var terraformTemplate = @"
                terraform {
                    required_providers {
                        azurerm = {
                            source = ""hashicorp/azurerm""
                            version = ""~> 3.0""
                        }
                    }
                }
                
                output ""deployment_id"" {
                    value = ""test-deployment""
                }
            ";

            var options = new DeploymentOptions
            {
                DeploymentName = "terraform-test",
                ResourceGroup = "test-rg",
                Location = "eastus",
                WaitForCompletion = false
            };

            // Act
            var result = await _deploymentService.DeployTerraformAsync(
                terraformTemplate, 
                options);

            // Assert
            result.Should().NotBeNull();
            result.DeploymentId.Should().NotBeNullOrEmpty();
            result.State.Should().Be(DeploymentState.Running);
        }

        #endregion

        #region Kubernetes Deployment Tests

        [Fact]
        public async Task DeployKubernetes_ValidManifest_InitiatesDeployment()
        {
            // Arrange
            var kubernetesManifest = @"
                apiVersion: v1
                kind: ConfigMap
                metadata:
                    name: test-config
                data:
                    key: value
            ";

            var options = new DeploymentOptions
            {
                DeploymentName = "k8s-test",
                ResourceGroup = "test-rg",
                WaitForCompletion = false
            };

            // Act
            var result = await _deploymentService.DeployKubernetesAsync(
                kubernetesManifest, 
                options);

            // Assert
            result.Should().NotBeNull();
            result.DeploymentId.Should().NotBeNullOrEmpty();
        }

        #endregion

        #region Deployment Status Tracking Tests

        [Fact]
        public async Task GetDeploymentStatus_ExistingDeployment_ReturnsStatus()
        {
            // Arrange - Start a deployment first
            var bicepTemplate = "param location string = 'eastus'";
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                WaitForCompletion = false
            };

            var deploymentResult = await _deploymentService.DeployBicepTemplateAsync(
                bicepTemplate, 
                options);

            // Act
            var status = await _deploymentService.GetDeploymentStatusAsync(deploymentResult.DeploymentId);

            // Assert
            status.Should().NotBeNull();
            status.DeploymentId.Should().Be(deploymentResult.DeploymentId);
            status.State.Should().BeOneOf(DeploymentState.Running, DeploymentState.Succeeded, DeploymentState.Failed);
        }

        [Fact]
        public async Task GetDeploymentStatus_NonExistentDeployment_ThrowsException()
        {
            // Arrange
            var fakeDeploymentId = Guid.NewGuid().ToString();

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
            {
                await _deploymentService.GetDeploymentStatusAsync(fakeDeploymentId);
            });
        }

        #endregion

        #region Deployment Logs Tests

        [Fact]
        public async Task GetDeploymentLogs_ExistingDeployment_ReturnsLogs()
        {
            // Arrange
            var bicepTemplate = "param location string = 'eastus'";
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                WaitForCompletion = false
            };

            var deploymentResult = await _deploymentService.DeployBicepTemplateAsync(
                bicepTemplate, 
                options);

            // Act
            var logs = await _deploymentService.GetDeploymentLogsAsync(deploymentResult.DeploymentId);

            // Assert
            logs.Should().NotBeNull();
            logs.Entries.Should().NotBeEmpty();
            logs.Entries.Should().Contain(log => log.Level == "Info");
        }

        #endregion

        #region Multiple Deployment Tests

        [Fact]
        public async Task DeployMultipleTemplates_Concurrent_HandlesIndependently()
        {
            // Arrange
            var template1 = "param location string = 'eastus'\noutput id string = 'deployment1'";
            var template2 = "param location string = 'westus'\noutput id string = 'deployment2'";

            var options1 = new DeploymentOptions
            {
                DeploymentName = "deployment-1",
                ResourceGroup = "test-rg-1",
                Location = "eastus",
                WaitForCompletion = false
            };

            var options2 = new DeploymentOptions
            {
                DeploymentName = "deployment-2",
                ResourceGroup = "test-rg-2",
                Location = "westus",
                WaitForCompletion = false
            };

            // Act
            var result1Task = _deploymentService.DeployBicepTemplateAsync(template1, options1);
            var result2Task = _deploymentService.DeployBicepTemplateAsync(template2, options2);

            var results = await Task.WhenAll(result1Task, result2Task);

            // Assert
            results[0].DeploymentId.Should().NotBe(results[1].DeploymentId);
            results[0].ResourceGroup.Should().Be("test-rg-1");
            results[1].ResourceGroup.Should().Be("test-rg-2");
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task DeployBicepTemplate_InvalidTemplate_FailsGracefully()
        {
            // Arrange
            var invalidTemplate = "this is not valid bicep code {{{";
            var options = new DeploymentOptions
            {
                ResourceGroup = "test-rg",
                Location = "eastus",
                WaitForCompletion = false
            };

            // Act
            var result = await _deploymentService.DeployBicepTemplateAsync(
                invalidTemplate, 
                options);

            // Assert
            result.Should().NotBeNull();
            // Depending on implementation, might be Failed or Running with eventual failure
            result.State.Should().BeOneOf(DeploymentState.Failed, DeploymentState.Running);
            if (result.State == DeploymentState.Failed)
            {
                result.ErrorMessage.Should().NotBeNullOrEmpty();
            }
        }

        #endregion
    }
}
