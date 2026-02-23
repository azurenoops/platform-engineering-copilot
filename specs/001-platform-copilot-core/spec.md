# Feature Specification: Build Platform Copilot Core

**Feature Branch**: `001-platform-copilot-core`  
**Created**: 2026-02-22  
**Status**: Draft  
**Input**: User description: "Develop the Platform Engineering Copilot, an AI-powered infrastructure and compliance platform for Azure Government. Build the foundational multi-agent platform that compliance officers, platform engineers, security leads, and auditors use to manage Azure Government environments through natural language conversation."

## Out of Scope (Initial Build Phase)

- **Admin Dashboard (US11)**: Deferred to a follow-on phase. The core conversational agent platform is prioritized over visual operational dashboards.
- **Compliance Monitoring (US10)**: Deferred to a follow-on phase. The full continuous monitoring infrastructure (scheduled checks, event-driven drift detection with 5-minute latency, continuous stream with 60-second latency, alert lifecycle management, and auto-escalation) is operationally complex and depends on the assessment engine, agent infrastructure, and configuration context being stable first. However, the lightweight on-demand `compliance_monitoring` tool (status, scan, alerts, trend queries against existing data and live Azure APIs) is included in the initial build as part of the Compliance Agent tool set per compliance-tools.md.
- **GitHub Copilot Extension (US12)**: Scaffold-only for initial phase. Project structure and extension manifest created; full functionality deferred.
- **M365 Copilot Extension (US13)**: Scaffold-only for initial phase. Project structure created; full Teams bot, Adaptive Cards, and Excel/Word integration deferred.

## Clarifications

### Session 2026-02-22

- Q: What should be explicitly out-of-scope for the initial build phase? → A: Extensions (US12, US13) scaffold-only; Admin Dashboard (US11) and Compliance Monitoring (US10) deferred to follow-on phase.
- Q: How should the Infrastructure Agent produce compliant-by-default templates? → A: Three methods supported — (1) Template Generator (default), (2) AI-generated, (3) Bicep ACR (Azure Container Registry modules). Template Generator is the default method.
- Q: How long should assessment results and audit logs be retained? → A: Assessment data retained for 3 years; audit logs retained for 7 years (immutable).
- Q: What is the maximum acceptable assessment time for large environments (1,000+ resources)? → A: 5 minutes for ≤2,000 resources, 10 minutes for ≤5,000 resources, with mandatory progress streaming.
- Q: What observability signals should the platform expose beyond audit logging? → A: Health endpoint, structured metrics (latency, error rate per agent), and distributed tracing (correlation IDs across agent calls).
- Q: Can a user hold multiple roles simultaneously? → A: Yes; the user receives the union of all assigned role permissions.
- Q: Does the `compliance_monitoring` tool conflict with US10 (Compliance Monitoring) being deferred? → A: No; `compliance_monitoring` is a lightweight on-demand query tool (status, scan, alerts, trend) included in the initial build. US10's full infrastructure (scheduled checks, event-driven drift, continuous stream, alert lifecycle, auto-escalation) remains deferred.
- Q: Should `compliance-tools.md` be the canonical source of truth for Compliance Agent tool names and schemas? → A: Yes. `compliance-tools.md` is canonical. mcp-tools.md, tasks.md, and spec.md must align with its tool names (`compliance_assess`, `compliance_remediate`, `compliance_collect_evidence`, `compliance_generate_document`, etc.), response envelopes, and error codes.
- Q: Should the response envelope from compliance-tools.md be the platform-wide standard for all agents? → A: Yes. All 8 agents adopt the same envelope schema (`status`, `data`, `metadata` with `executionTimeMs`/`timestamp`), error format (`errorCode`, `message`, `suggestion`), and pagination schema. This ensures consistent parsing, logging, and error handling across the entire tool surface.
- Q: Should `get_secure_score`, `get_defender_recommendations`, `get_policy_compliance` move from Security Agent to Compliance Agent? → A: No. Keep those 3 tools in the Security Agent (security posture focus). The Compliance Agent gets the 12 tools defined in compliance-tools.md (compliance posture focus). The tools serve different purposes despite overlapping data sources.
- Q: Should evidence collection append or replace existing evidence by default? → A: Append by default. Each collection creates new immutable records with fresh timestamps for audit trail integrity. `replace: true` is an explicit opt-in parameter for re-collection scenarios.

### Session 2026-02-22 (NistService + Accessibility)

- Q: Should the spec include Section 508 / WCAG 2.1 AA accessibility requirements for all user-facing interfaces? → A: Yes. All user-facing interfaces (Chat UI, Kanban board, Admin Dashboard) must conform to WCAG 2.1 AA. Federal legal requirement for government IT systems.
- Q: How should the NIST control catalog data be sourced and versioned? → A: Dual-source strategy. Fetch from the NIST OSCAL machine-readable catalog (JSON) on GitHub when online; fall back to embedded resource snapshot when offline. OSCAL JSON is the authoritative format in both cases.
- Q: How should the system handle subscriptions exceeding 5,000 resources? → A: Accept with a warning and best-effort SLA. Warn that scan may exceed 10 minutes, require explicit confirmation before proceeding, and stream progress throughout. No hard cap on resource count.
- Q: What secret management strategy should be used for production environments? → A: Azure Key Vault with managed identity. All secrets, connection strings, and API keys stored in Key Vault; application authenticates via managed identity (no credentials in config). FIPS 140-2 Level 2 validated, required for IL5/IL6.
- Q: Should the spec establish canonical terms for key concepts? → A: Yes. Use "assessment" (not "scan") for the overall compliance evaluation operation, and "finding" (not "violation") for individual results. Aligns with data model entities `ComplianceAssessment` and `ComplianceFinding`.

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Compliance Officer Runs an Assessment (Priority: P1)

A Compliance Officer opens the Chat UI and types "run a compliance assessment against NIST 800-53." The Orchestrator routes the message to the Compliance Agent. Because this operation queries live Azure resources, the system checks for an active CAC session and an active PIM elevation. If the officer is not authenticated, a prompt appears: "This operation requires CAC authentication and PIM elevation. Please insert your CAC/PIV card and authenticate, then activate your PIM role." After authentication and PIM activation the Compliance Agent executes a combined assessment (resource-based and policy-based), streaming progress as each control family is evaluated. When the assessment completes the officer sees a summary: total controls evaluated, passing, failing, and not applicable, grouped by control family with critical findings first. The officer then says "show me the AC failures" and the system recalls the prior assessment context and filters to Access Control findings.

