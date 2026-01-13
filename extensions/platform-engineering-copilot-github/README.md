# Platform Engineering Copilot - GitHub Copilot Extension

A VS Code extension that integrates with GitHub Copilot Chat, providing AI-powered Azure infrastructure management through the Platform Engineering Copilot's multi-agent architecture.

## Features

- **@platform Chat Participant**: Interact with 6 specialized agents directly in GitHub Copilot Chat
- **Agent Commands**: Route requests to specific agents using slash commands
- **Compliance Analysis**: Analyze files and workspaces for NIST 800-53 compliance
- **Template Generation**: Generate Bicep, Terraform, and Kubernetes templates
- **Workspace Integration**: Save generated templates directly to your project

## Specialized Agents

| Command | Agent | Capabilities |
|---------|-------|--------------|
| `/infrastructure` | Infrastructure Agent | Bicep/Terraform generation, ARM deployment |
| `/compliance` | Compliance Agent | NIST 800-53 scanning, remediation, ATO docs |
| `/cost` | Cost Management Agent | Cost analysis, optimization recommendations |
| `/discover` | Discovery Agent | Resource inventory, dependency mapping |
| `/knowledge` | KnowledgeBase Agent | NIST controls, STIGs, compliance frameworks |
| `/config` | Configuration Agent | Subscription context, environment settings |

## Requirements

- VS Code 1.90.0 or higher
- GitHub Copilot extension
- Platform Engineering Copilot MCP Server (running in HTTP mode)

## Installation

### From VSIX

```bash
# Build the extension
cd extensions/platform-engineering-copilot-github
npm install
npm run compile
npm run package

# Install in VS Code
code --install-extension platform-copilot-github-0.9.0.vsix
```

### Development

```bash
# Install dependencies
npm install

# Compile
npm run compile

# Watch mode for development
npm run watch

# Press F5 in VS Code to launch Extension Development Host
```

## Configuration

Open VS Code settings and search for "Platform Copilot":

| Setting | Default | Description |
|---------|---------|-------------|
| `platform-copilot.apiUrl` | `http://localhost:5100` | MCP Server HTTP endpoint |
| `platform-copilot.apiKey` | (empty) | Optional API key |
| `platform-copilot.timeout` | `60000` | Request timeout (ms) |
| `platform-copilot.enableLogging` | `true` | Enable debug logging |

## Usage

### Chat with @platform

Open GitHub Copilot Chat (Ctrl+Shift+I) and use the `@platform` participant:

```
@platform Create a Bicep template for an Azure Kubernetes Service cluster with NIST 800-53 compliance
```

### Use Agent Commands

Route directly to specialized agents:

```
@platform /compliance Scan my Bicep templates for security issues
@platform /cost Analyze costs for subscription xyz-123
@platform /infrastructure Generate Terraform for a secure storage account
```

### Analyze Current File

Use Command Palette (Ctrl+Shift+P):
- **Platform Copilot: Analyze Current File for Compliance**
- **Platform Copilot: Analyze Workspace for Compliance**
- **Platform Copilot: Check Platform API Health**

## Starting the MCP Server

The extension requires the MCP server running in HTTP mode:

```bash
# From platform-engineering-copilot root
dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http

# Or with Docker
docker-compose -f docker-compose.essentials.yml up -d
```

The server will be available at `http://localhost:5100`.

## Troubleshooting

### Cannot connect to MCP server

1. Verify the MCP server is running: `curl http://localhost:5100/health`
2. Check the API URL in settings matches the server
3. View Output panel → "Platform Engineering Copilot" for detailed logs

### Request timeout

Increase the timeout setting for complex operations:
```json
{
  "platform-copilot.timeout": 120000
}
```

### Extension not activating

Ensure GitHub Copilot extension is installed and authenticated.

## Architecture

```
┌─────────────────────────────────────────┐
│        GitHub Copilot Chat              │
│              @platform                  │
└─────────────────┬───────────────────────┘
                  │ HTTP
                  ▼
┌─────────────────────────────────────────┐
│       MCP Server (HTTP Mode)            │
│         localhost:5100                  │
└─────────────────┬───────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────┐
│         Agent FX Orchestrator           │
├─────────────────────────────────────────┤
│ Infrastructure │ Compliance │ Cost      │
│ Discovery      │ Knowledge  │ Config    │
└─────────────────────────────────────────┘
```

## Contributing

See the main [CONTRIBUTING.md](../../CONTRIBUTING.md) for guidelines.

## License

MIT - See [LICENSE](../../LICENSE)
