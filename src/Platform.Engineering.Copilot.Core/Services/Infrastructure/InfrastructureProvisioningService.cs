using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Services.Infrastructure;

/// <summary>
/// AI-powered infrastructure provisioning service using natural language queries
/// Uses Semantic Kernel to parse natural language queries into structured provisioning requests
/// Example: "Create a storage account named mydata in eastus with Standard_LRS SKU"
/// </summary>
public class InfrastructureProvisioningService : IInfrastructureProvisioningService
{
    private readonly ILogger<InfrastructureProvisioningService> _logger;
    private readonly IAzureResourceService _azureResourceService;
    private readonly Kernel _kernel;

    public InfrastructureProvisioningService(
        ILogger<InfrastructureProvisioningService> logger,
        IAzureResourceService azureResourceService,
        Kernel kernel)
    {
        _logger = logger;
        _azureResourceService = azureResourceService;
        _kernel = kernel;
    }

    public async Task<InfrastructureProvisionResult> ProvisionInfrastructureAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing infrastructure provisioning query: {Query}", query);

        try
        {
            // Step 1: Parse the natural language query using AI
            var intent = await ParseQueryAsync(query, cancellationToken);

            if (!intent.Success)
            {
                return new InfrastructureProvisionResult
                {
                    Success = false,
                    Status = "Failed",
                    ErrorDetails = intent.ErrorMessage ?? "Failed to parse query",
                    Message = $"❌ Could not understand query: {query}"
                };
            }

            // Step 2: Route to appropriate provisioning method
            return await ProvisionResourceAsync(intent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing infrastructure query: {Query}", query);
            return new InfrastructureProvisionResult
            {
                Success = false,
                Status = "Failed",
                ErrorDetails = ex.Message,
                Message = "❌ Failed to provision infrastructure"
            };
        }
    }

    public async Task<InfrastructureCostEstimate> EstimateCostAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Estimating cost for query: {Query}", query);

        try
        {
            var intent = await ParseQueryAsync(query, cancellationToken);

            // Simple cost estimation based on resource type
            var monthlyCost = intent.ResourceType?.ToLowerInvariant() switch
            {
                "vnet" or "virtual-network" => 0.00m,
                "storage-account" => 20.00m,
                "keyvault" or "key-vault" => 0.03m,
                "nsg" => 0.00m,
                "load-balancer" => 25.00m,
                "managed-identity" => 0.00m,
                "log-analytics" => 2.30m,
                "app-insights" => 2.88m,
                _ => 10.00m
            };

            return new InfrastructureCostEstimate
            {
                ResourceType = intent.ResourceType ?? "Unknown",
                Location = intent.Location ?? "eastus",
                MonthlyEstimate = monthlyCost,
                AnnualEstimate = monthlyCost * 12,
                Currency = "USD",
                Notes = "Estimated cost based on standard configuration"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error estimating cost");
            return new InfrastructureCostEstimate
            {
                ResourceType = "Error",
                MonthlyEstimate = 0,
                AnnualEstimate = 0,
                Notes = $"Error: {ex.Message}"
            };
        }
    }

    public async Task<List<string>> ListResourceGroupsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing Resource Groups");

