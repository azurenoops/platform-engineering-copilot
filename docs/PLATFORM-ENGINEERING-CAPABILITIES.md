# Platform Engineering Copilot - Capability Assessment & Roadmap

**Version:** 1.0  
**Last Updated:** January 2026  
**Author:** Platform Engineering Architect Agent

---

## Overview

This document maps Platform Engineering Copilot capabilities to recommended platform engineering best practices, identifies gaps, and provides a prioritized enhancement roadmap.

---

## Capability Matrix

### 1. Developer Self-Service (Golden Paths)

| Capability | Current State | Implementation | Gap Analysis |
|------------|---------------|----------------|--------------|
| **Service Templates** | ✅ Complete | Environment Agent: 10 tools for template lifecycle | Template catalog exists via `list_service_templates` |
| **Environment Provisioning** | ✅ Complete | `create_environment_from_template`, `clone_provisioned_environment` | Interactive wizard exists |
| **IaC Generation** | ✅ Complete | Infrastructure Agent: Bicep, Terraform generation | Advisory mode - generates but doesn't auto-deploy |
| **Repository Scaffolding** | ⚠️ Partial | Templates include repo structure, but no Git automation | Need: GitHub/ADO repo creation tool |
| **DoD Metadata Collection** | ⚠️ Partial | Templates know IL levels, but no interactive wizard | Need: 8-step interactive DoD onboarding wizard |
| **Golden Path Wizard** | ❌ Missing | No guided multi-step wizard UI | Need: Conversational wizard for new service requests |

**Agents Involved:** Environment, Infrastructure, Configuration

---

### 2. Governance & Compliance

| Capability | Current State | Implementation | Gap Analysis |
|------------|---------------|----------------|--------------|
| **NIST 800-53 Scanning** | ✅ Complete | `run_compliance_assessment` (12 control families) | Real Azure scanning via Resource Graph |
| **FedRAMP Baselines** | ✅ Complete | Configurable baselines (Low/Moderate/High) | Includes control inheritance |
| **STIG Mapping** | ✅ Complete | KnowledgeBase: `explain_stig`, `search_stigs` | 50+ STIG controls indexed |
| **RMF Process Guidance** | ✅ Complete | `explain_rmf` with 7-step process | Navy-specific workflows included |
| **Impact Level Guidance** | ✅ Complete | `explain_impact_level` (IL2-IL6) | DoD IL requirements documented |
| **Automated Remediation** | ✅ Complete | `execute_remediation`, `batch_remediation` | Dry-run mode, rollback support |
| **Policy Enforcement** | ⚠️ Partial | Governance options exist but not runtime enforced | Need: Pre-deployment policy gates |
| **Guardrails** | ⚠️ Partial | Config-based restrictions (`ApprovedRegions`) | Need: Runtime enforcement in provisioning |

**Agents Involved:** Compliance, KnowledgeBase

---

### 3. ATO Documentation & Evidence

| Capability | Current State | Implementation | Gap Analysis |
|------------|---------------|----------------|--------------|
| **SSP Generation** | ✅ Complete | `generate_compliance_document` type="SSP" | Uses AI + FedRAMP templates |
| **SAR Generation** | ✅ Complete | `generate_compliance_document` type="SAR" | Includes control assessment narratives |
| **POA&M Generation** | ✅ Complete | `generate_compliance_document` type="POAM" | Links findings to remediation plans |
| **Evidence Collection** | ✅ Complete | `collect_evidence` tool | Storage account integration |
| **Control Narratives** | ✅ Complete | AI-generated from templates | FedRAMP template library |
| **Architecture Diagrams** | ❌ Missing | No diagram generation | Need: Mermaid/Visio diagram tool |
| **SAP Generation** | ❌ Missing | Not implemented | Need: Security Assessment Plan generator |

**Agents Involved:** Compliance, (Document sub-agent internal)

---

### 4. Code Quality & Security

| Capability | Current State | Implementation | Gap Analysis |
|------------|---------------|----------------|--------------|
| **IaC Static Analysis** | ⚠️ Partial | `CodeScanningEngine` exists | Advisory only, integrates Checkov/tfsec |
| **Secrets Detection** | ✅ Complete | Pattern-based scanning | Configurable patterns |
| **Dependency Scanning** | ⚠️ Partial | Config flag exists | Engine stubbed but not full implementation |
| **PR Review Integration** | ❌ Missing | GitHub extension exists, no PR webhook | Need: GitHub PR review bot |
| **STIG Checks** | ⚠️ Partial | Config flag exists | Engine stubbed |
| **Repository Scanning** | ⚠️ Partial | `ScanRepositoryAsync` method exists | Needs remote repo access enhancement |

**Agents Involved:** Compliance (CodeScanningAgent sub-agent)

---

### 5. FinOps & Cost Management

