# Platform MCP Universal Extension

This VS Code extension provides universal chat interface compatibility with the Platform MCP Server, enabling any chat interface to directly access platform engineering tools through the Model Context Protocol (MCP).

## Overview

The Platform MCP Universal Extension eliminates the need for a Router layer by connecting chat interfaces directly to the Platform MCP Server. This approach provides:

- **Direct MCP Protocol Integration**: Native support for the Model Context Protocol
- **Universal Chat Compatibility**: Works with any chat interface that supports VS Code extensions
- **Comprehensive Tool Access**: Full access to all Platform MCP Server tools including:
  - Azure resource management
  - Bicep deployments
  - Infrastructure monitoring
  - Cost analysis
  - Security assessments
  - Governance compliance

## Architecture

```
Chat Interface → VS Code Extension → Platform MCP Server
```

Instead of the previous Router-based architecture:
```
Chat Interface → Router → Platform MCP Server
```

This direct approach reduces complexity and improves performance.

## Features

### MCP Client Integration
- Full MCP protocol implementation
- Automatic tool discovery and analysis
- Dynamic parameter extraction from natural language
- Intelligent tool selection based on user intent

### Chat Participant
- Registered as `@platform` chat participant
- Natural language processing for tool identification
- Smart parameter mapping
- Rich response formatting

### Tool Categories
The extension provides access to all Platform MCP Server tools:
- **Bicep**: Template deployments and management
- **Azure**: Resource operations and monitoring
- **Cost**: Usage analysis and optimization
- **Security**: Compliance and vulnerability assessments
- **Governance**: Policy enforcement and auditing

## Installation

1. Install the extension: `platform-mcp-extension-0.1.0.vsix`
2. Ensure Platform MCP Server is running (default: http://localhost:3001)
3. Configure extension settings if needed:
   - `platformMcp.serverUrl`: MCP Server endpoint
   - `platformMcp.timeout`: Request timeout (default: 30000ms)
   - `platformMcp.enableLogging`: Debug logging (default: false)

## Usage

### In VS Code Chat

```
@platform deploy an AKS cluster with monitoring enabled
@platform analyze costs for the last 30 days
@platform check security compliance for resource group "prod-rg"
@platform create a storage account with encryption
```

### Natural Language Processing

The extension intelligently parses natural language requests:
- Identifies relevant tools based on keywords and context
- Extracts parameters from conversational input
- Maps user intent to specific tool capabilities
- Provides confidence scoring for tool selection

## Configuration

### Extension Settings

- **Server URL**: Configure the Platform MCP Server endpoint
- **Timeout**: Set request timeout for tool executions
- **Logging**: Enable debug logging for troubleshooting

### MCP Server Requirements

- Platform MCP Server must be running and accessible
- Server should expose MCP-compliant endpoints
- Tool metadata should be available via `/tools/list`

## Development

### Building from Source

```bash
npm install
npm run compile
npm run package
```

### Testing

The extension includes comprehensive testing for:
- MCP protocol communication
- Tool parameter extraction
- Natural language processing
- Error handling and recovery

## Troubleshooting

### Common Issues

1. **Server Connection Failed**
   - Verify Platform MCP Server is running
   - Check server URL configuration
   - Ensure network connectivity

2. **Tool Not Found**
   - Confirm tool exists on MCP Server
   - Check tool name spelling
   - Verify server tool metadata

3. **Parameter Errors**
   - Review natural language input
   - Check required parameter formatting
   - Validate data types and constraints

### Debug Logging

Enable debug logging in extension settings to troubleshoot:
- MCP protocol communication
- Tool discovery and analysis
- Parameter extraction logic
- HTTP request/response details

## License

MIT License - see LICENSE file for details

## Support

For issues and questions:
- Check Platform MCP Server logs
- Enable extension debug logging
- Review VS Code developer console
- Verify MCP protocol compliance