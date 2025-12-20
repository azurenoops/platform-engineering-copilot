using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Models.Compliance;

namespace Platform.Engineering.Copilot.Infrastructure.Core;

/// <summary>
/// Partial class containing Azure Template Generation functions:
/// - Generating ARM, Bicep, Terraform templates
/// - Generating configuration files (YAML, JSON)
/// - Validating infrastructure-as-code templates
/// - Formatting generated files for output
/// </summary>
public partial class InfrastructurePlugin
{
    // ========== AZURE TEMPLATE INFRASTRUCTURE FUNCTIONS ==========

    [KernelFunction("generate_infrastructure_template")]
    [Description("Generate complete Bicep or Terraform infrastructure templates for Azure resources from natural language descriptions. " +
                 "THIS IS THE PRIMARY FUNCTION for creating new infrastructure - use this for ANY request to create/provision/deploy NEW Azure resources. " +
                 "For multiple resources, call this function multiple times (once per resource type). " +
                 "Examples: 'Create AKS cluster', 'Deploy storage account', 'Set up virtual network with monitoring'. " +
                 "Use smart defaults - don't ask for missing details, infer from context.")]
    public async Task<string> GenerateInfrastructureTemplateAsync(
        [Description("Description of the specific resource to deploy. Examples: 'SQL database for application data', 'Storage account for blob storage', 'Virtual network with web/app/data subnets'")]
        string description,
        [Description("Single resource type to deploy. Examples: 'sql-database', 'storage-account', 'vnet', 'aks', 'keyvault', 'app-service', 'cosmos-db'. For multiple resources, call this function multiple times.")]
        string resourceType,
        [Description("Azure region/location. Examples: 'usgovvirginia', 'eastus', 'westus2', 'centralus'. Default: 'eastus'")]
        string location = "eastus",
        [Description("Number of nodes/instances (for AKS, VMs, etc.). Default: 3")]
        int nodeCount = 3,
        [Description("Subscription ID where resources will be deployed. Optional.")]
        string? subscriptionId = null,
        [Description("Template format: 'bicep' or 'terraform'. Default: 'bicep'")]
        string templateFormat = "bicep",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔧 generate_infrastructure_template CALLED - Format: {Format}, ResourceType: {ResourceType}, Description: {Description}",
                templateFormat, resourceType, description);

            // Map resource type to infrastructure format
            var infraFormat = templateFormat.ToLowerInvariant() == "terraform"
                ? InfrastructureFormat.Terraform
                : InfrastructureFormat.Bicep;

            // ✅ CHECK CACHE FIRST: Return cached template if available (avoids LLM call)
            var cachedResponse = GetCachedTemplate(_currentConversationId, resourceType, templateFormat);
            if (!string.IsNullOrEmpty(cachedResponse))
            {
                _logger.LogInformation("✅ Returning CACHED template for {ResourceType} - avoiding LLM call", resourceType);
                return cachedResponse;
            }

            // Map resource type to compute platform
            var computePlatform = MapResourceTypeToComputePlatform(resourceType);

