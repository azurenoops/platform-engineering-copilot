using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Interfaces.Deployment;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Platform.Engineering.Copilot.Agents.Infrastructure.Deployment;

/// <summary>
/// Deploys Terraform templates using the Terraform CLI
/// </summary>
public class TerraformDeployer : ITemplateDeployer
{
    private readonly ILogger<TerraformDeployer> _logger;
    private readonly DeployerOptions _options;

    public string Format => "Terraform";

    public TerraformDeployer(
        ILogger<TerraformDeployer> logger,
        IOptions<DeployerOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public bool CanHandle(string format) =>
        format.Equals("Terraform", StringComparison.OrdinalIgnoreCase) ||
        format.Equals("TF", StringComparison.OrdinalIgnoreCase);

    public async Task<TemplateDeploymentResult> DeployAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new TemplateDeploymentResult
        {
            DeploymentId = $"tf-{Guid.NewGuid():N}",
            StartedAt = DateTime.UtcNow
        };

        var workDir = Path.Combine(Path.GetTempPath(), $"terraform-{result.DeploymentId}");

        try
        {
            _logger.LogInformation("🌍 Starting Terraform deployment for {Environment}", request.EnvironmentName);

            // Create working directory
            Directory.CreateDirectory(workDir);

            // Write main.tf
            var mainTfPath = Path.Combine(workDir, "main.tf");
            await File.WriteAllTextAsync(mainTfPath, request.TemplateContent, cancellationToken);

            // Write terraform.tfvars.json for parameters
            if (request.Parameters.Count > 0)
            {
                var tfvarsPath = Path.Combine(workDir, "terraform.tfvars.json");
                var tfvars = JsonSerializer.Serialize(request.Parameters, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                await File.WriteAllTextAsync(tfvarsPath, tfvars, cancellationToken);
            }

            // Write backend configuration if provided
            if (request.TerraformBackend != null)
            {
                var backendContent = GenerateBackendConfig(request.TerraformBackend, request.EnvironmentName);
                var backendPath = Path.Combine(workDir, "backend.tf");
                await File.WriteAllTextAsync(backendPath, backendContent, cancellationToken);
            }

            // Set up environment variables for Azure authentication
            var envVars = new Dictionary<string, string>
            {
                ["ARM_SUBSCRIPTION_ID"] = request.SubscriptionId,
                ["ARM_USE_CLI"] = "true", // Use Azure CLI authentication
                ["TF_IN_AUTOMATION"] = "true",
                ["TF_INPUT"] = "false"
            };

            // Add Azure Government endpoint if configured
            if (_options.UseGovernmentCloud)
            {
                envVars["ARM_ENVIRONMENT"] = "usgovernment";
            }

            // Step 1: terraform init
            _logger.LogInformation("Running terraform init...");
            var (initCode, initOut, initErr) = await ExecuteCommandAsync(
                "init", workDir, envVars, cancellationToken);

            if (initCode != 0)
            {
                result.Success = false;
                result.Status = "InitFailed";
                result.Errors.Add($"Terraform init failed: {initErr}");
                result.RawOutput = initOut;
                return result;
            }

            if (request.WhatIf)
            {
                // Step 2a: terraform plan (dry run)
                _logger.LogInformation("Running terraform plan (what-if)...");
                var (planCode, planOut, planErr) = await ExecuteCommandAsync(
                    "plan -detailed-exitcode", workDir, envVars, cancellationToken);

                result.RawOutput = planOut;
                result.Status = planCode switch
                {
                    0 => "NoChanges",
                    2 => "ChangesDetected",
                    _ => "PlanFailed"
                };
                result.Success = planCode == 0 || planCode == 2;

                if (!result.Success)
                {
                    result.Errors.Add($"Terraform plan failed: {planErr}");
                }

                // Parse planned resources from output
                ParsePlanOutput(planOut, result);
            }
            else
            {
                // Step 2b: terraform apply
                _logger.LogInformation("Running terraform apply...");
                var (applyCode, applyOut, applyErr) = await ExecuteCommandAsync(
                    "apply -auto-approve", workDir, envVars, cancellationToken);

                result.RawOutput = applyOut;
                result.Success = applyCode == 0;
                result.Status = applyCode == 0 ? "Succeeded" : "Failed";

                if (!result.Success)
                {
                    result.Errors.Add($"Terraform apply failed: {applyErr}");
                }
                else
                {
                    // Get outputs
                    var (outputCode, outputOut, _) = await ExecuteCommandAsync(
                        "output -json", workDir, envVars, cancellationToken);

                    if (outputCode == 0 && !string.IsNullOrWhiteSpace(outputOut))
                    {
                        try
                        {
                            var outputs = JsonSerializer.Deserialize<Dictionary<string, TerraformOutput>>(outputOut);
                            if (outputs != null)
                            {
                                foreach (var output in outputs)
                                {
                                    result.OutputValues[output.Key] = output.Value.Value ?? "";
                                    result.Outputs.Add($"{output.Key}: {output.Value.Value}");
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            _logger.LogWarning("Could not parse Terraform outputs");
                        }
                    }

                    // Parse state to get resources
                    await ParseStateForResourcesAsync(workDir, envVars, result, cancellationToken);
                }

                _logger.LogInformation("✅ Terraform deployment completed with status: {Status}", result.Status);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Terraform deployment failed: {Message}", ex.Message);
            result.Success = false;
            result.Status = "Failed";
            result.Errors.Add(ex.Message);
        }
        finally
        {
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;

            // Cleanup working directory
            try
            {
                if (Directory.Exists(workDir))
                {
                    Directory.Delete(workDir, true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup Terraform working directory");
            }
        }

        return result;
    }

    public async Task<ValidationResult> ValidateAsync(
        string templateContent,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { IsValid = true };
        var workDir = Path.Combine(Path.GetTempPath(), $"tf-validate-{Guid.NewGuid():N}");

        try
        {
            // Basic syntax checks
            if (string.IsNullOrWhiteSpace(templateContent))
            {
                result.IsValid = false;
                result.Errors.Add("Template content is empty");
                return result;
            }

            // Create temp directory and write file
            Directory.CreateDirectory(workDir);
            await File.WriteAllTextAsync(Path.Combine(workDir, "main.tf"), templateContent, cancellationToken);

            // Run terraform validate
            var (initCode, _, initErr) = await ExecuteCommandAsync(
                "init -backend=false", workDir, new Dictionary<string, string>(), cancellationToken);

            if (initCode != 0)
            {
                result.IsValid = false;
                result.Errors.Add($"Terraform init failed: {initErr}");
                return result;
            }

            var (validateCode, validateOut, validateErr) = await ExecuteCommandAsync(
                "validate -json", workDir, new Dictionary<string, string>(), cancellationToken);

            if (validateCode != 0)
            {
                result.IsValid = false;
                result.Errors.Add(validateErr);
            }

            // Parse validation JSON output
            if (!string.IsNullOrWhiteSpace(validateOut))
            {
                try
                {
                    var validation = JsonSerializer.Deserialize<TerraformValidationResult>(validateOut);
                    if (validation != null)
                    {
                        result.IsValid = validation.Valid;
                        if (validation.Diagnostics != null)
                        {
                            foreach (var diag in validation.Diagnostics)
                            {
                                if (diag.Severity == "error")
                                    result.Errors.Add(diag.Summary ?? "Unknown error");
                                else
                                    result.Warnings.Add(diag.Summary ?? "Unknown warning");
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    // If JSON parsing fails, use raw output
                    if (validateCode != 0)
                    {
                        result.Errors.Add(validateOut);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(workDir))
                {
                    Directory.Delete(workDir, true);
                }
            }
            catch { /* ignore cleanup errors */ }
        }

        return result;
    }

    /// <summary>
    /// Get the status of a deployment - Terraform doesn't have async deployments like ARM
    /// </summary>
    public Task<DeploymentStatusResult> GetDeploymentStatusAsync(
        string subscriptionId,
        string deploymentName,
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        // Terraform deployments are synchronous (we wait for apply to complete)
        // This method exists for interface compliance but isn't typically used
        return Task.FromResult(new DeploymentStatusResult
        {
            DeploymentName = deploymentName,
            ProvisioningState = "NotApplicable",
            ErrorMessage = "Terraform deployments complete synchronously. Use the deployment result directly."
        });
    }

    private string GenerateBackendConfig(TerraformBackendConfig config, string environmentName)
    {
        var key = string.IsNullOrEmpty(config.Key) ? $"{environmentName}.tfstate" : config.Key;

        return config.Type.ToLowerInvariant() switch
        {
            "azurerm" => $@"
terraform {{
  backend ""azurerm"" {{
    resource_group_name  = ""{config.ResourceGroupName}""
    storage_account_name = ""{config.StorageAccountName}""
    container_name       = ""{config.ContainerName}""
    key                  = ""{key}""
  }}
}}
",
            _ => $@"
terraform {{
  backend ""{config.Type}"" {{
    key = ""{key}""
  }}
}}
"
        };
    }

    private async Task<(int ExitCode, string Output, string Error)> ExecuteCommandAsync(
        string arguments,
        string workingDirectory,
        Dictionary<string, string> environmentVariables,
        CancellationToken cancellationToken)
    {
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _options.TerraformExecutablePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        foreach (var env in environmentVariables)
        {
            process.StartInfo.EnvironmentVariables[env.Key] = env.Value;
        }

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null) outputBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null) errorBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        var timeoutMs = _options.DeploymentTimeoutMinutes * 60 * 1000;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(true); } catch { }
            throw new TimeoutException($"Terraform command timed out after {_options.DeploymentTimeoutMinutes} minutes");
        }

        return (process.ExitCode, outputBuilder.ToString(), errorBuilder.ToString());
    }

    private void ParsePlanOutput(string planOutput, TemplateDeploymentResult result)
    {
        // Parse resource changes from terraform plan output
        var createPattern = new Regex(@"\+ ([\w.]+)\.([\w-]+)", RegexOptions.Multiline);
        var updatePattern = new Regex(@"~ ([\w.]+)\.([\w-]+)", RegexOptions.Multiline);
        var deletePattern = new Regex(@"- ([\w.]+)\.([\w-]+)", RegexOptions.Multiline);

        foreach (Match match in createPattern.Matches(planOutput))
        {
            result.Resources.Add(new DeployedResourceInfo
            {
                Type = match.Groups[1].Value,
                Name = match.Groups[2].Value,
                ProvisioningState = "ToCreate"
            });
        }

        foreach (Match match in updatePattern.Matches(planOutput))
        {
            result.Resources.Add(new DeployedResourceInfo
            {
                Type = match.Groups[1].Value,
                Name = match.Groups[2].Value,
                ProvisioningState = "ToUpdate"
            });
        }

        foreach (Match match in deletePattern.Matches(planOutput))
        {
            result.Resources.Add(new DeployedResourceInfo
            {
                Type = match.Groups[1].Value,
                Name = match.Groups[2].Value,
                ProvisioningState = "ToDelete"
            });
        }
    }

    private async Task ParseStateForResourcesAsync(
        string workDir,
        Dictionary<string, string> envVars,
        TemplateDeploymentResult result,
        CancellationToken cancellationToken)
    {
        var (stateCode, stateOut, _) = await ExecuteCommandAsync(
            "state list", workDir, envVars, cancellationToken);

        if (stateCode == 0 && !string.IsNullOrWhiteSpace(stateOut))
        {
            var resources = stateOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var resource in resources)
            {
                var parts = resource.Trim().Split('.');
                if (parts.Length >= 2)
                {
                    result.Resources.Add(new DeployedResourceInfo
                    {
                        Type = parts[0],
                        Name = parts.Length > 1 ? parts[1] : resource,
                        ResourceId = resource,
                        ProvisioningState = "Succeeded"
                    });
                }
            }
        }
    }

    private class TerraformOutput
    {
        public object? Value { get; set; }
        public string? Type { get; set; }
        public bool Sensitive { get; set; }
    }

    private class TerraformValidationResult
    {
        public bool Valid { get; set; }
        public List<TerraformDiagnostic>? Diagnostics { get; set; }
    }

    private class TerraformDiagnostic
    {
        public string? Severity { get; set; }
        public string? Summary { get; set; }
        public string? Detail { get; set; }
    }
}
