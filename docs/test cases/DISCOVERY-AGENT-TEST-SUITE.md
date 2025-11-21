# Discovery Agent Test Suite

**Last Updated:** November 13, 2025  
**Agent:** Discovery  
**Plugin Functions:** 17 total  
**Purpose:** Comprehensive testing of all Discovery Agent capabilities

## 📋 Overview

The Discovery Agent handles Azure resource inventory, health monitoring, dependency analysis, and resource discovery with Azure MCP integration for best practices and diagnostics.

**Key Capabilities:**
- **Resource Discovery**: Find and list Azure resources with filtering
- **Inventory Management**: Comprehensive resource tracking and categorization
- **Health Monitoring**: Resource health status and history
- **Dependency Analysis**: Resource relationship mapping
- **Tag Management**: Tag-based search and compliance
- **Best Practices**: Azure MCP integration for recommendations
- **Documentation Search**: Azure docs integration

## 🎯 Quick Test Commands

```bash
# Test resource discovery
curl -X POST http://localhost:5100/mcp/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "What resources do I have in subscription 00000000-0000-0000-0000-000000000000?"}' | jq .
```

## 🧪 Test Cases by Function Category

### 1️⃣ Resource Discovery Functions (5 functions)

#### Test DISC-1.1: Discover All Resources
```
What resources are running in subscription 00000000-0000-0000-0000-000000000000?
```
**Expected Function:** `discover_azure_resources`  
**Expected Output:**
- ✅ Total resource count
- ✅ Breakdown by type (VMs, Storage, Databases, etc.)
- ✅ Breakdown by location
- ✅ Breakdown by resource group
- ✅ Resource list (top 50)
- ✅ Next steps suggestions

**Validation:**
- ✅ Agent: Discovery ONLY
- ✅ Intent: "discovery"
- ⏱️ Time: 15-30 seconds
- ✅ Accurate resource inventory

---

#### Test DISC-1.2: Filter by Resource Type
```
Find all AKS clusters in subscription 00000000-0000-0000-0000-000000000000
```
**Expected Function:** `discover_azure_resources` (with resourceType filter)  
**Expected Output:**
- ✅ Only AKS clusters listed
- ✅ Cluster names and IDs
- ✅ Node counts
- ✅ Kubernetes versions
- ✅ Locations

**Validation:**
- ✅ Only requested resource type
- ✅ No other resources shown

---

#### Test DISC-1.3: Filter by Location
```
Show me all resources in eastus region for subscription 00000000-0000-0000-0000-000000000000
```
**Expected Function:** `filter_resources_by_location`  
**Expected Output:**
- ✅ Resources filtered to eastus
- ✅ Resource breakdown by type
- ✅ Count and summary
- ✅ Cost implications (if available)

**Validation:**
- ✅ Location filter applied correctly
- ✅ Only eastus resources shown

---

#### Test DISC-1.4: Filter by Resource Group
```
List all resources in resource group rg-prod-eastus
```
**Expected Function:** `discover_azure_resources` (with resourceGroup filter)  
**Expected Output:**
- ✅ Resources in specified RG only
- ✅ Resource types in group
- ✅ Tag compliance
- ✅ Health status summary

**Validation:**
- ✅ Scoped to single resource group
- ✅ Complete resource list

---

#### Test DISC-1.5: Get Resource Details
```
Show me details for resource /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-prod-eastus/providers/Microsoft.ContainerService/managedClusters/aks-prod-001
```
**Expected Function:** `get_resource_details`  
**Expected Output:**
- ✅ Resource properties
- ✅ Configuration details
- ✅ Tags
- ✅ Health status
- ✅ Dependencies (if applicable)

**Validation:**
- ✅ Comprehensive resource information
- ✅ Current configuration shown

---

### 2️⃣ Tag Management Functions (1 function)

#### Test DISC-2.1: Search by Tag
```
Find all resources with tag Environment=Production
```
**Expected Function:** `search_resources_by_tag`  
**Expected Output:**
- ✅ Resources matching tag criteria
- ✅ Tag values shown
- ✅ Resource breakdown
- ✅ Missing tags identified

**Validation:**
- ✅ Tag filter accurate
- ✅ Only matching resources returned

