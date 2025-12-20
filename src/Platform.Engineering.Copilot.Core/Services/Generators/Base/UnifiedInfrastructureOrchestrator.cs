using System.Text;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Generators.Adapters;
using Platform.Engineering.Copilot.Core.Services.Generators.Storage;
using Platform.Engineering.Copilot.Core.Services.Generators.Database;
using Platform.Engineering.Copilot.Core.Services.Generators.Kubernetes;
using Platform.Engineering.Copilot.Core.Services.Generators.Infrastructure;
using Platform.Engineering.Copilot.Core.Services.Generators.KeyVault;
using Platform.Engineering.Copilot.Core.Services.Generators.AppService;
using Platform.Engineering.Copilot.Core.Services.Generators.Containers;
using Microsoft.Extensions.Logging;

namespace Platform.Engineering.Copilot.Core.Services.Generators.Base;

/// <summary>
/// Unified infrastructure orchestrator that manages all module generators
/// Supports both Terraform and Bicep, delegates to platform-specific generators
/// </summary>
public class UnifiedInfrastructureOrchestrator : IInfrastructureGenerator
{
    private readonly ILogger<UnifiedInfrastructureOrchestrator> _logger;
    private readonly List<IModuleGenerator> _moduleGenerators;

    public UnifiedInfrastructureOrchestrator(ILogger<UnifiedInfrastructureOrchestrator> logger)
    {
        _logger = logger;
        _moduleGenerators = new List<IModuleGenerator>();

        // Register all module generators
        RegisterModuleGenerators();
    }

    /// <summary>
    /// Registers all available module generators
    /// Uses IResourceModuleGenerator implementations directly (they implement IModuleGenerator)
    /// </summary>
    private void RegisterModuleGenerators()
    {
        // Register Terraform non-Azure generators (AWS/GCP) - still use adapters
        _moduleGenerators.Add(new TerraformECSModuleAdapter());
        _moduleGenerators.Add(new TerraformLambdaModuleAdapter());
        _moduleGenerators.Add(new TerraformCloudRunModuleAdapter());
        _moduleGenerators.Add(new TerraformEKSModuleAdapter());
        _moduleGenerators.Add(new TerraformGKEModuleAdapter());

        // Register Terraform Azure generators - use IResourceModuleGenerator implementations directly
        _moduleGenerators.Add(new TerraformAKSResourceModuleGenerator());
        _moduleGenerators.Add(new TerraformAppServiceResourceModuleGenerator());
        _moduleGenerators.Add(new TerraformContainerInstancesResourceModuleGenerator());
        _moduleGenerators.Add(new TerraformNetworkResourceModuleGenerator());
        _moduleGenerators.Add(new TerraformStorageResourceModuleGenerator());
        _moduleGenerators.Add(new TerraformSQLResourceModuleGenerator());
        _moduleGenerators.Add(new TerraformKeyVaultResourceModuleGenerator());

        // Register Bicep Azure generators - use IResourceModuleGenerator implementations directly
        _moduleGenerators.Add(new BicepAKSResourceModuleGenerator());
        _moduleGenerators.Add(new BicepAppServiceResourceModuleGenerator());
        _moduleGenerators.Add(new BicepContainerAppsResourceModuleGenerator());
        _moduleGenerators.Add(new BicepNetworkResourceModuleGenerator());
        _moduleGenerators.Add(new BicepStorageAccountModuleGenerator());
        _moduleGenerators.Add(new BicepSQLModuleGenerator());
        _moduleGenerators.Add(new BicepKeyVaultModuleGenerator());

        _logger.LogInformation("Registered {Count} module generators", _moduleGenerators.Count);
    }

    public async Task<Dictionary<string, string>> GenerateAsync(
        TemplateGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, string>();
        var format = request.Infrastructure?.Format ?? InfrastructureFormat.Terraform;

        _logger.LogInformation(
            "Generating infrastructure for {Platform} on {Provider} using {Format}",
            request.Infrastructure?.ComputePlatform,
            request.Infrastructure?.Provider,
            format);

        // Find the appropriate module generator
        var generator = FindModuleGenerator(request);

        if (generator == null)
        {
            _logger.LogWarning(
                "No module generator found for {Platform} on {Provider} with {Format}",
                request.Infrastructure?.ComputePlatform,
                request.Infrastructure?.Provider,
                format);

            // Fall back to basic generation
            return await GenerateFallbackAsync(request, cancellationToken);
        }

        // Generate module files using the specialized generator
        var moduleFiles = await Task.Run(() => generator.GenerateModule(request), cancellationToken);

        foreach (var file in moduleFiles)
        {
            files[file.Key] = file.Value;
        }

        _logger.LogInformation(
            "Generated {Count} module files using {GeneratorType}",
            moduleFiles.Count,
            generator.GetType().Name);

        // Generate root orchestration files based on format
        if (generator == null)
        {
            var rootFiles = format switch
            {
                InfrastructureFormat.Terraform => GenerateTerraformRootFiles(request),
                InfrastructureFormat.Bicep => GenerateBicepRootFiles(request),
                _ => new Dictionary<string, string>()
            };

            foreach (var file in rootFiles)
            {
                files[file.Key] = file.Value;
            }

            _logger.LogInformation(
                "Generated {TotalCount} total infrastructure files ({ModuleCount} module, {RootCount} root)",
                files.Count,
                moduleFiles.Count,
                rootFiles.Count);
        }
        return files;
    }

