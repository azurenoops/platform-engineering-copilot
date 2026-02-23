# MCP Tool Contracts: Compliance Agent

**Branch**: `001-core-compliance` | **Date**: 2026-02-21

All tools are exposed via MCP JSON-RPC 2.0 protocol. Tools are invoked through `tools/call`
with the tool name and JSON arguments. All responses follow the standard envelope schema
per Constitution Principle VII.

## Response Envelope

Every tool response MUST conform to this schema:

```json
{
  "status": "success | error | partial",
  "data": { /* tool-specific payload */ },
  "metadata": {
    "toolName": "compliance_assess",
    "executionTimeMs": 12345,
    "timestamp": "2026-02-21T10:30:00Z"
  }
}
```

- `metadata.executionTimeMs` is an integer in **milliseconds**.
- `metadata.timestamp` is **ISO 8601 with UTC timezone** (suffix `Z`).
- `status: "partial"` indicates the operation partially succeeded (e.g., resource scan
  succeeded but policy scan failed in combined mode).

Error responses add:
```json
{
  "status": "error",
  "data": null,
  "error": {
    "message": "Human-readable explanation",
    "errorCode": "SUBSCRIPTION_NOT_CONFIGURED",
    "suggestion": "Run 'set subscription <subscription-id>' first"
  },
  "metadata": { ... }
}
```

When multiple errors occur in a single operation, the `error` field becomes an array:
```json
{
  "status": "error",
  "data": null,
  "errors": [
    { "message": "...", "errorCode": "RESOURCE_SCAN_FAILED", "suggestion": "..." },
    { "message": "...", "errorCode": "POLICY_SCAN_FAILED", "suggestion": "..." }
  ],
  "metadata": { ... }
}
```
Multiple errors are ordered by severity (most critical first).

---

## Pagination

Tools that return unbounded result sets MUST support pagination:

```json
{
  "pagination": {
    "page": 1,
    "pageSize": 25,
    "totalItems": 142,
    "totalPages": 6,
    "hasNextPage": true,
    "nextPageToken": "eyJ..."
  }
}
```

**Paginated tools**: `compliance_assess` (findings list), `compliance_history`,
`compliance_audit_log`, `compliance_collect_evidence` (evidence list),
`compliance_monitoring` (alerts).

**Default page size**: 25 items. **Maximum page size**: 100 items.

Pagination is requested via optional parameters:
```json
{
  "page": 1,
  "pageSize": 25
}
```

---

## Maximum Response Size

- Individual tool responses MUST NOT exceed **1MB** of JSON payload.
- Findings lists default to **top 25** most critical, sorted by severity descending.
- If a response would exceed 1MB, it MUST be truncated with a `truncated: true` flag and
  a message indicating how to retrieve the full result set via pagination.

---

## Parameter Conventions

### Case Sensitivity

- Enum parameters (`framework`, `scanType`, `severity`, `baseline`, `documentType`)
  MUST be **case-insensitive** on input. The system normalizes to canonical form
  (e.g., `nist80053` → `NIST80053`, `high` → `High`).
- Control family IDs MUST be **case-insensitive** on input, normalized to uppercase
  (e.g., `ac` → `AC`).
- `controlFamilies` comma-separated string: Whitespace around commas is trimmed
  (e.g., `"AC, AU, SC"` → `["AC", "AU", "SC"]`).

### Subscription Fallback

All tools that accept `subscriptionId` MUST behave identically when it is omitted:
fall back to the configured default from `IAgentStateManager` key `config:subscriptionId`.
If no default is configured, return error code `SUBSCRIPTION_NOT_CONFIGURED`.

---

## HTTP Status Code Mapping

When served via `McpHttpBridge`, MCP error codes map to HTTP status codes:

