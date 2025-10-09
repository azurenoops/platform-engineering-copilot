using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Services.Validation;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Tests.Integration.Core.Services.Validation;

public class ConfigurationValidationServiceIntegrationTests
{
    private readonly Mock<ILogger<ConfigurationValidationService>> _mockLogger;
    private readonly Mock<IConfigurationValidator> _mockAksValidator;
    private readonly Mock<IConfigurationValidator> _mockLambdaValidator;
    private readonly ConfigurationValidationService _service;

    public ConfigurationValidationServiceIntegrationTests()
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

    [Fact]
    public void ValidateRequest_CompleteWorkflow_WithMultipleValidators()
    {
        // Arrange
        var aksRequest = new TemplateGenerationRequest
        {
            ServiceName = "aks-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.AKS,
                Provider = CloudProvider.Azure
            }
        };

        var lambdaRequest = new TemplateGenerationRequest
        {
            ServiceName = "lambda-service",
            Infrastructure = new InfrastructureSpec
            {
                ComputePlatform = ComputePlatform.Lambda,
                Provider = CloudProvider.AWS
            }
        };

        _mockAksValidator
            .Setup(v => v.ValidateTemplate(aksRequest))
            .Returns(new ValidationResult 
            { 
                IsValid = true,
                Warnings = new List<ValidationWarning>
                {
                    new ValidationWarning { Code = "AKS_WARNING" }
                }
            });

        _mockLambdaValidator
            .Setup(v => v.ValidateTemplate(lambdaRequest))
            .Returns(new ValidationResult 
            { 
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new ValidationError { Code = "LAMBDA_ERROR" }
                }
            });

        // Act
        var aksResult = _service.ValidateRequest(aksRequest);
        var lambdaResult = _service.ValidateRequest(lambdaRequest);

        // Assert
        aksResult.IsValid.Should().BeTrue();
        aksResult.Platform.Should().Be("AKS");
        aksResult.Warnings.Should().HaveCount(1);

        lambdaResult.IsValid.Should().BeFalse();
        lambdaResult.Platform.Should().Be("Lambda");
        lambdaResult.Errors.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_InitializesValidatorsDictionary()
    {
        // Act
        var platforms = _service.GetSupportedPlatforms().ToList();

        // Assert
        platforms.Should().NotBeEmpty();
        platforms.Should().HaveCount(2);
    }

    [Fact]
    public void ValidateRequest_LogsValidationResults()
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
                IsValid = true,
                Errors = new List<ValidationError>(),
                Warnings = new List<ValidationWarning>(),
                Recommendations = new List<ValidationRecommendation>()
            });

        // Act
        _service.ValidateRequest(request);

        // Assert - Verify logging occurred
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Validating template request")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }
}