**Why this priority**: Compliance assessment is the core value proposition of the entire platform. Without it, no other agent capability delivers meaningful outcomes.

**Independent Test**: Can be fully tested by sending a natural language message, verifying Orchestrator routing to the Compliance Agent, confirming CAC gate enforcement, and validating that a structured assessment summary is returned with correct grouping.

**Acceptance Scenarios**:

1. **Given** a Compliance Officer has an active CAC session, an active PIM elevation, and a configured subscription, **When** they type "run a compliance assessment," **Then** the Compliance Agent executes a combined assessment and returns results grouped by control family within 60 seconds.
2. **Given** a Compliance Officer has no active CAC session or no active PIM elevation, **When** they type "run a compliance assessment," **Then** the system prompts for CAC authentication and PIM elevation before proceeding.
3. **Given** an assessment has completed, **When** the officer types "show me the AC failures," **Then** the system filters the previous results to Access Control findings only.
4. **Given** an assessment takes longer than 10 seconds, **When** processing continues, **Then** progress messages stream to the chat showing which control family is currently being evaluated.

---

### User Story 2 — Platform Engineer Remediates a Finding (Priority: P2)

A Platform Engineer reviews assessment results and types "fix AC-2.1." Because remediation modifies live Azure resources, the system checks for an active CAC session and PIM elevation with write-tier privileges. If either is missing, the system prompts accordingly (e.g., "This operation requires CAC authentication and write-level PIM elevation"). Once authenticated and elevated, the Compliance Agent explains the proposed remediation, identifies affected resources, and presents a dry-run preview showing what will change. The engineer reviews the preview and types "apply this." Because AC-2 is a high-risk control family (Access Control), an extra confirmation appears: "⚠️ This affects access control. Are you sure?" The engineer confirms, and the remediation executes with progress updates. Upon completion the engineer sees a success summary with the list of resources modified.

**Why this priority**: Remediation is the natural follow-on to assessment. Without the ability to act on findings, the assessment is informational only.

**Independent Test**: Can be fully tested by triggering a remediation command, verifying CAC + PIM write-tier enforcement, verifying dry-run output, confirming high-risk warning for AC/IA/SC families, and validating that no changes are applied until explicit confirmation.

**Acceptance Scenarios**:

1. **Given** a Platform Engineer with active CAC session and write-tier PIM elevation, **When** they type "fix AC-2.1," **Then** the system presents a dry-run preview explaining what will change and which resources are affected.
2. **Given** a Platform Engineer without an active CAC session or without write-tier PIM elevation, **When** they type "fix AC-2.1," **Then** the system prompts for the missing authentication component (CAC, PIM, or both) before proceeding.
3. **Given** a dry-run preview is displayed, **When** the engineer types "apply this," **Then** the system shows a high-risk warning for AC-family controls and requires explicit confirmation.
4. **Given** the engineer confirms the high-risk warning, **When** remediation executes, **Then** progress updates stream and a success summary lists all modified resources.
5. **Given** a remediation fails mid-execution, **When** the failure occurs, **Then** the system stops immediately, explains the failure in plain language, offers rollback guidance, and logs the failure.

---

### User Story 3 — Orchestrator Routes Messages to Specialized Agents (Priority: P3)

A user types a natural language message in the Chat UI. The Orchestrator analyzes the intent and routes the message to the appropriate specialized agent. For example, "what's my secure score?" routes to the Security Agent, "show cost analysis for last 30 days" routes to the Cost Management Agent, and "explain NIST AC-2" routes to the Knowledge Base Agent. When the user explicitly targets an agent (e.g., using a prefix or addressing an agent by name), routing bypasses intent analysis and goes directly to the named agent. The response streams back in real-time with Markdown formatting.

**Why this priority**: The Orchestrator is the backbone that unifies all agents into a single conversational experience. Without correct routing, users cannot reach any agent reliably.

**Independent Test**: Can be fully tested by sending messages with varying intents and verifying that each is routed to the correct agent, including direct-targeting scenarios.

**Acceptance Scenarios**:

1. **Given** a user types "what's my secure score?", **When** the Orchestrator processes the message, **Then** the Security Agent handles the request.
2. **Given** a user types "explain NIST AC-2," **When** the Orchestrator processes the message, **Then** the Knowledge Base Agent handles the request without requiring CAC.
3. **Given** a user explicitly targets the Cost Management Agent, **When** the message is sent, **Then** routing bypasses intent analysis and goes directly to that agent.
4. **Given** a user types a message that no agent clearly handles, **When** the Orchestrator processes the message, **Then** the system responds with a helpful clarification indicating which agents are available and what they handle.

---

### User Story 4 — Auditor Reviews Compliance Evidence (Priority: P4)

An Auditor opens the Chat UI and types "collect evidence for AC-2." Because evidence collection queries live Azure resources, the system checks for an active CAC session and read-tier PIM elevation. If either is missing, the system prompts accordingly. Once authenticated and elevated, the Compliance Agent gathers configuration exports, policy snapshots, Defender for Cloud recommendations, activity logs, and resource inventories — all timestamped and packaged. The Auditor can also browse previously cached assessment results and generated compliance documents (SSP, SAR, POA&M) without authentication. If the Auditor attempts to execute a remediation, the system responds: "This operation requires Platform Engineer privileges with write-tier PIM elevation. Your current role: Auditor."

**Why this priority**: Audit readiness is a primary value driver for government customers; however, it depends on assessment data from P1 and the agent infrastructure from P3.

**Independent Test**: Can be fully tested by requesting evidence collection with an active CAC session and read-tier PIM elevation, verifying the evidence package contents, confirming that missing CAC or PIM prompts correctly, and confirming that unauthorized actions (e.g., remediation) are denied with a descriptive role-based message.

**Acceptance Scenarios**:

