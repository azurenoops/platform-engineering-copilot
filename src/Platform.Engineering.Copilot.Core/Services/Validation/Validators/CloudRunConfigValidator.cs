using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Core.Services.Validation.Validators;

/// <summary>
/// Validates configurations for Google Cloud Run
/// </summary>
public class CloudRunConfigValidator : IConfigurationValidator
{
    private readonly ILogger<CloudRunConfigValidator> _logger;

    // Cloud Run Limits (as of October 2025)
    private const int MIN_CPU_MILLICORES = 1000;  // 1 vCPU minimum
    private const int MAX_CPU_MILLICORES = 8000;  // 8 vCPU maximum
    private const int MIN_MEMORY_MB = 128;
    private const int MAX_MEMORY_MB = 32768;  // 32 GB
    private const int MIN_CONCURRENCY = 1;
    private const int MAX_CONCURRENCY = 1000;
    private const int MAX_TIMEOUT_SECONDS = 3600;  // 1 hour
    private const int MAX_INSTANCES = 1000;

    // Memory increments (MB)
    private const int MEMORY_INCREMENT_MB = 128;

    public string PlatformName => "CloudRun";

    public CloudRunConfigValidator(ILogger<CloudRunConfigValidator> logger)
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

        // Validate Cloud Run-specific configuration
        ValidateCpu(deployment, result);
        ValidateMemory(deployment, result);
        ValidateConcurrency(deployment, result);
        ValidateInstances(deployment, result);
        
        // Add optimization recommendations
        AddOptimizationRecommendations(deployment, result);