| Capability | Current State | Implementation | Gap Analysis |
|------------|---------------|----------------|--------------|
| **Cost Analysis** | ✅ Complete | `analyze_azure_costs` (by service, RG, tag) | Real Azure Cost Management API |
| **Optimization Recommendations** | ✅ Complete | `get_optimization_recommendations` | Azure Advisor integration |
| **Budget Management** | ✅ Complete | `manage_budgets` | Create/monitor budgets |
| **Cost Forecasting** | ✅ Complete | `forecast_costs` | 30-day projections |
| **Anomaly Detection** | ✅ Complete | `detect_cost_anomalies` | Threshold-based |
| **What-If Scenarios** | ✅ Complete | `model_cost_scenario` | Pre-deployment cost estimates |
| **Showback/Chargeback** | ⚠️ Partial | Tag-based cost allocation | Need: Team/mission reporting |

**Agents Involved:** Cost Management

---

### 6. Observability & Discovery

| Capability | Current State | Implementation | Gap Analysis |
|------------|---------------|----------------|--------------|
| **Resource Inventory** | ✅ Complete | `discover_azure_resources` (9 tools) | Resource Graph queries |
| **Dependency Mapping** | ✅ Complete | `map_resource_dependencies` | Cross-resource relationships |
| **Health Monitoring** | ✅ Complete | `get_resource_health` | Azure Resource Health API |
| **Tag Compliance** | ✅ Complete | `search_resources_by_tag` | Required tag checking |
| **Multi-Subscription** | ✅ Complete | `list_subscriptions` | Cross-subscription discovery |
| **Orphan Detection** | ⚠️ Partial | Mentioned in prompt | Need: Dedicated orphan resource tool |

**Agents Involved:** Discovery

---

### 7. Access Control & Security Operations

| Capability | Current State | Implementation | Gap Analysis |
|------------|---------------|----------------|--------------|
| **Comprehensive Audit Logging** | ✅ Complete | `AuditLogs` table, `get_assessment_audit_log` | AU-2/AU-3 compliant |
| **RBAC Awareness** | ⚠️ Partial | Tools mention RBAC requirements | Need: Runtime RBAC enforcement |
| **Role Restrictions** | ⚠️ Partial | Documentation mentions role requirements | Need: `[Authorize(Roles=...)]` enforcement |
| **IL Restrictions** | ❌ Missing | IL levels documented but not enforced | Need: IL-based access control |
| **2-Person Integrity** | ❌ Missing | Not implemented | Need: Approval workflow for critical ops |
| **Auto-Expiring Elevation** | ❌ Missing | Not implemented | Need: PIM-style temp privilege grants |
| **PIM Integration** | ⚠️ Partial | `AzurePim` config section exists | Config only, no runtime integration |

**Agents Involved:** (Security Agent exists but minimal), Configuration

---

### 8. Platform Operations

| Capability | Current State | Implementation | Gap Analysis |
|------------|---------------|----------------|--------------|
| **Environment Lifecycle** | ✅ Complete | Create, clone, scale, delete | Full CRUD |
| **Drift Detection** | ✅ Complete | `detect_environment_drift` | Config comparison |
| **Drift Remediation** | ✅ Complete | `remediate_environment_drift` | Auto-fix capability |
| **Blue/Green Deployments** | ⚠️ Partial | Config exists for strategy | Need: Deployment orchestration |
| **Canary Deployments** | ⚠️ Partial | Config exists for phases | Need: Traffic routing integration |
| **Rollback Automation** | ⚠️ Partial | Remediation has rollback | Need: Full deployment rollback |

**Agents Involved:** Environment

---

## Priority Roadmap

### Phase 1: Security & Governance Hardening (High Priority)

| Enhancement | Effort | Impact | NIST Controls |
|-------------|--------|--------|---------------|
| **RBAC Enforcement** - Add `[RequireRole]` attributes to sensitive tools | 2-3 days | High | AC-2, AC-3 |
| **IL-Based Access Control** - Restrict tool access by impact level | 3-5 days | High | AC-3, AC-4 |
| **Pre-Deployment Policy Gates** - Block non-compliant provisioning | 3-5 days | High | CM-3, CM-4 |
| **Runtime Guardrails** - Enforce `ApprovedRegions`, naming conventions | 2-3 days | Medium | CM-7 |

---

### Phase 2: Approval Workflows (High Priority - NIST AC-5, AC-6)

| Enhancement | Effort | Impact | NIST Controls |
|-------------|--------|--------|---------------|
| **2-Person Integrity Approval** - Require approval for destructive ops | 5-7 days | Critical | AC-5, CM-5 |
| **Auto-Expiring Privileges** - Time-boxed elevation for sensitive ops | 3-5 days | High | AC-2(2), AC-6 |
| **PIM Integration** - Connect to Azure AD PIM for JIT access | 5-7 days | High | AC-2(5), AC-6(1) |
| **Approval Audit Trail** - Log all approval decisions | 2-3 days | Medium | AU-2, AU-3 |

---

### Phase 3: Golden Path Service Wizard (Medium Priority)

