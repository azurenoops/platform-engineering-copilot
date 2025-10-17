using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using global::Azure.ResourceManager;
using global::Azure.ResourceManager.Resources;
using global::Azure.ResourceManager.Resources.Models;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services.Deployment
{
    /// <summary>
    /// Service for orchestrating infrastructure deployments across cloud providers.
    /// Executes Bicep, Terraform, and Kubernetes deployments.
    /// </summary>
    public class DeploymentOrchestrationService : IDeploymentOrchestrationService
    {
        private readonly ILogger<DeploymentOrchestrationService> _logger;
        private readonly IAzureResourceService _azureResourceService;
        private readonly Dictionary<string, DeploymentExecutionContext> _activeDeployments = new();
        private readonly object _lock = new();

        public DeploymentOrchestrationService(
            ILogger<DeploymentOrchestrationService> logger,
            IAzureResourceService azureResourceService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _azureResourceService = azureResourceService ?? throw new ArgumentNullException(nameof(azureResourceService));
        }

        #region Bicep Deployment

        public async Task<DeploymentResult> DeployBicepTemplateAsync(
            string templateContent,
            DeploymentOptions options,
            Dictionary<string, string>? additionalFiles = null,
            CancellationToken cancellationToken = default)
        {
            var deploymentId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting Bicep deployment {DeploymentId} to {ResourceGroup}", 
                deploymentId, options.ResourceGroup);

            var result = new DeploymentResult
            {
                DeploymentId = deploymentId,
                DeploymentName = options.DeploymentName,
                ResourceGroup = options.ResourceGroup,
                State = DeploymentState.Running,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                // FIX: Extract actual template content if it's been incorrectly serialized as a Template object
                templateContent = ExtractActualTemplateContent(templateContent);

                // Track deployment
                var context = new DeploymentExecutionContext
                {
                    DeploymentId = deploymentId,
                    StartedAt = DateTime.UtcNow,
                    State = DeploymentState.Running,
                    Logs = new List<LogEntry>()
                };

                lock (_lock)
                {
                    _activeDeployments[deploymentId] = context;
                }

                AddLog(context, "Info", $"Starting Bicep deployment to {options.ResourceGroup}");

                // Create temporary directory for Bicep template and modules
                var tempDir = Path.Combine(Path.GetTempPath(), "bicep-deployments", deploymentId);
                Directory.CreateDirectory(tempDir);
                
                // Write main template file
                var templateFile = Path.Combine(tempDir, "main.bicep");
                await File.WriteAllTextAsync(templateFile, templateContent, cancellationToken);
                AddLog(context, "Info", $"Bicep template written to {templateFile}");

                // Write additional Bicep module files if provided (for multi-file templates)
                if (additionalFiles != null && additionalFiles.Count > 0)
                {
                    _logger.LogInformation("Writing {Count} additional Bicep module files", additionalFiles.Count);
                    foreach (var (fileName, content) in additionalFiles)
                    {
                        var moduleFile = Path.Combine(tempDir, fileName);
                        
                        // Create subdirectories if the file path contains them
                        var moduleDir = Path.GetDirectoryName(moduleFile);
                        if (!string.IsNullOrEmpty(moduleDir))
                        {
                            Directory.CreateDirectory(moduleDir);
                        }
                        
                        await File.WriteAllTextAsync(moduleFile, content, cancellationToken);
                        _logger.LogDebug("Written module file: {FileName}", fileName);
                    }
                    AddLog(context, "Info", $"Written {additionalFiles.Count} module files");
                }

                // Build parameters file if needed
                string? parametersFile = null;
                if (options.Parameters.Count > 0)
                {
                    parametersFile = Path.Combine(tempDir, "parameters.json");
                    var parametersJson = BuildBicepParametersJson(options.Parameters);
                    await File.WriteAllTextAsync(parametersFile, parametersJson, cancellationToken);
                    AddLog(context, "Info", $"Parameters file written with {options.Parameters.Count} parameters");
                }

                // Ensure resource group exists
                AddLog(context, "Info", $"Ensuring resource group {options.ResourceGroup} exists");
                await _azureResourceService.CreateResourceGroupAsync(
                    options.ResourceGroup,
                    options.Location,
                    options.SubscriptionId,
                    options.Tags,
                    cancellationToken);

                // Execute Azure deployment using Azure SDK
                AddLog(context, "Info", "Executing Azure deployment via SDK");
                
                // Use Azure SDK to deploy (through IAzureResourceService)
                // This is a simplified version - in production you'd call Azure.ResourceManager directly
                var deploymentSuccess = await ExecuteAzureDeploymentAsync(
                    templateFile,
                    options,
                    context,
                    cancellationToken);

                if (deploymentSuccess)
                {
                    result.Success = true;
                    result.State = DeploymentState.Succeeded;
                    AddLog(context, "Info", "Deployment succeeded");
                }
                else
                {
                    result.Success = false;
                    result.State = DeploymentState.Failed;
                    result.ErrorMessage = "Deployment failed - check logs for details";
                    AddLog(context, "Error", "Deployment failed");
                }

                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;

                context.State = result.State;
                context.CompletedAt = result.CompletedAt;

                // Cleanup temp files
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup temp directory {TempDir}", tempDir);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Bicep deployment {DeploymentId}", deploymentId);
                
                result.Success = false;
                result.State = DeploymentState.Failed;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;

                return result;
            }
        }

        private async Task<bool> ExecuteAzureDeploymentAsync(
            string templateFile,
            DeploymentOptions options,
            DeploymentExecutionContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                // Read template content
                var templateContent = await File.ReadAllTextAsync(templateFile, cancellationToken);

                AddLog(context, "Info", "Starting Azure ARM deployment via SDK");
                AddLog(context, "Info", $"Template size: {templateContent.Length} bytes");
                AddLog(context, "Info", $"Target: {options.ResourceGroup} in {options.Location}");

                // Get ARM client from Azure Resource Service
                var armClient = _azureResourceService.GetArmClient();
                if (armClient == null)
                {
                    AddLog(context, "Error", "ARM client not available");
                    return false;
                }

                // Get subscription ID
                var subscriptionId = _azureResourceService.GetSubscriptionId(options.SubscriptionId);
                AddLog(context, "Info", $"Using subscription: {subscriptionId}");

                // Get the resource group resource
                var subscriptionResource = armClient.GetSubscriptionResource(
                    SubscriptionResource.CreateResourceIdentifier(subscriptionId));

                var resourceGroupResource = (await subscriptionResource.GetResourceGroups()
                    .GetAsync(options.ResourceGroup, cancellationToken)).Value;

                AddLog(context, "Info", $"Resource group {options.ResourceGroup} validated");

                // Prepare deployment properties
                var deploymentName = options.DeploymentName ?? $"deployment-{DateTime.UtcNow:yyyyMMddHHmmss}";
                
                // Convert template to ARM JSON, passing the temp directory so module files can be found
                var tempDir = Path.GetDirectoryName(templateFile);
                var templateJson = ConvertBicepToArmJson(templateContent, tempDir);
                var templateData = BinaryData.FromString(templateJson);
                
                // Create ARM deployment properties
                var deploymentProperties = new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
                {
                    Template = templateData
                };

                // Add parameters if any
                if (options.Parameters.Count > 0)
                {
                    var parametersJson = BuildArmParametersJson(options.Parameters);
                    deploymentProperties.Parameters = BinaryData.FromString(parametersJson);
                    AddLog(context, "Info", $"Added {options.Parameters.Count} parameters to deployment");
                }

                // Create deployment content
                var deploymentContent = new ArmDeploymentContent(deploymentProperties);

                AddLog(context, "Info", $"Starting ARM deployment: {deploymentName}");

                // Start the deployment
                var deploymentOperation = await resourceGroupResource.GetArmDeployments()
                    .CreateOrUpdateAsync(
                        global::Azure.WaitUntil.Completed,
                        deploymentName,
                        deploymentContent,
                        cancellationToken);

                var deployment = deploymentOperation.Value;
                
                // Check deployment state
                var provisioningState = deployment.Data.Properties.ProvisioningState;
                AddLog(context, "Info", $"Deployment completed with state: {provisioningState}");

                if (provisioningState == ResourcesProvisioningState.Succeeded)
                {
                    AddLog(context, "Info", "Deployment succeeded");
                    
                    // Log outputs if any
                    if (deployment.Data.Properties.Outputs != null)
                    {
                        AddLog(context, "Info", $"Deployment outputs: {deployment.Data.Properties.Outputs}");
                    }
                    
                    return true;
                }
                else
                {
                    AddLog(context, "Error", $"Deployment failed with state: {provisioningState}");
                    
                    // Log error details if available
                    if (deployment.Data.Properties.Error != null)
                    {
                        AddLog(context, "Error", $"Error: {deployment.Data.Properties.Error.Message}");
                        AddLog(context, "Error", $"Error Code: {deployment.Data.Properties.Error.Code}");
                        
                        // Suggest checking Azure Portal for detailed operation errors
                        AddLog(context, "Info", $"For detailed resource-level errors, check Azure Portal deployment operations for: {deploymentName}");
                    }
                    
                    return false;
                }
            }
            catch (global::Azure.RequestFailedException ex)
            {
                AddLog(context, "Error", $"Azure deployment failed: {ex.Message}");
                AddLog(context, "Error", $"Status: {ex.Status}, Error Code: {ex.ErrorCode}");
                _logger.LogError(ex, "Azure deployment request failed");
                return false;
            }
            catch (Exception ex)
            {
                AddLog(context, "Error", $"Deployment execution failed: {ex.Message}");
                _logger.LogError(ex, "Deployment execution failed");
                return false;
            }
        }

        private string ConvertBicepToArmJson(string bicepContent, string? existingTempDir = null)
        {
            var trimmed = bicepContent.TrimStart();
            
            // If content is already ARM JSON, use it as-is
            if (trimmed.StartsWith("{") && trimmed.Contains("\"$schema\""))
            {
                _logger.LogDebug("Template is already ARM JSON format");
                return bicepContent;
            }

            // Compile Bicep to ARM JSON using Bicep CLI
            _logger.LogInformation("Compiling Bicep template to ARM JSON");

            try
            {
                // Use existing temp directory if provided (contains module files), otherwise create new one
                var tempDir = existingTempDir ?? Path.Combine(Path.GetTempPath(), $"bicep-compile-{Guid.NewGuid()}");
                var createdTempDir = existingTempDir == null;
                
                if (createdTempDir)
                {
                    Directory.CreateDirectory(tempDir);
                }

                try
                {
                    // Write Bicep content to temp file
                    var bicepFile = Path.Combine(tempDir, "template.bicep");
                    File.WriteAllText(bicepFile, bicepContent);
                    _logger.LogDebug("Wrote Bicep template to {BicepFile}", bicepFile);

                    // Compile using Bicep CLI
                    var armJsonFile = Path.Combine(tempDir, "template.json");
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = "bicep",
                        Arguments = $"build \"{bicepFile}\" --outfile \"{armJsonFile}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(processInfo);
                    if (process == null)
                    {
                        throw new InvalidOperationException("Failed to start Bicep process");
                    }

                    var output = process.StandardOutput.ReadToEnd();
                    var error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        _logger.LogError("Bicep compilation failed with exit code {ExitCode}. Error: {Error}", 
                            process.ExitCode, error);
                        throw new InvalidOperationException($"Bicep compilation failed: {error}");
                    }

                    // Read the compiled ARM JSON
                    if (!File.Exists(armJsonFile))
                    {
                        throw new FileNotFoundException($"Bicep compilation did not produce output file: {armJsonFile}");
                    }

                    var armJson = File.ReadAllText(armJsonFile);
                    _logger.LogInformation("Successfully compiled Bicep to ARM JSON ({Length} bytes)", armJson.Length);
                    
                    return armJson;
                }
                finally
                {
                    // Only clean up temp directory if we created it (not if using existing deployment directory)
                    if (createdTempDir)
                    {
                        try
                        {
                            if (Directory.Exists(tempDir))
                            {
                                Directory.Delete(tempDir, recursive: true);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to clean up temp directory {TempDir}", tempDir);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                _logger.LogError(ex, "Error compiling Bicep template");
                throw new NotSupportedException(
                    "Bicep compilation failed. Please ensure Bicep CLI is installed (https://aka.ms/bicep-install) " +
                    "or provide an ARM JSON template instead.", ex);
            }
        }

        private string BuildArmParametersJson(Dictionary<string, string> parameters)
        {
            // Build parameters object for ARM deployment (not a parameters file)
            // Azure SDK expects just the parameters object, not a full parameters file with $schema
            var sb = new StringBuilder();
            sb.AppendLine("{");
            
            var paramList = parameters.ToList();
            for (int i = 0; i < paramList.Count; i++)
            {
                var param = paramList[i];
                
                // Determine if the value should be treated as a number or boolean
                // Only treat pure integers as numbers (not floats/decimals which could be version strings)
                string valueJson;
                if (int.TryParse(param.Value, out _) && !param.Value.Contains("."))
                {
                    // Integer value - don't quote it
                    valueJson = param.Value;
                }
                else if (bool.TryParse(param.Value, out var boolValue))
                {
                    // Boolean value - use lowercase true/false
                    valueJson = boolValue.ToString().ToLower();
                }
                else
                {
                    // String value (including version numbers, decimals, etc.) - quote it
                    valueJson = $"\"{param.Value}\"";
                }
                
                // ARM parameters expect { "value": <value> } format
                sb.Append($"  \"{param.Key}\": {{ \"value\": {valueJson} }}");
                if (i < paramList.Count - 1)
                    sb.AppendLine(",");
                else
                    sb.AppendLine();
            }
            
            sb.AppendLine("}");
            
            return sb.ToString();
        }

        #endregion

        #region Terraform Deployment

        public async Task<DeploymentResult> DeployTerraformAsync(
            string terraformContent,
            DeploymentOptions options,
            CancellationToken cancellationToken = default)
        {
            var deploymentId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting Terraform deployment {DeploymentId}", deploymentId);

            var result = new DeploymentResult
            {
                DeploymentId = deploymentId,
                DeploymentName = options.DeploymentName,
                ResourceGroup = options.ResourceGroup,
                State = DeploymentState.Running,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                var context = new DeploymentExecutionContext
                {
                    DeploymentId = deploymentId,
                    StartedAt = DateTime.UtcNow,
                    State = DeploymentState.Running,
                    Logs = new List<LogEntry>()
                };

                lock (_lock)
                {
                    _activeDeployments[deploymentId] = context;
                }

                AddLog(context, "Info", "Starting Terraform deployment");

                // Create temp directory for Terraform files
                var tempDir = Path.Combine(Path.GetTempPath(), "terraform-deployments", deploymentId);
                Directory.CreateDirectory(tempDir);
                var mainTfFile = Path.Combine(tempDir, "main.tf");
                await File.WriteAllTextAsync(mainTfFile, terraformContent, cancellationToken);

                AddLog(context, "Info", $"Terraform configuration written to {tempDir}");

                // Execute terraform commands
                var initSuccess = await ExecuteTerraformCommandAsync("init", tempDir, context, cancellationToken);
                if (!initSuccess)
                {
                    result.Success = false;
                    result.State = DeploymentState.Failed;
                    result.ErrorMessage = "Terraform init failed";
                    return result;
                }

                var planSuccess = await ExecuteTerraformCommandAsync("plan -out=tfplan", tempDir, context, cancellationToken);
                if (!planSuccess)
                {
                    result.Success = false;
                    result.State = DeploymentState.Failed;
                    result.ErrorMessage = "Terraform plan failed";
                    return result;
                }

                var applySuccess = await ExecuteTerraformCommandAsync("apply -auto-approve tfplan", tempDir, context, cancellationToken);
                if (!applySuccess)
                {
                    result.Success = false;
                    result.State = DeploymentState.Failed;
                    result.ErrorMessage = "Terraform apply failed";
                    return result;
                }

                result.Success = true;
                result.State = DeploymentState.Succeeded;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;

                context.State = DeploymentState.Succeeded;
                context.CompletedAt = result.CompletedAt;

                // Cleanup
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cleanup Terraform temp directory");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Terraform deployment {DeploymentId}", deploymentId);
                
                result.Success = false;
                result.State = DeploymentState.Failed;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;

                return result;
            }
        }

        private async Task<bool> ExecuteTerraformCommandAsync(
            string command,
            string workingDirectory,
            DeploymentExecutionContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                AddLog(context, "Info", $"Executing: terraform {command}");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "terraform",
                    Arguments = command,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    AddLog(context, "Error", "Failed to start terraform process");
                    return false;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    AddLog(context, "Info", output);
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    AddLog(context, "Warning", error);
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                AddLog(context, "Error", $"Terraform command failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Kubernetes Deployment

        public async Task<DeploymentResult> DeployKubernetesAsync(
            string kubernetesManifest,
            DeploymentOptions options,
            CancellationToken cancellationToken = default)
        {
            var deploymentId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting Kubernetes deployment {DeploymentId}", deploymentId);

            var result = new DeploymentResult
            {
                DeploymentId = deploymentId,
                DeploymentName = options.DeploymentName,
                State = DeploymentState.Running,
                StartedAt = DateTime.UtcNow
            };

            try
            {
                var context = new DeploymentExecutionContext
                {
                    DeploymentId = deploymentId,
                    StartedAt = DateTime.UtcNow,
                    State = DeploymentState.Running,
                    Logs = new List<LogEntry>()
                };

                lock (_lock)
                {
                    _activeDeployments[deploymentId] = context;
                }

                AddLog(context, "Info", "Starting Kubernetes deployment");

                // Create temp file for manifest
                var tempFile = Path.Combine(Path.GetTempPath(), $"k8s-manifest-{deploymentId}.yaml");
                await File.WriteAllTextAsync(tempFile, kubernetesManifest, cancellationToken);

                AddLog(context, "Info", $"Kubernetes manifest written to {tempFile}");

                // Execute kubectl apply
                var success = await ExecuteKubectlCommandAsync($"apply -f {tempFile}", context, cancellationToken);

                if (success)
                {
                    result.Success = true;
                    result.State = DeploymentState.Succeeded;
                    AddLog(context, "Info", "Kubernetes deployment succeeded");
                }
                else
                {
                    result.Success = false;
                    result.State = DeploymentState.Failed;
                    result.ErrorMessage = "Kubectl apply failed";
                    AddLog(context, "Error", "Kubernetes deployment failed");
                }

                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;

                context.State = result.State;
                context.CompletedAt = result.CompletedAt;

                // Cleanup
                try
                {
                    File.Delete(tempFile);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temp Kubernetes manifest");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Kubernetes deployment {DeploymentId}", deploymentId);
                
                result.Success = false;
                result.State = DeploymentState.Failed;
                result.ErrorMessage = ex.Message;
                result.CompletedAt = DateTime.UtcNow;
                result.Duration = result.CompletedAt - result.StartedAt;

                return result;
            }
        }

        private async Task<bool> ExecuteKubectlCommandAsync(
            string command,
            DeploymentExecutionContext context,
            CancellationToken cancellationToken)
        {
            try
            {
                AddLog(context, "Info", $"Executing: kubectl {command}");

                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "kubectl",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(processStartInfo);
                if (process == null)
                {
                    AddLog(context, "Error", "Failed to start kubectl process");
                    return false;
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(output))
                {
                    AddLog(context, "Info", output);
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    AddLog(context, "Warning", error);
                }

                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                AddLog(context, "Error", $"Kubectl command failed: {ex.Message}");
                return false;
            }
        }

        #endregion

        #region Deployment Status & Logs

        public async Task<OrchestrationDeploymentStatus> GetDeploymentStatusAsync(
            string deploymentId,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Suppress async warning

            DeploymentExecutionContext? context;
            lock (_lock)
            {
                _activeDeployments.TryGetValue(deploymentId, out context);
            }

            if (context == null)
            {
                return new OrchestrationDeploymentStatus
                {
                    DeploymentId = deploymentId,
                    State = DeploymentState.NotStarted,
                    CurrentOperation = "Deployment not found"
                };
            }

            var status = new OrchestrationDeploymentStatus
            {
                DeploymentId = deploymentId,
                State = context.State,
                CurrentOperation = context.CurrentOperation,
                LastUpdated = context.Logs.LastOrDefault()?.Timestamp ?? context.StartedAt,
                ProgressPercentage = CalculateProgress(context)
            };

            return status;
        }

        public async Task<DeploymentLogs> GetDeploymentLogsAsync(
            string deploymentId,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Suppress async warning

            DeploymentExecutionContext? context;
            lock (_lock)
            {
                _activeDeployments.TryGetValue(deploymentId, out context);
            }

            if (context == null)
            {
                return new DeploymentLogs
                {
                    DeploymentId = deploymentId,
                    Entries = new List<LogEntry>
                    {
                        new LogEntry
                        {
                            Timestamp = DateTime.UtcNow,
                            Level = "Warning",
                            Message = "Deployment not found"
                        }
                    }
                };
            }

            return new DeploymentLogs
            {
                DeploymentId = deploymentId,
                Entries = context.Logs.ToList()
            };
        }

        public async Task<bool> CancelDeploymentAsync(
            string deploymentId,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Suppress async warning

            DeploymentExecutionContext? context;
            lock (_lock)
            {
                _activeDeployments.TryGetValue(deploymentId, out context);
            }

            if (context == null || context.State != DeploymentState.Running)
            {
                return false;
            }

            context.State = DeploymentState.Canceled;
            context.CompletedAt = DateTime.UtcNow;
            AddLog(context, "Warning", "Deployment canceled by user");

            return true;
        }

        public async Task<DeploymentValidationResult> ValidateDeploymentAsync(
            string templateContent,
            string templateType,
            DeploymentOptions options,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Suppress async warning

            var result = new DeploymentValidationResult
            {
                IsValid = true
            };

            // Basic validation
            if (string.IsNullOrWhiteSpace(templateContent))
            {
                result.IsValid = false;
                result.Errors.Add("Template content is empty");
            }

            if (string.IsNullOrWhiteSpace(options.ResourceGroup))
            {
                result.IsValid = false;
                result.Errors.Add("Resource group is required");
            }

            if (string.IsNullOrWhiteSpace(options.Location))
            {
                result.Warnings.Add("Location not specified, using default");
            }

            // Template-specific validation would go here
            switch (templateType.ToLowerInvariant())
            {
                case "bicep":
                    // Validate Bicep syntax
                    break;
                case "terraform":
                    // Validate Terraform syntax
                    break;
                case "kubernetes":
                    // Validate Kubernetes YAML
                    break;
            }

            return result;
        }

        #endregion

        #region Helper Methods

        private void AddLog(DeploymentExecutionContext context, string level, string message)
        {
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                Message = message,
                Source = "DeploymentOrchestrationService"
            };

            context.Logs.Add(logEntry);
            context.CurrentOperation = message;

            _logger.Log(level switch
            {
                "Error" => LogLevel.Error,
                "Warning" => LogLevel.Warning,
                _ => LogLevel.Information
            }, "{DeploymentId}: {Message}", context.DeploymentId, message);
        }

        private int CalculateProgress(DeploymentExecutionContext context)
        {
            return context.State switch
            {
                DeploymentState.NotStarted => 0,
                DeploymentState.Queued => 10,
                DeploymentState.Running => 50,
                DeploymentState.Succeeded => 100,
                DeploymentState.Failed => 100,
                DeploymentState.Canceled => 100,
                _ => 0
            };
        }

        private string BuildBicepParametersJson(Dictionary<string, string> parameters)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"$schema\": \"https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#\",");
            sb.AppendLine("  \"contentVersion\": \"1.0.0.0\",");
            sb.AppendLine("  \"parameters\": {");

            var entries = parameters.Select(kvp => $"    \"{kvp.Key}\": {{ \"value\": \"{kvp.Value}\" }}");
            sb.AppendLine(string.Join(",\n", entries));

            sb.AppendLine("  }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        /// <summary>
        /// Extracts the actual template content from a potentially incorrectly serialized Template object
        /// </summary>
        /// <remarks>
        /// Fixes bug where entire Template object was JSON-serialized into the Content field,
        /// creating nested serialization (Template.Content contains serialized Template object).
        /// </remarks>
        private string ExtractActualTemplateContent(string templateContent)
        {
            if (string.IsNullOrWhiteSpace(templateContent))
                return templateContent;

            var trimmed = templateContent.TrimStart();
            
            // Check if this looks like a serialized Template object (starts with {"Id":)
            if (trimmed.StartsWith("{\"Id\":") || trimmed.StartsWith("{\"id\":"))
            {
                try
                {
                    // Parse as JSON to extract the actual Content field
                    using var doc = JsonDocument.Parse(templateContent);
                    var root = doc.RootElement;
                    
                    // Check if this has Template object properties
                    if (root.TryGetProperty("Content", out var contentElement))
                    {
                        var actualContent = contentElement.GetString();
                        if (!string.IsNullOrWhiteSpace(actualContent))
                        {
                            _logger.LogWarning("Detected incorrectly serialized Template object. Extracting actual content.");
                            // Recursively extract in case of multiple levels of nesting
                            return ExtractActualTemplateContent(actualContent);
                        }
                    }
                    else if (root.TryGetProperty("content", out var contentElementLower))
                    {
                        var actualContent = contentElementLower.GetString();
                        if (!string.IsNullOrWhiteSpace(actualContent))
                        {
                            _logger.LogWarning("Detected incorrectly serialized template object (lowercase). Extracting actual content.");
                            return ExtractActualTemplateContent(actualContent);
                        }
                    }
                }
                catch (JsonException)
                {
                    // Not valid JSON, return as-is
                    _logger.LogDebug("Template content is not valid JSON, using as-is");
                }
            }

            // Return unchanged if no extraction needed
            return templateContent;
        }

        #endregion

        /// <summary>
        /// Internal context for tracking deployment execution
        /// </summary>
        private class DeploymentExecutionContext
        {
            public string DeploymentId { get; set; } = string.Empty;
            public DeploymentState State { get; set; }
            public DateTime StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public string CurrentOperation { get; set; } = string.Empty;
            public List<LogEntry> Logs { get; set; } = new();
        }
    }
}