| MCP Error Code | HTTP Status |
|---------------|-------------|
| `SUBSCRIPTION_NOT_CONFIGURED` | 400 Bad Request |
| `SUBSCRIPTION_NOT_FOUND` | 404 Not Found |
| `AZURE_AUTH_FAILED` | 401 Unauthorized |
| `AZURE_API_ERROR` | 502 Bad Gateway |
| `RESOURCE_SCAN_FAILED` | 502 Bad Gateway |
| `POLICY_SCAN_FAILED` | 502 Bad Gateway |
| `DEFENDER_SCAN_FAILED` | 502 Bad Gateway |
| `REMEDIATION_FAILED` | 500 Internal Server Error |
| `REMEDIATION_DENIED` | 403 Forbidden |
| `APPROVAL_REQUIRED` | 403 Forbidden |
| `DOCUMENT_GENERATION_FAILED` | 500 Internal Server Error |
| `NO_ASSESSMENT_DATA` | 404 Not Found |
| `INVALID_CONTROL_ID` | 400 Bad Request |
| `RATE_LIMITED` | 429 Too Many Requests |
| `REMEDIATION_IN_PROGRESS` | 409 Conflict |
| `ASSESSMENT_TIMEOUT` | 504 Gateway Timeout |
| `INVALID_SUBSCRIPTION_ID` | 400 Bad Request |
| `INVALID_FRAMEWORK` | 400 Bad Request |
| `INVALID_BASELINE` | 400 Bad Request |

---

## Tool: `compliance_assess`

Run a NIST 800-53 compliance assessment against an Azure subscription.

### Parameters

```json
{
  "name": "compliance_assess",
  "description": "Run a NIST 800-53 compliance assessment against an Azure subscription",
  "inputSchema": {
    "type": "object",
    "properties": {
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID. If omitted, uses configured default."
      },
      "framework": {
        "type": "string",
        "enum": ["NIST80053", "FedRAMPHigh", "FedRAMPModerate", "DoDIL5"],
        "description": "Compliance framework to assess against. Default from configuration."
      },
      "scanType": {
        "type": "string",
        "enum": ["resource", "policy", "combined"],
        "default": "combined",
        "description": "Type of scan: resource (Azure Resource Graph), policy (Azure Policy), or combined."
      },
      "controlFamilies": {
        "type": "string",
        "description": "Comma-separated control families to assess (e.g., 'AC,AU,SC'). All if omitted."
      },
      "resourceTypes": {
        "type": "string",
        "description": "Comma-separated resource types to scan (e.g., 'microsoft.storage/storageaccounts'). All if omitted."
      },
      "includePassed": {
        "type": "boolean",
        "default": false,
        "description": "Include passing controls in results."
      }
    },
    "required": []
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "assessmentId": "a1b2c3d4-...",
    "subscriptionId": "sub-123",
    "framework": "NIST80053",
    "baseline": "High",
    "scanType": "combined",
    "assessedAt": "2026-02-21T10:30:00Z",
    "complianceScore": 85.5,
    "summary": {
      "totalControls": 421,
      "passedControls": 360,
      "failedControls": 48,
      "notAssessedControls": 13
    },
    "resourceScanSummary": {
      "resourcesScanned": 142,
      "compliant": 130,
      "nonCompliant": 12,
      "compliancePercentage": 91.5
    },
    "policyScanSummary": {
      "policiesEvaluated": 312,
      "compliant": 265,
      "nonCompliant": 47,
      "compliancePercentage": 84.9
    },
    "findingsByFamily": [
      {
        "family": "AC",
        "familyName": "Access Control",
        "totalControls": 25,
        "passed": 20,
        "failed": 5,
        "criticalCount": 1,
        "highCount": 2,
        "mediumCount": 2
      }
    ],
    "criticalFindings": [
      {
        "findingId": "f1-...",
        "controlId": "AC-2",
        "title": "Account Management",
        "severity": "Critical",
        "scanSource": "resource",
        "resourceId": "/subscriptions/.../resourceGroups/.../providers/...",
        "remediationGuidance": "Enable MFA for all privileged accounts"
      }
    ]
  },
  "metadata": {
    "toolName": "compliance_assess",
    "executionTimeMs": 45230,
    "timestamp": "2026-02-21T10:30:45Z"
  }
}
```

---

## Tool: `compliance_get_control_family`

Get details for a specific NIST 800-53 control family.

### Parameters

```json
{
  "name": "compliance_get_control_family",
  "description": "Get detailed information about a NIST 800-53 control family and current compliance status",
  "inputSchema": {
    "type": "object",
    "properties": {
      "familyId": {
        "type": "string",
        "description": "Control family abbreviation (e.g., 'AC', 'AU', 'SC')."
      },
      "includeControls": {
        "type": "boolean",
        "default": true,
        "description": "Include individual controls in the response."
      }
    },
    "required": ["familyId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "family": "AC",
    "familyName": "Access Control",
    "totalControls": 25,
    "controls": [
      {
        "controlId": "AC-2",
        "title": "Account Management",
        "status": "Fail",
        "severity": "High",
        "resourceFindings": 3,
        "policyFindings": 2,
        "remediationGuidance": "..."
      }
    ]
  },
  "metadata": { ... }
}
```

