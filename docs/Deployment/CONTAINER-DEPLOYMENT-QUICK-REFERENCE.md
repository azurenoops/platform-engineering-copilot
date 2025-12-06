# Container Infrastructure Quick Reference

## Deployment Commands

Choose either Terraform or Bicep based on your preference. Both provide the same infrastructure capabilities.

### Initial Setup - Terraform

```bash
# 1. Navigate to Terraform directory
cd /Users/johnspinella/repos/platform-engineering-copilot/infra/terraform

# 2. Create a terraform.tfvars file (or use -var flags)
cat > terraform.tfvars <<EOF
# Required settings
subscription_id = "your-subscription-id"
location        = "eastus"
environment     = "dev"

# Container infrastructure
enable_container_infrastructure = true
enable_aks                      = true
enable_aci                      = true
deploy_admin_api                = true
deploy_chat                     = true

# Use existing resources (optional)
use_existing_network       = false
use_existing_log_analytics = false
use_existing_key_vault     = false

# If using existing resources, uncomment and set:
# existing_vnet_name                      = "my-existing-vnet"
# existing_vnet_resource_group            = "my-network-rg"
# existing_app_service_subnet_name        = "app-subnet"
# existing_private_endpoint_subnet_name   = "pe-subnet"
# existing_log_analytics_workspace_name   = "my-existing-laws"
# existing_log_analytics_resource_group   = "my-monitoring-rg"
# existing_key_vault_name                 = "my-existing-kv"
# existing_key_vault_resource_group       = "my-security-rg"
EOF

# 3. Initialize Terraform
terraform init

# 4. Plan deployment
terraform plan -out=tfplan

# 5. Apply
terraform apply tfplan

# 6. View outputs
terraform output container_infrastructure_summary
```

### Initial Setup - Bicep

```bash
# 1. Navigate to Bicep directory
cd /Users/johnspinella/repos/platform-engineering-copilot/infra/bicep

# 2. Choose a parameters file based on deployment target:
# - main.parameters.aks.json      (for AKS deployment)
# - main.parameters.aci.json      (for ACI deployment)
# - main.parameters.appservice.json (for App Service deployment)

# 3. Copy and customize the appropriate parameters file
cp main.parameters.aks.json main.parameters.dev.json

# 4. Edit main.parameters.dev.json - Key parameters to customize:
#    - deployAdminApi: Control Admin API deployment (true/false)
#    - deployChat: Control Chat deployment (true/false)
#    - useExistingNetwork: Use existing VNet instead of creating new (true/false)
#    - useExistingLogAnalytics: Use existing Log Analytics workspace (true/false)
#    - useExistingKeyVault: Use existing Key Vault (true/false)
#
# If using existing resources, also set:
#    - existingVNetName, existingVNetResourceGroup
#    - existingAppServiceSubnetName, existingPrivateEndpointSubnetName
#    - existingLogAnalyticsWorkspaceName, existingLogAnalyticsResourceGroup
#    - existingKeyVaultName, existingKeyVaultResourceGroup
#
# All parameters are now included in the template files with default values

# 5. Validate deployment
az deployment sub what-if \
  --location eastus \
  --template-file main.bicep \
  --parameters @main.parameters.dev.json

# 6. Deploy
az deployment sub create \
  --location eastus \
  --template-file main.bicep \
  --parameters @main.parameters.dev.json \
  --name platform-copilot-$(date +%Y%m%d-%H%M%S)

# 7. View outputs
DEPLOYMENT_NAME=$(az deployment sub list \
  --query "[?contains(name, 'platform-copilot')].name | [0]" -o tsv)

az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query properties.outputs
```

### Get ACR Details

#### Terraform
```bash
# Get ACR name
ACR_NAME=$(terraform output -raw acr_name)

# Get login server
ACR_LOGIN_SERVER=$(terraform output -raw acr_login_server)

# Login to ACR
az acr login --name "$ACR_NAME"

# List images
az acr repository list --name "$ACR_NAME"
```

#### Bicep
```bash
# Get deployment name (use the most recent deployment)
DEPLOYMENT_NAME=$(az deployment sub list \
  --query "[?contains(name, 'platform-copilot')].name | [0]" -o tsv)

# Get ACR name
ACR_NAME=$(az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query "properties.outputs.acrName.value" -o tsv)

# Get login server
ACR_LOGIN_SERVER=$(az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query "properties.outputs.acrLoginServer.value" -o tsv)

# Login to ACR
az acr login --name "$ACR_NAME"

# List images
az acr repository list --name "$ACR_NAME"
```

### Get AKS Access

#### Terraform
```bash
# Get AKS name
AKS_NAME=$(terraform output -raw aks_cluster_name)
RESOURCE_GROUP=$(terraform output -raw resource_group_name)

# Get credentials
az aks get-credentials \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --overwrite-existing

# Verify connection
kubectl cluster-info
kubectl get nodes
```

