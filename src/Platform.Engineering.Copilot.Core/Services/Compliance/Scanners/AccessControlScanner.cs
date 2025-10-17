using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Authorization;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Extensions;

namespace Platform.Engineering.Copilot.Core.Services.Compliance;

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
        return await ScanControlAsync(subscriptionId, null, control, cancellationToken);
    }

    /// <summary>
    /// Resource group-scoped scanning with optional RG parameter
    /// </summary>
    public async Task<List<AtoFinding>> ScanControlAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control,
        CancellationToken cancellationToken = default)
    {
        var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
        _logger.LogDebug("Scanning control {ControlId} for {Scope} in subscription {SubscriptionId}", 
            control.Id, scope, subscriptionId);

        var findings = new List<AtoFinding>();

        // CRITICAL: Control IDs from NIST catalog are lowercase (ac-2, ac-3, etc.)
        // Use case-insensitive comparison
        var controlId = control.Id?.ToUpperInvariant();

        // Scan based on specific AC controls
        switch (controlId)
        {
            case "AC-2":
                findings.AddRange(await ScanAccountManagementAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "AC-3":
                findings.AddRange(await ScanAccessEnforcementAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "AC-4":
                findings.AddRange(await ScanInformationFlowEnforcementAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "AC-5":
                findings.AddRange(await ScanSeparationOfDutiesAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "AC-6":
                findings.AddRange(await ScanLeastPrivilegeAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "AC-7":
                findings.AddRange(await ScanUnsuccessfulLogonAttemptsAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "AC-17":
                findings.AddRange(await ScanRemoteAccessAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "AC-20":
                findings.AddRange(await ScanUseOfExternalSystemsAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            default:
                findings.AddRange(await ScanGenericAccessControlAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;
        }

        // Enrich all findings with auto-remediation information
        return findings.WithAutoRemediationInfo();
    }

    private async Task<List<AtoFinding>> ScanAccountManagementAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scanScope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning account management (AC-2) for {ScanScope} in subscription {SubscriptionId}", scanScope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            if (resources == null || resources.Count == 0)
            {
                _logger.LogWarning("No resources found for {ScanScope} in subscription {SubscriptionId}", scanScope, subscriptionId);
                return findings;
            }

            // Determine scope resource ID early for use in all code paths
            string scopeResourceId = string.IsNullOrEmpty(resourceGroupName)
                ? $"/subscriptions/{subscriptionId}"
                : $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}";

            // PROACTIVE CHECK: Query actual role assignments using ARM client
            // When RG is specified, check RG-level role assignments; otherwise check subscription-level
            var armClient = _azureService.GetArmClient();
            if (armClient != null)
            {
                try
                {
                    // Known Azure built-in role definition IDs
                    var ownerRoleId = "8e3af657-a8ff-443c-a75c-2fe8c4bcb635";
                    var contributorRoleId = "b24988ac-6180-42a0-ab88-20f7382dd24c";
                    var userAccessAdminRoleId = "18d7d88d-d35e-4fb5-a5c3-7773c20a72d9";
                    
                    int ownerCount = 0;
                    int contributorCount = 0;
                    int userAccessAdminCount = 0;
                    int totalRoleAssignments = 0;
                    var broadAssignmentDetails = new List<string>();
                    
                    ArmResource scopeResource;
                    
                    if (string.IsNullOrEmpty(resourceGroupName))
                    {
                        // Subscription-level scan
                        scopeResource = armClient.GetSubscriptionResource(new ResourceIdentifier(scopeResourceId));
                    }
                    else
                    {
                        // Resource group-level scan
                        scopeResource = armClient.GetResourceGroupResource(new ResourceIdentifier(scopeResourceId));
                    }
                    
                    // Get role assignment collection from the scope resource (subscription or RG)
                    var roleAssignmentCollection = scopeResource.GetRoleAssignments();
                    
                    // Get all role assignments at the specified scope
                    await foreach (var roleAssignment in roleAssignmentCollection.GetAllAsync(cancellationToken: cancellationToken))
                    {
                        totalRoleAssignments++;
                        var roleDefinitionId = roleAssignment.Data.RoleDefinitionId.ToString();
                        
                        // Check if this assignment is scoped to our target scope
                        var assignmentScope = roleAssignment.Data.Scope;
                        var isScopedToTarget = assignmentScope.Equals(scopeResourceId, StringComparison.OrdinalIgnoreCase);
                        
                        if (isScopedToTarget)
                        {
                            if (roleDefinitionId.Contains(ownerRoleId, StringComparison.OrdinalIgnoreCase))
                            {
                                ownerCount++;
                                broadAssignmentDetails.Add($"Owner role assigned to principal {roleAssignment.Data.PrincipalId}");
                            }
                            else if (roleDefinitionId.Contains(contributorRoleId, StringComparison.OrdinalIgnoreCase))
                            {
                                contributorCount++;
                                broadAssignmentDetails.Add($"Contributor role assigned to principal {roleAssignment.Data.PrincipalId}");
                            }
                            else if (roleDefinitionId.Contains(userAccessAdminRoleId, StringComparison.OrdinalIgnoreCase))
                            {
                                userAccessAdminCount++;
                                broadAssignmentDetails.Add($"User Access Administrator role assigned to principal {roleAssignment.Data.PrincipalId}");
                            }
                        }
                    }
                    
                    _logger.LogInformation("Found {TotalRoleAssignments} role assignments at {ScanScope}: {OwnerCount} Owners, {ContributorCount} Contributors, {UserAccessAdminCount} User Access Admins", 
                        totalRoleAssignments, scanScope, ownerCount, contributorCount, userAccessAdminCount);
                    
                    // CRITICAL: Overly broad role assignments detected
                    if (ownerCount > 0 || contributorCount > 0 || userAccessAdminCount > 0)
                    {
                        var totalBroad = ownerCount + contributorCount + userAccessAdminCount;
                        var scopeDescription = string.IsNullOrEmpty(resourceGroupName) 
                            ? "subscription level" 
                            : $"resource group '{resourceGroupName}' level";
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = scopeResourceId,
                            ResourceType = "Microsoft.Authorization/roleAssignments",
                            ResourceName = string.IsNullOrEmpty(resourceGroupName) ? "Subscription Role Assignments" : $"Resource Group '{resourceGroupName}' Role Assignments",
                            FindingType = AtoFindingType.AccessControl,
                            Severity = AtoFindingSeverity.High,
                            Title = $"Overly Broad Role Assignments Detected at {scopeDescription}",
                            Description = $"Found {totalBroad} high-privilege role assignments at {scopeDescription}: {ownerCount} Owner(s), {contributorCount} Contributor(s), {userAccessAdminCount} User Access Administrator(s). Total role assignments: {totalRoleAssignments}. Resources in scope: {resources.Count}. Broad permissions violate least privilege principles and increase risk of unauthorized access. DoD ATO compliance requires restrictive, resource-specific role assignments with regular access reviews.",
                            Recommendation = $@"IMMEDIATE ACTIONS REQUIRED per AC-2:
1. Review all subscription-level role assignments in Azure Portal → Subscriptions → Access Control (IAM)
2. Identify and remediate broad role assignments:
   - OWNER roles ({ownerCount} found): Full access including ability to assign roles to others (HIGHEST RISK)
     * Can create, modify, delete all resources
     * Can grant access to other users
     * Can modify security settings
   - CONTRIBUTOR roles ({contributorCount} found): Full resource management without role assignment capability (HIGH RISK)
     * Can create, modify, delete all resources
     * Cannot grant access to other users
   - USER ACCESS ADMINISTRATOR roles ({userAccessAdminCount} found): Can manage user access to Azure resources (HIGH RISK)
     * Can assign roles to users/groups/service principals
3. Replace subscription-level roles with RESOURCE-SPECIFIC assignments:
   - Assign roles at Resource Group level (e.g., Contributor for 'rg-app-prod')
   - Assign roles at Resource level (e.g., 'Virtual Machine Contributor' for specific VMs)
   - Use built-in fine-grained roles: 'Storage Blob Data Contributor', 'SQL DB Contributor', etc.
4. Create CUSTOM RBAC roles with minimal required permissions:
   - Azure Portal → Subscriptions → Access Control (IAM) → Roles → Add → Add custom role
   - Define only actions/dataActions needed for specific job function
   - Document custom role justification in SSP
5. Implement Azure AD Privileged Identity Management (PIM):
   - Enable time-limited (just-in-time) access for privileged roles
   - Require approval workflow for role activation
   - Set maximum activation duration (recommended: 8 hours)
   - Enable multi-factor authentication for activation
6. Configure Azure AD Access Reviews (REQUIRED for DoD):
   - Quarterly reviews for all privileged roles
   - Manager approval required for Owner/Contributor renewals
   - Automatic removal if review not completed within 30 days
7. Enable MFA for all users with privileged roles (MANDATORY for DoD)
8. Configure Azure Monitor alerts for role assignment changes:
   - Activity Log → Alerts → Create → 'Microsoft.Authorization/roleAssignments/write'
   - Send alerts to security team for investigation
9. Use Managed Identities instead of service principals:
   - Eliminates need for service principal credentials
   - Automatic credential rotation
   - Better audit trail
10. Document in System Security Plan (SSP):
    - List all privileged role assignments with justification
    - Approval authority and review schedule
    - Incident response procedures for unauthorized access

REFERENCES:
- NIST 800-53 AC-2: Account Management
- NIST 800-53 AC-6: Least Privilege  
- NIST 800-53 AC-2(7): Privileged User Accounts
- DoD Cloud Computing SRG: Access Control Requirements
- Azure Well-Architected Framework: Identity and Access Management
- CIS Azure Foundations Benchmark: Identity and Access Management",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-2", "AC-6", "AC-2(7)", "AC-6(1)" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC", "CIS" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    else if (totalRoleAssignments > 0)
                    {
                        // Role assignments exist but none are overly broad - good!
                        var scopeDescription = string.IsNullOrEmpty(resourceGroupName) 
                            ? "subscription level" 
                            : $"resource group '{resourceGroupName}' level";
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = scopeResourceId,
                            ResourceType = "Microsoft.Authorization/roleAssignments",
                            ResourceName = string.IsNullOrEmpty(resourceGroupName) ? "Subscription Role Assignments" : $"Resource Group '{resourceGroupName}' Role Assignments",
                            FindingType = AtoFindingType.AccessControl,
                            Severity = AtoFindingSeverity.Informational,
                            Title = $"Least Privilege Role Assignments Implemented at {scopeDescription}",
                            Description = $"Found {totalRoleAssignments} role assignments with NO overly broad Owner or Contributor roles at {scopeDescription}. Role assignments appear to follow least privilege principles with resource-specific or fine-grained permissions.",
                            Recommendation = @"MAINTAIN CURRENT POSTURE per AC-2:
1. Continue quarterly access reviews using Azure AD Access Reviews
2. Document all role assignments in System Security Plan (SSP)
3. Maintain audit logs of role assignment changes (retain for minimum 1 year per DoD)
4. Verify Azure AD Privileged Identity Management (PIM) is enabled for any remaining privileged roles
5. Ensure all account creation/modification/deletion events are logged to SIEM
6. Review role assignments quarterly to identify unused permissions
7. Configure Azure Monitor alerts for new role assignment creation
8. Periodically audit for custom roles that may have excessive permissions
9. Verify MFA is enabled for all users with any privileged access
10. Test account lifecycle management procedures annually

REFERENCES:
- NIST 800-53 AC-2: Account Management
- NIST 800-53 AC-6: Least Privilege
- Azure Well-Architected Framework: Identity and Access Management
- Continue monitoring with Microsoft Defender for Cloud",
                            ComplianceStatus = AtoComplianceStatus.Compliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-2", "AC-6" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        // No role assignments at all - flag as issue
                        var scopeDescription = string.IsNullOrEmpty(resourceGroupName) 
                            ? "subscription" 
                            : $"resource group '{resourceGroupName}'";
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = scopeResourceId,
                            ResourceType = "Microsoft.Authorization/roleAssignments",
                            ResourceName = string.IsNullOrEmpty(resourceGroupName) ? "Subscription Role Assignments" : $"Resource Group '{resourceGroupName}' Role Assignments",
                            FindingType = AtoFindingType.AccessControl,
                            Severity = AtoFindingSeverity.Medium,
                            Title = $"No Role Assignments Found at {scopeDescription} - Verify Access Controls",
                            Description = $"No role assignments found at {scopeDescription} with {resources.Count} resources. This may indicate a misconfiguration or that access is managed through inherited permissions.",
                            Recommendation = @"VERIFICATION REQUIRED per AC-2:
1. Verify subscription is properly configured in Azure AD tenant
2. Check for inherited role assignments from Management Group level
3. Ensure proper RBAC is configured for all resources
4. Document access control strategy in System Security Plan (SSP)
5. If using Management Group inheritance, document this architecture decision

REFERENCES:
- NIST 800-53 AC-2: Account Management
- Azure RBAC documentation",
                            ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-2" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception roleEx)
                {
                    _logger.LogWarning(roleEx, "Unable to query role assignments for {ScanScope} in subscription {SubscriptionId}", scanScope, subscriptionId);
                    
                    // Fallback if ARM client query fails
                    var scopeDescription = string.IsNullOrEmpty(resourceGroupName) 
                        ? "subscription" 
                        : $"resource group '{resourceGroupName}'";
                    
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = scopeResourceId,
                        ResourceType = "Microsoft.Authorization/roleAssignments",
                        ResourceName = string.IsNullOrEmpty(resourceGroupName) ? "Subscription Role Assignments" : $"Resource Group '{resourceGroupName}' Role Assignments",
                        FindingType = AtoFindingType.AccessControl,
                        Severity = AtoFindingSeverity.Medium,
                        Title = $"Unable to Query Role Assignments at {scopeDescription} - Manual Review Required",
                        Description = $"Unable to automatically scan role assignments at {scopeDescription} (Error: {roleEx.Message}). Manual review required for {resources.Count} resources.",
                        Recommendation = @"MANUAL REVIEW REQUIRED per AC-2:
1. Navigate to Azure Portal → Subscriptions → Access Control (IAM)
2. Review all role assignments, particularly Owner and Contributor roles
3. Verify assignments follow least privilege principles
4. Document findings in ATO compliance assessment
5. Ensure service principal has 'Reader' permission to query role assignments

REFERENCES:
- NIST 800-53 AC-2: Account Management",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "AC-2" },
                        ComplianceFrameworks = new List<string> { "NIST-800-53" },
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // No ARM client available
                _logger.LogWarning("ARM client not available for {ScanScope} in subscription {SubscriptionId}", scanScope, subscriptionId);
                
                var scopeDescription = string.IsNullOrEmpty(resourceGroupName) 
                    ? "subscription" 
                    : $"resource group '{resourceGroupName}'";
                
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = scopeResourceId,
                    ResourceType = "Microsoft.Authorization/roleAssignments",
                    ResourceName = string.IsNullOrEmpty(resourceGroupName) ? "Subscription Role Assignments" : $"Resource Group '{resourceGroupName}' Role Assignments",
                    FindingType = AtoFindingType.AccessControl,
                    Severity = AtoFindingSeverity.Medium,
                    Title = $"Unable to Query Role Assignments at {scopeDescription} - ARM Client Unavailable",
                    Description = $"ARM client not available to scan role assignments at {scopeDescription}. Manual review required for {resources.Count} resources.",
                    Recommendation = @"MANUAL REVIEW REQUIRED per AC-2:
1. Navigate to Azure Portal → Subscriptions → Access Control (IAM)
2. Review all role assignments, particularly Owner and Contributor roles
3. Verify assignments follow least privilege principles
4. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-2: Account Management",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-2" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning account management for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanAccessEnforcementAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning access enforcement (AC-3) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);
                return findings;
            }

            var vms = resources.Where(r => r.Type?.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            if (!vms.Any())
            {
                // No VMs - informational
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Network/networkSecurityGroups",
                    ResourceName = "Network Security Groups",
                    FindingType = AtoFindingType.AccessControl,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "No Virtual Machines Requiring Network Access Controls",
                    Description = "No virtual machines found requiring network security group validation.",
                    Recommendation = "When deploying VMs, ensure NSGs are configured per AC-3 requirements before resource deployment.",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-3" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // PROACTIVE CHECK: Query each VM's network interfaces and check for NSGs
            var armClient = _azureService.GetArmClient();
            if (armClient != null)
            {
                try
                {
                    var vmsWithoutNsgs = new List<string>();
                    var vmsWithNics = 0;
                    var totalVms = vms.Count;
                    
                    foreach (var vm in vms)
                    {
                        try
                        {
                            var vmResource = armClient.GetGenericResource(new ResourceIdentifier(vm.Id));
                            var vmData = await vmResource.GetAsync(cancellationToken: cancellationToken);
                            
                            // Parse the properties JSON
                            var propertiesJson = JsonDocument.Parse(vmData.Value.Data.Properties.ToStream());
                            var properties = propertiesJson.RootElement;
                            
                            // Get network interfaces from VM properties
                            if (properties.TryGetProperty("networkProfile", out var networkProfile))
                            {
                                if (networkProfile.TryGetProperty("networkInterfaces", out var nicsArray))
                                {
                                    bool hasNsgProtection = false;
                                    
                                    foreach (var nicElement in nicsArray.EnumerateArray())
                                    {
                                        if (nicElement.TryGetProperty("id", out var nicIdProperty))
                                        {
                                            var nicId = nicIdProperty.GetString();
                                            if (!string.IsNullOrEmpty(nicId))
                                            {
                                                vmsWithNics++;
                                                
                                                // Get the NIC resource to check for NSG
                                                var nicResource = armClient.GetGenericResource(new ResourceIdentifier(nicId));
                                                var nicData = await nicResource.GetAsync(cancellationToken: cancellationToken);
                                                
                                                // Parse NIC properties JSON
                                                var nicPropsJson = JsonDocument.Parse(nicData.Value.Data.Properties.ToStream());
                                                var nicProps = nicPropsJson.RootElement;
                                                
                                                // Check if NIC has NSG attached
                                                if (nicProps.TryGetProperty("networkSecurityGroup", out var nsgProperty))
                                                {
                                                    hasNsgProtection = true;
                                                    break;
                                                }
                                                
                                                // Check if NIC's subnet has NSG
                                                if (nicProps.TryGetProperty("ipConfigurations", out var ipConfigs))
                                                {
                                                    foreach (var ipConfig in ipConfigs.EnumerateArray())
                                                    {
                                                        if (ipConfig.TryGetProperty("properties", out var ipConfigProps))
                                                        {
                                                            if (ipConfigProps.TryGetProperty("subnet", out var subnet))
                                                            {
                                                                if (subnet.TryGetProperty("id", out var subnetIdProperty))
                                                                {
                                                                    var subnetId = subnetIdProperty.GetString();
                                                                    if (!string.IsNullOrEmpty(subnetId))
                                                                    {
                                                                        // Get subnet resource to check for NSG
                                                                        var subnetResource = armClient.GetGenericResource(new ResourceIdentifier(subnetId));
                                                                        var subnetData = await subnetResource.GetAsync(cancellationToken: cancellationToken);
                                                                        
                                                                        // Parse subnet properties JSON
                                                                        var subnetPropsJson = JsonDocument.Parse(subnetData.Value.Data.Properties.ToStream());
                                                                        var subnetProps = subnetPropsJson.RootElement;
                                                                        
                                                                        if (subnetProps.TryGetProperty("networkSecurityGroup", out var subnetNsg))
                                                                        {
                                                                            hasNsgProtection = true;
                                                                            break;
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                    
                                                    if (hasNsgProtection) break;
                                                }
                                            }
                                        }
                                    }
                                    
                                    if (!hasNsgProtection)
                                    {
                                        vmsWithoutNsgs.Add(vm.Name ?? vm.Id);
                                    }
                                }
                            }
                        }
                        catch (Exception vmEx)
                        {
                            _logger.LogWarning(vmEx, "Unable to query network configuration for VM {VmId}", vm.Id);
                            // If we can't query, assume it's unprotected to be safe
                            vmsWithoutNsgs.Add(vm.Name ?? vm.Id);
                        }
                    }
                    
                    _logger.LogInformation("Scanned {TotalVms} VMs: {VmsWithNics} have NICs, {VmsWithoutNsgs} without NSG protection", 
                        totalVms, vmsWithNics, vmsWithoutNsgs.Count);
                    
                    // CRITICAL: VMs exist without NSG protection
                    if (vmsWithoutNsgs.Any())
                    
                    // CRITICAL: VMs exist without NSG protection
                    if (vmsWithoutNsgs.Any())
                    {
                        var vmList = string.Join(", ", vmsWithoutNsgs.Take(10));
                        var remainingCount = vmsWithoutNsgs.Count > 10 ? $" (+{vmsWithoutNsgs.Count - 10} more)" : "";
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Network/networkSecurityGroups",
                            ResourceName = "Network Security Groups",
                            FindingType = AtoFindingType.AccessControl,
                            Severity = AtoFindingSeverity.Critical,
                            Title = "CRITICAL: Virtual Machines Without Network Security Groups",
                            Description = $"Found {vmsWithoutNsgs.Count} of {totalVms} virtual machines without Network Security Group protection (neither at NIC nor subnet level). VMs without NSGs: {vmList}{remainingCount}. This is a CRITICAL DoD ATO blocker. VMs are exposed without network-level access control enforcement, violating AC-3 and SC-7 requirements.",
                            Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per AC-3:
1. STOP all VM deployments until NSGs are implemented
2. For each unprotected VM, create or attach NSGs:
   - OPTION A: Attach NSG to Network Interface (NIC-level protection):
     * Azure Portal → Virtual Machines → [VM Name] → Networking → Network Interface
     * Click NIC name → Network security group → Associate existing or create new
   - OPTION B: Attach NSG to Subnet (subnet-level protection - RECOMMENDED):
     * Azure Portal → Virtual Networks → [VNet Name] → Subnets → [Subnet Name]
     * Network security group → Associate existing or create new NSG
3. Configure NSG rules following DoD STIG requirements:
   - Deny all inbound traffic from Internet by default (priority 4096)
   - Allow only specific required ports from known IP ranges
   - For Windows VMs: RDP (3389) only from jump box/bastion subnet
   - For Linux VMs: SSH (22) only from jump box/bastion subnet
   - Document allowed traffic in System Security Plan (SSP)
4. Enable NSG flow logs for all NSGs (REQUIRED for DoD):
   - Azure Portal → Network Security Groups → [NSG Name] → Flow logs
   - Configure flow logs with 90-day retention to Log Analytics workspace
   - Enable Traffic Analytics for network behavior analysis
5. Implement defense-in-depth approach:
   - Use Azure Bastion for secure RDP/SSH access (eliminates public IP exposure)
   - Deploy Azure Firewall for centralized egress control
   - Implement network micro-segmentation between tiers
6. Enable Microsoft Defender for Cloud to detect NSG misconfigurations
7. Document network access requirements for each VM in SSP

REFERENCES:
- NIST 800-53 AC-3: Access Enforcement
- NIST 800-53 SC-7: Boundary Protection
- NIST 800-53 SC-7(5): Deny by Default / Allow by Exception
- DoD Cloud Computing SRG Impact Level 5: Network Access Control
- Azure Security Benchmark: Network Security controls",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-3", "SC-7", "SC-7(5)", "SC-7(8)" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC", "Azure Security Benchmark" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        // All VMs have NSG protection - compliant
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
                            Description = $"All {totalVms} virtual machines have Network Security Group protection (either at NIC or subnet level). Network-level access enforcement is in place per AC-3.",
                            Recommendation = @"MAINTAIN CURRENT POSTURE per AC-3:
1. Enable NSG flow logs if not already enabled:
   - Azure Portal → Network Security Groups → [NSG Name] → Flow logs
   - Configure with 90-day retention to Log Analytics (minimum for DoD)
2. Configure Traffic Analytics for network behavior analysis
3. Set up Azure Monitor alerts for NSG rule modifications:
   - Activity Log → Alerts → Create alert for 'Microsoft.Network/networkSecurityGroups/securityRules/write'
4. Review NSG rules quarterly to remove unused/overly permissive rules:
   - Audit rules with source 'Internet' or '*' (any source)
   - Verify deny-by-default rules are in place (priority 4096)
   - Document business justification for each allow rule
5. Document NSG rule justifications in System Security Plan (SSP)
6. Implement Azure Policy to enforce NSG requirements:
   - Policy: 'Network interfaces should have a network security group'
   - Policy: 'Subnets should be associated with a Network Security Group'
7. Use Azure Bastion for secure VM access (eliminates public IP exposure)
8. Implement Azure Firewall for centralized egress control across VNets
9. Enable Microsoft Defender for Cloud recommendations for NSG hardening
10. Test NSG rules regularly to ensure they enforce intended access controls

REFERENCES:
- NIST 800-53 AC-3: Access Enforcement
- NIST 800-53 SC-7: Boundary Protection
- Continue monitoring with Microsoft Defender for Cloud",
                            ComplianceStatus = AtoComplianceStatus.Compliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-3", "SC-7" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception armEx)
                {
                    _logger.LogWarning(armEx, "Unable to query VM network configurations for subscription {SubscriptionId}", subscriptionId);
                    
                    // Fallback if ARM client query fails
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceType = "Microsoft.Network/networkSecurityGroups",
                        ResourceName = "Network Security Groups",
                        FindingType = AtoFindingType.AccessControl,
                        Severity = AtoFindingSeverity.High,
                        Title = "Unable to Query VM Network Security Configuration - Manual Review Required",
                        Description = $"Unable to automatically scan NSG protection for {vms.Count} virtual machines (Error: {armEx.Message}). Manual review required.",
                        Recommendation = @"MANUAL REVIEW REQUIRED per AC-3:
1. For each virtual machine, verify NSG protection:
   - Azure Portal → Virtual Machines → [VM Name] → Networking
   - Check 'Network security group' section for NIC-level NSG
   - Check 'Subnet' section for subnet-level NSG
2. Ensure at least one NSG is attached (NIC or subnet level)
3. Verify NSG rules follow deny-by-default principles
4. Document findings in ATO compliance assessment
5. Ensure service principal has 'Reader' permission to query VM network configurations

REFERENCES:
- NIST 800-53 AC-3: Access Enforcement
- NIST 800-53 SC-7: Boundary Protection",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "AC-3", "SC-7" },
                        ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // No ARM client available
                _logger.LogWarning("ARM client not available for subscription {SubscriptionId}", subscriptionId);
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Network/networkSecurityGroups",
                    ResourceName = "Network Security Groups",
                    FindingType = AtoFindingType.AccessControl,
                    Severity = AtoFindingSeverity.High,
                    Title = "Unable to Query VM Network Security Configuration - ARM Client Unavailable",
                    Description = $"ARM client not available to scan NSG protection for {vms.Count} virtual machines. Manual review required.",
                    Recommendation = @"MANUAL REVIEW REQUIRED per AC-3:
1. For each virtual machine, verify NSG protection:
   - Azure Portal → Virtual Machines → [VM Name] → Networking
   - Check for Network Security Group at NIC or subnet level
2. Ensure NSG rules follow deny-by-default principles
3. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-3: Access Enforcement",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
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
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning least privilege (AC-6) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);
                return findings;
            }

            // PROACTIVE CHECK: Resources that SHOULD use managed identities
            var criticalResources = resources.Where(r => 
                r.Type?.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true ||
                r.Type?.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) == true ||
                r.Type?.Equals("Microsoft.ContainerService/managedClusters", StringComparison.OrdinalIgnoreCase) == true ||
                r.Type?.Equals("Microsoft.Logic/workflows", StringComparison.OrdinalIgnoreCase) == true
            ).ToList();
            
            if (!criticalResources.Any())
            {
                // No critical resources requiring managed identities - informational
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.ManagedIdentity",
                    ResourceName = "Managed Identity Configuration",
                    FindingType = AtoFindingType.AccessControl,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "No Resources Requiring Managed Identity Configuration",
                    Description = "No critical resources found requiring managed identity validation (VMs, App Services, AKS, Logic Apps).",
                    Recommendation = "When deploying critical resources, implement Managed Identities per AC-6 requirements to eliminate credential management.",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-6" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // PROACTIVE CHECK: Query each critical resource to check for managed identity
            var armClient = _azureService.GetArmClient();
            if (armClient != null)
            {
                try
                {
                    var resourcesWithoutManagedIdentity = new List<string>();
                    var totalCriticalResources = criticalResources.Count;
                    var resourcesChecked = 0;
                    
                    foreach (var resource in criticalResources)
                    {
                        try
                        {
                            resourcesChecked++;
                            var genericResource = armClient.GetGenericResource(new ResourceIdentifier(resource.Id));
                            var resourceData = await genericResource.GetAsync(cancellationToken: cancellationToken);
                            
                            // Parse the resource properties JSON
                            var propertiesJson = JsonDocument.Parse(resourceData.Value.Data.Properties.ToStream());
                            var properties = propertiesJson.RootElement;
                            
                            bool hasManagedIdentity = false;
                            
                            // Check for identity property (system-assigned or user-assigned)
                            if (resourceData.Value.Data.Identity != null)
                            {
                                var identity = resourceData.Value.Data.Identity;
                                
                                // Check identity type - SystemAssigned or UserAssigned
                                var identityType = identity.ManagedServiceIdentityType.ToString();
                                if (identityType.Contains("SystemAssigned", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasManagedIdentity = true;
                                    _logger.LogDebug("Resource {ResourceId} has System-Assigned Managed Identity", resource.Id);
                                }
                                // Check for User-Assigned Identities
                                if (identity.UserAssignedIdentities != null && identity.UserAssignedIdentities.Any())
                                {
                                    hasManagedIdentity = true;
                                    _logger.LogDebug("Resource {ResourceId} has {Count} User-Assigned Managed Identities", 
                                        resource.Id, identity.UserAssignedIdentities.Count);
                                }
                            }
                            
                            if (!hasManagedIdentity)
                            {
                                resourcesWithoutManagedIdentity.Add($"{resource.Name} ({resource.Type})");
                                _logger.LogInformation("Resource {ResourceId} does NOT have Managed Identity enabled", resource.Id);
                            }
                        }
                        catch (Exception resourceEx)
                        {
                            _logger.LogWarning(resourceEx, "Unable to query managed identity for resource {ResourceId}", resource.Id);
                            // If we can't query, assume it doesn't have managed identity to be safe
                            resourcesWithoutManagedIdentity.Add($"{resource.Name ?? resource.Id} ({resource.Type})");
                        }
                    }
                    
                    _logger.LogInformation("Scanned {TotalResources} critical resources: {ResourcesWithoutMI} without Managed Identity", 
                        totalCriticalResources, resourcesWithoutManagedIdentity.Count);
                    
                    // CRITICAL: Critical resources without Managed Identity
                    if (resourcesWithoutManagedIdentity.Any())
                    {
                        var resourceList = string.Join(", ", resourcesWithoutManagedIdentity.Take(10));
                        var remainingCount = resourcesWithoutManagedIdentity.Count > 10 ? $" (+{resourcesWithoutManagedIdentity.Count - 10} more)" : "";
                        var resourceTypes = criticalResources.Select(r => r.Type).Distinct().ToList();
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.ManagedIdentity",
                            ResourceName = "Managed Identity Configuration",
                            FindingType = AtoFindingType.AccessControl,
                            Severity = AtoFindingSeverity.High,
                            Title = "Critical Resources Without Managed Identity",
                            Description = $"Found {resourcesWithoutManagedIdentity.Count} of {totalCriticalResources} critical resources without Managed Identity enabled. Resources without Managed Identity: {resourceList}{remainingCount}. These resources may be using hardcoded credentials, service principal secrets, or access keys, violating AC-6 least privilege principles. Managed identities eliminate credential management and provide automatic credential rotation.",
                            Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per AC-6:
1. Enable Managed Identity on each affected resource:
   
   FOR VIRTUAL MACHINES:
   - Azure Portal → Virtual Machines → [VM Name] → Identity
   - System assigned → Status: ON → Save
   - Managed identity is now created with same lifecycle as VM
   
   FOR APP SERVICES / FUNCTION APPS:
   - Azure Portal → App Services → [App Name] → Identity
   - System assigned → Status: ON → Save
   - Update application code to use DefaultAzureCredential
   
   FOR AKS CLUSTERS:
   - Azure Portal → Kubernetes services → [Cluster Name] → Properties
   - Enable managed identity (requires cluster recreation if not initially enabled)
   - Use workload identity for pod-level authentication
   
   FOR LOGIC APPS:
   - Azure Portal → Logic Apps → [Logic App Name] → Identity
   - System assigned → Status: ON → Save
   - Update connectors to use managed identity authentication

2. Grant Managed Identity RBAC permissions to required resources:
   - Target Resource → Access Control (IAM) → Add role assignment
   - Select appropriate role (e.g., 'Storage Blob Data Contributor', 'Key Vault Secrets User')
   - Assign access to: Managed Identity
   - Select the resource's system-assigned identity
   - Principle of least privilege: Grant minimal permissions required

3. Update application code to use Managed Identity:
   - .NET: Use DefaultAzureCredential from Azure.Identity SDK
   - Python: Use DefaultAzureCredential from azure-identity package
   - Node.js: Use DefaultAzureCredential from @azure/identity
   - Code example:
     var credential = new DefaultAzureCredential();
     var client = new BlobServiceClient(new Uri(blobUri), credential);

4. Remove hardcoded credentials and service principals:
   - Delete connection strings from application configuration
   - Remove service principal credentials from Key Vault (if only used by these resources)
   - Delete access keys from configuration files
   - Rotate any exposed credentials immediately

5. For resources that need Key Vault access:
   - Grant managed identity 'Key Vault Secrets User' role on Key Vault
   - Update code to retrieve secrets using managed identity
   - Remove Key Vault access policies (use RBAC instead)

6. Document managed identity usage in System Security Plan (SSP):
   - List all resources with managed identities
   - Document RBAC permissions granted to each identity
   - Justify permissions based on application requirements
   - Include architecture diagram showing identity flow

7. Implement monitoring and alerts:
   - Enable Azure Monitor for identity authentication events
   - Configure alerts for failed authentication attempts
   - Review managed identity permissions quarterly

BENEFITS OF MANAGED IDENTITIES:
- No credential management or rotation required
- Automatic credential lifecycle management
- Credentials never exposed in code, config, or Key Vault
- Reduced attack surface (no secrets to steal)
- Better audit trail via Azure AD sign-in logs
- Compliance with NIST 800-53 AC-6 and IA-5(7)

REFERENCES:
- NIST 800-53 AC-6: Least Privilege
- NIST 800-53 IA-5(7): No Embedded Unencrypted Static Authenticators
- DoD Cloud Computing SRG: Credential Management Requirements
- Azure Security Benchmark: IM-2 Use managed identities to eliminate authentication credentials
- Azure Well-Architected Framework: Identity and Access Management",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-6", "IA-5", "IA-5(7)", "SC-28" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC", "Azure Security Benchmark" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        // All critical resources have managed identities - compliant
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.ManagedIdentity",
                            ResourceName = "Managed Identity Configuration",
                            FindingType = AtoFindingType.AccessControl,
                            Severity = AtoFindingSeverity.Informational,
                            Title = "Managed Identities Implemented for Least Privilege",
                            Description = $"All {totalCriticalResources} critical resources have Managed Identity enabled (System-Assigned or User-Assigned). This eliminates credential management and follows DoD best practices for secure authentication.",
                            Recommendation = @"MAINTAIN CURRENT POSTURE per AC-6:
1. Review managed identity RBAC permissions quarterly:
   - Azure Portal → Azure AD → Managed identities
   - Select each identity → Azure role assignments
   - Verify permissions follow least privilege (minimal required access)
   - Remove unused or overly permissive role assignments

2. Monitor managed identity usage:
   - Enable Azure AD sign-in logs for managed identities
   - Configure Log Analytics workspace to collect identity events
   - Set retention to 90 days minimum (DoD requirement)
   - Review authentication patterns for anomalies

3. Configure alerts for identity issues:
   - Failed authentication attempts from managed identities
   - New role assignments to managed identities
   - Managed identity deletion or modification
   - Send alerts to security team for investigation

4. Implement Azure Policy for enforcement:
   - Policy: 'Virtual machines should use managed identity'
   - Policy: 'App Service apps should use managed identity'
   - Policy: 'Function apps should use managed identity'
   - Set to 'Deny' effect to prevent non-compliant deployments

5. Document managed identity architecture in SSP:
   - List all resources with managed identities
   - Document RBAC permissions for each identity
   - Include architecture diagram showing authentication flows
   - Justify all permissions based on application requirements

6. Use User-Assigned Identities for shared scenarios:
   - Multiple resources accessing same backend services
   - Consistent identity across environment lifecycle
   - Simplified RBAC management for common patterns

7. Avoid service principals where possible:
   - Prefer managed identities for Azure resource authentication
   - Use service principals only for non-Azure services
   - Implement credential rotation for any remaining service principals

8. Test application resilience:
   - Verify applications handle Azure AD token refresh
   - Test behavior during identity permission changes
   - Validate retry logic for transient authentication failures

9. Review Key Vault access:
   - Verify managed identities use RBAC (not access policies)
   - Grant 'Key Vault Secrets User' role (READ ONLY)
   - Restrict 'Key Vault Secrets Officer' to security admins

10. Plan for disaster recovery:
    - Document managed identity dependencies
    - Include identity recreation in DR runbooks
    - Test identity restoration procedures annually

REFERENCES:
- NIST 800-53 AC-6: Least Privilege
- Azure Security Benchmark: IM-2 Use managed identities
- Azure Well-Architected Framework: Identity and Access Management
- Continue monitoring with Microsoft Defender for Cloud",
                            ComplianceStatus = AtoComplianceStatus.Compliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-6", "IA-5" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "Azure Security Benchmark" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception armEx)
                {
                    _logger.LogWarning(armEx, "Unable to query managed identity configurations for subscription {SubscriptionId}", subscriptionId);
                    
                    // Fallback if ARM client query fails
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceType = "Microsoft.ManagedIdentity",
                        ResourceName = "Managed Identity Configuration",
                        FindingType = AtoFindingType.AccessControl,
                        Severity = AtoFindingSeverity.Medium,
                        Title = "Unable to Query Managed Identity Configuration - Manual Review Required",
                        Description = $"Unable to automatically scan managed identity configuration for {criticalResources.Count} critical resources (Error: {armEx.Message}). Manual review required.",
                        Recommendation = @"MANUAL REVIEW REQUIRED per AC-6:
1. For each critical resource, verify managed identity is enabled:
   - Azure Portal → [Resource Type] → [Resource Name] → Identity
   - Check System assigned or User assigned tab for enabled identities
2. Verify applications use managed identity for authentication (not service principals)
3. Review RBAC permissions granted to each managed identity
4. Document findings in ATO compliance assessment
5. Ensure service principal has 'Reader' permission to query resource configurations

REFERENCES:
- NIST 800-53 AC-6: Least Privilege
- Azure Security Benchmark: Use managed identities",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "AC-6", "IA-5" },
                        ComplianceFrameworks = new List<string> { "NIST-800-53", "Azure Security Benchmark" },
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // No ARM client available
                _logger.LogWarning("ARM client not available for subscription {SubscriptionId}", subscriptionId);
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.ManagedIdentity",
                    ResourceName = "Managed Identity Configuration",
                    FindingType = AtoFindingType.AccessControl,
                    Severity = AtoFindingSeverity.Medium,
                    Title = "Unable to Query Managed Identity Configuration - ARM Client Unavailable",
                    Description = $"ARM client not available to scan managed identity configuration for {criticalResources.Count} critical resources. Manual review required.",
                    Recommendation = @"MANUAL REVIEW REQUIRED per AC-6:
1. For each critical resource, verify managed identity is enabled:
   - Azure Portal → [Resource Type] → [Resource Name] → Identity
   - Verify System assigned or User assigned identity is enabled
2. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-6: Least Privilege",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
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
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning unsuccessful logon attempts (AC-7) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for subscription {SubscriptionId}", subscriptionId);
                return findings;
            }

            // Get VMs that should be monitored for authentication attempts
            var vms = resources.Where(r => r.Type?.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            // Get Log Analytics Workspaces
            var logWorkspaces = resources.Where(r => r.Type?.Equals("Microsoft.OperationalInsights/workspaces", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            if (!vms.Any())
            {
                // No VMs to monitor - informational
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.OperationalInsights/workspaces",
                    ResourceName = "Log Analytics Workspace",
                    FindingType = AtoFindingType.Security,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "No Virtual Machines Requiring Logon Attempt Monitoring",
                    Description = "No virtual machines found requiring authentication attempt monitoring configuration.",
                    Recommendation = "When deploying VMs, configure Log Analytics agents and diagnostic settings per AC-7 requirements.",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-7" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
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
                    Severity = AtoFindingSeverity.Critical,
                    Title = "CRITICAL: No Log Analytics Workspace for Authentication Monitoring",
                    Description = $"Found {vms.Count} virtual machines but NO Log Analytics workspace to collect authentication logs. This is a DoD ATO blocker. Cannot monitor failed logon attempts or detect unauthorized access attempts per AC-7 requirements.",
                    Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per AC-7:
1. Create Log Analytics Workspace:
   - Azure Portal → Log Analytics workspaces → Create
   - Select appropriate region for data sovereignty compliance
   - Configure 90-day retention minimum (DoD requirement)
   - Enable workspace for Microsoft Defender for Cloud

2. Install Log Analytics agents on all VMs:
   - Azure Portal → Virtual Machines → [VM Name] → Extensions + applications
   - Add 'Azure Monitor Agent' extension
   - Configure Data Collection Rules (DCR) to collect:
     * Windows Security Event Logs (Event ID 4625 - Failed logon)
     * Windows Security Event Logs (Event ID 4624 - Successful logon)
     * Linux Syslog (auth facility for failed SSH attempts)
     * Linux Syslog (authpriv facility for authentication events)

3. Configure KQL alerts for failed logon attempts:
   - Log Analytics workspace → Alerts → Create alert rule
   - Windows failed logons query:
     SecurityEvent
     | where EventID == 4625
     | summarize FailedLogons = count() by Computer, Account, bin(TimeGenerated, 15m)
     | where FailedLogons >= 5
   - Linux failed SSH query:
     Syslog
     | where Facility == 'auth' or Facility == 'authpriv'
     | where SyslogMessage contains 'Failed password'
     | summarize FailedSSH = count() by Computer, ProcessName, bin(TimeGenerated, 15m)
     | where FailedSSH >= 5

4. Set alert thresholds per DoD requirements:
   - 5 failed logon attempts within 15 minutes = trigger alert
   - 10 failed logon attempts within 1 hour = escalate to security team
   - Account lockout after excessive failures (configure in AD/OS)

5. Configure alert action groups:
   - Send email to security team
   - Create ServiceNow/Jira incident ticket
   - Trigger automated response (optional: block IP via NSG)
   - Send to SIEM for correlation

6. Enable Microsoft Defender for Cloud:
   - Provides additional threat detection for VMs
   - Detects brute force attacks automatically
   - Integrates with Log Analytics workspace

7. Document monitoring procedures in SSP:
   - List all monitored VMs and their log sources
   - Define alert thresholds and response procedures
   - Document account lockout policies
   - Include incident response workflow

REFERENCES:
- NIST 800-53 AC-7: Unsuccessful Logon Attempts
- NIST 800-53 AU-6: Audit Review, Analysis, and Reporting
- DoD Cloud Computing SRG: Monitoring and Logging Requirements
- Azure Security Benchmark: LT-4 Enable logging for Azure resources",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-7", "AU-6", "AU-12", "SI-4" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "Azure Security Benchmark" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // PROACTIVE CHECK: Query each VM to check for Log Analytics agent and diagnostic settings
            var armClient = _azureService.GetArmClient();
            if (armClient != null)
            {
                try
                {
                    var vmsWithoutMonitoring = new List<string>();
                    var totalVms = vms.Count;
                    
                    foreach (var vm in vms)
                    {
                        try
                        {
                            var vmResource = armClient.GetGenericResource(new ResourceIdentifier(vm.Id));
                            var vmData = await vmResource.GetAsync(cancellationToken: cancellationToken);
                            
                            bool hasLogAnalyticsAgent = false;
                            
                            // Check for extensions (Log Analytics Agent / Azure Monitor Agent)
                            // Try to get VM extensions
                            try
                            {
                                var extensionsUri = $"{vm.Id}/extensions";
                                var extensionsResource = armClient.GetGenericResource(new ResourceIdentifier(extensionsUri));
                                var extensionsData = await extensionsResource.GetAsync(cancellationToken: cancellationToken);
                                
                                var extensionsJson = JsonDocument.Parse(extensionsData.Value.Data.Properties.ToStream());
                                if (extensionsJson.RootElement.TryGetProperty("value", out var extensionsArray))
                                {
                                    foreach (var extension in extensionsArray.EnumerateArray())
                                    {
                                        if (extension.TryGetProperty("properties", out var extProps))
                                        {
                                            if (extProps.TryGetProperty("type", out var extType))
                                            {
                                                var extensionType = extType.GetString();
                                                // Check for Log Analytics agents
                                                if (extensionType?.Contains("MicrosoftMonitoringAgent", StringComparison.OrdinalIgnoreCase) == true ||
                                                    extensionType?.Contains("OmsAgentForLinux", StringComparison.OrdinalIgnoreCase) == true ||
                                                    extensionType?.Contains("AzureMonitorWindowsAgent", StringComparison.OrdinalIgnoreCase) == true ||
                                                    extensionType?.Contains("AzureMonitorLinuxAgent", StringComparison.OrdinalIgnoreCase) == true)
                                                {
                                                    hasLogAnalyticsAgent = true;
                                                    _logger.LogDebug("VM {VmId} has monitoring agent: {ExtensionType}", vm.Id, extensionType);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (Exception extEx)
                            {
                                _logger.LogDebug(extEx, "Unable to query extensions for VM {VmId}, assuming no agent", vm.Id);
                            }
                            
                            if (!hasLogAnalyticsAgent)
                            {
                                vmsWithoutMonitoring.Add(vm.Name ?? vm.Id);
                                _logger.LogInformation("VM {VmId} does NOT have Log Analytics agent installed", vm.Id);
                            }
                        }
                        catch (Exception vmEx)
                        {
                            _logger.LogWarning(vmEx, "Unable to query monitoring configuration for VM {VmId}", vm.Id);
                            // If we can't query, assume it's not monitored to be safe
                            vmsWithoutMonitoring.Add(vm.Name ?? vm.Id);
                        }
                    }
                    
                    _logger.LogInformation("Scanned {TotalVms} VMs: {VmsWithoutMonitoring} without authentication monitoring", 
                        totalVms, vmsWithoutMonitoring.Count);
                    
                    // HIGH: VMs exist without authentication monitoring
                    if (vmsWithoutMonitoring.Any())
                    {
                        var vmList = string.Join(", ", vmsWithoutMonitoring.Take(10));
                        var remainingCount = vmsWithoutMonitoring.Count > 10 ? $" (+{vmsWithoutMonitoring.Count - 10} more)" : "";
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.OperationalInsights/workspaces",
                            ResourceName = "Log Analytics Agent Configuration",
                            FindingType = AtoFindingType.Security,
                            Severity = AtoFindingSeverity.High,
                            Title = "Virtual Machines Without Authentication Monitoring",
                            Description = $"Found {vmsWithoutMonitoring.Count} of {totalVms} virtual machines without Log Analytics agents installed. VMs without monitoring: {vmList}{remainingCount}. Cannot monitor failed logon attempts or detect unauthorized access per AC-7. This violates DoD requirements for continuous monitoring and incident detection.",
                            Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per AC-7:
1. Install Log Analytics agent on each unmonitored VM:
   
   FOR WINDOWS VMs:
   - Azure Portal → Virtual Machines → [VM Name] → Extensions + applications
   - Add extension → 'Azure Monitor Agent' (recommended) OR 'MicrosoftMonitoringAgent' (legacy)
   - Configure workspace connection to existing Log Analytics workspace
   - Agent will automatically start collecting Security Event Logs
   
   FOR LINUX VMs:
   - Azure Portal → Virtual Machines → [VM Name] → Extensions + applications
   - Add extension → 'Azure Monitor Agent' (recommended) OR 'OmsAgentForLinux' (legacy)
   - Configure workspace connection to existing Log Analytics workspace
   - Agent will automatically start collecting Syslog events

2. Configure Data Collection Rules (DCR) for Azure Monitor Agent:
   - Azure Portal → Monitor → Data Collection Rules → Create
   - Add data sources:
     * Windows Security Events → Select 'Common' or 'All Security Events'
     * Linux Syslog → Select 'auth' and 'authpriv' facilities with 'LOG_INFO' level minimum
   - Associate DCR with target VMs
   - Verify data flow within 5-10 minutes

3. For legacy agents, configure data collection:
   - Log Analytics workspace → Agents configuration
   - Windows Events → Add 'Security' event log
   - Linux Syslog → Add 'auth' and 'authpriv' facilities

4. Create KQL alert queries for failed logon detection:
   
   WINDOWS FAILED LOGONS (Event ID 4625):
   SecurityEvent
   | where EventID == 4625
   | where TimeGenerated > ago(15m)
   | summarize FailedLogons = count() by Computer, Account, IpAddress
   | where FailedLogons >= 5
   | project Computer, Account, IpAddress, FailedLogons, Alert = 'Possible brute force attack'
   
   LINUX FAILED SSH ATTEMPTS:
   Syslog
   | where Facility in ('auth', 'authpriv')
   | where SyslogMessage contains 'Failed password' or SyslogMessage contains 'authentication failure'
   | where TimeGenerated > ago(15m)
   | summarize FailedSSH = count() by Computer, ProcessName, SyslogMessage
   | where FailedSSH >= 5
   | project Computer, ProcessName, FailedSSH, Alert = 'Possible SSH brute force'

5. Configure alert rules with appropriate thresholds:
   - 5 failed attempts in 15 minutes = WARNING alert
   - 10 failed attempts in 15 minutes = HIGH alert (escalate)
   - 20 failed attempts in 15 minutes = CRITICAL alert (immediate response)
   - Configure account lockout policies in OS/AD

6. Set up alert action groups:
   - Email: security@organization.mil
   - SMS: Security team on-call number
   - Webhook: SIEM integration endpoint
   - ServiceNow/Jira: Auto-create incident ticket
   - Optional: Azure Automation runbook to block attacking IPs

7. Enable Microsoft Defender for Cloud threat detection:
   - Provides ML-based brute force attack detection
   - Correlates authentication failures across multiple VMs
   - Detects suspicious authentication patterns
   - Integrates with existing Log Analytics workspace

8. Test monitoring configuration:
   - Generate test failed logon attempts
   - Verify events appear in Log Analytics within 5 minutes
   - Confirm alerts fire according to thresholds
   - Validate action group notifications are received

9. Document in System Security Plan (SSP):
   - List all monitored VMs with agent versions
   - Document alert thresholds and justification
   - Define incident response procedures for authentication failures
   - Include escalation matrix and contact information
   - Document account lockout policies (e.g., 5 attempts = 30 min lockout)

10. Implement continuous monitoring:
    - Review authentication logs daily
    - Investigate all HIGH/CRITICAL alerts within 1 hour
    - Quarterly review of alert thresholds and false positive rate
    - Annual testing of incident response procedures

REFERENCES:
- NIST 800-53 AC-7: Unsuccessful Logon Attempts (5 attempts, 15 minute window)
- NIST 800-53 AU-6: Audit Review, Analysis, and Reporting
- NIST 800-53 SI-4: System Monitoring
- DoD Cloud Computing SRG: Continuous Monitoring Requirements
- Azure Security Benchmark: LT-4 Enable logging for security investigation",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-7", "AU-6", "AU-12", "SI-4" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "Azure Security Benchmark" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        // All VMs have monitoring agents - compliant
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = logWorkspaces[0].Id,
                            ResourceType = "Microsoft.OperationalInsights/workspaces",
                            ResourceName = logWorkspaces[0].Name,
                            FindingType = AtoFindingType.Security,
                            Severity = AtoFindingSeverity.Informational,
                            Title = "Authentication Monitoring Configured on All VMs",
                            Description = $"All {totalVms} virtual machines have Log Analytics agents installed and configured to send authentication logs to workspace '{logWorkspaces[0].Name}'. Failed logon attempt monitoring is in place per AC-7.",
                            Recommendation = @"MAINTAIN CURRENT POSTURE per AC-7:
1. Verify alert rules are configured and functioning:
   - Log Analytics workspace → Alerts → Review existing rules
   - Test alerts by generating controlled failed logon attempts
   - Verify alert notifications reach security team

2. Review alert thresholds quarterly:
   - Analyze false positive rates
   - Adjust thresholds based on environment baseline
   - Document threshold changes in SSP

3. Monitor agent health:
   - Azure Portal → Log Analytics workspace → Agents
   - Verify all agents report 'Connected' status
   - Investigate any agents showing 'Disconnected' immediately
   - Ensure agent versions are up to date

4. Review authentication logs regularly:
   - Daily review of failed logon patterns
   - Weekly trend analysis for anomalies
   - Monthly report to security leadership
   - Document all investigations in incident log

5. Implement account lockout policies:
   - Windows: Group Policy → Account Lockout Policy
     * Lockout threshold: 5 invalid attempts
     * Lockout duration: 30 minutes minimum
     * Reset counter after: 30 minutes
   - Linux: Configure pam_faillock module
     * deny=5 (lockout after 5 attempts)
     * unlock_time=1800 (30 minutes)

6. Configure alert severity escalation:
   - 5-9 failed attempts: Low severity (log only)
   - 10-19 failed attempts: Medium severity (email security team)
   - 20+ failed attempts: High severity (immediate investigation)
   - Successful logon after multiple failures: Critical (potential compromise)

7. Enable Microsoft Defender for Cloud recommendations:
   - Review security alerts for VMs
   - Implement Just-In-Time (JIT) VM access
   - Disable public RDP/SSH exposure (use Azure Bastion)

8. Integrate with SIEM:
   - Forward Log Analytics data to central SIEM
   - Create correlation rules for distributed attacks
   - Configure automated response playbooks

9. Test incident response procedures:
   - Quarterly tabletop exercises
   - Annual red team testing of authentication controls
   - Document lessons learned and update procedures

10. Document in System Security Plan:
    - Current alert configurations and thresholds
    - Incident response procedures and timelines
    - Account lockout policies and exceptions
    - Review and approval authority

REFERENCES:
- NIST 800-53 AC-7: Unsuccessful Logon Attempts
- NIST 800-53 AU-6: Audit Review, Analysis, and Reporting
- Continue monitoring with Microsoft Defender for Cloud",
                            ComplianceStatus = AtoComplianceStatus.Compliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-7", "AU-6" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception armEx)
                {
                    _logger.LogWarning(armEx, "Unable to query VM monitoring configurations for subscription {SubscriptionId}", subscriptionId);
                    
                    // Fallback if ARM client query fails
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceType = "Microsoft.OperationalInsights/workspaces",
                        ResourceName = "Log Analytics Agent Configuration",
                        FindingType = AtoFindingType.Security,
                        Severity = AtoFindingSeverity.Medium,
                        Title = "Unable to Query VM Monitoring Configuration - Manual Review Required",
                        Description = $"Unable to automatically scan Log Analytics agent configuration for {vms.Count} virtual machines (Error: {armEx.Message}). Manual review required.",
                        Recommendation = @"MANUAL REVIEW REQUIRED per AC-7:
1. For each virtual machine, verify Log Analytics agent is installed:
   - Azure Portal → Virtual Machines → [VM Name] → Extensions + applications
   - Look for 'MicrosoftMonitoringAgent', 'OmsAgentForLinux', or 'AzureMonitorAgent'
2. Verify agent is connected to Log Analytics workspace
3. Confirm authentication events are being collected
4. Verify alert rules are configured for failed logon attempts
5. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-7: Unsuccessful Logon Attempts",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "AC-7", "AU-6" },
                        ComplianceFrameworks = new List<string> { "NIST-800-53" },
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                // No ARM client available
                _logger.LogWarning("ARM client not available for subscription {SubscriptionId}", subscriptionId);
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.OperationalInsights/workspaces",
                    ResourceName = "Log Analytics Agent Configuration",
                    FindingType = AtoFindingType.Security,
                    Severity = AtoFindingSeverity.Medium,
                    Title = "Unable to Query VM Monitoring Configuration - ARM Client Unavailable",
                    Description = $"ARM client not available to scan Log Analytics agent configuration for {vms.Count} virtual machines. Manual review required.",
                    Recommendation = @"MANUAL REVIEW REQUIRED per AC-7:
1. For each virtual machine, verify Log Analytics agent is installed:
   - Azure Portal → Virtual Machines → [VM Name] → Extensions + applications
2. Verify agents are connected and collecting authentication events
3. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-7: Unsuccessful Logon Attempts",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
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
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning generic access control {ControlId} for {Scope} in subscription {SubscriptionId}", control.Id, scope, subscriptionId);

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

    private async Task<List<AtoFinding>> ScanInformationFlowEnforcementAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning information flow enforcement (AC-4) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);
                return findings;
            }

            // Get virtual networks for subnet segmentation analysis
            var vnets = resources.Where(r => r.Type?.Equals("Microsoft.Network/virtualNetworks", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            if (!vnets.Any())
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Network/virtualNetworks",
                    ResourceName = "Virtual Networks",
                    FindingType = AtoFindingType.Security,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "No Virtual Networks Requiring Information Flow Controls",
                    Description = "No virtual networks found requiring information flow enforcement validation.",
                    Recommendation = "When deploying resources, implement network segmentation per AC-4 requirements.",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-4" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // PROACTIVE CHECK: Query each VNet to check for subnet segmentation and route tables
            var armClient = _azureService.GetArmClient();
            if (armClient != null)
            {
                try
                {
                    var vnetsWithoutSegmentation = new List<string>();
                    var subnetsWithoutRouteTables = new List<string>();
                    var totalVnets = vnets.Count;
                    var totalSubnets = 0;
                    
                    foreach (var vnet in vnets)
                    {
                        try
                        {
                            var vnetResource = armClient.GetGenericResource(new ResourceIdentifier(vnet.Id));
                            var vnetData = await vnetResource.GetAsync(cancellationToken: cancellationToken);
                            
                            // Parse VNet properties to get subnets
                            var vnetPropsJson = JsonDocument.Parse(vnetData.Value.Data.Properties.ToStream());
                            var vnetProps = vnetPropsJson.RootElement;
                            
                            if (vnetProps.TryGetProperty("subnets", out var subnetsArray))
                            {
                                var subnetCount = subnetsArray.GetArrayLength();
                                totalSubnets += subnetCount;
                                
                                // Check if VNet has proper segmentation (multiple subnets)
                                if (subnetCount <= 1)
                                {
                                    vnetsWithoutSegmentation.Add(vnet.Name ?? vnet.Id);
                                    _logger.LogInformation("VNet {VnetId} has only {SubnetCount} subnet - lacks network segmentation", vnet.Id, subnetCount);
                                }
                                
                                // Check each subnet for route table (UDR) to enforce traffic flow
                                foreach (var subnet in subnetsArray.EnumerateArray())
                                {
                                    if (subnet.TryGetProperty("name", out var subnetNameProp))
                                    {
                                        var subnetName = subnetNameProp.GetString();
                                        
                                        // Skip special Azure subnets
                                        if (subnetName?.Equals("GatewaySubnet", StringComparison.OrdinalIgnoreCase) == true ||
                                            subnetName?.Equals("AzureBastionSubnet", StringComparison.OrdinalIgnoreCase) == true ||
                                            subnetName?.Equals("AzureFirewallSubnet", StringComparison.OrdinalIgnoreCase) == true)
                                        {
                                            continue;
                                        }
                                        
                                        if (subnet.TryGetProperty("properties", out var subnetProps))
                                        {
                                            // Check for route table (UDR)
                                            if (!subnetProps.TryGetProperty("routeTable", out var _))
                                            {
                                                subnetsWithoutRouteTables.Add($"{vnet.Name}/{subnetName}");
                                                _logger.LogInformation("Subnet {SubnetName} in VNet {VnetName} does NOT have route table - cannot enforce traffic flow", subnetName, vnet.Name);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception vnetEx)
                        {
                            _logger.LogWarning(vnetEx, "Unable to query segmentation for VNet {VnetId}", vnet.Id);
                            vnetsWithoutSegmentation.Add(vnet.Name ?? vnet.Id);
                        }
                    }
                    
                    _logger.LogInformation("Scanned {TotalVnets} VNets with {TotalSubnets} subnets: {VnetsWithoutSegmentation} lack segmentation, {SubnetsWithoutRouteTables} subnets without UDR", 
                        totalVnets, totalSubnets, vnetsWithoutSegmentation.Count, subnetsWithoutRouteTables.Count);
                    
                    // HIGH: VNets without proper segmentation or subnets without route tables
                    if (vnetsWithoutSegmentation.Any() || subnetsWithoutRouteTables.Any())
                    {
                        var issues = new List<string>();
                        if (vnetsWithoutSegmentation.Any())
                        {
                            var vnetList = string.Join(", ", vnetsWithoutSegmentation.Take(5));
                            issues.Add($"{vnetsWithoutSegmentation.Count} VNet(s) with insufficient segmentation: {vnetList}");
                        }
                        if (subnetsWithoutRouteTables.Any())
                        {
                            var subnetList = string.Join(", ", subnetsWithoutRouteTables.Take(5));
                            issues.Add($"{subnetsWithoutRouteTables.Count} subnet(s) without route tables: {subnetList}");
                        }
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Network/virtualNetworks",
                            ResourceName = "Network Segmentation",
                            FindingType = AtoFindingType.Security,
                            Severity = AtoFindingSeverity.High,
                            Title = "Insufficient Network Segmentation and Information Flow Controls",
                            Description = $"Information flow enforcement issues detected: {string.Join("; ", issues)}. Without proper subnet segmentation and User-Defined Routes (UDR), traffic flows cannot be controlled per AC-4. This violates DoD requirements for network isolation and traffic inspection.",
                            Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per AC-4:
1. Implement network segmentation in each VNet:
   - Separate subnets for different tiers: web tier, app tier, data tier
   - Separate subnets for different sensitivity levels (IL4, IL5, IL6)
   - Dedicated subnets for management resources (Bastion, jump boxes)
   - Minimum 3 subnets per VNet for proper tier separation

2. Create User-Defined Routes (UDR) for each subnet:
   - Azure Portal → Route tables → Create
   - Add route: Destination 0.0.0.0/0 → Next hop type: Virtual appliance → Next hop address: [Azure Firewall IP]
   - Associate route table with subnet
   - Forces all traffic through central inspection point

3. Deploy Azure Firewall for centralized traffic control:
   - Create dedicated AzureFirewallSubnet (minimum /26 CIDR)
   - Deploy Azure Firewall Standard or Premium (Premium for IL5: TLS inspection)
   - Configure DNAT, Network, and Application rules
   - Default DENY all traffic, explicitly ALLOW required flows only

4. Configure Network Security Groups (NSG) on each subnet:
   - Defense-in-depth: NSG + UDR + Azure Firewall
   - NSG rules for intra-subnet traffic control
   - Deny all by default, allow specific ports/protocols
   - Log all NSG decisions to Log Analytics

5. Implement micro-segmentation with Application Security Groups (ASG):
   - Create ASGs for logical groupings: web-servers, app-servers, db-servers
   - NSG rules reference ASGs instead of IP addresses
   - Simplifies rule management as workloads scale
   - Example: Allow web-servers ASG → app-servers ASG on port 443

6. Enable VNet flow logs:
   - Azure Portal → Virtual Networks → [VNet] → Flow logs
   - Configure with 90-day retention to Log Analytics
   - Analyze traffic patterns and detect anomalies
   - Identify unauthorized information flows

7. Implement Private Link for PaaS services:
   - Storage, SQL, Key Vault accessed via private endpoints
   - Eliminates Internet traffic for PaaS services
   - Traffic flows through VNet and subject to UDR/firewall inspection

8. Document information flow policies:
   - Data flow diagram showing allowed communication paths
   - Justification for each allowed flow in SSP
   - Classification of data in each network segment
   - Approval authority for new information flows

9. Test traffic flow enforcement:
   - Verify traffic actually flows through firewall (check firewall logs)
   - Test that unauthorized flows are blocked
   - Validate asymmetric routing is prevented
   - Document test results in ATO package

10. Configure monitoring and alerts:
    - Alert on new subnets created without route tables
    - Alert on route table modifications
    - Alert on Azure Firewall rule changes
    - Alert on high volume of denied flows (possible misconfiguration or attack)

REFERENCES:
- NIST 800-53 AC-4: Information Flow Enforcement
- NIST 800-53 SC-7: Boundary Protection
- DoD Cloud Computing SRG: Network Segmentation Requirements
- Azure Security Benchmark: NS-2 Segment networks",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-4", "SC-7", "SC-7(5)" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "Azure Security Benchmark" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Network/virtualNetworks",
                            ResourceName = "Network Segmentation",
                            FindingType = AtoFindingType.Security,
                            Severity = AtoFindingSeverity.Informational,
                            Title = "Network Segmentation and Information Flow Controls Implemented",
                            Description = $"All {totalVnets} virtual network(s) have proper subnet segmentation (multiple subnets) and all {totalSubnets} workload subnets have User-Defined Routes configured. Information flow enforcement mechanisms are in place per AC-4.",
                            Recommendation = @"MAINTAIN CURRENT POSTURE per AC-4:
1. Quarterly review of route tables and traffic flows
2. Verify Azure Firewall logs show traffic is being inspected
3. Test that unauthorized flows are blocked
4. Update network diagrams when topology changes
5. Document all information flow policies in SSP
6. Enable VNet flow logs with 90-day retention
7. Create alerts for route table modifications

REFERENCES:
- NIST 800-53 AC-4: Information Flow Enforcement
- Continue monitoring with Microsoft Defender for Cloud",
                            ComplianceStatus = AtoComplianceStatus.Compliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-4" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception armEx)
                {
                    _logger.LogWarning(armEx, "Unable to query network segmentation for subscription {SubscriptionId}", subscriptionId);
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceType = "Microsoft.Network/virtualNetworks",
                        ResourceName = "Network Segmentation",
                        FindingType = AtoFindingType.Security,
                        Severity = AtoFindingSeverity.Medium,
                        Title = "Unable to Query Network Segmentation - Manual Review Required",
                        Description = $"Unable to automatically scan network segmentation for {vnets.Count} VNet(s) (Error: {armEx.Message}). Manual review required.",
                        Recommendation = @"MANUAL REVIEW REQUIRED per AC-4:
1. For each VNet, verify multiple subnets for tier separation
2. For each subnet, verify route table (UDR) is configured
3. Verify route tables force traffic through Azure Firewall or NVA
4. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-4: Information Flow Enforcement",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "AC-4" },
                        ComplianceFrameworks = new List<string> { "NIST-800-53" },
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                _logger.LogWarning("ARM client not available for subscription {SubscriptionId}", subscriptionId);
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Network/virtualNetworks",
                    ResourceName = "Network Segmentation",
                    FindingType = AtoFindingType.Security,
                    Severity = AtoFindingSeverity.Medium,
                    Title = "Unable to Query Network Segmentation - ARM Client Unavailable",
                    Description = $"ARM client not available to scan network segmentation for {vnets.Count} VNet(s). Manual review required.",
                    Recommendation = @"MANUAL REVIEW REQUIRED per AC-4:
1. Verify each VNet has proper subnet segmentation
2. Verify each subnet has route table configured
3. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-4: Information Flow Enforcement",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-4" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning information flow enforcement for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanSeparationOfDutiesAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning separation of duties (AC-5) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            if (resources == null || resources.Count == 0)
            {
                _logger.LogWarning("No resources found for subscription {SubscriptionId}", subscriptionId);
                return findings;
            }

            var subscriptionResourceId = $"/subscriptions/{subscriptionId}";
            
            // Check for conflicting role assignments (e.g., same principal with both Owner and Reader)
            var armClient = _azureService.GetArmClient();
            if (armClient != null)
            {
                try
                {
                    var subscription = armClient.GetSubscriptionResource(new ResourceIdentifier(subscriptionResourceId));
                    var roleAssignmentCollection = subscription.GetRoleAssignments();
                    
                    // Track principals with multiple roles
                    var principalRoles = new Dictionary<string, List<string>>();
                    
                    await foreach (var roleAssignment in roleAssignmentCollection.GetAllAsync(cancellationToken: cancellationToken))
                    {
                        var principalId = roleAssignment.Data.PrincipalId.ToString();
                        var roleDefinitionId = roleAssignment.Data.RoleDefinitionId.ToString();
                        
                        if (!principalRoles.ContainsKey(principalId))
                        {
                            principalRoles[principalId] = new List<string>();
                        }
                        principalRoles[principalId].Add(roleDefinitionId);
                    }
                    
                    // Identify principals with conflicting roles (Owner + any other, Contributor + User Access Admin)
                    var ownerRoleId = "8e3af657-a8ff-443c-a75c-2fe8c4bcb635";
                    var contributorRoleId = "b24988ac-6180-42a0-ab88-20f7382dd24c";
                    var userAccessAdminRoleId = "18d7d88d-d35e-4fb5-a5c3-7773c20a72d9";
                    
                    var conflictingPrincipals = principalRoles
                        .Where(p => p.Value.Count > 1 && 
                               (p.Value.Any(r => r.Contains(ownerRoleId)) || 
                                (p.Value.Any(r => r.Contains(contributorRoleId)) && p.Value.Any(r => r.Contains(userAccessAdminRoleId)))))
                        .ToList();
                    
                    if (conflictingPrincipals.Any())
                    {
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = subscriptionResourceId,
                            ResourceType = "Microsoft.Authorization/roleAssignments",
                            ResourceName = "Separation of Duties",
                            FindingType = AtoFindingType.AccessControl,
                            Severity = AtoFindingSeverity.Medium,
                            Title = "Potential Separation of Duties Violations",
                            Description = $"Found {conflictingPrincipals.Count} principals with multiple privileged roles that may violate separation of duties principles. Principals with Owner roles OR combined Contributor + User Access Administrator roles can bypass segregation controls.",
                            Recommendation = @"REMEDIATION REQUIRED per AC-5:
1. Review principals with multiple roles:
   - Azure Portal → Subscriptions → Access Control (IAM) → Role assignments
   - Identify users/service principals with Owner role
   - Identify users with BOTH Contributor AND User Access Administrator roles

2. Implement role separation:
   - NO single principal should have Owner role (combines resource management + RBAC administration)
   - Separate resource management (Contributor) from access management (User Access Administrator)
   - Use custom roles with minimal permissions instead of broad built-in roles

3. Define and enforce segregation of duties policies:
   - Development teams: Contributor role on dev resource groups only
   - Security teams: Security Admin role (read + security operations, no resource management)
   - IAM admins: User Access Administrator role (RBAC only, no resource access)
   - Audit teams: Reader role (read-only across all resources)

4. Implement Azure AD Privileged Identity Management (PIM):
   - Time-limited role activations (just-in-time access)
   - Approval workflow for privileged role activation
   - Prevents standing access to conflicting roles
   - Audit trail of all role activations

5. Use Azure AD Access Reviews:
   - Quarterly review of all role assignments
   - Manager or peer approval required for renewal
   - Automatically remove assignments not approved

6. Document separation of duties in SSP:
   - Role matrix showing function segregation
   - Approval authorities for each role
   - Compensating controls for unavoidable conflicts
   - Exception process and justification requirements

REFERENCES:
- NIST 800-53 AC-5: Separation of Duties
- NIST 800-53 AC-6: Least Privilege
- DoD Cloud Computing SRG: Access Control Requirements",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-5", "AC-6" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = subscriptionResourceId,
                            ResourceType = "Microsoft.Authorization/roleAssignments",
                            ResourceName = "Separation of Duties",
                            FindingType = AtoFindingType.AccessControl,
                            Severity = AtoFindingSeverity.Informational,
                            Title = "No Obvious Separation of Duties Violations Detected",
                            Description = "No principals detected with obviously conflicting role combinations at subscription level. Manual review of specific role combinations still recommended.",
                            Recommendation = @"MAINTAIN CURRENT POSTURE per AC-5:
1. Continue quarterly access reviews using Azure AD Access Reviews
2. Document separation of duties policies in SSP
3. Implement Azure AD PIM for time-limited privileged access
4. Monitor for new conflicting role assignments

REFERENCES:
- NIST 800-53 AC-5: Separation of Duties",
                            ComplianceStatus = AtoComplianceStatus.Compliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-5" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception roleEx)
                {
                    _logger.LogWarning(roleEx, "Unable to query role assignments for subscription {SubscriptionId}", subscriptionId);
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = subscriptionResourceId,
                        ResourceType = "Microsoft.Authorization/roleAssignments",
                        ResourceName = "Separation of Duties",
                        FindingType = AtoFindingType.AccessControl,
                        Severity = AtoFindingSeverity.Medium,
                        Title = "Unable to Validate Separation of Duties - Manual Review Required",
                        Description = $"Unable to automatically validate separation of duties (Error: {roleEx.Message}). Manual review required.",
                        Recommendation = @"MANUAL REVIEW REQUIRED per AC-5:
1. Review all role assignments for conflicting duties
2. Verify no single principal has Owner role
3. Ensure Contributor and User Access Administrator roles are assigned to different principals
4. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-5: Separation of Duties",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "AC-5" },
                        ComplianceFrameworks = new List<string> { "NIST-800-53" },
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = subscriptionResourceId,
                    ResourceType = "Microsoft.Authorization/roleAssignments",
                    ResourceName = "Separation of Duties",
                    FindingType = AtoFindingType.AccessControl,
                    Severity = AtoFindingSeverity.Medium,
                    Title = "Unable to Validate Separation of Duties - ARM Client Unavailable",
                    Description = "ARM client not available to validate separation of duties. Manual review required.",
                    Recommendation = @"MANUAL REVIEW REQUIRED per AC-5:
1. Review all role assignments for conflicting duties
2. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-5: Separation of Duties",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-5" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning separation of duties for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanRemoteAccessAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning remote access (AC-17) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for subscription {SubscriptionId}", subscriptionId);
                return findings;
            }

            var vms = resources.Where(r => r.Type?.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            if (!vms.Any())
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Network/bastionHosts",
                    ResourceName = "Remote Access Controls",
                    FindingType = AtoFindingType.Security,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "No Virtual Machines Requiring Remote Access",
                    Description = "No virtual machines found requiring remote access validation.",
                    Recommendation = "When deploying VMs, implement Azure Bastion or VPN Gateway per AC-17 requirements.",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-17" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // PROACTIVE CHECK: Query each VM to check for public IP exposure
            var armClient = _azureService.GetArmClient();
            if (armClient != null)
            {
                try
                {
                    var vmsWithPublicIPs = new List<string>();
                    var totalVms = vms.Count;
                    
                    foreach (var vm in vms)
                    {
                        try
                        {
                            var vmResource = armClient.GetGenericResource(new ResourceIdentifier(vm.Id));
                            var vmData = await vmResource.GetAsync(cancellationToken: cancellationToken);
                            
                            // Parse VM properties to get network interfaces
                            var vmPropsJson = JsonDocument.Parse(vmData.Value.Data.Properties.ToStream());
                            var vmProps = vmPropsJson.RootElement;
                            
                            if (vmProps.TryGetProperty("networkProfile", out var networkProfile))
                            {
                                if (networkProfile.TryGetProperty("networkInterfaces", out var nicsArray))
                                {
                                    foreach (var nicElement in nicsArray.EnumerateArray())
                                    {
                                        if (nicElement.TryGetProperty("id", out var nicIdProperty))
                                        {
                                            var nicId = nicIdProperty.GetString();
                                            if (!string.IsNullOrEmpty(nicId))
                                            {
                                                // Get NIC resource to check for public IP
                                                var nicResource = armClient.GetGenericResource(new ResourceIdentifier(nicId));
                                                var nicData = await nicResource.GetAsync(cancellationToken: cancellationToken);
                                                
                                                var nicPropsJson = JsonDocument.Parse(nicData.Value.Data.Properties.ToStream());
                                                var nicProps = nicPropsJson.RootElement;
                                                
                                                // Check IP configurations for public IP
                                                if (nicProps.TryGetProperty("ipConfigurations", out var ipConfigs))
                                                {
                                                    foreach (var ipConfig in ipConfigs.EnumerateArray())
                                                    {
                                                        if (ipConfig.TryGetProperty("properties", out var ipConfigProps))
                                                        {
                                                            if (ipConfigProps.TryGetProperty("publicIPAddress", out var publicIP))
                                                            {
                                                                vmsWithPublicIPs.Add(vm.Name ?? vm.Id);
                                                                _logger.LogInformation("VM {VmId} HAS public IP - direct Internet exposure", vm.Id);
                                                                break;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception vmEx)
                        {
                            _logger.LogWarning(vmEx, "Unable to query public IP configuration for VM {VmId}", vm.Id);
                            // If we can't query, flag as potential issue to be safe
                            vmsWithPublicIPs.Add(vm.Name ?? vm.Id);
                        }
                    }
                    
                    _logger.LogInformation("Scanned {TotalVms} VMs: {VmsWithPublicIPs} have public IP addresses", 
                        totalVms, vmsWithPublicIPs.Count);
                    
                    // CRITICAL: VMs with public IPs (direct Internet exposure)
                    if (vmsWithPublicIPs.Any())
                    {
                        var vmList = string.Join(", ", vmsWithPublicIPs.Take(10));
                        var remainingCount = vmsWithPublicIPs.Count > 10 ? $" (+{vmsWithPublicIPs.Count - 10} more)" : "";
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Network/publicIPAddresses",
                            ResourceName = "VM Public IP Exposure",
                            FindingType = AtoFindingType.Security,
                            Severity = AtoFindingSeverity.Critical,
                            Title = "CRITICAL: Virtual Machines With Public IP Addresses",
                            Description = $"Found {vmsWithPublicIPs.Count} of {totalVms} virtual machines with public IP addresses directly attached. VMs with public IPs: {vmList}{remainingCount}. Direct Internet exposure violates AC-17 and creates attack surface for unauthorized remote access. This is a DoD ATO blocker.",
                            Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per AC-17:
1. Deploy Azure Bastion for secure remote access (RECOMMENDED for DoD):
   - Azure Portal → Bastions → Create
   - Select 'Standard' SKU (session recording, native client support)
   - Requires dedicated 'AzureBastionSubnet' (minimum /26 CIDR)
   - Enables RDP/SSH access without public IPs

2. Remove public IP addresses from ALL VMs:
   - Azure Portal → Virtual Machines → [VM Name] → Networking
   - For each network interface:
     * Click NIC name → IP configurations
     * Dissociate public IP address
     * Delete public IP resource (if no longer needed)
   - CRITICAL: Ensure you have alternate access (Bastion/VPN) BEFORE removing public IPs

3. Verify NSG rules block direct Internet RDP/SSH:
   - Azure Portal → Network Security Groups → [NSG Name] → Inbound security rules
   - Ensure NO rules allow source 'Internet' or '*' to destination port 3389 (RDP) or 22 (SSH)
   - Add explicit DENY rules for RDP/SSH from Internet (priority < 4096)

4. Configure Azure Bastion for each affected VM:
   - Ensure VM's VNet is peered with Bastion's VNet (if in different VNets)
   - Test connectivity: Azure Portal → VM → Connect → Bastion
   - Enter credentials and verify successful connection

5. Enable Bastion session recording (DoD audit requirement):
   - Azure Portal → Bastions → [Bastion Name] → Configuration
   - Enable 'Session Recording' → Select Storage Account
   - Retent sessions for 90 days minimum (DoD requirement)
   - Review recordings for security investigations

6. Implement Just-In-Time (JIT) VM Access:
   - Microsoft Defender for Cloud → Just-in-time VM access
   - Enable JIT for all VMs
   - Maximum access duration: 3 hours
   - Require approval workflow for access requests
   - Automatically revokes access after expiration

7. Configure Conditional Access for VM sign-in:
   - Azure AD → Security → Conditional Access → New Policy
   - Target applications: 'Azure Windows VM Sign-In', 'Azure Linux VM Sign-In'
   - Conditions: All users, All locations
   - Grant controls: Require MFA, Require compliant device
   - Session controls: Sign-in frequency = Every time

8. Alternative remote access methods (if Bastion not suitable):
   - Point-to-Site VPN: Individual user VPN with Azure AD authentication + MFA
   - Site-to-Site VPN: Corporate network VPN tunnel
   - ExpressRoute: Dedicated private connection (for classified workloads)
   - All require MFA and must NOT use public IPs on VMs

9. Enable diagnostic logging:
   - Bastion: Diagnostic Settings → Send to Log Analytics (90-day retention)
   - VPN Gateway: Diagnostic Settings → Send to Log Analytics
   - Create alerts for:
     * New public IP associations to VMs (should be blocked by policy)
     * Failed Bastion authentication attempts
     * Bastion connections from unusual locations

10. Implement Azure Policy to prevent public IP creation:
    - Policy: 'Virtual machines should not have a public IP address'
    - Effect: Deny (prevents deployment of VMs with public IPs)
    - Scope: Subscription or resource group level
    - Document exceptions in SSP (e.g., load balancers may need public IPs)

11. Document remote access procedures in SSP:
    - Approved remote access methods (Bastion, VPN)
    - Access request and approval workflow
    - MFA requirements (no exceptions for administrative access)
    - Maximum session duration and timeout policies
    - Incident response for unauthorized access attempts

REFERENCES:
- NIST 800-53 AC-17: Remote Access
- NIST 800-53 AC-17(1): Remote Access Monitoring and Control
- NIST 800-53 IA-2(1): Multi-Factor Authentication for Network Access
- DoD Cloud Computing SRG: Remote Access Controls (IL5 requirements)
- Azure Security Benchmark: NS-1 Establish network segmentation boundaries",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-17", "AC-17(1)", "IA-2(1)", "SC-7" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "Azure Security Benchmark" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Network/publicIPAddresses",
                            ResourceName = "Remote Access Controls",
                            FindingType = AtoFindingType.Security,
                            Severity = AtoFindingSeverity.Informational,
                            Title = "No VMs With Public IP Exposure",
                            Description = $"All {totalVms} virtual machines are accessed without public IPs (likely through Bastion, VPN, or private connectivity). Secure remote access controls are in place per AC-17.",
                            Recommendation = @"MAINTAIN CURRENT POSTURE per AC-17:
1. Verify Azure Bastion or VPN Gateway is deployed and operational
2. Enable Bastion session recording with 90-day retention (DoD requirement)
3. Implement Just-In-Time (JIT) VM Access for time-limited access
4. Configure Conditional Access policies requiring MFA for VM sign-in
5. Implement Azure Policy to DENY creation of VMs with public IPs
6. Enable diagnostic logging for all remote access methods
7. Create alerts for:
   - New public IP associations to VMs
   - Failed authentication attempts
   - Remote access from unusual locations
8. Quarterly review of remote access permissions and logs
9. Document remote access procedures in SSP
10. Test remote access controls and incident response procedures

REFERENCES:
- NIST 800-53 AC-17: Remote Access
- Continue monitoring with Microsoft Defender for Cloud",
                            ComplianceStatus = AtoComplianceStatus.Compliant,
                            AffectedNistControls = new List<string> { control.Id ?? "AC-17" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception armEx)
                {
                    _logger.LogWarning(armEx, "Unable to query VM public IP configurations for subscription {SubscriptionId}", subscriptionId);
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceType = "Microsoft.Network/publicIPAddresses",
                        ResourceName = "Remote Access Controls",
                        FindingType = AtoFindingType.Security,
                        Severity = AtoFindingSeverity.High,
                        Title = "Unable to Query VM Public IP Configuration - Manual Review Required",
                        Description = $"Unable to automatically scan public IP exposure for {vms.Count} VMs (Error: {armEx.Message}). Manual review required.",
                        Recommendation = @"MANUAL REVIEW REQUIRED per AC-17:
1. For each VM, check for public IP addresses:
   - Azure Portal → Virtual Machines → [VM Name] → Networking
   - Check each network interface for public IP assignment
2. Verify Azure Bastion or VPN Gateway is deployed
3. Verify no VMs are directly accessible from Internet
4. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-17: Remote Access",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "AC-17" },
                        ComplianceFrameworks = new List<string> { "NIST-800-53" },
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                _logger.LogWarning("ARM client not available for subscription {SubscriptionId}", subscriptionId);
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Network/publicIPAddresses",
                    ResourceName = "Remote Access Controls",
                    FindingType = AtoFindingType.Security,
                    Severity = AtoFindingSeverity.High,
                    Title = "Unable to Query VM Public IP Configuration - ARM Client Unavailable",
                    Description = $"ARM client not available to scan public IP exposure for {vms.Count} VMs. Manual review required.",
                    Recommendation = @"MANUAL REVIEW REQUIRED per AC-17:
1. Check each VM for public IP addresses
2. Verify secure remote access solution is deployed
3. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 AC-17: Remote Access",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-17" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning remote access for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanUseOfExternalSystemsAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning use of external systems (AC-20) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);
                return findings;
            }

            // Check for external integrations that need governance
            var logicApps = resources.Where(r => r.Type?.Equals("Microsoft.Logic/workflows", StringComparison.OrdinalIgnoreCase) == true).ToList();
            var apiManagement = resources.Where(r => r.Type?.Equals("Microsoft.ApiManagement/service", StringComparison.OrdinalIgnoreCase) == true).ToList();
            var dataFactory = resources.Where(r => r.Type?.Equals("Microsoft.DataFactory/factories", StringComparison.OrdinalIgnoreCase) == true).ToList();
            var eventGrid = resources.Where(r => r.Type?.Equals("Microsoft.EventGrid/topics", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            var externalIntegrationResources = logicApps.Count + apiManagement.Count + dataFactory.Count + eventGrid.Count;
            
            if (externalIntegrationResources == 0)
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Logic/workflows",
                    ResourceName = "External System Integration",
                    FindingType = AtoFindingType.Security,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "No External System Integrations Detected",
                    Description = "No Logic Apps, API Management, Data Factory, or Event Grid resources found that integrate with external systems.",
                    Recommendation = "When integrating with external systems, implement controls per AC-20 requirements including authorization agreements and security assessments.",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "AC-20" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // Flag for manual review since we can't automatically validate authorization agreements
            var resourceDetails = new List<string>();
            if (logicApps.Any()) resourceDetails.Add($"{logicApps.Count} Logic App(s)");
            if (apiManagement.Any()) resourceDetails.Add($"{apiManagement.Count} API Management instance(s)");
            if (dataFactory.Any()) resourceDetails.Add($"{dataFactory.Count} Data Factory instance(s)");
            if (eventGrid.Any()) resourceDetails.Add($"{eventGrid.Count} Event Grid topic(s)");
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Logic/workflows",
                ResourceName = "External System Integration",
                FindingType = AtoFindingType.Compliance,
                Severity = AtoFindingSeverity.Medium,
                Title = "External System Integrations Require Governance Review",
                Description = $"Found {externalIntegrationResources} resources that may integrate with external systems: {string.Join(", ", resourceDetails)}. Per AC-20, all connections to external systems require authorization agreements, security assessments, and documented data flows.",
                Recommendation = @"MANUAL REVIEW REQUIRED per AC-20:
1. Inventory all external system connections:
   - Logic Apps: Review all connectors and workflow definitions
   - API Management: Review all backend APIs and external subscriptions
   - Data Factory: Review all linked services and external data sources
   - Event Grid: Review all external event subscriptions

2. For EACH external system connection, verify:
   - Interconnection Security Agreement (ISA) or Memorandum of Understanding (MOU) exists
   - External system has current Authorization to Operate (ATO) or equivalent
   - Data classification matches or exceeds classification of data being shared
   - Security controls of external system meet minimum requirements
   - Continuous monitoring of external system security posture

3. Document data flows in System Security Plan (SSP):
   - Source system → Data classification → Destination system
   - Type of data exchanged (PII, PHI, CUI, etc.)
   - Frequency and volume of data exchange
   - Security controls applied (encryption in transit/rest, access controls)
   - Approval authority and date of authorization

4. Implement technical controls for external connections:
   - Use Private Link/Private Endpoints where possible (eliminate Internet exposure)
   - Require mutual TLS authentication for API connections
   - Use Managed Identities instead of shared secrets
   - Implement API gateways with rate limiting and throttling
   - Enable logging of all external data transfers

5. Configure API Management policies (if using APIM):
   - Validate JWT tokens from external systems
   - Apply IP filtering to allow only known external IPs
   - Implement quota and rate limit policies
   - Enable request/response logging for audit
   - Use certificate-based authentication for backend services

6. Enable Logic Apps diagnostic logging:
   - Log all trigger events from external systems
   - Log all action executions and results
   - Send logs to Log Analytics with 90-day retention
   - Create alerts for failed external connections

7. Implement Azure Policy to detect new external connections:
   - Policy: Alert when Logic App connectors are created
   - Policy: Alert when API Management backends are added
   - Policy: Alert when Data Factory linked services are created
   - Require approval workflow before external connection is operationalized

8. Quarterly review of external system authorizations:
   - Verify ISA/MOU is still valid and current
   - Confirm external system ATO is still active
   - Review data flows for continued necessity
   - Update security assessments for changes in risk

9. Incident response for external system compromises:
   - Immediately disable Logic App/API Management connections
   - Review audit logs for data exfiltration
   - Notify external system owner of potential compromise
   - Follow incident response procedures in SSP

10. Document in SSP:
    - List of all authorized external systems with ISA references
    - Data flow diagrams showing external connections
    - Risk assessment for each external connection
    - Compensating controls for unavoidable risks
    - Approval authority and review schedule

REFERENCES:
- NIST 800-53 AC-20: Use of External Systems
- NIST 800-53 CA-3: Information Exchange
- DoD Cloud Computing SRG: External Connections Requirements
- CNSSI 1253: Security Categorization and Control Selection",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "AC-20", "CA-3", "SC-7(8)" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning use of external systems for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }
}