        return result;
    }

    public bool IsValid(TemplateGenerationRequest request)
    {
        var result = ValidateTemplate(request);
        return result.IsValid;
    }

    /// <summary>
    /// Validates CPU configuration
    /// </summary>
    private void ValidateCpu(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment?.Resources == null) return;

        var cpuMillicores = ParseCpuToMillicores(deployment.Resources.CpuRequest);

        if (cpuMillicores == 0)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.CpuRequest",
                Message = "Invalid CPU format for Cloud Run",
                Code = "CLOUDRUN_INVALID_CPU_FORMAT",
                CurrentValue = deployment.Resources.CpuRequest,
                ExpectedValue = "Examples: '1000m' (1 vCPU), '2000m' (2 vCPU)",
                DocumentationUrl = "https://cloud.google.com/run/docs/configuring/cpu"
            });
            result.IsValid = false;
            return;
        }

        // Validate CPU range
        if (cpuMillicores < MIN_CPU_MILLICORES)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.CpuRequest",
                Message = $"Cloud Run minimum CPU is {MIN_CPU_MILLICORES}m (1 vCPU)",
                Code = "CLOUDRUN_CPU_TOO_LOW",
                CurrentValue = $"{cpuMillicores}m",
                ExpectedValue = $">= {MIN_CPU_MILLICORES}m",
                DocumentationUrl = "https://cloud.google.com/run/docs/configuring/cpu"
            });
            result.IsValid = false;
        }

        if (cpuMillicores > MAX_CPU_MILLICORES)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.CpuRequest",
                Message = $"Cloud Run maximum CPU is {MAX_CPU_MILLICORES}m (8 vCPU)",
                Code = "CLOUDRUN_CPU_TOO_HIGH",
                CurrentValue = $"{cpuMillicores}m",
                ExpectedValue = $"<= {MAX_CPU_MILLICORES}m",
                DocumentationUrl = "https://cloud.google.com/run/docs/configuring/cpu"
            });
            result.IsValid = false;
        }

        // Validate CPU is in full vCPU increments
        if (cpuMillicores % 1000 != 0)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.CpuRequest",
                Message = "Cloud Run CPU must be in full vCPU increments (1000m, 2000m, etc.)",
                Code = "CLOUDRUN_INVALID_CPU_INCREMENT",
                CurrentValue = $"{cpuMillicores}m",
                ExpectedValue = "1000m, 2000m, 3000m, 4000m, 5000m, 6000m, 7000m, or 8000m",
                DocumentationUrl = "https://cloud.google.com/run/docs/configuring/cpu"
            });
            result.IsValid = false;
        }
    }

    /// <summary>
    /// Validates memory configuration
    /// </summary>
    private void ValidateMemory(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment?.Resources == null) return;

        var memoryMb = ParseMemoryToMb(deployment.Resources.MemoryRequest);

        if (memoryMb == 0)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = "Invalid memory format for Cloud Run",
                Code = "CLOUDRUN_INVALID_MEMORY_FORMAT",
                CurrentValue = deployment.Resources.MemoryRequest,
                ExpectedValue = "Examples: '512Mi', '1Gi', '2Gi'",
                DocumentationUrl = "https://cloud.google.com/run/docs/configuring/memory-limits"
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
                Message = $"Cloud Run minimum memory is {MIN_MEMORY_MB} MB",
                Code = "CLOUDRUN_MEMORY_TOO_LOW",
                CurrentValue = $"{memoryMb} MB",
                ExpectedValue = $">= {MIN_MEMORY_MB} MB",
                DocumentationUrl = "https://cloud.google.com/run/docs/configuring/memory-limits"
            });
            result.IsValid = false;
        }

        if (memoryMb > MAX_MEMORY_MB)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = $"Cloud Run maximum memory is {MAX_MEMORY_MB} MB (32 GB)",
                Code = "CLOUDRUN_MEMORY_TOO_HIGH",
                CurrentValue = $"{memoryMb} MB",
                ExpectedValue = $"<= {MAX_MEMORY_MB} MB",
                DocumentationUrl = "https://cloud.google.com/run/docs/configuring/memory-limits"
            });
            result.IsValid = false;
        }

        // Validate memory increment
        if (memoryMb % MEMORY_INCREMENT_MB != 0)
        {
            var roundedUp = (int)Math.Ceiling((double)memoryMb / MEMORY_INCREMENT_MB) * MEMORY_INCREMENT_MB;
            var roundedDown = (int)Math.Floor((double)memoryMb / MEMORY_INCREMENT_MB) * MEMORY_INCREMENT_MB;

            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = $"Cloud Run memory must be in {MEMORY_INCREMENT_MB} MB increments",
                Code = "CLOUDRUN_INVALID_MEMORY_INCREMENT",
                CurrentValue = $"{memoryMb} MB",
                ExpectedValue = $"Nearest valid: {roundedDown} MB or {roundedUp} MB",
                DocumentationUrl = "https://cloud.google.com/run/docs/configuring/memory-limits"
            });
            result.IsValid = false;
        }

        // Validate CPU-to-memory ratio
        ValidateCpuMemoryRatio(deployment, memoryMb, result);

        // Warning for high memory allocation
        if (memoryMb > 16384)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = $"High memory allocation ({memoryMb} MB) will increase costs significantly",
                Code = "CLOUDRUN_HIGH_MEMORY_COST",
                Severity = WarningSeverity.Medium,
                Impact = "Consider right-sizing based on actual memory usage"
            });
        }
    }

    /// <summary>
    /// Validates CPU-to-memory ratio
    /// </summary>
    private void ValidateCpuMemoryRatio(DeploymentSpec deployment, int memoryMb, ValidationResult result)
    {
        var cpuMillicores = ParseCpuToMillicores(deployment.Resources.CpuRequest);
        if (cpuMillicores == 0) return;

        var cpuCount = cpuMillicores / 1000.0;
        var memoryGb = memoryMb / 1024.0;

        // Cloud Run recommends 0.5 GB to 8 GB per vCPU
        var minMemoryGb = cpuCount * 0.5;
        var maxMemoryGb = cpuCount * 8.0;

        if (memoryGb < minMemoryGb)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.Resources",
                Message = $"Memory ({memoryGb:F1} GB) may be too low for {cpuCount} vCPU(s)",
                Code = "CLOUDRUN_LOW_MEMORY_FOR_CPU",
                Severity = WarningSeverity.Medium,
                Impact = $"Recommended minimum: {minMemoryGb:F1} GB for {cpuCount} vCPU(s)"
            });
        }

        if (memoryGb > maxMemoryGb)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.Resources",
                Message = $"Memory ({memoryGb:F1} GB) may be excessive for {cpuCount} vCPU(s)",
                Code = "CLOUDRUN_HIGH_MEMORY_FOR_CPU",
                Severity = WarningSeverity.Low,
                Impact = $"Recommended maximum: {maxMemoryGb:F1} GB for {cpuCount} vCPU(s)"
            });
        }
    }

    /// <summary>
    /// Validates concurrency configuration
    /// </summary>
    private void ValidateConcurrency(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment == null) return;

        // Note: Using MaxReplicas as proxy for concurrency setting
        var concurrency = deployment.MaxReplicas;

        if (concurrency < MIN_CONCURRENCY)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.MaxReplicas",
                Message = $"Concurrency should be at least {MIN_CONCURRENCY}",
                Code = "CLOUDRUN_LOW_CONCURRENCY",
                Severity = WarningSeverity.Low
            });
        }

        if (concurrency > MAX_CONCURRENCY)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MaxReplicas",
                Message = $"Cloud Run maximum concurrency is {MAX_CONCURRENCY} requests per container",
                Code = "CLOUDRUN_CONCURRENCY_TOO_HIGH",
                CurrentValue = concurrency.ToString(),
                ExpectedValue = $"<= {MAX_CONCURRENCY}",
                DocumentationUrl = "https://cloud.google.com/run/docs/configuring/concurrency"
            });
            result.IsValid = false;
        }

        // Recommendation for optimal concurrency
        if (concurrency == 1)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.MaxReplicas",
                Message = "Consider increasing concurrency for better resource utilization",
                Code = "CLOUDRUN_INCREASE_CONCURRENCY",
                CurrentValue = "1",
                RecommendedValue = "80 (default)",
                Reason = "Higher concurrency reduces cold starts and improves cost efficiency",
                Benefit = "Better performance and lower costs"
            });
        }
    }

    /// <summary>
    /// Validates instance configuration
    /// </summary>
    private void ValidateInstances(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment == null) return;

        if (deployment.MaxReplicas > MAX_INSTANCES)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.MaxReplicas",
                Message = $"Maximum instance count ({deployment.MaxReplicas}) exceeds typical Cloud Run limit ({MAX_INSTANCES})",
                Code = "CLOUDRUN_HIGH_INSTANCE_COUNT",
                Severity = WarningSeverity.High,
                Impact = "May require quota increase request"
            });
        }

        // Validate min <= max
        if (deployment.MinReplicas > deployment.MaxReplicas)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MinReplicas,Deployment.MaxReplicas",
                Message = "Minimum instances cannot exceed maximum instances",
                Code = "CLOUDRUN_INVALID_INSTANCE_RANGE",
                CurrentValue = $"Min: {deployment.MinReplicas}, Max: {deployment.MaxReplicas}",
                ExpectedValue = "Min <= Max"
            });
            result.IsValid = false;
        }

        // Recommendation for minimum instances
        if (deployment.MinReplicas == 0)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.MinReplicas",
                Message = "Consider setting minimum instances > 0 for latency-sensitive applications",
                Code = "CLOUDRUN_MIN_INSTANCES",
                CurrentValue = "0",
                RecommendedValue = "1 or more",
                Reason = "Minimum instances eliminate cold starts",
                Benefit = "Consistent response times, no cold start latency"
            });
        }
    }

    /// <summary>
    /// Adds optimization recommendations
    /// </summary>
    private void AddOptimizationRecommendations(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment?.Resources == null) return;

        // Recommend CPU allocation strategy
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "CpuAllocation",
            Message = "Consider using 'CPU is always allocated' for CPU-intensive workloads",
            Code = "CLOUDRUN_CPU_ALLOCATION",
            RecommendedValue = "Always allocated (vs. only during request processing)",
            Reason = "Enables background processing and improves performance for CPU-bound tasks",
            Benefit = "Better performance for compute-intensive operations"
        });

        // Recommend startup CPU boost
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "StartupCpuBoost",
            Message = "Enable startup CPU boost to reduce cold start time",
            Code = "CLOUDRUN_STARTUP_BOOST",
            RecommendedValue = "true",
            Reason = "Provides additional CPU during container startup",
            Benefit = "Faster cold starts, improved user experience"
        });
    }

    // Helper methods
    private int ParseCpuToMillicores(string cpu)
    {
        if (string.IsNullOrWhiteSpace(cpu)) return 0;
        
        if (cpu.EndsWith("m"))
        {
            return int.TryParse(cpu[..^1], out var millicores) ? millicores : 0;
        }
        
        return int.TryParse(cpu, out var cores) ? cores * 1000 : 0;
    }

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
}
