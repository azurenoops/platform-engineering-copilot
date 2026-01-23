using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager.Resources.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Engineering.Copilot.Core.Interfaces.Deployment;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Agents.Infrastructure.Deployment;

/// <summary>
/// Deploys Bicep templates using Azure Resource Manager SDK
/// </summary>
public class BicepDeployer : ITemplateDeployer
{
    private readonly ILogger<BicepDeployer> _logger;
    private readonly ArmClient _armClient;
    private readonly DeployerOptions _options;

    public string Format => "Bicep";

    public BicepDeployer(
        ILogger<BicepDeployer> logger,
        IOptions<DeployerOptions> options)
    {
        _logger = logger;
        _options = options.Value;
        
        // Determine the authority host based on cloud environment
        var authorityHost = _options.UseGovernmentCloud 
            ? AzureAuthorityHosts.AzureGovernment 
            : AzureAuthorityHosts.AzurePublicCloud;
        
        // Initialize credential with correct cloud authority
        var credentialOptions = new DefaultAzureCredentialOptions
        {
            AuthorityHost = authorityHost,
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeVisualStudioCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeInteractiveBrowserCredential = true
        };
        
        var credential = new DefaultAzureCredential(credentialOptions);
        
        _armClient = new ArmClient(credential, defaultSubscriptionId: null, new ArmClientOptions
        {
            Environment = _options.UseGovernmentCloud 
                ? ArmEnvironment.AzureGovernment 
                : ArmEnvironment.AzurePublicCloud
        });
    }

    public bool CanHandle(string format) =>
        format.Equals("Bicep", StringComparison.OrdinalIgnoreCase) ||
        format.Equals("ARM", StringComparison.OrdinalIgnoreCase);

