# Platform Engineering Copilot - Leadership Demo Script

**Date:** December 2025  
**Duration:** 30-45 minutes  
**Audience:** Executive Leadership / Stakeholders  
**Demo URL:** http://52.245.246.154:5001

---

## 📋 Executive Summary

The **Platform Engineering Copilot** is an AI-powered platform that transforms how teams interact with Azure Government environments. Instead of navigating complex portals, running manual scripts, or consulting 500-page compliance documents, users simply **ask questions in plain English**.

### Who Benefits?

| Persona | Pain Points Solved | Time Savings |
|---------|-------------------|--------------|
| **Mission Owners** | Infrastructure provisioning, cost tracking, compliance remediation | Days → Minutes |
| **Platform Engineers** | Resource visibility, multi-subscription management, troubleshooting | Hours → Seconds |
| **RMF/ISSM Personnel** | ATO preparation, control assessments, evidence collection | Weeks → Hours |
| **FinOps Teams** | Cost allocation, optimization, budget forecasting | Monthly → Real-time |

### Key Value Propositions
- 🕐 **90% faster** compliance assessments
- 💰 **20-30% cost savings** through optimization recommendations  
- 📝 **Days to minutes** for infrastructure provisioning
- 🔒 **Compliance built-in** to all generated templates
- 📚 **Instant expertise** - no more searching through NIST documents

---

## 🎯 Demo Flow by Persona

### Demo Structure (Choose Based on Audience)

| Audience Focus | Demos to Prioritize | Duration |
|----------------|---------------------|----------|
| **Executive Overview** | All personas briefly | 30 min |
| **Mission Owner Deep Dive** | Sections 1-2 | 20 min |
| **Platform Engineering Focus** | Sections 3 | 15 min |
| **RMF/Security Focus** | Sections 4-5 | 20 min |

---

## 👤 SECTION 1: Mission Owner Use Cases (15 minutes)

### Who is a Mission Owner?
> Program managers, application owners, and team leads who need infrastructure deployed, costs tracked, and compliance maintained for their specific mission systems.

### Why They Love the Copilot
- **No Azure portal expertise required** - just describe what you need
- **Get compliant infrastructure** without being a security expert
- **Track costs** without finance team delays
- **Fix compliance issues** with step-by-step guidance

---

### 1.1 - Setup Context (30 seconds)
```
Set my subscription to 453c2549-4cc5-464f-ba66-acad920823e8
```
**Why ask this?** Mission owners often have access to multiple subscriptions. This tells the Copilot which environment to work with.

---

### 1.2 - "I need compliant infrastructure" (3 minutes)

**Scenario:** *"I need a new storage account for my application, but it has to meet FedRAMP requirements. I don't know all the security settings needed."*

```
Generate a Bicep template for a FedRAMP High compliant storage account named mission-data-store
```

**Expected Output:** Complete Bicep template with:
- ✅ Encryption at rest (Azure managed keys)
- ✅ TLS 1.2 minimum enforced
- ✅ Private endpoint configuration
- ✅ Soft delete enabled
- ✅ Diagnostic logging to Log Analytics
- ✅ Compliance tags

**Business Value:** 
> *"Without the Copilot, this mission owner would need to: (1) read FedRAMP documentation, (2) understand Azure security settings, (3) write the template manually, (4) have security review it. That's 2-3 days. With Copilot: 30 seconds."*

---

### 1.3 - "How much is my project costing?" (2 minutes)

**Scenario:** *"Finance is asking for my cloud costs, but I don't have time to dig through Azure Cost Management."*

```
What's the cost breakdown for resource group platengcopilot-dev-rg this month?
```

**Expected Output:**
- Total spend for the resource group
- Cost by resource type (Container Instances, SQL, Storage)
- Daily trend
- Projected month-end total

**Follow-up:**
```
Which resources in my resource group are the most expensive?
```

**Business Value:**
> *"Mission owners can answer budget questions in real-time without waiting for monthly reports or bothering the finance team."*

---

### 1.4 - "My system failed a security audit - how do I fix it?" (4 minutes)