#### Bicep
```bash
# Get AKS name and resource group
AKS_NAME=$(az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query "properties.outputs.aksClusterName.value" -o tsv)

RESOURCE_GROUP=$(az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query "properties.outputs.resourceGroupName.value" -o tsv)

# Get credentials
az aks get-credentials \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --overwrite-existing

# Verify connection
kubectl cluster-info
kubectl get nodes
```

### Build and Push Images

```bash
# Build image
docker build -t "$ACR_LOGIN_SERVER/myapp:v1" .

# Push to ACR
docker push "$ACR_LOGIN_SERVER/myapp:v1"

# Verify
az acr repository show-tags \
  --name "$ACR_NAME" \
  --repository myapp
```

### Deploy to AKS

```bash
# Create namespace
kubectl create namespace myapp

# Deploy application
kubectl apply -f deployment.yaml -n myapp

# Check status
kubectl get pods -n myapp
kubectl get svc -n myapp
```

## Essential kubectl Commands

```bash
# Nodes
kubectl get nodes
kubectl top nodes
kubectl describe node NODE_NAME

# Pods
kubectl get pods -A
kubectl top pods -A
kubectl logs POD_NAME -n NAMESPACE
kubectl exec -it POD_NAME -n NAMESPACE -- bash

# Deployments
kubectl get deployments -A
kubectl rollout status deployment/NAME -n NAMESPACE
kubectl rollout restart deployment/NAME -n NAMESPACE

# Services
kubectl get svc -A
kubectl describe svc SERVICE_NAME -n NAMESPACE

# Events
kubectl get events -A --sort-by='.lastTimestamp'
```

## Troubleshooting

### ACR Issues

```bash
# Check ACR health
az acr check-health --name "$ACR_NAME"

# Test ACR-AKS integration
az aks check-acr \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --acr "$ACR_NAME"

# View ACR logs
az acr repository list --name "$ACR_NAME"
```

### AKS Issues

```bash
# Check cluster status
az aks show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --query "powerState.code"

# Get node pool status
az aks nodepool list \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME"

# View cluster events
kubectl get events -A --sort-by='.lastTimestamp' | tail -20

# Check pod issues
kubectl describe pod POD_NAME -n NAMESPACE
kubectl logs POD_NAME -n NAMESPACE --previous
```

### Common Fixes

```bash
# Restart deployment
kubectl rollout restart deployment/NAME -n NAMESPACE

# Scale deployment
kubectl scale deployment/NAME --replicas=3 -n NAMESPACE

# Re-attach ACR to AKS
az aks update \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --attach-acr "$ACR_NAME"

# Cordon and drain node
kubectl cordon NODE_NAME
kubectl drain NODE_NAME --ignore-daemonsets --delete-emptydir-data
kubectl uncordon NODE_NAME
```

## Monitoring

### Azure Portal
1. Navigate to AKS cluster
2. Go to **Monitoring** > **Insights**
3. View cluster, node, and container metrics

### kubectl Commands

```bash
# Resource usage
kubectl top nodes
kubectl top pods -A

# Cluster info
kubectl cluster-info
kubectl get componentstatuses

# Recent events
kubectl get events -A --sort-by='.lastTimestamp' | tail -20
```

### Log Analytics Queries

```kusto
// Container logs
ContainerLog
| where TimeGenerated > ago(1h)
| where Namespace == "platform-copilot"
| order by TimeGenerated desc
| take 100

// Pod performance
Perf
| where ObjectName == "K8SContainer"
| where CounterName == "cpuUsageNanoCores"
| summarize AvgCPU = avg(CounterValue) by bin(TimeGenerated, 5m), InstanceName
| render timechart
```

## Scaling

### Manual Scaling

```bash
# Scale deployment
kubectl scale deployment/NAME --replicas=5 -n NAMESPACE

# Scale node pool
az aks nodepool scale \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --node-count 5
```

### Auto-Scaling

```bash
# Enable cluster autoscaler
az aks nodepool update \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --enable-cluster-autoscaler \
  --min-count 2 \
  --max-count 10

# Create HPA
kubectl autoscale deployment NAME \
  --cpu-percent=70 \
  --min=3 \
  --max=10 \
  -n NAMESPACE
```

## Updates

### Update Kubernetes Version

```bash
# Check available versions
az aks get-upgrades \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME"

# Upgrade cluster
az aks upgrade \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --kubernetes-version 1.29.0
```

### Update Node Image

```bash
# Update node pool image
az aks nodepool upgrade \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --node-image-only
```

## Security

### Scan Images

```bash
# Scan with Trivy
trivy image "$ACR_LOGIN_SERVER/myapp:v1"

# Scan for high/critical only
trivy image --severity HIGH,CRITICAL \
  "$ACR_LOGIN_SERVER/myapp:v1"
```

### Network Policies

```bash
# List network policies
kubectl get networkpolicies -A

# Apply network policy
kubectl apply -f network-policy.yaml
```

