# App Service Deployment Options

## Overview

The Platform Engineering Copilot infrastructure now supports conditional deployment of the Admin API and Chat app services, while the MCP server is always deployed.

## Deployment Configuration

### Control Variables

Two new variables control which app services are deployed:

```hcl
# Deploy Admin API (optional - default: true)
variable "deploy_admin_api" {
  description = "Deploy Admin API app service"
  type        = bool
  default     = true
}

# Deploy Chat (optional - default: true)
variable "deploy_chat" {
  description = "Deploy Chat app service"
  type        = bool
  default     = true
}
```

### Always Deployed

- **MCP Server** - Always deployed, cannot be disabled

### Optional Deployments

- **Admin API** - Controlled by `deploy_admin_api` variable
- **Chat** - Controlled by `deploy_chat` variable

## Usage Examples

### Example 1: Deploy All Services (Default)

```hcl
# terraform.tfvars
project_name = "platsup"
environment  = "dev"

# Optional - these are defaults
deploy_admin_api = true
deploy_chat      = true
```

**Result:**
- ✅ MCP Server deployed
- ✅ Admin API deployed
- ✅ Chat deployed

### Example 2: MCP Server Only

```hcl
# terraform.tfvars
project_name = "platsup"
environment  = "dev"

# Disable optional services
deploy_admin_api = false
deploy_chat      = false
```

**Result:**
- ✅ MCP Server deployed
- ❌ Admin API not deployed
- ❌ Chat not deployed

### Example 3: MCP Server + Admin API

```hcl
# terraform.tfvars
project_name = "platsup"
environment  = "dev"

deploy_admin_api = true
deploy_chat      = false
```

**Result:**
- ✅ MCP Server deployed
- ✅ Admin API deployed
- ❌ Chat not deployed

### Example 4: MCP Server + Chat

```hcl
# terraform.tfvars
project_name = "platsup"
environment  = "dev"

deploy_admin_api = false
deploy_chat      = true
```

**Result:**
- ✅ MCP Server deployed
- ❌ Admin API not deployed
- ✅ Chat deployed

## Infrastructure Changes

### App Services Created

Based on your configuration, the following Azure App Services will be created:

1. **App Service Plan** (Always created)
   - Shared by all deployed app services
   - Name: `{project_name}-asp-{environment}`

2. **MCP Server** (Always deployed)
   - Name: `{project_name}-mcp-{environment}`
   - URL: `https://{project_name}-mcp-{environment}.azurewebsites.net`

3. **Admin API** (Conditional)
   - Name: `{project_name}-admin-api-{environment}`
   - URL: `https://{project_name}-admin-api-{environment}.azurewebsites.net`
   - Deployed when: `deploy_admin_api = true`

4. **Chat** (Conditional)
   - Name: `{project_name}-chat-{environment}`
   - URL: `https://{project_name}-chat-{environment}.azurewebsites.net`
   - Deployed when: `deploy_chat = true`

### Related Resources

The following resources are automatically configured based on deployment:

#### SQL Firewall Rules
- MCP Server firewall rules: Always created
- Admin API firewall rules: Created only if `deploy_admin_api = true`
- Chat firewall rules: Created only if `deploy_chat = true`

#### Key Vault Access Policies
- MCP Server managed identity: Always granted access
- Admin API managed identity: Granted access only if `deploy_admin_api = true`
- Chat managed identity: Granted access only if `deploy_chat = true`

#### VNet Integration
- Configured for all deployed app services
- Uses the same app service subnet

#### Diagnostic Settings
- Log Analytics integration configured for each deployed service
- Includes HTTP logs, console logs, app logs, and audit logs

## Terraform Outputs

### Conditional Outputs

The following outputs return `null` if the service is not deployed:

```hcl
# Admin API outputs (null if deploy_admin_api = false)
output "admin_api_url"
output "api_app_service_name"
output "api_app_service_hostname"
output "api_app_service_identity_principal_id"

# Chat outputs (null if deploy_chat = false)
output "chat_url"
output "chat_app_service_name"
output "chat_app_service_hostname"
output "chat_app_service_identity_principal_id"
```

### Always Available Outputs

These outputs are always available:

```hcl
# MCP Server outputs
output "mcp_url"
output "mcp_app_service_name"
output "mcp_app_service_hostname"
output "mcp_app_service_identity_principal_id"

# Deployment summary
output "deployment_summary"
```

### View Outputs

```bash
# View all outputs
terraform output

# View specific output
terraform output mcp_url
terraform output admin_api_url
terraform output chat_url

# View deployment summary
terraform output deployment_summary
```

## Deployment Commands

### Initial Deployment

```bash
cd infra/terraform

# Initialize
terraform init

# Plan with custom configuration
terraform plan -var="deploy_admin_api=true" -var="deploy_chat=false"

# Or use tfvars file
terraform plan -var-file=terraform.dev.tfvars

# Apply
terraform apply -var-file=terraform.dev.tfvars
```

### Modify Existing Deployment

You can add or remove services after initial deployment:

