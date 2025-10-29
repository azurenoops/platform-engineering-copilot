# Docker Configuration Guide

## Overview

The Platform Engineering Copilot application is containerized using Docker with separate containers for each service:

- **platform-mcp** (Port 5100): MCP Server - Multi-Agent Orchestrator (Primary Service)
- **platform-chat** (Port 5001): Chat interface service
- **admin-api** (Port 5002): Admin API service
- **admin-client** (Port 5003): Admin client interface
- **sqlserver** (Port 1433): SQL Server 2022 database

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Docker Network                        │
│          (plaform-engineering-copilot-network)           │
│                                                          │
│  ┌──────────────────────────────────────────┐           │
│  │         MCP Server (Primary)             │           │
│  │            :5100                         │           │
│  │  Multi-Agent Orchestrator:               │           │
│  │  - Infrastructure Agent                  │           │
│  │  - Cost Optimization Agent               │           │
│  │  - Compliance (RMF/NIST) Agent          │           │
│  │  - Security Agent                        │           │
│  │  - Document Agent                        │           │
│  │  - ATO Preparation Agent                 │           │
│  └────────┬─────────────────────────────────┘           │
│           │                                              │
│           │ HTTP (5100)                                  │
│           ▼                                              │
│  ┌──────────────┐    ┌──────────────┐                  │
│  │Platform Chat │    │ AI Clients   │                  │
│  │   :5001      │    │ (stdio mode) │                  │
│  └──────────────┘    └──────────────┘                  │
│                                                          │
│         ┌────────────────┐                              │
│         │ SQL Server 2022│                              │
│         │     :1433      │                              │
│         │ - McpDb        │                              │
│         │ - ChatDb       │                              │
│         │ - AdminDb      │                              │
│         └────────────────┘                              │
│         ▲           ▲                                    │
│  ┌──────┴───────┐  │                                    │
│  │  Admin API   │  │                                    │
│  │   :5002      │──┘                                    │
│  └──────────────┘                                       │
│         ▲                                                │
│  ┌──────┴───────┐                                       │
│  │ Admin Client │                                       │
│  │   :5003      │                                       │
│  └──────────────┘                                       │
└─────────────────────────────────────────────────────────┘
```

## Docker Compose Files

### docker-compose.yml
Main configuration file with all services (MCP Server, Chat, Admin API, Admin Client, SQL Server, optional Nginx and Redis).

### docker-compose.essentials.yml
Minimal configuration with only the MCP Server and its dependencies (SQL Server). Use this for:
- MCP Server development with AI clients (GitHub Copilot, Claude Desktop)
- Minimal resource usage
- Testing MCP server functionality in isolation

### docker-compose.dev.yml
Development overrides with hot reload for all services.

### docker-compose.prod.yml
Production overrides with scaling, resource limits, and production-ready configuration.

## Services

### 1. MCP Server (Primary Service)
- **Port**: 5100 (HTTP mode)
- **Dockerfile**: `src/Platform.Engineering.Copilot.Mcp/Dockerfile`
- **Database**: `McpDb`
- **Purpose**: Multi-agent orchestrator with 6 specialized agents
- **Modes**:
  - **HTTP Mode** (default in Docker): Accessible via HTTP for Chat web app
  - **stdio Mode**: For AI clients like GitHub Copilot and Claude Desktop

**Agents:**
1. **Infrastructure Agent**: Azure resource provisioning and management
2. **Cost Optimization Agent**: Cost analysis, budgets, and recommendations
3. **Compliance Agent**: RMF/NIST 800-53 compliance and gap analysis
4. **Security Agent**: Security scanning and vulnerability assessment
5. **Document Agent**: ATO documentation generation and management
6. **ATO Preparation Agent**: ATO package orchestration and submission

### 2. Platform Chat
- **Port**: 5001
- **Dockerfile**: `src/Platform.Engineering.Copilot.Chat/Dockerfile`
- **Database**: `ChatDb`
- **Purpose**: Web-based chat interface for natural language infrastructure management
- **Connects to**: MCP Server via HTTP (`http://platform-mcp:5100`)
- **Requirements**: Node.js 20.x (included in Docker image for frontend build)

### 3. Admin API
- **Port**: 5002
- **Dockerfile**: `src/Platform.Engineering.Copilot.Admin.API/Dockerfile`
- **Database**: `SupervisorAdminDb`
- **Purpose**: Administrative API for platform management

### 4. Admin Client
- **Port**: 5003
- **Dockerfile**: `src/Platform.Engineering.Copilot.Admin.Client/Dockerfile`
- **Purpose**: Web-based admin console
- **Requirements**: Node.js 20.x (included in Docker image for frontend build)

### 5. SQL Server
- **Port**: 1433
- **Image**: `mcr.microsoft.com/mssql/server:2022-latest`
- **Databases**: 
  - `McpDb` (MCP Server)
  - `ChatDb` (Chat Service)
  - `AdminDb` (Admin API)

### 6. Optional Services

