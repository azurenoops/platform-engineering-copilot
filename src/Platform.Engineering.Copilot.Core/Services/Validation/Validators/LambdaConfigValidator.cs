using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Core.Services.Validation.Validators;

/// <summary>
/// Validates configurations for AWS Lambda
/// </summary>
public class LambdaConfigValidator : IConfigurationValidator
{
    private readonly ILogger<LambdaConfigValidator> _logger;

    // Lambda Limits (as of October 2025)
    private const int MIN_MEMORY_MB = 128;
    private const int MAX_MEMORY_MB = 10240; // 10 GB
    private const int MEMORY_INCREMENT_MB = 64;
    private const int MIN_TIMEOUT_SECONDS = 1;
    private const int MAX_TIMEOUT_SECONDS = 900; // 15 minutes
    private const int MAX_ENV_VARS_SIZE_KB = 4;
    private const int MAX_DEPLOYMENT_PACKAGE_SIZE_MB = 50;
    private const int MAX_UNZIPPED_PACKAGE_SIZE_MB = 250;
    private const int MAX_CONCURRENT_EXECUTIONS = 1000; // Default account limit
    private const int RECOMMENDED_MIN_MEMORY_MB = 512;
    private const int COST_OPTIMIZATION_MEMORY_MB = 1769; // Price/performance sweet spot

    // Supported runtimes (update periodically)
    private static readonly HashSet<string> SUPPORTED_RUNTIMES = new()
    {
        // Python
        "python3.9", "python3.10", "python3.11", "python3.12",
        // Node.js
        "nodejs16.x", "nodejs18.x", "nodejs20.x",
        // Java
        "java11", "java17", "java21",
        // .NET
        "dotnet6", "dotnet8",
        // Ruby
        "ruby3.2", "ruby3.3",
        // Go
        "provided.al2", "provided.al2023",
        // Custom runtime
        "provided"
    };

    public string PlatformName => "Lambda";

    public LambdaConfigValidator(ILogger<LambdaConfigValidator> logger)
    {
        _logger = logger;
    }

    public ValidationResult ValidateTemplate(TemplateGenerationRequest request)
    {
        var result = new ValidationResult
        {
            IsValid = true,
            Platform = PlatformName
        };

        var deployment = request.Deployment;
        var infrastructure = request.Infrastructure;
        var application = request.Application;

        // Validate Lambda-specific configuration
        ValidateMemory(deployment, result);
        ValidateConcurrency(deployment, result);
        ValidateRuntime(infrastructure, result);
        ValidateEnvironmentVariables(application, result);
        
        // Add cost optimization recommendations
        AddCostOptimizationRecommendations(deployment, result);

        return result;
    }

    public bool IsValid(TemplateGenerationRequest request)
    {
        var result = ValidateTemplate(request);
        return result.IsValid;
    }

    /// <summary>
    /// Validates Lambda memory configuration
    /// </summary>
    private void ValidateMemory(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment?.Resources == null) return;

        var memoryRequest = deployment.Resources.MemoryRequest;
        
        // Parse memory from Kubernetes format to MB
        var memoryMb = ParseMemoryToMb(memoryRequest);
        
