# Quickstart: Add Azure OpenAI to Platform Copilot Agents

**Feature**: `002-azure-openai-agents`
**Date**: 2026-02-22

## Prerequisites

- .NET 9.0 SDK installed
- Repository cloned and on branch `002-azure-openai-agents`
- Solution builds cleanly: `dotnet build Platform.Engineering.Copilot.sln`
- All 625 existing tests pass: `dotnet test Platform.Engineering.Copilot.sln`
- (Optional) Azure OpenAI resource deployed with a GPT-4o model

## Implementation Order

This is the recommended sequence for implementing the feature. Each step is independently buildable and testable — the system remains functional at every step.

### Step 1: Package & Configuration Foundation

**Goal**: Add NuGet packages, options class, and config sections. No behavior changes.

1. Add to `Core.csproj`:
   - `Azure.AI.OpenAI` (2.1.0)
   - `Microsoft.Extensions.AI.OpenAI` (10.3.0)
   - Upgrade `Microsoft.Extensions.AI` from `9.1.0-preview.1.25064.3` to `10.3.0`

2. Create `AzureOpenAIOptions` class in `Core/Services/` or `Core/Configuration/`

3. Add config keys to `appsettings.json` in both Mcp and Chat:
   ```json
   "AzureOpenAI": {
     "AgentAIEnabled": false,
     "MaxToolCallRounds": 5,
     "Temperature": 0.3
   }
   ```

4. Fix M.E.AI 10.x breaking changes:
   - `PlatformOrchestrator.cs`: `CompleteAsync` → `GetResponseAsync`, `ChatCompletion` → `ChatResponse`
   - `MockChatClientFactory.cs`: Return `ChatResponse` instead of `ChatCompletion`
   - `OrchestratorTests.cs`: Update mock setups to use `GetResponseAsync` and `ChatResponse`

**Verify**: `dotnet build` passes. `dotnet test` — all 625 tests still pass.

### Step 2: Chat Client Factory

**Goal**: Factory that constructs `IChatClient` from config. DI registration.

1. Create `AzureOpenAIChatClientFactory` in `Core/Services/`
2. Register in both Mcp and Chat `Program.cs`
3. Write unit tests for factory (valid config → non-null, empty config → null, gov endpoint detection)

**Verify**: `dotnet test` — 625 existing + new factory tests pass.

### Step 3: BaseAgent AI Integration

**Goal**: Add `IChatClient?` to `BaseAgent`, implement `ProcessMessageAsync`.

1. Add `IChatClient?` and `AzureOpenAIOptions` to `BaseAgent` constructor (both optional/nullable with defaults)
2. Implement `ProcessMessageAsync` with:
   - AI-enabled mode: build messages → build tools → streaming LLM call with `FunctionInvokingChatClient`
   - Fallback mode: first-tool direct execution (current behavior)
3. Implement private helpers: `BuildChatMessages`, `BuildAITools`
4. Write unit tests for `ProcessMessageAsync` (mock LLM → tool call → text response; null client fallback; max rounds)

**Verify**: `dotnet test` — all existing BaseAgent tests still pass + new AI tests pass.

### Step 4: Concrete Agent Updates

**Goal**: All 8 agents accept `IChatClient?` in their constructors.

1. Update each agent constructor to accept `IChatClient? chatClient = null` and `AzureOpenAIOptions? options = null`
2. Pass to `base(logger, chatClient, options)`
3. Update DI registrations in both Mcp and Chat `Program.cs`
4. Verify existing agent tests still pass (they don't inject `IChatClient`, so they get null → fallback mode)

**Verify**: `dotnet test` — all 625 existing tests pass (agents default to fallback).

### Step 5: ChatHub Integration

**Goal**: `ChatHub.SendMessage` uses `ProcessMessageAsync` instead of brute-force tool execution.

1. Update `SendMessage` to call `agent.ProcessMessageAsync(message.Content, session.Messages, progress, ct)`
2. Wire streaming tokens from progress callback to `StreamToken` SignalR events
3. Store AI-generated response (not raw JSON) in session
4. Write integration tests for ChatHub AI flow

**Verify**: `dotnet test` — all tests pass. Manual test: send a message via SignalR, verify streaming response.

### Step 6: System Prompt Enhancement

**Goal**: Add "Response Guidelines" and "Tool Selection" sections to all 9 `.prompt.txt` files.

1. Append sections to each prompt file (do not modify existing content)
2. Verify prompts load correctly in tests

**Verify**: `dotnet test` — all tests pass. Verify prompt content in test output.

### Step 7: Chat Program.cs Agent Registration

**Goal**: Chat host has the same agent registrations as Mcp host.

1. Extract shared registration into `AddPlatformCopilotServices` extension method in Core
2. Both Mcp and Chat `Program.cs` call the shared method
3. Verify Chat host can route messages to agents

**Verify**: `dotnet test` — all tests pass. End-to-end: Chat host routes and processes messages.

## Verification Commands

```bash
# Build
dotnet build Platform.Engineering.Copilot.sln

# Run all tests
dotnet test Platform.Engineering.Copilot.sln

# Run only unit tests
dotnet test tests/Platform.Engineering.Copilot.Tests.Unit/

# Run only integration tests
dotnet test tests/Platform.Engineering.Copilot.Tests.Integration/

# Start MCP server (HTTP mode)
dotnet run --project src/Platform.Engineering.Copilot.Mcp/

# Start Chat server
dotnet run --project src/Platform.Engineering.Copilot.Chat/
```

## Configuration for Local Development

To enable AI features locally, set in `appsettings.Development.json`:

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://your-resource.openai.azure.com",
    "ApiKey": "your-key-here",
    "DeploymentName": "gpt-4o",
    "ModelId": "gpt-4o",
    "AgentAIEnabled": true,
    "MaxToolCallRounds": 5,
    "Temperature": 0.3
  }
}
```

Without these values, the system operates in fallback mode (current behavior — direct tool execution, raw JSON responses).

## Key Implementation Notes

1. **The `FunctionInvokingChatClient` middleware handles the tool-call loop** — you don't need to write a manual loop. Wrap the base `IChatClient` via `new ChatClientBuilder(client).UseFunctionInvocation().Build()`.

2. **`BaseTool.Parameters` is a JSON Schema string.** Parse it to create `AIFunction` parameter metadata, or use `AIFunctionFactory.Create()` with a wrapper delegate.

3. **`SessionMessage.Role` maps to `ChatRole`**: `"User"` → `ChatRole.User`, `"Assistant"` → `ChatRole.Assistant`, `"System"` → `ChatRole.System`.

4. **ChatHub has a local `ChatMessage` DTO** — don't confuse it with `Microsoft.Extensions.AI.ChatMessage`. Use fully-qualified names or aliases if needed.

5. **The Chat `Program.cs` currently has NO agent registrations** — the orchestrator there has zero agents. This must be fixed alongside or before the ChatHub integration (Step 5/7).
