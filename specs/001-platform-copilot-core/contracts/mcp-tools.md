# MCP Tool Contracts

**Branch**: `001-platform-copilot-core` | **Date**: 2026-02-22

The MCP Server (port 5100) exposes tools via JSON-RPC over HTTP and stdio. This document defines the tool catalog, parameter schemas, and authentication requirements.

## Transport

| Mode | Protocol | Endpoint |
|------|----------|----------|
| HTTP (default) | JSON-RPC over HTTP POST | `http://localhost:5100` |
| stdio (`--stdio`) | JSON-RPC over stdin/stdout | N/A (piped) |

Both modes expose identical tool capabilities (FR-007).

---

## Tool Authentication Metadata

Every tool includes auth metadata in its descriptor:

```json
{
  "name": "tool_name",
  "description": "...",
  "parameters": { ... },
  "metadata": {
    "requiresAuthentication": true | false,
    "pimTierRequired": "None" | "Read" | "Write"
  }
}
```

Server-side enforcement (FR-010):
- `requiresAuthentication: false` → No CAC or PIM check; executes immediately
- `requiresAuthentication: true, pimTierRequired: "Read"` → CAC + read-tier PIM required
- `requiresAuthentication: true, pimTierRequired: "Write"` → CAC + write-tier PIM required

---

## Tool Catalog by Agent

### Compliance Agent (12 tools)

> **Detailed contract**: See [compliance-tools.md](compliance-tools.md) for full parameter schemas, response envelopes, error codes, pagination, parameter conventions, HTTP status code mapping, and tool-specific behaviors.
>
> **Dependency**: `compliance_get_control_family`, `compliance_assess`, and `compliance_remediate` consume `INistService` (FR-080) for control catalog lookups and remediation guidance.

All Compliance Agent tools follow the platform-wide response envelope: `{ status, data, metadata: { toolName, executionTimeMs, timestamp } }`.

#### `compliance_assess`

Run a NIST 800-53 compliance assessment against an Azure subscription.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

**Parameters**: `subscriptionId`, `framework` (NIST80053/FedRAMPHigh/FedRAMPModerate/DoDIL5), `scanType` (resource/policy/combined), `controlFamilies`, `resourceTypes`, `includePassed`. All optional — defaults from configuration.

#### `compliance_get_control_family`

Get details for a specific NIST 800-53 control family.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

**Parameters**: `familyId` (required), `includeControls` (default: true).

#### `compliance_remediate`

Remediate a compliance finding — single or batch. Defaults to dry-run mode.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Write |

**Parameters**: `findingId` (mutually exclusive with `controlFamily`), `controlFamily`, `severity`, `applyRemediation` (default: false), `dryRun` (default: true). `dryRun` takes precedence if both provided.

#### `compliance_validate_remediation`

Validate that a previously applied remediation was successful.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

**Parameters**: `findingId` (required), `executionId`, `subscriptionId`.

#### `compliance_generate_plan`

Generate a prioritized remediation plan for all open findings.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

**Parameters**: `subscriptionId`, `resourceGroupName`. All optional.

#### `compliance_collect_evidence`

Collect compliance evidence from Azure for audit purposes. Defaults to append mode (immutable records); `replace: true` for explicit opt-in replacement.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

**Parameters**: `controlId` (required), `subscriptionId`, `resourceGroup`, `replace` (default: false). Paginated.

#### `compliance_generate_document`

Generate a compliance document (SSP, SAR, or POA&M) based on assessment results.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

**Parameters**: `documentType` (required: SSP/SAR/POAM), `subscriptionId`, `framework`, `systemName`, `owner`, `assessmentId`. Max 5MB output.

#### `compliance_status`

Get current compliance posture summary (lightweight, reads from DB only).

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

**Parameters**: `subscriptionId`, `framework`. All optional.

#### `compliance_history`

Get compliance assessment history and trend data.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

**Parameters**: `subscriptionId`, `days` (default: 30), `scanType`. Paginated.

#### `compliance_monitoring`

Lightweight on-demand compliance monitoring (status, scan, alerts, trend). Full continuous monitoring infrastructure (US10) is deferred.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

**Parameters**: `action` (required: status/scan/alerts/trend), `subscriptionId`, `days` (default: 30). Paginated (alerts).

