# Research: Add Azure OpenAI to Platform Copilot Agents

**Feature**: `002-azure-openai-agents`
**Date**: 2026-02-22
**Status**: Complete

## Research Task 1: Azure.AI.OpenAI Package & IChatClient Bridging

### Decision: Use `Azure.AI.OpenAI` 2.1.0 + `Microsoft.Extensions.AI.OpenAI` 10.3.0

### Rationale

The `Azure.AI.OpenAI` NuGet package (stable 2.1.0) provides `AzureOpenAIClient` but does **not** directly implement `IChatClient` from `Microsoft.Extensions.AI`. A bridge package — `Microsoft.Extensions.AI.OpenAI` (stable 10.3.0) — provides the `.AsIChatClient()` extension method on the OpenAI `ChatClient` object.

The wiring pattern is:
1. `AzureOpenAIClient` → `azureClient.GetChatClient(deploymentName)` → `chatClient.AsIChatClient()`
2. Both packages go in `Core.csproj` only

### Alternatives Considered

| Alternative | Rejected Because |
|-------------|-----------------|
| Use `Azure.AI.OpenAI` alone without bridge | Does not implement `IChatClient`; would require custom adapter code |
| Use `OpenAI` package directly (non-Azure) | No Azure Government support; no managed identity auth |
| Build custom `IChatClient` wrapper | Unnecessary when official bridge package exists |
| Use Semantic Kernel's built-in Azure OpenAI | Over-abstraction; SK is already referenced but for future use cases. `IChatClient` is the right abstraction level per existing orchestrator pattern |

### Azure Government Support

`AzureOpenAIClient` natively supports Azure Government via `AzureOpenAIClientOptions.Audience`:
- `AzureOpenAIAudience.AzureGovernment` — sets auth scope to `https://cognitiveservices.azure.us/.default`
- Detection: check if the endpoint URI contains `.us` or `.azure.us`
- Constructor accepts `DefaultAzureCredential` for managed identity (prod) or API key via `ApiKeyCredential` from `System.ClientModel`

### Constructor Pattern

```
API Key: new AzureOpenAIClient(new Uri(endpoint), new ApiKeyCredential(apiKey))
Managed Identity: new AzureOpenAIClient(new Uri(endpoint), new DefaultAzureCredential(), options)
```

Note: `ApiKeyCredential` is from `System.ClientModel`, NOT `Azure.AzureKeyCredential`.

---

## Research Task 2: Microsoft.Extensions.AI Version — 9.x-preview vs 10.x Stable

### Decision: Upgrade `Microsoft.Extensions.AI` from 9.1.0-preview to 10.3.0 (stable)

### Rationale

The codebase currently references `Microsoft.Extensions.AI 9.1.0-preview.1.25064.3`. The stable release is 10.3.0 with breaking API changes. Staying on 9.x-preview is not viable because:

1. **`Microsoft.Extensions.AI.OpenAI` 10.3.0** (the bridge package) requires M.E.AI 10.x types. Version mismatch would cause compile errors.
2. Preview packages carry API instability risk; 10.x is the GA release.
3. The breaking changes are mechanical renames, not behavioral changes.

### Breaking Changes to Address

| 9.x-preview (current) | 10.x stable (target) | Files Affected |
|---|---|---|
| `CompleteAsync()` | `GetResponseAsync()` | `PlatformOrchestrator.cs` |
| `CompleteStreamingAsync()` | `GetStreamingResponseAsync()` | N/A (not used yet) |
| `ChatCompletion` | `ChatResponse` | `PlatformOrchestrator.cs`, `MockChatClientFactory.cs`, `OrchestratorTests.cs` |
| `StreamingChatCompletionUpdate` | `ChatResponseUpdate` | N/A (not used yet) |
| `response.Message.Text` | `response.Message.Text` | Unchanged — property name survived |

### Scope of Migration

- `PlatformOrchestrator.cs`: 1 call site (`CompleteAsync` → `GetResponseAsync`), 1 type ref (`ChatCompletion` → `ChatResponse`)
- `MockChatClientFactory.cs`: 4 factory methods return `ChatCompletion` → `ChatResponse`
- `OrchestratorTests.cs`: ~6 inline mock setups with `CompleteAsync` → `GetResponseAsync` and `ChatCompletion` → `ChatResponse`
- All other codebase: no changes needed

### Alternatives Considered

| Alternative | Rejected Because |
|-------------|-----------------|
| Stay on 9.x-preview | Incompatible with `Microsoft.Extensions.AI.OpenAI` 10.3.0 bridge; preview-quality API |
| Use adapter layer to wrap version differences | Over-engineering for mechanical renames |
| Wait for newer M.E.AI version | 10.3.0 is current stable; no benefit to waiting |

---

## Research Task 3: Function/Tool Calling Pattern

### Decision: Use `FunctionInvokingChatClient` via `.UseFunctionInvocation()` for automatic tool-call loop

### Rationale

`Microsoft.Extensions.AI` provides two approaches for handling LLM tool calls:

**Approach A: Automatic (chosen)** — Wrap the `IChatClient` with `FunctionInvokingChatClient` via:
```
IChatClient client = new ChatClientBuilder(innerClient)
    .UseFunctionInvocation()
    .Build();
```
This middleware intercepts tool-call responses, invokes the matching `AIFunction`, sends results back to the LLM, and repeats until a text response is produced. Works with both `GetResponseAsync` and `GetStreamingResponseAsync`.

**Approach B: Manual** — Check for `FunctionCallContent` in response messages, execute tools, append `FunctionResultContent`, call `GetResponseAsync` again. Requires explicit loop with max-round tracking.

Approach A is chosen because:
1. Less code — the loop, error handling, and round limiting are built into the middleware
2. Streaming support is automatic — tool calls in streaming mode are handled transparently
3. Standard pattern recommended by Microsoft
4. The `FunctionInvokingChatClient` has a `MaximumIterationsPerRequest` property for the max-rounds limit (FR-011)

### Tool Definition Pattern

Each `BaseTool` must be wrapped as an `AIFunction` for the `ChatOptions.Tools` list. Since `BaseTool` uses a JSON Schema string for `Parameters`, the recommended approach is:

1. Create a helper method on `BaseAgent` (e.g., `BuildAITools()`) that converts registered `BaseTool` instances into `AIFunction` objects
2. Each `AIFunction` wraps the tool's `Name`, `Description`, parameter schema (parsed from JSON), and an invocation delegate that calls `ExecuteToolAsync`
3. The `AIFunction` delegate receives arguments as a dictionary (matching `BaseTool.ExecuteAsync`'s `Dictionary<string, object?>`)

### Key Types

| Type | Purpose |
|------|---------|
| `AIFunction` (extends `AITool`) | Function definition with name, description, schema, and invocation delegate |
| `AIFunctionFactory.Create()` | Creates `AIFunction` from .NET method delegate |
| `FunctionCallContent` | In response messages — represents LLM's request to call a function |
| `FunctionResultContent` | In tool-response messages — represents the function's return value |
| `ChatOptions.Tools` | `IList<AITool>` — list of available tools for the LLM |
| `ChatOptions.Temperature` | `float?` — generation temperature |
| `FunctionInvokingChatClient` | Middleware that auto-handles the tool-call loop |

### Alternatives Considered

| Alternative | Rejected Because |
|-------------|-----------------|
| Manual tool-call loop in `ProcessMessageAsync` | More code, more error-prone, duplicates functionality already in `FunctionInvokingChatClient` |
| Semantic Kernel function calling | SK is referenced but would add unnecessary abstraction; `IChatClient` + `AIFunction` is simpler |
| Define tools as native .NET methods with `[Description]` attributes | Would require changing `BaseTool` contract; tools already have `Name`, `Description`, `Parameters` as properties |

---

## Research Task 4: Streaming Pattern

### Decision: Use `GetStreamingResponseAsync` with `FunctionInvokingChatClient` for real-time token streaming

### Rationale

`IChatClient.GetStreamingResponseAsync()` returns `IAsyncEnumerable<ChatResponseUpdate>`. When combined with `FunctionInvokingChatClient` (`.UseFunctionInvocation()`), tool calls are handled transparently mid-stream — only text updates reach the consumer.

Pattern for `ProcessMessageAsync` with streaming:
1. Build messages (system prompt + history + user message)
2. Set `ChatOptions` with tools and temperature
3. Call `GetStreamingResponseAsync(messages, options)`
4. `await foreach` the updates — stream each text fragment via `IProgress<ProgressUpdate>`
5. Accumulate updates for storing the complete response in session history

### Token Streaming to SignalR

The `ChatHub` already has `StreamToken` SignalR event. The `ProcessMessageAsync` progress callback maps directly:
- Each `ChatResponseUpdate` with text content → `StreamToken` event via `IProgress<ProgressUpdate>`
- Tool execution in progress → `ProgressUpdate` event with tool name and status

### Alternatives Considered

| Alternative | Rejected Because |
|-------------|-----------------|
| Non-streaming `GetResponseAsync` followed by fake token splitting | Current approach (split on spaces); poor UX with 5–15 second delay before any output |
| Server-Sent Events instead of SignalR | SignalR infrastructure already exists (`StreamToken`, `ProgressUpdate`); changing transport is out of scope |

---

## Research Task 5: Feature Flag & Configuration

### Decision: Use `IConfiguration` binding to a strongly-typed options class

### Rationale

The existing codebase reads `AzureOpenAI` config as a flat section in `appsettings.json`. The new settings (`AgentAIEnabled`, `MaxToolCallRounds`, `Temperature`) fit naturally alongside existing `Endpoint`, `ApiKey`, `DeploymentName`, `ModelId`.

### Configuration Shape

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "ApiKey": "",
    "DeploymentName": "gpt-4o",
    "ModelId": "gpt-4o",
    "AgentAIEnabled": false,
    "MaxToolCallRounds": 5,
    "Temperature": 0.3
  }
}
```

### Options Class

Create `AzureOpenAIOptions` in `Core/Configuration/` (or `Core/Services/`):
- `Endpoint` (string)
- `ApiKey` (string)
- `DeploymentName` (string)
- `ModelId` (string)
- `AgentAIEnabled` (bool, default `false`)
- `MaxToolCallRounds` (int, default `5`)
- `Temperature` (float, default `0.3f`)

Register via `services.Configure<AzureOpenAIOptions>(config.GetSection("AzureOpenAI"))`.

### Alternatives Considered

| Alternative | Rejected Because |
|-------------|-----------------|
| Inline `IConfiguration.GetValue<T>()` calls | Scattered access; no validation; harder to test |
| LaunchDarkly / Azure App Configuration feature flags | Over-engineering for a single boolean; adds external dependency |
| Environment variables only | Config already uses `appsettings.json` pattern; env vars can still override via .NET config layering |

---

## Research Task 6: Chat Program.cs Agent Registration Gap

### Decision: Share agent registration between MCP and Chat hosts via a shared `ServiceCollectionExtensions` method

### Rationale

The MCP `Program.cs` has a `ConfigureSharedServices()` method with full agent/tool DI registration. The Chat `Program.cs` registers only `PlatformOrchestrator` with a logger — no agents. The orchestrator in Chat has zero agents, making it non-functional for real routing.

### Approach

1. Extract agent/tool DI registration from MCP's `ConfigureSharedServices()` into a shared extension method in Core (e.g., `AddPlatformCopilotServices(this IServiceCollection)`)
2. Both MCP and Chat `Program.cs` call this shared method
3. The shared method also handles `IChatClient` registration from config

### Alternatives Considered

| Alternative | Rejected Because |
|-------------|-----------------|
| Duplicate agent registrations in Chat `Program.cs` | DRY violation; maintenance burden; divergence risk |
| Keep Chat with empty orchestrator | Chat would never work with AI; defeats the purpose of ChatHub integration |
| Move all DI to a shared project | Over-engineering; a single extension method in Core suffices |

---

## Research Task 7: BaseTool to AIFunction Conversion

### Decision: Add a `ToAIFunction()` method on `BaseAgent` that wraps `BaseTool` instances as `AIFunction` objects

### Rationale

`BaseTool.Parameters` is a JSON Schema string describing the tool's parameters. `AIFunction` needs:
- `Name` (string) — maps directly from `BaseTool.Name`
- `Description` (string) — maps directly from `BaseTool.Description`
- Parameter metadata — parsed from the JSON Schema string
- An invocation delegate — wraps `BaseTool.ExecuteAsync()`

### Approach

Create a private helper in `BaseAgent`:
```
private IList<AITool> BuildAITools()
```
For each registered `BaseTool`:
1. Parse `Parameters` JSON Schema to extract parameter names/types/descriptions
2. Create an `AIFunction` with a delegate that:
   - Receives arguments from the LLM
   - Maps them to `Dictionary<string, object?>`
   - Calls `ExecuteToolAsync(tool.Name, parameters, progress, cancellationToken)`
   - Returns the JSON string result
3. Return the list of `AITool` for `ChatOptions.Tools`

Using `AIFunctionFactory.Create()` with attributes is not suitable because tools are dynamically registered, not static methods.

### Alternatives Considered

| Alternative | Rejected Because |
|-------------|-----------------|
| Add `AsAITool()` method to `BaseTool` | Would couple `BaseTool` to `Microsoft.Extensions.AI` types; spec says agents depend on abstraction only |
| Use Semantic Kernel function registration | Over-abstraction; SK pipeline not needed for this feature |
| Static method delegates with `[Description]` | Tools are registered dynamically at runtime, not known at compile time |

---

## Summary of Resolved Items

| Item | Resolution |
|------|-----------|
| Package needed | `Azure.AI.OpenAI` 2.1.0 + `Microsoft.Extensions.AI.OpenAI` 10.3.0 (both in Core only) |
| M.E.AI version | Upgrade from 9.1.0-preview to 10.3.0 (stable); mechanical renames in ~3 files |
| IChatClient bridge | `chatClient.AsIChatClient()` extension method from bridge package |
| Azure Gov support | `AzureOpenAIAudience.AzureGovernment` + `.us` endpoint URI |
| Tool-call loop | `FunctionInvokingChatClient` via `.UseFunctionInvocation()` — automatic multi-round |
| Max rounds | `FunctionInvokingChatClient.MaximumIterationsPerRequest` property |
| Streaming | `GetStreamingResponseAsync` + `FunctionInvokingChatClient` = transparent tool calls during streaming |
| Temperature | `ChatOptions.Temperature = 0.3f` |
| Config | Strongly-typed `AzureOpenAIOptions` class; `IConfiguration` binding |
| Chat DI gap | Shared extension method for agent/tool/client registration |
| BaseTool → AIFunction | Dynamic `AIFunction` creation with delegate wrapping `ExecuteToolAsync` |
| Spec correction | Method is `.AsIChatClient()` not `.AsChatClient()`; needs 2 packages not 1 |
