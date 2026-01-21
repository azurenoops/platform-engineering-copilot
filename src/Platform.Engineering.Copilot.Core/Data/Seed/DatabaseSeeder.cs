using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Core.Data.Context;
using Platform.Engineering.Copilot.Core.Data.Entities;

namespace Platform.Engineering.Copilot.Core.Data.Seed;

/// <summary>
/// Database seeder for initial data
/// </summary>
public static class DatabaseSeeder
{
  /// <summary>
  /// Seed the database with initial data
  /// </summary>
  public static async Task SeedAsync(PlatformEngineeringCopilotContext context)
  {
    await context.Database.EnsureCreatedAsync();

    // Seed environment templates
    await SeedInfrastructureTemplatesAsync(context);

    // Seed service templates (for Admin UI)
    await SeedServiceTemplatesAsync(context);

    // Seed intent patterns
    await SeedIntentPatternsAsync(context);

    await context.SaveChangesAsync();
  }

  private static async Task SeedInfrastructureTemplatesAsync(PlatformEngineeringCopilotContext context)
  {
    if (await context.InfrastructureTemplates.AnyAsync())
      return;

    var templates = new[]
    {
            new InfrastructureTemplate
            {
                Id = Guid.NewGuid(),
                Name = "Basic Microservice",
                Description = "Basic microservice template with container app, database, and monitoring",
                TemplateType = "microservice",
                Version = "1.0.0",
                Format = "Bicep",
                DeploymentTier = "basic",
                MultiRegionSupported = false,
                DisasterRecoverySupported = false,
                HighAvailabilitySupported = false,
                Content = """
                {
                  "template": {
                    "containerApp": {
                      "cpu": "0.25",
                      "memory": "0.5Gi",
                      "replicas": { "min": 1, "max": 3 }
                    },
                    "database": {
                      "type": "sqlDatabase",
                      "tier": "Basic",
                      "size": "S0"
                    },
                    "monitoring": {
                      "applicationInsights": true,
                      "logAnalytics": true
                    }
                  }
                }
                """,
                Parameters = """
                {
                  "appName": { "type": "string", "required": true },
                  "environment": { "type": "string", "required": true, "allowedValues": ["dev", "staging", "prod"] },
                  "location": { "type": "string", "defaultValue": "eastus" }
                }
                """,
                Tags = """{"category": "microservice", "complexity": "basic"}""",
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
                IsPublic = true
            },
            new InfrastructureTemplate
            {
                Id = Guid.NewGuid(),
                Name = "Enterprise Web Application",
                Description = "Enterprise-grade web application with high availability, multi-region support, and advanced monitoring",
                TemplateType = "web-app",
                Version = "1.0.0",
                Format = "Bicep",
                DeploymentTier = "enterprise",
                MultiRegionSupported = true,
                DisasterRecoverySupported = true,
                HighAvailabilitySupported = true,
                Content = """
                {
                  "template": {
                    "webApp": {
                      "sku": "P3V3",
                      "instances": 3,
                      "autoscale": true
                    },
                    "database": {
                      "type": "sqlDatabase",
                      "tier": "Premium",
                      "size": "P2",
                      "geoReplication": true
                    },
                    "cdn": {
                      "enabled": true,
                      "caching": "aggressive"
                    },
                    "monitoring": {
                      "applicationInsights": true,
                      "logAnalytics": true,
                      "azureMonitor": true
                    }
                  }
                }
                """,
                Parameters = """
                {
                  "appName": { "type": "string", "required": true },
                  "environment": { "type": "string", "required": true, "allowedValues": ["staging", "prod"] },
                  "primaryLocation": { "type": "string", "defaultValue": "eastus" },
                  "secondaryLocation": { "type": "string", "defaultValue": "westus2" }
                }
                """,
                Tags = """{"category": "web-app", "complexity": "enterprise", "ha": "true"}""",
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
                IsPublic = true
            },
            new InfrastructureTemplate
            {
                Id = Guid.NewGuid(),
                Name = "ML Platform",
                Description = "Machine learning platform with compute clusters, storage, and MLOps pipeline",
                TemplateType = "ml-platform",
                Version = "1.0.0",
                Format = "Bicep",
                DeploymentTier = "premium",
                MultiRegionSupported = true,
                DisasterRecoverySupported = false,
                HighAvailabilitySupported = true,
                Content = """
                {
                  "template": {
                    "mlWorkspace": {
                      "sku": "Basic"
                    },
                    "computeCluster": {
                      "vmSize": "Standard_DS3_v2",
                      "minNodes": 0,
                      "maxNodes": 10
                    },
                    "storage": {
                      "type": "storageAccount",
                      "sku": "Standard_LRS",
                      "containers": ["data", "models", "experiments"]
                    },
                    "mlOps": {
                      "enabled": true,
                      "pipeline": "azureDevOps"
                    }
                  }
                }
                """,
                Parameters = """
                {
                  "workspaceName": { "type": "string", "required": true },
                  "environment": { "type": "string", "required": true, "allowedValues": ["dev", "staging", "prod"] },
                  "location": { "type": "string", "defaultValue": "eastus" },
                  "computeVmSize": { "type": "string", "defaultValue": "Standard_DS3_v2" }
                }
                """,
                Tags = """{"category": "ml-platform", "complexity": "premium", "compute": "true"}""",
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true,
                IsPublic = true
            }
        };

    await context.InfrastructureTemplates.AddRangeAsync(templates);
  }

