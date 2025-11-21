using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using System.Text.Json;
using Azure.ResourceManager.Resources;

namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;

/// <summary>
/// Specialized evidence collector for Access Control (AC) family using real Azure APIs
/// </summary>
public class AccessControlEvidenceCollector : IEvidenceCollector
{
    private readonly ILogger _logger;
    private readonly IAzureResourceService _azureService;

    public AccessControlEvidenceCollector(ILogger logger, IAzureResourceService azureService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _azureService = azureService ?? throw new ArgumentNullException(nameof(azureService));
    }

    public async Task<List<ComplianceEvidence>> CollectConfigurationEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        CancellationToken cancellationToken = default)
    {
        // Collect all AC evidence and filter for configuration-type evidence
        var allEvidence = await CollectAccessControlEvidenceAsync(subscriptionId, controlFamily, collectedBy, cancellationToken);
        return allEvidence.Where(e => 
            e.EvidenceType == "NetworkSecurityGroups" || 
            e.EvidenceType == "KeyVaultConfiguration" ||
            e.EvidenceType == "VirtualMachines").ToList();
    }

    public async Task<List<ComplianceEvidence>> CollectLogEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        CancellationToken cancellationToken = default)
    {
        // Collect all AC evidence and filter for log-type evidence (Log Analytics)
        var allEvidence = await CollectAccessControlEvidenceAsync(subscriptionId, controlFamily, collectedBy, cancellationToken);
        return allEvidence.Where(e => e.EvidenceType == "LogAnalyticsWorkspaces").ToList();
    }

    public async Task<List<ComplianceEvidence>> CollectMetricEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        CancellationToken cancellationToken = default)
    {
        // Access Control family doesn't typically have metric-based evidence
        // Return empty list as this is expected for AC family
        _logger.LogDebug("No metric evidence collected for Access Control family (expected behavior)");
        return new List<ComplianceEvidence>();
    }

    public async Task<List<ComplianceEvidence>> CollectPolicyEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement Azure Policy evidence collection for Access Control
        // Should collect: RBAC policies, Azure Policy assignments, conditional access policies
        _logger.LogWarning("Azure Policy evidence collection not yet implemented for Access Control family");
        return new List<ComplianceEvidence>();
    }

    public async Task<List<ComplianceEvidence>> CollectAccessControlEvidenceAsync(
        string subscriptionId, 
        string controlFamily,
        string collectedBy,
        CancellationToken cancellationToken = default)
    {
        var evidence = new List<ComplianceEvidence>();

        try
        {
            _logger.LogDebug("Collecting Access Control evidence for subscription {SubscriptionId}", subscriptionId);

            // Get all resources for evidence collection
            var resources = await _azureService.ListAllResourceGroupsInSubscriptionAsync(subscriptionId);
            
            if (resources == null || resources.Count() == 0)
            {
                _logger.LogWarning("No resources found for subscription {SubscriptionId}", subscriptionId);
                return evidence;
            }

            // Collect Network Security Group evidence (AC-3, AC-4)
            var nsgs = resources.Where(r => ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Network/networkSecurityGroups", StringComparison.OrdinalIgnoreCase)).ToList();
            if (nsgs.Any())
            {
                var nsgData = new Dictionary<string, object>
                {
                    ["totalNSGs"] = nsgs.Count,
                    ["nsgList"] = nsgs.Select(nsg => new
                    {
                        name = ((GenericResource)nsg).Data.Name,
                        id = ((GenericResource)nsg).Data.Id.ToString(),
                        location = ((GenericResource)nsg).Data.Location.ToString(),
                        resourceGroup = ((GenericResource)nsg).Id.ResourceGroupName
                    }).ToList(),
                    ["collectedAt"] = DateTimeOffset.UtcNow
                };

                evidence.Add(new ComplianceEvidence
                {
                    EvidenceId = Guid.NewGuid().ToString(),
                    EvidenceType = "NetworkSecurityGroups",
                    ControlId = "AC-3",
                    ResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.Network/networkSecurityGroups",
                    CollectedAt = DateTimeOffset.UtcNow,
                    Data = nsgData,
                    Screenshot = JsonSerializer.Serialize(nsgData, new JsonSerializerOptions { WriteIndented = true }),
                    CollectedBy = collectedBy
                });
            }

            // Collect Key Vault evidence (AC-6 - Least Privilege)
            var keyVaults = resources.Where(r => ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.KeyVault/vaults", StringComparison.OrdinalIgnoreCase)).ToList();
            if (keyVaults.Any())
            {
                var kvData = new Dictionary<string, object>
                {
                    ["totalKeyVaults"] = keyVaults.Count,
                    ["keyVaultList"] = keyVaults.Select(kv => new
                    {
                        name = ((GenericResource)kv).Data.Name,
                        id = ((GenericResource)kv).Data.Id.ToString(),
                        location = ((GenericResource)kv).Data.Location.ToString(),
                        resourceGroup = ((GenericResource)kv).Id.ResourceGroupName
                    }).ToList(),
                    ["collectedAt"] = DateTimeOffset.UtcNow
                };

                evidence.Add(new ComplianceEvidence
                {
                    EvidenceId = Guid.NewGuid().ToString(),
                    EvidenceType = "KeyVaultConfiguration",
                    ControlId = "AC-6",
                    ResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.KeyVault/vaults",
                    CollectedAt = DateTimeOffset.UtcNow,
                    Data = kvData,
                    Screenshot = JsonSerializer.Serialize(kvData, new JsonSerializerOptions { WriteIndented = true }),
                    CollectedBy = collectedBy
                });
            }

            // Collect Log Analytics Workspace evidence (AC-7 - Logon monitoring)
            var logWorkspaces = resources.Where(r => ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.OperationalInsights/workspaces", StringComparison.OrdinalIgnoreCase)).ToList();
            if (logWorkspaces.Any())
            {
                var logData = new Dictionary<string, object>
                {
                    ["totalWorkspaces"] = logWorkspaces.Count,
                    ["workspaceList"] = logWorkspaces.Select(ws => new
                    {
                        name = ((GenericResource)ws).Data.Name,
                        id = ((GenericResource)ws).Data.Id.ToString(),
                        location = ((GenericResource)ws).Data.Location.ToString(),
                        resourceGroup = ((GenericResource)ws).Id.ResourceGroupName
                    }).ToList(),
                    ["collectedAt"] = DateTimeOffset.UtcNow
                };

                evidence.Add(new ComplianceEvidence
                {
                    EvidenceId = Guid.NewGuid().ToString(),
                    EvidenceType = "LogAnalyticsWorkspaces",
                    ControlId = "AC-7",
                    ResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.OperationalInsights/workspaces",
                    CollectedAt = DateTimeOffset.UtcNow,
                    Data = logData,
                    Screenshot = JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = true }),
                    CollectedBy = collectedBy
                });
            }

            // Collect Virtual Machine evidence (AC-2 - Account Management)
            var vms = resources.Where(r => ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase)).ToList();
            if (vms.Any())
            {
                var vmData = new Dictionary<string, object>
                {
                    ["totalVMs"] = vms.Count,
                    ["vmList"] = vms.Select(vm => new
                    {
                        name = ((GenericResource)vm).Data.Name,
                        id = ((GenericResource)vm).Data.Id.ToString(),
                        location = ((GenericResource)vm).Data.Location.ToString(),
                        resourceGroup = ((GenericResource)vm).Id.ResourceGroupName
                    }).ToList(),
                    ["collectedAt"] = DateTimeOffset.UtcNow
                };

                evidence.Add(new ComplianceEvidence
                {
                    EvidenceId = Guid.NewGuid().ToString(),
                    EvidenceType = "VirtualMachines",
                    ControlId = "AC-2",
                    ResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.Compute/virtualMachines",
                    CollectedAt = DateTimeOffset.UtcNow,
                    Data = vmData,
                    Screenshot = JsonSerializer.Serialize(vmData, new JsonSerializerOptions { WriteIndented = true }),
                    CollectedBy = collectedBy
                });
            }

            _logger.LogInformation("Collected {Count} pieces of Access Control evidence for subscription {SubscriptionId}", 
                evidence.Count, subscriptionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting Access Control evidence for subscription {SubscriptionId}", subscriptionId);
        }

        return evidence;
    }
}