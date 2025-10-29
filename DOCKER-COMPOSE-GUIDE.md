# Docker Compose Configuration Guide

This project includes multiple docker-compose configurations to support different deployment scenarios.

## Available Configurations

### 1. docker-compose.essentials.yml (MCP Server Only)
**Minimal configuration with only the MCP Server and SQL Server**

**Includes:**
- MCP Server (port 5100)
- SQL Server (port 1433)

**Use when:**
- Developing with AI clients (GitHub Copilot, Claude Desktop) via stdio mode
- Testing MCP server functionality in isolation
- Minimal resource usage required
- Don't need the web-based chat interface

**Start command:**
```bash
docker-compose -f docker-compose.essentials.yml up -d
```

**AI Client Connection:**
The MCP server will be available for stdio connections through the container.

---

### 2. docker-compose.yml (Default - All Services)
**Complete configuration with all services**

**Includes:**
- MCP Server (port 5100) - Multi-agent orchestrator
- Platform Chat (port 5001) - Web chat interface
- Admin API (port 5002) - Admin backend
- Admin Client (port 5003) - Admin web console
- SQL Server (port 1433) - Database
- Nginx (ports 80/443) - Optional reverse proxy (profile: `proxy`)
- Redis (port 6379) - Optional caching (profile: `cache`)

**Use when:**
- Running the complete platform with all features
- Production deployments
- Need web-based chat interface
- Need admin console access

**Start command:**
```bash
# All core services
docker-compose up -d

# Include reverse proxy
docker-compose --profile proxy up -d

# Include caching
docker-compose --profile cache up -d

# All services including optional
docker-compose --profile proxy --profile cache up -d
```

---

### 3. docker-compose.all.yml (Identical to docker-compose.yml)
**Explicit alias for the full configuration**

**Use when:**
- You want to be explicit about running all services
- Documentation or scripts need clear naming

**Start command:**
```bash
docker-compose -f docker-compose.all.yml up -d
```

---

### 4. docker-compose.dev.yml (Development Overrides)
**Development environment with hot reload**

**Features:**
- Source code mounted as volumes for hot reload
- Development environment settings
- Detailed logging
- Adminer database UI

**Use with any base configuration:**
```bash
# Essentials + Dev
docker-compose -f docker-compose.essentials.yml -f docker-compose.dev.yml up -d

# All services + Dev
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```

---

### 5. docker-compose.prod.yml (Production Overrides)
**Production environment with scaling and resource limits**

**Features:**
- 2 replicas for MCP, Chat, Admin services
- Resource limits (CPU, Memory)
- Production environment settings
- Restart policies

**Use with any base configuration:**
```bash
# Essentials + Prod
docker-compose -f docker-compose.essentials.yml -f docker-compose.prod.yml up -d

# All services + Prod
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

---

## Common Use Cases

### Local Development (MCP Server Only)
```bash
docker-compose -f docker-compose.essentials.yml -f docker-compose.dev.yml up -d
```
- Minimal services (MCP + SQL)
- Hot reload enabled
- Connect via GitHub Copilot or Claude Desktop

### Local Development (All Services)
```bash
docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d
```
- All services running
- Hot reload enabled
- Web chat interface available at http://localhost:5001
- Admin console at http://localhost:5003

### Production Deployment (All Services)
```bash
docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```
- All services with production settings
- Resource limits enforced
- Multiple replicas for high availability

### Production with Reverse Proxy
```bash
docker-compose -f docker-compose.yml -f docker-compose.prod.yml --profile proxy up -d
```
- All services with production settings
- Nginx reverse proxy on ports 80/443

---

## Service Health Checks

Check the status of running services:

```bash
# For essentials
docker-compose -f docker-compose.essentials.yml ps

# For all services
docker-compose ps

# Check specific service health
curl http://localhost:5100/health  # MCP Server
curl http://localhost:5001/health  # Chat
curl http://localhost:5002/health  # Admin API
curl http://localhost:5003/health  # Admin Client
```

---

## Logs

View logs for running services:

```bash
# All services
docker-compose logs -f

# Specific service
docker-compose logs -f platform-mcp
docker-compose logs -f sqlserver

# With essentials configuration
docker-compose -f docker-compose.essentials.yml logs -f platform-mcp
```

---

## Cleanup

Stop and remove containers:

```bash
# Stop essentials
docker-compose -f docker-compose.essentials.yml down

# Stop all services
docker-compose down

# Stop and remove volumes (WARNING: deletes data)
docker-compose down -v
```

---

## Port Reference

| Service       | Port | Configuration              |
|--------------|------|----------------------------|
| MCP Server   | 5100 | All configurations         |
| Platform Chat| 5001 | docker-compose.yml only    |
| Admin API    | 5002 | docker-compose.yml only    |
| Admin Client | 5003 | docker-compose.yml only    |
| SQL Server   | 1433 | All configurations         |
| Nginx        | 80   | Optional (profile: proxy)  |
| Nginx SSL    | 443  | Optional (profile: proxy)  |
| Redis        | 6379 | Optional (profile: cache)  |

---

## Environment Variables

All configurations use the same environment variables from your `.env` file:

```bash
# Copy example file
cp .env.example .env

# Edit with your values
nano .env
```

See `DOCKER.md` for complete environment variable documentation.

---

## Quick Reference

| Goal | Command |
|------|---------|
| MCP Server only (dev) | `docker-compose -f docker-compose.essentials.yml -f docker-compose.dev.yml up -d` |
| MCP Server only (prod) | `docker-compose -f docker-compose.essentials.yml -f docker-compose.prod.yml up -d` |
| All services (dev) | `docker-compose -f docker-compose.yml -f docker-compose.dev.yml up -d` |
| All services (prod) | `docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d` |
| All + proxy (prod) | `docker-compose -f docker-compose.yml -f docker-compose.prod.yml --profile proxy up -d` |
| Stop essentials | `docker-compose -f docker-compose.essentials.yml down` |
| Stop all | `docker-compose down` |

---

## Architecture Comparison

### Essentials Configuration
```
┌──────────────────────────────┐
│  MCP Server (5100)           │
│  - HTTP Mode                 │
│  - stdio Mode (for AI)       │
└─────────┬────────────────────┘
          │
          ▼
┌──────────────────────────────┐
│  SQL Server (1433)           │
│  - McpDb                     │
└──────────────────────────────┘
```

### Full Configuration
```
┌──────────────────────────────┐
│  MCP Server (5100)           │
└─────────┬────────────────────┘
          │
    ┌─────┴─────┬──────────────┐
    ▼           ▼              ▼
┌────────┐ ┌─────────┐ ┌──────────────┐
│ Chat   │ │Admin API│ │ SQL Server   │
│ (5001) │ │ (5002)  │ │   (1433)     │
└────────┘ └────┬────┘ └──────────────┘
                │
                ▼
         ┌──────────────┐
         │Admin Client  │
         │   (5003)     │
         └──────────────┘
```
