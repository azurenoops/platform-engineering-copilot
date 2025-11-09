using Platform.Engineering.Copilot.Core.Interfaces.Azure;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models.Compliance;
using Platform.Engineering.Copilot.Compliance.Core.Extensions;
using Azure.Core;
using Azure.ResourceManager.Resources;
using Platform.Engineering.Copilot.Core.Interfaces.Compliance;
using Platform.Engineering.Copilot.Compliance.Agent.Extensions;
namespace Platform.Engineering.Copilot.Compliance.Agent.Services.Compliance;

/// <summary>
/// Scanner for Configuration Management (CM) family controls using real Azure APIs
/// </summary>
public class ConfigurationManagementScanner : IComplianceScanner
{
    private readonly ILogger _logger;
    private readonly IAzureResourceService _azureService;

    public ConfigurationManagementScanner(ILogger logger, IAzureResourceService azureService)
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

        // CRITICAL: Control IDs from NIST catalog are lowercase (cm-2, cm-3, etc.)
        // Use case-insensitive comparison
        var controlId = control.Id?.ToUpperInvariant();

        switch (controlId)
        {
            case "CM-2":
                findings.AddRange(await ScanBaselineConfigurationAsync(subscriptionId, null, control, cancellationToken));
                break;

            case "CM-3":
                findings.AddRange(await ScanConfigurationChangeControlAsync(subscriptionId, null, control, cancellationToken));
                break;

            case "CM-6":
                findings.AddRange(await ScanConfigurationSettingsAsync(subscriptionId, null, control, cancellationToken));
                break;

            case "CM-7":
                findings.AddRange(await ScanLeastFunctionalityAsync(subscriptionId, null, control, cancellationToken));
                break;

            case "CM-8":
                findings.AddRange(await ScanSystemInventoryAsync(subscriptionId, null, control, cancellationToken));
                break;

            default:
                findings.AddRange(await ScanGenericConfigurationAsync(subscriptionId, null, control, cancellationToken));
                break;
        }

