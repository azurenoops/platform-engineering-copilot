# Quickstart: Admin Dashboard Client

**Feature**: 004-admin-client  
**Date**: 2026-02-23

---

## Prerequisites

- .NET 9.0 SDK
- Admin API running at `http://localhost:5050` (feature 003)
- Docker (optional, for containerized deployment)

## Development Setup

### 1. Start the Admin API

```bash
cd src/Platform.Engineering.Copilot.Admin.API
dotnet run --environment Development
# API available at http://localhost:5050
```

### 2. Start the Admin Client

```bash
cd src/Platform.Engineering.Copilot.Admin.Client
dotnet run
# Client available at http://localhost:5000
```

### 3. Open in browser

Navigate to `http://localhost:5000`. The dashboard should load and display data from the Admin API.

## Running Tests

### Unit Tests

```bash
dotnet test tests/Platform.Engineering.Copilot.Tests.Unit \
  --filter "FullyQualifiedName~AdminClient" \
  --verbosity normal
```

### All Tests

```bash
dotnet test Platform.Engineering.Copilot.sln --verbosity normal
```

## Docker Build

```bash
cd src/Platform.Engineering.Copilot.Admin.Client
docker build -t platform-admin-client .
docker run -p 5000:80 platform-admin-client
```

## Configuration

### appsettings.json

```json
{
  "AdminApi": {
    "BaseUrl": "http://localhost:5050"
  }
}
```

### Environment Variables (Docker)

| Variable | Default | Description |
|----------|---------|-------------|
| `ADMIN_API_URL` | `http://platform-admin-api:5050` | Admin API base URL for nginx reverse proxy |

## Key URLs

| URL | Description |
|-----|-------------|
| `/` | Dashboard |
| `/templates` | Template catalog |
| `/templates/create` | Create template |
| `/templates/{id}` | Template details |
| `/templates/edit/{id}` | Edit template |
| `/environments` | Environment list |
| `/environments/create` | Provision environment |
| `/environments/{id}` | Environment details |
| `/compliance` | Compliance dashboard |
| `/compliance/environment/{id}` | Environment compliance |
| `/drift` | Drift detection |
| `/health` | Health status |
| `/settings` | Application settings |