1. **Given** an Auditor with an active CAC session and read-tier PIM elevation, **When** they type "collect evidence for AC-2," **Then** the system returns a timestamped evidence package containing configuration exports, policy snapshots, Defender recommendations, activity logs, and resource inventories.
2. **Given** an Auditor without a CAC session or without PIM elevation, **When** they type "collect evidence for AC-2," **Then** the system prompts for the missing authentication component (CAC, PIM, or both) before proceeding.
3. **Given** an Auditor without a CAC session, **When** they browse previously cached assessment results, **Then** the results display without requiring authentication.
4. **Given** an Auditor, **When** they attempt to execute a remediation, **Then** the system returns a descriptive denial message stating the required role, required PIM tier (write), and the Auditor's current role.

---

### User Story 5 — Platform Engineer Configures Subscription Context (Priority: P5)

Before running any assessment, a Platform Engineer types "set my subscription to abc-123." The Configuration Agent stores this as the default subscription. The engineer then types "set default framework to FedRAMP High" and the Configuration Agent updates the compliance default used by all other agents. When the engineer later types "show my configuration," the current settings are displayed. If the engineer tries to run a compliance assessment without a configured subscription, the Compliance Agent responds: "No subscription configured. Run 'set subscription <id>' first."

**Why this priority**: Configuration is a prerequisite for all Azure-connected operations, but it is a simple settings operation that supports the higher-priority stories.

**Independent Test**: Can be fully tested by setting and retrieving configuration values, then verifying that other agents read the stored configuration correctly.

**Acceptance Scenarios**:

1. **Given** a new user session, **When** the engineer types "set my subscription to abc-123," **Then** the Configuration Agent stores the subscription and confirms the setting.
2. **Given** a configured subscription, **When** the engineer types "show my configuration," **Then** all current settings (subscription, cloud environment, default framework, baseline, default scan type, region, dry-run preference) are displayed.
3. **Given** no subscription is configured, **When** a user attempts a compliance assessment, **Then** the Compliance Agent returns an error message directing the user to set a subscription first.
4. **Given** a setting change, **When** the engineer types "set default framework to FedRAMP High," **Then** subsequent compliance assessments default to FedRAMP High without requiring the framework to be specified each time.

---

### User Story 6 — Knowledge Base Agent Answers Compliance Questions (Priority: P6)

A user types "What are the FedRAMP High requirements for access control?" The Knowledge Base Agent explains the requirements in plain language, maps them to relevant Azure services, and provides implementation guidance. The user then asks "Explain NIST AC-2" and receives a detailed breakdown with examples. All Knowledge Base interactions are local, use embedded reference data, and never require CAC authentication. The agent also handles STIG queries, framework comparisons, and ATO preparation guidance.

**Why this priority**: The Knowledge Base is a high-value feature that works independently of Azure connectivity and provides immediate utility, but it does not interact with live infrastructure.

**Independent Test**: Can be fully tested by querying compliance framework controls and verifying that responses include plain-language explanations, Azure service mappings, and implementation guidance — all without CAC.

**Acceptance Scenarios**:

1. **Given** a user types "What are the FedRAMP High requirements for access control?", **When** the Knowledge Base Agent processes the query, **Then** the response includes plain-language explanations with Azure service mappings.
2. **Given** a user types "Explain NIST AC-2," **When** the agent processes the query, **Then** a detailed breakdown is returned with control description, implementation examples, and related controls.
3. **Given** the user has no CAC session, **When** any Knowledge Base query is made, **Then** the response is returned without authentication prompts.

---

### User Story 7 — Infrastructure Agent Generates and Deploys Templates (Priority: P7)

A Platform Engineer types "Generate Bicep for an AKS cluster in usgovvirginia." The Infrastructure Agent supports three methods for producing compliant-by-default templates: (1) **Template Generator** (default) — a built-in generator that assembles templates from known-compliant resource patterns with security properties pre-configured; (2) **AI-generated** — the agent uses AI to create a template tailored to the request; (3) **Bicep ACR** — the agent pulls verified compliant modules from an Azure Container Registry. All methods produce templates with compliance annotations mapping each property to NIST controls (e.g., `// SC-8: Transmission Confidentiality`). Template generation is local and does not require authentication. The engineer then says "deploy this to rg-prod" and the agent previews the resources to be created, waits for confirmation, and executes the deployment — which requires CAC authentication and PIM elevation.

**Why this priority**: IaC generation extends the platform's value beyond assessment into proactive compliance, but it depends on the foundational agent infrastructure and configuration context.

**Independent Test**: Can be fully tested by requesting template generation and verifying output includes compliance annotations, then verifying that deployment flow enforces CAC + PIM and confirmation gates.

**Acceptance Scenarios**:

1. **Given** a Platform Engineer types "Generate Bicep for an AKS cluster in usgovvirginia," **When** the Infrastructure Agent processes the request, **Then** a Bicep template is returned with compliance annotations mapping properties to NIST controls.
2. **Given** no CAC session, **When** a user requests template generation, **Then** the template is generated locally without authentication prompts.
3. **Given** a generated template, **When** the engineer types "deploy this to rg-prod," **Then** the system checks for CAC and PIM elevation, previews resources, waits for confirmation, and deploys.
4. **Given** a deployment fails, **When** the failure occurs, **Then** the system explains the failure in plain language and offers troubleshooting guidance.

---

### User Story 8 — Cost Management Agent Analyzes Spending (Priority: P8)

A user types "Show cost analysis for last 30 days grouped by resource type." The Cost Management Agent queries Azure Cost Management (requires CAC + PIM), returns a formatted breakdown with totals, trends, and anomaly flags. The user then asks "how can I save money?" and receives optimization suggestions identifying idle resources, oversized capacity, unused disks, and reserved instance opportunities. Viewing cached cost reports does not require authentication.

**Why this priority**: Cost visibility is important for operational efficiency but is not a compliance-critical path. It can be built once the core agent infrastructure is established.

**Independent Test**: Can be fully tested by requesting cost analysis, validating the response format includes grouped breakdown, totals, and trends, and verifying cached reports are accessible without CAC.

**Acceptance Scenarios**:

1. **Given** an authenticated user, **When** they type "show cost analysis for last 30 days grouped by resource type," **Then** a formatted table is returned with totals, trends, and anomaly indicators.
2. **Given** cost data has been previously retrieved, **When** a user without CAC views cached cost reports, **Then** the cached data displays without authentication prompts.
3. **Given** an authenticated user types "how can I save money?", **When** the agent analyzes resource utilization, **Then** optimization suggestions are returned with estimated savings.

---

### User Story 9 — Compliance Officer Creates a Remediation Board (Priority: P9)

