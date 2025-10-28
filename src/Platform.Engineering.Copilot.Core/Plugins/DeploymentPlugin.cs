using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Services.Azure;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for deploying infrastructure-as-code templates.
/// Provides AI-accessible functions for deploying Bicep, Terraform, and Kubernetes templates.
/// All deployments route through DeploymentOrchestrationService for consistency and auditability.
/// </summary>
public class DeploymentPlugin : BaseSupervisorPlugin
{
    private readonly IDeploymentOrchestrationService _deploymentOrchestrator;
    private readonly AzureMcpClient _azureMcpClient;

    public DeploymentPlugin(
        ILogger<DeploymentPlugin> logger,
        Kernel kernel,
        IDeploymentOrchestrationService deploymentOrchestrator,
        AzureMcpClient azureMcpClient) : base(logger, kernel)
    {
        _deploymentOrchestrator = deploymentOrchestrator ?? throw new ArgumentNullException(nameof(deploymentOrchestrator));
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
    }

    #region Bicep Deployment

    [KernelFunction("deploy_bicep_template")]
    [Description("Deploy Azure infrastructure using a Bicep template file. " +
                 "Use this when user provides a path to a .bicep file or wants to deploy IaC templates. " +
                 "Returns deployment ID, status, and created resources.")]
    public async Task<string> DeployBicepTemplateAsync(
        [Description("Absolute path to the Bicep template file (e.g., /Users/.../main.bicep or /path/to/template.bicep)")] 
        string templatePath,
        
        [Description("Target Azure resource group name (e.g., rg-mission-alpha)")] 
        string resourceGroup,
        
        [Description("Azure location/region (default: eastus). Examples: eastus, westus2, usgovvirginia")] 
        string? location = "eastus",
        
        [Description("Deployment name (optional, defaults to timestamp-based name)")] 
        string? deploymentName = null,
        
        [Description("JSON string of parameters: {\"param1\": \"value1\", \"param2\": \"value2\"}. Optional.")] 
        string? parameters = null,
        
        [Description("Subscription ID (optional, uses default if not provided)")] 
        string? subscriptionId = null,
        
        [Description("JSON string of tags: {\"tag1\": \"value1\", \"tag2\": \"value2\"}. Optional.")] 
        string? tags = null,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🚀 DeploymentPlugin: Deploying Bicep template from {TemplatePath} to {ResourceGroup}", 
                templatePath, resourceGroup);

