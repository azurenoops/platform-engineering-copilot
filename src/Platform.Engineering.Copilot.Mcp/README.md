# Platform Engineering Copilot - MCP Server

This is the **Model Context Protocol (MCP)** server that exposes the Platform Engineering Copilot's multi-agent orchestrator to external AI tools and web applications.

## Dual-Mode Operation

The MCP server supports **TWO modes** of operation:

### 1. **Stdio Mode** (for external AI tools)
Used by GitHub Copilot, Claude Desktop, Cline, and other MCP-compatible AI assistants.

**How it works:**
- AI tool spawns MCP server as subprocess
- Communicates via JSONRPC 2.0 over stdin/stdout
- Standard MCP protocol

**Usage:**
```bash
# Run in stdio mode (default)
dotnet run --project src/Platform.Engineering.Copilot.Mcp

# Or explicitly
dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --stdio
```

**GitHub Copilot Configuration** (`~/.copilot/config.json`):
```json
{
  "mcpServers": {
    "platform-engineering-copilot": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/Users/johnspinella/repos/platform-engineering-copilot/src/Platform.Engineering.Copilot.Mcp"
      ]
    }
  }
}
```

**Claude Desktop Configuration** (`~/Library/Application Support/Claude/claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "platform-engineering-copilot": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/Users/johnspinella/repos/platform-engineering-copilot/src/Platform.Engineering.Copilot.Mcp"
      ]
    }
  }
}
```

### 2. **HTTP Mode** (for web applications)
Used by the Chat web app and other HTTP clients.

**How it works:**
- MCP server exposes REST API endpoints
- HTTP bridge converts HTTP requests to MCP tool calls
- Compatible with existing Chat app

**Usage:**
```bash
# Run in HTTP mode on default port (5100)
dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http

# Run on custom port
dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http --port 8080
```

**Endpoints:**
- `POST /api/chat/intelligent-query` - Process chat message through orchestrator
- `GET /api/chat/history/{conversationId}` - Get conversation history
- `GET /api/chat/suggestions/{conversationId}` - Get proactive suggestions
- `GET /health` - Health check

**Example Request:**
```bash
curl -X POST http://localhost:5100/api/chat/intelligent-query \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Generate a Bicep template for an AKS cluster",
    "conversationId": "123e4567-e89b-12d3-a456-426614174000"
  }'
```

## Architecture

```
External AI Tools (GitHub Copilot, Claude, Cline)
  ↓
  ↓ stdio (JSONRPC over stdin/stdout)
  ↓
┌─────────────────────────────────────┐
│     MCP Server (Dual-Mode)          │
│  ┌───────────┐     ┌─────────────┐ │
│  │  Stdio    │     │ HTTP Bridge │ │
│  │  Listener │     │             │ │
│  └─────┬─────┘     └──────┬──────┘ │
│        │                  │         │
│        └────────┬─────────┘         │
│                 ↓                   │
│   PlatformEngineeringCopilotTools   │
│                 ↓                   │
│        Core (Orchestrator)          │
│                 ↓                   │
│   6 Specialized Agents              │
│   - Infrastructure                  │
│   - Compliance                      │
│   - CostManagement                  │
│   - Discovery                       │
│   - Environment                     │
│   - Onboarding                      │
└─────────────────────────────────────┘
  ↑
  ↑ HTTP (REST endpoints)
  ↑
Chat Web App
```

## Multi-Agent Orchestration

The MCP server exposes the **full multi-agent orchestrator**, preserving all intelligence:

1. **Intent Analysis**: Determines user's goal
2. **Agent Selection**: Chooses which specialized agents to use
3. **Execution Planning**: Coordinates agents (sequential, parallel, collaborative)
4. **Response Synthesis**: Combines agent outputs into coherent response

### Optimizations

The orchestrator includes several optimizations for efficiency (40-60% LLM reduction):

- **Skip Synthesis for Single-Agent**: If only one agent responds, skip synthesis LLM call (33% savings)
- **Reduced Planning Prompt**: Simplified prompt from 300 lines to 20 lines (70% token savings)
- **Agent-Neutral Fast-Path**: Detects unambiguous single-agent requests for ALL 6 agents, skips planning (60% savings)
- **Execution Plan Caching**: Semantic hashing with 15min TTL, matches similar requests (50% savings for repeated patterns)

### Available Agents

1. **InfrastructureAgent**: Generate IaC templates (Bicep/Terraform)
2. **ComplianceAgent**: Scan resources for NIST/FedRAMP compliance
3. **CostManagementAgent**: Analyze and estimate costs
4. **DiscoveryAgent**: Inventory resources, monitor health
5. **EnvironmentAgent**: Manage environment lifecycle, cloning
6. **OnboardingAgent**: Onboard missions and teams

## Configuration

The MCP server requires configuration for:
- **Azure OpenAI**: LLM endpoints for orchestrator and agents
- **Database**: SQL Server connection for conversation state
- **GitHub**: Access token for GitHub integration
- **NIST Controls**: OSCAL content URLs for compliance

See `appsettings.json` for details. Environment variables can override settings:

```bash
export AZURE_OPENAI_API_KEY="your-key"
export AZURE_OPENAI_ENDPOINT="https://mcp-ai.openai.azure.us/"
export GITHUB_TOKEN="your-github-token"
```

## Development

### Build
```bash
dotnet build src/Platform.Engineering.Copilot.Mcp
```

### Run Tests
```bash
# Test stdio mode
dotnet run --project src/Platform.Engineering.Copilot.Mcp
# Send test message via stdin (Ctrl+D to submit)

# Test HTTP mode
dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http
curl http://localhost:5100/health
```

### Docker

The MCP server can run in both modes via Docker:

**Stdio mode** (for external AI tools to spawn):
```dockerfile
# External tools spawn the container directly
# No port exposure needed
```

**HTTP mode** (for Chat web app):
```yaml
# docker-compose.yml
services:
  platform-mcp:
    build: ./src/Platform.Engineering.Copilot.Mcp
    command: ["--http", "--port", "5100"]
    ports:
      - "5100:5100"
    environment:
      - AZURE_OPENAI_API_KEY=${AZURE_OPENAI_API_KEY}
      - GITHUB_TOKEN=${GITHUB_TOKEN}
    depends_on:
      - sqlserver
```

## Troubleshooting

### Stdio Mode Issues

**Problem**: GitHub Copilot can't connect to MCP server  
**Solution**: Check logs in stderr (MCP writes logs to stderr, not stdout)

**Problem**: "Tool not found" errors  
**Solution**: Verify `PlatformEngineeringCopilotTools` is registered in DI

### HTTP Mode Issues

**Problem**: "Connection refused" on port 5100  
**Solution**: 
1. Check if port is already in use: `lsof -i :5100`
2. Try custom port: `dotnet run -- --http --port 8080`

**Problem**: CORS errors from Chat web app  
**Solution**: Add CORS configuration to `RunHttpModeAsync()` in `Program.cs`

## Migration from API Project

The MCP server **replaces** the `Platform.Engineering.Copilot.API` project:

**Before:**
```
Chat → HTTP → API (localhost:7001) → Core
```

**After:**
```
Chat → HTTP → MCP (localhost:5100) → Core
GitHub Copilot → stdio → MCP → Core
```

**Benefits:**
- Single entry point for both web and AI tools
- Eliminates duplicate API layer
- Direct access to multi-agent orchestrator
- Preserves full intelligence (no simplified wrappers)

## Next Steps

1. **Update Chat App**: Change `appsettings.json` to use MCP HTTP endpoints
2. **Test Integration**: Verify both stdio and HTTP modes work
3. **Remove API Project**: Delete `Platform.Engineering.Copilot.API` after migration
4. **Update docker-compose**: Switch from `platform-api` to `platform-mcp`

## Learn More

- [MCP Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Copilot MCP Integration](https://docs.github.com/copilot/customizing-copilot/using-mcp-servers)
- [Platform Engineering Copilot Architecture](../../docs/ARCHITECTURE.md)
