# Infrastructure Agent - Azure Arc Capabilities

The Infrastructure Agent provides Azure Arc onboarding automation and extension deployment for hybrid infrastructure.

## Overview

Azure Arc enables centralized management of hybrid servers. The Infrastructure Agent's Arc functions enable you to:

- **Generate onboarding scripts** for connecting servers to Azure Arc
- **Deploy management extensions** (monitoring, security, automation)
- **Track onboarding progress** across your hybrid fleet
- **Automate hybrid infrastructure provisioning**

## Available Functions

### 1. `generate_arc_onboarding_script`

Generates platform-specific scripts for connecting servers to Azure Arc.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | Yes | Target Azure subscription |
| `resourceGroupName` | string | Yes | Target resource group for Arc resources |
| `location` | string | No | Azure region (default: eastus) |
| `platform` | string | No | Target OS: Windows, Linux (default: Windows) |
| `tags` | string | No | JSON object with tags to apply |
| `servicePrincipalId` | string | No | Service principal for automated onboarding |
| `servicePrincipalSecret` | string | No | Service principal secret |

**Example Prompts:**
```
"Generate Arc onboarding script for Windows servers"
"Create Linux Arc onboarding script for production"
"Generate Arc script with environment tags"
```

**Generated Script includes:**
- Prerequisites check
- Azure Connected Machine Agent installation
- Authentication (interactive or service principal)
- Agent configuration
- Registration with Azure
- Post-onboarding validation

**Windows Script (PowerShell):**
```powershell
# Download and install the Azure Connected Machine agent
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest -Uri https://aka.ms/AzureConnectedMachineAgent -OutFile AzureConnectedMachineAgent.msi
msiexec /i AzureConnectedMachineAgent.msi /l*v installlog.txt /qn

# Connect to Azure Arc
& "$env:ProgramFiles\AzureConnectedMachineAgent\azcmagent.exe" connect `
    --subscription-id "<subscription-id>" `
    --resource-group "<resource-group>" `
    --location "<location>" `
    --tags "Environment=Production"
```

**Linux Script (Bash):**
```bash
# Download and install the agent
wget https://aka.ms/azcmagent -O ~/install_linux_azcmagent.sh
sudo bash ~/install_linux_azcmagent.sh

# Connect to Azure Arc
sudo azcmagent connect \
    --subscription-id "<subscription-id>" \
    --resource-group "<resource-group>" \
    --location "<location>" \
    --tags "Environment=Production"
```

---

### 2. `deploy_arc_extensions`

Deploys management extensions to Arc-enabled servers using ARM templates.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `machineResourceId` | string | Yes | Arc machine resource ID |
| `extensionTypes` | string | Yes | Comma-separated: Monitoring, Security, Automation |

**Extension Types:**
- **Monitoring** - Azure Monitor Agent (AMA)
- **Security** - Microsoft Defender for Servers
- **Automation** - Azure Automation Hybrid Worker

**Example Prompts:**
```
"Deploy monitoring extension to Arc server hybrid-db-01"
"Enable Defender on all Arc machines"
"Install Azure Monitor agent on Arc servers"
```

**Response includes:**
- ARM template for extension deployment
- Per-extension configuration
- Template storage location
- Deployment instructions

---

### 3. `get_arc_onboarding_status`

Tracks onboarding progress and identifies servers not yet connected.

**Parameters:**
| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `subscriptionId` | string | No | Filter to specific subscription |
| `resourceGroupName` | string | No | Filter to specific resource group |

**Example Prompts:**
```
"Show Arc onboarding status"
"Which servers haven't been onboarded to Arc?"
"What's the Arc adoption rate?"
```

**Response includes:**
- Total onboarded count
- Onboarding by status (Connected/Disconnected)
- Agent version distribution
- Last activity timestamps
- Machines pending onboarding (if detectable)

## Generated ARM Templates

