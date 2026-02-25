# Research: Admin Dashboard Client

**Feature**: 004-admin-client  
**Date**: 2026-02-23  
**Purpose**: Resolve technical unknowns, validate technology choices, and document decisions.

---

## 1. Blazored Libraries Compatibility with .NET 9.0

**Decision**: Use Blazored.Toast 4.2.1, Blazored.Modal 7.3.1, Blazored.LocalStorage 4.5.0.

**Rationale**: The existing `Platform.Engineering.Copilot.Admin.Client.csproj` already targets `net9.0` with Blazor WebAssembly 9.0.9. Blazored libraries are well-maintained, widely adopted in the Blazor ecosystem, and have been tested against .NET 8+ with no known incompatibilities in their latest versions. The packages target `netstandard2.0` / `net6.0+` and are forward-compatible.

**Alternatives considered**:
- MudBlazor / Radzen: Full component libraries that would replace Bootstrap entirely. Rejected because the spec explicitly requires Bootstrap 5.3.2 and Font Awesome 6.5.1, making a component library redundant.
- Custom implementations: Building our own toast/modal/localStorage wrappers. Rejected because Blazored libraries are mature, well-tested, and save significant development time.

---

## 2. Testing Strategy for Blazor WebAssembly Components

**Decision**: Use standard xUnit + Moq + FluentAssertions for service and model unit tests. Skip bUnit component tests in this iteration.

**Rationale**: The Admin Client's testable surface is primarily in the HTTP service layer (4 services making ~48 API calls) and data models (~60 DTOs). These are standard C# classes that can be tested with xUnit + Moq using `MockHttpMessageHandler` without any Blazor-specific testing framework. Razor component rendering tests (bUnit) would require adding a new test dependency and test project, adding complexity for limited value — the pages are primarily data-binding and UI composition that is harder to test meaningfully at the unit level.

**Alternatives considered**:
- bUnit: A dedicated Blazor component testing library. Deferred because the service layer provides higher ROI for testing, and bUnit requires significant setup for components that depend on Blazored services, HttpClient, and JS interop. Can be added in a future iteration if needed.
- Playwright/Selenium: End-to-end browser tests. Out of scope for unit test coverage; considered future work.

---

## 3. Client-Side Pagination Implementation

**Decision**: Build a reusable `Pagination.razor` shared component that accepts total item count and page size, renders Bootstrap 5 pagination controls, and emits a page-changed event.

**Rationale**: The clarification session confirmed client-side pagination (load all data, paginate in browser). A shared component ensures consistent pagination UX across template catalog, environments list, compliance dashboard, drift page, and health page. Bootstrap 5 provides built-in `pagination` CSS classes.

**Alternatives considered**:
- Third-party pagination component: Rejected because Bootstrap pagination CSS is sufficient and avoids another dependency.
- Virtual scrolling: Rejected because the expected data volumes (10s-100s of items) don't warrant it, and it adds significant complexity.

---

## 4. Theme Switching via JS Interop

**Decision**: Use a small `theme.js` file with JS interop for applying CSS classes to `document.body` and listening to `prefers-color-scheme` changes.

**Rationale**: Blazor WASM cannot directly access the DOM or `window.matchMedia`. JS interop is the standard pattern for DOM manipulation in Blazor. The implementation requires:
1. A `setTheme(theme)` function that adds/removes `theme-dark` / `theme-light` classes on `document.body`.
2. A `watchSystemTheme(dotNetRef)` function that registers a `matchMedia('(prefers-color-scheme: dark)')` listener and invokes a .NET callback when the OS theme changes.

**Alternatives considered**:
- CSS-only with `@media (prefers-color-scheme)`: Only supports auto mode, doesn't allow user override to a specific theme. Rejected.
- Blazor CSS isolation: Doesn't support dynamic theme switching at the document level. Rejected.

---

## 5. Admin API JSON Serialization Compatibility

**Decision**: Use `System.Text.Json` with default `camelCase` property naming to match the Admin API.

**Rationale**: The Admin API's `Program.cs` uses `AddControllers()` with no custom `JsonSerializerOptions`, which means ASP.NET Core defaults to `camelCase` serialization. The client must use matching `camelCase` property naming or `JsonPropertyName` attributes on DTOs. Since `System.Text.Json` defaults to camelCase in `HttpClient` extension methods (`GetFromJsonAsync`, `PostAsJsonAsync`), no custom configuration is needed — the defaults align.

