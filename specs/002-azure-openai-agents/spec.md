# Feature Specification: Add Azure OpenAI to Platform Copilot Agents

**Feature Branch**: `002-azure-openai-agents`  
**Created**: 2026-02-22  
**Status**: Draft  
**Input**: User description: "Add Azure OpenAI support to Platform Copilot Agents"

## User Scenarios & Testing *(mandatory)*

### User Story 1 — AI-Powered Conversational Agent Responses (Priority: P1)

A platform engineer asks the copilot a natural-language question within an agent's domain — for example, "What's our current FedRAMP compliance posture?" The orchestrator routes the message to the Compliance agent. Instead of picking the first available tool and dumping raw JSON back, the agent understands the user's intent, selects the appropriate tool (`compliance_assess`), executes it, and then interprets the JSON results into a human-readable Markdown response with severity badges, tables, and actionable recommendations. The user gets a conversational answer, not a data dump.

**Why this priority**: This is the core value proposition. Without agents that can think, the copilot is just a glorified CLI with a chat skin. Every other story depends on agents being able to process messages through an LLM.

**Independent Test**: Can be fully tested by sending a message to any single agent with a mock LLM that returns a tool call followed by a text response — verify the tool executes and the natural-language response (not raw JSON) reaches the user.

**Acceptance Scenarios**:

1. **Given** Azure OpenAI is configured and `AgentAIEnabled` is `true`, **When** a user sends "assess our NIST 800-53 compliance" to the chat, **Then** the Compliance agent calls the `compliance_assess` tool with appropriate parameters, interprets the result, and returns a Markdown-formatted response with findings, severity levels, and next steps.
2. **Given** Azure OpenAI is configured and `AgentAIEnabled` is `true`, **When** a user sends "how much are we spending on compute?" to the chat, **Then** the Cost Management agent calls the relevant cost analysis tool and returns a conversational summary with cost breakdowns and trend analysis.
3. **Given** the LLM responds with a tool call, **When** the tool executes successfully, **Then** the tool result is sent back to the LLM as a tool-response message and the LLM generates a final natural-language answer incorporating the tool output.

---

### User Story 2 — Multi-Tool Chaining Within an Agent (Priority: P2)

A user asks a compound question that requires multiple tools — for example, "Assess our compliance and then generate a remediation plan for anything critical." The agent's LLM recognizes this requires two sequential tool calls: first `compliance_assess`, then `compliance_generate_plan` using the assessment results. The agent executes both tools in sequence as directed by the LLM and synthesizes the combined results into a single cohesive response.

**Why this priority**: Real-world platform engineering questions are rarely single-tool. Users shouldn't have to break compound requests into separate messages. This unlocks the full power of having 59 tools across 8 agents.

**Independent Test**: Can be tested by sending a compound request to an agent with a mock LLM configured to return two sequential tool calls followed by a text response — verify both tools execute in order and both results are available in the final response.

**Acceptance Scenarios**:

1. **Given** an agent with multiple registered tools, **When** the LLM responds with a tool call and then a second tool call after receiving the first result, **Then** both tools execute in sequence and the LLM produces a unified response incorporating both results.
2. **Given** an agent processes a multi-tool request, **When** the tool-call loop reaches the configured maximum rounds (default 5), **Then** the agent terminates gracefully and returns an explanation to the user that the request was too complex to complete fully.
3. **Given** the first tool in a chain fails, **When** the LLM receives the error, **Then** the LLM decides how to proceed — either calling an alternative tool, explaining the error to the user, or both.

---

### User Story 3 — Graceful Degradation Without Azure OpenAI (Priority: P1)

A deployment environment does not have Azure OpenAI configured (e.g., air-gapped IL6, dev/test without AI). The copilot continues working exactly as it does today — the orchestrator routes messages, agents execute tools directly, and raw tool output is returned. No errors, no degraded UX messaging — just the current behavior unchanged. The AI layer is additive, not a hard dependency.

