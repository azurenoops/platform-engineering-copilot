# Platform Engineering Copilot - Demo Quick Reference

**Demo URL:** http://[52.245.246.154:5001](http://52.245.246.154:5001/)  
**Subscription ID:** 453c2549-4cc5-464f-ba66-acad920823e8

---

## ⚙️ START HERE: Set Subscription Context

```
Set my subscription to 453c2549-4cc5-464f-ba66-acad920823e8
```

---

## 👤 MISSION OWNER Queries

*"I need infrastructure, cost info, and compliance fixes for my project"*

### Get Compliant Infrastructure
```
Generate a Bicep template for a FedRAMP High compliant storage account named mission-data-store
```

```
Create Terraform for a secure AKS cluster with 3 nodes that meets DoD IL5 requirements
```

### Track My Costs
```
What's the cost breakdown for resource group platengcopilot-dev-rg this month?
```

```
Which resources in my resource group are the most expensive?
```

### Fix Compliance Issues
```
Run a compliance scan on resource group platengcopilot-dev-rg
```

```
Show me how to fix the high-severity findings
```

### Understand Requirements
```
What is NIST 800-53 control AC-2 and how does it apply to Azure?
```

---

## 👷 PLATFORM ENGINEER Queries

*"I need visibility across the entire environment"*

### Resource Inventory
```
What resources are running in subscription 453c2549-4cc5-464f-ba66-acad920823e8?
```

```
Show me all container instances and their current status
```

```
Find all storage accounts and show their network access settings
```

### Cross-Cutting Analysis
```
Find cost optimization opportunities across my subscription
```

```
Give me a complete status report including inventory, compliance score, and current spend
```

### Architecture Design
```
Design a hub-spoke network topology with Azure Firewall for prod, staging, and dev
```

---

## 🛡️ RMF / ISSM / SECURITY Queries

*"I need compliance assessments, control knowledge, and ATO evidence"*

### Compliance Assessments
```
Run a NIST 800-53 compliance scan on subscription 453c2549-4cc5-464f-ba66-acad920823e8
```

```
Run a compliance scan on resource group platengcopilot-dev-rg
```

### Control Knowledge (NO SCAN - just information)
```
What is NIST 800-53 control IA-2?
```

```
What evidence do I need to collect for IA-2 during an audit?
```

```
What controls are in FedRAMP High baseline?
```

```
How does NIST 800-171 map to NIST 800-53?
```

### ATO Preparation
```
Create a prioritized remediation plan for the compliance findings
```

```
Collect compliance evidence for SC-28 in my subscription
```

```
What security issues exist and how do I fix them?
```

---

## 🔄 MULTI-AGENT Power Queries

*"Complex questions that combine multiple capabilities"*

```
Give me a complete assessment of resource group platengcopilot-dev-rg including inventory, compliance, and cost
```

```
Find all storage accounts and check their compliance status
```

```
What resources are non-compliant and how much would it cost to fix them?
```

```
I need a new SQL database for a FedRAMP Moderate system. Generate the template and show me what compliance controls it satisfies.
```

---

## 📊 Quick Persona Guide

| If You Are... | Start With... | Key Benefit |
|---------------|---------------|-------------|
| **Mission Owner** | "Generate a compliant [resource]..." | Self-service infrastructure |
| **Platform Engineer** | "What resources are running..." | Instant visibility |
| **RMF/ISSM** | "What is control [X]?" or "Run compliance scan..." | Compliance expertise on demand |
| **FinOps** | "What's the cost breakdown..." | Real-time cost data |

---

## 💡 Pro Tips

### For Knowledge Questions (No Scan)
Use phrases like:
- "What is..."
- "Explain..."
- "Define..."
- "What does [control] require?"

### For Active Scanning
Use phrases like:
- "Run a scan..."
- "Check compliance..."
- "Assess..."
- "Find security issues..."

### For Infrastructure Generation
Be specific about:
- Resource type (storage, AKS, SQL)
- Compliance framework (FedRAMP High, DoD IL5)
- Naming conventions

---

## ⚠️ Demo Tips

1. **Match queries to audience** - Mission owners want IaC/costs; RMF wants compliance
2. **Explain the "why"** - Why would someone ask this question?
3. **Highlight time savings** - "This would take X hours manually"
4. **Show knowledge vs. scanning** - Different use cases
5. **End with multi-agent** - Shows orchestration power

---

## 🚫 Avoid During Demo

- Asking to "deploy" or "create" actual resources (generates templates only)
- Rushing compliance scans (they take 15-30 seconds)
- Knowledge questions that accidentally trigger scans (fixed in latest version)
- Asking about unsupported clouds (Azure only)

---

*Quick Reference Card - Platform Engineering Copilot v0.8.0*