**Scenario:** *"Security team told me my storage account isn't compliant. I need to fix it but don't know what's wrong."*

```
Run a compliance scan on resource group platengcopilot-dev-rg
```

**Expected Output:**
- Compliance score (e.g., 65%)
- Specific findings with severity (High, Medium, Low)
- Affected NIST controls
- Remediation recommendations

**Follow-up:**
```
Show me how to fix the high-severity findings
```

**Expected Output:**
- Step-by-step remediation instructions
- Azure CLI commands to run
- Estimated time to remediate

**Business Value:**
> *"Instead of hiring a consultant or waiting for the security team, mission owners can self-service compliance fixes."*

---

### 1.5 - "I need to understand this control requirement" (2 minutes)

**Scenario:** *"The auditor mentioned AC-2. What does that even mean for my system?"*

```
What is NIST 800-53 control AC-2 and how does it apply to my Azure resources?
```

**Expected Output:**
- Control definition (Account Management)
- What it requires
- Azure-specific implementation guidance
- Related controls

**Business Value:**
> *"Mission owners don't need to become compliance experts. They can get plain-English explanations instantly."*

---

### Mission Owner Summary

| Task | Traditional Approach | With Copilot |
|------|---------------------|--------------|
| Create compliant storage | 2-3 days | 30 seconds |
| Get cost breakdown | Wait for monthly report | Instant |
| Fix compliance finding | File ticket, wait for security | Self-service in minutes |
| Understand control | Read NIST document | Plain English answer |

---

## 👷 SECTION 2: Platform Engineer Use Cases (10 minutes)

### Who is a Platform Engineer?
> Infrastructure engineers, DevOps specialists, and cloud architects responsible for managing the entire Azure environment across multiple teams and subscriptions.

### Why They Love the Copilot
- **Single pane of glass** across all resources
- **Natural language queries** instead of complex KQL/Resource Graph
- **Quick troubleshooting** without portal navigation
- **Cross-cutting visibility** for compliance and cost

---

### 2.1 - "What's running in my environment?" (2 minutes)

**Scenario:** *"Leadership asked for a quick inventory. I don't want to click through 50 resource groups."*

```
What resources are running in subscription 453c2549-4cc5-464f-ba66-acad920823e8?
```

**Expected Output:**
- Complete resource inventory (26 resources)
- Breakdown by type (Container Instances: 4, SQL Servers: 2, Storage: 3, etc.)
- Breakdown by resource group
- Health status

**Follow-up:**
```
Show me all container instances and their current status
```

**Business Value:**
> *"What would take 30 minutes of portal clicking takes 10 seconds with Copilot."*

---

### 2.2 - "Find resources with specific characteristics" (2 minutes)

**Scenario:** *"I need to find all storage accounts that might be publicly accessible."*

```
Find all storage accounts in my subscription and show their network access settings
```

**Expected Output:**
- List of storage accounts
- Public access status for each
- Network rules configured
- Private endpoint status

**Business Value:**
> *"Platform engineers can quickly audit configurations without writing Resource Graph queries."*

---

### 2.3 - "Cross-cutting cost analysis" (2 minutes)

**Scenario:** *"We need to reduce cloud spend by 20%. Where should we focus?"*

```
Find cost optimization opportunities across my subscription
```

**Expected Output:**
- Right-sizing recommendations (oversized VMs)
- Unused/idle resources to delete
- Reserved Instance savings potential
- Storage tier optimization

**Follow-up:**
```
What would we save by implementing these recommendations?
```

**Business Value:**
> *"Platform engineers can identify 20-30% cost savings opportunities in minutes."*

---

### 2.4 - "Design new architecture" (3 minutes)

**Scenario:** *"We need to set up network isolation for a new project."*

```
Design a hub-spoke network topology with Azure Firewall for three environments: prod, staging, and dev
```

**Expected Output:**
- Network architecture diagram (text-based)
- Bicep/Terraform templates for all components
- NSG rules for segmentation
- Firewall policy recommendations

**Business Value:**
> *"Architecture that would take a day to design and document is generated in minutes."*

---

### Platform Engineer Summary

