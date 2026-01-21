# Platform Engineering Copilot - Architecture

**Version:** 3.1  
**Last Updated:** January 2026

---

## Overview

The Platform Engineering Copilot is an AI-powered infrastructure and compliance platform built on .NET 9.0. The system uses a **BaseAgent/BaseTool pattern** with specialized AI agents coordinated through a Model Context Protocol (MCP) server.

### Key Characteristics

- **BaseAgent/BaseTool Pattern**: All agents extend `BaseAgent`, all tools extend `BaseTool`
- **MCP Server**: Dual-mode operation (HTTP:5100 + stdio for AI clients)
- **Multi-Agent Orchestration**: 7 specialized agents with 52 tools
- **Azure Government**: Primary target with NIST 800-53 compliance

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                         CLIENTS                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌────────────────────────┐ │
│  │ Web Chat     │  │ Admin UI     │  │ AI Clients             │ │
│  │ (5001)       │  │ (5000)       │  │ • GitHub Copilot       │ │
│  │              │  │              │  │ • Claude Desktop       │ │
│  └──────┬───────┘  └──────┬───────┘  └────────┬───────────────┘ │
│         │ HTTP            │ HTTP              │ stdio            │
└─────────┴─────────────────┴───────────────────┴─────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                    MCP SERVER (5100)                             │
│                Platform.Engineering.Copilot.Mcp                  │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │              PlatformAgentGroupChat                         │ │
│  │  ├─ PlatformSelectionStrategy (fast-path routing)          │ │
│  │  ├─ PlatformTerminationStrategy                             │ │
│  │  └─ BaseAgent instances                                     │ │
│  └────────────────────────────────────────────────────────────┘ │
│                              ↓                                   │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │                 SPECIALIZED AGENTS (7)                      │ │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐           │ │
│  │  │Infrastructure│ │ Compliance │ │    Cost     │           │ │
│  │  │   Agent (6) │ │ Agent (12) │ │  Agent (6)  │           │ │
│  │  └─────────────┘ └─────────────┘ └─────────────┘           │ │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐           │ │
│  │  │  Discovery  │ │ Environment│ │ Knowledge   │           │ │
│  │  │   Agent (9) │ │ Agent (10) │ │ Base (8)    │           │ │
│  │  └─────────────┘ └─────────────┘ └─────────────┘           │ │
│  │  ┌─────────────┐                                           │ │
│  │  │Configuration│                                           │ │
│  │  │   Agent (1) │                                           │ │
│  │  └─────────────┘                                           │ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                    AZURE SERVICES                                │
│  • Azure Resource Manager  • Defender for Cloud                 │
│  • Cost Management API     • Azure Policy                       │
│  • Resource Graph          • Key Vault                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## BaseAgent/BaseTool Framework

### Core Abstractions

The framework provides two base classes that all agents and tools must extend:

```
┌──────────────────────────────────────────────────────────────────┐
│                       BaseAgent/BaseTool Pattern                 │
├──────────────────────────────────────────────────────────────────┤
│  BaseAgent (abstract)                                             │
│    ├─ AgentId: string                                             │
│    ├─ AgentName: string                                           │
│    ├─ Description: string                                         │
│    ├─ RegisteredTools: List<BaseTool>                             │
│    ├─ RegisterTool(tool) - adds tool to agent                     │
│    ├─ ProcessAsync(context) → AgentResponse                       │
│    └─ GetSystemPrompt() → string (loaded from embedded resource)  │
├──────────────────────────────────────────────────────────────────┤
│  BaseTool (abstract)                                              │
│    ├─ Name: string (e.g., "run_compliance_assessment")            │
│    ├─ Description: string (shown to LLM for selection)            │
│    ├─ Parameters: List<ToolParameter>                             │
│    ├─ ExecuteAsync(arguments) → string (JSON result)              │
│    └─ AsAITool() → AITool (for LLM function calling)              │
└──────────────────────────────────────────────────────────────────┘
```

### BaseAgent Implementation

```csharp
public class ComplianceAgent : BaseAgent
{
    public override string AgentId => "compliance";
    public override string AgentName => "Compliance Agent";
    public override string Description => "NIST 800-53 compliance scanning and remediation";

    public ComplianceAgent(
        IChatClient chatClient,
        ILogger<ComplianceAgent> logger,
        ComplianceAssessmentTool assessmentTool,
        BatchRemediationTool remediationTool,
        DefenderForCloudTool dfcTool
    ) : base(chatClient, logger)
    {
        RegisterTool(assessmentTool);
        RegisterTool(remediationTool);
        RegisterTool(dfcTool);
    }

    protected override string GetSystemPrompt()
    {
        var template = SystemPromptLoader.LoadFromType<ComplianceAgent>("ComplianceAgent.prompt.txt");
        return SystemPromptLoader.ApplyVariables(template ?? "", new Dictionary<string, string>
        {
            ["agentName"] = AgentName,
            ["agentId"] = AgentId
        });
    }
}
```