After an assessment reveals findings, the Compliance Officer types "create remediation board." The system generates a Kanban-style board with columns: Backlog, To Do, In Progress, In Review, Blocked, and Done. Each finding becomes a task card with an auto-generated ID, a title derived from the control, a severity badge, an assignee field, and an SLA-based due date (Critical: 24h, High: 7 days, Medium: 30 days, Low: 90 days). The officer can drag tasks between columns, assign them to team members, and add comments. Moving a task to "Done" triggers a validation scan against live Azure to confirm the fix (requires CAC + PIM). Viewing the board and adding comments does not require authentication.

**Why this priority**: Remediation tracking formalizes the workflow from finding to resolution, but it depends on assessment results and remediation capabilities from P1 and P2.

**Independent Test**: Can be fully tested by creating a board from assessment results, verifying task card properties (ID, title, severity, SLA dates), and confirming column transitions including validation triggers.

**Acceptance Scenarios**:

1. **Given** a completed assessment with findings, **When** the officer types "create remediation board," **Then** a Kanban board is generated with one task per finding, each having correct severity, SLA dates, and control-derived titles.
2. **Given** a board with tasks, **When** a user moves a task to "Blocked," **Then** the system requires a comment explaining the blocker before accepting the move.
3. **Given** a board with tasks, **When** a user moves a task to "Done," **Then** a validation scan executes against live Azure (requiring CAC + PIM) to confirm remediation.
4. **Given** a task's SLA expires, **When** the task is not acknowledged, **Then** the task is highlighted as overdue and auto-escalation occurs.

---

### User Story 10 — Compliance Monitoring Detects Drift (Priority: P10)

A Security Lead enables compliance monitoring and configures scheduled checks (default: hourly). The monitoring system detects compliance drift — such as HTTPS being disabled on a storage account, a NIST initiative being unassigned, or a secure score drop — and generates alerts classified by severity. Alerts show what changed, who changed it, which control is affected, and a recommended action. Related alerts within a 5-minute window are grouped to reduce noise. Alerts follow a lifecycle (New → Acknowledged → In Progress → Resolved/Dismissed) with auto-escalation if not acknowledged within SLA. Setting up monitoring requires CAC + PIM; viewing cached monitoring dashboards does not.

**Why this priority**: Continuous monitoring is a critical compliance capability, but it is operationally complex and depends on the assessment engine, agent infrastructure, and configuration context from higher-priority stories.

**Independent Test**: Can be fully tested by simulating a compliance drift event, verifying alert generation with correct classification, grouping, and lifecycle transitions.

**Acceptance Scenarios**:

1. **Given** monitoring is enabled with hourly scheduled checks, **When** a resource configuration changes to a non-compliant state, **Then** an alert is generated with severity classification, affected control, change author, and recommended action.
2. **Given** multiple changes occur on the same resource within 5 minutes, **When** alerts are generated, **Then** they are grouped into a single alert to reduce noise.
3. **Given** a Critical alert is generated, **When** it is not acknowledged within 1 hour, **Then** the alert auto-escalates to the next tier.
4. **Given** a user without a CAC session, **When** they view the monitoring dashboard, **Then** cached data displays without authentication prompts.

---

### User Story 11 — Admin Dashboard Provides Operational Oversight (Priority: P11)

An administrator opens the Admin Dashboard at port 5000 and sees an overview of service templates, environments, deployments, governance snapshots, and cost trends — all from cached data without requiring authentication. The admin navigates to service template management and performs CRUD operations on service templates (predefined IaC configurations for common Azure Government workloads). When the admin triggers a deployment from the dashboard, CAC authentication and PIM elevation are required. The dashboard communicates with the Admin API at port 5050.

**Why this priority**: The dashboard provides visual operational oversight but is not on the critical path for the conversational agent experience.

**Independent Test**: Can be fully tested by loading the dashboard, verifying cached data renders, performing service template CRUD, and confirming that deployment actions enforce CAC + PIM.

**Acceptance Scenarios**:

1. **Given** an administrator opens the Admin Dashboard, **When** the page loads, **Then** overview panels display cached service template, environment, deployment, and cost data without requiring CAC.
2. **Given** the admin navigates to service template management, **When** they create, update, or delete a service template, **Then** changes are persisted through the Admin API.
3. **Given** the admin triggers a deployment, **When** the action requires Azure access, **Then** CAC authentication and PIM elevation are enforced before proceeding.

---

### User Story 12 — GitHub Copilot Extension Provides In-Editor Compliance (Priority: P12)

A developer editing a Bicep file in VS Code uses the `@platform` chat participant. Typing `@platform is this file compliant?` triggers local compliance analysis (no CAC required) and returns inline hints showing which NIST controls pass or fail. Code lens annotations above resource definitions display compliance status. Typing `@platform /compliance run assessment` triggers a full assessment (requires CAC + PIM), with authentication prompted via VS Code notification if needed. Copilot auto-suggests compliant configurations during code completion.

**Why this priority**: The VS Code extension extends the platform into the developer's workflow, but it is secondary to the core Chat UI and agent platform.

**Independent Test**: Can be fully tested by opening a Bicep file, invoking the `@platform` participant, verifying local compliance hints render inline, and confirming that Azure operations prompt for CAC.

**Acceptance Scenarios**:

1. **Given** a developer has a Bicep file open, **When** they type `@platform is this file compliant?`, **Then** inline compliance hints appear without requiring CAC.
2. **Given** a developer types `@platform /compliance run assessment`, **When** no CAC session exists, **Then** a VS Code notification prompts for authentication.
3. **Given** code completion is active in a Bicep file, **When** resource properties are suggested, **Then** compliant defaults are prioritized (e.g., HTTPS enabled, TLS 1.2).

---

### User Story 13 — M365 Copilot Extension Enables Teams-Based Compliance (Priority: P13)

A Compliance Officer chats with the Platform Copilot bot in Microsoft Teams. The bot responds with Adaptive Cards containing action buttons (View Details, Create Tasks, Remediate). Critical alerts post proactively to the Security channel. A weekly compliance digest card summarizes the week. Azure operations from Teams require CAC authentication via Teams SSO combined with CAC certificate and PIM elevation. In Excel, compliance findings can be exported to spreadsheets. In Word, SSP sections and POA&M documents are generated following FedRAMP templates.

