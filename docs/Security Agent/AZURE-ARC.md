# Security Agent - Azure Arc Capabilities

The Security Agent provides comprehensive security posture assessment and threat protection recommendations for Azure Arc-enabled servers.

## Overview

Azure Arc enables centralized security management for hybrid infrastructure. The Security Agent's Arc functions enable you to:

- **Assess security posture** across Arc-enabled servers
- **Check Microsoft Defender** enrollment and status
- **Generate security recommendations** with prioritized actions
- **Calculate security scores** based on industry benchmarks

## Available Functions

### 1. `get_arc_security_posture`

Assesses the overall security posture of Arc-enabled servers.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | No | Filter to specific subscription |
| `resourceGroupName` | string | No | Filter to specific resource group |

**Example Prompts:**
```
"Check security posture of Arc servers"
"What's the security status of our hybrid infrastructure?"
"Assess Arc machine security"
```

**Response includes:**
- Overall security score (0-100)
- Machine count and assessment coverage
- Security status breakdown:
  - Secure machines count
  - At-risk machines count
  - Unassessed machines count
- Defender enrollment status
- Agent health summary
- Key findings by severity

---

### 2. `check_arc_defender_status`

Checks Microsoft Defender for Servers enrollment across Arc machines.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | No | Filter to specific subscription |
| `machineResourceId` | string | No | Filter to specific machine |

**Example Prompts:**
```
"Is Defender enabled on Arc servers?"
"Check Defender status for hybrid-db-01"
"Which Arc machines don't have Defender?"
```

**Response includes:**
- Defender extension presence
- Enrollment status per machine
- Extension provisioning state
- Protection level (P1/P2)
- Last agent communication
- Coverage percentage across fleet

---

### 3. `get_arc_security_recommendations`

Generates prioritized security recommendations for Arc servers.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | No | Filter to specific subscription |
| `severity` | string | No | Filter by severity: High, Medium, Low |

**Example Prompts:**
```
"What security improvements are needed for Arc servers?"
"Show high severity security issues on hybrid machines"
"Get security recommendations for Arc infrastructure"
```

**Response includes:**
- Prioritized recommendations list:
  - Priority ranking
  - Severity level
  - Recommendation title
  - Description
  - Affected machine count
  - Remediation steps
  - Estimated effort
- Summary by severity
- Quick wins identification

## Security Scoring

The Security Agent calculates scores based on multiple factors:

| Factor | Weight | Description |
|--------|--------|-------------|
| Defender Enrollment | 25% | MDE extension installed and active |
| Agent Health | 20% | Connected status, recent heartbeat |
| Extension Coverage | 20% | Monitoring, security extensions |
| Compliance Status | 20% | Policy compliance findings |
| Vulnerability Status | 15% | Known vulnerabilities |

### Score Interpretation

| Score Range | Rating | Action Required |
|-------------|--------|-----------------|
| 90-100 | Excellent | Maintain current posture |
| 70-89 | Good | Address medium findings |
| 50-69 | Fair | Prioritize high findings |
| 0-49 | Poor | Immediate attention needed |

## Common Use Cases

### Security Assessment
```
User: "How secure are our Arc-enabled servers?"

Security Agent will:
1. Query all Arc machines
2. Check Defender enrollment
3. Assess agent health
4. Calculate security score
5. Identify high-risk machines
```

### Defender Gap Analysis
```
User: "Which hybrid servers don't have Defender?"

Security Agent will:
1. List all Arc machines
2. Check for MDE extension
3. Report enrollment status
4. Identify gaps
5. Recommend remediation
```

### Security Hardening
```
User: "What should I do to improve Arc server security?"

Security Agent will:
1. Generate recommendations
2. Prioritize by severity/effort
3. Provide remediation steps
4. Identify quick wins
5. Estimate implementation effort
```

## Integration with Other Agents

| Agent | Integration |
|-------|-------------|
| **Discovery** | Machine inventory → security assessment targets |
| **Compliance** | Compliance findings → security score input |
| **Infrastructure** | Security recommendations → extension deployment |

## Microsoft Defender for Servers

### Protection Tiers

| Tier | Features | Use Case |
|------|----------|----------|
| **Plan 1 (P1)** | EDR, threat detection | Basic protection |
| **Plan 2 (P2)** | P1 + vulnerability mgmt, JIT access | Full protection |

### Extension Names

| Platform | Extension Name | Publisher |
|----------|---------------|-----------|
| Windows | MDE.Windows | Microsoft.Azure.AzureDefenderForServers |
| Linux | MDE.Linux | Microsoft.Azure.AzureDefenderForServers |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Security Agent                          │
│                                                             │
│  ┌─────────────────┐  ┌─────────────────┐                  │
│  │ get_arc_        │  │ check_arc_      │                  │
│  │ security_       │  │ defender_       │                  │
│  │ posture         │  │ status          │                  │
│  └────────┬────────┘  └────────┬────────┘                  │
│           │                    │                            │
│           │    ┌───────────────┴──────────┐                │
│           │    │ get_arc_security_        │                │
│           │    │ recommendations          │                │
│           │    └───────────────┬──────────┘                │
│           │                    │                            │
│           ▼                    ▼                            │
│  ┌─────────────────────────────────────────────────────┐   │
│  │              Azure Security Center                   │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐          │   │
│  │  │ Defender │  │ Security │  │ Recom-   │          │   │
│  │  │ Status   │  │  Score   │  │ mendations│          │   │
│  │  └──────────┘  └──────────┘  └──────────┘          │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## Quick Start

1. **Check security posture:**
   ```
   "What's the security status of our Arc servers?"
   ```

2. **Verify Defender coverage:**
   ```
   "Check Defender enrollment on Arc machines"
   ```

3. **Get recommendations:**
   ```
   "What security improvements are needed for hybrid servers?"
   ```

## Recommendation Categories

### High Priority
- Enable Microsoft Defender for Servers
- Install security monitoring extension
- Address critical vulnerabilities
- Enable secure boot (if supported)

### Medium Priority
- Configure log collection
- Enable just-in-time VM access
- Implement network segmentation
- Update agent to latest version

### Low Priority
- Enable diagnostic logging
- Configure backup
- Review extension inventory
- Tag resources for management

## Troubleshooting

### Common Issues

| Issue | Cause | Resolution |
|-------|-------|------------|
| No security score | Machine disconnected | Verify Arc agent connectivity |
| Defender not detected | Extension not installed | Deploy MDE extension |
| Stale assessment | Agent not reporting | Check agent health |
| Recommendations missing | Assessment pending | Wait for next scan cycle |

### Verification Commands

```bash
# Check Arc agent status
azcmagent show

# Verify Defender extension
az connectedmachine extension list --machine-name <name> -g <rg>

# Check agent connectivity
azcmagent check
```

## Related Documentation

- [Azure Arc Overview](../AZURE-ARC.md) - Main Arc documentation
- [Agent Architecture](../AGENTS.md) - Full agent overview
- [Discovery Agent Arc](../Discovery%20Agent/AZURE-ARC.md) - Machine inventory
- [Compliance Agent Arc](../Compliance%20Agent/AZURE-ARC.md) - Compliance scanning
- [Infrastructure Agent Arc](../Infrastructure%20Agent/AZURE-ARC.md) - Extension deployment
