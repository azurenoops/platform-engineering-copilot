# Knowledge Base Agent Test Suite

**Last Updated:** December 2, 2025  
**Agent:** KnowledgeBase  
**Plugin Functions:** 20 total  
**Purpose:** Comprehensive testing of all Knowledge Base Agent capabilities

## 📋 Overview

The Knowledge Base Agent provides informational responses about compliance frameworks, NIST controls, STIGs, DoD instructions, and RMF processes. It does NOT perform assessments or scans - those are handled by the Compliance Agent.

**Key Differences:**
- **Knowledge Base Agent**: "What is...", "Explain...", "Show me...", "How does..." (informational queries)
- **Compliance Agent**: "Check...", "Scan...", "Assess...", "Validate..." (actual compliance scanning)

## 🎯 Quick Test Commands

```bash
# Test via MCP endpoint
curl -X POST http://localhost:5100/mcp/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "YOUR_QUERY"}' | jq .

# Check orchestrator routing
docker logs plaform-engineering-copilot-mcp --since 2m | grep -E "(Plan created|Executing task with|agentType)"
```

## 🧪 Test Cases by Function Category

### 1️⃣ NIST Control Family Functions (6 functions)

#### Test KB-1.1: Get NIST Control Family Details
```
What controls are in the NIST 800-53 CM (Configuration Management) family?
```
**Expected Function:** `get_nist_control_family`  
**Expected Output:**
- ✅ List of all CM controls (CM-1 through CM-14)
- ✅ Control titles and objectives
- ✅ Control family description
- ✅ Related security functions

**Validation:**
- ✅ Agent: KnowledgeBase (NOT Compliance)
- ✅ Intent: "knowledge"
- ✅ Response type: Informational
- ⏱️ Time: 5-15 seconds

---

#### Test KB-1.2: Explain Specific NIST Control
```
Explain NIST control AC-2 Account Management in detail
```
**Expected Function:** `explain_nist_control`  
**Expected Output:**
- ✅ Full control description
- ✅ Control enhancements (AC-2(1) through AC-2(13))
- ✅ Implementation guidance
- ✅ Related controls

**Validation:**
- ✅ Agent: KnowledgeBase
- ✅ Detailed explanation of baseline + enhancements
- ✅ Implementation examples provided

---

#### Test KB-1.3: Search NIST Controls by Keyword
```
Find all NIST controls related to encryption
```
**Expected Function:** `search_nist_controls`  
**Expected Output:**
- ✅ List of encryption-related controls (SC-8, SC-13, SC-28, etc.)
- ✅ Brief description of each
- ✅ Control families represented

**Validation:**
- ✅ Search across all control families
- ✅ Keyword matching in titles and descriptions
- ✅ Relevant results only

---

#### Test KB-1.4: Get Control Family Summary
```
Give me a summary of the NIST 800-53 Access Control (AC) family
```
**Expected Function:** `get_nist_control_family`  
**Expected Output:**
- ✅ AC family overview
- ✅ Number of controls in family
- ✅ Key focus areas (authentication, authorization, least privilege)
- ✅ Sample controls (AC-2, AC-3, AC-6)

**Validation:**
- ✅ High-level summary appropriate for overview
- ✅ Family purpose clearly explained

---

#### Test KB-1.5: List All NIST Control Families
```
What are all the NIST 800-53 control families?
```
**Expected Function:** `list_nist_control_families`  
**Expected Output:**
- ✅ All 20 control families (AC, AU, AT, CM, CP, IA, IR, MA, MP, PE, PL, PS, PT, RA, CA, SC, SI, SA, SR, PM)
- ✅ Family names and abbreviations
- ✅ Brief purpose for each

**Validation:**
- ✅ Complete list (20 families)
- ✅ Correctly grouped by category (Management, Operational, Technical)

---

#### Test KB-1.6: Get Baseline Controls
```
What are the baseline NIST 800-53 controls for low-impact systems?
```
**Expected Function:** `get_baseline_controls`  
**Expected Output:**
- ✅ Low baseline control list
- ✅ Minimum required controls per family
- ✅ Explanation of impact levels

**Validation:**
- ✅ Correct baseline (Low = 109 controls, Moderate = 325, High = 421)
- ✅ Impact level clearly stated

---

### 2️⃣ STIG Mapping Functions (4 functions)

#### Test KB-2.1: Map NIST Control to STIGs
```
What STIGs implement NIST control AC-2 Account Management?
```
**Expected Function:** `get_stigs_for_nist_control`  
**Expected Output:**
- ✅ List of STIGs that map to AC-2
- ✅ STIG IDs and titles
- ✅ Implementation details per STIG
- ✅ Applicable platforms (Windows, Linux, Azure, etc.)

