# Agent Remediation Boundaries

> **Decision Guide:** Understanding when Compliance Agent self-remediates vs. delegating to Infrastructure Agent

---

## Table of Contents

1. [Overview](#overview)
2. [Remediation Architecture](#remediation-architecture)
3. [Compliance Agent Remediation Scope](#compliance-agent-remediation-scope)
4. [Infrastructure Agent Remediation Scope](#infrastructure-agent-remediation-scope)
5. [Decision Matrix](#decision-matrix)
6. [Orchestration Patterns](#orchestration-patterns)
7. [Configuration Examples](#configuration-examples)

---

## Overview

The Platform Engineering Copilot uses a **hybrid remediation model** where:

- **Compliance Agent** owns configuration-level and policy-based remediations
- **Infrastructure Agent** owns resource lifecycle and topology changes
- **OrchestratorAgent** coordinates complex remediation workflows requiring both

### Key Principles

✅ **No Direct Agent-to-Agent Calls**
- All multi-agent coordination flows through OrchestratorAgent
- Prevents circular dependencies and infinite loops
- Single point of control for execution planning

✅ **Domain Expertise Separation**
- Compliance = Assess, Validate, Fix Configuration Issues
- Infrastructure = Provision, Modify, Delete Resources
- Clear boundaries reduce code duplication

✅ **Risk-Based Automation**
- Low-risk remediations: Auto-execute
- Medium-risk remediations: Require user confirmation
- High-risk remediations: Manual review + Infrastructure Agent

---

## Remediation Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                       User Request                               │
│           "Make my infrastructure compliant"                     │
└──────────────────────────┬──────────────────────────────────────┘
                           ↓
┌──────────────────────────────────────────────────────────────────┐
│                    OrchestratorAgent                              │
│  • Analyzes intent                                               │
│  • Creates execution plan                                        │
│  • Routes to appropriate agents                                  │
└──────────────────────────┬───────────────────────────────────────┘
                           ↓
        ┌──────────────────┴──────────────────┐
        ↓                                     ↓
┌───────────────────────┐          ┌──────────────────────────┐
│   Compliance Agent    │          │  Infrastructure Agent    │
│                       │          │                          │
│ CONFIGURATION-LEVEL   │          │ RESOURCE-LEVEL          │
│ REMEDIATIONS:         │          │ REMEDIATIONS:           │
│ ✓ Tags                │          │ ✓ Resource creation     │
│ ✓ Encryption settings │          │ ✓ Resource deletion     │
│ ✓ HTTPS enforcement   │          │ ✓ Network topology      │
│ ✓ Public access       │          │ ✓ SKU changes           │
│ ✓ Soft delete         │          │ ✓ Region migrations     │
│ ✓ Firewall rules      │          │ ✓ Complex reconfigs     │
│ ✓ Audit logging       │          │                          │
│ ✓ TLS versions        │          │ Uses:                   │
│                       │          │ • DynamicTemplateGen    │
│ Uses:                 │          │ • DeploymentOrchest     │
│ • Azure ARM API       │          │ • NetworkDesignService  │
│ • Property updates    │          │ • PolicyEnforcement     │
│ • InfraRemediation    │          │                          │
│   Service             │          │                          │
└───────────────────────┘          └──────────────────────────┘
```

---

## Compliance Agent Remediation Scope

### What Compliance Agent Fixes

The Compliance Agent's `ComplianceRemediationService` handles **configuration-level changes** that:
- Do not require resource recreation
- Are low-risk property updates
- Align with compliance frameworks (NIST 800-53, FedRAMP, STIG)
- Can be executed via Azure Resource Manager API PATCH operations

### Supported Remediation Types

#### **Storage Accounts**
```yaml
Remediation Type: storage:encryption
Risk Level: Low
Action: Enable encryption at rest
API: PATCH /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{name}
Example:
  properties:
    encryption:
      services:
        blob:
          enabled: true
```

```yaml
Remediation Type: storage:https
Risk Level: Low
Action: Enforce HTTPS-only traffic
API: PATCH /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{name}
Example:
  properties:
    supportsHttpsTrafficOnly: true
```

```yaml
Remediation Type: storage:publicaccess
Risk Level: Medium
Action: Disable public blob access
API: PATCH /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Storage/storageAccounts/{name}
Example:
  properties:
    allowBlobPublicAccess: false
```

#### **Key Vaults**
```yaml
Remediation Type: keyvault:softdelete
Risk Level: Low
Action: Enable soft delete
API: PATCH /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.KeyVault/vaults/{name}
Example:
  properties:
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
```

```yaml
Remediation Type: keyvault:purgeprotection
Risk Level: Medium
Action: Enable purge protection (IRREVERSIBLE)
API: PATCH /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.KeyVault/vaults/{name}
Example:
  properties:
    enablePurgeProtection: true
```

#### **Network Security Groups**
```yaml
Remediation Type: nsg:openports
Risk Level: High
Action: Remove/restrict open ports (RDP, SSH)
API: DELETE /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Network/networkSecurityGroups/{nsg}/securityRules/{rule}
Example:
  - Delete rules allowing 0.0.0.0/0 on ports 22, 3389
  - Replace with restricted source IPs
```

#### **SQL Databases**
```yaml
Remediation Type: sql:auditing
Risk Level: Low
Action: Enable auditing to Log Analytics
API: PUT /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Sql/servers/{server}/databases/{db}/auditingSettings/default
Example:
  properties:
    state: "Enabled"
    storageAccountSubscriptionId: "{subscription}"
```

```yaml
Remediation Type: sql:threatdetection
Risk Level: Low
Action: Enable Advanced Threat Protection
API: PUT /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Sql/servers/{server}/databases/{db}/securityAlertPolicies/default
Example:
  properties:
    state: "Enabled"
    emailAccountAdmins: true
```

#### **Virtual Machines**
```yaml
Remediation Type: vm:monitoring
Risk Level: Low
Action: Install Azure Monitor Agent extension
API: PUT /subscriptions/{id}/resourceGroups/{rg}/providers/Microsoft.Compute/virtualMachines/{vm}/extensions/AzureMonitorAgent
Example:
  properties:
    publisher: "Microsoft.Azure.Monitor"
    type: "AzureMonitorWindowsAgent"
    autoUpgradeMinorVersion: true
```

#### **Generic Tags**
```yaml
Remediation Type: tags:missing
Risk Level: Low
Action: Add required compliance tags
API: PATCH /subscriptions/{id}/resourceGroups/{rg}/providers/{type}/{name}
Example:
  tags:
    Environment: "Production"
    CostCenter: "CC-12345"
    Owner: "platform-team@example.com"
    Compliance: "NIST-800-53"
```

### Risk Levels & Approval

```typescript
enum RemediationRiskLevel {
  Low,      // Auto-execute (tags, enable features)
  Medium,   // User confirmation required (purge protection, firewall changes)
  High,     // Manual review + approval (delete rules, resource moves)
  Critical  // NEVER auto-execute (resource deletion, region migration)
}
```

### Example Workflow

```
User: "Fix compliance issues in rg-prod"

Compliance Agent:
1. Scans resources against NIST 800-53
2. Identifies 12 findings:
   - 5 Low Risk (missing tags, encryption disabled)
   - 4 Medium Risk (firewall rules, soft delete)
   - 3 High Risk (open NSG ports, public access)
   
3. Generates InfrastructureRemediationPlan:
   - Auto-execute: Low risk (5 items)
   - User confirm: Medium risk (4 items)
   - Delegate to Infrastructure: High risk (3 items)
   
4. Executes low/medium risk fixes via ARM API
5. Returns high-risk items to Orchestrator
```

---

## Infrastructure Agent Remediation Scope

### What Infrastructure Agent Actually Does

The Infrastructure Agent **generates Infrastructure-as-Code templates** for:
- New resource creation (VNets, VMs, Storage, etc.)
- Complex topology modifications
- Fundamental resource reconfigurations (SKU changes, region migrations)
- Compliance-enhanced deployments (FedRAMP, DoD IL2-IL6)

**IMPORTANT:** Infrastructure Agent does NOT automatically provision resources. It generates Bicep/Terraform templates that users review and deploy manually. The `provision_infrastructure` function exists but is ONLY used when users explicitly say "deploy NOW" or "create IMMEDIATELY".

### Supported Template Generation Types

#### **Resource Lifecycle Templates**
- Generate Bicep/Terraform for new resources (VNets, Subnets, NSGs, Key Vaults)
- Generate deletion scripts for non-compliant resources
- Generate replacement templates (SKU changes requiring recreation)

#### **Network Topology Templates**
- VNet peering Bicep templates
- Subnet restructuring configurations
- Private endpoint deployment templates
- Azure Firewall infrastructure code
- Application Gateway setup templates

#### **Complex Reconfiguration Templates**
- VM size change templates (with restart handling)
- Database SKU change scripts (with downtime planning)
- Storage account migration templates (cross-region)
- AKS cluster upgrade manifests

#### **Compliance-Enhanced Templates**
- Bicep templates with FedRAMP High, DoD IL5, NIST 800-53 controls
- Terraform configurations with IL2/IL4/IL5/IL6 policy enforcement
- Azure Policy definitions for preventive controls
- Multi-phase deployment orchestration plans

### Example Workflow

```
User: "Redesign network to meet FedRAMP High requirements"

Infrastructure Agent:
1. Analyzes current topology (via Azure MCP)
2. Designs compliant network architecture:
   - Hub-and-spoke VNet topology
   - NSGs on all subnets with FedRAMP rules
   - Azure Firewall for egress filtering
   - Private endpoints for PaaS services
   
3. Generates Bicep template package:
   - main.bicep (hub VNet, Azure Firewall, parameters)
   - spoke.bicep (spoke VNets, peering configuration)
   - nsg-rules.bicep (FedRAMP baseline security rules)
   - deploy.sh (phased deployment script)
   
4. Provides deployment instructions:
   - Phase 1: Review templates and parameters
   - Phase 2: Deploy hub resources (az deployment group create)
   - Phase 3: Deploy spoke VNets (parallel deployment)
   - Phase 4: Configure peering and private endpoints
   
**User manually reviews and deploys templates** using Azure CLI/Portal.
```

---

## Decision Matrix

### When to Use Compliance Agent

| Scenario | Agent | Reason |
|----------|-------|--------|
| Add missing tags to resources | **Compliance** | Low-risk property update |
| Enable encryption at rest | **Compliance** | Configuration change, no recreation |
| Enable soft delete on Key Vault | **Compliance** | Low-risk feature enablement |
| Enable SQL auditing | **Compliance** | Configuration change via ARM |
| Fix HTTPS-only setting | **Compliance** | Property update |
| Remove overly permissive firewall rules | **Compliance** | Security configuration |

### When to Use Infrastructure Agent

| Scenario | Agent | Reason |
|----------|-------|--------|
| Create Private DNS Zone | **Infrastructure** | Generate resource template |
| Deploy Azure Firewall | **Infrastructure** | Generate complex resource template + configuration |
| Migrate storage account to new region | **Infrastructure** | Generate migration template + data transfer plan |
| Change VM SKU | **Infrastructure** | Generate recreation template |
| Redesign network topology | **Infrastructure** | Generate multi-resource architecture templates |
| Deploy AKS cluster with compliance baseline | **Infrastructure** | Generate compliant provisioning templates |

### When to Use Both (via Orchestrator)

| Scenario | Orchestration Flow | Reason |
|----------|-------------------|--------|
| "Make infrastructure compliant" | Discovery → Compliance (assess) → Compliance (simple fixes) → Infrastructure (complex fixes) → Compliance (re-assess) | Multi-phase remediation |
| "Deploy FedRAMP-compliant environment" | Infrastructure (provision) → Compliance (validate) → Compliance (fix gaps) | Provision-then-harden |
| "Clone prod to staging with compliance" | Discovery (inventory) → Environment (clone) → Compliance (scan) → Compliance (remediate) | Multi-agent workflow |

---

## Orchestration Patterns

### Pattern 1: Simple Compliance Scan + Fix

```
User: "Fix storage account compliance issues"

OrchestratorAgent:
├─ Step 1: Compliance Agent (scan)
│  └─ Findings: encryption disabled, public access enabled
├─ Step 2: Compliance Agent (auto-remediate)
│  └─ Fixes: 2/2 (low risk)
└─ Result: All issues resolved
```

### Pattern 2: Mixed Risk Remediation

```
User: "Remediate all findings in rg-prod"

OrchestratorAgent:
├─ Step 1: Compliance Agent (scan)
│  └─ Findings: 5 low, 3 medium, 2 high risk
├─ Step 2: Compliance Agent (auto-fix low risk)
│  └─ Fixed: 5/5 tags, encryption settings
├─ Step 3: User Confirmation (medium risk)
│  └─ User approves: Enable purge protection (2 Key Vaults)
├─ Step 4: Compliance Agent (execute approved medium)
│  └─ Fixed: 2/2 purge protection enabled
├─ Step 5: Infrastructure Agent (high risk)
│  └─ Generate remediation templates for NSG changes
└─ Result: User reviews + deploys templates manually
```

### Pattern 3: Full Environment Compliance

```
User: "Create FedRAMP High compliant environment"

OrchestratorAgent (Sequential Execution):
├─ Step 1: Infrastructure Agent
│  └─ Generate templates: VNet, Subnets, NSGs, Key Vault, Storage, Log Analytics
│  └─ User deploys templates to Azure
├─ Step 2: Compliance Agent (scan)
│  └─ Assess: Deployed resources against FedRAMP baseline
├─ Step 3: Compliance Agent (remediate)
│  └─ Fix: 8 configuration issues (tags, encryption, auditing)
├─ Step 4: Compliance Agent (re-scan)
│  └─ Validate: 100% compliant
├─ Step 5: Document Agent
│  └─ Generate: SSP, SAR evidence, POA&M
└─ Result: Compliant environment + documentation
```

### Pattern 4: Parallel Assessment

```
User: "Assess compliance across all subscriptions"

OrchestratorAgent (Parallel Execution):
├─ Discovery Agent (parallel)
│  └─ Inventory: All resources in all subscriptions
├─ Compliance Agent (parallel per subscription)
│  ├─ Subscription A: 45 findings
│  ├─ Subscription B: 12 findings
│  └─ Subscription C: 3 findings
├─ Cost Management Agent (parallel)
│  └─ Estimate: Remediation costs per subscription
└─ Result: Aggregated compliance report + cost impact
```

---

## Configuration Examples

### Enable Automated Remediation

```json
{
  "AgentConfiguration": {
    "ComplianceAgent": {
      "Enabled": true,
      "Temperature": 0.2,
      "MaxTokens": 6000,
      "EnableAutomatedRemediation": true,
      "DefaultFramework": "NIST80053",
      "AutoRemediationRiskThreshold": "Medium"
    }
  }
}
```

### Configure Remediation Risk Tolerance

```json
{
  "ComplianceAgent": {
    "Remediation": {
      "AutoExecuteMaxRisk": "Low",
      "RequireApprovalForRisk": ["Medium", "High"],
      "NeverAutoExecuteRisk": ["Critical"],
      "MaxParallelRemediations": 5,
      "EnableRollback": true,
      "RollbackOnFirstFailure": true
    }
  }
}
```

### Infrastructure Agent Compliance Features

```json
{
  "AgentConfiguration": {
    "InfrastructureAgent": {
      "Enabled": true,
      "Temperature": 0.4,
      "MaxTokens": 8000,
      "EnableComplianceEnhancement": true,
      "DefaultComplianceLevel": "IL4",
      "EnforceCompliancePolicies": true
    }
  }
}
```

---

## Best Practices

### 1. Start with Discovery
Always use Discovery Agent to inventory resources before remediating

### 2. Assess Before Fixing
Run Compliance scan first to understand full scope

### 3. Review High-Risk Changes
Never auto-execute Critical or High risk remediations

### 4. Test in Non-Prod First
Validate remediation plans in dev/test environments

### 5. Enable Rollback
Configure rollback capability for medium/high risk changes

### 6. Monitor & Validate
Re-scan after remediation to confirm success

### 7. Document Evidence
Use Document Agent to capture remediation evidence for ATO

---

## Summary

| Aspect | Compliance Agent | Infrastructure Agent |
|--------|------------------|---------------------|
| **Scope** | Configuration-level fixes | Template generation for resources |
| **Risk** | Low to Medium | N/A (user deploys) |
| **Operations** | PATCH, PUT (properties) via ARM API | Generate Bicep/Terraform templates |
| **Examples** | Tags, encryption flags, firewall rules | VNets, VMs, network topology templates |
| **Automation** | Auto-execute low risk | Generates templates, user deploys |
| **Service** | ComplianceRemediationService | DynamicTemplateGenerator |
| **Coordination** | Via OrchestratorAgent | Via OrchestratorAgent |

**Key Takeaway:** Compliance Agent handles "turn it on/off" fixes via direct ARM API calls. Infrastructure Agent generates "build it/tear it down" templates for user deployment.
