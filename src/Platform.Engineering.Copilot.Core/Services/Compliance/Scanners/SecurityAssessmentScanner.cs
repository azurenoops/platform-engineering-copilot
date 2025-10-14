using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Platform.Engineering.Copilot.Core.Models;
using Platform.Engineering.Copilot.Core.Interfaces;
using Platform.Engineering.Copilot.Core.Extensions;

namespace Platform.Engineering.Copilot.Core.Services.Compliance;

/// <summary>
/// Scanner for Security Assessment and Authorization (CA) family controls using real Azure APIs
/// </summary>
public class SecurityAssessmentScanner : IComplianceScanner
{
    private readonly ILogger _logger;
    private readonly IAzureResourceService _azureService;

    public SecurityAssessmentScanner(ILogger logger, IAzureResourceService azureService)
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

        // CRITICAL: Control IDs from NIST catalog are lowercase (ca-2, ca-7, etc.)
        // Use case-insensitive comparison
        var controlId = control.Id?.ToUpperInvariant();

        switch (controlId)
        {
            case "CA-2":
                findings.AddRange(await ScanSecurityAssessmentsAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "CA-7":
                findings.AddRange(await ScanContinuousMonitoringAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            default:
                findings.AddRange(await ScanGenericSecurityAssessmentAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;
        }

        // Enrich all findings with auto-remediation information
        return findings.WithAutoRemediationInfo();
    }


    private async Task<List<AtoFinding>> ScanSecurityAssessmentsAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogInformation("Scanning security assessments (CA-2) for {Scope} in subscription {SubscriptionId}", 
                scope, subscriptionId);
            
            var armClient = _azureService.GetArmClient();
            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            // CA-2 requires formal security control assessments
            // Note: Cannot automatically verify assessment documentation, but can verify automated assessment infrastructure
            
            // Check for policy assignments (automated security assessments)
            var policyAssignments = resources.Where(r => 
                r.Type?.Equals("Microsoft.Authorization/policyAssignments", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
            
            // Count assessable resources (require control assessments)
            var totalResources = resources.Count;
            
            // CA-2 is critical for ATO - security control assessments must be conducted
            // High severity because this is foundational for authorization
            var severity = AtoFindingSeverity.High;
            
            var description = $"CA-2 requires formal security control assessments for all {totalResources} resources in subscription. " +
                             $"Found {policyAssignments.Count} Azure Policy assignments for automated assessments. " +
                             $"However, CA-2 also requires independent assessor verification and formal assessment reports.";
            
            description += "\n\n**AUTOMATED ASSESSMENT INFRASTRUCTURE**:";
            description += $"\n- Azure Policy Assignments: {policyAssignments.Count} (automated control checks)";
            description += $"\n- Total Resources: {totalResources} (all require control assessments)";
            
            description += "\n\n**REQUIRED ASSESSMENTS** (per NIST 800-53A):";
            description += "\n1. **Security Control Assessment**: Independent assessor evaluates control implementation";
            description += "\n2. **Continuous Assessment**: Automated tools (Defender for Cloud, Azure Policy)";
            description += "\n3. **Assessment Report**: Formal documentation of findings and risks";
            description += "\n4. **Plan of Action & Milestones (POA&M)**: Track remediation of identified weaknesses";
            
            description += "\n\n**ASSESSMENT FREQUENCY** (FedRAMP/DoD):";
            description += "\n- Initial Assessment: Before system goes live (ATO)";
            description += "\n- Annual Assessment: Full control assessment (required)";
            description += "\n- Continuous Monitoring: Automated assessments (daily via Defender for Cloud)";
            description += "\n- Significant Changes: Re-assessment required (major architecture changes)";
            
            description += "\n\n**AZURE TOOLS FOR CA-2**:";
            description += "\n- **Microsoft Defender for Cloud**: 200+ automated security assessments, Secure Score (0-100%), Regulatory Compliance dashboards";
            description += "\n- **Azure Policy**: Custom compliance policies, remediation tasks, compliance reporting";
            description += "\n- **Azure Security Benchmark**: FedRAMP High, NIST 800-53, DoD IL5 compliance mappings";
            description += "\n- **Third-Party Assessment**: Independent 3PAO (FedRAMP) or DIACAP assessor (DoD) required";

            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Authorization/policyAssignments",
                ResourceName = "Security Control Assessments",
                FindingType = AtoFindingType.SecurityAssessment,
                Severity = severity,
                Title = "Security Control Assessment Required for CA-2 Compliance",
                Description = description,
                Recommendation = @"IMMEDIATE ACTION REQUIRED per CA-2 (Security Assessments):

1. **Enable Microsoft Defender for Cloud** (Automated Assessments):
   - Azure Portal → Microsoft Defender for Cloud → Environment settings
   - Select subscription → Defender plans → Enable all plans:
     - Defender for Servers ($15/VM/month): VM security posture, vulnerability scanning
     - Defender for App Service ($15/app/month): Web app vulnerabilities, threat detection
     - Defender for Storage ($10/storage account/month): Malware scanning, unusual access
     - Defender for SQL ($15/server/month): SQL vulnerability assessment, threat protection
     - Defender for Containers ($7/vCore/month): Container scanning, Kubernetes protection
     - Defender for Key Vault ($0.02/10K transactions): Key vault threat detection
     - Defender for DNS ($0.70/million queries): DNS anomaly detection
     - Defender for Resource Manager ($5/million operations): Control plane protection
   
   - **Benefits**:
     - 200+ automated security assessments (checks controls continuously)
     - Secure Score: Overall security posture (0-100%, target 80%+ for FedRAMP High)
     - Regulatory Compliance: FedRAMP High, NIST 800-53, DoD IL5 dashboards
     - Attack path analysis: Multi-step attack scenario identification
     - Security alerts: Real-time threat detection and incident response
   
   - **Cost**: Approximately 2-5% of total infrastructure cost

2. **Review Secure Score and Recommendations**:
   - Azure Portal → Defender for Cloud → Secure Score
   - Current score: X/100 points (lower score = more vulnerabilities)
   - **Target Scores**:
     - FedRAMP Moderate: 70% minimum
     - FedRAMP High: 80% minimum
     - DoD IL5: 90% minimum
   
   - Click each recommendation for details:
     - Affected resources (specific VMs, apps, storage accounts)
     - Remediation steps (automated fix-it button when available)
     - Impact on Secure Score (points gained by fixing)
     - Security control mapping (which NIST controls this addresses)
   
   - **Prioritize**:
     - High severity findings first (critical vulnerabilities)
     - Quick wins (high score impact, low effort)
     - Compliance gaps (FedRAMP/DoD specific requirements)

3. **Configure Regulatory Compliance Dashboard**:
   - Defender for Cloud → Regulatory compliance
   - Add compliance standards:
     - Azure Security Benchmark (enabled by default)
     - NIST SP 800-53 Rev 5 (FedRAMP baseline)
     - DoD Cloud Computing SRG (IL4/IL5)
     - PCI DSS 4.0 (if applicable)
     - CMMC Level 2 (DoD contractors)
   
   - **Review Compliance Status**:
     - Overall compliance percentage (target 90%+)
     - Failed assessments by control family (AC, AU, CM, etc.)
     - Passed assessments (evidence for SSP)
     - Not applicable assessments (document why)
   
   - Export compliance reports:
     - Download PDF/CSV reports (monthly for AO)
     - Share with assessors (3PAO or DIACAP)
     - Include in SSP as continuous monitoring evidence

4. **Deploy Azure Policy Initiatives**:
   - Azure Portal → Policy → Definitions → Initiatives
   - Assign built-in initiatives:
     - **Azure Security Benchmark**: Core security policies
     - **FedRAMP High**: FedRAMP-specific controls
     - **DoD Impact Level 5**: IL5 compliance policies
     - **NIST SP 800-53 Rev. 5**: Full NIST control set
   
   - **Policy Effects**:
     - Audit: Report non-compliance (no enforcement)
     - Deny: Block non-compliant deployments (prevent misconfigurations)
     - DeployIfNotExists: Auto-remediate (e.g., deploy diagnostic settings)
     - Modify: Auto-fix configurations (e.g., add required tags)
   
   - Assign at subscription or resource group scope
   - Allow exclusions with documented justification (document in SSP)
   - Review compliance: Policy → Compliance (percentage by initiative)

5. **Conduct Independent Security Assessment**:
   - **FedRAMP**: Hire FedRAMP-approved Third-Party Assessment Organization (3PAO)
   - **DoD**: Use DIACAP-certified assessor or DoD assessment team
   - **Commercial ATO**: Use qualified independent assessor
   
   - **Assessment Scope**:
     - Review all NIST 800-53 controls in SSP
     - Interview system owners and administrators
     - Test control implementations (sampling methodology per 800-53A)
     - Review evidence (logs, configurations, screenshots)
     - Validate compensating controls (if any deviations)
   
   - **Deliverables**:
     - Security Assessment Report (SAR): Formal findings
     - Security Assessment Plan (SAP): Assessment methodology
     - Risk Exposure Table: All identified risks
     - POA&M: Remediation plan for findings

6. **Develop Security Assessment Plan (SAP)**:
   - Document assessment methodology (per NIST 800-53A)
   - Define assessment objectives (verify control implementation)
   - Identify assessment team (lead assessor, technical assessors, SMEs)
   - Determine assessment methods:
     - **Examine**: Review documentation (SSP, policies, procedures)
     - **Interview**: Talk to system owners, admins, users
     - **Test**: Validate technical controls (vulnerability scans, penetration tests)
   
   - Define sampling strategy (if not testing 100% of controls)
   - Document assessment schedule (duration, milestones)
   - Coordinate with system owner (access, interviews, testing windows)

7. **Perform Control Testing** (per NIST 800-53A):
   - **Access Control (AC)**:
     - Test MFA enforcement (attempt login without MFA)
     - Review RBAC assignments (verify least privilege)
     - Test privileged access management (PIM approvals)
   
   - **Audit and Accountability (AU)**:
     - Review audit logs (verify comprehensive logging)
     - Test log integrity (verify tamper protection)
     - Validate alerting (trigger test alerts)
   
   - **Identification and Authentication (IA)**:
     - Test password policies (attempt weak passwords)
     - Verify managed identities (no hardcoded credentials)
     - Review session timeouts (test timeout enforcement)
   
   - **System and Communications Protection (SC)**:
     - Test encryption in transit (verify TLS 1.2+)
     - Verify encryption at rest (check storage/database encryption)
     - Test firewall rules (attempt blocked connections)
   
   - Document all test results (screenshots, logs, tool output)

8. **Generate Security Assessment Report (SAR)**:
   - **Executive Summary**: High-level findings, risk posture, ATO recommendation
   - **Assessment Methodology**: How controls were tested (examine/interview/test)
   - **Findings by Control Family**: 
     - Control ID and description
     - Assessment result (Satisfied, Other than Satisfied, Not Applicable)
     - Evidence reviewed (documentation, logs, configurations)
     - Weakness identified (if control not fully implemented)
     - Risk level (Low/Moderate/High based on likelihood × impact)
   - **Risk Exposure Table**: All findings with risk scores
   - **Recommendations**: Prioritized remediation actions
   - **Assessor Statement**: Independent verification of control effectiveness

9. **Create Plan of Action & Milestones (POA&M)**:
   - For each finding in SAR:
     - **Control ID**: Which NIST control has weakness
     - **Weakness**: Specific implementation gap
     - **Risk**: Likelihood × Impact (per NIST SP 800-30)
     - **Remediation**: Action items to close gap
     - **Resources Required**: Budget, personnel, time
     - **Scheduled Completion Date**: Target date for closure
     - **Milestones**: Interim steps (25%, 50%, 75%, 100%)
     - **Status**: Open, Ongoing, Risk Accepted, Closed
   
   - POA&M Template: Use FedRAMP POA&M template (required format)
   - Monthly Updates: Submit updated POA&M to AO (track progress)
   - Risk Acceptance: AO must formally accept all risks

10. **Implement Continuous Assessment Process**:
    - **Daily**: Defender for Cloud automated assessments
    - **Weekly**: Review new Defender recommendations (assign owners)
    - **Monthly**: Update POA&M, submit to AO, review Secure Score trends
    - **Quarterly**: 
      - Review Regulatory Compliance dashboard
      - Test incident response procedures
      - Conduct tabletop exercises (security scenarios)
      - Update risk assessment
    - **Annually**: 
      - Full independent security assessment (3PAO or assessor)
      - Update all SSP sections
      - Re-authorization decision by AO
      - Update contingency plan and test

ASSESSMENT REQUIREMENTS (FedRAMP/DoD):
- **Initial Assessment**: Before ATO (full assessment by 3PAO or DIACAP assessor)
- **Annual Assessment**: Full control re-assessment (maintain ATO)
- **Continuous Monitoring**: Daily automated assessments (Defender for Cloud)
- **Significant Change**: Re-assessment for major changes (new services, architecture changes)
- **POA&M Updates**: Monthly submission to AO (track remediation progress)
- **Secure Score Target**: 80%+ (FedRAMP High), 90%+ (DoD IL5)

ASSESSMENT ARTIFACTS (Required for ATO):
- Security Assessment Plan (SAP): Assessment methodology
- Security Assessment Report (SAR): Formal findings
- POA&M: Remediation plan for all findings
- Risk Exposure Table: Risk scores for all findings
- Test Evidence: Screenshots, logs, scan results
- Assessor Qualifications: 3PAO approval letter or assessor resume

THIRD-PARTY ASSESSMENT ORGANIZATIONS (FedRAMP):
- Must use FedRAMP-approved 3PAO (see fedramp.gov/assessors)
- Cost: $150K-$500K+ (depending on system complexity)
- Duration: 3-6 months (planning, testing, reporting)
- Required for FedRAMP authorization

DOD ASSESSMENT (DIACAP/DoD SRG):
- Use DoD-approved assessment team or commercial assessor
- Follow DoD Cloud Computing SRG (IL4/IL5 requirements)
- SCCA compliance verification (Secure Cloud Computing Architecture)
- IL5: Requires DISA validation for classified workloads

REFERENCES:
- NIST 800-53 CA-2: Security Assessments
- NIST 800-53A: Assessing Security and Privacy Controls
- FedRAMP Security Assessment Framework: https://www.fedramp.gov/assets/resources/documents/CSP_A_FedRAMP_Authorization_Boundary_Guidance.pdf
- DoD Cloud Computing SRG: https://dl.dod.cyber.mil/wp-content/uploads/cloud/SRG/",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "CA-2", "CA-2(1)", "CA-2(2)" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning security assessments for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanContinuousMonitoringAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogInformation("Scanning continuous monitoring (CA-7) for {Scope} in subscription {SubscriptionId}", 
                scope, subscriptionId);
            
            var armClient = _azureService.GetArmClient();
            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            // CA-7 requires continuous monitoring of security controls
            // Check for monitoring infrastructure
            
            // CHECK 1: Log Analytics workspaces (centralized logging)
            var logAnalyticsWorkspaces = resources.Where(r => 
                r.Type?.Equals("Microsoft.OperationalInsights/workspaces", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
            
            // CHECK 2: Resources that should have diagnostic settings
            var keyVaults = resources.Where(r => 
                r.Type?.Equals("Microsoft.KeyVault/vaults", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            var storageAccounts = resources.Where(r => 
                r.Type?.Equals("Microsoft.Storage/storageAccounts", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            var appServices = resources.Where(r => 
                r.Type?.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            var sqlServers = resources.Where(r => 
                r.Type?.Equals("Microsoft.Sql/servers", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            var networkSecurityGroups = resources.Where(r => 
                r.Type?.Equals("Microsoft.Network/networkSecurityGroups", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            var virtualMachines = resources.Where(r => 
                r.Type?.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true).ToList();
            
            var totalMonitorableResources = keyVaults.Count + storageAccounts.Count + appServices.Count + 
                                           sqlServers.Count + networkSecurityGroups.Count + virtualMachines.Count;
            
            if (logAnalyticsWorkspaces.Count == 0)
            {
                // Critical: No Log Analytics workspace for continuous monitoring
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.OperationalInsights/workspaces",
                    ResourceName = "Continuous Monitoring Infrastructure",
                    FindingType = AtoFindingType.SecurityAssessment,
                    Severity = AtoFindingSeverity.Critical,
                    Title = "No Log Analytics Workspace for Continuous Monitoring",
                    Description = $"No Log Analytics workspace found for continuous security monitoring. " +
                                 $"Found {totalMonitorableResources} resources requiring continuous monitoring (Key Vaults, Storage, Apps, SQL, NSGs, VMs). " +
                                 $"CA-7 requires continuous monitoring of security controls with centralized log collection and alerting.",
                    Recommendation = @"IMMEDIATE ACTION REQUIRED per CA-7 (Continuous Monitoring):

1. **Deploy Log Analytics Workspace**:
   - Azure Portal → Create Resource → Log Analytics Workspace
   - Region: Same as primary workloads (minimize egress costs)
   - Pricing tier: Pay-as-you-go (per GB ingested)
   - Retention: 365 days minimum (FedRAMP requirement), 730 days (DoD IL5)
   - Daily cap: Set alert at 80% of budget (prevent bill shock)

2. **Enable Diagnostic Settings on All Resources**:
   - Key Vaults: AuditEvent logs (all secret/key/certificate operations)
   - Storage Accounts: StorageRead, StorageWrite, StorageDelete
   - App Services: AppServiceHTTPLogs, AppServiceConsoleLogs, AppServiceAuditLogs
   - SQL Servers: SQLSecurityAuditEvents, Errors, DatabaseWaitStatistics
   - Network Security Groups: NetworkSecurityGroupEvent, NetworkSecurityGroupRuleCounter
   - Virtual Machines: Install Log Analytics agent (collect syslog, security events)

3. **Deploy Azure Monitor Agents**:
   - VMs: Azure Monitor Agent (AMA) replaces legacy Log Analytics agent
   - Containers: Container Insights for AKS clusters
   - App Services: Application Insights for application performance monitoring

4. **Configure Security Alerts**:
   - Defender for Cloud: Enable all security alerts
   - Log Analytics: Create alert rules for suspicious activities
   - Azure Monitor: Configure action groups (email, SMS, webhook, Logic Apps)

5. **Enable Microsoft Sentinel** (SIEM):
   - Deploy Sentinel solution in Log Analytics workspace
   - Connect data sources (Defender, Entra ID, Azure Activity)
   - Enable analytics rules (detect threats, suspicious activities)
   - Configure automation playbooks (auto-response to incidents)

6. **Implement Continuous Monitoring Process**:
   - Daily: Review Defender for Cloud recommendations
   - Weekly: Review Sentinel incidents, tune analytics rules
   - Monthly: Generate compliance reports, update POA&M
   - Quarterly: Risk assessment updates, control effectiveness reviews

REFERENCES:
- NIST 800-53 CA-7: Continuous Monitoring
- FedRAMP Continuous Monitoring Requirements
- Azure Monitor Documentation: https://docs.microsoft.com/azure/azure-monitor/",
                    ComplianceStatus = AtoComplianceStatus.NonCompliant,
                    AffectedNistControls = new List<string> { control.Id ?? "CA-7", "CA-7(1)" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Have Log Analytics - check monitoring coverage
                var workspaceName = logAnalyticsWorkspaces[0].Name ?? "Log Analytics Workspace";
                var workspaceLocation = logAnalyticsWorkspaces[0].Location ?? "unknown";
                
                // Note: Full diagnostic settings validation requires querying each resource individually
                // This provides guidance on expected coverage
                
                var description = $"Found {logAnalyticsWorkspaces.Count} Log Analytics workspace(s) for continuous monitoring. " +
                                 $"Workspace: {workspaceName} ({workspaceLocation}). " +
                                 $"Identified {totalMonitorableResources} resources requiring diagnostic settings and continuous monitoring.";
                
                description += "\n\n**RESOURCES REQUIRING MONITORING**:";
                if (keyVaults.Any()) description += $"\n- Key Vaults: {keyVaults.Count} (audit all secret/key access)";
                if (storageAccounts.Any()) description += $"\n- Storage Accounts: {storageAccounts.Count} (monitor data access patterns)";
                if (appServices.Any()) description += $"\n- App Services: {appServices.Count} (application logs, HTTP logs)";
                if (sqlServers.Any()) description += $"\n- SQL Servers: {sqlServers.Count} (audit events, security logs)";
                if (networkSecurityGroups.Any()) description += $"\n- Network Security Groups: {networkSecurityGroups.Count} (firewall rule hits)";
                if (virtualMachines.Any()) description += $"\n- Virtual Machines: {virtualMachines.Count} (security events, syslog)";
                
                description += "\n\n**CONTINUOUS MONITORING COMPONENTS** (CA-7):";
                description += "\n1. **Log Collection**: Diagnostic settings send logs to Log Analytics";
                description += "\n2. **Threat Detection**: Defender for Cloud analyzes logs for security threats";
                description += "\n3. **Anomaly Detection**: Machine learning identifies unusual behavior";
                description += "\n4. **Security Alerts**: Real-time notifications for suspicious activities";
                description += "\n5. **Compliance Dashboards**: Regulatory compliance status (FedRAMP, NIST 800-53)";
                description += "\n6. **Incident Response**: Automated playbooks for common scenarios";
                
                description += "\n\n**VERIFICATION NEEDED**:";
                description += "\n- Verify all resources have diagnostic settings configured";
                description += "\n- Verify Log Analytics agents installed on VMs";
                description += "\n- Verify alert rules configured for security events";
                description += "\n- Verify action groups configured for notifications";
                description += "\n- Verify Microsoft Sentinel enabled (SIEM solution)";
                
                var severity = totalMonitorableResources > 50 ? AtoFindingSeverity.High :
                               totalMonitorableResources > 10 ? AtoFindingSeverity.Medium : AtoFindingSeverity.Low;
                
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = logAnalyticsWorkspaces[0].Id ?? $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.OperationalInsights/workspaces",
                    ResourceName = workspaceName,
                    FindingType = AtoFindingType.SecurityAssessment,
                    Severity = severity,
                    Title = $"Continuous Monitoring Infrastructure Present - Verify Coverage for {totalMonitorableResources} Resources",
                    Description = description,
                    Recommendation = @"IMMEDIATE ACTION REQUIRED per CA-7 (Continuous Monitoring):

1. **Verify Diagnostic Settings on All Resources**:
   - Azure Portal → Monitor → Diagnostic settings
   - Filter by: Resources without diagnostic settings
   - For each resource type, configure appropriate log categories:
   
   **Key Vaults**:
   - Log category: AuditEvent (all operations: Get, List, Create, Delete)
   - Destination: Log Analytics workspace
   - Retention: 365 days minimum (FedRAMP), 730 days (DoD IL5)
   
   **Storage Accounts**:
   - Blob service: StorageRead, StorageWrite, StorageDelete
   - Table service: StorageRead, StorageWrite, StorageDelete
   - Queue service: StorageRead, StorageWrite, StorageDelete
   - File service: StorageRead, StorageWrite, StorageDelete
   
   **App Services**:
   - AppServiceHTTPLogs: All HTTP requests (status codes, response times)
   - AppServiceConsoleLogs: Application console output
   - AppServiceAuditLogs: App Service management operations
   - AppServiceAppLogs: Application-generated logs
   
   **SQL Servers/Databases**:
   - SQLSecurityAuditEvents: All database access attempts
   - Errors: SQL errors and warnings
   - Timeouts: Query timeouts
   - Blocks: Blocking events
   - Deadlocks: Deadlock occurrences
   
   **Network Security Groups**:
   - NetworkSecurityGroupEvent: NSG rule evaluations
   - NetworkSecurityGroupRuleCounter: Rule hit counts
   - Useful for: Identifying blocked traffic, security rule effectiveness
   
   **Virtual Machines**:
   - Install Azure Monitor Agent (AMA)
   - Data Collection Rules (DCR):
     - Windows: Security events, System events, Application events
     - Linux: Syslog (auth, authpriv, daemon, kern)
     - Performance counters (CPU, memory, disk, network)

2. **Configure Azure Policy for Automatic Diagnostic Settings**:
   - Azure Portal → Policy → Definitions
   - Search: 'Deploy Diagnostic Settings'
   - Assign policies:
     - 'Deploy Diagnostic Settings for Key Vault to Log Analytics workspace'
     - 'Deploy Diagnostic Settings for Storage to Log Analytics workspace'
     - 'Deploy Diagnostic Settings for SQL Databases to Log Analytics workspace'
     - 'Deploy Diagnostic Settings for Network Security Groups to Log Analytics workspace'
   
   - Policy effect: DeployIfNotExists (auto-configure new resources)
   - Remediation: Create remediation task (fix existing resources)
   - Benefits: Ensures all new resources automatically send logs

3. **Deploy Azure Monitor Agents** (VMs and VM Scale Sets):
   - Azure Portal → Virtual Machines → Extensions
   - Install: Azure Monitor Agent (AMA) - replaces legacy agents
   - Create Data Collection Rule (DCR):
     - Data sources: Windows Event Logs, Syslog, Performance counters
     - Destinations: Log Analytics workspace
     - Assign DCR to VMs
   
   - **Windows VMs**:
     - Security events: All events (for IL5) or Common events (for FedRAMP)
     - System events: Error, Warning, Information
     - Application events: Error, Warning
   
   - **Linux VMs**:
     - Syslog: auth, authpriv (authentication), daemon, kern (kernel)
     - Facility severity: Notice, Warning, Error, Critical, Alert, Emergency
   
   - Use Azure Policy to auto-deploy agents:
     - 'Configure Linux virtual machines to run Azure Monitor Agent'
     - 'Configure Windows virtual machines to run Azure Monitor Agent'

4. **Enable Microsoft Defender for Cloud** (If Not Already Enabled):
   - Azure Portal → Defender for Cloud → Environment settings
   - Enable all Defender plans (see CA-2 recommendations for details)
   - Configure continuous export:
     - Export to: Log Analytics workspace
     - Export: Security recommendations, Security alerts, Secure Score
     - Benefits: Historical trending, compliance reporting, SIEM integration

5. **Deploy Microsoft Sentinel** (SIEM for Advanced Threat Detection):
   - Azure Portal → Create Resource → Microsoft Sentinel
   - Select Log Analytics workspace (use existing)
   - Connect data sources:
     - **Azure Activity**: Subscription management operations
     - **Microsoft Entra ID**: Sign-ins, audit logs, identity risks
     - **Microsoft Defender for Cloud**: Security alerts and recommendations
     - **Azure Firewall**: Network traffic logs
     - **Office 365** (if applicable): Email, SharePoint, Teams activity
   
   - Enable analytics rules:
     - Built-in: Microsoft security templates (200+ rules)
     - Anomaly detection: Machine learning-based threat detection
     - Custom rules: KQL queries for specific threats
   
   - Configure automation playbooks:
     - Auto-respond to common incidents (block IP, disable user, notify team)
     - Logic Apps integration (ServiceNow, Slack, PagerDuty)
   
   - Cost: ~$2-$5/GB ingested (additional cost beyond Log Analytics)

6. **Configure Security Alerts and Action Groups**:
   - Azure Monitor → Alerts → Alert rules
   - Create alerts for critical events:
     - **High-Severity Defender Alerts**: Immediate notification
     - **Failed MFA Attempts**: 5+ failures in 30 minutes
     - **Privileged Role Activation**: PIM role assignment
     - **Key Vault Access Spike**: 10x normal access rate
     - **Storage Data Exfiltration**: Unusual download volume
     - **SQL Injection Attempts**: Defender for SQL alerts
   
   - Action Groups (who to notify):
     - Email: Security team, ISSO, ISSM
     - SMS: On-call engineer (critical alerts only)
     - Webhook: ServiceNow (create incidents automatically)
     - Azure Function: Custom auto-response logic
     - Logic App: Complex multi-step response workflows
   
   - Alert suppression: Prevent alert fatigue (group similar alerts)

7. **Implement Log Analytics Queries for Continuous Monitoring**:
   - Azure Portal → Log Analytics Workspace → Logs
   - Save queries for regular reviews:
   
   **Failed Authentication Attempts**:
   SigninLogs
   | where ResultType != 0
   | where TimeGenerated > ago(24h)
   | summarize FailedAttempts=count() by UserPrincipalName, IPAddress, AppDisplayName
   | where FailedAttempts > 5
   | order by FailedAttempts desc
   
   **Key Vault Secret Access**:
   AzureDiagnostics
   | where ResourceType == 'VAULTS'
   | where OperationName == 'SecretGet'
   | where TimeGenerated > ago(7d)
   | summarize AccessCount=count() by CallerIPAddress, identity_claim_upn_s, ResourceId
   | order by AccessCount desc
   
   **Network Security Group Denials**:
   AzureDiagnostics
   | where Category == 'NetworkSecurityGroupRuleCounter'
   | where type_s == 'block'
   | summarize BlockedConnections=count() by NSGName_s, primaryIPv4Address_s, direction_s
   | order by BlockedConnections desc
   
   **SQL Authentication Failures**:
   AzureDiagnostics
   | where ResourceType == 'SERVERS'
   | where Category == 'SQLSecurityAuditEvents'
   | where statement_s contains 'LOGIN FAILED'
   | summarize FailedLogins=count() by server_principal_name_s, client_ip_s
   | order by FailedLogins desc

8. **Create Compliance Dashboards and Workbooks**:
   - Azure Portal → Monitor → Workbooks
   - Use templates or create custom:
     - **Security Overview**: Defender alerts, Secure Score, compliance percentage
     - **Authentication Monitoring**: Failed logins, MFA usage, risky sign-ins
     - **Resource Compliance**: Resources with/without diagnostic settings
     - **Incident Response**: Open incidents, MTTR (mean time to resolve), SLA compliance
   
   - Share with stakeholders:
     - ISSO/ISSM: Weekly security posture review
     - AO: Monthly compliance status
     - System Owner: Daily operational monitoring

9. **Configure Log Analytics Workspace Settings**:
   - Azure Portal → Log Analytics Workspace → Usage and estimated costs
   - **Retention**: 365 days minimum (FedRAMP requirement), 730 days (DoD IL5)
   - **Daily Cap**: Set limit to prevent unexpected costs (alert at 80%)
   - **Data Export**: Continuous export to Storage Account for long-term retention
   - **Access Control**: Reader role for security team, Contributor for admins
   - **Private Link**: Isolate workspace from public internet (IL5 requirement)

10. **Implement Continuous Monitoring Process** (CA-7):
    - **Daily** (Automated):
      - Defender for Cloud scans all resources (200+ assessments)
      - Sentinel analytics rules evaluate logs (detect threats)
      - Security alerts generated for suspicious activities
      - Automated playbooks respond to common incidents
    
    - **Daily** (Manual):
      - Review Defender for Cloud security alerts (triage and investigate)
      - Review Sentinel incidents (assign to analysts)
      - Check for new high/critical recommendations
    
    - **Weekly**:
      - Security team meeting: Review open incidents, discuss trends
      - Tune Sentinel analytics rules (reduce false positives)
      - Review failed authentication reports
      - Update firewall rules based on NSG logs
    
    - **Monthly**:
      - Generate compliance report (Regulatory Compliance dashboard)
      - Update POA&M with latest findings and progress
      - Submit monthly report to AO (required for FedRAMP/DoD)
      - Review Secure Score trends (track improvements)
      - Audit new resources (ensure diagnostic settings enabled)
    
    - **Quarterly**:
      - Update risk assessment (new threats, vulnerabilities)
      - Review control effectiveness (are controls working?)
      - Test incident response procedures (tabletop exercises)
      - Review and optimize Log Analytics costs (query optimization)
      - Update continuous monitoring strategy in SSP

CONTINUOUS MONITORING METRICS (Track in Monthly Reports):
- **Secure Score**: Target 80%+ (FedRAMP High), 90%+ (DoD IL5)
- **Compliance Percentage**: Target 95%+ for all assigned policies
- **Open Security Alerts**: Count by severity (trend should decrease)
- **Mean Time to Detect (MTTD)**: Target <1 hour for critical threats
- **Mean Time to Respond (MTTR)**: Target <4 hours for critical incidents
- **POA&M Items Closed**: Track remediation velocity
- **Resources with Diagnostic Settings**: Target 100%
- **Unresolved Vulnerabilities**: Count by severity, age

CONTINUOUS MONITORING REQUIREMENTS (FedRAMP/DoD):
- **Log Retention**: 365 days minimum (FedRAMP), 730 days (DoD IL5)
- **Monitoring Coverage**: 100% of system components (all resources)
- **Alert Response**: 15 minutes (critical), 1 hour (high), 4 hours (medium) per DoD SRG
- **Monthly Reporting**: Submit to AO (security posture, new findings, POA&M updates)
- **Annual Assessment**: Full control re-assessment by 3PAO or assessor
- **Significant Change**: Re-assessment for major system changes

COST OPTIMIZATION TIPS:
- **Data Collection Rules**: Only collect logs you'll use (avoid verbose logging)
- **Log Analytics Commitment Tiers**: Prepay for 100GB+/day (30% discount)
- **Archive Tier**: Move old logs to low-cost storage (compliance retention)
- **Query Optimization**: Use time ranges, summarize early (reduce scanned data)
- **Alert Tuning**: Reduce false positives (save investigation time)

REFERENCES:
- NIST 800-53 CA-7: Continuous Monitoring
- NIST 800-137: Information Security Continuous Monitoring (ISCM)
- FedRAMP Continuous Monitoring Requirements: https://www.fedramp.gov/assets/resources/documents/CSP_Continuous_Monitoring_Strategy_Guide.pdf
- Azure Monitor Best Practices: https://docs.microsoft.com/azure/azure-monitor/best-practices
- Microsoft Sentinel Documentation: https://docs.microsoft.com/azure/sentinel/",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    AffectedNistControls = new List<string> { control.Id ?? "CA-7", "CA-7(1)" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning continuous monitoring for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanGenericSecurityAssessmentAsync(
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
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Subscription",
                ResourceName = "Security Assessment Review",
                FindingType = AtoFindingType.SecurityAssessment,
                Severity = AtoFindingSeverity.Informational,
                Title = $"Control {control.Id} - Manual Review Required",
                Description = control.Title ?? "Security assessment control requires manual review",
                Recommendation = "Review security assessment and authorization documentation",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "CA" },
                ComplianceFrameworks = new List<string> { "NIST-800-53" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning generic security assessment for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }
}
