# Discovery Agent - Azure Arc Capabilities

The Discovery Agent provides comprehensive Azure Arc-enabled server discovery and inventory management for hybrid infrastructure.

## Overview

Azure Arc extends Azure management capabilities to on-premises, multi-cloud, and edge infrastructure. The Discovery Agent's Arc functions enable you to:

- **Inventory Arc-enabled servers** across all subscriptions
- **Analyze connection health** and identify connectivity issues
- **Track installed extensions** for monitoring, security, and management
- **Assess hybrid infrastructure readiness** for cloud migration

## Available Functions

### 1. `list_arc_machines`

Discovers all Azure Arc-enabled servers across your subscriptions.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | No | Filter to specific subscription |

**Example Prompts:**
```
"List all Arc-enabled servers"
"Show me Arc machines in subscription abc-123"
"What hybrid servers are connected to Azure?"
```

**Response includes:**
- Machine name and resource ID
- OS type and version
- Agent version and status
- Location and resource group
- Connection status (Connected/Disconnected)
- Last status change timestamp

---

### 2. `get_arc_machine_details`

Retrieves detailed information about a specific Arc-enabled server.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `resourceId` | string | Yes | Full Azure resource ID |

**Example Prompts:**
```
"Show details for Arc server hybrid-db-01"
"What's the status of my Arc machine /subscriptions/.../machines/prod-app-01"
```

**Response includes:**
- Full machine properties
- Hardware details (processor count, memory)
- Network information (private/public IPs, FQDN)
- Agent configuration
- Extension count
- Connected status with last status change

---

### 3. `get_arc_extensions`

Lists all extensions installed on Arc-enabled servers.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | No | Filter to specific subscription |
| `machineResourceId` | string | No | Filter to specific machine |

**Example Prompts:**
```
"What extensions are installed on Arc servers?"
"Show monitoring extensions on hybrid-db-01"
"List all MMA extensions across Arc machines"
```

**Response includes:**
- Extension name and type
- Publisher information
- Provisioning state
- Version information
- Machine association

---

### 4. `get_arc_connection_health`

Analyzes connection health across Arc-enabled servers.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | No | Filter to specific subscription |
| `includeDetails` | bool | No | Include detailed machine info (default: false) |

**Example Prompts:**
```
"Check Arc connection health"
"Which Arc servers are disconnected?"
"Show me Arc machines with connectivity issues"
```

**Response includes:**
- Total machine count
- Connected vs disconnected counts
- Connection health percentage
- Machines organized by status
- Last seen timestamps for disconnected machines
- Days since last connection

## Common Use Cases

### Hybrid Infrastructure Inventory
```
User: "Give me an inventory of all our hybrid servers"

Discovery Agent will:
1. Query all Arc-enabled machines
2. Group by OS type and location
3. Report agent versions and connection status
4. Identify any disconnected machines
```

### Connectivity Troubleshooting
```
User: "Why can't I manage my on-prem servers from Azure?"

Discovery Agent will:
1. Check Arc agent connection status
2. Identify disconnected machines
3. Report last known connection time
4. Recommend troubleshooting steps
```

### Extension Audit
```
User: "Which Arc servers have Log Analytics agent installed?"

Discovery Agent will:
1. Query extensions across Arc machines
2. Filter for MicrosoftMonitoringAgent
3. Report version and provisioning status
4. Identify machines without the extension
```

## Integration with Other Agents

The Discovery Agent's Arc findings feed into:

| Agent | Integration |
|-------|-------------|
| **Compliance** | Discovered machines → compliance scanning targets |
| **Infrastructure** | Health data → onboarding script generation |
| **Security** | Extension inventory → security posture assessment |

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Discovery Agent                          │
│                                                             │
│  ┌─────────────────┐  ┌─────────────────┐                  │
│  │ list_arc_       │  │ get_arc_machine │                  │
│  │ machines        │  │ _details        │                  │
│  └────────┬────────┘  └────────┬────────┘                  │
│           │                    │                            │
│  ┌────────┴────────┐  ┌────────┴────────┐                  │
│  │ get_arc_        │  │ get_arc_        │                  │
│  │ extensions      │  │ connection_     │                  │
│  │                 │  │ health          │                  │
│  └────────┬────────┘  └────────┬────────┘                  │
│           │                    │                            │
│           └────────────┬───────┘                            │
│                        ▼                                    │
│            ┌───────────────────────┐                        │
│            │  Azure Resource Graph  │                        │
│            │  microsoft.hybridcompute│                       │
│            │  /machines             │                        │
│            └───────────────────────┘                        │
└─────────────────────────────────────────────────────────────┘
```

## Related Documentation

- [Azure Arc Overview](../AZURE-ARC.md) - Main Arc documentation
- [Agent Architecture](../AGENTS.md) - Full agent overview
- [Infrastructure Agent Arc](../Infrastructure%20Agent/AZURE-ARC.md) - Onboarding scripts
- [Compliance Agent Arc](../Compliance%20Agent/AZURE-ARC.md) - Compliance scanning
