# Quickstart: Admin API

**Feature**: 003-admin-api  
**Date**: 2026-02-23

## Prerequisites

- .NET 9.0 SDK
- SQL Server (local or Docker) — or use EF Core InMemory for dev
- Git (for template sync features)
- Azure AD tenant (optional — dev bypass available)

## Build & Run

```bash
# From repo root
cd src/Platform.Engineering.Copilot.Admin.API

# Restore and build
dotnet restore
dotnet build

# Run (Development mode)
dotnet run --environment Development
```

The API starts on `http://localhost:5050`. Swagger UI available at `http://localhost:5050/swagger`.

## Configuration

### appsettings.Development.json

Key settings for local development:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=PlatformCopilot;User Id=sa;Password=DevPassword123!;TrustServerCertificate=true"
  },
  "DatabaseProvider": "InMemory",
  "Authentication": {
    "DevBypass": true,
    "RequireCac": false,
    "RequirePim": false
  },
  "Swagger": { "Enabled": true },
  "Cors": {
    "AllowedOrigins": "http://localhost:5000,http://localhost:5003,http://localhost:5200,http://localhost:5201"
  },
  "GitSync": {
    "IntervalMinutes": 60,
    "Enabled": false
  },
  "DeploymentPolling": {
    "IntervalSeconds": 30,
    "InitialDelaySeconds": 10
  }
}
```

Set `DatabaseProvider` to `"InMemory"` to skip SQL Server for local development.  
Set `Authentication:DevBypass` to `true` to skip JWT validation locally.

## Verify

```bash
# Health check
curl http://localhost:5050/health

# Create a template
curl -X POST http://localhost:5050/api/templates \
  -H "Content-Type: application/json" \
  -d '{
    "name": "test-aks",
    "content": "param location string\nresource aks Microsoft.ContainerService/managedClusters@2024-01-01 = { location: location }"
  }'

# List templates
curl http://localhost:5050/api/templates

# Swagger UI
open http://localhost:5050/swagger
```

## Docker

```bash
# From repo root
docker build -f src/Platform.Engineering.Copilot.Admin.API/Dockerfile -t admin-api .
docker run -p 5050:5050 -e ASPNETCORE_ENVIRONMENT=Development admin-api
```

## Test

```bash
# From repo root — run Admin API tests
dotnet test tests/Platform.Engineering.Copilot.Tests.Unit --filter "FullyQualifiedName~Admin"
dotnet test tests/Platform.Engineering.Copilot.Tests.Integration --filter "FullyQualifiedName~Admin"
```

## NuGet Packages Required

The Admin API project needs these packages added to the csproj:

| Package | Purpose |
|---------|---------|
| Swashbuckle.AspNetCore | Swagger UI + OpenAPI generation |
| Serilog.AspNetCore | Structured logging |
| Serilog.Sinks.File | Rolling daily log files |
| Microsoft.AspNetCore.Authentication.JwtBearer | Azure AD JWT auth |
| Microsoft.EntityFrameworkCore.SqlServer | SQL Server provider |
| Microsoft.EntityFrameworkCore.InMemory | Dev/test in-memory DB |

## Key Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| /health | GET | Health check |
| /swagger | GET | Swagger UI (dev only) |
| /api/templates | GET | List templates |
| /api/templates | POST | Create template |
| /api/templates/{id} | GET/PUT/DELETE | Template CRUD |
| /api/templates/{id}/approve | POST | Approve template |
| /api/templates/match | POST | NL template matching |
| /api/environments | GET | List environments |
| /api/environments | POST | Create environment |
| /api/environments/{id} | GET/DELETE | Environment lifecycle |
| /api/environments/{id}/scale | POST | Scale environment |
| /api/environments/summary | GET | Dashboard summary |
| /api/compliance/summary | GET | Compliance overview (stub) |
