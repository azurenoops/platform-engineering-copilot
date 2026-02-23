# Data Model: Add Azure OpenAI to Platform Copilot Agents

**Feature**: `002-azure-openai-agents`
**Date**: 2026-02-22

## Entities

### AzureOpenAIOptions (NEW)

Configuration options for Azure OpenAI integration, bound from the `AzureOpenAI` configuration section.

| Field | Type | Default | Validation | Description |
|-------|------|---------|------------|-------------|
| `Endpoint` | `string` | `""` | URI format when non-empty | Azure OpenAI resource endpoint URL |
| `ApiKey` | `string` | `""` | — | API key for authentication (alternative to managed identity) |
| `DeploymentName` | `string` | `"gpt-4o"` | Non-empty when endpoint is set | Model deployment name |
| `ModelId` | `string` | `"gpt-4o"` | — | Model identifier for display/logging |
| `AgentAIEnabled` | `bool` | `false` | — | Feature flag: enable LLM-powered agent processing |
| `MaxToolCallRounds` | `int` | `5` | Range: 1–20 | Maximum tool-call loop iterations per request |
| `Temperature` | `float` | `0.3f` | Range: 0.0–2.0 | LLM response temperature |

**Relationships**: Read by `AzureOpenAIChatClientFactory` to construct the AI client. Read by `BaseAgent.ProcessMessageAsync` for `AgentAIEnabled`, `MaxToolCallRounds`, and `Temperature`.

**State Transitions**: None — immutable configuration. Reloaded from config on application restart.

---

### BaseAgent (MODIFIED)

Extended with optional AI client and message processing capability.

| Field | Type | Change | Description |
|-------|------|--------|-------------|
| `AgentId` | `string` | Unchanged | Unique agent identifier |
| `AgentName` | `string` | Unchanged | Human-friendly display name |
| `Description` | `string` | Unchanged | Short description for LLM intent classification |
| `Keywords` | `IReadOnlyList<string>` | Unchanged | Keywords for fast-path routing |
| `RequiredPimTier` | `PimTier` | Unchanged | Minimum PIM tier |
| `Logger` | `ILogger` | Unchanged | Structured logger |
| `_tools` | `List<BaseTool>` | Unchanged | Registered tools |
| `_chatClient` | `IChatClient?` | **NEW** | Optional AI chat client (nullable) |
| `_options` | `IOptions<AzureOpenAIOptions>` | **NEW** | Configuration options (injected via DI or default) |

**New Methods**:

| Method | Signature | Description |
|--------|-----------|-------------|
| `ProcessMessageAsync` | `(string userMessage, IReadOnlyList<SessionMessage> history, IProgress<ProgressUpdate>? progress, CancellationToken ct)` → `Task<string>` | Main AI processing pipeline |
| `BuildAITools` | `()` → `IList<AITool>` (private) | Converts registered `BaseTool` instances to `AIFunction` objects |
| `BuildChatMessages` | `(string userMessage, IReadOnlyList<SessionMessage> history)` → `List<ChatMessage>` (private) | Constructs LLM message sequence |

**Relationships**: 
- Contains 0..N `BaseTool` instances (unchanged)
- Optionally holds `IChatClient` (new)
- Reads `AzureOpenAIOptions` (new)

**State Transitions for ProcessMessageAsync**:

```
Start
  ├── IChatClient is null OR AgentAIEnabled is false
  │     → Fallback: Execute first tool directly → Return raw result
  │
  └── IChatClient available AND AgentAIEnabled is true
        → Build system prompt + history + user message
        → Build AITool definitions from registered tools
        → Configure ChatOptions (tools, temperature)
        → Call GetStreamingResponseAsync
        → [FunctionInvokingChatClient handles tool-call loop automatically]
        → Stream text tokens via IProgress<ProgressUpdate>
        → Return complete response text
```

---

### SessionMessage (EXISTING — behavioral change only)

No structural changes. Behavioral change: `Content` for assistant-role messages must store AI-generated natural-language text, not raw tool JSON output.

| Field | Type | Change | Description |
|-------|------|--------|-------------|
| `Role` | `string` | Unchanged | "User", "Assistant", or "System" |
| `Content` | `string` | **Behavioral** | Now stores AI response text (not raw JSON) for assistant messages |
| `Timestamp` | `DateTimeOffset` | Unchanged | Message timestamp |
| `CorrelationId` | `string` | Unchanged | Request correlation ID |
| `AgentId` | `string?` | Unchanged | Optional originating agent ID |

---

### AzureOpenAIChatClientFactory (NEW)

Factory service responsible for constructing an `IChatClient` from configuration.

| Field | Type | Description |
|-------|------|-------------|
| `_options` | `AzureOpenAIOptions` | Injected configuration |
| `_logger` | `ILogger` | For logging factory decisions |

**Methods**:

| Method | Signature | Description |
|--------|-----------|-------------|
| `CreateChatClient` | `()` → `IChatClient?` | Returns configured client or null |

**Validation Rules**:
- If `Endpoint` is empty/null → return null (fallback mode)
- If `Endpoint` contains `.us` or `.azure.us` → set `AzureOpenAIAudience.AzureGovernment`
- If `ApiKey` is non-empty → use `ApiKeyCredential` auth
- If `ApiKey` is empty → use `DefaultAzureCredential` (managed identity)

**Relationships**: Uses `AzureOpenAIOptions`. Produces `IChatClient` for DI container.

---

### ChatHub (EXISTING — method change)

| Method | Change | Description |
|--------|--------|-------------|
| `SendMessage` | **MODIFIED** | Calls `agent.ProcessMessageAsync()` instead of `agent.ExecuteToolAsync()` on first tool |

**Data Flow Change**:

Before:
```
User Message → RouteAsync → agent.GetToolMetadata().First().Name → ExecuteToolAsync(empty params) → raw JSON → StreamToken
```

After:
```
User Message → RouteAsync → agent.ProcessMessageAsync(message, session.Messages, progress, ct) → AI response → StreamToken
```

---

## Entity Relationships

```
AzureOpenAIOptions ──reads──→ AzureOpenAIChatClientFactory ──produces──→ IChatClient
                                                                              │
                     ┌────────────────────────────────────────────────────────┘
                     ▼
                BaseAgent (holds IChatClient?)
                     │
                     ├── ProcessMessageAsync
                     │     ├── BuildChatMessages (system prompt + history + user msg)
                     │     ├── BuildAITools (BaseTool → AIFunction)
                     │     └── GetStreamingResponseAsync (via FunctionInvokingChatClient)
                     │           └── [auto tool-call loop] → ExecuteToolAsync → tool result → LLM
                     │
                     └── 8 Concrete Agents (ComplianceAgent, DiscoveryAgent, etc.)
                           └── Each passes IChatClient? to base()

ChatHub.SendMessage → PlatformOrchestrator.RouteAsync → BaseAgent.ProcessMessageAsync
                                                              │
                                                              ▼
                                                       ConversationSession
                                                       (stores AI responses)
```
