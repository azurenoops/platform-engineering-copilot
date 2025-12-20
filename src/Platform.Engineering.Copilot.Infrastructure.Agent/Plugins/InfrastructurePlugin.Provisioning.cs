using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Platform.Engineering.Copilot.Infrastructure.Core;

/// <summary>
/// Partial class containing Azure Provision infrastructure functions:
/// - Provisioning of infrastructure resources
/// - Deprovisioning of infrastructure resources
/// - Scaling of infrastructure resources
/// - Monitoring of infrastructure resources
/// - Managing infrastructure configurations
/// </summary>
public partial class InfrastructurePlugin
{
   
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
}