  private static async Task SeedIntentPatternsAsync(PlatformEngineeringCopilotContext context)
  {
    if (await context.IntentPatterns.AnyAsync())
      return;

    var patterns = new[]
    {
            new IntentPattern
            {
                Id = Guid.NewGuid(),
                Pattern = @"(?i)create\s+(?:a\s+)?(?<type>aks|kubernetes|web\s*app|function|container)\s+(?:environment\s+)?(?:named\s+)?['""]?(?<name>[^'""]+)['""]?",
                IntentCategory = "environment_management",
                IntentAction = "create",
                Weight = 0.9m,
                ParameterExtractionRules = """
                {
                  "type": {"regex": "(?<type>aks|kubernetes|web\\s*app|function|container)", "mapping": {"kubernetes": "aks", "web app": "webapp", "container": "containerapp"}},
                  "name": {"regex": "(?:named\\s+)?['\"]?(?<name>[^'\"]+)['\"]?"}
                }
                """,
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new IntentPattern
            {
                Id = Guid.NewGuid(),
                Pattern = @"(?i)(?:list|show|get)\s+(?:all\s+)?(?:my\s+)?environments?(?:\s+in\s+(?<subscription>[^\s]+))?",
                IntentCategory = "environment_management",
                IntentAction = "list",
                Weight = 0.95m,
                ParameterExtractionRules = """
                {
                  "subscriptionId": {"regex": "(?:in\\s+(?<subscription>[^\\s]+))"}
                }
                """,
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new IntentPattern
            {
                Id = Guid.NewGuid(),
                Pattern = @"(?i)scale\s+(?<name>[^\s]+)\s+to\s+(?<replicas>\d+)\s+(?:replicas?|instances?)",
                IntentCategory = "environment_management",
                IntentAction = "scale",
                Weight = 0.9m,
                ParameterExtractionRules = """
                {
                  "name": {"regex": "scale\\s+(?<name>[^\\s]+)"},
                  "replicas": {"regex": "to\\s+(?<replicas>\\d+)"}
                }
                """,
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new IntentPattern
            {
                Id = Guid.NewGuid(),
                Pattern = @"(?i)delete\s+(?:environment\s+)?['""]?(?<name>[^'""]+)['""]?(?:\s+from\s+(?<resourceGroup>[^\s]+))?",
                IntentCategory = "environment_management",
                IntentAction = "delete",
                Weight = 0.85m,
                ParameterExtractionRules = """
                {
                  "name": {"regex": "delete\\s+(?:environment\\s+)?['\"]?(?<name>[^'\"]+)['\"]?"},
                  "resourceGroupName": {"regex": "(?:from\\s+(?<resourceGroup>[^\\s]+))"}
                }
                """,
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            },
            new IntentPattern
            {
                Id = Guid.NewGuid(),
                Pattern = @"(?i)deploy\s+(?<template>[^\s]+)\s+template(?:\s+as\s+(?<name>[^\s]+))?",
                IntentCategory = "environment_management",
                IntentAction = "template-deploy",
                Weight = 0.9m,
                ParameterExtractionRules = """
                {
                  "templateType": {"regex": "deploy\\s+(?<template>[^\\s]+)"},
                  "name": {"regex": "(?:as\\s+(?<name>[^\\s]+))"}
                }
                """,
                CreatedBy = "system",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            }
        };

    await context.IntentPatterns.AddRangeAsync(patterns);
  }

  private static async Task SeedServiceTemplatesAsync(PlatformEngineeringCopilotContext context)
  {
    if (await context.ServiceTemplates.AnyAsync())
      return;

    var templates = new[]
    {
            new ServiceTemplateEntity
            {
                Id = Guid.NewGuid(),
                Name = "aks-standard",
                DisplayName = "Standard AKS Cluster",
                Description = "Production-ready Azure Kubernetes Service cluster with autoscaling, monitoring, and security best practices.",
                Version = "2.0.0",
                Category = "Compute",
                Format = "Bicep",
                Status = "Published",
                MainTemplateContent = "",
                CreatedBy = "platform-team",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RequiresApproval = false,
                EnforceCompliance = true,
                ComplianceFrameworks = "NIST-800-53",
                Keywords = "kubernetes,aks,containers,k8s,cluster,microservices",
                UseCases = "Microservices,Container workloads,API hosting,Background workers",
                AiSelectionHint = "Use this template when user needs Kubernetes, AKS, container orchestration, or microservices platform",
                ApprovedBy = "platform-team",
                ApprovedAt = DateTime.UtcNow,
                ApprovalSource = "Internal",
                ParametersJson = """[{"Name":"clusterName","DisplayName":"Cluster Name","Description":"Name of the AKS cluster","Type":"String","Required":true},{"Name":"nodeCount","DisplayName":"Initial Node Count","Description":"Initial number of nodes","Type":"Number","DefaultValue":3},{"Name":"nodeSize","DisplayName":"Node Size","Type":"Choice","DefaultValue":"Standard_D4s_v5","AllowedValues":["Standard_D2s_v5","Standard_D4s_v5","Standard_D8s_v5"]}]"""
            },
            new ServiceTemplateEntity
            {
                Id = Guid.NewGuid(),
                Name = "webapp-standard",
                DisplayName = "Standard Web Application",
                Description = "Azure Web App with staging slot, Application Insights, and recommended security settings.",
                Version = "1.5.0",
                Category = "Web",
                Format = "Bicep",
                Status = "Published",
                MainTemplateContent = "",
                CreatedBy = "platform-team",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RequiresApproval = false,
                EnforceCompliance = true,
                ComplianceFrameworks = "NIST-800-53",
                Keywords = "web,webapp,app service,website,api,dotnet,node",
                UseCases = "Web APIs,Websites,.NET applications,Node.js apps",
                AiSelectionHint = "Use this for web applications, REST APIs, websites, or when user mentions App Service",
                ApprovedBy = "platform-team",
                ApprovedAt = DateTime.UtcNow,
                ApprovalSource = "Internal",
                ParametersJson = """[{"Name":"webAppName","DisplayName":"Web App Name","Type":"String","Required":true},{"Name":"sku","DisplayName":"SKU","Type":"String","DefaultValue":"S1"},{"Name":"linuxFxVersion","DisplayName":"Linux FX Version","Type":"String","DefaultValue":"php|7.4"}]""",
                GitRepositoryUrl = "https://github.com/Azure/azure-quickstart-templates",
                GitBranch = "master",
                GitPath = "quickstarts/microsoft.web/webapp-basic-linux/main.bicep",
                GitAutoSync = true,
                GitSyncIntervalMinutes = 15
            },
            new ServiceTemplateEntity
            {
                Id = Guid.NewGuid(),
                Name = "containerapp-standard",
                DisplayName = "Standard Container App",
                Description = "Azure Container App with autoscaling, ingress, and Dapr integration options.",
                Version = "1.2.0",
                Category = "Containers",
                Format = "Bicep",
                Status = "Published",
                MainTemplateContent = "",
                CreatedBy = "platform-team",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RequiresApproval = false,
                EnforceCompliance = true,
                ComplianceFrameworks = "NIST-800-53",
                Keywords = "container,containerapp,docker,serverless,dapr",
                UseCases = "Containerized apps,Serverless containers,Microservices with Dapr",
                AiSelectionHint = "Use for containerized applications when full Kubernetes is not needed",
                ApprovedBy = "platform-team",
                ApprovedAt = DateTime.UtcNow,
                ApprovalSource = "Internal",
                ParametersJson = """[{"Name":"appName","DisplayName":"Container App Name","Type":"String","Required":true},{"Name":"image","DisplayName":"Container Image","Type":"String","Required":true}]"""
            },
            new ServiceTemplateEntity
            {
                Id = Guid.NewGuid(),
                Name = "microservice-fullstack",
                DisplayName = "Full-Stack Microservice",
                Description = "Complete microservice environment with AKS, Azure SQL, Redis Cache, and Service Bus.",
                Version = "1.0.0",
                Category = "Composite",
                Format = "Bicep",
                Status = "Published",
                MainTemplateContent = "",
                CreatedBy = "platform-team",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RequiresApproval = true,
                EnforceCompliance = true,
                ComplianceFrameworks = "NIST-800-53",
                Keywords = "microservice,fullstack,complete,database,cache,messaging",
                UseCases = "Complete microservice stack,New applications,Production workloads",
                AiSelectionHint = "Use when user needs a complete environment with database, cache, and messaging",
                ParametersJson = """[{"Name":"serviceName","DisplayName":"Service Name","Type":"String","Required":true}]"""
            },
            new ServiceTemplateEntity
            {
                Id = Guid.NewGuid(),
                Name = "fedramp-high-environment",
                DisplayName = "FedRAMP High Compliant Environment",
                Description = "Environment pre-configured for FedRAMP High compliance with all required security controls.",
                Version = "1.0.0",
                Category = "Compliance",
                Format = "Bicep",
                Status = "Published",
                MainTemplateContent = "",
                CreatedBy = "security-team",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RequiresApproval = true,
                EnforceCompliance = true,
                ComplianceFrameworks = "FedRAMP-High,NIST-800-53",
                Keywords = "fedramp,compliance,government,security,high,nist",
                UseCases = "Government workloads,FedRAMP certification,High security requirements",
                AiSelectionHint = "Use for government, FedRAMP, or high-security compliance requirements",
                ApprovedBy = "security-team",
                ApprovedAt = DateTime.UtcNow,
                ApprovalSource = "Internal",
                ParametersJson = """[{"Name":"environmentName","DisplayName":"Environment Name","Type":"String","Required":true},{"Name":"systemName","DisplayName":"System Name","Type":"String","Required":true}]"""
            }
        };

    await context.ServiceTemplates.AddRangeAsync(templates);
  }
}