---

## Tool: `compliance_remediate`

Remediate a compliance finding (single or batch).

### Parameters

```json
{
  "name": "compliance_remediate",
  "description": "Remediate one or more compliance findings. Defaults to dry-run mode.",
  "inputSchema": {
    "type": "object",
    "properties": {
      "findingId": {
        "type": "string",
        "description": "Specific finding ID to remediate. Mutually exclusive with batch parameters."
      },
      "controlFamily": {
        "type": "string",
        "description": "Remediate all findings in this control family (batch mode)."
      },
      "severity": {
        "type": "string",
        "enum": ["Critical", "High", "Medium", "Low"],
        "description": "Remediate all findings at or above this severity (batch mode)."
      },
      "applyRemediation": {
        "type": "boolean",
        "default": false,
        "description": "Actually apply the remediation (false = dry-run)."
      },
      "dryRun": {
        "type": "boolean",
        "default": true,
        "description": "Run in dry-run mode (show what would change without applying)."
      }
    },
    "required": []
  }
}
```

### Response (Dry-Run)

```json
{
  "status": "success",
  "data": {
    "mode": "dry-run",
    "remediationPlanId": "rp-...",
    "totalFindings": 5,
    "resourceRemediations": 3,
    "policyRemediations": 2,
    "highRiskWarning": "⚠️ This remediation affects Access Control settings. Applying this could impact user access.",
    "steps": [
      {
        "stepId": "s1-...",
        "findingId": "f1-...",
        "controlId": "AC-2",
        "description": "Enable MFA for privileged accounts",
        "remediationType": "ResourceConfiguration",
        "riskLevel": "High",
        "affectedResources": ["/subscriptions/.../..."],
        "estimatedImpact": "Users will need to re-authenticate with MFA"
      }
    ],
    "confirmationRequired": true,
    "confirmationMessage": "Apply 5 remediations (3 resource changes, 2 policy assignments)? Reply 'apply this remediation' to proceed."
  },
  "metadata": { ... }
}
```

---

## Tool: `compliance_validate_remediation`

Validate that a remediation was applied successfully.

### Parameters

```json
{
  "name": "compliance_validate_remediation",
  "description": "Validate that a previously applied remediation was successful",
  "inputSchema": {
    "type": "object",
    "properties": {
      "findingId": {
        "type": "string",
        "description": "Finding ID to validate."
      },
      "executionId": {
        "type": "string",
        "description": "Remediation execution ID."
      },
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID."
      }
    },
    "required": ["findingId"]
  }
}
```

---

## Tool: `compliance_generate_plan`

Generate a prioritized remediation plan.

### Parameters

```json
{
  "name": "compliance_generate_plan",
  "description": "Generate a prioritized remediation plan for all open findings",
  "inputSchema": {
    "type": "object",
    "properties": {
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID."
      },
      "resourceGroupName": {
        "type": "string",
        "description": "Scope to a specific resource group."
      }
    },
    "required": []
  }
}
```

---

## Tool: `compliance_collect_evidence`

Collect compliance evidence for a specific control or control family.

### Parameters

```json
{
  "name": "compliance_collect_evidence",
  "description": "Collect compliance evidence from Azure for audit purposes",
  "inputSchema": {
    "type": "object",
    "properties": {
      "controlId": {
        "type": "string",
        "description": "NIST control ID (e.g., 'AC-2') or family (e.g., 'AC')."
      },
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID. Uses configured default if omitted."
      },
      "resourceGroup": {
        "type": "string",
        "description": "Scope to a specific resource group."
      }
    },
    "required": ["controlId"]
  }
}
```

### Response

```json
{
  "status": "success",
  "data": {
    "controlId": "AC-2",
    "evidenceCount": 4,
    "evidence": [
      {
        "evidenceId": "ev-...",
        "type": "ConfigurationExport",
        "category": "Configuration",
        "description": "Azure AD Conditional Access policies export",
        "collectedAt": "2026-02-21T10:35:00Z",
        "contentSizeBytes": 4096,
        "resourceId": "/subscriptions/.../..."
      }
    ]
  },
  "metadata": { ... }
}
```

