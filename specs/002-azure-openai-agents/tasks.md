# Tasks: Add Azure OpenAI to Platform Copilot Agents

**Input**: Design documents from `/specs/002-azure-openai-agents/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/ ✅, quickstart.md ✅

**Tests**: Included — FR-027 explicitly requires 8+ test scenarios covering AI integration flow.

**Organization**: Tasks grouped by user story to enable independent implementation and testing. Six user stories at three priority levels (P1×2, P2×2, P3×2).

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Package & Configuration Foundation)

**Purpose**: Add NuGet packages, configuration class, and migrate M.E.AI 9.x→10.x breaking changes. No behavioral changes — system operates identically after this phase.

- [x] T001 Add NuGet packages to src/Platform.Engineering.Copilot.Core/Platform.Engineering.Copilot.Core.csproj: Azure.AI.OpenAI 2.1.0, Microsoft.Extensions.AI.OpenAI 10.3.0, upgrade Microsoft.Extensions.AI from 9.1.0-preview.1.25064.3 to 10.3.0
- [x] T002 [P] Create AzureOpenAIOptions strongly-typed config class (Endpoint, ApiKey, DeploymentName, ModelId, AgentAIEnabled=false, MaxToolCallRounds=5, Temperature=0.3f) with validation annotations (MaxToolCallRounds range 1–20, Temperature range 0.0–2.0, DeploymentName non-empty when Endpoint is set) in src/Platform.Engineering.Copilot.Core/Services/AzureOpenAIOptions.cs
- [x] T003 [P] Add AgentAIEnabled, MaxToolCallRounds, Temperature fields to AzureOpenAI config section in src/Platform.Engineering.Copilot.Mcp/appsettings.json and src/Platform.Engineering.Copilot.Chat/appsettings.json
- [x] T004 [P] Migrate PlatformOrchestrator to M.E.AI 10.x API (CompleteAsync → GetResponseAsync, ChatCompletion → ChatResponse) in src/Platform.Engineering.Copilot.Core/Agents/PlatformOrchestrator.cs
- [x] T005 [P] Migrate MockChatClientFactory to M.E.AI 10.x API (ChatCompletion → ChatResponse return types) in tests/Platform.Engineering.Copilot.Tests.Integration/MockChatClientFactory.cs
- [x] T006 Migrate OrchestratorTests to M.E.AI 10.x API (CompleteAsync → GetResponseAsync, ChatCompletion → ChatResponse in mock setups) in tests/Platform.Engineering.Copilot.Tests.Unit/Orchestrator/OrchestratorTests.cs

**Checkpoint**: `dotnet build` passes. `dotnet test` — all 625 existing tests still pass. No behavioral changes.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Chat client factory, shared DI registration, and service wiring. MUST complete before any user story work.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T007 Create AzureOpenAIChatClientFactory with endpoint/API-key/managed-identity auth, Azure Government detection (.us URI → AzureOpenAIAudience.AzureGovernment), and null-return for missing config in src/Platform.Engineering.Copilot.Core/Services/AzureOpenAIChatClientFactory.cs
- [x] T008 Write AzureOpenAIChatClientFactory unit tests (valid config → non-null client, empty endpoint → null, Gov endpoint detection, API key vs managed identity paths) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/AzureOpenAIChatClientFactoryTests.cs
- [x] T037 [P] Write AzureOpenAIOptions validation unit tests (MaxToolCallRounds outside 1–20 rejects, Temperature outside 0.0–2.0 rejects, empty DeploymentName with non-empty Endpoint rejects) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/AzureOpenAIChatClientFactoryTests.cs
- [x] T009 Create shared AddPlatformCopilotServices extension method (agent/tool DI, IChatClient registration, AzureOpenAIOptions binding) in src/Platform.Engineering.Copilot.Core/Extensions/ServiceCollectionExtensions.cs
- [x] T010 Update both Program.cs files to use AddPlatformCopilotServices for unified agent/client registration in src/Platform.Engineering.Copilot.Mcp/Program.cs and src/Platform.Engineering.Copilot.Chat/Program.cs

**Checkpoint**: Foundation ready. Factory tested. Both hosts register agents and IChatClient via shared method. `dotnet test` — all tests pass.

---

## Phase 3: User Story 1 — AI-Powered Conversational Agent Responses (Priority: P1) 🎯 MVP

**Goal**: Agents process messages through Azure OpenAI LLM — understanding intent, selecting tools, executing them, and returning natural-language responses instead of raw JSON.

**Independent Test**: Send a message to any agent with a mock IChatClient that returns a tool call followed by a text response — verify the tool executes and a natural-language response (not raw JSON) is returned.

**Covers**: FR-005 through FR-018, SC-001, SC-002

> **NOTE: Per Constitution III, write test stubs (T015, T018, T035) FIRST with expected method signatures — they will fail until implementation tasks (T011–T014, T016–T017) are complete.**

### Implementation for User Story 1

- [x] T011 [US1] Add optional IChatClient? and IOptions<AzureOpenAIOptions> parameters to BaseAgent constructor (nullable with defaults, backward-compatible) in src/Platform.Engineering.Copilot.Core/Agents/BaseAgent.cs
- [x] T012 [US1] Implement BuildChatMessages helper (system prompt via GetSystemPrompt() → ChatRole.System, SessionMessage history → ChatRole.User/Assistant, current user message → ChatRole.User) in src/Platform.Engineering.Copilot.Core/Agents/BaseAgent.cs
- [x] T013 [US1] Implement BuildAITools helper (each registered BaseTool → AIFunction with Name, Description, parameter schema from JSON, delegate wrapping ExecuteToolAsync) in src/Platform.Engineering.Copilot.Core/Agents/BaseAgent.cs
- [x] T014 [US1] Implement ProcessMessageAsync with FunctionInvokingChatClient: AI-enabled mode (build messages → build tools → ChatOptions with temperature → GetStreamingResponseAsync → stream tokens via IProgress → return text), fallback mode (null client or AgentAIEnabled=false → first tool direct execution → raw JSON), LLM-failure recovery (catch LLM exceptions → log → fall back to direct tool execution), and structured logging per Constitution V (log LLM calls, tool-call rounds, tool execution duration, termination reason) in src/Platform.Engineering.Copilot.Core/Agents/BaseAgent.cs
- [x] T015 [P] [US1] Write BaseAgentAITests: single tool call via mock LLM returns natural-language text response, tool result sent back to LLM before final answer in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/BaseAgentAITests.cs
- [x] T016 [US1] Update all 8 concrete agent constructors to accept optional IChatClient? and IOptions<AzureOpenAIOptions> and forward to base() in src/Platform.Engineering.Copilot.Agents/ (ComplianceAgent, InfrastructureAgent, CostManagementAgent, DiscoveryAgent, EnvironmentAgent, KnowledgeBaseAgent, ConfigurationAgent, SecurityAgent)
- [x] T017 [US1] Update ChatHub.SendMessage to call agent.ProcessMessageAsync(message.Content, session.Messages, progress, cancellationToken) instead of ExecuteToolAsync on first tool, and store AI response text in SessionMessage.Content in src/Platform.Engineering.Copilot.Chat/Hubs/ChatHub.cs
- [x] T018 [US1] Write ChatHub AI integration tests (message routing → AI agent response flow, AI response stored in session) in tests/Platform.Engineering.Copilot.Tests.Integration/Chat/ChatHubAIIntegrationTests.cs
- [x] T035 [P] [US1] Write empty-LLM-response edge case test (LLM returns no text and no tool calls → ProcessMessageAsync returns empty string or explanatory message) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/BaseAgentAITests.cs

**Checkpoint**: User Story 1 fully functional. Agents process messages through LLM, execute tools, return natural-language responses. Edge cases covered. `dotnet test` — all existing + new tests pass.

---

## Phase 4: User Story 3 — Graceful Degradation Without Azure OpenAI (Priority: P1)

**Goal**: System operates identically to pre-feature behavior when Azure OpenAI is not configured or feature flag is disabled. No errors, no degraded UX — just the current direct-tool-execution behavior unchanged.

**Independent Test**: Run the entire test suite with IChatClient set to null and AgentAIEnabled=false — verify all existing behavior is preserved and all 625 tests pass.

**Covers**: FR-013, FR-022, FR-025, FR-026, SC-004, SC-007

### Tests for User Story 3

- [x] T019 [P] [US3] Write null-client fallback unit test (IChatClient is null → direct first-tool execution → raw JSON returned) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/BaseAgentAITests.cs
- [x] T020 [P] [US3] Write feature flag bypass unit test (AgentAIEnabled=false with non-null IChatClient → LLM skipped, direct tool execution) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/BaseAgentAITests.cs
- [x] T021 [US3] Validate all 625+ existing tests pass with zero modifications — run full test suite with null IChatClient (dotnet test Platform.Engineering.Copilot.sln)
- [x] T034 [P] [US3] Write LLM-runtime-failure fallback test (LLM throws exception during GetStreamingResponseAsync → ProcessMessageAsync catches, logs, falls back to first-tool direct execution → returns raw JSON) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/BaseAgentAITests.cs

**Checkpoint**: Graceful degradation proven. All existing tests pass unchanged. Feature flag and LLM-failure recovery both validated.

---

## Phase 5: User Story 2 — Multi-Tool Chaining Within an Agent (Priority: P2)

**Goal**: Agents handle compound requests requiring multiple sequential tool calls — the LLM chains tools automatically via FunctionInvokingChatClient and synthesizes combined results into a single cohesive response.

**Independent Test**: Send a compound request to an agent with a mock LLM returning two sequential tool calls followed by text — verify both tools execute in order and results are in the final response.

**Covers**: FR-009, FR-010, FR-011, FR-023, SC-003

### Implementation for User Story 2

- [x] T022 [US2] Configure FunctionInvokingChatClient.MaximumIterationsPerRequest from AzureOpenAIOptions.MaxToolCallRounds in ProcessMessageAsync in src/Platform.Engineering.Copilot.Core/Agents/BaseAgent.cs

### Tests for User Story 2

- [x] T023 [P] [US2] Write multi-tool chain unit test (mock LLM returns tool call A → result → tool call B → result → unified text response) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/BaseAgentAITests.cs
- [x] T024 [P] [US2] Write max rounds exceeded unit test (tool loop hits MaxToolCallRounds limit → agent returns explanatory message) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/BaseAgentAITests.cs
- [x] T036 [P] [US2] Write hallucinated-tool-name recovery test (FunctionInvokingChatClient receives unknown tool name from LLM → error sent back to LLM → LLM recovers with text response or error explanation) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/BaseAgentAITests.cs

**Checkpoint**: Multi-tool chaining works. Max round limit enforced. Hallucinated tool recovery validated. `dotnet test` — all tests pass.

---

## Phase 6: User Story 4 — Streaming AI Responses in Real Time (Priority: P2)

**Goal**: LLM responses stream token-by-token to the chat UI via SignalR StreamToken events, providing immediate feedback. Tool execution progress reported via ProgressUpdate events.

**Independent Test**: Send a message and verify multiple StreamToken events arrive over SignalR before the final ReceiveMessage event, each containing a partial response fragment.

**Covers**: FR-017, SC-005, SC-009

### Implementation for User Story 4

- [x] T025 [US4] Add tool-execution progress reporting ("Running {toolName}...") in BuildAITools delegate wrapper before calling ExecuteToolAsync in src/Platform.Engineering.Copilot.Core/Agents/BaseAgent.cs

### Tests for User Story 4

- [x] T026 [P] [US4] Write streaming token progress unit test (multiple IProgress reports during LLM response generation) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/BaseAgentAITests.cs
- [x] T027 [US4] Write streaming integration test (StreamToken SignalR events precede final ReceiveMessage event) in tests/Platform.Engineering.Copilot.Tests.Integration/Chat/ChatHubAIIntegrationTests.cs

**Checkpoint**: Streaming verified. Tokens arrive progressively. Tool execution reports progress. `dotnet test` — all tests pass.

---

## Phase 7: User Story 5 — Conversation Context Across Messages (Priority: P3)

**Goal**: Follow-up messages leverage full conversation history — AI-generated responses (not raw JSON) are stored in session and passed to the LLM for subsequent turns, enabling contextual continuity.

**Independent Test**: Send two sequential messages to the same session — verify the second LLM call includes the first user message and the first AI response in its message history.

**Covers**: FR-016, FR-018, SC-006

### Tests for User Story 5

- [x] T028 [P] [US5] Write conversation context unit test (BuildChatMessages includes prior AI response text, 10-message retention verified) in tests/Platform.Engineering.Copilot.Tests.Unit/Agents/BaseAgentAITests.cs
- [x] T029 [P] [US5] Write ChatHub conversation history integration test (second message's LLM call includes first AI response in history) in tests/Platform.Engineering.Copilot.Tests.Integration/Chat/ChatHubAIIntegrationTests.cs

**Checkpoint**: Conversation context proven. Follow-up messages carry prior AI responses. `dotnet test` — all tests pass.

---

## Phase 8: User Story 6 — Enhanced System Prompts for Tool Selection (Priority: P3)

**Goal**: Each agent's .prompt.txt is extended with "Response Guidelines" (Markdown formatting, severity badges, tables, actionable recommendations) and "Tool Selection" (intent-based selection, chaining guidance, error handling, clarifying questions) sections. Existing prompt content preserved unchanged.

**Independent Test**: Verify each prompt file contains the new sections and that existing content is unchanged.

**Covers**: FR-019, FR-020, FR-021, SC-010

### Implementation for User Story 6

- [x] T030 [US6] Append "Response Guidelines" and "Tool Selection" sections to all 9 agent .prompt.txt files (compliance, infrastructure, costmanagement, discovery, environment, knowledgebase, configuration, security, orchestrator) in src/Platform.Engineering.Copilot.Agents/ — preserve all existing content, only add new sections

**Checkpoint**: All 9 prompts enhanced. Existing content unchanged. `dotnet test` — all tests pass.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Documentation, end-to-end validation, and final verification.

- [x] T031 [P] Update architecture and agent documentation with Azure OpenAI integration details in docs/ARCHITECTURE.md and docs/AGENTS.md
- [x] T032 Run quickstart.md end-to-end validation (all 7 steps verified, build passes, tests pass)
- [x] T033 Final test suite verification — confirm total test count (625 existing + new AI tests), zero failures, code cleanup

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Setup (Phase 1) completion — **BLOCKS all user stories**
- **US1 (Phase 3)**: Depends on Foundational (Phase 2) — core AI pipeline, **MVP delivery point**
- **US3 (Phase 4)**: Depends on US1 (Phase 3) — tests the fallback path implemented in US1
- **US2 (Phase 5)**: Depends on US1 (Phase 3) — extends max-rounds configuration in ProcessMessageAsync
- **US4 (Phase 6)**: Depends on US1 (Phase 3) — extends streaming and progress reporting
- **US5 (Phase 7)**: Depends on US1 (Phase 3) — tests conversation history flow through ChatHub
- **US6 (Phase 8)**: No code dependency on US1 (prompt files only) — but logically follows US1 for integration testing
- **Polish (Phase 9)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational (Phase 2) — no story dependencies
- **US3 (P1)**: Can start after US1 — tests fallback behavior implemented in US1
- **US2 (P2)**: Can start after US1 — extends ProcessMessageAsync with max-rounds config
- **US4 (P2)**: Can start after US1 — extends streaming progress reporting
- **US5 (P3)**: Can start after US1 — tests conversation history integration
- **US6 (P3)**: Can start after Foundational (Phase 2) — prompt files are independent of ProcessMessageAsync code
- **US2, US4, US5, US6 can proceed in parallel** after US1 is complete (if staffed)

### Within Phase 3 (US1)

1. T011 (BaseAgent constructor) — FIRST
2. T012, T013 — PARALLEL (BuildChatMessages + BuildAITools, both extend BaseAgent)
3. T014 — depends on T011, T012, T013 (ProcessMessageAsync uses all three)
4. T015, T016, T035 — PARALLEL after T014 (tests + concrete agent updates + edge case test)
5. T017 — depends on T016 (ChatHub needs agents that accept IChatClient)
6. T018 — depends on T017 (integration tests need ChatHub updated)

---

## Parallel Opportunities

### Phase 1: Setup

```
# After T001 (packages), launch in parallel:
T002: Create AzureOpenAIOptions class
T003: Add config fields to appsettings.json
T004: Migrate PlatformOrchestrator.cs
T005: Migrate MockChatClientFactory.cs

