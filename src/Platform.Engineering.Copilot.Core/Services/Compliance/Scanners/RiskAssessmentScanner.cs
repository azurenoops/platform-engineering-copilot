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
/// Scanner for Risk Assessment (RA) family controls using real Azure APIs
/// </summary>
public class RiskAssessmentScanner : IComplianceScanner
{
    private readonly ILogger _logger;
    private readonly IAzureResourceService _azureService;

    public RiskAssessmentScanner(ILogger logger, IAzureResourceService azureService)
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

        // CRITICAL: Control IDs from NIST catalog are lowercase (ra-3, ra-5, etc.)
        // Use case-insensitive comparison
        var controlId = control.Id?.ToUpperInvariant();

        switch (controlId)
        {
            case "RA-3":
                findings.AddRange(await ScanRiskAssessmentAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            case "RA-5":
                findings.AddRange(await ScanVulnerabilityScanningAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;

            default:
                findings.AddRange(await ScanGenericRiskAssessmentAsync(subscriptionId, resourceGroupName, control, cancellationToken));
                break;
        }

        // Enrich all findings with auto-remediation information
        return findings.WithAutoRemediationInfo();
    }


    private async Task<List<AtoFinding>> ScanRiskAssessmentAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogInformation("Scanning risk assessment (RA-3) for {Scope} in subscription {SubscriptionId}", 
                scope, subscriptionId);
            
            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            // Note: Querying Defender for Cloud Secure Score and compliance assessments
            // requires Microsoft.Security/secureScores and Microsoft.Security/regulatoryComplianceStandards
            // These require additional API calls beyond basic resource listing
            
            var description = $@"RA-3 requires comprehensive risk assessment of information systems and environments. Subscription contains {resources.Count} resources requiring risk assessment.

**Risk Assessment Requirements (NIST 800-53 RA-3)**:
- **Frequency**: Annual minimum (FedRAMP), Quarterly recommended (DoD IL4+), Continuous (with significant changes)
- **Scope**: All information systems, applications, and supporting infrastructure
- **Methodology**: NIST SP 800-30 Risk Management Guide
- **Components**:
  - Threat identification (NIST 800-30 Table D-2)
  - Vulnerability identification (scanning, assessments)
  - Likelihood determination (High/Medium/Low)
  - Impact analysis (confidentiality, integrity, availability)
  - Risk determination (likelihood × impact)
  - Risk response (accept, mitigate, transfer, avoid)

**Microsoft Defender for Cloud Integration**:
- **Secure Score**: Overall security posture (0-100%)
- **Regulatory Compliance**: FedRAMP, NIST 800-53, PCI-DSS, SOC 2 assessments
- **Security Recommendations**: 200+ automated security checks
- **Attack Path Analysis**: Multi-step attack scenario identification

**Manual Verification Required**:
This scan identifies the need for risk assessment but cannot automatically perform the assessment. Manual steps:

1. Azure Portal → Microsoft Defender for Cloud → Overview → Secure Score
2. Review current score and recommendations
3. Regulatory compliance → FedRAMP High / NIST 800-53 → Review assessment status
4. Recommendations → Review high/critical security findings
5. Document risk assessment findings in SSP
6. Update risk assessment report (annual minimum)";

            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Microsoft.Security/assessments",
                ResourceName = "Risk Assessment",
                FindingType = AtoFindingType.RiskAssessment,
                Severity = AtoFindingSeverity.High,
                Title = $"Risk Assessment Required: {resources.Count} Resources Need Assessment (RA-3)",
                Description = description,
                Recommendation = @"IMMEDIATE ACTION REQUIRED per RA-3 (Risk Assessment):

1. **Enable Microsoft Defender for Cloud** (Comprehensive risk assessment):
   - Azure Portal → Microsoft Defender for Cloud
   - Enable all Defender plans (Servers, Containers, SQL, App Service, Storage, Key Vault, ARM, DNS)
   - **Secure Score**: Provides automated security posture assessment
   - **Benefits**:
     - Continuous security assessment
     - 200+ security recommendations
     - Regulatory compliance dashboards (FedRAMP, NIST 800-53)
     - Attack path analysis
     - Cloud security graph

2. **Review Secure Score** (Overall security posture):
   - Defender for Cloud → Overview → Secure Score
   - Current score: X/100 (goal: 80%+ for FedRAMP)
   - **Score Calculation**: (Implemented recommendations points / Total available points) × 100
   - **Categories**:
     - Enable MFA (highest impact)
     - Secure management ports
     - Apply system updates
     - Enable encryption
     - Remediate vulnerabilities
     - Restrict network access
   - **Actions**: Implement recommendations to improve score

3. **Enable Regulatory Compliance Assessment**:
   - Defender for Cloud → Regulatory compliance
   - Add standard: FedRAMP High, NIST 800-53 Rev 5, DoD IL5
   - **Assessment**: Automated check of 200+ controls
   - **Status**: Pass/Fail for each control
   - **Export**: PDF compliance report for AO
   - **Frequency**: Continuous assessment, daily updates

4. **Conduct Formal Risk Assessment** (NIST SP 800-30):
   - **Step 1: Prepare for Assessment**
     - Identify purpose (ATO, annual review, system change)
     - Define scope (subscription, specific systems)
     - Identify stakeholders (System Owner, ISSO, ISSM, AO)
     - Gather documentation (architecture diagrams, data flows, security controls)
   
   - **Step 2: Conduct Assessment**
     - **Threat Identification**: Use NIST 800-30 Appendix D threat sources
       - Adversarial (APT, insider threat, hacktivists)
       - Accidental (user error, administrator error)
       - Structural (equipment failure, software bug)
       - Environmental (natural disaster, power failure)
     
     - **Vulnerability Identification**: Sources
       - Defender for Cloud recommendations (200+ checks)
       - Vulnerability scan results (Qualys, Nessus)
       - Penetration test findings (annual requirement)
       - Security control assessment results
       - Known CVEs in deployed software
     
     - **Likelihood Determination** (per NIST 800-30 Table H-2):
       - High (>= 0.7): Expected to occur multiple times per year
       - Medium (0.2-0.7): Expected to occur once per year
       - Low (<= 0.2): Expected to occur less than once per year
       - Factors: Threat capability, intent, targeting; control effectiveness
     
     - **Impact Analysis** (per FIPS 199):
       - High: Severe or catastrophic adverse effect
       - Medium: Serious adverse effect
       - Low: Limited adverse effect
       - Categories: Confidentiality, Integrity, Availability
     
     - **Risk Determination** (likelihood × impact matrix):
       - Very High: Likelihood High + Impact High
       - High: Likelihood High + Impact Medium OR Likelihood Medium + Impact High
       - Medium: Likelihood High + Impact Low OR Likelihood Medium + Impact Medium OR Likelihood Low + Impact High
       - Low: Likelihood Medium + Impact Low OR Likelihood Low + Impact Medium
       - Very Low: Likelihood Low + Impact Low
   
   - **Step 3: Communicate Results**
     - Risk Assessment Report (formal document)
     - Risk Register (spreadsheet tracking all risks)
     - Executive Summary (for AO and management)
     - Plan of Action & Milestones (POA&M) for identified risks
   
   - **Step 4: Maintain Assessment**
     - Update quarterly or with significant system changes
     - Track risk status (new, open, mitigated, closed)
     - Re-assess after control implementation

5. **Create Risk Register** (Track all identified risks):
   - **Columns**:
     - Risk ID: Unique identifier (RISK-001, RISK-002)
     - Risk Description: Brief description of risk
     - Threat Source: What could cause this risk
     - Vulnerability: Weakness being exploited
     - Likelihood: High/Medium/Low
     - Impact: High/Medium/Low (C/I/A)
     - Risk Level: Overall risk determination
     - Controls in Place: Existing mitigations
     - Residual Risk: Risk after controls applied
     - Risk Response: Accept/Mitigate/Transfer/Avoid
     - Owner: Responsible party
     - Status: Open/In Progress/Closed
     - Target Closure Date: When risk will be addressed
   
   - **Example Risks**:
     - RISK-001: Unpatched VMs vulnerable to exploit (High likelihood, High impact)
     - RISK-002: SQL databases accessible from public Internet (Medium likelihood, High impact)
     - RISK-003: No MFA on privileged accounts (High likelihood, Critical impact)

6. **Develop Plan of Action & Milestones (POA&M)**:
   - For each identified risk with residual risk >= Medium
   - **POA&M Items**:
     - Control ID: IA-2(1) - MFA for privileged accounts
     - Weakness: No MFA enabled on 15 admin accounts
     - Risk: High (credential compromise could lead to full tenant takeover)
     - Remediation: Enable Conditional Access policy requiring MFA
     - Resources Required: Azure AD Premium P1 licenses
     - Scheduled Completion Date: 30 days from assessment
     - Milestones: Week 1 (policy created), Week 2 (testing), Week 3 (production)
     - Status: In Progress

7. **Implement Attack Path Analysis**:
   - Defender for Cloud → Cloud Security Explorer
   - Query: ""Attack paths to critical assets""
   - **Analysis**: Multi-step attack scenarios
   - **Example**: Internet-exposed VM → Lateral movement → SQL server with sensitive data
   - **Remediation**: Address each step in attack path (NSG rules, JIT access, network segmentation)

8. **Configure Continuous Security Assessment**:
   - Defender for Cloud → Environment settings → Continuous export
   - Export security recommendations to Log Analytics
   - Create Azure Monitor alerts for new High/Critical findings
   - **Automation**: Azure Logic Apps to create POA&M items from findings

9. **Conduct Risk Assessment Workshops** (Quarterly):
   - **Participants**: System Owner, ISSO, ISSM, DevOps team, Security team
   - **Agenda**:
     - Review Secure Score trends (improving or declining?)
     - Review new security findings since last assessment
     - Discuss changes to system (new features, integrations)
     - Re-assess likelihood and impact for existing risks
     - Identify new risks from threat intelligence
     - Update risk register and POA&M
   - **Output**: Updated risk assessment report

10. **Document in SSP** (System Security Plan):
    - Risk assessment methodology (NIST SP 800-30)
    - Risk assessment frequency (annual minimum)
    - Risk assessment roles (lead assessor, participants)
    - Risk appetite and tolerance levels (AO-defined)
    - Risk register (all identified risks)
    - POA&M (mitigation plans for unacceptable risks)
    - Risk assessment report (latest annual assessment)
    - Secure Score trends (quarterly snapshots)

RISK ASSESSMENT REQUIREMENTS (FedRAMP/DoD):
- **Frequency**: Annual minimum (FedRAMP), Quarterly (DoD IL4+), Continuous monitoring
- **Methodology**: NIST SP 800-30 Rev 1 Risk Management Guide
- **Scope**: All systems, applications, and infrastructure components
- **Documentation**: Risk Assessment Report, Risk Register, POA&M
- **Approval**: Authorizing Official (AO) must review and accept risks
- **Updates**: With significant system changes, new threats, or control changes

DEFENDER FOR CLOUD SECURE SCORE TARGETS:
- **Minimum**: 60% (basic security hygiene)
- **FedRAMP Moderate**: 70% (good security posture)
- **FedRAMP High**: 80%+ (strong security posture)
- **DoD IL5**: 90%+ (very strong security posture)
- **Best Practice**: Monthly review, target 2-5% improvement per quarter

RISK ASSESSMENT TOOLS:
- **Microsoft Defender for Cloud**: Secure Score, Regulatory Compliance, Attack Path Analysis
- **NIST SP 800-30**: Risk Management Guide (methodology)
- **NIST SP 800-37**: RMF Guide (overall risk management framework)
- **Threat Modeling**: Microsoft Threat Modeling Tool, STRIDE methodology
- **Attack Simulation**: Assume Breach exercises, Red Team assessments

REFERENCES:
- NIST 800-53 RA-3: Risk Assessment
- NIST 800-53 RA-3(1): Supply Chain Risk Assessment
- NIST SP 800-30 Rev 1: Guide for Conducting Risk Assessments
- NIST SP 800-37 Rev 2: Risk Management Framework
- FIPS 199: Standards for Security Categorization
- FedRAMP Risk Assessment Template: https://www.fedramp.gov/assets/resources/templates/
- Defender for Cloud Secure Score: https://docs.microsoft.com/azure/defender-for-cloud/secure-score-security-controls",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "RA-3", "RA-3(1)" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC", "NIST-800-30" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning risk assessment for subscription {SubscriptionId}", subscriptionId);
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Subscription",
                ResourceName = "Risk Assessment Scan",
                FindingType = AtoFindingType.RiskAssessment,
                Severity = AtoFindingSeverity.High,
                Title = "Risk Assessment Scan Error - Manual Review Required",
                Description = $"Could not complete automated risk assessment scan: {ex.Message}. Manual review required to verify RA-3 compliance.",
                Recommendation = "Manually conduct risk assessment using NIST SP 800-30 methodology. Review Defender for Cloud Secure Score and regulatory compliance assessments per RA-3 requirements.",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "RA-3" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                DetectedAt = DateTime.UtcNow
            });
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanVulnerabilityScanningAsync(
        string subscriptionId,
        string? resourceGroupName,
        NistControl control, 
        CancellationToken cancellationToken)
    {
        var findings = new List<AtoFinding>();

        try
        {
            var scope = string.IsNullOrEmpty(resourceGroupName) ? "subscription" : $"resource group '{resourceGroupName}'";
            _logger.LogInformation("Scanning vulnerability scanning (RA-5) for {Scope} in subscription {SubscriptionId}", 
                scope, subscriptionId);
            
            var armClient = _azureService.GetArmClient();
            var resources = string.IsNullOrEmpty(resourceGroupName)
                ? await _azureService.ListAllResourcesAsync(subscriptionId)
                : await _azureService.ListAllResourcesAsync(subscriptionId, resourceGroupName);
            
            // Get resources that require vulnerability scanning
            var virtualMachines = resources.Where(r => 
                r.Type?.Equals("Microsoft.Compute/virtualMachines", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            var containerRegistries = resources.Where(r => 
                r.Type?.Equals("Microsoft.ContainerRegistry/registries", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            var sqlServers = resources.Where(r => 
                r.Type?.Equals("Microsoft.Sql/servers", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
                
            var appServices = resources.Where(r => 
                r.Type?.Equals("Microsoft.Web/sites", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
                
            var storageAccounts = resources.Where(r => 
                r.Type?.Equals("Microsoft.Storage/storageAccounts", StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            var totalResources = virtualMachines.Count + containerRegistries.Count + sqlServers.Count + 
                                appServices.Count + storageAccounts.Count;

            if (totalResources == 0)
            {
                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Security/assessments",
                    ResourceName = "Vulnerability Scanning",
                    FindingType = AtoFindingType.RiskAssessment,
                    Severity = AtoFindingSeverity.Informational,
                    Title = "No Resources Requiring Vulnerability Scanning",
                    Description = "No VMs, container registries, SQL servers, App Services, or storage accounts found. RA-5 requires vulnerability scanning for information systems and applications.",
                    Recommendation = @"As resources are deployed, enable vulnerability scanning per RA-5:

1. **Microsoft Defender for Servers**: VM vulnerability assessment
2. **Microsoft Defender for Containers**: Container image scanning
3. **Microsoft Defender for SQL**: SQL vulnerability assessment
4. **Microsoft Defender for App Service**: Web application scanning
5. **Microsoft Defender for Storage**: Malware scanning

REFERENCES:
- NIST 800-53 RA-5: Vulnerability Monitoring and Scanning
- Azure Defender Plans: https://docs.microsoft.com/azure/defender-for-cloud/",
                    ComplianceStatus = AtoComplianceStatus.NotApplicable,
                    AffectedNistControls = new List<string> { control.Id ?? "RA-5" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                    DetectedAt = DateTime.UtcNow
                });
            }
            else
            {
                // Note: Querying Defender for Cloud assessments requires Microsoft.Security provider
                // This would need additional API calls to check Defender plan status
                // For now, we'll provide comprehensive guidance based on resource presence
                
                var description = $@"Found {totalResources} resources requiring vulnerability scanning per RA-5:

**Resource Inventory**:
- Virtual Machines: {virtualMachines.Count} (require Defender for Servers)
- Container Registries: {containerRegistries.Count} (require Defender for Containers)
- SQL Servers: {sqlServers.Count} (require Defender for SQL)
- App Services: {appServices.Count} (require Defender for App Service)
- Storage Accounts: {storageAccounts.Count} (require Defender for Storage)

**RA-5 Requirements**:
- Vulnerability scanning: Weekly minimum (FedRAMP Moderate), Daily recommended (FedRAMP High)
- Scan types: Authenticated scans, network scans, application scans
- Remediation timeline: 30 days (high), 90 days (moderate), 120 days (low)
- Reporting: Monthly vulnerability reports to AO
- Tools: SCAP-compliant scanners (NIST-certified)

**Microsoft Defender for Cloud**:
Microsoft Defender for Cloud provides continuous vulnerability assessment and security recommendations. Enable Defender plans to meet RA-5 requirements for automated vulnerability scanning.

**Manual Verification Required**:
This scan detects resources requiring vulnerability scanning but cannot automatically verify Defender plan enablement. Manual verification steps:

1. Azure Portal → Microsoft Defender for Cloud → Environment settings
2. Select subscription → Defender plans
3. Verify enabled plans: Servers, Containers, SQL, App Service, Storage
4. Check Security recommendations for vulnerability findings
5. Review Secure Score for overall security posture
6. Validate continuous export to Log Analytics for compliance reporting";

                var severity = totalResources > 50 ? AtoFindingSeverity.High :
                               totalResources > 10 ? AtoFindingSeverity.Medium : AtoFindingSeverity.Low;

                findings.Add(new AtoFinding
                {
                    Id = Guid.NewGuid().ToString(),
                    SubscriptionId = subscriptionId,
                    ResourceId = $"/subscriptions/{subscriptionId}",
                    ResourceType = "Microsoft.Security/assessments",
                    ResourceName = "Vulnerability Scanning",
                    FindingType = AtoFindingType.RiskAssessment,
                    Severity = severity,
                    Title = $"Vulnerability Scanning Required: {totalResources} Resources Need Assessment (RA-5)",
                    Description = description,
                    Recommendation = @"IMMEDIATE ACTION REQUIRED per RA-5 (Vulnerability Monitoring and Scanning):

1. **Enable Microsoft Defender for Servers** (VMs vulnerability assessment):
   - Azure Portal → Microsoft Defender for Cloud → Environment settings
   - Select subscription → Defender plans → Servers: On
   - Choose plan tier: Plan 2 (includes vulnerability assessment)
   - **Features included**:
     - Qualys vulnerability scanner (agentless or agent-based)
     - Microsoft Defender Vulnerability Management (built-in)
     - File integrity monitoring
     - Adaptive application controls
     - Just-in-time VM access
   - **Scans**: Automatically scans VMs every 24 hours
   - **Cost**: ~$15/server/month (varies by region)

2. **Enable Microsoft Defender for Containers** (Container image scanning):
   - Defender plans → Containers: On
   - **Features included**:
     - Container Registry scanning (ACR images)
     - Kubernetes cluster protection
     - Runtime threat detection
     - Image vulnerability assessment (CVE database)
   - **Scans**: Automatic on image push, daily re-scan of registry
   - **Integration**: CI/CD pipelines, admission controllers

3. **Enable Microsoft Defender for SQL** (Database vulnerability assessment):
   - Defender plans → SQL: On (covers Azure SQL Database and SQL on VMs)
   - **Features included**:
     - Vulnerability Assessment: Scans for misconfigurations
     - Advanced Threat Protection: Detects anomalous activities
     - Data classification and sensitivity labeling
   - **Scans**: Weekly automatic scans, on-demand scans
   - **Baselines**: Define accepted security posture, alert on deviations

4. **Enable Microsoft Defender for App Service** (Web application scanning):
   - Defender plans → App Service: On
   - **Features included**:
     - Web application vulnerability scanning
     - Threat detection (SQL injection, XSS, RCE)
     - Security recommendations for App Service configuration
   - **Monitoring**: Runtime threat detection, suspicious requests

5. **Enable Microsoft Defender for Storage** (Malware scanning):
   - Defender plans → Storage: On
   - **Features included**:
     - Malware scanning (uploaded files)
     - Threat detection (unusual access patterns)
     - Sensitive data threat detection
   - **Scans**: On-upload scanning, activity analysis

6. **Configure Vulnerability Assessment Settings**:
   - Defender for Cloud → Recommendations → Remediate vulnerabilities
   - Set scan frequency: Daily (recommended for FedRAMP High)
   - Enable automatic remediation: Critical and High findings
   - Configure notifications: Email SOC on new High/Critical findings
   - Export findings: Send to Log Analytics, Sentinel, SIEM

7. **Deploy Additional Scanning Tools** (SCAP compliance):
   - **For Windows VMs**:
     - DISA STIG Scanner (Government-provided)
     - Microsoft Baseline Security Analyzer (MBSA)
     - Tenable Nessus Professional (SCAP-certified)
     - Rapid7 InsightVM (SCAP-certified)
   
   - **For Linux VMs**:
     - OpenSCAP (NIST-certified, open source)
     - Tenable Nessus (cross-platform)
     - Qualys VMDR (Defender for Cloud integration)
   
   - **Deployment**: Azure VM extensions, configuration management tools

8. **Establish Vulnerability Management Process**:
   - **Weekly**: Review new vulnerability findings
   - **Monthly**: Generate vulnerability report for AO
   - **Quarterly**: Update vulnerability management plan
   - **Remediation Timelines** (per NIST 800-53 RA-5):
     - Critical vulnerabilities: 15 days maximum
     - High vulnerabilities: 30 days maximum
     - Moderate vulnerabilities: 90 days maximum
     - Low vulnerabilities: 120 days maximum
   - **Exception Process**: Document and AO-approve any deviations

9. **Configure Continuous Export** (Compliance reporting):
   - Defender for Cloud → Environment settings → Continuous export
   - Export to Log Analytics workspace: All recommendations and alerts
   - Export frequency: Streaming (real-time)
   - Export data: Security recommendations, Secure Score, Regulatory compliance
   - **Purpose**: Compliance reporting, audit evidence, trend analysis

10. **Document in SSP** (System Security Plan):
    - Vulnerability scanning tools and versions
    - Scan frequency and scope (all systems, monthly minimum)
    - Remediation timeline by severity
    - Exception process and approvals
    - Vulnerability management roles (scanner, reviewer, remediator)
    - Monthly vulnerability metrics (total, new, remediated, exceptions)

VULNERABILITY SCANNING REQUIREMENTS (FedRAMP/DoD):
- **Scan Frequency**: Monthly minimum (FedRAMP Moderate), Weekly minimum (FedRAMP High), Daily recommended
- **Scan Types**: Authenticated network scans, operating system scans, database scans, web application scans
- **Scanner Requirements**: SCAP-compliant (NIST-certified), CVE database updated weekly
- **Remediation Timeline**: 30 days (High), 90 days (Moderate), 120 days (Low)
- **Reporting**: Monthly report to AO with vulnerability statistics
- **False Positives**: Documented and re-verified quarterly

DEFENDER FOR CLOUD PRICING (Estimates):
- **Servers**: ~$15/VM/month (Plan 2 with vulnerability assessment)
- **Containers**: ~$7/vCore/month (AKS), registry scanning included
- **SQL**: ~$15/server/month (Azure SQL), ~$15/VM/month (SQL on VM)
- **App Service**: ~$0.02/App Service/hour (~$15/month)
- **Storage**: ~$10/storage account/month (malware scanning)
- **Total**: Varies by resource count, typically 2-5% of infrastructure cost

INTEGRATION WITH CI/CD:
- **Azure DevOps**: Defender for DevOps extension (code scanning)
- **GitHub**: GitHub Advanced Security integration
- **Container Registries**: ACR Task integration for image scanning
- **Policy Enforcement**: Azure Policy to require Defender enablement

REFERENCES:
- NIST 800-53 RA-5: Vulnerability Monitoring and Scanning
- NIST 800-53 RA-5(1): Update Tool Capability
- NIST 800-53 RA-5(2): Update Vulnerabilities to be Scanned
- NIST 800-53 RA-5(5): Privileged Access (authenticated scans)
- FedRAMP Vulnerability Scanning Requirements
- SCAP-Validated Tools: https://nvd.nist.gov/scap/validated-tools
- Microsoft Defender for Cloud Pricing: https://azure.microsoft.com/pricing/details/defender-for-cloud/
- Defender for Cloud Documentation: https://docs.microsoft.com/azure/defender-for-cloud/",
                    ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                    AffectedNistControls = new List<string> { control.Id ?? "RA-5", "RA-5(1)", "RA-5(2)", "RA-5(5)" },
                    ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP", "DoD SRG", "CMMC" },
                    DetectedAt = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning vulnerability scanning for subscription {SubscriptionId}", subscriptionId);
            
            findings.Add(new AtoFinding
            {
                Id = Guid.NewGuid().ToString(),
                SubscriptionId = subscriptionId,
                ResourceId = $"/subscriptions/{subscriptionId}",
                ResourceType = "Subscription",
                ResourceName = "Vulnerability Scanning Scan",
                FindingType = AtoFindingType.RiskAssessment,
                Severity = AtoFindingSeverity.High,
                Title = "Vulnerability Scanning Scan Error - Manual Review Required",
                Description = $"Could not complete automated vulnerability scanning assessment: {ex.Message}. Manual review required to verify RA-5 compliance.",
                Recommendation = "Manually verify Microsoft Defender for Cloud is enabled with appropriate plans (Servers, Containers, SQL, App Service, Storage) per RA-5 requirements.",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "RA-5" },
                ComplianceFrameworks = new List<string> { "NIST-800-53", "FedRAMP" },
                DetectedAt = DateTime.UtcNow
            });
        }

        return findings;
    }

    private async Task<List<AtoFinding>> ScanGenericRiskAssessmentAsync(
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
                ResourceName = "Risk Assessment Review",
                FindingType = AtoFindingType.RiskAssessment,
                Severity = AtoFindingSeverity.Informational,
                Title = $"Control {control.Id} - Manual Review Required",
                Description = control.Title ?? "Risk assessment control requires manual review",
                Recommendation = "Review risk management framework and assessment procedures",
                ComplianceStatus = AtoComplianceStatus.ManualReviewRequired,
                AffectedNistControls = new List<string> { control.Id ?? "RA" },
                ComplianceFrameworks = new List<string> { "NIST-800-53" },
                DetectedAt = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning generic risk assessment for subscription {SubscriptionId}", subscriptionId);
        }

        return findings;
    }
}
