# Platform Engineering Copilot - Agents Reference

**Version:** 3.1  
**Last Updated:** January 2026

---

## Overview

The Platform Engineering Copilot uses **7 specialized AI agents** built on the BaseAgent/BaseTool pattern. Each agent extends `BaseAgent` and registers domain-specific tools that extend `BaseTool`.

### Agent Summary

| Agent | ID | Tools | Domain |
|-------|-----|-------|--------|
| [Compliance](#compliance-agent) | `compliance` | 12 | NIST 800-53, FedRAMP, remediation |
| [Infrastructure](#infrastructure-agent) | `infrastructure` | 6 | Azure provisioning, IaC generation |
| [Cost Management](#cost-management-agent) | `cost-management` | 6 | Cost analysis, optimization |
| [Discovery](#discovery-agent) | `discovery` | 9 | Resource inventory, health |
| [Environment](#environment-agent) | `environment` | 10 | Template lifecycle, drift detection |
| [Knowledge Base](#knowledge-base-agent) | `knowledgebase` | 8 | Compliance education, NIST/STIG |
| [Configuration](#configuration-agent) | `configuration` | 1 | Subscription settings |

### BaseAgent Pattern

All agents follow this pattern:

```csharp
public class MyAgent : BaseAgent
{
    public override string AgentId => "my-agent";
    public override string AgentName => "My Agent";
    public override string Description => "What this agent does";
    
    public MyAgent(IChatClient chatClient, ILogger logger, MyTool tool)
        : base(chatClient, logger)
    {
        RegisterTool(tool);  // Tools available to this agent
    }
    
    protected override string GetSystemPrompt()
    {
        // Loaded from external prompt file via SystemPromptLoader
        return SystemPromptLoader.LoadFromType<MyAgent>("MyAgent.prompt.txt") ?? "";
    }
}
```

---

## Compliance Agent

**ID:** `compliance`  
**Purpose:** NIST 800-53 compliance assessment, automated remediation, and ATO documentation generation.

### Tools (12)

| Tool | Name | Description |
|------|------|-------------|
| Assessment | `run_compliance_assessment` | Run NIST 800-53 scan against subscription/resource group |
| Batch Remediation | `batch_remediation` | Fix multiple findings filtered by severity |
| Execute Remediation | `execute_remediation` | Fix single finding by finding ID |
| Remediation Plan | `generate_remediation_plan` | Create prioritized remediation plan |
| Validate Remediation | `validate_remediation` | Verify remediation was successful |
| Defender Findings | `get_defender_findings` | Fetch Microsoft Defender for Cloud findings |
| Control Details | `get_control_family_details` | Get NIST control family information |
| Evidence Collection | `collect_evidence` | Gather compliance evidence artifacts |
| Document Generation | `generate_compliance_document` | Generate SSP, SAR, or POA&M documents |
| Compliance Status | `get_compliance_status` | Current compliance summary |
| Compliance History | `get_compliance_history` | Compliance trends over time |
| Audit Log | `get_assessment_audit_log` | Assessment audit trail |

### Example Queries

```
"Run NIST 800-53 compliance scan"
"Check compliance for subscription xyz"
"Start remediation for high-priority issues"
"Generate SSP document"
"What's my secure score?"
"Show Defender for Cloud findings"
"Collect evidence for AC-2 control"
```

### Configuration

```json
{
  "ComplianceAgent": {
    "Enabled": true,
    "Temperature": 0.2,
    "MaxTokens": 4000,
    "DefaultFramework": "NIST80053",
    "DefaultBaseline": "FedRAMPHigh",
    "EnableAutomatedRemediation": true,
    "DefenderForCloud": {
      "Enabled": true,
      "IncludeSecureScore": true,
      "MapToNistControls": true
    }
  }
}
```

---

## Infrastructure Agent

**ID:** `infrastructure`  
**Purpose:** Azure resource provisioning, IaC template generation, and scaling analysis.

### Tools (6)

| Tool | Name | Description |
|------|------|-------------|
| Template Generation | `generate_infrastructure_template` | Generate Bicep or Terraform templates |
| Template Retrieval | `get_template_files` | Retrieve generated template files |
| Provisioning | `provision_infrastructure` | Deploy template to Azure |
| Scaling Analysis | `analyze_scaling` | Predict scaling needs and capacity |
| Azure Arc | `generate_arc_onboarding_script` | Generate Arc onboarding scripts |
| Resource Deletion | `delete_resource_group` | Delete Azure resource group |

### Example Queries

```
"Generate Bicep for an AKS cluster in usgovvirginia"
"Create Terraform for 3-tier web application"
"Deploy the infrastructure template"
"Analyze scaling needs for my VMs"
"Generate Arc onboarding script for my on-prem servers"
"Delete resource group rg-test"
```

### Configuration

```json
{
  "InfrastructureAgent": {
    "Enabled": true,
    "Temperature": 0.4,
    "MaxTokens": 4000,
    "DefaultRegion": "usgovvirginia",
    "EnableComplianceEnhancement": true,
    "DefaultComplianceFramework": "NIST80053",
    "EnablePredictiveScaling": true,
    "EnableAzureArc": true
  }
}
```

---

## Cost Management Agent

**ID:** `cost-management`  
**Purpose:** Azure cost analysis, optimization recommendations, budgets, and forecasting.

### Tools (6)

| Tool | Name | Description |
|------|------|-------------|
| Cost Analysis | `analyze_azure_costs` | Analyze costs by service, resource group, tag |
| Optimization | `get_optimization_recommendations` | Get savings opportunities |
| Budget Management | `manage_budgets` | Monitor budget utilization and alerts |
| Cost Forecast | `forecast_costs` | Project future spending |
| Cost Scenarios | `model_cost_scenario` | What-if analysis for changes |
| Anomaly Detection | `detect_cost_anomalies` | Identify unusual spending patterns |

### Example Queries

```
"Show cost analysis for last 30 days"
"What are my top spending services?"
"Find cost optimization opportunities"
"Forecast costs for next month"
"Are there any cost anomalies?"
"Model cost if I add 5 more VMs"
```

### Configuration

```json
{
  "CostManagementAgent": {
    "Enabled": true,
    "Temperature": 0.3,
    "MaxTokens": 4000,
    "DefaultCurrency": "USD",
    "DefaultTimeframe": "MonthToDate",
    "EnableAnomalyDetection": true,
    "EnableOptimizationRecommendations": true,
    "CostManagement": {
      "AnomalyThresholdPercentage": 50,
      "MinimumSavingsThreshold": 100.00
    }
  }
}
```

---

## Discovery Agent

**ID:** `discovery`  
**Purpose:** Azure resource discovery, inventory, health monitoring, and dependency mapping.

### Tools (9)

| Tool | Name | Description |
|------|------|-------------|
| List Subscriptions | `list_subscriptions` | List accessible Azure subscriptions |
| Resource Discovery | `discover_azure_resources` | Discover resources with filters |
| Resource Details | `get_resource_details` | Get detailed resource properties |
| Resource Health | `get_resource_health` | Check resource health status |
| Subscription Inventory | `get_subscription_inventory` | Full subscription inventory report |
| Resource Group Summary | `get_resource_group_summary` | Summary of resource group contents |
| Resource Group List | `list_resource_groups` | List all resource groups |
| Tag Search | `search_resources_by_tag` | Find resources by tag values |
| Dependency Mapping | `map_resource_dependencies` | Map resource relationships |

### Example Queries

```
"List all my Azure subscriptions"
"Show all VMs in my subscription"
"What resources are in rg-production?"
"Which resources are unhealthy?"
"Map dependencies for my web app"
"Find resources tagged with environment=production"
```

### Configuration

```json
{
  "DiscoveryAgent": {
    "Enabled": true,
    "Temperature": 0.3,
    "MaxTokens": 4000,
    "EnableHealthMonitoring": true,
    "EnableDependencyMapping": true
  }
}
```

---

## Environment Agent

**ID:** `environment`  
**Purpose:** Platform Engineering template management, environment lifecycle, and drift detection.

### Tools (10)

| Tool | Name | Description |
|------|------|-------------|
| List Templates | `list_service_templates` | Browse available service templates |
| Template Details | `get_template_details` | Get template parameters and info |
| Find Template | `find_matching_template` | Find template matching requirements |
| Create Environment | `create_environment_from_template` | Provision from template |
| List Environments | `list_provisioned_environments` | View all environments |
| Clone Environment | `clone_provisioned_environment` | Clone existing environment |
| Scale Environment | `scale_provisioned_environment` | Scale environment resources |
| Delete Environment | `delete_provisioned_environment` | Delete an environment |
| Detect Drift | `detect_environment_drift` | Check for configuration drift |
| Remediate Drift | `remediate_environment_drift` | Auto-fix drift issues |

### Example Queries

```
"Show available service templates"
"I need an environment for a containerized web app"
"Create production environment from AKS template"
"Clone dev environment to staging"
"Scale my test environment to medium"
"Check for configuration drift in production"
"Fix drift in my environment"
```

### Configuration

```json
{
  "EnvironmentAgent": {
    "Enabled": true,
    "Temperature": 0.3,
    "MaxTokens": 4000,
    "EnableDriftDetection": true,
    "EnableAutoRemediation": false
  }
}
```

---

## Knowledge Base Agent

**ID:** `knowledgebase`  
**Purpose:** Compliance education and guidance for NIST 800-53, STIG, RMF, and FedRAMP.

### Tools (8)

| Tool | Name | Description |
|------|------|-------------|
| NIST Control Explainer | `explain_nist_control` | Explain NIST control requirements |
| NIST Control Search | `search_nist_controls` | Find controls by keyword |
| STIG Explainer | `explain_stig` | Explain STIG control requirements |
| STIG Search | `search_stigs` | Search STIG controls |
| RMF Explainer | `explain_rmf` | Explain RMF process and steps |
| Impact Level | `explain_impact_level` | Explain DoD IL2-IL6 levels |
| FedRAMP Templates | `get_fedramp_template_guidance` | FedRAMP template requirements |

### Example Queries

```
"Explain NIST control AC-2"
"What are the requirements for SC-7?"
"Search for controls related to encryption"
"Explain the STIG for Windows Server 2022"
"What are the RMF steps?"
"What's the difference between IL4 and IL5?"
"What templates do I need for FedRAMP High?"
```

### Configuration

```json
{
  "KnowledgeBaseAgent": {
    "Enabled": true,
    "Temperature": 0.2,
    "MaxTokens": 4000,
    "EnableRag": true,
    "EnableSemanticSearch": true
  }
}
```

---

## Configuration Agent

**ID:** `configuration`  
**Purpose:** Azure subscription configuration and platform settings management.

### Tools (1)

| Tool | Name | Description |
|------|------|-------------|
| Configure Subscription | `configure_subscription` | Set, get, or clear default subscription |

### Example Queries

```
"Set my subscription to 453c2549-4cc5-464f-ba66-acad920823e8"
"What's my current subscription?"
"Clear my subscription settings"
```

### Configuration

```json
{
  "ConfigurationAgent": {
    "Enabled": true,
    "Temperature": 0.2,
    "MaxTokens": 2000
  }
}
```

---

## Agent Routing

The `PlatformSelectionStrategy` routes user requests to the appropriate agent based on intent analysis:

| Keywords/Intent | Routed To |
|-----------------|-----------|
| compliance, NIST, FedRAMP, remediation, assessment, SSP, POA&M | Compliance Agent |
| create, deploy, Bicep, Terraform, provision, Arc, template | Infrastructure Agent |
| cost, spend, budget, forecast, optimization, savings | Cost Management Agent |
| list, discover, inventory, health, resources, subscriptions | Discovery Agent |
| environment, template, clone, scale, drift | Environment Agent |
| explain, what is, STIG, RMF, impact level, FedRAMP guidance | Knowledge Base Agent |
| configure, subscription, settings | Configuration Agent |

---

## Adding New Agents

1. Create agent directory: `src/Platform.Engineering.Copilot.Agents/MyDomain/`
2. Implement agent class extending `BaseAgent`
3. Create tools extending `BaseTool`
4. Create prompt file in `Prompts/MyDomainAgent.prompt.txt`
5. Register in DI via `Extensions/ServiceCollectionExtensions.cs`
6. Add configuration section in `appsettings.json`

See [DEVELOPMENT.md](./DEVELOPMENT.md) for detailed implementation guide.
"Search for resources tagged owner:john"
```

### Configuration

```json
{
  "DiscoveryAgent": {
    "Enabled": true,
    "Temperature": 0.3,
    "EnableHealthMonitoring": true
  }
}
```

---

## Environment Agent

**Purpose**: Environment lifecycle management, cloning, and scaling.

### Tools (4)

| Tool | Description |
|------|-------------|
| `clone_environment` | Clone environment to new RG |
| `scale_environment` | Scale environment resources |
| `get_environment_status` | Environment health summary |
| `destroy_environment` | Delete environment resources |

### Example Queries

```
"Clone dev environment to staging"
"Scale production to high availability"
"What's the status of dev environment?"
```

### Configuration

```json
{
  "EnvironmentAgent": {
    "Enabled": true
  }
}
```

---

## Security Agent

**Purpose**: Security posture assessment, vulnerability scanning, and policy enforcement.

### Tools (5)

| Tool | Description |
|------|-------------|
| `get_security_posture` | Overall security score |
| `run_vulnerability_scan` | Scan for vulnerabilities |
| `get_policy_compliance` | Azure Policy compliance |
| `get_security_recommendations` | Security improvement suggestions |
| `get_threat_alerts` | Active threat alerts |

### Example Queries

```
"What's my security posture?"
"Run vulnerability scan"
"Show policy compliance status"
"Are there any active threats?"
```

### Configuration

```json
{
  "SecurityAgent": {
    "Enabled": true
  }
}
```

---

## Fast-Path Selection

The `PlatformSelectionStrategy` routes requests to agents based on keywords:

| Keywords | Agent |
|----------|-------|
| compliance, nist, fedramp, stig, assessment, remediation | Compliance |
| create, deploy, provision, terraform, bicep, kubernetes | Infrastructure |
| cost, spending, budget, savings, optimization | Cost |
| list, resources, inventory, health, discover | Discovery |
| environment, clone, scale, lifecycle | Environment |
| security, vulnerability, threat, policy | Security |

---

## Agent Coordination

Agents coordinate through `PlatformAgentGroupChat` with shared context:

1. **No Direct Agent Calls**: Agents never call each other directly
2. **Shared Memory**: Assessment results cached for multi-turn workflows
3. **Context Passing**: Subscription ID, findings shared across turns
4. **Tool Chaining**: One agent's output can inform another's action

### Example Multi-Turn Workflow

```
Turn 1: "Check compliance" → Compliance Agent runs assessment
Turn 2: "Start remediation" → Uses cached findings (no re-scan)
Turn 3: "Show cost impact" → Cost Agent analyzes affected resources
```

---

## Adding a New Tool

1. **Create tool class** in `Agents/{Agent}/Tools/`:

```csharp
public class MyNewTool : BaseTool
{
    public override string Name => "my_new_tool";
    
    public override string Description =>
        "Description shown to LLM for selection. " +
        "Use when user says: 'do X', 'perform Y'";

    public MyNewTool(ILogger<MyNewTool> logger, IMyService service) 
        : base(logger)
    {
        _service = service;
        Parameters.Add(new ToolParameter("param1", "Description", true));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var param1 = GetRequiredString(arguments, "param1");
        var result = await _service.DoSomethingAsync(param1);
        return ToJson(new { success = true, result });
    }
}
```

2. **Register in DI** (`ServiceCollectionExtensions.cs`):
```csharp
services.AddScoped<MyNewTool>();
```

3. **Inject into agent** constructor and call `RegisterTool(myNewTool)`

4. **Add to MCP tool list** in `McpHttpBridge.cs`

---

## Related Documentation

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System architecture
- [DEPLOYMENT.md](./DEPLOYMENT.md) - Deployment guide
- [.github/prompts/](../.github/prompts/) - Agent prompt files