#### `compliance_audit_log`

Query the audit trail of all compliance actions.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

**Parameters**: `subscriptionId`, `days` (default: 7), `actionType`. Paginated.

#### `compliance_chat`

Natural language compliance interaction with conversation memory.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

**Parameters**: `message` (required), `conversationId`.

---

### Infrastructure Agent (6 tools)

#### `generate_infrastructure_template`

Generates a compliant IaC template.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

**Parameters**:
```json
{
  "resourceType": { "type": "string", "required": true, "description": "e.g., 'AKS cluster', 'Storage Account'" },
  "region": { "type": "string", "default": "usgovvirginia" },
  "method": { "type": "string", "enum": ["template-generator", "ai-generated", "bicep-acr"], "default": "template-generator" },
  "format": { "type": "string", "enum": ["bicep", "terraform"], "default": "bicep" },
  "additionalRequirements": { "type": "string", "description": "Free-text customization" }
}
```

**Response**:
```json
{
  "templateId": "guid",
  "method": "template-generator",
  "format": "bicep",
  "content": "// Generated Bicep template\nresource storageAccount ...",
  "complianceAnnotations": [
    { "line": 5, "property": "supportsHttpsTrafficOnly", "controlId": "SC-8", "controlName": "Transmission Confidentiality" }
  ],
  "annotationCoverage": 0.92,
  "expiresAt": "2026-02-22T11:00:00Z"
}
```

#### `provision_infrastructure`

Deploys a generated template to Azure.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Write |

**Parameters**:
```json
{
  "templateId": { "type": "string", "required": true },
  "resourceGroup": { "type": "string", "required": true },
  "confirm": { "type": "boolean", "default": false }
}
```

#### `validate_template`

Validates a template against compliance rules.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

#### `list_deployments`

Lists recent deployments.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

#### `get_deployment_status`

Gets status of a specific deployment.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

#### `rollback_deployment`

Rolls back a failed deployment.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Write |

---

### Cost Management Agent (6 tools)

#### `get_cost_analysis`

Queries Azure Cost Management for spending breakdown.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

**Parameters**:
```json
{
  "timeframe": { "type": "string", "enum": ["7d", "30d", "90d", "custom"], "default": "30d" },
  "groupBy": { "type": "string", "enum": ["resourceType", "resourceGroup", "service", "tag"], "default": "resourceType" },
  "startDate": { "type": "string", "format": "date", "description": "Required if timeframe=custom" },
  "endDate": { "type": "string", "format": "date" }
}
```

#### `get_cost_forecast`

Forecasts future spending based on historical data.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

#### `get_optimization_suggestions`

Identifies cost-saving opportunities.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

**Response**:
```json
{
  "suggestions": [
    {
      "category": "Idle Resources",
      "resource": "/subscriptions/.../virtualMachines/vm1",
      "description": "VM has <5% CPU usage for 14 days",
      "estimatedMonthlySavings": 150.00,
      "action": "Deallocate or resize"
    }
  ],
  "totalEstimatedSavings": 450.00
}
```

#### `get_cached_cost_report`

Retrieves previously fetched cost data.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

#### `get_budget_status`

Checks budget consumption and alerts.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

#### `get_cost_anomalies`

Detects anomalous spending patterns.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

---

### Discovery Agent (9 tools)

#### `discover_resources`

Queries Azure Resource Graph for resource inventory.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

**Parameters**:
```json
{
  "resourceType": { "type": "string", "description": "Filter by type (optional)" },
  "subscriptionId": { "type": "string" },
  "includeHealth": { "type": "boolean", "default": true }
}
```

#### `get_resource_dependencies`

Maps resource dependencies.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

#### `cross_subscription_query`

Queries resources across multiple subscriptions.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

*... plus 6 additional discovery tools (resource health, network topology, tag analysis, etc.)*

---

### Knowledge Base Agent (8 tools)

> **Dependency**: All KB tools consume `INistService` (FR-080) for control catalog queries. No Azure connectivity required — all data loaded from embedded JSON at startup.

#### `explain_control`

Explains a compliance control in plain language.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

**Parameters**:
```json
{
  "controlId": { "type": "string", "required": true, "description": "e.g., 'AC-2', 'SC-8'" },
  "framework": { "type": "string", "default": "nist-800-53" }
}
```