**Why this priority**: Equal to P1 because the system must never break when AI is unavailable. This is a DoD IL5/IL6 system where some environments will not have external AI endpoints. All 625 existing tests must continue to pass unchanged.

**Independent Test**: Can be tested by running the entire test suite with `IChatClient` set to null and `AgentAIEnabled` set to `false` — verify all existing behavior is preserved and all 625 tests pass.

**Acceptance Scenarios**:

1. **Given** Azure OpenAI configuration is empty or missing, **When** the system starts, **Then** the chat client factory returns null, agents receive null for `IChatClient`, and the system operates in fallback mode (direct tool execution).
2. **Given** `AgentAIEnabled` is `false` in configuration, **When** a user sends a message, **Then** agents use fallback mode (first matching tool, direct execution, raw output) even if `IChatClient` is available.
3. **Given** all existing agent, tool, orchestrator, and ChatHub tests, **When** run with no Azure OpenAI configured, **Then** all 625 tests pass with zero modifications.

---

### User Story 4 — Streaming AI Responses in Real Time (Priority: P2)

When an agent processes a message through the LLM, the response streams token-by-token to the chat UI via SignalR rather than arriving as a single block after the entire generation completes. The user sees the response building progressively, providing immediate feedback that the system is working and reducing perceived latency.

**Why this priority**: Streaming is table-stakes UX for any AI chat application. Without it, users face 5–15 second delays with no feedback while the LLM generates responses. The existing `StreamToken` SignalR method already exists — this story wires real LLM tokens through it.

**Independent Test**: Can be tested by sending a message and verifying that multiple `StreamToken` events arrive over the SignalR connection before the final `ReceiveMessage` event, with each token containing a partial response fragment.

**Acceptance Scenarios**:

1. **Given** an agent processes a message through the LLM, **When** the LLM generates its response, **Then** individual tokens stream to the client via `StreamToken` SignalR events before the complete message arrives via `ReceiveMessage`.
2. **Given** the LLM is processing a tool call (not generating text), **When** tool execution is in progress, **Then** progress updates stream via `ProgressUpdate` SignalR events indicating which tool is running.

---

### User Story 5 — Conversation Context Across Messages (Priority: P3)

A user has an ongoing conversation with the copilot. Their follow-up messages reference prior context — "What about for the production subscription?" after already asking about dev costs, or "Now check compliance for those same resources." The agent receives the full conversation history and the LLM uses it to resolve references and maintain context across turns.

**Why this priority**: Conversation continuity is expected in any chat-based AI interface, but the existing `ConversationSession` already stores messages with ≥10-message retention. This story ensures that stored history is actually passed to the LLM and includes AI-generated responses (not just raw tool output).

**Independent Test**: Can be tested by sending two sequential messages to the same session and verifying the second LLM call includes the first user message and the first AI response in its message history.

**Acceptance Scenarios**:

1. **Given** a user has sent a message and received an AI-generated response, **When** they send a follow-up message in the same session, **Then** the AI-generated response (not raw tool JSON) is included in the conversation history passed to the LLM.
2. **Given** a session has 10+ messages, **When** the agent processes a new message, **Then** at least the most recent 10 messages are included in the conversation context.

---

### User Story 6 — Enhanced System Prompts for Tool Selection (Priority: P3)

Each agent's `.prompt.txt` system prompt is enhanced with guidance for the LLM about how to select tools, when to chain multiple tools, how to format responses with Markdown, when to ask clarifying questions, and how to handle tool errors gracefully. The existing persona and tool descriptions in the prompts are preserved and extended.

**Why this priority**: The LLM's quality of tool selection and response formatting depends entirely on the system prompt. Without guidance, the model will make poor tool choices and return inconsistently formatted responses. However, this can be iterated on after the core pipeline works.