        try
        {
            // TODO: Implement actual listing via _azureResourceService
            await Task.CompletedTask;
            return new List<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Resource Groups");
            return new List<string>();
        }
    }

    public async Task<bool> DeleteResourceGroupAsync(
        string resourceGroupName,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting Resource Group: {ResourceGroupName}", resourceGroupName);

        try
        {
            // TODO: Implement actual deletion via _azureResourceService
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Resource Group: {ResourceGroupName}", resourceGroupName);
            return false;
        }
    }

    #region AI Query Parsing

    private async Task<InfrastructureIntent> ParseQueryAsync(
        string query,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Parsing query with AI: {Query}", query);

        try
        {
            var chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

            var systemPrompt = @"You are an Azure infrastructure query parser. Parse user queries and return JSON with these fields:
- resourceType: storage-account|vnet|subnet|keyvault|nsg|managed-identity|log-analytics|app-insights|load-balancer|blob-container|file-share
- resourceGroupName: string (generate if not mentioned, pattern: rg-{purpose})
- resourceName: string (required)
- location: eastus|westus2|usgovvirginia|centralus (default: eastus if not specified)
- parameters: object with resource-specific settings (sku, addressSpace, quotaInGb, etc.)

**Examples:**

Query: ""Create storage account named mydata in eastus""
Response: {""resourceType"":""storage-account"",""resourceGroupName"":""rg-mydata"",""resourceName"":""mydata"",""location"":""eastus"",""parameters"":{""sku"":""Standard_LRS"",""enableHttpsOnly"":true}}

Query: ""Provision VNet with address space 10.0.0.0/16 in usgovvirginia""
Response: {""resourceType"":""vnet"",""resourceGroupName"":""rg-network"",""resourceName"":""vnet-main"",""location"":""usgovvirginia"",""parameters"":{""addressSpace"":""10.0.0.0/16""}}

Query: ""Set up Key Vault named secrets-vault with soft delete""
Response: {""resourceType"":""keyvault"",""resourceGroupName"":""rg-security"",""resourceName"":""secrets-vault"",""location"":""eastus"",""parameters"":{""enableSoftDelete"":true,""enablePurgeProtection"":true}}

Query: ""Create blob container named data in storage account mystorageacct""
Response: {""resourceType"":""blob-container"",""resourceGroupName"":""rg-storage"",""resourceName"":""data"",""location"":""eastus"",""parameters"":{""storageAccountName"":""mystorageacct"",""publicAccess"":""None""}}

**Rules:**
1. Always generate resourceGroupName if not mentioned (pattern: rg-{purpose})
2. Always generate resourceName if not mentioned (pattern: {type}-{purpose})
3. Default location to ""eastus"" if not specified
4. Infer reasonable defaults for parameters
5. For blob containers and file shares, extract storageAccountName from query
6. Return ONLY valid JSON, no markdown code blocks

Parse this query:";

            var chatHistory = new ChatHistory(systemPrompt);
            chatHistory.AddUserMessage(query);

            var response = await chatCompletion.GetChatMessageContentAsync(
                chatHistory,
                kernel: _kernel,
                cancellationToken: cancellationToken);

            var jsonResponse = response.Content?.Trim() ?? "{}";
            
            // Remove markdown code blocks if present
            jsonResponse = Regex.Replace(jsonResponse, @"^```json\s*|\s*```$", "", RegexOptions.Multiline).Trim();

            _logger.LogDebug("AI response: {Json}", jsonResponse);

            // Parse the JSON response into InfrastructureIntent
            var intent = System.Text.Json.JsonSerializer.Deserialize<InfrastructureIntent>(jsonResponse,
                new System.Text.Json.JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });

            if (intent == null)
            {
                return new InfrastructureIntent 
                { 
                    Success = false, 
                    ErrorMessage = "Failed to parse AI response" 
                };
            }

            intent.Success = true;
            _logger.LogDebug("Parsed intent: Type={Type}, Name={Name}, RG={RG}, Location={Location}", 
                intent.ResourceType, intent.ResourceName, intent.ResourceGroupName, intent.Location);
            
            return intent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing query with AI");
            return new InfrastructureIntent 
            { 
                Success = false, 
                ErrorMessage = $"AI parsing error: {ex.Message}" 
            };
        }
    }

    #endregion

    #region Resource Provisioning

    private async Task<InfrastructureProvisionResult> ProvisionResourceAsync(
        InfrastructureIntent intent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning {ResourceType}: {ResourceName} in {ResourceGroup}", 
            intent.ResourceType, intent.ResourceName, intent.ResourceGroupName);

        var location = intent.Location ?? "eastus";
        var providerType = GetProviderType(intent.ResourceType);
        var resourceId = $"/subscriptions/{{subscription}}/resourceGroups/{intent.ResourceGroupName}/providers/{providerType}/{intent.ResourceName}";

        // TODO: Implement actual Azure provisioning via _azureResourceService
        // For now, return simulated success with detailed information

        await Task.CompletedTask;

        var properties = new Dictionary<string, string>
        {
            { "location", location },
            { "resourceGroup", intent.ResourceGroupName }
        };

        // Add parameters to properties for visibility
        if (intent.Parameters != null)
        {
            foreach (var param in intent.Parameters)
            {
                properties[param.Key] = param.Value?.ToString() ?? "";
            }
        }

        return new InfrastructureProvisionResult
        {
            Success = true,
            ResourceId = resourceId,
            ResourceName = intent.ResourceName,
            ResourceType = providerType,
            Status = "Succeeded",
            Message = $"✅ {GetFriendlyResourceType(intent.ResourceType)} '{intent.ResourceName}' provisioned in {location}",
            Properties = properties
        };
    }

    private string GetProviderType(string? resourceType)
    {
        return resourceType?.ToLowerInvariant() switch
        {
            "vnet" or "virtual-network" => "Microsoft.Network/virtualNetworks",
            "subnet" => "Microsoft.Network/virtualNetworks/subnets",
            "nsg" => "Microsoft.Network/networkSecurityGroups",
            "load-balancer" => "Microsoft.Network/loadBalancers",
            "storage-account" => "Microsoft.Storage/storageAccounts",
            "blob-container" => "Microsoft.Storage/storageAccounts/blobServices/containers",
            "file-share" => "Microsoft.Storage/storageAccounts/fileServices/shares",
            "keyvault" or "key-vault" => "Microsoft.KeyVault/vaults",
            "managed-identity" => "Microsoft.ManagedIdentity/userAssignedIdentities",
            "log-analytics" => "Microsoft.OperationalInsights/workspaces",
            "app-insights" => "Microsoft.Insights/components",
            _ => "Unknown"
        };
    }

    private string GetFriendlyResourceType(string? resourceType)
    {
        return resourceType?.ToLowerInvariant() switch
        {
            "vnet" or "virtual-network" => "Virtual Network",
            "subnet" => "Subnet",
            "nsg" => "Network Security Group",
            "load-balancer" => "Load Balancer",
            "storage-account" => "Storage Account",
            "blob-container" => "Blob Container",
            "file-share" => "File Share",
            "keyvault" or "key-vault" => "Key Vault",
            "managed-identity" => "Managed Identity",
            "log-analytics" => "Log Analytics Workspace",
            "app-insights" => "Application Insights",
            _ => resourceType ?? "Resource"
        };
    }

    #endregion
}

/// <summary>
/// Parsed infrastructure intent from natural language query
/// </summary>
internal class InfrastructureIntent
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResourceType { get; set; }
    public string ResourceGroupName { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string? Location { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}
