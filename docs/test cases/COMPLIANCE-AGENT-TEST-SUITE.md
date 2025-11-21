# Compliance Agent Test Suite

**Last Updated:** November 13, 2025  
**Agent:** Compliance  
**Plugin Functions:** 17 total  
**Purpose:** Comprehensive testing of all Compliance Agent capabilities

## 📋 Overview

The Compliance Agent handles NIST 800-53 compliance assessments, remediation, evidence collection, RMF documentation, and security hardening **with a focus on ATO/RMF processes**.

**Key Capabilities:**
- **Compliance Assessments**: NIST 800-53, FedRAMP, DoD IL scans mapped to control families
- **RMF Process Automation**: Evidence collection, eMASS package generation, POA&M creation
- **Control-Centric Remediation**: Fixes mapped to specific NIST controls (not just security findings)
- **ATO Package Preparation**: SSP, SAR, POA&M, Risk Assessment documents
- **Multi-Framework Support**: NIST 800-53 Rev 5, FedRAMP, DoD IL2/IL4/IL5/IL6
- **Defender Integration**: Leverages Azure Defender findings, maps to NIST controls

## 🔄 How This Differs from Defender for Cloud

| Feature | Defender for Cloud | Compliance Agent |
|---------|-------------------|------------------|
| **Primary Focus** | Security posture & threat detection | RMF/ATO compliance process |
| **Output** | Security alerts, recommendations | eMASS packages, POA&Ms, SSPs |
| **Control Mapping** | Azure Security Benchmark, CIS | NIST 800-53, FedRAMP, DoD IL |
| **Evidence Collection** | No | Yes (for ATO packages) |
| **Remediation** | Security-focused | Control-focused (AC-2, AU-3, SC-7) |
| **Integration** | Standalone | Orchestrates Defender + Policy + Advisor |

**Value Add:** This agent translates Azure security findings into compliance artifacts required for federal ATO processes.

## 🎯 Quick Test Commands

```bash
# Test compliance scan
curl -X POST http://localhost:5100/mcp/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "Check NIST 800-53 compliance for subscription 00000000-0000-0000-0000-000000000000"}' | jq .
```

## 🧪 Test Cases by Function Category

### 1️⃣ Compliance Assessment Functions (3 functions)

#### Test COMP-1.1: Run Compliance Assessment (Control-Mapped)
```
Check NIST 800-53 compliance for subscription 00000000-0000-0000-0000-000000000000
```
**Expected Function:** `run_compliance_assessment`  
**Expected Output:**
- ✅ Compliance score (e.g., 78%)
- ✅ Failing controls list mapped to NIST families:
  - **AC-2** (Account Management): 3 violations
  - **AU-3** (Audit Record Content): 5 violations  
  - **SC-7** (Boundary Protection): 2 violations
- ✅ Findings with severity (Critical/High/Medium/Low)
- ✅ Defender for Cloud findings **mapped to controls**
- ✅ Remediation recommendations per control

**How This Differs from DFC:**
- DFC shows: "Security Center recommendations"
- This shows: "NIST 800-53 control violations" (AC-2, AU-3, SC-7)
- DFC says: "Enable MFA"
- This says: "AC-2 violation: MFA not enforced. Remediate to meet NIST requirement."

**Validation:**
- ✅ Agent: Compliance ONLY
- ✅ Intent: "compliance"
- ⏱️ Time: 30-60 seconds
- ✅ Scans existing resources (no provisioning)
- ✅ Control family mapping present

---

#### Test COMP-1.2: Get Control Family Details
```
Show me all controls in the NIST 800-53 Access Control (AC) family
```
**Expected Function:** `get_control_family_details`  
**Expected Output:**
- ✅ AC family controls list (AC-1 through AC-25)
- ✅ Control descriptions
- ✅ Implementation guidance
- ✅ Related control families

**Validation:**
- ✅ Complete AC family coverage
- ✅ Control details accurate

---

#### Test COMP-1.3: Get Compliance Status
```
What's my current compliance status for subscription 00000000-0000-0000-0000-000000000000?
```
**Expected Function:** `get_compliance_status`  
**Expected Output:**
- ✅ Overall compliance percentage
- ✅ Status per control family
- ✅ Recent changes/trends
- ✅ Top violations

**Validation:**
- ✅ Current status shown
- ✅ Historical comparison provided

---

### 2️⃣ Evidence & Documentation Functions (4 functions)