# Then sequential:
T006: Migrate OrchestratorTests.cs (depends on T004 + T005)
```

### Phase 3: User Story 1 (MVP)

```
# After T011 (BaseAgent constructor), launch in parallel:
T012: BuildChatMessages helper
T013: BuildAITools helper

# After T014 (ProcessMessageAsync), launch in parallel:
T015: BaseAgentAITests
T016: Concrete agent constructor updates
T035: Empty LLM response edge case test
```

### After US1 Complete — Stories in Parallel

```
# These story phases can run concurrently:
US2 (Phase 5): Max rounds config + multi-tool tests
US4 (Phase 6): Progress reporting + streaming tests
US5 (Phase 7): Conversation context tests
US6 (Phase 8): Prompt file updates
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (packages, config, M.E.AI migration)
2. Complete Phase 2: Foundational (factory, shared DI)
3. Complete Phase 3: User Story 1 (ProcessMessageAsync, agent updates, ChatHub)
4. **STOP and VALIDATE**: Agents process messages through LLM, return natural-language responses
5. Deploy/demo with `AgentAIEnabled: false` (safe) — flip to `true` when ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready (no behavioral changes, all tests pass)
2. Add US1 → Test independently → **MVP! Agents can think** 🎯
3. Add US3 → Validate degradation → Confidence in safety net
4. Add US2 → Multi-tool chaining → Compound questions work
5. Add US4 → Streaming verified → Real-time UX
6. Add US5 → Context proven → Conversations feel natural
7. Add US6 → Prompts enhanced → Better tool selection and response formatting
8. Polish → Docs + validation → Production ready

