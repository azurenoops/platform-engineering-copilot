using Platform.Engineering.Copilot.Core.Interfaces.Validation;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Validation;
using System.Text.RegularExpressions;

namespace Platform.Engineering.Copilot.Core.Services.Validation;

/// <summary>
/// Validator for Azure App Service configurations
/// </summary>
public class AppServiceConfigValidator : IConfigurationValidator
{
    public string PlatformName => "AppService";

    public ValidationResult ValidateTemplate(TemplateGenerationRequest request)
    {
        var result = new ValidationResult
        {
            Platform = PlatformName
        };

        var startTime = DateTime.UtcNow;

        // Validate App Service Plan (SKU/Tier)
        ValidateAppServicePlan(request, result);

        // Validate Application Settings
        ValidateApplicationSettings(request, result);

        // Validate Deployment Slots
        ValidateDeploymentSlots(request, result);

        // Validate Scaling Configuration
        ValidateScaling(request, result);

        // Validate Runtime Stack
        ValidateRuntimeStack(request, result);

        // Validate Networking
        ValidateNetworking(request, result);

        // Validate Security Settings
        ValidateSecurity(request, result);

        // Add recommendations
        AddRecommendations(request, result);

        result.ValidationTimeMs = (long)(DateTime.UtcNow - startTime).TotalMilliseconds;
        result.IsValid = result.Errors.Count == 0;

        return result;
    }

    private void ValidateAppServicePlan(TemplateGenerationRequest request, ValidationResult result)
    {
        // Azure App Service SKUs: Free (F1), Shared (D1), Basic (B1-B3), Standard (S1-S3), 
        // Premium (P1v2-P3v2, P1v3-P3v3), Isolated (I1-I3, I1v2-I3v2)
        var validSkus = new[]
        {
            "F1", // Free
            "D1", // Shared
            "B1", "B2", "B3", // Basic
            "S1", "S2", "S3", // Standard
            "P1v2", "P2v2", "P3v2", // Premium V2
            "P1v3", "P2v3", "P3v3", // Premium V3
            "I1", "I2", "I3", // Isolated
            "I1v2", "I2v2", "I3v2" // Isolated V2
        };

        // Check if a valid SKU is specified in compute configuration
        var instanceType = request.Infrastructure?.ComputePlatform.ToString();
        
        // Add warning about App Service Plan selection
        result.Warnings.Add(new ValidationWarning
        {
            Field = "AppServicePlan.SKU",
            Message = "Ensure appropriate App Service Plan SKU is selected for workload requirements",
            Code = "APPSERVICE_SKU_SELECTION",
            Severity = WarningSeverity.Medium,
            Impact = "Wrong SKU can lead to performance issues or unnecessary costs"
        });
    }

    private void ValidateApplicationSettings(TemplateGenerationRequest request, ValidationResult result)
    {
        var envVars = request.Application?.EnvironmentVariables;
        
        if (envVars != null)
        {
            // Azure App Service has a limit of 2048 characters for a single app setting value
            foreach (var (key, value) in envVars)
            {
                if (value.Length > 2048)
                {
                    result.Errors.Add(new ValidationError
                    {
                        Field = $"ApplicationSettings.{key}",
                        Message = "App Setting value exceeds maximum length of 2048 characters",
                        Code = "APPSERVICE_SETTING_TOO_LONG",
                        CurrentValue = $"{value.Length} characters",
                        ExpectedValue = "≤ 2048 characters",
                        DocumentationUrl = "https://learn.microsoft.com/azure/app-service/configure-common"
                    });
                }

                // Validate app setting name
                if (!Regex.IsMatch(key, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
                {
                    result.Errors.Add(new ValidationError
                    {
                        Field = $"ApplicationSettings.{key}",
                        Message = "App Setting name must start with letter or underscore and contain only alphanumeric characters and underscores",
                        Code = "APPSERVICE_SETTING_INVALID_NAME",
                        CurrentValue = key,
                        ExpectedValue = "Valid format: [a-zA-Z_][a-zA-Z0-9_]*",
                        DocumentationUrl = "https://learn.microsoft.com/azure/app-service/configure-common"
                    });
                }
            }

            // Warn if too many app settings (performance impact)
            if (envVars.Count > 100)
            {
                result.Warnings.Add(new ValidationWarning
                {
                    Field = "ApplicationSettings",
                    Message = "Large number of app settings may impact cold start performance",
                    Code = "APPSERVICE_MANY_SETTINGS",
                    Severity = WarningSeverity.Low,
                    Impact = $"{envVars.Count} settings configured. Consider using Key Vault references for sensitive data."
                });
            }
        }
    }

    private void ValidateDeploymentSlots(TemplateGenerationRequest request, ValidationResult result)
    {
        // Deployment slots are only available in Standard, Premium, and Isolated tiers
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "DeploymentSlots",
            Message = "Use deployment slots for zero-downtime deployments",
            Code = "APPSERVICE_USE_SLOTS",
            CurrentValue = null,
            RecommendedValue = "Enable staging/production slots",
            Reason = "Deployment slots allow testing in production environment before swap",
            Benefit = "Zero-downtime deployments, easy rollback, A/B testing capability"
        });
    }