### RBAC

```bash
# Check permissions
kubectl auth can-i --list

# Create service account
kubectl create serviceaccount ACCOUNT_NAME -n NAMESPACE

# Create role binding
kubectl create rolebinding BINDING_NAME \
  --role=ROLE_NAME \
  --serviceaccount=NAMESPACE:ACCOUNT_NAME \
  -n NAMESPACE
```

## Cleanup

### Delete Resources

```bash
# Delete deployment
kubectl delete deployment NAME -n NAMESPACE

# Delete namespace
kubectl delete namespace NAME
```

#### Terraform
```bash
# Destroy infrastructure (CAUTION!)
terraform destroy
```

#### Bicep
```bash
# Delete resource group (CAUTION!)
RESOURCE_GROUP=$(az deployment sub show \
  --name "$DEPLOYMENT_NAME" \
  --query "properties.outputs.resourceGroupName.value" -o tsv)

az group delete --name "$RESOURCE_GROUP" --yes --no-wait
```

## Using Existing Resources

Both Terraform and Bicep support using existing Azure resources instead of creating new ones. All parameter files (`main.parameters.*.json`) now include these parameters with empty string defaults.

### Existing Network (VNet)

#### Terraform
```hcl
# In terraform.tfvars:
use_existing_network              = true
existing_vnet_name                = "my-existing-vnet"
existing_vnet_resource_group      = "my-network-rg"
existing_app_service_subnet_name  = "app-subnet"
existing_private_endpoint_subnet_name = "pe-subnet"
```

#### Bicep
```json
// In main.parameters.dev.json (or any .aks/.aci/.appservice file):
"useExistingNetwork": { "value": true },
"existingVNetName": { "value": "my-existing-vnet" },
"existingVNetResourceGroup": { "value": "my-network-rg" },
"existingAppServiceSubnetName": { "value": "app-subnet" },
"existingPrivateEndpointSubnetName": { "value": "pe-subnet" }
```

### Existing Log Analytics Workspace

#### Terraform
```hcl
# In terraform.tfvars:
use_existing_log_analytics         = true
existing_log_analytics_workspace_name = "my-existing-laws"
existing_log_analytics_resource_group = "my-monitoring-rg"
```

#### Bicep
```json
// In main.parameters.dev.json (or any .aks/.aci/.appservice file):
"useExistingLogAnalytics": { "value": true },
"existingLogAnalyticsWorkspaceName": { "value": "my-existing-laws" },
"existingLogAnalyticsResourceGroup": { "value": "my-monitoring-rg" }
```

### Existing Key Vault

#### Terraform
```hcl
# In terraform.tfvars:
use_existing_key_vault         = true
existing_key_vault_name        = "my-existing-kv"
existing_key_vault_resource_group = "my-security-rg"
```

#### Bicep
```json
// In main.parameters.dev.json (or any .aks/.aci/.appservice file):
"useExistingKeyVault": { "value": true },
"existingKeyVaultName": { "value": "my-existing-kv" },
"existingKeyVaultResourceGroup": { "value": "my-security-rg" }
```

## Conditional Deployment

All parameter files now include `deployAdminApi` and `deployChat` parameters to control component deployment.

### Deploy Only Chat (No Admin API)

#### Terraform
```hcl
# In terraform.tfvars:
deploy_admin_api = false
deploy_chat      = true
```

#### Bicep
```json
// In main.parameters.dev.json (or any .aks/.aci/.appservice file):
"deployAdminApi": { "value": false },
"deployChat": { "value": true }
```

### Deploy Only Admin API (No Chat)

#### Terraform
```hcl
# In terraform.tfvars:
deploy_admin_api = true
deploy_chat      = false
```

#### Bicep
```json
// In main.parameters.dev.json (or any .aks/.aci/.appservice file):
"deployAdminApi": { "value": true },
"deployChat": { "value": false }
```

### Deploy Both (Default)

Both parameters default to `true` in all template files, deploying both Admin API and Chat components.

## Documentation Links

- [AKS Deployment Guide](./AKS-DEPLOYMENT-GUIDE.md)
- [Kubernetes Operations Guide](./KUBERNETES-OPERATIONS-GUIDE.md)
- [Container Build Guide](./CONTAINER-BUILD-GUIDE.md)
- [Validation Guide](./CONTAINER-INFRASTRUCTURE-VALIDATION.md)
- [Implementation Summary](./CONTAINER-INFRASTRUCTURE-SUMMARY.md)

## Support Resources

- **Azure AKS Docs:** https://docs.microsoft.com/en-us/azure/aks/
- **Azure ACR Docs:** https://docs.microsoft.com/en-us/azure/container-registry/
- **Kubernetes Docs:** https://kubernetes.io/docs/
- **kubectl Cheat Sheet:** https://kubernetes.io/docs/reference/kubectl/cheatsheet/