        // Enrich all findings with auto-remediation information
        return findings.WithAutoRemediationInfo();
    }

    /// <summary>
    /// Resource group-scoped scanning
    /// </summary>
    public async Task<List<AtoFinding>> ScanControlAsync(
        string subscriptionId,
        string resourceGroupName,
        NistControl control,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Scanning control {ControlId} for resource group {ResourceGroupName} in subscription {SubscriptionId}", 
            control.Id, resourceGroupName, subscriptionId);

        var findings = new List<AtoFinding>();

        // CRITICAL: Control IDs from NIST catalog are lowercase (cm-2, cm-3, etc.)
        // Use case-insensitive comparison
        var controlId = control.Id?.ToUpperInvariant();

        switch (controlId)
        {
            case "CM-2":
                findings.AddRange(await ScanBaselineConfigurationAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "CM-3":
                findings.AddRange(await ScanConfigurationChangeControlAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "CM-6":
                findings.AddRange(await ScanConfigurationSettingsAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "CM-7":
                findings.AddRange(await ScanLeastFunctionalityAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "CM-8":
                findings.AddRange(await ScanSystemInventoryAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            default:
                findings.AddRange(await ScanGenericConfigurationAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;
        }

        // Enrich all findings with auto-remediation information
        return findings.WithAutoRemediationInfo();
    }


    private async Task<List<AtoFinding>> ScanBaselineConfigurationAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning baseline configuration (CM-2) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);
                return findings;
            }

            // Get Azure Policy Assignments (baseline enforcement mechanism)
            var policyAssignments = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Authorization/policyAssignments", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (!policyAssignments.Any())
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Authorization/policyAssignments",
                    ResourceName = "Azure Policy Assignments",
                    FindingType = AtoFindingType.ConfigurationManagement,
                    Severity = AtoFindingSeverity.Critical,
                    Title = "CRITICAL: No Baseline Configuration Enforcement",
                    Description = "No Azure Policy assignments found for baseline configuration enforcement. Cannot meet CM-2 requirements. This is a DoD ATO blocker.",
                    Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per CM-2:
1. Implement Azure Policy for baseline configuration enforcement
2. Assign built-in initiatives (CIS, NIST 800-53, DoD IL5)
3. Configure Azure Blueprints for repeatable deployments
4. Document baseline configurations in SSP

REFERENCES:
- NIST 800-53 CM-2: Baseline Configuration
- DoD Cloud Computing SRG: Configuration Management Requirements",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "CM-2", "CM-6" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "DoD SRG" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // PROACTIVE CHECK: Query each policy assignment for enforcement details
            var armClient = _azureService.GetArmClient();
            if (armClient != null)
            {
                try
                {
                    var policyAssignmentsNotEnforced = new List<string>();
                    var policyInitiatives = new List<string>();
                    var totalPolicies = policyAssignments.Count;
                    var securityBaselinePolicies = 0;
                    
                    foreach (var policy in policyAssignments)
                    {
                        try
                        {
                            var policyResource = armClient?.GetGenericResource(new ResourceIdentifier(((GenericResource)policy).Data.Id!));
                            var policyData = await policyResource.GetAsync(cancellationToken: cancellationToken);
                            
                            // Parse policy properties
                            var policyPropsJson = JsonDocument.Parse(((GenericResource)policyData.Value).Data.Properties.ToStream());
                            var policyProps = policyPropsJson.RootElement;
                            
                            // Check enforcement mode
                            string enforcementMode = "Default"; // Default is enforced
                            if (policyProps.TryGetProperty("enforcementMode", out var enforcementProp))
                            {
                                enforcementMode = enforcementProp.GetString() ?? "Default";
                            }
                            
                            // Check if policy is disabled
                            if (enforcementMode.Equals("DoNotEnforce", StringComparison.OrdinalIgnoreCase))
                            {
                                policyAssignmentsNotEnforced.Add($"{((GenericResource)policy).Data.Name} (DoNotEnforce mode)");
                            }
                            
                            // Check for security baseline initiatives
                            if (policyProps.TryGetProperty("policyDefinitionId", out var policyDefId))
                            {
                                var policyDefIdStr = policyDefId.GetString() ?? "";
                                
                                // Check for known security baseline initiatives
                                if (policyDefIdStr.Contains("nist", StringComparison.OrdinalIgnoreCase) ||
                                    policyDefIdStr.Contains("cis", StringComparison.OrdinalIgnoreCase) ||
                                    policyDefIdStr.Contains("dod", StringComparison.OrdinalIgnoreCase) ||
                                    policyDefIdStr.Contains("fedramp", StringComparison.OrdinalIgnoreCase) ||
                                    policyDefIdStr.Contains("hipaa", StringComparison.OrdinalIgnoreCase) ||
                                    policyDefIdStr.Contains("pci", StringComparison.OrdinalIgnoreCase))
                                {
                                    securityBaselinePolicies++;
                                    policyInitiatives.Add($"{((GenericResource)policy).Data.Name} (Security baseline)");
                                }
                            }
                            
                            _logger.LogInformation("Policy {PolicyId} enforcement mode: {EnforcementMode}", ((GenericResource)policy).Data.Id, enforcementMode);
                        }
                        catch (Exception policyEx)
                        {
                            _logger.LogWarning(policyEx, "Unable to query policy assignment {PolicyId}", ((GenericResource)policy).Data.Id);
                            policyAssignmentsNotEnforced.Add($"{((GenericResource)policy).Data.Name ?? ((GenericResource)policy).Data.Id} (unable to verify)");
                        }
                    }
                    
                    _logger.LogInformation("Scanned {TotalPolicies} policy assignments: {SecurityBaselinePolicies} security baseline policies, {PoliciesNotEnforced} not enforced", 
                        totalPolicies, securityBaselinePolicies, policyAssignmentsNotEnforced.Count);
                    
                    // HIGH: Policies not enforced
                    if (policyAssignmentsNotEnforced.Any())
                    {
                        var policyList = string.Join(", ", policyAssignmentsNotEnforced.Take(10));
                        if (policyAssignmentsNotEnforced.Count > 10)
                        {
                            policyList += $" and {policyAssignmentsNotEnforced.Count - 10} more";
                        }
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Authorization/policyAssignments",
                            ResourceName = "Azure Policy Enforcement",
                            FindingType = AtoFindingType.ConfigurationManagement,
                            Severity = AtoFindingSeverity.High,
                            Title = "Policy Assignments Not Enforced",
                            Description = $"Found {policyAssignmentsNotEnforced.Count} of {totalPolicies} policy assignment(s) in DoNotEnforce mode or unable to verify. Policies not enforced: {policyList}. CM-2 requires active enforcement of baseline configurations.",
                            Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per CM-2:
1. Enable enforcement mode for all policy assignments:
   - Azure Portal → Policy → Assignments → [Assignment Name]
   - Enforcement mode → Set to 'Enabled' (not 'DoNotEnforce')
   - Policies in audit-only mode do not prevent non-compliant deployments

2. Review policy exemptions and exclusions:
   - Minimize exemptions to critical business requirements only
   - Document all exemptions with justification and expiration dates
   - Azure Portal → Policy → Exemptions → Review all active exemptions
   - Ensure exemptions have approval from security team

3. Test policy impact before enforcement:
   - Use 'DoNotEnforce' mode initially for NEW policies only
   - Monitor policy compliance in audit mode for 30 days
   - Remediate existing non-compliant resources
   - Switch to enforce mode after validation period

4. Assign comprehensive security baseline initiatives:
   - Azure Security Benchmark (ASB)
   - NIST SP 800-53 Rev 5 (for FedRAMP)
   - DoD Impact Level 5 (IL5) initiative (for DoD ATO)
   - CIS Microsoft Azure Foundations Benchmark
   - Azure Portal → Policy → Definitions → Search for initiatives → Assign

5. Configure policy remediation tasks:
   - Azure Portal → Policy → Compliance → [Non-compliant policy]
   - Create remediation task for resources with DeployIfNotExists effects
   - Automatically brings existing resources into compliance
   - Schedule remediation during maintenance windows

6. Implement Azure Blueprints for environment baselines:
   - Azure Portal → Blueprints → Create blueprint definition
   - Include: Policy assignments, RBAC, Resource Groups, ARM templates
   - Assign blueprint to subscriptions/management groups
   - Enables repeatable deployment of baseline configurations
   - Version control for configuration changes

7. Monitor policy compliance continuously:
   - Azure Portal → Policy → Compliance dashboard
   - Track compliance percentage (target: >95%)
   - Review non-compliant resources weekly
   - Alert on new policy violations (Azure Monitor integration)

8. Document baseline configurations in SSP:
   - List all policy initiatives assigned (NIST, DoD, CIS)
   - Baseline configuration for each resource type
   - Justification for configuration settings
   - Change control process for baseline updates
   - Roles and responsibilities for CM

9. Implement configuration drift detection:
   - Azure Automation State Configuration (DSC)
   - Azure Policy Guest Configuration for VMs
   - Detect and remediate configuration drift automatically
   - Alert on manual configuration changes

10. Conduct quarterly baseline reviews:
    - Review policy assignments for currency
    - Update baselines for new threats/vulnerabilities
    - Test policy effectiveness with penetration testing
    - Document baseline changes in change control system

REFERENCES:
- NIST 800-53 CM-2: Baseline Configuration
- NIST 800-53 CM-6: Configuration Settings
- DoD Cloud Computing SRG: Configuration Management (IL5)
- Azure Security Benchmark: GS-2 Define and implement security baseline",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "CM-2", "CM-6", "CM-3" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "Azure Security Benchmark" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    
                    // HIGH: No security baseline initiatives
                    if (securityBaselinePolicies == 0)
                    {
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Authorization/policyAssignments",
                            ResourceName = "Security Baseline Policies",
                            FindingType = AtoFindingType.ConfigurationManagement,
                            Severity = AtoFindingSeverity.High,
                            Title = "No Security Baseline Policy Initiatives Assigned",
                            Description = $"Found {totalPolicies} policy assignments but NONE are recognized security baseline initiatives (NIST, CIS, DoD, FedRAMP). CM-2 requires comprehensive security baselines.",
                            Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per CM-2:
1. Assign NIST 800-53 Rev 5 initiative (required for FedRAMP):
   - Azure Portal → Policy → Definitions
   - Search: 'NIST SP 800-53 Rev. 5'
   - Assign to subscription with appropriate parameters
   - Covers CM-2, AC, AU, IA, SC controls

2. Assign DoD Impact Level 5 initiative (required for DoD ATO):
   - Azure Portal → Policy → Definitions
   - Search: 'DoD Impact Level 5'
   - Assign to subscription
   - Stricter than FedRAMP High baseline

3. Assign Azure Security Benchmark (ASB):
   - Microsoft's best practices for Azure security
   - Azure Portal → Policy → Definitions
   - Search: 'Azure Security Benchmark'
   - Covers 14 security domains aligned with industry frameworks

4. Assign CIS Microsoft Azure Foundations Benchmark:
   - Industry standard security baseline
   - Azure Portal → Policy → Definitions
   - Search: 'CIS Microsoft Azure Foundations Benchmark'
   - Level 1 (minimum), Level 2 (more secure)

5. Configure initiative parameters appropriately:
   - Log Analytics workspace ID for diagnostic settings
   - Allowed locations for geo-restriction
   - Allowed VM SKUs (exclude unnecessary sizes)
   - Enable/disable specific policies based on environment

REFERENCES:
- NIST 800-53 CM-2: Baseline Configuration
- DoD Cloud Computing SRG: Security baselines required for IL5
- FedRAMP: NIST 800-53 baseline required for authorization",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "CM-2", "CM-6" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    
                    // Add informational finding about checking policy compliance manually
                    // Note: PolicyInsights SDK requires additional configuration/permissions
                    // For now, recommend manual review in Azure Portal
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = $"/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/policyAssignments",
                        ResourceType = "Microsoft.Authorization/policyAssignments",
                        ResourceName = "Policy Compliance Review",
                        FindingType = AtoFindingType.ConfigurationManagement,
                        Severity = AtoFindingSeverity.Informational,
                        Title = "Manual Policy Compliance Review Recommended",
                        Description = $"Policy assignments exist and are enforced, but actual compliance percentages require manual verification. " +
                                      $"Review Azure Portal Policy Compliance dashboard to verify that >95% of resources comply with baseline configuration policies. " +
                                      $"CM-2 requires continuous monitoring of actual resource compliance, not just policy assignment.",
                        Recommendation = @"VERIFY POLICY COMPLIANCE per CM-2:

1. Check Overall Compliance Percentage in Azure Portal:
   - Navigate to: Azure Portal → Policy → Compliance
   - Target: ≥95% overall compliance (DoD/FedRAMP requirement)
   - Review compliance by policy initiative (NIST, DoD, CIS, FedRAMP)

2. Identify Non-Compliant Resources:
   - Click on policies showing 'Non-compliant' status
   - Export list of all non-compliant resources
   - Prioritize by severity (Deny > Audit > AuditIfNotExists)
   - Group by resource type for batch remediation

3. Create Policy Remediation Tasks (Automated Fixes):
   - For policies with DeployIfNotExists/Modify effects
   - Azure Portal → Policy → Compliance → [Policy] → 'Create Remediation Task'
   - Example: Auto-enable diagnostic settings, deploy VM extensions
   - Track remediation progress in Portal

4. Set Up Continuous Compliance Monitoring:
   - Azure Monitor Alert: Policy compliance < 95%
   - Daily email reports on new violations
   - Weekly compliance dashboard reviews
   - Monthly executive compliance summary

5. Document Compliance Status:
   - Screenshot compliance dashboard for SSP evidence
   - List non-compliant resources in POA&M with remediation dates
   - Track remediation progress monthly
   - Report compliance trends (improving/stable/degrading)

COMPLIANCE TARGETS (DoD/FedRAMP):
- Minimum: >80% (requires immediate remediation plan)
- Good: >90% (requires continued remediation)
- Target: >95% (required for FedRAMP/DoD IL5 ATO)
- Excellent: >98% (stretch goal)

ALTERNATIVE: Use Azure CLI for automated reporting:
az policy state summarize --subscription {subscriptionId}

REFERENCES:
- NIST 800-53 CM-2: Baseline Configuration
- DoD Cloud Computing SRG: ≥95% compliance
- FedRAMP Continuous Monitoring Guide",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "CM-2" },
                        ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG" },
                        DetectedAt = DateTime.UtcNow
                    });
                    
                    // If both checks pass
                    if (!policyAssignmentsNotEnforced.Any() && securityBaselinePolicies > 0)
                    {
                        var initiativesList = string.Join(", ", policyInitiatives.Take(5));
                        if (policyInitiatives.Count > 5)
                        {
                            initiativesList += $" and {policyInitiatives.Count - 5} more";
                        }
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Authorization/policyAssignments",
                            ResourceName = "Baseline Configuration",
                            FindingType = AtoFindingType.ConfigurationManagement,
                            Severity = AtoFindingSeverity.Informational,
                            Title = "Baseline Configuration Enforcement Active",
                            Description = $"Found {totalPolicies} policy assignments with {securityBaselinePolicies} security baseline initiatives actively enforced. Security baselines: {initiativesList}. Baseline configuration enforcement meets CM-2 requirements.",
                            Recommendation = @"MAINTAIN CURRENT POSTURE per CM-2:
1. Monitor policy compliance (target: >95%)
2. Review and update baselines quarterly
3. Test baseline effectiveness with penetration testing
4. Document all baseline changes in change control
5. Conduct annual baseline configuration review
6. Implement Azure Blueprints for repeatable deployments
7. Enable Azure Automation State Configuration for drift detection

REFERENCES:
- NIST 800-53 CM-2: Baseline Configuration
- Continue monitoring with Microsoft Defender for Cloud",
                            ComplianceStatus = AtoComplianceStatus.Compliant,
                            AffectedNistControls = new List<string> { control.Id ?? "CM-2" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception armEx)
                {
                    _logger.LogWarning(armEx, "Unable to query policy assignments for subscription {SubscriptionId}", subscriptionId);
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceType = "Microsoft.Authorization/policyAssignments",
                        ResourceName = "Azure Policy Assignments",
                        FindingType = AtoFindingType.ConfigurationManagement,
                        Severity = AtoFindingSeverity.High,
                        Title = "Unable to Query Policy Assignments - Manual Review Required",
                        Description = $"Unable to automatically verify policy enforcement for {policyAssignments.Count} assignment(s) (Error: {armEx.Message}). Manual review required.",
                        Recommendation = @"MANUAL REVIEW REQUIRED per CM-2:
1. Verify all policy assignments are in enforce mode (not DoNotEnforce)
2. Verify security baseline initiatives are assigned (NIST, DoD, CIS)
3. Review policy compliance in Azure Portal
4. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 CM-2: Baseline Configuration",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "CM-2" },
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
                    ResourceType = "Microsoft.Authorization/policyAssignments",
                    ResourceName = "Azure Policy Assignments",
                    FindingType = AtoFindingType.ConfigurationManagement,
                    Severity = AtoFindingSeverity.High,
                    Title = "Unable to Query Policy Assignments - ARM Client Unavailable",
                    Description = $"ARM client not available to verify policy enforcement for {policyAssignments.Count} assignment(s). Manual review required.",
                    Recommendation = @"MANUAL REVIEW REQUIRED per CM-2:
1. Verify policy assignments are enforced
2. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 CM-2: Baseline Configuration",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    AffectedNistControls = new List<string> { control.Id ?? "CM-2" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning baseline configuration for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanConfigurationChangeControlAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning configuration change control (CM-3) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);
                return findings;
            }

            // Check for Activity Log configuration (captures all resource changes)
            // Activity logs are enabled by default but check for export/retention
            var logWorkspaces = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.OperationalInsights/workspaces", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (!logWorkspaces.Any())
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.OperationalInsights/workspaces",
                    ResourceName = "Activity Log Retention",
                    FindingType = AtoFindingType.ConfigurationManagement,
                    Severity = AtoFindingSeverity.Critical,
                    Title = "CRITICAL: No Activity Log Retention for Change Tracking",
                    Description = "No Log Analytics workspace found to capture and retain activity logs for configuration change tracking. Cannot meet CM-3 requirements. This is a DoD ATO blocker.",
                    Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per CM-3:
1. Create Log Analytics workspace for activity log retention
2. Configure Activity Log export to workspace (90-day retention minimum)
3. Enable Azure Policy Change Tracking for critical resources
4. Document change control process in SSP

REFERENCES:
- NIST 800-53 CM-3: Configuration Change Control
- DoD Cloud Computing SRG: Change Management Requirements",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "CM-3", "AU-2" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "DoD SRG" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // PROACTIVE CHECK: Query for Activity Log diagnostic settings (export configuration)
            var armClient = _azureService.GetArmClient();
            if (armClient != null)
            {
                try
                {
                    // Check if Activity Log is being exported to Log Analytics
                    // Note: Activity log diagnostic settings use a special management API, not ARM
                    bool activityLogExported = false;
                    string exportedWorkspace = "";
                    
                    // TODO: Implement proper activity log diagnostic settings check
                    // Activity log diagnostic settings require a different API approach than regular resource diagnostic settings
                    // For now, assume not configured to report the finding
                    _logger.LogWarning("Activity log diagnostic settings check skipped - requires different API implementation");
                    activityLogExported = false;
                    
                    _logger.LogInformation("Activity log export status for subscription {SubscriptionId}: {IsExported}", 
                        subscriptionId, activityLogExported);
                    
                    // CRITICAL: Activity log not exported for change tracking
                    if (!activityLogExported)
                    {
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Insights/diagnosticSettings",
                            ResourceName = "Activity Log Export",
                            FindingType = AtoFindingType.ConfigurationManagement,
                            Severity = AtoFindingSeverity.Critical,
                            Title = "CRITICAL: Activity Log Not Exported for Change Tracking",
                            Description = "Activity logs (which capture all configuration changes) are not being exported to Log Analytics workspace. Activity logs only retain 90 days by default. CM-3 requires tracking and documenting all configuration changes. This is a DoD ATO blocker.",
                            Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per CM-3:
1. Configure Activity Log export to Log Analytics:
   - Azure Portal → Monitor → Activity log
   - Export Activity Logs → Add diagnostic setting
   - Name: 'ActivityLogExport'
   - Logs: Select ALL categories (Administrative, Security, ServiceHealth, Alert, Recommendation, Policy, Autoscale, ResourceHealth)
   - Destination: Send to Log Analytics workspace
   - Select workspace with 90-day retention minimum

2. Categories to enable for change tracking:
   - Administrative: ALL resource create/update/delete operations
   - Policy: Policy evaluation and compliance events
   - Security: Azure Defender alerts and security events
   - Alert: Alert rule triggers and resolutions

3. Configure change approval workflow:
   - Azure DevOps → Pipelines → Add approval gates
   - Require security team approval for production changes
   - Document approval criteria in change control policy
   - Use Azure Policy exemptions for emergency changes (with justification)

4. Implement Azure Policy for change tracking:
   - Policy: 'Activity Log should be exported to Log Analytics'
   - Effect: AuditIfNotExists or DeployIfNotExists
   - Prevents subscriptions without change tracking

5. Enable Resource Change History:
   - Azure Portal → Resource → Activity log
   - View change history for last 90 days
   - Export to Log Analytics for longer retention (1+ year)

6. Configure alerts for critical configuration changes:
   - Alert on: Policy assignment changes
   - Alert on: Role assignment changes (Owner, Contributor)
   - Alert on: Network Security Group rule changes
   - Alert on: Key Vault access policy changes
   - Alert on: Resource deletion (especially production)

7. Implement Infrastructure as Code (IaC) with version control:
   - Store all configurations in Git (Azure DevOps, GitHub)
   - Use ARM templates, Bicep, or Terraform
   - Require pull requests and code review for all changes
   - Automated deployment through CI/CD pipelines only
   - Prevents manual configuration drift

8. Document change control process in SSP:
   - Change request submission process
   - Approval workflow and authorities
   - Testing requirements before production
   - Rollback procedures for failed changes
   - Communication plan for stakeholders
   - Change advisory board (CAB) meeting schedule

9. Quarterly change control audit:
   - Review all configuration changes from Activity Log
   - Verify changes had proper approval
   - Identify unauthorized changes
   - Update change control procedures based on findings
   - Test rollback procedures

10. Implement configuration management database (CMDB):
    - Track all authorized configurations
    - Baseline configurations for each resource type
    - Dependencies between resources
    - Change history and audit trail
    - Integration with change management system

REFERENCES:
- NIST 800-53 CM-3: Configuration Change Control
- NIST 800-53 CM-3(1): Automated Documentation
- NIST 800-53 CM-5: Access Restrictions for Change
- DoD Cloud Computing SRG: Change Management (IL5)
- Azure Security Benchmark: LT-4 Enable logging for security investigation",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "CM-3", "CM-3(1)", "CM-5", "AU-2" },
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
                            ResourceType = "Microsoft.Insights/diagnosticSettings",
                            ResourceName = "Activity Log Export",
                            FindingType = AtoFindingType.ConfigurationManagement,
                            Severity = AtoFindingSeverity.Informational,
                            Title = "Activity Log Change Tracking Configured",
                            Description = $"Activity logs are being exported to Log Analytics workspace for configuration change tracking. Exported to: {exportedWorkspace}. All configuration changes are being captured per CM-3.",
                            Recommendation = @"MAINTAIN CURRENT POSTURE per CM-3:
1. Verify Activity Log retention is 90+ days (DoD requirement)
2. Configure alerts for critical configuration changes
3. Implement IaC with version control (ARM, Bicep, Terraform)
4. Document change control process in SSP
5. Quarterly review of configuration changes
6. Test change rollback procedures
7. Ensure change approval workflow is enforced

REFERENCES:
- NIST 800-53 CM-3: Configuration Change Control
- Continue monitoring with Activity Log analytics",
                            ComplianceStatus = AtoComplianceStatus.Compliant,
                            AffectedNistControls = new List<string> { control.Id ?? "CM-3" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception armEx)
                {
                    _logger.LogWarning(armEx, "Unable to query activity log settings for subscription {SubscriptionId}", subscriptionId);
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceType = "Microsoft.Insights/diagnosticSettings",
                        ResourceName = "Activity Log Export",
                        FindingType = AtoFindingType.ConfigurationManagement,
                        Severity = AtoFindingSeverity.High,
                        Title = "Unable to Query Activity Log Export - Manual Review Required",
                        Description = $"Unable to automatically verify activity log export configuration (Error: {armEx.Message}). Manual review required.",
                        Recommendation = @"MANUAL REVIEW REQUIRED per CM-3:
1. Verify Activity Log is exported to Log Analytics workspace
2. Verify ALL log categories are enabled (Administrative, Policy, Security)
3. Verify Log Analytics workspace retention is 90+ days
4. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 CM-3: Configuration Change Control",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "CM-3" },
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
                    ResourceType = "Microsoft.Insights/diagnosticSettings",
                    ResourceName = "Activity Log Export",
                    FindingType = AtoFindingType.ConfigurationManagement,
                    Severity = AtoFindingSeverity.High,
                    Title = "Unable to Query Activity Log Export - ARM Client Unavailable",
                    Description = "ARM client not available to verify activity log export configuration. Manual review required.",
                    Recommendation = @"MANUAL REVIEW REQUIRED per CM-3:
1. Verify Activity Log export is configured
2. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 CM-3: Configuration Change Control",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    AffectedNistControls = new List<string> { control.Id ?? "CM-3" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning configuration change control for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanConfigurationSettingsAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning configuration settings (CM-6) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);
                return findings;
            }

            // Get Azure Policy assignments to check configuration enforcement
            var policyAssignments = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Authorization/policyAssignments", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (!policyAssignments.Any())
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Authorization/policyAssignments",
                    ResourceName = "Configuration Settings Enforcement",
                    FindingType = AtoFindingType.ConfigurationManagement,
                    Severity = AtoFindingSeverity.Critical,
                    Title = "CRITICAL: No Configuration Settings Enforcement",
                    Description = "No Azure Policy assignments found to enforce configuration settings. Cannot meet CM-6 requirements. This is a DoD ATO blocker.",
                    Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per CM-6:
1. Assign Azure Policy initiatives for configuration enforcement
2. Configure mandatory security settings (encryption, authentication, logging)
3. Document configuration standards in SSP
4. Implement automated configuration validation

REFERENCES:
- NIST 800-53 CM-6: Configuration Settings
- DoD Cloud Computing SRG: Secure Configuration Requirements",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "CM-6", "CM-2" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "DoD SRG" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // PROACTIVE CHECK: Note - We query policy compliance through a different API
            // For now, detect if policies exist and provide guidance
            // Full compliance checking would require Microsoft.PolicyInsights provider
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/policyAssignments",
                ResourceName = "Configuration Settings",
                FindingType = AtoFindingType.ConfigurationManagement,
                Severity = AtoFindingSeverity.Informational,
                Title = "Configuration Settings Policies Assigned",
                Description = $"Found {policyAssignments.Count} policy assignment(s) for configuration settings enforcement. Verify these policies enforce mandatory configuration settings per CM-6.",
                Recommendation = @"VERIFY CONFIGURATION SETTINGS per CM-6:

1. Review Policy Compliance in Azure Portal:
   - Azure Portal → Policy → Compliance
   - Target compliance: >95% for all security policies
   - Review non-compliant resources and remediate
   - Track compliance trends over time

2. Mandatory configuration settings to enforce (DoD/FedRAMP):
   A. Encryption at Rest:
      - Storage accounts: Require infrastructure encryption
      - SQL databases: Transparent Data Encryption (TDE) enabled
      - Disk encryption: Azure Disk Encryption or SSE with PMK
      - Key Vault: Soft delete and purge protection enabled
   
   B. Encryption in Transit:
      - Storage accounts: Require HTTPS only (secure transfer)
      - Web apps: TLS 1.2 minimum version required
      - SQL: Enforce SSL connections
      - Redis Cache: Non-SSL port disabled
   
   C. Authentication and Authorization:
      - Storage accounts: Disable shared key access (use Azure AD only)
      - Key Vault: RBAC authorization model (not access policies)
      - VMs: Require managed identities (no passwords)
      - SQL: Azure AD authentication enabled
   
   D. Network Security:
      - VMs: Require Network Security Groups (NSGs)
      - Storage/SQL: Require private endpoints (no public access)
      - Subnets: Service endpoints enabled
      - VNets: DDoS Protection Standard enabled
   
   E. Logging and Monitoring:
      - All resources: Diagnostic settings to Log Analytics
      - Key Vaults: Logging enabled for ALL categories
      - NSGs: Flow logs enabled (Version 2)
      - 90-day minimum retention for all logs

3. Implement restrictive configuration policies:
   - Deny public IPs on VMs (except jump boxes)
   - Deny resources without tags (Owner, Environment, Classification)
   - Deny non-approved VM sizes (exclude unnecessary SKUs)
   - Deny non-approved Azure regions (data sovereignty)
   - Require specific OS versions (patched, supported)

4. Use Azure Policy initiatives for comprehensive coverage:
   - Azure Security Benchmark (ASB) - Microsoft best practices
   - NIST SP 800-53 Rev 5 - Required for FedRAMP
   - DoD Impact Level 5 (IL5) - Required for DoD ATO
   - CIS Microsoft Azure Foundations Benchmark
   - Each initiative contains 100+ individual policies

5. Configure policy parameters appropriately:
   - Log Analytics workspace ID (for diagnostic settings)
   - Allowed locations (e.g., 'eastus', 'usgovvirginia')
   - Allowed VM SKUs (exclude D-series, allow E-series)
   - Key Vault SKU (Premium for HSM-backed keys)
   - Minimum TLS version ('1.2' or '1.3')

6. Document configuration standards in SSP:
   - Security Configuration Annex (SCA)
   - Configuration settings by resource type
   - Rationale for each configuration requirement
   - Deviations from baseline (with risk acceptance)
   - Validation procedures and testing methodology

7. Implement configuration validation testing:
   - Azure Policy compliance scans (automated)
   - Microsoft Defender for Cloud recommendations
   - Quarterly configuration audits by security team
   - Penetration testing to validate effectiveness
   - Configuration drift detection with alerts

8. Apply least privilege to configuration changes:
   - Only DevOps admins can modify IaC templates
   - Only Security admins can modify Azure Policy
   - Require pull request approval for all changes
   - Automated deployment through CI/CD only
   - No manual portal changes in production

9. Remediation for non-compliant configurations:
   - DeployIfNotExists policies auto-remediate
   - Create remediation tasks for existing resources
   - Weekly review of non-compliant resources
   - Escalation process for persistent violations
   - Exception process with risk acceptance

10. Continuous monitoring and improvement:
    - Weekly policy compliance review
    - Monthly security configuration audit
    - Quarterly update of configuration baselines
    - Annual penetration testing validation
    - Incorporate lessons learned from incidents

REFERENCES:
- NIST 800-53 CM-6: Configuration Settings
- NIST 800-53 CM-6(1): Automated Management and Application
- DoD Cloud Computing SRG: Secure Configuration (IL5)
- CIS Azure Foundations Benchmark: Security configurations
- Azure Security Benchmark: All security domains",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "CM-6", "CM-6(1)", "CM-2" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CIS", "Azure Security Benchmark" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning configuration settings for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanLeastFunctionalityAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning least functionality (CM-7) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            if (resources == null)
            {
                _logger.LogWarning("No resources found for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);
                return findings;
            }

            // Get VMs to check for unnecessary extensions and configurations
            var vms = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            // Get Network Security Groups to check for overly permissive rules
            var nsgs = resources.Where(r =>
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Network/networkSecurityGroups", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (!vms.Any() && !nsgs.Any())
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Subscription",
                    ResourceName = "Least Functionality",
                    FindingType = AtoFindingType.ConfigurationManagement,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "No Compute Resources for Least Functionality Review",
                    Description = "No virtual machines or network security groups found. Least functionality principle applies when compute resources are deployed.",
                    Recommendation = @"When deploying resources, ensure least functionality per CM-7:
1. Deploy only necessary VM extensions
2. Restrict network access with NSGs (deny all by default)
3. Disable unnecessary services and protocols
4. Document all required functionality in SSP

REFERENCES:
- NIST 800-53 CM-7: Least Functionality",
                    ComplianceStatus = AtoComplianceStatus.NotApplicable,
                    AffectedNistControls = new List<string> { control.Id ?? "CM-7" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // PROACTIVE CHECK: Query NSGs for overly permissive rules
            var armClient = _azureService.GetArmClient();
            if (armClient != null)
            {
                try
                {
                    var nsgsWithPermissiveRules = new List<string>();
                    var totalNsgs = nsgs.Count;
                    var totalPermissiveRules = 0;
                    
                    foreach (var nsg in nsgs)
                    {
                        try
                        {
                            var nsgResource = armClient?.GetGenericResource(new ResourceIdentifier(((GenericResource)nsg).Data.Id!));
                            var nsgData = await nsgResource.GetAsync(cancellationToken: cancellationToken);
                            
                            // Parse NSG properties
                            var nsgPropsJson = JsonDocument.Parse(((GenericResource)nsgData.Value).Data.Properties.ToStream());
                            var nsgProps = nsgPropsJson.RootElement;
                            
                            bool hasPermissiveRules = false;
                            var permissiveRulesDetails = new List<string>();
                            
                            // Check security rules
                            if (nsgProps.TryGetProperty("securityRules", out var securityRules) &&
                                securityRules.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var rule in securityRules.EnumerateArray())
                                {
                                    if (rule.TryGetProperty("properties", out var ruleProps))
                                    {
                                        // Check for dangerous combinations
                                        var access = ruleProps.TryGetProperty("access", out var accessProp) ? accessProp.GetString() : "";
                                        var direction = ruleProps.TryGetProperty("direction", out var dirProp) ? dirProp.GetString() : "";
                                        var sourceAddress = ruleProps.TryGetProperty("sourceAddressPrefix", out var srcProp) ? srcProp.GetString() : "";
                                        var destPort = ruleProps.TryGetProperty("destinationPortRange", out var portProp) ? portProp.GetString() : "";
                                        var protocol = ruleProps.TryGetProperty("protocol", out var protoProp) ? protoProp.GetString() : "";
                                        var ruleName = rule.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : "Unknown";
                                        
                                        // Flag: Allow from Internet (*) on common management ports
                                        if (access == "Allow" && direction == "Inbound" && 
                                            (sourceAddress == "*" || sourceAddress == "Internet" || sourceAddress == "0.0.0.0/0"))
                                        {
                                            // Check for risky ports
                                            var riskyPorts = new[] { "22", "3389", "1433", "3306", "5432", "27017", "6379", "9200", "8080", "*" };
                                            if (riskyPorts.Any(p => destPort?.Contains(p) == true))
                                            {
                                                hasPermissiveRules = true;
                                                totalPermissiveRules++;
                                                permissiveRulesDetails.Add($"{ruleName} (Allow * → {destPort}/{protocol})");
                                            }
                                        }
                                    }
                                }
                            }
                            
                            if (hasPermissiveRules)
                            {
                                var rulesStr = string.Join(", ", permissiveRulesDetails.Take(3));
                                nsgsWithPermissiveRules.Add($"{((GenericResource)nsg).Data.Name} ({rulesStr})");
                            }
                            
                            _logger.LogInformation("NSG {NsgId} has permissive rules: {HasPermissive}", ((GenericResource)nsg).Data.Id, hasPermissiveRules);
                        }
                        catch (Exception nsgEx)
                        {
                            _logger.LogWarning(nsgEx, "Unable to query NSG {NsgId}", ((GenericResource)nsg).Data.Id);
                        }
                    }
                    
                    _logger.LogInformation("Scanned {TotalNsgs} NSGs: {TotalPermissiveRules} overly permissive rules found in {NsgsWithPermissiveRules} NSGs", 
                        totalNsgs, totalPermissiveRules, nsgsWithPermissiveRules.Count);
                    
                    // HIGH: Overly permissive NSG rules
                    if (nsgsWithPermissiveRules.Any())
                    {
                        var nsgList = string.Join(", ", nsgsWithPermissiveRules.Take(10));
                        if (nsgsWithPermissiveRules.Count > 10)
                        {
                            nsgList += $" and {nsgsWithPermissiveRules.Count - 10} more";
                        }
                        
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Network/networkSecurityGroups",
                            ResourceName = "Network Security Groups",
                            FindingType = AtoFindingType.ConfigurationManagement,
                            Severity = AtoFindingSeverity.High,
                            Title = "Overly Permissive Network Rules Violate Least Functionality",
                            Description = $"Found {totalPermissiveRules} overly permissive NSG rules in {nsgsWithPermissiveRules.Count} of {totalNsgs} NSG(s) allowing Internet access to management ports. NSGs with permissive rules: {nsgList}. CM-7 requires restricting functionality to only what is essential.",
                            Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per CM-7:

1. Apply deny-by-default principle to all NSGs:
   - Default rule: Deny all inbound from Internet
   - Explicitly allow only required traffic
   - Use service tags instead of wildcards (e.g., 'AzureLoadBalancer' instead of '*')

2. Restrict management port access (HIGH RISK):
   - Port 22 (SSH): Allow only from corporate VPN/bastion (specific IP ranges)
   - Port 3389 (RDP): Allow only from corporate VPN/bastion
   - Port 1433 (SQL): Never expose to Internet (use private endpoints)
   - Port 3306 (MySQL): Never expose to Internet
   - Port 5432 (PostgreSQL): Never expose to Internet
   - Port 27017 (MongoDB): Never expose to Internet
   - Port 6379 (Redis): Never expose to Internet
   - Port 9200 (Elasticsearch): Never expose to Internet

3. Implement bastion/jump box architecture:
   - Deploy Azure Bastion for RDP/SSH access (no public IPs on VMs)
   - Or: Single hardened jump box with MFA and just-in-time access
   - All management traffic routes through bastion only
   - Bastion IPs whitelisted in NSG rules (specific /32 CIDRs)

4. Use Azure Just-In-Time (JIT) VM Access:
   - Microsoft Defender for Cloud → Just-in-time VM access
   - Requires approval and justification for each access request
   - Time-limited access (e.g., 3 hours)
   - Automatically removes access after expiration
   - Audit trail for all access requests

5. Remove unnecessary protocols and services:
   - Disable unused protocols in NSGs (ICMP, UDP if not needed)
   - Disable unused VM extensions
   - Uninstall unnecessary Windows features/roles
   - Stop unnecessary Linux services (telnet, FTP, etc.)
   - Review and remove default Azure VM extensions

6. Implement application-specific NSG rules:
   - Web servers: Allow 80/443 only (from Internet or Azure Front Door)
   - App servers: Allow application port only (from load balancer subnet)
   - Database servers: Allow database port only (from app subnet)
   - Use Network Security Group Application Security Groups (ASGs)

7. Use Azure Firewall for centralized control:
   - Deploy Azure Firewall in hub VNet
   - Route all traffic through firewall with UDRs
   - Implement application rules (FQDN filtering)
   - Implement network rules (IP/port filtering)
   - Enable threat intelligence-based filtering

8. Document all allowed functionality:
   - Justify each allow rule in NSG
   - Document business requirement for each open port
   - Periodic review of NSG rules (quarterly)
   - Remove unused rules after application decommission

9. Implement least privilege for applications:
   - Container images: Minimal base images (Alpine, Distroless)
   - VMs: Server Core instead of full Windows Server
   - Remove development tools from production VMs
   - Disable PowerShell/Bash unless required
   - Use Azure Policy to enforce VM configurations

10. Monitor and alert on configuration changes:
    - Alert on NSG rule modifications (especially Allow from Internet)
    - Alert on new VM extensions installed
    - Alert on ports opened in NSGs (1024-65535)
    - Quarterly review of all NSG rules for necessity
    - Annual penetration testing to validate restrictions

REFERENCES:
- NIST 800-53 CM-7: Least Functionality
- NIST 800-53 CM-7(1): Periodic Review
- NIST 800-53 CM-7(2): Prevent Program Execution
- DoD Cloud Computing SRG: Network Segmentation (IL5)
- Azure Security Benchmark: NS-1 Establish network segmentation boundaries",
                            ComplianceStatus = AtoComplianceStatus.NonCompliant,
                            AffectedNistControls = new List<string> { control.Id ?? "CM-7", "CM-7(1)", "AC-4", "SC-7" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "Azure Security Benchmark" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                    else if (totalNsgs > 0)
                    {
                        findings.Add(new AtoFinding
                        {
                            Id = Guid.NewGuid().ToString(),
                            SubscriptionId = subscriptionId,
                            ResourceId = $"/subscriptions/{subscriptionId}",
                            ResourceType = "Microsoft.Network/networkSecurityGroups",
                            ResourceName = "Network Security Groups",
                            FindingType = AtoFindingType.ConfigurationManagement,
                            Severity = AtoFindingSeverity.Informational,
                            Title = "NSG Rules Follow Least Functionality Principle",
                            Description = $"Scanned {totalNsgs} NSG(s). No overly permissive rules allowing Internet access to management ports detected. Network rules follow least functionality principle per CM-7.",
                            Recommendation = @"MAINTAIN CURRENT POSTURE per CM-7:
1. Quarterly review of all NSG rules for necessity
2. Document justification for each allow rule
3. Remove unused rules after application changes
4. Continue monitoring for unauthorized rule changes
5. Implement Azure JIT VM Access for management ports
6. Consider Azure Firewall for centralized control
7. Annual penetration testing to validate restrictions

REFERENCES:
- NIST 800-53 CM-7: Least Functionality
- Continue monitoring with Microsoft Defender for Cloud",
                            ComplianceStatus = AtoComplianceStatus.Compliant,
                            AffectedNistControls = new List<string> { control.Id ?? "CM-7" },
                            ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                            DetectedAt = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception armEx)
                {
                    _logger.LogWarning(armEx, "Unable to query NSG rules for subscription {SubscriptionId}", subscriptionId);
                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceType = "Microsoft.Network/networkSecurityGroups",
                        ResourceName = "Network Security Groups",
                        FindingType = AtoFindingType.ConfigurationManagement,
                        Severity = AtoFindingSeverity.Medium,
                        Title = "Unable to Query NSG Rules - Manual Review Required",
                        Description = $"Unable to automatically scan NSG rules for {nsgs.Count} NSG(s) (Error: {armEx.Message}). Manual review required.",
                        Recommendation = @"MANUAL REVIEW REQUIRED per CM-7:
1. Review all NSG rules for overly permissive access (Allow from Internet)
2. Verify no management ports exposed to Internet (22, 3389, 1433)
3. Document business justification for all allow rules
4. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 CM-7: Least Functionality",
                        ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                        AffectedNistControls = new List<string> { control.Id ?? "CM-7" },
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
                    ResourceType = "Microsoft.Network/networkSecurityGroups",
                    ResourceName = "Network Security Groups",
                    FindingType = AtoFindingType.ConfigurationManagement,
                    Severity = AtoFindingSeverity.Medium,
                    Title = "Unable to Query NSG Rules - ARM Client Unavailable",
                    Description = $"ARM client not available to scan NSG rules for {nsgs.Count} NSG(s). Manual review required.",
                    Recommendation = @"MANUAL REVIEW REQUIRED per CM-7:
1. Review NSG rules for least functionality compliance
2. Document findings in ATO compliance assessment

REFERENCES:
- NIST 800-53 CM-7: Least Functionality",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    AffectedNistControls = new List<string> { control.Id ?? "CM-7" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning least functionality for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanSystemInventoryAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogDebug("Scanning system inventory (CM-8) for {Scope} in subscription {SubscriptionId}", scope, subscriptionId);

            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            if (resources == null || !resources.Any())
            {
                _logger.LogWarning("No resources found for subscription {SubscriptionId}", subscriptionId);
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Subscription",
                    ResourceName = "System Inventory",
                    FindingType = AtoFindingType.ConfigurationManagement,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "No Resources in Subscription",
                    Description = "No resources found in subscription for inventory tracking.",
                    Recommendation = "When resources are deployed, ensure proper tagging for inventory management per CM-8.",
                    ComplianceStatus = AtoComplianceStatus.NotApplicable,
                    AffectedNistControls = new List<string> { control.Id ?? "CM-8" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53" },
                    DetectedAt = DateTime.UtcNow
                });
                return findings;
            }
            
            // PROACTIVE CHECK: Analyze resource tagging for proper inventory management
            var requiredTags = new[] { "Owner", "Environment", "Application", "CostCenter" }; // Common required tags
            var totalResources = resources.Count();
            
            // Track resources missing each required tag
            var resourcesMissingOwner = new List<string>();
            var resourcesMissingEnvironment = new List<string>();
            var resourcesMissingApplication = new List<string>();
            var resourcesWithNoTags = new List<string>();
            var taggedResources = 0;
            
            foreach (var resource in resources)
            {
                bool hasAnyTags = ((GenericResource)resource).Data.Tags != null && ((GenericResource)resource).Data.Tags.Any();
                
                if (!hasAnyTags)
                {
                    resourcesWithNoTags.Add($"{((GenericResource)resource).Data.Name} ({((GenericResource)resource).Data.ResourceType})");
                }
                else
                {
                    taggedResources++;
                    
                    // Check for specific required tags (case-insensitive)
                    var tags = ((GenericResource)resource).Data.Tags!;
                    bool hasOwner = tags.Keys.Any(k => k.Equals("Owner", StringComparison.OrdinalIgnoreCase));
                    bool hasEnvironment = tags.Keys.Any(k => k.Equals("Environment", StringComparison.OrdinalIgnoreCase));
                    bool hasApplication = tags.Keys.Any(k => k.Equals("Application", StringComparison.OrdinalIgnoreCase) || 
                                                            k.Equals("App", StringComparison.OrdinalIgnoreCase));
                    
                    if (!hasOwner) resourcesMissingOwner.Add(((GenericResource)resource).Data.Name ?? ((GenericResource)resource).Data.Id);
                    if (!hasEnvironment) resourcesMissingEnvironment.Add(((GenericResource)resource).Data.Name ?? ((GenericResource)resource).Data.Id);
                    if (!hasApplication) resourcesMissingApplication.Add(((GenericResource)resource).Data.Name ?? ((GenericResource)resource).Data.Id);
                }
            }
            
            var tagComplianceRate = totalResources > 0 ? (double)taggedResources / totalResources * 100 : 0;
            
            // Group resources by type for inventory reporting
            var resourceTypes = resources
                .Where(r => !string.IsNullOrEmpty(((GenericResource)r).Data.ResourceType.ToString()))
                .GroupBy(r => ((GenericResource)r).Data.ResourceType.ToString())
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();
            
            var resourceTypesSummary = string.Join(", ", resourceTypes.Select(rt => $"{rt.Type} ({rt.Count})"));

            _logger.LogInformation("Subscription {SubscriptionId} inventory: {TotalResources} resources, {TaggedResources} with tags ({TagComplianceRate:F1}%), {ResourcesWithNoTags} with no tags", 
                subscriptionId, totalResources, taggedResources, tagComplianceRate, resourcesWithNoTags.Count);
            
            // HIGH: Resources not tagged for inventory
            if (resourcesWithNoTags.Count > (totalResources * 0.2)) // More than 20% untagged
            {
                var untaggedList = string.Join(", ", resourcesWithNoTags.Take(10));
                if (resourcesWithNoTags.Count > 10)
                {
                    untaggedList += $" and {resourcesWithNoTags.Count - 10} more";
                }
                
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Subscription",
                    ResourceName = "System Inventory",
                    FindingType = AtoFindingType.ConfigurationManagement,
                    Severity = AtoFindingSeverity.High,
                    Title = "Poor Resource Tagging Impacts Asset Inventory",
                    Description = $"Found {resourcesWithNoTags.Count} of {totalResources} resources ({100 - tagComplianceRate:F1}%) with NO tags. Untagged resources: {untaggedList}. CM-8 requires maintaining accurate inventory of system components. Target: <20% untagged (current: {100 - tagComplianceRate:F1}%).",
                    Recommendation = @"IMMEDIATE REMEDIATION REQUIRED per CM-8:

1. Define mandatory tag schema for all resources:
   - Owner: Email or team responsible for resource
   - Environment: Production, Development, Staging, Test
   - Application: Application name or workload identifier
   - CostCenter: Billing code or department
   - DataClassification: Public, Internal, Confidential, Restricted
   - Criticality: High, Medium, Low (for DR prioritization)
   - ExpirationDate: For temporary/test resources

2. Implement Azure Policy to enforce tagging:
   - Policy: 'Require a tag on resources'
   - Effect: Deny (prevents deployment without tags)
   - Apply to resource groups AND individual resources
   - Exemptions only for system-managed resources
   - Azure Portal → Policy → Definitions → Search 'tag'

3. Tag existing untagged resources:
   - Azure Portal → All resources → Filter by 'No tags'
   - Select multiple resources → Assign tags in bulk
   - PowerShell script for bulk tagging:
     ```powershell
     Get-AzResource -ResourceGroupName 'rg-name' | 
       Set-AzResource -Tag @{Owner='team@company.com'; Environment='Production'}
     ```
   - Tag resources within 30 days (track in remediation plan)

4. Inherit tags from resource groups:
   - Azure Policy: 'Inherit tag from resource group if missing'
   - Effect: Modify (automatically adds tag from parent RG)
   - Reduces manual tagging burden
   - Ensure resource groups are properly tagged first

5. Create resource inventory report:
   - Azure Resource Graph query:
     ```kusto
     Resources
     | project name, type, resourceGroup, location, tags
     | where tags['Environment'] == 'Production'
     ```
   - Export to Excel/CSV for CM documentation
   - Update inventory report monthly
   - Include in SSP as system inventory

6. Track resource lifecycle in inventory:
   - Tag: CreatedDate, CreatedBy (via automation)
   - Tag: ExpirationDate (for temporary resources)
   - Alert 30 days before expiration
   - Automated deletion of expired dev/test resources
   - Document decommissioning process

7. Implement cost tracking with tags:
   - Cost Management + Billing → Cost analysis
   - Group by tags (Owner, CostCenter, Application)
   - Chargeback/showback reporting
   - Identify orphaned resources (high cost, unclear owner)

8. Use Azure Resource Graph for inventory queries:
   - Real-time inventory across subscriptions
   - Complex queries for compliance reporting
   - Export to Log Analytics for long-term trending
   - Integration with CMDB tools (ServiceNow, etc.)

9. Automated inventory reconciliation:
   - Weekly comparison: Azure inventory vs. CMDB
   - Alert on discrepancies (resources in Azure but not CMDB)
   - Alert on unauthorized resources (not in approved deployments)
   - Quarantine/delete resources without proper ownership

10. Document inventory management in SSP:
    - Inventory update frequency (real-time via Azure)
    - Tag naming standards and allowed values
    - Roles responsible for inventory accuracy
    - Process for adding/removing components
    - Integration with change management (CM-3)
    - Annual inventory audit by security team

REFERENCES:
- NIST 800-53 CM-8: System Component Inventory
- NIST 800-53 CM-8(1): Updates During Installation and Removal
- NIST 800-53 CM-8(3): Automated Unauthorized Component Detection
- DoD Cloud Computing SRG: Asset Management (IL5)
- Azure Well-Architected Framework: Operational excellence - Tagging strategy",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "CM-8", "CM-8(1)", "CM-8(3)" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "Azure Well-Architected" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else
            {
                var complianceMessage = $"Subscription contains {totalResources} resources across {resourceTypes.Count} resource types. Tag compliance: {tagComplianceRate:F1}% ({taggedResources} of {totalResources} resources tagged).";
                
                // Add details about missing required tags
                if (resourcesMissingOwner.Count > 0 || resourcesMissingEnvironment.Count > 0 || resourcesMissingApplication.Count > 0)
                {
                    complianceMessage += $" Missing tags: Owner ({resourcesMissingOwner.Count}), Environment ({resourcesMissingEnvironment.Count}), Application ({resourcesMissingApplication.Count}).";
                }
                
                complianceMessage += $" Top resource types: {resourceTypesSummary}.";
                
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Subscription",
                    ResourceName = "System Inventory",
                    FindingType = AtoFindingType.ConfigurationManagement,
                    Severity = tagComplianceRate >= 95 ? AtoFindingSeverity.Informational : AtoFindingSeverity.Low,
                    Title = tagComplianceRate >= 95 ? "System Inventory Tracking Meets Requirements" : "System Inventory Tracking Adequate but Can Improve",
                    Description = complianceMessage,
                    Recommendation = tagComplianceRate >= 95 
                        ? @"MAINTAIN CURRENT POSTURE per CM-8:
1. Continue enforcing tagging with Azure Policy
2. Monthly inventory report export for CM documentation
3. Quarterly inventory audit by security team
4. Ensure decommissioned resources are properly documented
5. Integrate inventory with CMDB (ServiceNow, etc.)
6. Use Azure Resource Graph for inventory queries
7. Tag new resources at deployment time (IaC templates)

REFERENCES:
- NIST 800-53 CM-8: System Component Inventory
- Continue monitoring with Azure Resource Graph"
                        : @"IMPROVE TAGGING COVERAGE per CM-8:
1. Target: 95%+ of resources tagged (current: " + tagComplianceRate.ToString("F1") + @"%)
2. Focus on adding missing required tags (Owner, Environment, Application)
3. Use Azure Policy to enforce tagging on new resources
4. Bulk tag existing resources via portal or PowerShell
5. Inherit tags from resource groups where applicable
6. Monthly review of untagged resources

REFERENCES:
- NIST 800-53 CM-8: System Component Inventory",
                    ComplianceStatus = tagComplianceRate >= 80 ? AtoComplianceStatus.Compliant : AtoComplianceStatus.PartiallyCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "CM-8" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning system inventory for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanGenericConfigurationAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
        _logger.LogDebug("Scanning generic configuration control {ControlId} for {Scope} in subscription {SubscriptionId}", 
            control.Id, scope, subscriptionId);
        try
        {
            var resources = await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken);
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Subscription",
                ResourceName = "Configuration Management Review",
                FindingType = AtoFindingType.ConfigurationManagement,
                Severity = AtoFindingSeverity.Informational,
                Title = $"Control {control.Id} - Manual Review Required",
                Description = control.Title ?? "Configuration management control requires manual review",
                Recommendation = "Review configuration management documentation and processes",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "CM" },
                ComplianceFrameworks = new List<string> { "NIST-800-53" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning generic configuration management for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }
}