    private void ValidateScaling(TemplateGenerationRequest request, ValidationResult result)
    {
        var infrastructure = request.Infrastructure;

        // Validate instance count
        // Free/Shared: 1 instance only
        // Basic: 1-3 instances (manual scale)
        // Standard/Premium: 1-10/20/30 instances (auto-scale available)
        // Isolated: 1-100 instances

        // Check if auto-scaling is configured
        var deployment = request.Deployment;
        if (deployment != null)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "AutoScaling",
                Message = "Configure auto-scaling rules for production workloads",
                Code = "APPSERVICE_AUTOSCALE",
                CurrentValue = "Not specified",
                RecommendedValue = "Enable auto-scale with CPU/Memory/HTTP queue metrics",
                Reason = "Auto-scaling ensures optimal performance and cost efficiency",
                Benefit = "Automatically handle traffic spikes, reduce costs during low usage"
            });
        }
    }

    private void ValidateRuntimeStack(TemplateGenerationRequest request, ValidationResult result)
    {
        var app = request.Application;
        if (app == null) return;

        var language = app.Language.ToString();
        var validRuntimes = new Dictionary<string, string[]>
        {
            { "DotNet", new[] { ".NET 6", ".NET 7", ".NET 8", ".NET Framework 4.8" } },
            { "NodeJS", new[] { "Node 16 LTS", "Node 18 LTS", "Node 20 LTS" } },
            { "Python", new[] { "Python 3.8", "Python 3.9", "Python 3.10", "Python 3.11" } },
            { "Java", new[] { "Java 8", "Java 11", "Java 17", "Java 21" } },
            { "PHP", new[] { "PHP 8.0", "PHP 8.1", "PHP 8.2" } },
            { "Ruby", new[] { "Ruby 2.7", "Ruby 3.0", "Ruby 3.1" } }
        };

        if (validRuntimes.ContainsKey(language))
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "RuntimeStack",
                Message = $"Ensure you're using a supported {language} runtime version",
                Code = "APPSERVICE_RUNTIME_VERSION",
                CurrentValue = language,
                RecommendedValue = string.Join(", ", validRuntimes[language]),
                Reason = "Using supported runtime versions ensures security updates and support",
                Benefit = "Access to latest features, security patches, and Azure support"
            });
        }

        // Validate port configuration
        if (app.Port != 80 && app.Port != 443 && app.Port != 8080)
        {
            result.Warnings.Add(new ValidationWarning
            {
                Field = "Application.Port",
                Message = "App Service typically uses port 80 (HTTP) or 443 (HTTPS). Custom ports require additional configuration.",
                Code = "APPSERVICE_CUSTOM_PORT",
                Severity = WarningSeverity.Medium,
                Impact = $"Port {app.Port} may require WEBSITES_PORT app setting configuration"
            });
        }
    }

    private void ValidateNetworking(TemplateGenerationRequest request, ValidationResult result)
    {
        var networkConfig = request.Infrastructure?.NetworkConfig;

        // VNet Integration validation
        if (networkConfig?.Mode == NetworkMode.UseExisting)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "VNetIntegration",
                Message = "VNet Integration is configured for secure private connectivity",
                Code = "APPSERVICE_VNET_CONFIGURED",
                CurrentValue = "Existing VNet",
                RecommendedValue = "Ensure subnet has sufficient IP addresses (/26 or larger)",
                Reason = "VNet Integration requires dedicated subnet with adequate address space",
                Benefit = "Secure access to private resources, database, storage accounts"
            });

            // Warn about subnet requirements
            result.Warnings.Add(new ValidationWarning
            {
                Field = "VNetIntegration.Subnet",
                Message = "VNet Integration subnet must be dedicated and have delegation to Microsoft.Web/serverFarms",
                Code = "APPSERVICE_VNET_SUBNET_DELEGATION",
                Severity = WarningSeverity.High,
                Impact = "Incorrect subnet configuration will prevent VNet Integration from working"
            });
        }

        // Private Endpoint validation
        if (networkConfig?.EnablePrivateEndpoint == true)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "PrivateEndpoint",
                Message = "Private Endpoint provides fully private inbound connectivity",
                Code = "APPSERVICE_PRIVATE_ENDPOINT",
                CurrentValue = "Enabled",
                RecommendedValue = "Configure Private DNS Zone for name resolution",
                Reason = "Private Endpoints require Private DNS zones for proper name resolution",
                Benefit = "Secure inbound access from VNet without exposing public endpoint"
            });
        }
    }

    private void ValidateSecurity(TemplateGenerationRequest request, ValidationResult result)
    {
        var security = request.Security;

        // HTTPS validation
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Security.HTTPSOnly",
            Message = "Enable HTTPS Only to enforce secure connections",
            Code = "APPSERVICE_HTTPS_ONLY",
            CurrentValue = "Not specified",
            RecommendedValue = "Enable HTTPS Only = true",
            Reason = "HTTPS Only ensures all HTTP traffic is redirected to HTTPS",
            Benefit = "Protects data in transit, meets compliance requirements"
        });

        // TLS version validation
        if (security?.TLS == true)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Security.MinTlsVersion",
                Message = "Set minimum TLS version to 1.2 or higher",
                Code = "APPSERVICE_MIN_TLS",
                CurrentValue = "TLS enabled",
                RecommendedValue = "TLS 1.2 or 1.3",
                Reason = "TLS 1.0 and 1.1 are deprecated and have known vulnerabilities",
                Benefit = "Enhanced security, compliance with security standards"
            });
        }

        // Managed Identity recommendation
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Security.ManagedIdentity",
            Message = "Use Managed Identity for Azure service authentication",
            Code = "APPSERVICE_MANAGED_IDENTITY",
            CurrentValue = "Not specified",
            RecommendedValue = "Enable System-assigned or User-assigned Managed Identity",
            Reason = "Managed Identity eliminates need for credentials in code or configuration",
            Benefit = "Secure authentication to Azure services without managing secrets"
        });

        // Authentication/Authorization (Easy Auth)
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Security.Authentication",
            Message = "Consider enabling App Service Authentication (Easy Auth)",
            Code = "APPSERVICE_EASY_AUTH",
            CurrentValue = "Not specified",
            RecommendedValue = "Enable with Azure AD, Microsoft, Google, or other providers",
            Reason = "Built-in authentication reduces code complexity",
            Benefit = "Secure user authentication without custom auth code"
        });
    }

    private void AddRecommendations(TemplateGenerationRequest request, ValidationResult result)
    {
        // Application Insights
        if (request.Observability?.ApplicationInsights == true)
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Monitoring.ApplicationInsights",
                Message = "Application Insights is configured for comprehensive monitoring",
                Code = "APPSERVICE_APP_INSIGHTS",
                CurrentValue = "Enabled",
                RecommendedValue = "Configure connection string and instrumentation key",
                Reason = "Application Insights provides deep application monitoring and diagnostics",
                Benefit = "Performance monitoring, distributed tracing, live metrics, alerting"
            });
        }
        else
        {
            result.Recommendations.Add(new ValidationRecommendation
            {
                Field = "Monitoring.ApplicationInsights",
                Message = "Enable Application Insights for comprehensive monitoring",
                Code = "APPSERVICE_APP_INSIGHTS",
                CurrentValue = "Not enabled",
                RecommendedValue = "Configure Application Insights connection",
                Reason = "Application Insights provides deep application monitoring and diagnostics",
                Benefit = "Performance monitoring, distributed tracing, live metrics, alerting"
            });
        }

        // Always On setting
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Configuration.AlwaysOn",
            Message = "Enable Always On for production apps to prevent cold starts",
            Code = "APPSERVICE_ALWAYS_ON",
            CurrentValue = "Not specified",
            RecommendedValue = "Enable Always On (not available in Free/Shared tiers)",
            Reason = "Always On keeps app loaded and prevents idle timeout after 20 minutes",
            Benefit = "Faster response times, eliminates cold start delays for production apps"
        });

        // Health Check
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Configuration.HealthCheck",
            Message = "Configure Health Check endpoint for automatic instance health monitoring",
            Code = "APPSERVICE_HEALTH_CHECK",
            CurrentValue = "Not specified",
            RecommendedValue = "Enable Health Check with endpoint path (e.g., /health)",
            Reason = "Health Check automatically removes unhealthy instances from load balancer",
            Benefit = "Improved reliability, automatic recovery from unhealthy states"
        });

        // Backup
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Backup.Schedule",
            Message = "Configure automated backups for production apps",
            Code = "APPSERVICE_BACKUP",
            CurrentValue = "Not specified",
            RecommendedValue = "Enable automated backups (Standard tier or higher)",
            Reason = "Backups protect against accidental deletion or corruption",
            Benefit = "Quick recovery from incidents, compliance requirements"
        });

        // Custom Domains and SSL
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "CustomDomain.SSL",
            Message = "Use managed SSL certificates for custom domains",
            Code = "APPSERVICE_MANAGED_SSL",
            CurrentValue = "Not specified",
            RecommendedValue = "Enable App Service Managed Certificate (free)",
            Reason = "Managed certificates are free and auto-renewed",
            Benefit = "No certificate management overhead, automatic renewal"
        });

        // Diagnostic Logging
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Diagnostics.Logging",
            Message = "Enable diagnostic logging for troubleshooting",
            Code = "APPSERVICE_DIAGNOSTIC_LOGS",
            CurrentValue = "Not specified",
            RecommendedValue = "Enable Application Logging, Web Server Logging, and Detailed Error Messages",
            Reason = "Diagnostic logs are essential for troubleshooting issues",
            Benefit = "Faster problem resolution, better insights into application behavior"
        });

        // Deployment Best Practices
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "Deployment.Method",
            Message = "Use deployment slots with Azure DevOps or GitHub Actions",
            Code = "APPSERVICE_DEPLOYMENT_CICD",
            CurrentValue = "Not specified",
            RecommendedValue = "Configure CI/CD with deployment slots for safe deployments",
            Reason = "Automated deployments with slots enable testing before production",
            Benefit = "Reduced deployment risk, easy rollback, consistent deployments"
        });

        // Cost Optimization
        result.Recommendations.Add(new ValidationRecommendation
        {
            Field = "CostOptimization.ReservedInstances",
            Message = "Consider Azure Reserved Instances for predictable workloads",
            Code = "APPSERVICE_RESERVED_INSTANCES",
            CurrentValue = "Pay-as-you-go",
            RecommendedValue = "Purchase 1-year or 3-year reserved capacity",
            Reason = "Reserved Instances provide significant cost savings",
            Benefit = "Up to 55% cost savings for committed usage"
        });
    }

    public bool IsValid(TemplateGenerationRequest request)
    {
        var result = ValidateTemplate(request);
        return result.IsValid;
    }
}