**Why this priority**: M365 integration extends reach across the organization but depends on the full agent platform and is the lowest priority for the initial phase.

**Independent Test**: Can be fully tested by sending a message to the Teams bot, verifying an Adaptive Card response is returned, and confirming that Azure operations enforce CAC + PIM via Teams SSO.

**Acceptance Scenarios**:

1. **Given** a user sends a message to the bot in Teams, **When** the Orchestrator processes it, **Then** the response is formatted as an Adaptive Card with action buttons.
2. **Given** a Critical compliance alert is generated, **When** Teams notifications are configured, **Then** the alert is posted to the designated Security channel.
3. **Given** a user triggers an Azure operation from Teams, **When** CAC authentication is required, **Then** authentication occurs via Teams SSO combined with CAC certificate validation.

---

### Edge Cases

- What happens when a user sends a message that matches multiple agents equally well? The Orchestrator MUST select the most relevant agent based on primary keywords and return a transparent routing explanation (e.g., "Routing to Compliance Agent based on 'assessment' keyword").
- How does the system handle Azure API rate limiting during a large scan? The Compliance Agent MUST implement exponential backoff, inform the user about the delay, and resume automatically.
- What happens when a CAC session or PIM elevation expires mid-remediation? The system MUST stop the current operation gracefully, preserve partial results in the session, prompt for re-authentication and/or PIM re-activation, and resume the operation after successful re-authentication.
- What happens when a user has CAC authentication but their PIM role elevation has expired? The system MUST detect the expired PIM elevation, prompt the user to re-activate their PIM role, and block the operation until elevation is confirmed. The system MUST NOT re-prompt for CAC if the CAC session is still valid.
- How does the system handle a subscription with thousands of resources? The assessment MUST stream progress, support pagination, and complete within the defined performance targets: ≤60 seconds for ≤500 resources, ≤5 minutes for ≤2,000 resources, ≤10 minutes for ≤5,000 resources. A progress indicator MUST show estimated time remaining.
- What happens when a remediation dry-run reveals no affected resources? The agent MUST inform the user that no resources match the finding criteria and suggest verifying the subscription or scope.
- How does the system handle concurrent users running assessments against the same subscription? Operations MUST be isolated per session with no cross-session data leakage.
- What happens when the Knowledge Base is queried for a control that does not exist? The agent MUST return a clear message that the control ID is not recognized, suggest similar controls, and indicate the frameworks currently supported.
- How does the system behave when the MCP Server is configured in stdio mode vs. HTTP mode? Both modes MUST expose identical tool capabilities; only the transport layer changes.
- What happens when a user's CAC maps to a role that does not exist in the system? Authentication succeeds but authorization fails with a descriptive message listing valid roles.
- What happens when the user says "fix all Access Control findings" but there are 200+ findings? The system MUST group by severity, display an estimated scope summary, request explicit batch confirmation, and execute sequentially with progress updates — never silently executing a large batch.
- What happens when a subscription has more than 5,000 resources? The system MUST warn the user that the assessment may exceed 10 minutes, display the estimated resource count, and require explicit confirmation before proceeding. Progress streaming with ETA MUST remain active throughout. There is no hard cap on resource count; the system operates on a best-effort SLA beyond the 5,000-resource tier.

## Requirements *(mandatory)*

### Functional Requirements

#### Multi-Agent Platform

- **FR-001**: System MUST provide an Orchestrator that analyzes natural language input and routes messages to the appropriate specialized agent based on intent.
- **FR-002**: System MUST support 8 specialized agents: Compliance, Infrastructure, Cost Management, Discovery, Environment, Knowledge Base, Configuration, and Security.
- **FR-003**: All agents MUST extend a common base agent abstraction with standardized identity, description, system prompt, and tool registration interfaces.
- **FR-004**: All tools MUST extend a common base tool abstraction with name, description, parameter schema, execution method, and an authentication-required flag.
- **FR-005**: System MUST support both intent-based routing (Orchestrator selects agent) and direct targeting (user explicitly names an agent).
- **FR-006**: System prompts for each agent MUST be externalized in separate prompt files, not embedded in code.
- **FR-007**: The MCP Server MUST operate in dual transport mode: HTTP for web clients and stdio for AI clients.

#### CAC Authentication & PIM Elevation

- **FR-008**: Operations that interact with live Azure resources MUST require both CAC/PIV authentication and an active PIM (Privileged Identity Management) role elevation before execution. CAC establishes identity; PIM provides just-in-time privileged access appropriate for IL5/IL6 environments.
- **FR-009**: Operations that are local or use cached data (Knowledge Base queries, viewing cached results, template generation, board viewing, commenting, configuration preferences) MUST NOT require authentication or PIM elevation.
- **FR-010**: Each tool MUST declare whether it requires authentication via a dedicated property. The MCP Server MUST enforce both CAC session validity and PIM elevation status server-side before executing any tool marked as requiring authentication.
- **FR-011**: When an unauthenticated user triggers a tool requiring authentication, the system MUST prompt for CAC authentication and PIM elevation with a clear, actionable message indicating which step is missing (CAC, PIM, or both).
- **FR-012**: CAC sessions MUST have a configurable timeout (default: 8 hours). PIM elevations MUST have a separate configurable timeout (default: 4 hours, maximum: 8 hours per Azure AD PIM policy). After either timeout, the next Azure operation MUST require re-authentication or re-elevation respectively.
- **FR-013**: The UI status bar MUST display current CAC session status with remaining time AND PIM elevation status with remaining time (e.g., "🔒 CAC: 6h 32m | PIM: 3h 15m" or "🔓 CAC: Not authenticated | PIM: Inactive").
- **FR-014**: If a CAC session or PIM elevation expires during an operation, the system MUST stop gracefully, preserve partial results, prompt for re-authentication or re-elevation (only the expired component), and resume after successful completion.
- **FR-015**: The system MUST support a development bypass mode (configurable via application settings) for local testing that disables both CAC and PIM enforcement while maintaining the authentication flow and enforcement points.
- **FR-016**: CAC certificate details and PIM elevation tokens MUST NOT be cached or exposed in logs, error messages, or chat responses.
- **FR-069**: Write operations (remediations, deployments, policy modifications) MUST require a PIM elevation with a higher privilege tier than read-only operations (assessments, discovery, cost queries). The system MUST distinguish between read-eligible and write-eligible PIM roles.
- **FR-070**: PIM elevation requests MUST support justification text. When a user activates a PIM role, the system MUST prompt for a business justification that is logged for audit purposes.
- **FR-071**: The system MUST check PIM role eligibility before prompting for activation. If a user is not eligible for the required PIM role, the system MUST return a descriptive message indicating the required role and how to request eligibility.

