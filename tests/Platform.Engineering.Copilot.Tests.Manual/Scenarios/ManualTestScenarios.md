# Manual Test Scenarios — Platform Engineering Copilot

> T146 — Verification steps aligned with quickstart.md for validating end-to-end functionality.

## Prerequisites

- .NET 9.0 SDK installed
- Docker Desktop running
- Terminal / PowerShell available
- CAC/PIV reader connected (or `DEV_BYPASS=true` for local dev)

---

## Scenario 1: Health Check Verification

**Objective**: Confirm MCP server and Admin API start and report healthy status.

### Steps

1. Start the MCP server:
   ```bash
   cd src/Platform.Engineering.Copilot.Mcp
   dotnet run
   ```
2. Open a new terminal. Verify MCP health:
   ```bash
   curl http://localhost:5000/health
   ```
3. **Expected**: JSON response with `status: "Healthy"`, all services listed.

4. Start the Admin API:
   ```bash
   cd src/Platform.Engineering.Copilot.Admin.API
   dotnet run
   ```
5. Verify Admin API health:
   ```bash
   curl http://localhost:5050/api/health
   ```
6. **Expected**: JSON with `status: "Healthy"`, 8 agents listed, `version: "1.0.0"`.

| Check | Expected | Pass? |
|-------|----------|-------|
| MCP server starts without errors | No exceptions in console | ☐ |
| MCP /health returns 200 | HTTP 200, status "Healthy" | ☐ |
| Admin API starts without errors | No exceptions in console | ☐ |
| Admin /api/health returns 200 | HTTP 200, 8 agents "Healthy" | ☐ |

---

## Scenario 2: MCP Tool List Verification

**Objective**: Confirm all 52 MCP tools are registered and listable.

### Steps

1. With MCP server running, request tool list:
   ```bash
   curl -X POST http://localhost:5000/mcp \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}'
   ```
2. **Expected**: JSON-RPC response with `result.tools` array.
3. Count the tools:
   ```bash
   curl -s -X POST http://localhost:5000/mcp \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' \
     | python3 -c "import sys,json; print(len(json.load(sys.stdin)['result']['tools']))"
   ```
4. **Expected**: 52 tools.

| Check | Expected | Pass? |
|-------|----------|-------|
| tools/list returns 200 | JSON-RPC response | ☐ |
| Tool count = 52 | Exactly 52 tools | ☐ |
| All 8 agent tool sets present | compliance, config, infra, kb, cost, discovery, env, security | ☐ |

---

## Scenario 3: Knowledge Base Query

**Objective**: Verify NIST 800-53 data is accessible through KnowledgeBase agent tools.

### Steps

1. With MCP server running, invoke the KB search tool:
   ```bash
   curl -X POST http://localhost:5000/mcp \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"kb_search_controls","arguments":{"query":"access control","framework":"NIST80053"}}}'
   ```
2. **Expected**: JSON result with matching NIST 800-53 Rev5 controls (e.g., AC-1, AC-2).

3. Query a specific control:
   ```bash
   curl -X POST http://localhost:5000/mcp \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"kb_get_control","arguments":{"controlId":"AC-2","framework":"NIST80053"}}}'
   ```
4. **Expected**: Full control definition for AC-2 with title, family, baselines.

| Check | Expected | Pass? |
|-------|----------|-------|
| kb_search_controls returns results | Non-empty results array | ☐ |
| Results include AC-family controls | AC-1, AC-2 in results | ☐ |
| kb_get_control returns AC-2 | Control with title, family, baselines | ☐ |
| FedRAMP baseline info present | High/Moderate applicability shown | ☐ |

---

## Scenario 4: Chat Session via SignalR

**Objective**: Validate real-time chat with message routing, context retention, and streaming.

### Steps

1. Start the Chat project:
   ```bash
   cd src/Platform.Engineering.Copilot.Chat
   dotnet run
   ```
2. Open browser to `http://localhost:5100`.
3. Verify the chat UI loads with:
   - Auth status bar (CAC and PIM indicators)
   - Message input area
   - Quick action buttons
4. Type "What is my secure score?" and press Enter.
5. **Expected**: Message appears in chat, response streams back with token-by-token rendering.

6. Type "Show compliance status for FedRAMP High" and press Enter.
7. **Expected**: Response includes compliance assessment with framework-specific controls.

8. Verify session context: Type "Tell me more about the first finding" (refers to previous response).
9. **Expected**: Response references the prior compliance result — confirms ≥10 message retention (SC-006).

