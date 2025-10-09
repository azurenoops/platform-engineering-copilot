using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Core.Services.Validation.Validators;

/// <summary>
/// Validates configurations for Azure Kubernetes Service (AKS)
/// </summary>
public class AKSConfigValidator : IConfigurationValidator
{
    private readonly ILogger<AKSConfigValidator> _logger;

    // AKS Limits (as of October 2025)
    private const int MIN_NODE_COUNT = 1;
    private const int MAX_NODE_COUNT_PER_POOL = 1000;
    private const int RECOMMENDED_MAX_NODE_COUNT = 100;
    private const int MIN_POD_PER_NODE = 10;
    private const int MAX_POD_PER_NODE = 250;

    // Supported Kubernetes versions (update periodically)
    private static readonly string[] SUPPORTED_K8S_VERSIONS = new[]
    {
        "1.27", "1.28", "1.29", "1.30"
    };

    public string PlatformName => "AKS";

    public AKSConfigValidator(ILogger<AKSConfigValidator> logger)
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
        var network = infrastructure?.NetworkConfig;

        // Validate deployment configuration
        ValidateNodePool(deployment, result);
        ValidateResources(deployment, result);
        ValidateNetworking(network, result);
        ValidateKubernetesVersion(result);
        
        // Add recommendations
        AddOptimizationRecommendations(deployment, result);

