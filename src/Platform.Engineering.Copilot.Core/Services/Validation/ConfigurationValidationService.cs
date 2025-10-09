using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Core.Services.Validation;

/// <summary>
/// Central service for validating template generation configurations
/// Routes validation to platform-specific validators
/// </summary>
public class ConfigurationValidationService
{
    private readonly ILogger<ConfigurationValidationService> _logger;
    private readonly Dictionary<string, IConfigurationValidator> _validators;

    public ConfigurationValidationService(
        ILogger<ConfigurationValidationService> logger,
        IEnumerable<IConfigurationValidator> validators)
    {
        _logger = logger;
        _validators = validators.ToDictionary(v => v.PlatformName, v => v, StringComparer.OrdinalIgnoreCase);
        
        _logger.LogInformation("Initialized ConfigurationValidationService with {Count} validators: {Validators}",
            _validators.Count,
            string.Join(", ", _validators.Keys));
    }

    /// <summary>
    /// Validates a complete template generation request
    /// </summary>
    public ValidationResult ValidateRequest(TemplateGenerationRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Validating template request for service: {ServiceName}, Platform: {Platform}",
                request.ServiceName,
                request.Infrastructure?.ComputePlatform);

            // Determine platform from request
            var platformName = GetPlatformName(request);
            
            if (string.IsNullOrEmpty(platformName))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Platform = "Unknown",
                    ValidationTimeMs = stopwatch.ElapsedMilliseconds,
                    Errors = new List<ValidationError>
                    {
                        new ValidationError
                        {
                            Field = "Infrastructure.ComputePlatform",
                            Message = "Compute platform not specified or invalid",
                            Code = "PLATFORM_NOT_SPECIFIED"
                        }
                    }
                };
            }

            // Get platform-specific validator
            if (!_validators.TryGetValue(platformName, out var validator))
            {
                _logger.LogWarning("No validator found for platform: {Platform}. Available validators: {Validators}",
                    platformName,
                    string.Join(", ", _validators.Keys));
                
                return new ValidationResult
                {
                    IsValid = true, // Allow unknown platforms (they'll use fallback generators)
                    Platform = platformName,
                    ValidationTimeMs = stopwatch.ElapsedMilliseconds,
                    Warnings = new List<ValidationWarning>
                    {
                        new ValidationWarning
                        {
                            Field = "Infrastructure.ComputePlatform",
                            Message = $"No validator available for platform '{platformName}'. Basic validation only.",
                            Code = "NO_VALIDATOR_AVAILABLE",
                            Severity = WarningSeverity.Low
                        }
                    }
                };
            }

            // Run platform-specific validation
            var result = validator.ValidateTemplate(request);
            result.Platform = platformName;
            result.ValidationTimeMs = stopwatch.ElapsedMilliseconds;

            _logger.LogInformation("Validation complete for {Platform}. Valid: {IsValid}, Errors: {ErrorCount}, Warnings: {WarningCount}, Recommendations: {RecommendationCount}",
                platformName, result.IsValid, result.Errors.Count, result.Warnings.Count, result.Recommendations.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Validation failed with exception for service: {ServiceName}", request.ServiceName);
            
            return new ValidationResult
            {
                IsValid = false,
                Platform = GetPlatformName(request) ?? "Unknown",
                ValidationTimeMs = stopwatch.ElapsedMilliseconds,
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Field = "General",
                        Message = $"Validation failed: {ex.Message}",
                        Code = "VALIDATION_EXCEPTION"
                    }
                }
            };
        }
    }

    /// <summary>
    /// Quick validation check without detailed results
    /// </summary>
    public bool IsValid(TemplateGenerationRequest request)
    {
        var result = ValidateRequest(request);
        return result.IsValid;
    }

    /// <summary>
    /// Get list of available platform validators
    /// </summary>
    public IEnumerable<string> GetSupportedPlatforms()
    {
        return _validators.Keys;
    }

    /// <summary>
    /// Determines platform name from request
    /// </summary>
    private string? GetPlatformName(TemplateGenerationRequest request)
    {
        if (request.Infrastructure == null)
        {
            return null;
        }

        // Map ComputePlatform enum to validator platform names
        return request.Infrastructure.ComputePlatform switch
        {
            ComputePlatform.Kubernetes => GetKubernetesPlatform(request.Infrastructure.Provider),
            ComputePlatform.AKS => "AKS",
            ComputePlatform.EKS => "EKS",
            ComputePlatform.GKE => "GKE",
            ComputePlatform.ContainerApps => "ContainerApps",
            ComputePlatform.ECS => "ECS",
            ComputePlatform.AppService => "AppService",
            ComputePlatform.Lambda => "Lambda",
            ComputePlatform.CloudRun => "CloudRun",
            ComputePlatform.VirtualMachine => GetVMPlatform(request.Infrastructure.Provider),
            _ => null
        };
    }

    /// <summary>
    /// Determine specific Kubernetes platform from cloud provider
    /// </summary>
    private string GetKubernetesPlatform(CloudProvider provider)
    {
        return provider switch
        {
            CloudProvider.Azure => "AKS",
            CloudProvider.AWS => "EKS",
            CloudProvider.GCP => "GKE",
            _ => "AKS" // Default to AKS
        };
    }

    /// <summary>
    /// Determine VM platform from cloud provider
    /// </summary>
    private string GetVMPlatform(CloudProvider provider)
    {
        return provider switch
        {
            CloudProvider.Azure => "AzureVM",
            CloudProvider.AWS => "EC2",
            CloudProvider.GCP => "ComputeEngine",
            _ => "AzureVM" // Default to Azure
        };
    }
}
