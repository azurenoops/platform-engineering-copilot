# Quickstart: NIST Controls Knowledge Foundation

**Feature**: 005-nist-controls-foundation  
**Date**: 2026-02-24

## Prerequisites

- .NET 9.0 SDK installed
- Repository cloned, branch `005-nist-controls-foundation` checked out
- Access to `https://raw.githubusercontent.com` (optional — embedded fallback works offline)

## Build

```bash
cd /Users/johnspinella/repos/platform-engineering-copilot-v2
dotnet build Platform.Engineering.Copilot.sln
```

Expected: 0 errors, 0 new warnings.

## Run Tests

```bash
dotnet test Platform.Engineering.Copilot.sln --verbosity normal
```

Expected: All tests pass (existing 771+ plus new tests added by this feature).

## Run Tests with Coverage

```bash
dotnet test Platform.Engineering.Copilot.sln \
  --collect:"XPlat Code Coverage" \
  --results-directory ./coverage
```

Expected: 80%+ coverage on new code (NistService enhancements, NistControlsCacheWarmupService, NistControlsHealthCheck).

## Verify Configuration

Check that `appsettings.json` in the Mcp project has the `NistControls` section:

```json
{
  "NistControls": {
    "BaseUrl": "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5",
    "TimeoutSeconds": 60,
    "CacheDurationHours": 24,
    "MaxRetryAttempts": 3,
    "RetryDelaySeconds": 2,
    "EnableOfflineFallback": true,
    "OfflineFallbackPath": "Data/nist-800-53-fallback.json",
    "EnableMemoryCache": true,
    "EnableDetailedLogging": false
  }
}
```

## Verify Startup Behavior

1. Start the Mcp application:
   ```bash
   cd src/Platform.Engineering.Copilot.Mcp
   dotnet run --environment Development
   ```

2. Within 15 seconds, the console should log:
   ```
   [INF] NIST catalog loaded from embedded resources. Controls: 10, Source: EmbeddedFallback
   [INF] Successfully warmed up NIST controls cache with 10 controls
   ```
   (In development, GitHub fetch is disabled; embedded 10-control catalog is used.)

3. Check health endpoint:
   ```bash
   curl http://localhost:5000/health | jq
   ```
   Expected: Status includes `nist-controls` entry with `Healthy` status.

## Verify Air-Gapped Mode

1. Set `NistControls:EnableOfflineFallback` to `true` (default)
2. Ensure `Data/nist-800-53-fallback.json` exists in the content root
3. Block outbound network (or set `BaseUrl` to an unreachable host)
4. Start the application
5. Expected: Catalog loads from fallback file; health check reports Healthy

## Key Files to Inspect

| File | Purpose |
|------|---------|
| `Core/Services/INistService.cs` | Extended interface with 4 new async methods |
| `Core/Services/NistService.cs` | Enhanced implementation — caching, retry, new methods |
| `Core/Configuration/NistControlsOptions.cs` | Validated configuration class |
| `Core/Observability/NistControlsHealthCheck.cs` | IHealthCheck implementation |
| `Core/Observability/ComplianceMetricsService.cs` | Activity + Metrics wrapper |
| `Agents/Compliance/Services/NistControlsCacheWarmupService.cs` | Background cache warmup |
| `Agents/Extensions/ServiceCollectionExtensions.cs` | Updated DI registrations |
| `Tests.Unit/Services/NistServiceEnhancedTests.cs` | New functionality tests |
| `Tests.Unit/Services/NistControlsHealthCheckTests.cs` | Health check tests |
| `Tests.Unit/Services/NistControlsCacheWarmupServiceTests.cs` | Warmup service tests |