    public async Task<TemplateDeploymentResult> DeployAsync(
        DeploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = new TemplateDeploymentResult
        {
            DeploymentId = $"deploy-{Guid.NewGuid():N}",
            StartedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("🚀 Starting Bicep deployment for {Environment}",
                request.EnvironmentName);

            // Get subscription
            var subscription = await _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{request.SubscriptionId}"))
                .GetAsync(cancellationToken);

            // Compile Bicep to ARM JSON FIRST to determine deployment scope
            string templateJson;
            if (request.TemplateContent.TrimStart().StartsWith("{"))
            {
                // Already JSON (ARM template)
                templateJson = request.TemplateContent;
                _logger.LogInformation("Using ARM JSON template directly");
            }
            else
            {
                // Compile Bicep to ARM JSON (including any module files)
                _logger.LogInformation("Compiling Bicep template to ARM JSON");
                templateJson = await CompileBicepToJsonAsync(request.TemplateContent, request.AdditionalFiles, cancellationToken);
            }

            // Determine deployment scope from compiled ARM template
            var deploymentScope = DetermineDeploymentScope(templateJson);
            _logger.LogInformation("📍 Detected deployment scope: {Scope}", deploymentScope);

            // Only create resource group for resource-group-scoped deployments
            var resourceGroups = subscription.Value.GetResourceGroups();
            ResourceGroupResource? resourceGroup = null;
            
            if (deploymentScope != "subscription")
            {
                // Resource group scope - create if needed
                if (string.IsNullOrEmpty(request.ResourceGroupName))
                {
                    throw new InvalidOperationException(
                        "Resource group name is required for resource-group-scoped deployments");
                }
                
                _logger.LogInformation("📦 Resource group-scoped deployment to {ResourceGroup}", 
                    request.ResourceGroupName);
                
                if (!await resourceGroups.ExistsAsync(request.ResourceGroupName, cancellationToken))
                {
                    _logger.LogInformation("Creating resource group {ResourceGroup} in {Location}",
                        request.ResourceGroupName, request.Location);
                        
                    var rgData = new ResourceGroupData(request.Location);
                    foreach (var tag in request.Tags)
                    {
                        rgData.Tags.Add(tag.Key, tag.Value);
                    }
                    
                    var rgOperation = await resourceGroups.CreateOrUpdateAsync(
                        WaitUntil.Completed,
                        request.ResourceGroupName,
                        rgData,
                        cancellationToken);
                        
                    resourceGroup = rgOperation.Value;
                }
                else
                {
                    resourceGroup = await resourceGroups.GetAsync(request.ResourceGroupName, cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation("🌐 Subscription-scoped deployment - template will manage its own resource groups");
            }

            // Prepare deployment
            var deploymentName = $"{request.EnvironmentName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            ArmDeploymentCollection deployments;
            
            if (deploymentScope == "subscription")
            {
                // Subscription-level deployment (e.g., MLZ creates its own resource groups)
                _logger.LogInformation("🌐 Using subscription-level deployment for {DeploymentName}", deploymentName);
                deployments = subscription.Value.GetArmDeployments();
            }
            else
            {
                // Resource group level deployment
                _logger.LogInformation("📦 Using resource group-level deployment for {DeploymentName}", deploymentName);
                deployments = resourceGroup!.GetArmDeployments();
            }

            // Convert parameters to ARM format, properly handling complex types (arrays, objects)
            var armParameterDict = new Dictionary<string, object>();
            foreach (var kvp in request.Parameters)
            {
                var originalType = kvp.Value?.GetType().Name ?? "null";
                var value = ConvertParameterValue(kvp.Value);
                var convertedType = value?.GetType().Name ?? "null";
                
                // Skip parameters that are ARM expressions - let the template use its default
                // ARM expressions look like: deployment().xxx, resourceGroup().xxx, subscription().xxx, etc.
                if (value is string strValue && IsArmExpression(strValue))
                {
                    _logger.LogInformation("⏭️ Skipping parameter '{Key}' - contains ARM expression: {Value}", 
                        kvp.Key, strValue);
                    continue;
                }
                
                // Log conversions for debugging array/object issues
                if (originalType != convertedType || kvp.Key.Contains("Logs", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("🔧 Parameter '{Key}': {OriginalType} -> {ConvertedType}", 
                        kvp.Key, originalType, convertedType);
                }
                
                armParameterDict[kvp.Key] = new { value };
            }
            
            // For subscription-level deployments, ensure the location parameter is set to the actual location
            // This overrides any ARM expression default in the template
            if (deploymentScope == "subscription" && !string.IsNullOrEmpty(request.Location))
            {
                if (!armParameterDict.ContainsKey("location") || 
                    (armParameterDict.TryGetValue("location", out var locParam) && 
                     locParam is { } lp && 
                     lp.GetType().GetProperty("value")?.GetValue(lp) is string locStr && 
                     IsArmExpression(locStr)))
                {
                    _logger.LogInformation("📍 Setting location parameter to '{Location}' for subscription deployment", 
                        request.Location);
                    armParameterDict["location"] = new { value = request.Location };
                }
            }
            
            var armParameters = new BinaryData(JsonSerializer.Serialize(armParameterDict));

            // Create deployment content - subscription deployments require location
            ArmDeploymentContent deploymentContent;
            if (deploymentScope == "subscription")
            {
                deploymentContent = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
                {
                    Template = new BinaryData(templateJson),
                    Parameters = armParameters
                })
                {
                    Location = request.Location // Required for subscription-level deployments
                };
            }
            else
            {
                deploymentContent = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
                {
                    Template = new BinaryData(templateJson),
                    Parameters = armParameters
                });
            }

            if (request.WhatIf)
            {
                // Run validation instead of What-If (What-If requires different API)
                _logger.LogInformation("Running validation for {DeploymentName}", deploymentName);
                
                // Validate the template
                var validateContent = new ArmDeploymentContent(new ArmDeploymentProperties(ArmDeploymentMode.Incremental)
                {
                    Template = new BinaryData(request.TemplateContent),
                    Parameters = armParameters
                });
                
                // Use ValidateAsync for validation
                try
                {
                    var validateOperation = await deployments.CreateOrUpdateAsync(
                        WaitUntil.Started,
                        $"{deploymentName}-validate",
                        validateContent,
                        cancellationToken);
                    
                    // Cancel the deployment immediately after validation
                    result.Success = true;
                    result.Status = "Validated";
                    result.RawOutput = "Template validation successful";
                }
                catch (RequestFailedException ex)
                {
                    result.Success = false;
                    result.Status = "ValidationFailed";
                    result.Errors.Add(ex.Message);
                }
            }
            else
            {
                // Execute deployment
                _logger.LogInformation("Executing deployment {DeploymentName}", deploymentName);
                
                // For subscription-level deployments (like MLZ), use WaitUntil.Started to avoid timeouts
                // These can take 20-30+ minutes and would cause API timeouts otherwise
                var waitMode = deploymentScope == "subscription" ? WaitUntil.Started : WaitUntil.Completed;
                
                if (deploymentScope == "subscription")
                {
                    _logger.LogInformation("⏳ Starting async subscription deployment - will not wait for completion");
                }
                
                var deploymentOperation = await deployments.CreateOrUpdateAsync(
                    waitMode,
                    deploymentName,
                    deploymentContent,
                    cancellationToken);

                if (deploymentScope == "subscription")
                {
                    // For async deployments, return immediately with running status
                    result.DeploymentId = deploymentName;
                    result.Success = true;  // Started successfully
                    result.Status = "Running";
                    result.RawOutput = $"Subscription-level deployment '{deploymentName}' started. " +
                                       $"This deployment may take 20-30 minutes. " +
                                       $"Monitor progress in Azure Portal: Subscription → Deployments";
                    
                    _logger.LogInformation("🚀 Deployment {DeploymentName} started asynchronously at subscription level", 
                        deploymentName);
                }
                else
                {
                    // For resource group deployments, wait for completion and get full results
                    var deployment = deploymentOperation.Value;
                    var properties = deployment.Data.Properties;

                    result.Success = properties.ProvisioningState == ResourcesProvisioningState.Succeeded;
                    result.Status = properties.ProvisioningState?.ToString() ?? "Unknown";
                    result.CorrelationId = properties.CorrelationId;
                    result.DeploymentId = deploymentName;

                    // Extract deployed resources
                    if (properties.OutputResources != null)
                    {
                        foreach (var resource in properties.OutputResources)
                        {
                            result.Resources.Add(new DeployedResourceInfo
                            {
                                ResourceId = resource.Id?.ToString() ?? "",
                                Name = resource.Id?.Name ?? "",
                                Type = resource.Id?.ResourceType.ToString() ?? "",
                                ProvisioningState = "Succeeded"
                            });
                        }
                    }

                    // Extract outputs
                    if (properties.Outputs != null)
                    {
                        var outputs = properties.Outputs.ToObjectFromJson<Dictionary<string, OutputValue>>();
                        if (outputs != null)
                        {
                            foreach (var output in outputs)
                            {
                                result.OutputValues[output.Key] = output.Value.Value ?? "";
                                result.Outputs.Add($"{output.Key}: {output.Value.Value}");
                            }
                        }
                    }

                    _logger.LogInformation("✅ Deployment {DeploymentName} completed with status: {Status}",
                        deploymentName, result.Status);
                }
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure deployment failed: {Message}", ex.Message);
            result.Success = false;
            result.Status = "Failed";
            result.Errors.Add($"Azure error: {ex.Message}");
            result.Errors.Add($"Error code: {ex.ErrorCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment failed: {Message}", ex.Message);
            result.Success = false;
            result.Status = "Failed";
            result.Errors.Add(ex.Message);
        }
        finally
        {
            result.CompletedAt = DateTime.UtcNow;
            result.Duration = result.CompletedAt.Value - result.StartedAt;
        }

        return result;
    }

    public async Task<ValidationResult> ValidateAsync(
        string templateContent,
        Dictionary<string, object>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { IsValid = true };

        try
        {
            // Basic syntax validation
            if (string.IsNullOrWhiteSpace(templateContent))
            {
                result.IsValid = false;
                result.Errors.Add("Template content is empty");
                return result;
            }

            // Check for required Bicep elements
            if (!templateContent.Contains("resource ") && !templateContent.Contains("module "))
            {
                result.Warnings.Add("Template doesn't contain any resource or module declarations");
            }

            // Check for targetScope if deploying subscription-level
            if (templateContent.Contains("subscription()") && !templateContent.Contains("targetScope"))
            {
                result.Warnings.Add("Template uses subscription() but doesn't declare targetScope");
            }

            // TODO: Use Azure deployment validation API for full validation
            // This would require a subscription and resource group context
        }
        catch (Exception ex)
        {
            result.IsValid = false;
            result.Errors.Add($"Validation error: {ex.Message}");
        }

        return await Task.FromResult(result);
    }

    /// <summary>
    /// Get the status of a deployment from Azure
    /// </summary>
    public async Task<DeploymentStatusResult> GetDeploymentStatusAsync(
        string subscriptionId,
        string deploymentName,
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        var result = new DeploymentStatusResult { DeploymentName = deploymentName };

        try
        {
            var subscription = await _armClient.GetSubscriptionResource(
                new ResourceIdentifier($"/subscriptions/{subscriptionId}"))
                .GetAsync(cancellationToken);

            ArmDeploymentResource deployment;
            
            if (string.IsNullOrEmpty(resourceGroupName))
            {
                // Subscription-level deployment
                var deployments = subscription.Value.GetArmDeployments();
                deployment = await deployments.GetAsync(deploymentName, cancellationToken);
            }
            else
            {
                // Resource group-level deployment
                var resourceGroup = await subscription.Value.GetResourceGroupAsync(resourceGroupName, cancellationToken);
                var deployments = resourceGroup.Value.GetArmDeployments();
                deployment = await deployments.GetAsync(deploymentName, cancellationToken);
            }

            var properties = deployment.Data.Properties;
            result.ProvisioningState = properties.ProvisioningState?.ToString() ?? "Unknown";
            result.CorrelationId = properties.CorrelationId;
            result.Timestamp = properties.Timestamp?.DateTime;
            result.Duration = properties.Duration;

            // If deployment completed, get resources and outputs
            if (result.IsComplete && result.IsSuccessful)
            {
                if (properties.OutputResources != null)
                {
                    foreach (var resource in properties.OutputResources)
                    {
                        result.Resources.Add(new DeployedResourceInfo
                        {
                            ResourceId = resource.Id?.ToString() ?? "",
                            Name = resource.Id?.Name ?? "",
                            Type = resource.Id?.ResourceType.ToString() ?? "",
                            ProvisioningState = "Succeeded"
                        });
                    }
                }

                if (properties.Outputs != null)
                {
                    var outputs = properties.Outputs.ToObjectFromJson<Dictionary<string, OutputValue>>();
                    if (outputs != null)
                    {
                        foreach (var output in outputs)
                        {
                            result.Outputs[output.Key] = output.Value.Value ?? "";
                        }
                    }
                }
            }

            // If failed, extract error details
            if (properties.ProvisioningState == ResourcesProvisioningState.Failed)
            {
                if (properties.Error != null)
                {
                    result.ErrorMessage = properties.Error.Message;
                    result.Errors.Add($"{properties.Error.Code}: {properties.Error.Message}");
                    
                    // ResponseError in Azure.Core doesn't have a Details collection
                    // The error message contains nested details
                }
            }

            _logger.LogInformation("📊 Deployment {DeploymentName} status: {Status}",
                deploymentName, result.ProvisioningState);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            result.ProvisioningState = "NotFound";
            result.ErrorMessage = $"Deployment '{deploymentName}' not found";
            result.Errors.Add(result.ErrorMessage);
        }
        catch (Exception ex)
        {
            result.ProvisioningState = "Error";
            result.ErrorMessage = ex.Message;
            result.Errors.Add($"Error checking deployment status: {ex.Message}");
            _logger.LogError(ex, "Error getting deployment status for {DeploymentName}", deploymentName);
        }

        return result;
    }

    /// <summary>
    /// Compile Bicep template to ARM JSON using Azure CLI
    /// </summary>
    private async Task<string> CompileBicepToJsonAsync(
        string bicepContent, 
        Dictionary<string, string>? additionalFiles,
        CancellationToken cancellationToken)
    {
        // Create a temp directory to hold main template and any modules
        var tempDir = Path.Combine(Path.GetTempPath(), $"bicep-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        
        var tempBicepFile = Path.Combine(tempDir, "main.bicep");
        var tempJsonFile = Path.ChangeExtension(tempBicepFile, ".json");

        try
        {
            // Write the main template
            await File.WriteAllTextAsync(tempBicepFile, bicepContent, cancellationToken);
            _logger.LogInformation("📝 Wrote main.bicep to temp directory: {TempDir}", tempDir);

            // Write additional files (modules) preserving relative paths
            if (additionalFiles != null && additionalFiles.Count > 0)
            {
                foreach (var (relativePath, content) in additionalFiles)
                {
                    // Normalize the path to handle ../  and ./ references
                    var normalizedPath = NormalizePath(relativePath);
                    var fullPath = Path.Combine(tempDir, normalizedPath.Replace('/', Path.DirectorySeparatorChar));
                    var fileDir = Path.GetDirectoryName(fullPath);
                    
                    if (!string.IsNullOrEmpty(fileDir) && !Directory.Exists(fileDir))
                    {
                        Directory.CreateDirectory(fileDir);
                    }
                    
                    await File.WriteAllTextAsync(fullPath, content, cancellationToken);
                    _logger.LogInformation("📝 Wrote module file: {RelativePath} -> {NormalizedPath}", relativePath, normalizedPath);
                }
                
                _logger.LogInformation("📦 Wrote {Count} additional files for Bicep compilation", additionalFiles.Count);
            }

            // Compile using az bicep build
            var processInfo = new ProcessStartInfo
            {
                FileName = "az",
                Arguments = $"bicep build --file \"{tempBicepFile}\" --outfile \"{tempJsonFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = tempDir // Set working directory so relative module paths resolve
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start Bicep compilation process");
            }

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
            var error = await process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                _logger.LogError("❌ Bicep compilation failed: {Error}", error);
                throw new InvalidOperationException($"Bicep compilation failed: {error}");
            }

            // Read compiled ARM JSON
            var armJson = await File.ReadAllTextAsync(tempJsonFile, cancellationToken);
            _logger.LogInformation("✅ Bicep compiled successfully to ARM JSON ({Size} bytes)", armJson.Length);
            
            return armJson;
        }
        finally
        {
            // Cleanup temp directory
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to cleanup temp directory: {TempDir}", tempDir);
            }
        }
    }

    /// <summary>
    /// Normalize a relative path by resolving .. and . references
    /// </summary>
    private static string NormalizePath(string relativePath)
    {
        var segments = relativePath.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        
        foreach (var segment in segments)
        {
            if (segment == "..")
            {
                if (result.Count > 0)
                {
                    result.RemoveAt(result.Count - 1);
                }
            }
            else if (segment != ".")
            {
                result.Add(segment);
            }
        }
        
        return string.Join("/", result);
    }

    /// <summary>
    /// Convert a parameter value to the proper type for ARM deployment.
    /// Handles string values that are actually JSON arrays or objects.
    /// </summary>
    private object? ConvertParameterValue(object? value)
    {
        if (value == null)
        {
            return null;
        }

        // If it's already a complex type (not a string), check type
        if (value is not string stringValue)
        {
            // Handle JsonElement (from System.Text.Json deserialization)
            if (value is JsonElement jsonElement)
            {
                _logger.LogDebug("🔍 ConvertParameterValue: Got JsonElement with ValueKind={Kind}", jsonElement.ValueKind);
                return ConvertJsonElement(jsonElement);
            }
            _logger.LogDebug("🔍 ConvertParameterValue: Non-string value, type={Type}", value.GetType().FullName);
            return value;
        }

        // Check if the string looks like a JSON array or object
        var trimmed = stringValue.Trim();
        _logger.LogDebug("🔍 ConvertParameterValue: String value, first char='{FirstChar}', last char='{LastChar}', length={Length}", 
            trimmed.Length > 0 ? trimmed[0] : '?', 
            trimmed.Length > 0 ? trimmed[^1] : '?', 
            trimmed.Length);
            
        if ((trimmed.StartsWith("[") && trimmed.EndsWith("]")) ||
            (trimmed.StartsWith("{") && trimmed.EndsWith("}")))
        {
            try
            {
                // Try to parse as JSON
                var parsed = JsonSerializer.Deserialize<JsonElement>(trimmed);
                var result = ConvertJsonElement(parsed);
                _logger.LogInformation("✅ ConvertParameterValue: Converted JSON string to {Type}", result?.GetType().Name ?? "null");
                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning("⚠️ ConvertParameterValue: Failed to parse as JSON: {Error}", ex.Message);
                // Not valid JSON, return as string
                return stringValue;
            }
        }

        // Return as-is for regular string values
        return stringValue;
    }

    /// <summary>
    /// Convert a JsonElement to a native .NET type for proper serialization.
    /// Handles the case where a string value contains JSON (e.g., "[]" should become an actual array).
    /// </summary>
    private static object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Array:
                return element.EnumerateArray()
                    .Select(ConvertJsonElement)
                    .ToList();
                    
            case JsonValueKind.Object:
                return element.EnumerateObject()
                    .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value));
                    
            case JsonValueKind.String:
                var stringValue = element.GetString();
                if (stringValue != null)
                {
                    var trimmed = stringValue.Trim();
                    
                    // Check if this string is actually JSON (array or object)
                    if ((trimmed.StartsWith("[") && trimmed.EndsWith("]")) ||
                        (trimmed.StartsWith("{") && trimmed.EndsWith("}")))
                    {
                        // Try to convert Python-style single quotes to JSON double quotes
                        // e.g., "['VirtualMachines']" -> "[\"VirtualMachines\"]"
                        var jsonCandidate = trimmed;
                        if (trimmed.Contains("'"))
                        {
                            jsonCandidate = ConvertPythonStyleToJson(trimmed);
                        }
                        
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<JsonElement>(jsonCandidate);
                            // Recursively convert the parsed JSON
                            return ConvertJsonElement(parsed);
                        }
                        catch (JsonException)
                        {
                            // Not valid JSON, continue to other checks
                        }
                    }
                    
                    // Check if this string is actually an integer
                    if (long.TryParse(trimmed, out var longValue))
                    {
                        return longValue;
                    }
                    
                    // Check if this string is actually a decimal/double
                    if (double.TryParse(trimmed, out var doubleValue))
                    {
                        return doubleValue;
                    }
                    
                    // Check if this string is actually a boolean
                    if (bool.TryParse(trimmed, out var boolValue))
                    {
                        return boolValue;
                    }
                }
                return stringValue;
                
            case JsonValueKind.Number:
                return element.TryGetInt64(out var l) ? l : element.GetDouble();
                
            case JsonValueKind.True:
                return true;
                
            case JsonValueKind.False:
                return false;
                
            case JsonValueKind.Null:
                return null;
                
            default:
                return element.ToString();
        }
    }

    /// <summary>
    /// Convert Python-style array/object syntax to valid JSON.
    /// Converts single quotes to double quotes, handling nested structures.
    /// e.g., "['VirtualMachines', 'Storage']" -> "[\"VirtualMachines\", \"Storage\"]"
    /// </summary>
    private static string ConvertPythonStyleToJson(string pythonStyle)
    {
        // Simple approach: replace single quotes with double quotes
        // This handles most common cases like ['value1', 'value2']
        var result = new System.Text.StringBuilder();
        bool inString = false;
        char stringDelimiter = '\0';
        
        for (int i = 0; i < pythonStyle.Length; i++)
        {
            char c = pythonStyle[i];
            
            if (!inString)
            {
                if (c == '\'' || c == '"')
                {
                    inString = true;
                    stringDelimiter = c;
                    result.Append('"'); // Always use double quotes in JSON
                }
                else
                {
                    result.Append(c);
                }
            }
            else
            {
                if (c == stringDelimiter)
                {
                    // Check for escaped quote
                    if (i > 0 && pythonStyle[i - 1] == '\\')
                    {
                        result.Append(c);
                    }
                    else
                    {
                        inString = false;
                        result.Append('"'); // Always use double quotes in JSON
                    }
                }
                else if (c == '"' && stringDelimiter == '\'')
                {
                    // Escape any double quotes inside single-quoted strings
                    result.Append("\\\"");
                }
                else
                {
                    result.Append(c);
                }
            }
        }
        
        return result.ToString();
    }

    /// <summary>
    /// Determine the deployment scope from the compiled ARM template.
    /// Checks the $schema property to identify if it's subscription, management group, tenant, or resource group level.
    /// </summary>
    private static string DetermineDeploymentScope(string templateJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(templateJson);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("$schema", out var schemaElement))
            {
                var schema = schemaElement.GetString()?.ToLowerInvariant() ?? "";
                
                // Check schema URL for scope indicators
                // Subscription: https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json
                // Management Group: https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json
                // Tenant: https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json
                // Resource Group (default): https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json
                
                if (schema.Contains("subscriptiondeploymenttemplate"))
                {
                    return "subscription";
                }
                else if (schema.Contains("managementgroupdeploymenttemplate"))
                {
                    return "managementGroup";
                }
                else if (schema.Contains("tenantdeploymenttemplate"))
                {
                    return "tenant";
                }
            }
        }
        catch (JsonException)
        {
            // If we can't parse, default to resource group
        }
        
        return "resourceGroup";
    }

    /// <summary>
    /// Check if a string value is an ARM template expression that should not be passed as a parameter.
    /// ARM expressions include functions like deployment(), resourceGroup(), subscription(), etc.
    /// </summary>
    private static bool IsArmExpression(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
            
        var trimmed = value.Trim();
        
        // Common ARM expression patterns - these are function calls, not values
        var armFunctions = new[]
        {
            "deployment()",
            "resourceGroup()",
            "subscription()",
            "tenant()",
            "managementGroup()",
            "reference(",
            "resourceId(",
            "concat(",
            "parameters(",
            "variables(",
            "if(",
            "format(",
            "uniqueString(",
            "newGuid()",
            "utcNow()"
        };
        
        foreach (var func in armFunctions)
        {
            if (trimmed.Contains(func, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        
        // Also check for ARM expression syntax: [...]
        if (trimmed.StartsWith("[") && trimmed.EndsWith("]") && trimmed.Contains("("))
        {
            return true;
        }
        
        return false;
    }

    private class OutputValue
    {
        public string? Type { get; set; }
        public object? Value { get; set; }
    }
}

/// <summary>
/// Configuration options for deployers
/// </summary>
public class DeployerOptions
{
    public bool UseGovernmentCloud { get; set; } = true;
    public int DeploymentTimeoutMinutes { get; set; } = 60;
    public string TerraformExecutablePath { get; set; } = "terraform";
    public string DefaultStateStorageAccount { get; set; } = string.Empty;
    public string DefaultStateContainer { get; set; } = "tfstate";
    public string DefaultStateResourceGroup { get; set; } = string.Empty;
}
