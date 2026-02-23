# M365 Copilot Extension — Platform Engineering Copilot

This directory contains the scaffold for the Microsoft 365 Copilot Extension that integrates Platform Copilot with Teams and M365 Copilot.

## Structure

```
M365/
├── README.md                # This file
├── manifest.json            # Teams bot / M365 Copilot manifest
├── PlatformBot.cs           # Teams bot implementation
└── AdaptiveCards/            # Adaptive Card templates
    ├── ComplianceCard.json  # Assessment result card
    └── CostCard.json        # Cost analysis card
```

## Features (FR-065)

- **Teams Bot**: Interact with Platform Copilot directly in Microsoft Teams
- **M365 Copilot Plugin**: Extend M365 Copilot with compliance and infrastructure capabilities
- **Adaptive Cards**: Rich interactive cards for assessment results, cost analysis, and remediation boards
- **Proactive Notifications**: Alert on compliance drift, budget overruns, and deployment status changes

## Integration

The extension communicates with the Platform Copilot MCP server via HTTP SSE transport:
```
/platform assess FedRAMP High
/platform cost analysis
/platform explain AC-2
```

## Status

**Scaffold only** — Full implementation requires Microsoft 365 Copilot Extension SDK.