**Alternatives considered**:
- `Newtonsoft.Json`: Available but not needed since both sides use `System.Text.Json` with matching defaults. Adding Newtonsoft would increase the WASM download size.
- `JsonPropertyName` attributes on every property: Not needed since both APIs use the same default naming policy.

---

## 6. Existing Admin Client Scaffold Assessment

**Decision**: Rewrite the existing pages and layout; keep the project file and Dockerfile as starting points.

**Rationale**: The existing scaffold has 4 placeholder pages (`Home.razor`, `Templates.razor`, `Environments.razor`, `Deployments.razor`) with hardcoded data, a default Blazor template layout (`NavMenu.razor` with hamburger menu), and no services or models. The spec requires 13 pages, a sidebar layout, 4 HTTP services, ~60 DTOs, and shared components. The existing pages will be replaced entirely. The `.csproj` and `Dockerfile` can be updated in place (adding NuGet packages to csproj, updating nginx.conf for the new requirements).

**Alternatives considered**:
- Delete and recreate the project: Rejected because the existing project is already referenced in the solution file and has correct build infrastructure.
- Keep existing pages and extend: Rejected because the existing pages have hardcoded data and incompatible layouts.

---

## 7. HttpClient Registration Pattern

**Decision**: Register a named `HttpClient` via `IHttpClientFactory` with base address from configuration.

**Rationale**: The spec requires `AdminApi:BaseUrl` configuration (default `http://localhost:5050`). The current `Program.cs` registers a simple `HttpClient` with the host's base address. This must be changed to use `IHttpClientFactory` pattern for proper lifecycle management and testability. The `Microsoft.Extensions.Http` NuGet package provides `AddHttpClient<TService>()` which allows typed clients per service.

**Alternatives considered**:
- Single shared HttpClient: Simpler but doesn't support per-service configuration or easy mocking in tests.
- Typed HttpClient per service: The recommended pattern. Each service class receives its own `HttpClient` instance via DI, configured with the API base address.

---

## 8. Form Validation Approach

**Decision**: Use Blazor's built-in `EditForm` + `DataAnnotationsValidator` with `onblur` validation via custom `EditContext` integration.

**Rationale**: The clarification session confirmed inline validation on field blur with disabled submit. Blazor's `EditForm` provides `EditContext` which supports field-level validation. Combined with `DataAnnotationsValidator`, this gives us `[Required]`, `[StringLength]`, `[Url]` etc. on request DTOs. The blur behavior requires subscribing to `EditContext.OnFieldChanged` and triggering validation per field.

**Alternatives considered**:
- FluentValidation: More powerful but adds a dependency and is overkill for form-level required/format checks.
- Manual validation in code-behind: More flexible but loses the declarative annotation benefit and requires more code.

---

## 9. Docker/nginx Configuration

**Decision**: Two-stage Dockerfile (SDK publish → nginx:alpine). nginx serves static files with immutable caching for `_framework/`, gzip, `try_files` fallback, and reverse proxy for `/api/`.

**Rationale**: This is the standard Blazor WASM deployment pattern. The existing `Dockerfile` and `nginx.conf` in the project provide a starting point but need updating for the new requirements (API reverse proxy, health endpoint, caching headers, gzip).

**Alternatives considered**:
- Kestrel-hosted Blazor: Would require an ASP.NET server project. Rejected because the spec requires a pure client-side SPA.
- Azure Static Web Apps: Not applicable for Docker-based deployment requirement.

---

## 10. Sidebar Layout vs. Existing Template NavMenu

**Decision**: Replace the existing `NavMenu.razor` (hamburger-style top nav) with a fixed sidebar layout using Bootstrap 5 flexbox utilities.

**Rationale**: The spec requires a persistent sidebar with four navigation sections (Dashboard, Service Templates, Environments, Compliance), each with Font Awesome icons. The existing Blazor template uses a collapsible hamburger menu which doesn't match the spec. The sidebar will use Bootstrap's `d-flex` + `vh-100` pattern with a fixed-width sidebar and flexible content area.

**Alternatives considered**:
- Keep existing NavMenu and add sidebar as a separate component: Rejected because the layout architecture is fundamentally different.
- Offcanvas sidebar (collapses on mobile): Could be added as a future enhancement but the spec defines a fixed sidebar.