#### Role-Based Access Control

- **FR-017**: System MUST support four user roles: Compliance Officer, Platform Engineer, Security Lead, and Auditor. A user MAY hold multiple roles simultaneously; when multiple roles are assigned, the user receives the union of all role permissions (i.e., the highest privilege across all assigned roles applies).
- **FR-018**: Role MUST be derived from CAC identity, directory group membership, and active PIM role assignments. The Configuration Agent MUST store the CAC certificate mapping and track PIM elevation state.
- **FR-019**: Role determines which tools a user can execute, not which agents or tools are visible. All users MUST see all agents. PIM elevation tier (read vs. write) further constrains which operations are permitted within a role.
- **FR-020**: Unauthorized actions MUST return a descriptive message stating the required role, required PIM tier, and the user's current roles and elevation status.

#### Compliance Agent

- **FR-021**: System MUST support three scan types: resource-based (Azure Resource Graph), policy-based (Azure Policy), and combined (default).
- **FR-022**: System MUST support four compliance frameworks: NIST 800-53 Rev 5 (default), FedRAMP High, FedRAMP Moderate, and DoD IL5.
- **FR-023**: Assessment results MUST show total controls evaluated, passing, failing, and not applicable, grouped by control family with critical findings displayed first.
- **FR-024**: Remediation MUST default to dry-run mode. No changes MUST be applied until the user explicitly confirms.
- **FR-025**: High-risk control families (AC, IA, SC) MUST trigger an additional confirmation warning before remediation execution.
- **FR-026**: Batch remediation MUST group findings by severity, display scope estimates, require explicit confirmation, and execute sequentially with progress updates.
- **FR-027**: Evidence collection MUST package configuration exports, policy snapshots, Defender recommendations, activity logs, and resource inventories with timestamps. Evidence collection MUST default to **append** mode (each collection creates new immutable records). A `replace: true` parameter enables explicit opt-in replacement of existing evidence for the same control. Responses MUST include `previousEvidenceCount` when existing evidence is present.
- **FR-028**: Document generation MUST produce SSPs, SARs, and POA&Ms in Markdown following FedRAMP templates. Documents MUST NOT exceed 5MB; truncation with a note is required for oversized results.
- **FR-029**: The Compliance Agent MUST integrate with Defender for Cloud to pull recommendations and map them to NIST controls for assessment correlation. _(Note: The Security Agent separately exposes posture-focused Defender for Cloud tools — `get_secure_score`, `get_security_recommendations`, `manage_security_policy` — see FR-046. The Compliance Agent maps Defender recommendations to NIST controls; the Security Agent provides direct score and recommendation access.)_
- **FR-079**: All tool responses across all agents MUST conform to the standard response envelope schema: `{ status, data, metadata: { toolName, executionTimeMs, timestamp } }`. Error responses MUST use `{ errorCode, message, suggestion }`. Paginated responses MUST include `{ page, pageSize, totalItems, totalPages, hasNextPage }`. Individual responses MUST NOT exceed 1MB. See compliance-tools.md for the canonical envelope definition.

#### Infrastructure Agent

- **FR-030**: System MUST support three methods for generating compliant-by-default IaC templates (Bicep and Terraform): (a) Template Generator (default) — assembles templates from known-compliant resource patterns; (b) AI-generated — creates templates using AI tailored to the user's request; (c) Bicep ACR — pulls verified compliant modules from an Azure Container Registry. The default method MUST be Template Generator unless the user specifies otherwise.
- **FR-031**: Generated templates from all three methods MUST include inline comments mapping resource properties to NIST controls.
- **FR-032**: Template generation MUST be a local operation not requiring authentication. Deployment MUST require authentication.
- **FR-033**: Deployment MUST preview resources, require user confirmation, and provide progress updates.

#### Cost Management Agent

- **FR-034**: System MUST provide cost analysis with breakdown by resource type, totals, trends, and anomaly detection.
- **FR-035**: System MUST provide cost forecasting based on historical data and optimization suggestions (idle resources, oversized VMs, unused disks, reserved instances).
- **FR-036**: Live cost queries MUST require authentication. Cached cost reports MUST be accessible without authentication.

#### Discovery Agent

- **FR-037**: System MUST query Azure Resource Graph to inventory resources with health status.
- **FR-038**: System MUST support cross-subscription resource queries and dependency mapping.

#### Environment Agent

- **FR-039**: System MUST support environment cloning with proper naming conventions.
- **FR-040**: System MUST support drift detection between environments.

#### Knowledge Base Agent

- **FR-041**: System MUST provide plain-language explanations of compliance controls with Azure service mappings and implementation guidance.
- **FR-042**: System MUST support NIST 800-53, FedRAMP, DoD IL5, and STIG queries using embedded reference data, entirely offline with no authentication required. The embedded reference data is provided by the `NistService` (see FR-080).

#### Configuration Agent

- **FR-043**: System MUST store and manage user settings: default subscription, cloud environment, default compliance framework, baseline level (High/Moderate/Low), default scan type, default region, and dry-run preference. Settings are stored via `IAgentStateManager` shared state and consumed by all other agents. _(Mapping to configuration-tools.md sub-actions: `set_subscription` handles subscription; `set_framework` handles compliance framework; `set_baseline` handles baseline level; `set_preference` handles cloud environment, scan type, region, and dry-run preference. Subscription is **required** — all other settings have defaults: framework=NIST 800-53, baseline=High, scanType=combined.)_
- **FR-044**: Settings MUST be consumed by all other agents. Missing required settings (e.g., subscription) MUST produce a clear error directing the user to configure them.
- **FR-045**: Setting preferences MUST be a local operation. Validating a subscription against Azure MUST require authentication.

#### Security Agent

- **FR-046**: System MUST provide secure score retrieval, security recommendations, and security policy management through Defender for Cloud and Azure Security Center via three dedicated tools: `get_secure_score`, `get_security_recommendations`, and `manage_security_policy` (see mcp-tools.md Security Agent section).