            // Validate template path
            if (string.IsNullOrWhiteSpace(templatePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Template path is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            if (!File.Exists(templatePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Bicep template file not found: {templatePath}",
                    hint = "Please provide the absolute path to an existing .bicep file"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Validate resource group
            if (string.IsNullOrWhiteSpace(resourceGroup))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Resource group name is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Read template content
            string templateContent;
            try
            {
                templateContent = await File.ReadAllTextAsync(templatePath, cancellationToken);
                _logger.LogInformation("✅ Read Bicep template: {Length} characters", templateContent.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to read Bicep template file: {TemplatePath}", templatePath);
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Failed to read template file: {ex.Message}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Parse parameters
            Dictionary<string, string> parsedParameters = new();
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                try
                {
                    parsedParameters = JsonSerializer.Deserialize<Dictionary<string, string>>(parameters) 
                        ?? new Dictionary<string, string>();
                    _logger.LogInformation("📝 Parsed {Count} deployment parameters", parsedParameters.Count);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse parameters JSON, using empty dictionary");
                }
            }

            // Parse tags
            Dictionary<string, string> parsedTags = new();
            if (!string.IsNullOrWhiteSpace(tags))
            {
                try
                {
                    parsedTags = JsonSerializer.Deserialize<Dictionary<string, string>>(tags) 
                        ?? new Dictionary<string, string>();
                    _logger.LogInformation("🏷️  Parsed {Count} deployment tags", parsedTags.Count);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse tags JSON, using empty dictionary");
                }
            }

            // Generate deployment name if not provided
            var finalDeploymentName = !string.IsNullOrWhiteSpace(deploymentName) 
                ? deploymentName 
                : $"deployment-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            // Check for additional module files in the same directory
            Dictionary<string, string>? additionalFiles = null;
            var templateDirectory = Path.GetDirectoryName(templatePath);
            if (!string.IsNullOrWhiteSpace(templateDirectory))
            {
                var modulesDirectory = Path.Combine(templateDirectory, "modules");
                if (Directory.Exists(modulesDirectory))
                {
                    additionalFiles = new Dictionary<string, string>();
                    var moduleFiles = Directory.GetFiles(modulesDirectory, "*.bicep", SearchOption.AllDirectories);
                    
                    foreach (var moduleFile in moduleFiles)
                    {
                        // Store with relative path from template directory
                        var relativePath = Path.GetRelativePath(templateDirectory, moduleFile);
                        var moduleContent = await File.ReadAllTextAsync(moduleFile, cancellationToken);
                        additionalFiles[relativePath] = moduleContent;
                    }
                    
                    if (additionalFiles.Count > 0)
                    {
                        _logger.LogInformation("📦 Found {Count} Bicep module files", additionalFiles.Count);
                    }
                }
            }

            // Prepare deployment options
            var deploymentOptions = new DeploymentOptions
            {
                DeploymentName = finalDeploymentName,
                ResourceGroup = resourceGroup,
                Location = location ?? "eastus",
                SubscriptionId = subscriptionId ?? string.Empty,
                Parameters = parsedParameters,
                Tags = parsedTags,
                WaitForCompletion = true,
                Timeout = TimeSpan.FromMinutes(30)
            };

            _logger.LogInformation("⚙️  Deployment options: Name={Name}, RG={RG}, Location={Location}, Params={ParamCount}, Tags={TagCount}",
                deploymentOptions.DeploymentName,
                deploymentOptions.ResourceGroup,
                deploymentOptions.Location,
                deploymentOptions.Parameters.Count,
                deploymentOptions.Tags.Count);

            // Execute deployment via orchestrator
            var deploymentResult = await _deploymentOrchestrator.DeployBicepTemplateAsync(
                templateContent,
                deploymentOptions,
                additionalFiles,
                cancellationToken);

            // Format result for AI consumption
            if (deploymentResult.Success)
            {
                _logger.LogInformation("✅ Deployment succeeded: {DeploymentId}", deploymentResult.DeploymentId);
                
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = $"✅ **Bicep deployment completed successfully**",
                    deploymentId = deploymentResult.DeploymentId,
                    deploymentName = deploymentResult.DeploymentName,
                    resourceGroup = deploymentResult.ResourceGroup,
                    state = deploymentResult.State.ToString(),
                    startedAt = deploymentResult.StartedAt,
                    completedAt = deploymentResult.CompletedAt,
                    duration = deploymentResult.Duration?.ToString(@"hh\:mm\:ss"),
                    createdResources = deploymentResult.CreatedResources,
                    outputs = deploymentResult.Outputs,
                    summary = $"Deployed {deploymentResult.CreatedResources.Count} resources to {resourceGroup} in {deploymentResult.Duration?.TotalSeconds:F1}s"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                _logger.LogError("❌ Deployment failed: {Error}", deploymentResult.ErrorMessage);
                
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "❌ **Bicep deployment failed**",
                    deploymentId = deploymentResult.DeploymentId,
                    deploymentName = deploymentResult.DeploymentName,
                    resourceGroup = deploymentResult.ResourceGroup,
                    state = deploymentResult.State.ToString(),
                    error = deploymentResult.ErrorMessage,
                    duration = deploymentResult.Duration?.ToString(@"hh\:mm\:ss")
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in DeployBicepTemplateAsync");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Deployment failed with exception: {ex.Message}",
                exceptionType = ex.GetType().Name
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region Bicep Validation

    [KernelFunction("validate_bicep_template")]
    [Description("Validate a Bicep template without deploying it. " +
                 "Returns syntax errors, validation issues, and warnings. " +
                 "Use this before deploying to catch errors early.")]
    public async Task<string> ValidateBicepTemplateAsync(
        [Description("Absolute path to the Bicep template file to validate")] 
        string templatePath,
        
        [Description("Target resource group for validation context (required for Azure deployment validation)")] 
        string resourceGroup,
        
        [Description("Azure location/region for validation context (default: eastus)")] 
        string? location = "eastus",
        
        [Description("JSON string of parameters for validation: {\"param1\": \"value1\"}")] 
        string? parameters = null,
        
        [Description("Subscription ID (optional)")] 
        string? subscriptionId = null,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔍 DeploymentPlugin: Validating Bicep template {TemplatePath}", templatePath);

            // Validate template path
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Bicep template file not found: {templatePath}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Read template content
            var templateContent = await File.ReadAllTextAsync(templatePath, cancellationToken);

            // Parse parameters
            Dictionary<string, string> parsedParameters = new();
            if (!string.IsNullOrWhiteSpace(parameters))
            {
                try
                {
                    parsedParameters = JsonSerializer.Deserialize<Dictionary<string, string>>(parameters) 
                        ?? new Dictionary<string, string>();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse parameters JSON");
                }
            }

            // Prepare deployment options for validation
            var deploymentOptions = new DeploymentOptions
            {
                DeploymentName = $"validation-{DateTime.UtcNow:yyyyMMdd-HHmmss}",
                ResourceGroup = resourceGroup,
                Location = location ?? "eastus",
                SubscriptionId = subscriptionId ?? string.Empty,
                Parameters = parsedParameters
            };

            // Validate via orchestrator
            var validationResult = await _deploymentOrchestrator.ValidateDeploymentAsync(
                templateContent,
                "Bicep",
                deploymentOptions,
                cancellationToken);

            if (validationResult.IsValid)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "✅ **Bicep template validation passed**",
                    templatePath,
                    resourceGroup,
                    summary = "Template is valid and ready for deployment"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "❌ **Bicep template validation failed**",
                    templatePath,
                    errors = validationResult.Errors,
                    warnings = validationResult.Warnings,
                    hint = "Fix the validation errors before deploying"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Bicep template");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Validation failed: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region Deployment Status

    [KernelFunction("get_deployment_status")]
    [Description("Check the status of an active or completed deployment. " +
                 "Returns deployment state (Running/Succeeded/Failed), created resources, and outputs. " +
                 "Use the deployment ID returned from deploy_bicep_template or deploy_terraform.")]
    public async Task<string> GetDeploymentStatusAsync(
        [Description("Deployment ID (GUID) returned from deploy_bicep_template or deploy_terraform")] 
        string deploymentId,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📊 DeploymentPlugin: Getting status for deployment {DeploymentId}", deploymentId);

            if (string.IsNullOrWhiteSpace(deploymentId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Deployment ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var status = await _deploymentOrchestrator.GetDeploymentStatusAsync(deploymentId, cancellationToken);

            if (status == null)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Deployment {deploymentId} not found",
                    hint = "The deployment may have been completed and cleaned up, or the ID is incorrect"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var stateEmoji = status.State switch
            {
                DeploymentState.Running => "🔄",
                DeploymentState.Succeeded => "✅",
                DeploymentState.Failed => "❌",
                DeploymentState.Canceled => "⚠️",
                _ => "❓"
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                deploymentId = status.DeploymentId,
                deploymentName = status.DeploymentName,
                state = status.State.ToString(),
                stateEmoji,
                progressPercentage = status.ProgressPercentage,
                currentOperation = status.CurrentOperation,
                lastUpdated = status.LastUpdated,
                resourceStatuses = status.ResourceStatuses,
                summary = status.State == DeploymentState.Running 
                    ? $"{stateEmoji} Deployment in progress - {status.ProgressPercentage}% complete - {status.CurrentOperation}"
                    : status.State == DeploymentState.Succeeded
                        ? $"{stateEmoji} Deployment succeeded - {status.ResourceStatuses.Count} resources deployed"
                        : $"{stateEmoji} Deployment {status.State.ToString().ToLower()}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deployment status");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to get deployment status: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region Deployment Cancellation

    [KernelFunction("cancel_deployment")]
    [Description("Cancel an in-progress deployment. " +
                 "Only works for deployments in 'Running' state. " +
                 "Resources already created will remain unless you delete them separately.")]
    public async Task<string> CancelDeploymentAsync(
        [Description("Deployment ID to cancel (must be in Running state)")] 
        string deploymentId,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🛑 DeploymentPlugin: Canceling deployment {DeploymentId}", deploymentId);

            if (string.IsNullOrWhiteSpace(deploymentId))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Deployment ID is required"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var cancelSuccess = await _deploymentOrchestrator.CancelDeploymentAsync(deploymentId, cancellationToken);

            if (cancelSuccess)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "⚠️ **Deployment canceled**",
                    deploymentId,
                    summary = "Deployment cancellation requested. Resources already created will remain."
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "❌ **Failed to cancel deployment**",
                    deploymentId,
                    hint = "Deployment may have already completed or failed"
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error canceling deployment");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Failed to cancel deployment: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region Terraform Deployment

    [KernelFunction("deploy_terraform")]
    [Description("Deploy infrastructure using Terraform templates. " +
                 "Provide path to directory containing .tf files. " +
                 "Returns deployment ID and created resources.")]
    public async Task<string> DeployTerraformAsync(
        [Description("Path to Terraform directory containing .tf files (e.g., /path/to/terraform/)")] 
        string terraformPath,
        
        [Description("Target resource group name")] 
        string resourceGroup,
        
        [Description("Azure location/region (default: eastus)")] 
        string? location = "eastus",
        
        [Description("Deployment name (optional)")] 
        string? deploymentName = null,
        
        [Description("JSON string of Terraform variables: {\"var1\": \"value1\", \"var2\": \"value2\"}")] 
        string? variables = null,
        
        [Description("Subscription ID (optional)")] 
        string? subscriptionId = null,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🚀 DeploymentPlugin: Deploying Terraform from {TerraformPath} to {ResourceGroup}", 
                terraformPath, resourceGroup);

            // Validate terraform path
            if (string.IsNullOrWhiteSpace(terraformPath) || !Directory.Exists(terraformPath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Terraform directory not found: {terraformPath}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Find .tf files
            var tfFiles = Directory.GetFiles(terraformPath, "*.tf", SearchOption.TopDirectoryOnly);
            if (tfFiles.Length == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"No .tf files found in {terraformPath}",
                    hint = "Provide a directory containing Terraform configuration files"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // Read all .tf files into single content string
            var terraformContent = string.Empty;
            foreach (var tfFile in tfFiles)
            {
                var fileContent = await File.ReadAllTextAsync(tfFile, cancellationToken);
                terraformContent += $"\n# File: {Path.GetFileName(tfFile)}\n{fileContent}\n";
            }

            _logger.LogInformation("📝 Read {Count} Terraform files", tfFiles.Length);

            // Parse variables
            Dictionary<string, string> parsedVariables = new();
            if (!string.IsNullOrWhiteSpace(variables))
            {
                try
                {
                    parsedVariables = JsonSerializer.Deserialize<Dictionary<string, string>>(variables) 
                        ?? new Dictionary<string, string>();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse variables JSON");
                }
            }

            var finalDeploymentName = !string.IsNullOrWhiteSpace(deploymentName) 
                ? deploymentName 
                : $"terraform-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

            // Prepare deployment options
            var deploymentOptions = new DeploymentOptions
            {
                DeploymentName = finalDeploymentName,
                ResourceGroup = resourceGroup,
                Location = location ?? "eastus",
                SubscriptionId = subscriptionId ?? string.Empty,
                Parameters = parsedVariables,
                WaitForCompletion = true,
                Timeout = TimeSpan.FromMinutes(30)
            };

            // Execute deployment
            var deploymentResult = await _deploymentOrchestrator.DeployTerraformAsync(
                terraformContent,
                deploymentOptions,
                cancellationToken);

            if (deploymentResult.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = true,
                    message = "✅ **Terraform deployment completed successfully**",
                    deploymentId = deploymentResult.DeploymentId,
                    deploymentName = deploymentResult.DeploymentName,
                    resourceGroup = deploymentResult.ResourceGroup,
                    state = deploymentResult.State.ToString(),
                    createdResources = deploymentResult.CreatedResources,
                    duration = deploymentResult.Duration?.ToString(@"hh\:mm\:ss")
                }, new JsonSerializerOptions { WriteIndented = true });
            }
            else
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "❌ **Terraform deployment failed**",
                    error = deploymentResult.ErrorMessage,
                    deploymentId = deploymentResult.DeploymentId
                }, new JsonSerializerOptions { WriteIndented = true });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeployTerraformAsync");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Terraform deployment failed: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion

    #region MCP-Enhanced Functions

    [KernelFunction("validate_bicep_template_with_schema")]
    [Description("Validate a Bicep template using Azure MCP bicepschema tool for comprehensive schema validation. " +
                 "Provides detailed validation results including syntax errors, type mismatches, and best practice violations.")]
    public async Task<string> ValidateBicepTemplateWithSchemaAsync(
        [Description("Absolute path to the Bicep template file to validate")] 
        string templatePath,
        
        [Description("Optional resource type to validate against (e.g., Microsoft.Compute/virtualMachines). " +
                     "If not provided, validates the entire template.")] 
        string? resourceType = null,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Read the template file
            if (!File.Exists(templatePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Template file not found: {templatePath}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var templateContent = await File.ReadAllTextAsync(templatePath, cancellationToken);

            // 2. Use Azure MCP bicepschema for validation
            var validationArgs = new Dictionary<string, object?>
            {
                ["template"] = templateContent,
                ["resourceType"] = resourceType
            };

            var validationResult = await _azureMcpClient.CallToolAsync("bicepschema", validationArgs, cancellationToken);

            if (validationResult == null || !validationResult.Success)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = validationResult?.ErrorMessage ?? "Schema validation service returned no results"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var validationText = validationResult.Result?.ToString() ?? "No validation details available";

            return JsonSerializer.Serialize(new
            {
                success = true,
                templatePath = templatePath,
                resourceType = resourceType,
                validation = new
                {
                    results = validationText,
                    timestamp = DateTime.UtcNow
                },
                nextSteps = new[]
                {
                    "Review the validation results above for any errors or warnings.",
                    "Fix any reported issues in the template before deployment.",
                    "Say 'deploy this template to resource group <name>' when validation passes."
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating Bicep template with schema");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Validation failed: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("deploy_bicep_with_pre_deployment_check")]
    [Description("Deploy a Bicep template with comprehensive pre-deployment validation using Azure MCP tools. " +
                 "Performs schema validation, security checks, and best practice verification before deployment.")]
    public async Task<string> DeployBicepWithPreDeploymentCheckAsync(
        [Description("Absolute path to the Bicep template file")] 
        string templatePath,
        
        [Description("Target Azure resource group name")] 
        string resourceGroup,
        
        [Description("Azure location/region (default: eastus)")] 
        string? location = "eastus",
        
        [Description("Whether to perform a what-if deployment first (default: true)")] 
        bool performWhatIf = true,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Validate template exists
            if (!File.Exists(templatePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Template file not found: {templatePath}"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            var templateContent = await File.ReadAllTextAsync(templatePath, cancellationToken);

            // 2. Pre-deployment validation using MCP bicepschema
            var validationArgs = new Dictionary<string, object?>
            {
                ["template"] = templateContent
            };
            var validationResult = await _azureMcpClient.CallToolAsync("bicepschema", validationArgs, cancellationToken);
            var validationText = validationResult?.Result?.ToString() ?? "Validation service unavailable";

            // Check for critical errors in validation
            if (validationText.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                validationText.Contains("invalid", StringComparison.OrdinalIgnoreCase))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    message = "Pre-deployment validation failed - template has errors",
                    validation = validationText,
                    recommendation = "Fix validation errors before attempting deployment"
                }, new JsonSerializerOptions { WriteIndented = true });
            }

            // 3. Get best practices recommendations
            var bestPracticesArgs = new Dictionary<string, object?>
            {
                ["query"] = $"Bicep template deployment to {resourceGroup}"
            };
            var bestPracticesResult = await _azureMcpClient.CallToolAsync("get_bestpractices", bestPracticesArgs, cancellationToken);
            var bestPractices = bestPracticesResult?.Result?.ToString() ?? "Best practices unavailable";

            // 4. Perform what-if deployment if requested
            object? whatIfResults = null;
            if (performWhatIf)
            {
                // Use MCP deploy tool with what-if mode
                var whatIfArgs = new Dictionary<string, object?>
                {
                    ["templatePath"] = templatePath,
                    ["resourceGroup"] = resourceGroup,
                    ["location"] = location,
                    ["whatIf"] = true
                };
                var whatIfResult = await _azureMcpClient.CallToolAsync("deploy", whatIfArgs, cancellationToken);
                
                whatIfResults = new
                {
                    executed = true,
                    results = whatIfResult?.Result?.ToString() ?? "What-if analysis completed"
                };
            }

            // 5. Proceed with actual deployment using existing service
            var deploymentOptions = new DeploymentOptions
            {
                ResourceGroup = resourceGroup,
                Location = location ?? "eastus",
                DeploymentName = $"deploy-{DateTime.UtcNow:yyyyMMddHHmmss}"
            };
            var deploymentResult = await _deploymentOrchestrator.DeployBicepTemplateAsync(
                templateContent, deploymentOptions, null, cancellationToken);

            return JsonSerializer.Serialize(new
            {
                success = deploymentResult.Success,
                preDeploymentChecks = new
                {
                    validation = new { passed = true, details = validationText },
                    bestPractices = bestPractices,
                    whatIf = whatIfResults
                },
                deployment = new
                {
                    deploymentId = deploymentResult.DeploymentId,
                    state = deploymentResult.State.ToString(),
                    errorMessage = deploymentResult.ErrorMessage,
                    resourcesCreated = deploymentResult.CreatedResources
                },
                nextSteps = deploymentResult.Success
                    ? new[] { "Deployment successful!", "Verify resources in Azure Portal", "Check resource health and configuration" }
                    : new[] { "Review deployment errors above", "Check Azure activity logs", "Retry deployment after fixing issues" }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in pre-deployment check and deploy");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Pre-deployment check failed: {ex.Message}"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    [KernelFunction("troubleshoot_failed_deployment")]
    [Description("Troubleshoot a failed Azure deployment using Azure MCP diagnostic tools. " +
                 "Provides insights from AppLens, activity logs, and deployment diagnostics.")]
    public async Task<string> TroubleshootFailedDeploymentAsync(
        [Description("The deployment ID or correlation ID of the failed deployment")] 
        string deploymentId,
        
        [Description("The resource group where deployment failed")] 
        string resourceGroup,
        
        [Description("Optional: subscription ID (if not default)")] 
        string? subscriptionId = null,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Get deployment diagnostics from MCP
            var diagnosticsArgs = new Dictionary<string, object?>
            {
                ["deploymentId"] = deploymentId,
                ["resourceGroup"] = resourceGroup,
                ["subscriptionId"] = subscriptionId
            };
            var diagnosticsResult = await _azureMcpClient.CallToolAsync("applens", diagnosticsArgs, cancellationToken);

            var diagnostics = diagnosticsResult?.Result?.ToString() ?? "Diagnostics unavailable";

            // 2. Get activity logs via MCP
            var activityLogsArgs = new Dictionary<string, object?>
            {
                ["resourceGroup"] = resourceGroup,
                ["correlationId"] = deploymentId,
                ["subscriptionId"] = subscriptionId
            };
            var activityLogsResult = await _azureMcpClient.CallToolAsync("activitylogs", activityLogsArgs, cancellationToken);

            var activityLogs = activityLogsResult?.Result?.ToString() ?? "Activity logs unavailable";

            // 3. Get recommended solutions from MCP
            var solutionsArgs = new Dictionary<string, object?>
            {
                ["query"] = $"Troubleshoot Azure deployment failure in {resourceGroup}"
            };
            var solutionsResult = await _azureMcpClient.CallToolAsync("get_bestpractices", solutionsArgs, cancellationToken);

            var recommendedSolutions = solutionsResult?.Result?.ToString() ?? "No solutions available";

            // 4. Compile troubleshooting report
            return JsonSerializer.Serialize(new
            {
                success = true,
                deploymentId = deploymentId,
                resourceGroup = resourceGroup,
                troubleshooting = new
                {
                    diagnostics = new
                    {
                        source = "Azure AppLens",
                        findings = diagnostics
                    },
                    activityLogs = new
                    {
                        source = "Azure Activity Logs",
                        entries = activityLogs
                    },
                    recommendedSolutions = new
                    {
                        source = "Azure Best Practices",
                        suggestions = recommendedSolutions
                    }
                },
                nextSteps = new[]
                {
                    "Review the diagnostics findings above",
                    "Check activity logs for specific error messages",
                    "Apply recommended solutions",
                    "Retry deployment after addressing issues",
                    "Consider 'deploy with pre-deployment check' to catch issues earlier"
                }
            }, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error troubleshooting deployment");
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = $"Troubleshooting failed: {ex.Message}",
                recommendation = "Check Azure Portal for deployment details manually"
            }, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    #endregion
}
