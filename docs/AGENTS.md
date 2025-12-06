# Platform Engineering Copilot - Agent Guide

> Comprehensive documentation for all specialized AI agents in the Platform Engineering Copilot

## Overview

The Platform Engineering Copilot uses a **multi-agent orchestration architecture** where specialized AI agents coordinate to handle complex infrastructure, compliance, cost management, and operational tasks. Each agent is an expert in its domain with dedicated plugins, system prompts, and capabilities.

### Related Documentation

📖 **[Agent Coordination Workflows](AGENT-COORDINATION-WORKFLOWS.md)** - Visual diagrams showing how agents coordinate via OrchestratorAgent  
🔧 **[Agent Remediation Boundaries](AGENT-REMEDIATION-BOUNDARIES.md)** - When Compliance Agent self-remediates vs. delegating to Infrastructure Agent  
⚙️ **[Agent Orchestration & Configuration](AGENT-ORCHESTRATION.md)** - Configuration guide and agent discovery

### Architecture

```
User Query → Orchestrator Agent → Specialized Agents → Response
                    ↓
            [Execution Plan]
                    ↓
         ┌──────────┴──────────┐
         ↓                     ↓
    Sequential            Parallel
    Execution            Execution
```

**Key Features:**
- **Intelligent Routing**: Orchestrator analyzes intent and routes to appropriate agents
- **Shared Memory**: Agents share context, files, and results across the conversation
- **Plugin Architecture**: Each agent has specialized plugins for domain-specific operations
- **Azure Integration**: Direct integration with Azure services via MCP Server and Azure SDK
- **No Direct Agent-to-Agent Calls**: All coordination flows through OrchestratorAgent (prevents circular dependencies)

---

## 🤖 Available Agents

