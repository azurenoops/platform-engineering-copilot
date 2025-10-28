using Microsoft.EntityFrameworkCore;
using Platform.Engineering.Copilot.Data.Context;
using Platform.Engineering.Copilot.Data.Entities;

namespace Platform.Engineering.Copilot.Data.Seed;

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
        await SeedEnvironmentTemplatesAsync(context);

        // Seed intent patterns
        await SeedIntentPatternsAsync(context);

        await context.SaveChangesAsync();
    }

    private static async Task SeedEnvironmentTemplatesAsync(PlatformEngineeringCopilotContext context)
    {
        if (await context.EnvironmentTemplates.AnyAsync())
            return;

        var templates = new[]
        {
            new EnvironmentTemplate
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
            new EnvironmentTemplate
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
            new EnvironmentTemplate
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

        await context.EnvironmentTemplates.AddRangeAsync(templates);
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
}