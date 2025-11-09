using System.ComponentModel;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Platform.Engineering.Copilot.Core.Interfaces.Infrastructure;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Plugins;
using Platform.Engineering.Copilot.Core.Services;
using Platform.Engineering.Copilot.Core.Services.Agents;
using Platform.Engineering.Copilot.Core.Services.Azure;
using Platform.Engineering.Copilot.Infrastructure.Core.Services;

namespace Platform.Engineering.Copilot.Infrastructure.Core;

/// <summary>
/// Semantic Kernel plugin for Azure infrastructure provisioning
/// Enhanced with Azure MCP Server integration for best practices, schema validation, and Azure Developer CLI
/// Uses natural language queries to provision infrastructure via AI-powered service
/// Example: "Create a storage account named mydata in eastus with Standard_LRS"
/// </summary>
public class InfrastructurePlugin : BaseSupervisorPlugin
{
    private readonly IInfrastructureProvisioningService _infrastructureService;
    private readonly IDynamicTemplateGenerator _templateGenerator;
    private readonly INetworkTopologyDesignService _networkDesignService;
    private readonly IPredictiveScalingEngine _scalingEngine;
    private readonly IComplianceAwareTemplateEnhancer _complianceEnhancer;
    private readonly SharedMemory _sharedMemory;
    private readonly AzureMcpClient _azureMcpClient;
    private string? _currentConversationId; // Set by agent before function calls

    public InfrastructurePlugin(
        ILogger<InfrastructurePlugin> logger,
        Kernel kernel,
        IInfrastructureProvisioningService infrastructureService,
        IDynamicTemplateGenerator templateGenerator,
        INetworkTopologyDesignService networkDesignService,
        IPredictiveScalingEngine scalingEngine,
        IComplianceAwareTemplateEnhancer complianceEnhancer,
        SharedMemory sharedMemory,
        AzureMcpClient azureMcpClient)
        : base(logger, kernel)
    {
        _infrastructureService = infrastructureService;
        _templateGenerator = templateGenerator;
        _networkDesignService = networkDesignService;
        _scalingEngine = scalingEngine;
        _complianceEnhancer = complianceEnhancer;
        _sharedMemory = sharedMemory;
        _azureMcpClient = azureMcpClient ?? throw new ArgumentNullException(nameof(azureMcpClient));
    }

    /// <summary>
    /// Set the current conversation ID for context
    /// </summary>
    public void SetConversationId(string conversationId)
    {
        _currentConversationId = conversationId;
        _logger.LogInformation("🆔 InfrastructurePlugin: ConversationId set to: {ConversationId}", conversationId);
    }