---

### 3️⃣ Resource Group Functions (2 functions)

#### Test DISC-3.1: List Resource Groups
```
List all resource groups in subscription 00000000-0000-0000-0000-000000000000
```
**Expected Function:** `list_resource_groups`  
**Expected Output:**
- ✅ All resource groups listed
- ✅ Resource count per group
- ✅ Locations
- ✅ Tags (if applicable)

**Validation:**
- ✅ Complete resource group list
- ✅ Accurate counts

---

#### Test DISC-3.2: Get Resource Group Summary
```
Show me a summary of resource group rg-prod-eastus
```
**Expected Function:** `get_resource_group_summary`  
**Expected Output:**
- ✅ Resource count
- ✅ Resource types breakdown
- ✅ Total cost (if available)
- ✅ Tag compliance
- ✅ Health status

**Validation:**
- ✅ Comprehensive summary
- ✅ Actionable insights

---

### 4️⃣ Subscription Functions (1 function)

#### Test DISC-4.1: List Subscriptions
```
What Azure subscriptions do I have access to?
```
**Expected Function:** `list_subscriptions`  
**Expected Output:**
- ✅ All accessible subscriptions
- ✅ Subscription names and IDs
- ✅ Subscription states (Active, Disabled)
- ✅ Tenant associations

**Validation:**
- ✅ All subscriptions shown
- ✅ Current user's access reflected

---

### 5️⃣ Health Monitoring Functions (3 functions)

#### Test DISC-5.1: Get Resource Health
```
What's the health status of resource /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-prod-eastus/providers/Microsoft.ContainerService/managedClusters/aks-prod-001?
```
**Expected Function:** `get_resource_health_status`  
**Expected Output:**
- ✅ Current health state (Available, Degraded, Unavailable, Unknown)
- ✅ Health history
- ✅ Root cause (if issue exists)
- ✅ Recommended actions

**Validation:**
- ✅ Accurate health status
- ✅ Azure Health integration

---

#### Test DISC-5.2: Subscription Health Overview
```
Give me a health overview of my entire subscription
```
**Expected Function:** `get_subscription_health_overview`  
**Expected Output:**
- ✅ Overall health score
- ✅ Healthy resource count
- ✅ Degraded resource count
- ✅ Unhealthy resource count
- ✅ Critical issues flagged

**Validation:**
- ✅ Subscription-wide health summary
- ✅ Issues prioritized

---

#### Test DISC-5.3: Resource Health History
```
Show me the health history for my AKS cluster over the last 7 days
```
**Expected Function:** `get_resource_health_history`  
**Expected Output:**
- ✅ Historical health events
- ✅ Downtime incidents
- ✅ Degradation periods
- ✅ Recovery times
- ✅ Trend analysis

**Validation:**
- ✅ 7-day time range
- ✅ Complete health timeline

---

### 6️⃣ Dependency Analysis (1 function)

#### Test DISC-6.1: Analyze Dependencies
```
What resources depend on resource /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/rg-prod-eastus/providers/Microsoft.KeyVault/vaults/kv-prod-001?
```
**Expected Function:** `analyze_resource_dependencies`  
**Expected Output:**
- ✅ Dependent resources listed
- ✅ Dependency types (uses, consumed by)
- ✅ Dependency graph (visual or text)
- ✅ Impact analysis (if deleted)

**Validation:**
- ✅ Accurate dependency mapping
- ✅ Both inbound and outbound dependencies

---

### 7️⃣ Inventory & Reporting (1 function)

#### Test DISC-7.1: Get Inventory Summary
```
Give me a complete inventory summary for subscription 00000000-0000-0000-0000-000000000000
```
**Expected Function:** `get_resource_inventory_summary`  
**Expected Output:**
- ✅ Total resource count
- ✅ Resource distribution by type
- ✅ Resource distribution by location
- ✅ Tag compliance percentage
- ✅ Untagged resources flagged
- ✅ Cost summary (if available)
- ✅ Health summary

**Validation:**
- ✅ Comprehensive inventory report
- ✅ Suitable for stakeholder review
- ⏱️ Time: 20-40 seconds

---