**Validation:**
- ✅ Agent: KnowledgeBase
- ✅ Multiple STIG mappings shown
- ✅ Cross-reference between NIST and STIG clear

---

#### Test KB-2.2: Search STIGs by Keyword
```
Find all STIGs related to password requirements
```
**Expected Function:** `search_stigs`  
**Expected Output:**
- ✅ STIGs matching "password" keyword
- ✅ STIG IDs, severity, titles
- ✅ Related NIST controls
- ✅ Multiple platforms covered

**Validation:**
- ✅ Keyword search across STIG database
- ✅ Severity levels shown (CAT I, II, III)
- ✅ Results from multiple STIG libraries

---

#### Test KB-2.3: Get Complete Control Mapping
```
Show me the complete mapping between NIST controls and STIGs
```
**Expected Function:** `get_control_mapping`  
**Expected Output:**
- ✅ Comprehensive NIST → STIG mapping table
- ✅ All control families covered
- ✅ Multiple STIGs per control shown
- ✅ CCI (Control Correlation Identifier) references

**Validation:**
- ✅ Large dataset returned (hundreds of mappings)
- ✅ Structured format (control → STIG → CCI)
- ✅ Useful for compliance crosswalk

---

#### Test KB-2.4: Explain STIG Requirement
```
Explain STIG requirement SV-230222r627750 in detail
```
**Expected Function:** `explain_stig_requirement`  
**Expected Output:**
- ✅ Full STIG requirement text
- ✅ Severity (CAT I/II/III)
- ✅ Check text and fix text
- ✅ Related NIST controls
- ✅ CCI mapping

**Validation:**
- ✅ Agent: KnowledgeBase
- ✅ Complete STIG details provided
- ✅ Implementation guidance included

---

### 3️⃣ RMF Process Functions (3 functions)

#### Test KB-3.1: Explain RMF Step
```
What happens in the RMF Categorize step?
```
**Expected Function:** `explain_rmf_process`  
**Expected Output:**
- ✅ Categorize step purpose
- ✅ Activities performed (FIPS 199, impact analysis)
- ✅ Key deliverables (security categorization)
- ✅ Inputs and outputs
- ✅ Next step in RMF

**Validation:**
- ✅ Agent: KnowledgeBase
- ✅ RMF lifecycle context provided
- ✅ NIST SP 800-37 referenced

---

#### Test KB-3.2: Get Complete RMF Lifecycle
```
Explain the complete RMF process from start to finish
```
**Expected Function:** `explain_rmf_process` (multiple calls or comprehensive)  
**Expected Output:**
- ✅ All 7 RMF steps (Prepare, Categorize, Select, Implement, Assess, Authorize, Monitor)
- ✅ Purpose of each step
- ✅ Key activities per step
- ✅ Deliverables per step
- ✅ Step dependencies

**Validation:**
- ✅ Complete lifecycle explained
- ✅ NIST SP 800-37 Rev. 2 guidance
- ✅ Continuous monitoring emphasis

---

#### Test KB-3.3: RMF Step Comparison
```
What's the difference between RMF Assess and Monitor steps?
```
**Expected Function:** `explain_rmf_process` (for both steps)  
**Expected Output:**
- ✅ Assess: One-time evaluation before ATO
- ✅ Monitor: Continuous ongoing surveillance
- ✅ Key differences in activities
- ✅ Relationship between steps

**Validation:**
- ✅ Clear distinction between steps
- ✅ When each applies in lifecycle

---

### 4️⃣ DoD Instruction Functions (2 functions)

#### Test KB-4.1: Explain DoD Instruction
```
What is DoD Instruction 8500.01 and what does it cover?
```
**Expected Function:** `explain_dod_instruction`  
**Expected Output:**
- ✅ Full instruction title and purpose
- ✅ Scope and applicability
- ✅ Key requirements
- ✅ Related instructions
- ✅ Relationship to RMF

**Validation:**
- ✅ Agent: KnowledgeBase
- ✅ DoD-specific guidance provided
- ✅ Cybersecurity requirements explained

---

#### Test KB-4.2: Search DoD Instructions
```
Find DoD instructions related to cloud security
```
**Expected Function:** `search_dod_instructions`  
**Expected Output:**
- ✅ Relevant DoD instructions (8500.01, 8510.01, etc.)
- ✅ Brief description of each
- ✅ Cloud-specific guidance
- ✅ Compliance requirements

**Validation:**
- ✅ Keyword search across DoD instruction library
- ✅ Cloud security focus
- ✅ DoD Cloud Computing SRG referenced

