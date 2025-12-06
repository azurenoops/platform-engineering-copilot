# Conditional Deployment and Existing Resource Integration Guide

## Overview

The Platform Engineering Copilot infrastructure now supports:

1. **Conditional App Service Deployment** - Choose which services to deploy (Admin API, Chat, MCP Server)
2. **Existing Resource Integration** - Use existing Azure infrastructure (Virtual Network, Log Analytics Workspace, Key Vault)
3. **Unified Terraform and Bicep Support** - Feature parity across both IaC tools

## Table of Contents

- [App Service Deployment Options](#app-service-deployment-options)
- [Using Existing Resources](#using-existing-resources)
- [Bicep Deployment](#bicep-deployment)
- [Terraform Deployment](#terraform-deployment)
- [GitHub Actions Workflow](#github-actions-workflow)
- [Deployment Scenarios](#deployment-scenarios)
- [Migration Guide](#migration-guide)

---

## App Service Deployment Options

### Service Deployment Control

**MCP Server**: Always deployed (core requirement)
**Admin API**: Optional (controlled by parameter)
**Chat Service**: Optional (controlled by parameter)

### Bicep Parameters

```bicep
@description('Deploy Admin API service')
param deployAdminApi bool = true

@description('Deploy Chat service')
param deployChat bool = true
```

### Terraform Variables

```hcl
variable "deploy_admin_api" {
  description = "Deploy Admin API app service"
  type        = bool
  default     = true
}

variable "deploy_chat" {
  description = "Deploy Chat app service"
  type        = bool
  default     = true
}
```

### Deployment Matrix

| Scenario | deploy_admin_api | deploy_chat | Services Deployed |
|----------|------------------|-------------|-------------------|
| Full Deployment | true | true | MCP + Admin API + Chat |
| MCP + Admin Only | true | false | MCP + Admin API |
| MCP + Chat Only | false | true | MCP + Chat |
| MCP Only | false | false | MCP Server only |

---

## Using Existing Resources

### Supported Existing Resources

1. **Virtual Network** - Reuse existing VNet and subnets
2. **Log Analytics Workspace** - Connect to existing monitoring infrastructure
3. **Key Vault** - Use existing Key Vault for secrets management

### Benefits of Using Existing Resources

- **Cost Savings** - Avoid duplicate infrastructure costs
- **Compliance** - Leverage pre-approved, compliant resources
- **Integration** - Connect to existing monitoring and networking
- **Shared Resources** - Multiple applications can share common infrastructure

---

## Bicep Deployment

### Existing Virtual Network

```bicep
// Enable existing network mode
param useExistingNetwork bool = true
param existingVnetName string = 'my-existing-vnet'
param existingVnetResourceGroup string = 'my-network-rg'
param existingAppServiceSubnetName string = 'app-subnet'
param existingPrivateEndpointSubnetName string = 'private-endpoint-subnet'
```

**Prerequisites:**
- Virtual Network must exist in Azure
- Subnets must be configured with appropriate address spaces
- Subnet delegation may be required for App Service integration

### Existing Log Analytics Workspace

```bicep
// Enable existing Log Analytics mode
param useExistingLogAnalytics bool = true
param existingLogAnalyticsWorkspaceName string = 'my-existing-workspace'
param existingLogAnalyticsResourceGroup string = 'my-monitoring-rg'
```

**Prerequisites:**
- Workspace must exist in same Azure subscription
- Appropriate permissions for writing logs
- Workspace must be in same or compatible region

### Existing Key Vault

```bicep
// Enable existing Key Vault mode
param useExistingKeyVault bool = true
param existingKeyVaultName string = 'my-existing-keyvault'
param existingKeyVaultResourceGroup string = 'my-security-rg'
```

**Prerequisites:**
- Key Vault must exist with appropriate access policies
- Managed identities must have permissions to read secrets
- Key Vault must allow network access from deployment location

### Complete Bicep Example

```bash
az deployment sub create \
  --name "platform-engineering-dev" \
  --location eastus \
  --template-file infra/bicep/main.bicep \
  --parameters environment=dev \
  --parameters deployAdminApi=true \
  --parameters deployChat=false \
  --parameters useExistingNetwork=true \
  --parameters existingVnetName=shared-vnet \
  --parameters existingVnetResourceGroup=network-rg \
  --parameters existingAppServiceSubnetName=apps-subnet \
  --parameters existingPrivateEndpointSubnetName=pe-subnet \
  --parameters useExistingLogAnalytics=true \
  --parameters existingLogAnalyticsWorkspaceName=shared-workspace \
  --parameters existingLogAnalyticsResourceGroup=monitoring-rg
```

---

## Terraform Deployment

### Existing Virtual Network

```hcl
# terraform.tfvars
use_existing_network               = true
existing_vnet_name                 = "my-existing-vnet"
existing_vnet_resource_group       = "my-network-rg"
existing_app_service_subnet_name   = "app-subnet"
existing_private_endpoint_subnet_name = "private-endpoint-subnet"
```

### Existing Log Analytics Workspace

```hcl
# terraform.tfvars
use_existing_log_analytics              = true
existing_log_analytics_workspace_name   = "my-existing-workspace"
existing_log_analytics_resource_group   = "my-monitoring-rg"
```

### Existing Key Vault

```hcl
# terraform.tfvars
use_existing_keyvault            = true
existing_keyvault_name           = "my-existing-keyvault"
existing_keyvault_resource_group = "my-security-rg"
```

### Complete Terraform Example

```bash
terraform apply \
  -var="environment=dev" \
  -var="deploy_admin_api=true" \
  -var="deploy_chat=false" \
  -var="use_existing_network=true" \
  -var="existing_vnet_name=shared-vnet" \
  -var="existing_vnet_resource_group=network-rg" \
  -var="existing_app_service_subnet_name=apps-subnet" \
  -var="existing_private_endpoint_subnet_name=pe-subnet" \
  -var="use_existing_log_analytics=true" \
  -var="existing_log_analytics_workspace_name=shared-workspace" \
  -var="existing_log_analytics_resource_group=monitoring-rg"
```

---

## GitHub Actions Workflow

### Workflow Inputs

The infrastructure deployment workflow now supports all new parameters:

```yaml
workflow_dispatch:
  inputs:
    deployAdminApi:
      description: 'Deploy Admin API service'
      default: true
      type: boolean
    deployChat:
      description: 'Deploy Chat service'
      default: true
      type: boolean
    useExistingNetwork:
      description: 'Use existing Virtual Network'
      default: false
      type: boolean
    existingVnetName:
      description: 'Existing VNet name'
      type: string
    useExistingLogAnalytics:
      description: 'Use existing Log Analytics Workspace'
      default: false
      type: boolean
    existingLogAnalyticsWorkspaceName:
      description: 'Existing Log Analytics Workspace name'
      type: string
    useExistingKeyVault:
      description: 'Use existing Key Vault'
      default: false
      type: boolean
    existingKeyVaultName:
      description: 'Existing Key Vault name'
      type: string
```

### Running the Workflow

1. Navigate to Actions tab in GitHub
2. Select "Infrastructure Deployment" workflow
3. Click "Run workflow"
4. Configure deployment options:
   - Select environment (dev/staging/prod)
   - Choose deployment target (appservice/aks/aci)
   - Enable/disable Admin API and Chat deployment
   - Optionally specify existing resources
5. Click "Run workflow" to start deployment

---

## Deployment Scenarios

### Scenario 1: Full Deployment with New Resources

**Use Case**: Greenfield deployment, creating all infrastructure from scratch

**Bicep**:
```bash
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters environment=dev \
  --parameters deployAdminApi=true \
  --parameters deployChat=true
```

**Terraform**:
```bash
terraform apply -var="environment=dev"
```

**Result**: Creates VNet, Log Analytics, Key Vault, SQL, Storage, and all App Services

---

### Scenario 2: MCP Only with Existing Network

**Use Case**: Deploying only MCP server into existing infrastructure

**Bicep**:
```bash
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters environment=dev \
  --parameters deployAdminApi=false \
  --parameters deployChat=false \
  --parameters useExistingNetwork=true \
  --parameters existingVnetName=corporate-vnet \
  --parameters existingAppServiceSubnetName=apps \
  --parameters existingPrivateEndpointSubnetName=private
```

**Terraform**:
```bash
terraform apply \
  -var="deploy_admin_api=false" \
  -var="deploy_chat=false" \
  -var="use_existing_network=true" \
  -var="existing_vnet_name=corporate-vnet"
```

**Result**: Deploys only MCP server, uses existing network, creates new Log Analytics and Key Vault

---

### Scenario 3: Shared Monitoring Infrastructure

**Use Case**: Multiple applications sharing central monitoring workspace

**Bicep**:
```bash
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters environment=dev \
  --parameters useExistingLogAnalytics=true \
  --parameters existingLogAnalyticsWorkspaceName=central-workspace \
  --parameters existingLogAnalyticsResourceGroup=monitoring-rg
```

**Terraform**:
```bash
terraform apply \
  -var="use_existing_log_analytics=true" \
  -var="existing_log_analytics_workspace_name=central-workspace" \
  -var="existing_log_analytics_resource_group=monitoring-rg"
```

**Result**: All services write logs/metrics to existing shared workspace

---

### Scenario 4: Enterprise Compliance Setup

**Use Case**: IL5/IL6 deployment using pre-approved, compliant resources

**Bicep**:
```bash
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters environment=prod \
  --parameters deployAdminApi=true \
  --parameters deployChat=true \
  --parameters useExistingNetwork=true \
  --parameters existingVnetName=il5-compliant-vnet \
  --parameters existingVnetResourceGroup=il5-network-rg \
  --parameters useExistingLogAnalytics=true \
  --parameters existingLogAnalyticsWorkspaceName=il5-workspace \
  --parameters useExistingKeyVault=true \
  --parameters existingKeyVaultName=il5-keyvault
```

**Terraform**:
```bash
terraform apply \
  -var="environment=prod" \
  -var="use_existing_network=true" \
  -var="existing_vnet_name=il5-compliant-vnet" \
  -var="use_existing_log_analytics=true" \
  -var="existing_log_analytics_workspace_name=il5-workspace" \
  -var="use_existing_keyvault=true" \
  -var="existing_keyvault_name=il5-keyvault"
```

**Result**: Uses all existing compliant infrastructure, only creates App Services, SQL, and Storage

---

## Migration Guide

### Migrating from Always-On to Conditional Deployment

#### Step 1: Assess Current Deployment

```bash
# Check currently deployed services
az webapp list --resource-group <rg-name> --output table
```

#### Step 2: Update Parameters

**If keeping all services (default):**
- No changes needed, deploy_admin_api and deploy_chat default to `true`

**If removing Chat service:**
```bash
# Bicep
--parameters deployChat=false

# Terraform
-var="deploy_chat=false"
```

#### Step 3: Plan Changes

```bash
# Bicep - What-If analysis
az deployment sub what-if \
  --template-file infra/bicep/main.bicep \
  --parameters deployChat=false

# Terraform - Plan
terraform plan -var="deploy_chat=false"
```

#### Step 4: Apply Changes

```bash
# Bicep
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters deployChat=false

# Terraform
terraform apply -var="deploy_chat=false"
```

#### Step 5: Verify Deployment

```bash
# Check deployment summary
az deployment sub show \
  --name <deployment-name> \
  --query "properties.outputs.deploymentSummary.value"
```

### Migrating to Existing Resources

#### Step 1: Identify Existing Resources

```bash
# List Virtual Networks
az network vnet list --output table

# List Log Analytics Workspaces
az monitor log-analytics workspace list --output table

# List Key Vaults
az keyvault list --output table
```

#### Step 2: Note Resource Details

Required information:
- Resource name
- Resource group name
- Subnet names (for VNet)
- Region/location

#### Step 3: Update Deployment Parameters

Create a parameters file or update terraform.tfvars:

**Bicep parameters.json:**
```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "useExistingNetwork": {
      "value": true
    },
    "existingVnetName": {
      "value": "my-vnet"
    },
    "existingVnetResourceGroup": {
      "value": "network-rg"
    },
    "existingAppServiceSubnetName": {
      "value": "apps-subnet"
    },
    "existingPrivateEndpointSubnetName": {
      "value": "pe-subnet"
    }
  }
}
```

**Terraform terraform.tfvars:**
```hcl
use_existing_network               = true
existing_vnet_name                 = "my-vnet"
existing_vnet_resource_group       = "network-rg"
existing_app_service_subnet_name   = "apps-subnet"
existing_private_endpoint_subnet_name = "pe-subnet"
```

#### Step 4: Validate Configuration

```bash
# Bicep
az deployment sub validate \
  --template-file infra/bicep/main.bicep \
  --parameters @parameters.json

# Terraform
terraform validate
terraform plan
```

#### Step 5: Deploy

```bash
# Bicep
az deployment sub create \
  --template-file infra/bicep/main.bicep \
  --parameters @parameters.json

# Terraform
terraform apply
```

---

## Best Practices

### Resource Naming

- Use consistent naming conventions for existing resources
- Document resource relationships and dependencies
- Tag existing resources appropriately for governance

### Security

- Ensure existing resources meet IL5/IL6 compliance requirements
- Validate network security group rules on existing subnets
- Verify Key Vault access policies include managed identities
- Review private endpoint configurations

### Monitoring

- Verify existing Log Analytics workspace has sufficient capacity
- Check retention policies align with compliance requirements
- Enable diagnostic settings on all resources

### Cost Optimization

- Use existing resources to reduce duplication costs
- Deploy only needed services (disable Admin API or Chat if not required)
- Choose appropriate SKUs based on workload requirements

### Change Management

- Test conditional deployments in dev environment first
- Use What-If/Plan commands before production deployments
- Maintain separate parameter files per environment
- Document all existing resource dependencies

---

## Troubleshooting

### Common Issues

#### Issue: Subnet Not Delegated
**Error**: "Subnet must be delegated to Microsoft.Web/serverFarms"

**Solution**:
```bash
az network vnet subnet update \
  --resource-group <rg-name> \
  --vnet-name <vnet-name> \
  --name <subnet-name> \
  --delegations Microsoft.Web/serverFarms
```

#### Issue: Key Vault Access Denied
**Error**: "Access denied to Key Vault"

**Solution**:
```bash
# Add access policy for managed identity
az keyvault set-policy \
  --name <keyvault-name> \
  --object-id <managed-identity-principal-id> \
  --secret-permissions get list
```

#### Issue: Log Analytics Workspace Region Mismatch
**Error**: "Workspace must be in same region"

**Solution**: Use a workspace in the same region as deployment or create new workspace

#### Issue: Network Security Rules Blocking Traffic
**Error**: "Unable to connect to service"

**Solution**: Review NSG rules on existing subnets, ensure appropriate ports are allowed

---

## Reference

### Bicep Parameters Reference

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| deployAdminApi | bool | true | Deploy Admin API service |
| deployChat | bool | true | Deploy Chat service |
| useExistingNetwork | bool | false | Use existing VNet |
| existingVnetName | string | '' | Name of existing VNet |
| existingVnetResourceGroup | string | current RG | Resource group of existing VNet |
| existingAppServiceSubnetName | string | '' | App Service subnet name |
| existingPrivateEndpointSubnetName | string | '' | Private endpoint subnet name |
| useExistingLogAnalytics | bool | false | Use existing workspace |
| existingLogAnalyticsWorkspaceName | string | '' | Workspace name |
| existingLogAnalyticsResourceGroup | string | current RG | Workspace resource group |
| useExistingKeyVault | bool | false | Use existing Key Vault |
| existingKeyVaultName | string | '' | Key Vault name |
| existingKeyVaultResourceGroup | string | current RG | Key Vault resource group |

### Terraform Variables Reference

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| deploy_admin_api | bool | true | Deploy Admin API service |
| deploy_chat | bool | true | Deploy Chat service |
| use_existing_network | bool | false | Use existing VNet |
| existing_vnet_name | string | "" | Name of existing VNet |
| existing_vnet_resource_group | string | "" | Resource group of existing VNet |
| existing_app_service_subnet_name | string | "" | App Service subnet name |
| existing_private_endpoint_subnet_name | string | "" | Private endpoint subnet name |
| use_existing_log_analytics | bool | false | Use existing workspace |
| existing_log_analytics_workspace_name | string | "" | Workspace name |
| existing_log_analytics_resource_group | string | "" | Workspace resource group |
| use_existing_keyvault | bool | false | Use existing Key Vault |
| existing_keyvault_name | string | "" | Key Vault name |
| existing_keyvault_resource_group | string | "" | Key Vault resource group |

---

## Support

For issues or questions:
1. Check the troubleshooting section above
2. Review deployment logs in Azure Portal
3. Verify existing resource configurations
4. Consult platform engineering team

---

## Change Log

### v0.9.0 - Conditional Deployment & Existing Resources
- Added conditional deployment for Admin API and Chat services
- Implemented existing resource integration (VNet, Log Analytics, Key Vault)
- Updated Bicep and Terraform templates for feature parity
- Enhanced GitHub Actions workflow with new parameters
- Created comprehensive documentation

---

**Document Version**: 1.0  
**Last Updated**: 2024  
**Maintained By**: Platform Engineering Team