#### Nginx (Reverse Proxy)
- **Ports**: 80, 443
- **Profile**: `proxy`
- **Purpose**: Reverse proxy for production deployments

#### Redis (Cache)
- **Port**: 6379
- **Profile**: `cache`
- **Purpose**: Distributed caching and session management

## Quick Start

### MCP Server Only (Essentials)

```bash
# Start just the MCP Server and SQL Server
docker-compose -f docker-compose.essentials.yml up -d

# View logs
docker-compose -f docker-compose.essentials.yml logs -f

# Stop
docker-compose -f docker-compose.essentials.yml down
```

**Use this when:**
- Connecting with GitHub Copilot or Claude Desktop (stdio mode)
- Testing MCP server functionality in isolation
- Minimal resource usage needed

### All Services - Development Environment

```bash
# Start all services with hot reload
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d

# View logs
docker-compose logs -f

# Stop all services
docker-compose down
```

### All Services - Production Environment

```bash
# Start all services in production mode
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# View logs
docker-compose logs -f platform-mcp

# Stop all services
docker-compose down
```

## Environment Variables

The application uses environment variables for configuration. Create a `.env` file in the root directory.

### Quick Setup

```bash
# Copy the example file
cp .env.example .env

# Edit with your values
nano .env  # or use your preferred editor
```

### Configuration Sections

#### 1. Azure Configuration
```env
AZURE_SUBSCRIPTION_ID=453c2549-4cc5-464f-ba66-acad920823e8
AZURE_TENANT_ID=5310815f-d7b8-4c10-9879-9eccf368d822
AZURE_CLOUD_ENVIRONMENT=AzureGovernment
AZURE_USE_MANAGED_IDENTITY=false
AZURE_ENABLED=true
```

#### 2. Azure OpenAI Configuration
```env
AZURE_OPENAI_API_KEY=your-api-key-here
AZURE_OPENAI_ENDPOINT=https://mcp-ai.openai.azure.us/
AZURE_OPENAI_DEPLOYMENT=gpt-4o
AZURE_OPENAI_CHAT_DEPLOYMENT=gpt-4o
AZURE_OPENAI_EMBEDDING_DEPLOYMENT=text-embedding-ada-002
```

#### 3. GitHub Configuration (Optional)
```env
GITHUB_TOKEN=your-github-token-here
GITHUB_API_BASE_URL=https://api.github.com
GITHUB_DEFAULT_OWNER=your-org-name
GITHUB_ENABLED=true
```

#### 4. NIST Controls Configuration
```env
NIST_CONTROLS_BASE_URL=https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json
NIST_CONTROLS_TIMEOUT=60
NIST_CONTROLS_CACHE_DURATION=24
NIST_CONTROLS_OFFLINE_FALLBACK=true
```

#### 5. Email Notifications (Azure Communication Services)
```env
EMAIL_CONNECTION_STRING=endpoint=https://your-acs.communication.azure.us/;accesskey=xxx
EMAIL_SENDER_EMAIL=noreply@flankspeed.navy.mil
EMAIL_SENDER_NAME=Navy Flankspeed Platform
EMAIL_ENABLE_NOTIFICATIONS=true
EMAIL_MOCK_MODE=false
```

#### 6. Slack Notifications
```env
SLACK_WEBHOOK_URL=https://hooks.slack.com/services/xxx
SLACK_ENABLE_NOTIFICATIONS=true
SLACK_CHANNEL_NAME=#flankspeed-ops
SLACK_BOT_USERNAME=Flankspeed Platform
```

#### 7. Teams Notifications
```env
TEAMS_WEBHOOK_URL=your-teams-webhook-url
TEAMS_ENABLE_NOTIFICATIONS=false
```

#### 8. SQL Server
```env
SA_PASSWORD=SupervisorDB123!
```

### Complete .env File

See `.env.example` for a complete template with all available options and their default values.

**Configuration Mapping:**

The environment variables map to the hierarchical configuration in `appsettings.json`:

| Environment Variable | appsettings.json Path |
|---------------------|----------------------|
| `AZURE_SUBSCRIPTION_ID` | `Gateway.Azure.SubscriptionId` |
| `AZURE_OPENAI_API_KEY` | `Gateway.AzureOpenAI.ApiKey` |
| `GITHUB_TOKEN` | `Gateway.GitHub.AccessToken` |
| `NIST_CONTROLS_BASE_URL` | `NistControls.BaseUrl` |
| `EMAIL_SENDER_EMAIL` | `EmailNotifications.SenderEmail` |
| `SLACK_WEBHOOK_URL` | `SlackNotifications.WebhookUrl` |

ASP.NET Core uses `__` (double underscore) as a separator in environment variables:
- `Gateway__Azure__SubscriptionId=${AZURE_SUBSCRIPTION_ID}`
- `Gateway__AzureOpenAI__ApiKey=${AZURE_OPENAI_API_KEY}`
- `NistControls__BaseUrl=${NIST_CONTROLS_BASE_URL}`

## Database Configuration

All services use SQL Server with separate databases:

