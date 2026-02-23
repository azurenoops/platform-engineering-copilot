# MCP Tool Contracts: Configuration Agent

**Branch**: `001-core-compliance` | **Date**: 2026-02-21

The Configuration Agent exposes a single tool with sub-actions for managing
ATO Copilot settings. Settings are stored in `IAgentStateManager` shared state
and consumed by the Compliance Agent.

---

## Tool: `configuration_manage`

Manage ATO Copilot configuration settings.

### Parameters

```json
{
  "name": "configuration_manage",
  "description": "Manage ATO Copilot settings: subscription, framework, baseline, environment, and preferences",
  "inputSchema": {
    "type": "object",
    "properties": {
      "action": {
        "type": "string",
        "enum": [
          "get_configuration",
          "set_subscription",
          "set_framework",
          "set_baseline",
          "set_preference"
        ],
        "description": "Configuration action to perform."
      },
      "subscriptionId": {
        "type": "string",
        "description": "Azure subscription ID (for set_subscription action)."
      },
      "framework": {
        "type": "string",
        "enum": ["NIST80053", "FedRAMPHigh", "FedRAMPModerate", "DoDIL5"],
        "description": "Compliance framework (for set_framework action)."
      },
      "baseline": {
        "type": "string",
        "enum": ["High", "Moderate", "Low"],
        "description": "Baseline level (for set_baseline action)."
      },
      "preferenceName": {
        "type": "string",
        "enum": ["dryRunDefault", "defaultScanType", "cloudEnvironment", "region"],
        "description": "Preference name (for set_preference action)."
      },
      "preferenceValue": {
        "type": "string",
        "description": "Preference value (for set_preference action)."
      }
    },
    "required": ["action"]
  }
}
```

### Response: `get_configuration`

```json
{
  "status": "success",
  "data": {
    "subscriptionId": "abc-123-def-456",
    "framework": "NIST80053",
    "baseline": "High",
    "cloudEnvironment": "AzureGovernment",
    "dryRunDefault": true,
    "defaultScanType": "combined",
    "region": "usgovvirginia",
    "lastUpdated": "2026-02-21T10:00:00Z"
  },
  "metadata": {
    "toolName": "configuration_manage",
    "executionTimeMs": 5,
    "timestamp": "2026-02-21T10:00:01Z"
  }
}
```

### Response: `set_subscription`

```json
{
  "status": "success",
  "data": {
    "message": "Default subscription set to abc-123-def-456",
    "subscriptionId": "abc-123-def-456",
    "previousValue": null
  },
  "metadata": { ... }
}
```

### Response: `set_framework`

```json
{
  "status": "success",
  "data": {
    "message": "Default framework set to FedRAMP High",
    "framework": "FedRAMPHigh",
    "previousValue": "NIST80053"
  },
  "metadata": { ... }
}
```

### Response: `set_preference`

```json
{
  "status": "success",
  "data": {
    "message": "Cloud environment set to AzureGovernment",
    "preferenceName": "cloudEnvironment",
    "preferenceValue": "AzureGovernment",
    "previousValue": "AzureCloud"
  },
  "metadata": { ... }
}
```

### Error Responses

```json
{
  "status": "error",
  "data": null,
  "error": {
    "message": "Invalid subscription ID format",
    "errorCode": "INVALID_SUBSCRIPTION_ID",
    "suggestion": "Provide a valid GUID-format subscription ID"
  },
  "metadata": { ... }
}
```

### Configuration Error Codes

| Code | Meaning | Suggestion |
|------|---------|------------|
| `INVALID_SUBSCRIPTION_ID` | Subscription ID is not a valid GUID | "Provide a valid GUID-format subscription ID (e.g., 'xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx')" |
| `INVALID_FRAMEWORK` | Framework value not recognized | "Use one of: NIST80053, FedRAMPHigh, FedRAMPModerate, DoDIL5" |
| `INVALID_BASELINE` | Baseline value not recognized | "Use one of: High, Moderate, Low" |
| `INVALID_PREFERENCE_NAME` | Preference name not recognized | "Valid preferences: dryRunDefault, defaultScanType, cloudEnvironment, region" |
| `INVALID_PREFERENCE_VALUE` | Preference value not valid for the given name | See valid values table below |
| `MISSING_REQUIRED_PARAM` | Required parameter for this action is missing | "The '[action]' action requires '[param]' parameter" |

### Valid `set_preference` Values

| `preferenceName` | Valid `preferenceValue` Options | Default |
|-----------------|-------------------------------|---------|
| `dryRunDefault` | `"true"`, `"false"` | `"true"` |
| `defaultScanType` | `"resource"`, `"policy"`, `"combined"` | `"combined"` |
| `cloudEnvironment` | `"AzureGovernment"`, `"AzureCloud"` | `"AzureGovernment"` |
| `region` | Any valid Azure region string (e.g., `"usgovvirginia"`, `"usgovarizona"`) | `"usgovvirginia"` |

All preference values are **case-insensitive** on input, normalized to canonical form.

---

## Agent Routing

The MCP server routes requests to Configuration Agent or Compliance Agent based on intent:

| Intent Pattern | Routes To |
|---------------|-----------|
| "set subscription", "configure subscription" | Configuration Agent |
| "set framework", "set baseline" | Configuration Agent |
| "switch to [government\|commercial]" | Configuration Agent |
| "what's my configuration", "show settings" | Configuration Agent |
| "run assessment", "scan", "check compliance" | Compliance Agent |
| "remediate", "fix" | Compliance Agent |
| "collect evidence", "gather evidence" | Compliance Agent |
| "generate SSP/SAR/POA&M" | Compliance Agent |
| "show history", "am I compliant" | Compliance Agent |
| "show audit log" | Compliance Agent |

---

## Shared State Keys

Configuration Agent writes to `IAgentStateManager` with key prefix `config:`:

| Key | Type | Description |
|-----|------|-------------|
| `config:settings` | `ConfigurationSettings` | Complete settings object |
| `config:subscriptionId` | `string` | Quick-access subscription ID |
| `config:framework` | `string` | Quick-access framework |
| `config:baseline` | `string` | Quick-access baseline |

Compliance Agent reads these keys to resolve defaults when parameters are omitted.