    /// <summary>
    /// Finds the appropriate module generator for the request
    /// </summary>
    private IModuleGenerator? FindModuleGenerator(TemplateGenerationRequest request)
    {
        return _moduleGenerators.FirstOrDefault(g => g.CanGenerate(request));
    }

    /// <summary>
    /// Generates Terraform root orchestration files (main.tf, providers.tf, etc.)
    /// </summary>
    private Dictionary<string, string> GenerateTerraformRootFiles(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        files["infra/main.tf"] = GenerateTerraformMain(request);
        files["infra/variables.tf"] = GenerateTerraformVariables(request);
        files["infra/outputs.tf"] = GenerateTerraformOutputs(request);
        files["infra/providers.tf"] = GenerateTerraformProviders(request);
        files["infra/terraform.tfvars"] = GenerateTerraformTfvars(request);

        return files;
    }

    /// <summary>
    /// Generates Bicep root orchestration files (main.bicep, parameters files, etc.)
    /// </summary>
    private Dictionary<string, string> GenerateBicepRootFiles(TemplateGenerationRequest request)
    {
        var files = new Dictionary<string, string>();

        files["infra/main.bicep"] = GenerateBicepMain(request);
        files["infra/main.parameters.json"] = GenerateBicepParameters(request);

        return files;
    }

    /// <summary>
    /// Generates fallback infrastructure when no specialized generator is available
    /// </summary>
    private async Task<Dictionary<string, string>> GenerateFallbackAsync(
        TemplateGenerationRequest request,
        CancellationToken cancellationToken)
    {
        var files = new Dictionary<string, string>();
        var format = request.Infrastructure?.Format ?? InfrastructureFormat.Terraform;

        _logger.LogWarning("Using fallback generation for {Format}", format);

        if (format == InfrastructureFormat.Terraform)
        {
            files["infra/main.tf"] = GenerateFallbackTerraform(request);
        }
        else if (format == InfrastructureFormat.Bicep)
        {
            files["infra/main.bicep"] = GenerateFallbackBicep(request);
        }

        return await Task.FromResult(files);
    }

    #region Terraform Root File Generation