| Agent | Icon | Primary Purpose | AgentType Enum |
|-------|------|----------------|----------------|
| [Infrastructure](#-infrastructure-agent) | 🏗️ | Azure resource provisioning, IaC generation | `Infrastructure` |
| [Compliance](#️-compliance-agent) | 🛡️ | NIST 800-53 scanning, security assessments | `Compliance` |
| [Cost Management](#-cost-management-agent) | 💰 | Cost analysis, budget tracking, optimization | `CostManagement` |
| [Environment](#-environment-agent) | 🌍 | Environment lifecycle, cloning, scaling | `Environment` |
| [Discovery](#-discovery-agent) | 🔍 | Resource discovery, inventory, health monitoring | `Discovery` |
| [Knowledge Base](#-knowledge-base-agent) | 📚 | NIST/DoD compliance knowledge retrieval | `KnowledgeBase` |
| [Document](#-document-agent) | 📄 | ATO documentation (SSP, SAR, POA&M) | `Compliance` |
| [ATO Preparation](#-ato-preparation-agent) | 🔐 | ATO package orchestration | `Compliance` |

---

## 🏗️ Infrastructure Agent

**Purpose**: Azure infrastructure provisioning and Infrastructure-as-Code (IaC) generation

### Capabilities

**Resource Provisioning:**
- Create Azure resources via ARM API (Storage Accounts, VMs, AKS, Databases, etc.)
- Multi-cloud support (Azure primary, AWS/GCP secondary)
- Natural language to Azure resource conversion

**Infrastructure-as-Code:**
- **Bicep**: Generate complete templates with Azure MCP Server integration
- **Terraform**: HCL template generation with provider configuration
- **Kubernetes**: YAML manifests for deployments, services, ConfigMaps
- **ARM Templates**: JSON-based resource definitions

**Advanced Features:**
- **Network Topology Design**: VNets, subnets, NSGs, route tables with visual diagrams
- **Predictive Scaling**: Forecast resource needs hours/days/weeks ahead
- **Compliance-Aware Templates**: Automatically inject NIST 800-53 controls into IaC
  - FedRAMP High: 10 controls (AC, AU, SC, IA families)
  - DoD IL5: 15 controls (PE, RA, CA, SI families)
  - PCI-DSS: 8 controls (access control, encryption)

**Plugins:**
- `InfrastructurePlugin`: Main provisioning and template generation
- `ServiceWizardPlugin`: Interactive DoD Service Creation Wizard (8-step workflow)
- `ConfigurationPlugin`: Azure subscription management

### Example Prompts

```
# Resource Creation
"Create a storage account named mydata in eastus with Standard_LRS"
"Deploy an AKS cluster with 3 nodes in rg-prod"

# IaC Generation
"Generate Bicep template for a 3-tier web application"
"Create Terraform for a hub-spoke network topology"

# Compliance
"Create FedRAMP High compliant storage account template"
"Design network topology with DoD IL5 controls"

# Service Wizard
"Start DoD service creation wizard"
"Help me with DoDAAC and Impact Level selection"
```

### Key Services

- `InfrastructureProvisioningService`: Azure resource API interactions (rarely used - only for explicit "deploy NOW" requests)
- `TemplateGenerationService`: **Primary function** - IaC template creation (Bicep/Terraform)
- `NetworkTopologyDesignService`: Network design and visualization
- `PredictiveScalingEngine`: AI-powered scaling forecasts
- `ComplianceAwareTemplateEnhancer`: Inject compliance controls into generated templates
- `PolicyEnforcementService`: Azure Policy integration (shared with Compliance Agent)
- `DeploymentOrchestrationService`: Multi-phase deployment planning

**What Infrastructure Agent Actually Does:**  
Infrastructure Agent **generates Infrastructure-as-Code templates** (Bicep/Terraform) that users review and deploy manually. It does NOT automatically provision resources unless the user explicitly says "deploy NOW" or "create IMMEDIATELY". See [remediation boundaries documentation](AGENT-REMEDIATION-BOUNDARIES.md) for the division between template generation (Infrastructure) vs. direct ARM API changes (Compliance).

### Configuration

```json
{
  "AgentType": "Infrastructure",
  "Temperature": 0.4,
  "MaxTokens": 8000,
  "DefaultSubscriptionId": "..."
}
```

---

## 🛡️ Compliance Agent

**Purpose**: Compliance assessment, NIST 800-53 controls, security scanning, and remediation

### Capabilities

**Compliance Frameworks:**
- **NIST 800-53 Rev 5**: All 18 control families (1000+ controls)
  - AC (Access Control), AU (Audit), SC (System Communications), IA (Identification)
  - CM (Configuration Management), IR (Incident Response), RA (Risk Assessment)
  - Full FedRAMP High, Moderate, Low baseline support
- **NIST 800-171**: CUI protection for DoD contractors
- **ISO 27001**: Information security management
- **SOC 2 Type II**: Trust service criteria
- **PCI-DSS**: Payment card industry standards

**Security Scanning:**
- **Azure Resource Scanning**: Evaluate deployed resources against compliance policies
- **Code Security Scanning**: Analyze repositories for vulnerabilities
- **Azure Defender Integration**: Leverage Microsoft Defender for Cloud findings
- **Gap Analysis**: Identify non-compliant controls with remediation steps

**Automated Remediation:**
- Generate Azure Policy definitions for control enforcement
- Create Bicep/Terraform templates with compliance fixes
- Deploy remediation scripts (PowerShell, Azure CLI)
- Track remediation status and evidence collection

**Evidence Collection:**
- Automated evidence gathering for controls (screenshots, configs, logs)
- Evidence storage in Azure Storage with versioning
- Control narrative generation with evidence references

### Example Prompts

```
# Compliance Scanning
"Run NIST 800-53 compliance scan on subscription xyz"
"Check FedRAMP High baseline compliance for rg-prod"
"Scan resource group for PCI-DSS violations"

# Security Analysis
"Scan this repository for security vulnerabilities"
"Analyze code for STIG compliance issues"
"Check for secrets in code"

# Remediation
"Generate remediation plan for AC-2 control failures"
"Fix non-compliant storage accounts in rg-data"
"Create Azure Policy to enforce encryption at rest"

# Evidence & Documentation
"Collect evidence for AU-2 (Audit Events)"
"Generate control narrative for SC-7 (Boundary Protection)"
```

### Key Services

- `ComplianceEngine`: Core compliance scanning logic
- `CodeScanningEngine`: Repository and code analysis
- `GovernanceEngine`: Azure Policy evaluation
- `ComplianceRemediationService`: Automated remediation for **configuration-level changes** (tags, encryption settings, firewall rules)
  - See [Agent Remediation Boundaries](AGENT-REMEDIATION-BOUNDARIES.md) for what Compliance Agent fixes vs. Infrastructure Agent
- `DefenderForCloudService`: Microsoft Defender integration
- `EvidenceCollectors`: Control-specific evidence gathering

**Remediation Scope:**  
Compliance Agent handles **low-to-medium risk configuration changes** (property updates via Azure ARM API). For **high-risk changes** (resource creation/deletion, topology changes), Compliance Agent delegates to Infrastructure Agent via OrchestratorAgent. See [remediation boundaries documentation](AGENT-REMEDIATION-BOUNDARIES.md) for details.

### Plugins

- `CompliancePlugin`: Main compliance operations
- `ConfigurationPlugin`: Azure subscription management

### Configuration

```json
{
  "AgentType": "Compliance",
  "Temperature": 0.2,
  "MaxTokens": 6000,
  "EnableDefenderIntegration": true
}
```

---

## 💰 Cost Management Agent

**Purpose**: Azure cost analysis, budget management, and cost optimization

### Capabilities

**Cost Analysis:**
- **Real-time Cost Tracking**: Current month spend, daily trends
- **Multi-dimensional Breakdowns**:
  - By resource type (VMs, Storage, Networking)
  - By resource group
  - By location/region
  - By tags (cost center, environment, project)
  - By service tier

**Budget Management:**
- Create and track budgets for subscriptions/resource groups
- Budget alerts and thresholds (50%, 80%, 100%)
- Forecast spend based on historical trends
- Monthly, quarterly, annual budget planning

**Cost Optimization:**
- **Right-sizing Recommendations**: Identify over-provisioned VMs
- **Reserved Instances**: RI coverage analysis and savings estimates
- **Idle Resources**: Detect unused VMs, storage, databases
- **Disk Optimization**: Premium SSD → Standard SSD conversions
- **Auto-shutdown Schedules**: Dev/test resource automation
- **Savings Plans**: Azure Savings Plan recommendations

**Reporting:**
- Cost allocation reports (CSV, JSON)
- Executive dashboards with visualizations
- Cost anomaly detection (unusual spikes)
- Chargeback reports for business units

### Example Prompts

```
# Cost Analysis
"Show cost breakdown for the last 30 days"
"What's the total spend on resource group rg-prod?"
"Cost by service type in East US region"

# Budgets
"Create a $10,000 monthly budget for rg-dev"
"Check budget status for subscription xyz"
"Set alert when we reach 80% of budget"

# Optimization
"Find cost optimization opportunities"
"Show me idle resources I can shut down"
"Calculate savings from Reserved Instances"
"Right-size recommendations for VMs in rg-prod"

# Forecasting
"Forecast next month's cost"
"Estimate annual cost at current run rate"
```

### Key Services

- `CostOptimizationEngine`: Analysis and recommendations
- `AzureCostManagementService`: Azure Cost Management API integration
- `BudgetManagementService`: Budget tracking and alerts

### Plugins

- `CostManagementPlugin`: All cost operations
- `ConfigurationPlugin`: Azure subscription management

### Configuration

```json
{
  "AgentType": "CostManagement",
  "Temperature": 0.3,
  "MaxTokens": 4000
}
```

---

## 🌍 Environment Agent

**Purpose**: Environment lifecycle management, cloning, scaling, and drift detection

### Capabilities

**Environment Lifecycle:**
- **Create**: Provision new environments (dev, test, staging, production)
- **Clone**: Replicate environments with all resources
- **Scale**: Horizontal/vertical scaling of environment resources
- **Destroy**: Clean up environments with dependency-aware deletion

**Deployment Patterns:**
- **Blue-Green Deployments**: Zero-downtime environment swaps
- **Canary Releases**: Gradual traffic shifting (10% → 50% → 100%)
- **Rolling Updates**: Progressive resource updates
- **A/B Testing**: Multiple environment variants

**Configuration Management:**
- **Drift Detection**: Compare actual vs. desired state
- **Environment Promotion**: Dev → Test → Staging → Prod
- **Configuration Sync**: Keep environments consistent
- **Tag Management**: Environment classification and tracking

**Resource Management:**
- Clone resource groups with dependencies
- Environment health monitoring
- Resource dependency mapping
- Backup and restore environments

### Example Prompts

```
# Environment Operations
"Create a new dev environment"
"Clone production to staging environment"
"Scale up the test environment by 50%"
"Destroy the old-dev environment"

# Deployment Patterns
"Deploy with blue-green strategy"
"Start canary deployment with 10% traffic"
"Perform rolling update of production"

# Drift & Configuration
"Check for configuration drift in staging"
"Promote dev environment to test"
"Sync configuration between environments"
```

### Key Services

- `EnvironmentLifecycleService`: Environment CRUD operations
- `EnvironmentCloningService`: Resource replication
- `DeploymentStrategyService`: Blue-green, canary patterns
- `DriftDetectionService`: Configuration drift analysis

### Plugins

- `EnvironmentManagementPlugin`: All environment operations
- `ConfigurationPlugin`: Azure subscription management

### Configuration

```json
{
  "AgentType": "Environment",
  "Temperature": 0.3,
  "MaxTokens": 4000
}
```

---

## 🔍 Discovery Agent

**Purpose**: Azure resource discovery, inventory, health monitoring, and dependency mapping

### Capabilities

**Resource Discovery:**
- **Subscription-wide Discovery**: All resources across subscriptions
- **Resource Group Scoping**: Targeted discovery
- **Type Filtering**: Find specific resource types (VMs, Storage, AKS, etc.)
- **Tag-based Search**: Discover by tags (environment, owner, cost-center)
- **Location-based Search**: Resources in specific regions

**Inventory Management:**
- **Comprehensive Inventory**: All resource properties and metadata
- **Resource Tagging Analysis**: Identify untagged/improperly tagged resources
- **Lifecycle Tracking**: Creation dates, modification dates
- **Cost Allocation**: Resource-level cost assignment

**Health Monitoring:**
- **Resource Health Status**: Azure Resource Health API integration
- **Performance Metrics**: CPU, memory, network, disk utilization
- **Availability Tracking**: Uptime percentages
- **Alert Correlation**: Link alerts to resources

**Dependency Mapping:**
- **Resource Relationships**: VNets → Subnets → NICs → VMs
- **Orphaned Resources**: Disks, NICs, IPs without parent resources
- **Cross-resource Dependencies**: Application service maps
- **Network Topology**: Visualize connectivity

**Reporting:**
- **JSON/CSV/Excel Exports**: Structured inventory data
- **Compliance Reports**: Tagging compliance, naming conventions
- **Architecture Diagrams**: Auto-generated dependency views

### Example Prompts

```
# Resource Discovery
"List all resources in my subscription"
"Find all VMs in East US region"
"Show storage accounts in resource group rg-data"
"Discover AKS clusters with tag environment=production"

# Inventory & Tagging
"Generate inventory report for rg-prod"
"Find resources missing 'cost-center' tag"
"Show all untagged resources"

# Health & Performance
"Check health status of all VMs"
"Show performance metrics for aks-cluster-01"
"Find resources with high CPU utilization"

# Dependencies
"Map dependencies for app-service-001"
"Find orphaned disks and NICs"
"Show network topology for rg-network"
```

### Key Services

- `AzureResourceDiscoveryService`: Resource enumeration via Azure SDK
- `ResourceHealthMonitoringService`: Health and performance tracking
- `DependencyMappingService`: Resource relationship analysis
- `AzureMcpClient`: Azure MCP Server integration

### Plugins

- `AzureResourceDiscoveryPlugin`: Main discovery operations
- `ConfigurationPlugin`: Azure subscription management

### Configuration

```json
{
  "AgentType": "Discovery",
  "Temperature": 0.3,
  "MaxTokens": 4000
}
```

---

## 📚 Knowledge Base Agent

**Purpose**: DoD/NIST compliance knowledge retrieval and question answering

### Capabilities

**Knowledge Domains:**
- **NIST 800-53 Rev 5**: Control descriptions, implementation guidance, assessment procedures
- **NIST 800-171**: CUI protection requirements
- **FedRAMP**: Authorization process, SSP templates, baseline controls
- **DoD Cloud Computing SRG**: Impact Levels (IL2-IL6)
- **RMF Process**: Risk Management Framework steps
- **STIG/SCAP**: Security Technical Implementation Guides
- **eMASS**: Enterprise Mission Assurance Support Service

**Query Types:**
- Control lookup by ID (e.g., "What is AC-2?")
- Implementation guidance ("How do I implement AU-2?")
- Baseline queries ("Show all FedRAMP High controls")
- Control family information ("Tell me about the AC family")
- Compliance mappings ("Map NIST 800-171 to 800-53")

**Response Format:**
- Control ID, title, and description
- Implementation requirements
- Assessment procedures
- Related controls
- References to official documentation

### Example Prompts

```
# Control Lookup
"What is NIST 800-53 control AC-2?"
"Explain AU-2 (Audit Events)"
"Tell me about the SC family controls"

# Implementation Guidance
"How do I implement IA-2 (Identification and Authentication)?"
"What evidence is needed for SC-7 (Boundary Protection)?"

# Baseline Queries
"Show all FedRAMP High baseline controls"
"List DoD IL5 required controls"

# Compliance Questions
"What's the difference between 800-53 and 800-171?"
"How do I get a FedRAMP ATO?"
```

### Key Services

- `KnowledgeBaseSearchService`: Semantic search over compliance documents
- `ControlMappingService`: Cross-framework control mappings

### Plugins

- `KnowledgeBasePlugin`: Knowledge retrieval and Q&A
- `ConfigurationPlugin`: Azure subscription management

### Configuration

```json
{
  "AgentType": "KnowledgeBase",
  "Temperature": 0.2,
  "MaxTokens": 2000,
  "KnowledgeBaseEndpoint": "..."
}
```

---

## 📄 Document Agent

**Purpose**: ATO documentation generation and compliance artifact management

### Capabilities

**Document Types:**
- **SSP (System Security Plan)**: Complete system security documentation
- **SAR (Security Assessment Report)**: Assessment findings
- **SAP (Security Assessment Plan)**: Assessment methodology
- **POA&M (Plan of Action & Milestones)**: Remediation tracking
- **STIG Checklists**: Security Technical Implementation Guide compliance
- **Runbooks**: Operational procedures
- **Architecture Diagrams**: Technical documentation

**Document Operations:**
- **Generate from Templates**: Pre-built FedRAMP/DoD templates
- **Control Narratives**: AI-generated control implementation descriptions
- **Section Updates**: Modify specific SSP sections
- **Evidence Attachment**: Link evidence to controls
- **Version Control**: Track document revisions
- **Export Formats**: PDF, DOCX, Markdown

**Content Generation:**
- AI-powered control narratives based on actual deployments
- Automatic evidence references
- Customer vs. inherited responsibility matrices
- Control implementation summaries

### Example Prompts

```
# Document Generation
"Generate SSP for my production environment"
"Create POA&M for failed controls"
"Generate SAR from latest compliance scan"

# Control Narratives
"Write control narrative for AC-2"
"Generate implementation description for AU-2"

# Document Management
"Update SSP Section 10 (Incident Response)"
"Export SSP as PDF"
"Upload security documentation"

# Templates
"Show available ATO document templates"
"Create STIG checklist for Windows Server"
```

### Key Services

- `DocumentGenerationService`: Template-based document creation
- `ControlNarrativeGenerator`: AI-powered narrative writing
- `DocumentStorageService`: Azure Blob Storage integration

### Plugins

- `DocumentGenerationPlugin`: Document operations
- `ConfigurationPlugin`: Azure subscription management

### Configuration

```json
{
  "AgentType": "Compliance",
  "Temperature": 0.3,
  "MaxTokens": 8000
}
```

---

## 🔐 ATO Preparation Agent

**Purpose**: Authority to Operate (ATO) package orchestration and readiness tracking

### Capabilities

**ATO Package Components:**
- **System Security Plan (SSP)**: Complete with all appendices
- **Security Assessment Report (SAR)**: Assessment findings
- **Plan of Action & Milestones (POA&M)**: Remediation tracking
- **Contingency Plan**: Disaster recovery procedures
- **Incident Response Plan**: Security incident handling
- **Configuration Management Plan**: Change control
- **Continuous Monitoring Plan**: Ongoing security monitoring

**Orchestration:**
- **Multi-agent Coordination**: Orchestrate Compliance, Document, and Security agents
- **Workflow Management**: Step-by-step ATO preparation
- **Readiness Assessment**: Gap analysis and completion tracking
- **Timeline Management**: Track ATO milestones and deadlines

**eMASS Integration:**
- Package formatting for eMASS upload
- Control inheritance mapping
- Artifact organization and submission

### Example Prompts

```
# ATO Package
"Prepare ATO package for production system"
"Check ATO readiness for resource group rg-prod"
"Generate eMASS package"

# Orchestration
"Start ATO preparation workflow"
"What documents do I need for FedRAMP ATO?"
"Create contingency plan for my environment"

# Tracking
"Show ATO package completion status"
"What controls still need evidence?"
```

### Key Services

- `AtoOrchestrationService`: Multi-agent workflow coordination
- `ReadinessAssessmentService`: ATO gap analysis
- `EMassIntegrationService`: eMASS package preparation

### Plugins

- `AtoPreparationPlugin`: ATO orchestration
- `ConfigurationPlugin`: Azure subscription management

### Configuration

```json
{
  "AgentType": "Compliance",
  "Temperature": 0.2,
  "MaxTokens": 6000
}
```

---

## 🔀 Agent Orchestration

### Orchestrator Agent

The **OrchestratorAgent** is the "brain" of the multi-agent system. It analyzes user queries, creates execution plans, and coordinates specialized agents.

**Responsibilities:**
1. **Intent Analysis**: Determine primary intent (infrastructure, compliance, cost, etc.)
2. **Execution Planning**: Create sequential or parallel task plans
3. **Agent Selection**: Route tasks to appropriate specialized agents
4. **Result Aggregation**: Combine results from multiple agents
5. **Error Handling**: Retry logic and fallback strategies

**Execution Patterns:**
- **Sequential**: Tasks must complete in order (e.g., deploy → scan → document)
- **Parallel**: Independent tasks run concurrently (e.g., cost analysis + inventory)

### Intent Keywords

The orchestrator uses keyword matching to route requests:

| Intent | Keywords |
|--------|----------|
| **Infrastructure** | provision, deploy, create resource, bicep, terraform, arm template, network, topology, aks, vm, storage |
| **Compliance** | compliance, nist, 800-53, ato, emass, security, vulnerability, scan, control, assessment |
| **Cost** | cost, estimate, price, budget, spend, optimize, savings, forecast |
| **Environment** | environment, clone, scale, lifecycle, blue-green, canary, staging, production |
| **Discovery** | list, find, discover, inventory, show, resources, dependency, orphaned, tags |
| **Document** | document, diagram, architecture, runbook, ssp, sar, sap, procedure, policy |

### Shared Memory

Agents use **SharedMemory** to share context, files, and results:

```csharp
// Store file for later retrieval
memory.StoreFile(conversationId, "template.bicep", bicepContent);

// Retrieve file in another agent
var template = memory.GetFile(conversationId, "template.bicep");

// Store metadata
memory.StoreMetadata(conversationId, "deploymentId", deploymentId);
```

**Use Cases:**
- InfrastructureAgent generates Bicep → EnvironmentAgent deploys it
- ComplianceAgent scans resources → DocumentAgent generates SSP
- DiscoveryAgent finds resources → CostManagementAgent analyzes costs

---

## Configuration

### Agent Registry (appsettings.json)

```json
{
  "AgentConfiguration": {
    "InfrastructureAgent": {
      "Enabled": true
    },
    "ComplianceAgent": {
      "Enabled": true
    },
    "CostManagementAgent": {
      "Enabled": true
    },
    "EnvironmentAgent": {
      "Enabled": true
    },
    "DiscoveryAgent": {
      "Enabled": true
    },
    "KnowledgeBaseAgent": {
      "Enabled": true
    }
  }
}
```

### Agent-Specific Configuration

Each agent can be configured with:
- **Temperature**: Creativity level (0.0 = deterministic, 1.0 = creative)
- **MaxTokens**: Response length limit
- **Plugins**: Enabled plugin list
- **DefaultSubscriptionId**: Azure subscription to use

---

## Usage Examples

### Multi-Agent Workflows

**Example 1: Deploy and Validate**
```
User: "Deploy a web app with database and run compliance scan"

Orchestrator:
  Task 1: InfrastructureAgent → Deploy resources
  Task 2: ComplianceAgent → Scan deployed resources
  Task 3: DocumentAgent → Generate documentation
```

**Example 2: Cost-Optimized Environment**
```
User: "Create a dev environment and analyze costs"

Orchestrator:
  Parallel:
    - EnvironmentAgent → Create environment
    - CostManagementAgent → Estimate costs
  Sequential:
    - CostManagementAgent → Optimization recommendations
```

**Example 3: ATO Package**
```
User: "Prepare FedRAMP ATO package for production"

Orchestrator:
  Task 1: DiscoveryAgent → Inventory resources
  Task 2: ComplianceAgent → Run NIST 800-53 scan
  Task 3: DocumentAgent → Generate SSP
  Task 4: DocumentAgent → Generate SAR
  Task 5: AtoPreparationAgent → Create POA&M
  Task 6: AtoPreparationAgent → Package for eMASS
```

---

## Troubleshooting

### Agent Not Responding

```bash
# Check agent configuration
cat appsettings.json | grep -A 5 "AgentConfiguration"

# Review logs
docker logs platform-engineering-copilot-mcp | grep "Agent initialized"
```

### Wrong Agent Selected

The orchestrator may route to wrong agent if intent is unclear. Be specific:

❌ "Check my stuff" → Unclear intent
✅ "Run NIST compliance scan on rg-prod" → Clear intent (ComplianceAgent)

### Plugin Errors

```bash
# Verify plugin registration
docker logs platform-engineering-copilot-mcp | grep "Registered.*Plugin"

# Check Azure credentials
az account show
```

---

## Development

### Creating a New Agent

1. **Define Agent Interface**
   ```csharp
   public class MyCustomAgent : ISpecializedAgent
   {
       public AgentType AgentType => AgentType.Custom;
       
       public async Task<AgentResponse> ProcessAsync(AgentTask task, SharedMemory memory)
       {
           // Implementation
       }
   }
   ```

2. **Create Plugin**
   ```csharp
   public class CustomPlugin
   {
       [KernelFunction("custom_operation")]
       [Description("Performs custom operation")]
       public async Task<string> PerformOperation(string input)
       {
           // Implementation
       }
   }
   ```

3. **Register in DI Container**
   ```csharp
   services.AddSingleton<ISpecializedAgent, MyCustomAgent>();
   ```

4. **Update AgentType Enum**
   ```csharp
   public enum AgentType
   {
       // ... existing types
       Custom
   }
   ```

---

## References

- **[Agent Coordination Workflows](AGENT-COORDINATION-WORKFLOWS.md)** - Mermaid diagrams showing agent orchestration patterns
- **[Agent Remediation Boundaries](AGENT-REMEDIATION-BOUNDARIES.md)** - Decision matrix for Compliance vs. Infrastructure remediation
- **[Agent Orchestration & Configuration](AGENT-ORCHESTRATION.md)** - Configuration guide and enabling/disabling agents
- **[Architecture Documentation](ARCHITECTURE.md)** - Overall system architecture
- **[Development Guide](DEVELOPMENT.md)** - Local development setup

- [Architecture Documentation](./ARCHITECTURE.md)
- [Agent Orchestration](./AGENT-ORCHESTRATION.md)
- [Getting Started](./GETTING-STARTED.md)
- [GitHub Copilot Extension](../extensions/platform-engineering-copilot-github/README.md)

---

**Last Updated**: November 2025  
**Version**: 0.6.35
