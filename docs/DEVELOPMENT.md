# Development Guide

**Last Updated:** October 29, 2025  
**Version:** 2.1

This comprehensive guide covers the MCP-centric architecture, development setup, contribution guidelines, and API documentation for the Platform Engineering Copilot.

## 📋 Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Development Setup](#development-setup)
3. [Project Structure](#project-structure)
4. [Contributing Guidelines](#contributing-guidelines)
5. [API Reference](#api-reference)
6. [Testing](#testing)
7. [Building and Packaging](#building-and-packaging)
8. [Debugging](#debugging)

---

## 🏗️ Architecture Overview

### System Architecture

The Platform Engineering Copilot is now an **MCP-centric platform** with dual-mode operation. The MCP Server (port 5100) orchestrates six specialized AI agents and services both web clients (HTTP) and AI clients (stdio MCP protocol).

```mermaid
graph TB
  subgraph "Client Layer"
    ChatUI[Platform Chat :5001]
    AdminUI[Admin Client :5003]
    MCPClients[GitHub Copilot / Claude Desktop]
  end

  subgraph "MCP Server Layer"
    MCP[MCP Server :5100\nHTTP + stdio]
  end
    ```
    platform-engineering-copilot/
    ├── Platform.Engineering.Copilot.sln               # Solution root
    ├── appsettings.json                               # Shared configuration (loaded by Admin API)
    ├── docs/                                          # Architecture, development, integration guides
    ├── extensions/                                    # MCP extension packages (GitHub, M365)
    ├── infra/                                         # Bicep and Terraform infrastructure modules
    ├── scripts/                                       # Utility scripts (Docker, data seeding, tooling)
    ├── src/
    │   ├── Platform.Engineering.Copilot.Mcp/          # Dual-mode MCP server (stdio + HTTP bridge)
    │   │   ├── Server/                                # Minimal API bridge for /mcp/chat
    │   │   └── Tools/                                 # MCP tool surface backed by Semantic Kernel
    │   ├── Platform.Engineering.Copilot.Chat/         # Chat service + React SPA host
    │   │   ├── Controllers/                           # REST endpoints for conversations/messages
    │   │   ├── Hubs/                                  # SignalR hubs for streaming responses
    │   │   ├── Services/                              # Chat orchestration services
    │   │   └── ClientApp/                             # React 18 front-end with Tailwind
    │   ├── Platform.Engineering.Copilot.Admin.API/    # Admin REST API (Swagger-enabled)
    │   │   ├── Controllers/                           # Template, deployment, governance endpoints
    │   │   └── Services/                              # Business logic wiring to Core & Data
    │   ├── Platform.Engineering.Copilot.Admin.Client/ # Admin SPA host + React client
    │   │   ├── Controllers/                           # Razor fallback endpoints
    │   │   └── ClientApp/                             # React 18 admin dashboard
    │   ├── Platform.Engineering.Copilot.Core/         # Multi-agent orchestration, plugins, services
    │   │   ├── Plugins/                               # Semantic Kernel plugins per agent domain
    │   │   ├── Services/                              # Cost, compliance, infrastructure engines
    │   │   └── Models/                                # Domain models shared across services
    │   └── Platform.Engineering.Copilot.Data/         # EF Core context, migrations, seeding
    │       ├── Context/                               # `PlatformEngineeringCopilotContext`
    │       ├── Entities/                              # Persistent entity definitions
    │       ├── Migrations/                            # EF Core migrations history
    │       └── Seed/                                  # Optional data seeding helpers
    ├── tests/
    │   ├── Platform.Engineering.Copilot.Tests.Unit/   # xUnit + FluentAssertions unit tests
    │   ├── Platform.Engineering.Copilot.Tests.Integration/
    │   └── Platform.Engineering.Copilot.Tests.Manual/
    ├── docker-compose.yml                             # Full platform deployment
    ├── docker-compose.dev.yml                         # Hot-reload friendly developer compose file
    ├── DOCKER.md                                      # Container orchestration documentation
    └── README.md                                      # Project overview and quick start
    ```
  - Provides services for infrastructure provisioning, compliance assessments, cost analysis, security scanning, documentation automation
  - Shared abstractions used by MCP Server, Platform Chat, and Admin services

#### Data Layer (`Platform.Engineering.Copilot.Data`)
- **Technology**: Entity Framework Core 9.0
- **Responsibilities**:
  - Hosts the consolidated `PlatformEngineeringCopilotContext` for templates, deployments, approvals, and analytics
  - Provides migrations, seed scripts, and data access services consumed by MCP, Chat, and Admin workloads
  - Ships with SQLite by default and can switch to SQL Server for shared environments

#### Test Projects (`Platform.Engineering.Copilot.Tests.*`)
- **Technology**: xUnit, FluentAssertions, AutoFixture
- **Responsibilities**:
  - Unit, integration, and manual tests covering MCP Server, agents, web applications
  - Regression validation for agent workflows and infrastructure provisioning scenarios

### Technology Stack

#### Backend
- **.NET 9.0 / C# 12** across all services
- **ASP.NET Core 9.0** for MCP HTTP surface and Admin API
- **Entity Framework Core 9.0** with shared `PlatformEngineeringCopilotContext`
- **Microsoft.SemanticKernel 1.26.0** for agent orchestration and function calling
- **SignalR 1.1** for streaming responses to Platform Chat
- **Serilog 4.2+** for structured logging (console, file, Application Insights)
- **Swashbuckle 9.0.5** for Admin API Swagger documentation

#### Frontend
- **React 18** single-page applications (Admin Client)
- **ASP.NET Core Razor + React hybrid** for Platform Chat (SignalR streaming UI)
- **TypeScript 4.9** for client-side typing
- **Tailwind CSS 3.3** for utility-first styling
- **Axios** for Admin API and MCP HTTP calls

#### AI & ML
- **Azure OpenAI GPT-4o** (primary LLM) with function calling enabled
- **Semantic Kernel Plugins** (`InfrastructurePlugin`, `CompliancePlugin`, `CostManagementPlugin`, etc.)
- **ManagedAgentRouter** for intent detection and multi-agent handoffs
- **Context Memory Providers** for session persistence across HTTP and stdio modes

#### Data & Messaging
- **SQLite** (default development store for environment management + chat transcripts)
- **SQL Server 2022** (optional shared database for team environments)
- **Redis** (optional distributed cache for chat sessions and rate limiting)
- **Azure Key Vault** for secrets and API keys

#### Cloud Integrations
- **Azure Resource Manager SDK** (Compute, Network, Storage, AppService, ContainerService)
- **Azure Policy Insights SDK** for compliance evaluation
- **Azure Cost Management API** for budgeting and optimization insights
- **Azure Monitor Query** for logs and metrics
- **GitHub Octokit** for repository automation (ATO documentation workflows)
- **KubernetesClient** for future multi-cloud agent operations

---

## 🛠️ Development Setup

### Prerequisites

- **.NET 9.0 SDK**
- **VS Code (C# Dev Kit)** or **Visual Studio 2022 17.11+**
- **Node.js 18 LTS**
- **SQL Server 2022** or Docker Desktop *(optional; SQLite is default)*
- **Azure CLI** *(optional but recommended)*
- **Docker Desktop**
- **Git**
- **Redis** *(optional cache provider for production parity)*

### 1. Environment Setup

```bash
git clone https://github.com/azurenoops/platform-engineering-copilot.git
cd platform-engineering-copilot

dotnet restore Platform.Engineering.Copilot.sln
dotnet build Platform.Engineering.Copilot.sln
dotnet test Platform.Engineering.Copilot.sln

npm install --prefix src/Platform.Engineering.Copilot.Chat/ClientApp
npm install --prefix src/Platform.Engineering.Copilot.Admin.Client/ClientApp
```

### 2. Database Setup

#### Environment Management Database (default: SQLite)

```bash
dotnet tool update --global dotnet-ef
dotnet ef database update \
  --project src/Platform.Engineering.Copilot.Data/Platform.Engineering.Copilot.Data.csproj
```

This creates `platform_engineering_copilot_management.db` at the repository root. Use `ConnectionStrings:DefaultConnection` in `appsettings.json` to change the location.

#### Switch to SQL Server

```bash
dotnet ef database update \
  --project src/Platform.Engineering.Copilot.Data/Platform.Engineering.Copilot.Data.csproj \
  --connection "Server=localhost,1433;Database=PlatformCopilot;User Id=sa;Password=YourStrongPassword!;TrustServerCertificate=true"
```

Set `DatabaseProvider` to `SqlServer` in the relevant configuration file to make SQL Server the default provider.

#### Chat Transcript Store

`Platform.Engineering.Copilot.Chat` uses SQLite (`chat.db`) and calls `EnsureCreated()` on startup. Override `ConnectionStrings:DefaultConnection` in `src/Platform.Engineering.Copilot.Chat/appsettings.Development.json` if you want SQL Server instead.

#### SQL Server via Docker

```bash
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong@Passw0rd" \
   -p 1433:1433 --name platform-engineering-sql \
   -d mcr.microsoft.com/mssql/server:2022-latest
```

Update `ConnectionStrings:SqlServerConnection` to `Server=localhost,1433` and rerun migrations.

### 3. Azure Setup (Optional)

```bash
# Azure Government
az cloud set --name AzureUSGovernment
az login

# Azure Commercial (uncomment if applicable)
# az cloud set --name AzureCloud
# az login

dotnet clean Platform.Engineering.Copilot.sln
dotnet restore Platform.Engineering.Copilot.sln
dotnet build Platform.Engineering.Copilot.sln --configuration Debug

# Build individual services in Release if needed
dotnet build src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj --configuration Release
dotnet build src/Platform.Engineering.Copilot.Chat/Platform.Engineering.Copilot.Chat.csproj --configuration Release
dotnet build src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj --configuration Release
dotnet build src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj --configuration Release
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/path/to/platform_engineering_copilot_management.db",
    "SqlServerConnection": "Server=localhost,1433;Database=PlatformCopilot;User Id=sa;Password=YourStrongPassword!;TrustServerCertificate=true"
dotnet build Platform.Engineering.Copilot.sln --configuration Release --no-restore

dotnet publish src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj \
  --configuration Release \
  --output ./publish/mcp \
  --runtime linux-x64 \
  --self-contained false

dotnet publish src/Platform.Engineering.Copilot.Chat/Platform.Engineering.Copilot.Chat.csproj \
  --configuration Release \
  --output ./publish/chat \
  --runtime linux-x64 \
  --self-contained false

dotnet publish src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj \
  --configuration Release \
  --output ./publish/admin-api \
  --runtime linux-x64 \
  --self-contained false

# Build the Admin SPA assets before publishing the host
npm run build --prefix src/Platform.Engineering.Copilot.Admin.Client/ClientApp
dotnet publish src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj \
  --configuration Release \
  --output ./publish/admin-client \
  --runtime linux-x64 \
  --self-contained false
    "AzureOpenAI": {
      "Endpoint": "https://your-openai-endpoint/",
      "ApiKey": "<api-key>",
      "DeploymentName": "gpt-4o",
      "ChatDeploymentName": "gpt-4o",
      "EmbeddingDeploymentName": "text-embedding-ada-002"
```dockerfile
# src/Platform.Engineering.Copilot.Mcp/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj", "src/Platform.Engineering.Copilot.Mcp/"]
COPY ["src/Platform.Engineering.Copilot.Core/Platform.Engineering.Copilot.Core.csproj", "src/Platform.Engineering.Copilot.Core/"]
RUN dotnet restore "src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj"

COPY . .
WORKDIR "/src/src/Platform.Engineering.Copilot.Mcp"
RUN dotnet publish "Platform.Engineering.Copilot.Mcp.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Mcp.dll", "--http"]

> Keep secrets outside source control. Use environment variables, `.env` (for Docker), or [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets).

#### User Secrets (optional)

docker build -t platform-copilot-mcp:latest -f src/Platform.Engineering.Copilot.Mcp/Dockerfile .
docker build -t platform-copilot-chat:latest -f src/Platform.Engineering.Copilot.Chat/Dockerfile .
docker build -t platform-copilot-admin-api:latest -f src/Platform.Engineering.Copilot.Admin.API/Dockerfile .
docker build -t platform-copilot-admin-client:latest -f src/Platform.Engineering.Copilot.Admin.Client/Dockerfile .

### 5. Running the Application

#### MCP Server (dual mode)

```bash
# stdio mode for GitHub Copilot / Claude Desktop
dotnet run --project src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj

# HTTP bridge for web clients (default port 5100)
    <TargetFramework>net9.0</TargetFramework>
```

#### Platform Chat (REST + SignalR)

```bash
dotnet run --project src/Platform.Engineering.Copilot.Chat/Platform.Engineering.Copilot.Chat.csproj --urls http://0.0.0.0:5001
npm start --prefix src/Platform.Engineering.Copilot.Chat/ClientApp
```

Run `npm run build --prefix src/Platform.Engineering.Copilot.Chat/ClientApp` if you prefer serving static assets instead of the React dev server.

#### Admin API & Admin Client

```bash
dotnet run --project src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj --urls http://0.0.0.0:5002
dotnet run --project src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj --urls http://0.0.0.0:5003
npm start --prefix src/Platform.Engineering.Copilot.Admin.Client/ClientApp
```

#### IDE Tooling

- Visual Studio: configure multiple startup projects (MCP HTTP, Chat, Admin API, Admin Client).
- VS Code: define a compound launch configuration targeting the same projects.

#### Docker Compose (Development)

```bash
docker-compose -f docker-compose.dev.yml up --build
docker-compose logs -f platform-mcp
```

---

## 📁 Project Structure

### Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally
3. **Create a feature branch** from `main`
4. **Make your changes** following our coding standards
5. **Write tests** for new functionality
6. **Run all tests** to ensure nothing breaks
7. **Submit a pull request** with a clear description

### Branch Naming Convention

```
feature/description       # New features
bugfix/description       # Bug fixes
hotfix/description       # Critical fixes
docs/description         # Documentation updates
refactor/description     # Code refactoring
```

Examples:
- `feature/azure-cost-optimization`
- `bugfix/mcp-command-parsing`
- `docs/api-reference-update`

### Coding Standards

#### C# Code Style

```csharp
// Prefer PascalCase for public members and methods
public sealed class EnvironmentProvisioningCoordinator : IEnvironmentProvisioningCoordinator
{
  // Use _camelCase for private readonly dependencies
  private readonly IInfrastructureProvisioningService _provisioningService;
  private readonly ITemplateStorageService _templateStorage;
  private readonly ILogger<EnvironmentProvisioningCoordinator> _logger;

  public EnvironmentProvisioningCoordinator(
    IInfrastructureProvisioningService provisioningService,
    ITemplateStorageService templateStorage,
    ILogger<EnvironmentProvisioningCoordinator> logger)
  {
    _provisioningService = provisioningService;
    _templateStorage = templateStorage;
    _logger = logger;
  }

  // Always use async/await for asynchronous operations
  public async Task<EnvironmentProvisioningResult> ProvisionAsync(
    EnvironmentProvisioningRequest request,
    CancellationToken cancellationToken = default)
  {
    ArgumentNullException.ThrowIfNull(request);

    var template = await _templateStorage.GetTemplateByIdAsync(
      request.TemplateId,
      cancellationToken);

    if (template is null)
    {
      return EnvironmentProvisioningResult.Fail("Template not found.");
    }

    var deploymentResult = await _provisioningService.DeployAsync(
      template,
      request.Parameters,
      cancellationToken);

    _logger.LogInformation("Provisioned environment {EnvironmentName} in resource group {ResourceGroup}",
      deploymentResult.EnvironmentName,
      deploymentResult.ResourceGroup);

    return EnvironmentProvisioningResult.Success(deploymentResult);
  }
}
```

#### Configuration Standards

```json
// Prefer PascalCase for section names and camelCase for individual keys
{
  "Gateway": {
    "Azure": {
      "SubscriptionId": "<subscription-id>",
      "TenantId": "<tenant-id>",
      "CloudEnvironment": "AzureUSGovernment",
      "UseManagedIdentity": false
    },
    "AzureOpenAI": {
      "Endpoint": "https://your-endpoint.openai.azure.us/",
      "DeploymentName": "gpt-4o"
    }
  },
  "McpServer": {
    "HttpPort": 5100,
    "EnableAuditLogging": true
  }
}
```

### Testing Requirements

#### Unit Tests

```csharp
public class EnvironmentManagementEngineTests
{
  private readonly EnvironmentManagementEngine _engine;
  private readonly Mock<IAzureResourceService> _azureResourceService = new();
  private readonly Mock<IDeploymentOrchestrationService> _orchestrator = new();

  public EnvironmentManagementEngineTests()
  {
    _engine = new EnvironmentManagementEngine(
      Mock.Of<ILogger<EnvironmentManagementEngine>>(),
      _orchestrator.Object,
      _azureResourceService.Object,
      Mock.Of<IGitHubServices>(),
      Mock.Of<ITemplateStorageService>(),
      Mock.Of<IDynamicTemplateGenerator>());
  }

  [Fact]
  public async Task ProvisionAsync_WithMissingResourceGroup_ReturnsFailure()
  {
    // Arrange
    var request = new EnvironmentCreationRequest
    {
      Name = "env-dev",
      ResourceGroup = string.Empty,
      Type = EnvironmentType.AKS,
      Location = "usgovvirginia"
    };

    // Act
    var result = await _engine.CreateEnvironmentAsync(request);

    // Assert
    result.Success.Should().BeFalse();
    result.ErrorMessage.Should().Contain("Resource group is required");
  }
}
```

#### Integration Tests

```csharp
public class ConversationsControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
  private readonly HttpClient _client;

  public ConversationsControllerTests(WebApplicationFactory<Program> factory)
  {
    _client = factory.WithWebHostBuilder(builder =>
    {
      builder.UseSetting("ConnectionStrings:DefaultConnection", "Data Source=:memory:");
    }).CreateClient();
  }

  [Fact]
  public async Task CreateConversation_ReturnsCreatedConversation()
  {
    // Arrange
    var payload = new { title = "Infra request", userId = "test-user" };

    // Act
    var response = await _client.PostAsJsonAsync("/api/conversations", payload);

    // Assert
    response.StatusCode.Should().Be(HttpStatusCode.Created);
  }
}
```

### Documentation Standards

#### XML Documentation

```csharp
/// <summary>
/// Executes a platform engineering tool with the specified parameters.
/// </summary>
/// <param name="toolName">The name of the tool to execute</param>
/// <param name="parameters">Dictionary of parameters for the tool</param>
/// <returns>A task representing the asynchronous operation with tool result</returns>
/// <exception cref="ArgumentNullException">Thrown when toolName is null or empty</exception>
/// <exception cref="ToolNotFoundException">Thrown when the specified tool is not found</exception>
public async Task<ToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
{
    // Implementation
}
```

#### README Updates

When adding new features, update relevant documentation:
- Tool descriptions in main README
- API endpoints in this development guide
- Configuration examples
- Usage examples

### Pull Request Process

1. **Ensure CI passes** - All tests must pass
2. **Update documentation** - Include relevant docs updates
3. **Add changelog entry** - Describe your changes
4. **Request review** - Tag relevant maintainers
5. **Address feedback** - Respond to review comments
6. **Squash and merge** - Clean up commit history

### Issue Reporting

When reporting bugs, include:
- **Steps to reproduce** the issue
- **Expected behavior** vs actual behavior
- **Environment details** (OS, .NET version, etc.)
- **Log outputs** and error messages
- **Screenshots** if applicable

Feature requests should include:
- **Use case description** - Why is this needed?
- **Proposed solution** - How should it work?
- **Alternatives considered** - Other approaches
- **Implementation notes** - Technical considerations

---

## 📡 API Reference

### Base URLs

- **MCP HTTP Bridge**: `http://localhost:5100`
- **Platform Chat API**: `http://localhost:5001`
- **Admin API**: `http://localhost:5002`
- **SignalR Hub**: `ws://localhost:5001/chathub`

### MCP HTTP Bridge

The MCP server exposes a minimal HTTP surface so that web clients can reuse the same multi-agent orchestration used by stdio integrations.

#### `POST /mcp/chat`

```json
{
  "message": "Generate a landing zone in usgovvirginia",
  "conversationId": "optional-session-id",
  "context": {
    "subscriptionId": "<subscription-id>",
    "environment": "dev"
  }
}
```

**Response (ChatMcpResult):**

```json
{
  "success": true,
  "response": "Created a landing zone template targeting usgovvirginia.",
  "conversationId": "b0f2dc71-c486-4e3a-9d83-33f3c44fd15f",
  "intentType": "infrastructure.provision",
  "confidence": 0.91,
  "toolExecuted": true,
  "toolResult": {
    "templatePath": "terraform/platform-landing-zone/main.tf"
  },
  "processingTimeMs": 742,
  "suggestions": [
    {
      "title": "Validate cost baseline",
      "description": "Run the cost optimization agent for the new landing zone." ,
      "priority": "Medium"
    }
  ]
}
```

#### `GET /health`

Returns simple status metadata:

```json
{
  "status": "healthy",
  "mode": "dual (http+stdio)",
  "server": "Platform Engineering Copilot MCP",
  "version": "0.7.1"
}
```

> **Tip:** Add `conversationId` to correlate chat sessions across HTTP and SignalR clients.

### Platform Chat REST API

The Chat service hosts both REST endpoints and SignalR hubs for managing conversations and messages.

| Endpoint | Description |
| --- | --- |
| `GET /api/conversations` | List conversations for a user (`userId` query optional). |
| `POST /api/conversations` | Create a new conversation (`title`, `userId`). |
| `GET /api/conversations/{conversationId}` | Retrieve a specific conversation. |
| `DELETE /api/conversations/{conversationId}` | Soft-delete a conversation. |
| `GET /api/conversations/search?query=` | Full-text search across transcripts. |
| `GET /api/messages?conversationId=` | Paginated message history. |
| `POST /api/messages` | Send a message and stream MCP response. |
| `POST /api/messages/{messageId}/attachments` | Upload message attachments (<=10 MB). |

#### Message Example

```http
POST /api/messages
Content-Type: application/json

{
  "conversationId": "b0f2dc71-c486-4e3a-9d83-33f3c44fd15f",
  "userId": "john.doe",
  "message": "Scan subscription 1234 for FedRAMP gaps"
}
```

**Response:** returns the persisted chat message while the MCP server streams updates over SignalR.

### SignalR Channels

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("http://localhost:5001/chathub")
  .build();

connection.on("ReceiveMessage", (user, message) => {
  console.log(`${user}: ${message}`);
});

connection.on("ReceiveToolResult", result => {
  renderToolOutput(result);
});

await connection.start();
await connection.invoke("SendMessage", "john.doe", "/mcp show_cost_trends subscription=1234");
```

### Admin API

Swagger UI is available at `http://localhost:5002` (development). Core controller routes include:

| Controller | Route | Highlights |
| --- | --- | --- |
| `TemplateAdminController` | `api/admin/templates` | CRUD for environment templates, validation, preview rendering. |
| `EnvironmentAdminController` | `api/admin/environments` | Environment inventory, status updates, lifecycle management. |
| `DeploymentAdminController` | `api/admin/deployments` | Trigger redeployments, fetch history, retrieve artifacts. |
| `GovernanceAdminController` | `api/admin/governance` | Policy evaluation, approval workflows, compliance snapshots. |
| `CostAdminController` | `api/admin/cost` | Cost trend reports, budget status, optimization insights. |
| `ServiceCreationAdminController` | `api/admin/ServiceCreation` | Navy Flankspeed ServiceCreation workflows. |

#### Create Template

```http
POST /api/admin/templates
Content-Type: application/json

{
  "templateName": "aks-standard",
  "serviceName": "AKS",
  "templateType": "Terraform",
  "infrastructure": {
    "format": "terraform",
    "content": "..."
  }
}
```

**Selected Response:**

```json
{
  "success": true,
  "templateId": "template_aks_standard",
  "message": "Template created successfully"
}
```

### Authentication & Authorization

- Development builds run without authentication.
- Production deployments should front the MCP, Chat, and Admin APIs with Azure AD / Entra ID (see `docs/AZURE-AUTHENTICATION.md`).
- Secure secrets (OpenAI keys, GitHub tokens) via Azure Key Vault or user secrets.

### Error Handling

All services follow structured error payloads:

```json
{
  "success": false,
  "errors": [
    "Azure subscription 1234 not found"
  ],
  "traceId": "00-d1f6e1f2690f214a6f3a5b8fd3c0d4f1-88dc340c2a2d9546-01"
}
```

Include the `traceId` when reporting issues—the value maps to Serilog and Application Insights telemetry.

---

## 🧪 Testing

### Test Strategy

The project uses a comprehensive testing strategy with multiple levels:

1. **Unit Tests**: Test individual components in isolation
2. **Integration Tests**: Test component interactions
3. **End-to-End Tests**: Test complete user workflows
4. **Performance Tests**: Test system performance under load

### Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/Platform.Engineering.Copilot.Tests.Unit

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~EnvironmentManagementEngineTests.ProvisionAsync_WithMissingResourceGroup_ReturnsFailure"

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"
```

### Test Configuration

#### Test Database

Tests use an in-memory database for isolation:

```csharp
// In test setup
services.AddDbContext<PlatformEngineeringCopilotContext>(options =>
  options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
```

#### Mock Services

Use Moq for mocking dependencies:

```csharp
public class AzureCostManagementServiceTests
{
  private readonly Mock<IAzureCostClient> _costClient = new();
  private readonly AzureCostManagementService _service;

  public AzureCostManagementServiceTests()
  {
    _service = new AzureCostManagementService(
      _costClient.Object,
      Mock.Of<ILogger<AzureCostManagementService>>());
  }

  [Fact]
  public async Task GetCostBreakdownAsync_WhenClientReturnsValues_EmitsSameTotals()
  {
    // Arrange
    _costClient
      .Setup(client => client.GetCostBreakdownAsync("subscription-id", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
      .ReturnsAsync(new AzureCostBreakdown
      {
        TotalCost = 1250.42m,
        Services = new[] { new AzureServiceCost("Compute", 950.11m) }
      });

    // Act
    var result = await _service.GetCostBreakdownAsync("subscription-id", DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

    // Assert
    result.TotalCost.Should().Be(1250.42m);
    result.Services.Should().ContainSingle(service => service.Name == "Compute" && service.Cost == 950.11m);
  }
}
```

### Test Categories

#### Unit Tests

Focus on testing individual methods and classes:

```csharp
[TestClass]
public class McpCommandParserTests
{
    [TestMethod]
    [DataRow("/mcp azure_discover_resources subscription_id=test", "azure_discover_resources")]
    [DataRow("/mcp ato_compliance_scan resource_group=rg", "ato_compliance_scan")]
    public void ParseCommand_ValidInput_ExtractsToolName(string input, string expectedTool)
    {
        // Arrange
        var parser = new McpCommandParser();
        
        // Act
        var result = parser.ParseCommand(input);
        
        // Assert
        Assert.AreEqual(expectedTool, result.ToolName);
    }
}
```

#### Integration Tests

Test API endpoints and service interactions:

```csharp
[TestClass]
public class ToolsControllerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    
    public ToolsControllerIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }
    
    [TestMethod]
    public async Task ExecuteTool_AzureDiscovery_ReturnsValidData()
    {
        // Arrange
        var request = new ToolExecutionRequest
        {
            ToolName = "azure_discover_resources",
            Parameters = new Dictionary<string, object>
            {
                ["subscription_id"] = "test-subscription"
            }
        };
        
        // Act
        var response = await _client.PostAsJsonAsync("/api/tools/execute", request);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ToolExecutionResponse>();
        Assert.IsTrue(result.Success);
    }
}
```

#### Performance Tests

Test system performance under load:

```csharp
public class LoadTests : IClassFixture<WebApplicationFactory<Program>>
{
  private readonly HttpClient _client;

  public LoadTests(WebApplicationFactory<Program> factory)
  {
    _client = factory.CreateClient();
  }

  [Fact]
  public async Task ChatEndpoint_WithConcurrentRequests_CompletesSuccessfully()
  {
    const int concurrentRequests = 50;

    async Task<HttpResponseMessage> InvokeAsync(int index)
    {
      var payload = new { message = $"status check #{index}" };
      return await _client.PostAsJsonAsync("/mcp/chat", payload);
    }

    var responses = await Task.WhenAll(Enumerable.Range(0, concurrentRequests).Select(InvokeAsync));

    responses.Should().OnlyContain(response => response.IsSuccessStatusCode);
  }
}
```

### Code Coverage

Aim for high code coverage:

```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage" --results-directory ./coverage

# Generate HTML report (requires reportgenerator tool)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"coverage/**/coverage.cobertura.xml" -targetdir:coverage-report -reporttypes:Html
```

**Coverage Targets:**
- **Unit Tests**: 80%+ line coverage
- **Integration Tests**: 70%+ end-to-end coverage
- **Critical Paths**: 95%+ coverage for security and compliance code

---
## 🔨 Building and Packaging

### Build Process

#### Local Development Build

```bash
dotnet clean Platform.Engineering.Copilot.sln
dotnet restore Platform.Engineering.Copilot.sln
dotnet build Platform.Engineering.Copilot.sln --configuration Debug

# Build individual services in Release
dotnet build src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj --configuration Release
dotnet build src/Platform.Engineering.Copilot.Chat/Platform.Engineering.Copilot.Chat.csproj --configuration Release
dotnet build src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj --configuration Release
dotnet build src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj --configuration Release
```

#### Production Build

```bash
dotnet build Platform.Engineering.Copilot.sln --configuration Release --no-restore

dotnet publish src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained false \
  --output ./publish/mcp

dotnet publish src/Platform.Engineering.Copilot.Chat/Platform.Engineering.Copilot.Chat.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained false \
  --output ./publish/chat

dotnet publish src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained false \
  --output ./publish/admin-api

npm run build --prefix src/Platform.Engineering.Copilot.Admin.Client/ClientApp
dotnet publish src/Platform.Engineering.Copilot.Admin.Client/Platform.Engineering.Copilot.Admin.Client.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained false \
  --output ./publish/admin-client
```

### Docker Build

#### Multi-stage Docker Build

```dockerfile
# src/Platform.Engineering.Copilot.Mcp/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj", "src/Platform.Engineering.Copilot.Mcp/"]
COPY ["src/Platform.Engineering.Copilot.Core/Platform.Engineering.Copilot.Core.csproj", "src/Platform.Engineering.Copilot.Core/"]
RUN dotnet restore "src/Platform.Engineering.Copilot.Mcp/Platform.Engineering.Copilot.Mcp.csproj"

COPY . .
WORKDIR "/src/src/Platform.Engineering.Copilot.Mcp"
RUN dotnet publish "Platform.Engineering.Copilot.Mcp.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Mcp.dll", "--http"]
```

#### Build Docker Images

```bash
docker build -t platform-copilot-mcp:latest -f src/Platform.Engineering.Copilot.Mcp/Dockerfile .
docker build -t platform-copilot-chat:latest -f src/Platform.Engineering.Copilot.Chat/Dockerfile .
docker build -t platform-copilot-admin-api:latest -f src/Platform.Engineering.Copilot.Admin.API/Dockerfile .
docker build -t platform-copilot-admin-client:latest -f src/Platform.Engineering.Copilot.Admin.Client/Dockerfile .
```

### Package Management

#### NuGet Packages

Create reusable NuGet packages for shared components:

```xml
<!-- Platform.Engineering.Copilot.Core.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <PackageId>Platform.Engineering.Copilot.Core</PackageId>
    <PackageVersion>1.0.0</PackageVersion>
    <Authors>Platform Engineering Team</Authors>
    <Description>Core contracts and models for Platform Engineering Copilot</Description>
    <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
  </PropertyGroup>
</Project>
```

```bash
dotnet pack src/Platform.Engineering.Copilot.Core --configuration Release
dotnet nuget push src/Platform.Engineering.Copilot.Core/bin/Release/Platform.Engineering.Copilot.Core.1.0.0.nupkg \
  --source https://api.nuget.org/v3/index.json \
  --api-key <your-api-key>
```

### Versioning Strategy

Use semantic versioning (SemVer):

- **Major** – breaking changes
- **Minor** – new features, backward compatible
- **Patch** – bug fixes, backward compatible

Example: `1.2.3`

#### Automated Versioning

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <VersionPrefix>1.0.0</VersionPrefix>
    <VersionSuffix Condition="'$(Configuration)' == 'Debug'">dev</VersionSuffix>
  </PropertyGroup>
</Project>
```

---

## 🐛 Debugging

### Local Debugging

#### Visual Studio

1. Set breakpoints in the target project (MCP, Chat, Admin API, Admin Client).
2. In Solution Properties → Common Properties → Startup Project select **Multiple startup projects** and set:
   - `Platform.Engineering.Copilot.Mcp` → **Start** (HTTP mode)
   - `Platform.Engineering.Copilot.Chat` → **Start**
   - `Platform.Engineering.Copilot.Admin.API` → **Start**
   - `Platform.Engineering.Copilot.Admin.Client` → **Start**
3. Press F5. Visual Studio will launch each service with the configured ports (5100/5001/5002/5003).
4. Use **Debug → Windows → Output** and Serilog sinks for detailed diagnostics.

#### VS Code

1. Install C# extension
2. Use `.vscode/launch.json` configuration:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "MCP HTTP",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/src/Platform.Engineering.Copilot.Mcp/bin/Debug/net9.0/Platform.Engineering.Copilot.Mcp.dll",
      "args": ["--http", "--port", "5100"],
      "cwd": "${workspaceFolder}/src/Platform.Engineering.Copilot.Mcp",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    {
      "name": "Platform Chat",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/src/Platform.Engineering.Copilot.Chat/bin/Debug/net9.0/Platform.Engineering.Copilot.Chat.dll",
      "args": ["--urls", "http://0.0.0.0:5001"],
      "cwd": "${workspaceFolder}/src/Platform.Engineering.Copilot.Chat",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  ]
}
```

### Remote Debugging

#### Docker Container Debugging

```dockerfile
# Development Dockerfile with debugging support for Admin API
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj \
  -c Debug -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 5002

RUN apt-get update && apt-get install -y unzip procps \
  && rm -rf /var/lib/apt/lists/*
RUN curl -sSL https://aka.ms/getvsdbgsh | /bin/sh /dev/stdin -v latest -l /vsdbg

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Admin.API.dll"]
```

#### Attach to Process

```bash
# Find process ID
docker exec -it container-name ps aux | grep dotnet

# Attach debugger (in VS Code with C# extension)
# Command Palette → .NET: Attach to Process
```

### Logging and Diagnostics

#### Structured Logging with Serilog

```csharp
// Program.cs
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .WriteTo.Console()
        .WriteTo.File("logs/app.log", rollingInterval: RollingInterval.Day)
        .WriteTo.ApplicationInsights(context.Configuration.GetConnectionString("ApplicationInsights")));
```

#### Application Insights Integration

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=your-key..."
  },
  "Logging": {
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  }
}
```

#### Custom Telemetry

```csharp
public class PlatformToolService
{
    private readonly ILogger<PlatformToolService> _logger;
    private readonly TelemetryClient _telemetryClient;
    
    public async Task<ToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
    {
        using var activity = Activity.StartActivity($"ExecuteTool-{toolName}");
        
        _logger.LogInformation("Executing tool {ToolName} with parameters {Parameters}", 
            toolName, parameters);
        
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var result = await InternalExecuteAsync(toolName, parameters);
            
            _telemetryClient.TrackEvent("ToolExecuted", new Dictionary<string, string>
            {
                ["ToolName"] = toolName,
                ["Success"] = result.Success.ToString(),
                ["Duration"] = stopwatch.ElapsedMilliseconds.ToString()
            });
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolName);
            _telemetryClient.TrackException(ex);
            throw;
        }
    }
}
```

### Performance Profiling

#### dotnet-trace

```bash
# Install dotnet-trace
dotnet tool install --global dotnet-trace

# Collect trace
dotnet-trace collect --process-id <pid> --duration 00:00:30

# Analyze with PerfView or Visual Studio
```

#### Memory Profiling

```bash
# Install dotnet-dump
dotnet tool install --global dotnet-dump

# Create memory dump
dotnet-dump collect --process-id <pid>

# Analyze dump
dotnet-dump analyze dump.dmp
```

---

*For additional development support, see the [main documentation](DOCUMENTATION.md) or create an issue on GitHub.*