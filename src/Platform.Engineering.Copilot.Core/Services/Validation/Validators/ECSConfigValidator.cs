using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Core.Services.Validation.Validators;

/// <summary>
/// Validates configurations for AWS Elastic Container Service (ECS)
/// </summary>
public class ECSConfigValidator : IConfigurationValidator
{
    private readonly ILogger<ECSConfigValidator> _logger;

    // ECS Fargate CPU/Memory valid combinations (as of October 2025)
    private static readonly Dictionary<int, int[]> FARGATE_CPU_MEMORY_COMBINATIONS = new()
    {
        { 256, new[] { 512, 1024, 2048 } },  // 0.25 vCPU
        { 512, new[] { 1024, 2048, 3072, 4096 } },  // 0.5 vCPU
        { 1024, new[] { 2048, 3072, 4096, 5120, 6144, 7168, 8192 } },  // 1 vCPU
        { 2048, new[] { 4096, 5120, 6144, 7168, 8192, 9216, 10240, 11264, 12288, 13312, 14336, 15360, 16384 } },  // 2 vCPU
        { 4096, new[] { 8192, 9216, 10240, 11264, 12288, 13312, 14336, 15360, 16384, 17408, 18432, 19456, 20480, 21504, 22528, 23552, 24576, 25600, 26624, 27648, 28672, 29696, 30720 } }  // 4 vCPU
    };

    // ECS Limits
    private const int MAX_TASK_MEMORY_MB = 30720;  // 30 GB for Fargate
    private const int MAX_TASK_CPU = 4096;  // 4 vCPU for Fargate
    private const int MIN_TASK_MEMORY_MB = 512;
    private const int MIN_TASK_CPU = 256;

    public string PlatformName => "ECS";

    public ECSConfigValidator(ILogger<ECSConfigValidator> logger)
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

        // Validate ECS-specific configuration
        ValidateFargateCpuMemory(deployment, result);
        ValidateTaskCount(deployment, result);
        ValidateNetworking(infrastructure?.NetworkConfig, result);
        
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
    /// Validates Fargate CPU/Memory combinations
    /// </summary>
    private void ValidateFargateCpuMemory(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment?.Resources == null) return;

        var cpuMillicores = ParseCpuToMillicores(deployment.Resources.CpuRequest);
        var memoryMb = ParseMemoryToMb(deployment.Resources.MemoryRequest);