---

### 5️⃣ DoD Workflow Functions (2 functions)

#### Test KB-5.1: Explain DoD ATO Workflow
```
What is the DoD ATO process and what steps are involved?
```
**Expected Function:** `explain_dod_workflow`  
**Expected Output:**
- ✅ ATO workflow overview
- ✅ Pre-assessment activities
- ✅ Assessment phase
- ✅ Authorization decision
- ✅ Continuous monitoring
- ✅ Roles (AO, ISSO, ISSM)

**Validation:**
- ✅ Agent: KnowledgeBase
- ✅ DoD-specific ATO process explained
- ✅ Different from FedRAMP process

---

#### Test KB-5.2: Compare DoD Workflows
```
What's the difference between DoD ATO and FedRAMP authorization?
```
**Expected Function:** `compare_dod_workflows` or `explain_dod_workflow`  
**Expected Output:**
- ✅ DoD ATO characteristics (DISA oversight, STIG-focused)
- ✅ FedRAMP characteristics (3PAO, JAB/Agency)
- ✅ Key differences in process
- ✅ Common elements
- ✅ When to use which

**Validation:**
- ✅ Clear comparison provided
- ✅ Both processes explained
- ✅ Decision guidance

---

### 6️⃣ Impact Level Functions (1 function)

#### Test KB-6.1: Explain DoD Impact Level
```
What are the requirements for DoD Impact Level 5 (IL5)?
```
**Expected Function:** `explain_impact_level`  
**Expected Output:**
- ✅ IL5 definition (controlled unclassified information)
- ✅ Required NIST controls (High baseline + DoD overlays)
- ✅ Physical security requirements (PE controls)
- ✅ Azure Government requirements
- ✅ Differences from IL4

**Validation:**
- ✅ Agent: KnowledgeBase
- ✅ IL5-specific guidance
- ✅ Enhanced controls beyond NIST High baseline
- ✅ DoD Cloud Computing SRG referenced

---

#### Test KB-6.2: Compare Impact Levels
```
What's the difference between IL2, IL4, and IL5?
```
**Expected Function:** `explain_impact_level` (multiple)  
**Expected Output:**
- ✅ IL2: Controlled unclassified, low sensitivity
- ✅ IL4: Controlled unclassified, moderate sensitivity
- ✅ IL5: Controlled unclassified, high sensitivity
- ✅ Control differences per level
- ✅ Use cases for each

**Validation:**
- ✅ Clear comparison table/list
- ✅ Control progression explained
- ✅ Azure cloud region requirements

---

## 🔄 Multi-Turn Conversation Tests

### Test KB-7.1: Follow-up Questions
```
Turn 1: "What is NIST control CM-6?"
Turn 2: "How does this relate to STIGs?"
Turn 3: "What about the RMF process?"
```
**Expected Behavior:**
- Turn 1: `explain_nist_control` for CM-6
- Turn 2: `get_stigs_for_nist_control` for CM-6
- Turn 3: Context maintained, explains CM-6 role in RMF Select step

**Validation:**
- ✅ Context preserved across turns
- ✅ Agent remembers previous control (CM-6)
- ✅ No redundant information repeated

---

### Test KB-7.2: Progressive Detail Drilling
```
Turn 1: "Tell me about the CM control family"
Turn 2: "What about CM-2 specifically?"
Turn 3: "What STIGs implement CM-2?"
```
**Expected Behavior:**
- Turn 1: `get_nist_control_family` for CM
- Turn 2: `explain_nist_control` for CM-2
- Turn 3: `get_stigs_for_nist_control` for CM-2

**Validation:**
- ✅ Progressive narrowing of scope
- ✅ Each turn builds on previous
- ✅ Appropriate level of detail per question

---

## 🎯 Edge Cases & Error Handling

### Test KB-8.1: Invalid Control ID
```
What is NIST control ZZ-99?
```
**Expected:** Graceful error message, suggest valid control families

---

### Test KB-8.2: Ambiguous Request
```
Tell me about controls
```
**Expected:** Agent asks clarifying questions:
- "Which controls? NIST 800-53, STIGs, DoD-specific?"
- "Any specific control family or topic?"

---

### Test KB-8.3: Mixed Intent (Knowledge + Action)
```
Explain NIST AC-2 and scan my subscription for compliance
```
**Expected:** 
- First: KnowledgeBase explains AC-2
- Then: Orchestrator routes compliance scan to Compliance Agent
- Two-phase response

**Validation:**
- ✅ Knowledge question answered first
- ✅ Action request routed to correct agent
- ✅ Both responses provided

