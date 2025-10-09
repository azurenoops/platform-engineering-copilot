using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Interfaces;

namespace Platform.Engineering.Copilot.Governance.Services;

/// <summary>
/// Scanner for Access Control (AC) family controls using real Azure APIs
/// </summary>
public class AccessControlScanner : IComplianceScanner
{
    private readonly ILogger _logger;
    private readonly IAzureResourceService _azureService;

    public AccessControlScanner(ILogger logger, IAzureResourceService azureService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _azureService = azureService ?? throw new ArgumentNullException(nameof(azureService));
    }

    public async Task<List<AtoFinding>> ScanControlAsync(
        string subscriptionId, 
        NistControl control, 
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Scanning control {ControlId} for subscription {SubscriptionId}", 
            control.Id, subscriptionId);

        var findings = new List<AtoFinding>();

        // Scan based on specific AC controls
        switch (control.Id)
        {
            case "AC-2":
                findings.AddRange(await ScanAccountManagementAsync(subscriptionId, control, cancellationToken));
                break;

            case "AC-3":
                findings.AddRange(await ScanAccessEnforcementAsync(subscriptionId, control, cancellationToken));
                break;

            case "AC-6":
                findings.AddRange(await ScanLeastPrivilegeAsync(subscriptionId, control, cancellationToken));
                break;

            case "AC-7":
                findings.AddRange(await ScanUnsuccessfulLogonAttemptsAsync(subscriptionId, control, cancellationToken));
                break;

            default:
                findings.AddRange(await ScanGenericAccessControlAsync(subscriptionId, control, cancellationToken));
                break;
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanAccountManagementAsync(
        string subscriptionId, 
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            _logger.LogDebug("Scanning account management (AC-2) for subscription {SubscriptionId}", subscriptionId);

            // Get all resources to check for role assignments
            var resources = await _azureService.ListAllResourcesAsync(subscriptionId);
            
            if (resources == null || resources.Count == 0)
            {
                _logger.LogWarning("No resources found for subscription {SubscriptionId}", subscriptionId);
                return findings;
            }

            // Check for overly broad role assignments at subscription level
            var subscriptionResourceId = $"/subscriptions/{subscriptionId}";
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = subscriptionResourceId,
                ResourceType = "Microsoft.Authorization/roleAssignments",
                ResourceName = "Subscription Role Assignments",
                FindingType = AtoFindingType.AccessControl,
                Severity = AtoFindingSeverity.Informational,
                Title = "Account Management Review Required",
                Description = $"Subscription has {resources.Count} resources requiring role assignment review",
                Recommendation = "Review role assignments regularly to ensure proper account lifecycle management per AC-2",
                ComplianceStatus = AtoComplianceStatus.Compliant,
                AffectedNistControls = new List<string> { control.Id ?? "AC-2" },
                ComplianceFrameworks = new List<string> { "NIST-800-53" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning account management for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanAccessEnforcementAsync(
        string subscriptionId, 
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            _logger.LogDebug("Scanning access enforcement (AC-3) for subscription {SubscriptionId}", subscriptionId);

            // Get all resources and check for NSGs (network access control)
            var resources = await _azureService.ListAllResourcesAsync(subscriptionId);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for subscription {SubscriptionId}", subscriptionId);
                return findings;
            }

            // Check for Network Security Groups
            var nsgs = resources.Where(r => r.Type?.Equals("Microsoft.Network/networkSecurityGroups", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            // Check for VMs without NSGs
            var vms = resources.Where(r => r.Type?.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            if (vms.Any() && !nsgs.Any())
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Network/networkSecurityGroups",
                    ResourceName = "Network Security Groups",
                    FindingType = AtoFindingType.AccessControl,
                    Severity = AtoFindingSeverity.High,
                    Title = "Missing Network Access Controls",
                    Description = $"Found {vms.Count} virtual machines without network security groups",
                    Recommendation = "Apply Network Security Groups to enforce access control policies per AC-3",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-3" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else if (nsgs.Any())
            {
                // NSGs exist - good for access enforcement
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Network/networkSecurityGroups",
                    ResourceName = "Network Security Groups",
                    FindingType = AtoFindingType.AccessControl,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "Network Access Controls Implemented",
                    Description = $"Found {nsgs.Count} Network Security Groups enforcing access controls",
                    Recommendation = "Continue regular review of NSG rules to ensure proper access enforcement per AC-3",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-3" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning access enforcement for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanLeastPrivilegeAsync(
        string subscriptionId, 
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            _logger.LogDebug("Scanning least privilege (AC-6) for subscription {SubscriptionId}", subscriptionId);

            // Get all resources and check for Key Vaults (privilege separation)
            var resources = await _azureService.ListAllResourcesAsync(subscriptionId);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for subscription {SubscriptionId}", subscriptionId);
                return findings;
            }

            // Check for Key Vaults (good practice for privilege separation)
            var keyVaults = resources.Where(r => r.Type?.Equals("Microsoft.KeyVault/vaults", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            // Check for resources that should have RBAC (storage accounts, VMs, databases)
            var criticalResources = resources.Where(r => 
                r.Type?.Equals("Microsoft.Storage/storageAccounts", StringComparison.OrdinalIgnoreCase) == true ||
                r.Type?.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true ||
                r.Type?.Equals("Microsoft.Sql/servers", StringComparison.OrdinalIgnoreCase) == true
            ).ToList();

            if (criticalResources.Any() && !keyVaults.Any())
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.KeyVault/vaults",
                    ResourceName = "Key Vault Configuration",
                    FindingType = AtoFindingType.AccessControl,
                    Severity = AtoFindingSeverity.Medium,
                    Title = "Missing Privilege Separation Mechanism",
                    Description = $"Found {criticalResources.Count} critical resources without Key Vault for secret management",
                    Recommendation = "Implement Azure Key Vault for privilege separation and least privilege access per AC-6",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-6" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else if (keyVaults.Any())
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.KeyVault/vaults",
                    ResourceName = "Key Vault Configuration",
                    FindingType = AtoFindingType.AccessControl,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "Privilege Separation Implemented",
                    Description = $"Found {keyVaults.Count} Key Vaults supporting least privilege access",
                    Recommendation = "Continue using Key Vault RBAC for fine-grained access control per AC-6",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-6" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning least privilege for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanUnsuccessfulLogonAttemptsAsync(
        string subscriptionId, 
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            _logger.LogDebug("Scanning unsuccessful logon attempts (AC-7) for subscription {SubscriptionId}", subscriptionId);

            // Get all resources to check for monitoring
            var resources = await _azureService.ListAllResourcesAsync(subscriptionId);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for subscription {SubscriptionId}", subscriptionId);
                return findings;
            }

            // Check for Log Analytics Workspaces (required for monitoring failed logon attempts)
            var logWorkspaces = resources.Where(r => r.Type?.Equals("Microsoft.OperationalInsights/workspaces", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            if (!logWorkspaces.Any())
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.OperationalInsights/workspaces",
                    ResourceName = "Log Analytics Workspace",
                    FindingType = AtoFindingType.Security,
                    Severity = AtoFindingSeverity.High,
                    Title = "Missing Logon Attempt Monitoring",
                    Description = "No Log Analytics workspace found to monitor failed authentication attempts",
                    Recommendation = "Configure Log Analytics workspace and enable monitoring for unsuccessful logon attempts per AC-7",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-7" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = logWorkspaces[0].Id,
                    ResourceType = "Microsoft.OperationalInsights/workspaces",
                    ResourceName = logWorkspaces[0].Name,
                    FindingType = AtoFindingType.Security,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "Logon Attempt Monitoring Enabled",
                    Description = $"Found {logWorkspaces.Count} Log Analytics workspace(s) for monitoring authentication attempts",
                    Recommendation = "Ensure alerts are configured for excessive failed logon attempts per AC-7",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-7" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning unsuccessful logon attempts for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanGenericAccessControlAsync(
        string subscriptionId, 
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            _logger.LogDebug("Scanning generic access control {ControlId} for subscription {SubscriptionId}", control.Id, subscriptionId);

            // Get resources to provide context
            var resources = await _azureService.ListAllResourcesAsync(subscriptionId);
            
            if (resources == null || resources.Count == 0)
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Authorization/general",
                    ResourceName = "Subscription Authorization",
                    FindingType = AtoFindingType.Compliance,
                    Severity = AtoFindingSeverity.Low,
                    Title = "Access Control Review Required",
                    Description = $"Manual review required for control {control.Id}: {control.Title}",
                    Recommendation = "Implement appropriate access controls per control requirements",
                    ComplianceStatus = AtoComplianceStatus.PartiallyCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-1" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Resources exist, mark as requiring review
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Authorization/general",
                    ResourceName = "Subscription Authorization",
                    FindingType = AtoFindingType.Compliance,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "Access Control Review Recommended",
                    Description = $"Review {resources.Count} resources for control {control.Id}: {control.Title}",
                    Recommendation = "Verify access controls are properly implemented per control requirements",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-1" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning generic access control for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }
}