#### Test COMP-2.1: Collect Evidence
```
Collect evidence for all NIST controls scoped to resource group rg-prod-eastus
```
**Expected Function:** `collect_evidence`  
**Expected Output:**
- ✅ Evidence artifacts collected
- ✅ Configuration snapshots
- ✅ Audit logs
- ✅ Security findings
- ✅ Evidence package summary

**Validation:**
- ✅ Evidence files generated
- ✅ Suitable for ATO package
- ✅ Timestamped and organized

---

#### Test COMP-2.2: Generate eMASS Package
```
Generate an eMASS package for my production environment
```
**Expected Function:** `generate_emass_package`  
**Expected Output:**
- ✅ System Security Plan (SSP)
- ✅ Security Assessment Report (SAR)
- ✅ Plan of Action & Milestones (POA&M)
- ✅ Risk Assessment Report
- ✅ Package ready for eMASS upload

**Validation:**
- ✅ All required documents included
- ✅ DoD format compliance
- ✅ Complete and accurate

---

#### Test COMP-2.3: Generate POA&M
```
Create a Plan of Action & Milestones for my compliance gaps
```
**Expected Function:** `generate_poam`  
**Expected Output:**
- ✅ POA&M document
- ✅ Each weakness listed
- ✅ Remediation steps
- ✅ Milestones and target dates
- ✅ Resources required

**Validation:**
- ✅ NIST/DoD format
- ✅ Actionable milestones
- ✅ Realistic timelines

---

#### Test COMP-2.4: Generate Compliance Certificate
```
Generate a compliance certificate for my FedRAMP High assessment
```
**Expected Function:** `generate_compliance_certificate`  
**Expected Output:**
- ✅ Compliance certificate document
- ✅ Assessment details
- ✅ Control implementation status
- ✅ Assessor information
- ✅ Validity period

**Validation:**
- ✅ Professional format
- ✅ Accurate compliance status
- ✅ Suitable for stakeholder review

---

### 3️⃣ Remediation Functions (5 functions)

#### Test COMP-3.1: Generate Remediation Plan
```
Generate a remediation plan for my compliance violations
```
**Expected Function:** `generate_remediation_plan`  
**Expected Output:**
- ✅ Prioritized violation list
- ✅ Remediation steps per violation
- ✅ Effort estimates
- ✅ Dependencies identified
- ✅ Implementation order

**Validation:**
- ✅ Critical violations prioritized
- ✅ Actionable steps provided
- ✅ Realistic effort estimates

---

#### Test COMP-3.2: Execute Remediation
```
Execute automated remediation for resource group rg-prod-eastus
```
**Expected Function:** `execute_remediation`  
**Expected Output:**
- ✅ Remediation execution started
- ✅ Progress tracking
- ✅ Actions taken per violation
- ✅ Success/failure status
- ✅ Verification results

**Validation:**
- ✅ Automated fixes applied
- ✅ No manual intervention for supported fixes
- ⚠️ **WARNING:** Modifies Azure resources

---

#### Test COMP-3.3: Validate Remediation
```
Validate the remediation results for resource group rg-prod-eastus
```
**Expected Function:** `validate_remediation`  
**Expected Output:**
- ✅ Post-remediation scan results
- ✅ Fixed violations confirmed
- ✅ Remaining violations listed
- ✅ Compliance score improvement

**Validation:**
- ✅ Violations confirmed as fixed
- ✅ New compliance score shown

---

#### Test COMP-3.4: Get Remediation Progress
```
Show me the remediation progress for my environment
```
**Expected Function:** `get_remediation_progress`  
**Expected Output:**
- ✅ Overall progress percentage
- ✅ Completed remediations
- ✅ In-progress remediations
- ✅ Pending remediations
- ✅ Estimated completion time

**Validation:**
- ✅ Accurate progress tracking
- ✅ Timeline estimates reasonable

---

#### Test COMP-3.5: Get Remediation Guide
```
How do I remediate NIST control AC-2 failures?
```
**Expected Function:** `get_remediation_guide`  
**Expected Output:**
- ✅ Control-specific remediation steps
- ✅ Azure configurations required
- ✅ Code examples
- ✅ Testing guidance

**Validation:**
- ✅ Detailed step-by-step guide
- ✅ Azure-specific instructions

---

### 4️⃣ Risk & Security Functions (3 functions)

