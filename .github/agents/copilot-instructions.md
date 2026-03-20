# platform-engineering-copilot-v2 Development Guidelines

Auto-generated from all feature plans. Last updated: 2026-02-22

## Active Technologies
- .NET 9.0 / C# 12 (001-platform-copilot-core)
- Microsoft Agents SDK (Microsoft.Agents.Protocols, Microsoft.Agents.Builder, Microsoft.Agents.Client) — multi-agent orchestration
- Semantic Kernel 1.26.0, Microsoft.Extensions.AI 9.1.0 — AI orchestration, function calling, IChatClient
- ModelContextProtocol 0.4.0-preview — MCP server (HTTP + stdio dual transport)
- Entity Framework Core 9.0 — SQL Server + SQLite fallback. Two DbContexts: `PlatformEngineeringCopilotContext` (16 entities), `ChatDbContext` (4 entities). 20 enumerations. Non-EF NistService models for OSCAL data.
- Serilog 4.2+ — structured logging (console+file dev, App Insights prod)
- SignalR 1.1 — real-time chat streaming
- Azure SDK (ARM, Policy, Cost Management, Defender, Resource Graph, Monitor)
- Blazor WASM — admin dashboard (port 5000)
- xUnit 2.9+, FluentAssertions, Moq — testing
- C# 12 / .NET 9.0 (net9.0 TFM across all projects) + `Microsoft.Extensions.AI` 9.1.0-preview.1.25064.3 (already in Core), `Azure.AI.OpenAI` (to be added to Core), `Microsoft.SemanticKernel` 1.26.0 (existing), `Microsoft.AspNetCore.SignalR` 1.1.0 (Chat project) (002-azure-openai-agents)
- N/A for this feature — conversation sessions remain in-memory (`ConversationSession` with `List<SessionMessage>`) (002-azure-openai-agents)
- C# 12 / .NET 9.0 + ASP.NET Core 9.0, EF Core 9.0 (SQL Server + InMemory), Serilog.AspNetCore, Swashbuckle.AspNetCore, Microsoft.AspNetCore.Authentication.JwtBearer, Azure.Identity (003-admin-api)
- SQL Server (production) / EF Core InMemory (dev/test), existing `PlatformEngineeringCopilotContext` (003-admin-api)
- C# / .NET 9.0 + Blazor WebAssembly 9.0.x, Blazored.Toast 4.2.1, Blazored.Modal 7.3.1, Blazored.LocalStorage 4.5.0, Bootstrap 5.3.2 (CDN), Font Awesome 6.5.1 (CDN) (004-admin-client)
- Browser localStorage (settings persistence only) (004-admin-client)
- C# / .NET 9.0 (net9.0) + Microsoft.Extensions.Caching.Memory (new), Microsoft.Extensions.Http.Polly (new), System.Diagnostics.Activity (BCL), xUnit 2.9.2, FluentAssertions 7.0.0, Moq 4.20.72 (005-nist-controls-foundation)
- IMemoryCache (in-memory), embedded JSON resources, offline fallback JSON file (005-nist-controls-foundation)
- C# / .NET 9.0 (net9.0) + Azure.ResourceManager.* (ARM SDK), Azure.ResourceManager.PolicyInsights, Azure.ResourceManager.SecurityCenter, Azure.Storage.Blobs, Entity Framework Core 9.0, Microsoft.Extensions.Caching.Memory, Polly v8 (006-ato-compliance-engine)
- SQL Server via EF Core (existing `PlatformEngineeringCopilotContext` with 18 DbSets), Azure Blob Storage (new — for evidence packages and certificates) (006-ato-compliance-engine)
- C# / .NET 9.0 + Microsoft.Extensions.AI, Azure.AI.OpenAI, Azure.ResourceManager, Entity Framework Core, SignalR, ModelContextProtocol SDK 0.4.0-preview.2 (007-remove-ato-compliance)
- EF Core with SQL Server (InMemory for tests); Azure Blob Storage (evidence — being removed) (007-remove-ato-compliance)

## Project Structure

```text
src/
├── Platform.Engineering.Copilot.Core/       # BaseAgent, BaseTool, DbContexts, Auth, NistService, Observability
├── Platform.Engineering.Copilot.Agents/     # 8 agents (Compliance, Infrastructure, Cost, Discovery, Environment, KB, Config, Security)
├── Platform.Engineering.Copilot.Mcp/        # MCP server (port 5100, HTTP + stdio)
├── Platform.Engineering.Copilot.Chat/       # ASP.NET Core Razor + SignalR (port 5001)
├── Platform.Engineering.Copilot.Admin.API/  # REST + Swagger (port 5050)
├── Platform.Engineering.Copilot.Admin.Client/ # Blazor WASM (port 5000)
├── Platform.Engineering.Copilot.State/      # Scaffold only
└── Platform.Engineering.Copilot.Channels/   # Scaffold only (GitHub/M365 extensions)

tests/
├── Platform.Engineering.Copilot.Tests.Unit/
├── Platform.Engineering.Copilot.Tests.Integration/
└── Platform.Engineering.Copilot.Tests.Manual/
```

## Commands

```bash
dotnet build Platform.Engineering.Copilot.sln
dotnet test
docker compose -f docker-compose.mcp.yml up
```

## Code Style

- .NET 9.0 / C# 12: Follow standard conventions
- All agents MUST extend BaseAgent, all tools MUST extend BaseTool (Constitution Principle II — NON-NEGOTIABLE)
- System prompts externalized in *.prompt.txt files
- All tool responses use platform-wide response envelope (FR-079)
- Canonical terms: "assessment" (not "scan"), "finding" (not "violation")

## Key Patterns

- **Auth**: CAC/PIV + PIM dual-gate for IL5/IL6. Dev bypass mode available.
- **NistService**: Dual-source OSCAL (GitHub fetch + embedded fallback for air-gapped)
- **Secrets**: Azure Key Vault with managed identity (prod), .env fallback (dev)
- **Accessibility**: WCAG 2.1 Level AA for all user-facing interfaces
- **Testing**: Test-first (Constitution Principle III — NON-NEGOTIABLE). 80%+ unit coverage.

## Recent Changes
- 007-remove-ato-compliance: Added C# / .NET 9.0 + Microsoft.Extensions.AI, Azure.AI.OpenAI, Azure.ResourceManager, Entity Framework Core, SignalR, ModelContextProtocol SDK 0.4.0-preview.2
- 006-ato-compliance-engine: Added C# / .NET 9.0 (net9.0) + Azure.ResourceManager.* (ARM SDK), Azure.ResourceManager.PolicyInsights, Azure.ResourceManager.SecurityCenter, Azure.Storage.Blobs, Entity Framework Core 9.0, Microsoft.Extensions.Caching.Memory, Polly v8
- 005-nist-controls-foundation: Added C# / .NET 9.0 (net9.0) + Microsoft.Extensions.Caching.Memory (new), Microsoft.Extensions.Http.Polly (new), System.Diagnostics.Activity (BCL), xUnit 2.9.2, FluentAssertions 7.0.0, Moq 4.20.72

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