**Independent Test**: Can be tested by verifying each prompt file contains the new "Response Guidelines" and "Tool Selection" sections, and by sending ambiguous requests to an agent and verifying the LLM makes reasonable tool selections per the prompt guidance.

**Acceptance Scenarios**:

1. **Given** each agent's `.prompt.txt` file, **When** loaded at agent startup, **Then** the prompt contains "Response Guidelines" and "Tool Selection" sections in addition to the existing persona and tool descriptions.
2. **Given** a user sends an ambiguous request like "help me with compliance", **When** the agent processes it, **Then** the LLM asks a clarifying question rather than arbitrarily picking a tool (as guided by the system prompt).
3. **Given** a tool returns an error, **When** the LLM receives the error result, **Then** the LLM explains the error in user-friendly language rather than returning the raw error JSON.

---

### Edge Cases

- What happens when the Azure OpenAI endpoint is unreachable mid-conversation (network timeout after successful initial connection)?
- How does the system handle a tool call from the LLM that references a tool name not registered on the agent (hallucinated tool name)?
- What happens when the LLM returns empty content (no text, no tool calls) in its response?
- How does the system behave when the Azure OpenAI API key is expired or revoked after initial successful validation?
- What happens when the LLM response exceeds the maximum token limit (truncated response)?
- How does the system handle concurrent requests from the same user session to the same agent — are LLM calls serialized or parallelized?
- What happens when a tool execution takes longer than expected (>30 seconds) during a multi-tool chain — does the LLM timeout or wait?

## Requirements *(mandatory)*

### Functional Requirements

#### Azure OpenAI Client Wiring

- **FR-001**: System MUST construct an Azure OpenAI chat client from configuration values (endpoint, API key, deployment name) in the `AzureOpenAI` configuration section.
- **FR-002**: System MUST support Azure Government endpoints (`.us` domain suffixes) for IL5/IL6 deployments.
- **FR-003**: System MUST return null gracefully when Azure OpenAI configuration is empty or missing, enabling fallback mode without AI features.
- **FR-004**: System MUST make the constructed chat client available through dependency injection in both the MCP server and Chat server entry points.

#### Agent-Level AI Integration

- **FR-005**: `BaseAgent` MUST accept an optional (nullable) chat client in its constructor alongside the existing logger parameter.
- **FR-006**: `BaseAgent` MUST expose a `ProcessMessageAsync` method that accepts: user message text, conversation history, a progress callback, and a cancellation token.
- **FR-007**: `ProcessMessageAsync` MUST build the chat context starting with the agent's system prompt, followed by conversation history, followed by the current user message.
- **FR-008**: `ProcessMessageAsync` MUST generate function/tool definitions from the agent's registered tools (name, description, parameter schema) and include them in the LLM request.
- **FR-009**: When the LLM responds with a tool/function call, the agent MUST look up the tool by name, execute it via the existing `ExecuteToolAsync` mechanism, and return the tool result to the LLM as a tool-response message.
- **FR-010**: The agent MUST support multi-round tool calling — the LLM may issue multiple sequential tool calls before producing a final text response.
- **FR-011**: The agent MUST enforce a configurable maximum tool-call round limit (default: 5) to prevent infinite loops. When the limit is reached, the agent MUST terminate and return an explanatory message to the user.
- **FR-012**: When the LLM produces a final text response (not a tool call), the agent MUST return that text as the agent's answer.
- **FR-013**: When the chat client is null, `ProcessMessageAsync` MUST fall back to the current behavior: execute the first matching tool with the provided parameters and return the raw result.
- **FR-014**: All 8 concrete agents (Compliance, Infrastructure, Cost Management, Discovery, Environment, Knowledge Base, Configuration, Security) MUST be updated to accept and forward the optional chat client to `BaseAgent`.

#### ChatHub Integration