### Connection Strings Format:
```
Server=sqlserver,1433;Database={DatabaseName};User=sa;Password=SupervisorDB123!;TrustServerCertificate=true;MultipleActiveResultSets=true;Encrypt=false
```

### Databases:
- **McpDb**: MCP Server data (agents, sessions, artifacts)
- **ChatDb**: Chat history and sessions
- **AdminDb**: Admin and configuration data

### Database Initialization
Databases are automatically created on first startup via `scripts/init-databases.sql`.

## Building Individual Services

```bash
# Build MCP Server
docker-compose build platform-mcp --no-cache

# Build Platform Chat
docker-compose build platform-chat --no-cache

# Build Admin API
docker-compose build admin-api --no-cache

# Build Admin Client
docker-compose build admin-client --no-cache

# Build all services
docker-compose build --no-cache
```

## Volumes

- **plaform-engineering-copilot-sqlserver-data**: Persistent SQL Server data
- **plaform-engineering-copilot-logs**: Application logs from all services

## Health Checks

All services include health checks:

- **MCP Server**: `http://localhost:5100/health`
- **Platform Chat**: `http://localhost:5001/health`
- **Admin API**: `http://localhost:5002/health`
- **Admin Client**: `http://localhost:5003/health`
- **SQL Server**: SQL query health check

## Troubleshooting

### Common Build Issues

#### npm not found error
If you see `npm: not found` during build, the Dockerfiles for `platform-chat` and `admin-client` automatically install Node.js 20.x. If the build fails:
```bash
# Ensure you're using the latest Dockerfiles
git pull

# Clear Docker cache and rebuild
docker-compose build --no-cache
```

### Check Service Status
```bash
docker-compose ps
```

### View Logs
```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f platform-mcp
docker-compose logs -f sqlserver
```

### Restart a Service
```bash
docker-compose restart platform-mcp
```

### Rebuild and Restart
```bash
docker-compose up -d --build platform-mcp
```

### Access SQL Server
```bash
# Using docker exec
docker exec -it supervisor-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P SupervisorDB123! -C

# From host (requires sqlcmd installed)
sqlcmd -S localhost,1433 -U sa -P SupervisorDB123!
```

### Reset Everything
```bash
# Stop and remove all containers, networks, and volumes
docker-compose down -v

# Rebuild and start
docker-compose up -d --build
```

## Network Configuration

All services run on the `plaform-engineering-copilot-network` bridge network, allowing them to communicate using service names as hostnames.

## Port Mapping

| Service        | Container Port | Host Port | Mode    |
|---------------|----------------|-----------|---------|
| MCP Server    | 5100          | 5100      | HTTP    |
| Platform Chat | 5001          | 5001      | HTTP    |
| Admin API     | 5002          | 5002      | HTTP    |
| Admin Client  | 5003          | 5003      | HTTP    |
| SQL Server    | 1433          | 1433      | TCP     |
| Nginx         | 80/443        | 80/443    | HTTP(S) |
| Redis         | 6379          | 6379      | TCP     |

## Development vs Production

### Development Mode
- Hot reload enabled for all .NET services
- Source code mounted as volumes
- Detailed logging
- Development SQL Server edition

### Production Mode
- Optimized runtime containers
- Multiple replicas for API services
- Resource limits enforced
- Standard SQL Server edition
- Enhanced security settings

## Security Considerations

1. **Change default SA password** in production
2. **Use Azure Key Vault** for secrets in production
3. **Enable SSL/TLS** for external connections
4. **Configure firewall rules** appropriately
5. **Use managed identities** where possible

## Connecting AI Clients to MCP Server

### GitHub Copilot
Add to `.github/copilot/config.json`:
```json
{
  "mcp": {
    "servers": {
      "platform-engineering-copilot": {
        "command": "docker",
        "args": ["exec", "-i", "plaform-engineering-copilot-mcp", "dotnet", "run", "--project", "/app/Platform.Engineering.Copilot.Mcp.csproj"]
      }
    }
  }
}
```

### Claude Desktop
Add to Claude Desktop config:
```json
{
  "mcpServers": {
    "platform-engineering-copilot": {
      "command": "docker",
      "args": ["exec", "-i", "plaform-engineering-copilot-mcp", "dotnet", "run", "--project", "/app/Platform.Engineering.Copilot.Mcp.csproj"]
    }
  }
}
```

## Next Steps

### For MCP Server Only (Essentials)
1. Configure your `.env` file with actual credentials
2. Start MCP server: `docker-compose -f docker-compose.essentials.yml up -d`
3. Verify MCP server is healthy: `docker-compose -f docker-compose.essentials.yml ps`
4. Test the MCP API: `curl http://localhost:5100/health`
5. Connect AI clients (GitHub Copilot or Claude Desktop)

### For All Services
1. Configure your `.env` file with actual credentials
2. Start all services: `docker-compose up -d`
3. Verify all services are healthy: `docker-compose ps`
4. Access the admin console: `http://localhost:5003`
5. Access the chat interface: `http://localhost:5001`
6. Test the MCP API: `curl http://localhost:5100/health`
