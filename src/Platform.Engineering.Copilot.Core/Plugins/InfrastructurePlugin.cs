using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Core.Plugins;

/// <summary>
/// Semantic Kernel plugin for Azure infrastructure provisioning
/// Uses natural language queries to provision infrastructure via AI-powered service
/// Example: "Create a storage account named mydata in eastus with Standard_LRS"
/// </summary>
public class InfrastructurePlugin : BaseSupervisorPlugin
{
    private readonly IInfrastructureProvisioningService _infrastructureService;

    public InfrastructurePlugin(
        ILogger<InfrastructurePlugin> logger,
        Kernel kernel,
        IInfrastructureProvisioningService infrastructureService)
        : base(logger, kernel)
    {
        _infrastructureService = infrastructureService;
    }

    [KernelFunction("provision_infrastructure")]
    [Description("Provision Azure infrastructure from natural language query. " +
                 "Supports storage accounts, VNets, Key Vaults, NSGs, managed identities, Load Balancers, and more.")]
    public async Task<string> ProvisionInfrastructureAsync(
        [Description("Natural language query describing the infrastructure to provision. " +
                     "Examples: 'Create a storage account named mydata in eastus', " +
                     "'Provision VNet with address space 10.0.0.0/16', " +
                     "'Set up Key Vault with soft delete enabled'")] 
        string query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing infrastructure provisioning query: {Query}", query);

            var result = await _infrastructureService.ProvisionInfrastructureAsync(query, cancellationToken);

            if (result.Success)
            {
                return $"{result.Message}\n" +
                       $"📍 Resource ID: {result.ResourceId}\n" +
                       $"📦 Resource Type: {result.ResourceType}\n" +
                       $"✅ Status: {result.Status}";
            }
            else
            {
                return $"❌ Failed to provision infrastructure\n" +
                       $"Query: {query}\n" +
                       $"Error: {result.ErrorDetails}\n" +
                       $"Suggestion: Check query syntax and ensure all required parameters are provided";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error provisioning infrastructure: {Query}", query);
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
}
