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
/// Scanner for Identification and Authentication (IA) family controls using real Azure APIs
/// </summary>
public class IdentificationAuthenticationScanner : IComplianceScanner
{
    private readonly ILogger _logger;
    private readonly IAzureResourceService _azureService;

    public IdentificationAuthenticationScanner(ILogger logger, IAzureResourceService azureService)
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
    /// Resource group-scoped scanning
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

        // CRITICAL: Control IDs from NIST catalog are lowercase (ia-2, ia-4, etc.)
        // Use case-insensitive comparison
        var controlId = control.Id?.ToUpperInvariant();

        switch (controlId)
        {
            case "IA-2":
                findings.AddRange(await ScanMFARequirementsAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "IA-4":
                findings.AddRange(await ScanIdentifierManagementAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "IA-5":
                findings.AddRange(await ScanAuthenticatorManagementAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            default:
                findings.AddRange(await ScanGenericAuthenticationAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;
        }

        // Enrich all findings with auto-remediation information
        return findings.WithAutoRemediationInfo();
    }


    private async Task<List<AtoFinding>> ScanMFARequirementsAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogInformation("Scanning MFA requirements (IA-2) for {Scope} in subscription {SubscriptionId}", 
                scope, subscriptionId);
            
            // Note: MFA policies are configured at Azure AD/Entra ID tenant level, not subscription level
            // Querying Conditional Access policies requires Microsoft Graph API with different auth
            // This scan provides comprehensive guidance for manual verification
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/conditionalAccessPolicies",
                ResourceName = "Multi-Factor Authentication",
                FindingType = AtoFindingType.IdentityManagement,
                Severity = AtoFindingSeverity.High,
                Title = "MFA Configuration Verification Required (IA-2)",
                Description = @"Multi-factor authentication (MFA) is critical for IA-2 compliance. MFA policies are configured at the Azure AD/Entra ID tenant level through Conditional Access policies.

**REQUIRED VERIFICATION** (Azure Portal → Entra ID → Security → Conditional Access):

1. **Privileged Users** (100% MFA requirement):
   - All Global Administrators
   - All Privileged Role Administrators
   - All Security Administrators
   - All subscription Owners/Contributors
   - All users with write permissions to production resources

2. **Standard Users** (MFA for sensitive operations):
   - Access to Azure Portal
   - Access to production subscriptions
   - Access from non-trusted locations
   - Access from unmanaged devices

3. **Service Accounts/Break Glass**:
   - Service accounts: Use service principals/managed identities (no MFA needed)
   - Break glass accounts: Documented exception with compensating controls
   - Emergency access: 2 break glass accounts maximum

**VERIFICATION STEPS**:
1. Entra ID → Users → Per-user MFA status → All admins should show 'Enforced'
2. Entra ID → Security → Conditional Access → Review all policies
3. Entra ID → Security → Authentication methods → Review allowed MFA methods
4. Sign-in logs → Filter by MFA requirement → Verify no privileged sign-ins without MFA",
                Recommendation = @"IMMEDIATE ACTION REQUIRED per IA-2 (Identification and Authentication):

1. **Enable Conditional Access Policies for MFA**:
   - Azure Portal → Entra ID → Security → Conditional Access → New policy
   
   **Policy 1: Require MFA for Administrators** (CRITICAL):
   - Name: 'Require MFA for all administrators'
   - Users: All Azure AD admin roles (select all directory roles)
   - Cloud apps: All cloud apps
   - Conditions: Any location, any device
   - Grant: Require multi-factor authentication
   - Session: Sign-in frequency 12 hours
   - Enable policy: Report-only (test first), then On
   
   **Policy 2: Require MFA for Azure Management**:
   - Name: 'Require MFA for Azure portal access'
   - Users: All users
   - Cloud apps: Microsoft Azure Management (portal access)
   - Conditions: Any location
   - Grant: Require multi-factor authentication
   - Enable policy: On
   
   **Policy 3: Require MFA for Risky Sign-ins**:
   - Name: 'Require MFA for medium/high risk sign-ins'
   - Users: All users
   - Conditions: Sign-in risk level: Medium or High
   - Grant: Require multi-factor authentication
   - Enable policy: On

2. **Configure Allowed MFA Methods** (FIPS 140-2 compliant):
   - Entra ID → Security → Authentication methods → Policies
   - **Recommended methods** (strongest to weakest):
     1. FIDO2 Security Keys (FIPS 140-2 Level 2) - **Required for IL5**
     2. Microsoft Authenticator (Push notifications)
     3. Windows Hello for Business
     4. Certificate-based authentication (smartcards)
   - **DISABLE for DoD/FedRAMP**:
     - SMS text messages (phishing vulnerable, not FIPS compliant)
     - Voice calls (not FIPS compliant)
   - **For IL5**: Only FIDO2 keys, smartcards, or PIV/CAC allowed

3. **Implement Passwordless Authentication** (IA-5(1)):
   - Microsoft Authenticator: Passwordless phone sign-in
   - Windows Hello for Business: Biometric + PIN
   - FIDO2 Security Keys: Hardware-based authentication
   - Benefits: Phishing-resistant, no passwords to compromise
   - Migration: Enable alongside MFA, gradually transition users

4. **Configure Authentication Strengths**:
   - Entra ID → Security → Authentication methods → Authentication strengths
   - Create custom strength: 'DoD IL5 Compliant'
   - Allowed methods: FIDO2, Certificate-based authentication
   - Apply to Conditional Access policies for privileged roles

5. **Set Up Break Glass Accounts**:
   - Create 2 cloud-only accounts: breakglass1@, breakglass2@
   - Assign Global Administrator role
   - Store credentials in physical safe (split knowledge)
   - **EXCLUDE from all Conditional Access policies** (including MFA)
   - Monitor usage: Alert immediately if accessed
   - Document in SSP and incident response plan
   - Test quarterly (document test results)

6. **Enable Identity Protection**:
   - Entra ID → Security → Identity Protection → Policies
   - User risk policy: High risk = require password change + MFA
   - Sign-in risk policy: Medium/high risk = require MFA
   - MFA registration policy: Require all users to register for MFA
   - Benefits: Automated risk detection, compromised credential protection

7. **Configure Session Management**:
   - Conditional Access → Session controls
   - Sign-in frequency: 12 hours for admins, 7 days for users
   - Persistent browser session: Disabled for privileged users
   - Continuous access evaluation (CAE): Enabled (real-time token revocation)

8. **Monitor MFA Usage**:
   - Entra ID → Sign-in logs → Filter: 'MFA result'
   - Azure Monitor: Alert on failed MFA attempts (5+ in 30 minutes)
   - Workbook: MFA registration and usage report
   - Weekly: Review users not registered for MFA (enforce registration)
   - Monthly: Audit privileged users for MFA compliance (100% required)

9. **Implement Phishing-Resistant MFA** (FedRAMP High):
   - FIDO2 security keys (Yubico, Feitian, Google Titan)
   - Windows Hello for Business with TPM
   - Certificate-based authentication (smartcards, PIV/CAC)
   - **Eliminate**: SMS, voice calls, legacy push notifications
   - Timeline: All privileged users within 90 days

10. **Document MFA Configuration in SSP**:
    - List all Conditional Access policies
    - MFA methods allowed per user type
    - Break glass account procedures
    - MFA failure procedures (account lockout, unlock process)
    - Exception process (document all exceptions with justification)
    - Testing schedule (quarterly MFA policy testing)

MFA REQUIREMENTS (FedRAMP/DoD):
- **FedRAMP Moderate/High**: MFA required for privileged users (IA-2(1))
- **FedRAMP High**: Phishing-resistant MFA required (IA-2(1), IA-2(2))
- **DoD IL4**: MFA with FIPS 140-2 Level 1 minimum
- **DoD IL5**: Phishing-resistant MFA (FIDO2, PIV/CAC only), FIPS 140-2 Level 2
- **Coverage**: 100% of privileged users, 95%+ of all users
- **Monitoring**: Real-time alerting on MFA failures, monthly compliance reporting

VERIFICATION CHECKLIST:
☐ All Global Admins have MFA enabled (check per-user MFA status)
☐ Conditional Access policy for admin MFA (check policy list)
☐ Conditional Access policy for Azure Management access (check policy list)
☐ Only FIPS-compliant MFA methods allowed (check authentication methods)
☐ Break glass accounts configured and documented (2 accounts, credentials secured)
☐ Identity Protection policies enabled (user risk + sign-in risk)
☐ MFA monitoring alerts configured (failed attempts, registration gaps)
☐ Quarterly MFA policy testing documented (test reports in SSP)

REFERENCES:
- NIST 800-53 IA-2: Identification and Authentication (Organizational Users)
- NIST 800-53 IA-2(1): Multi-Factor Authentication (Privileged Accounts)
- NIST 800-53 IA-2(2): Multi-Factor Authentication (Non-Privileged Accounts)
- NIST 800-63B: Digital Identity Guidelines (Authenticator Assurance Levels)
- FedRAMP MFA Requirements: Phishing-resistant MFA for High baseline
- DoD Cloud Computing SRG: FIPS 140-2 compliant MFA (IL4+)
- Azure Conditional Access: https://docs.microsoft.com/entra/identity/conditional-access/
- FIDO2 Security Keys: https://docs.microsoft.com/entra/identity/authentication/howto-authentication-passwordless-security-key",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "IA-2", "IA-2(1)", "IA-2(2)", "IA-2(8)", "IA-2(12)" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC", "NIST-800-63B" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning MFA requirements for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanIdentifierManagementAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogInformation("Scanning identifier management (IA-4) for {Scope} in subscription {SubscriptionId}", 
                scope, subscriptionId);
            
            var armClient = _azureService.GetArmClient();
            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            // Get managed identities
            var userAssignedIdentities = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.ManagedIdentity/userAssignedIdentities", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            // Get resources that SHOULD use managed identities
            var virtualMachines = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
                
            var appServices = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
                
            var functionApps = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) == true &&
                ((GenericResource)r).Data.Kind?.Contains("functionapp", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
                
            var containerInstances = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.ContainerInstance/containerGroups", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            // Track resources without managed identity
            var vmsWithoutManagedIdentity = new List<string>();
            var appServicesWithoutManagedIdentity = new List<string>();
            var functionAppsWithoutManagedIdentity = new List<string>();
            var containerInstancesWithoutManagedIdentity = new List<string>();

            // CHECK 1: Query VMs for managed identity configuration
            _logger.LogInformation("Checking managed identity for {VmCount} VMs", virtualMachines.Count);
            
            foreach (var vm in virtualMachines)
            {
                try
                {
                    var resourceId = ResourceIdentifier.Parse(((GenericResource)vm).Data.Id! ?? "");
                    var vmResource = armClient?.GetGenericResource(resourceId);
                    var vmData = await vmResource.GetAsync(cancellationToken);
                    
                    // Check for identity property
                    bool hasManagedIdentity = false;
                    if (vmData.Value.Data.Properties.ToObjectFromJson<JsonElement>().TryGetProperty("identity", out var identity))
                    {
                        if (identity.TryGetProperty("type", out var identityType))
                        {
                            var type = identityType.GetString();
                            hasManagedIdentity = !string.IsNullOrEmpty(type) && 
                                                !type.Equals("None", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    
                    // Also check top-level identity (sometimes it's at root, not in properties)
                    var vmJson = System.Text.Json.JsonSerializer.Serialize(vmData.Value.Data);
                    var vmElement = JsonDocument.Parse(vmJson);
                    if (vmElement.RootElement.TryGetProperty("identity", out var rootIdentity))
                    {
                        if (rootIdentity.TryGetProperty("type", out var identityType))
                        {
                            var type = identityType.GetString();
                            hasManagedIdentity = !string.IsNullOrEmpty(type) && 
                                                !type.Equals("None", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    
                    if (!hasManagedIdentity)
                    {
                        vmsWithoutManagedIdentity.Add($"{((GenericResource)vm).Data.Name} ({((GenericResource)vm).Data.Location})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query managed identity for VM {VmId}", ((GenericResource)vm).Data.Id!);
                    vmsWithoutManagedIdentity.Add($"{((GenericResource)vm).Data.Name} (status unknown)");
                }
            }

            // CHECK 2: Query App Services for managed identity
            _logger.LogInformation("Checking managed identity for {AppCount} App Services", appServices.Count);
            
            foreach (var app in appServices)
            {
                try
                {
                    // Skip function apps (counted separately)
                    if (((GenericResource)app).Data.Kind?.Contains("functionapp", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        continue;
                    }
                    
                    var resourceId = ResourceIdentifier.Parse(((GenericResource)app).Data.Id! ?? "");
                    var appResource = armClient?.GetGenericResource(resourceId);
                    var appData = await appResource.GetAsync(cancellationToken);
                    
                    // Check for managed identity
                    bool hasManagedIdentity = false;
                    var appJson = System.Text.Json.JsonSerializer.Serialize(appData.Value.Data);
                    var appElement = JsonDocument.Parse(appJson);
                    if (appElement.RootElement.TryGetProperty("identity", out var identity))
                    {
                        if (identity.TryGetProperty("type", out var identityType))
                        {
                            var type = identityType.GetString();
                            hasManagedIdentity = !string.IsNullOrEmpty(type) && 
                                                !type.Equals("None", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    
                    if (!hasManagedIdentity)
                    {
                        appServicesWithoutManagedIdentity.Add($"{((GenericResource)app).Data.Name} ({((GenericResource)app).Data.Location})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query managed identity for App Service {AppId}", ((GenericResource)app).Data.Id!);
                    appServicesWithoutManagedIdentity.Add($"{((GenericResource)app).Data.Name} (status unknown)");
                }
            }

            // CHECK 3: Query Function Apps for managed identity
            _logger.LogInformation("Checking managed identity for {FuncCount} Function Apps", functionApps.Count);
            
            foreach (var func in functionApps)
            {
                try
                {
                    var resourceId = ResourceIdentifier.Parse(((GenericResource)func).Data.Id! ?? "");
                    var funcResource = armClient?.GetGenericResource(resourceId);
                    var funcData = await funcResource.GetAsync(cancellationToken);
                    
                    // Check for managed identity
                    bool hasManagedIdentity = false;
                    var funcJson = System.Text.Json.JsonSerializer.Serialize(funcData.Value.Data);
                    var funcElement = JsonDocument.Parse(funcJson);
                    if (funcElement.RootElement.TryGetProperty("identity", out var identity))
                    {
                        if (identity.TryGetProperty("type", out var identityType))
                        {
                            var type = identityType.GetString();
                            hasManagedIdentity = !string.IsNullOrEmpty(type) && 
                                                !type.Equals("None", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    
                    if (!hasManagedIdentity)
                    {
                        functionAppsWithoutManagedIdentity.Add($"{((GenericResource)func).Data.Name} ({((GenericResource)func).Data.Location})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query managed identity for Function App {FuncId}", ((GenericResource)func).Data.Id!);
                    functionAppsWithoutManagedIdentity.Add($"{((GenericResource)func).Data.Name} (status unknown)");
                }
            }

            // CHECK 4: Query Container Instances for managed identity
            _logger.LogInformation("Checking managed identity for {ContainerCount} Container Instances", containerInstances.Count);
            
            foreach (var container in containerInstances)
            {
                try
                {
                    var resourceId = ResourceIdentifier.Parse(((GenericResource)container).Data.Id! ?? "");
                    var containerResource = armClient?.GetGenericResource(resourceId);
                    var containerData = await containerResource.GetAsync(cancellationToken);
                    
                    // Check for managed identity
                    bool hasManagedIdentity = false;
                    var containerJson = System.Text.Json.JsonSerializer.Serialize(containerData.Value.Data);
                    var containerElement = JsonDocument.Parse(containerJson);
                    if (containerElement.RootElement.TryGetProperty("identity", out var identity))
                    {
                        if (identity.TryGetProperty("type", out var identityType))
                        {
                            var type = identityType.GetString();
                            hasManagedIdentity = !string.IsNullOrEmpty(type) && 
                                                !type.Equals("None", StringComparison.OrdinalIgnoreCase);
                        }
                    }
                    
                    if (!hasManagedIdentity)
                    {
                        containerInstancesWithoutManagedIdentity.Add($"{((GenericResource)container).Data.Name} ({((GenericResource)container).Data.Location})");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not query managed identity for Container Instance {ContainerId}", ((GenericResource)container).Data.Id!);
                    containerInstancesWithoutManagedIdentity.Add($"{((GenericResource)container).Data.Name} (status unknown)");
                }
            }

            // Generate findings based on managed identity usage
            var totalResources = virtualMachines.Count + appServices.Count + functionApps.Count + containerInstances.Count;
            var totalWithoutMI = vmsWithoutManagedIdentity.Count + appServicesWithoutManagedIdentity.Count + 
                                functionAppsWithoutManagedIdentity.Count + containerInstancesWithoutManagedIdentity.Count;

            if (totalResources == 0)
            {
                // No compute resources found
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.ManagedIdentity/userAssignedIdentities",
                    ResourceName = "Managed Identities",
                    FindingType = AtoFindingType.IdentityManagement,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "No Compute Resources for Managed Identity Assessment",
                    Description = $"Found {userAssignedIdentities.Count} user-assigned managed identities but no compute resources (VMs, App Services, Functions, Containers) to assess. " +
                                 $"IA-4 requires unique identification and authentication for organizational users and processes.",
                    Recommendation = @"As compute resources are deployed, ensure managed identities are used per IA-4:

1. **Enable System-Assigned Managed Identity**: Automatically managed lifecycle
2. **Use User-Assigned Managed Identity**: Shared across multiple resources
3. **Replace Service Principals**: Eliminate credential management overhead
4. **Configure RBAC**: Assign least privilege roles to managed identities
5. **Monitor Usage**: Track managed identity authentication in Azure Monitor

REFERENCES:
- NIST 800-53 IA-4: Identifier Management
- Azure Managed Identities: https://docs.microsoft.com/azure/active-directory/managed-identities-azure-resources/",
                    ComplianceStatus = AtoComplianceStatus.NotApplicable,
                    AffectedNistControls = new List<string> { control.Id ?? "IA-4" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else if (totalWithoutMI > 0)
            {
                var percentageWithoutMI = (double)totalWithoutMI / totalResources * 100;
                var severity = percentageWithoutMI > 50 ? AtoFindingSeverity.High :
                               percentageWithoutMI > 20 ? AtoFindingSeverity.Medium : AtoFindingSeverity.Low;

                var description = $"Found {totalWithoutMI} of {totalResources} compute resources without managed identity configured ({percentageWithoutMI:F1}%). " +
                                 $"IA-4 requires unique identification for organizational users and processes. " +
                                 $"Managed identities eliminate the need for credential management and provide automatic credential rotation.";

                if (vmsWithoutManagedIdentity.Any())
                {
                    description += $"\n\n**VMs without managed identity ({vmsWithoutManagedIdentity.Count})**: {string.Join(", ", vmsWithoutManagedIdentity.Take(10))}";
                    if (vmsWithoutManagedIdentity.Count > 10) description += $" and {vmsWithoutManagedIdentity.Count - 10} more";
                }

                if (appServicesWithoutManagedIdentity.Any())
                {
                    description += $"\n\n**App Services without managed identity ({appServicesWithoutManagedIdentity.Count})**: {string.Join(", ", appServicesWithoutManagedIdentity.Take(5))}";
                    if (appServicesWithoutManagedIdentity.Count > 5) description += $" and {appServicesWithoutManagedIdentity.Count - 5} more";
                }

                if (functionAppsWithoutManagedIdentity.Any())
                {
                    description += $"\n\n**Function Apps without managed identity ({functionAppsWithoutManagedIdentity.Count})**: {string.Join(", ", functionAppsWithoutManagedIdentity.Take(5))}";
                    if (functionAppsWithoutManagedIdentity.Count > 5) description += $" and {functionAppsWithoutManagedIdentity.Count - 5} more";
                }

                if (containerInstancesWithoutManagedIdentity.Any())
                {
                    description += $"\n\n**Container Instances without managed identity ({containerInstancesWithoutManagedIdentity.Count})**: {string.Join(", ", containerInstancesWithoutManagedIdentity.Take(5))}";
                    if (containerInstancesWithoutManagedIdentity.Count > 5) description += $" and {containerInstancesWithoutManagedIdentity.Count - 5} more";
                }

                description += $"\n\n**Managed Identity Summary**:";
                description += $"\n- User-Assigned Managed Identities: {userAssignedIdentities.Count}";
                description += $"\n- Resources with Managed Identity: {totalResources - totalWithoutMI}";
                description += $"\n- Resources without Managed Identity: {totalWithoutMI}";

                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Multiple",
                    ResourceName = "Managed Identity Configuration",
                    FindingType = AtoFindingType.IdentityManagement,
                    Severity = severity,
                    Title = $"Insufficient Managed Identity Usage: {totalWithoutMI}/{totalResources} Resources Need Configuration",
                    Description = description,
                    Recommendation = @"IMMEDIATE ACTION REQUIRED per IA-4 (Identifier Management):

1. **Enable System-Assigned Managed Identity** (Recommended for most scenarios):
   - **Virtual Machines**:
     - Azure Portal → VM → Identity → System assigned → Status: On
     - Lifecycle: Tied to VM (deleted when VM deleted)
     - Use case: VM-specific access to Azure resources
   
   - **App Services / Function Apps**:
     - Azure Portal → App Service → Identity → System assigned → Status: On
     - Automatic credential rotation (no expiration)
     - Use case: App accessing Key Vault, Storage, SQL Database
   
   - **Container Instances**:
     - Azure Portal → Container Instance → Identity → System assigned → Status: On
     - Use case: Container accessing Azure resources without credentials

2. **Create User-Assigned Managed Identity** (For shared scenarios):
   - Azure Portal → Create Resource → User Assigned Managed Identity
   - Benefits: Independent lifecycle, shared across multiple resources
   - Use cases:
     - Multiple VMs need same access to resources
     - Cross-subscription resource access
     - Separate identity lifecycle from resource lifecycle
   
   - **Assign to Resources**:
     - Azure Portal → Resource → Identity → User assigned → Add
     - Select existing user-assigned managed identity
     - One resource can have multiple user-assigned identities

3. **Configure RBAC Permissions for Managed Identities**:
   - Azure Portal → Target Resource (e.g., Key Vault) → Access control (IAM)
   - Add role assignment
   - Assign to: Managed Identity
   - Select: System-assigned or user-assigned managed identity
   - Role: Least privilege (e.g., Key Vault Secrets User, Storage Blob Data Reader)
   - **Common Scenarios**:
     - VM → Key Vault: Key Vault Secrets User
     - App Service → Storage: Storage Blob Data Contributor
     - Function App → SQL Database: SQL DB Contributor
     - VM → Azure Resource Manager: Reader (for automation)

4. **Replace Service Principals with Managed Identities**:
   - **Before** (Service Principal with secrets in code):
     ```
     ClientId + ClientSecret stored in app configuration
     Secrets expire, require rotation, vulnerable to exposure
     ```
   
   - **After** (Managed Identity):
     ```
     No credentials in code, automatic token acquisition
     Tokens automatically rotated by Azure platform
     No expiration management, no secret storage
     ```
   
   - **Migration Steps**:
     1. Enable managed identity on resource
     2. Grant managed identity RBAC permissions
     3. Update code to use DefaultAzureCredential (supports MI)
     4. Remove service principal credentials from code/configuration
     5. Revoke service principal credentials
     6. Delete service principal if no longer needed

5. **Update Application Code to Use Managed Identity**:
   - **Azure SDK for .NET**:
     ```csharp
     var credential = new DefaultAzureCredential();
     var keyVaultClient = new SecretClient(vaultUri, credential);
     var storageClient = new BlobServiceClient(accountUri, credential);
     ```
   
   - **Azure SDK for Python**:
     ```python
     from azure.identity import DefaultAzureCredential
     credential = DefaultAzureCredential()
     key_vault_client = SecretClient(vault_url, credential)
     ```
   
   - **REST API** (Get token for specific resource):
     ```bash
     curl 'http://169.254.169.254/metadata/identity/oauth2/token?resource=https://vault.azure.net' -H Metadata:true
     ```

6. **Implement Managed Identity Best Practices**:
   - **Prefer System-Assigned**: Use unless you need shared identity
   - **Least Privilege**: Assign minimal required permissions
   - **Scope RBAC**: Assign roles at resource/RG level, not subscription
   - **Separate Environments**: Don't share identities across prod/non-prod
   - **Monitor Usage**: Track managed identity sign-ins in Azure Monitor
   - **Document Assignments**: Maintain inventory of MI → Resource mappings

7. **Configure Conditional Access for Managed Identities**:
   - Entra ID → Security → Conditional Access → New policy
   - Users: Include 'Workload identities'
   - Target: Managed identities
   - Conditions: Location-based restrictions (if applicable)
   - Grant: Block/Allow based on requirements
   - Use case: Prevent managed identity use from unexpected locations

8. **Enable Diagnostic Logging for Managed Identities**:
   - Azure Monitor → Diagnostic settings (on Key Vault, Storage, etc.)
   - Log category: AuditEvent, Sign-in logs
   - Destination: Log Analytics workspace
   - Query: Filter by 'appId' of managed identity
   - Alert: Unusual access patterns, failed authentications
   - Retention: 365 days minimum (FedRAMP requirement)

9. **Implement Managed Identity for Cross-Subscription Access**:
   - Scenario: VM in Subscription A needs access to Key Vault in Subscription B
   - Solution: User-assigned managed identity
   - Steps:
     1. Create user-assigned MI in Subscription A
     2. Assign MI to VM in Subscription A
     3. Grant MI permissions in Subscription B (Key Vault)
     4. Use DefaultAzureCredential in code (works automatically)

10. **Audit and Inventory Managed Identities**:
    - Azure Portal → Managed Identities → Review all identities
    - For each identity:
      - What resources use it? (User-assigned only)
      - What permissions does it have? (RBAC assignments)
      - When was it last used? (Sign-in logs)
      - Is it still needed? (Delete if unused)
    - Quarterly: Review and remove unused managed identities
    - Document: Maintain inventory in SSP

MANAGED IDENTITY BENEFITS (IA-4 Compliance):
- **No Credential Management**: Eliminates secrets in code/configuration
- **Automatic Rotation**: Azure handles token refresh automatically
- **No Expiration**: Tokens don't expire (unlike service principal secrets)
- **Least Privilege**: Fine-grained RBAC permissions
- **Audit Trail**: All access logged in Azure Monitor
- **Cost**: Free (no additional charge)

WHEN TO USE EACH TYPE:
- **System-Assigned**: Single resource identity (lifecycle tied to resource)
  - Use for: VMs, App Services, Function Apps accessing specific resources
  - Lifecycle: Created with resource, deleted with resource
  
- **User-Assigned**: Shared identity across multiple resources
  - Use for: Multi-VM solutions, cross-subscription access
  - Lifecycle: Independent (survives resource deletion)

MIGRATION PRIORITY (High to Low):
1. **Production App Services/Function Apps**: High exposure, critical workloads
2. **Production VMs**: Often have hardcoded credentials
3. **Container Instances**: Frequently use secrets in environment variables
4. **Development/Test Resources**: Lower priority but still valuable

REFERENCES:
- NIST 800-53 IA-4: Identifier Management
- NIST 800-53 IA-4(4): Identify User Status
- Azure Managed Identities Overview: https://docs.microsoft.com/azure/active-directory/managed-identities-azure-resources/
- Azure SDK DefaultAzureCredential: https://docs.microsoft.com/dotnet/api/azure.identity.defaultazurecredential
- Managed Identity Best Practices: https://docs.microsoft.com/azure/active-directory/managed-identities-azure-resources/managed-identity-best-practice-recommendations",
                    ComplianceStatus = AtoComplianceStatus.PartiallyCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "IA-4", "IA-4(4)" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "CMMC" },
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
                    ResourceType = "Microsoft.ManagedIdentity/userAssignedIdentities",
                    ResourceName = "Managed Identity Configuration",
                    FindingType = AtoFindingType.IdentityManagement,
                    Severity = AtoFindingSeverity.Informational,
                    Title = $"Managed Identities Configured: {totalResources} Resources Using Secure Authentication",
                    Description = $"All {totalResources} compute resources have managed identity enabled. " +
                                 $"This includes {virtualMachines.Count} VMs, {appServices.Count} App Services, " +
                                 $"{functionApps.Count} Function Apps, and {containerInstances.Count} Container Instances. " +
                                 $"Found {userAssignedIdentities.Count} user-assigned managed identities. " +
                                 $"IA-4 identifier management requirements are met.",
                    Recommendation = @"MAINTAIN MANAGED IDENTITY SECURITY per IA-4:

1. **Quarterly Access Reviews**: Audit RBAC permissions for all managed identities
2. **Monitor Usage**: Review managed identity sign-in logs for anomalies
3. **Least Privilege**: Ensure identities have minimal required permissions
4. **Remove Unused**: Delete managed identities no longer in use
5. **Document Inventory**: Maintain list of managed identities and their purposes
6. **Update SSP**: Keep identifier management section current
7. **Review Conditional Access**: Verify policies apply to workload identities
8. **Audit New Resources**: Ensure new compute resources use managed identities

Continue quarterly reviews and maintain inventory to ensure compliance is maintained.",
                    ComplianceStatus = AtoComplianceStatus.Compliant,
                    AffectedNistControls = new List<string> { control.Id ?? "IA-4" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning identifier management for subscription {SubscriptionId}", subscriptionId);
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Subscription",
                ResourceName = "Identifier Management Scan",
                FindingType = AtoFindingType.IdentityManagement,
                Severity = AtoFindingSeverity.High,
                Title = "Identifier Management Scan Error - Manual Review Required",
                Description = $"Could not complete automated managed identity scan: {ex.Message}. Manual review required to verify IA-4 compliance.",
                Recommendation = "Manually verify all compute resources (VMs, App Services, Function Apps, Containers) have managed identities enabled per IA-4 requirements.",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "IA-4" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                DetectedAt = DateTime.UtcNow
            });
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanAuthenticatorManagementAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogInformation("Scanning authenticator management (IA-5) for {Scope} in subscription {SubscriptionId}", 
                scope, subscriptionId);
            
            var armClient = _azureService.GetArmClient();
            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            // Get Key Vaults for secret/credential management
            var keyVaults = resources.Where(r => 
                ((GenericResource)r).Data.ResourceType.ToString().Equals("Microsoft.KeyVault/vaults", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            // Track Key Vaults with security issues
            var keyVaultsWithoutSoftDelete = new List<string>();
            var keyVaultsWithoutPurgeProtection = new List<string>();
            var keyVaultsWithoutRBAC = new List<string>();
            var keyVaultsPublicAccess = new List<string>();

            if (keyVaults.Count == 0)
            {
                // No Key Vaults found - critical for IA-5
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.KeyVault/vaults",
                    ResourceName = "Key Vault",
                    FindingType = AtoFindingType.IdentityManagement,
                    Severity = AtoFindingSeverity.High,
                    Title = "No Key Vaults for Credential Management",
                    Description = "No Azure Key Vaults found for secure authenticator storage. IA-5 requires secure management of authenticators (passwords, certificates, keys).",
                    Recommendation = @"IMMEDIATE ACTION REQUIRED per IA-5 (Authenticator Management):

1. **Deploy Azure Key Vault**:
   - Azure Portal → Create Resource → Key Vault
   - Select appropriate region (must match or be close to workloads)
   - Choose pricing tier: Standard (software keys) or Premium (HSM-backed keys)
   - Enable soft delete: 90 days retention (required for FedRAMP)
   - Enable purge protection: Prevents permanent deletion
   - Network: Private endpoint or firewall rules (no public access for IL5)

2. **Configure RBAC Access Control**:
   - Azure Portal → Key Vault → Access control (IAM)
   - Disable legacy access policies
   - Enable Azure RBAC for data plane operations
   - Assign least privilege roles:
     - Key Vault Secrets Officer: Full secret management
     - Key Vault Secrets User: Read-only secret access
     - Key Vault Certificates Officer: Certificate management
     - Key Vault Crypto Officer: Key operations

3. **Enable Advanced Security Features**:
   - Soft delete: 90-day recovery window (FedRAMP requirement)
   - Purge protection: Prevent malicious deletion
   - Private Link: Isolate Key Vault from public Internet
   - Firewall: Allow only trusted Azure services and specific IPs
   - Diagnostic settings: Send audit logs to Log Analytics (365-day retention)

4. **Implement Secret Rotation**:
   - Set expiration dates on all secrets (90 days maximum for privileged credentials)
   - Configure Azure Automation or Logic Apps for automatic rotation
   - Use Key Vault Event Grid integration for expiration notifications
   - Document rotation procedures in SSP

5. **Store All Authenticators in Key Vault**:
   - Service principal credentials
   - Database connection strings
   - API keys and tokens
   - SSH keys and certificates
   - Encryption keys
   - Storage account keys

REFERENCES:
- NIST 800-53 IA-5: Authenticator Management
- FedRAMP Key Vault Requirements: Soft delete + purge protection mandatory
- DoD Cloud Computing SRG: FIPS 140-2 Level 2 HSM required (IL5)
- Azure Key Vault Best Practices: https://docs.microsoft.com/azure/key-vault/",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "IA-5" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else
            {
                // CHECK 1: Query each Key Vault for security configuration
                _logger.LogInformation("Checking security configuration for {KvCount} Key Vaults", keyVaults.Count);
                
                foreach (var kv in keyVaults)
                {
                    try
                    {
                        var resourceId = ResourceIdentifier.Parse(((GenericResource)kv).Data.Id! ?? "");
                        var kvResource = armClient?.GetGenericResource(resourceId);
                        var kvData = await kvResource.GetAsync(cancellationToken);
                        
                        var properties = JsonDocument.Parse(kvData.Value.Data.Properties.ToStream());
                        
                        // Check soft delete
                        bool hasSoftDelete = false;
                        if (properties.RootElement.TryGetProperty("enableSoftDelete", out var softDelete))
                        {
                            hasSoftDelete = softDelete.GetBoolean();
                        }
                        
                        if (!hasSoftDelete)
                        {
                            keyVaultsWithoutSoftDelete.Add($"{((GenericResource)kv).Data.Name} ({((GenericResource)kv).Data.Location})");
                        }
                        
                        // Check purge protection
                        bool hasPurgeProtection = false;
                        if (properties.RootElement.TryGetProperty("enablePurgeProtection", out var purgeProtection))
                        {
                            hasPurgeProtection = purgeProtection.GetBoolean();
                        }
                        
                        if (!hasPurgeProtection)
                        {
                            keyVaultsWithoutPurgeProtection.Add($"{((GenericResource)kv).Data.Name} ({((GenericResource)kv).Data.Location})");
                        }
                        
                        // Check for RBAC authorization
                        bool usesRBAC = false;
                        if (properties.RootElement.TryGetProperty("enableRbacAuthorization", out var rbac))
                        {
                            usesRBAC = rbac.GetBoolean();
                        }
                        
                        if (!usesRBAC)
                        {
                            keyVaultsWithoutRBAC.Add($"{((GenericResource)kv).Data.Name} (using legacy access policies)");
                        }
                        
                        // Check network access (public vs private)
                        bool hasPublicAccess = true; // Default to public unless proven otherwise
                        if (properties.RootElement.TryGetProperty("networkAcls", out var networkAcls))
                        {
                            if (networkAcls.TryGetProperty("defaultAction", out var defaultAction))
                            {
                                var action = defaultAction.GetString();
                                hasPublicAccess = action?.Equals("Allow", StringComparison.OrdinalIgnoreCase) == true;
                            }
                        }
                        
                        if (hasPublicAccess)
                        {
                            keyVaultsPublicAccess.Add($"{((GenericResource)kv).Data.Name} (public network access enabled)");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not query configuration for Key Vault {KvId}", ((GenericResource)kv).Data.Id!);
                        keyVaultsWithoutSoftDelete.Add($"{((GenericResource)kv).Data.Name} (status unknown)");
                    }
                }

                // Generate findings based on Key Vault security posture
                var totalIssues = keyVaultsWithoutSoftDelete.Count + 
                                 keyVaultsWithoutPurgeProtection.Count + 
                                 keyVaultsWithoutRBAC.Count + 
                                 keyVaultsPublicAccess.Count;

                if (totalIssues > 0)
                {
                    var severity = (keyVaultsWithoutSoftDelete.Count > 0 || keyVaultsPublicAccess.Count > 0) 
                        ? AtoFindingSeverity.High : AtoFindingSeverity.Medium;

                    var description = $"Found {keyVaults.Count} Key Vaults with {totalIssues} security configuration issues. " +
                                     $"IA-5 requires secure management and storage of authenticators.";

                    if (keyVaultsWithoutSoftDelete.Any())
                    {
                        description += $"\n\n**Key Vaults without soft delete ({keyVaultsWithoutSoftDelete.Count})**: {string.Join(", ", keyVaultsWithoutSoftDelete.Take(5))}";
                        if (keyVaultsWithoutSoftDelete.Count > 5) description += $" and {keyVaultsWithoutSoftDelete.Count - 5} more";
                    }

                    if (keyVaultsWithoutPurgeProtection.Any())
                    {
                        description += $"\n\n**Key Vaults without purge protection ({keyVaultsWithoutPurgeProtection.Count})**: {string.Join(", ", keyVaultsWithoutPurgeProtection.Take(5))}";
                        if (keyVaultsWithoutPurgeProtection.Count > 5) description += $" and {keyVaultsWithoutPurgeProtection.Count - 5} more";
                    }

                    if (keyVaultsWithoutRBAC.Any())
                    {
                        description += $"\n\n**Key Vaults without RBAC ({keyVaultsWithoutRBAC.Count})**: {string.Join(", ", keyVaultsWithoutRBAC.Take(5))}";
                        if (keyVaultsWithoutRBAC.Count > 5) description += $" and {keyVaultsWithoutRBAC.Count - 5} more";
                    }

                    if (keyVaultsPublicAccess.Any())
                    {
                        description += $"\n\n**Key Vaults with public access ({keyVaultsPublicAccess.Count})**: {string.Join(", ", keyVaultsPublicAccess.Take(5))}";
                        if (keyVaultsPublicAccess.Count > 5) description += $" and {keyVaultsPublicAccess.Count - 5} more";
                    }

                    findings.Add(new AtoFinding
                    {
                        Id = Guid.NewGuid().ToString(),
                        SubscriptionId = subscriptionId,
                        ResourceId = $"/subscriptions/{subscriptionId}",
                        ResourceType = "Microsoft.KeyVault/vaults",
                        ResourceName = "Key Vault Security Configuration",
                        FindingType = AtoFindingType.IdentityManagement,
                        Severity = severity,
                        Title = $"Key Vault Security Issues: {totalIssues} Configurations Need Remediation",
                        Description = description,
                        Recommendation = @"IMMEDIATE ACTION REQUIRED per IA-5 (Authenticator Management):

1. **Enable Soft Delete on All Key Vaults**:
   - Azure Portal → Key Vault → Properties → Soft delete
   - Set retention period: 90 days (FedRAMP requirement)
   - Cannot be disabled once enabled (by design)
   - Allows recovery of deleted secrets/keys within retention period
   - Required for IL4+ DoD compliance

2. **Enable Purge Protection**:
   - Azure Portal → Key Vault → Properties → Purge protection
   - Prevents permanent deletion during soft delete retention period
   - Protects against malicious insider threats
   - Cannot be disabled once enabled
   - Required for FedRAMP High and DoD IL5

3. **Migrate to Azure RBAC Authorization**:
   - Azure Portal → Key Vault → Access configuration
   - Permission model: Azure role-based access control
   - Disable legacy vault access policies
   - Benefits: Unified IAM, audit logging, conditional access integration
   - Assign roles:
     - Key Vault Secrets Officer: Secret management
     - Key Vault Secrets User: Read-only access
     - Key Vault Crypto Officer: Key operations
     - Key Vault Certificates Officer: Certificate management

4. **Restrict Network Access**:
   - Azure Portal → Key Vault → Networking → Firewalls and virtual networks
   - Set default action: Deny
   - Add firewall rules for specific IPs (admin access only)
   - Enable private endpoint for Azure resources
   - Allow trusted Microsoft services (checkbox)
   - For IL5: Private endpoint required, no public access

5. **Configure Diagnostic Settings**:
   - Azure Portal → Key Vault → Diagnostic settings
   - Send AuditEvent logs to Log Analytics workspace
   - Retention: 365 days minimum (FedRAMP requirement)
   - Alert on:
     - Secret access (monitor privileged credential usage)
     - Failed authentication attempts
     - Key Vault configuration changes
     - Secret expiration (30 days before)

6. **Implement Secret Rotation Policy**:
   - Set expiration dates on all secrets
   - High-privilege credentials: 90 days maximum
   - Service account credentials: 180 days maximum
   - Certificates: 365 days (1 year) maximum
   - Use Azure Automation for automatic rotation
   - Document rotation procedures in SSP

7. **Enable Advanced Threat Protection**:
   - Microsoft Defender for Key Vault: Detects unusual access patterns
   - Alert on: Suspicious access from anonymous IPs, Tor exit nodes
   - Alert on: Unusual secret enumeration (reconnaissance activity)
   - Alert on: High volume secret access
   - Integration with Microsoft Sentinel for SIEM

8. **Use Managed HSM for High-Value Keys** (IL5 requirement):
   - For customer-managed encryption keys (CMK)
   - FIPS 140-2 Level 3 validated HSMs
   - Dedicated HSM pool (no shared tenancy)
   - Required for DoD IL5 workloads
   - Higher cost but mandatory for classified data

9. **Implement Key Vault Naming Convention**:
   - Environment-specific vaults: kv-prod-eastus, kv-dev-eastus
   - Separate vaults for different security boundaries
   - Never mix production and non-production secrets
   - Document naming in cloud adoption framework

10. **Regular Secret Hygiene**:
    - Monthly: Audit secrets without expiration dates (set expirations)
    - Quarterly: Review access logs for unused secrets (delete if unused)
    - Annually: Rotate all secrets regardless of expiration
    - Remove secrets from code repositories (scan with Defender for DevOps)
    - Use managed identities instead of service principals where possible

AUTHENTICATOR REQUIREMENTS (FedRAMP/DoD):
- Soft Delete: 90-day minimum retention (required)
- Purge Protection: Enabled (required for High/IL5)
- Network Isolation: Private endpoints (required for IL5)
- Audit Logging: 365-day retention minimum
- Secret Expiration: 90 days for privileged, 180 days for service accounts
- HSM Backing: Required for IL5 customer-managed keys

REFERENCES:
- NIST 800-53 IA-5: Authenticator Management
- NIST 800-53 IA-5(1): Password-based authentication
- FedRAMP Key Vault Requirements: Soft delete + purge protection
- DoD Cloud Computing SRG: FIPS 140-2 Level 3 HSM (IL5)
- Azure Key Vault Security: https://docs.microsoft.com/azure/key-vault/general/security-features",
                        ComplianceStatus = AtoComplianceStatus.NonCompliant,
                        AffectedNistControls = new List<string> { control.Id ?? "IA-5", "IA-5(1)" },
                        ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC" },
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
                        ResourceType = "Microsoft.KeyVault/vaults",
                        ResourceName = "Key Vault Security Configuration",
                        FindingType = AtoFindingType.IdentityManagement,
                        Severity = AtoFindingSeverity.Informational,
                        Title = $"Key Vault Security Configured: {keyVaults.Count} Vaults Secure",
                        Description = $"All {keyVaults.Count} Key Vaults have proper security configuration. " +
                                     $"Soft delete, purge protection, RBAC, and network restrictions are enabled. " +
                                     $"IA-5 authenticator management requirements are met.",
                        Recommendation = @"MAINTAIN KEY VAULT SECURITY per IA-5:

1. **Monthly Secret Audits**: Review secrets without expiration dates, set expirations
2. **Quarterly Access Reviews**: Audit who has access to Key Vault, remove unused permissions
3. **Monitor Access Logs**: Review Key Vault audit logs for suspicious activity
4. **Test Secret Rotation**: Verify automated rotation works for all secrets
5. **Update Documentation**: Keep SSP authenticator management section current
6. **Rotate Secrets**: Follow 90-day rotation for privileged credentials
7. **Review Alerts**: Ensure Defender for Key Vault alerts are configured
8. **Backup Verification**: Test Key Vault backup and restore procedures

Continue monthly secret hygiene and quarterly access reviews to ensure compliance is maintained.",
                        ComplianceStatus = AtoComplianceStatus.Compliant,
                        AffectedNistControls = new List<string> { control.Id ?? "IA-5" },
                        ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG" },
                        DetectedAt = DateTime.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning authenticator management for subscription {SubscriptionId}", subscriptionId);
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Subscription",
                ResourceName = "Authenticator Management Scan",
                FindingType = AtoFindingType.IdentityManagement,
                Severity = AtoFindingSeverity.High,
                Title = "Authenticator Management Scan Error - Manual Review Required",
                Description = $"Could not complete automated Key Vault scan: {ex.Message}. Manual review required to verify IA-5 compliance.",
                Recommendation = "Manually verify all Key Vaults have soft delete, purge protection, RBAC, and network restrictions enabled per IA-5 requirements.",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "IA-5" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                DetectedAt = DateTime.UtcNow
            });
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanGenericAuthenticationAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId, cancellationToken)
                : await _azureService.ListAllResourcesInResourceGroupAsync(subscriptionId, resourceGroupName, cancellationToken);
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Subscription",
                ResourceName = "Identity and Authentication Review",
                FindingType = AtoFindingType.IdentityManagement,
                Severity = AtoFindingSeverity.Informational,
                Title = $"Control {control.Id} - Manual Review Required",
                Description = control.Title ?? "Identity and authentication control requires manual review",
                Recommendation = "Review identity and authentication policies in Entra ID",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "IA" },
                ComplianceFrameworks = new List<string> { "NIST-800-53" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning generic authentication for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }
}