### 8️⃣ Azure MCP Integration Functions (3 functions)

#### Test DISC-8.1: Discover with Guidance
```
Discover resources with best practices guidance
```
**Expected Function:** `discover_resources_with_guidance`  
**Expected Output:**
- ✅ Resource discovery results
- ✅ Azure MCP best practices per resource
- ✅ Configuration recommendations
- ✅ Security improvements

**Validation:**
- ✅ MCP integration working
- ✅ Best practices included

---

#### Test DISC-8.2: Get Resource with Diagnostics
```
Get resource details with diagnostics for my AKS cluster
```
**Expected Function:** `get_resource_with_diagnostics`  
**Expected Output:**
- ✅ Resource details
- ✅ Diagnostic logs
- ✅ Azure MCP diagnostic insights
- ✅ Troubleshooting guidance

**Validation:**
- ✅ Diagnostics accurate
- ✅ MCP insights valuable

---

#### Test DISC-8.3: Get Resource Best Practices
```
What are the best practices for my storage account?
```
**Expected Function:** `get_resource_best_practices`  
**Expected Output:**
- ✅ Resource-specific best practices
- ✅ Azure MCP recommendations
- ✅ Security hardening steps
- ✅ Cost optimization tips

**Validation:**
- ✅ Relevant best practices
- ✅ Actionable recommendations

---

### 9️⃣ Documentation Search (2 functions)

#### Test DISC-9.1: Search Azure Documentation
```
Search Azure docs for AKS private cluster networking
```
**Expected Function:** `search_azure_documentation`  
**Expected Output:**
- ✅ Relevant documentation excerpts
- ✅ Configuration examples
- ✅ Links to official docs
- ✅ Step-by-step guides

**Validation:**
- ✅ Accurate search results
- ✅ Azure-specific content

---

#### Test DISC-9.2: Generate Bicep for Resource
```
Generate Bicep code for my existing storage account
```
**Expected Function:** `generate_bicep_for_resource`  
**Expected Output:**
- ✅ Bicep template matching resource configuration
- ✅ All properties included
- ✅ Ready for redeployment
- ✅ Comments explaining configuration

**Validation:**
- ✅ Accurate Bicep generation
- ✅ Deployable template

---

## 🔄 Multi-Turn Conversation Tests

### Test DISC-10.1: Discovery → Details Workflow
```
Turn 1: "What resources do I have?"
Turn 2: (Agent shows resource list)
Turn 3: "Show me details for the AKS cluster"
Turn 4: (Agent shows AKS details)
Turn 5: "What depends on this cluster?"
```
**Expected Behavior:**
- Progressive discovery workflow
- Context maintained (resource IDs)
- Logical drill-down

**Validation:**
- ✅ Context preserved
- ✅ No redundant questions

---

## 🎯 Edge Cases & Error Handling

### Test DISC-11.1: Empty Subscription
```
Discover resources in a subscription with no resources
```
**Expected:** Graceful message, setup guidance

---

### Test DISC-11.2: Invalid Resource ID
```
Get details for resource /subscriptions/invalid/...
```
**Expected:** Error message, ask for valid resource ID

---

## 🚨 Anti-Patterns (Should NOT Work This Way)

### ❌ Test DISC-12.1: Provisioning Request to Discovery
```
Deploy an AKS cluster
```
**Expected:** Routes to **Infrastructure Agent**, NOT Discovery

---

## 📊 Validation Checklist

- [ ] `agentType: "Discovery"` in plan
- [ ] Intent: "discovery"
- [ ] Accurate resource data from Azure
- [ ] Health status integration working
- [ ] Tag filtering accurate
- ⏱️ Discovery: 15-30 seconds
- ⏱️ Inventory: 20-40 seconds

---

## 📖 Related Documentation

- **Azure Resource Graph:** [Azure Resource Graph Docs](https://learn.microsoft.com/en-us/azure/governance/resource-graph/)
- **Azure Resource Health:** [Azure Resource Health Docs](https://learn.microsoft.com/en-us/azure/service-health/resource-health-overview)

---

**Last Updated:** November 13, 2025  
**Test Coverage:** 17 functions, 25+ test cases  
**Status:** Ready for comprehensive testing
