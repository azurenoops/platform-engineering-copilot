# Platform Engineering Copilot - Architecture

**Version:** 3.1  
**Last Updated:** January 2026

---

## Overview

The Platform Engineering Copilot is an AI-powered infrastructure and platform management system built on .NET 9.0. The system uses a **BaseAgent/BaseTool pattern** with specialized AI agents coordinated through a Model Context Protocol (MCP) server.

### Key Characteristics

- **BaseAgent/BaseTool Pattern**: All agents extend `BaseAgent`, all tools extend `BaseTool`
- **MCP Server**: Dual-mode operation (HTTP:5100 + stdio for AI clients)
- **Multi-Agent Orchestration**: 7 specialized agents
- **Azure Government**: Primary target for government cloud workloads

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
│  │  │Infrastructure│ │  Security   │ │    Cost     │           │ │
│  │  │   Agent (6) │ │  Agent (6)  │ │  Agent (6)  │           │ │
│  │  └─────────────┘ └─────────────┘ └─────────────┘           │ │
│  │  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐           │ │
│  │  │  Discovery  │ │ Environment│ │ Knowledge   │           │ │
│  │  │   Agent (9) │ │ Agent (10) │ │ Base (0)    │           │ │
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
│    ├─ Name: string (e.g., "list_azure_resources")            │
│    ├─ Description: string (shown to LLM for selection)            │
│    ├─ Parameters: List<ToolParameter>                             │
│    ├─ ExecuteAsync(arguments) → string (JSON result)              │
│    └─ AsAITool() → AITool (for LLM function calling)              │
└──────────────────────────────────────────────────────────────────┘
```

### BaseAgent Implementation

```csharp
public class InfrastructureAgent : BaseAgent
{
    public override string AgentId => "infrastructure";
    public override string AgentName => "Infrastructure Agent";
    public override string Description => "Azure provisioning, IaC generation (Bicep/Terraform)";

    public InfrastructureAgent(
        IChatClient chatClient,
        ILogger<InfrastructureAgent> logger,
        GenerateBicepTool bicepTool,
        GenerateTerraformTool terraformTool,
        DeployResourceTool deployTool
    ) : base(chatClient, logger)
    {
        RegisterTool(bicepTool);
        RegisterTool(terraformTool);
        RegisterTool(deployTool);
    }