### BaseTool Implementation

```csharp
public class ComplianceAssessmentTool : BaseTool
{
    public override string Name => "run_compliance_assessment";
    
    public override string Description =>
        "Run NIST 800-53 compliance assessment against Azure subscription. " +
        "Use when user asks: 'check compliance', 'run assessment', 'NIST scan'";

    public ComplianceAssessmentTool(
        ILogger<ComplianceAssessmentTool> logger,
        IAtoComplianceEngine complianceEngine) : base(logger)
    {
        _complianceEngine = complianceEngine;
        
        Parameters.Add(new ToolParameter("subscription_id", "Azure subscription ID", false));
        Parameters.Add(new ToolParameter("resource_group", "Scope to resource group", false));
        Parameters.Add(new ToolParameter("control_families", "Filter: AC,AU,SC", false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var subscriptionId = GetRequiredString(arguments, "subscription_id");
        var result = await _complianceEngine.RunAssessmentAsync(subscriptionId);
        return ToJson(new { success = true, assessment = result });
    }
}
```

---

## Project Structure

```
src/
├── Platform.Engineering.Copilot.Mcp/           # MCP Server (HTTP:5100 + stdio)
│   ├── Program.cs                              # Dual-mode startup
│   ├── Services/
│   │   └── McpHttpBridge.cs                    # HTTP endpoint bridge
│   └── Dockerfile
│
├── Platform.Engineering.Copilot.Agents/        # All agents and tools
│   ├── Common/
│   │   ├── BaseAgent.cs                        # Agent base class
│   │   ├── BaseTool.cs                         # Tool base class
│   │   └── SystemPromptLoader.cs               # External prompt loader
│   │
│   ├── Prompts/                                # Externalized agent prompts
│   │   ├── ComplianceAgent.prompt.txt
│   │   ├── InfrastructureAgent.prompt.txt
│   │   ├── CostManagementAgent.prompt.txt
│   │   ├── DiscoveryAgent.prompt.txt
│   │   ├── EnvironmentAgent.prompt.txt
│   │   ├── KnowledgeBaseAgent.prompt.txt
│   │   └── ConfigurationAgent.prompt.txt
│   │
│   ├── Compliance/                             # Compliance Agent (12 tools)
│   │   ├── Agents/ComplianceAgent.cs
│   │   ├── Tools/
│   │   └── Services/Engines/
│   │
│   ├── Infrastructure/                         # Infrastructure Agent (6 tools)
│   ├── CostManagement/                         # Cost Agent (6 tools)
│   ├── Discovery/                              # Discovery Agent (9 tools)
│   ├── Environment/                            # Environment Agent (10 tools)
│   ├── KnowledgeBase/                          # Knowledge Base Agent (8 tools)
│   └── Configuration/                          # Configuration Agent (1 tool)
│
├── Platform.Engineering.Copilot.Core/          # Shared models, interfaces
│   ├── Interfaces/
│   ├── Models/
│   └── Data/
│
├── Platform.Engineering.Copilot.Chat/          # Web Chat UI (5001)
└── Platform.Engineering.Copilot.Admin.API/     # Admin API (5000)
```

---

## Agent Catalog

| Agent | ID | Tools | Primary Capability |
|-------|-----|-------|-------------------|
| **Compliance** | `compliance` | 12 | NIST 800-53 scanning, remediation, DFC integration |
| **Infrastructure** | `infrastructure` | 6 | Azure provisioning, IaC generation (Bicep/Terraform) |
| **Cost** | `cost-management` | 6 | Cost analysis, optimization, budget tracking |
| **Discovery** | `discovery` | 9 | Resource inventory, health monitoring |
| **Environment** | `environment` | 10 | Environment lifecycle, drift detection |
| **Knowledge Base** | `knowledgebase` | 8 | NIST/STIG/RMF compliance education |
| **Configuration** | `configuration` | 1 | Subscription configuration |

See [AGENTS.md](./AGENTS.md) for complete tool reference.

---

## Request Flow

```
User: "Check NIST compliance for my subscription"
                    ↓
┌─────────────────────────────────────────────────────────────────┐
│ PlatformAgentGroupChat.InvokeAsync()                            │
│   └─ PlatformSelectionStrategy.SelectAgentAsync()               │
│       └─ Fast-path: "compliance" + "NIST" → Compliance Agent    │
└─────────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────────┐
│ ComplianceAgent.ProcessAsync(context)                           │
│   ├─ Load prompt from ComplianceAgent.prompt.txt                │
│   ├─ Include RegisteredTools as AITools                         │
│   └─ ChatClient.GetResponseAsync(messages, tools)               │
└─────────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────────┐
│ LLM selects: run_compliance_assessment                          │
│   Arguments: { subscription_id: "..." }                         │
└─────────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────────┐
│ ComplianceAssessmentTool.ExecuteAsync()                         │
│   ├─ Call AtoComplianceEngine.RunAssessmentAsync()              │
│   ├─ Scan Azure resources via Resource Graph                    │
│   ├─ Integrate DFC findings for RA/CA families                  │
│   └─ Return JSON with findings, scores, recommendations         │
└─────────────────────────────────────────────────────────────────┘
```