    private string GenerateTerraformMain(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var platform = infrastructure.ComputePlatform;

        sb.AppendLine($"# Main Terraform configuration for {serviceName}");
        sb.AppendLine($"# Provider: {infrastructure.Provider}");
        sb.AppendLine($"# Region: {infrastructure.Region}");
        sb.AppendLine($"# Platform: {platform}");
        sb.AppendLine();

        sb.AppendLine("locals {");
        sb.AppendLine($"  service_name = var.service_name");
        sb.AppendLine($"  environment  = var.environment");
        sb.AppendLine($"  location     = var.location");
        sb.AppendLine($"  common_tags = {{");
        sb.AppendLine($"    Service     = local.service_name");
        sb.AppendLine($"    Environment = local.environment");
        sb.AppendLine($"    ManagedBy   = \"terraform\"");
        sb.AppendLine($"  }}");
        sb.AppendLine("}");
        sb.AppendLine();

        // Reference the module based on platform
        var modulePath = GetModulePath(platform);
        var moduleName = GetModuleName(platform);

        sb.AppendLine($"# {platform} Module");
        sb.AppendLine($"module \"{moduleName}\" {{");
        sb.AppendLine($"  source = \"{modulePath}\"");
        sb.AppendLine();
        sb.AppendLine("  # Basic configuration");

        if (infrastructure.Provider == CloudProvider.Azure)
        {
            sb.AppendLine("  cluster_name        = local.service_name");
            sb.AppendLine("  location            = local.location");
            sb.AppendLine("  resource_group_name = azurerm_resource_group.main.name");
            sb.AppendLine("  environment         = local.environment");
            sb.AppendLine("  tags                = local.common_tags");
        }
        else if (infrastructure.Provider == CloudProvider.AWS)
        {
            sb.AppendLine("  cluster_name = local.service_name");
            sb.AppendLine("  region       = local.location");
            sb.AppendLine("  environment  = local.environment");
            sb.AppendLine("  tags         = local.common_tags");
        }
        else if (infrastructure.Provider == CloudProvider.GCP)
        {
            sb.AppendLine("  project_id   = var.gcp_project_id");
            sb.AppendLine("  cluster_name = local.service_name");
            sb.AppendLine("  region       = local.location");
            sb.AppendLine("  environment  = local.environment");
            sb.AppendLine("  labels       = local.common_tags");
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Add resource group for Azure
        if (infrastructure.Provider == CloudProvider.Azure)
        {
            sb.AppendLine("# Resource Group");
            sb.AppendLine("resource \"azurerm_resource_group\" \"main\" {");
            sb.AppendLine("  name     = \"rg-${local.service_name}\"");
            sb.AppendLine("  location = local.location");
            sb.AppendLine("  tags     = local.common_tags");
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private string GenerateTerraformVariables(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        sb.AppendLine("# Variables for infrastructure deployment");
        sb.AppendLine();
        sb.AppendLine("variable \"service_name\" {");
        sb.AppendLine("  description = \"Name of the service\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{serviceName}\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"environment\" {");
        sb.AppendLine("  description = \"Environment (dev, staging, prod)\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine("  default     = \"dev\"");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("variable \"location\" {");
        sb.AppendLine("  description = \"Cloud region for deployment\"");
        sb.AppendLine("  type        = string");
        sb.AppendLine($"  default     = \"{infrastructure.Region ?? "us-east-1"}\"");
        sb.AppendLine("}");

        if (infrastructure.Provider == CloudProvider.GCP)
        {
            sb.AppendLine();
            sb.AppendLine("variable \"gcp_project_id\" {");
            sb.AppendLine("  description = \"GCP project ID\"");
            sb.AppendLine("  type        = string");
            sb.AppendLine("}");
        }

        return sb.ToString();
    }

    private string GenerateTerraformOutputs(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();
        var moduleName = GetModuleName(infrastructure.ComputePlatform);

        sb.AppendLine("# Outputs from infrastructure deployment");
        sb.AppendLine();

        // Platform-specific outputs
        switch (infrastructure.ComputePlatform)
        {
            case ComputePlatform.EKS:
            case ComputePlatform.GKE:
            case ComputePlatform.AKS:
            case ComputePlatform.Kubernetes:
                sb.AppendLine("output \"cluster_name\" {");
                sb.AppendLine($"  value = module.{moduleName}.cluster_name");
                sb.AppendLine("}");
                sb.AppendLine();
                sb.AppendLine("output \"cluster_endpoint\" {");
                sb.AppendLine($"  value     = module.{moduleName}.cluster_endpoint");
                sb.AppendLine("  sensitive = true");
                sb.AppendLine("}");
                break;

            case ComputePlatform.ECS:
                sb.AppendLine("output \"cluster_name\" {");
                sb.AppendLine($"  value = module.{moduleName}.cluster_name");
                sb.AppendLine("}");
                sb.AppendLine();
                sb.AppendLine("output \"service_name\" {");
                sb.AppendLine($"  value = module.{moduleName}.service_name");
                sb.AppendLine("}");
                break;

            case ComputePlatform.Lambda:
                sb.AppendLine("output \"function_name\" {");
                sb.AppendLine($"  value = module.{moduleName}.function_name");
                sb.AppendLine("}");
                sb.AppendLine();
                sb.AppendLine("output \"function_arn\" {");
                sb.AppendLine($"  value = module.{moduleName}.function_arn");
                sb.AppendLine("}");
                break;

            case ComputePlatform.CloudRun:
                sb.AppendLine("output \"service_url\" {");
                sb.AppendLine($"  value = module.{moduleName}.service_url");
                sb.AppendLine("}");
                break;
        }

        return sb.ToString();
    }

    private string GenerateTerraformProviders(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        sb.AppendLine("terraform {");
        sb.AppendLine("  required_version = \">= 1.6.0\"");
        sb.AppendLine();
        sb.AppendLine("  required_providers {");

        switch (infrastructure.Provider)
        {
            case CloudProvider.Azure:
                sb.AppendLine("    azurerm = {");
                sb.AppendLine("      source  = \"hashicorp/azurerm\"");
                sb.AppendLine("      version = \"~> 3.80\"");
                sb.AppendLine("    }");
                break;

            case CloudProvider.AWS:
                sb.AppendLine("    aws = {");
                sb.AppendLine("      source  = \"hashicorp/aws\"");
                sb.AppendLine("      version = \"~> 5.0\"");
                sb.AppendLine("    }");
                break;

            case CloudProvider.GCP:
                sb.AppendLine("    google = {");
                sb.AppendLine("      source  = \"hashicorp/google\"");
                sb.AppendLine("      version = \"~> 5.0\"");
                sb.AppendLine("    }");
                break;
        }

        sb.AppendLine("  }");
        sb.AppendLine("}");
        sb.AppendLine();

        // Provider configuration
        switch (infrastructure.Provider)
        {
            case CloudProvider.Azure:
                sb.AppendLine("provider \"azurerm\" {");
                sb.AppendLine("  features {}");
                sb.AppendLine("}");
                break;

            case CloudProvider.AWS:
                sb.AppendLine("provider \"aws\" {");
                sb.AppendLine("  region = var.location");
                sb.AppendLine("}");
                break;

            case CloudProvider.GCP:
                sb.AppendLine("provider \"google\" {");
                sb.AppendLine("  project = var.gcp_project_id");
                sb.AppendLine("  region  = var.location");
                sb.AppendLine("}");
                break;
        }

        return sb.ToString();
    }

    private string GenerateTerraformTfvars(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        sb.AppendLine($"service_name = \"{serviceName}\"");
        sb.AppendLine("environment  = \"dev\"");
        sb.AppendLine($"location     = \"{infrastructure.Region ?? "us-east-1"}\"");

        if (infrastructure.Provider == CloudProvider.GCP)
        {
            sb.AppendLine("gcp_project_id = \"your-project-id\"  # Update with your GCP project ID");
        }

        return sb.ToString();
    }

    private string GenerateFallbackTerraform(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Fallback Terraform configuration for {request.ServiceName}");
        sb.AppendLine($"# Platform: {request.Infrastructure?.ComputePlatform}");
        sb.AppendLine($"# Provider: {request.Infrastructure?.Provider}");
        sb.AppendLine();
        sb.AppendLine("# TODO: Implement specialized generator for this platform");
        return sb.ToString();
    }

    #endregion

    #region Bicep Root File Generation

    private string GenerateBicepMain(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        var serviceName = request.ServiceName ?? "service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        sb.AppendLine($"// Main Bicep configuration for {serviceName}");
        sb.AppendLine($"// Provider: {infrastructure.Provider}");
        sb.AppendLine($"// Region: {infrastructure.Region}");
        sb.AppendLine();

        sb.AppendLine("targetScope = 'resourceGroup'");
        sb.AppendLine();

        sb.AppendLine("@description('Name of the service')");
        sb.AppendLine($"param serviceName string = '{serviceName}'");
        sb.AppendLine();
        sb.AppendLine("@description('Environment name')");
        sb.AppendLine("param environment string = 'dev'");
        sb.AppendLine();
        sb.AppendLine("@description('Azure region for resources')");
        sb.AppendLine($"param location string = '{infrastructure.Region ?? "eastus"}'");
        sb.AppendLine();

        sb.AppendLine("// Common tags");
        sb.AppendLine("var commonTags = {");
        sb.AppendLine("  Service: serviceName");
        sb.AppendLine("  Environment: environment");
        sb.AppendLine("  ManagedBy: 'bicep'");
        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("// TODO: Add module references");

        return sb.ToString();
    }

    private string GenerateBicepParameters(TemplateGenerationRequest request)
    {
        var serviceName = request.ServiceName ?? "service";
        var infrastructure = request.Infrastructure ?? new InfrastructureSpec();

        return $$"""
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "serviceName": {
      "value": "{{serviceName}}"
    },
    "environment": {
      "value": "dev"
    },
    "location": {
      "value": "{{infrastructure.Region ?? "eastus"}}"
    }
  }
}
""";
    }

    private string GenerateFallbackBicep(TemplateGenerationRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Fallback Bicep configuration for {request.ServiceName}");
        sb.AppendLine($"// Platform: {request.Infrastructure?.ComputePlatform}");
        sb.AppendLine();
        sb.AppendLine("// TODO: Implement specialized Bicep generator for this platform");
        return sb.ToString();
    }

    #endregion

    #region Helper Methods

    private string GetModulePath(ComputePlatform platform)
    {
        return platform switch
        {
            ComputePlatform.EKS => "./modules/eks",
            ComputePlatform.GKE => "./modules/gke",
            ComputePlatform.AKS => "./modules/aks",
            ComputePlatform.ECS => "./modules/ecs",
            ComputePlatform.Lambda => "./modules/lambda",
            ComputePlatform.CloudRun => "./modules/cloud_run",
            ComputePlatform.AppService => "./modules/app_service",
            ComputePlatform.ContainerApps => "./modules/container_apps",
            _ => "./modules/compute"
        };
    }

    private string GetModuleName(ComputePlatform platform)
    {
        return platform switch
        {
            ComputePlatform.EKS => "eks",
            ComputePlatform.GKE => "gke",
            ComputePlatform.AKS => "aks",
            ComputePlatform.ECS => "ecs",
            ComputePlatform.Lambda => "lambda",
            ComputePlatform.CloudRun => "cloud_run",
            ComputePlatform.AppService => "app_service",
            ComputePlatform.ContainerApps => "container_apps",
            _ => "compute"
        };
    }

    #endregion
}
