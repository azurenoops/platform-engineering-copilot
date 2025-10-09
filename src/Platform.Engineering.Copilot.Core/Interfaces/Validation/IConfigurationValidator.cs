using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Core.Interfaces.Validation;

/// <summary>
/// Interface for platform-specific configuration validators
/// </summary>
public interface IConfigurationValidator
{
    /// <summary>
    /// Platform name this validator handles (e.g., "AKS", "Lambda", "ECS")
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Validates a full template generation request
    /// </summary>
    /// <param name="request">Template generation request</param>
    /// <returns>Validation result with errors, warnings, and recommendations</returns>
    ValidationResult ValidateTemplate(TemplateGenerationRequest request);

    /// <summary>
    /// Quick validation to check if configuration is generally valid
    /// </summary>
    /// <param name="request">Template generation request</param>
    /// <returns>True if valid, false otherwise</returns>
    bool IsValid(TemplateGenerationRequest request);
}
