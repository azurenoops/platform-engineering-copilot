# SignalR Hub Contract

**Branch**: `001-platform-copilot-core` | **Date**: 2026-02-22

The Chat UI (port 5001) communicates with the backend via a SignalR hub for real-time bidirectional messaging.

## Connection

| Property | Value |
|----------|-------|
| Hub URL | `ws://localhost:5001/chathub` |
| Transport | WebSocket (primary), Server-Sent Events (fallback) |
| Auth | Bearer JWT in query string or header |
| Reconnection | Automatic with exponential backoff |

---

## Server → Client Methods

### `ReceiveMessage`

Delivers a complete message from an agent.

```json
{
  "messageId": "guid",
  "agentId": "compliance",
  "agentName": "Compliance Agent",
  "content": "## Assessment Complete\n\n| Family | Pass | Fail |\n|...",
  "role": "Assistant",
  "correlationId": "guid",
  "timestamp": "2026-02-22T10:30:00Z"
}
```

### `StreamToken`

Streams individual tokens for progressive rendering.

```json
{
  "correlationId": "guid",
  "token": "The ",
  "isComplete": false
}
```

When streaming is complete:
```json
{
  "correlationId": "guid",
  "token": "",
  "isComplete": true,
  "fullMessageId": "guid"
}
```

### `ProgressUpdate`

Reports progress for long-running operations (scans >10s per SC-001).

```json
{
  "correlationId": "guid",
  "phase": "Scanning control family AC (Access Control)",
  "currentStep": 3,
  "totalSteps": 18,
  "percentComplete": 17,
  "estimatedTimeRemaining": "00:02:34",
  "findingsCount": 4,
  "status": "Running"
}
```

Status values: `Running`, `Completed`, `Failed`, `Cancelled`

### `AuthRequired`

Prompts client for authentication when a tool requires CAC/PIM.

```json
{
  "correlationId": "guid",
  "requiredComponents": ["CAC", "PIM"],
  "pimTier": "Write",
  "message": "This operation requires CAC authentication and write-level PIM elevation.",
  "returnAction": "remediate_finding"
}
```

### `SessionStatus`

Periodic update of session authentication state.

```json
{
  "cacStatus": "Active",
  "cacRemainingMinutes": 392,
  "pimStatus": "Active",
  "pimTier": "Read",
  "pimRemainingMinutes": 195,
  "roles": ["ComplianceOfficer", "SecurityLead"]
}
```

### `ErrorNotification`

Reports errors in plain language (FR-067).

```json
{
  "correlationId": "guid",
  "code": "AZURE_API_ERROR",
  "message": "Unable to query Azure Policy. The subscription may not have the NIST 800-53 initiative assigned.",
  "troubleshooting": "Verify policy assignment: az policy assignment list --subscription <id>",
  "retryable": true
}
```

---

## Client → Server Methods

### `SendMessage`

Sends a user message to the orchestrator.

```json
{
  "conversationId": "guid | null",
  "content": "run a compliance assessment against NIST 800-53",
  "targetAgentId": "compliance | null"
}
```

- `conversationId`: null for new conversation
- `targetAgentId`: null for intent-based routing; set for direct targeting (FR-005)

**Returns**: `correlationId` (guid) — used to match streaming responses

### `ConfirmAction`

User confirms a pending action (e.g., remediation execution, high-risk acknowledgment).

```json
{
  "correlationId": "guid",
  "confirmed": true,
  "justification": "Approved remediation for quarterly audit preparation"
}
```

### `CancelAction`

User cancels a pending or running action.

```json
{
  "correlationId": "guid"
}
```

### `UpdateAuth`

Client notifies server of authentication state change.

```json
{
  "cacToken": "Bearer eyJ...",
  "pimActivated": true,
  "pimTier": "Write",
  "pimJustification": "Remediation of AC-2 findings from Q1 assessment"
}
```

---

## Connection Lifecycle

```
Client                          Server
  |                               |
  |--- negotiate (HTTP) -------->|
  |<-- connectionId, transport --|
  |                               |
  |--- connect (WebSocket) ----->|
  |<-- SessionStatus -----------|  (initial auth state)
  |                               |
  |--- SendMessage ------------->|
  |<-- StreamToken (n times) ----|  (progressive response)
  |<-- ProgressUpdate (if long) -|
  |<-- ReceiveMessage -----------|  (final complete message)
  |                               |
  |--- SendMessage ------------->|  (follow-up, same conversation)
  |<-- AuthRequired -------------|  (if tool needs CAC/PIM)
  |--- UpdateAuth -------------->|  (user authenticates)
  |<-- StreamToken ... ----------|  (operation proceeds)
  |                               |
```
