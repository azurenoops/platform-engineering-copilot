# Compliance Agent - Azure Arc Capabilities

The Compliance Agent provides comprehensive compliance scanning and policy assessment for Azure Arc-enabled servers, supporting federal security frameworks like STIG, CIS, and NIST 800-53.

## Overview

Azure Arc enables centralized compliance management for hybrid infrastructure. The Compliance Agent's Arc functions enable you to:

- **Scan Arc machines** against security baselines (STIG, CIS, Azure)
- **Track guest configuration** policy compliance status
- **Generate compliance summaries** across your hybrid fleet
- **Map findings to NIST 800-53** control families

## Available Functions

### 1. `scan_arc_machine_compliance`

Scans Arc-enabled servers against security baselines with detailed findings.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | No | Filter to specific subscription |
| `resourceGroupName` | string | No | Filter to specific resource group |
| `baselineType` | string | No | Baseline: STIG, CIS, Azure (default: Azure) |

**Baseline Options:**
- **STIG** - DoD Security Technical Implementation Guides
- **CIS** - Center for Internet Security Benchmarks
- **Azure** - Azure Security Baseline

**Example Prompts:**
```
"Scan Arc servers against STIG baseline"
"Check CIS compliance on hybrid machines"
"Run Azure security baseline scan on Arc servers"
```

**Response includes:**
- Overall compliance score (percentage)
- Summary by severity (High/Medium/Low)
- Detailed findings per machine:
  - Machine name and resource ID
  - Individual compliance score
  - Finding details with:
    - Severity level
    - Control ID (baseline-specific)
    - Description
    - Remediation guidance
    - NIST 800-53 control mapping

---

### 2. `get_arc_guest_configuration_status`

Retrieves Azure Policy guest configuration status for Arc machines.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | No | Filter to specific subscription |
| `machineResourceId` | string | No | Filter to specific machine |

**Example Prompts:**
```
"Show guest configuration status for Arc servers"
"What policies apply to my hybrid machines?"
"Check policy compliance on Arc machine hybrid-db-01"
```

**Response includes:**
- Assignment report summaries:
  - Configuration name
  - Compliance status
  - Last compliance check time
  - Non-compliant resource count
- Machine-level status:
  - Extension provisioning state
  - Agent status
  - Overall compliance state

---

### 3. `get_arc_compliance_summary`

Generates an aggregate compliance summary across Arc-enabled servers.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | No | Filter to specific subscription |

**Example Prompts:**
```
"Give me a compliance summary for Arc servers"
"What's the overall compliance status of hybrid infrastructure?"
"How compliant are our Arc-enabled machines?"
```

**Response includes:**
- Total machine count
- Compliance status breakdown:
  - Compliant vs non-compliant counts
  - Compliance percentage
- Guest configuration adoption:
  - Extension installation rate
  - Policy assignment coverage
- Severity distribution of findings
- NIST 800-53 control family mapping

## Security Baseline Mappings

### STIG Controls

| Control ID | Description | NIST Mapping |
|------------|-------------|--------------|
| V-12345 | Password complexity | IA-5 |
| V-12346 | Account lockout | AC-7 |
| V-12347 | Audit logging | AU-2 |

### CIS Controls

| Control | Description | NIST Mapping |
|---------|-------------|--------------|
| CIS-1.1 | System inventory | CM-8 |
| CIS-2.1 | Software inventory | CM-8 |
| CIS-4.1 | Admin privileges | AC-6 |

### Azure Baseline

| Control | Description | NIST Mapping |
|---------|-------------|--------------|
| AZ-SEC-01 | Encryption at rest | SC-28 |
| AZ-SEC-02 | Network security | SC-7 |
| AZ-SEC-03 | Identity protection | IA-2 |

## Common Use Cases

### Federal Compliance Assessment
```
User: "Check STIG compliance for all Arc servers"

Compliance Agent will:
1. Query all Arc-enabled machines
2. Evaluate each against STIG baseline
3. Generate findings with severity
4. Map to NIST 800-53 controls
5. Calculate overall compliance score
```

### ATO Support
```
User: "Generate compliance report for our ATO package"

Compliance Agent will:
1. Scan against all baselines (STIG, CIS, Azure)
2. Aggregate findings by NIST control family
3. Provide remediation guidance
4. Generate summary for documentation
```

### Continuous Compliance Monitoring
```
User: "Are there any new compliance violations on hybrid servers?"

Compliance Agent will:
1. Check guest configuration status
2. Compare against previous assessments
3. Highlight new or changed findings
4. Prioritize by severity
```

## Integration with Other Agents

| Agent | Integration |
|-------|-------------|
| **Discovery** | Machine inventory → compliance scanning targets |
| **Infrastructure** | Compliance gaps → remediation deployments |
| **Security** | Compliance findings → security posture enrichment |

## NIST 800-53 Control Families

The Compliance Agent maps findings to these control families:

| Family | Code | Focus Area |
|--------|------|------------|
| Access Control | AC | User access, least privilege |
| Audit & Accountability | AU | Logging, monitoring |
| Configuration Management | CM | Baselines, inventory |
| Identification & Auth | IA | Authentication, MFA |
| System & Comms Protection | SC | Encryption, network security |
| System & Info Integrity | SI | Vulnerability mgmt, patching |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Compliance Agent                         │
│                                                             │
│  ┌─────────────────┐  ┌─────────────────┐                  │
│  │ scan_arc_       │  │ get_arc_guest_  │                  │
│  │ machine_        │  │ configuration_  │                  │
│  │ compliance      │  │ status          │                  │
│  └────────┬────────┘  └────────┬────────┘                  │
│           │                    │                            │
│           │    ┌───────────────┴──────────┐                │
│           │    │ get_arc_compliance_      │                │
│           │    │ summary                  │                │
│           │    └───────────────┬──────────┘                │
│           └────────────┬───────┘                            │
│                        ▼                                    │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Azure Policy / Guest Configuration      │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐          │   │
│  │  │  STIG    │  │   CIS    │  │  Azure   │          │   │
│  │  │ Baseline │  │ Baseline │  │ Baseline │          │   │
│  │  └──────────┘  └──────────┘  └──────────┘          │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Quick Start

1. **Check compliance summary:**
   ```
   "What's the compliance status of our Arc servers?"
   ```

2. **Scan specific baseline:**
   ```
   "Run STIG compliance scan on Arc machines"
   ```

3. **Review policy status:**
   ```
   "Show guest configuration policies for hybrid servers"
   ```

## Related Documentation

- [Azure Arc Overview](../AZURE-ARC.md) - Main Arc documentation
- [Agent Architecture](../AGENTS.md) - Full agent overview
- [Compliance Quick Reference](./QUICK-REFERENCE.md) - Compliance shortcuts
- [Security Agent Arc](../Security%20Agent/AZURE-ARC.md) - Security posture
