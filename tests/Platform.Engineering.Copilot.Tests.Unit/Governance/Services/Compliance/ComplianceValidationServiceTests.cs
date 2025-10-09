using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Governance.Configuration;
using Platform.Engineering.Copilot.Governance.Services;
using Xunit;

namespace Platform.Engineering.Copilot.Tests.Unit.Governance.Services.Compliance;

/// <summary>
/// Comprehensive unit tests for ComplianceValidationService and NistControlsHealthCheck
/// Tests validation logic, health checks, and compliance data verification
/// </summary>
public class ComplianceValidationServiceTests
{
    private readonly Mock<INistControlsService> _mockNistControlsService;
    private readonly Mock<ILogger<ComplianceValidationService>> _mockValidationLogger;
    private readonly Mock<ILogger<NistControlsHealthCheck>> _mockHealthCheckLogger;
    private readonly NistControlsOptions _options;

    public ComplianceValidationServiceTests()
    {
        _mockNistControlsService = new Mock<INistControlsService>();
        _mockValidationLogger = new Mock<ILogger<ComplianceValidationService>>();
        _mockHealthCheckLogger = new Mock<ILogger<NistControlsHealthCheck>>();
        
        _options = new NistControlsOptions
        {
            BaseUrl = "https://example.com",
            CacheDurationHours = 4,
            EnableOfflineFallback = false,
            MaxRetryAttempts = 3,
            RetryDelaySeconds = 2,
            TimeoutSeconds = 30
        };
    }

    #region Constructor Tests

