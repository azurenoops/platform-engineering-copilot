using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;

namespace Platform.Engineering.Copilot.Core.Services.Validation;

/// <summary>
/// Validator for Azure Container Apps configurations
/// </summary>
public class ContainerAppsConfigValidator : IConfigurationValidator
{
    public string PlatformName => "ContainerApps";

    public ValidationResult ValidateTemplate(TemplateGenerationRequest request)
    {
        var result = new ValidationResult
        {
            Platform = PlatformName
        };

        var startTime = DateTime.UtcNow;

        // Validate CPU and Memory configuration
        ValidateCpuMemoryConfiguration(request, result);

        // Validate replica scaling
        ValidateReplicaScaling(request, result);

        // Validate container configuration
        ValidateContainerConfiguration(request, result);

        // Validate ingress configuration
        ValidateIngressConfiguration(request, result);

        // Validate managed environment
        ValidateManagedEnvironment(request, result);

        // Validate networking
        ValidateNetworking(request, result);

        // Validate security
        ValidateSecurity(request, result);

        // Add recommendations
        AddRecommendations(request, result);

        result.ValidationTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        result.IsValid = result.Errors.Count == 0;

        return result;
    }

    private void ValidateCpuMemoryConfiguration(TemplateGenerationRequest request, ValidationResult result)
    {
        var resources = request.Deployment?.Resources;
        if (resources == null) return;

        // Valid CPU values: 0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.5, 3, 3.5, 4
        var validCpuValues = new[] { "0.25", "0.5", "0.75", "1", "1.25", "1.5", "1.75", "2", "2.5", "3", "3.5", "4", "250m", "500m", "750m", "1000m", "1250m", "1500m", "1750m", "2000m", "2500m", "3000m", "3500m", "4000m" };
        var cpuLimit = resources.CpuLimit?.Trim();

        if (!string.IsNullOrEmpty(cpuLimit) && !validCpuValues.Contains(cpuLimit))
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.Resources.CpuLimit",
                Message = $"Invalid CPU value '{cpuLimit}'. Valid values: 0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.5, 3, 3.5, 4 vCPU",
                Code = "CONTAINERAPPS_INVALID_CPU",
                CurrentValue = cpuLimit,
                ExpectedValue = "0.25, 0.5, 0.75, 1, 1.25, 1.5, 1.75, 2, 2.5, 3, 3.5, or 4"
            });
        }

        // Parse memory value (supports Gi, Mi format)
        var memoryLimit = resources.MemoryLimit?.Trim();
        if (!string.IsNullOrEmpty(memoryLimit))
        {
            double memoryGi = 0;
            if (memoryLimit.EndsWith("Gi", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(memoryLimit[..^2], out var gi))
                    memoryGi = gi;
            }
            else if (memoryLimit.EndsWith("Mi", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(memoryLimit[..^2], out var mi))
                    memoryGi = mi / 1024.0;
            }

            // Valid memory range: 0.5 Gi - 8 Gi
            if (memoryGi < 0.5 || memoryGi > 8)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Deployment.Resources.MemoryLimit",
                    Message = $"Memory must be between 0.5 Gi and 8 Gi. Current: {memoryLimit}",
                    Code = "CONTAINERAPPS_INVALID_MEMORY",
                    CurrentValue = memoryLimit,
                    ExpectedValue = "0.5Gi - 8Gi"
                });
            }

            // Validate CPU/Memory ratio (recommended)
            if (!string.IsNullOrEmpty(cpuLimit) && validCpuValues.Contains(cpuLimit))
            {
                // Parse CPU to double (handle both decimal and millicore formats)
                double cpu = 0;
                if (cpuLimit.EndsWith("m"))
                {
                    if (double.TryParse(cpuLimit[..^1], out var millicores))
                        cpu = millicores / 1000.0;
                }
                else
                {
                    double.TryParse(cpuLimit, out cpu);
                }

                // Recommended ratio: 2 GiB per vCPU (but flexible)
                var expectedMemory = cpu * 2;
                if (cpu > 0 && (memoryGi < cpu * 0.5 || memoryGi > cpu * 4))
                {
                    result.Warnings.Add(new ValidationWarning
                    {
                        Field = "Deployment.Resources.MemoryLimit",
                        Message = $"Memory-to-CPU ratio is outside recommended range. For {cpu} vCPU, recommended memory is {expectedMemory} GiB (range: {cpu * 0.5} - {cpu * 4} GiB)",
                        Code = "CONTAINERAPPS_MEMORY_CPU_RATIO",
                        Severity = WarningSeverity.Medium,
                        Impact = $"Current: {memoryGi} GiB for {cpu} vCPU"
                    });
                }
            }
        }
    }

    private void ValidateReplicaScaling(TemplateGenerationRequest request, ValidationResult result)
    {
        var deployment = request.Deployment;
        if (deployment == null) return;

        // Validate min replicas (0-30)
        if (deployment.MinReplicas < 0 || deployment.MinReplicas > 30)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MinReplicas",
                Message = $"Minimum replicas must be between 0 and 30. Current: {deployment.MinReplicas}",
                Code = "CONTAINERAPPS_INVALID_MIN_REPLICAS",
                CurrentValue = deployment.MinReplicas.ToString(),
                ExpectedValue = "0-30"
            });
        }

        // Validate max replicas (1-30)
        if (deployment.MaxReplicas < 1 || deployment.MaxReplicas > 30)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MaxReplicas",
                Message = $"Maximum replicas must be between 1 and 30. Current: {deployment.MaxReplicas}",
                Code = "CONTAINERAPPS_INVALID_MAX_REPLICAS",
                CurrentValue = deployment.MaxReplicas.ToString(),
                ExpectedValue = "1-30"
            });
        }

        // Min must be <= Max
        if (deployment.MinReplicas > deployment.MaxReplicas)
        {
            result.Errors.Add(new ValidationError
            {
                Field = "Deployment.MinReplicas",
                Message = $"Minimum replicas ({deployment.MinReplicas}) cannot be greater than maximum replicas ({deployment.MaxReplicas})",
                Code = "CONTAINERAPPS_MIN_GT_MAX_REPLICAS",
                CurrentValue = $"{deployment.MinReplicas} > {deployment.MaxReplicas}",
                ExpectedValue = $"MinReplicas <= MaxReplicas"
            });
        }

        // Warn if not using scale-to-zero capability
        if (deployment.MinReplicas > 0)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.MinReplicas",
                Message = "Consider setting minimum replicas to 0 to enable scale-to-zero and reduce costs when idle. Container Apps supports cold start optimization.",
                Code = "CONTAINERAPPS_SCALE_TO_ZERO",
                Benefit = "Cost Optimization - pay only when processing requests",
                CurrentValue = deployment.MinReplicas.ToString(),
                RecommendedValue = "0"
            });
        }
    }

    private void ValidateContainerConfiguration(TemplateGenerationRequest request, ValidationResult result)
    {
        // Container Apps requires container images
        // This is a general recommendation since the model doesn't have a specific container image field
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Infrastructure.ContainerImage",
            Message = "Container Apps requires a container image. Ensure you specify a valid container registry image (ACR, Docker Hub, etc.)",
            Code = "CONTAINERAPPS_REQUIRES_IMAGE",
            Benefit = "Required for deployment",
            RecommendedValue = "Use Azure Container Registry (ACR) for best integration: <registry-name>.azurecr.io/<image>:<tag>"
        });

        // Validate port configuration
        if (request.Application?.Port == null || request.Application.Port <= 0)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Application.Port",
                Message = "Container port not specified. Default port 80 will be used for ingress.",
                Code = "CONTAINERAPPS_MISSING_PORT",
                Severity = WarningSeverity.Low,
                Impact = "Application may not be accessible if it listens on a different port"
            });
        }
    }

    private void ValidateIngressConfiguration(TemplateGenerationRequest request, ValidationResult result)
    {
        // Recommend enabling ingress for web applications
        if (request.Application?.Type == ApplicationType.WebAPI || request.Application?.Type == ApplicationType.WebApp)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Infrastructure.EnableIngress",
                Message = "Enable external ingress for your API/Web App to receive HTTP/HTTPS traffic. Container Apps provides built-in ingress with automatic TLS termination.",
                Code = "CONTAINERAPPS_ENABLE_INGRESS",
                Benefit = "Accessibility - makes your application accessible from the internet",
                RecommendedValue = "Enable external ingress"
            });
        }

        // If private networking is enabled, recommend private ingress
        var networkConfig = request.Infrastructure?.NetworkConfig;
        if (networkConfig?.EnablePrivateEndpoint == true)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Infrastructure.IngressVisibility",
                Message = "Use internal ingress for private Container Apps accessible only from within your VNet.",
                Code = "CONTAINERAPPS_INTERNAL_INGRESS",
                Benefit = "Security - restricts access to VNet only",
                RecommendedValue = "Internal ingress visibility"
            });
        }
    }

    private void ValidateManagedEnvironment(TemplateGenerationRequest request, ValidationResult result)
    {
        // Container Apps requires a Managed Environment (workspace)
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Infrastructure.ManagedEnvironment",
            Message = "Container Apps runs in a Managed Environment. Consider sharing environments across multiple apps to reduce costs.",
            Code = "CONTAINERAPPS_SHARED_ENVIRONMENT",
            Benefit = "Cost Optimization - shared environment reduces base costs",
            RecommendedValue = "Use shared managed environment for related apps"
        });

        // Recommend using VNet-integrated environment for production
        var networkConfig = request.Infrastructure?.NetworkConfig;
        if (networkConfig == null || networkConfig.Mode == NetworkMode.CreateNew && string.IsNullOrWhiteSpace(networkConfig.VNetName))
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Infrastructure.NetworkConfig.VNetName",
                Message = "Use VNet integration for production Container Apps to enable private networking, network security groups, and integration with other Azure services.",
                Code = "CONTAINERAPPS_VNET_INTEGRATION",
                Benefit = "Security & Networking - enables private connectivity and network isolation",
                CurrentValue = "Public environment (consumption-only VNet)",
                RecommendedValue = "Customer-provided VNet with dedicated subnet"
            });
        }
    }

    private void ValidateNetworking(TemplateGenerationRequest request, ValidationResult result)
    {
        var networkConfig = request.Infrastructure?.NetworkConfig;
        if (networkConfig == null) return;

        // If VNet is specified, validate subnet
        if (networkConfig.Mode == NetworkMode.CreateNew && !string.IsNullOrWhiteSpace(networkConfig.VNetName))
        {
            if (networkConfig.Subnets == null || networkConfig.Subnets.Count == 0)
            {
                result.Errors.Add(new ValidationError
                {
                    Field = "Infrastructure.NetworkConfig.Subnets",
                    Message = "At least one subnet is required when using VNet integration with Container Apps.",
                    Code = "CONTAINERAPPS_MISSING_SUBNET",
                    CurrentValue = "No subnets configured",
                    ExpectedValue = "At least one subnet with /23 or larger CIDR"
                });
            }
            else
            {
                // Validate subnet size (requires /23 or larger for Container Apps)
                foreach (var subnet in networkConfig.Subnets)
                {
                    if (!string.IsNullOrWhiteSpace(subnet.AddressPrefix))
                    {
                        var prefix = subnet.AddressPrefix.Split('/').LastOrDefault();
                        if (int.TryParse(prefix, out var cidr))
                        {
                            if (cidr > 23)
                            {
                                result.Errors.Add(new ValidationError
                                {
                                    Field = $"Infrastructure.NetworkConfig.Subnets[{subnet.Name}].AddressPrefix",
                                    Message = $"Container Apps requires a subnet with /23 or larger CIDR block. Current: /{cidr}",
                                    Code = "CONTAINERAPPS_SUBNET_TOO_SMALL",
                                    CurrentValue = $"/{cidr}",
                                    ExpectedValue = "/23 or larger (e.g., /21, /20)"
                                });
                            }
                        }
                    }

                    // Subnet must be dedicated to Container Apps environment
                    result.Warnings.Add(new ValidationWarning
                    {
                        Field = $"Infrastructure.NetworkConfig.Subnets[{subnet.Name}]",
                        Message = "The subnet must be exclusively dedicated to the Container Apps environment. Do not share it with other Azure resources.",
                        Code = "CONTAINERAPPS_DEDICATED_SUBNET",
                        Severity = WarningSeverity.Medium,
                        Impact = "Sharing subnets can cause IP address exhaustion and deployment failures"
                    });
                }
            }
        }
    }

    private void ValidateSecurity(TemplateGenerationRequest request, ValidationResult result)
    {
        // Recommend managed identity
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Security.ManagedIdentity",
            Message = "Enable system-assigned or user-assigned managed identity to securely access Azure resources (Key Vault, Storage, etc.) without storing credentials.",
            Code = "CONTAINERAPPS_MANAGED_IDENTITY",
            Benefit = "Security - eliminates need for storing credentials",
            RecommendedValue = "Enable managed identity"
        });

        // Recommend using Key Vault for secrets
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Security.SecretsManagement",
            Message = "Store sensitive secrets in Azure Key Vault and reference them in Container Apps using managed identity.",
            Code = "CONTAINERAPPS_KEY_VAULT_SECRETS",
            Benefit = "Security - centralized secret management with audit logging",
            RecommendedValue = "Use Key Vault references"
        });

        // Recommend HTTPS-only for external ingress
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Security.HttpsOnly",
            Message = "Container Apps automatically provisions and renews TLS certificates for custom domains. Ensure HTTP to HTTPS redirect is enabled.",
            Code = "CONTAINERAPPS_HTTPS_ONLY",
            Benefit = "Security - encrypted communication",
            RecommendedValue = "Enable HTTPS-only with automatic TLS certificates"
        });
    }

    private void AddRecommendations(TemplateGenerationRequest request, ValidationResult result)
    {
        // Dapr integration
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Infrastructure.EnableDapr",
            Message = "Enable Dapr (Distributed Application Runtime) for microservices communication, state management, pub/sub, and more with zero code changes.",
            Code = "CONTAINERAPPS_ENABLE_DAPR",
            Benefit = "Microservices Capabilities - simplified distributed application patterns",
            RecommendedValue = "Enable Dapr for microservices patterns"
        });

        // Application Insights
        if (request.Observability?.ApplicationInsights != true)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Observability.ApplicationInsights",
                Message = "Enable Application Insights for comprehensive monitoring, distributed tracing, and log analytics.",
                Code = "CONTAINERAPPS_APP_INSIGHTS",
                Benefit = "Observability - detailed application performance monitoring",
                CurrentValue = "Disabled",
                RecommendedValue = "Enable Application Insights"
            });
        }

        // Log Analytics workspace
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Observability.LogAnalytics",
            Message = "Configure Log Analytics workspace for centralized logging, custom queries, and long-term log retention.",
            Code = "CONTAINERAPPS_LOG_ANALYTICS",
            Benefit = "Observability - centralized log management",
            RecommendedValue = "Use Log Analytics workspace"
        });

        // Health probes
        if (request.Application?.IncludeHealthCheck != true || request.Application?.IncludeReadinessProbe != true)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Application.HealthProbes",
                Message = "Configure liveness and readiness probes to ensure Container Apps can detect and restart unhealthy containers.",
                Code = "CONTAINERAPPS_HEALTH_PROBES",
                Benefit = "Reliability - automatic health monitoring and recovery",
                RecommendedValue = "Enable liveness and readiness probes"
            });
        }

        // Revision mode
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Deployment.RevisionMode",
            Message = "Use 'Multiple' revision mode for blue-green deployments, A/B testing, and traffic splitting. Use 'Single' for simple deployments.",
            Code = "CONTAINERAPPS_REVISION_MODE",
            Benefit = "Deployment Flexibility - enables advanced deployment strategies",
            RecommendedValue = "Multiple revision mode for production apps"
        });

        // Cost optimization
        if (request.Deployment?.MinReplicas > 0)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Deployment.MinReplicas",
                Message = "Container Apps uses consumption-based pricing. Enable scale-to-zero (min replicas = 0) to pay only when processing requests.",
                Code = "CONTAINERAPPS_CONSUMPTION_PRICING",
                Benefit = "Cost Optimization - eliminate idle costs",
                CurrentValue = request.Deployment.MinReplicas.ToString(),
                RecommendedValue = "0 (scale-to-zero)"
            });
        }

        // Container registry integration
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Infrastructure.ContainerRegistry",
            Message = "Use Azure Container Registry (ACR) with managed identity authentication for secure, fast container image pulls.",
            Code = "CONTAINERAPPS_ACR_INTEGRATION",
            Benefit = "Security & Performance - integrated authentication and geo-replication",
            RecommendedValue = "Use ACR with managed identity"
        });

        // Environment variables vs secrets
        if (request.Application?.EnvironmentVariables != null && request.Application.EnvironmentVariables.Count > 10)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Application.EnvironmentVariables",
                Message = $"You have {request.Application.EnvironmentVariables.Count} environment variables. Ensure sensitive values are stored as secrets, not environment variables.",
                Code = "CONTAINERAPPS_ENV_VS_SECRETS",
                Benefit = "Security - protect sensitive configuration",
                CurrentValue = $"{request.Application.EnvironmentVariables.Count} environment variables",
                RecommendedValue = "Move sensitive values to secrets"
            });
        }

        // Session affinity
        if (request.Deployment?.MaxReplicas > 1)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Infrastructure.SessionAffinity",
                Message = "Consider enabling session affinity (sticky sessions) if your application requires routing users to the same replica.",
                Code = "CONTAINERAPPS_SESSION_AFFINITY",
                Benefit = "Application Behavior - consistent routing for stateful applications",
                RecommendedValue = "Enable if stateful sessions are needed"
            });
        }
    }

    public bool IsValid(TemplateGenerationRequest request)
    {
        var result = ValidateTemplate(request);
        return result.IsValid;
    }
}