**Response**:
```json
{
  "controlId": "AC-2",
  "controlName": "Account Management",
  "family": "Access Control",
  "description": "Plain-language explanation...",
  "azureServiceMappings": [
    { "service": "Azure AD", "capability": "User lifecycle management" },
    { "service": "Azure Policy", "capability": "Enforce account policies" }
  ],
  "implementationGuidance": "Step-by-step guidance...",
  "relatedControls": ["AC-3", "AC-6", "IA-2"]
}
```

#### `compare_frameworks`

Compares controls across frameworks.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

#### `get_stig_guidance`

Returns STIG implementation guidance.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

#### `get_ato_checklist`

Provides ATO preparation guidance.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

#### `search_controls`

Searches across all frameworks by keyword.

| Property | Value |
|----------|-------|
| Auth | Not required |
| PIM Tier | None |

*... plus 3 additional KB tools (framework summary, control mapping, implementation examples)*

---

### Configuration Agent (1 tool)

> **Detailed contract**: See [configuration-tools.md](configuration-tools.md) for full parameter schema, sub-actions, response formats, error codes, shared state keys, and routing patterns.

#### `configuration_manage`

Manages ATO Copilot configuration settings via 5 sub-actions: `get_configuration`, `set_subscription`, `set_framework`, `set_baseline`, `set_preference`.

| Property | Value |
|----------|-------|
| Auth | Not required (for local settings) |
| PIM Tier | None (Read for validation) |

**Parameters**:
```json
{
  "action": { "type": "string", "enum": ["get_configuration", "set_subscription", "set_framework", "set_baseline", "set_preference"], "required": true },
  "subscriptionId": { "type": "string", "description": "Azure subscription ID (for set_subscription)" },
  "framework": { "type": "string", "enum": ["NIST80053", "FedRAMPHigh", "FedRAMPModerate", "DoDIL5"] },
  "baseline": { "type": "string", "enum": ["High", "Moderate", "Low"] },
  "preferenceName": { "type": "string", "enum": ["dryRunDefault", "defaultScanType", "cloudEnvironment", "region"] },
  "preferenceValue": { "type": "string", "description": "Preference value (for set_preference)" }
}
```

**Shared State**: Writes to `IAgentStateManager` with `config:` prefix (config:settings, config:subscriptionId, config:framework, config:baseline). Other agents read these keys for defaults.

---

### Security Agent

#### `get_secure_score`

Retrieves Azure Secure Score.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

#### `get_security_recommendations`

Lists security recommendations from Defender.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

#### `manage_security_policy`

Views/modifies security policies.

| Property | Value |
|----------|-------|
| Auth | Required (Read for view, Write for modify) |
| PIM Tier | Read / Write |

---

### Environment Agent (10 tools)

#### `clone_environment`

Clones an environment with proper naming.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Write |

#### `detect_drift`

Detects drift between environments.

| Property | Value |
|----------|-------|
| Auth | Required |
| PIM Tier | Read |

*... plus 8 additional environment tools (compare, promote, list, status, etc.)*

---

## Error Response Contract

All tools across all agents return errors using the platform-wide response envelope (FR-079):

```json
{
  "status": "error",
  "data": null,
  "error": {
    "errorCode": "AUTH_REQUIRED | PIM_REQUIRED | ROLE_DENIED | SUBSCRIPTION_NOT_CONFIGURED | AZURE_API_ERROR | VALIDATION_ERROR",
    "message": "Plain-language error description (FR-067)",
    "suggestion": "Step to resolve..."
  },
  "metadata": {
    "toolName": "tool_name",
    "executionTimeMs": 123,
    "timestamp": "2026-02-22T10:30:00Z"
  }
}
```

For auth-specific errors, `error` also includes role context:

```json
{
  "errorCode": "ROLE_DENIED",
  "message": "This operation requires Platform Engineer role with write-tier PIM elevation.",
  "suggestion": "Contact your administrator to request role eligibility.",
  "details": {
    "requiredRole": "PlatformEngineer",
    "requiredPimTier": "Write",
    "currentRoles": ["Auditor"],
    "currentPimTier": "None"
  }
}
```

See [compliance-tools.md](compliance-tools.md) for the complete error code catalog and HTTP status code mapping.
