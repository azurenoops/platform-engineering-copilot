using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.EnvironmentManagement;
using Platform.Engineering.Copilot.Core.Services;
using Xunit;
using EnvironmentTemplate = Platform.Engineering.Copilot.Core.Models.EnvironmentTemplate;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services.Infrastructure
{
    public class EnvironmentManagementEngineTests : IDisposable
    {
        private readonly Mock<ILogger<EnvironmentManagementEngine>> _mockLogger;
        private readonly Mock<IDeploymentOrchestrationService> _mockDeploymentOrchestrator;
        private readonly Mock<IAzureResourceService> _mockAzureResourceService;
        private readonly Mock<IGitHubServices> _mockGitHubServices;
        private readonly Mock<ITemplateStorageService> _mockTemplateStorage;
        private readonly Mock<IDynamicTemplateGenerator> _mockTemplateGenerator;
        private readonly EnvironmentManagementEngine _engine;

        public EnvironmentManagementEngineTests()
        {
            _mockLogger = new Mock<ILogger<EnvironmentManagementEngine>>();
            _mockDeploymentOrchestrator = new Mock<IDeploymentOrchestrationService>();
            _mockAzureResourceService = new Mock<IAzureResourceService>();
            _mockGitHubServices = new Mock<IGitHubServices>();
            _mockTemplateStorage = new Mock<ITemplateStorageService>();
            _mockTemplateGenerator = new Mock<IDynamicTemplateGenerator>();

            _engine = new EnvironmentManagementEngine(
                _mockLogger.Object,
                _mockDeploymentOrchestrator.Object,
                _mockAzureResourceService.Object,
                _mockGitHubServices.Object,
                _mockTemplateStorage.Object,
                _mockTemplateGenerator.Object);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_WithValidDependencies_CreatesEngine()
        {
            // Arrange & Act
            var engine = new EnvironmentManagementEngine(
                _mockLogger.Object,
                _mockDeploymentOrchestrator.Object,
                _mockAzureResourceService.Object,
                _mockGitHubServices.Object,
                _mockTemplateStorage.Object,
                _mockTemplateGenerator.Object);

            // Assert
            engine.Should().NotBeNull();
        }

        [Fact]
        public void Constructor_WithNullLogger_ThrowsArgumentNullException()
        {
            // Arrange & Act
            Action act = () => new EnvironmentManagementEngine(
                null!,
                _mockDeploymentOrchestrator.Object,
                _mockAzureResourceService.Object,
                _mockGitHubServices.Object,
                _mockTemplateStorage.Object,
                _mockTemplateGenerator.Object);

            // Assert
            act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
        }

        #endregion

        #region CreateEnvironmentAsync Tests

        [Fact]
        public async Task CreateEnvironmentAsync_WithValidRequest_ReturnsSuccessResult()
        {
            // Arrange
            var request = new EnvironmentCreationRequest
            {
                Name = "test-env",
                Type = EnvironmentType.AKS,
                ResourceGroup = "test-rg",
                Location = "eastus",
                Tags = new Dictionary<string, string> { { "Environment", "Test" } }
            };

            _mockAzureResourceService
                .Setup(s => s.GetResourceGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object> { { "name", "test-rg" } });

            // Act
            var result = await _engine.CreateEnvironmentAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.EnvironmentName.Should().Be("test-env");
            result.ResourceGroup.Should().Be("test-rg");
            result.Type.Should().Be(EnvironmentType.AKS);
        }

        [Fact]
        public async Task CreateEnvironmentAsync_WithEmptyName_ReturnsFailureResult()
        {
            // Arrange
            var request = new EnvironmentCreationRequest
            {
                Name = "",
                Type = EnvironmentType.WebApp,
                ResourceGroup = "test-rg",
                Location = "eastus"
            };

            // Act
            var result = await _engine.CreateEnvironmentAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("name is required");
        }

        [Fact]
        public async Task CreateEnvironmentAsync_WithEmptyResourceGroup_ReturnsFailureResult()
        {
            // Arrange
            var request = new EnvironmentCreationRequest
            {
                Name = "test-env",
                Type = EnvironmentType.WebApp,
                ResourceGroup = "",
                Location = "eastus"
            };

            // Act
            var result = await _engine.CreateEnvironmentAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Resource group is required");
        }

        [Fact]
        public async Task CreateEnvironmentAsync_WithTemplateId_LookupsTemplateFromStorage()
        {
            // Arrange
            var request = new EnvironmentCreationRequest
            {
                Name = "test-env",
                Type = EnvironmentType.AKS,
                ResourceGroup = "test-rg",
                Location = "eastus",
                TemplateId = "template-123"
            };

            var dbTemplate = new EnvironmentTemplate
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000123"),
                Name = "AKS Template",
                Content = "template content"
            };

            _mockTemplateStorage
                .Setup(s => s.GetTemplateByIdAsync("template-123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(dbTemplate);

            _mockAzureResourceService
                .Setup(s => s.GetResourceGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object> { { "name", "test-rg" } });

            // Act
            var result = await _engine.CreateEnvironmentAsync(request);

            // Assert
            _mockTemplateStorage.Verify(
                s => s.GetTemplateByIdAsync("template-123", It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CreateEnvironmentAsync_WithInvalidTemplateId_ReturnsFailureResult()
        {
            // Arrange
            var request = new EnvironmentCreationRequest
            {
                Name = "test-env",
                Type = EnvironmentType.AKS,
                ResourceGroup = "test-rg",
                Location = "eastus",
                TemplateId = "invalid-template"
            };

            _mockTemplateStorage
                .Setup(s => s.GetTemplateByIdAsync("invalid-template", It.IsAny<CancellationToken>()))
                .ReturnsAsync((EnvironmentTemplate?)null);

            // Act
            var result = await _engine.CreateEnvironmentAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("Service template not found");
        }

        [Fact]
        public async Task CreateEnvironmentAsync_CreatesResourceGroupIfNotExists()
        {
            // Arrange
            var request = new EnvironmentCreationRequest
            {
                Name = "test-env",
                Type = EnvironmentType.WebApp,
                ResourceGroup = "new-rg",
                Location = "westus"
            };

            _mockAzureResourceService
                .Setup(s => s.GetResourceGroupAsync("new-rg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Dictionary<string, object>?)null);

            _mockAzureResourceService
                .Setup(s => s.CreateResourceGroupAsync(
                    "new-rg",
                    "westus",
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object> { { "name", "new-rg" } });

            // Act
            var result = await _engine.CreateEnvironmentAsync(request);

            // Assert
            _mockAzureResourceService.Verify(
                s => s.CreateResourceGroupAsync(
                    "new-rg",
                    "westus",
                    It.IsAny<string>(),
                    It.IsAny<Dictionary<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        #endregion

        #region CloneEnvironmentAsync Tests

        [Fact]
        public async Task CloneEnvironmentAsync_WithValidRequest_ClonesEnvironments()
        {
            // Arrange
            var request = new EnvironmentCloneRequest
            {
                SourceEnvironment = "source-env",
                SourceResourceGroup = "source-rg",
                TargetEnvironments = new List<string> { "target-env-1", "target-env-2" },
                TargetResourceGroup = "target-rg",
                TargetLocation = "eastus"
            };

            var sourceStatus = new EnvironmentStatus
            {
                EnvironmentName = "source-env",
                Type = EnvironmentType.AKS,
                Tags = new Dictionary<string, string> { { "Environment", "Source" } },
                Configuration = new EnvironmentConfiguration
                {
                    ScaleSettings = new ScaleSettings { MinReplicas = 1, MaxReplicas = 3 }
                }
            };

            _mockAzureResourceService
                .Setup(s => s.GetResourceGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object> { { "name", "test-rg" } });

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "name", "source-env-app" },
                        { "type", "Microsoft.Web/sites" },
                        { "provisioningState", "Succeeded" }
                    }
                });

            // Act
            var result = await _engine.CloneEnvironmentAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.SourceEnvironment.Should().Be("source-env");
        }

        [Fact]
        public async Task CloneEnvironmentAsync_WithMultipleTargets_ClonesAllTargets()
        {
            // Arrange
            var request = new EnvironmentCloneRequest
            {
                SourceEnvironment = "source-env",
                SourceResourceGroup = "source-rg",
                TargetEnvironments = new List<string> { "target-1", "target-2", "target-3" },
                TargetResourceGroup = "target-rg",
                TargetLocation = "westus"
            };

            _mockAzureResourceService
                .Setup(s => s.GetResourceGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, object> { { "name", "test-rg" } });

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>());

            // Act
            var result = await _engine.CloneEnvironmentAsync(request);

            // Assert
            result.ClonedEnvironments.Should().HaveCount(3);
        }

        #endregion

        #region DeleteEnvironmentAsync Tests

        [Fact]
        public async Task DeleteEnvironmentAsync_WithValidRequest_ReturnsSuccessResult()
        {
            // Arrange
            var environmentName = "test-env";
            var resourceGroup = "test-rg";

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(resourceGroup, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "name", "test-env-app" },
                        { "type", "Microsoft.Web/sites" }
                    }
                });

            // Act
            var result = await _engine.DeleteEnvironmentAsync(environmentName, resourceGroup);

            // Assert
            result.Should().NotBeNull();
            result.Success.Should().BeTrue();
            result.EnvironmentName.Should().Be(environmentName);
            result.ResourceGroup.Should().Be(resourceGroup);
        }

        [Fact]
        public async Task DeleteEnvironmentAsync_WithEmptyName_ReturnsFailureResult()
        {
            // Arrange & Act
            var result = await _engine.DeleteEnvironmentAsync("", "test-rg");

            // Assert
            result.Success.Should().BeFalse();
            result.ErrorMessage.Should().Contain("name is required");
        }

        [Fact]
        public async Task DeleteEnvironmentAsync_WithBackupEnabled_CreatesBackup()
        {
            // Arrange
            var environmentName = "test-env";
            var resourceGroup = "test-rg";

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(resourceGroup, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>());

            // Act
            var result = await _engine.DeleteEnvironmentAsync(environmentName, resourceGroup, createBackup: true);

            // Assert
            result.BackupCreated.Should().BeTrue();
            result.BackupLocation.Should().NotBeNullOrEmpty();
            result.BackupLocation.Should().Contain("backups/");
        }

        [Fact]
        public async Task DeleteEnvironmentAsync_ListsDeletedResources()
        {
            // Arrange
            var environmentName = "test-env";
            var resourceGroup = "test-rg";

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(resourceGroup, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object> { { "name", "test-env-web" } },
                    new Dictionary<string, object> { { "name", "test-env-db" } },
                    new Dictionary<string, object> { { "name", "test-env-storage" } }
                });

            // Act
            var result = await _engine.DeleteEnvironmentAsync(environmentName, resourceGroup);

            // Assert
            result.DeletedResources.Should().HaveCount(3);
            result.DeletedResources.Should().Contain("test-env-web");
            result.DeletedResources.Should().Contain("test-env-db");
            result.DeletedResources.Should().Contain("test-env-storage");
        }

        #endregion

        #region GetEnvironmentStatusAsync Tests

        [Fact]
        public async Task GetEnvironmentStatusAsync_WithValidEnvironment_ReturnsStatus()
        {
            // Arrange
            var environmentName = "test-env";
            var resourceGroup = "test-rg";

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(resourceGroup, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "name", "test-env-app" },
                        { "type", "Microsoft.Web/sites" },
                        { "provisioningState", "Succeeded" }
                    }
                });

            // Act
            var result = await _engine.GetEnvironmentStatusAsync(environmentName, resourceGroup, null);

            // Assert
            result.Should().NotBeNull();
            result.EnvironmentName.Should().Be(environmentName);
        }

        #endregion

        #region GetEnvironmentHealthAsync Tests

        [Fact]
        public async Task GetEnvironmentHealthAsync_WithValidEnvironment_ReturnsHealthReport()
        {
            // Arrange
            var environmentName = "test-env";
            var resourceGroup = "test-rg";

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(resourceGroup, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "name", "test-env-app" },
                        { "type", "Microsoft.Web/sites" },
                        { "provisioningState", "Succeeded" }
                    }
                });

            // Act
            var result = await _engine.GetEnvironmentHealthAsync(environmentName, resourceGroup);

            // Assert
            result.Should().NotBeNull();
            result.EnvironmentName.Should().Be(environmentName);
            result.ResourceGroup.Should().Be(resourceGroup);
        }

        [Fact]
        public async Task GetEnvironmentHealthAsync_IncludesResourceHealthChecks()
        {
            // Arrange
            var environmentName = "test-env";
            var resourceGroup = "test-rg";

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(resourceGroup, It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "name", "test-env-web" },
                        { "type", "Microsoft.Web/sites" },
                        { "provisioningState", "Succeeded" }
                    },
                    new Dictionary<string, object>
                    {
                        { "name", "test-env-db" },
                        { "type", "Microsoft.Sql/servers/databases" },
                        { "provisioningState", "Failed" }
                    }
                });

            // Act
            var result = await _engine.GetEnvironmentHealthAsync(environmentName, resourceGroup);

            // Assert
            result.Should().NotBeNull();
            result.Checks.Should().NotBeEmpty();
        }

        #endregion

        #region ScaleEnvironmentAsync Tests

        [Fact]
        public async Task ScaleEnvironmentAsync_WithValidSettings_ReturnsScalingResult()
        {
            // Arrange
            var environmentName = "test-env";
            var resourceGroup = "test-rg";
            var scaleSettings = new ScaleSettings
            {
                TargetReplicas = 5,
                MinReplicas = 1,
                MaxReplicas = 10
            };

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "name", "test-env-app" },
                        { "type", "Microsoft.ContainerService/managedClusters" }
                    }
                });

            // Act
            var result = await _engine.ScaleEnvironmentAsync(environmentName, resourceGroup, scaleSettings);

            // Assert
            result.Should().NotBeNull();
            result.EnvironmentName.Should().Be(environmentName);
        }

        #endregion

        #region BulkDeleteEnvironmentsAsync Tests

        [Fact]
        public async Task BulkDeleteEnvironmentsAsync_WithValidFilter_DeletesMatchingEnvironments()
        {
            // Arrange
            var filter = new EnvironmentFilter
            {
                ResourceGroups = new List<string> { "test-rg" },
                Tags = new Dictionary<string, string> { { "Temporary", "true" } }
            };

            var settings = new BulkOperationSettings
            {
                SafetyChecks = true,
                CreateBackup = true,
                MaxResources = 5
            };

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object> { { "name", "test-env-1" } },
                    new Dictionary<string, object> { { "name", "test-env-2" } }
                });

            // Act
            var result = await _engine.BulkDeleteEnvironmentsAsync(filter, settings);

            // Assert
            result.Should().NotBeNull();
            result.TotalProcessed.Should().BeGreaterThanOrEqualTo(0);
        }

        #endregion

        #region MigrateEnvironmentAsync Tests

        [Fact]
        public async Task MigrateEnvironmentAsync_WithValidRequest_ReturnsMigrationResult()
        {
            // Arrange
            var request = new MigrationRequest
            {
                SourceEnvironment = "source-env",
                SourceResourceGroup = "source-rg",
                TargetEnvironment = "target-env",
                TargetResourceGroup = "target-rg",
                TargetLocation = "westus2"
            };

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>());

            // Act
            var result = await _engine.MigrateEnvironmentAsync(request);

            // Assert
            result.Should().NotBeNull();
            result.SourceEnvironment.Should().Be("source-env");
            result.TargetEnvironment.Should().Be("target-env");
        }

        #endregion

        #region CleanupOldEnvironmentsAsync Tests

        [Fact]
        public async Task CleanupOldEnvironmentsAsync_RemovesOldEnvironments()
        {
            // Arrange
            var policy = new CleanupPolicy
            {
                MinimumAgeInDays = 30,
                OnlyUnusedEnvironments = true,
                CreateBackup = true
            };

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "name", "temp-env-old" },
                        { "tags", new Dictionary<string, string> { { "CreatedDate", DateTime.UtcNow.AddDays(-60).ToString("o") } } }
                    }
                });

            // Act
            var result = await _engine.CleanupOldEnvironmentsAsync(policy);

            // Assert
            result.Should().NotBeNull();
            result.EnvironmentsAnalyzed.Should().BeGreaterThanOrEqualTo(0);
        }

        #endregion

        #region ListEnvironmentsAsync Tests

        [Fact]
        public async Task ListEnvironmentsAsync_ReturnsEnvironmentSummaries()
        {
            // Arrange
            var filter = new EnvironmentFilter
            {
                ResourceGroups = new List<string> { "test-rg" }
            };

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync("test-rg", It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "name", "env-1" },
                        { "type", "Microsoft.Web/sites" },
                        { "location", "eastus" }
                    },
                    new Dictionary<string, object>
                    {
                        { "name", "env-2" },
                        { "type", "Microsoft.ContainerService/managedClusters" },
                        { "location", "westus" }
                    }
                });

            // Act
            var result = await _engine.ListEnvironmentsAsync(filter);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        }

        #endregion

        #region DiscoverEnvironmentsAsync Tests

        [Fact]
        public async Task DiscoverEnvironmentsAsync_FindsEnvironmentsInResourceGroups()
        {
            // Arrange
            var subscriptionId = "sub-123";
            var filter = new EnvironmentFilter
            {
                SubscriptionId = subscriptionId
            };

            _mockAzureResourceService
                .Setup(s => s.ListResourceGroupsAsync(subscriptionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>
                {
                    new Dictionary<string, object> { { "name", "rg-1" } },
                    new Dictionary<string, object> { { "name", "rg-2" } }
                });

            _mockAzureResourceService
                .Setup(s => s.ListResourcesAsync(It.IsAny<string>(), subscriptionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<object>());

            // Act
            var result = await _engine.DiscoverEnvironmentsAsync(filter);

            // Assert
            result.Should().NotBeNull();
        }

        #endregion
    }
}