    [Fact]
    public void ComplianceValidationService_Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var service = new ComplianceValidationService(
            _mockNistControlsService.Object,
            _mockValidationLogger.Object);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void ComplianceValidationService_Constructor_WithNullNistControlsService_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ComplianceValidationService(
            null!,
            _mockValidationLogger.Object);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("nistControlsService");
    }

    [Fact]
    public void ComplianceValidationService_Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new ComplianceValidationService(
            _mockNistControlsService.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void NistControlsHealthCheck_Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var healthCheck = new NistControlsHealthCheck(
            _mockNistControlsService.Object,
            _mockHealthCheckLogger.Object,
            Options.Create(_options));

        // Assert
        healthCheck.Should().NotBeNull();
    }

    [Fact]
    public void NistControlsHealthCheck_Constructor_WithNullNistControlsService_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new NistControlsHealthCheck(
            null!,
            _mockHealthCheckLogger.Object,
            Options.Create(_options));

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("nistControlsService");
    }

    [Fact]
    public void NistControlsHealthCheck_Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new NistControlsHealthCheck(
            _mockNistControlsService.Object,
            null!,
            Options.Create(_options));

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void NistControlsHealthCheck_Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var act = () => new NistControlsHealthCheck(
            _mockNistControlsService.Object,
            _mockHealthCheckLogger.Object,
            null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    #endregion

    #region ValidateConfigurationAsync Tests

    [Fact]
    public async Task ValidateConfigurationAsync_WithValidConfiguration_ReturnsValidResult()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var catalog = CreateTestCatalog();

        _mockNistControlsService
            .Setup(x => x.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1");

        _mockNistControlsService
            .Setup(x => x.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(catalog);

        // Act
        var result = await service.ValidateConfigurationAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Info.Should().Contain(i => i.Contains("NIST controls version: 5.1"));
        result.Info.Should().Contain(i => i.Contains("controls"));
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithUnknownVersion_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();

        _mockNistControlsService
            .Setup(x => x.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Unknown");

        // Act
        var result = await service.ValidateConfigurationAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NIST controls service is not available"));
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithNullCatalog_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();

        _mockNistControlsService
            .Setup(x => x.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1");

        _mockNistControlsService
            .Setup(x => x.GetCatalogAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((NistCatalog?)null);

        // Act
        var result = await service.ValidateConfigurationAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NIST catalog is not available"));
    }

    [Fact]
    public async Task ValidateConfigurationAsync_WithException_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();

        _mockNistControlsService
            .Setup(x => x.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        // Act
        var result = await service.ValidateConfigurationAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Configuration validation failed"));
        result.Errors.Should().Contain(e => e.Contains("Service unavailable"));
    }

    #endregion

    #region ValidateControlMappingsAsync Tests

    [Fact]
    public async Task ValidateControlMappingsAsync_WithAllValidControls_ReturnsValidResult()
    {
        // Arrange
        var service = CreateComplianceValidationService();

        _mockNistControlsService
            .Setup(x => x.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await service.ValidateControlMappingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
        result.Info.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task ValidateControlMappingsAsync_WithInvalidControl_AddsWarning()
    {
        // Arrange
        var service = CreateComplianceValidationService();

        _mockNistControlsService
            .Setup(x => x.ValidateControlIdAsync("SC-13", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockNistControlsService
            .Setup(x => x.ValidateControlIdAsync(It.IsNotIn("SC-13"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await service.ValidateControlMappingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("SC-13") && w.Contains("not found"));
    }

    [Fact]
    public async Task ValidateControlMappingsAsync_WithException_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();

        _mockNistControlsService
            .Setup(x => x.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Validation failed"));

        // Act
        var result = await service.ValidateControlMappingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Control mapping validation failed"));
    }

    #endregion

    #region ValidateAtoScanRequest Tests

    [Fact]
    public void ValidateAtoScanRequest_WithValidRequest_ReturnsValidResult()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var request = new AtoScanRequest
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            ResourceGroupName = "test-rg",
            Configuration = new AtoScanConfiguration
            {
                ComplianceFrameworks = new List<string> { "NIST-800-53" }
            }
        };

        // Act
        var result = service.ValidateAtoScanRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAtoScanRequest_WithNullRequest_ReturnsInvalidResult()
    {
        // Arrange
        var service = CreateComplianceValidationService();

        // Act
        var result = service.ValidateAtoScanRequest(null!);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cannot be null"));
    }

    [Fact]
    public void ValidateAtoScanRequest_WithMissingSubscriptionId_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var request = new AtoScanRequest
        {
            SubscriptionId = "",
            ResourceGroupName = "test-rg",
            Configuration = new AtoScanConfiguration
            {
                ComplianceFrameworks = new List<string> { "NIST-800-53" }
            }
        };

        // Act
        var result = service.ValidateAtoScanRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Subscription ID is required"));
    }

    [Fact]
    public void ValidateAtoScanRequest_WithInvalidSubscriptionIdFormat_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var request = new AtoScanRequest
        {
            SubscriptionId = "invalid-guid",
            ResourceGroupName = "test-rg",
            Configuration = new AtoScanConfiguration
            {
                ComplianceFrameworks = new List<string> { "NIST-800-53" }
            }
        };

        // Act
        var result = service.ValidateAtoScanRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("must be a valid GUID"));
    }

    [Fact]
    public void ValidateAtoScanRequest_WithMissingResourceGroupName_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var request = new AtoScanRequest
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            ResourceGroupName = "",
            Configuration = new AtoScanConfiguration
            {
                ComplianceFrameworks = new List<string> { "NIST-800-53" }
            }
        };

        // Act
        var result = service.ValidateAtoScanRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Resource group name is required"));
    }

    [Fact]
    public void ValidateAtoScanRequest_WithTooLongResourceGroupName_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var request = new AtoScanRequest
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            ResourceGroupName = new string('a', 91), // 91 characters
            Configuration = new AtoScanConfiguration
            {
                ComplianceFrameworks = new List<string> { "NIST-800-53" }
            }
        };

        // Act
        var result = service.ValidateAtoScanRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cannot exceed 90 characters"));
    }

    [Fact]
    public void ValidateAtoScanRequest_WithNullConfiguration_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var request = new AtoScanRequest
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            ResourceGroupName = "test-rg",
            Configuration = null
        };

        // Act
        var result = service.ValidateAtoScanRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Scan configuration is required"));
    }

    [Fact]
    public void ValidateAtoScanRequest_WithNoComplianceFrameworks_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var request = new AtoScanRequest
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            ResourceGroupName = "test-rg",
            Configuration = new AtoScanConfiguration
            {
                ComplianceFrameworks = new List<string>()
            }
        };

        // Act
        var result = service.ValidateAtoScanRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("At least one compliance framework must be specified"));
    }

    [Fact]
    public void ValidateAtoScanRequest_WithUnknownFramework_AddsWarning()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var request = new AtoScanRequest
        {
            SubscriptionId = Guid.NewGuid().ToString(),
            ResourceGroupName = "test-rg",
            Configuration = new AtoScanConfiguration
            {
                ComplianceFrameworks = new List<string> { "NIST-800-53", "UNKNOWN-FRAMEWORK" }
            }
        };

        // Act
        var result = service.ValidateAtoScanRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Unknown compliance frameworks") && w.Contains("UNKNOWN-FRAMEWORK"));
    }

    #endregion

    #region ValidateAtoFinding Tests

    [Fact]
    public void ValidateAtoFinding_WithValidFinding_ReturnsValidResult()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var finding = new AtoFinding
        {
            Id = "finding-1",
            ResourceId = "/subscriptions/sub1/resourceGroups/rg1/providers/Microsoft.Compute/virtualMachines/vm1",
            Title = "Encryption not enabled",
            Description = "Resource encryption is not enabled",
            AffectedControls = new List<string> { "SC-13", "SC-28" },
            ComplianceFrameworks = new List<string> { "NIST-800-53" },
            Severity = AtoFindingSeverity.High,
            IsRemediable = true
        };

        // Act
        var result = service.ValidateAtoFinding(finding);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAtoFinding_WithNullFinding_ReturnsInvalidResult()
    {
        // Arrange
        var service = CreateComplianceValidationService();

        // Act
        var result = service.ValidateAtoFinding(null!);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("cannot be null"));
    }

    [Fact]
    public void ValidateAtoFinding_WithMissingId_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var finding = new AtoFinding
        {
            Id = "",
            ResourceId = "/subscriptions/sub1/resourceGroups/rg1",
            Title = "Test",
            Description = "Test description"
        };

        // Act
        var result = service.ValidateAtoFinding(finding);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Finding ID is required"));
    }

    [Fact]
    public void ValidateAtoFinding_WithMissingResourceId_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var finding = new AtoFinding
        {
            Id = "finding-1",
            ResourceId = "",
            Title = "Test",
            Description = "Test description"
        };

        // Act
        var result = service.ValidateAtoFinding(finding);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Resource ID is required"));
    }

    [Fact]
    public void ValidateAtoFinding_WithMissingTitle_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var finding = new AtoFinding
        {
            Id = "finding-1",
            ResourceId = "/subscriptions/sub1",
            Title = "",
            Description = "Test description"
        };

        // Act
        var result = service.ValidateAtoFinding(finding);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Finding title is required"));
    }

    [Fact]
    public void ValidateAtoFinding_WithMissingDescription_AddsError()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var finding = new AtoFinding
        {
            Id = "finding-1",
            ResourceId = "/subscriptions/sub1",
            Title = "Test",
            Description = ""
        };

        // Act
        var result = service.ValidateAtoFinding(finding);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Finding description is required"));
    }

    [Fact]
    public void ValidateAtoFinding_WithNoAffectedControls_AddsWarning()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var finding = new AtoFinding
        {
            Id = "finding-1",
            ResourceId = "/subscriptions/sub1",
            Title = "Test",
            Description = "Test description",
            AffectedControls = new List<string>()
        };

        // Act
        var result = service.ValidateAtoFinding(finding);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("should specify affected controls"));
    }

    [Fact]
    public void ValidateAtoFinding_WithNoComplianceFrameworks_AddsWarning()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var finding = new AtoFinding
        {
            Id = "finding-1",
            ResourceId = "/subscriptions/sub1",
            Title = "Test",
            Description = "Test description",
            ComplianceFrameworks = new List<string>()
        };

        // Act
        var result = service.ValidateAtoFinding(finding);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("should specify applicable compliance frameworks"));
    }

    [Fact]
    public void ValidateAtoFinding_WithCriticalNonRemediableFinding_AddsWarning()
    {
        // Arrange
        var service = CreateComplianceValidationService();
        var finding = new AtoFinding
        {
            Id = "finding-1",
            ResourceId = "/subscriptions/sub1",
            Title = "Critical issue",
            Description = "Cannot be fixed",
            Severity = AtoFindingSeverity.Critical,
            IsRemediable = false
        };

        // Act
        var result = service.ValidateAtoFinding(finding);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain(w => w.Contains("Critical findings should typically be remediable"));
    }

    #endregion

    #region NistControlsHealthCheck Tests

    [Fact]
    public async Task CheckHealthAsync_WithHealthyService_ReturnsHealthyStatus()
    {
        // Arrange
        var healthCheck = CreateNistControlsHealthCheck();

        _mockNistControlsService
            .Setup(x => x.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1");

        _mockNistControlsService
            .Setup(x => x.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(HealthStatus.Healthy);
        result.Description.Should().Contain("fully operational");
        result.Data.Should().ContainKey("version");
        result.Data.Should().ContainKey("valid_test_controls");
        result.Data.Should().ContainKey("response_time_ms");
    }

    [Fact]
    public async Task CheckHealthAsync_WithUnknownVersion_ReturnsDegraded()
    {
        // Arrange
        var healthCheck = CreateNistControlsHealthCheck();

        _mockNistControlsService
            .Setup(x => x.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("Unknown");

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("version information is unavailable");
    }

    [Fact]
    public async Task CheckHealthAsync_WithPartialControlValidation_ReturnsDegraded()
    {
        // Arrange
        var healthCheck = CreateNistControlsHealthCheck();

        _mockNistControlsService
            .Setup(x => x.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1");

        _mockNistControlsService
            .Setup(x => x.ValidateControlIdAsync("AC-3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _mockNistControlsService
            .Setup(x => x.ValidateControlIdAsync("SC-13", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mockNistControlsService
            .Setup(x => x.ValidateControlIdAsync("AU-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("partially operational");
        result.Data["valid_test_controls"].Should().Be("1/3");
    }

    [Fact]
    public async Task CheckHealthAsync_WithNoValidControls_ReturnsUnhealthy()
    {
        // Arrange
        var healthCheck = CreateNistControlsHealthCheck();

        _mockNistControlsService
            .Setup(x => x.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("5.1");

        _mockNistControlsService
            .Setup(x => x.ValidateControlIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("not operational");
    }

    [Fact]
    public async Task CheckHealthAsync_WithException_ReturnsUnhealthy()
    {
        // Arrange
        var healthCheck = CreateNistControlsHealthCheck();

        _mockNistControlsService
            .Setup(x => x.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service error"));

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("failed");
        result.Data.Should().ContainKey("error");
    }

    [Fact]
    public async Task CheckHealthAsync_WithCancellation_ReturnsDegraded()
    {
        // Arrange
        var healthCheck = CreateNistControlsHealthCheck();

        _mockNistControlsService
            .Setup(x => x.GetVersionAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("timed out");
    }

    #endregion

    #region ValidationResult Tests

    [Fact]
    public void ValidationResult_DefaultState_IsValid()
    {
        // Arrange & Act
        var result = new ValidationResult();

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Warnings.Should().BeEmpty();
        result.Info.Should().BeEmpty();
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public void ValidationResult_AddError_SetsIsValidToFalse()
    {
        // Arrange
        var result = new ValidationResult();

        // Act
        result.AddError("Test error");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Test error");
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void ValidationResult_AddWarning_DoesNotAffectIsValid()
    {
        // Arrange
        var result = new ValidationResult();

        // Act
        result.AddWarning("Test warning");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().Contain("Test warning");
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void ValidationResult_AddInfo_DoesNotAffectValidity()
    {
        // Arrange
        var result = new ValidationResult();

        // Act
        result.AddInfo("Test info");

        // Assert
        result.IsValid.Should().BeTrue();
        result.Info.Should().Contain("Test info");
        result.HasIssues.Should().BeFalse();
    }

    #endregion

    #region Helper Methods

    private ComplianceValidationService CreateComplianceValidationService()
    {
        return new ComplianceValidationService(
            _mockNistControlsService.Object,
            _mockValidationLogger.Object);
    }

    private NistControlsHealthCheck CreateNistControlsHealthCheck()
    {
        return new NistControlsHealthCheck(
            _mockNistControlsService.Object,
            _mockHealthCheckLogger.Object,
            Options.Create(_options));
    }

    private NistCatalog CreateTestCatalog()
    {
        return new NistCatalog
        {
            Metadata = new CatalogMetadata
            {
                Title = "NIST SP 800-53 Rev. 5",
                Version = "5.1",
                LastModified = DateTime.UtcNow,
                OscalVersion = "1.0.0"
            },
            Groups = new List<ControlGroup>
            {
                new ControlGroup
                {
                    Id = "ac",
                    Title = "Access Control",
                    Controls = new List<NistControl>
                    {
                        new NistControl
                        {
                            Id = "AC-3",
                            Title = "Access Enforcement"
                        }
                    }
                }
            }
        };
    }

    #endregion
}