    protected override string GetSystemPrompt()
    {
        var template = SystemPromptLoader.LoadFromType<InfrastructureAgent>("InfrastructureAgent.prompt.txt");
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
public class GenerateBicepTool : BaseTool
{
    public override string Name => "generate_bicep_template";
    
    public override string Description =>
        "Generate Bicep infrastructure-as-code template for Azure resources. " +
        "Use when user asks: 'create template', 'generate bicep', 'provision resources'";

    public GenerateBicepTool(
        ILogger<GenerateBicepTool> logger) : base(logger)
    {
        Parameters.Add(new ToolParameter("resource_type", "Azure resource type", true));
        Parameters.Add(new ToolParameter("region", "Azure region", false));
        Parameters.Add(new ToolParameter("resource_group", "Target resource group", false));
    }

    public override async Task<string> ExecuteAsync(
        IDictionary<string, object?> arguments,
        CancellationToken cancellationToken = default)
    {
        var resourceType = GetRequiredString(arguments, "resource_type");
        var template = await GenerateTemplate(resourceType);
        return ToJson(new { success = true, template });
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
│   │   ├── InfrastructureAgent.prompt.txt
│   │   ├── CostManagementAgent.prompt.txt
│   │   ├── DiscoveryAgent.prompt.txt
│   │   ├── EnvironmentAgent.prompt.txt
│   │   ├── KnowledgeBaseAgent.prompt.txt
│   │   └── ConfigurationAgent.prompt.txt
│   │
│   ├── Infrastructure/                         # Infrastructure Agent (6 tools)
│   ├── CostManagement/                         # Cost Agent (6 tools)
│   ├── Security/                               # Security Agent
│   ├── Discovery/                              # Discovery Agent (9 tools)
│   ├── Environment/                            # Environment Agent (10 tools)
│   ├── KnowledgeBase/                          # Knowledge Base Agent (shell — no tools)
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
| **Infrastructure** | `infrastructure` | 6 | Azure provisioning, IaC generation (Bicep/Terraform) |
| **Security** | `security` | 6 | Security posture management, Defender for Cloud |
| **Cost** | `cost-management` | 6 | Cost analysis, optimization, budget tracking |
| **Discovery** | `discovery` | 9 | Resource inventory, health monitoring |
| **Environment** | `environment` | 10 | Environment lifecycle, drift detection |
| **Knowledge Base** | `knowledgebase` | 0 | Platform knowledge and documentation (shell — future MCP integration) |
| **Configuration** | `configuration` | 1 | Subscription configuration |

See [AGENTS.md](./AGENTS.md) for complete tool reference.

---

## Request Flow

```
User: "Generate a Bicep template for my storage account"
                    ↓
┌─────────────────────────────────────────────────────────────────┐
│ PlatformAgentGroupChat.InvokeAsync()                            │
│   └─ PlatformSelectionStrategy.SelectAgentAsync()               │
│       └─ Fast-path: "bicep" + "template" → Infrastructure Agent    │
└─────────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────────┐
│ InfrastructureAgent.ProcessAsync(context)                      │
│   ├─ Load prompt from InfrastructureAgent.prompt.txt             │
│   ├─ Include RegisteredTools as AITools                         │
│   └─ ChatClient.GetResponseAsync(messages, tools)               │
└─────────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────────┐
│ LLM selects: generate_bicep_template                            │
│   Arguments: { resource_type: "storage", region: "usgovva" }    │
└─────────────────────────────────────────────────────────────────┘
                    ↓
┌─────────────────────────────────────────────────────────────────┐
│ GenerateBicepTool.ExecuteAsync()                                │
│   ├─ Generate Bicep template for requested resource             │
│   ├─ Apply region and resource group settings                   │
│   └─ Return JSON with template content                          │
└─────────────────────────────────────────────────────────────────┘
```

---

## Fast-Path Agent Selection

The `PlatformSelectionStrategy` uses keyword matching for instant routing:

```csharp
// Infrastructure patterns
if (message.ContainsAny("create", "deploy", "provision", "terraform", "bicep"))
    return InfrastructureAgent;

// Security patterns
if (message.ContainsAny("secure", "score", "defender", "security", "vulnerability"))
    return SecurityAgent;

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
if (message.ContainsAny("knowledge", "documentation", "platform", "help", "guide"))
    return KnowledgeBaseAgent;
```

---

## Configuration

All configuration in `appsettings.json`:

```json
{
  "AgentConfiguration": {
    "InfrastructureAgent": {
      "Enabled": true,
      "Temperature": 0.4,
      "DefaultRegion": "usgovvirginia"
    },
    "SecurityAgent": {
      "Enabled": true,
      "Temperature": 0.3
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
| AI Abstractions | Microsoft.Extensions.AI 10.3.0 |
| Azure OpenAI | Azure.AI.OpenAI 2.1.0, Microsoft.Extensions.AI.OpenAI 10.3.0 |
| MCP | ModelContextProtocol 0.4.0-preview |
| Azure SDK | Azure.ResourceManager.* |
| Database | SQLite (default) / SQL Server |
| Cache | IMemoryCache |
| Container | Docker, ACI, AKS |

---

## Azure OpenAI Integration

### Overview

Agents can optionally process user messages through Azure OpenAI for natural-language understanding, tool selection, and response generation. When Azure OpenAI is not configured or the feature flag is disabled, agents fall back to direct tool execution (pre-feature behavior).

### Architecture

```
User Message
      │
      ▼
┌─────────────────────────────────────────────────┐
│ BaseAgent.ProcessMessageAsync()                  │
│                                                  │
│  ┌─ AI Enabled? ─────────────────────────────┐  │
│  │ YES: ExecuteAIPipeline                     │  │
│  │  1. BuildChatMessages (system + history)   │  │
│  │  2. BuildAITools (BaseTool → AIFunction)   │  │
│  │  3. FunctionInvokingChatClient             │  │
│  │     └─ Auto tool-call loop                 │  │
│  │     └─ MaximumIterationsPerRequest         │  │
│  │  4. GetStreamingResponseAsync              │  │
│  │     └─ Token streaming via IProgress       │  │
│  │  5. Return natural-language text           │  │
│  ├────────────────────────────────────────────┤  │
│  │ NO: FallbackDirectToolExecution            │  │
│  │  1. Execute first registered tool          │  │
│  │  2. Return raw JSON (pre-feature behavior) │  │
│  └────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────┘
```

### Configuration

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<resource>.openai.azure.com/",
    "DeploymentName": "gpt-4o",
    "AgentAIEnabled": false,
    "MaxToolCallRounds": 5,
    "Temperature": 0.3
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `AgentAIEnabled` | `false` | Feature flag — `false` keeps pre-feature behavior |
| `MaxToolCallRounds` | `5` | Max LLM↔tool loop iterations (range 1–20) |
| `Temperature` | `0.3` | LLM response creativity (range 0.0–2.0) |

### Key Classes

| Class | Project | Purpose |
|-------|---------|---------|
| `AzureOpenAIOptions` | Core | Strongly-typed configuration with validation |
| `AzureOpenAIChatClientFactory` | Core | Creates `IChatClient` from config (null if unconfigured) |
| `ServiceCollectionExtensions` | Agents | Shared DI registration for both hosts |
| `BaseAgent.ProcessMessageAsync` | Core | AI pipeline with fallback |
| `BaseAgent.BuildChatMessages` | Core | Conversation context assembly |
| `BaseAgent.BuildAITools` | Core | Tool-to-AIFunction bridging |

### Authentication

- **API Key**: Set `AzureOpenAI:ApiKey` in configuration
- **Managed Identity**: Omit API key — uses `DefaultAzureCredential`
- **Azure Government**: Endpoints containing `.us` automatically use `AzureOpenAIAudience.AzureGovernment`

### Graceful Degradation

The system operates identically to pre-feature behavior when:
- `AgentAIEnabled` is `false` (default)
- `AzureOpenAI:Endpoint` is not configured
- `IChatClient` is null (no Azure OpenAI resource available)
- LLM throws a runtime exception (falls back to direct tool execution)

---

## Related Documentation

- [AGENTS.md](./AGENTS.md) - Detailed agent capabilities and tools
- [DEPLOYMENT.md](./DEPLOYMENT.md) - Docker, ACI, AKS deployment
- [GETTING-STARTED.md](./GETTING-STARTED.md) - Quick start guide
- [DEVELOPMENT.md](./DEVELOPMENT.md) - Development workflow
