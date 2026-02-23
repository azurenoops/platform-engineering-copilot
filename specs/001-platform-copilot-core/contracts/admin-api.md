# Admin REST API Contract

**Branch**: `001-platform-copilot-core` | **Date**: 2026-02-22

The Admin API (port 5050) provides REST endpoints for the Admin Dashboard (Blazor WASM on port 5000). Swagger/OpenAPI available at `/swagger`.

## Base URL

```
http://localhost:5050/api
```

## Authentication

- **Read operations** (GET): No authentication required — served from cached data (FR-063)
- **Write operations** (POST/PUT/DELETE): CAC + PIM Write-tier required
- **Deployment operations**: CAC + PIM Write-tier required

---

## Service Templates

### `GET /api/templates`

List all service templates.

**Auth**: Not required

**Response** `200 OK`:
```json
{
  "templates": [
    {
      "templateId": "guid",
      "name": "Standard AKS Cluster",
      "description": "IL5-compliant AKS cluster with network policies",
      "category": "Compute",
      "version": "1.2.0",
      "isApproved": true,
      "approvedBy": "Jane Smith",
      "gitSyncStatus": "Synced",
      "updatedAt": "2026-02-20T08:00:00Z"
    }
  ],
  "totalCount": 15
}
```

### `GET /api/templates/{templateId}`

Get service template details including full Bicep content.

**Auth**: Not required

### `POST /api/templates`

Create a new service template.

**Auth**: Required (CAC + PIM Write)

**Request**:
```json
{
  "name": "Standard AKS Cluster",
  "description": "IL5-compliant AKS cluster...",
  "category": "Compute",
  "contentBicep": "// Bicep content...",
  "parameters": { "clusterName": { "type": "string" } },
  "gitRepoUrl": "https://github.com/org/templates",
  "gitBranch": "main",
  "version": "1.0.0"
}
```

**Response** `201 Created`

### `PUT /api/templates/{templateId}`

Update a service template.

**Auth**: Required (CAC + PIM Write)

### `DELETE /api/templates/{templateId}`

Delete a service template.

**Auth**: Required (CAC + PIM Write)

### `POST /api/templates/{templateId}/sync`

Trigger Git sync for a service template.

**Auth**: Required (CAC + PIM Write)

---

## Environments

### `GET /api/environments`

List provisioned environments.

**Auth**: Not required

**Response** `200 OK`:
```json
{
  "environments": [
    {
      "environmentId": "guid",
      "name": "rg-prod-gov",
      "subscriptionId": "guid",
      "status": "Active",
      "resourceCount": 45,
      "lastScanAt": "2026-02-22T06:00:00Z",
      "complianceScore": 0.87
    }
  ]
}
```

### `GET /api/environments/{environmentId}`

Get environment details and resource inventory.

**Auth**: Not required

---

## Deployments

### `GET /api/deployments`

List recent deployments.

**Auth**: Not required

**Response** `200 OK`:
```json
{
  "deployments": [
    {
      "deploymentId": "guid",
      "templateName": "Standard AKS Cluster",
      "resourceGroup": "rg-prod-gov",
      "status": "Succeeded",
      "initiatedBy": "John Doe",
      "startedAt": "2026-02-22T09:00:00Z",
      "completedAt": "2026-02-22T09:05:00Z"
    }
  ]
}
```

### `POST /api/deployments`

Initiate a deployment from a service template.

**Auth**: Required (CAC + PIM Write)

**Request**:
```json
{
  "templateId": "guid",
  "resourceGroup": "rg-prod-gov",
  "parameters": { "clusterName": "aks-prod-01" }
}
```

---

## Governance

### `GET /api/governance/snapshots`

List governance snapshots.

**Auth**: Not required

**Response** `200 OK`:
```json
{
  "snapshots": [
    {
      "snapshotId": "guid",
      "timestamp": "2026-02-22T00:00:00Z",
      "overallCompliance": 0.92,
      "policyAssignments": 145,
      "nonCompliantResources": 23,
      "secureScore": 78
    }
  ]
}
```

---

## Cost Overview

### `GET /api/costs/summary`

Get cost summary for dashboard display.

**Auth**: Not required

**Response** `200 OK`:
```json
{
  "currentMonth": 15234.50,
  "previousMonth": 14890.25,
  "changePercent": 2.3,
  "forecastEndOfMonth": 16100.00,
  "topResources": [
    { "resourceType": "Virtual Machines", "cost": 8500.00 }
  ]
}
```

---

## Health & Observability

### `GET /api/health`

System health check (FR-075).

**Auth**: Not required

**Response** `200 OK`:
```json
{
  "status": "Healthy",
  "timestamp": "2026-02-22T10:30:00Z",
  "agents": {
    "compliance": { "status": "Healthy", "lastCheck": "2026-02-22T10:29:55Z" },
    "infrastructure": { "status": "Healthy", "lastCheck": "2026-02-22T10:29:55Z" },
    "cost-management": { "status": "Degraded", "lastCheck": "2026-02-22T10:29:55Z", "reason": "Azure Cost API latency >5s" },
    "discovery": { "status": "Healthy" },
    "environment": { "status": "Healthy" },
    "knowledgebase": { "status": "Healthy" },
    "configuration": { "status": "Healthy" },
    "security": { "status": "Healthy" }
  },
  "database": { "status": "Healthy" },
  "version": "1.0.0"
}
```

---

## Error Response Contract

All error responses follow a consistent format:

```json
{
  "error": {
    "code": "AUTH_REQUIRED | PIM_REQUIRED | NOT_FOUND | VALIDATION_ERROR | INTERNAL_ERROR",
    "message": "Plain-language error description",
    "details": { ... },
    "traceId": "correlation-id"
  }
}
```

| HTTP Status | Error Code | When |
|-------------|-----------|------|
| 401 | AUTH_REQUIRED | CAC not authenticated |
| 403 | PIM_REQUIRED | PIM elevation missing or insufficient tier |
| 403 | ROLE_DENIED | User role does not permit operation |
| 404 | NOT_FOUND | Resource not found |
| 400 | VALIDATION_ERROR | Invalid request parameters |
| 500 | INTERNAL_ERROR | Server error |
