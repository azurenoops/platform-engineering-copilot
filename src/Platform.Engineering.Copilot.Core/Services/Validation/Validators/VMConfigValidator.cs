using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Core.Services.Validation.Validators;

/// <summary>
/// Validates configurations for Azure Virtual Machines
/// </summary>
public class VMConfigValidator : IConfigurationValidator
{
    private readonly ILogger<VMConfigValidator> _logger;

    // Common Azure VM sizes and their specs
    private static readonly Dictionary<string, (int Cores, int MemoryGb)> AZURE_VM_SIZES = new()
    {
        // B-series (Burstable)
        { "Standard_B1s", (1, 1) },
        { "Standard_B1ms", (1, 2) },
        { "Standard_B2s", (2, 4) },
        { "Standard_B2ms", (2, 8) },
        { "Standard_B4ms", (4, 16) },
        
        // D-series (General purpose)
        { "Standard_D2s_v3", (2, 8) },
        { "Standard_D4s_v3", (4, 16) },
        { "Standard_D8s_v3", (8, 32) },
        { "Standard_D16s_v3", (16, 64) },
        { "Standard_D32s_v3", (32, 128) },
        
        // E-series (Memory optimized)
        { "Standard_E2s_v3", (2, 16) },
        { "Standard_E4s_v3", (4, 32) },
        { "Standard_E8s_v3", (8, 64) },
        { "Standard_E16s_v3", (16, 128) },
        
        // F-series (Compute optimized)
        { "Standard_F2s_v2", (2, 4) },
        { "Standard_F4s_v2", (4, 8) },
        { "Standard_F8s_v2", (8, 16) },
        { "Standard_F16s_v2", (16, 32) }
    };

    // Disk limits
    private const int MIN_OS_DISK_GB = 30;
    private const int MAX_OS_DISK_GB = 4095;
    private const int RECOMMENDED_OS_DISK_GB = 128;

    public string PlatformName => "AzureVM";

    public VMConfigValidator(ILogger<VMConfigValidator> logger)
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

        // Validate VM-specific configuration
        ValidateVMSize(deployment, result);
        ValidateInstanceCount(deployment, result);
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
    /// Validates VM size based on CPU and memory requirements
    /// </summary>
    private void ValidateVMSize(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment?.Resources == null) return;

        var cpuMillicores = ParseCpuToMillicores(deployment.Resources.CpuRequest);
        var memoryMb = ParseMemoryToMb(deployment.Resources.MemoryRequest);

