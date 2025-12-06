# Azure Kubernetes Service (AKS) Deployment Guide

## Overview

This guide provides comprehensive step-by-step instructions for deploying the Platform Engineering Copilot to Azure Kubernetes Service (AKS) with IL5/IL6 compliance features.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Architecture Overview](#architecture-overview)
- [Pre-Deployment Steps](#pre-deployment-steps)
- [Terraform Deployment](#terraform-deployment)
- [AKS Configuration](#aks-configuration)
- [Container Registry Setup](#container-registry-setup)
- [Application Deployment](#application-deployment)
- [Security Configuration](#security-configuration)
- [Monitoring Setup](#monitoring-setup)
- [Troubleshooting](#troubleshooting)

## Prerequisites

### Required Tools

- **Azure CLI** (v2.50.0 or later)
  ```bash
  az --version
  # Install: brew install azure-cli
  ```

- **kubectl** (v1.28.0 or later)
  ```bash
  kubectl version --client
  # Install: az aks install-cli
  ```

- **Helm** (v3.12.0 or later)
  ```bash
  helm version
  # Install: brew install helm
  ```

- **Terraform** (v1.5.0 or later)
  ```bash
  terraform version
  # Install: brew install terraform
  ```

- **Docker** (for building container images)
  ```bash
  docker --version
  # Install: brew install --cask docker
  ```

### Required Azure Permissions

- **Subscription Contributor** or **Owner** role
- **Azure AD permissions** to create service principals and app registrations
- **Network Contributor** for VNet configuration
- **User Access Administrator** for role assignments

### Azure AD Configuration

1. **Create Azure AD Admin Group for AKS**
   ```bash
   # Create AD group for AKS administrators
   az ad group create \
     --display-name "AKS-Platform-Engineering-Admins" \
     --mail-nickname "aks-pe-admins"
   
   # Get the group object ID
   AKS_ADMIN_GROUP_ID=$(az ad group show \
     --group "AKS-Platform-Engineering-Admins" \
     --query id -o tsv)
   
   echo "AKS Admin Group ID: $AKS_ADMIN_GROUP_ID"
   ```

2. **Add Users to Admin Group**
   ```bash
   # Add current user
   CURRENT_USER_ID=$(az ad signed-in-user show --query id -o tsv)
   
   az ad group member add \
     --group "$AKS_ADMIN_GROUP_ID" \
     --member-id "$CURRENT_USER_ID"
   ```

## Architecture Overview

### AKS Cluster Configuration

The AKS deployment includes:

- **Private Cluster**: API server accessible only through private endpoint
- **Azure CNI**: Advanced networking with Azure Virtual Network integration
- **Azure AD Integration**: RBAC with Azure Active Directory
- **Network Policies**: Azure Network Policy for pod-to-pod security
- **Workload Identity**: OIDC-based authentication for workloads
- **System Node Pool**: 3 nodes (Standard_D4s_v5) for system services
- **User Node Pool**: Auto-scaling 2-10 nodes for application workloads
- **Zone Redundancy**: Multi-zone deployment for high availability

### Network Architecture

```
┌─────────────────────────────────────────────────────────────┐
│ Virtual Network (10.0.0.0/16)                              │
│                                                             │
│  ┌────────────────────────────────────────────────────┐   │
│  │ Management Subnet (10.0.3.0/24)                    │   │
│  │  - AKS System Nodes                                │   │
│  │  - AKS User Nodes                                  │   │
│  │  - ACI Containers                                  │   │
│  └────────────────────────────────────────────────────┘   │
│                                                             │
│  ┌────────────────────────────────────────────────────┐   │
│  │ Private Endpoint Subnet (10.0.2.0/24)             │   │
│  │  - ACR Private Endpoint                            │   │
│  │  - AKS Private Endpoint                            │   │
│  │  - Key Vault Private Endpoint                      │   │
│  └────────────────────────────────────────────────────┘   │
│                                                             │
└─────────────────────────────────────────────────────────────┘

Service CIDR: 10.1.0.0/16
DNS Service IP: 10.1.0.10
```

## Pre-Deployment Steps

### 1. Set Environment Variables

Create a `terraform.tfvars` file in the `infra/terraform` directory:

```hcl
# Basic Configuration
project_name        = "platsup"
environment         = "dev"
location            = "East US"
resource_group_name = "rg-platsup-dev-eastus"

# Azure AD Configuration
key_vault_admin_object_id  = "YOUR_OBJECT_ID"
azure_ad_admin_object_id   = "YOUR_OBJECT_ID"
azure_ad_admin_login       = "your-email@domain.com"

# SQL Configuration
sql_admin_password = "YourSecurePassword123!"

# Network Configuration
vnet_address_prefix            = "10.0.0.0/16"
app_service_subnet_prefix      = "10.0.1.0/24"
private_endpoint_subnet_prefix = "10.0.2.0/24"
management_subnet_prefix       = "10.0.3.0/24"

# Enable Container Infrastructure
enable_container_infrastructure = true
enable_aks                      = true
enable_aci                      = true

# ACR Configuration
acr_sku                           = "Premium"
acr_admin_enabled                 = false
acr_public_network_access_enabled = false
acr_zone_redundancy_enabled       = true
acr_enable_private_endpoint       = true
acr_content_trust_enabled         = true
acr_quarantine_enabled            = true
acr_retention_policy_enabled      = true
acr_retention_days                = 30

# Optional: Geo-replication for ACR
acr_georeplications = [
  {
    location                = "West US"
    zone_redundancy_enabled = true
  }
]

# AKS Configuration
aks_kubernetes_version       = "1.28.3"
aks_enable_private_cluster   = true
aks_system_node_vm_size      = "Standard_D4s_v5"
aks_system_node_count        = 3
aks_availability_zones       = ["1", "2", "3"]

# AKS User Node Pool
aks_enable_user_node_pool = true
aks_user_node_vm_size     = "Standard_D4s_v5"
aks_user_node_min_count   = 2
aks_user_node_max_count   = 10

# AKS Azure AD Integration
aks_enable_azure_rbac          = true
aks_aad_admin_group_object_ids = ["YOUR_AKS_ADMIN_GROUP_ID"]

# AKS Network Configuration
aks_network_plugin  = "azure"
aks_network_policy  = "azure"
aks_service_cidr    = "10.1.0.0/16"
aks_dns_service_ip  = "10.1.0.10"
aks_outbound_type   = "loadBalancer"

# AKS Security
aks_enable_azure_policy = true
aks_enable_defender     = true

# ACI Configuration
aci_mcp_container_image = "YOUR_ACR_NAME.azurecr.io/mcp-server:latest"
aci_mcp_cpu_cores       = 2
aci_mcp_memory_in_gb    = 4
aci_mcp_port            = 5100
aci_restart_policy      = "Always"
aci_enable_vnet_integration = true

# Monitoring
alert_email_addresses = ["your-email@domain.com"]
```

### 2. Get Your Azure AD Object ID

```bash
# Get your object ID
az ad signed-in-user show --query id -o tsv

# Get AKS admin group object ID
az ad group show --group "AKS-Platform-Engineering-Admins" --query id -o tsv
```

### 3. Validate Network CIDR Ranges

Ensure the CIDR ranges don't conflict with existing networks:

```bash
# List existing VNets in subscription
az network vnet list --query "[].{Name:name, AddressSpace:addressSpace.addressPrefixes}" -o table
```

## Terraform Deployment

### 1. Initialize Terraform

```bash
cd infra/terraform

# Initialize Terraform
terraform init

# Validate configuration
terraform validate
```

### 2. Plan Deployment

```bash
# Create execution plan
terraform plan -out=tfplan

# Review the plan carefully
# Ensure all container infrastructure resources are included:
# - azurerm_container_registry.acr
# - azurerm_kubernetes_cluster.aks
# - azurerm_kubernetes_cluster_node_pool.user (if enabled)
# - azurerm_container_group.aci
# - Role assignments for ACR pull
```

### 3. Apply Configuration

```bash
# Apply the plan
terraform apply tfplan

# This will create approximately 50-60 resources including:
# - Resource Group
# - Virtual Network with 3 subnets
# - Network Security Groups
# - Azure Container Registry (Premium)
# - AKS Cluster with system node pool
# - AKS User node pool (auto-scaling)
# - Azure Container Instance
# - Private endpoints
# - Log Analytics Workspace
# - Application Insights
# - Role assignments

# Deployment time: ~15-20 minutes
```

### 4. Capture Outputs

```bash
# Save important outputs
terraform output -json > outputs.json

# View container infrastructure summary
terraform output container_infrastructure_summary

# Get ACR login server
terraform output acr_login_server

# Get AKS cluster name
terraform output aks_cluster_name
```

## AKS Configuration

### 1. Get AKS Credentials

```bash
# Set variables
RESOURCE_GROUP="rg-platsup-dev-eastus"
AKS_NAME=$(terraform output -raw aks_cluster_name)

# Get credentials
az aks get-credentials \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --overwrite-existing

# Verify connection
kubectl cluster-info
kubectl get nodes
```

### 2. Verify Node Pools

```bash
# Check system node pool
kubectl get nodes -l agentpool=system

# Check user node pool
kubectl get nodes -l agentpool=user

# View node pool details
az aks nodepool list \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  -o table
```

### 3. Configure kubectl Context

```bash
# Get current context
kubectl config current-context

# View contexts
kubectl config get-contexts

# Set namespace as default
kubectl config set-context --current --namespace=default
```

### 4. Verify Azure AD Integration

```bash
# Test RBAC
kubectl get pods --all-namespaces

# If you get permission errors, verify Azure AD group membership
az ad group member check \
  --group "AKS-Platform-Engineering-Admins" \
  --member-id "$(az ad signed-in-user show --query id -o tsv)"
```

## Container Registry Setup

### 1. Login to ACR

```bash
# Get ACR name
ACR_NAME=$(terraform output -raw acr_name)
ACR_LOGIN_SERVER=$(terraform output -raw acr_login_server)

# Login to ACR
az acr login --name "$ACR_NAME"

# Verify login
az acr repository list --name "$ACR_NAME" -o table
```

### 2. Verify ACR-AKS Integration

```bash
# Check role assignment
az role assignment list \
  --scope "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ContainerRegistry/registries/$ACR_NAME" \
  --query "[?roleDefinitionName=='AcrPull'].{Principal:principalName, Role:roleDefinitionName}" \
  -o table

# The AKS kubelet identity should have AcrPull role
```

### 3. Build and Push Sample Image

```bash
# Build a sample image
docker build -t "$ACR_LOGIN_SERVER/hello-world:v1" .

# Push to ACR
docker push "$ACR_LOGIN_SERVER/hello-world:v1"

# Verify image
az acr repository show-tags \
  --name "$ACR_NAME" \
  --repository hello-world \
  -o table
```

## Application Deployment

### 1. Create Namespace

```bash
# Create application namespace
kubectl create namespace platform-copilot

# Label namespace
kubectl label namespace platform-copilot \
  environment=dev \
  project=platform-engineering-copilot
```

### 2. Create Kubernetes Secrets

```bash
# Get SQL connection string from Key Vault
KV_NAME=$(terraform output -raw key_vault_name)

SQL_CONNECTION_STRING=$(az keyvault secret show \
  --vault-name "$KV_NAME" \
  --name "DatabaseConnectionString" \
  --query value -o tsv)

# Create secret
kubectl create secret generic app-secrets \
  --namespace platform-copilot \
  --from-literal=connection-string="$SQL_CONNECTION_STRING"
```

### 3. Deploy Application with Helm

Create a `values.yaml` file:

```yaml
# values.yaml
replicaCount: 3

image:
  repository: platsupacr.azurecr.io/platform-copilot-api
  tag: latest
  pullPolicy: Always

service:
  type: ClusterIP
  port: 80

ingress:
  enabled: true
  className: nginx
  hosts:
    - host: copilot.example.com
      paths:
        - path: /
          pathType: Prefix

resources:
  limits:
    cpu: 1000m
    memory: 2Gi
  requests:
    cpu: 500m
    memory: 1Gi

autoscaling:
  enabled: true
  minReplicas: 3
  maxReplicas: 10
  targetCPUUtilizationPercentage: 70

podSecurityContext:
  runAsNonRoot: true
  runAsUser: 1000
  fsGroup: 1000
  seccompProfile:
    type: RuntimeDefault

securityContext:
  allowPrivilegeEscalation: false
  capabilities:
    drop:
    - ALL
  readOnlyRootFilesystem: true
```

Deploy with Helm:

```bash
# Install or upgrade
helm upgrade --install platform-copilot ./helm/platform-copilot \
  --namespace platform-copilot \
  --values values.yaml \
  --wait
```

### 4. Verify Deployment

```bash
# Check pods
kubectl get pods -n platform-copilot

# Check services
kubectl get services -n platform-copilot

# Check ingress
kubectl get ingress -n platform-copilot

# View logs
kubectl logs -n platform-copilot -l app=platform-copilot --tail=50
```

## Security Configuration

### 1. Enable Azure Policy for AKS

Azure Policy is already enabled via Terraform. Verify policies:

```bash
# List policy assignments
az policy assignment list \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?contains(name, 'aks')].{Name:name, Policy:displayName}" \
  -o table
```

### 2. Configure Network Policies

Create a network policy to restrict pod communication:

```yaml
# network-policy.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: platform-copilot-netpol
  namespace: platform-copilot
spec:
  podSelector:
    matchLabels:
      app: platform-copilot
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - namespaceSelector:
        matchLabels:
          name: ingress-nginx
    ports:
    - protocol: TCP
      port: 80
  egress:
  - to:
    - namespaceSelector: {}
    ports:
    - protocol: TCP
      port: 443  # HTTPS
    - protocol: TCP
      port: 1433 # SQL Server
    - protocol: TCP
      port: 53   # DNS
    - protocol: UDP
      port: 53   # DNS
```

Apply the policy:

```bash
kubectl apply -f network-policy.yaml
```

### 3. Configure Pod Security Standards

```bash
# Label namespace with Pod Security Standard
kubectl label namespace platform-copilot \
  pod-security.kubernetes.io/enforce=restricted \
  pod-security.kubernetes.io/audit=restricted \
  pod-security.kubernetes.io/warn=restricted
```

### 4. Enable Microsoft Defender for Containers

Defender is enabled via Terraform. Verify:

```bash
# Check Defender status
az security pricing show \
  --name Containers \
  --query "pricingTier" -o tsv
```

## Monitoring Setup

### 1. Verify Container Insights

```bash
# Check if Container Insights is enabled
az aks show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --query "addonProfiles.omsagent.enabled" -o tsv
```

### 2. View Metrics in Azure Portal

1. Navigate to AKS cluster in Azure Portal
2. Go to **Monitoring** > **Insights**
3. View:
   - Cluster performance
   - Node metrics
   - Container metrics
   - Logs

### 3. Query Logs with Kusto

```kusto
// Recent container logs
ContainerLog
| where TimeGenerated > ago(1h)
| where Namespace == "platform-copilot"
| project TimeGenerated, Computer, ContainerName, LogEntry
| order by TimeGenerated desc
| take 100

// Pod performance
Perf
| where ObjectName == "K8SContainer"
| where CounterName == "cpuUsageNanoCores"
| summarize AvgCPU = avg(CounterValue) by bin(TimeGenerated, 5m), InstanceName
| render timechart
```

### 4. Configure Alerts

Create alerts for critical conditions:

```bash
# Alert on pod crash loop
az monitor metrics alert create \
  --name "PodCrashLoop" \
  --resource-group "$RESOURCE_GROUP" \
  --scopes "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.ContainerService/managedClusters/$AKS_NAME" \
  --condition "count PodCount where Status == 'Failed' > 5" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action email your-email@domain.com
```

## Troubleshooting

### Common Issues

#### 1. Cannot Access AKS API Server

**Symptom**: `kubectl` commands fail with connection timeout

**Solution**:
```bash
# Verify you're on correct network (private cluster)
# Option 1: Use Azure Bastion or VPN
# Option 2: Disable private cluster temporarily
az aks update \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --enable-private-cluster false

# Re-enable after testing
az aks update \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --enable-private-cluster true
```

#### 2. Pods Cannot Pull from ACR

**Symptom**: `ImagePullBackOff` error

**Solution**:
```bash
# Verify ACR integration
az aks check-acr \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --acr "$ACR_NAME"

# Re-attach ACR if needed
az aks update \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --attach-acr "$ACR_NAME"
```

#### 3. Azure AD Authentication Issues

**Symptom**: `Unauthorized` or `Forbidden` errors

**Solution**:
```bash
# Clear kubectl cache
rm -rf ~/.kube/cache

# Re-authenticate
az login
az aks get-credentials \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --overwrite-existing

# Verify group membership
az ad group member list \
  --group "AKS-Platform-Engineering-Admins" \
  --query "[].userPrincipalName" -o table
```

#### 4. Node Pool Scaling Issues

**Symptom**: Nodes not scaling as expected

**Solution**:
```bash
# Check node pool configuration
az aks nodepool show \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --query "{MinCount:minCount, MaxCount:maxCount, CurrentCount:count}" -o table

# Manually scale if needed
az aks nodepool scale \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --node-count 5
```

### Debugging Commands

```bash
# View cluster events
kubectl get events --all-namespaces --sort-by='.lastTimestamp'

# Describe problematic pod
kubectl describe pod POD_NAME -n platform-copilot

# View pod logs
kubectl logs POD_NAME -n platform-copilot --previous

# Execute commands in pod
kubectl exec -it POD_NAME -n platform-copilot -- /bin/bash

# Check cluster health
kubectl get componentstatuses
kubectl get nodes -o wide

# View resource usage
kubectl top nodes
kubectl top pods -n platform-copilot
```

## Next Steps

- Review [Kubernetes Operations Guide](./KUBERNETES-OPERATIONS-GUIDE.md) for Day-2 operations
- Review [Container Build Guide](./CONTAINER-BUILD-GUIDE.md) for CI/CD integration
- Set up [GitOps with Flux or Argo CD](./GITOPS-GUIDE.md)
- Configure [External Secrets Operator](./EXTERNAL-SECRETS.md)
- Implement [Backup and Disaster Recovery](./BACKUP-DR.md)

## Additional Resources

- [AKS Documentation](https://docs.microsoft.com/en-us/azure/aks/)
- [Azure Container Registry Documentation](https://docs.microsoft.com/en-us/azure/container-registry/)
- [Kubernetes Best Practices](https://kubernetes.io/docs/concepts/configuration/overview/)
- [Azure Policy for AKS](https://docs.microsoft.com/en-us/azure/governance/policy/concepts/policy-for-kubernetes)
