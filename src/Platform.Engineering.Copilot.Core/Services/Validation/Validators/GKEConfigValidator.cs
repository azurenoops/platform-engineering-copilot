using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Core.Services.Validation.Validators;

/// <summary>
/// Validates configurations for Google Kubernetes Engine (GKE)
/// </summary>
public class GKEConfigValidator : IConfigurationValidator
{
    private readonly ILogger<GKEConfigValidator> _logger;

    // GKE Limits (as of October 2025)
    private const int MIN_NODE_COUNT = 1;
    private const int MAX_NODE_COUNT_PER_POOL = 1000;
    private const int RECOMMENDED_MAX_NODE_COUNT = 100;

    // Supported Kubernetes versions (update periodically)
    private static readonly string[] SUPPORTED_K8S_VERSIONS = new[]
    {
        "1.27", "1.28", "1.29", "1.30"
    };

    public string PlatformName => "GKE";

    public GKEConfigValidator(ILogger<GKEConfigValidator> logger)
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

        // Validate GKE-specific configuration
        ValidateNodePool(deployment, result);
        ValidateResources(deployment, result);
        ValidateNetworking(network, result);
        
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
                Message = $"GKE node pool must have at least {MIN_NODE_COUNT} node",
                Code = "GKE_INVALID_MIN_NODES",
                CurrentValue = deployment.MinReplicas.ToString(),
                ExpectedValue = $">= {MIN_NODE_COUNT}",
                DocumentationUrl = "https://cloud.google.com/kubernetes-engine/docs/concepts/cluster-autoscaler"
            });
            result.IsValid = false;
        }

        // Validate maximum replicas
        if (deployment.MaxReplicas > MAX_NODE_COUNT_PER_POOL)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MaxReplicas",
                Message = $"GKE node pool maximum is {MAX_NODE_COUNT_PER_POOL} nodes per pool",
                Code = "GKE_INVALID_MAX_NODES",
                CurrentValue = deployment.MaxReplicas.ToString(),
                ExpectedValue = $"<= {MAX_NODE_COUNT_PER_POOL}",
                DocumentationUrl = "https://cloud.google.com/kubernetes-engine/quotas"
            });
            result.IsValid = false;
        }

        // Warning for large node pools
        if (deployment.MaxReplicas > RECOMMENDED_MAX_NODE_COUNT)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.MaxReplicas",
                Message = $"Large node pools (>{RECOMMENDED_MAX_NODE_COUNT} nodes) may be difficult to manage",
                Code = "GKE_LARGE_NODE_POOL",
                Severity = WarningSeverity.Medium,
                Impact = "Consider using multiple node pools for better management"
            });
        }

        // Validate min <= max
        if (deployment.MinReplicas > deployment.MaxReplicas)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MinReplicas,Deployment.MaxReplicas",
                Message = "Minimum replicas cannot exceed maximum replicas",
                Code = "GKE_INVALID_REPLICA_RANGE",
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
                Message = "Auto-scaling is enabled but min and max replicas are equal",
                Code = "GKE_AUTOSCALING_NO_EFFECT",
                Severity = WarningSeverity.Low,
                Impact = "Auto-scaling will have no effect"
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
                Message = "Invalid CPU format",
                Code = "GKE_INVALID_CPU_FORMAT",
                CurrentValue = resources.CpuRequest,
                ExpectedValue = "Examples: '1', '2', '500m', '1500m'",
                DocumentationUrl = "https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/"
            });
            result.IsValid = false;
        }

        // Validate memory format
        if (!IsValidMemoryFormat(resources.MemoryRequest))
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.MemoryRequest",
                Message = "Invalid memory format",
                Code = "GKE_INVALID_MEMORY_FORMAT",
                CurrentValue = resources.MemoryRequest,
                ExpectedValue = "Examples: '128Mi', '512Mi', '1Gi', '4Gi'",
                DocumentationUrl = "https://kubernetes.io/docs/concepts/configuration/manage-resources-containers/"
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
                    Code = "GKE_CPU_REQUEST_EXCEEDS_LIMIT",
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
                    Code = "GKE_MEMORY_REQUEST_EXCEEDS_LIMIT",
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

        if (network.Mode == NetworkMode.CreateNew)
        {
            if (string.IsNullOrWhiteSpace(network.VNetAddressSpace))
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.VNetAddressSpace",
                    Message = "VPC CIDR block is required for new network creation",
                    Code = "GKE_VPC_CIDR_REQUIRED"
                });
                result.IsValid = false;
            }

            // Validate at least one subnet
            if (network.Subnets == null || network.Subnets.Count == 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.Subnets",
                    Message = "At least one subnet is required for GKE node pool",
                    Code = "GKE_SUBNET_REQUIRED"
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
                    Message = "At least one existing subnet must be selected for GKE node pool",
                    Code = "GKE_EXISTING_SUBNET_REQUIRED"
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
        if (deployment == null) return;

        // Recommend enabling auto-scaling
        if (!deployment.AutoScaling)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.AutoScaling",
                Message = "Enable cluster autoscaler for better resource utilization",
                Code = "GKE_ENABLE_AUTOSCALING",
                CurrentValue = "false",
                RecommendedValue = "true",
                Reason = "Cluster autoscaler adjusts node count based on workload demand",
                Benefit = "Reduce costs during low-traffic periods and ensure capacity during peaks"
            });
        }

        // Recommend GKE Autopilot for simplified operations
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "ClusterMode",
            Message = "Consider GKE Autopilot for fully managed Kubernetes experience",
            Code = "GKE_AUTOPILOT",
            RecommendedValue = "Autopilot mode",
            Reason = "Autopilot manages node provisioning, scaling, and security",
            Benefit = "Reduced operational overhead and optimized resource utilization"
        });

        // Recommend workload identity
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "WorkloadIdentity",
            Message = "Enable Workload Identity for secure service account access",
            Code = "GKE_WORKLOAD_IDENTITY",
            RecommendedValue = "true",
            Reason = "Workload Identity is the recommended way to access Google Cloud services",
            Benefit = "Better security by eliminating service account keys"
        });
    }

    // Helper methods
    private bool IsValidCpuFormat(string cpu)
    {
        if (string.IsNullOrWhiteSpace(cpu)) return false;
        
        if (cpu.EndsWith("m"))
        {
            return int.TryParse(cpu[..^1], out var millicores) && millicores > 0;
        }
        
        return int.TryParse(cpu, out var cores) && cores > 0;
    }

    private bool IsValidMemoryFormat(string memory)
    {
        if (string.IsNullOrWhiteSpace(memory)) return false;
        
        return (memory.EndsWith("Mi") || memory.EndsWith("Gi")) &&
               int.TryParse(memory[..^2], out var value) && value > 0;
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
}