Each story adds value without breaking previous stories. Feature flag (`AgentAIEnabled`) provides a kill switch at every step.

### Parallel Team Strategy

With multiple developers after US1 is complete:

1. Team completes Setup + Foundational + US1 together (sequential, core pipeline)
2. Once US1 is done:
   - Developer A: US2 (multi-tool) + US4 (streaming) — both extend ProcessMessageAsync
   - Developer B: US3 (degradation validation) + US5 (conversation context) — both test focused
   - Developer C: US6 (prompt files) — independent of code changes
3. Stories complete and integrate independently

---

## FR/SC Coverage Matrix

| Requirement | Tasks |
|---|---|
| FR-001 Client construction | T007 |
| FR-002 Azure Government | T007 |
| FR-003 Null return | T007, T008 |
| FR-004 DI both hosts | T009, T010 |
| FR-005 Optional IChatClient | T011 |
| FR-006 ProcessMessageAsync | T014 |
| FR-007 Chat context building | T012 |
| FR-008 Tool definitions | T013 |
| FR-009 Tool execution | T014, T036 |
| FR-010 Multi-round tools | T022, T023 |
| FR-011 Max rounds limit | T022, T024 |
| FR-012 Text response return | T014, T015, T035 |
| FR-013 Null client fallback | T014, T019, T034 |
| FR-014 8 concrete agents | T016 |
| FR-015 ChatHub ProcessMessageAsync | T017 |
| FR-016 Conversation history | T017, T028, T029 |
| FR-017 Streaming tokens | T025, T026, T027 |
| FR-018 AI response in session | T017, T029 |
| FR-019 Response Guidelines | T030 |
| FR-020 Tool Selection prompts | T030 |
| FR-021 Preserve existing prompts | T030 |
| FR-022 AgentAIEnabled flag | T002, T014, T020 |
| FR-023 MaxToolCallRounds | T002, T022, T037 |
| FR-024 Temperature | T002, T014, T037 |
| FR-025 Flag=false skips LLM | T020 |
| FR-026 625 tests pass | T021 |
| FR-027 New test coverage | T008, T015, T018–T020, T023–T024, T026–T029, T034–T037 |
| SC-001 NL responses | T014, T015 |
| SC-002 Tool selection | T013, T015 |
| SC-003 Multi-tool | T022, T023 |
| SC-004 625 tests pass | T021 |
| SC-005 First token <2s | T025, T026 |
| SC-006 Context 10 msgs | T028, T029 |
| SC-007 Flag toggle | T002, T020 |
| SC-008 8+ test scenarios | T008, T015, T018–T020, T023–T024, T026–T029, T034–T037 (16 scenarios) |
| SC-009 95% <15s | T014 (streaming) |
| SC-010 Additive prompts | T030 |

---

## Notes

- All file paths are relative to repository root
- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps each task to its user story for traceability
- Commit after each task or logical group
- Stop at any checkpoint to validate independently
- Feature flag `AgentAIEnabled` defaults to `false` — safe at every step
- `IChatClient` is always nullable — system never breaks without AI
- M.E.AI migration (T004–T006) is mechanical renames, not behavioral changes
- `FunctionInvokingChatClient` handles the tool-call loop automatically — no manual loop needed
- ChatHub has a local `ChatMessage` DTO — use fully-qualified names to avoid collision with `Microsoft.Extensions.AI.ChatMessage`
