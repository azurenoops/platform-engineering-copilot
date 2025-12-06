# Getting Started with Platform Engineering Copilot

> **Complete setup guide from zero to running system in 15 minutes**  
> **Last Updated:** November 2025

---

## 📋 Table of Contents

1. [Quick Start (5 Minutes)](#quick-start-5-minutes)
2. [Development Setup](#development-setup)
3. [Docker Deployment](#docker-deployment)
4. [Production Deployment](#production-deployment)
5. [AI Client Integration](#ai-client-integration)
6. [Verification & Testing](#verification--testing)
7. [Next Steps](#next-steps)
8. [Troubleshooting](#troubleshooting)

---

## 🚀 Quick Start (5 Minutes)

Choose your path:

### Option A: Docker (Recommended for First-Time Users)

```bash
# 0. Login to Azure (REQUIRED for Discovery Agent)
az login --use-device-code
az account set --subscription "your-subscription-id"

# 1. Clone repository
git clone https://github.com/azurenoops/platform-engineering-copilot.git
cd platform-engineering-copilot

# 2. Copy environment template
cp .env.example .env

# 3. Edit .env with your Azure credentials (see AUTHENTICATION.md)
# Required: AZURE_SUBSCRIPTION_ID, AZURE_TENANT_ID, AZURE_OPENAI_API_KEY

# 4. Start services
docker compose -f docker-compose.essentials.yml up -d

# 5. Verify
curl http://localhost:5100/health
```

**You're running!** MCP Server is at `http://localhost:5100`

> **⚠️ Critical:** You MUST run `az login` before starting Docker! The container needs your Azure credentials to discover resources. See [troubleshooting](#-troubleshooting) if Discovery Agent returns no resources.

### Option B: Local Development (.NET)

```bash
# 1. Prerequisites: .NET 9.0 SDK, Node.js 18+
dotnet --version  # Should show 9.0.x
node --version    # Should show 18.x or higher

# 2. Clone and restore
git clone https://github.com/azurenoops/platform-engineering-copilot.git
cd platform-engineering-copilot
dotnet restore

# 3. Setup database (SQLite - no Docker required)
dotnet tool update --global dotnet-ef
cd src/Platform.Engineering.Copilot.Data
dotnet ef database update
cd ../..

# 4. Configure Azure authentication
az login  # See AUTHENTICATION.md for details

# 5. Run MCP Server
cd src/Platform.Engineering.Copilot.Mcp
dotnet run
```

**You're running!** MCP Server is at `http://localhost:5100`

---

## 🛠️ Development Setup

### Prerequisites

| Component | Version | Required | Notes |
|-----------|---------|----------|-------|
| **.NET SDK** | 9.0+ | ✅ Yes | [Download](https://dotnet.microsoft.com/download) |
| **Node.js** | 18 LTS+ | ✅ Yes | For React frontends |
| **Docker Desktop** | Latest | ⚠️ Recommended | For full stack development |
| **VS Code** | Latest | ⚠️ Recommended | With C# Dev Kit extension |
| **Azure CLI** | 2.50+ | ⚠️ Recommended | For Azure authentication |
| **Git** | 2.0+ | ✅ Yes | Version control |

### Step 1: Clone and Install

```bash
# Clone repository
git clone https://github.com/azurenoops/platform-engineering-copilot.git
cd platform-engineering-copilot

# Restore .NET dependencies
dotnet restore Platform.Engineering.Copilot.sln

# Build solution
dotnet build Platform.Engineering.Copilot.sln

# Run tests
dotnet test Platform.Engineering.Copilot.sln

# Install frontend dependencies
npm install --prefix src/Platform.Engineering.Copilot.Chat/ClientApp
npm install --prefix src/Platform.Engineering.Copilot.Admin.Client/ClientApp
```

### Step 2: Database Setup

**SQLite (Default - Zero Configuration)**

```bash
# Install EF Core tools
dotnet tool update --global dotnet-ef

# Run migrations
cd src/Platform.Engineering.Copilot.Data
dotnet ef database update
cd ../..
```

This creates `platform_engineering_copilot_management.db` in the repository root.

**SQL Server (Optional - Team Environments)**

```bash
# Start SQL Server via Docker
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
   -p 1433:1433 --name platform-sql -d mcr.microsoft.com/mssql/server:2022-latest

# Update connection string in appsettings.Development.json
# Then run migrations
cd src/Platform.Engineering.Copilot.Data
dotnet ef database update \
  --connection "Server=localhost,1433;Database=PlatformCopilot;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true"
cd ../..
```

### Step 3: Azure Authentication

**Development (User Identity)**

```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "your-subscription-id"

# Verify
az account show
```

**CAC/PIV Authentication (Azure Government)**

For DoD environments requiring Common Access Card (CAC) or Personal Identity Verification (PIV):

```bash
# Configure for Azure Government
az cloud set --name AzureUSGovernment
az login

# Set tenant ID
export AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)
```

**Enable CAC token validation** in `appsettings.json`:
```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.us/",
    "Audience": "api://your-api-id",
    "RequireMfa": true,
    "RequireCac": true,
    "EnableUserTokenPassthrough": true
  }
}
```

📖 **Complete CAC/PIV guide**: See `releases/CAC-AUTHENTICATION.md` for detailed setup

**Production (Managed Identity)**

See **[AUTHENTICATION.md](./AUTHENTICATION.md)** for production setup with managed identity.

### Step 4: Configuration

**📍 Configuration is now centralized at repository root** (`appsettings.json`)

**Quick setup:**
```bash
# Copy example configuration
cp appsettings.example.json appsettings.json

# Edit with your Azure credentials
vi appsettings.json
```

**Configuration structure** (`appsettings.json` at repository root):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "Gateway": {
    "Azure": {
      "UseManagedIdentity": false,
      "CloudEnvironment": "AzureGovernment",
      "Enabled": true,
      "EnableUserTokenPassthrough": true
    },
    "AzureOpenAI": {
      "ApiKey": "your-azure-openai-api-key-here",
      "Endpoint": "https://your-resource-name.openai.azure.us/",
      "DeploymentName": "gpt-4o",
      "UseManagedIdentity": false,
      "ChatDeploymentName": "gpt-4o",
      "EmbeddingDeploymentName": "text-embedding-ada-002"
    },
    "GitHub": {
      "AccessToken": "ghp_your_github_personal_access_token_here",
      "ApiBaseUrl": "https://api.github.com",
      "DefaultOwner": "your-github-username-or-org",
      "Enabled": true,
      "PersonalAccessToken": "ghp_your_github_personal_access_token_here",
      "WebhookSecret": "your-webhook-secret-here",
      "EnablePrReviews": true,
      "AutoApproveOnSuccess": false,
      "MaxFileSizeKb": 1024
    },
    "ConnectionTimeoutSeconds": 60,
    "RequestTimeoutSeconds": 300
  },
  "AzureAd": {
    "Instance": "https://login.microsoftonline.us/",    
    "Audience": "api://platform-engineering-copilot",
    "RequireMfa": false,
    "RequireCac": false,
    "ValidIssuers": [
      "https://login.microsoftonline.us/{tenant-id}/v2.0",
      "https://sts.windows.net/{tenant-id}/"
    ],
    "EnableUserTokenPassthrough": false
  },
  "AzureOpenAI": {
    "Endpoint": "https://your-endpoint.openai.azure.com/",
    "ApiKey": "your-api-key",
    "DeploymentName": "gpt-4o",
    "EmbeddingDeploymentName": "text-embedding-ada-002"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=platform_engineering_copilot_management.db"
  },
  "AgentConfiguration": {
    "InfrastructureAgent": {
      "Enabled": true,
      "Temperature": 0.4,
      "MaxTokens": 8000,
      "DefaultRegion": "usgovvirginia"
    },
    "ComplianceAgent": {
      "Enabled": true,
      "Temperature": 0.2,
      "MaxTokens": 6000,
      "DefaultFramework": "NIST80053"
    },
    "CostManagementAgent": {
      "Enabled": true,
      "Temperature": 0.3,
      "DefaultCurrency": "USD"
    },
    "DiscoveryAgent": {
      "Enabled": true,
      "Temperature": 0.3,
      "EnableHealthMonitoring": true
    },
    "EnvironmentAgent": {
      "Enabled": true
    },
    "SecurityAgent": {
      "Enabled": true
    },
    "KnowledgeBaseAgent": {
      "Enabled": true
    },
    "ServiceCreationAgent": {
      "Enabled": true
    },
    "DocumentAgent": {
      "Enabled": false
    }
  }
}
```

### Step 5: Run Services

**MCP Server Only**

```bash
cd src/Platform.Engineering.Copilot.Mcp
dotnet run
# Runs on http://localhost:5100
```

**Full Stack (All Services)**

Terminal 1 - MCP Server:
```bash
cd src/Platform.Engineering.Copilot.Mcp
dotnet run  # Port 5100
```

Terminal 2 - Chat Service:
```bash
cd src/Platform.Engineering.Copilot.Chat
dotnet run  # Port 5001
```

Terminal 3 - Admin API:
```bash
cd src/Platform.Engineering.Copilot.Admin.API
dotnet run  # Port 5002
```

Terminal 4 - Admin Client:
```bash
cd src/Platform.Engineering.Copilot.Admin.Client
dotnet run  # Port 5003
```

**Or use the convenience scripts:**

```bash
# Start MCP only
./scripts/start-mcp.sh

# Start Chat + MCP
./scripts/start-chat.sh

# Start everything
./scripts/start-all.sh
```

---

## 🐳 Docker Deployment

### Quick Docker Start

```bash
# 1. Login to Azure (Required for resource discovery)
az login --use-device-code
az account set --subscription "your-subscription-id"

# 2. Copy environment template
cp .env.example .env

# 3. Edit .env with your credentials
# Required variables:
#   - AZURE_SUBSCRIPTION_ID
#   - AZURE_TENANT_ID
#   - AZURE_OPENAI_API_KEY
#   - AZURE_OPENAI_ENDPOINT
#
# Optional (for CAC/PIV authentication):
#   - AZURE_AD_INSTANCE
#   - AZURE_AD_TENANT_ID
#   - AZURE_AD_CLIENT_ID
#   - AZURE_AD_CLIENT_SECRET
#   - AZURE_AD_AUDIENCE
#   - AZURE_AD_REQUIRE_MFA
#   - AZURE_AD_REQUIRE_CAC
#   - AZURE_AD_ENABLE_USER_TOKEN_PASSTHROUGH

# 4. Start essentials (MCP + Database)
docker compose -f docker-compose.essentials.yml up -d

# 5. View logs
docker compose logs -f platform-mcp

# 6. Verify
curl http://localhost:5100/health
```

> **⚠️ Important:** The container needs access to your Azure credentials!  
> The `docker-compose.essentials.yml` automatically mounts your `~/.azure` directory so the container can use your Azure CLI login.  
> **Without `az login`, Agents cannot query Azure resources.**

### How Azure Authentication Works in Docker

The MCP container uses **DefaultAzureCredential** which tries authentication methods in this order:

1. **Environment variables** (Service Principal) - For production
2. **Managed Identity** - For Azure-hosted containers
3. **Azure CLI** - For local development ✅ **(Mounted via volume)**
4. **Azure PowerShell** - Alternative to CLI
5. **Interactive browser** - Last resort

**For local Docker development**, the container uses your Azure CLI credentials via:
```yaml
volumes:
  - ~/.azure:/root/.azure:ro  # Read-only mount
```

This means:
- ✅ You must run `az login` on your host machine **before** starting Docker
- ✅ The container will use your Azure identity
- ✅ No need to configure Service Principal for local dev
- ✅ Agents can query your Azure subscriptions

**For production**, use Service Principal or Managed Identity (see [AUTHENTICATION.md](./AUTHENTICATION.md)).

### Full Stack Docker

```bash
# Start all services (MCP, Chat, Admin, Database)
docker compose up -d

# View all logs
docker compose logs -f

# Check status
docker compose ps
```

### Docker Compose Options

**Essentials (MCP + SQL Server)**
- `docker-compose.essentials.yml` - MCP Server and database only
- Best for: AI client development, MCP protocol testing

**Development (Hot Reload)**
- `docker-compose.dev.yml` - Hot reload enabled
- Combine with: `-f docker-compose.yml -f docker-compose.dev.yml`
- Best for: Active development, rapid iteration

**Production (Optimized)**
- `docker-compose.prod.yml` - Production optimizations
- Combine with: `-f docker-compose.yml -f docker-compose.prod.yml`
- Best for: Production deployments, performance testing

**All Services**
- `docker-compose.yml` - Complete platform
- Best for: Full feature testing, demo environments

### Docker Management

```bash
# View logs
docker compose logs -f platform-mcp

# Restart service
docker compose restart platform-mcp

# Stop all
docker compose down

# Stop and remove volumes
docker compose down -v

# Rebuild specific service
docker compose build platform-mcp
docker compose up -d platform-mcp

# Scale services
docker compose up -d --scale platform-mcp=3
```

**📖 Complete Docker guide:** [DEPLOYMENT.md](./DEPLOYMENT.md#docker-deployment)

### Centralized Configuration (v0.7.0+)

Configuration is now centralized at the repository root for easier management:

**File Structure:**
```
platform-engineering-copilot/
├── appsettings.json              ← Centralized configuration (single source of truth)
├── appsettings.example.json      ← Template for new deployments
├── .env                          ← Environment-specific overrides
└── src/Platform.Engineering.Copilot.Mcp/
    └── (no appsettings.json)     ← References root config via .csproj link
```

**Configuration Priority** (later overrides earlier):
1. `appsettings.json` (root) - Base configuration
2. Environment variables - Per-environment overrides
3. Command-line arguments - Runtime overrides

**Docker Integration:**
- Dockerfile copies `appsettings.json` from root during build
- Docker Compose mounts config as read-only volume
- Environment variables override config values

**Azure AD Environment Variables** (available in all docker-compose files):
```yaml
- AzureAd__Instance=${AZURE_AD_INSTANCE:-https://login.microsoftonline.us/}
- AzureAd__TenantId=${AZURE_AD_TENANT_ID:-}
- AzureAd__ClientId=${AZURE_AD_CLIENT_ID:-}
- AzureAd__ClientSecret=${AZURE_AD_CLIENT_SECRET:-}
- AzureAd__Audience=${AZURE_AD_AUDIENCE:-}
- AzureAd__RequireMfa=${AZURE_AD_REQUIRE_MFA:-false}
- AzureAd__RequireCac=${AZURE_AD_REQUIRE_CAC:-false}
- AzureAd__EnableUserTokenPassthrough=${AZURE_AD_ENABLE_USER_TOKEN_PASSTHROUGH:-false}
- Gateway__Azure__EnableUserTokenPassthrough=${AZURE_ENABLE_USER_TOKEN_PASSTHROUGH:-false}
```

**Benefits:**
- ✅ Single configuration file for all environments
- ✅ Easy version control without secrets
- ✅ Environment-specific overrides via `.env`
- ✅ Container-safe with read-only mounts
- ✅ Consistent dev/prod/container deployments

---

## ☁️ Production Deployment

### Azure Container Apps (Recommended)

```bash
# 1. Create Container Apps environment
az containerapp env create \
  --name platform-copilot-env \
  --resource-group platform-copilot-rg \
  --location eastus

# 2. Deploy MCP Server
az containerapp create \
  --name platform-mcp \
  --resource-group platform-copilot-rg \
  --environment platform-copilot-env \
  --image your-registry/platform-mcp:latest \
  --target-port 5100 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 10

# 3. Deploy Chat Service (optional)
az containerapp create \
  --name platform-chat \
  --resource-group platform-copilot-rg \
  --environment platform-copilot-env \
  --image your-registry/platform-chat:latest \
  --target-port 5001 \
  --ingress external \
  --env-vars McpServer__BaseUrl=https://platform-mcp.yourdomain.com
```

### Kubernetes (AKS)

```bash
# 1. Create AKS cluster
az aks create \
  --resource-group platform-copilot-rg \
  --name platform-copilot-aks \
  --node-count 3 \
  --enable-addons monitoring

# 2. Get credentials
az aks get-credentials \
  --resource-group platform-copilot-rg \
  --name platform-copilot-aks

# 3. Deploy
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/deployments.yaml
kubectl apply -f k8s/services.yaml
kubectl apply -f k8s/ingress.yaml
```

### Azure Container Instances (Simple)

```bash
# Deploy MCP Server
az container create \
  --resource-group platform-copilot-rg \
  --name platform-mcp \
  --image your-registry/platform-mcp:latest \
  --ports 5100 \
  --environment-variables ASPNETCORE_ENVIRONMENT=Production \
  --cpu 2 \
  --memory 4
```

**📖 Complete deployment guide:** [DEPLOYMENT.md](./DEPLOYMENT.md)

---

## 🤖 AI Client Integration

### GitHub Copilot (VS Code Extension)

```bash
# 1. Install extension
# Navigate to extensions/ platform-engineering-copilot-github
cd extensions/platform-engineering-copilot-github

# 2. Build and install
npm install
npm run compile
code --install-extension platform-engineering-copilot-github-0.1.0.vsix

# 3. Configure (VS Code Settings)
{
  "platformCopilot.mcpServer.url": "http://localhost:5100",
  "platformCopilot.azure.subscriptionId": "your-subscription-id"
}

# 4. Use in VS Code
@platform Show me all VMs in resource group rg-prod
```

**📖 Complete integration guide:** [INTEGRATIONS.md](./INTEGRATIONS.md#github-copilot-integration)

### Claude Desktop (MCP stdio)

```json
// ~/Library/Application Support/Claude/claude_desktop_config.json (macOS)
{
  "mcpServers": {
    "platform-copilot": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "/path/to/platform-engineering-copilot/src/Platform.Engineering.Copilot.Mcp"
      ],
      "env": {
        "AZURE_SUBSCRIPTION_ID": "your-subscription-id",
        "AZURE_TENANT_ID": "your-tenant-id"
      }
    }
  }
}
```

**📖 Complete MCP integration:** [INTEGRATIONS.md](./INTEGRATIONS.md#mcp-protocol-integration)

### M365 Copilot

```bash
# 1. Install extension
cd extensions/platform-engineering-copilot-m365
npm install
npm run build

# 2. Deploy to Teams
# Upload to Teams App Studio
# Configure permissions

# 3. Use in Teams/M365
@PlatformCopilot analyze costs for subscription xyz
```

**📖 Complete M365 guide:** [INTEGRATIONS.md](./INTEGRATIONS.md#m365-copilot-integration)

---

## 🧠 LLM Configuration & Model Requirements

### Supported LLM Providers

Platform Engineering Copilot uses **Microsoft Semantic Kernel** and supports multiple LLM providers:

| Provider | Models | Recommended | Notes |
|----------|--------|-------------|-------|
| **Azure OpenAI** | GPT-4o, GPT-4 Turbo, GPT-4 | ✅ **GPT-4o** | Best performance, Azure Gov support |
| **OpenAI** | GPT-4o, GPT-4 Turbo, GPT-4 | ⚠️ GPT-4o | No Azure Gov compliance |
| **Ollama** | llama3.1, mistral, codellama | ❌ Not recommended | Local testing only, limited function calling |

### Recommended Configuration

**Production (Azure Government)**
```json
{
  "Gateway": {
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.us/",
      "ApiKey": "your-api-key-or-use-managed-identity",
      "DeploymentName": "gpt-4o",
      "ChatDeploymentName": "gpt-4o",
      "EmbeddingDeploymentName": "text-embedding-ada-002",
      "UseManagedIdentity": false
    },
    "Azure": {
      "CloudEnvironment": "AzureGovernment"
    }
  }
}
```

**Development (Azure Commercial)**
```json
{
  "Gateway": {
    "AzureOpenAI": {
      "Endpoint": "https://your-resource.openai.azure.com/",
      "ApiKey": "your-api-key",
      "DeploymentName": "gpt-4o",
      "ChatDeploymentName": "gpt-4o",
      "EmbeddingDeploymentName": "text-embedding-ada-002"
    }
  }
}
```

### Model Requirements by Agent

Each specialized agent has different LLM requirements:

#### 1. **Orchestrator Agent** (Entry Point)
- **Model**: GPT-4o or GPT-4 Turbo
- **Context Window**: 128K tokens minimum
- **Temperature**: 0.0 (deterministic routing)
- **Why**: Routes requests to specialized agents, requires consistent decision-making
- **Token Usage**: Low (50-200 tokens per request)

#### 2. **Infrastructure Agent** 🏗️
- **Model**: GPT-4o (required for code generation)
- **Context Window**: 128K tokens (for large Bicep/Terraform templates)
- **Temperature**: 0.2 (balance creativity and accuracy)
- **Function Calling**: Required (15+ kernel functions)
- **Why**: Generates production-ready IaC templates, requires deep Azure knowledge
- **Token Usage**: High (2000-8000 tokens per template)
- **Special**: Integrates with Azure MCP Server for best practices

```bash
# Infrastructure Agent Functions:
- generate_infrastructure_template         # Bicep/Terraform generation
- generate_compliant_infrastructure_template  # FedRAMP/NIST templates
- generate_il_compliant_template           # DoD Impact Level templates
- design_network_topology                  # Network architecture
- set_azure_subscription                   # Context configuration
- search_azure_documentation               # Live Azure docs
```

#### 3. **Compliance Agent** 🛡️
- **Model**: GPT-4o or GPT-4 Turbo
- **Context Window**: 128K tokens (for NIST 800-53 control mappings)
- **Temperature**: 0.0 (strict compliance assessment)
- **Function Calling**: Required (10+ kernel functions)
- **Why**: Analyzes compliance gaps, maps NIST controls, validates configurations
- **Token Usage**: Medium (1000-5000 tokens per scan)
- **Knowledge Base**: 1000+ NIST controls, 500+ STIG rules

```bash
# Compliance Agent Functions:
- assess_nist_compliance                   # NIST 800-53 gap analysis
- scan_code_for_compliance                 # IaC compliance scanning
- generate_ato_documentation               # ATO package generation
- query_knowledge_base                     # DoD/Navy requirements
- review_pull_request                      # PR compliance review
```

#### 4. **Cost Management Agent** 💰
- **Model**: GPT-4o or GPT-4 Turbo
- **Context Window**: 32K tokens minimum
- **Temperature**: 0.1 (precise cost analysis)
- **Function Calling**: Required (8+ kernel functions)
- **Why**: Analyzes spending patterns, forecasts costs, recommends optimizations
- **Token Usage**: Medium (500-3000 tokens per analysis)

```bash
# Cost Management Functions:
- analyze_subscription_costs               # Cost breakdown
- get_cost_recommendations                 # Savings opportunities
- forecast_costs                           # Predictive analytics
- create_budget                            # Budget management
```

#### 5. **Discovery Agent** 🔍
- **Model**: GPT-4 Turbo or GPT-4o
- **Context Window**: 32K tokens minimum
- **Temperature**: 0.0 (accurate resource inventory)
- **Function Calling**: Required (12+ kernel functions)
- **Why**: Queries Azure resources, relationship mapping, dependency analysis
- **Token Usage**: Low-Medium (200-2000 tokens per query)

```bash
# Discovery Agent Functions:
- list_resources                           # Resource inventory
- get_resource_dependencies                # Dependency graph
- query_resource_graph                     # Azure Resource Graph queries
- get_resource_health                      # Health status
```

#### 6. **Security Agent** 🔐
- **Model**: GPT-4o (required for threat analysis)
- **Context Window**: 64K tokens minimum
- **Temperature**: 0.0 (precise security assessment)
- **Function Calling**: Required (10+ kernel functions)
- **Why**: Security scanning, vulnerability detection, threat modeling
- **Token Usage**: Medium-High (1000-6000 tokens per scan)

```bash
# Security Agent Functions:
- scan_security_vulnerabilities            # Vulnerability scanning
- assess_security_posture                  # Security baseline
- analyze_network_security                 # NSG analysis
- check_encryption_status                  # Encryption validation
```

#### 7. **Document Agent** 📄
- **Model**: GPT-4o or GPT-4 Turbo
- **Context Window**: 128K tokens (for long documents)
- **Temperature**: 0.3 (creative documentation)
- **Function Calling**: Required (8+ kernel functions)
- **Why**: Generates technical documentation, diagrams, runbooks
- **Token Usage**: High (3000-10000 tokens per document)

```bash
# Document Agent Functions:
- generate_architecture_diagram            # Mermaid diagrams
- generate_ssp_document                    # System Security Plan
- generate_runbook                         # Operations runbook
- search_documentation                     # Doc search
```

### Token Budget Planning

**Typical Request Costs:**

| Operation | Tokens (Input) | Tokens (Output) | Total Cost @ GPT-4o |
|-----------|----------------|-----------------|---------------------|
| Simple query | 100-500 | 50-200 | ~$0.002 |
| Template generation | 2000-5000 | 2000-6000 | ~$0.04-$0.08 |
| Compliance scan | 3000-8000 | 1000-4000 | ~$0.05-$0.10 |
| ATO documentation | 5000-15000 | 8000-20000 | ~$0.15-$0.30 |
| Multi-agent workflow | 8000-20000 | 5000-15000 | ~$0.15-$0.30 |

**Cost Optimization Tips:**
- Use caching for repeated queries (built-in with `IntelligentChatCacheService`)
- Enable agent selection to avoid unnecessary LLM calls
- Use fast-path routing for simple commands
- Set appropriate context window limits per agent

### Temperature Settings by Task

> **⚠️ Planned Feature**: Per-agent temperature configuration is not yet implemented. Currently, temperature is hardcoded in each agent (e.g., EnvironmentAgent uses 0.3). This feature is planned for a future release.

**Recommended Temperature Values (for future implementation):**

| Agent | Temperature | Reasoning |
|-------|-------------|-----------|
| Orchestrator | 0.0 | Deterministic routing decisions |
| Infrastructure | 0.2 | Balance creativity and accuracy for code generation |
| Compliance | 0.0 | Strict, deterministic compliance assessment |
| Cost Management | 0.1 | Precise analysis with slight flexibility |
| Discovery | 0.0 | Accurate resource queries and inventory |
| Security | 0.0 | Precise security scanning and threat detection |
| Document | 0.3 | Creative documentation and diagram generation |

**Current Behavior:**
- All agents currently use the same Azure OpenAI deployment
- Temperature is set individually in each agent's code
- No configuration-based override available yet

### Model Limitations & Workarounds

#### GPT-4 Turbo Limitations
- **Function Calling**: Sometimes misses required parameters
- **Workaround**: Use GPT-4o or add explicit parameter validation
- **Code Generation**: May produce incomplete templates
- **Workaround**: Use post-processing validation

#### GPT-4o Strengths
- ✅ **Best function calling accuracy** (99%+ on kernel function selection)
- ✅ **Fastest response times** (2-3x faster than GPT-4 Turbo)
- ✅ **Better code generation** for Bicep/Terraform
- ✅ **128K context window** (handles large templates)
- ✅ **Structured outputs** (better JSON formatting)

#### Azure Government vs Commercial
- **Azure Government**: GPT-4o, GPT-4 Turbo available in `usgovvirginia`, `usgovarizona`
- **Endpoint**: Use `.azure.us` domain (e.g., `https://your-resource.openai.azure.us/`)
- **Compliance**: FedRAMP High authorized, IL4/IL5 compatible
- **Latency**: Slightly higher than commercial (50-100ms additional)

### Environment Variables Reference

```bash
# Required for all agents
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT=gpt-4o
AZURE_OPENAI_CHAT_DEPLOYMENT=gpt-4o
AZURE_OPENAI_EMBEDDING_DEPLOYMENT=text-embedding-ada-002

# Optional - Managed Identity (Production)
AZURE_OPENAI_USE_MANAGED_IDENTITY=true

# Optional - Model overrides per agent
INFRASTRUCTURE_AGENT_MODEL=gpt-4o
COMPLIANCE_AGENT_MODEL=gpt-4o
COST_AGENT_MODEL=gpt-4-turbo
```

### Testing Different Models

```bash
# Test with GPT-4o (recommended)
export AZURE_OPENAI_DEPLOYMENT=gpt-4o
docker-compose up -d platform-mcp

# Test with GPT-4 Turbo (fallback)
export AZURE_OPENAI_DEPLOYMENT=gpt-4-turbo
docker-compose up -d platform-mcp

# Monitor token usage
docker logs platform-mcp 2>&1 | grep "Token usage"
```

### Troubleshooting LLM Issues

**Problem: "Model not found" error**
```bash
# Solution: Verify deployment name
az cognitiveservices account deployment list \
  --name your-openai-resource \
  --resource-group your-rg
```

**Problem: High token costs**
```bash
# Solution: Enable caching and agent selection
{
  "AgentConfiguration": {
    "EnableAgentSelection": true,
    "CacheTTLMinutes": 30
  }
}
```

**Problem: Slow responses**
```bash
# Solution: Use GPT-4o instead of GPT-4 Turbo
# GPT-4o is 2-3x faster for most operations
AZURE_OPENAI_DEPLOYMENT=gpt-4o
```

**Problem: Function calling failures**
```bash
# Solution: Ensure using GPT-4o or GPT-4 Turbo
# GPT-3.5 and older models don't support function calling
```

**📖 Learn more:**
- [ARCHITECTURE.md](./ARCHITECTURE.md#llm-integration) - Detailed LLM architecture
- [Azure OpenAI Documentation](https://learn.microsoft.com/en-us/azure/ai-services/openai/)

---

## ✅ Verification & Testing

### Health Checks

```bash
# MCP Server
curl http://localhost:5100/health
# Expected: {"status":"Healthy","results":{...}}

# Chat Service (if running)
curl http://localhost:5001/health

# Admin API (if running)
curl http://localhost:5002/health

# Admin Client (if running)
curl http://localhost:5003/health
```

### MCP Tools Test

```bash
# List available MCP tools
curl http://localhost:5100/mcp/tools

# Test infrastructure query
curl -X POST http://localhost:5100/mcp/chat \
  -H "Content-Type: application/json" \
  -d '{"message":"List all VMs in my subscription"}'
```

### Natural Language Queries

```bash
# Test via Chat UI (if running)
# Navigate to http://localhost:5001

# Try these queries:
# - "Show me all VMs in my subscription"
# - "Analyze costs for resource group rg-prod"
# - "Check compliance status for AKS clusters"
# - "Generate a Bicep template for a web app"
```

**📖 Complete test cases:** [NATURAL-LANGUAGE-TEST-CASES.md](./NATURAL-LANGUAGE-TEST-CASES.md)

### Run Automated Tests

```bash
# Unit tests
dotnet test tests/Platform.Engineering.Copilot.Tests.Unit

# Integration tests (requires Azure credentials)
dotnet test tests/Platform.Engineering.Copilot.Tests.Integration

# All tests
dotnet test Platform.Engineering.Copilot.sln
```

---

## 🎯 Feature Examples

### Infrastructure Provisioning

**Generate Bicep Template**
```bash
# Via Chat UI or MCP
"Generate a Bicep template for a web app with:
- App Service Plan (B1 tier)
- Web App with .NET 8 runtime
- Azure SQL Database (Basic tier)
- Application Insights
- All resources in resource group rg-prod"

# Returns complete Bicep template ready to deploy
```

**Generate Terraform**
```bash
"Create Terraform configuration for:
- AKS cluster with 3 nodes
- Azure Container Registry
- Log Analytics workspace
- Network security groups
- All in East US region"

# Returns modular Terraform files (main.tf, variables.tf, outputs.tf)
```

**Kubernetes Manifests**
```bash
"Generate Kubernetes deployment for Node.js application:
- Deployment with 3 replicas
- Service (LoadBalancer)
- ConfigMap for environment variables
- HorizontalPodAutoscaler (2-10 pods)"

# Returns complete K8s manifests
```

**📖 Learn more:** [ARCHITECTURE.md](./ARCHITECTURE.md) - Infrastructure agent capabilities and template generation

---

### Cost Analysis & Optimization

**Analyze Resource Group Costs**
```bash
"Analyze costs for resource group rg-prod over the last 30 days"

# Returns:
# - Total costs by service
# - Breakdown by resource
# - Cost trends
# - Optimization recommendations
```

**Cost Optimization Recommendations**
```bash
"What can I do to reduce costs in my subscription?"

# Returns:
# - Underutilized VMs (recommend downsizing)
# - Unattached disks (recommend deletion)
# - Reserved instance opportunities
# - Shutdown schedules for dev/test environments
# - Estimated savings per recommendation
```

**Budget & Alerts**
```bash
"Create a budget of $5000/month for resource group rg-prod with:
- Alert at 80% threshold
- Alert at 100% threshold
- Email notifications to admin@company.com"

# Creates Azure budget with alerts
```

**Cost Forecasting**
```bash
"Predict my Azure costs for next quarter based on current usage"

# Returns:
# - Projected costs by month
# - Growth trends
# - Anomaly predictions
# - Confidence intervals
```

**📖 Learn more:** [ARCHITECTURE.md](./ARCHITECTURE.md) - Cost Management agent features and predictive analytics

---

### Compliance & Security

**RMF/STIG Compliance Check**
```bash
"Check NIST 800-53 compliance for AKS cluster aks-prod"

# Returns:
# - Control family assessment (AC, AU, CM, IA, etc.)
# - Gap analysis
# - Non-compliant configurations
# - Remediation steps
# - STIG checklist status
```

**NIST Control Lookup**
```bash
"Show me all NIST 800-53 controls for Azure SQL Database"

# Returns:
# - Applicable controls (AC-2, AC-3, AU-2, etc.)
# - Implementation guidance
# - Azure Policy mappings
# - DoD STIG mappings
```

**Vulnerability Scan**
```bash
"Scan resource group rg-prod for security vulnerabilities"

# Returns:
# - Open ports and exposed services
# - Unencrypted storage accounts
# - Missing network security groups
# - Outdated TLS versions
# - Missing Azure Policy assignments
# - Severity ratings (Critical, High, Medium, Low)
```

**ATO Documentation Generation**
```bash
"Generate ATO documentation package for subscription xyz including:
- System Security Plan (SSP)
- Security Assessment Report (SAR)
- Plan of Action & Milestones (POA&M)
- Control Implementation Summary"

# Generates complete ATO package with NIST/STIG mappings
```

**📖 Learn more:** 
- [KNOWLEDGE-BASE.md](./KNOWLEDGE-BASE.md) - Complete RMF/STIG compliance guide
- [PHASE1.md](./PHASE1.md) - Current compliance status (98%)
- [KNOWLEDGE-BASE-INTEGRATION-GUIDE.md](./KNOWLEDGE-BASE-INTEGRATION-GUIDE.md) - Integration patterns

---

### Resource Discovery & Monitoring

**Inventory Query**
```bash
"Show me all virtual machines in my subscription with their:
- Size
- Status (running/stopped)
- OS type
- Location
- Cost per month"

# Returns tabular inventory with details
```

**Health Status**
```bash
"Check health status of all resources in resource group rg-prod"

# Returns:
# - Resource health state
# - Active alerts
# - Recent failures
# - Performance metrics
# - Recommended actions
```

**Network Topology**
```bash
"Analyze network topology for resource group rg-prod"

# Returns:
# - Virtual networks and subnets
# - Network security groups and rules
# - Connected resources
# - Public IP addresses
# - Network flow diagram
```

**Dependency Mapping**
```bash
"Show me all dependencies for web app myapp-prod"

# Returns:
# - Database connections
# - Storage account dependencies
# - Key Vault references
# - Managed identity assignments
# - Dependency graph visualization
```

**📖 Learn more:** [ARCHITECTURE.md](./ARCHITECTURE.md) - Discovery agent capabilities and monitoring features

---

### Architecture Diagrams

**C4 Container Diagram**
```bash
"Generate C4 container diagram for resource group rg-microservices"

# Returns Mermaid diagram showing:
# - System boundaries
# - Containers (services)
# - Databases
# - Message queues
# - Relationships
```

**Sequence Diagram**
```bash
"Create sequence diagram for our PR review workflow:
1. Developer creates PR
2. GitHub webhook triggers review
3. Copilot scans IaC files
4. Posts inline comments
5. Approves or requests changes"

# Returns Mermaid sequence diagram
```

**Database ERD**
```bash
"Generate entity-relationship diagram for our user management database"

# Returns Mermaid ERD with:
# - Tables (Users, Roles, Permissions, Sessions)
# - Columns and data types
# - Primary/foreign key relationships
# - Cardinality indicators
```

**Deployment Flowchart**
```bash
"Create flowchart for our environment provisioning process"

# Returns Mermaid flowchart with:
# - Process steps
# - Decision points
# - Manual approval gates
# - Success/failure paths
```

**📖 Learn more:** [ARCHITECTURE.md](./ARCHITECTURE.md#architecture-diagram-generation) - Complete diagram generation guide (9 diagram types)

---

### Workspace & Template Creation

**Create Microservice Workspace**
```bash
"Create a workspace template for microservice with:
- .NET 8 Web API project structure
- Dockerfile for containerization
- Bicep templates for Azure resources
- GitHub Actions CI/CD pipeline
- README with setup instructions"

# Generates complete workspace ready to clone
```

**DoD Compliant Template**
```bash
"Generate IL5-compliant workspace template including:
- RMF/STIG baseline configuration
- Azure Policy assignments
- NIST 800-53 control mappings
- Security documentation templates
- Compliance checklist"

# Creates DoD-ready workspace with compliance baked in
```

**Custom Template**
```bash
"Create workspace template for Python data science project with:
- Jupyter notebooks setup
- requirements.txt
- Docker configuration
- Azure ML integration
- Data pipeline templates"

# Generates specialized workspace for data science
```

**📖 Learn more:** [WORKSPACE-CREATION.md](./WORKSPACE-CREATION.md) - Complete workspace creation guide and template system

---

### Environment Management

**Clone Environment**
```bash
"Clone production environment to staging with:
- Same infrastructure configuration
- Scaled-down SKUs (save costs)
- Separate database
- Isolated network"

# Creates staging environment mirroring production
```

**Environment Comparison**
```bash
"Compare dev and prod environments and show differences in:
- Resource configurations
- Network topology
- Security settings
- Costs"

# Returns detailed comparison report
```

**Auto-Scaling Configuration**
```bash
"Set up auto-scaling for AKS cluster aks-prod:
- Min 3 nodes, max 10 nodes
- Scale up at 70% CPU
- Scale down at 30% CPU
- Cool down period: 5 minutes"

# Configures cluster autoscaler
```

**📖 Learn more:** [ARCHITECTURE.md](./ARCHITECTURE.md) - Environment agent capabilities and lifecycle management

---

## 📚 Next Steps

### For Developers

1. **Understand the Architecture**
   - Read [ARCHITECTURE.md](./ARCHITECTURE.md) - System design and components
   - Review [AGENT-ORCHESTRATION.md](./AGENT-ORCHESTRATION.md) - Agent configuration

2. **Configure Agents**
   - Edit `appsettings.json` → `AgentConfiguration:{AgentName}:Enabled`
   - See [AGENT-ORCHESTRATION.md](./AGENT-ORCHESTRATION.md#agent-configuration)

3. **Explore Features**

   **Infrastructure as Code Generation**
   - Generate Bicep templates: "Create a Bicep template for a web app with SQL database"
   - Generate Terraform: "Generate Terraform for AKS cluster with monitoring"
   - Kubernetes manifests: "Create K8s deployment for Node.js app"
   - See all templates: [ARCHITECTURE.md](./ARCHITECTURE.md)

   **Cost Management**
   - Analyze costs: "Show me costs for resource group rg-prod"
   - Cost optimization: "What resources can I optimize to save money?"
   - Budget tracking: "Create budget alerts for subscription xyz"
   - Forecasting: "Predict costs for next quarter"

   **Compliance & Security**
   - RMF/STIG checks: "Check compliance for AKS cluster"
   - NIST controls: "Show me NIST 800-53 controls for Azure SQL"
   - Vulnerability scanning: "Scan resource group for security issues"
   - ATO preparation: "Generate ATO documentation for my environment"
   - [KNOWLEDGE-BASE.md](./KNOWLEDGE-BASE.md) - Complete compliance guide

   **Resource Discovery**
   - Inventory: "List all VMs in my subscription"
   - Health monitoring: "Show health status of all resources"
   - Configuration analysis: "Analyze network topology for rg-prod"
   - Dependency mapping: "Show dependencies for web app xyz"

   **Architecture Diagrams**
   - C4 diagrams: "Generate C4 container diagram for rg-prod"
   - Sequence diagrams: "Create sequence diagram for PR review workflow"
   - ERD diagrams: "Diagram database schema for user management"
   - Flowcharts: "Generate flowchart for deployment process"

   **Workspace Creation**
   - One-click templates: "Create workspace for microservice with CI/CD"
   - Custom templates: "Generate workspace with Terraform and GitHub Actions"
   - DoD compliance: "Create IL5-compliant workspace template"
   - [WORKSPACE-CREATION.md](./WORKSPACE-CREATION.md)

   **Environment Management**
   - Clone environments: "Clone production to staging"
   - Environment comparison: "Compare dev and prod configurations"
   - Automated scaling: "Set up auto-scaling for AKS cluster"

4. **Build Integrations**
   - [INTEGRATIONS.md](./INTEGRATIONS.md) - GitHub Copilot, M365, MCP API
   - [PR-REVIEW-INTEGRATION.md](./PR-REVIEW-INTEGRATION.md) - Automated PR reviews

### For DevOps Engineers

1. **Production Deployment**
   - [DEPLOYMENT.md](./DEPLOYMENT.md) - Complete deployment guide
   - [AUTHENTICATION.md](./AUTHENTICATION.md) - Managed identity setup

2. **Monitoring**
   - [DEPLOYMENT.md](./DEPLOYMENT.md#monitoring--observability) - Application Insights, Prometheus

3. **Security**
   - [DEPLOYMENT.md](./DEPLOYMENT.md#security-considerations) - HTTPS, secrets management

### For Compliance Teams

1. **Compliance Status**
   - [PHASE1.md](./PHASE1.md) - Phase 1 status (98% complete)
   - [PHASE2.md](./PHASE2.md) - Phase 2 roadmap

2. **RMF/STIG Knowledge Base**
   - [KNOWLEDGE-BASE.md](./KNOWLEDGE-BASE.md) - Complete compliance guide
   - [KNOWLEDGE-BASE-INTEGRATION-GUIDE.md](./KNOWLEDGE-BASE-INTEGRATION-GUIDE.md) - Integration patterns

### For Architects

1. **System Design**
   - [ARCHITECTURE.md](./ARCHITECTURE.md) - Complete architecture
   - [AGENT-ORCHESTRATION.md](./AGENT-ORCHESTRATION.md) - Agent patterns

2. **Advanced Topics**
   - [KNOWLEDGE-BASE-ARCHITECTURE-DIAGRAM.md](./KNOWLEDGE-BASE-ARCHITECTURE-DIAGRAM.md) - KB architecture
   - [DEVELOPMENT.md](./DEVELOPMENT.md) - Deep dive into codebase

---

## 🆘 Troubleshooting

### Common Issues

**Issue: "dotnet: command not found"**
```bash
# Install .NET 9.0 SDK
# macOS: brew install dotnet
# Windows: Download from https://dotnet.microsoft.com/download
```

**Issue: "Azure authentication failed"**
```bash
# Login to Azure
az login

# Set subscription
az account set --subscription "your-subscription-id"

# Verify
az account show
```

**Issue: "Discovery Agent returns no resources" or "Taking too long"**

This means the Docker container doesn't have access to Azure credentials.

```bash
# 1. Ensure you're logged in to Azure on your HOST machine
az login --use-device-code
az account set --subscription "your-subscription-id"

# 2. Verify ~/.azure directory exists
ls -la ~/.azure/

# 3. Restart Docker container to pick up credentials
docker-compose -f docker-compose.essentials.yml down
docker-compose -f docker-compose.essentials.yml up -d

# 4. Check container can access Azure
docker exec plaform-engineering-copilot-mcp ls -la /root/.azure

# 5. Watch logs for authentication errors
docker logs -f plaform-engineering-copilot-mcp | grep -i "auth\|credential\|azure"
```

**Root Cause:** The container mounts `~/.azure:/root/.azure:ro` to use your Azure CLI credentials. If you haven't run `az login`, this directory is empty and DefaultAzureCredential fails silently.

**Solution:** Always run `az login` before starting Docker containers.

**Issue: "Database migration failed"**
```bash
# Ensure EF tools installed
dotnet tool update --global dotnet-ef

# Delete database and recreate
rm platform_engineering_copilot_management.db
cd src/Platform.Engineering.Copilot.Data
dotnet ef database update
```

**Issue: "Docker container won't start"**
```bash
# Check logs
docker compose logs -f platform-mcp

# Verify environment variables
docker compose config

# Restart services
docker compose down
docker compose up -d
```

**Issue: "MCP Server returns 500 error"**
```bash
# Check logs
docker compose logs platform-mcp

# Verify Azure credentials in .env
cat .env | grep AZURE

# Test Azure connection
az account show
```

**Issue: "Agent not responding"**
```bash
# Check agent configuration
cat src/Platform.Engineering.Copilot.Mcp/appsettings.json | grep AgentConfiguration

# Verify agent is enabled
# See AGENT-ORCHESTRATION.md for configuration
```

### Getting Help

- **Documentation**: See [docs/README.md](./README.md) for complete index
- **Issues**: Create issue at [GitHub Issues](https://github.com/azurenoops/platform-engineering-copilot/issues)
- **Authentication**: See [AUTHENTICATION.md](./AUTHENTICATION.md)
- **Deployment**: See [DEPLOYMENT.md](./DEPLOYMENT.md)
- **Development**: See [DEVELOPMENT.md](./DEVELOPMENT.md)

---

## 📖 Additional Documentation

| Document | Purpose |
|----------|---------|
| **[AUTHENTICATION.md](./AUTHENTICATION.md)** | Azure authentication setup |
| **[CAC-AUTHENTICATION.md](../releases/CAC-AUTHENTICATION.md)** | CAC/PIV authentication for Azure Government (v0.7.0) |
| **[DEPLOYMENT.md](./DEPLOYMENT.md)** | Production deployment guide |
| **[DEVELOPMENT.md](./DEVELOPMENT.md)** | Development deep dive |
| **[ARCHITECTURE.md](./ARCHITECTURE.md)** | System architecture |
| **[INTEGRATIONS.md](./INTEGRATIONS.md)** | GitHub Copilot, M365, MCP API |
| **[AGENT-ORCHESTRATION.md](./AGENT-ORCHESTRATION.md)** | Agent configuration |
| **[KNOWLEDGE-BASE.md](./KNOWLEDGE-BASE.md)** | RMF/STIG compliance |
| **[WORKSPACE-CREATION.md](./WORKSPACE-CREATION.md)** | Workspace templates |
| **[PHASE1.md](./PHASE1.md)** | Compliance status (98%) |

---

**Document Version:** 1.0  
**Last Updated:** November 2025  
**Status:** ✅ Production Ready

**Supersedes:** N/A (New consolidated quick-start guide)
