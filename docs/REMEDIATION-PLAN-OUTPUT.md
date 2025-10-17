# Remediation Plan Output Format

## Overview
The remediation plan output has been enhanced to clearly show what automated actions will be performed vs. what requires manual intervention.

## Output Structure

### Summary Section
```json
{
  "summary": {
    "totalFindings": 124,
    "autoRemediable": 3,
    "manualRequired": 121,
    "estimatedEffort": "PT133H",
    "priority": "Medium",
    "riskReduction": 100.0
  }
}
```

### Remediation Items

Each remediation item now includes these key fields for clear communication:

#### For Auto-Remediable Findings

```json
{
  "findingId": "abc-123",
  "resourceId": "/subscriptions/.../storageAccounts/mystorage",
  "priority": "P0 - Immediate",
  "automationAvailable": true,
  
  // Clear indicator of automation
  "actionSummary": "✨ AUTO-REMEDIATION: Will automatically execute 2 step(s) when you run remediation",
  
  // Numbered list of exact automated actions
  "automatedActions": [
    {
      "step": 1,
      "action": "Enable Storage Encryption using Azure Storage Service Encryption",
      "actionType": "Configuration Change"
    },
    {
      "step": 2,
      "action": "Verify automated remediation",
      "actionType": "System Update"
    }
  ],
  
  // One-line summary for user-friendly display
  "summary": "✨ Enable Storage Encryption → Verify automated remediation"
}
```

#### For Manual Remediation Findings

```json
{
  "findingId": "def-456",
  "resourceId": "/subscriptions/.../virtualMachines/web-01",
  "priority": "P1 - Within 24 hours",
  "automationAvailable": false,
  
  // Clear indicator of manual work required
  "actionSummary": "🔧 MANUAL REMEDIATION: Requires 3 manual step(s)",
  
  // Detailed steps with commands
  "manualSteps": [
    {
      "step": 1,
      "description": "Review current VM configuration",
      "command": "az vm show --name web-01 --resource-group rg-prod",
      "script": null
    },
    {
      "step": 2,
      "description": "Enable Azure Disk Encryption",
      "command": "az vm encryption enable --name web-01 --disk-encryption-keyvault vault-01",
      "script": "Enable-DiskEncryption.ps1"
    },
    {
      "step": 3,
      "description": "Verify encryption status",
      "command": "az vm encryption show --name web-01",
      "script": null
    }
  ],
  
  // One-line summary
  "summary": "🔧 Review configuration → Enable encryption → Verify status"
}
```

## How to Display Remediation Plan

### For Auto-Remediable Findings

**Show users:**
1. `actionSummary` - "✨ AUTO-REMEDIATION: Will automatically execute X step(s)"
2. `automatedActions` - Numbered list of actions that will be performed
3. `summary` - One-line description
4. `priority` and `effort` for planning

**Example Output:**
```
🛠️ Auto-Remediable Finding: fc9144c5-7bf0-41be-84b7-214626ad2022

Resource: /subscriptions/453c2549.../storageAccounts/mystorage
Priority: P0 - Immediate
Effort: 10 minutes

✨ AUTO-REMEDIATION: Will automatically execute 2 step(s) when you run remediation

Automated Actions:
  1. Enable Storage Encryption using Azure Storage Service Encryption (Configuration Change)
  2. Verify automated remediation (System Update)

Summary: Enable Storage Encryption → Verify automated remediation

✅ Ready to auto-fix when you execute the remediation plan
```

### For Manual Findings

**Show users:**
1. `actionSummary` - "🔧 MANUAL REMEDIATION: Requires X manual step(s)"
2. `manualSteps` - Numbered steps with commands and scripts
3. `summary` - One-line description
4. `priority` and `effort` for planning

**Example Output:**
```
🔧 Manual Finding: def-456-789

Resource: /subscriptions/453c2549.../virtualMachines/web-01
Priority: P1 - Within 24 hours
Effort: 2 hours

🔧 MANUAL REMEDIATION: Requires 3 manual step(s)

Manual Steps:
  1. Review current VM configuration
     Command: az vm show --name web-01 --resource-group rg-prod
     
  2. Enable Azure Disk Encryption
     Command: az vm encryption enable --name web-01 --disk-encryption-keyvault vault-01
     Script: Enable-DiskEncryption.ps1
     
  3. Verify encryption status
     Command: az vm encryption show --name web-01

Summary: Review configuration → Enable encryption → Verify status

⚠️ Requires manual execution - follow the commands above
```

## Next Steps in Output

The `nextSteps` array provides guidance on what to do next:

```json
{
  "nextSteps": [
    "✨ AUTO-REMEDIATION: 3 finding(s) can be automatically fixed. For each auto-remediable finding, display the 'actionSummary' and 'automatedActions' fields to show exactly what will be done.",
    "🔧 MANUAL REMEDIATION: 121 finding(s) require manual action. For these findings, display the 'manualSteps' field to see step-by-step instructions with commands.",
    "⚡ Execute Plan: Say 'execute the remediation plan' to automatically fix all auto-remediable findings.",
    "📊 Track Progress: Say 'show me the remediation progress' to track execution status and completion percentage.",
    "📋 For user-friendly display: Show the 'summary' field for each remediation item - it provides a clear one-line description of what will happen."
  ]
}
```

## Field Reference

| Field | Type | Description | When to Display |
|-------|------|-------------|-----------------|
| `actionSummary` | string | High-level description of auto/manual status | Always - first thing users should see |
| `automatedActions` | array | Numbered list of automated actions | Only for auto-remediable findings |
| `manualSteps` | array | Numbered manual steps with commands | Only for manual findings |
| `summary` | string | One-line user-friendly description | Always - great for quick overview |
| `steps` | array | Legacy format | For backward compatibility |
| `validationSteps` | array | Post-remediation validation | Optional - for detailed planning |
| `dependencies` | array | Other findings that must be fixed first | When present - for sequencing |

## Implementation Notes

### Auto-Remediation Execution
When user says "execute the remediation plan":
- System reads `automatedActions` (or internal `steps` with commands)
- Executes each action automatically
- Shows progress: "Executing step 1/2: Enable Storage Encryption..."
- Validates results
- Reports success/failure

### Manual Remediation Guidance
For manual findings:
- Display `manualSteps` with clear step numbers
- Show commands that can be copy-pasted
- Indicate which steps have automation scripts available
- Provide validation guidance after execution

## Example: Complete Remediation Plan Display

```markdown
🛠️ REMEDIATION PLAN SUMMARY
Subscription: prod-subscription-01

Total Findings: 124
Auto-Remediable: 3
Manual Required: 121
Estimated Effort: 133 hours
Priority: Medium
Risk Reduction: 100%

---

🔧 AUTO-REMEDIABLE FINDINGS (3)

Finding 1: fc9144c5-7bf0-41be-84b7-214626ad2022
Resource: /subscriptions/.../storageAccounts/mystorage
Priority: P0 - Immediate | Effort: 10 min

✨ Will automatically execute 2 step(s):
  1. Enable Storage Encryption (Configuration Change)
  2. Verify automated remediation (System Update)

Finding 2: 7a52816e-4756-456f-b6d3-4109a5bde1cd
Resource: /subscriptions/.../workspaces/mcp-logs
Priority: P3 - Within 30 days | Effort: 5 min

✨ Will automatically execute 2 step(s):
  1. Enable Diagnostic Settings (Configuration Change)
  2. Verify automated remediation (System Update)

Finding 3: abc-def-ghi-jkl
Resource: /subscriptions/.../keyVaults/vault-01
Priority: P0 - Immediate | Effort: 5 min

✨ Will automatically execute 2 step(s):
  1. Enable Soft Delete protection (Configuration Change)
  2. Verify automated remediation (System Update)

---

🔧 MANUAL REMEDIATION REQUIRED (121)

Note: Showing first 10 findings. See full report for complete list.

Finding 1: c0d3af9c-24b0-4f28-bad8-4230f548ce76
Resource: /subscriptions/.../virtualMachines/web-01
Priority: P0 - Immediate | Effort: 2 hours

🔧 Requires 3 manual step(s):
  1. Review VM configuration
  2. Apply security hardening
  3. Validate changes

[... additional manual findings ...]

---

📅 TIMELINE
Start: 2025-10-14
End: 2025-10-20
Duration: 6 days

---

📈 NEXT STEPS

✨ Auto-Remediation: Execute the plan to automatically fix 3 findings
🔧 Manual Work: Follow step-by-step instructions for 121 findings
⚡ Execute: Say "execute the remediation plan" to start auto-fixes
📊 Track: Say "show remediation progress" for status updates
```

## Troubleshooting

### "Review and apply manual remediation" appears for auto-remediable findings

**Cause**: The finding's `RemediationActions` list is empty or null.

**Solution**: 
1. Ensure findings are enriched with `.WithAutoRemediationInfo()` before generating plan
2. Check that `FindingAutoRemediationService.GetRemediationActions()` returns actions for the finding type
3. Verify finding title/resource type matches patterns in `FindingAutoRemediationService`

### No automated actions shown

**Cause**: Output is showing legacy `steps` field instead of new `automatedActions` field.

**Solution**:
- Display `automatedActions` field for auto-remediable findings (where `automationAvailable: true`)
- Display `manualSteps` field for manual findings (where `automationAvailable: false`)
- Use `summary` field for quick one-line description