        return result;
    }

    public bool IsValid(TemplateGenerationRequest request)
    {
        var result = ValidateTemplate(request);
        return result.IsValid;
    }

    /// <summary>
    /// Validates node pool configuration
    /// </summary>
    private void ValidateNodePool(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment == null) return;

        // Validate minimum replicas (node count)
        if (deployment.MinReplicas < MIN_NODE_COUNT)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MinReplicas",
                Message = $"AKS node pool must have at least {MIN_NODE_COUNT} node",
                Code = "AKS_INVALID_MIN_NODES",
                CurrentValue = deployment.MinReplicas.ToString(),
                ExpectedValue = $">= {MIN_NODE_COUNT}",
                DocumentationUrl = "https://learn.microsoft.com/en-us/azure/aks/concepts-scale"
            });
            result.IsValid = false;
        }

        // Validate maximum replicas
        if (deployment.MaxReplicas > MAX_NODE_COUNT_PER_POOL)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MaxReplicas",
                Message = $"AKS node pool maximum is {MAX_NODE_COUNT_PER_POOL} nodes per pool",
                Code = "AKS_INVALID_MAX_NODES",
                CurrentValue = deployment.MaxReplicas.ToString(),
                ExpectedValue = $"<= {MAX_NODE_COUNT_PER_POOL}",
                DocumentationUrl = "https://learn.microsoft.com/en-us/azure/aks/quotas-skus-regions"
            });
            result.IsValid = false;
        }

        // Warning for large node pools
        if (deployment.MaxReplicas > RECOMMENDED_MAX_NODE_COUNT)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.MaxReplicas",
                Message = $"Large node pools (>{RECOMMENDED_MAX_NODE_COUNT} nodes) may be difficult to manage. Consider using multiple node pools.",
                Code = "AKS_LARGE_NODE_POOL",
                Severity = WarningSeverity.Medium,
                Impact = "May affect cluster upgrade times and management complexity"
            });
        }

        // Validate min <= max
        if (deployment.MinReplicas > deployment.MaxReplicas)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MinReplicas,Deployment.MaxReplicas",
                Message = "Minimum replicas cannot exceed maximum replicas",
                Code = "AKS_INVALID_REPLICA_RANGE",
                CurrentValue = $"Min: {deployment.MinReplicas}, Max: {deployment.MaxReplicas}",
                ExpectedValue = "Min <= Max"
            });
            result.IsValid = false;
        }

        // Validate auto-scaling enabled with proper range
        if (deployment.AutoScaling && deployment.MinReplicas == deployment.MaxReplicas)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.AutoScaling",
                Message = "Auto-scaling is enabled but min and max replicas are equal. Auto-scaling will have no effect.",
                Code = "AKS_AUTOSCALING_NO_EFFECT",
                Severity = WarningSeverity.Low
            });
        }
    }

    /// <summary>
    /// Validates resource requests and limits
    /// </summary>
    private void ValidateResources(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment?.Resources == null) return;

        var resources = deployment.Resources;

        // Validate CPU format
        if (!IsValidCpuFormat(resources.CpuRequest))
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.CpuRequest",
                Message = "Invalid CPU format. Use whole numbers (e.g., '2') or millicores (e.g., '500m')",
                Code = "AKS_INVALID_CPU_FORMAT",
                CurrentValue = resources.CpuRequest,
                ExpectedValue = "Examples: '1', '2', '500m', '1500m'",
                DocumentationUrl = "https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/"
            });
            result.IsValid = false;
        }

        if (!IsValidCpuFormat(resources.CpuLimit))
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.CpuLimit",
                Message = "Invalid CPU format",
                Code = "AKS_INVALID_CPU_FORMAT",
                CurrentValue = resources.CpuLimit
            });
            result.IsValid = false;
        }

        // Validate memory format
        if (!IsValidMemoryFormat(resources.MemoryRequest))
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = "Invalid memory format. Use Mi or Gi suffix (e.g., '512Mi', '2Gi')",
                Code = "AKS_INVALID_MEMORY_FORMAT",
                CurrentValue = resources.MemoryRequest,
                ExpectedValue = "Examples: '128Mi', '512Mi', '1Gi', '4Gi'",
                DocumentationUrl = "https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/"
            });
            result.IsValid = false;
        }

        if (!IsValidMemoryFormat(resources.MemoryLimit))
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryLimit",
                Message = "Invalid memory format",
                Code = "AKS_INVALID_MEMORY_FORMAT",
                CurrentValue = resources.MemoryLimit
            });
            result.IsValid = false;
        }

        // Validate request <= limit
        if (IsValidCpuFormat(resources.CpuRequest) && IsValidCpuFormat(resources.CpuLimit))
        {
            var requestMillicores = ParseCpuToMillicores(resources.CpuRequest);
            var limitMillicores = ParseCpuToMillicores(resources.CpuLimit);

            if (requestMillicores > limitMillicores)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Deployment.Resources.CpuRequest,Deployment.Resources.CpuLimit",
                    Message = "CPU request cannot exceed CPU limit",
                    Code = "AKS_CPU_REQUEST_EXCEEDS_LIMIT",
                    CurrentValue = $"Request: {resources.CpuRequest}, Limit: {resources.CpuLimit}"
                });
                result.IsValid = false;
            }
        }

        if (IsValidMemoryFormat(resources.MemoryRequest) && IsValidMemoryFormat(resources.MemoryLimit))
        {
            var requestMi = ParseMemoryToMi(resources.MemoryRequest);
            var limitMi = ParseMemoryToMi(resources.MemoryLimit);

            if (requestMi > limitMi)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Deployment.Resources.MemoryRequest,Deployment.Resources.MemoryLimit",
                    Message = "Memory request cannot exceed memory limit",
                    Code = "AKS_MEMORY_REQUEST_EXCEEDS_LIMIT",
                    CurrentValue = $"Request: {resources.MemoryRequest}, Limit: {resources.MemoryLimit}"
                });
                result.IsValid = false;
            }
        }
    }

    /// <summary>
    /// Validates networking configuration
    /// </summary>
    private void ValidateNetworking(NetworkingConfiguration? network, ValidationResult result)
    {
        if (network == null) return;

        // Validate VNet address space
        if (network.Mode == NetworkMode.CreateNew)
        {
            if (string.IsNullOrWhiteSpace(network.VNetAddressSpace))
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.VNetAddressSpace",
                    Message = "VNet address space is required for new network creation",
                    Code = "AKS_VNET_ADDRESS_SPACE_REQUIRED"
                });
                result.IsValid = false;
            }

            // Validate at least one subnet
            if (network.Subnets == null || network.Subnets.Count == 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.Subnets",
                    Message = "At least one subnet is required for AKS node pool",
                    Code = "AKS_SUBNET_REQUIRED"
                });
                result.IsValid = false;
            }
            else
            {
                // Check subnet sizes for AKS requirements
                foreach (var subnet in network.Subnets)
                {
                    if (subnet.Purpose == SubnetPurpose.Application)
                    {
                        // AKS requires /24 or larger for node pools
                        if (!IsSubnetSizeSufficient(subnet.AddressPrefix, 24))
                        {
                            result.Warnings.Add(new ValidationWarning
                            {
                                Field = $"Infrastructure.NetworkConfig.Subnets[{subnet.Name}]",
                                Message = $"AKS node subnet '{subnet.Name}' should typically be /24 or larger to accommodate node scaling",
                                Code = "AKS_SMALL_SUBNET",
                                Severity = WarningSeverity.Medium,
                                Impact = "May limit node scaling capabilities"
                            });
                        }
                    }
                }
            }
        }
        else if (network.Mode == NetworkMode.UseExisting)
        {
            if (network.ExistingSubnets == null || network.ExistingSubnets.Count == 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.ExistingSubnets",
                    Message = "At least one existing subnet must be selected for AKS node pool",
                    Code = "AKS_EXISTING_SUBNET_REQUIRED"
                });
                result.IsValid = false;
            }
        }
    }

    /// <summary>
    /// Validates Kubernetes version
    /// </summary>
    private void ValidateKubernetesVersion(ValidationResult result)
    {
        // Note: In a real implementation, you'd get this from the request
        // For now, just add a recommendation to use supported versions
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "KubernetesVersion",
            Message = "Use a supported Kubernetes version for AKS",
            Code = "AKS_K8S_VERSION",
            RecommendedValue = string.Join(", ", SUPPORTED_K8S_VERSIONS),
            Reason = "Ensures compatibility and security updates",
            Benefit = "Access to latest features and security patches"
        });
    }

    /// <summary>
    /// Adds optimization recommendations
    /// </summary>
    private void AddOptimizationRecommendations(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment == null) return;

        // Recommend enabling auto-scaling
        if (!deployment.AutoScaling)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.AutoScaling",
                Message = "Enable auto-scaling for better resource utilization and cost optimization",
                Code = "AKS_ENABLE_AUTOSCALING",
                CurrentValue = "false",
                RecommendedValue = "true",
                Reason = "Auto-scaling adjusts node count based on workload demand",
                Benefit = "Reduce costs during low-traffic periods and ensure capacity during peak times"
            });
        }

        // Recommend resource limits
        if (deployment.Resources != null)
        {
            var cpuRequest = ParseCpuToMillicores(deployment.Resources.CpuRequest);
            var cpuLimit = ParseCpuToMillicores(deployment.Resources.CpuLimit);

            // Check if request and limit are too far apart
            if (cpuLimit > cpuRequest * 4)
            {
                result.Recommendations.Add(new ValidationRecommendation
                {
                    Field = "Deployment.Resources.CpuLimit",
                    Message = "CPU limit is significantly higher than request. Consider reducing the gap for better scheduling",
                    Code = "AKS_CPU_LIMIT_TOO_HIGH",
                    CurrentValue = $"Request: {deployment.Resources.CpuRequest}, Limit: {deployment.Resources.CpuLimit}",
                    RecommendedValue = "Limit should be 1-2x the request",
                    Reason = "Large gaps can lead to resource overcommitment and scheduling issues",
                    Benefit = "More predictable performance and better resource utilization"
                });
            }
        }
    }

    // Helper methods for parsing and validation
    private bool IsValidCpuFormat(string cpu)
    {
        if (string.IsNullOrWhiteSpace(cpu)) return false;
        
        // Check for millicores format (e.g., "500m")
        if (cpu.EndsWith("m"))
        {
            return int.TryParse(cpu[..^1], out var millicores) && millicores > 0;
        }
        
        // Check for whole number format (e.g., "2")
        return int.TryParse(cpu, out var cores) && cores > 0;
    }

    private bool IsValidMemoryFormat(string memory)
    {
        if (string.IsNullOrWhiteSpace(memory)) return false;
        
        // Check for Mi or Gi suffix
        if (memory.EndsWith("Mi") || memory.EndsWith("Gi"))
        {
            var numberPart = memory.EndsWith("Mi") ? memory[..^2] : memory[..^2];
            return int.TryParse(numberPart, out var value) && value > 0;
        }
        
        return false;
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

    private int ParseMemoryToMi(string memory)
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

    private bool IsSubnetSizeSufficient(string cidr, int recommendedPrefixLength)
    {
        if (string.IsNullOrWhiteSpace(cidr)) return false;
        
        var parts = cidr.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var prefixLength))
        {
            return false;
        }
        
        // Smaller prefix length = larger subnet (e.g., /16 is larger than /24)
        return prefixLength <= recommendedPrefixLength;
    }
}
