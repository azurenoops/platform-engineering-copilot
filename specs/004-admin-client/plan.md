# Implementation Plan: Admin Dashboard Client

**Branch**: `004-admin-client` | **Date**: 2026-02-23 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/004-admin-client/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

Transform the existing scaffolded Blazor WebAssembly project (`Platform.Engineering.Copilot.Admin.Client`) into a full-featured admin dashboard for managing service templates, provisioned environments, compliance posture, drift detection, health monitoring, and application settings. The client is a pure client-side SPA communicating with the Admin API (feature 003) over HTTP. It uses Bootstrap 5.3 + Font Awesome for UI, Blazored libraries for toast/modal/localStorage, and deploys via Docker with nginx.

## Technical Context

**Language/Version**: C# / .NET 9.0  
**Primary Dependencies**: Blazor WebAssembly 9.0.x, Blazored.Toast 4.2.1, Blazored.Modal 7.3.1, Blazored.LocalStorage 4.5.0, Bootstrap 5.3.2 (CDN), Font Awesome 6.5.1 (CDN)  
**Storage**: Browser localStorage (settings persistence only)  
**Testing**: xUnit 2.9.2, FluentAssertions 7.0.0, Moq 4.20.72 (unit tests for services and models via MockHttpMessageHandler; no bUnit — see research.md Decision #2)  
**Target Platform**: Web browser (Blazor WebAssembly), Docker (nginx:alpine for production)  
**Project Type**: web-app (SPA)  
**Performance Goals**: Dashboard loads in <3 seconds; theme changes apply instantly  
**Constraints**: No project references to other solution projects; pure HTTP client; no server-side rendering  
**Scale/Scope**: 13 pages, 4 HTTP services, 47 DTOs, 54 functional requirements

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| # | Gate | Status | Notes |
|---|------|--------|-------|
| I | Documentation as Source of Truth | PASS | Architecture documented in docs/ARCHITECTURE.md; Admin Client port (5000) already defined. No conflicting guidance. |
| II | BaseAgent/BaseTool Architecture | N/A | This feature is a UI client — no agents or tools are involved. |
| III | Test-First Development | PASS | xUnit/FluentAssertions/Moq for service+model unit tests using MockHttpMessageHandler. No bUnit this iteration (see research.md Decision #2) — 80%+ coverage target applies to the testable service and model layers, which contain the HTTP logic and data contracts. Razor pages are data-binding/UI composition with minimal testable logic. |
| IV | Azure Government & Compliance First | PASS (partial) | Client is pure SPA with no direct Azure interaction. Admin API handles all Azure calls. Auth is a placeholder for future implementation. No credentials stored client-side. |
| V | Observability & Structured Logging | PASS (adapted) | Browser-side logging via `ILogger` (Microsoft.Extensions.Logging). No Serilog in WASM. Console logging for development. |

**Gate Violations**: None. Constitution principles II and V are adapted for the client-side context (no agents, no Serilog in WASM), which is justified by the nature of a browser-hosted SPA.

## Project Structure

### Documentation (this feature)

```text
specs/004-admin-client/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
src/Platform.Engineering.Copilot.Admin.Client/
├── Platform.Engineering.Copilot.Admin.Client.csproj
├── Program.cs                          # App bootstrap, service registration
├── App.razor                           # Router + Blazored wrappers
├── _Imports.razor                      # Global usings
├── Dockerfile                          # Two-stage: SDK publish → nginx:alpine
├── nginx.conf                          # Reverse proxy + caching + routing
├── wwwroot/
│   ├── index.html                      # Bootstrap 5.3 + Font Awesome CDN refs
│   ├── css/
│   │   └── app.css                     # Custom styles, theme classes
│   └── js/
│       └── theme.js                    # JS interop for theme switching
├── Layout/
│   ├── MainLayout.razor                # Sidebar + header + content area
│   └── MainLayout.razor.css            # Scoped layout styles
├── Models/
│   ├── Templates.cs                    # Template DTOs (summary, detail, params, guardrails)
│   ├── Environments.cs                 # Environment DTOs (summary, detail, resources)
│   ├── Compliance.cs                   # Compliance DTOs (summary, framework, controls)
│   ├── Drift.cs                        # Drift DTOs (detection result, items)
│   ├── Health.cs                       # Health DTOs
│   ├── Activities.cs                   # Activity DTOs
│   ├── Requests.cs                     # All request DTOs for API calls
│   └── AppSettings.cs                  # Settings model (~40 properties)
├── Services/
│   ├── TemplateApiService.cs           # Template CRUD, approval, Git, validation
│   ├── EnvironmentApiService.cs        # Environment CRUD, lifecycle, drift, health
│   ├── ComplianceApiService.cs         # Compliance summary, scan, details
│   └── AppSettingsService.cs           # localStorage persistence, theme, events
├── Pages/
│   ├── Dashboard.razor                 # / — summary cards, recent environments
│   ├── TemplateCatalog.razor           # /templates — card grid with filters
│   ├── TemplateCreate.razor            # /templates/create — paste or Git import
│   ├── TemplateDetail.razor            # /templates/{id} — full template view
│   ├── TemplateEdit.razor              # /templates/edit/{id} — edit form
│   ├── EnvironmentList.razor           # /environments — list with actions
│   ├── EnvironmentCreate.razor         # /environments/create — provision form
│   ├── EnvironmentDetail.razor         # /environments/{id} — 7-tab detail
│   ├── ComplianceDashboard.razor       # /compliance — scores + scanning
│   ├── ComplianceDetail.razor          # /compliance/environment/{id} — controls
│   ├── DriftDetection.razor            # /drift — bulk scan + per-env
│   ├── HealthStatus.razor              # /health — health monitoring
│   └── Settings.razor                  # /settings — 6-tab settings
└── Shared/
    ├── StatusBadge.razor               # Reusable status badge component
    ├── Pagination.razor                # Client-side pagination controls
    ├── LoadingSpinner.razor            # Loading indicator
    ├── EmptyState.razor                # Empty state with retry button
    ├── ConfirmModal.razor              # Standard confirm modal
    ├── TypeToConfirmModal.razor        # Type-to-confirm for bulk actions
    └── Breadcrumb.razor                # Breadcrumb navigation

tests/Platform.Engineering.Copilot.Tests.Unit/AdminClient/
├── Services/
│   ├── TemplateApiServiceTests.cs
│   ├── EnvironmentApiServiceTests.cs
│   ├── ComplianceApiServiceTests.cs
│   └── AppSettingsServiceTests.cs
└── Models/
    ├── TemplateModelTests.cs
    ├── EnvironmentModelTests.cs
    └── AppSettingsTests.cs
```

**Structure Decision**: Extend the existing `Platform.Engineering.Copilot.Admin.Client` project (already in the solution) with Models/, Services/, Pages/, and Shared/ directories. Unit tests go into the existing `Tests.Unit` project under a new `AdminClient/` folder. No new test project needed since service and model tests don't require bUnit — they test HTTP service wrappers and DTOs using standard xUnit + Moq with `MockHttpMessageHandler`.

## Complexity Tracking

> No constitution violations to justify — all gates pass or are N/A for a client-side SPA.