#### Shared Services

- **FR-080**: System MUST provide a `NistService` (in `Platform.Engineering.Copilot.Core/Services/`) that loads, indexes, and queries the NIST 800-53 Rev 5 control catalog along with FedRAMP High, FedRAMP Moderate, and DoD IL5 overlays. The service MUST use a **dual-source strategy**: (1) attempt to fetch the authoritative NIST OSCAL machine-readable catalog (JSON) from the official NIST GitHub repository at startup or on-demand refresh, and (2) fall back to an embedded OSCAL snapshot when the GitHub source is unreachable (e.g., air-gapped IL5/IL6 environments). The OSCAL JSON format is authoritative in both cases. The service MUST expose: (a) lookup by control ID (e.g., `AC-2`, `AC-2(1)`), (b) lookup by control family (e.g., `AC`), (c) full-text search across control titles and descriptions, (d) framework comparison (which controls are shared/unique across frameworks), (e) baseline filtering (High/Moderate/Low), and (f) STIG mapping where applicable. The service is consumed by both the Compliance Agent (assessment mapping, control family details, remediation guidance) and the Knowledge Base Agent (explain_control, compare_frameworks, search_controls, get_stig_guidance). The service MUST log the active data source (GitHub fetch vs. embedded fallback) and the catalog version/date at startup.

#### Chat Interface

- **FR-047**: System MUST provide a conversational chat interface that maintains context within a session, enabling follow-up references to previous results.
- **FR-048**: Chat responses MUST be formatted in Markdown with tables, code blocks, collapsible sections, severity badges, and action buttons.
- **FR-049**: Responses MUST stream in real-time.

#### Remediation Kanban Board

