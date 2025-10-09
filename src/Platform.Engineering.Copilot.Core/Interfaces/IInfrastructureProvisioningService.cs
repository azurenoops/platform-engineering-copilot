using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Platform.Engineering.Copilot.Core.Interfaces;

/// <summary>
/// AI-powered infrastructure provisioning service using natural language queries
/// Supports provisioning Azure resources through conversational commands
/// Example: "Create a storage account named mydata in eastus with Standard_LRS"
/// </summary>
public interface IInfrastructureProvisioningService
{
    /// <summary>
    /// Provision infrastructure from natural language query
    /// Uses AI to parse intent, extract parameters, and execute provisioning
    /// </summary>
    /// <param name="query">Natural language query describing the infrastructure to provision</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Provisioning result with resource details or error information</returns>
    /// <example>
    /// Examples:
    /// - "Create a storage account named mydata in eastus with Standard_LRS SKU"
    /// - "Provision a VNet with address space 10.0.0.0/16 and 3 subnets"
    /// - "Set up Key Vault with soft delete enabled in usgovvirginia"
    /// - "Create NSG named app-nsg in westus2"
    /// - "Deploy Log Analytics workspace with 90 day retention"
    /// </example>
    Task<InfrastructureProvisionResult> ProvisionInfrastructureAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Estimate cost for infrastructure from natural language query
    /// </summary>
    Task<InfrastructureCostEstimate> EstimateCostAsync(
        string query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all resource groups in the subscription
    /// </summary>
    Task<List<string>> ListResourceGroupsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a resource group and all its resources
    /// </summary>
    Task<bool> DeleteResourceGroupAsync(
        string resourceGroupName,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of infrastructure provisioning operation
/// </summary>
public class InfrastructureProvisionResult
{
    public bool Success { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public string ResourceName { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? ErrorDetails { get; set; }
    public Dictionary<string, string>? Properties { get; set; }
    public DateTime ProvisionedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Cost estimate for infrastructure resources
/// </summary>
public class InfrastructureCostEstimate
{
    public string ResourceType { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal MonthlyEstimate { get; set; }
    public decimal AnnualEstimate { get; set; }
    public string Currency { get; set; } = "USD";
    public Dictionary<string, decimal>? CostBreakdown { get; set; }
    public string? Notes { get; set; }
}
