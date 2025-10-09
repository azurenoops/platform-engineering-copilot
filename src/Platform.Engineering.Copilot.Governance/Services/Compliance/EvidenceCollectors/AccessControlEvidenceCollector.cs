using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Interfaces;
using System.Text.Json;

namespace Platform.Engineering.Copilot.Governance.Services;

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
        CancellationToken cancellationToken = default)
    {
        return new List<ComplianceEvidence>(); // Delegate to specific AC evidence
    }

    public async Task<List<ComplianceEvidence>> CollectLogEvidenceAsync(
        string subscriptionId, 
        string controlFamily, 
        CancellationToken cancellationToken = default)
    {
        return new List<ComplianceEvidence>(); // Delegate to specific AC evidence
    }

    public async Task<List<ComplianceEvidence>> CollectMetricEvidenceAsync(
        string subscriptionId, 
        string controlFamily, 
        CancellationToken cancellationToken = default)
    {
        return new List<ComplianceEvidence>(); // Delegate to specific AC evidence
    }

    public async Task<List<ComplianceEvidence>> CollectPolicyEvidenceAsync(
        string subscriptionId, 
        string controlFamily, 
        CancellationToken cancellationToken = default)
    {
        return new List<ComplianceEvidence>(); // Delegate to specific AC evidence
    }

    public async Task<List<ComplianceEvidence>> CollectAccessControlEvidenceAsync(
        string subscriptionId, 
        string controlFamily, 
        CancellationToken cancellationToken = default)
    {
        var evidence = new List<ComplianceEvidence>();

        try
        {
            _logger.LogDebug("Collecting Access Control evidence for subscription {SubscriptionId}", subscriptionId);

            // Get all resources for evidence collection
            var resources = await _azureService.ListAllResourcesAsync(subscriptionId);
            
            if (resources == null || resources.Count == 0)
            {
                _logger.LogWarning("No resources found for subscription {SubscriptionId}", subscriptionId);
                return evidence;
            }

            // Collect Network Security Group evidence (AC-3, AC-4)
            var nsgs = resources.Where(r => r.Type?.Equals("Microsoft.Network/networkSecurityGroups", StringComparison.OrdinalIgnoreCase) == true).ToList();
            if (nsgs.Any())
            {
                var nsgData = new Dictionary<string, object>
                {
                    ["totalNSGs"] = nsgs.Count,
                    ["nsgList"] = nsgs.Select(nsg => new
                    {
                        name = nsg.Name,
                        id = nsg.Id,
                        location = nsg.Location,
                        resourceGroup = nsg.ResourceGroup
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
                    Screenshot = JsonSerializer.Serialize(nsgData, new JsonSerializerOptions { WriteIndented = true })
                });
            }

            // Collect Key Vault evidence (AC-6 - Least Privilege)
            var keyVaults = resources.Where(r => r.Type?.Equals("Microsoft.KeyVault/vaults", StringComparison.OrdinalIgnoreCase) == true).ToList();
            if (keyVaults.Any())
            {
                var kvData = new Dictionary<string, object>
                {
                    ["totalKeyVaults"] = keyVaults.Count,
                    ["keyVaultList"] = keyVaults.Select(kv => new
                    {
                        name = kv.Name,
                        id = kv.Id,
                        location = kv.Location,
                        resourceGroup = kv.ResourceGroup
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
                    Screenshot = JsonSerializer.Serialize(kvData, new JsonSerializerOptions { WriteIndented = true })
                });
            }

            // Collect Log Analytics Workspace evidence (AC-7 - Logon monitoring)
            var logWorkspaces = resources.Where(r => r.Type?.Equals("Microsoft.OperationalInsights/workspaces", StringComparison.OrdinalIgnoreCase) == true).ToList();
            if (logWorkspaces.Any())
            {
                var logData = new Dictionary<string, object>
                {
                    ["totalWorkspaces"] = logWorkspaces.Count,
                    ["workspaceList"] = logWorkspaces.Select(ws => new
                    {
                        name = ws.Name,
                        id = ws.Id,
                        location = ws.Location,
                        resourceGroup = ws.ResourceGroup
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
                    Screenshot = JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = true })
                });
            }

            // Collect Virtual Machine evidence (AC-2 - Account Management)
            var vms = resources.Where(r => r.Type?.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true).ToList();
            if (vms.Any())
            {
                var vmData = new Dictionary<string, object>
                {
                    ["totalVMs"] = vms.Count,
                    ["vmList"] = vms.Select(vm => new
                    {
                        name = vm.Name,
                        id = vm.Id,
                        location = vm.Location,
                        resourceGroup = vm.ResourceGroup
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
                    Screenshot = JsonSerializer.Serialize(vmData, new JsonSerializerOptions { WriteIndented = true })
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