- **FR-050**: System MUST generate a Kanban board from assessment findings with six columns: Backlog, To Do, In Progress, In Review, Blocked, Done.
- **FR-051**: Each task card MUST display: auto-generated ID (REM-###), control-derived title, severity badge (color-coded), assignee, comment count, and due date.
- **FR-052**: SLA-based due dates MUST be auto-set by severity: Critical 24h, High 7 days, Medium 30 days, Low 90 days. Overdue tasks MUST be highlighted.
- **FR-053**: Moving a task to "Blocked" MUST require a comment. Moving to "Done" MUST trigger a validation scan requiring authentication.
- **FR-054**: Users MUST be able to add unlimited comments to tasks. Users can edit or delete their own comments. Compliance Officers can delete any comment.
- **FR-055**: Tasks assigned to the current user MUST be visually distinguished.
- **FR-056**: Board viewing and commenting MUST NOT require authentication. Validation scans triggered by completing tasks MUST require authentication.

#### Compliance Monitoring

- **FR-057**: System MUST support three monitoring modes: scheduled (default hourly), event-driven (5-minute latency), and continuous stream (60-second latency).
- **FR-058**: System MUST detect four drift categories: baseline drift, policy drift, compliance state drift, and secure score drops.
- **FR-059**: Alerts MUST be classified as Critical (1h SLA), High (4h), Medium (24h), or Low (7 days) and follow a lifecycle: New → Acknowledged → In Progress → Resolved/Dismissed.
- **FR-060**: Related alerts within a 5-minute window MUST be grouped to prevent noise.
- **FR-061**: Alerts not acknowledged within SLA MUST auto-escalate.

#### Admin Dashboard

- **FR-062**: System MUST provide a web-based admin dashboard for service template management (CRUD with Git sync), environment monitoring, deployment tracking, governance snapshots, and cost overview. Service templates are predefined IaC configurations for common Azure Government workloads.
- **FR-063**: Dashboard viewing MUST use cached data and NOT require authentication. Admin operations modifying Azure resources MUST require authentication.

#### Extensions (Scaffolding Only for Initial Phase)

- **FR-064**: System MUST include project scaffolding for a GitHub Copilot Chat participant (`@platform`) supporting inline compliance checking (local, no authentication), assessment commands (authentication required), and compliant code suggestions.
- **FR-065**: System MUST include project scaffolding for an M365 Copilot extension supporting Teams bot with Adaptive Cards, proactive notifications, and Excel/Word integration.

#### Accessibility

- **FR-081**: All user-facing interfaces (Chat UI, Remediation Kanban Board, Admin Dashboard) MUST conform to WCAG 2.1 Level AA. This includes: (a) full keyboard navigation for all interactive elements, (b) screen reader compatibility with ARIA landmarks, roles, and live regions for streaming chat content, (c) minimum 4.5:1 color contrast ratio for normal text and 3:1 for large text, (d) visible focus indicators on all interactive elements, (e) text alternatives for all non-text content including severity badges and status indicators, and (f) no reliance on color alone to convey information (e.g., severity must also use icons or text labels). Section 508 compliance is a federal legal requirement for government IT systems.

#### Audit & Error Handling

- **FR-066**: Every agent action MUST be audit-logged with: who (user identity), what (action performed), when (timestamp), which resources (affected resources), and what outcome (success/failure).
- **FR-067**: Azure API failures MUST be explained in plain language with troubleshooting suggestions and retry options. Raw exceptions MUST NOT be shown to users.
- **FR-068**: Failed remediations MUST stop immediately, describe the failure, offer rollback guidance, and be audit-logged.

#### Secret Management

- **FR-082**: In production environments, all secrets, connection strings, API keys, and Azure credentials MUST be stored in Azure Key Vault. The application MUST authenticate to Key Vault using managed identity (no credentials in application configuration or environment variables). Key Vault MUST be FIPS 140-2 Level 2 validated per IL5/IL6 requirements. For local development, environment variables via `.env` files are permitted as a convenience fallback when Key Vault is not available.

#### Data Retention

- **FR-072**: Assessment results, evidence packages, and compliance documents MUST be retained for a minimum of 3 years from creation date.
- **FR-073**: Audit log entries MUST be retained for a minimum of 7 years from creation date and MUST be immutable (append-only; no modification or deletion permitted).
- **FR-074**: The system MUST support configurable retention policies per data category. Retention defaults (3 years assessments, 7 years audit logs) MUST apply unless overridden by organizational policy.

#### Observability

- **FR-075**: System MUST expose a health check endpoint (`/health`) that reports overall system status and per-agent availability (healthy, degraded, unavailable).
- **FR-076**: System MUST emit structured metrics for each agent and tool invocation including: request latency (p50, p95, p99), error rate, throughput (requests per minute), and active session count.
- **FR-077**: System MUST propagate a correlation ID across all agent calls within a single user request, enabling distributed tracing from Orchestrator routing through agent execution to tool invocation and Azure API calls.
- **FR-078**: All structured logs MUST include the correlation ID, agent name, tool name, user identity (redacted as needed), and timestamp. Logs MUST follow the structured logging format defined in the constitution (Principle V).

### Key Entities

- **User**: Identity derived from CAC certificate, associated roles (one or more of: Compliance Officer, Platform Engineer, Security Lead, Auditor; permissions are the union of all assigned roles), default configuration settings (subscription, cloud environment, framework), session state including CAC expiration time.
- **Agent**: A specialized AI assistant with unique identity, name, description, system prompt, and registered tools. Each agent handles a specific domain (compliance, infrastructure, cost, etc.).
- **Tool**: An executable capability registered to an agent. Has a name, description, parameter schema, and an authentication-required flag. Tools are the lowest-level units of work.
- **Assessment**: A compliance evaluation result containing scan type (resource/policy/combined), framework used, timestamp, summary scores, and a collection of findings grouped by control family. Retained for a minimum of 3 years.
- **Finding**: A single compliance finding or observation tied to a specific control, resource, severity level, and remediation guidance.
- **Remediation Task**: A work item on the Kanban board derived from a finding. Has an ID, title, severity, assignee, status (column), SLA-based due date, and comments.
- **Evidence Package**: A timestamped, immutable collection of configuration exports, policy snapshots, Defender recommendations, activity logs, and resource inventories assembled for a specific control. Evidence collection defaults to append mode (new records created on each collection); replace mode is explicit opt-in.
- **Compliance Document**: A generated Markdown document (SSP, SAR, or POA&M) following FedRAMP templates, linked to assessment data.
- **Alert**: A monitoring notification triggered by compliance drift. Has a severity classification, lifecycle state, affected control, change author, grouping key, and recommended action.
- **Configuration**: A user's stored settings including default subscription, cloud environment, default framework, baseline level, default scan type, region, dry-run preference, CAC certificate mapping, and PIM role eligibility cache. Managed via `IAgentStateManager` shared state with `config:` key prefix.
- **IaC Template**: A Bicep or Terraform template with compliance annotations, generated by the Infrastructure Agent for deployment or local use.
- **Audit Log Entry**: An immutable record of an agent action containing user identity, action type, timestamp, affected resources, and outcome. Retained for a minimum of 7 years (immutable, append-only).
- **Control Catalog**: The NIST 800-53 Rev 5 control definitions with FedRAMP High/Moderate and DoD IL5 overlays, loaded as embedded structured JSON. Managed by `NistService`. Includes control ID, title, family, description, implementation guidance, Azure service mappings, STIG references, and baseline applicability (High/Moderate/Low).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can complete a full compliance assessment (combined scan) against a configured subscription within 60 seconds for environments with up to 500 resources, within 5 minutes for up to 2,000 resources, and within 10 minutes for up to 5,000 resources. For subscriptions exceeding 5,000 resources, the system operates on a best-effort basis with mandatory progress streaming and user confirmation before starting. Progress streaming with ETA MUST be active for all scans exceeding 10 seconds.
- **SC-002**: The Orchestrator correctly routes natural language messages to the intended agent at least 90% of the time without user intervention.
- **SC-003**: 100% of tools that interact with live Azure resources enforce both CAC authentication and PIM elevation server-side; no unauthenticated or un-elevated Azure operations can succeed.
- **SC-004**: Users can remediate a single finding (from "fix" command through dry-run, confirmation, and execution) in under 3 minutes.
- **SC-005**: All four user roles can perform their permitted operations and are denied unauthorized operations with descriptive error messages 100% of the time.
- **SC-006**: Chat interface maintains conversational context across at least 10 sequential follow-up messages within a session.
- **SC-007**: Evidence packages contain all required artifact types (config exports, policy snapshots, Defender recommendations, activity logs, resource inventories) with valid timestamps.
- **SC-008**: The Knowledge Base Agent answers compliance framework queries without requiring any Azure connectivity or authentication.
- **SC-009**: Generated IaC templates include compliance annotations mapping at least 80% of security-relevant properties to their corresponding NIST controls.
- **SC-010**: Compliance monitoring detects simulated drift events and generates correctly classified alerts within the configured latency window (hourly for scheduled, 5 minutes for event-driven, 60 seconds for continuous).
- **SC-011**: The Remediation Kanban board auto-assigns SLA-correct due dates for all severity levels and correctly highlights overdue tasks.
- **SC-012**: All agent actions produce audit log entries with complete identity, timestamp, resource, and outcome data — verified by audit log query.
- **SC-013**: The `/health` endpoint returns agent-level status within 2 seconds, and all agent invocations emit structured metrics with correlation IDs that can be traced end-to-end from Orchestrator to tool execution.

## Assumptions

- Azure Government subscriptions with appropriate permissions are available for testing resource-based and policy-based scans.
- CAC/PIV authentication infrastructure and Azure AD PIM are available in the target IL5/IL6 environment. For development, the configurable bypass mode (RequireCac: false, RequirePim: false) will be used.
- PIM role definitions for the platform are pre-configured in Azure AD with appropriate eligibility assignments for each user persona. Read-eligible and write-eligible tiers are defined.
- Defender for Cloud is enabled on test subscriptions to support secure score retrieval and recommendation mapping.
- Azure Policy initiatives for NIST 800-53 and FedRAMP are assigned in test environments.
- The NIST 800-53 Rev 5, FedRAMP High/Moderate, and DoD IL5 control catalogs are available as structured reference data for the Knowledge Base Agent.
- Users have modern browsers supporting WebSocket connections for real-time chat streaming.
- Docker and docker-compose are available for local development and deployment.
- Azure Key Vault is provisioned in the target Azure Government environment with managed identity access configured for the application's service principal. FIPS 140-2 Level 2 validation is enabled.
- The standard SLA windows (Critical: 24h, High: 7d, Medium: 30d, Low: 90d for remediation; Critical: 1h, High: 4h, Medium: 24h, Low: 7d for monitoring alerts) are acceptable defaults per organizational policy.