            // Get Azure best practices from MCP server
            string? bestPracticesGuidance = null;
            try
            {
                await _azureMcpClient.InitializeAsync(cancellationToken);
                _logger.LogInformation("📚 Fetching Azure best practices for {ResourceType} via Azure MCP", resourceType);

                var toolName = infraFormat == InfrastructureFormat.Terraform ? "azureterraformbestpractices" : "get_bestpractices";
                var bestPractices = await _azureMcpClient.CallToolAsync(toolName,
                    new Dictionary<string, object?>
                    {
                        ["resourceTypes"] = resourceType
                    }, cancellationToken);

                if (bestPractices.Success)
                {
                    bestPracticesGuidance = bestPractices.Result?.ToString();
                    _logger.LogInformation("✅ Retrieved Azure best practices guidance ({Length} chars)", bestPracticesGuidance?.Length ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Could not retrieve best practices from Azure MCP - continuing without them");
            }

            // Create template generation request with resource-specific defaults
            var request = BuildTemplateGenerationRequest(
                resourceType,
                description,
                infraFormat,
                computePlatform,
                location,
                nodeCount,
                subscriptionId);

            // Add best practices to description if available
            if (!string.IsNullOrEmpty(bestPracticesGuidance))
            {
                request.Description = $"{request.Description}\n\n=== AZURE BEST PRACTICES ===\n{bestPracticesGuidance}";
            }

            // Generate the template
            var result = await _templateGenerator.GenerateTemplateAsync(request, cancellationToken);

            _logger.LogInformation("📄 Template generation result - Success: {Success}, File count: {FileCount}, Error: {Error}",
                result.Success, result.Files?.Count ?? 0, result.ErrorMessage ?? "none");

            if (!result.Success || !result.Files.Any())
            {
                _logger.LogWarning("❌ Template generation failed or returned no files");
                return $"❌ Failed to generate template: {result.ErrorMessage ?? "Unknown error"}";
            }

            _logger.LogInformation("✅ Successfully generated {Count} template files", result.Files.Count);

            // Generate a unique template name based on resource type and timestamp
            var templateName = $"{resourceType}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            _lastGeneratedTemplateName = templateName;

            // Store files in shared memory for later retrieval (in-memory fallback)
            _logger.LogInformation("💾 About to store files. ConversationId: '{ConversationId}', IsNull: {IsNull}, IsEmpty: {IsEmpty}",
                _currentConversationId, _currentConversationId == null, string.IsNullOrEmpty(_currentConversationId));

            if (!string.IsNullOrEmpty(_currentConversationId))
            {
                _sharedMemory.StoreGeneratedFiles(_currentConversationId, result.Files);
                _logger.LogInformation("📦 Stored {Count} files in SharedMemory for conversation {ConversationId}",
                    result.Files.Count, _currentConversationId);
            }
            else
            {
                _logger.LogWarning("⚠️ SKIPPED storing files in SharedMemory because ConversationId is null or empty!");
            }

            // Also persist to database for durability
            try
            {
                // Create template object as Dictionary for proper serialization by TemplateStorageService
                var tagsDict = new Dictionary<string, string>
                {
                    ["location"] = location,
                    ["resourceType"] = resourceType,
                    ["conversationId"] = _currentConversationId ?? "unknown"
                };

                var templateToStore = new Dictionary<string, object>
                {
                    ["name"] = templateName,
                    ["description"] = description,
                    ["templateType"] = resourceType,
                    ["version"] = "1.0.0",
                    ["format"] = infraFormat.ToString().ToLowerInvariant(),
                    ["content"] = result.Files.FirstOrDefault().Value ?? "",
                    ["files"] = result.Files,
                    ["createdBy"] = "InfrastructureAgent",
                    ["azureService"] = resourceType,
                    ["tags"] = System.Text.Json.JsonSerializer.Serialize(tagsDict)
                };

                await _templateStorageService.StoreTemplateAsync(templateName, templateToStore, cancellationToken);
                _logger.LogInformation("💾 Persisted template '{TemplateName}' to database with {FileCount} files for conversation {ConversationId}",
                    templateName, result.Files.Count, _currentConversationId ?? "unknown");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to persist template to database, but SharedMemory storage succeeded");
            }

            // Format the response with summary - templates are stored in DB for retrieval
            var response = new StringBuilder();
            response.AppendLine($"✅ Generated {infraFormat} template for {resourceType}");
            response.AppendLine();
            response.AppendLine($"📍 **Location**: {location}");
            if (resourceType.ToLowerInvariant() == "aks")
            {
                response.AppendLine($"🔢 **Node Count**: {nodeCount}");
            }
            if (!string.IsNullOrEmpty(subscriptionId))
            {
                response.AppendLine($"📦 **Subscription**: {subscriptionId}");
            }
            response.AppendLine();
            response.AppendLine($"📄 **Generated {result.Files.Count} Files:**");
            response.AppendLine();

            // List files with sizes
            foreach (var file in result.Files.OrderBy(f => f.Key))
            {
                var lines = file.Value.Split('\n').Length;
                var sizeKb = file.Value.Length / 1024.0;
                response.AppendLine($"- `{file.Key}` ({lines} lines, {sizeKb:F1} KB)");
            }

            response.AppendLine();
            response.AppendLine("💡 **To view the code:** Ask me to \"Show all generated files\" or \"Show the [filename]\" to see specific files.");
            response.AppendLine();

            response.AppendLine("💡 **Next Steps:**");
            if (infraFormat == InfrastructureFormat.Bicep)
            {
                response.AppendLine("1. Click **'📁 Create Project in Workspace'** below to save all files");
                response.AppendLine("2. Review and customize parameters");
                response.AppendLine("3. Deploy: `az deployment group create --resource-group <rg-name> --template-file main.bicep`");
            }
            else
            {
                response.AppendLine("1. Click **'📁 Create Project in Workspace'** below to save all files");
                response.AppendLine("2. Review and customize terraform parameters");
                response.AppendLine("3. Run `terraform init`");
                response.AppendLine("4. Run `terraform plan` to review changes");
                response.AppendLine("5. Run `terraform apply` to deploy");
            }

            var responseText = response.ToString();
            
            // ✅ SAFETY CHECK: Ensure response is never blank
            if (string.IsNullOrWhiteSpace(responseText))
            {
                _logger.LogWarning("⚠️ UNEXPECTED: Response text is empty/whitespace! This should never happen.");
                responseText = $"✅ Generated {infraFormat} template for {resourceType}\n\n⚠️ Note: Response formatting issue occurred but files were generated and stored successfully.";
            }
            
            // ✅ CACHE THE SUCCESSFUL RESPONSE: Store in cache to avoid LLM call next time
            CacheTemplate(_currentConversationId, resourceType, templateFormat, responseText);

            _logger.LogInformation("✅ Returning template response ({ContentLength} chars) for {ResourceType}", responseText.Length, resourceType);
            return responseText;
        }
        catch (OperationCanceledException ocex)
        {
            _logger.LogWarning(ocex, "❌ Template generation was cancelled for {ResourceType}", resourceType);
            return $"⏱️ Template generation request was cancelled or timed out for {resourceType}. Please try again.";
        }
        catch (HttpRequestException hexp)
        {
            _logger.LogWarning(hexp, "❌ Network error during template generation for {ResourceType}", resourceType);
            return $"🌐 Network error during template generation: {hexp.Message}. This may be due to rate limiting. Please try again in a moment.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generating infrastructure template for {ResourceType}: {ErrorType} - {ErrorMessage}",
                resourceType, ex.GetType().Name, ex.Message);
            var errorMsg = $"❌ Error generating template: {ex.Message}";
            
            // Log stack trace for debugging
            if (!string.IsNullOrEmpty(ex.StackTrace))
            {
                _logger.LogDebug("Stack trace: {StackTrace}", ex.StackTrace);
            }
            
            return errorMsg;
        }
    }

    [KernelFunction("generate_compliant_infrastructure_template")]
    [Description("Generate compliance-enhanced Bicep or Terraform templates with FedRAMP High, DoD IL5, NIST 800-53, SOC2, or GDPR controls automatically injected. IMPORTANT: Only call this AFTER gathering requirements through conversation. Ask about environment type, specific compliance needs, monitoring preferences, etc. Use when users explicitly mention compliance frameworks like 'FedRAMP', 'DoD IL5', 'NIST 800-53', 'SOC2', 'GDPR', 'compliant', 'compliance', 'secure', 'hardened'.")]
    public async Task<string> GenerateCompliantInfrastructureTemplateAsync(
        [Description("Natural language description of what to deploy. Example: 'AKS cluster with 3 nodes', 'PostgreSQL database', 'Storage account'")]
        string description,
        [Description("Single resource type to deploy. Examples: 'sql-database', 'storage-account', 'vnet', 'aks', 'keyvault', 'app-service', 'cosmos-db'. Optional - will be inferred from description if not provided.")]
        string? resourceType = null,
        [Description("Compliance framework to apply: 'FedRAMP-High', 'DoD-IL5', 'NIST-800-53', 'SOC2', 'GDPR'. Default: FedRAMP-High")]
        string complianceFramework = "NIST-800-53",
        [Description("Azure region/location. Examples: 'usgovvirginia', 'eastus', 'westus2', 'centralus'. Default: 'eastus'")]
        string location = "eastus",
        [Description("Number of nodes/instances (for AKS, VMs, etc.). Default: 3")]
        int nodeCount = 3,
        [Description("Subscription ID where resources will be deployed. Optional.")]
        string? subscriptionId = null,
        [Description("Template format: 'bicep' or 'terraform'. Default: 'bicep'")]
        string templateFormat = "bicep",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔧 generate_infrastructure_template CALLED - Format: {Format}, ResourceType: {ResourceType}, Description: {Description}",
                templateFormat, resourceType, description);

            // Determine resource type from parameter or infer from description
            var effectiveResourceType = resourceType;
            if (string.IsNullOrWhiteSpace(effectiveResourceType))
            {
                // Try to infer from description
                var descriptionLower = description.ToLowerInvariant();
                if (descriptionLower.Contains("storage") || descriptionLower.Contains("blob"))
                    effectiveResourceType = "storage";
                else if (descriptionLower.Contains("aks") || descriptionLower.Contains("kubernetes"))
                    effectiveResourceType = "aks";
                else if (descriptionLower.Contains("sql") || descriptionLower.Contains("database"))
                    effectiveResourceType = "database";
                else if (descriptionLower.Contains("keyvault") || descriptionLower.Contains("key vault"))
                    effectiveResourceType = "keyvault";
                else if (descriptionLower.Contains("vnet") || descriptionLower.Contains("network"))
                    effectiveResourceType = "vnet";
                else if (descriptionLower.Contains("appservice") || descriptionLower.Contains("app service") || descriptionLower.Contains("webapp"))
                    effectiveResourceType = "appservice";
                else
                    effectiveResourceType = "infrastructure"; // Generic fallback

                _logger.LogInformation("🔍 Inferred resource type '{ResourceType}' from description", effectiveResourceType);
            }

            // Map resource type to infrastructure format
            var infraFormat = templateFormat.ToLowerInvariant() == "terraform"
                ? InfrastructureFormat.Terraform
                : InfrastructureFormat.Bicep;

            // Map resource type to compute platform
            var computePlatform = MapResourceTypeToComputePlatform(effectiveResourceType);

            // Get Azure best practices from MCP server
            string? bestPracticesGuidance = null;
            try
            {
                await _azureMcpClient.InitializeAsync(cancellationToken);
                _logger.LogInformation("📚 Fetching Azure best practices for {ResourceType} via Azure MCP", effectiveResourceType);

                var toolName = infraFormat == InfrastructureFormat.Terraform ? "azureterraformbestpractices" : "get_bestpractices";
                var bestPractices = await _azureMcpClient.CallToolAsync(toolName,
                    new Dictionary<string, object?>
                    {
                        ["resourceTypes"] = effectiveResourceType
                    }, cancellationToken);

                if (bestPractices.Success)
                {
                    bestPracticesGuidance = bestPractices.Result?.ToString();
                    _logger.LogInformation("✅ Retrieved Azure best practices guidance ({Length} chars)", bestPracticesGuidance?.Length ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Could not retrieve best practices from Azure MCP - continuing without them");
            }

            // Create template generation request with resource-specific defaults
            var request = BuildTemplateGenerationRequest(
                effectiveResourceType,
                description,
                infraFormat,
                computePlatform,
                location,
                nodeCount,
                subscriptionId);

            // Add best practices to description if available
            if (!string.IsNullOrEmpty(bestPracticesGuidance))
            {
                request.Description = $"{request.Description}\n\n=== AZURE BEST PRACTICES ===\n{bestPracticesGuidance}";
            }

            // Use compliance enhancer to inject controls and validate (if enabled)
            TemplateGenerationResult result;
            if (_complianceEnhancer != null)
            {
                result = await _complianceEnhancer.EnhanceWithComplianceAsync(
                    request,
                    complianceFramework,
                    cancellationToken);
            }
            else
            {
                // Fallback to basic template generation without compliance enhancement
                _logger.LogWarning("Compliance enhancement is disabled. Generating template without compliance controls.");
                result = await _templateGenerator.GenerateTemplateAsync(request, cancellationToken);
            }

            if (!result.Success)
            {
                return $"❌ Error generating compliant template: {result.ErrorMessage}";
            }

            // Generate a unique template name
            var templateName = $"compliant-{description.Replace(" ", "-").ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            _lastGeneratedTemplateName = templateName;

            // Store files in shared memory for later retrieval
            _logger.LogInformation("💾 About to store files. ConversationId: '{ConversationId}', IsNull: {IsNull}, IsEmpty: {IsEmpty}",
                _currentConversationId, _currentConversationId == null, string.IsNullOrEmpty(_currentConversationId));

            if (!string.IsNullOrEmpty(_currentConversationId))
            {
                _sharedMemory.StoreGeneratedFiles(_currentConversationId, result.Files);
                _logger.LogInformation("📦 Stored {Count} files in SharedMemory for conversation {ConversationId}",
                    result.Files.Count, _currentConversationId);
            }
            else
            {
                _logger.LogWarning("⚠️ SKIPPED storing files because ConversationId is null or empty!");
            }

            // Also persist to database for durability
            try
            {
                var tagsDict = new Dictionary<string, string>
                {
                    ["location"] = location,
                    ["complianceFramework"] = complianceFramework,
                    ["conversationId"] = _currentConversationId ?? "unknown"
                };

                var templateToStore = new Dictionary<string, object>
                {
                    ["name"] = templateName,
                    ["description"] = $"Compliance-enhanced: {description}",
                    ["templateType"] = "compliant-infrastructure",
                    ["version"] = "1.0.0",
                    ["format"] = infraFormat.ToString().ToLowerInvariant(),
                    ["content"] = result.Files.FirstOrDefault().Value ?? "",
                    ["files"] = result.Files,
                    ["createdBy"] = "InfrastructureAgent",
                    ["azureService"] = description,
                    ["tags"] = System.Text.Json.JsonSerializer.Serialize(tagsDict)
                };

                await _templateStorageService.StoreTemplateAsync(templateName, templateToStore, cancellationToken);
                _logger.LogInformation("💾 Persisted compliant template '{TemplateName}' to database with {FileCount} files for conversation {ConversationId}",
                    templateName, result.Files.Count, _currentConversationId ?? "unknown");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to persist compliant template to database, but SharedMemory storage succeeded");
            }

            // Format response
            var response = new StringBuilder();
            response.AppendLine($"✅ **Compliance-Enhanced Infrastructure Template Generated**");
            response.AppendLine();
            response.AppendLine($"🛡️ **Compliance Framework**: {complianceFramework}");
            response.AppendLine($"📝 **Description**: {description}");
            response.AppendLine($"🔧 **Format**: {infraFormat}");
            response.AppendLine($"🌍 **Location**: {location}");
            if (nodeCount > 0)
            {
                response.AppendLine($"🔢 **Node Count**: {nodeCount}");
            }
            response.AppendLine();

            // Add compliance summary from result
            if (!string.IsNullOrEmpty(result.Summary))
            {
                response.AppendLine(result.Summary);
                response.AppendLine();
            }

            response.AppendLine("📄 **Generated Files:**");
            response.AppendLine();

            // List files with sizes (don't include full code blocks to avoid token limits)
            foreach (var file in result.Files.OrderBy(f => f.Key))
            {
                var lines = file.Value.Split('\n').Length;
                var sizeKb = file.Value.Length / 1024.0;
                response.AppendLine($"- `{file.Key}` ({lines} lines, {sizeKb:F1} KB)");
            }

            response.AppendLine();
            response.AppendLine("💡 **To view the code:** Ask me to \"Show all generated files\" or \"Show the [filename]\" to see specific files.");
            response.AppendLine();

            response.AppendLine("💡 **Next Steps:**");
            if (infraFormat == InfrastructureFormat.Bicep)
            {
                response.AppendLine("1. Click **'📁 Create Project in Workspace'** below to save all files");
                response.AppendLine("2. Review the compliance-enhanced template");
                response.AppendLine("3. Verify all required NIST controls are implemented");
                response.AppendLine("4. Customize parameters in `parameters.json` if needed");
                response.AppendLine("5. Deploy: `az deployment group create --resource-group <rg-name> --template-file main.bicep`");
                response.AppendLine("6. After deployment, validate that all compliance controls are properly configured and active");
            }
            else
            {
                response.AppendLine("1. Click **'📁 Create Project in Workspace'** below to save all files");
                response.AppendLine("2. Review the compliance-enhanced template");
                response.AppendLine("3. Verify all required NIST controls are implemented");
                response.AppendLine("4. Run `terraform init` and `terraform plan`");
                response.AppendLine("5. Deploy with `terraform apply`");
                response.AppendLine("6. After deployment, validate that all compliance controls are properly configured and active");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliant infrastructure template: {Description}", description);
            return $"❌ Error generating compliant template: {ex.Message}";
        }
    }

    [KernelFunction("generate_composite_infrastructure")]
    [Description("Generate multi-resource infrastructure with a MAIN ORCHESTRATOR file (main.bicep or main.tf) that coordinates all modules. " +
                 "Use this for complex deployments: 'AKS with VNet', '3-tier architecture', 'landing zone', 'microservices platform'. " +
                 "ALWAYS produces: 1) main.bicep/main.tf at root, 2) modules/ folder with per-resource templates. " +
                 "Supports patterns: 'three-tier' (VNet with web/app/data subnets + NSGs), 'aks-with-vnet' (VNet + AKS + ACR + KeyVault), " +
                 "'landing-zone' (hub-spoke VNet + management + AKS), 'microservices' (AKS + observability), 'serverless' (Functions + Storage), " +
                 "'scca-compliant' (SCCA-compliant Landing Zone with Bastion).")]
    public async Task<string> GenerateCompositeInfrastructureAsync(
        [Description("Base name for the deployment (used as prefix for all resources). Example: 'myapp', 'platform', 'contoso'")]
        string serviceName,
        [Description("Description of the infrastructure needed. Example: 'AKS cluster with supporting VNet and security', '3-tier web application with database'")]
        string description,
        [Description("Architecture pattern: 'three-tier', 'aks-with-vnet', 'landing-zone', 'microservices', 'serverless', 'data-platform', 'scca-compliant', or 'custom'. Default: 'aks-with-vnet'")]
        string architecturePattern = "aks-with-vnet",
        [Description("Azure region/location. Examples: 'usgovvirginia', 'eastus', 'westus2'. Default: 'eastus'")]
        string location = "eastus",
        [Description("Environment tier: 'dev', 'staging', 'prod'. Affects sizing and redundancy. Default: 'dev'")]
        string environment = "dev",
        [Description("Template format: 'bicep' or 'terraform'. Default: 'bicep'")]
        string templateFormat = "bicep",
        [Description("Subscription ID where resources will be deployed. Optional.")]
        string? subscriptionId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🏗️ generate_composite_infrastructure CALLED - Pattern: {Pattern}, Service: {Service}, Format: {Format}",
                architecturePattern, serviceName, templateFormat);

            // Map string pattern to enum
            var pattern = architecturePattern.ToLowerInvariant().Replace("-", "").Replace("_", "") switch
            {
                "threetier" or "3tier" => ArchitecturePattern.ThreeTier,
                "akswithvnet" or "aksvnet" => ArchitecturePattern.AksWithVNet,
                "landingzone" => ArchitecturePattern.LandingZone,
                "microservices" => ArchitecturePattern.Microservices,
                "serverless" => ArchitecturePattern.Serverless,
                "dataplatform" => ArchitecturePattern.DataPlatform,
                "sccacompliant" or "scca" => ArchitecturePattern.SccaCompliant,
                "custom" => ArchitecturePattern.Custom,
                _ => ArchitecturePattern.AksWithVNet  // Default
            };

            var format = templateFormat.ToLowerInvariant() == "terraform"
                ? InfrastructureFormat.Terraform
                : InfrastructureFormat.Bicep;

            // Build composite request
            var request = new CompositeInfrastructureRequest
            {
                ServiceName = serviceName,
                Description = description,
                Pattern = pattern,
                Format = format,
                Provider = CloudProvider.Azure,
                Region = location,
                Environment = environment,
                SubscriptionId = subscriptionId
            };

            // Get the composite generator from DI (needs to be injected)
            var compositeGenerator = _compositeGenerator;
            if (compositeGenerator == null)
            {
                _logger.LogWarning("⚠️ CompositeInfrastructureGenerator not available, falling back to single-resource generation");
                return await GenerateInfrastructureTemplateAsync(description, "aks", location, 3, subscriptionId, templateFormat, cancellationToken);
            }

            // Generate composite infrastructure
            var result = await compositeGenerator.GenerateAsync(request, cancellationToken);

            if (!result.Success || !result.Files.Any())
            {
                _logger.LogWarning("❌ Composite generation failed: {Error}", result.ErrorMessage);
                return $"❌ Failed to generate composite infrastructure: {result.ErrorMessage ?? "Unknown error"}";
            }

            _logger.LogInformation("✅ Generated composite infrastructure: {FileCount} files, {ModuleCount} modules",
                result.Files.Count, result.ModulePaths.Count);

            // Generate unique template name
            var templateName = $"composite-{pattern.ToString().ToLowerInvariant()}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
            _lastGeneratedTemplateName = templateName;

            // Store in SharedMemory
            if (!string.IsNullOrEmpty(_currentConversationId))
            {
                _sharedMemory.StoreGeneratedFiles(_currentConversationId, result.Files);
                _logger.LogInformation("📦 Stored {Count} composite files in SharedMemory", result.Files.Count);
            }

            // Persist to database
            try
            {
                var tagsDict = new Dictionary<string, string>
                {
                    ["location"] = location,
                    ["pattern"] = pattern.ToString(),
                    ["environment"] = environment,
                    ["conversationId"] = _currentConversationId ?? "unknown"
                };

                var templateToStore = new Dictionary<string, object>
                {
                    ["name"] = templateName,
                    ["description"] = description,
                    ["templateType"] = $"composite-{pattern}",
                    ["version"] = "1.0.0",
                    ["format"] = format.ToString().ToLowerInvariant(),
                    ["content"] = result.Files.GetValueOrDefault(result.MainFilePath) ?? "",
                    ["files"] = result.Files,
                    ["createdBy"] = "InfrastructureAgent-Composite",
                    ["azureService"] = "multi-resource",
                    ["tags"] = System.Text.Json.JsonSerializer.Serialize(tagsDict)
                };

                await _templateStorageService.StoreTemplateAsync(templateName, templateToStore, cancellationToken);
                _logger.LogInformation("💾 Persisted composite template '{TemplateName}' to database", templateName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Failed to persist composite template to database");
            }

            // Format response
            var response = new StringBuilder();
            response.AppendLine($"✅ Generated **{pattern}** composite infrastructure");
            response.AppendLine();
            response.AppendLine($"📐 **Architecture Pattern**: {pattern}");
            response.AppendLine($"📍 **Location**: {location}");
            response.AppendLine($"🏢 **Environment**: {environment}");
            response.AppendLine($"📋 **Format**: {format}");
            response.AppendLine();

            response.AppendLine($"📄 **Generated {result.Files.Count} Files:**");
            response.AppendLine();

            // Group files by directory
            response.AppendLine("**Root Files:**");
            foreach (var file in result.Files.Where(f => !f.Key.Contains('/')).OrderBy(f => f.Key))
            {
                var lines = file.Value.Split('\n').Length;
                response.AppendLine($"- `{file.Key}` ({lines} lines) ⭐ **Main Orchestrator**");
            }

            if (result.ModulePaths.Any())
            {
                response.AppendLine();
                response.AppendLine("**Modules:**");
                foreach (var modulePath in result.ModulePaths.OrderBy(m => m))
                {
                    var moduleFiles = result.Files.Where(f => f.Key.StartsWith(modulePath)).ToList();
                    response.AppendLine($"- `{modulePath}/` ({moduleFiles.Count} files)");
                }
            }

            response.AppendLine();
            response.AppendLine("🏗️ **Architecture:**");
            foreach (var resourceResult in result.ResourceResults.Where(r => r.Success))
            {
                response.AppendLine($"  └── {resourceResult.ResourceType} → `{resourceResult.ModulePath}`");
            }

            response.AppendLine();
            response.AppendLine("💡 **To view the code:** Ask me to \"Show all generated files\" or \"Show main.bicep\"");
            response.AppendLine();

            response.AppendLine("💡 **Next Steps:**");
            if (format == InfrastructureFormat.Bicep)
            {
                response.AppendLine("1. Click **'📁 Create Project in Workspace'** below to save all files");
                response.AppendLine("2. Review `main.bicep` - it orchestrates all modules");
                response.AppendLine("3. Customize `main.parameters.json` with your values");
                response.AppendLine($"4. Deploy: `az deployment group create --resource-group rg-{serviceName}-{environment} --template-file main.bicep --parameters main.parameters.json`");
            }
            else
            {
                response.AppendLine("1. Click **'📁 Create Project in Workspace'** below to save all files");
                response.AppendLine("2. Review `main.tf` - it orchestrates all modules");
                response.AppendLine("3. Customize `variables.tf` with your values");
                response.AppendLine("4. Run `terraform init && terraform plan && terraform apply`");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating composite infrastructure: {Pattern}", architecturePattern);
            return $"❌ Error generating composite infrastructure: {ex.Message}";
        }
    }


    [KernelFunction("get_module_files")]
    [Description("Retrieve all PREVIOUSLY GENERATED files for a specific module type (storage, aks, database, network). IMPORTANT: Use this when user asks to 'show storage files', 'display the storage module', etc. DO NOT call generate_infrastructure_template again. Files must already exist from a prior generation.")]
    public Task<string> GetModuleFilesAsync(
        [Description("Module type to retrieve. Valid values: 'storage', 'aks', 'database', 'network', 'appservice', 'containerapps'. Use lowercase.")]
        string moduleType)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentConversationId))
            {
                return Task.FromResult("❌ No conversation context available");
            }

            var allFiles = _sharedMemory.GetGeneratedFileNames(_currentConversationId);
            if (!allFiles.Any())
            {
                return Task.FromResult("❌ No generated files found. Please generate a template first.");
            }

            // Filter files by module type (matches path patterns like "modules/aks/" or "infra/modules/database/")
            var modulePattern = moduleType.ToLowerInvariant();
            var matchingFiles = allFiles
                .Where(f => f.ToLowerInvariant().Contains($"/{modulePattern}/") ||
                           f.ToLowerInvariant().Contains($"modules/{modulePattern}"))
                .OrderBy(f => f)
                .ToList();

            if (!matchingFiles.Any())
            {
                var availableModules = allFiles
                    .Where(f => f.Contains("/modules/") || f.Contains("modules/"))
                    .Select(f =>
                    {
                        var parts = f.Split('/');
                        var moduleIndex = Array.IndexOf(parts, "modules");
                        return moduleIndex >= 0 && moduleIndex < parts.Length - 1 ? parts[moduleIndex + 1] : null;
                    })
                    .Where(m => m != null)
                    .Distinct()
                    .ToList();

                if (availableModules.Any())
                {
                    return Task.FromResult($"❌ No files found for module '{moduleType}'. Available modules:\n" +
                        string.Join("\n", availableModules.Select(m => $"- {m}")));
                }
                return Task.FromResult($"❌ No files found for module '{moduleType}'.");
            }

            var response = new StringBuilder();
            response.AppendLine($"## 📦 {moduleType.ToUpperInvariant()} Module Files ({matchingFiles.Count} files)");
            response.AppendLine();

            foreach (var fileName in matchingFiles)
            {
                var content = _sharedMemory.GetGeneratedFile(_currentConversationId, fileName);
                if (content != null)
                {
                    response.AppendLine($"### 📁 {fileName}");
                    response.AppendLine();
                    response.AppendLine("```bicep");
                    response.AppendLine(content);
                    response.AppendLine("```");
                    response.AppendLine();
                }
            }

            return Task.FromResult(response.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving module files: {ModuleType}", moduleType);
            return Task.FromResult($"❌ Error retrieving module files: {ex.Message}");
        }
    }

    [KernelFunction("get_generated_file")]
    [Description("Retrieve and display a PREVIOUSLY GENERATED template file. IMPORTANT: Use this when user asks to 'show', 'display', or 'view' a specific file that was ALREADY generated. DO NOT call generate_infrastructure_template again. Files must already exist from a prior generation.")]
    public async Task<string> GetGeneratedFileAsync(
        [Description("Filename to retrieve. Can be partial (e.g., 'main.bicep') or full path (e.g., 'infra/modules/storage/main.bicep'). System will find the matching file.")]
        string fileName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔍 Attempting to retrieve file: {FileName}", fileName);

            // First try SharedMemory (in-memory cache)
            if (!string.IsNullOrEmpty(_currentConversationId))
            {
                var availableFiles = _sharedMemory.GetGeneratedFileNames(_currentConversationId);
                _logger.LogInformation("📦 Available files in memory: {Count}", availableFiles.Count);

                if (availableFiles.Any())
                {
                    // Try exact match first
                    var matchingFile = availableFiles.FirstOrDefault(f => f == fileName);

                    // If no exact match, try partial match (ends with the requested filename)
                    if (matchingFile == null)
                    {
                        matchingFile = availableFiles.FirstOrDefault(f => f.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
                    }

                    // If still no match, try case-insensitive contains
                    if (matchingFile == null)
                    {
                        matchingFile = availableFiles.FirstOrDefault(f => f.Contains(fileName, StringComparison.OrdinalIgnoreCase));
                    }

                    if (matchingFile != null)
                    {
                        var content = _sharedMemory.GetGeneratedFile(_currentConversationId, matchingFile);
                        if (content != null)
                        {
                            _logger.LogInformation("✅ Found file in SharedMemory: {MatchingFile}", matchingFile);
                            return FormatFileResponse(matchingFile, content);
                        }
                    }
                }
            }

            // Fallback: Try to retrieve from database using last generated template name
            _logger.LogInformation("📂 SharedMemory miss, checking database...");

            if (!string.IsNullOrEmpty(_lastGeneratedTemplateName))
            {
                var template = await _templateStorageService.GetTemplateByNameAsync(_lastGeneratedTemplateName, cancellationToken);
                if (template?.Files != null && template.Files.Any())
                {
                    var dbFile = template.Files.FirstOrDefault(f =>
                        f.FileName == fileName ||
                        f.FileName.EndsWith(fileName, StringComparison.OrdinalIgnoreCase) ||
                        f.FileName.Contains(fileName, StringComparison.OrdinalIgnoreCase));

                    if (dbFile != null)
                    {
                        _logger.LogInformation("✅ Found file in database: {FileName}", dbFile.FileName);
                        return FormatFileResponse(dbFile.FileName, dbFile.Content);
                    }
                }
            }

            // List recent templates that might have the file
            var recentTemplates = await _templateStorageService.ListAllTemplatesAsync(cancellationToken);
            var templatesWithFile = recentTemplates
                .Where(t => t.Files?.Any(f => f.FileName.Contains(fileName, StringComparison.OrdinalIgnoreCase)) == true)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .ToList();

            if (templatesWithFile.Any())
            {
                var firstMatch = templatesWithFile.First();
                var file = firstMatch.Files?.FirstOrDefault(f => f.FileName.Contains(fileName, StringComparison.OrdinalIgnoreCase));
                if (file != null)
                {
                    _logger.LogInformation("✅ Found file '{FileName}' in template '{TemplateName}'", file.FileName, firstMatch.Name);
                    return FormatFileResponse(file.FileName, file.Content);
                }
            }

            _logger.LogWarning("❌ File '{FileName}' not found in memory or database", fileName);
            return $"❌ File '{fileName}' not found. Please generate a template first using 'Generate a Bicep/Terraform template for...'";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file: {FileName}", fileName);
            return $"❌ Error retrieving file: {ex.Message}";
        }
    }

    [KernelFunction("get_all_generated_files")]
    [Description("Retrieve and display ALL PREVIOUSLY GENERATED template files at once. IMPORTANT: Use this when user asks to 'show all files', 'display everything', or 'show all generated templates'. DO NOT call generate_infrastructure_template again. Files must already exist from a prior generation. Warning: Response may be very long.")]
    public async Task<string> GetAllGeneratedFilesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // First try SharedMemory
            if (!string.IsNullOrEmpty(_currentConversationId))
            {
                var fileNames = _sharedMemory.GetGeneratedFileNames(_currentConversationId);
                if (fileNames.Any())
                {
                    _logger.LogInformation("📦 Found {Count} files in SharedMemory", fileNames.Count);
                    return FormatAllFilesFromMemory(fileNames);
                }
            }

            // Fallback to database
            _logger.LogInformation("📂 SharedMemory empty, checking database...");

            if (!string.IsNullOrEmpty(_lastGeneratedTemplateName))
            {
                var template = await _templateStorageService.GetTemplateByNameAsync(_lastGeneratedTemplateName, cancellationToken);
                if (template?.Files != null && template.Files.Any())
                {
                    _logger.LogInformation($"📦 Found {template.Files.Count()} files in database template '{template.Name}'");
                    return FormatAllFilesFromDatabase(template);
                }
            }

            // Try most recent template
            var recentTemplates = await _templateStorageService.ListAllTemplatesAsync(cancellationToken);
            var mostRecent = recentTemplates
                .Where(t => t.Files?.Any() == true)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefault();

            if (mostRecent?.Files != null)
            {
                _logger.LogInformation($"📦 Using most recent template {mostRecent.Name} with {mostRecent.Files.Count()} files");
                return FormatAllFilesFromDatabase(mostRecent);
            }

            return "❌ No generated files found. Please generate a template first using 'Generate a Bicep/Terraform template for...'";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all files");
            return $"❌ Error retrieving files: {ex.Message}";
        }
    }

    // ========== AZURE MCP ENHANCED FUNCTIONS ==========

    [KernelFunction("generate_template_with_best_practices")]
    [Description("Generate Azure infrastructure templates with built-in Microsoft best practices and schema validation. " +
                 "Combines dynamic template generation with Azure MCP best practices guidance and Bicep schema validation. " +
                 "Use when you want infrastructure templates that follow Azure Well-Architected Framework by default.")]
    public async Task<string> GenerateInfrastructureTemplateWithBestPracticesAsync(
        [Description("Natural language infrastructure requirements (e.g., 'storage account with encryption and private endpoint')")]
        string requirements,

        [Description("Template format: 'bicep' or 'terraform' (default: bicep)")]
        string format = "bicep",

        [Description("Include Microsoft best practices recommendations (default: true)")]
        bool includeBestPractices = true,

        [Description("Validate template with Bicep schema (default: true)")]
        bool validateSchema = true,

        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating infrastructure template with best practices for: {Requirements}", requirements);

            // 1. Use existing template generator for base template
            var infraFormat = format.ToLowerInvariant() == "terraform"
                ? InfrastructureFormat.Terraform
                : InfrastructureFormat.Bicep;

            var resourceTypes = ExtractResourceTypes(requirements);
            var primaryResourceType = resourceTypes.FirstOrDefault() ?? "general infrastructure";

            var templateRequest = BuildTemplateGenerationRequest(
                primaryResourceType,
                requirements,
                infraFormat,
                ComputePlatform.AKS,  // Use AKS as default compute platform
                "eastus",
                3,
                null);

            var templateResult = await _templateGenerator.GenerateTemplateAsync(templateRequest, cancellationToken);

            if (!templateResult.Success)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"Template generation failed: {templateResult.ErrorMessage}"
                });
            }

            var generatedTemplate = templateResult.Files?.FirstOrDefault().Value ?? "";

            // 2. Use Azure MCP to get best practices
            object? bestPracticesData = null;
            if (includeBestPractices)
            {
                try
                {
                    await _azureMcpClient.InitializeAsync(cancellationToken);

                    _logger.LogInformation("Fetching best practices for resource types via Azure MCP");

                    var toolName = format == "terraform" ? "azureterraformbestpractices" : "get_bestpractices";
                    var bestPractices = await _azureMcpClient.CallToolAsync(toolName,
                        new Dictionary<string, object?>
                        {
                            ["resourceTypes"] = string.Join(", ", resourceTypes)
                        }, cancellationToken);

                    bestPracticesData = new
                    {
                        available = bestPractices.Success,
                        source = format == "terraform" ? "Terraform" : "Azure",
                        recommendations = bestPractices.Success ? bestPractices.Result : "Best practices not available"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve best practices from Azure MCP");
                    bestPracticesData = new
                    {
                        available = false,
                        error = "Best practices service temporarily unavailable"
                    };
                }
            }

            // 3. Use Azure MCP Bicep schema for validation (Bicep only)
            object? schemaValidation = null;
            if (validateSchema && format == "bicep")
            {
                try
                {
                    _logger.LogInformation("Validating template with Bicep schema via Azure MCP");

                    var validation = await _azureMcpClient.CallToolAsync("bicepschema",
                        new Dictionary<string, object?>
                        {
                            ["command"] = "validate",
                            ["parameters"] = new { template = generatedTemplate }
                        }, cancellationToken);

                    schemaValidation = new
                    {
                        available = validation.Success,
                        valid = validation.Success,
                        result = validation.Success ? validation.Result : "Schema validation not available"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not validate schema with Azure MCP");
                    schemaValidation = new
                    {
                        available = false,
                        error = "Schema validation service temporarily unavailable"
                    };
                }
            }

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                format = format,
                requirements = requirements,
                template = new
                {
                    content = generatedTemplate,
                    files = templateResult.Files
                },
                bestPractices = bestPracticesData,
                schemaValidation = schemaValidation,
                nextSteps = new[]
                {
                    "Review the generated template and best practices recommendations above.",
                    schemaValidation != null ? "Check schema validation results for any template issues." : null,
                    "Say 'deploy this template to resource group <name>' to deploy the infrastructure.",
                    "Say 'generate documentation for this template' for deployment guides."
                }.Where(s => s != null)
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating infrastructure template with best practices");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    #region DoD Impact Level Compliance Functions

    [KernelFunction("validate_template_il_compliance")]
    [Description("Validate a Bicep/Terraform/ARM template against DoD Impact Level compliance policies (IL2, IL4, IL5, IL6). Returns compliance violations and warnings. Use when user asks to 'validate template for IL5' or 'check compliance for Impact Level 6'.")]
    public async Task<string> ValidateTemplateIlComplianceAsync(
        [Description("The template content to validate (Bicep, Terraform, or ARM JSON)")]
        string templateContent,
        [Description("Template type: 'Bicep', 'Terraform', 'ARM', 'Kubernetes', or 'Helm'")]
        string templateType,
        [Description("Target DoD Impact Level: 'IL2', 'IL4', 'IL5', or 'IL6'")]
        string impactLevel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔍 Validating template for {ImpactLevel} compliance", impactLevel);

            // Parse enum values
            if (!Enum.TryParse<TemplateType>(templateType, ignoreCase: true, out var parsedTemplateType))
            {
                return $"❌ Invalid template type: {templateType}. Must be one of: Bicep, Terraform, ARM, Kubernetes, Helm";
            }

            if (!Enum.TryParse<ImpactLevel>(impactLevel, ignoreCase: true, out var parsedImpactLevel))
            {
                return $"❌ Invalid Impact Level: {impactLevel}. Must be one of: IL2, IL4, IL5, IL6";
            }

            var request = new TemplateValidationRequest
            {
                TemplateContent = templateContent,
                Type = parsedTemplateType,
                TargetImpactLevel = parsedImpactLevel,
                RequiresApproval = parsedImpactLevel >= ImpactLevel.IL5
            };

            var result = await _policyEnforcementService.ValidateTemplateAsync(request, cancellationToken);

            var response = new StringBuilder();
            response.AppendLine($"## 🛡️ {parsedImpactLevel} Compliance Validation Results");
            response.AppendLine();
            response.AppendLine($"**Template Type:** {parsedTemplateType}");
            response.AppendLine($"**Target Impact Level:** {parsedImpactLevel}");
            response.AppendLine($"**Validated At:** {result.ValidatedAt:yyyy-MM-dd HH:mm:ss} UTC");
            response.AppendLine();

            if (result.IsCompliant)
            {
                response.AppendLine("✅ **Status:** COMPLIANT");
                response.AppendLine();
                response.AppendLine($"🎉 Template meets all {parsedImpactLevel} compliance requirements!");
            }
            else
            {
                response.AppendLine("❌ **Status:** NOT COMPLIANT");
                response.AppendLine();
                response.AppendLine($"**Total Violations:** {result.Violations.Count}");
                response.AppendLine($"- 🔴 Critical: {result.CriticalViolations}");
                response.AppendLine($"- 🟠 High: {result.HighViolations}");
                response.AppendLine($"- 🟡 Medium: {result.MediumViolations}");
                response.AppendLine($"- 🟢 Low: {result.LowViolations}");
                response.AppendLine();

                if (result.Violations.Any())
                {
                    response.AppendLine("### 📋 Policy Violations");
                    response.AppendLine();

                    foreach (var violation in result.Violations.OrderByDescending(v => v.Severity))
                    {
                        var severityEmoji = violation.Severity switch
                        {
                            PolicyViolationSeverity.Critical => "🔴",
                            PolicyViolationSeverity.High => "🟠",
                            PolicyViolationSeverity.Medium => "🟡",
                            _ => "🟢"
                        };

                        response.AppendLine($"{severityEmoji} **{violation.PolicyName}** ({violation.PolicyId})");
                        response.AppendLine($"   - **Description:** {violation.Description}");
                        response.AppendLine($"   - **Recommended Action:** {violation.RecommendedAction}");
                        response.AppendLine();
                    }
                }
            }

            if (result.Warnings?.Any() == true)
            {
                response.AppendLine("### ⚠️ Warnings");
                response.AppendLine();
                foreach (var warning in result.Warnings)
                {
                    response.AppendLine($"- {warning}");
                }
                response.AppendLine();
            }

            response.AppendLine("### 💡 Next Steps");
            if (!result.IsCompliant)
            {
                response.AppendLine("1. Review the policy violations above");
                response.AppendLine("2. Use 'get_remediation_guidance' for specific fix instructions");
                response.AppendLine($"3. Apply fixes to the template and re-validate");
                response.AppendLine($"4. For pre-hardened templates, use 'generate_il_compliant_template'");
            }
            else
            {
                response.AppendLine("1. Template is ready for deployment");
                if (parsedImpactLevel >= ImpactLevel.IL5)
                {
                    response.AppendLine("2. Submit for approval workflow (required for IL5/IL6)");
                }
                response.AppendLine($"3. Deploy to allowed regions only");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating template IL compliance");
            return $"❌ Error: {ex.Message}";
        }
    }

    [KernelFunction("generate_il_compliant_template")]
    [Description("Generate a pre-hardened Bicep/Terraform/ARM template with DoD Impact Level compliance controls baked in. Use when user asks 'generate an IL5 storage account' or 'create IL6-compliant AKS template'.")]
    public async Task<string> GenerateIlCompliantTemplateAsync(
        [Description("Azure resource type: 'StorageAccount', 'VirtualMachine', 'AksCluster', 'SqlDatabase', 'KeyVault', 'AppService', etc.")]
        string resourceType,
        [Description("Target DoD Impact Level: 'IL2', 'IL4', 'IL5', or 'IL6'")]
        string impactLevel,
        [Description("Template format: 'Bicep', 'Terraform', or 'ARM'")]
        string templateType = "Bicep",
        [Description("Resource name")]
        string resourceName = "myresource",
        [Description("Azure region (must be compliant with Impact Level restrictions)")]
        string region = "usgovvirginia",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🏗️ Generating {ImpactLevel}-compliant {ResourceType} template", impactLevel, resourceType);

            // Parse enum values
            if (!Enum.TryParse<AzureResourceType>(resourceType, ignoreCase: true, out var parsedResourceType))
            {
                return $"❌ Invalid resource type: {resourceType}. Must be one of: StorageAccount, VirtualMachine, AksCluster, SqlDatabase, KeyVault, AppService, ContainerRegistry, CosmosDb, FunctionApp, ApiManagement, ServiceBus, VirtualNetwork, NetworkSecurityGroup";
            }

            if (!Enum.TryParse<ImpactLevel>(impactLevel, ignoreCase: true, out var parsedImpactLevel))
            {
                return $"❌ Invalid Impact Level: {impactLevel}. Must be one of: IL2, IL4, IL5, IL6";
            }

            if (!Enum.TryParse<TemplateType>(templateType, ignoreCase: true, out var parsedTemplateType))
            {
                return $"❌ Invalid template type: {templateType}. Must be one of: Bicep, Terraform, ARM";
            }

            // Get Azure best practices from MCP server
            string? bestPracticesGuidance = null;
            try
            {
                await _azureMcpClient.InitializeAsync(cancellationToken);
                _logger.LogInformation("📚 Fetching Azure best practices for {ResourceType} via Azure MCP", resourceType);

                var toolName = parsedTemplateType == TemplateType.Terraform ? "azureterraformbestpractices" : "get_bestpractices";
                var bestPractices = await _azureMcpClient.CallToolAsync(toolName,
                    new Dictionary<string, object?>
                    {
                        ["resourceTypes"] = resourceType
                    }, cancellationToken);

                if (bestPractices.Success)
                {
                    bestPracticesGuidance = bestPractices.Result?.ToString();
                    _logger.LogInformation("✅ Retrieved Azure best practices guidance ({Length} chars)", bestPracticesGuidance?.Length ?? 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Could not retrieve best practices from Azure MCP - continuing without them");
            }

            var request = new IlTemplateRequest
            {
                ImpactLevel = parsedImpactLevel,
                TemplateType = parsedTemplateType,
                ResourceType = parsedResourceType,
                ResourceName = resourceName,
                Region = region
            };

            var template = await _policyEnforcementService.GenerateCompliantTemplateAsync(request, cancellationToken);

            var response = new StringBuilder();
            response.AppendLine($"## 🛡️ {parsedImpactLevel}-Compliant {parsedResourceType} Template");
            response.AppendLine();
            response.AppendLine($"**Template Format:** {template.TemplateType}");
            response.AppendLine($"**Resource Type:** {template.ResourceType}");
            response.AppendLine($"**Impact Level:** {template.ImpactLevel}");
            response.AppendLine($"**Generated At:** {template.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC");
            response.AppendLine($"**Applied Policies:** {template.AppliedPolicies.Count}");
            response.AppendLine();

            response.AppendLine("### 📜 Template Content");
            response.AppendLine();
            response.AppendLine("```" + template.TemplateType.ToString().ToLowerInvariant());
            response.AppendLine(template.TemplateContent);
            response.AppendLine("```");
            response.AppendLine();

            response.AppendLine("### 🔒 Applied Security Controls");
            response.AppendLine();
            foreach (var policyId in template.AppliedPolicies.Take(10))
            {
                response.AppendLine($"- {policyId}");
            }
            if (template.AppliedPolicies.Count > 10)
            {
                response.AppendLine($"- ... and {template.AppliedPolicies.Count - 10} more");
            }
            response.AppendLine();

            response.AppendLine("### 💡 Next Steps");
            response.AppendLine("1. Review the generated template above");
            response.AppendLine("2. Customize resource-specific properties as needed");
            response.AppendLine("3. Validate with 'validate_template_il_compliance' before deployment");
            response.AppendLine($"4. Deploy to allowed regions: {region}");
            if (parsedImpactLevel >= ImpactLevel.IL5)
            {
                response.AppendLine("5. Submit for approval workflow (required for IL5/IL6)");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating IL-compliant template");
            return $"❌ Error: {ex.Message}";
        }
    }

    [KernelFunction("get_il_policy_requirements")]
    [Description("Get detailed DoD Impact Level policy requirements including encryption, networking, identity, allowed regions, and mandatory tags. Use when user asks 'what are IL5 requirements' or 'show me IL6 policy details'.")]
    public async Task<string> GetIlPolicyRequirementsAsync(
        [Description("DoD Impact Level: 'IL2', 'IL4', 'IL5', or 'IL6'")]
        string impactLevel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("📋 Retrieving {ImpactLevel} policy requirements", impactLevel);

            if (!Enum.TryParse<ImpactLevel>(impactLevel, ignoreCase: true, out var parsedImpactLevel))
            {
                return $"❌ Invalid Impact Level: {impactLevel}. Must be one of: IL2, IL4, IL5, IL6";
            }

            var policy = await _policyEnforcementService.GetPolicyForImpactLevelAsync(parsedImpactLevel, cancellationToken);

            var response = new StringBuilder();
            response.AppendLine($"## 🛡️ {policy.Name}");
            response.AppendLine();
            response.AppendLine($"**Description:** {policy.Description}");
            response.AppendLine();

            response.AppendLine("### 🌍 Allowed Regions");
            response.AppendLine();
            foreach (var region in policy.AllowedRegions)
            {
                response.AppendLine($"- {region}");
            }
            response.AppendLine();

            response.AppendLine("### 🔐 Encryption Requirements");
            response.AppendLine();
            response.AppendLine($"- **Encryption at Rest:** {(policy.EncryptionRequirements.RequiresEncryptionAtRest ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **Encryption in Transit:** {(policy.EncryptionRequirements.RequiresEncryptionInTransit ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **Customer-Managed Keys:** {(policy.EncryptionRequirements.RequiresCustomerManagedKeys ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **FIPS 140-2 Compliance:** {(policy.EncryptionRequirements.RequiresFipsCompliantEncryption ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **HSM-Backed Keys:** {(policy.EncryptionRequirements.RequiresHsmBackedKeys ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **Minimum TLS Version:** {policy.MinimumTlsVersion}");
            response.AppendLine($"- **Key Vault SKU:** {policy.EncryptionRequirements.AllowedKeyVaultSku}");
            response.AppendLine($"- **Minimum Key Size:** {policy.EncryptionRequirements.MinimumKeySize} bits");
            response.AppendLine();

            response.AppendLine("### 🌐 Network Requirements");
            response.AppendLine();
            response.AppendLine($"- **Private Endpoints:** {(policy.NetworkRequirements.RequiresPrivateEndpoints ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **Public Network Access:** {(policy.AllowPublicNetworkAccess ? "✅ Allowed" : "❌ Denied")}");
            response.AppendLine($"- **Network Isolation:** {(policy.NetworkRequirements.RequiresNetworkIsolation ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **DDoS Protection:** {(policy.NetworkRequirements.RequiresDDoSProtection ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **Dedicated Subnet:** {(policy.NetworkRequirements.RequiresDedicatedSubnet ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **NSG Default Deny:** {(policy.NetworkRequirements.RequiresNsgRules ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **Internet Egress:** {(policy.NetworkRequirements.AllowInternetEgress ? "✅ Allowed" : "❌ Denied")}");
            if (policy.NetworkRequirements.AllowedServiceEndpoints?.Any() == true)
            {
                response.AppendLine($"- **Allowed Service Endpoints:** {string.Join(", ", policy.NetworkRequirements.AllowedServiceEndpoints)}");
            }
            response.AppendLine();

            response.AppendLine("### 👤 Identity Requirements");
            response.AppendLine();
            response.AppendLine($"- **Managed Identity:** {(policy.IdentityRequirements.RequiresManagedIdentity ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **Multi-Factor Auth:** {(policy.IdentityRequirements.RequiresMfa ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **Privileged Identity Management:** {(policy.IdentityRequirements.RequiresPim ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **CAC/PIV Authentication:** {(policy.IdentityRequirements.RequiresCac ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **Conditional Access:** {(policy.IdentityRequirements.RequiresConditionalAccess ? "✅ Required" : "⚪ Optional")}");
            response.AppendLine($"- **Service Principals:** {(policy.IdentityRequirements.AllowsServicePrincipals ? "✅ Allowed" : "❌ Not Allowed")}");
            response.AppendLine();

            response.AppendLine("### 🏷️ Mandatory Tags");
            response.AppendLine();
            foreach (var tag in policy.MandatoryTags)
            {
                response.AppendLine($"- **{tag.Key}:** {tag.Value}");
            }
            response.AppendLine();

            response.AppendLine("### 💡 Compliance Frameworks");
            response.AppendLine();
            var frameworks = parsedImpactLevel switch
            {
                ImpactLevel.IL2 => "FedRAMP Low, NIST 800-53 Low Baseline",
                ImpactLevel.IL4 => "FedRAMP Moderate, NIST 800-53 Moderate Baseline",
                ImpactLevel.IL5 => "FedRAMP High, NIST 800-53 High Baseline, STIG Compliance",
                ImpactLevel.IL6 => "FedRAMP High+, NIST 800-53 High Baseline, STIG Compliance, TOP SECRET Controls",
                _ => "Unknown"
            };
            response.AppendLine($"- {frameworks}");

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving IL policy requirements");
            return $"❌ Error: {ex.Message}";
        }
    }

    [KernelFunction("apply_il_policies_to_template")]
    [Description("Apply DoD Impact Level hardening policies to an existing template by adding advisory comments and recommendations. Use when user says 'harden this template for IL5' or 'add IL6 policies to my Bicep file'.")]
    public async Task<string> ApplyIlPoliciesToTemplateAsync(
        [Description("The existing template content to enhance")]
        string templateContent,
        [Description("Template type: 'Bicep', 'Terraform', or 'ARM'")]
        string templateType,
        [Description("Target DoD Impact Level: 'IL2', 'IL4', 'IL5', or 'IL6'")]
        string impactLevel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔧 Applying {ImpactLevel} policies to {TemplateType} template", impactLevel, templateType);

            if (!Enum.TryParse<TemplateType>(templateType, ignoreCase: true, out var parsedTemplateType))
            {
                return $"❌ Invalid template type: {templateType}. Must be one of: Bicep, Terraform, ARM";
            }

            if (!Enum.TryParse<ImpactLevel>(impactLevel, ignoreCase: true, out var parsedImpactLevel))
            {
                return $"❌ Invalid Impact Level: {impactLevel}. Must be one of: IL2, IL4, IL5, IL6";
            }

            var hardenedTemplate = await _policyEnforcementService.ApplyPoliciesToTemplateAsync(
                templateContent,
                parsedTemplateType,
                parsedImpactLevel,
                cancellationToken);

            var response = new StringBuilder();
            response.AppendLine($"## 🔧 Template Enhanced with {parsedImpactLevel} Policies");
            response.AppendLine();
            response.AppendLine($"**Template Type:** {parsedTemplateType}");
            response.AppendLine($"**Impact Level:** {parsedImpactLevel}");
            response.AppendLine();

            response.AppendLine("### 📜 Hardened Template");
            response.AppendLine();
            response.AppendLine("```" + parsedTemplateType.ToString().ToLowerInvariant());
            response.AppendLine(hardenedTemplate);
            response.AppendLine("```");
            response.AppendLine();

            response.AppendLine("### 💡 Next Steps");
            response.AppendLine("1. Review the advisory comments added to the template");
            response.AppendLine("2. Implement the recommended security controls");
            response.AppendLine("3. Validate with 'validate_template_il_compliance'");
            response.AppendLine("4. Deploy to approved regions only");

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying IL policies to template");
            return $"❌ Error: {ex.Message}";
        }
    }

    [KernelFunction("get_remediation_guidance")]
    [Description("Get IaC-specific remediation code snippets for fixing compliance violations. Returns Bicep/Terraform/ARM code to fix specific policy violations. Use when user asks 'how do I fix this violation' or 'show me the code to enable CMK'.")]
    public async Task<string> GetRemediationGuidanceAsync(
        [Description("Policy violation ID (e.g., 'ENC-001', 'NET-001')")]
        string policyId,
        [Description("Policy name or description")]
        string policyName,
        [Description("Template type for code examples: 'Bicep', 'Terraform', or 'ARM'")]
        string templateType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("🔧 Generating remediation guidance for {PolicyId} in {TemplateType}", policyId, templateType);

            if (!Enum.TryParse<TemplateType>(templateType, ignoreCase: true, out var parsedTemplateType))
            {
                return $"❌ Invalid template type: {templateType}. Must be one of: Bicep, Terraform, ARM";
            }

            var violation = new PolicyViolation
            {
                PolicyId = policyId,
                PolicyName = policyName,
                Severity = PolicyViolationSeverity.High,
                Description = $"Policy violation: {policyName}",
                RecommendedAction = "Apply the remediation code below"
            };

            var guidance = await _policyEnforcementService.GetRemediationGuidanceAsync(violation, parsedTemplateType, cancellationToken);

            var response = new StringBuilder();
            response.AppendLine($"## 🔧 Remediation Guidance for {policyId}");
            response.AppendLine();
            response.AppendLine($"**Policy:** {policyName}");
            response.AppendLine($"**Template Type:** {parsedTemplateType}");
            response.AppendLine();

            response.AppendLine("### 📝 Remediation Code");
            response.AppendLine();
            response.AppendLine("```" + parsedTemplateType.ToString().ToLowerInvariant());
            response.AppendLine(guidance);
            response.AppendLine("```");
            response.AppendLine();

            response.AppendLine("### 💡 Implementation Steps");
            response.AppendLine("1. Copy the code snippet above");
            response.AppendLine("2. Integrate it into your template at the appropriate location");
            response.AppendLine("3. Update resource references and parameter names as needed");
            response.AppendLine("4. Re-validate with 'validate_template_il_compliance'");

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating remediation guidance");
            return $"❌ Error: {ex.Message}";
        }
    }

    private static string GetTemplateExtension(TemplateType templateType)
    {
        return templateType switch
        {
            TemplateType.Bicep => "bicep",
            TemplateType.Terraform => "tf",
            TemplateType.ARM => "json",
            TemplateType.Kubernetes => "yaml",
            TemplateType.Helm => "yaml",
            _ => "txt"
        };
    }

    #endregion

    // Helper to map resource type to compute platform

    private List<string> ExtractResourceTypes(string requirements)
    {
        var types = new List<string>();
        var lowerReqs = requirements.ToLowerInvariant();

        if (lowerReqs.Contains("storage") || lowerReqs.Contains("blob")) types.Add("Microsoft.Storage/storageAccounts");
        if (lowerReqs.Contains("aks") || lowerReqs.Contains("kubernetes")) types.Add("Microsoft.ContainerService/managedClusters");
        if (lowerReqs.Contains("app service") || lowerReqs.Contains("webapp")) types.Add("Microsoft.Web/sites");
        if (lowerReqs.Contains("sql") || lowerReqs.Contains("database")) types.Add("Microsoft.Sql/servers");
        if (lowerReqs.Contains("keyvault") || lowerReqs.Contains("key vault")) types.Add("Microsoft.KeyVault/vaults");
        if (lowerReqs.Contains("vm") || lowerReqs.Contains("virtual machine")) types.Add("Microsoft.Compute/virtualMachines");
        if (lowerReqs.Contains("vnet") || lowerReqs.Contains("network")) types.Add("Microsoft.Network/virtualNetworks");

        return types.Any() ? types : new List<string> { "general" };
    }

    /// <summary>
    /// Builds a complete TemplateGenerationRequest with resource-specific defaults
    /// </summary>
    private TemplateGenerationRequest BuildTemplateGenerationRequest(
        string resourceType,
        string description,
        InfrastructureFormat infraFormat,
        ComputePlatform computePlatform,
        string location,
        int nodeCount,
        string? subscriptionId)
    {
        var resourceTypeLower = resourceType?.ToLowerInvariant() ?? "";
        var isAKS = resourceTypeLower == "aks" || resourceTypeLower == "kubernetes" || resourceTypeLower == "k8s";
        var isAppService = resourceTypeLower == "app-service" || resourceTypeLower == "appservice" || resourceTypeLower == "webapp";
        var isContainerApps = resourceTypeLower == "container-apps" || resourceTypeLower == "containerapps";
        var isStorage = resourceTypeLower == "storage" || resourceTypeLower == "storage-account" || resourceTypeLower == "storageaccount";
        var isDatabase = resourceTypeLower.Contains("sql") || resourceTypeLower.Contains("database") || resourceTypeLower.Contains("postgres") || resourceTypeLower.Contains("mysql");
        var isNetworking = resourceTypeLower == "vnet" || resourceTypeLower == "network" || resourceTypeLower.Contains("virtual-network");

        var request = new TemplateGenerationRequest
        {
            ServiceName = $"{resourceType}-deployment",
            Description = description,
            TemplateType = "infrastructure-only",
            Infrastructure = new InfrastructureSpec
            {
                Format = infraFormat,
                Provider = CloudProvider.Azure,
                Region = location,
                ComputePlatform = computePlatform,
                Environment = "production",
                SubscriptionId = subscriptionId
            },
            Security = new SecuritySpec(),
            Observability = new ObservabilitySpec()
        };

        // AKS-specific configuration
        if (isAKS)
        {
            request.Infrastructure.ClusterName = $"{resourceType}-cluster";
            request.Infrastructure.NodeCount = nodeCount;
            request.Infrastructure.NodeSize = "Standard_D4s_v3";
            request.Infrastructure.KubernetesVersion = "1.30";
            request.Infrastructure.NetworkPlugin = "azure";
            request.Infrastructure.EnableAutoScaling = true;
            request.Infrastructure.MinNodeCount = 1;
            request.Infrastructure.MaxNodeCount = 10;

            // Zero Trust security defaults for AKS
            request.Security.EnableWorkloadIdentity = true;
            request.Security.EnableAzurePolicy = true;
            request.Security.EnableSecretStore = true;
            request.Security.EnableDefender = true;
            request.Security.EnablePrivateCluster = true;
            request.Security.NetworkPolicy = "azure";
            request.Security.EnableAzureRBAC = true;
            request.Security.EnableAADIntegration = true;

            // Monitoring defaults for AKS
            request.Observability.EnableContainerInsights = true;
            request.Observability.EnablePrometheus = true;
        }
        // App Service-specific configuration
        else if (isAppService)
        {
            request.Infrastructure.AppServicePlanSku = "P1v3"; // Production-grade
            request.Infrastructure.AlwaysOn = true;
            request.Infrastructure.HttpsOnly = true;
            request.Infrastructure.EnableVnetIntegration = true;

            // Application defaults
            request.Application = new ApplicationSpec
            {
                Language = ProgrammingLanguage.DotNet, // Default, should be overridden
                Framework = "aspnetcore"
            };

            // Security defaults for App Service
            request.Security.EnableManagedIdentity = true;
            request.Security.EnablePrivateEndpoint = true;
            request.Security.EnableKeyVault = true;
            request.Security.HttpsOnly = true;

            // Monitoring defaults for App Service
            request.Observability.ApplicationInsights = true;
            request.Observability.EnableDiagnostics = true;
        }
        // Container Apps-specific configuration
        else if (isContainerApps)
        {
            request.Infrastructure.ContainerImage = "mcr.microsoft.com/azuredocs/containerapps-helloworld:latest";
            request.Infrastructure.ContainerPort = 80;
            request.Infrastructure.MinReplicas = 1;
            request.Infrastructure.MaxReplicas = 10;
            request.Infrastructure.CpuCores = "0.5";
            request.Infrastructure.MemorySize = "1Gi";
            request.Infrastructure.EnableDapr = true;
            request.Infrastructure.ExternalIngress = true;

            // Security defaults for Container Apps
            request.Security.EnableManagedIdentity = true;
            request.Security.AllowInsecure = false;

            // Monitoring defaults for Container Apps
            request.Observability.EnableContainerInsights = true;
            request.Observability.ApplicationInsights = true;
        }
        // Storage Account-specific configuration
        else if (isStorage)
        {
            // Storage defaults - minimal configuration for infrastructure-only
            request.Security.EnablePrivateEndpoint = true;
            request.Observability.EnableDiagnostics = true;
        }
        // Database-specific configuration
        else if (isDatabase)
        {
            // Database defaults
            request.Security.EnablePrivateEndpoint = true;
            request.Security.EnableDefender = true;
            request.Observability.EnableDiagnostics = true;
        }
        // Networking-specific configuration
        else if (isNetworking)
        {
            // VNet defaults - minimal configuration
            request.Observability.EnableDiagnostics = true;
        }

        return request;
    }

    /// <summary>
    /// Maps resource type string to ComputePlatform enum
    /// </summary>
    private ComputePlatform MapResourceTypeToComputePlatform(string resourceType)
    {
        var normalized = resourceType?.ToLowerInvariant().Replace("-", "").Replace("_", "");

        return normalized switch
        {
            // Kubernetes
            "aks" => ComputePlatform.AKS,
            "kubernetes" => ComputePlatform.AKS,
            "k8s" => ComputePlatform.AKS,
            "eks" => ComputePlatform.EKS,
            "gke" => ComputePlatform.GKE,

            // App Services
            "appservice" => ComputePlatform.AppService,
            "webapp" => ComputePlatform.AppService,
            "webapps" => ComputePlatform.AppService,

            // Containers
            "containerapps" => ComputePlatform.ContainerApps,
            "containerapp" => ComputePlatform.ContainerApps,
            "functions" => ComputePlatform.Functions,
            "lambda" => ComputePlatform.Lambda,
            "ecs" => ComputePlatform.ECS,
            "fargate" => ComputePlatform.Fargate,
            "cloudrun" => ComputePlatform.CloudRun,

            // Virtual Machines
            "vm" => ComputePlatform.VirtualMachines,
            "virtualmachine" => ComputePlatform.VirtualMachines,
            "virtualmachines" => ComputePlatform.VirtualMachines,

            // Storage
            "storage" => ComputePlatform.Storage,
            "storageaccount" => ComputePlatform.Storage,
            "blob" => ComputePlatform.Storage,
            "blobstorage" => ComputePlatform.Storage,

            // Database
            "sql" => ComputePlatform.Database,
            "sqldatabase" => ComputePlatform.Database,
            "database" => ComputePlatform.Database,
            "postgres" => ComputePlatform.Database,
            "postgresql" => ComputePlatform.Database,
            "mysql" => ComputePlatform.Database,
            "cosmosdb" => ComputePlatform.Database,
            "cosmos" => ComputePlatform.Database,

            // Networking
            "vnet" => ComputePlatform.Networking,
            "virtualnetwork" => ComputePlatform.Networking,
            "network" => ComputePlatform.Networking,
            "networking" => ComputePlatform.Networking,
            "subnet" => ComputePlatform.Networking,
            "nsg" => ComputePlatform.Networking,

            // Security
            "keyvault" => ComputePlatform.Security,
            "vault" => ComputePlatform.Security,
            "managedidentity" => ComputePlatform.Security,
            "identity" => ComputePlatform.Security,

            // Default - return Networking for infrastructure-only resources instead of AKS
            _ => ComputePlatform.Networking
        };
    }

    /// <summary>
    /// Formats a single file for response output with proper code block syntax
    /// </summary>
    private static string FormatFileResponse(string fileName, string content)
    {
        var fileExt = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        var codeBlockType = fileExt switch
        {
            "bicep" => "bicep",
            "tf" => "hcl",
            "json" => "json",
            "yaml" or "yml" => "yaml",
            _ => fileExt
        };

        var response = new StringBuilder();
        response.AppendLine($"### 📁 {fileName}");
        response.AppendLine();
        response.AppendLine($"```{codeBlockType}");
        response.AppendLine(content);
        response.AppendLine("```");
        return response.ToString();
    }

    /// <summary>
    /// Formats all files from SharedMemory for response output
    /// </summary>
    private string FormatAllFilesFromMemory(List<string> fileNames)
    {
        var response = new StringBuilder();
        response.AppendLine($"## 📦 All {fileNames.Count} Generated Files");
        response.AppendLine();

        foreach (var fileName in fileNames.OrderBy(f => f))
        {
            var content = _sharedMemory.GetGeneratedFile(_currentConversationId!, fileName);
            if (content != null)
            {
                response.Append(FormatFileResponse(fileName, content));
                response.AppendLine();
            }
        }

        return response.ToString();
    }

    /// <summary>
    /// Formats all files from database template for response output
    /// </summary>
    private static string FormatAllFilesFromDatabase(EnvironmentTemplate template)
    {
        var response = new StringBuilder();
        response.AppendLine($"## 📦 Template: {template.Name}");
        response.AppendLine($"**Generated:** {template.CreatedAt:yyyy-MM-dd HH:mm:ss} UTC");
        response.AppendLine($"**Files:** {template.Files?.Count() ?? 0}");
        response.AppendLine();

        if (template.Files != null)
        {
            foreach (var file in template.Files.OrderBy(f => f.Order))
            {
                var fileExt = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
                var codeBlockType = fileExt switch
                {
                    "bicep" => "bicep",
                    "tf" => "hcl",
                    "json" => "json",
                    "yaml" or "yml" => "yaml",
                    _ => fileExt
                };

                response.AppendLine($"### 📁 {file.FileName}");
                response.AppendLine();
                response.AppendLine($"```{codeBlockType}");
                response.AppendLine(file.Content);
                response.AppendLine("```");
                response.AppendLine();
            }
        }

        return response.ToString();
    }
}