        if (cpuMillicores == 0 || memoryMb == 0)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources",
                Message = "Invalid CPU or memory format for ECS Fargate",
                Code = "ECS_INVALID_RESOURCE_FORMAT",
                CurrentValue = $"CPU: {deployment.Resources.CpuRequest}, Memory: {deployment.Resources.MemoryRequest}",
                ExpectedValue = "CPU: millicores (e.g., '1000m') or cores (e.g., '1'), Memory: Mi or Gi (e.g., '2Gi')",
                DocumentationUrl = "https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task-cpu-memory-error.html"
            });
            result.IsValid = false;
            return;
        }

        // Convert millicores to ECS CPU units (1 vCPU = 1024 units)
        var ecsCpu = cpuMillicores;  // Already in correct format (millicores = ECS CPU units)

        // Validate CPU range
        if (ecsCpu < MIN_TASK_CPU)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.CpuRequest",
                Message = $"ECS Fargate minimum CPU is {MIN_TASK_CPU} units (0.25 vCPU)",
                Code = "ECS_CPU_TOO_LOW",
                CurrentValue = $"{ecsCpu} units",
                ExpectedValue = $">= {MIN_TASK_CPU} units",
                DocumentationUrl = "https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task-cpu-memory-error.html"
            });
            result.IsValid = false;
        }

        if (ecsCpu > MAX_TASK_CPU)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.CpuRequest",
                Message = $"ECS Fargate maximum CPU is {MAX_TASK_CPU} units (4 vCPU)",
                Code = "ECS_CPU_TOO_HIGH",
                CurrentValue = $"{ecsCpu} units",
                ExpectedValue = $"<= {MAX_TASK_CPU} units",
                DocumentationUrl = "https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task-cpu-memory-error.html"
            });
            result.IsValid = false;
        }

        // Validate memory range
        if (memoryMb < MIN_TASK_MEMORY_MB)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = $"ECS Fargate minimum memory is {MIN_TASK_MEMORY_MB} MB",
                Code = "ECS_MEMORY_TOO_LOW",
                CurrentValue = $"{memoryMb} MB",
                ExpectedValue = $">= {MIN_TASK_MEMORY_MB} MB"
            });
            result.IsValid = false;
        }

        if (memoryMb > MAX_TASK_MEMORY_MB)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = $"ECS Fargate maximum memory is {MAX_TASK_MEMORY_MB} MB (30 GB)",
                Code = "ECS_MEMORY_TOO_HIGH",
                CurrentValue = $"{memoryMb} MB",
                ExpectedValue = $"<= {MAX_TASK_MEMORY_MB} MB"
            });
            result.IsValid = false;
        }

        // Validate CPU/Memory combination for Fargate
        if (result.IsValid && !IsValidFargateCombination(ecsCpu, memoryMb))
        {
            var validMemory = GetValidMemoryForCpu(ecsCpu);
            var validMemoryStr = validMemory.Length > 0 
                ? string.Join(", ", validMemory.Select(m => $"{m}MB")) 
                : "No valid combinations";

            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources",
                Message = $"Invalid CPU/Memory combination for ECS Fargate. CPU {ecsCpu} units requires specific memory values.",
                Code = "ECS_INVALID_CPU_MEMORY_COMBINATION",
                CurrentValue = $"{ecsCpu} CPU units, {memoryMb} MB memory",
                ExpectedValue = $"For {ecsCpu} CPU units, valid memory: {validMemoryStr}",
                DocumentationUrl = "https://docs.aws.amazon.com/AmazonECS/latest/developerguide/task-cpu-memory-error.html"
            });
            result.IsValid = false;
        }

        // Warning for high resource allocation
        if (ecsCpu >= 2048 && memoryMb >= 8192)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.Resources",
                Message = "High resource allocation will increase costs significantly",
                Code = "ECS_HIGH_RESOURCE_COST",
                Severity = WarningSeverity.Medium,
                Impact = $"Using {ecsCpu / 1024.0:F2} vCPU and {memoryMb / 1024.0:F1} GB memory"
            });
        }
    }

    /// <summary>
    /// Validates task count configuration
    /// </summary>
    private void ValidateTaskCount(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment == null) return;

        // Validate min <= max
        if (deployment.MinReplicas > deployment.MaxReplicas)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MinReplicas,Deployment.MaxReplicas",
                Message = "Minimum task count cannot exceed maximum task count",
                Code = "ECS_INVALID_TASK_RANGE",
                CurrentValue = $"Min: {deployment.MinReplicas}, Max: {deployment.MaxReplicas}",
                ExpectedValue = "Min <= Max"
            });
            result.IsValid = false;
        }

        // Warning for single task (no high availability)
        if (deployment.MinReplicas == 1 && deployment.MaxReplicas == 1)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.MinReplicas,Deployment.MaxReplicas",
                Message = "Running only 1 task provides no high availability",
                Code = "ECS_NO_HIGH_AVAILABILITY",
                Severity = WarningSeverity.High,
                Impact = "Service will be unavailable during deployments or task failures"
            });
        }

        // Recommendation for auto-scaling
        if (!deployment.AutoScaling)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.AutoScaling",
                Message = "Enable auto-scaling for better resource utilization",
                Code = "ECS_ENABLE_AUTOSCALING",
                CurrentValue = "false",
                RecommendedValue = "true",
                Reason = "Auto-scaling adjusts task count based on workload demand",
                Benefit = "Reduce costs during low-traffic periods and ensure capacity during peaks"
            });
        }
    }

    /// <summary>
    /// Validates networking configuration
    /// </summary>
    private void ValidateNetworking(NetworkingConfiguration? network, ValidationResult result)
    {
        if (network == null) return;

        // ECS Fargate requires awsvpc network mode, which needs proper VPC configuration
        if (network.Mode == NetworkMode.CreateNew)
        {
            if (string.IsNullOrWhiteSpace(network.VNetAddressSpace))
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.VNetAddressSpace",
                    Message = "VPC CIDR block is required for ECS Fargate",
                    Code = "ECS_VPC_CIDR_REQUIRED"
                });
                result.IsValid = false;
            }

            // Validate subnets
            if (network.Subnets == null || network.Subnets.Count == 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.Subnets",
                    Message = "At least 2 subnets in different availability zones are recommended for ECS high availability",
                    Code = "ECS_SUBNET_REQUIRED"
                });
                result.IsValid = false;
            }
            else if (network.Subnets.Count < 2)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Field = "Infrastructure.NetworkConfig.Subnets",
                    Message = "Using only 1 subnet reduces high availability. Deploy across multiple AZs.",
                    Code = "ECS_SINGLE_AZ",
                    Severity = WarningSeverity.High,
                    Impact = "Service may be unavailable if the availability zone fails"
                });
            }
        }
        else if (network.Mode == NetworkMode.UseExisting)
        {
            if (network.ExistingSubnets == null || network.ExistingSubnets.Count == 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.ExistingSubnets",
                    Message = "At least one existing subnet must be selected for ECS tasks",
                    Code = "ECS_EXISTING_SUBNET_REQUIRED"
                });
                result.IsValid = false;
            }
        }
    }

    /// <summary>
    /// Adds optimization recommendations
    /// </summary>
    private void AddOptimizationRecommendations(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment?.Resources == null) return;

        // Recommend Fargate Spot for cost savings
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "CapacityProvider",
            Message = "Consider using Fargate Spot for non-critical workloads",
            Code = "ECS_FARGATE_SPOT",
            RecommendedValue = "FARGATE_SPOT",
            Reason = "Fargate Spot can save up to 70% on compute costs",
            Benefit = "Significant cost reduction for fault-tolerant workloads"
        });

        // Recommend right-sizing
        var memoryMb = ParseMemoryToMb(deployment.Resources.MemoryRequest);
        if (memoryMb >= 8192)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = "Consider monitoring actual memory usage to right-size your tasks",
                Code = "ECS_RIGHTSIZE_MEMORY",
                CurrentValue = $"{memoryMb} MB",
                Reason = "Over-provisioning memory increases costs",
                Benefit = "Potential cost savings by matching allocation to actual usage"
            });
        }
    }

    // Helper methods
    private bool IsValidFargateCombination(int cpuUnits, int memoryMb)
    {
        if (!FARGATE_CPU_MEMORY_COMBINATIONS.ContainsKey(cpuUnits))
            return false;

        return FARGATE_CPU_MEMORY_COMBINATIONS[cpuUnits].Contains(memoryMb);
    }

    private int[] GetValidMemoryForCpu(int cpuUnits)
    {
        // Find the closest valid CPU value
        var validCpu = FARGATE_CPU_MEMORY_COMBINATIONS.Keys
            .OrderBy(cpu => Math.Abs(cpu - cpuUnits))
            .First();

        return FARGATE_CPU_MEMORY_COMBINATIONS[validCpu];
    }

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