10. Click the "Compliance Assessment" quick action button.
11. **Expected**: Pre-filled query is sent and processed.

| Check | Expected | Pass? |
|-------|----------|-------|
| Chat UI loads at /chat | All UI elements visible | ☐ |
| Message sends successfully | User message appears in thread | ☐ |
| Response streams back | Tokens appear incrementally | ☐ |
| Session context retained | Follow-up references prior messages | ☐ |
| Quick actions work | Pre-filled queries process correctly | ☐ |
| WCAG 2.1 AA | Keyboard nav works, contrast ≥ 4.5:1 | ☐ |

---

## Scenario 5: Admin API CRUD Operations

**Objective**: Verify template management, environment listing, and deployment creation.

### Steps

1. With Admin API running, list templates:
   ```bash
   curl http://localhost:5050/api/templates
   ```
2. **Expected**: JSON with `templates` array (3 seed templates) and `totalCount: 3`.

3. Create a new template:
   ```bash
   curl -X POST http://localhost:5050/api/templates \
     -H "Content-Type: application/json" \
     -d '{"name":"Test Template","description":"For testing","category":"Compute","version":"1.0.0"}'
   ```
4. **Expected**: 201 Created with new template object including `templateId`.

5. List environments:
   ```bash
   curl http://localhost:5050/api/environments
   ```
6. **Expected**: 3 environments (Production, Staging, Development) with compliance scores.

7. Create a deployment:
   ```bash
   curl -X POST http://localhost:5050/api/deployments \
     -H "Content-Type: application/json" \
     -d '{"templateId":"tmpl-001","environmentId":"env-001"}'
   ```
8. **Expected**: 202 Accepted with `status: "InProgress"`.

9. Get governance snapshot:
   ```bash
   curl http://localhost:5050/api/governance/snapshots
   ```
10. **Expected**: Snapshot with `overallComplianceScore`, frameworks, environment scores, critical findings.

11. Get cost summary:
    ```bash
    curl http://localhost:5050/api/costs/summary
    ```
12. **Expected**: Current month cost, previous month, change percent, top resources, by-environment breakdown.

| Check | Expected | Pass? |
|-------|----------|-------|
| GET /api/templates returns 200 | 3 seed templates | ☐ |
| POST /api/templates returns 201 | New template with ID | ☐ |
| PUT /api/templates/{id} returns 200 | Updated template | ☐ |
| DELETE /api/templates/{id} returns 204 | No content | ☐ |
| GET /api/environments returns 200 | 3 environments | ☐ |
| POST /api/deployments returns 202 | InProgress status | ☐ |
| GET /api/governance/snapshots returns 200 | Compliance data | ☐ |
| GET /api/costs/summary returns 200 | Cost breakdown | ☐ |

---

## Scenario 6: Docker Compose Full Stack

**Objective**: Validate all services start together via Docker Compose.

### Steps

1. From repo root:
   ```bash
   docker compose up --build -d
   ```
2. Wait for all services to start (check with `docker compose ps`).
3. Run health checks:
   ```bash
   curl http://localhost:5000/health    # MCP server
   curl http://localhost:5050/api/health # Admin API
   curl http://localhost:5100           # Chat UI
   ```
4. **Expected**: All three services respond with healthy status.

5. Run MCP tool list to verify full registration:
   ```bash
   curl -s -X POST http://localhost:5000/mcp \
     -H "Content-Type: application/json" \
     -d '{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}' | python3 -c "import sys,json; print(len(json.load(sys.stdin)['result']['tools']))"
   ```
6. **Expected**: 52 tools.

7. Tear down:
   ```bash
   docker compose down
   ```

| Check | Expected | Pass? |
|-------|----------|-------|
| docker compose up succeeds | All containers start | ☐ |
| MCP server healthy | /health returns 200 | ☐ |
| Admin API healthy | /api/health returns 200 | ☐ |
| Chat UI accessible | / returns HTML | ☐ |
| docker compose down clean | All containers removed | ☐ |

---

## Summary Checklist

| Scenario | Description | Status |
|----------|-------------|--------|
| 1 | Health Check | ☐ |
| 2 | MCP Tool List (52 tools) | ☐ |
| 3 | Knowledge Base Query | ☐ |
| 4 | Chat Session (SignalR) | ☐ |
| 5 | Admin API CRUD | ☐ |
| 6 | Docker Compose Full Stack | ☐ |
