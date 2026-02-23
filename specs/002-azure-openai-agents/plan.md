# Implementation Plan: Add Azure OpenAI to Platform Copilot Agents

**Branch**: `002-azure-openai-agents` | **Date**: 2026-02-22 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-azure-openai-agents/spec.md`

## Summary

Add Azure OpenAI–powered intelligence to the 8 platform agents. Currently, agents are tool dispatchers: the orchestrator routes a message, the agent's first tool runs with empty parameters, and raw JSON goes back to the user. This feature wires up a real Azure OpenAI client via the existing `IChatClient` abstraction from `Microsoft.Extensions.AI`, injects it into `BaseAgent`, and adds a `ProcessMessageAsync` method that builds chat context from the agent's system prompt + conversation history, registers the agent's tools as LLM function definitions, calls the LLM, and handles a multi-round tool-call loop (max 5 rounds) before returning a natural-language response. The `ChatHub` is updated to call `ProcessMessageAsync` instead of brute-forcing the first tool. A feature flag (`AgentAIEnabled`, default `false`) gates the entire flow for safe rollout. All 625 existing tests must continue passing unchanged.

## Technical Context

**Language/Version**: C# 12 / .NET 9.0 (net9.0 TFM across all projects)
**Primary Dependencies**: `Microsoft.Extensions.AI` 9.1.0-preview.1.25064.3 → 10.3.0 upgrade (already in Core), `Azure.AI.OpenAI` 2.1.0 (to be added to Core), `Microsoft.Extensions.AI.OpenAI` 10.3.0 (to be added to Core — bridge for `.AsIChatClient()`), `Microsoft.SemanticKernel` 1.26.0 (existing), `Microsoft.AspNetCore.SignalR` 1.1.0 (Chat project)
**Storage**: N/A for this feature — conversation sessions remain in-memory (`ConversationSession` with `List<SessionMessage>`)
**Testing**: xUnit 2.9.2 + FluentAssertions 7.0.0 + Moq 4.20.72; `dotnet test` across Tests.Unit and Tests.Integration
**Target Platform**: Linux containers (Docker) on Azure Government (IL5/IL6) and Azure Commercial
**Project Type**: Multi-project web service (MCP server + Chat/SignalR server + class libraries)
**Performance Goals**: First streaming token within 2 seconds of user message; 95% of AI responses complete within 15 seconds end-to-end
**Constraints**: System must function fully without Azure OpenAI configured (fallback mode); `Azure.AI.OpenAI` in Core only; `IChatClient` always nullable; 625 tests must continue passing
**Scale/Scope**: 8 agents, 59 tools, 9 system prompt files, 2 host entry points (Mcp + Chat), ~625 existing tests

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Principle | Requirement | Status | Notes |
|---|-----------|-------------|--------|-------|
| I | Documentation as Source of Truth | Changes follow `/docs/` guidance; cite doc paths | PASS | Existing `docs/ARCHITECTURE.md`, `docs/AGENTS.md`, `docs/DEVELOPMENT.md` cover agent/tool patterns. New feature updates those docs. |
| II | BaseAgent/BaseTool Architecture | All agents extend `BaseAgent`; all tools extend `BaseTool`; prompts in `*.prompt.txt`; tools via `RegisterTool()` | PASS | Feature extends `BaseAgent` (adds `IChatClient?` + `ProcessMessageAsync`). Does NOT change `BaseTool`. Does NOT change tool registration pattern. Prompts stay in `*.prompt.txt` — only extended with new sections. |
| III | Test-First Development | Behavior changes include corresponding tests; 80%+ coverage target | PASS | 8+ new test scenarios defined (FR-027). All 625 existing tests preserved (FR-026). |
| IV | Azure Government & Compliance First | Support `AzureUSGovernment`; use `DefaultAzureCredential`; no hardcoded credentials; Key Vault for secrets | PASS | Factory supports Azure Gov endpoints (`.us` suffixes). API key read from config (injected via env vars / Key Vault in deployment). No hardcoded credentials. |
| V | Observability & Structured Logging | Serilog; log tool executions, agent invocations, durations | PASS | `ProcessMessageAsync` will log: LLM calls, tool-call rounds, tool execution duration, final response generation. Uses existing `ILogger` injection pattern. |

**Gate result: PASS** — no violations. Proceeding to Phase 0.

### Post-Design Re-Check (after Phase 1)

| # | Principle | Status | Post-Design Notes |
|---|-----------|--------|-------------------|
| I | Documentation as Source of Truth | PASS | Design references existing `docs/ARCHITECTURE.md` BaseAgent/BaseTool patterns. Quickstart provides step-by-step implementation order. |
| II | BaseAgent/BaseTool Architecture | PASS | `BaseAgent` extended (not replaced). `BaseTool` unchanged. Prompts in `*.prompt.txt` — appended only. `RegisterTool()` pattern preserved. |
| III | Test-First Development | PASS | 7 implementation steps each with test verification. 8+ new test scenarios. 625 existing tests preserved. |
| IV | Azure Government & Compliance First | PASS | `AzureOpenAIChatClientFactory` detects `.us` endpoints → `AzureOpenAIAudience.AzureGovernment`. `DefaultAzureCredential` supported. No hardcoded credentials. |
| V | Observability & Structured Logging | PASS | `ProcessMessageAsync` logs at each phase — LLM call, tool execution, round count, response generation. Uses existing `ILogger`. |

**Post-design gate: PASS** — no violations introduced by the design.

## Project Structure

### Documentation (this feature)

```text
specs/002-azure-openai-agents/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── process-message-contract.md
└── tasks.md             # Phase 2 output (NOT created by /speckit.plan)
```

### Source Code (repository root)

```text
src/
├── Platform.Engineering.Copilot.Core/
│   ├── Agents/
│   │   ├── BaseAgent.cs                    # MODIFIED: +IChatClient?, +ProcessMessageAsync
│   │   └── PlatformOrchestrator.cs         # UNCHANGED (routing stays the same)
│   ├── Extensions/
│   │   └── ServiceCollectionExtensions.cs  # NEW: shared AddPlatformCopilotServices DI method
│   ├── Services/
│   │   └── AzureOpenAIChatClientFactory.cs # NEW: constructs IChatClient from config
│   └── Platform.Engineering.Copilot.Core.csproj  # MODIFIED: +Azure.AI.OpenAI, +Microsoft.Extensions.AI.OpenAI packages
│
├── Platform.Engineering.Copilot.Agents/
│   ├── Compliance/
│   │   ├── ComplianceAgent.cs              # MODIFIED: +IChatClient? constructor param
│   │   └── compliance.prompt.txt           # MODIFIED: +Response Guidelines, +Tool Selection
│   ├── Configuration/                      # Same pattern for all 8 agents...
│   ├── CostManagement/
│   ├── Discovery/
│   ├── Environment/
│   ├── Infrastructure/
│   ├── KnowledgeBase/
│   ├── Orchestrator/
│   └── Security/
│
├── Platform.Engineering.Copilot.Mcp/
│   ├── Program.cs                          # MODIFIED: +IChatClient DI registration
│   └── appsettings.json                    # MODIFIED: +AgentAIEnabled, +MaxToolCallRounds, +Temperature
│
└── Platform.Engineering.Copilot.Chat/
    ├── Hubs/ChatHub.cs                     # MODIFIED: use ProcessMessageAsync
    ├── Program.cs                          # MODIFIED: +IChatClient DI, +agent registrations
    └── appsettings.json                    # MODIFIED: +AzureOpenAI section

tests/
├── Platform.Engineering.Copilot.Tests.Unit/
│   └── Agents/
│       ├── BaseAgentAITests.cs             # NEW: ProcessMessageAsync unit tests
│       └── AzureOpenAIChatClientFactoryTests.cs  # NEW: factory tests
│
└── Platform.Engineering.Copilot.Tests.Integration/
    ├── Chat/ChatHubAIIntegrationTests.cs   # NEW: ChatHub with AI flow
    └── MockChatClientFactory.cs            # MODIFIED: +tool-call simulation methods
```

**Structure Decision**: No new projects. All changes fit within the existing multi-project structure. `AzureOpenAIChatClientFactory` goes in `Core/Services/` alongside existing service classes (`NistService`, `AzureErrorHandler`, `KeyVaultSecretProvider`). New tests go in the existing test projects under appropriate subdirectories.

## Complexity Tracking

No constitution violations — this section is empty. The feature adds no new projects, no new architectural patterns, and no new infrastructure abstractions beyond what's already established.
