# Research: Build Platform Copilot Core

**Branch**: `001-platform-copilot-core` | **Date**: 2026-02-22

## Table of Contents

- [1. Multi-Agent Orchestration Pattern](#1-multi-agent-orchestration-pattern)
- [2. MCP Server Dual Transport](#2-mcp-server-dual-transport)
- [3. CAC/PIV + PIM Authentication Flow](#3-cacpiv--pim-authentication-flow)
- [4. EF Core Data Model Strategy](#4-ef-core-data-model-strategy)
- [5. Real-time Streaming Architecture](#5-real-time-streaming-architecture)
- [6. Template Generation Architecture](#6-template-generation-architecture)

---

## 1. Multi-Agent Orchestration Pattern

**Decision**: Use the **Microsoft Agents SDK** (`Microsoft.Agents.*`) with a custom `PlatformOrchestrator` implementing keyword fast-path routing + LLM fallback. Each specialized agent is a `Microsoft.Agents.Builder.AgentApplication` registered with the orchestrator.

**Rationale**: The Microsoft Agents SDK provides a production-grade, extensible agent framework with built-in activity handling, state management, and channel integration (Teams, DirectLine, custom). This positions the platform for future M365 Copilot integration (US13) while providing the same multi-agent coordination capabilities. The three-layer architecture — `Microsoft.Agents.Protocols` (core types), `Microsoft.Agents.Builder` (agent construction), `Microsoft.Agents.Client` (inter-agent communication) — aligns with the BaseAgent/BaseTool constitution principle.

**Design Details**:

- **Agent Registration**: Each agent extends `BaseAgent` (which wraps `AgentApplication` from `Microsoft.Agents.Builder`). Agents are registered with the orchestrator at startup via DI. The orchestrator maintains an `IReadOnlyList<BaseAgent>` for routing.
- **Routing Strategy**: `PlatformOrchestrator.RouteAsync()` uses a two-tier approach:
  1. **Keyword fast-path** (O(1) lookup): Maps known keywords to agent IDs (e.g., "compliance"/"nist"/"fedramp" → ComplianceAgent, "cost"/"spending"/"budget" → CostManagementAgent)
  2. **LLM fallback**: When no keyword matches, classifies intent against agent descriptions via `IChatClient`
- **Direct targeting** (FR-005): When user explicitly names an agent (e.g., `@compliance`), routing bypasses intent analysis and routes directly to the named agent
- **Ambiguity handling**: When multiple agents match equally, select based on primary keywords and return a transparent routing explanation (e.g., "Routing to Compliance Agent based on 'assessment' keyword")
- **Tool Execution**: Each `BaseTool` is registered as a function on its parent agent. Tool invocation flows through `Microsoft.Agents.Builder`'s activity pipeline, enabling middleware for auth gating, audit logging, and PIM enforcement
- **Inter-Agent Communication**: `Microsoft.Agents.Client` provides the client abstraction for agent-to-agent calls when needed (e.g., Compliance Agent querying NistService data via KB Agent)
- **Context Flow**: Shared context (subscription, cached assessments) flows through `IAgentStateManager` and Semantic Kernel's `KernelArguments` for multi-turn workflows. `Microsoft.Agents.Builder` state management handles turn-level context
- **Termination**: The orchestrator determines when the selected agent's response is complete. Handles: auth-gating interrupts (CAC/PIM required responses), tool execution failures, and multi-step flows (dry-run → confirm → execute)

**Key Packages**:

| Package | Purpose |
|---|---|
| `Microsoft.Agents.Protocols` | Core types: `IActivity`, `ITurnContext`, channel protocols |
| `Microsoft.Agents.Builder` | `AgentApplication`, middleware pipeline, state management |
| `Microsoft.Agents.Client` | Inter-agent communication, `AgentClient` |
| `Microsoft.SemanticKernel` 1.26.0 | AI orchestration, function calling, prompt templates |
| `Microsoft.Extensions.AI` 9.1.0 | `IChatClient` abstraction for LLM routing fallback |

**Alternatives Considered**:

| Alternative | Why Rejected |
|---|---|
| Semantic Kernel `AgentGroupChat` | Limited to chat-only coordination; no activity pipeline, no channel integration, no built-in state management |
| AutoFunctionCallingFilter-only (no orchestrator) | Can't model multi-agent selection; only one agent's tools visible at a time |
| Semantic Kernel Planner (Handlebars/Stepwise) | Over-engineered for single-turn routing; adds latency and unpredictability |
| Pure LLM-based routing (no fast-path) | Adds 500–1500ms latency per message; fast-path keywords cover 80%+ of messages |
| Agent-to-agent direct calls without SDK | Creates tight coupling, makes tracing/auditing harder, no middleware pipeline |

---

## 2. MCP Server Dual Transport

**Decision**: Single `Program.cs` entry point with a mode switch — `--stdio` flag for AI client transport, default HTTP on port 5100 for web/admin clients. Uses `ModelContextProtocol 0.4.0-preview`.

**Rationale**: A single binary ensures identical tool capabilities across both transports (FR-007). The MCP SDK's builder APIs accept either `StdioServerTransport` or `HttpServerTransport`, so the same tool registry is reused with zero code duplication.

**Design Details**:

- **HTTP mode** (default): ASP.NET Core Kestrel on port 5100. `McpHttpBridge` maps MCP JSON-RPC methods (`tools/list`, `tools/call`) to HTTP endpoints. Web Chat (5001) and Admin API (5050) call in via HTTP.
- **stdio mode** (`--stdio` flag): Reads JSON-RPC from stdin, writes to stdout. GitHub Copilot and Claude Desktop connect this way.
- **Tool auth declaration**: Each `BaseTool` exposes `RequiresAuthentication` (bool) and `PimTierRequired` (`PimTier.None | PimTier.Read | PimTier.Write`). Both are exposed as custom MCP tool metadata via `tools/list`.
- **Server-side enforcement**: In HTTP mode, ASP.NET middleware validates JWT → `amr` claim for CAC → PIM role claims for tier. In stdio mode, relies on locally authenticated context (Azure CLI credential). Enforcement is always server-side (FR-010).

**Alternatives Considered**:

| Alternative | Why Rejected |
|---|---|
| Separate binaries for HTTP and stdio | Code duplication; violates "identical capabilities" requirement |
| gRPC instead of HTTP | Government proxy/firewall compatibility concerns; MCP spec is JSON-RPC |
| Skip server-side auth, trust client | Violates FR-010; CAC+PIM must be enforced server-side for IL5/IL6 |

---

## 3. CAC/PIV + PIM Authentication Flow

**Decision**: MSAL.NET with CAC-based interactive auth → JWT with `amr` claim validation → On-Behalf-Of (OBO) flow for Azure ARM operations → PIM tier modeled as role claims with read/write distinction.

**Rationale**: OBO flow preserves user identity for audit accountability (IL5 requirement). PIM tiers map naturally to JWT role claims. Server-side enforcement ensures no client-side bypass.

**Design Details**:

- **JWT Flow**:
  1. Client: User inserts CAC → MSAL `PublicClientApplication` triggers interactive auth against `login.microsoftonline.us` → Azure AD issues JWT with `amr: ["mfa", "rsa"]`
  2. Client sends `Authorization: Bearer <JWT>` to MCP server
  3. Server validates: `aud` = `api://platform-engineering-copilot`, `iss` = Gov tenant, `amr` contains `mfa` AND (`rsa` or `smartcard`)
  4. Server uses `ConfidentialClientApplication.AcquireTokenOnBehalfOf()` for ARM-scoped tokens

- **PIM Tiers**:
  - `PimTier.None`: Knowledge Base queries, template generation, cached data viewing
  - `PimTier.Read`: Assessments, discovery, cost queries, evidence collection
  - `PimTier.Write`: Remediations, deployments, policy modifications
  - Each `BaseTool` declares its required tier; middleware enforces before `ExecuteAsync`

- **Session Expiration Mid-Operation** (FR-014):
  1. Operation stops gracefully (no partial writes in inconsistent state)
  2. Partial results preserved in session (e.g., "12 of 18 control families scanned")
  3. System prompts for re-auth of only the expired component (CAC or PIM, not both)
  4. Operation resumes from checkpoint, not from scratch
  - CAC timeout: 8h (default). PIM timeout: 4h (default), 8h (max).

- **Dev Bypass Mode** (FR-015):
  - Config: `"RequireCac": false, "RequirePim": false` in `appsettings.Development.json`
  - Middleware still runs but uses `DefaultAzureCredential` (Azure CLI) instead of OBO
  - All enforcement points remain in code but are satisfied by bypass flag
  - Requires `ASPNETCORE_ENVIRONMENT=Development` AND explicit config flags

**Alternatives Considered**:

| Alternative | Why Rejected |
|---|---|
| Service principal instead of OBO | Loses user identity; audit trail breaks "who did this" |
| Certificate-based auth without Azure AD | No PIM integration; custom PKI validation is fragile |
| Single PIM tier (no read/write split) | Violates FR-069; auditors shouldn't need write elevation |
| Session tokens cached server-side | CAC tokens must not be cached (FR-016) |

---

## 4. EF Core Data Model Strategy

**Decision**: Two EF Core 9.0 `DbContext` classes — `PlatformEngineeringCopilotContext` (main) and `ChatDbContext` (chat history). SQL Server primary with SQLite fallback. Append-only audit log.

**Rationale**: Chat history is high-volume and low-retention; compliance data is lower-volume and high-retention (3–7 years). Separate contexts allow independent scaling, backup, and purge policies.

**Design Details**:

- **`PlatformEngineeringCopilotContext`**: ServiceTemplate, InfrastructureTemplate, ProvisionedEnvironment, ComplianceAssessment, ComplianceFinding, RemediationTask, AuditLog, ApprovalWorkflow, AgentConfiguration, EvidencePackage, ComplianceDocument, Alert, Configuration
- **`ChatDbContext`**: Conversation, ChatMessage, ConversationContext, MessageAttachment

- **Retention Enforcement**:
  | Entity | Retention | Mechanism |
  |---|---|---|
  | ComplianceAssessment + findings | 3 years (FR-072) | Soft-delete with `IsDeleted`; background archival job |
  | AuditLogEntity | 7 years, immutable (FR-073) | Append-only repository; DB-level `DENY UPDATE, DELETE`; partition by year |
  | InfrastructureTemplate | 30-minute TTL | Auto-cleanup background service |
  | ServiceTemplateEntity | Permanent | Versioned with approval workflow |

- **Immutable Audit Log** (FR-073):
  - Repository exposes only `AddAsync` and query methods — no Update or Remove
  - EF Core entity configured with `NoTracking` by default
  - Production: SQL Server `DENY UPDATE, DELETE ON AuditLogs TO [app_role]`
  - `RowVersion` for concurrency detection; partitioned by year on `Timestamp`

- **SQLite Fallback**:
  - Config: `DatabaseProvider: "SqlServer" | "Sqlite"`
  - Used for: local dev without Docker, disconnected/edge scenarios, unit testing
  - Migrations generated for SQL Server; SQLite uses `EnsureCreated()`
  - Avoid SQL Server-specific features in model (use GUID concurrency token instead of `rowversion`)

**Alternatives Considered**:

| Alternative | Why Rejected |
|---|---|
| Single DbContext | Chat pollutes compliance data lifecycle; different retention/scaling needs |
| Cosmos DB | Not available in all Azure Government regions; SQL Server is IL5 standard |
| Event sourcing for audit | Complexity cost too high; append-only table achieves immutability simply |

---

## 5. Real-time Streaming Architecture

**Decision**: ASP.NET Core SignalR hub for Chat UI (port 5001) with WebSocket transport, streaming agent responses token-by-token via `IAsyncEnumerable`, and structured progress messages for long-running operations.

**Rationale**: SignalR provides bidirectional communication (needed for user confirmations during remediations), native browser support, and built-in reconnection. `IAsyncEnumerable` streaming integrates naturally with Semantic Kernel's `GetStreamingResponseAsync()`.

**Design Details**:

- **ChatHub Methods**: `SendMessage(string message)`, `StreamResponse(IAsyncEnumerable<string> tokens)`, `SendProgressUpdate(ProgressUpdate update)`
- **Streaming Flow**:
  1. User sends message via SignalR → Orchestrator routes to agent
  2. Agent calls `IChatClient.GetStreamingResponseAsync()` → `IAsyncEnumerable<StreamingChatMessageContent>`
  3. Each chunk forwarded to client via SignalR streaming
  4. Client renders Markdown progressively

- **Progress Updates** (scans >10s per SC-001):
  ```json
  {
    "phase": "Scanning control family AC (Access Control)",
    "currentStep": 3,
    "totalSteps": 18,
    "percentComplete": 17,
    "estimatedTimeRemaining": "00:02:34",
    "findingsCount": 4
  }
  ```
  - `BaseTool.ExecuteAsync` accepts `IProgress<ProgressUpdate>` parameter
  - ETA calculated via elapsed time per control family × remaining, with exponential smoothing

- **Markdown Rendering** (FR-048):
  - Tables, code blocks, collapsible sections (`<details><summary>`), severity badges (`🔴 Critical`), action buttons (special `action://` links intercepted by client)
  - Fenced code blocks with language identifiers for Bicep/Terraform syntax highlighting

**Alternatives Considered**:

| Alternative | Why Rejected |
|---|---|
| Server-Sent Events (SSE) | Unidirectional; can't support user confirmations mid-remediation |
| gRPC streaming | Requires gRPC-Web proxy for browsers; SignalR works natively |
| Long polling | Higher latency, more server load than WebSockets |

---

## 6. Template Generation Architecture

**Decision**: Three methods per FR-030: (1) Template Generator (default, deterministic), (2) AI-generated (LLM-powered), (3) Bicep ACR (registry modules). All produce compliance-annotated output. Generation is local/unauthenticated; deployment requires CAC+PIM.

**Rationale**: Template Generator provides deterministic, guaranteed-compliant output for common patterns. AI covers novel/complex requests. ACR provides organization-approved, pre-reviewed modules. The combination covers the full spectrum from deterministic to flexible.

**Design Details**:

- **Template Generator (default)**:
  - Assembles templates from a curated library of known-compliant resource patterns
  - Each pattern has security properties pre-configured (TLS 1.2, HTTPS-only, private endpoints)
  - Composes patterns based on request (e.g., "AKS cluster" → AKS + NSG + Key Vault + monitoring patterns)
  - Deterministic: same input → same output

- **AI-generated**:
  - Infrastructure Agent's LLM creates templates tailored to the request
  - System prompt instructs LLM to include compliance annotations and security-by-default configs
  - Output validated against compliance checker before presentation

- **Bicep ACR**:
  - Pulls verified, org-approved Bicep modules from private Azure Container Registry
  - Modules have been through security review and approval
  - Registry access uses service credentials (not user identity); does not require CAC

- **Compliance Annotations** (FR-031, SC-009 ≥80% coverage):
  ```bicep
  resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
    properties: {
      supportsHttpsTrafficOnly: true    // SC-8: Transmission Confidentiality
      minimumTlsVersion: 'TLS1_2'      // SC-8(1): Cryptographic Protection
      networkAcls: {
        defaultAction: 'Deny'           // SC-7: Boundary Protection
      }
    }
  }
  ```

- **Auth Boundary** (FR-032):
  - `generate_infrastructure_template`: `RequiresAuthentication = false`
  - `provision_infrastructure`: `RequiresAuthentication = true, PimTierRequired = PimTier.Write`
  - Lifecycle: Generate (local) → Store (30-min TTL) → Preview → Confirm → Deploy (CAC+PIM)

**Alternatives Considered**:

| Alternative | Why Rejected |
|---|---|
| AI-only (no Template Generator) | Non-deterministic; can't guarantee compliance properties; LLMs hallucinate API versions |
| Template Generator only (no AI) | Can't handle novel or complex requests; too rigid for NL interaction |
| ACR-only | Requires pre-built modules for every scenario; can't handle ad-hoc requests |
| Require auth for generation | Violates FR-032; blocks offline/disconnected usage |