- **FR-015**: `ChatHub.SendMessage` MUST use `ProcessMessageAsync` on the matched agent instead of the current approach of calling `ExecuteToolAsync` on the first tool with empty parameters.
- **FR-016**: The full conversation history from the session MUST be passed to `ProcessMessageAsync`, including both user messages and AI-generated assistant responses.
- **FR-017**: When the LLM generates its response, tokens MUST stream to the client via the existing `StreamToken` SignalR method for real-time progressive display.
- **FR-018**: AI-generated responses stored in the conversation session MUST contain the natural-language response text, not raw tool output JSON.

#### System Prompt Enhancement

- **FR-019**: Each agent's `.prompt.txt` system prompt MUST be extended with a "Response Guidelines" section covering Markdown formatting, severity badges, tables, and code blocks.
- **FR-020**: Each agent's `.prompt.txt` system prompt MUST be extended with a "Tool Selection" section describing when and how to select tools, chain multiple tools, and handle tool errors.
- **FR-021**: Existing persona, role, and tool descriptions in each `.prompt.txt` MUST be preserved unchanged — only additive sections are permitted.

#### Configuration & Feature Flags

- **FR-022**: System MUST support a boolean feature flag (`AgentAIEnabled`, default: `false`) that controls whether agents use LLM-powered processing or direct tool execution.
- **FR-023**: System MUST support a configurable maximum tool-call round limit (`MaxToolCallRounds`, default: `5`).
- **FR-024**: System MUST support a configurable LLM temperature setting (`Temperature`, default: `0.3`).
- **FR-025**: When `AgentAIEnabled` is `false`, agents MUST skip LLM processing even if a chat client is available.

#### Testing & Backward Compatibility

- **FR-026**: All 625 existing unit and integration tests MUST continue to pass with zero modifications.
- **FR-027**: New tests MUST cover: single tool call via LLM, multi-tool chaining, null chat client fallback, max tool-call round enforcement, chat client factory with valid/empty configuration, ChatHub conversation history passing, and feature flag behavior.

### Key Entities

- **Chat Client Factory**: Responsible for constructing the AI chat client from configuration. Reads endpoint, API key, and deployment name. Supports Azure Commercial and Azure Government clouds. Returns null when configuration is absent.
- **Chat Context**: The assembled message sequence sent to the LLM — system prompt, conversation history (converted from session messages), and current user message. Also includes tool/function definitions derived from the agent's registered tools.
- **Tool Call Round**: A single cycle of: LLM requests a tool call → agent executes the tool → agent sends the result back to the LLM. Multiple rounds can occur in sequence before the LLM produces a final text response.
- **Session Message**: Existing entity representing a message in a conversation session. Stores role (User/Assistant/System), content, timestamp, correlation ID, and optional agent ID. Must store AI-generated natural language content for assistant messages, not raw tool JSON.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users receive natural-language responses that interpret tool results (not raw JSON) for 100% of AI-enabled agent interactions.
- **SC-002**: Agents correctly select the appropriate tool based on user intent for at least 90% of the defined acceptance test scenarios within their domain.
- **SC-003**: Multi-tool requests complete successfully with the LLM chaining 2+ tools in sequence without user intervention for compound questions.
- **SC-004**: System operates without errors and all 625 existing tests pass when Azure OpenAI is not configured (fallback mode).
- **SC-005**: First token of a streaming response arrives at the client within 2 seconds of the user sending a message (excluding tool execution time). *Operational target — validated via manual/load testing, not automated unit tests.*
- **SC-006**: Conversation context is preserved across turns — follow-up messages correctly reference information from at least the prior 10 messages in the session.
- **SC-007**: Feature flag toggling between AI-enabled and direct-execution modes can be performed via configuration change without code deployment or system restart.
- **SC-008**: New test coverage includes at least 8 test scenarios specifically validating the AI integration flow (single tool call, multi-tool chain, null client fallback, max rounds, factory valid/empty config, ChatHub history, feature flag behavior).
- **SC-009**: 95% of AI-generated responses complete within 15 seconds end-to-end (including tool execution and LLM generation time). *Operational target — validated via manual/load testing, not automated unit tests.*
- **SC-010**: System prompt enhancements are additive only — no existing prompt content is removed or rewritten.