#### Test COMP-4.1: Perform Risk Assessment
```
Perform a risk assessment for subscription 00000000-0000-0000-0000-000000000000
```
**Expected Function:** `perform_risk_assessment`  
**Expected Output:**
- ✅ Risk score (Low/Medium/High/Critical)
- ✅ Identified vulnerabilities
- ✅ Threat analysis
- ✅ Impact assessment
- ✅ Risk mitigation recommendations

**Validation:**
- ✅ Comprehensive risk analysis
- ✅ Prioritized risks shown
- ✅ Actionable mitigations

---

#### Test COMP-4.2: Apply Security Hardening
```
Apply security hardening to resource group rg-prod-eastus
```
**Expected Function:** `apply_security_hardening`  
**Expected Output:**
- ✅ Hardening measures applied
- ✅ Security configurations updated
- ✅ Azure Policy assignments
- ✅ Defender for Cloud enabled
- ✅ Before/after comparison

**Validation:**
- ✅ Security posture improved
- ✅ Hardening verified
- ⚠️ **WARNING:** Modifies Azure resources

---

#### Test COMP-4.3: Validate with Azure Policy
```
Validate my resources against Azure Policy compliance
```
**Expected Function:** `validate_compliance_with_azure_policy`  
**Expected Output:**
- ✅ Policy compliance status
- ✅ Non-compliant resources
- ✅ Policy violations list
- ✅ Remediation guidance

**Validation:**
- ✅ Azure Policy integration working
- ✅ Violations accurately identified

---

### 5️⃣ Timeline & Recommendations (2 functions)

#### Test COMP-5.1: Get Compliance Timeline
```
Show me the compliance timeline for my ATO process
```
**Expected Function:** `get_compliance_timeline`  
**Expected Output:**
- ✅ RMF process timeline
- ✅ Key milestones
- ✅ Current phase
- ✅ Estimated completion
- ✅ Dependencies and blockers

**Validation:**
- ✅ Realistic timeline
- ✅ All RMF phases included

---

#### Test COMP-5.2: Get Compliance Recommendations
```
What compliance recommendations do you have for improving my security posture?
```
**Expected Function:** `get_compliance_recommendations`  
**Expected Output:**
- ✅ Top 10 recommendations
- ✅ Priority order (Quick wins, Medium, Long-term)
- ✅ Effort estimates
- ✅ Expected compliance impact
- ✅ Implementation guidance

**Validation:**
- ✅ Actionable recommendations
- ✅ Prioritized by impact/effort

---

## 🔄 Multi-Turn Conversation Tests

### Test COMP-6.1: Assessment → Remediation Workflow
```
Turn 1: "Check compliance for subscription 00000000-0000-0000-0000-000000000000"
Turn 2: (Agent shows violations)
Turn 3: "Generate a remediation plan"
Turn 4: (Agent shows plan)
Turn 5: "Execute the automated fixes"
```
**Expected Behavior:**
- Progressive workflow through assessment → planning → execution
- Context maintained (subscription ID not re-asked)
- SharedMemory used between steps

**Validation:**
- ✅ Context preserved
- ✅ No redundant questions
- ✅ Logical workflow progression

---

## 🎯 Edge Cases & Error Handling

### Test COMP-7.1: Invalid Subscription ID
```
Check compliance for subscription invalid-id
```
**Expected:** Graceful error message, ask for valid subscription

---

### Test COMP-7.2: No Compliance Violations
```
Check compliance for a fully compliant environment
```
**Expected:** Positive report, congratulatory message, continue monitoring recommendation

---

## 🚨 Anti-Patterns (Should NOT Work This Way)

### ❌ Test COMP-8.1: Infrastructure Request to Compliance
```
Deploy an AKS cluster
```
**Expected:** Routes to **Infrastructure Agent**, NOT Compliance

---

## 📊 Validation Checklist

- [ ] `agentType: "Compliance"` in plan
- [ ] Assessment scans existing resources
- [ ] Remediation modifies resources (with warnings)
- [ ] Evidence collection generates artifacts
- [ ] RMF documentation complete and accurate
- ⏱️ Assessment: 30-60 seconds
- ⏱️ Remediation: 60-180 seconds

---

## 📖 Related Documentation

- **RMF Guidance:** [NIST SP 800-37 Rev. 2](https://csrc.nist.gov/publications/detail/sp/800-37/rev-2/final)
- **NIST Controls:** [NIST SP 800-53 Rev. 5](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)

---

**Last Updated:** November 13, 2025  
**Test Coverage:** 17 functions, 30+ test cases  
**Status:** Ready for comprehensive testing