---

### 9️⃣ Azure Documentation & Best Practices (2 functions)

#### Test KB-9.1: Search Azure Documentation
```
Search Azure docs for AKS private cluster networking
```
**Expected Function:** `search_azure_documentation`  
**Expected Output:**
- ✅ Relevant documentation excerpts
- ✅ Configuration examples
- ✅ Links to official docs
- ✅ Step-by-step guides

**Validation:**
- ✅ Accurate search results
- ✅ Azure-specific content
- ✅ Official Microsoft documentation

---

#### Test KB-9.2: Get Resource Best Practices
```
What are the best practices for my storage account?
```
**Expected Function:** `get_resource_best_practices`  
**Expected Output:**
- ✅ Resource-specific best practices
- ✅ Azure MCP recommendations
- ✅ Security hardening steps
- ✅ Cost optimization tips

**Validation:**
- ✅ Relevant best practices
- ✅ Actionable recommendations

---

## 🚨 Anti-Patterns (Should NOT Work This Way)

### ❌ Test KB-10.1: Assessment Request to Knowledge Base
```
Check my subscription for NIST 800-53 compliance
```
**Expected:** Routes to **Compliance Agent**, NOT Knowledge Base Agent  
**Reason:** This is an assessment/scan, not informational

---

### ❌ Test KB-10.2: Resource Creation Request
```
Deploy infrastructure with NIST controls
```
**Expected:** Routes to **Infrastructure Agent**, NOT Knowledge Base Agent  
**Reason:** This is resource creation, not informational

---

## 📊 Validation Checklist

For each Knowledge Base Agent test, verify:

### Agent Routing
- [ ] `agentType: "KnowledgeBase"` in plan
- [ ] Only KnowledgeBase agent invoked (not Compliance, Infrastructure, etc.)
- [ ] Intent: "knowledge" (not "compliance", "infrastructure")

### Response Quality
- [ ] Informational response (not "scan started" or "resources created")
- [ ] Cites NIST, DoD, or STIG sources appropriately
- [ ] Provides actionable information
- [ ] No hallucinated data (accurate control IDs, STIG references)

### Performance
- [ ] Response time: 5-15 seconds (informational queries are fast)
- [ ] No unnecessary API calls
- [ ] Plugin function invoked (check logs for function name)

### Context Handling
- [ ] Multi-turn conversations maintain context
- [ ] No redundant questions
- [ ] Previous answers referenced appropriately

---

## 🐛 Known Issues & Limitations

### Issue 1: Generic Responses
**Problem:** Knowledge Base returns generic Azure docs instead of specific NIST/STIG info  
**Cause:** Agent not being invoked (routing to Infrastructure instead)  
**Status:** ✅ FIXED (2025-11-13) - Added `"knowledgebase"` to ParseAgentType()

### Issue 2: Function Not Called
**Problem:** Agent invoked but plugin function not called  
**Cause:** Missing function in plugin or kernel  
**Check:** Look for function name in KnowledgeBasePlugin.cs

### Issue 3: Missing Data
**Problem:** "No STIG mapping found for control X"  
**Cause:** Knowledge base data incomplete  
**Solution:** Verify knowledge base services have data loaded

---

## 📖 Related Documentation

- **Agent Architecture:** [AGENT-ORCHESTRATION.md](./AGENT-ORCHESTRATION.md)
- **Knowledge Base Implementation:** [KNOWLEDGE-BASE-AGENT-RAG.md](./KNOWLEDGE-BASE-AGENT-RAG.md)
- **General Test Cases:** [NATURAL-LANGUAGE-TEST-CASES.md](./NATURAL-LANGUAGE-TEST-CASES.md)
- **RMF Guidance:** [NIST SP 800-37 Rev. 2](https://csrc.nist.gov/publications/detail/sp/800-37/rev-2/final)
- **NIST Controls:** [NIST SP 800-53 Rev. 5](https://csrc.nist.gov/publications/detail/sp/800-53/rev-5/final)

---

## 🎓 Testing Best Practices

1. **Test in Isolation:** Test Knowledge Base Agent separately from Compliance Agent
2. **Check Logs:** Always verify which agent was invoked via MCP logs
3. **Verify Functions:** Confirm the correct plugin function was called
4. **Context Matters:** Test multi-turn conversations to verify context handling
5. **Edge Cases:** Test invalid inputs, ambiguous requests, mixed intents

---

**Last Updated:** December 2, 2025  
**Test Coverage:** 20 functions, 32+ test cases  
**Status:** Knowledge Base Agent routing FIXED and ready for testing