| Task | Traditional Approach | With Copilot |
|------|---------------------|--------------|
| Resource inventory | 30+ min portal work | 10 seconds |
| Find misconfigured resources | Write KQL queries | Natural language |
| Identify cost savings | Analyze Cost Management | Instant recommendations |
| Design network architecture | Day of documentation | Minutes |

---

## 🛡️ SECTION 3: RMF / ISSM / Security Personnel Use Cases (15 minutes)

### Who are RMF Personnel?
> Information System Security Managers (ISSMs), Security Control Assessors (SCAs), RMF package developers, and compliance officers responsible for Authorization to Operate (ATO) packages.

### Why They Love the Copilot
- **Automated control assessments** instead of manual evidence collection
- **Instant NIST expertise** without searching 500-page documents
- **Evidence generation** for ATO packages
- **Continuous monitoring** instead of point-in-time assessments

---

### 3.1 - "I need to assess our NIST 800-53 compliance" (5 minutes)

**Scenario:** *"We have an ATO renewal coming up. I need to know our current compliance posture."*

```
Run a NIST 800-53 compliance scan on subscription 453c2549-4cc5-464f-ba66-acad920823e8
```

**Expected Output:**
- Overall compliance score
- Control family breakdown (AC, AU, CM, SC, etc.)
- Specific findings with severity
- Sources (NIST Scanner, STIG findings)

**Talking Point:**
> *"A manual compliance assessment takes 2-4 weeks with multiple analysts. We just did the technical controls in under a minute."*

**Important Context:** 
> *"Some control families like CP (Contingency Planning), IR (Incident Response), and RA (Risk Assessment) may show 0% because they require organizational procedures and documentation that can't be detected from Azure resources alone. The scanner focuses on technical controls that CAN be automated."*

---

### 3.2 - "What does this control actually require?" (3 minutes)

**Scenario:** *"The SCA is asking about our IA-2 implementation. I need to explain what we're doing."*

```
What is NIST 800-53 control IA-2 and what Azure services implement it?
```

**Expected Output:**
- IA-2: Identification and Authentication (Organizational Users)
- Control statement and requirements
- Azure implementation options (Azure AD, MFA, Conditional Access)
- Related controls (IA-2(1), IA-2(2), etc.)

**Follow-up:**
```
What evidence do I need to collect for IA-2 during an audit?
```

**Expected Output:**
- Azure AD configuration exports
- MFA enrollment reports
- Conditional Access policies
- Authentication logs

**Business Value:**
> *"RMF personnel don't need to memorize 1,000+ controls. They can get instant, actionable guidance."*

---

### 3.3 - "Map controls across frameworks" (2 minutes)

**Scenario:** *"We're working toward both FedRAMP and CMMC. How do they overlap?"*

```
What controls are in FedRAMP High baseline?
```

**Follow-up:**
```
How does NIST 800-171 map to NIST 800-53 for CUI protection?
```

**Expected Output:**
- Control mappings between frameworks
- Shared controls (implement once, satisfy multiple frameworks)
- Gap analysis

**Business Value:**
> *"Understanding control overlap can reduce assessment scope by 40%+ when pursuing multiple authorizations."*

---

### 3.4 - "Generate remediation plan for ATO" (3 minutes)

**Scenario:** *"We have 30 findings from our last assessment. I need a POA&M."*

```
Create a prioritized remediation plan for the compliance findings
```

**Expected Output:**
- Prioritized list by severity and risk
- Specific remediation steps per finding
- Estimated level of effort
- Azure CLI/PowerShell commands where applicable
- Timeline recommendations

**Business Value:**
> *"What would take days to compile manually is generated in seconds with actionable guidance."*

---

### 3.5 - "Collect evidence for auditors" (2 minutes)

**Scenario:** *"The auditor wants to see our SC-28 (Protection of Information at Rest) implementation."*

```
Collect compliance evidence for SC-28 in my subscription
```

**Expected Output:**
- Storage account encryption settings
- SQL TDE configuration
- Disk encryption status
- Key Vault configurations
- Evidence artifacts for auditor