#### Add Chat to Existing Deployment

```bash
# Update terraform.tfvars
# Change: deploy_chat = false
# To:     deploy_chat = true

terraform plan
terraform apply
```

#### Remove Admin API from Existing Deployment

```bash
# Update terraform.tfvars
# Change: deploy_admin_api = true
# To:     deploy_admin_api = false

terraform plan
terraform apply
```

**Note:** Removing a service will delete the app service and all its configuration.

## Cost Implications

### App Service Plan

The App Service Plan cost is the same regardless of how many services are deployed (they share the same plan).

**Example Costs (approximate):**
- B1 (Basic): ~$13/month
- S1 (Standard): ~$70/month
- P1v2 (Premium): ~$146/month

### Additional Services

Each deployed app service adds minimal additional cost:
- Storage for logs and diagnostics
- Network egress traffic
- SQL database firewall rules (no cost)
- Key Vault access policies (no cost)

**Cost Optimization:**
- Deploy only the services you need
- MCP Server is always deployed (required for core functionality)
- Disable Admin API if you don't need administrative features
- Disable Chat if you're using an alternative frontend

## Use Cases

### Development Environment

```hcl
# Dev: All services for testing
deploy_admin_api = true
deploy_chat      = true
```

### Production - MCP Only

```hcl
# Prod: API-only deployment
deploy_admin_api = false
deploy_chat      = false
```

Use this configuration if:
- You're using custom frontends
- MCP server is integrated into another application
- You want minimal deployment footprint

### Production - Full Stack

```hcl
# Prod: Complete deployment
deploy_admin_api = true
deploy_chat      = true
```

Use this configuration if:
- You need the full platform capabilities
- You're using the built-in admin interface
- You're deploying the complete solution

### Staging Environment

```hcl
# Staging: Admin API for testing, no chat
deploy_admin_api = true
deploy_chat      = false
```

Use this configuration if:
- Testing admin features
- Chat is tested separately
- Cost optimization for staging

## Validation

After deployment, verify which services are running:

```bash
# Get all app services in resource group
az webapp list \
  --resource-group "rg-platsup-dev-eastus" \
  --query "[].{Name:name, State:state, URL:defaultHostName}" \
  --output table

# Check specific service
az webapp show \
  --resource-group "rg-platsup-dev-eastus" \
  --name "platsup-mcp-dev" \
  --query "{Name:name, State:state, URL:defaultHostName}"
```

## Troubleshooting

### Service Not Deployed

**Issue:** Expected service is not deployed

**Solution:**
1. Check variable value in terraform.tfvars
2. Run `terraform plan` to see what will be created
3. Verify no errors in plan output

### Cannot Access Service URL

**Issue:** Output shows `null` for service URL

**Reason:** Service is not deployed (variable set to `false`)

**Solution:** Set deployment variable to `true` and apply

### Cost Higher Than Expected

**Issue:** Costs are higher even with services disabled

**Reason:** App Service Plan cost is fixed regardless of deployed services

**Solution:** 
- App Service Plan is shared and cost is the same
- Consider downgrading SKU if using fewer services
- Only SQL firewall rules and diagnostics are saved

## Best Practices

1. **Use Separate Environments**
   - Dev: All services enabled for testing
   - Staging: Match production configuration
   - Prod: Only required services

2. **Document Your Configuration**
   - Add comments in terraform.tfvars explaining why services are enabled/disabled
   - Keep track of changes in version control

3. **Test Before Disabling**
   - Ensure dependent applications don't require the service
   - Test integrations after disabling services

4. **Monitor After Changes**
   - Check application logs after enabling/disabling services
   - Verify dependent services still function

5. **Backup Before Major Changes**
   - Export Terraform state
   - Document current configuration
   - Have rollback plan ready

## Migration Guide

### From Always-On to Conditional

If you have an existing deployment where all services are deployed:

1. **Add Variables to Configuration**
   ```hcl
   # Add to terraform.tfvars
   deploy_admin_api = true  # Keep existing service
   deploy_chat      = true  # Keep existing service
   ```

2. **Run Terraform Plan**
   ```bash
   terraform plan
   ```
   
   Should show no changes if all variables are `true`.

3. **Disable Unwanted Services**
   ```hcl
   # Update terraform.tfvars
   deploy_admin_api = false  # Disable Admin API
   deploy_chat      = false  # Disable Chat
   ```

4. **Apply Changes**
   ```bash
   terraform plan  # Review changes
   terraform apply # Apply if changes look correct
   ```

## Support

For issues or questions:
- Check [GETTING-STARTED.md](./GETTING-STARTED.md)
- Review [DEPLOYMENT.md](./DEPLOYMENT.md)
- See [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)

## Summary

- **MCP Server**: Always deployed (core functionality)
- **Admin API**: Optional, controlled by `deploy_admin_api`
- **Chat**: Optional, controlled by `deploy_chat`
- **Default**: Both Admin API and Chat are enabled
- **Flexibility**: Can be changed at any time
- **Cost**: App Service Plan cost is fixed, minimal additional cost per service
