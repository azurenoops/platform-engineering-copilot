# Platform Engineering Copilot - Agents Reference

**Version:** 3.1  
**Last Updated:** January 2026

---

## Overview

The Platform Engineering Copilot uses **7 specialized AI agents** built on the BaseAgent/BaseTool pattern. Each agent extends `BaseAgent` and registers domain-specific tools that extend `BaseTool`.

### Agent Summary

| Agent | ID | Tools | Domain |
|-------|-----|-------|--------|
| [Infrastructure](#infrastructure-agent) | `infrastructure` | 6 | Azure provisioning, IaC generation |
| [Cost Management](#cost-management-agent) | `cost-management` | 6 | Cost analysis, optimization |
| [Discovery](#discovery-agent) | `discovery` | 9 | Resource inventory, health |
| [Environment](#environment-agent) | `environment` | 10 | Template lifecycle, drift detection |
| [Knowledge Base](#knowledge-base-agent) | `knowledgebase` | 0 | Shell — future MCP integration |
| [Configuration](#configuration-agent) | `configuration` | 1 | Subscription settings |
| [Security](#security-agent) | `security` | 6 | Defender scores, security posture |

### BaseAgent Pattern

All agents follow this pattern:

```csharp
public class MyAgent : BaseAgent
{
    public override string AgentId => "my-agent";
    public override string AgentName => "My Agent";
    public override string Description => "What this agent does";
    
    public MyAgent(
        ILogger<MyAgent> logger,
        BaseTool[] tools,
        IChatClient? chatClient = null,
        IOptions<AzureOpenAIOptions>? aiOptions = null)
        : base(logger, chatClient, aiOptions)
    {
        foreach (var tool in tools)
            RegisterTool(tool);
    }
    
    public override string GetSystemPrompt()
    {
        // Loaded from external prompt file via SystemPromptLoader
        return SystemPromptLoader.LoadFromType<MyAgent>("MyAgent.prompt.txt") ?? "";
    }
}
```

### AI-Powered Agent Responses

When Azure OpenAI is configured and `AgentAIEnabled` is `true`, agents process messages through the LLM pipeline:

1. **ProcessMessageAsync** — Entry point for AI-powered message processing
2. **BuildChatMessages** — Assembles system prompt + conversation history (up to 10 messages) + user message
3. **BuildAITools** — Wraps registered `BaseTool` instances as `AIFunction` objects for LLM function calling
4. **FunctionInvokingChatClient** — Automatically handles multi-round tool calling loops
5. **Streaming** — Tokens streamed via `IProgress<ProgressUpdate>` for real-time UI updates

When AI is disabled (default) or unavailable, agents fall back to direct tool execution returning raw JSON—preserving full backward compatibility.

```csharp
// AI-enabled: natural-language response
var result = await agent.ProcessMessageAsync(
    "List all Azure resources in my subscription",
    conversationHistory,
    progress,
    cancellationToken);
// Returns: "I found 42 resources across 5 resource groups..."

// Fallback (no AI): raw JSON
// Returns: {"status":"success","resources":[...]}
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
**Purpose:** Platform knowledge and documentation assistance. Shell agent — no tools currently registered. Reserved for future MCP integration.

### Tools (0)

No tools currently registered. This agent serves as a shell for future MCP tool integration.

### Example Queries

```
"What is the platform engineering copilot?"
"Help me understand the agent architecture"
"What documentation is available?"
```

### Configuration

```json
{
  "KnowledgeBaseAgent": {
    "Enabled": true,
    "Temperature": 0.2
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
| create, deploy, Bicep, Terraform, provision, Arc, template | Infrastructure Agent |
| cost, spend, budget, forecast, optimization, savings | Cost Management Agent |
| list, discover, inventory, health, resources, subscriptions | Discovery Agent |
| environment, template, clone, scale, drift | Environment Agent |
| knowledge, documentation, platform, help, guide | Knowledge Base Agent |
| configure, subscription, settings | Configuration Agent |
| secure, score, defender, security, vulnerability | Security Agent |

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
| create, deploy, provision, terraform, bicep, kubernetes | Infrastructure |
| cost, spending, budget, savings, optimization | Cost |
| list, resources, inventory, health, discover | Discovery |
| environment, clone, scale, lifecycle | Environment |
| security, vulnerability, threat, policy | Security |
| knowledge, documentation, platform, help, guide | Knowledge Base |

---

## Agent Coordination

Agents coordinate through `PlatformAgentGroupChat` with shared context:

1. **No Direct Agent Calls**: Agents never call each other directly
2. **Shared Memory**: Assessment results cached for multi-turn workflows
3. **Context Passing**: Subscription ID, findings shared across turns
4. **Tool Chaining**: One agent's output can inform another's action

### Example Multi-Turn Workflow

```
Turn 1: "Check security posture" → Security Agent runs assessment
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