### Monitoring Extension

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "resources": [
    {
      "type": "Microsoft.HybridCompute/machines/extensions",
      "apiVersion": "2023-10-03-preview",
      "name": "[concat(parameters('machineName'), '/AzureMonitorAgent')]",
      "location": "[parameters('location')]",
      "properties": {
        "publisher": "Microsoft.Azure.Monitor",
        "type": "AzureMonitorLinuxAgent",
        "autoUpgradeMinorVersion": true
      }
    }
  ]
}
```

### Security Extension (Defender)

```json
{
  "type": "Microsoft.HybridCompute/machines/extensions",
  "name": "[concat(parameters('machineName'), '/MDE.Linux')]",
  "properties": {
    "publisher": "Microsoft.Azure.AzureDefenderForServers",
    "type": "MDE.Linux",
    "autoUpgradeMinorVersion": true
  }
}
```

## Common Use Cases

### Mass Onboarding
```
User: "Generate scripts to onboard 100 Linux servers to Arc"

Infrastructure Agent will:
1. Generate Linux bash script with service principal auth
2. Include prerequisite checks
3. Add error handling and logging
4. Provide batch execution guidance
```

### Extension Standardization
```
User: "Deploy monitoring and security to all Arc servers"

Infrastructure Agent will:
1. Generate ARM templates for each extension type
2. Create deployment script
3. Track deployment status
4. Report any failures
```

### Hybrid Fleet Tracking
```
User: "How many servers are onboarded to Arc?"

Infrastructure Agent will:
1. Query Arc machine inventory
2. Summarize by status and region
3. Calculate onboarding percentage
4. Identify stale/disconnected machines
```

## Integration with Other Agents

| Agent | Integration |
|-------|-------------|
| **Discovery** | Machine inventory → onboarding targets |
| **Compliance** | Extension deployment → compliance prerequisite |
| **Security** | Defender deployment → security posture |

## Service Principal Setup

For automated onboarding without interactive login:

1. **Create Service Principal:**
   ```bash
   az ad sp create-for-rbac --name "Arc-Onboarding-SP" \
       --role "Azure Connected Machine Onboarding" \
       --scopes /subscriptions/<subscription-id>
   ```

2. **Use in Script Generation:**
   ```
   "Generate Arc script with service principal for automated onboarding"
   ```

3. **Store Credentials Securely:**
   - Azure Key Vault recommended
   - Environment variables for automation

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  Infrastructure Agent                       │
│                                                             │
│  ┌─────────────────┐  ┌─────────────────┐                  │
│  │ generate_arc_   │  │ deploy_arc_     │                  │
│  │ onboarding_     │  │ extensions      │                  │
│  │ script          │  │                 │                  │
│  └────────┬────────┘  └────────┬────────┘                  │
│           │                    │                            │
│           │    ┌───────────────┴──────────┐                │
│           │    │ get_arc_onboarding_      │                │
│           │    │ status                   │                │
│           │    └───────────────┬──────────┘                │
│           │                    │                            │
│           ▼                    ▼                            │
│  ┌─────────────────┐  ┌─────────────────────────────┐      │
│  │ Script          │  │ Azure Resource Manager      │      │
│  │ Generation      │  │ ARM Template Deployment     │      │
│  │ (PS1/Bash)      │  │                             │      │
│  └─────────────────┘  └─────────────────────────────┘      │
└─────────────────────────────────────────────────────────────┘
```

## Quick Start

1. **Generate onboarding script:**
   ```
   "Create Arc onboarding script for Linux production servers"
   ```

2. **Deploy extensions:**
   ```
   "Deploy monitoring to Arc server /subscriptions/.../machines/server-01"
   ```

3. **Check status:**
   ```
   "Show Arc onboarding progress"
   ```

## Troubleshooting

### Script Execution Issues

| Issue | Resolution |
|-------|------------|
| Permission denied | Run as Administrator (Windows) or sudo (Linux) |
| Network timeout | Check firewall rules for Arc endpoints |
| Agent install failed | Verify system requirements |

### Extension Deployment Issues

| Issue | Resolution |
|-------|------------|
| Extension stuck | Check agent connectivity |
| Template error | Validate ARM template syntax |
| Permission denied | Verify RBAC assignments |

## Related Documentation

- [Azure Arc Overview](../AZURE-ARC.md) - Main Arc documentation
- [Agent Architecture](../AGENTS.md) - Full agent overview
- [Discovery Agent Arc](../Discovery%20Agent/AZURE-ARC.md) - Machine inventory
- [Compliance Agent Arc](../Compliance%20Agent/AZURE-ARC.md) - Compliance scanning
