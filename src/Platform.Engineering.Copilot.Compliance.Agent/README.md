# Compliance Agent

> NIST 800-53 compliance assessment, security scanning, and automated remediation specialist

## Overview

The Compliance Agent is a specialized AI agent that performs comprehensive compliance assessments against NIST 800-53, FedRAMP, DoD Cloud SRG, and other security frameworks. It scans Azure resources, identifies gaps, and provides automated remediation.

**Agent Type**: `Compliance`  
**Icon**: 🛡️  
**Temperature**: 0.2 (high precision for compliance assessments)

## Capabilities

### 1. Compliance Frameworks

#### NIST 800-53 Rev 5
Complete support for all **18 control families** (1000+ controls):

| Family | Controls | Description |
|--------|----------|-------------|
| **AC** | Access Control | User accounts, least privilege, separation of duties |
| **AU** | Audit and Accountability | Logging, audit records, monitoring |
| **AT** | Awareness and Training | Security training programs |
| **CM** | Configuration Management | Baseline configurations, change control |
| **CP** | Contingency Planning | Disaster recovery, backup |
| **IA** | Identification and Authentication | User authentication, MFA |
| **IR** | Incident Response | Security incident handling |
| **MA** | Maintenance | System maintenance, tools |
| **MP** | Media Protection | Media sanitization, disposal |
| **PE** | Physical and Environmental | Physical security controls |
| **PL** | Planning | Security planning, architecture |
| **PS** | Personnel Security | Background checks, termination |
| **RA** | Risk Assessment | Vulnerability scanning, risk analysis |
| **CA** | Security Assessment | Testing, assessments, authorization |
| **SC** | System and Communications | Boundary protection, encryption |
| **SI** | System and Information Integrity | Flaw remediation, malware protection |
| **SA** | System and Services Acquisition | Secure development, supply chain |
| **PM** | Program Management | Risk management, critical infrastructure |

#### FedRAMP Baselines
- **FedRAMP High**: 421 controls for high-impact systems
- **FedRAMP Moderate**: 325 controls for moderate-impact systems
- **FedRAMP Low**: 125 controls for low-impact systems
- **Tailored for LI-SaaS**: 133 controls for low-impact SaaS

#### Other Frameworks
- **NIST 800-171**: CUI protection (110 controls)
- **DoD Cloud Computing SRG**: Impact Levels IL2-IL6
- **ISO 27001**: Information security management
- **SOC 2 Type II**: Trust service criteria
- **PCI-DSS**: Payment card industry standards
- **HIPAA**: Healthcare data protection

### 2. Security Scanning

#### Azure Resource Scanning
Evaluate deployed Azure resources against compliance policies:

- **Storage Accounts**: Encryption, public access, firewall rules
- **Virtual Machines**: OS patching, endpoint protection, disk encryption
- **AKS Clusters**: RBAC, network policies, pod security
- **Key Vaults**: Access policies, soft delete, purge protection
- **SQL Databases**: TDE, firewall rules, auditing
- **Networking**: NSG rules, private endpoints, DDoS protection
- **Identity**: RBAC assignments, managed identities, MFA

#### Code Security Scanning
Analyze source code repositories for vulnerabilities:

- **Secrets Detection**: Hardcoded credentials, API keys, tokens
- **Dependency Scanning**: Vulnerable packages, outdated libraries
- **SAST (Static Analysis)**: Security anti-patterns, code smells
- **License Compliance**: Open source license validation
- **STIG Compliance**: Security Technical Implementation Guide checks

#### Azure Defender Integration
Leverage Microsoft Defender for Cloud:

- **Security Score**: Overall security posture (0-100)
- **Recommendations**: Actionable security improvements
- **Alerts**: Active security threats and incidents
- **Regulatory Compliance**: Dashboard view of compliance status
- **Secure Score**: Track security improvements over time

### 3. Gap Analysis

Identify non-compliant controls and missing implementations:

