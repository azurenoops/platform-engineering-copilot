# Agent Coordination Workflow Diagrams

> **Visual Guide:** Understanding how agents coordinate through OrchestratorAgent

---

## Table of Contents

1. [High-Level Architecture](#high-level-architecture)
2. [Basic Workflows](#basic-workflows)
3. [Advanced Orchestration Patterns](#advanced-orchestration-patterns)
4. [Error Handling & Rollback](#error-handling--rollback)
5. [Real-World Scenarios](#real-world-scenarios)

---

## High-Level Architecture

### System Overview

```mermaid
graph TB
    User[👤 User] --> Chat[💬 Chat Interface]
    Chat --> ICS[IntelligentChatService]
    ICS --> Orch[🎼 OrchestratorAgent]
    
    Orch --> SM[SharedMemory]
    
    Orch --> Infra[🏗️ Infrastructure Agent]
    Orch --> Comp[🛡️ Compliance Agent]
    Orch --> Cost[💰 Cost Management Agent]
    Orch --> Env[🌍 Environment Agent]
    Orch --> Disc[🔍 Discovery Agent]
    Orch --> KB[📚 Knowledge Base Agent]
    
    Comp --> Code[Code Scanning Sub-Agent]
    Comp --> ATO[ATO Preparation Sub-Agent]
    Comp --> Doc[Document Sub-Agent]
    
    Infra --> InfraPlug[InfrastructurePlugin]
    Comp --> CompPlug[CompliancePlugin]
    Cost --> CostPlug[CostManagementPlugin]
    Disc --> DiscPlug[DiscoveryPlugin]
    
    InfraPlug --> Azure[☁️ Azure MCP Client]
    DiscPlug --> Azure
    CompPlug --> Azure
    
    SM --> Infra
    SM --> Orch
    
    style Orch fill:#e8f5e9,stroke:#1b5e20,stroke-width:3px
    style SM fill:#fff3e0,stroke:#e65100,stroke-width:2px
    style Azure fill:#e3f2fd,stroke:#0d47a1,stroke-width:2px
```

### Agent Interaction Rules

```
┌─────────────────────────────────────────────────────────────┐
│                   COORDINATION RULES                         │
├─────────────────────────────────────────────────────────────┤
│ ✅ Agents communicate ONLY via OrchestratorAgent           │
│ ✅ Agents share context via SharedMemory (singleton)        │
│ ✅ ConfigurationPlugin shared by ALL agents (subscription)  │
│ ✅ Sub-agents (Code, ATO, Document) use same AgentType     │
│ ❌ NO direct agent-to-agent function calls                  │
│ ❌ NO circular dependencies                                 │
│ ❌ NO shared mutable state (except SharedMemory)           │
└─────────────────────────────────────────────────────────────┘
```

---

## Basic Workflows

### 1. Single Agent Request

```mermaid
sequenceDiagram
    participant User
    participant Chat
    participant Orchestrator
    participant InfraAgent
    participant Azure
    
    User->>Chat: "Generate Bicep template for VNet"
    Chat->>Orchestrator: ProcessRequestAsync()
    
    Note over Orchestrator: Analyze intent
    Note over Orchestrator: Determine: Infrastructure Agent
    
    Orchestrator->>Orchestrator: CreateExecutionPlan()
    Note over Orchestrator: Plan: Sequential, 1 step
    
    Orchestrator->>InfraAgent: ProcessAsync(AgentTask)
    
    InfraAgent->>InfraAgent: Build system prompt
    InfraAgent->>InfraAgent: Invoke InfrastructurePlugin
    
    InfraAgent->>Azure: Query existing resources (optional)
    Azure-->>InfraAgent: Resource metadata
    
    InfraAgent->>InfraAgent: Generate Bicep template
    
    InfraAgent-->>Orchestrator: AgentResponse (template)
    
    Orchestrator->>Orchestrator: Synthesize response
    Orchestrator-->>Chat: Final response
    Chat-->>User: Display Bicep template
```

**Key Points:**
- Simple intent → Single agent execution
- No SharedMemory needed for isolated requests
- Orchestrator handles routing automatically

---

### 2. Sequential Multi-Agent Workflow

```mermaid
sequenceDiagram
    participant User
    participant Orchestrator
    participant Discovery
    participant Compliance
    participant SharedMemory
    
    User->>Orchestrator: "Scan my resources for compliance"
    
    Note over Orchestrator: Intent: Discovery + Compliance
    Note over Orchestrator: Plan: Sequential (Discovery → Compliance)
    
    Orchestrator->>Discovery: Step 1: Discover resources
    
    Discovery->>Discovery: List all resources in subscription
    Discovery->>SharedMemory: Store resource inventory
    
    Discovery-->>Orchestrator: AgentResponse (100 resources found)
    
    Orchestrator->>Compliance: Step 2: Assess compliance
    
    Compliance->>SharedMemory: Get resource inventory
    Compliance->>Compliance: Scan against NIST 800-53
    Compliance->>SharedMemory: Store findings
    
    Compliance-->>Orchestrator: AgentResponse (45 findings)
    
    Note over Orchestrator: Synthesize both responses
    Orchestrator-->>User: Summary + detailed findings
```

**Key Points:**
- Discovery runs first to populate SharedMemory
- Compliance reads from SharedMemory
- Sequential execution ensures data availability

---

### 3. Parallel Multi-Agent Workflow

```mermaid
sequenceDiagram
    participant User
    participant Orchestrator
    participant Compliance
    participant Cost
    participant Discovery
    
    User->>Orchestrator: "Analyze this resource group"
    
    Note over Orchestrator: Intent: Multi-faceted analysis
    Note over Orchestrator: Plan: Parallel (independent ops)
    
    par Parallel Execution
        Orchestrator->>Compliance: Assess compliance
        Orchestrator->>Cost: Estimate costs
        Orchestrator->>Discovery: Inventory resources
    end
    
    Compliance-->>Orchestrator: 12 findings
    Cost-->>Orchestrator: $450/month
    Discovery-->>Orchestrator: 25 resources
    
    Note over Orchestrator: Aggregate all responses
    Orchestrator-->>User: Combined report
```

**Key Points:**
- Independent operations run in parallel
- Faster execution (no blocking)
- Orchestrator aggregates results

---

## Advanced Orchestration Patterns

### 4. Compliance Remediation Flow

```mermaid
sequenceDiagram
    participant User
    participant Orchestrator
    participant Compliance
    participant InfraAgent
    participant SharedMemory
    participant Azure
    
    User->>Orchestrator: "Fix compliance issues in rg-prod"
    
    Note over Orchestrator: Plan: Discovery → Compliance → Remediation
    
    Orchestrator->>Compliance: Step 1: Scan for issues
    
    Compliance->>Azure: Get resources via MCP
    Azure-->>Compliance: Resource details
    
    Compliance->>Compliance: Assess against NIST 800-53
    
    Note over Compliance: Findings:<br/>5 Low Risk (tags)<br/>3 Medium Risk (encryption)<br/>2 High Risk (NSG rules)
    
    Compliance->>SharedMemory: Store findings
    Compliance-->>Orchestrator: Assessment complete (10 findings)
    
    Note over Orchestrator: Analyze risk levels
    
    Orchestrator->>Compliance: Step 2: Remediate Low/Medium risk
    
    Compliance->>Compliance: ComplianceRemediationService
    
    Note over Compliance: Auto-fix:<br/>✓ Add tags (5 resources)<br/>✓ Enable encryption (3 resources)
    
    Compliance->>Azure: PATCH resource properties
    Azure-->>Compliance: Updates successful
    
    Compliance-->>Orchestrator: 8/10 fixed automatically
    
    Orchestrator->>SharedMemory: Get high-risk findings (2 NSG rules)
    
    Orchestrator->>InfraAgent: Step 3: Generate remediation templates
    
    InfraAgent->>InfraAgent: Create NSG rule templates
    InfraAgent-->>Orchestrator: Bicep templates for manual review
    
    Orchestrator-->>User: ✅ 8 auto-fixed<br/>📋 2 require manual review (templates attached)
```

**Key Decisions:**
- Low/Medium risk: Compliance Agent handles via API
- High risk: Infrastructure Agent generates templates
- Orchestrator coordinates based on risk levels

---

### 5. Environment Provisioning with Compliance

```mermaid
sequenceDiagram
    participant User
    participant Orchestrator
    participant InfraAgent
    participant Compliance
    participant Document
    participant SharedMemory
    
    User->>Orchestrator: "Deploy FedRAMP High environment"
    
    Note over Orchestrator: Plan: Provision → Validate → Document
    
    Orchestrator->>InfraAgent: Step 1: Provision infrastructure
    
    InfraAgent->>InfraAgent: Generate IL4-compliant Bicep
    Note over InfraAgent: Resources:<br/>VNet, NSGs, Key Vault<br/>Storage, Log Analytics<br/>Private Endpoints
    
    InfraAgent->>InfraAgent: Deploy via Azure MCP
    InfraAgent->>SharedMemory: Store deployment details
    InfraAgent-->>Orchestrator: Deployment successful (25 resources)
    
    Orchestrator->>Compliance: Step 2: Validate compliance
    
    Compliance->>SharedMemory: Get deployed resources
    Compliance->>Compliance: Scan FedRAMP High baseline
    
    Note over Compliance: Findings:<br/>✓ All security controls met<br/>✗ 3 missing tags
    
    Compliance->>Compliance: Auto-fix missing tags
    Compliance->>SharedMemory: Store compliance report
    Compliance-->>Orchestrator: 100% compliant (after fixes)
    
    Orchestrator->>Document: Step 3: Generate ATO documentation
    
    Document->>SharedMemory: Get deployment + compliance data
    Document->>Document: Generate SSP, SAR, Evidence
    Document-->>Orchestrator: Documentation package ready
    
    Orchestrator-->>User: ✅ Environment deployed<br/>✅ Compliance validated<br/>📄 ATO docs generated
```

**Workflow Benefits:**
- Infrastructure deployed with compliance baseline
- Automatic validation after provisioning
- Documentation auto-generated from actual state

---

### 6. Cost-Aware Infrastructure Design

```mermaid
sequenceDiagram
    participant User
    participant Orchestrator
    participant Cost
    participant InfraAgent
    participant SharedMemory
    
    User->>Orchestrator: "Design AKS cluster within $2000/month budget"
    
    Note over Orchestrator: Plan: Cost Analysis → Design → Validation
    
    Orchestrator->>Cost: Step 1: Analyze budget constraints
    
    Cost->>Cost: Calculate AKS pricing tiers
    Note over Cost: Budget allocation:<br/>Nodes: $1200<br/>Storage: $400<br/>Networking: $300<br/>Monitoring: $100
    
    Cost->>SharedMemory: Store budget breakdown
    Cost-->>Orchestrator: Budget plan created
    
    Orchestrator->>InfraAgent: Step 2: Generate AKS template (with budget constraints)
    
    InfraAgent->>SharedMemory: Get budget constraints
    InfraAgent->>InfraAgent: Design cluster:<br/>3 nodes (Standard_D4s_v3)<br/>Managed disks (Premium SSD)<br/>Standard LB
    
    InfraAgent->>SharedMemory: Store template
    InfraAgent-->>Orchestrator: Template generated
    
    Orchestrator->>Cost: Step 3: Validate against budget
    
    Cost->>SharedMemory: Get template
    Cost->>Cost: Estimate actual cost: $1,850/month
    
    Cost-->>Orchestrator: ✅ Within budget ($150 buffer)
    
    Orchestrator-->>User: Template ready (estimated: $1,850/month)
```

**Coordination Pattern:**
- Cost Agent sets constraints
- Infrastructure Agent respects constraints via SharedMemory
- Cost Agent validates final design

---

## Error Handling & Rollback

### 7. Failed Remediation with Rollback

```mermaid
sequenceDiagram
    participant User
    participant Orchestrator
    participant Compliance
    participant Azure
    participant SharedMemory
    
    User->>Orchestrator: "Remediate compliance findings"
    
    Orchestrator->>Compliance: Execute remediation plan
    
    Compliance->>SharedMemory: Create rollback snapshot
    Note over SharedMemory: Stores original resource configs
    
    Compliance->>Azure: Action 1: Enable encryption ✅
    Azure-->>Compliance: Success
    
    Compliance->>Azure: Action 2: Update firewall ✅
    Azure-->>Compliance: Success
    
    Compliance->>Azure: Action 3: Enable purge protection ❌
    Azure-->>Compliance: Error: Insufficient permissions
    
    Note over Compliance: Detect failure<br/>RollbackOnFirstFailure: true
    
    Compliance->>SharedMemory: Get rollback snapshot
    
    Compliance->>Azure: Rollback Action 2
    Azure-->>Compliance: Reverted
    
    Compliance->>Azure: Rollback Action 1
    Azure-->>Compliance: Reverted
    
    Compliance-->>Orchestrator: Remediation failed (rolled back)
    
    Orchestrator-->>User: ❌ Remediation failed: Insufficient permissions<br/>✅ All changes rolled back
```

**Safety Features:**
- Snapshot before changes
- Rollback on first failure
- Return to original state

---

### 8. Partial Success Handling

```mermaid
sequenceDiagram
    participant User
    participant Orchestrator
    participant Compliance
    participant InfraAgent
    participant SharedMemory
    
    User->>Orchestrator: "Make subscription compliant"
    
    Orchestrator->>Compliance: Scan subscription (1000 resources)
    Compliance->>SharedMemory: Store 250 findings
    Compliance-->>Orchestrator: Assessment complete
    
    Orchestrator->>Compliance: Remediate findings (batch 1-100)
    
    Note over Compliance: Process 100 findings:<br/>85 success<br/>15 failures (locked resources)
    
    Compliance->>SharedMemory: Store success list + failure details
    Compliance-->>Orchestrator: Batch 1: 85% success
    
    Orchestrator->>Compliance: Remediate findings (batch 101-200)
    
    Note over Compliance: Process 100 findings:<br/>92 success<br/>8 failures
    
    Compliance-->>Orchestrator: Batch 2: 92% success
    
    Note over Orchestrator: Analyze failures<br/>15 locked resources<br/>8 permission issues
    
    Orchestrator->>InfraAgent: Generate unlock scripts for locked resources
    InfraAgent-->>Orchestrator: PowerShell scripts ready
    
    Orchestrator-->>User: ✅ 177/200 fixed automatically<br/>⚠️ 15 locked resources (script attached)<br/>❌ 8 permission issues (request admin access)
```

**Partial Success Strategy:**
- Continue on non-critical errors
- Track successes and failures separately
- Provide actionable next steps

---

## Real-World Scenarios

### 9. Complete ATO Preparation Workflow

```mermaid
graph TB
    Start([User: Prepare ATO package]) --> Orch[OrchestratorAgent]
    
    Orch --> Phase1[Phase 1: Discovery]
    Phase1 --> Disc[Discovery Agent]
    Disc --> SM1[SharedMemory:<br/>Resource Inventory]
    
    SM1 --> Phase2[Phase 2: Compliance Assessment]
    Phase2 --> Comp1[Compliance Agent]
    Comp1 --> SM2[SharedMemory:<br/>Control Assessment]
    
    SM2 --> Phase3[Phase 3: Remediation]
    Phase3 --> Decision{Risk Level?}
    
    Decision -->|Low/Medium| Comp2[Compliance Agent<br/>Auto-Remediate]
    Decision -->|High| Infra[Infrastructure Agent<br/>Generate Templates]
    
    Comp2 --> SM3[SharedMemory:<br/>Remediation Results]
    Infra --> SM3
    
    SM3 --> Phase4[Phase 4: Re-Assessment]
    Phase4 --> Comp3[Compliance Agent<br/>Verify 100% Compliance]
    
    Comp3 --> SM4[SharedMemory:<br/>Final Compliance State]
    
    SM4 --> Phase5[Phase 5: Documentation]
    Phase5 --> DocAgent[Document Agent]
    
    DocAgent --> SSP[Generate SSP]
    DocAgent --> SAR[Generate SAR]
    DocAgent --> POAM[Generate POA&M]
    DocAgent --> Evidence[Collect Evidence]
    
    SSP --> Final[Complete ATO Package]
    SAR --> Final
    POAM --> Final
    Evidence --> Final
    
    Final --> User([User receives<br/>ready-to-submit package])
    
    style Orch fill:#e8f5e9,stroke:#1b5e20,stroke-width:3px
    style SM1 fill:#fff3e0,stroke:#e65100
    style SM2 fill:#fff3e0,stroke:#e65100
    style SM3 fill:#fff3e0,stroke:#e65100
    style SM4 fill:#fff3e0,stroke:#e65100
    style Final fill:#c8e6c9,stroke:#2e7d32,stroke-width:2px
```

**Execution Plan:**
```yaml
Total Phases: 5
Estimated Duration: 45-60 minutes (automated)
Agent Coordination:
  - Phase 1: Discovery Agent (5 min)
  - Phase 2: Compliance Agent - Assessment (15 min)
  - Phase 3: Compliance + Infrastructure Agents (10-20 min)
  - Phase 4: Compliance Agent - Validation (5 min)
  - Phase 5: Document Agent (10-15 min)

SharedMemory Usage:
  - Resource inventory (Phase 1 → Phase 2)
  - Control findings (Phase 2 → Phase 3)
  - Remediation results (Phase 3 → Phase 4)
  - Compliance state (Phase 4 → Phase 5)
```

---

### 10. Multi-Subscription Compliance Rollout

```mermaid
graph TB
    Start([User: Make all subscriptions compliant]) --> Orch[OrchestratorAgent]
    
    Orch --> GetSubs[Discovery Agent:<br/>List Subscriptions]
    GetSubs --> SubList[3 Subscriptions Found]
    
    SubList --> Par{Parallel Processing}
    
    Par --> Sub1[Subscription A]
    Par --> Sub2[Subscription B]
    Par --> Sub3[Subscription C]
    
    Sub1 --> Disc1[Discovery Agent]
    Sub2 --> Disc2[Discovery Agent]
    Sub3 --> Disc3[Discovery Agent]
    
    Disc1 --> Comp1[Compliance Agent]
    Disc2 --> Comp2[Compliance Agent]
    Disc3 --> Comp3[Compliance Agent]
    
    Comp1 --> Fix1[Auto-Remediate]
    Comp2 --> Fix2[Auto-Remediate]
    Comp3 --> Fix3[Auto-Remediate]
    
    Fix1 --> Result1[Sub A: 45 findings → 42 fixed]
    Fix2 --> Result2[Sub B: 12 findings → 10 fixed]
    Fix3 --> Result3[Sub C: 3 findings → 3 fixed]
    
    Result1 --> Agg[Orchestrator:<br/>Aggregate Results]
    Result2 --> Agg
    Result3 --> Agg
    
    Agg --> Report[Combined Report:<br/>60 findings total<br/>55 auto-fixed 92%<br/>5 require manual review]
    
    Report --> User([User receives<br/>multi-subscription report])
    
    style Par fill:#e1f5fe,stroke:#01579b,stroke-width:2px
    style Agg fill:#fff3e0,stroke:#e65100,stroke-width:2px
```

---

## Summary: Key Coordination Patterns

### Pattern Types

| Pattern | Use Case | Agent Flow | Execution |
|---------|----------|------------|-----------|
| **Single Agent** | Simple requests | User → Orchestrator → Agent → User | Fast, direct |
| **Sequential** | Dependent steps | Agent A → Memory → Agent B | Ordered, data flow |
| **Parallel** | Independent ops | Agent A ‖ Agent B ‖ Agent C | Fast, concurrent |
| **Remediation** | Fix compliance | Compliance (assess) → Compliance/Infra (fix) | Risk-based routing |
| **Provision + Validate** | Deploy infrastructure | Infra → Compliance → Document | Quality assurance |
| **Cost-Aware Design** | Budget constraints | Cost → Infra → Cost | Constraint checking |
| **Multi-Subscription** | Enterprise scale | Parallel per subscription | Scalable |

### SharedMemory Data Flow

```
┌─────────────────────────────────────────────────────┐
│               SharedMemory Contents                  │
├──────────────┬──────────────────────────────────────┤
│ Key          │ Value                                │
├──────────────┼──────────────────────────────────────┤
│ resources    │ List of discovered resources         │
│ findings     │ Compliance assessment results        │
│ remediation  │ Remediation plan + execution results │
│ deployment   │ Infrastructure deployment details    │
│ costs        │ Cost estimates and breakdowns        │
│ evidence     │ Collected evidence for controls      │
│ rollback     │ Resource snapshots for rollback      │
└──────────────┴──────────────────────────────────────┘
```

### Orchestration Decision Tree

```
User Request
    ↓
Intent Analysis
    ├─ Single Domain? → Single Agent (direct)
    ├─ Multiple Domains? → Multi-Agent Workflow
    │   ├─ Independent? → Parallel Execution
    │   └─ Dependent? → Sequential Execution
    ├─ Compliance + Remediation? → Risk-Based Routing
    │   ├─ Low Risk → Compliance Agent (auto)
    │   ├─ Medium Risk → Compliance Agent (confirm)
    │   └─ High Risk → Infrastructure Agent (templates)
    └─ Provisioning? → Provision → Validate → Document
```

### Best Practices

✅ **DO:**
- Use SharedMemory for cross-agent data
- Plan execution order carefully (sequential vs parallel)
- Handle partial failures gracefully
- Provide rollback for risky operations
- Aggregate results from parallel executions

❌ **DON'T:**
- Call agents directly (always via Orchestrator)
- Assume agents run in specific order (declare dependencies)
- Ignore error states (handle and communicate)
- Auto-execute high-risk operations
- Skip validation after remediation