## Assumptions

- The `Microsoft.Extensions.AI` abstraction (`IChatClient`) used by the orchestrator is bridged to the Azure OpenAI SDK via the `Microsoft.Extensions.AI.OpenAI` package's `.AsIChatClient()` extension method. No custom adapter layer is needed.
- Azure Government endpoints follow the same API contract as Azure Commercial — only the base URL domain differs (`.us` suffix).
- The existing `ConversationSession` in-memory store with its current thread-safety model (`lock` + `List<SessionMessage>`) is sufficient for this feature. Distributed session storage is out of scope.
- Tool execution time is bounded by existing timeout mechanisms. The LLM waiting for tool results does not require a separate timeout beyond the overall request cancellation token.
- The 9 existing `.prompt.txt` files are the complete set requiring enhancement — one per agent. No new prompt files need to be created.
- Temperature of `0.3` is appropriate for compliance/infrastructure workloads that favor precision over creativity. This can be tuned per-agent later if needed.
- The `Dictionary<string, object?>` parameters that tools already accept can be populated from the LLM's function-call arguments via standard JSON deserialization.

## Constraints

- The `Azure.AI.OpenAI` NuGet package MUST only be added to the Core project. Agents depend on the `IChatClient` abstraction only — no direct Azure SDK dependency.
- `IChatClient` MUST remain optional (nullable) in `BaseAgent`. The system MUST function without any AI endpoint configured.
- The MCP tool interface and tool execution contract MUST NOT change. Tools receive `Dictionary<string, object?>` parameters and return JSON strings.
- The orchestrator's routing logic MUST NOT be modified. AI enhancement is agent-internal, not at the routing level.
- Existing `.prompt.txt` content MUST NOT be removed or rewritten — only extended with new sections.
- The current test count of 625 tests (566 unit + 59 integration) MUST remain passing with zero modifications to existing test code.

## Dependencies

- Azure OpenAI service endpoint (Azure Commercial or Azure Government) with a deployed GPT-4o model.
- `Azure.AI.OpenAI` 2.1.0 NuGet package (Azure OpenAI SDK client; does not directly implement `IChatClient`).
- `Microsoft.Extensions.AI.OpenAI` 10.3.0 NuGet package (bridge package providing `.AsIChatClient()` to connect the Azure SDK to the `IChatClient` abstraction).
- `Microsoft.Extensions.AI` — currently 9.1.0-preview in the Core project, to be upgraded to 10.3.0 (stable) for compatibility with the bridge package.
- Existing `PlatformOrchestrator` routing (unchanged) — the AI enhancement sits downstream of routing.

## Scope Boundaries

### In Scope

- Azure OpenAI client construction and DI registration
- `BaseAgent` enhancement with `ProcessMessageAsync` and LLM-powered tool calling
- All 8 concrete agent constructor updates
- `ChatHub.SendMessage` integration with `ProcessMessageAsync`
- Real-time token streaming through SignalR
- Conversation history passing to agents
- System prompt extensions (9 `.prompt.txt` files)
- Configuration additions (`AgentAIEnabled`, `MaxToolCallRounds`, `Temperature`)
- New unit and integration tests for the AI flow

### Out of Scope

- Changing the orchestrator's routing algorithm
- Modifying the MCP tool interface or tool execution signatures
- Adding AI capabilities inside individual tools (tools remain deterministic)
- Distributed conversation session storage (Redis, database-backed sessions)
- Per-agent temperature or model configuration (single global config for now)
- Prompt engineering optimization beyond the initial "Response Guidelines" and "Tool Selection" sections
- Usage tracking, token counting, or cost management for Azure OpenAI API calls
- Rate limiting or throttling of Azure OpenAI requests
- Multi-model support (single deployment name for all agents)
