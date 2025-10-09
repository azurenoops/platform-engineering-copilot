using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Core.Services.Validation.Validators;

/// <summary>
/// Validates configurations for Amazon Elastic Kubernetes Service (EKS)
/// </summary>
public class EKSConfigValidator : IConfigurationValidator
{
    private readonly ILogger<EKSConfigValidator> _logger;

    // EKS Limits (as of October 2025)
    private const int MIN_NODE_COUNT = 1;
    private const int MAX_NODE_COUNT_PER_GROUP = 450;
    private const int RECOMMENDED_MAX_NODE_COUNT = 100;

    // Supported Kubernetes versions (update periodically)
    private static readonly string[] SUPPORTED_K8S_VERSIONS = new[]
    {
        "1.27", "1.28", "1.29", "1.30"
    };

    public string PlatformName => "EKS";

    public EKSConfigValidator(ILogger<EKSConfigValidator> logger)
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

        // Validate EKS-specific configuration
        ValidateNodeGroup(deployment, result);
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
    /// Validates node group configuration
    /// </summary>
    private void ValidateNodeGroup(DeploymentSpec? deployment, ValidationResult result)
    {
        if (deployment == null) return;

        // Validate minimum replicas (node count)
        if (deployment.MinReplicas < MIN_NODE_COUNT)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MinReplicas",
                Message = $"EKS node group must have at least {MIN_NODE_COUNT} node",
                Code = "EKS_INVALID_MIN_NODES",
                CurrentValue = deployment.MinReplicas.ToString(),
                ExpectedValue = $">= {MIN_NODE_COUNT}",
                DocumentationUrl = "https://docs.aws.amazon.com/eks/latest/userguide/managed-node-groups.html"
            });
            result.IsValid = false;
        }

        // Validate maximum replicas
        if (deployment.MaxReplicas > MAX_NODE_COUNT_PER_GROUP)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MaxReplicas",
                Message = $"EKS managed node group maximum is {MAX_NODE_COUNT_PER_GROUP} nodes per group",
                Code = "EKS_INVALID_MAX_NODES",
                CurrentValue = deployment.MaxReplicas.ToString(),
                ExpectedValue = $"<= {MAX_NODE_COUNT_PER_GROUP}",
                DocumentationUrl = "https://docs.aws.amazon.com/eks/latest/userguide/service-quotas.html"
            });
            result.IsValid = false;
        }

        // Warning for large node groups
        if (deployment.MaxReplicas > RECOMMENDED_MAX_NODE_COUNT)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Deployment.MaxReplicas",
                Message = $"Large node groups (>{RECOMMENDED_MAX_NODE_COUNT} nodes) may be difficult to manage",
                Code = "EKS_LARGE_NODE_GROUP",
                Severity = WarningSeverity.Medium,
                Impact = "Consider using multiple node groups for better availability and management"
            });
        }

        // Validate min <= max
        if (deployment.MinReplicas > deployment.MaxReplicas)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MinReplicas,Deployment.MaxReplicas",
                Message = "Minimum replicas cannot exceed maximum replicas",
                Code = "EKS_INVALID_REPLICA_RANGE",
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
                Code = "EKS_AUTOSCALING_NO_EFFECT",
                Severity = WarningSeverity.Low,
                Impact = "Auto-scaling will have no effect"
            });
        }

        // Recommendation for multi-AZ deployment
        if (deployment.MinReplicas < 3)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.MinReplicas",
                Message = "Deploy at least 3 nodes across multiple availability zones for high availability",
                Code = "EKS_MULTI_AZ",
                CurrentValue = deployment.MinReplicas.ToString(),
                RecommendedValue = "3 or more (1 per AZ)",
                Reason = "Ensures cluster remains available if an AZ fails",
                Benefit = "High availability and fault tolerance"
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
                Code = "EKS_INVALID_CPU_FORMAT",
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
                Code = "EKS_INVALID_MEMORY_FORMAT",
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
                    Code = "EKS_CPU_REQUEST_EXCEEDS_LIMIT",
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
                    Code = "EKS_MEMORY_REQUEST_EXCEEDS_LIMIT",
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
                    Code = "EKS_VPC_CIDR_REQUIRED"
                });
                result.IsValid = false;
            }

            // EKS requires at least 2 subnets in different AZs
            if (network.Subnets == null || network.Subnets.Count < 2)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.Subnets",
                    Message = "EKS requires at least 2 subnets in different availability zones",
                    Code = "EKS_INSUFFICIENT_SUBNETS",
                    CurrentValue = network.Subnets?.Count.ToString() ?? "0",
                    ExpectedValue = ">= 2 subnets",
                    DocumentationUrl = "https://docs.aws.amazon.com/eks/latest/userguide/network_reqs.html"
                });
                result.IsValid = false;
            }
        }
        else if (network.Mode == NetworkMode.UseExisting)
        {
            if (network.ExistingSubnets == null || network.ExistingSubnets.Count < 2)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.ExistingSubnets",
                    Message = "EKS requires at least 2 existing subnets in different availability zones",
                    Code = "EKS_INSUFFICIENT_EXISTING_SUBNETS",
                    CurrentValue = network.ExistingSubnets?.Count.ToString() ?? "0",
                    ExpectedValue = ">= 2 subnets"
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
                Message = "Enable Cluster Autoscaler for better resource utilization",
                Code = "EKS_ENABLE_AUTOSCALING",
                CurrentValue = "false",
                RecommendedValue = "true",
                Reason = "Cluster Autoscaler adjusts node count based on workload demand",
                Benefit = "Reduce costs during low-traffic periods and ensure capacity during peaks"
            });
        }

        // Recommend managed node groups
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "NodeGroupType",
            Message = "Use EKS Managed Node Groups for simplified operations",
            Code = "EKS_MANAGED_NODE_GROUPS",
            RecommendedValue = "Managed Node Groups",
            Reason = "AWS manages node provisioning, updates, and termination",
            Benefit = "Reduced operational overhead and improved security"
        });

        // Recommend Fargate for serverless pods
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "ComputeType",
            Message = "Consider AWS Fargate for serverless pod execution",
            Code = "EKS_FARGATE",
            RecommendedValue = "Fargate profiles for specific workloads",
            Reason = "Fargate eliminates node management for serverless workloads",
            Benefit = "Pay only for pod resources, no node management overhead"
        });

        // Recommend IRSA (IAM Roles for Service Accounts)
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "IAMAuthentication",
            Message = "Enable IRSA (IAM Roles for Service Accounts) for secure AWS access",
            Code = "EKS_IRSA",
            RecommendedValue = "true",
            Reason = "IRSA provides fine-grained IAM permissions for pods",
            Benefit = "Better security by eliminating static credentials"
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