```
Gap Analysis Report:
✅ Compliant: 387 controls (92%)
❌ Non-Compliant: 23 controls (5%)
⚠️  Partially Compliant: 11 controls (3%)

Critical Gaps:
- AC-2: Missing account review process
- AU-2: Insufficient audit logging
- SC-7: Public endpoints detected
- IA-2: MFA not enforced

Remediation Priority:
1. High: SC-7 (7 resources with public access)
2. High: IA-2 (5 users without MFA)
3. Medium: AU-2 (3 resource groups missing diagnostics)
```

### 4. Automated Remediation

Generate and apply fixes for compliance violations:

#### Azure Policy Deployment
```bicep
// Auto-generated policy for AU-2
resource auditPolicy 'Microsoft.Authorization/policyDefinitions@2021-06-01' = {
  name: 'enforce-diagnostic-settings'
  properties: {
    policyType: 'Custom'
    mode: 'All'
    policyRule: {
      if: {
        field: 'type'
        equals: 'Microsoft.Storage/storageAccounts'
      }
      then: {
        effect: 'DeployIfNotExists'
        details: {
          type: 'Microsoft.Insights/diagnosticSettings'
          // ...
        }
      }
    }
  }
}
```

#### Resource Configuration Updates
```bicep
// Fix SC-7: Disable public access
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: 'data001'
  properties: {
    allowBlobPublicAccess: false // ✅ Remediated
    publicNetworkAccess: 'Disabled' // ✅ Remediated
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
    }
  }
}
```

#### Remediation Scripts
PowerShell, Azure CLI, or Terraform scripts for bulk remediation.

### 5. Evidence Collection

Automated evidence gathering for compliance audits:

**Evidence Types:**
- **Screenshots**: Azure Portal configurations
- **Configuration Files**: JSON/YAML exports
- **Logs**: Audit logs, activity logs, diagnostic logs
- **Reports**: Compliance scan results, vulnerability reports
- **Policies**: Azure Policy assignments and definitions

**Storage:**
- Azure Blob Storage with versioning
- Organized by control family and control ID
- Tamper-evident with immutable storage
- Retention policies aligned with compliance requirements

**Control-Specific Collectors:**
- **AC-2 (Account Management)**: User lists, RBAC assignments
- **AU-2 (Audit Events)**: Log Analytics queries, retention settings
- **SC-7 (Boundary Protection)**: NSG rules, firewall configurations
- **IA-2 (Authentication)**: MFA status, authentication methods

## Plugins

### CompliancePlugin

Main plugin for compliance assessments and scanning.

**Functions:**
- `run_compliance_scan` - Execute NIST 800-53 compliance scan
- `assess_control` - Assess specific control implementation
- `get_compliance_status` - Get overall compliance status
- `generate_gap_analysis` - Identify compliance gaps
- `remediate_control` - Apply automated remediation
- `collect_evidence` - Gather evidence for controls
- `export_compliance_report` - Generate compliance reports

### ConfigurationPlugin

Azure subscription and configuration management.

**Functions:**
- `set_azure_subscription` - Set active subscription
- `get_azure_subscription` - Get current subscription

## Sub-Agents

### CodeScanningAgent

Specialized for repository and code analysis.

**Capabilities:**
- Scan GitHub/Azure DevOps repositories
- Detect secrets and credentials
- Analyze dependencies for vulnerabilities
- STIG compliance checking
- Generate remediation pull requests

### DocumentAgent

Generate compliance documentation (see [Document Agent README](../Platform.Engineering.Copilot.Document.Agent/README.md)).

### AtoPreparationAgent

Orchestrate ATO package preparation (see [ATO Preparation Agent README](./ATO-AGENT.md)).

## Example Prompts

### Compliance Scanning

```
"Run NIST 800-53 compliance scan on subscription xyz"
"Assess FedRAMP High baseline compliance for rg-prod"
"Check DoD IL5 compliance for AKS cluster"
"Scan resource group for PCI-DSS violations"
"Evaluate HIPAA compliance for healthcare resources"
```

### Control Assessment

```
"Assess AC-2 (Account Management) implementation"
"Check AU-2 (Audit Events) compliance"
"Evaluate SC-7 (Boundary Protection) for my VNets"
"Is IA-2 (MFA) properly configured?"
"Show me SC-13 (Cryptographic Protection) status"
```

