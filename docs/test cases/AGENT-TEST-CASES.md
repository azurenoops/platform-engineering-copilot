# Platform Engineering Copilot - Agent Test Cases

This document provides natural language test queries for each specialized agent. Use these in GitHub Copilot Chat with `@platform` or directly through the MCP server.

> **Prerequisite**: Most agents require a configured Azure subscription. Start by setting your subscription using the Configuration Agent.

---

## Table of Contents
1. [Configuration Agent](#1-configuration-agent)
2. [Discovery Agent](#2-discovery-agent)
3. [Infrastructure Agent](#3-infrastructure-agent)
4. [Security Agent](#4-security-agent)
5. [Cost Management Agent](#5-cost-management-agent)
6. [KnowledgeBase Agent](#6-knowledgebase-agent)
7. [Environment Agent](#7-environment-agent)

---

## 1. Configuration Agent

**Purpose**: Manage Platform Engineering Copilot settings, especially Azure subscription configuration.

**Routing Keywords**: "set subscription", "configure", "my subscription", "default subscription", "settings"

### Test Case 1.1: Set Default Subscription
| Field | Value |
|-------|-------|
| **Query** | `Set my subscription to 453c2549-4cc5-464f-ba66-acad920823e8` |
| **Command** | `@platform /config Set my subscription to 453c2549-4cc5-464f-ba66-acad920823e8` |
| **Tool Called** | `configure_subscription` |
| **Parameters** | `action: "set"`, `subscriptionId: "453c2549-4cc5-464f-ba66-acad920823e8"` |
| **Expected Result** | ✅ Confirmation message with subscription ID, persisted to `~/.platform-copilot/config.json` |

### Test Case 1.2: Get Current Configuration
| Field | Value |
|-------|-------|
| **Query** | `What is my current subscription?` |
| **Command** | `@platform /config Show my current settings` |
| **Tool Called** | `configure_subscription` |
| **Parameters** | `action: "get"` |
| **Expected Result** | Current subscription ID and configuration details |

### Test Case 1.3: Clear Configuration
| Field | Value |
|-------|-------|
| **Query** | `Clear my subscription settings` |
| **Command** | `@platform /config Clear my default subscription` |
| **Tool Called** | `configure_subscription` |
| **Parameters** | `action: "clear"` |
| **Expected Result** | Confirmation that settings were cleared |

### Test Case 1.4: Invalid Subscription Format
| Field | Value |
|-------|-------|
| **Query** | `Set my subscription to not-a-guid` |
| **Tool Called** | `configure_subscription` |
| **Parameters** | `action: "set"`, `subscriptionId: "not-a-guid"` |
| **Expected Result** | ❌ Error message: "Invalid subscription ID format" |

---

## 2. Discovery Agent

**Purpose**: Discover and inventory Azure resources across subscriptions.

**Routing Keywords**: "list resources", "find", "discover", "show me", "what resources", "subscriptions", "inventory"

### Test Case 2.1: List Subscriptions
| Field | Value |
|-------|-------|
| **Query** | `List my subscriptions` |
| **Alternative Queries** | `List my Azure subscriptions`, `Show subscriptions`, `What subscriptions do I have?` |
| **Command** | `@platform /discover List my Azure subscriptions` |
| **Tool Called** | `list_subscriptions` |
| **Parameters** | None required |
| **Expected Result** | List of subscriptions with IDs, names, states, tenant IDs |
| **Routing Keywords** | "list subscriptions", "my subscriptions", "show subscriptions", "available subscriptions" |

### Test Case 2.2: Discover All Resources
| Field | Value |
|-------|-------|
| **Query** | `Show me all resources in my subscription` |
| **Command** | `@platform /discover What resources are in my subscription?` |
| **Tool Called** | `discover_azure_resources` |
| **Parameters** | `subscriptionId: "<configured-id>"` |
| **Expected Result** | List of resources with names, types, locations, resource groups |

### Test Case 2.3: Filter by Resource Group
| Field | Value |
|-------|-------|
| **Query** | `List resources in the rg-production resource group` |
| **Tool Called** | `discover_azure_resources` |
| **Parameters** | `subscriptionId: "<id>"`, `resourceGroup: "rg-production"` |
| **Expected Result** | Filtered list of resources in that resource group |

### Test Case 2.4: Filter by Resource Type
| Field | Value |
|-------|-------|
| **Query** | `Find all storage accounts in my subscription` |
| **Tool Called** | `discover_azure_resources` |
| **Parameters** | `subscriptionId: "<id>"`, `resourceType: "Microsoft.Storage/storageAccounts"` |
| **Expected Result** | List of storage accounts only |

### Test Case 2.5: Filter by Location
| Field | Value |
|-------|-------|
| **Query** | `What resources are in usgovvirginia?` |
| **Tool Called** | `discover_azure_resources` |
| **Parameters** | `subscriptionId: "<id>"`, `location: "usgovvirginia"` |
| **Expected Result** | Resources filtered by Azure Government Virginia region |

### Test Case 2.6: Filter by Tag
| Field | Value |
|-------|-------|
| **Query** | `Find resources tagged with environment=production` |
| **Tool Called** | `discover_azure_resources` |
| **Parameters** | `subscriptionId: "<id>"`, `tagFilter: "environment=production"` |
| **Expected Result** | Resources with matching tag |

### Test Case 2.7: Missing Subscription Error
| Field | Value |
|-------|-------|
| **Query** | `List resources` (without configured subscription) |
| **Tool Called** | `discover_azure_resources` |
| **Expected Result** | ❌ Error: "Subscription ID is required" |

---

## 3. Infrastructure Agent

**Purpose**: Generate IaC templates and provision Azure resources.

**Routing Keywords**: "create", "deploy", "provision", "generate template", "bicep", "terraform", "infrastructure", "AKS", "VM", "storage"

### Test Case 3.1: Generate Bicep Template
| Field | Value |
|-------|-------|
| **Query** | `Generate a Bicep template for a storage account` |
| **Command** | `@platform /infrastructure Create a Bicep template for a secure storage account` |
| **Tool Called** | `generate_infrastructure_template` |
| **Parameters** | `resource_type: "storage"`, `format: "bicep"` |
| **Expected Result** | Bicep template with HTTPS-only, encryption, security best practices |

### Test Case 3.2: Generate Terraform Template
| Field | Value |
|-------|-------|
| **Query** | `Create a Terraform template for an AKS cluster in eastus` |
| **Tool Called** | `generate_infrastructure_template` |
| **Parameters** | `resource_type: "aks"`, `format: "terraform"`, `location: "eastus"` |
| **Expected Result** | Terraform HCL template for AKS with networking |

### Test Case 3.3: Security-Enhanced Template
| Field | Value |
|-------|-------|
| **Query** | `Generate a secure Key Vault template` |
| **Tool Called** | `generate_infrastructure_template` |
| **Parameters** | `resource_type: "keyvault"`, `enable_security: true` |
| **Expected Result** | Key Vault template with purge protection, RBAC, private endpoints |

### Test Case 3.4: Provision Storage Account
| Field | Value |
|-------|-------|
| **Query** | `Create a storage account named mystorageacct in eastus` |
| **Command** | `@platform /infrastructure Deploy a storage account named mystorageacct` |
| **Tool Called** | `provision_infrastructure` |
| **Parameters** | `query: "Create storage account..."`, `resource_type: "storage-account"`, `resource_name: "mystorageacct"`, `location: "eastus"` |
| **Expected Result** | Resource provisioned with ARM deployment ID, resource ID |

### Test Case 3.5: Cost Estimation Only
| Field | Value |
|-------|-------|
| **Query** | `Estimate the cost of deploying a D4s_v3 VM` |
| **Tool Called** | `provision_infrastructure` |
| **Parameters** | `resource_type: "vm"`, `estimate_cost: true` |
| **Expected Result** | Monthly cost estimate without actually provisioning |

### Test Case 3.6: Multiple Resource Types
| Field | Value |
|-------|-------|
| **Query** | `Generate Bicep for a VNet with subnets and an NSG` |
| **Tool Called** | `generate_infrastructure_template` |
| **Parameters** | `resource_type: "vnet"`, `include_networking: true` |
| **Expected Result** | VNet template with subnet definitions and associated NSG |

### Test Case 3.7: Environment-Specific Template
| Field | Value |
|-------|-------|
| **Query** | `Create a production Redis cache template` |
| **Tool Called** | `generate_infrastructure_template` |
| **Parameters** | `resource_type: "redis"`, `environment: "prod"` |
| **Expected Result** | Redis template with production SKU, zone redundancy, TLS 1.2 |

---

## 4. Security Agent

**Purpose**: Security posture assessment, vulnerability scanning, and Defender for Cloud integration.

**Routing Keywords**: "security", "secure", "defender", "vulnerability", "threat", "posture", "score"

### Test Case 4.1: Get Security Posture
| Field | Value |
|-------|-------|
| **Query** | `What's my security posture?` |
| **Command** | `@platform /security Show my security posture` |
| **Tool Called** | `get_security_posture` |
| **Parameters** | `subscription_id: "<configured-id>"` |
| **Expected Result** | Security score, recommendations count, resource health summary |

### Test Case 4.2: Run Vulnerability Scan
| Field | Value |
|-------|-------|
| **Query** | `Run a vulnerability scan on my subscription` |
| **Tool Called** | `run_vulnerability_scan` |
| **Parameters** | `subscription_id: "<configured-id>"` |
| **Expected Result** | Vulnerability findings by severity with remediation guidance |

### Test Case 4.3: Get Security Recommendations
| Field | Value |
|-------|-------|
| **Query** | `What security improvements should I make?` |
| **Tool Called** | `get_security_recommendations` |
| **Parameters** | `subscription_id: "<configured-id>"` |
| **Expected Result** | Prioritized security recommendations from Defender for Cloud |

### Test Case 4.4: Get Threat Alerts
| Field | Value |
|-------|-------|
| **Query** | `Are there any active security threats?` |
| **Tool Called** | `get_threat_alerts` |
| **Parameters** | `subscription_id: "<configured-id>"` |
| **Expected Result** | Active threat alerts with severity and affected resources |

### Test Case 4.5: Get Policy Status
| Field | Value |
|-------|-------|
| **Query** | `Show Azure Policy status for my subscription` |
| **Tool Called** | `get_policy_compliance` |
| **Parameters** | `subscription_id: "<configured-id>"` |
| **Expected Result** | Policy assignment status, compliant/non-compliant resource counts |

### Test Case 4.6: Missing Subscription Error
| Field | Value |
|-------|-------|
| **Query** | `Check security posture` (without subscription) |
| **Expected Result** | ❌ Error: "Subscription ID is required. Use 'Set my subscription to <id>' first" |

---

## 5. Cost Management Agent

**Purpose**: Azure cost analysis, optimization, budgeting, and forecasting.

**Routing Keywords**: "cost", "spend", "budget", "savings", "optimize", "expensive", "billing", "forecast"

### Test Case 5.1: Analyze Costs (30 days)
| Field | Value |
|-------|-------|
| **Query** | `Show me my Azure costs for the last 30 days` |
| **Command** | `@platform /cost What did I spend last month?` |
| **Tool Called** | `analyze_azure_costs` |
| **Parameters** | `subscriptionId: "<id>"`, `lookbackDays: 30` |
| **Expected Result** | Cost dashboard with spend, trends, service breakdown, budget alerts |

### Test Case 5.2: Analyze Costs (7 days)
| Field | Value |
|-------|-------|
| **Query** | `What are my Azure costs this week?` |
| **Tool Called** | `analyze_azure_costs` |
| **Parameters** | `subscriptionId: "<id>"`, `lookbackDays: 7` |
| **Expected Result** | 7-day cost summary |

### Test Case 5.3: Group by Resource Group
| Field | Value |
|-------|-------|
| **Query** | `Show costs grouped by resource group` |
| **Tool Called** | `analyze_azure_costs` |
| **Parameters** | `subscriptionId: "<id>"`, `groupBy: "resource-group"` |
| **Expected Result** | Cost breakdown by resource group |

### Test Case 5.4: Group by Tag
| Field | Value |
|-------|-------|
| **Query** | `Show costs by cost-center tag` |
| **Tool Called** | `analyze_azure_costs` |
| **Parameters** | `subscriptionId: "<id>"`, `groupBy: "tag"`, `tagKey: "cost-center"` |
| **Expected Result** | Costs grouped by cost-center tag values |

### Test Case 5.5: Get Optimization Recommendations
| Field | Value |
|-------|-------|
| **Query** | `How can I reduce my Azure spending?` |
| **Command** | `@platform /cost What are my cost optimization opportunities?` |
| **Tool Called** | `get_optimization_recommendations` |
| **Parameters** | `subscriptionId: "<id>"` |
| **Expected Result** | Prioritized recommendations with estimated savings |

### Test Case 5.6: Filter Recommendations by Category
| Field | Value |
|-------|-------|
| **Query** | `Show rightsizing recommendations only` |
| **Tool Called** | `get_optimization_recommendations` |
| **Parameters** | `subscriptionId: "<id>"`, `category: "rightsizing"` |
| **Expected Result** | VM/resource sizing recommendations only |

### Test Case 5.7: Minimum Savings Threshold
| Field | Value |
|-------|-------|
| **Query** | `Show optimization opportunities over $500/month` |
| **Tool Called** | `get_optimization_recommendations` |
| **Parameters** | `subscriptionId: "<id>"`, `minimumSavings: 500` |
| **Expected Result** | Only recommendations with >$500 monthly savings |

### Test Case 5.8: Reserved Instance Recommendations
| Field | Value |
|-------|-------|
| **Query** | `Should I buy reserved instances?` |
| **Tool Called** | `get_optimization_recommendations` |
| **Parameters** | `subscriptionId: "<id>"`, `category: "reserved-instances"` |
| **Expected Result** | RI purchase recommendations with break-even analysis |

### Test Case 5.9: Missing Subscription Error
| Field | Value |
|-------|-------|
| **Query** | `What are my costs?` (without subscription) |
| **Expected Result** | ❌ Error: "Subscription ID is required" |

---

## 6. KnowledgeBase Agent

**Purpose**: Platform knowledge and documentation assistance. Shell agent — no tools currently registered. Reserved for future MCP integration.

**Routing Keywords**: "knowledge", "documentation", "platform", "help", "guide"

### Test Case 6.1: Platform Help
| Field | Value |
|-------|-------|
| **Query** | `What can the platform engineering copilot do?` |
| **Command** | `@platform /knowledge Help me understand the platform` |
| **Expected Result** | Overview of platform capabilities and available agents |

### Test Case 6.2: Agent Documentation
| Field | Value |
|-------|-------|
| **Query** | `What agents are available?` |
| **Expected Result** | List of 7 agents with their capabilities |

### Test Case 6.3: Architecture Overview
| Field | Value |
|-------|-------|
| **Query** | `How does the agent architecture work?` |
| **Expected Result** | Explanation of BaseAgent/BaseTool pattern and routing |

---

## 7. Environment Agent

**Purpose**: Platform Engineering template management, environment lifecycle, drift detection and remediation.

**Routing Keywords**: "environment", "template", "clone", "scale", "drift", "service catalog", "provision environment"

### Test Case 7.1: List Service Templates
| Field | Value |
|-------|-------|
| **Query** | `Show available service templates` |
| **Alternative Queries** | `List templates`, `What templates are available?`, `Show me the service catalog` |
| **Command** | `@platform /environment List service templates` |
| **Tool Called** | `list_service_templates` |
| **Parameters** | None required |
| **Expected Result** | List of available templates with names, categories, descriptions |
| **Prerequisite** | Database must be seeded with templates (run migrations or seed data) |

### Test Case 7.2: Get Template Details
| Field | Value |
|-------|-------|
| **Query** | `Show me details for the aks-standard template` |
| **Alternative Queries** | `Get details for AKS template`, `What parameters does the AKS template need?` |
| **Tool Called** | `get_template_details` |
| **Parameters** | `templateId: "aks-standard"` (or partial match like "AKS") |
| **Expected Result** | Template parameters, guardrails, version |
| **Notes** | The tool supports partial matching - "AKS" will find "aks-standard". First run `list_service_templates` to see available template names. |

### Test Case 7.3: Find Matching Template
| Field | Value |
|-------|-------|
| **Query** | `I need an environment for a containerized web app` |
| **Tool Called** | `find_matching_template` |
| **Parameters** | `requirements: "containerized web app"` |
| **Expected Result** | Recommended templates (AKS, Container Apps) with suitability scores |

### Test Case 7.3: Find Matching Template
| Field | Value |
|-------|-------|
| **Query** | `I need an environment for a secure landing zone` |
| **Tool Called** | `find_matching_template` |
| **Parameters** | `requirements: "secure landing zone"` |
| **Expected Result** | Recommended templates (Secure Landing Zone) with suitability scores |

### Test Case 7.4: Create Environment from Template
| Field | Value |
|-------|-------|
| **Query** | `Create a production environment from the AKS template` |
| **Tool Called** | `create_environment_from_template` |
| **Parameters** | `templateId: "<id>"`, `environmentName: "production"`, `size: "medium"` |
| **Expected Result** | Environment creation initiated, deployment ID returned |

### Test Case 7.5: List Provisioned Environments
| Field | Value |
|-------|-------|
| **Query** | `Show my provisioned environments` |
| **Tool Called** | `list_provisioned_environments` |
| **Parameters** | None required |
| **Expected Result** | List of environments with status, template, resource group |

### Test Case 7.6: Clone Environment
| Field | Value |
|-------|-------|
| **Query** | `Clone my dev environment to staging` |
| **Tool Called** | `clone_provisioned_environment` |
| **Parameters** | `sourceEnvironmentId: "<dev-id>"`, `newName: "staging"` |
| **Expected Result** | New environment created from dev configuration |

### Test Case 7.7: Scale Environment
| Field | Value |
|-------|-------|
| **Query** | `Scale my test environment to large` |
| **Tool Called** | `scale_provisioned_environment` |
| **Parameters** | `environmentId: "<id>"`, `size: "large"` |
| **Expected Result** | Environment scaling initiated, updated resource details |

### Test Case 7.8: Detect Drift
| Field | Value |
|-------|-------|
| **Query** | `Check for configuration drift in production` |
| **Tool Called** | `detect_environment_drift` |
| **Parameters** | `environmentId: "<production-id>"` |
| **Expected Result** | Drift report showing differences from template |

### Test Case 7.9: Remediate Drift
| Field | Value |
|-------|-------|
| **Query** | `Fix drift in my production environment` |
| **Tool Called** | `remediate_environment_drift` |
| **Parameters** | `environmentId: "<id>"` |
| **Expected Result** | Remediation applied, resources returned to template state |

### Test Case 7.10: Delete Environment
| Field | Value |
|-------|-------|
| **Query** | `Delete my test environment` |
| **Tool Called** | `delete_provisioned_environment` |
| **Parameters** | `environmentId: "<test-id>"` |
| **Expected Result** | Environment deletion confirmation, resource cleanup |

---

## Multi-Agent Workflows

These test cases involve the orchestrator routing to multiple agents.

### Test Case M.1: End-to-End Deployment
| Field | Value |
|-------|-------|
| **Query** | `Create a secure storage account, scan it, and show costs` |
| **Agents Involved** | Infrastructure → Security → Cost Management |
| **Expected Flow** | 1. Generate template, 2. Provision, 3. Scan security, 4. Show cost |

### Test Case M.2: Discovery + Security
| Field | Value |
|-------|-------|
| **Query** | `Find all storage accounts and check their security posture` |
| **Agents Involved** | Discovery → Security |
| **Expected Flow** | 1. List storage accounts, 2. Assess security posture |

### Test Case M.3: Knowledge + Security
| Field | Value |
|-------|-------|
| **Query** | `Help me understand the security agent and then check my posture` |
| **Agents Involved** | KnowledgeBase → Security |
| **Expected Flow** | 1. Explain security capabilities, 2. Run security assessment |

---

## Error Handling Test Cases

### Test Case E.1: No Subscription Configured
| Field | Value |
|-------|-------|
| **Query** | `Scan my subscription` (no subscription set) |
| **Expected Result** | Helpful error prompting to set subscription first |

### Test Case E.2: Invalid Resource Type
| Field | Value |
|-------|-------|
| **Query** | `Generate a template for quantum-computer` |
| **Expected Result** | Error with list of supported resource types |

### Test Case E.3: Network Timeout
| Field | Value |
|-------|-------|
| **Setup** | MCP server not running |
| **Query** | Any query |
| **Expected Result** | Connection error with troubleshooting guidance |

### Test Case E.4: Authentication Failure
| Field | Value |
|-------|-------|
| **Setup** | Invalid Azure credentials |
| **Query** | `List my resources` |
| **Expected Result** | Authentication error with login instructions |

---

## Testing Checklist

For each agent, verify:

- [ ] Basic query routing works
- [ ] Slash command routing works (`@platform /agent`)
- [ ] Required parameters are validated
- [ ] Missing subscription prompts for configuration
- [ ] Tool calls return expected JSON structure
- [ ] Error messages are helpful
- [ ] Response includes agent attribution
- [ ] Templates are properly formatted (Bicep/Terraform)
- [ ] Security findings include severity and remediation

---

## Quick Reference: Agent Routing

| User Says | Routes To |
|-----------|-----------|
| "set my subscription" | Configuration Agent |
| "list resources" | Discovery Agent |
| "find storage accounts" | Discovery Agent |
| "create a VM" | Infrastructure Agent |
| "generate bicep" | Infrastructure Agent |
| "scan for compliance" | Security Agent |
| "policy compliance scan" | Security Agent |
| "Azure Policy compliance" | Security Agent |
| "remediate finding" | Security Agent |
| "generate SSP" | Security Agent |
| "generate POA&M" | Security Agent |
| "what are my costs" | Cost Management Agent |
| "optimize spending" | Cost Management Agent |
| "what is the platform" | KnowledgeBase Agent |
| "help me understand" | KnowledgeBase Agent |
| "how does RMF work" | Security Agent |
| "show service templates" | Environment Agent |
| "create environment" | Environment Agent |
| "detect drift" | Environment Agent |
| "clone environment" | Environment Agent |

---

## Troubleshooting

### Environment Agent - No Templates Found

**Problem**: "Show me details for the AKS template" returns empty or error.

**Causes & Solutions**:

1. **Database not seeded**: Run migrations to seed default templates:
   ```bash
   cd src/Platform.Engineering.Copilot.Core
   dotnet ef database update
   ```

2. **First list available templates**:
   ```
   "List available service templates"
   ```
   This shows what templates exist. Then use the exact name:
   ```
   "Show details for aks-standard template"
   ```

3. **Check database connection**: Ensure SQLite file exists or SQL Server is accessible.

### Agent Routing Issues

**Problem**: Query goes to wrong agent or no response.

**Solutions**:
1. Use explicit slash commands: `@platform /environment Show templates`
2. Include routing keywords: "environment", "template", "drift"
3. Check MCP server logs for routing decisions

### Tool Execution Errors

**Problem**: Tool returns error or no data.

**Debug Steps**:
1. Check MCP server logs: `docker logs platform-mcp 2>&1 | tail -50`
2. Verify Azure authentication: `az account show`
3. Check subscription is set: "What is my current subscription?"