    [KernelFunction("provision_infrastructure")]
    [Description("Actually provision Azure infrastructure immediately. ONLY use when user explicitly says 'NOW', 'IMMEDIATELY', 'DEPLOY THIS', 'CREATE THE RESOURCE NOW'. For most requests, use generate_infrastructure_template instead.")]
    public async Task<string> ProvisionInfrastructureAsync(
        [Description("Type of resource to provision: 'storage-account', 'keyvault', 'vnet', 'nsg', 'managed-identity', 'log-analytics', 'app-insights'")]
        string resourceType,
        [Description("Name of the resource to create")]
        string resourceName,
        [Description("Name of the resource group (will be created if it doesn't exist)")]
        string resourceGroupName,
        [Description("Azure region: 'eastus', 'westus2', 'usgovvirginia', 'centralus'")]
        string location = "eastus",
        [Description("SKU or tier for the resource. Examples: 'Standard_LRS' for storage, 'standard' for Key Vault")]
        string? sku = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("⚠️  ACTUAL PROVISIONING requested for {ResourceType}: {ResourceName}", 
                resourceType, resourceName);

            // Build a structured query for the service
            var query = $"Create {resourceType} named {resourceName} in {location}";
            if (!string.IsNullOrEmpty(sku))
            {
                query += $" with {sku}";
            }

            var result = await _infrastructureService.ProvisionInfrastructureAsync(query, cancellationToken);

            if (result.Success)
            {
                return $"✅ **Resource Provisioned Successfully**\n\n" +
                       $"{result.Message}\n" +
                       $"📍 Resource ID: {result.ResourceId}\n" +
                       $"📦 Resource Type: {result.ResourceType}\n" +
                       $"🌍 Location: {location}\n" +
                       $"📊 Status: {result.Status}\n\n" +
                       $"💡 You can view this resource in the Azure Portal.";
            }
            else
            {
                return $"❌ **Provisioning Failed**\n\n" +
                       $"Resource: {resourceName}\n" +
                       $"Type: {resourceType}\n" +
                       $"Error: {result.ErrorDetails}\n\n" +
                       $"Suggestion: Check parameters and try again, or use generate_infrastructure_template to see the IaC code first.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error provisioning infrastructure: {ResourceType}/{ResourceName}", 
                resourceType, resourceName);
            return $"❌ Error: {ex.Message}";
        }
    }

    [KernelFunction("list_resource_groups")]
    [Description("List all resource groups in the Azure subscription")]
    public async Task<string> ListResourceGroupsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Listing resource groups");

            var resourceGroups = await _infrastructureService.ListResourceGroupsAsync(cancellationToken);

            if (resourceGroups.Any())
            {
                return $"📦 Found {resourceGroups.Count} resource groups:\n" +
                       string.Join("\n", resourceGroups.Select(rg => $"  • {rg}"));
            }
            else
            {
                return "📦 No resource groups found";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing resource groups");
            return $"❌ Error: {ex.Message}";
        }
    }

    [KernelFunction("delete_resource_group")]
    [Description("Delete a resource group and all its resources")]
    public async Task<string> DeleteResourceGroupAsync(
        [Description("Name of the resource group to delete")] 
        string resourceGroupName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deleting resource group: {ResourceGroupName}", resourceGroupName);

            var success = await _infrastructureService.DeleteResourceGroupAsync(resourceGroupName, cancellationToken);

            if (success)
            {
                return $"✅ Successfully deleted resource group: {resourceGroupName}";
            }
            else
            {
                return $"❌ Failed to delete resource group: {resourceGroupName}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting resource group: {ResourceGroupName}", resourceGroupName);
            return $"❌ Error: {ex.Message}";
        }
    }

    [KernelFunction("get_generated_file")]
    [Description("Retrieve and display a PREVIOUSLY GENERATED template file. IMPORTANT: Use this when user asks to 'show', 'display', or 'view' a specific file that was ALREADY generated. DO NOT call generate_infrastructure_template again. Files must already exist from a prior generation.")]
    public Task<string> GetGeneratedFileAsync(
        [Description("Filename to retrieve. Can be partial (e.g., 'main.bicep') or full path (e.g., 'infra/modules/storage/main.bicep'). System will find the matching file.")]
        string fileName)
    {
        try
        {
            if (string.IsNullOrEmpty(_currentConversationId))
            {
                _logger.LogWarning("❌ No conversation context available for file retrieval");
                return Task.FromResult("❌ No conversation context available");
            }

            _logger.LogInformation("🔍 Attempting to retrieve file: {FileName} for conversation: {ConversationId}", 
                fileName, _currentConversationId);

            var availableFiles = _sharedMemory.GetGeneratedFileNames(_currentConversationId);
            _logger.LogInformation("📦 Available files in memory: {Count}", availableFiles.Count);
            
            if (!availableFiles.Any())
            {
                _logger.LogWarning("❌ No generated files found in SharedMemory for conversation {ConversationId}", 
                    _currentConversationId);
                return Task.FromResult("❌ No generated files found. Please generate a template first.");
            }

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

            if (matchingFile == null)
            {
                _logger.LogWarning("❌ File '{FileName}' not found in available files", fileName);
                return Task.FromResult($"❌ File '{fileName}' not found. Available files:\n" +
                    string.Join("\n", availableFiles.Select(f => $"- {f}")));
            }

            _logger.LogInformation("✅ Found matching file: {MatchingFile}", matchingFile);

            var content = _sharedMemory.GetGeneratedFile(_currentConversationId, matchingFile);
            if (content == null)
            {
                _logger.LogWarning("❌ File content is null for: {MatchingFile}", matchingFile);
                return Task.FromResult($"❌ Unable to retrieve content for file: {matchingFile}");
            }

            var response = new StringBuilder();
            response.AppendLine($"### 📁 {matchingFile}");
            response.AppendLine();
            response.AppendLine("```bicep");
            response.AppendLine(content);
            response.AppendLine("```");

            return Task.FromResult(response.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file: {FileName}", fileName);
            return Task.FromResult($"❌ Error retrieving file: {ex.Message}");
        }
    }

    [KernelFunction("get_all_generated_files")]
    [Description("Retrieve and display ALL PREVIOUSLY GENERATED template files at once. IMPORTANT: Use this when user asks to 'show all files', 'display everything', or 'show all generated templates'. DO NOT call generate_infrastructure_template again. Files must already exist from a prior generation. Warning: Response may be very long.")]
    public Task<string> GetAllGeneratedFilesAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_currentConversationId))
            {
                return Task.FromResult("❌ No conversation context available");
            }

            var fileNames = _sharedMemory.GetGeneratedFileNames(_currentConversationId);
            if (!fileNames.Any())
            {
                return Task.FromResult("❌ No generated files found. Please generate a template first.");
            }

            var response = new StringBuilder();
            response.AppendLine($"## 📦 All {fileNames.Count} Generated Files");
            response.AppendLine();

            foreach (var fileName in fileNames.OrderBy(f => f))
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
            _logger.LogError(ex, "Error retrieving all files");
            return Task.FromResult($"❌ Error retrieving files: {ex.Message}");
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

            // Map resource type to compute platform
            var computePlatform = MapResourceTypeToComputePlatform(resourceType);

            // Create template generation request with resource-specific defaults
            var request = BuildTemplateGenerationRequest(
                resourceType, 
                description, 
                infraFormat, 
                computePlatform, 
                location, 
                nodeCount, 
                subscriptionId);

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

            // Format the response with summary and main file
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

            // List all files
            foreach (var file in result.Files.OrderBy(f => f.Key))
            {
                var lineCount = file.Value.Split('\n').Length;
                response.AppendLine($"- `{file.Key}` ({lineCount} lines)");
            }

            // Show main file content only
            var mainFile = result.Files.FirstOrDefault(f => f.Key.Contains("main.bicep") || f.Key.Contains("main.tf"));
            if (mainFile.Key != null)
            {
                response.AppendLine();
                response.AppendLine($"### 📁 {mainFile.Key}");
                response.AppendLine();
                response.AppendLine("```" + (infraFormat == InfrastructureFormat.Bicep ? "bicep" : "hcl"));
                response.AppendLine(mainFile.Value);
                response.AppendLine("```");
                response.AppendLine();
            }

            response.AppendLine("💡 **To view other module files**, ask:");
            response.AppendLine("- \"Show me the cluster.bicep file\"");
            response.AppendLine("- \"Show me the network.bicep file\"");
            response.AppendLine("- Or \"Show me all the files\"");
            response.AppendLine();

            response.AppendLine("💡 **Next Steps:**");
            if (infraFormat == InfrastructureFormat.Bicep)
            {
                response.AppendLine("1. Save the template as `main.bicep`");
                response.AppendLine("2. Review and customize parameters");
                response.AppendLine("3. Deploy: `az deployment group create --resource-group <rg-name> --template-file main.bicep`");
            }
            else
            {
                response.AppendLine("1. Save the template as `main.tf`");
                response.AppendLine("2. Review and customize terraform parameters");
                response.AppendLine("3. Run `terraform init`");
                response.AppendLine("4. Run `terraform plan` to review changes");
                response.AppendLine("5. Run `terraform apply` to deploy");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating infrastructure template: {ResourceType}", resourceType);
            return $"❌ Error generating template: {ex.Message}";
        }
    }

    [KernelFunction("generate_compliant_infrastructure_template")]
    [Description("Generate compliance-enhanced Bicep or Terraform templates with FedRAMP High, DoD IL5, NIST 800-53, SOC2, or GDPR controls automatically injected. IMPORTANT: Only call this AFTER gathering requirements through conversation. Ask about environment type, specific compliance needs, monitoring preferences, etc. Use when users explicitly mention compliance frameworks like 'FedRAMP', 'DoD IL5', 'NIST 800-53', 'SOC2', 'GDPR', 'compliant', 'compliance', 'secure', 'hardened'.")]
    public async Task<string> GenerateCompliantInfrastructureTemplate(
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

            // Create template generation request with resource-specific defaults
            var request = BuildTemplateGenerationRequest(
                effectiveResourceType, 
                description, 
                infraFormat, 
                computePlatform, 
                location, 
                nodeCount, 
                subscriptionId);

            // Use compliance enhancer to inject controls and validate
            var result = await _complianceEnhancer.EnhanceWithComplianceAsync(
                request, 
                complianceFramework, 
                cancellationToken);

            if (!result.Success)
            {
                return $"❌ Error generating compliant template: {result.ErrorMessage}";
            }

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

            // List files with sizes instead of dumping all content
            foreach (var file in result.Files)
            {
                var lines = file.Value.Split('\n').Length;
                var size = file.Value.Length;
                var sizeKb = size / 1024.0;
                response.AppendLine($"- `{file.Key}` ({lines} lines, {sizeKb:F1} KB)");
            }
            response.AppendLine();
            response.AppendLine("💡 **To view the code:** Ask me to \"Show the [filename]\" or \"Display all generated files\"");
            response.AppendLine();

            response.AppendLine("💡 **Next Steps:**");
            if (infraFormat == InfrastructureFormat.Bicep)
            {
                response.AppendLine("1. Ask me to \"Show the main.bicep file\" to view the complete template");
                response.AppendLine("2. Review the compliance-enhanced template");
                response.AppendLine("3. Verify all required NIST controls are implemented");
                response.AppendLine("4. Save the main template as `main.bicep`");
                response.AppendLine("5. Save the other module templates as `**.bicep` and customize parameters if needed");
                response.AppendLine("6. Deploy: `az deployment group create --resource-group <rg-name> --template-file main.bicep`");
                response.AppendLine("7. After deployment, validate that all compliance controls are properly configured and active");
            }
            else
            {
                response.AppendLine("1. Ask me to \"Show the main.tf file\" to view the complete template");
                response.AppendLine("2. Review the compliance-enhanced template");
                response.AppendLine("3. Verify all required NIST controls are implemented");
                response.AppendLine("4. Save the template as `main.tf`");
                response.AppendLine("5. Run `terraform init` and `terraform plan`");
                response.AppendLine("6. Deploy with `terraform apply`");
                response.AppendLine("7. After deployment, validate that all compliance controls are properly configured and active");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating compliant infrastructure template: {Description}", description);
            return $"❌ Error generating compliant template: {ex.Message}";
        }
    }

    [KernelFunction("design_network_topology")]
    [Description("Design a multi-tier Azure VNet network topology with automatic subnet calculations and intelligent tier naming (1-tier: Application, 2-tier: Web+Data, 3-tier: Web+App+Data, 4+: DMZ+App tiers+Data). Use this when users ask to design/create network architectures, VNets, subnets, or multi-tier topologies.")]
    public string DesignNetworkTopology(
        [Description("Address space for the VNet in CIDR notation. Examples: '10.0.0.0/16', '192.168.0.0/16', '172.16.0.0/12'. Default: '10.0.0.0/16'")]
        string addressSpace = "10.0.0.0/16",
        [Description("Number of application tiers (subnets). Supports any count: 1=single tier, 2=web+data, 3=web+app+data, 4+=DMZ+apps+data. Default: 3")]
        int tierCount = 3,
        [Description("Include Azure Bastion subnet for secure RDP/SSH access. Default: true")]
        bool includeBastion = true,
        [Description("Include Azure Firewall subnet for network security. Default: true")]
        bool includeFirewall = true,
        [Description("Include VPN Gateway subnet for hybrid connectivity. Default: true")]
        bool includeGateway = true)
    {
        try
        {
            _logger.LogInformation("Designing network topology: AddressSpace={AddressSpace}, Tiers={Tiers}, Bastion={Bastion}, Firewall={Firewall}, Gateway={Gateway}",
                addressSpace, tierCount, includeBastion, includeFirewall, includeGateway);

            // Design the topology
            var topology = _networkDesignService.DesignMultiTierTopology(
                addressSpace,
                tierCount,
                includeBastion,
                includeFirewall,
                includeGateway);

            // Format the response
            var response = new StringBuilder();
            response.AppendLine($"✅ **Network Topology Designed**");
            response.AppendLine();
            response.AppendLine($"📍 **VNet Name**: {topology.VNetName}");
            response.AppendLine($"🌐 **Address Space**: {topology.VNetAddressSpace}");
            response.AppendLine($"📊 **Total Subnets**: {topology.Subnets.Count}");
            response.AppendLine();
            response.AppendLine("### Subnet Layout");
            response.AppendLine();

            foreach (var subnet in topology.Subnets)
            {
                var icon = subnet.Purpose switch
                {
                    SubnetPurpose.ApplicationGateway => "🚪",
                    SubnetPurpose.Application => "⚙️",
                    SubnetPurpose.Database => "🗄️",
                    SubnetPurpose.PrivateEndpoints => "🔒",
                    _ => "📦"
                };

                response.AppendLine($"{icon} **{subnet.Name}**");
                response.AppendLine($"   - CIDR: `{subnet.AddressPrefix}`");
                response.AppendLine($"   - Purpose: {subnet.Purpose}");
                
                if (subnet.ServiceEndpoints != null && subnet.ServiceEndpoints.Any())
                {
                    response.AppendLine($"   - Service Endpoints: {string.Join(", ", subnet.ServiceEndpoints.Take(3))}{(subnet.ServiceEndpoints.Count > 3 ? "..." : "")}");
                }
                response.AppendLine();
            }

            response.AppendLine("### Security Features");
            response.AppendLine();
            if (topology.EnableNetworkSecurityGroup)
                response.AppendLine("✅ Network Security Groups (NSG) enabled");
            if (topology.EnableDDoSProtection)
                response.AppendLine("✅ DDoS Protection enabled");
            if (topology.EnablePrivateDns)
                response.AppendLine("✅ Private DNS enabled");
            if (topology.EnablePrivateEndpoint)
                response.AppendLine("✅ Private Endpoints enabled");

            response.AppendLine();
            response.AppendLine("### Special Subnets");
            response.AppendLine();
            if (includeGateway)
                response.AppendLine("🌉 **GatewaySubnet** - For VPN/ExpressRoute Gateway (required name)");
            if (includeBastion)
                response.AppendLine("🛡️ **AzureBastionSubnet** - For Azure Bastion host (required name)");
            if (includeFirewall)
                response.AppendLine("🔥 **AzureFirewallSubnet** - For Azure Firewall (required name)");

            response.AppendLine();
            response.AppendLine("### 🎯 **Tier Naming Convention**");
            response.AppendLine();
            response.AppendLine("Tiers are intelligently named based on architectural best practices:");
            response.AppendLine("- **1 tier**: Application");
            response.AppendLine("- **2 tiers**: Web → Data (classic 2-tier)");
            response.AppendLine("- **3 tiers**: Web → Application → Data (classic 3-tier)");
            response.AppendLine("- **4 tiers**: DMZ → Web → Application → Data");
            response.AppendLine("- **5 tiers**: DMZ → Web → Application → Business → Data");
            response.AppendLine("- **6+ tiers**: DMZ → AppTier1...N → Data");
            response.AppendLine();
            response.AppendLine("💡 **Next Steps:**");
            response.AppendLine("1. Review the subnet layout and adjust tier count if needed (supports any number of tiers)");
            response.AppendLine("2. Ask me to 'generate Bicep template for this VNet' or 'show me the Terraform code'");
            response.AppendLine("3. Customize NSG rules for each subnet based on your security requirements");
            response.AppendLine("4. Configure route tables if you need custom routing");

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error designing network topology: {AddressSpace}", addressSpace);
            return $"❌ Error designing network topology: {ex.Message}";
        }
    }

    [KernelFunction("calculate_subnet_cidrs")]
    [Description("Calculate subnet CIDRs from an address space. Use this when users ask 'how many subnets fit in X' or 'calculate subnets for Y address space'.")]
    public string CalculateSubnetCIDRs(
        [Description("Address space to subdivide in CIDR notation. Example: '10.0.0.0/16'")]
        string addressSpace,
        [Description("Number of subnets needed. Example: 8")]
        int requiredSubnets)
    {
        try
        {
            _logger.LogInformation("Calculating subnet CIDRs for {AddressSpace} with {Count} subnets",
                addressSpace, requiredSubnets);

            var subnets = _networkDesignService.CalculateSubnetCIDRs(
                addressSpace,
                requiredSubnets,
                SubnetAllocationStrategy.EqualSize);

            var response = new StringBuilder();
            response.AppendLine($"✅ **Subnet Calculation Complete**");
            response.AppendLine();
            response.AppendLine($"📍 **Address Space**: {addressSpace}");
            response.AppendLine($"📊 **Required Subnets**: {requiredSubnets}");
            response.AppendLine($"📊 **Calculated Subnets**: {subnets.Count}");
            response.AppendLine();
            response.AppendLine("### Subnet CIDRs");
            response.AppendLine();

            foreach (var subnet in subnets)
            {
                // Parse the CIDR to show available IPs
                var parts = subnet.AddressPrefix.Split('/');
                if (parts.Length == 2 && int.TryParse(parts[1], out var prefix))
                {
                    var hostBits = 32 - prefix;
                    var totalIPs = (int)Math.Pow(2, hostBits);
                    var usableIPs = totalIPs - 5; // Azure reserves 5 IPs per subnet

                    response.AppendLine($"📦 **{subnet.Name}**");
                    response.AppendLine($"   - CIDR: `{subnet.AddressPrefix}`");
                    response.AppendLine($"   - Total IPs: {totalIPs}");
                    response.AppendLine($"   - Usable IPs: {usableIPs} (Azure reserves 5)");
                    response.AppendLine();
                }
            }

            response.AppendLine("💡 **Note**: Azure reserves the first 4 IPs and last 1 IP in each subnet.");
            response.AppendLine("Reserved IPs: Network address, Default gateway, DNS (x2), Broadcast.");

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating subnet CIDRs: {AddressSpace}", addressSpace);
            return $"❌ Error calculating subnets: {ex.Message}";
        }
    }

    [KernelFunction("predict_scaling_needs")]
    [Description("Predict future scaling needs for Azure resources based on historical metrics and trends. Use when users ask about: 'will I need to scale', 'predict scaling', 'forecast resource usage', 'when should I scale', 'anticipate load'. Supports VMSS, App Service Plans, and AKS clusters.")]
    public async Task<string> PredictScalingNeeds(
        [Description("Azure resource ID (e.g., /subscriptions/xxx/resourceGroups/xxx/providers/Microsoft.Compute/virtualMachineScaleSets/xxx)")]
        string resourceId,
        [Description("How many hours into the future to predict. Examples: 24 for 1 day, 168 for 1 week, 720 for 1 month. Default: 24")]
        int predictionHoursAhead = 24)
    {
        try
        {
            _logger.LogInformation("Predicting scaling needs for resource: {ResourceId}, Hours: {Hours}",
                resourceId, predictionHoursAhead);

            var targetTime = DateTime.UtcNow.AddHours(predictionHoursAhead);
            var recommendation = await _scalingEngine.GeneratePredictionAsync(resourceId, targetTime);

            var response = new StringBuilder();
            response.AppendLine($"🔮 **Predictive Scaling Analysis**");
            response.AppendLine();
            response.AppendLine($"📍 **Resource**: `{resourceId.Split('/').Last()}`");
            response.AppendLine($"⏰ **Prediction Time**: {targetTime:yyyy-MM-dd HH:mm} UTC ({predictionHoursAhead} hours ahead)");
            response.AppendLine($"📊 **Current Instances**: {recommendation.CurrentInstances}");
            response.AppendLine($"🎯 **Recommended Instances**: {recommendation.RecommendedInstances}");
            response.AppendLine($"📈 **Predicted Load**: {recommendation.PredictedLoad:F1}%");
            response.AppendLine($"✅ **Confidence Score**: {recommendation.ConfidenceScore:P0}");
            response.AppendLine();

            response.AppendLine($"### 🚀 Recommended Action: **{recommendation.RecommendedAction}**");
            response.AppendLine();

            if (!string.IsNullOrEmpty(recommendation.Reasoning))
            {
                response.AppendLine($"**Reasoning**: {recommendation.Reasoning}");
                response.AppendLine();
            }

            if (recommendation.MetricPredictions != null && recommendation.MetricPredictions.Any())
            {
                response.AppendLine("### 📊 Metric Predictions");
                response.AppendLine();
                foreach (var metric in recommendation.MetricPredictions.Take(3))
                {
                    var latest = metric.Predictions.OrderBy(p => p.Timestamp).LastOrDefault();
                    if (latest != null)
                    {
                        response.AppendLine($"- **{metric.MetricName}**: {latest.Value:F2} (range: {latest.LowerBound:F2}-{latest.UpperBound:F2})");
                    }
                }
                response.AppendLine();
            }

            response.AppendLine("💡 **Next Steps:**");
            if (recommendation.RecommendedAction != Platform.Engineering.Copilot.Core.Models.PredictiveScaling.ScalingAction.None)
            {
                response.AppendLine($"1. Review the prediction and confidence score");
                response.AppendLine($"2. If you agree, ask me to 'apply scaling recommendation' to execute the change");
                response.AppendLine($"3. Monitor the resource after scaling to validate the prediction");
            }
            else
            {
                response.AppendLine("✅ No scaling action needed - your current capacity is optimal!");
            }

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error predicting scaling needs: {ResourceId}", resourceId);
            return $"❌ Error predicting scaling: {ex.Message}";
        }
    }

    [KernelFunction("optimize_scaling_configuration")]
    [Description("Analyze and optimize auto-scaling configuration for Azure resources. Use when users ask: 'optimize my scaling', 'improve auto-scaling', 'tune scaling rules', 'better scaling configuration', 'scaling efficiency'. IMPORTANT: Extract the ACTUAL resource name, resource group, and subscription ID from the user's message - do NOT use placeholder values like 'your-resource-group' or 'yourAppServicePlan'. Build the complete Azure resource ID in the format: /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Web/serverfarms/{appServicePlanName} or /subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Compute/virtualMachineScaleSets/{vmssName}")]
    public async Task<string> OptimizeScalingConfiguration(
        [Description("Complete Azure resource ID (extract actual resource name, resource group, and subscription from user message). Format: /subscriptions/{guid}/resourceGroups/{actual-rg-name}/providers/Microsoft.Web/serverfarms/{actual-plan-name}")]
        string resourceId)
    {
        try
        {
            _logger.LogInformation("Optimizing scaling configuration for: {ResourceId}", resourceId);

            var optimizedConfig = await _scalingEngine.OptimizeScalingConfigurationAsync(resourceId);

            var response = new StringBuilder();
            response.AppendLine($"⚙️ **Scaling Configuration Optimization**");
            response.AppendLine();
            response.AppendLine($"📍 **Resource**: `{resourceId.Split('/').Last()}`");
            response.AppendLine();

            response.AppendLine("### 🎯 Optimized Configuration");
            response.AppendLine();
            response.AppendLine($"- **Min Instances**: {optimizedConfig.Constraints.MinimumInstances}");
            response.AppendLine($"- **Max Instances**: {optimizedConfig.Constraints.MaximumInstances}");
            response.AppendLine($"- **Scale-Up Threshold**: {optimizedConfig.Thresholds.ScaleUpThreshold}%");
            response.AppendLine($"- **Scale-Down Threshold**: {optimizedConfig.Thresholds.ScaleDownThreshold}%");
            response.AppendLine($"- **Cooldown Period**: {optimizedConfig.Thresholds.CooldownMinutes} minutes");
            response.AppendLine($"- **Strategy**: {optimizedConfig.Strategy}");
            response.AppendLine();

            if (optimizedConfig.Metrics.PrimaryMetrics != null && optimizedConfig.Metrics.PrimaryMetrics.Any())
            {
                response.AppendLine("### 📊 Recommended Metrics to Monitor");
                response.AppendLine();
                foreach (var metric in optimizedConfig.Metrics.PrimaryMetrics)
                {
                    response.AppendLine($"- {metric}");
                }
                response.AppendLine();
            }

            response.AppendLine("### 💡 Configuration Details");
            response.AppendLine();
            response.AppendLine($"- **Prediction Model**: {optimizedConfig.PredictionSettings.Model}");
            response.AppendLine($"- **Lookback Period**: {optimizedConfig.Metrics.LookbackPeriodDays} days");
            response.AppendLine($"- **Prediction Horizon**: {optimizedConfig.Metrics.PredictionHorizonHours} hours");
            response.AppendLine($"- **Confidence Level**: {optimizedConfig.PredictionSettings.ConfidenceLevel:P0}");
            response.AppendLine();

            response.AppendLine("### 🚀 Next Steps");
            response.AppendLine("1. Review the optimized configuration above");
            response.AppendLine("2. Apply these settings to your resource's auto-scaling rules");
            response.AppendLine("3. Monitor performance for 1-2 weeks to validate effectiveness");
            response.AppendLine("4. Ask me to 'analyze scaling performance' to review results");

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error optimizing scaling configuration: {ResourceId}", resourceId);
            return $"❌ Error optimizing configuration: {ex.Message}";
        }
    }

    [KernelFunction("analyze_scaling_performance")]
    [Description("Analyze historical scaling performance and effectiveness. Use when users ask: 'how is my scaling performing', 'scaling efficiency', 'review scaling history', 'scaling performance metrics', 'was scaling effective'. IMPORTANT: Extract the ACTUAL resource name, resource group, and subscription ID from the user's message - do NOT use placeholder values.")]
    public async Task<string> AnalyzeScalingPerformance(
        [Description("Complete Azure resource ID (extract actual resource name, resource group, and subscription from user message). Format: /subscriptions/{guid}/resourceGroups/{actual-rg-name}/providers/Microsoft.Web/serverfarms/{actual-plan-name}")]
        string resourceId,
        [Description("Number of days to analyze. Default: 7")]
        int daysToAnalyze = 7)
    {
        try
        {
            _logger.LogInformation("Analyzing scaling performance for: {ResourceId}, Days: {Days}",
                resourceId, daysToAnalyze);

            var endDate = DateTime.UtcNow;
            var startDate = endDate.AddDays(-daysToAnalyze);

            var metrics = await _scalingEngine.AnalyzeScalingPerformanceAsync(resourceId, startDate, endDate);

            var response = new StringBuilder();
            response.AppendLine($"📈 **Scaling Performance Analysis**");
            response.AppendLine();
            response.AppendLine($"📍 **Resource**: `{resourceId.Split('/').Last()}`");
            response.AppendLine($"📅 **Period**: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} ({daysToAnalyze} days)");
            response.AppendLine();

            response.AppendLine("### 📊 Performance Metrics");
            response.AppendLine();
            response.AppendLine($"- **Total Scaling Events**: {metrics.TotalScalingEvents}");
            response.AppendLine($"- **Successful Events**: {metrics.SuccessfulScalingEvents}");
            response.AppendLine($"- **Success Rate**: {(metrics.TotalScalingEvents > 0 ? (double)metrics.SuccessfulScalingEvents / metrics.TotalScalingEvents : 0):P0}");
            response.AppendLine($"- **Average Response Time**: {metrics.AverageResponseTime:F1} minutes");
            response.AppendLine();

            response.AppendLine("### 💰 Cost Impact");
            response.AppendLine();
            response.AppendLine($"- **Cost Savings**: {metrics.CostSavingsPercentage:P1}");
            response.AppendLine($"- **Over-Provisioning Time**: {metrics.OverProvisioningPercentage:P0}");
            response.AppendLine($"- **Under-Provisioning Time**: {metrics.UnderProvisioningPercentage:P0}");
            response.AppendLine();

            response.AppendLine("### 🎯 Efficiency Score");
            response.AppendLine();
            // Calculate efficiency score based on success rate and provisioning balance
            var efficiencyScore = metrics.TotalScalingEvents > 0 
                ? ((double)metrics.SuccessfulScalingEvents / metrics.TotalScalingEvents) * 
                  (1 - Math.Abs(metrics.OverProvisioningPercentage - metrics.UnderProvisioningPercentage))
                : 0;
            
            var efficiencyEmoji = efficiencyScore switch
            {
                >= 0.9 => "🌟",
                >= 0.7 => "👍",
                >= 0.5 => "⚠️",
                _ => "❌"
            };
            response.AppendLine($"{efficiencyEmoji} **{efficiencyScore:P0}** - {GetEfficiencyRating(efficiencyScore)}");
            response.AppendLine();

            if (metrics.MetricAccuracy != null && metrics.MetricAccuracy.Any())
            {
                response.AppendLine("### � Metric Prediction Accuracy");
                response.AppendLine();
                foreach (var metric in metrics.MetricAccuracy.Take(5))
                {
                    response.AppendLine($"- **{metric.Key}**: {metric.Value:P0}");
                }
                response.AppendLine();
            }

            response.AppendLine("### 🚀 Next Steps");
            response.AppendLine("1. If efficiency is low, ask me to 'optimize scaling configuration'");
            response.AppendLine("2. Review over/under-provisioning percentages for tuning opportunities");
            response.AppendLine("3. Monitor cost savings and adjust thresholds as needed");

            return response.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing scaling performance: {ResourceId}", resourceId);
            return $"❌ Error analyzing performance: {ex.Message}";
        }
    }

    private string GetEfficiencyRating(double score)
    {
        return score switch
        {
            >= 0.9 => "Excellent",
            >= 0.7 => "Good",
            >= 0.5 => "Needs Improvement",
            _ => "Poor"
        };
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

    // ========== AZURE MCP ENHANCED FUNCTIONS ==========

    [KernelFunction("generate_infrastructure_template")]
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

    [KernelFunction("deploy_infrastructure_with_azd")]
    [Description("Deploy infrastructure using Azure Developer CLI (azd) with automated orchestration. " +
                 "Leverages official Microsoft Azure Developer CLI for production-grade deployments. " +
                 "Use when you want streamlined deployment automation with Microsoft-supported tooling.")]
    public async Task<string> DeployInfrastructureWithAzdAsync(
        [Description("Path to infrastructure template or Azure Developer template directory")] 
        string templatePath,
        
        [Description("Environment name (e.g., 'dev', 'staging', 'prod')")] 
        string environment,
        
        [Description("Azure location/region (e.g., 'eastus', 'usgovvirginia')")] 
        string location,
        
        [Description("Resource group name (optional - azd will create if not specified)")] 
        string? resourceGroup = null,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Deploying infrastructure with azd: {TemplatePath} to {Environment}", templatePath, environment);

            await _azureMcpClient.InitializeAsync(cancellationToken);

            // Use Azure MCP azd tool for deployment
            var deploymentParams = new Dictionary<string, object?>
            {
                ["command"] = "deploy",
                ["parameters"] = new
                {
                    templatePath = templatePath,
                    environment = environment,
                    location = location,
                    resourceGroup = resourceGroup
                }
            };

            _logger.LogInformation("Executing azd deployment via Azure MCP");
            var azdResult = await _azureMcpClient.CallToolAsync("azd", deploymentParams, cancellationToken);

            if (!azdResult.Success)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Azure Developer CLI deployment failed",
                    details = azdResult.Result
                });
            }

            // Also use deploy tool for status tracking
            var deployStatus = await _azureMcpClient.CallToolAsync("deploy", 
                new Dictionary<string, object?>
                {
                    ["command"] = "status",
                    ["parameters"] = new { environment }
                }, cancellationToken);

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                environment = environment,
                location = location,
                resourceGroup = resourceGroup ?? $"rg-{environment}",
                deployment = new
                {
                    tool = "Azure Developer CLI (azd)",
                    result = azdResult.Result,
                    status = deployStatus.Success ? deployStatus.Result : "Status check unavailable"
                },
                nextSteps = new[]
                {
                    "Review the deployment results above for any warnings or errors.",
                    "Say 'get deployment status for environment <name>' to check deployment progress.",
                    "Say 'list resources in resource group <name>' to see what was deployed.",
                    "Check Azure Portal for detailed deployment logs and resource status."
                }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deploying infrastructure with azd");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                recommendation = "Verify Azure Developer CLI is available and template path is correct."
            });
        }
    }

    [KernelFunction("provision_aks_with_best_practices")]
    [Description("Provision Azure Kubernetes Service (AKS) cluster with Microsoft best practices and security hardening. " +
                 "Combines infrastructure provisioning with Azure MCP AKS operations and best practices guidance. " +
                 "Use when you need production-ready AKS clusters with proper configuration.")]
    public async Task<string> ProvisionAksWithBestPracticesAsync(
        [Description("AKS cluster name")] 
        string clusterName,
        
        [Description("Azure resource group name")] 
        string resourceGroup,
        
        [Description("Azure location/region (e.g., 'eastus', 'usgovvirginia')")] 
        string location,
        
        [Description("Node count (default: 3)")] 
        int nodeCount = 3,
        
        [Description("VM size for nodes (default: 'Standard_DS2_v2')")] 
        string vmSize = "Standard_DS2_v2",
        
        [Description("Include Microsoft AKS best practices recommendations (default: true)")] 
        bool includeBestPractices = true,
        
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Provisioning AKS cluster with best practices: {ClusterName} in {ResourceGroup}", 
                clusterName, resourceGroup);

            // 1. Get AKS best practices from Azure MCP
            object? bestPracticesData = null;
            if (includeBestPractices)
            {
                try
                {
                    await _azureMcpClient.InitializeAsync(cancellationToken);
                    
                    _logger.LogInformation("Fetching AKS best practices via Azure MCP");

                    var bestPractices = await _azureMcpClient.CallToolAsync("get_bestpractices", 
                        new Dictionary<string, object?>
                        {
                            ["resourceType"] = "Microsoft.ContainerService/managedClusters"
                        }, cancellationToken);

                    bestPracticesData = new
                    {
                        available = bestPractices.Success,
                        recommendations = bestPractices.Success ? bestPractices.Result : "Best practices not available"
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not retrieve AKS best practices from Azure MCP");
                    bestPracticesData = new
                    {
                        available = false,
                        error = "Best practices service temporarily unavailable"
                    };
                }
            }

            // 2. Use existing infrastructure service to provision
            var provisioningRequest = $@"
                Create an AKS cluster named {clusterName} in resource group {resourceGroup} 
                in {location} with {nodeCount} nodes of size {vmSize}.
                Enable managed identity, network policy, and Azure RBAC.
                Configure monitoring with Azure Monitor and Log Analytics.
            ";

            var provisioningResult = await _infrastructureService.ProvisionInfrastructureAsync(provisioningRequest, cancellationToken);

            if (!provisioningResult.Success)
            {
                return System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    error = $"AKS provisioning failed: {provisioningResult.Message}"
                });
            }

            // 3. Use Azure MCP AKS tool for additional configuration
            object? aksConfiguration = null;
            try
            {
                _logger.LogInformation("Configuring AKS cluster via Azure MCP");

                var aksConfig = await _azureMcpClient.CallToolAsync("aks", 
                    new Dictionary<string, object?>
                    {
                        ["command"] = "get",
                        ["parameters"] = new 
                        { 
                            clusterName = clusterName,
                            resourceGroup = resourceGroup
                        }
                    }, cancellationToken);

                aksConfiguration = new
                {
                    available = aksConfig.Success,
                    details = aksConfig.Success ? aksConfig.Result : "AKS configuration not available"
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve AKS configuration from Azure MCP");
                aksConfiguration = new
                {
                    available = false,
                    error = "AKS configuration service temporarily unavailable"
                };
            }

            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = true,
                cluster = new
                {
                    name = clusterName,
                    resourceGroup = resourceGroup,
                    location = location,
                    nodeCount = nodeCount,
                    vmSize = vmSize
                },
                provisioning = new
                {
                    status = provisioningResult.Success ? "Completed" : "Failed",
                    message = provisioningResult.Message,
                    resourceId = provisioningResult.ResourceId
                },
                bestPractices = bestPracticesData,
                configuration = aksConfiguration,
                nextSteps = new[]
                {
                    "Review the AKS best practices recommendations above before deploying workloads.",
                    "Configure kubectl to connect: az aks get-credentials --resource-group " + resourceGroup + " --name " + clusterName,
                    "Say 'show me the AKS cluster configuration' to review detailed settings.",
                    "Say 'deploy application to AKS cluster <name>' to deploy your workloads.",
                    "Enable Azure Policy for Kubernetes for additional governance and compliance."
                }
            }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error provisioning AKS cluster with best practices");
            return System.Text.Json.JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

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
}
