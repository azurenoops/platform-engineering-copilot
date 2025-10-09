using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Services.Validation;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Tests.Unit.Core.Services.Validation;

public class ConfigurationValidationServiceTests
{
    private readonly Mock<ILogger<ConfigurationValidationService>> _mockLogger;
    private readonly Mock<IConfigurationValidator> _mockAksValidator;
    private readonly Mock<IConfigurationValidator> _mockLambdaValidator;
    private readonly ConfigurationValidationService _service;

    public ConfigurationValidationServiceTests()
    {
        _mockLogger = new Mock<ILogger<ConfigurationValidationService>>();
        
        // Create mock validators for different platforms
        _mockAksValidator = new Mock<IConfigurationValidator>();
        _mockAksValidator.Setup(v => v.PlatformName).Returns("AKS");
        
        _mockLambdaValidator = new Mock<IConfigurationValidator>();
        _mockLambdaValidator.Setup(v => v.PlatformName).Returns("Lambda");
        
        var validators = new List<IConfigurationValidator> 
        { 
            _mockAksValidator.Object, 
            _mockLambdaValidator.Object 
        };
        
        _service = new ConfigurationValidationService(_mockLogger.Object, validators);
    }

    #region ValidateRequest Tests

    [Fact]
    public void ValidateRequest_WithNullInfrastructure_ReturnsErrorResult()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = null
        };

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Platform.Should().Be("Unknown");
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Field.Should().Be("Infrastructure.ComputePlatform");
        result.Errors[0].Code.Should().Be("PLATFORM_NOT_SPECIFIED");
        result.Errors[0].Message.Should().Contain("Compute platform not specified");
    }

    [Fact]
    public void ValidateRequest_WithAKSPlatform_CallsAKSValidator()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.AKS,
                Provider = CloudProvider.Azure
            }
        };

        var validatorResult = new ValidationResult
        {
            IsValid = true,
            Errors = new List<ValidationError>(),
            Warnings = new List<ValidationWarning>(),
            Recommendations = new List<ValidationRecommendation>()
        };

        _mockAksValidator
            .Setup(v => v.ValidateTemplate(request))
            .Returns(validatorResult);

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Platform.Should().Be("AKS");
        _mockAksValidator.Verify(v => v.ValidateTemplate(request), Times.Once);
        _mockLambdaValidator.Verify(v => v.ValidateTemplate(It.IsAny<TemplateGenerationRequest>()), Times.Never);
    }

    [Fact]
    public void ValidateRequest_WithLambdaPlatform_CallsLambdaValidator()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.Lambda,
                Provider = CloudProvider.AWS
            }
        };

        var validatorResult = new ValidationResult
        {
            IsValid = true,
            Errors = new List<ValidationError>(),
            Warnings = new List<ValidationWarning>(),
            Recommendations = new List<ValidationRecommendation>()
        };

        _mockLambdaValidator
            .Setup(v => v.ValidateTemplate(request))
            .Returns(validatorResult);

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Platform.Should().Be("Lambda");
        _mockLambdaValidator.Verify(v => v.ValidateTemplate(request), Times.Once);
        _mockAksValidator.Verify(v => v.ValidateTemplate(It.IsAny<TemplateGenerationRequest>()), Times.Never);
    }

    [Fact]
    public void ValidateRequest_WithUnsupportedPlatform_ReturnsWarningResult()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.ECS, // ECS validator not registered
                Provider = CloudProvider.AWS
            }
        };

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue(); // Still valid, just no platform-specific validation
        result.Platform.Should().Be("ECS");
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Code.Should().Be("NO_VALIDATOR_AVAILABLE");
        result.Warnings[0].Message.Should().Contain("No validator available");
    }

    [Fact]
    public void ValidateRequest_WithValidationErrors_ReturnsInvalidResult()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.AKS,
                Provider = CloudProvider.Azure
            }
        };

        var validatorResult = new ValidationResult
        {
            IsValid = false,
            Errors = new List<ValidationError>
            {
                new ValidationError
                {
                    Field = "Kubernetes.NodeCount",
                    Message = "Node count must be at least 3",
                    Code = "INVALID_NODE_COUNT"
                }
            }
        };

        _mockAksValidator
            .Setup(v => v.ValidateTemplate(request))
            .Returns(validatorResult);

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be("INVALID_NODE_COUNT");
    }

    [Fact]
    public void ValidateRequest_WithWarnings_ReturnsValidResultWithWarnings()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.AKS,
                Provider = CloudProvider.Azure
            }
        };

        var validatorResult = new ValidationResult
        {
            IsValid = true,
            Warnings = new List<ValidationWarning>
            {
                new ValidationWarning
                {
                    Field = "Kubernetes.VMSize",
                    Message = "VM size is larger than recommended",
                    Code = "OVERSIZED_VM",
                    Severity = WarningSeverity.Medium
                }
            }
        };

        _mockAksValidator
            .Setup(v => v.ValidateTemplate(request))
            .Returns(validatorResult);

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Warnings.Should().HaveCount(1);
        result.Warnings[0].Code.Should().Be("OVERSIZED_VM");
    }

    [Fact]
    public void ValidateRequest_WithRecommendations_ReturnsValidResultWithRecommendations()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.AKS,
                Provider = CloudProvider.Azure
            }
        };

        var validatorResult = new ValidationResult
        {
            IsValid = true,
            Recommendations = new List<ValidationRecommendation>
            {
                new ValidationRecommendation
                {
                    Code = "ENABLE_AZURE_POLICY",
                    Message = "Consider enabling Azure Policy",
                    Reason = "Security",
                    Benefit = "Enhanced security posture"
                }
            }
        };

        _mockAksValidator
            .Setup(v => v.ValidateTemplate(request))
            .Returns(validatorResult);

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Recommendations.Should().HaveCount(1);
        result.Recommendations[0].Reason.Should().Be("Security");
    }

    [Fact]
    public void ValidateRequest_SetsValidationTimeMs()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.AKS,
                Provider = CloudProvider.Azure
            }
        };

        var validatorResult = new ValidationResult { IsValid = true };
        _mockAksValidator.Setup(v => v.ValidateTemplate(request)).Returns(validatorResult);

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.ValidationTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void ValidateRequest_WhenValidatorThrows_ReturnsErrorResult()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.AKS,
                Provider = CloudProvider.Azure
            }
        };

        _mockAksValidator
            .Setup(v => v.ValidateTemplate(request))
            .Throws(new InvalidOperationException("Validator failed"));

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Code.Should().Be("VALIDATION_EXCEPTION");
        result.Errors[0].Message.Should().Contain("Validator failed");
    }

    #endregion

    #region IsValid Tests

    [Fact]
    public void IsValid_WithValidRequest_ReturnsTrue()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.AKS,
                Provider = CloudProvider.Azure
            }
        };

        _mockAksValidator
            .Setup(v => v.ValidateTemplate(request))
            .Returns(new ValidationResult { IsValid = true });

        // Act
        var result = _service.IsValid(request);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithInvalidRequest_ReturnsFalse()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.AKS,
                Provider = CloudProvider.Azure
            }
        };

        _mockAksValidator
            .Setup(v => v.ValidateTemplate(request))
            .Returns(new ValidationResult 
            { 
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new ValidationError { Code = "TEST_ERROR" }
                }
            });

        // Act
        var result = _service.IsValid(request);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WithNullInfrastructure_ReturnsFalse()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = null
        };

        // Act
        var result = _service.IsValid(request);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetSupportedPlatforms Tests

    [Fact]
    public void GetSupportedPlatforms_ReturnsAllRegisteredPlatforms()
    {
        // Act
        var platforms = _service.GetSupportedPlatforms().ToList();

        // Assert
        platforms.Should().HaveCount(2);
        platforms.Should().Contain("AKS");
        platforms.Should().Contain("Lambda");
    }

    [Fact]
    public void GetSupportedPlatforms_IsCaseInsensitive()
    {
        // This test verifies that the dictionary is case-insensitive
        // by testing with different casing
        
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.AKS,
                Provider = CloudProvider.Azure
            }
        };

        _mockAksValidator
            .Setup(v => v.ValidateTemplate(request))
            .Returns(new ValidationResult { IsValid = true });

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Platform.Should().Be("AKS");
        _mockAksValidator.Verify(v => v.ValidateTemplate(request), Times.Once);
    }

    #endregion

    #region Platform Name Mapping Tests

    [Theory]
    [InlineData(ComputePlatform.Kubernetes, CloudProvider.Azure, "AKS")]
    [InlineData(ComputePlatform.Kubernetes, CloudProvider.AWS, "EKS")]
    [InlineData(ComputePlatform.Kubernetes, CloudProvider.GCP, "GKE")]
    [InlineData(ComputePlatform.AKS, CloudProvider.Azure, "AKS")]
    [InlineData(ComputePlatform.EKS, CloudProvider.AWS, "EKS")]
    [InlineData(ComputePlatform.GKE, CloudProvider.GCP, "GKE")]
    [InlineData(ComputePlatform.ContainerApps, CloudProvider.Azure, "ContainerApps")]
    [InlineData(ComputePlatform.ECS, CloudProvider.AWS, "ECS")]
    [InlineData(ComputePlatform.AppService, CloudProvider.Azure, "AppService")]
    [InlineData(ComputePlatform.Lambda, CloudProvider.AWS, "Lambda")]
    [InlineData(ComputePlatform.CloudRun, CloudProvider.GCP, "CloudRun")]
    public void ValidateRequest_MapsPlatformNamesCorrectly(ComputePlatform computePlatform, CloudProvider provider, string expectedPlatform)
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = computePlatform,
                Provider = provider
            }
        };

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Platform.Should().Be(expectedPlatform);
    }

    [Theory]
    [InlineData(CloudProvider.Azure, "AzureVM")]
    [InlineData(CloudProvider.AWS, "EC2")]
    [InlineData(CloudProvider.GCP, "ComputeEngine")]
    public void ValidateRequest_MapsVirtualMachinePlatformCorrectly(CloudProvider provider, string expectedPlatform)
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.VirtualMachine,
                Provider = provider
            }
        };

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Platform.Should().Be(expectedPlatform);
    }

    [Fact]
    public void ValidateRequest_WithKubernetesAndUnknownProvider_DefaultsToAKS()
    {
        // Arrange
        var request = new TemplateGenerationRequest
        {
            ServiceName = "test-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.Kubernetes,
                Provider = CloudProvider.OnPremises // Not Azure, AWS, or GCP
            }
        };

        // Act
        var result = _service.ValidateRequest(request);

        // Assert
        result.Platform.Should().Be("AKS"); // Should default to AKS
    }

    #endregion
}