---

## Tool: `compliance_generate_document`

Generate a compliance document (SSP, SAR, or POA&M).

### Parameters

```json
{
  "name": "compliance_generate_document",
  "description": "Generate a compliance document based on assessment results",
  "inputSchema": {
    "type": "object",
    "properties": {
      "documentType": {
        "type": "string",
        "enum": ["SSP", "SAR", "POAM"],
        "description": "Type of compliance document to generate."
      },
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID."
      },
      "framework": {
        "type": "string",
        "description": "Compliance framework."
      },
      "systemName": {
        "type": "string",
        "description": "Name of the system for the document header."
      },
      "owner": {
        "type": "string",
        "description": "System owner name."
      },
      "assessmentId": {
        "type": "string",
        "description": "Specific assessment to base the document on. Uses latest if omitted."
      }
    },
    "required": ["documentType"]
  }
}
```

---

## Tool: `compliance_status`

Get current compliance posture summary.

### Parameters

```json
{
  "name": "compliance_status",
  "description": "Get current compliance status summary for a subscription",
  "inputSchema": {
    "type": "object",
    "properties": {
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID."
      },
      "framework": {
        "type": "string",
        "description": "Filter by compliance framework."
      }
    },
    "required": []
  }
}
```

---

## Tool: `compliance_history`

Get compliance assessment history and trends.

### Parameters

```json
{
  "name": "compliance_history",
  "description": "Get compliance assessment history and trend data",
  "inputSchema": {
    "type": "object",
    "properties": {
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID."
      },
      "days": {
        "type": "integer",
        "default": 30,
        "description": "Number of days of history to retrieve."
      },
      "scanType": {
        "type": "string",
        "enum": ["resource", "policy", "combined"],
        "description": "Filter by scan type."
      }
    },
    "required": []
  }
}
```

---

## Tool: `compliance_monitoring`

Compliance monitoring including drift detection.

### Parameters

```json
{
  "name": "compliance_monitoring",
  "description": "Continuous compliance monitoring, drift detection, and alerts",
  "inputSchema": {
    "type": "object",
    "properties": {
      "action": {
        "type": "string",
        "enum": ["status", "scan", "alerts", "trend"],
        "description": "Monitoring action to perform."
      },
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID."
      },
      "days": {
        "type": "integer",
        "default": 30,
        "description": "Time window for alerts and trends."
      }
    },
    "required": ["action"]
  }
}
```

---

## Tool: `compliance_audit_log`

Query the audit trail of all compliance actions.

### Parameters

```json
{
  "name": "compliance_audit_log",
  "description": "Query the audit log of compliance actions",
  "inputSchema": {
    "type": "object",
    "properties": {
      "subscriptionId": {
        "type": "string",
        "description": "Filter by subscription."
      },
      "days": {
        "type": "integer",
        "default": 7,
        "description": "Number of days of audit history."
      },
      "actionType": {
        "type": "string",
        "description": "Filter by action type (Assessment, Remediation, etc.)."
      }
    },
    "required": []
  }
}
```

---

## Tool: `compliance_chat`

Natural language compliance interaction with conversation memory.

### Parameters

```json
{
  "name": "compliance_chat",
  "description": "Natural language compliance interaction with conversation context",
  "inputSchema": {
    "type": "object",
    "properties": {
      "message": {
        "type": "string",
        "description": "Natural language compliance question or command."
      },
      "conversationId": {
        "type": "string",
        "description": "Conversation ID for context continuity."
      }
    },
    "required": ["message"]
  }
}
```

---

## Error Codes