**Business Value:**
> *"Evidence collection that takes hours of screenshots and exports is automated."*

---

### RMF Personnel Summary

| Task | Traditional Approach | With Copilot |
|------|---------------------|--------------|
| Full compliance assessment | 2-4 weeks | Minutes |
| Control research | Search NIST PDFs | Instant answers |
| Framework mapping | Manual crosswalk | Automated |
| POA&M development | Days of analysis | Generated instantly |
| Evidence collection | Hours of exports | Automated |

---

## 🎯 SECTION 4: Multi-Agent Power Queries (5 minutes)

### Showcase: The Power of Orchestration

> *"The real magic happens when agents work together. These queries show complex workflows simplified to single questions."*

---

### 4.1 - "Give me the full picture" (2 minutes)
```
Give me a complete status report on resource group platengcopilot-dev-rg including inventory, compliance score, and current spend
```

**What happens behind the scenes:**
1. Discovery Agent → inventories resources
2. Compliance Agent → runs security assessment
3. Cost Agent → calculates spend

**Business Value:**
> *"One question triggers three specialized agents. The orchestrator knows how to coordinate them."*

---

### 4.2 - "Cost-aware compliance" (2 minutes)
```
What security issues exist in my subscription and how much would it cost to fix them?
```

**What happens:**
1. Compliance Agent → identifies security gaps
2. Cost Agent → estimates remediation costs
3. Combined prioritization by risk AND cost

---

### 4.3 - "End-to-end provisioning" (1 minute)
```
I need a new SQL database for a FedRAMP Moderate system. Generate the template and show me what compliance controls it satisfies.
```

**What happens:**
1. Infrastructure Agent → generates compliant SQL template
2. Knowledge Base Agent → maps to NIST controls
3. Compliance Agent → validates against baseline

---

## 📊 Closing Summary

### The Copilot Advantage by Persona

| Persona | Without Copilot | With Copilot |
|---------|-----------------|--------------|
| **Mission Owner** | Depends on platform team for everything | Self-service infrastructure and compliance |
| **Platform Engineer** | Drowning in portal tabs and scripts | Natural language operations |
| **RMF Personnel** | Weeks of manual assessment | Continuous, automated compliance |
| **FinOps** | Monthly reports, reactive | Real-time visibility, proactive |

### Why Natural Language Matters

Traditional approach requires:
- Azure Portal expertise
- KQL/Resource Graph knowledge
- ARM/Bicep/Terraform skills
- NIST document familiarity
- Cost Management navigation

**With Copilot:** Just ask a question.

### ROI Highlights

- **Time Savings:** 90% reduction in compliance assessment time
- **Cost Optimization:** 20-30% cloud cost reduction identified
- **Risk Reduction:** Compliance built into infrastructure from day one
- **Expertise Democratization:** Everyone gets instant compliance knowledge

---

## 🎤 Q&A Preparation

### Anticipated Questions

**Q: How does this integrate with our existing tools?**
> A: REST APIs, webhooks, and GitHub/Azure DevOps extensions available. Integrates into CI/CD pipelines for compliance gates.

**Q: Is the data secure?**
> A: All data stays within your Azure Government subscription. Azure AD authentication with CAC/PIV support.

**Q: What if the AI gives wrong information?**
> A: All outputs are recommendations requiring human review. No automatic changes to infrastructure.

**Q: How much does this cost to run?**
> A: Runs on your Azure infrastructure. Typical: $500-2000/month depending on usage.

**Q: Can we customize the compliance frameworks?**
> A: Yes - custom control families, organizational policies, and baselines can be added.

**Q: What about controls that can't be automated (like policies)?**
> A: The scanner focuses on technical controls. Procedural/policy controls are identified as "requires manual review" with guidance on what documentation is needed.

---

## 🔧 Demo Environment Details

- **Chat URL:** http://52.245.246.154:5001
- **MCP Server:** http://20.158.10.75:5100
- **Region:** Azure Government (usgovvirginia)
- **Subscription:** 453c2549-4cc5-464f-ba66-acad920823e8

---

*Demo script prepared for Platform Engineering Copilot v0.8.0*