### Gap Analysis

```
"Show compliance gaps for FedRAMP Moderate"
"What controls are failing in production?"
"Generate gap analysis report for DoD IL4"
"Prioritize remediation by severity"
```

### Code & Repository Scanning

```
"Scan this repository for security vulnerabilities"
"Check for hardcoded secrets in code"
"Analyze dependencies for CVEs"
"Run STIG compliance check on this codebase"
"Find all TODO comments related to security"
```

### Remediation

```
"Remediate AC-2 control failures"
"Fix all storage accounts with public access"
"Generate Azure Policy for AU-2 enforcement"
"Create remediation script for SC-7 violations"
"Apply automated fixes for non-compliant resources"
```

### Evidence Collection

```
"Collect evidence for AU-2 (Audit Events)"
"Gather screenshots for SC-7 configuration"
"Export compliance evidence for FedRAMP audit"
"Save RBAC assignments as evidence for AC-2"
```

## Key Services

| Service | Purpose |
|---------|---------|
| `ComplianceEngine` | Core compliance scanning logic |
| `ComplianceValidationService` | Control validation and assessment |
| `CodeScanningEngine` | Repository and code analysis |
| `GovernanceEngine` | Azure Policy evaluation |
| `ComplianceRemediationService` | Automated remediation |
| `DefenderForCloudService` | Microsoft Defender integration |
| `AssessmentService` | Store and retrieve assessment results |
| `EvidenceCollectors` | Control-specific evidence gathering |

## Configuration

### appsettings.json

```json
{
  "ComplianceAgent": {
    "Temperature": 0.2,
    "MaxTokens": 6000,
    "EnableDefenderIntegration": true,
    "EnableAutomatedRemediation": true,
    "DefaultFramework": "NIST80053",
    "DefaultBaseline": "FedRAMPHigh"
  },
  "DefenderForCloud": {
    "Enabled": true,
    "SubscriptionId": "your-subscription-id",
    "WorkspaceId": "your-log-analytics-workspace-id"
  },
  "CodeScanning": {
    "EnableSecretsDetection": true,
    "EnableDependencyScanning": true,
    "EnableStigChecks": true,
    "SecretPatterns": ["API_KEY", "PASSWORD", "SECRET", "TOKEN"]
  },
  "Evidence": {
    "StorageAccount": "complianceevidence",
    "Container": "evidence",
    "RetentionDays": 2555,
    "EnableVersioning": true,
    "EnableImmutability": true
  }
}
```

### Database Configuration

Compliance assessments are stored in SQL Server/PostgreSQL:

```sql
-- Assessments table
CREATE TABLE Assessments (
  AssessmentId UNIQUEIDENTIFIER PRIMARY KEY,
  SubscriptionId NVARCHAR(100),
  Framework NVARCHAR(50),
  Baseline NVARCHAR(50),
  AssessmentDate DATETIME,
  OverallScore DECIMAL(5,2),
  CompliantControls INT,
  NonCompliantControls INT,
  Status NVARCHAR(20)
);

-- Assessment results
CREATE TABLE AssessmentResults (
  ResultId UNIQUEIDENTIFIER PRIMARY KEY,
  AssessmentId UNIQUEIDENTIFIER REFERENCES Assessments(AssessmentId),
  ControlId NVARCHAR(20),
  Status NVARCHAR(20),
  Findings NVARCHAR(MAX),
  Evidence NVARCHAR(MAX),
  RemediationSteps NVARCHAR(MAX)
);
```

## Usage Patterns

### Pattern 1: Compliance Scan

```
User: "Run NIST 800-53 scan on rg-prod"

Agent Response:
🛡️ Compliance Assessment Results

Framework: NIST 800-53 Rev 5
Baseline: FedRAMP High
Scope: rg-prod (23 resources)

Overall Score: 87/100

✅ Compliant: 387 controls (92%)
❌ Non-Compliant: 23 controls (5%)
⚠️  Partially Compliant: 11 controls (3%)

Critical Findings:
1. SC-7: 3 storage accounts with public access
2. IA-2: 5 users without MFA
3. AU-2: 2 resource groups missing diagnostics

📊 Full report saved to assessment_20251119.json
```