| Enhancement | Effort | Impact | Status |
|-------------|--------|--------|--------|
| **8-Step Interactive Wizard** - Guided new service onboarding | 7-10 days | High | 🔲 Planned |
| **DoD Metadata Collection** - Mission, IL, ATO timeline, POC | 3-5 days | Medium | 🔲 Planned |
| **Repository Scaffolding** - Auto-create GitHub/ADO repos | 3-5 days | Medium | ✅ Complete |
| **Template Selection AI** - Recommend templates based on requirements | 2-3 days | Medium | 🔲 Planned |

> **Repository Scaffolding** implemented in v0.8.1 via `create_repository` tool (Environment Agent). Supports GitHub + Azure DevOps with project-type templates, CODEOWNERS, CI/CD pipelines, and branch policies.

---

### Phase 4: PR Review Integration (Medium Priority)

| Enhancement | Effort | Impact | Value |
|-------------|--------|--------|-------|
| **GitHub PR Webhook Handler** - Receive PR events | 3-5 days | Medium | Shift-left security |
| **IaC Static Analysis Bot** - Run Checkov/tfsec on PR | 3-5 days | Medium | Early detection |
| **Advisory Comments** - Post findings as PR comments | 2-3 days | Medium | Developer feedback |
| **Approval Gating** - Block merge on critical findings | 2-3 days | Medium | Quality gate |

---

### Phase 5: Documentation Enhancements (Lower Priority)

| Enhancement | Effort | Impact | Value |
|-------------|--------|--------|-------|
| **SAP Generation** - Security Assessment Plan document | 3-5 days | Medium | ATO readiness |
| **Architecture Diagram Generation** - Mermaid/Visio output | 5-7 days | Medium | Documentation |
| **Showback Reports** - Team/mission cost allocation | 3-5 days | Low | FinOps maturity |
| **Orphan Resource Detection** - Dedicated discovery tool | 2-3 days | Low | Cost optimization |

---

## Current Tool Count by Agent

| Agent | Tools | Status |
|-------|-------|--------|
| Compliance | 12 | ✅ Complete |
| Discovery | 9 | ✅ Complete |
| Environment | 11 | ✅ Complete (+1 repo scaffolding) |
| KnowledgeBase | 8 | ✅ Complete |
| Infrastructure | 6 | ✅ Complete |
| Cost Management | 6 | ✅ Complete |
| Configuration | 1 | ✅ Complete |
| Security | 5 | ⚠️ Config only (stubbed) |
| **Total** | **58** | |

---

## Capability Summary by Platform Engineering Principle

### Strengths

| Principle | Score | Evidence |
|-----------|-------|----------|
| **Compliant Code Generation** | 95% | Bicep, Terraform, K8s with NIST controls |
| **Governance Explanations** | 95% | NIST 800-53, RMF, STIG, IL mappings |
| **Documentation Assistant** | 85% | SSP, SAR, POA&M generation |
| **Knowledge & Guidance** | 90% | Template generation, compliance education |
| **Comprehensive Audit Logging** | 90% | AU-2/AU-3 compliant logging |
| **Pre-approved Operations** | 80% | Template-based provisioning |

### Partial Implementation

| Principle | Score | Gap |
|-----------|-------|-----|
| **PR Reviewer** | 40% | Static analysis exists, no PR integration |
| **Role + IL Restrictions** | 30% | Config exists, no runtime enforcement |
| **Policy Guardrails** | 50% | Config-only, not runtime enforced |
| **Golden Path Service Wizard** | 25% | Repository scaffolding done, wizard flow pending |

### Not Implemented

| Principle | Score | Gap |
|-----------|-------|-----|
| **2-Person Integrity Approval** | 0% | No approval workflow |
| **Auto-Expiring Privilege Elevation** | 0% | No PIM integration |

---

## Verification Commands

```bash
# Count tools by searching for Name property
grep -r "public override string Name =>" src/ | wc -l

# Count agent registrations
grep -r "RegisterTool" src/ | wc -l

# Verify audit logging implementation
grep -r "AuditLog" src/ | wc -l

# Check RBAC attributes
grep -r "\[Authorize" src/ | wc -l

# Check governance config usage
grep -r "GovernanceOptions" src/ | wc -l
```

---

## Appendix: Agent-to-Principle Mapping

| Platform Engineering Principle | Primary Agent | Supporting Agents |
|-------------------------------|---------------|-------------------|
| Developer Self-Service | Environment | Infrastructure, Configuration |
| Governance & Compliance | Compliance | KnowledgeBase |
| ATO Documentation | Compliance | (DocumentAgent sub-agent) |
| Code Security | Compliance | (CodeScanningAgent sub-agent) |
| FinOps | Cost Management | Discovery |
| Observability | Discovery | Environment |
| Access Control | Security | Configuration |
| Platform Operations | Environment | Infrastructure |

---

## Next Steps

1. **Prioritize Phase 1 & 2** - Security hardening and approval workflows address critical NIST controls
2. **Design Golden Path Wizard** - Define the 8-step wizard flow and DoD metadata schema
3. **Evaluate PR Integration** - Determine GitHub App vs webhook approach
4. **Plan PIM Integration** - Design Azure AD PIM connector for JIT access

---

*Generated by Platform Engineering Architect Agent*