        if (cpuMillicores == 0 || memoryMb == 0)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources",
                Message = "Invalid CPU or memory format for Azure VM",
                Code = "VM_INVALID_RESOURCE_FORMAT",
                CurrentValue = $"CPU: {deployment.Resources.CpuRequest}, Memory: {deployment.Resources.MemoryRequest}",
                ExpectedValue = "CPU: cores (e.g., '2', '4') or millicores (e.g., '2000m'), Memory: Mi or Gi (e.g., '8Gi')"
            });
            result.IsValid = false;
            return;
        }

        var requestedCores = cpuMillicores / 1000;
        var requestedMemoryGb = memoryMb / 1024;

        // Find suitable VM sizes
        var suitableSizes = FindSuitableVMSizes(requestedCores, requestedMemoryGb);

        if (suitableSizes.Count == 0)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.Resources",
                Message = $"No exact VM size match for {requestedCores} cores and {requestedMemoryGb} GB memory",
                Code = "VM_NO_EXACT_SIZE_MATCH",
                Severity = WarningSeverity.Medium,
                Impact = "Will provision larger VM size to meet requirements"
            });

            // Find the smallest VM that meets requirements
            var nextBestSize = FindNextBestVMSize(requestedCores, requestedMemoryGb);
            if (nextBestSize != null)
            {
                result.Recommendations.Add(new ValidationRecommendation
                {
                    Field = "VMSize",
                    Message = "Recommended VM size based on resource requirements",
                    Code = "VM_RECOMMENDED_SIZE",
                    CurrentValue = $"{requestedCores} cores, {requestedMemoryGb} GB",
                    RecommendedValue = $"{nextBestSize} ({AZURE_VM_SIZES[nextBestSize].Cores} cores, {AZURE_VM_SIZES[nextBestSize].MemoryGb} GB)",
                    Reason = "Smallest VM size that meets or exceeds requirements",
                    Benefit = "Optimal price/performance ratio"
                });
            }
        }
        else
        {
            var recommendedSize = suitableSizes.First();
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "VMSize",
                Message = "Recommended VM size based on resource requirements",
                Code = "VM_RECOMMENDED_SIZE",
                RecommendedValue = $"{recommendedSize} ({AZURE_VM_SIZES[recommendedSize].Cores} cores, {AZURE_VM_SIZES[recommendedSize].MemoryGb} GB)",
                Reason = "Exact match for requested resources",
                Benefit = "Optimal resource utilization"
            });
        }

        // Warning for oversized VMs
        if (requestedCores >= 16 || requestedMemoryGb >= 64)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.Resources",
                Message = "Large VM size will increase costs significantly",
                Code = "VM_LARGE_SIZE_COST",
                Severity = WarningSeverity.High,
                Impact = $"Requesting {requestedCores} cores and {requestedMemoryGb} GB memory"
            });
        }
    }

    /// <summary>
    /// Validates instance count
    /// </summary>
    private void ValidateInstanceCount(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment == null) return;

        // Validate min <= max
        if (deployment.MinReplicas > deployment.MaxReplicas)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MinReplicas,Deployment.MaxReplicas",
                Message = "Minimum instance count cannot exceed maximum instance count",
                Code = "VM_INVALID_INSTANCE_RANGE",
                CurrentValue = $"Min: {deployment.MinReplicas}, Max: {deployment.MaxReplicas}",
                ExpectedValue = "Min <= Max"
            });
            result.IsValid = false;
        }

        // Warning for single VM (no high availability)
        if (deployment.MinReplicas == 1 && deployment.MaxReplicas == 1)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.MinReplicas,Deployment.MaxReplicas",
                Message = "Running only 1 VM provides no high availability",
                Code = "VM_NO_HIGH_AVAILABILITY",
                Severity = WarningSeverity.High,
                Impact = "Service will be unavailable during VM maintenance or failures"
            });
        }

        // Recommendation for availability zones
        if (deployment.MinReplicas >= 2 && deployment.MinReplicas < 3)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.MinReplicas",
                Message = "Deploy at least 3 VMs across availability zones for high availability",
                Code = "VM_MULTI_AZ",
                CurrentValue = deployment.MinReplicas.ToString(),
                RecommendedValue = "3 or more (1 per availability zone)",
                Reason = "Ensures service remains available if a zone fails",
                Benefit = "99.99% SLA with zone-redundant deployment"
            });
        }

        // Recommendation for auto-scaling
        if (!deployment.AutoScaling)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.AutoScaling",
                Message = "Enable auto-scaling with VM Scale Sets for better resource utilization",
                Code = "VM_ENABLE_AUTOSCALING",
                CurrentValue = "false",
                RecommendedValue = "true",
                Reason = "Auto-scaling adjusts VM count based on workload demand",
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

        if (network.Mode == NetworkMode.CreateNew)
        {
            if (string.IsNullOrWhiteSpace(network.VNetAddressSpace))
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.VNetAddressSpace",
                    Message = "VNet address space is required for VM deployment",
                    Code = "VM_VNET_REQUIRED"
                });
                result.IsValid = false;
            }

            if (network.Subnets == null || network.Subnets.Count == 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.Subnets",
                    Message = "At least one subnet is required for VM deployment",
                    Code = "VM_SUBNET_REQUIRED"
                });
                result.IsValid = false;
            }
        }
        else if (network.Mode == NetworkMode.UseExisting)
        {
            if (network.ExistingSubnets == null || network.ExistingSubnets.Count == 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.ExistingSubnets",
                    Message = "At least one existing subnet must be selected for VM deployment",
                    Code = "VM_EXISTING_SUBNET_REQUIRED"
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
        // Recommend spot instances for cost savings
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "PricingModel",
            Message = "Consider Azure Spot VMs for non-critical workloads",
            Code = "VM_SPOT_INSTANCES",
            RecommendedValue = "Spot VMs for dev/test",
            Reason = "Spot VMs can save up to 90% on compute costs",
            Benefit = "Significant cost reduction for fault-tolerant workloads"
        });

        // Recommend reserved instances for production
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "PricingModel",
            Message = "Consider Azure Reserved VM Instances for production workloads",
            Code = "VM_RESERVED_INSTANCES",
            RecommendedValue = "1-year or 3-year reservations",
            Reason = "Reserved instances provide up to 72% cost savings",
            Benefit = "Predictable costs and significant savings for steady-state workloads"
        });

        // Recommend managed disks
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "DiskType",
            Message = "Use Azure Managed Disks for better reliability",
            Code = "VM_MANAGED_DISKS",
            RecommendedValue = "Premium SSD or Standard SSD",
            Reason = "Managed disks provide better availability and simplified management",
            Benefit = "Built-in replication and automatic failover"
        });

        // Recommend accelerated networking
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Networking",
            Message = "Enable Accelerated Networking for better performance",
            Code = "VM_ACCELERATED_NETWORKING",
            RecommendedValue = "true",
            Reason = "Accelerated Networking bypasses the host for lower latency",
            Benefit = "Up to 30 Gbps network throughput and reduced latency"
        });
    }

    // Helper methods
    private List<string> FindSuitableVMSizes(int requestedCores, int requestedMemoryGb)
    {
        return AZURE_VM_SIZES
            .Where(kv => kv.Value.Cores == requestedCores && kv.Value.MemoryGb == requestedMemoryGb)
            .Select(kv => kv.Key)
            .OrderBy(size => size)
            .ToList();
    }

    private string? FindNextBestVMSize(int requestedCores, int requestedMemoryGb)
    {
        // Find smallest VM that meets or exceeds both requirements
        return AZURE_VM_SIZES
            .Where(kv => kv.Value.Cores >= requestedCores && kv.Value.MemoryGb >= requestedMemoryGb)
            .OrderBy(kv => kv.Value.Cores)
            .ThenBy(kv => kv.Value.MemoryGb)
            .Select(kv => kv.Key)
            .FirstOrDefault();
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