### Pattern 2: Automated Remediation

```
User: "Fix SC-7 violations in rg-prod"

Agent Response:
🔧 Remediating SC-7 (Boundary Protection)

Found 3 non-compliant resources:
1. storageaccount1: Public blob access enabled
2. storageaccount2: Public network access allowed
3. webapp1: Public endpoint detected

Applying fixes...
✅ storageaccount1: Disabled public blob access
✅ storageaccount2: Configured private endpoint
✅ webapp1: Enabled VNet integration

Validation scan...
✅ All SC-7 violations remediated
📄 Remediation report: sc7_remediation_20251119.json
```

### Pattern 3: Evidence Collection

```
User: "Collect evidence for AU-2"

Agent Response:
📦 Collecting evidence for AU-2 (Audit Events)

Evidence collected:
✅ Diagnostic settings for all resources
✅ Log Analytics workspace configuration
✅ Retention policy settings (90 days)
✅ Azure Monitor logs query examples
✅ Storage account audit logs sample

Storage location:
  Account: complianceevidence
  Container: evidence/AU-2
  Files: 12 artifacts
  
🔒 Evidence sealed with immutable storage
```

## Integration with Other Agents

### → Infrastructure Agent
Infrastructure Agent generates templates → Compliance Agent validates compliance controls

### → Document Agent
Compliance Agent scans resources → Document Agent generates SSP with findings

### → ATO Preparation Agent
Compliance Agent assesses readiness → ATO Preparation orchestrates package creation

### → Discovery Agent
Discovery Agent inventories resources → Compliance Agent scans for compliance

## Troubleshooting

### Issue: Defender Integration Not Working

**Symptom**: "Defender for Cloud service unavailable"

**Solutions:**
```bash
# Enable Defender for Cloud
az security pricing create \
  --name VirtualMachines \
  --tier Standard

# Verify Defender status
az security pricing list

# Check Log Analytics workspace
az monitor log-analytics workspace show \
  --resource-group rg-security \
  --workspace-name law-compliance
```

### Issue: Scan Returns Empty Results

**Symptom**: "No resources found to scan"

**Solutions:**
```bash
# Verify subscription access
az account show

# Check resource provider registration
az provider show --namespace Microsoft.Security

# List resources in scope
az resource list --resource-group rg-prod
```

### Issue: Remediation Fails

**Symptom**: "Failed to apply remediation"

**Solutions:**
```bash
# Check RBAC permissions
az role assignment list --assignee <user-id>

# Verify Owner or Contributor role
az role assignment create \
  --role "Contributor" \
  --assignee <user-id> \
  --scope "/subscriptions/{sub-id}"

# Test Azure Policy deployment
az policy definition create --name test --rules '{...}'
```

## Performance

| Operation | Typical Duration | Resources Scanned |
|-----------|-----------------|-------------------|
| Single control assessment | 2-5 seconds | N/A |
| Resource group scan | 15-30 seconds | 10-50 resources |
| Subscription scan | 2-5 minutes | 100-500 resources |
| Code repository scan | 30-120 seconds | 1 repository |
| Evidence collection | 10-30 seconds | Per control |

## Limitations

- **Resource Types**: Not all Azure resource types have control mappings
- **Custom Policies**: User-defined policies not automatically assessed
- **Multi-Tenancy**: Scans single tenant at a time
- **Real-time Monitoring**: Scans are point-in-time, not continuous

## References

- [NIST 800-53 Rev 5](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)
- [FedRAMP Baselines](https://www.fedramp.gov/baselines/)
- [DoD Cloud Computing SRG](https://dl.dod.cyber.mil/wp-content/uploads/cloud/pdf/Cloud_Computing_SRG_v1r3.pdf)
- [Azure Security Benchmark](https://docs.microsoft.com/en-us/security/benchmark/azure/)
- [Microsoft Defender for Cloud](https://docs.microsoft.com/en-us/azure/defender-for-cloud/)

---

**Last Updated**: November 2025  
**Version**: 0.6.35  
**Agent Type**: `Compliance`