| Code | Meaning | Suggestion | Applicable Tools |
|------|---------|------------|-----------------|
| `SUBSCRIPTION_NOT_CONFIGURED` | No default subscription and none specified | "Run 'set subscription <id>' first" | All compliance tools |
| `SUBSCRIPTION_NOT_FOUND` | Subscription ID does not exist or no access | "Verify the subscription ID and your Azure permissions" | `compliance_assess`, `compliance_remediate`, `compliance_collect_evidence` |
| `AZURE_AUTH_FAILED` | Azure authentication failed | "Run 'az login' or check managed identity config" | All Azure-calling tools |
| `AZURE_API_ERROR` | Azure API call failed | "Check connectivity and retry" | All Azure-calling tools |
| `RESOURCE_SCAN_FAILED` | Resource Graph query failed | "Verify Reader role on subscription" | `compliance_assess` (resource/combined) |
| `POLICY_SCAN_FAILED` | Policy compliance query failed | "Verify Policy Reader role" | `compliance_assess` (policy/combined) |
| `DEFENDER_SCAN_FAILED` | Defender for Cloud query failed | "Verify Security Reader role" | `compliance_assess`, `compliance_status` |
| `REMEDIATION_FAILED` | Remediation execution failed | "Review the error details; consider manual remediation" | `compliance_remediate` |
| `REMEDIATION_DENIED` | User role cannot perform remediation | "Only ComplianceOfficer and PlatformEngineer can remediate" | `compliance_remediate` |
| `REMEDIATION_IN_PROGRESS` | Another remediation is running on this subscription | "Wait for the current remediation to complete or cancel it" | `compliance_remediate` |
| `APPROVAL_REQUIRED` | Remediation needs ComplianceOfficer approval | "Ask a Compliance Officer to approve" | `compliance_remediate` |
| `DOCUMENT_GENERATION_FAILED` | Document could not be generated | "Ensure assessment data exists" | `compliance_generate_document` |
| `NO_ASSESSMENT_DATA` | No assessments found for the request | "Run a compliance assessment first" | `compliance_status`, `compliance_history`, `compliance_generate_document` |
| `INVALID_CONTROL_ID` | Control ID format is invalid | "Use format like AC-2 or AC-2(1)" | `compliance_get_control_family`, `compliance_collect_evidence` |
| `RATE_LIMITED` | Azure API rate limit hit | "Wait and retry in a few seconds" | All Azure-calling tools |
| `ASSESSMENT_TIMEOUT` | Assessment exceeded 60-second timeout | "Try scanning fewer control families or a smaller subscription" | `compliance_assess` |
| `INVALID_SUBSCRIPTION_ID` | Subscription ID is not a valid GUID | "Provide a valid GUID-format subscription ID" | All tools accepting `subscriptionId` |
| `INVALID_FRAMEWORK` | Framework value is not recognized | "Use one of: NIST80053, FedRAMPHigh, FedRAMPModerate, DoDIL5" | `compliance_assess`, `configuration_manage` |
| `INVALID_BASELINE` | Baseline value is not recognized | "Use one of: High, Moderate, Low" | `configuration_manage` |

---

## Tool-Specific Behaviors

### Evidence Deduplication (`compliance_collect_evidence`)

When evidence already exists for a control:
- **Default**: **Append** new evidence alongside existing. Each collection creates new
  evidence records with fresh timestamps.
- The response includes `previousEvidenceCount` to indicate existing evidence.
- To replace old evidence, use `replace: true` parameter (optional, default `false`).

### Document Size Limits (`compliance_generate_document`)

- Generated documents MUST NOT exceed **5MB** of Markdown content.
- If a document would exceed this limit (e.g., SSP for a subscription with 1000+ findings),
  the system MUST truncate the findings section and append a note:
  "Document truncated. Full findings available via 'compliance_assess' tool."
- Documents include a `contentSizeBytes` field in the response metadata.

### Confirmation Protocol (`compliance_remediate`)

When `confirmationRequired: true` is returned:
1. The agent presents the dry-run plan to the user.
2. The user responds with the confirmation phrase (e.g., "apply this remediation").
3. The agent re-invokes `compliance_remediate` with `applyRemediation: true` and the
   same `remediationPlanId`.
4. The `dryRun` and `applyRemediation` parameters are mutually exclusive — if both are
   provided, `dryRun` takes precedence.

### Mutually Exclusive Parameters (`compliance_remediate`)

When both `findingId` and `controlFamily` are provided, the tool MUST return error:
"Parameters 'findingId' and 'controlFamily' are mutually exclusive. Specify one or the other."

### `compliance_status` vs `compliance_monitoring` Differentiation

- `compliance_status`: Returns current-point-in-time compliance posture from the latest
  assessment. Lightweight, reads from DB only.
- `compliance_monitoring` with `action: "status"`: Returns live status by querying Azure
  APIs for real-time data. Heavier, makes Azure API calls.