        if (memoryMb == 0)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = "Invalid memory format for Lambda. Use Mi or Gi suffix",
                Code = "LAMBDA_INVALID_MEMORY_FORMAT",
                CurrentValue = memoryRequest,
                ExpectedValue = "Examples: '512Mi', '1Gi', '2Gi'",
                DocumentationUrl = "https://docs.aws.amazon.com/lambda/latest/dg/configuration-memory.html"
            });
            result.IsValid = false;
            return;
        }

        // Validate memory range
        if (memoryMb < MIN_MEMORY_MB)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = $"Lambda memory must be at least {MIN_MEMORY_MB} MB",
                Code = "LAMBDA_MEMORY_TOO_LOW",
                CurrentValue = $"{memoryMb} MB",
                ExpectedValue = $">= {MIN_MEMORY_MB} MB",
                DocumentationUrl = "https://docs.aws.amazon.com/lambda/latest/dg/configuration-memory.html"
            });
            result.IsValid = false;
        }

        if (memoryMb > MAX_MEMORY_MB)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = $"Lambda memory cannot exceed {MAX_MEMORY_MB} MB (10 GB)",
                Code = "LAMBDA_MEMORY_TOO_HIGH",
                CurrentValue = $"{memoryMb} MB",
                ExpectedValue = $"<= {MAX_MEMORY_MB} MB",
                DocumentationUrl = "https://docs.aws.amazon.com/lambda/latest/dg/configuration-memory.html"
            });
            result.IsValid = false;
        }

        // Validate memory increment (must be multiple of 64 MB)
        if (memoryMb % MEMORY_INCREMENT_MB != 0)
        {
            var roundedUp = (int)Math.Ceiling((double)memoryMb / MEMORY_INCREMENT_MB) * MEMORY_INCREMENT_MB;
            var roundedDown = (int)Math.Floor((double)memoryMb / MEMORY_INCREMENT_MB) * MEMORY_INCREMENT_MB;
            
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = $"Lambda memory must be in {MEMORY_INCREMENT_MB} MB increments",
                Code = "LAMBDA_INVALID_MEMORY_INCREMENT",
                CurrentValue = $"{memoryMb} MB",
                ExpectedValue = $"Nearest valid: {roundedDown} MB or {roundedUp} MB",
                DocumentationUrl = "https://docs.aws.amazon.com/lambda/latest/dg/configuration-memory.html"
            });
            result.IsValid = false;
        }

        // Warning for very low memory
        if (memoryMb < RECOMMENDED_MIN_MEMORY_MB && result.IsValid)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = $"Memory allocation below {RECOMMENDED_MIN_MEMORY_MB} MB may cause performance issues",
                Code = "LAMBDA_LOW_MEMORY",
                Severity = WarningSeverity.Medium,
                Impact = "May result in slow cold starts and timeouts"
            });
        }

        // Warning for very high memory
        if (memoryMb > 3072 && result.IsValid)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = $"High memory allocation ({memoryMb} MB) will increase costs significantly",
                Code = "LAMBDA_HIGH_MEMORY_COST",
                Severity = WarningSeverity.Medium,
                Impact = $"Estimated cost increase: {CalculateCostMultiplier(memoryMb):P0} vs baseline (1024 MB)"
            });
        }
    }

    /// <summary>
    /// Validates Lambda concurrency configuration
    /// </summary>
    private void ValidateConcurrency(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment == null) return;

        // Note: In TemplateGenerationRequest, concurrency might be represented by MaxReplicas
        var concurrency = deployment.MaxReplicas;

        if (concurrency > MAX_CONCURRENT_EXECUTIONS)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.MaxReplicas",
                Message = $"Requested concurrency ({concurrency}) exceeds default account limit ({MAX_CONCURRENT_EXECUTIONS})",
                Code = "LAMBDA_HIGH_CONCURRENCY",
                Severity = WarningSeverity.High,
                Impact = "May require AWS support request to increase account limits"
            });
        }

        // Recommendation for reserved concurrency
        if (concurrency < 10 && result.IsValid)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.MaxReplicas",
                Message = "Consider using reserved concurrency to guarantee availability",
                Code = "LAMBDA_RESERVED_CONCURRENCY",
                CurrentValue = $"{concurrency}",
                RecommendedValue = "Set explicit reserved concurrency",
                Reason = "Prevents throttling and ensures predictable performance",
                Benefit = "Improves reliability for critical functions"
            });
        }
    }

    /// <summary>
    /// Validates Lambda runtime
    /// </summary>
    private void ValidateRuntime(InfrastructureSpec? infrastructure, ValidationResult result)
    {
        // Note: Runtime information would typically come from the request
        // For now, add a recommendation to use supported runtimes
        
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Runtime",
            Message = "Ensure you're using a supported Lambda runtime",
            Code = "LAMBDA_RUNTIME_VERSION",
            RecommendedValue = "Latest stable runtime version for your language",
            Reason = "Older runtimes may be deprecated or lack security updates",
            Benefit = "Access to latest features, performance improvements, and security patches"
        });
    }

    /// <summary>
    /// Validates environment variables
    /// </summary>
    private void ValidateEnvironmentVariables(ApplicationSpec? application, ValidationResult result)
    {
        if (application?.EnvironmentVariables == null) return;

        // Calculate total size of environment variables
        var totalSize = application.EnvironmentVariables
            .Sum(kv => kv.Key.Length + (kv.Value?.Length ?? 0));

        var totalSizeKb = totalSize / 1024.0;

        if (totalSizeKb > MAX_ENV_VARS_SIZE_KB)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Application.EnvironmentVariables",
                Message = $"Total size of environment variables ({totalSizeKb:F2} KB) exceeds Lambda limit ({MAX_ENV_VARS_SIZE_KB} KB)",
                Code = "LAMBDA_ENV_VARS_TOO_LARGE",
                CurrentValue = $"{totalSizeKb:F2} KB",
                ExpectedValue = $"<= {MAX_ENV_VARS_SIZE_KB} KB",
                DocumentationUrl = "https://docs.aws.amazon.com/lambda/latest/dg/configuration-envvars.html"
            });
            result.IsValid = false;
        }

        // Warning for large number of environment variables
        if (application.EnvironmentVariables.Count > 50)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Application.EnvironmentVariables",
                Message = $"Large number of environment variables ({application.EnvironmentVariables.Count}) may impact cold start time",
                Code = "LAMBDA_MANY_ENV_VARS",
                Severity = WarningSeverity.Low,
                Impact = "Consider using AWS Systems Manager Parameter Store or Secrets Manager"
            });
        }
    }

    /// <summary>
    /// Adds cost optimization recommendations
    /// </summary>
    private void AddCostOptimizationRecommendations(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment?.Resources == null) return;

        var memoryMb = ParseMemoryToMb(deployment.Resources.MemoryRequest);
        
        if (memoryMb == 0) return;

        // Recommend optimal memory allocation
        if (memoryMb < COST_OPTIMIZATION_MEMORY_MB)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = "Consider using Lambda Power Tuner to find optimal memory/cost balance",
                Code = "LAMBDA_POWER_TUNING",
                CurrentValue = $"{memoryMb} MB",
                RecommendedValue = $"~{COST_OPTIMIZATION_MEMORY_MB} MB (typical price/performance sweet spot)",
                Reason = "1769 MB allocation provides 1 full vCPU at optimal price point",
                Benefit = "Up to 50% cost reduction for CPU-intensive workloads"
            });
        }

        // Recommend ARM/Graviton2 for cost savings
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Architecture",
            Message = "Consider using ARM/Graviton2 architecture for 20% cost savings",
            Code = "LAMBDA_ARM_ARCHITECTURE",
            RecommendedValue = "arm64",
            Reason = "ARM-based functions are more cost-effective with similar performance",
            Benefit = "20% lower costs with same or better performance for most workloads"
        });
    }

    // Helper methods
    private int ParseMemoryToMb(string memory)
    {
        if (string.IsNullOrWhiteSpace(memory)) return 0;
        
        if (memory.EndsWith("Gi"))
        {
            return int.TryParse(memory[..^2], out var gi) ? gi * 1024 : 0;
        }
        
        if (memory.EndsWith("Mi"))
        {
            return int.TryParse(memory[..^2], out var mi) ? mi : 0;
        }
        
        return 0;
    }

    private double CalculateCostMultiplier(int memoryMb)
    {
        // Lambda pricing is linear with memory
        const int baselineMemory = 1024;
        return (double)memoryMb / baselineMemory - 1.0;
    }
}