---

## Fast-Path Agent Selection

The `PlatformSelectionStrategy` uses keyword matching for instant routing:

```csharp
// Compliance patterns
if (message.ContainsAny("compliance", "nist", "fedramp", "assessment", "remediation"))
    return ComplianceAgent;

// Infrastructure patterns
if (message.ContainsAny("create", "deploy", "provision", "terraform", "bicep"))
    return InfrastructureAgent;

// Cost patterns
if (message.ContainsAny("cost", "spending", "budget", "forecast", "optimization"))
    return CostManagementAgent;

// Discovery patterns
if (message.ContainsAny("discover", "list", "inventory", "health", "resources"))
    return DiscoveryAgent;

// Environment patterns
if (message.ContainsAny("environment", "template", "clone", "scale", "drift"))
    return EnvironmentAgent;

// Knowledge patterns
if (message.ContainsAny("explain", "what is", "stig", "rmf", "impact level"))
    return KnowledgeBaseAgent;
```

---

## Configuration

All configuration in `appsettings.json`:

```json
{
  "AgentConfiguration": {
    "ComplianceAgent": {
      "Enabled": true,
      "Temperature": 0.2,
      "MaxTokens": 4000,
      "DefenderForCloud": {
        "Enabled": true,
        "IncludeSecureScore": true,
        "MapToNistControls": true
      }
    },
    "InfrastructureAgent": {
      "Enabled": true,
      "Temperature": 0.4,
      "DefaultRegion": "usgovvirginia"
    },
    "CostManagementAgent": {
      "Enabled": true,
      "Temperature": 0.3
    },
    "DiscoveryAgent": {
      "Enabled": true,
      "Temperature": 0.3
    },
    "EnvironmentAgent": {
      "Enabled": true,
      "Temperature": 0.3
    },
    "KnowledgeBaseAgent": {
      "Enabled": true,
      "Temperature": 0.2
    },
    "ConfigurationAgent": {
      "Enabled": true,
      "Temperature": 0.2
    }
  }
}
```

---

## Adding a New Agent

1. **Create agent folder**: `src/Platform.Engineering.Copilot.Agents/{Name}/`

2. **Create prompt file**: `src/Platform.Engineering.Copilot.Agents/Prompts/{Name}Agent.prompt.txt`
   ```
   You are {{agentName}} for the Platform Engineering Copilot...
   
   ## Available Tools
   - tool_name: Description of when to use
   ```

3. **Create agent class** extending `BaseAgent`:
   ```csharp
   public class MyAgent : BaseAgent
   {
       public override string AgentId => "my-agent";
       public override string AgentName => "My Agent";
       
       protected override string GetSystemPrompt()
       {
           var template = SystemPromptLoader.LoadFromType<MyAgent>("MyAgent.prompt.txt");
           return SystemPromptLoader.ApplyVariables(template ?? "", variables);
       }
   }
   ```

4. **Create tools** extending `BaseTool`:
   ```csharp
   public class MyTool : BaseTool
   {
       public override string Name => "my_tool";
       public override string Description => "What this tool does";
   }
   ```

5. **Register in DI** (`ServiceCollectionExtensions.cs`):
   ```csharp
   services.AddScoped<MyTool>();
   services.AddScoped<MyAgent>();
   ```

6. **Update .csproj** to embed prompt:
   ```xml
   <EmbeddedResource Include="Prompts\*.prompt.txt" />
   ```

---

## Technology Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 9.0 |
| AI Framework | Microsoft Semantic Kernel 1.26+ |
| AI Abstractions | Microsoft.Extensions.AI 9.1.0 |
| MCP | ModelContextProtocol 0.4.0-preview |
| Azure SDK | Azure.ResourceManager.* |
| Database | SQLite (default) / SQL Server |
| Cache | IMemoryCache |
| Container | Docker, ACI, AKS |

---

## Related Documentation

- [AGENTS.md](./AGENTS.md) - Detailed agent capabilities and tools
- [DEPLOYMENT.md](./DEPLOYMENT.md) - Docker, ACI, AKS deployment
- [GETTING-STARTED.md](./GETTING-STARTED.md) - Quick start guide
- [DEVELOPMENT.md](./DEVELOPMENT.md) - Development workflow
