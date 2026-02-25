# HTTP API Contract: Admin Client → Admin API

**Feature**: 004-admin-client  
**Date**: 2026-02-23  
**API Base URL**: Configurable via `AdminApi:BaseUrl` (default: `http://localhost:5050`)

---

## Overview

The Admin Client consumes the Admin API (feature 003) over HTTP. This contract documents every API call the client makes, organized by service class. All requests use `System.Text.Json` with camelCase serialization. All error handling follows the pattern: try/catch → log via `ILogger` → return null/empty → show toast notification.

---

## TemplateApiService

### List Templates
```
GET /api/templates?category={cat}&status={status}&search={query}
→ 200: TemplateSummaryDto[]
```

### Get Template by ID
```
GET /api/templates/{id:guid}
→ 200: TemplateDetailDto + ETag header
→ 404: Not found
```

### Get Template by Name
```
GET /api/templates/by-name/{name}?version={version}
→ 200: TemplateDetailDto + ETag header
→ 404: Not found
```

### Get Categories
```
GET /api/templates/categories
→ 200: string[]
```

### Create Template
```
POST /api/templates
Body: CreateTemplateRequest
→ 201: TemplateDetailDto
→ 400: Validation errors
```

### Update Template
```
PUT /api/templates/{id:guid}
Headers: If-Match: {etag}
Body: UpdateTemplateRequest
→ 200: TemplateDetailDto
→ 404: Not found
→ 409: Concurrency conflict
```

### Delete Template
```
DELETE /api/templates/{id:guid}?deletedBy={user}
→ 204: No content
→ 404: Not found
```

### Submit for Approval
```
POST /api/templates/{id:guid}/submit-for-approval
→ 200: TemplateDetailDto
→ 400: Invalid state transition
```

### Approve Template
```
POST /api/templates/{id:guid}/approve
Body: ApprovalRequest
→ 200: TemplateDetailDto
→ 400: Invalid state transition
```

### Deprecate Template
```
POST /api/templates/{id:guid}/deprecate?deprecatedBy={user}&reason={reason}
→ 200: TemplateDetailDto
```

### Validate Template
```
POST /api/templates/validate
Body: ValidateTemplateRequest
→ 200: TemplateValidationResultDto
```

### Parse Bicep Parameters
```
POST /api/templates/parse-bicep-parameters
Body: ParseBicepParametersRequest
→ 200: TemplateParameterDto[]
```

### Parse Bicep Parameters from Git
```
POST /api/templates/parse-bicep-parameters-from-git
Body: ParseBicepFromGitRequest
→ 200: TemplateParameterDto[]
```

### Import from Git
```
POST /api/templates/import-from-git
Body: ImportFromGitRequest
→ 201: TemplateDetailDto
```

### Sync Template from Git
```
POST /api/templates/{id:guid}/sync?force={bool}
→ 200: TemplateDetailDto
```

### Sync All Templates
```
POST /api/templates/sync-all
→ 200: { syncedCount, failedCount, errors[] }
```

### Get Git Status
```
GET /api/templates/{id:guid}/git-status
→ 200: GitStatusDto
```

### Match Templates (Natural Language)
```
POST /api/templates/match
Body: TemplateMatchRequest { description, minScore, maxResults }
→ 200: TemplateMatchResultDto
```

---

## EnvironmentApiService

### List Environments
```
GET /api/environments?status={status}&hasDrift={bool}&skip={n}&take={n}
→ 200: { items: EnvironmentDetailDto[], totalCount, skip, take }
```

### Get Environment
```
GET /api/environments/{id:guid}
→ 200: EnvironmentDetailDto + ETag header
→ 404: Not found
```

### Get Environment Summary
```
GET /api/environments/summary
→ 200: EnvironmentSummaryDto
```

### Create Environment
```
POST /api/environments
Body: CreateEnvironmentRequest
→ 201: EnvironmentDetailDto
→ 400: Validation errors
```

### Scale Environment
```
POST /api/environments/{id:guid}/scale
Body: ScaleEnvironmentRequest
→ 200: ScaleResultDto
```

### Clone Environment
```
POST /api/environments/{id:guid}/clone
Body: CloneEnvironmentRequest
→ 201: EnvironmentDetailDto
```

### Reprovision Environment
```
POST /api/environments/{id:guid}/reprovision
→ 200: EnvironmentDetailDto
```

### Delete Environment
```
DELETE /api/environments/{id:guid}?deletedBy={user}&force={bool}
→ 204: No content
```

### Delete Azure Resources
```
POST /api/environments/{id:guid}/delete-resources
→ 200: DeleteResourcesResultDto
```

### Get Deleted Environments
```
GET /api/environments/deleted
→ 200: { items: EnvironmentDetailDto[], totalCount }
```

### Purge Environment
```
DELETE /api/environments/{id:guid}/purge
→ 204: No content
```

### Purge All Deleted Environments
```
DELETE /api/environments/purge-all
→ 200: { purgedCount }
```

### Get Environment Resources
```
GET /api/environments/{id:guid}/resources
→ 200: { resources: ResourceDto[], totalCount }
```

### Sync Resources from Azure
```
POST /api/environments/{id:guid}/sync-resources
→ 200: { syncedCount, newResources[] }
```

### Get Environment Health
```
GET /api/environments/{id:guid}/health
→ 200: EnvironmentHealthDto
```

### Get Environment Activities
```
GET /api/environments/{id:guid}/activities?skip={n}&take={n}
→ 200: ActivityListDto
```

### Get Expiring Environments
```
GET /api/environments/expiring?withinDays={n}
→ 200: { items: EnvironmentDetailDto[], totalCount, withinDays }
```

### Extend Expiration
```
POST /api/environments/{id:guid}/extend
Body: ExtendExpirationRequest
→ 200: EnvironmentDetailDto
```

### Detect Drift
```
POST /api/environments/{id:guid}/detect-drift
→ 200: DriftDetectionResultDto
```

### Remediate Drift
```
POST /api/environments/{id:guid}/remediate-drift
Body: RemediateDriftRequest (optional)
→ 200: RemediateDriftResultDto
```

---

## ComplianceApiService

### Get Compliance Summary
```
GET /api/compliance/summary
→ 200: ComplianceSummaryDto
```

### Trigger Compliance Scan
```
POST /api/compliance/scan?environmentId={guid}
→ 202: { status, message, scheduledAt }
```

### Get Environment Compliance
```
GET /api/compliance/environments/{environmentId:guid}
→ 200: EnvironmentComplianceDto
```

---

## Error Handling Contract

All service methods follow this pattern:

```
try {
    var response = await _httpClient.{Method}Async(url, ...);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<T>();
} catch (Exception ex) {
    _logger.LogError(ex, "Failed to {action}");
    return null; // or empty collection
}
```

The calling page/component is responsible for:
1. Checking for null/empty return values
2. Showing toast notifications for errors
3. Rendering empty states with retry buttons
