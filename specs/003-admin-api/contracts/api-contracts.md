# API Contracts: Admin API

**Feature**: 003-admin-api  
**Base URL**: `http://localhost:5050/api`  
**Auth**: Azure AD JWT Bearer Token (all endpoints)  
**Content-Type**: `application/json`

## Table of Contents

1. [Templates Controller](#templates-controller)
2. [Environments Controller](#environments-controller)
3. [Compliance Controller](#compliance-controller)
4. [Health Controller](#health-controller)
5. [Common Models](#common-models)
6. [Error Responses](#error-responses)
7. [Headers](#headers)

---

## Templates Controller

**Route**: `/api/templates`  
**Auth**: Admin role (mutations), Admin+Engineer (reads)

### GET /api/templates

List templates with filtering and pagination.

**Query Parameters**:
| Param | Type | Default | Notes |
|-------|------|---------|-------|
| category | string? | null | Filter by category |
| status | string? | null | Filter by TemplateStatus |
| search | string? | null | Keyword search across name, description, keywords |
| skip | int | 0 | Pagination offset |
| take | int | 50 | Page size (max 100) |

**Response 200**:
```json
[
  {
    "templateId": "guid",
    "name": "string",
    "displayName": "string?",
    "description": "string",
    "version": "string",
    "category": "string",
    "format": "Bicep|ARM|Terraform",
    "status": "Draft|PendingApproval|Published|Deprecated",
    "deploymentScope": "string?",
    "hasGitSource": true,
    "gitRepositoryUrl": "string?",
    "lastSyncedFromGit": "datetime?",
    "gitAutoSync": true,
    "createdAt": "datetime",
    "updatedAt": "datetime"
  }
]
```

### GET /api/templates/{id}

Get template by ID with full detail.

**Response 200**: `TemplateDetailDto` (see [DTOs](#template-dtos))  
**Response 404**: Template not found  
**Headers**: `ETag: "base64-rowversion"`

### GET /api/templates/by-name/{name}

Get template by name, optionally by version.

**Query Parameters**:
| Param | Type | Default |
|-------|------|---------|
| version | string? | null (returns latest) |

**Response 200**: `TemplateDetailDto`  
**Response 404**: Template not found

### GET /api/templates/categories

Get distinct template categories.

**Response 200**:
```json
["General", "Compute", "Networking", "Storage"]
```

### POST /api/templates

Create a new template.

**Request Body**: `CreateTemplateRequest`
```json
{
  "name": "string (3-100 chars, required)",
  "displayName": "string?",
  "description": "string?",
  "version": "string? (default: '1.0.0')",
  "category": "string? (default: 'General')",
  "format": "Bicep|ARM|Terraform (default: Bicep)",
  "content": "string (required)",
  "deploymentScope": "string?",
  "parametersJson": "string?",
  "guardrailsJson": "string?",
  "complianceFrameworks": "string?",
  "keywords": "string?",
  "useCases": "string?",
  "aiSelectionHints": "string?",
  "requiresApproval": "bool (default: true)",
  "gitRepoUrl": "string?",
  "gitBranch": "string?",
  "gitPath": "string?",
  "gitAutoSync": "bool (default: false)",
  "gitSyncIntervalMinutes": "int? (default: 60)"
}
```

**Response 201**: `TemplateDetailDto` + `Location` header  
**Response 400**: Validation error (name too short, missing content, duplicate name+version)

### PUT /api/templates/{id}

Update template (partial — only non-null fields applied).

**Request Headers**: `If-Match: "base64-rowversion"` (required)  
**Request Body**: `UpdateTemplateRequest`
```json
{
  "name": "string?",
  "displayName": "string?",
  "description": "string?",
  "version": "string?",
  "category": "string?",
  "format": "Bicep|ARM|Terraform?",
  "content": "string?",
  "deploymentScope": "string?",
  "parametersJson": "string?",
  "guardrailsJson": "string?",
  "complianceFrameworks": "string?",
  "keywords": "string?",
  "useCases": "string?",
  "aiSelectionHints": "string?",
  "requiresApproval": "bool?",
  "gitRepoUrl": "string?",
  "gitBranch": "string?",
  "gitPath": "string?",
  "gitAutoSync": "bool?",
  "gitSyncIntervalMinutes": "int?"
}
```

**Response 200**: `TemplateDetailDto`  
**Response 400**: Validation error  
**Response 404**: Template not found  
**Response 409**: Concurrency conflict (stale ETag)

### DELETE /api/templates/{id}

Soft-delete a template.

**Query Parameters**:
| Param | Type | Required |
|-------|------|----------|
| deletedBy | string | Yes |

**Response 204**: Deleted  
**Response 404**: Not found

### POST /api/templates/{id}/submit-for-approval

Submit a Draft template for approval.

**Response 200**: `TemplateDetailDto` (status = PendingApproval)  
**Response 400**: Invalid status transition  
**Response 404**: Not found

### POST /api/templates/{id}/approve

Approve a PendingApproval template.

**Request Body**: `ApprovalRequest`
```json
{
  "approvalSource": "Internal|External (required)",
  "approvedBy": "string (required)",
  "comments": "string?",
  "externalApprovalId": "string?",
  "externalApprovalUrl": "string?"
}
```

**Response 200**: `TemplateDetailDto` (status = Published)  
**Response 400**: Invalid status transition or missing fields  
**Response 404**: Not found

### POST /api/templates/{id}/deprecate

Deprecate a Published template.

**Query Parameters**:
| Param | Type | Required |
|-------|------|----------|
| deprecatedBy | string | Yes |
| reason | string | Yes |

**Response 200**: `TemplateDetailDto` (status = Deprecated)  
**Response 400**: Invalid status transition  
**Response 404**: Not found

### POST /api/templates/validate

Validate template content without creating.

**Request Body**: `ValidateTemplateRequest`
```json
{
  "name": "string?",
  "content": "string?",
  "format": "Bicep|ARM|Terraform (default: Bicep)"
}
```

**Response 200**: `TemplateValidationResultDto`
```json
{
  "isValid": true,
  "errors": ["string"],
  "warnings": ["string"]
}
```

### POST /api/templates/parse-bicep-parameters

Extract parameters from Bicep content.

**Request Body**: `ParseBicepParametersRequest`
```json
{
  "bicepContent": "string (required)"
}
```

**Response 200**:
```json
[
  {
    "name": "string",
    "displayName": "string?",
    "type": "string",
    "description": "string?",
    "required": true,
    "defaultValue": "string?",
    "allowedValues": ["string"]
  }
]
```

### POST /api/templates/parse-bicep-parameters-from-git

Extract parameters from Bicep file in a Git repo.

**Request Body**: `ParseBicepFromGitRequest`
```json
{
  "gitRepoUrl": "string (required)",
  "branch": "string? (default: 'main')",
  "filePath": "string? (default: 'main.bicep')"
}
```

**Response 200**: Same as parse-bicep-parameters  
**Response 400**: Git source unreachable or file not found

### POST /api/templates/match

Natural language template matching.

**Request Body**: `TemplateMatchRequest`
```json
{
  "description": "string (required)",
  "minScore": 0.3,
  "maxResults": 5
}
```

**Response 200**: `TemplateMatchResultDto`
```json
{
  "matches": [
    {
      "templateId": "guid",
      "templateName": "string",
      "score": 0.85,
      "reasoning": "string",
      "suggestedParameters": { "key": "value" }
    }
  ],
  "usedLlm": false,
  "processingTimeMs": 120
}
```

**Response 503**: NL matching service unavailable

### POST /api/templates/{id}/extract-parameters

Extract parameter values from natural language for a specific template.

**Request Body**: `ExtractParametersRequest`
```json
{
  "description": "string (required)"
}
```

**Response 200**: `ExtractedParametersDto`
```json
{
  "parameters": [
    {
      "name": "string",
      "value": "string",
      "confidence": 0.9,
      "reasoning": "string"
    }
  ]
}
```

### POST /api/templates/{id}/explain-match

Explain why a template matches a request.

**Request Body**: `ExplainMatchRequest`
```json
{
  "description": "string (required)"
}
```

**Response 200**: `TemplateExplanationDto`
```json
{
  "explanation": "string",
  "matchingFactors": [
    {
      "factor": "string",
      "weight": 0.8,
      "description": "string"
    }
  ]
}
```

### POST /api/templates/import-from-git

Import a template from a Git repository.

**Request Body**: `ImportFromGitRequest`
```json
{
  "gitRepoUrl": "string (required)",
  "branch": "string? (default: 'main')",
  "filePath": "string? (default: 'main.bicep')",
  "name": "string?",
  "category": "string?",
  "gitAutoSync": "bool (default: false)",
  "gitSyncIntervalMinutes": "int? (default: 60)"
}
```

**Response 201**: `TemplateDetailDto` + `Location` header  
**Response 400**: Git source unreachable

### POST /api/templates/{id}/sync

Sync template from Git source.

**Query Parameters**:
| Param | Type | Default |
|-------|------|---------|
| force | bool | false |

**Response 200**: `TemplateDetailDto` (updated)  
**Response 400**: No Git source configured  
**Response 404**: Not found

### POST /api/templates/sync-all

Bulk-sync all Git-sourced templates.

**Response 200**:
```json
{
  "syncedCount": 5,
  "failedCount": 1,
  "failures": [{ "templateId": "guid", "error": "string" }]
}
```

### GET /api/templates/{id}/git-status

Check if Git source has newer commits.

**Response 200**: `GitStatusDto`
```json
{
  "hasChanges": true,
  "currentCommitSha": "abc123",
  "latestCommitSha": "def456",
  "lastSyncedAt": "datetime?"
}
```

**Response 400**: No Git source configured  
**Response 404**: Not found

### POST /api/templates/{id}/reset-parameters

Reset manually-overridden parameters from Git.

**Response 200**: `TemplateDetailDto`  
**Response 404**: Not found

---

## Environments Controller

**Route**: `/api/environments`  
**Auth**: Engineer role (lifecycle), Admin (purge, status override), Admin+Engineer (reads)

### GET /api/environments

List environments with filtering and pagination.

**Query Parameters**:
| Param | Type | Default |
|-------|------|---------|
| subscriptionId | string? | null |
| templateId | Guid? | null |
| status | string? | null |
| hasDrift | bool? | null |
| skip | int | 0 |
| take | int | 50 |

**Response 200**:
```json
[
  {
    "id": "guid",
    "name": "string",
    "displayName": "string?",
    "description": "string?",
    "templateId": "guid",
    "templateName": "string?",
    "subscriptionId": "string",
    "resourceGroup": "string",
    "location": "string",
    "status": "Provisioning|Running|Failed|...",
    "statusMessage": "string?",
    "deploymentId": "string?",
    "hasDrift": false,
    "driftCount": 0,
    "estimatedMonthlyCost": 150.00,
    "ownerEmail": "string?",
    "expiresAt": "datetime?",
    "autoDelete": false,
    "deploymentScope": "string?",
    "tagsJson": "string?",
    "parameterValuesJson": "string?",
    "createdAt": "datetime",
    "updatedAt": "datetime"
  }
]
```

### GET /api/environments/{id}

Get environment by ID.

**Response 200**: `EnvironmentDetailDto`  
**Response 404**: Not found  
**Headers**: `ETag: "base64-rowversion"`

### POST /api/environments

Create an environment from a published template.

**Request Body**: `CreateEnvironmentRequest`
```json
{
  "templateId": "guid (required)",
  "environmentName": "string (3-100 chars, required)",
  "displayName": "string?",
  "description": "string?",
  "resourceGroup": "string (required)",
  "subscriptionId": "string (required)",
  "location": "string? (default: 'eastus')",
  "parameterValuesJson": "string?",
  "tagsJson": "string?",
  "ownerEmail": "string?",
  "expiresAt": "datetime?",
  "autoDelete": "bool (default: false)"
}
```

**Response 201**: `EnvironmentDetailDto` + `Location` header  
**Response 400**: Template not found, not Published, or validation error

### POST /api/environments/{id}/scale

Scale an environment.

**Request Body**: `ScaleEnvironmentRequest`
```json
{
  "nodeCount": "int?",
  "replicaCount": "int?",
  "sku": "string?",
  "tier": "string?",
  "additionalParameters": { "key": "value" }
}
```

**Response 200**: `ScaleResultDto`
```json
{
  "environmentId": "guid",
  "previousValues": { "nodeCount": 3 },
  "newValues": { "nodeCount": 5 },
  "status": "Scaling"
}
```

**Response 400**: Environment not in scalable state  
**Response 404**: Not found

### POST /api/environments/{id}/clone

Clone an environment.

**Request Body**: `CloneEnvironmentRequest`
```json
{
  "newName": "string (3-100 chars, required)",
  "displayName": "string?",
  "resourceGroup": "string?",
  "subscriptionId": "string?"
}
```

**Response 201**: `EnvironmentDetailDto` + `Location` header  
**Response 404**: Source not found

### POST /api/environments/{id}/reprovision

Reprovision a failed environment.

**Response 200**: `EnvironmentDetailDto` (status = Provisioning)  
**Response 400**: Environment not in Failed state  
**Response 404**: Not found

### DELETE /api/environments/{id}

Soft-delete an environment.

**Query Parameters**:
| Param | Type | Required |
|-------|------|----------|
| deletedBy | string | Yes |
| force | bool | No (default: false) |

**Response 204**: Deleted  
**Response 404**: Not found

### GET /api/environments/deleted

List soft-deleted environments.

**Response 200**: Array of `EnvironmentDetailDto` (deleted environments)

### DELETE /api/environments/{id}/purge

Permanently delete a soft-deleted environment.

**Response 204**: Purged  
**Response 404**: Not found or not soft-deleted

### DELETE /api/environments/purge-all

Permanently delete all soft-deleted environments.

**Response 200**:
```json
{ "purgedCount": 3 }
```

### GET /api/environments/{id}/resources

Get deployed resources for an environment.

**Response 200**:
```json
[
  {
    "id": "guid",
    "azureResourceId": "string",
    "name": "string",
    "type": "string",
    "location": "string?",
    "sku": "string?",
    "provisioningState": "string?",
    "deployedAt": "datetime?",
    "portalUrl": "https://portal.azure.us/...",
    "resourceGroupName": "string?"
  }
]
```

### POST /api/environments/{id}/sync-resources

Sync resources from Azure Resource Graph.

**Response 200**:
```json
{
  "resourcesFound": 5,
  "resourcesAdded": 2
}
```

### GET /api/environments/{id}/health

Get environment health status.

**Response 200**: `EnvironmentHealthDto`
```json
{
  "environmentId": "guid",
  "overallStatus": "Healthy|Degraded|Unhealthy",
  "hasDrift": false,
  "driftCount": 0,
  "estimatedMonthlyCost": 150.00,
  "issues": ["string"],
  "resourceHealth": [
    {
      "resourceName": "string",
      "resourceType": "string",
      "status": "string",
      "issues": ["string"]
    }
  ]
}
```

### GET /api/environments/{id}/activities

Get paginated activity history.

**Query Parameters**:
| Param | Type | Default |
|-------|------|---------|
| skip | int | 0 |
| take | int | 10 |

**Response 200**:
```json
{
  "activities": [
    {
      "id": "guid",
      "activityType": "string",
      "description": "string",
      "userId": "string?",
      "userName": "string?",
      "metadataJson": "string?",
      "timestamp": "datetime",
      "status": "string?",
      "errorMessage": "string?"
    }
  ],
  "hasMore": true
}
```

### GET /api/environments/summary

Get aggregate environment dashboard summary.

**Response 200**: `EnvironmentSummaryDto`
```json
{
  "totalCount": 25,
  "healthyCount": 20,
  "degradedCount": 3,
  "unhealthyCount": 2,
  "byStatus": { "Running": 20, "Provisioning": 3, "Failed": 2 },
  "driftCount": 3,
  "expiringWithin7Days": 2,
  "totalEstimatedMonthlyCost": 15000.00,
  "byTemplate": [
    { "templateName": "AKS Cluster", "count": 10 }
  ]
}
```

### GET /api/environments/expiring

List environments expiring soon.

**Query Parameters**:
| Param | Type | Default |
|-------|------|---------|
| withinDays | int | 7 |

**Response 200**: Array of `EnvironmentDetailDto`

### POST /api/environments/{id}/extend

Extend environment expiration.

**Request Body**: `ExtendExpirationRequest`
```json
{
  "newExpiresAt": "datetime (required)"
}
```

**Response 200**: `EnvironmentDetailDto`  
**Response 404**: Not found

### POST /api/environments/{id}/detect-drift

Detect drift for an environment.

**Response 200**: `DriftDetectionResultDto`
```json
{
  "environmentId": "guid",
  "driftItems": [
    {
      "id": "guid",
      "resourceId": "string",
      "resourceName": "string?",
      "resourceType": "string?",
      "propertyPath": "string",
      "expectedValue": "string?",
      "actualValue": "string?",
      "driftType": "PropertyChanged|ResourceAdded|ResourceRemoved",
      "severity": "Low|Medium|High|Critical",
      "canAutoRemediate": true
    }
  ],
  "totalDriftCount": 3,
  "detectedAt": "datetime"
}
```

### POST /api/environments/{id}/remediate-drift

Remediate drift items.

**Request Body** (optional): `RemediateDriftRequest`
```json
{
  "driftItemIds": ["guid", "guid"]
}
```

If body is null/empty, all drift items are remediated.

**Response 200**: `RemediateDriftResultDto`
```json
{
  "remediatedCount": 2,
  "failedCount": 1,
  "remainingCount": 0,
  "failures": [
    { "driftItemId": "guid", "error": "string" }
  ]
}
```

### POST /api/environments/{id}/refresh-status

Refresh deployment status from Azure.

**Response 200**: `RefreshDeploymentStatusResultDto`
```json
{
  "environmentId": "guid",
  "previousStatus": "Provisioning",
  "currentStatus": "Running",
  "statusChanged": true
}
```

### POST /api/environments/refresh-all-provisioning

Bulk-refresh all provisioning environments.

**Response 200**:
```json
{
  "refreshedCount": 3,
  "statusChanges": [
    {
      "environmentId": "guid",
      "previousStatus": "Provisioning",
      "currentStatus": "Running"
    }
  ]
}
```

### PATCH /api/environments/{id}/status

Manual status override (admin recovery).

**Request Body**: `UpdateStatusRequest`
```json
{
  "status": "Running|Failed|Suspended (required)",
  "reason": "string?"
}
```

**Response 200**: `EnvironmentDetailDto`  
**Response 404**: Not found

### POST /api/environments/{id}/delete-resources

Delete Azure resources for an environment.

**Response 200**: `DeleteResourcesResultDto`
```json
{
  "deletedResources": ["resource-id-1", "resource-id-2"],
  "failedResources": [
    { "resourceId": "string", "error": "string" }
  ],
  "deletedCount": 2,
  "failedCount": 0
}
```

---

## Compliance Controller

**Route**: `/api/compliance`  
**Auth**: Admin role  
**Note**: All endpoints return mock/stub data (TODO: ComplianceAgent integration)

### GET /api/compliance/summary

Get compliance overview.

**Response 200**: `ComplianceSummaryDto`
```json
{
  "overallScore": 85.5,
  "frameworkScores": [
    { "framework": "NIST 800-53", "score": 90.0 },
    { "framework": "FedRAMP High", "score": 81.0 }
  ],
  "environmentStatuses": [
    {
      "environmentId": "guid",
      "environmentName": "string",
      "complianceScore": 88.0,
      "violationCount": 3
    }
  ],
  "topViolations": [
    {
      "controlId": "string",
      "controlName": "string",
      "severity": "string",
      "affectedEnvironments": 5
    }
  ]
}
```

### POST /api/compliance/scan

Trigger a compliance scan.

**Query Parameters**:
| Param | Type | Required |
|-------|------|----------|
| environmentId | Guid? | No (scans all if omitted) |

**Response 202**: Accepted

### GET /api/compliance/environments/{environmentId}

Get per-environment compliance detail.

**Response 200**: `EnvironmentComplianceDto`
```json
{
  "environmentId": "guid",
  "environmentName": "string",
  "overallScore": 88.0,
  "frameworkResults": [
    {
      "framework": "NIST 800-53",
      "score": 90.0,
      "controls": [
        {
          "controlId": "AC-2",
          "controlName": "Account Management",
          "status": "Pass|Fail|NotApplicable",
          "severity": "High",
          "remediationGuidance": "string?"
        }
      ]
    }
  ],
  "resourceCompliance": [
    {
      "resourceName": "string",
      "resourceType": "string",
      "complianceScore": 95.0,
      "violations": 1
    }
  ]
}
```

---

## Health Controller

**Route**: `/health`  
**Auth**: None (unauthenticated)

### GET /health

Health check endpoint.

**Response 200**:
```json
{
  "status": "Healthy",
  "timestamp": "datetime"
}
```

---

## Common Models

### Template DTOs

#### TemplateDetailDto
Full template representation returned for single-template endpoints.

```json
{
  "templateId": "guid",
  "name": "string",
  "displayName": "string?",
  "description": "string",
  "version": "string",
  "category": "string",
  "format": "Bicep|ARM|Terraform",
  "status": "Draft|PendingApproval|Published|Deprecated",
  "content": "string",
  "deploymentScope": "string?",
  "parametersJson": "string?",
  "guardrailsJson": "string?",
  "complianceFrameworks": "string?",
  "keywords": "string?",
  "useCases": "string?",
  "aiSelectionHints": "string?",
  "additionalFilesJson": "string?",
  "parametersOverridden": false,
  "requiresApproval": true,
  "approvalSource": "string?",
  "approvedBy": "string?",
  "approvedAt": "datetime?",
  "approvalComments": "string?",
  "externalApprovalId": "string?",
  "externalApprovalUrl": "string?",
  "deprecatedBy": "string?",
  "deprecatedAt": "datetime?",
  "deprecationReason": "string?",
  "gitRepoUrl": "string?",
  "gitBranch": "string?",
  "gitPath": "string?",
  "gitCommitSha": "string?",
  "gitAutoSync": false,
  "gitSyncIntervalMinutes": 60,
  "gitSyncStatus": "NotConfigured|Synced|OutOfSync|SyncFailed",
  "gitLastSyncAt": "datetime?",
  "createdAt": "datetime",
  "createdBy": "string?",
  "updatedAt": "datetime"
}
```

#### TemplateParameterDto
```json
{
  "name": "string",
  "displayName": "string?",
  "description": "string?",
  "type": "string",
  "required": true,
  "defaultValue": "string?",
  "allowedValues": ["string"],
  "minValue": "number?",
  "maxValue": "number?",
  "displayOrder": 0
}
```

#### TemplateGuardrailDto
```json
{
  "type": "string",
  "property": "string",
  "operator": "string",
  "value": "string",
  "action": "Deny|Warn",
  "errorMessage": "string"
}
```

### Environment DTOs

#### EnvironmentDetailDto
```json
{
  "id": "guid",
  "name": "string",
  "displayName": "string?",
  "description": "string?",
  "templateId": "guid",
  "templateName": "string?",
  "subscriptionId": "string",
  "resourceGroup": "string",
  "location": "string",
  "status": "Provisioning|Running|Failed|Updating|Scaling|Deleting|Deleted|Suspended",
  "statusMessage": "string?",
  "deploymentId": "string?",
  "parameterValuesJson": "string?",
  "deployedResourcesJson": "string?",
  "tagsJson": "string?",
  "hasDrift": false,
  "driftCount": 0,
  "estimatedMonthlyCost": 150.00,
  "ownerEmail": "string?",
  "expiresAt": "datetime?",
  "autoDelete": false,
  "deploymentScope": "string?",
  "requestedBy": "string?",
  "createdAt": "datetime",
  "updatedAt": "datetime"
}
```

---

## Error Responses

All errors follow a consistent format:

```json
{
  "error": "string (error type)",
  "message": "string (human-readable)",
  "details": ["string"] 
}
```

**HTTP Status Codes**:
| Code | Meaning |
|------|---------|
| 400 | Validation error, invalid state transition, invalid request |
| 401 | Missing or invalid JWT token |
| 403 | Valid token but insufficient role permissions |
| 404 | Resource not found |
| 409 | Concurrency conflict (stale ETag) |
| 500 | Unexpected server error |
| 503 | Service unavailable (NL matching service) |

---

## Headers

### Request Headers
| Header | Purpose | When Required |
|--------|---------|--------------|
| Authorization | `Bearer {jwt-token}` | All endpoints except /health |
| If-Match | `"base64-rowversion"` | PUT/PATCH mutations (optimistic concurrency) |
| Content-Type | `application/json` | All request bodies |

### Response Headers
| Header | Purpose | When Present |
|--------|---------|--------------|
| ETag | `"base64-rowversion"` | GET single resource, POST create, PUT update |
| Location | URI of created resource | 201 Created responses |
