# Kubernetes Operations Guide

## Overview

This guide covers Day-2 operations for managing the Platform Engineering Copilot on Azure Kubernetes Service (AKS). It includes routine maintenance, scaling, upgrades, monitoring, and troubleshooting procedures.

## Table of Contents

- [Cluster Management](#cluster-management)
- [Application Management](#application-management)
- [Scaling Operations](#scaling-operations)
- [Upgrade Procedures](#upgrade-procedures)
- [Backup and Restore](#backup-and-restore)
- [Security Operations](#security-operations)
- [Monitoring and Alerting](#monitoring-and-alerting)
- [Cost Optimization](#cost-optimization)
- [Disaster Recovery](#disaster-recovery)

## Cluster Management

### Daily Health Checks

Create a daily health check script:

```bash
#!/bin/bash
# daily-health-check.sh

RESOURCE_GROUP="rg-platsup-dev-eastus"
AKS_NAME="platsup-aks-dev"
NAMESPACE="platform-copilot"

echo "=== AKS Daily Health Check ==="
echo "Date: $(date)"
echo ""

# 1. Cluster Status
echo "1. Cluster Status:"
az aks show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --query "{Status:powerState.code, Version:kubernetesVersion, Provisioning:provisioningState}" \
  -o table
echo ""

# 2. Node Status
echo "2. Node Status:"
kubectl get nodes -o wide
echo ""

# 3. Node Resource Usage
echo "3. Node Resource Usage:"
kubectl top nodes
echo ""

# 4. Pod Status
echo "4. Pod Status (All Namespaces):"
kubectl get pods --all-namespaces --field-selector status.phase!=Running,status.phase!=Succeeded
echo ""

# 5. Application Pods
echo "5. Application Pods ($NAMESPACE):"
kubectl get pods -n "$NAMESPACE" -o wide
echo ""

# 6. Pod Resource Usage
echo "6. Pod Resource Usage ($NAMESPACE):"
kubectl top pods -n "$NAMESPACE"
echo ""

# 7. Recent Events
echo "7. Recent Warning Events:"
kubectl get events --all-namespaces \
  --field-selector type=Warning \
  --sort-by='.lastTimestamp' \
  | tail -20
echo ""

# 8. Persistent Volume Claims
echo "8. PVC Status:"
kubectl get pvc --all-namespaces
echo ""

# 9. Services and Endpoints
echo "9. Services:"
kubectl get svc -n "$NAMESPACE"
echo ""

# 10. Ingress Status
echo "10. Ingress Status:"
kubectl get ingress --all-namespaces
echo ""

echo "=== Health Check Complete ==="
```

Make it executable and run:

```bash
chmod +x daily-health-check.sh
./daily-health-check.sh
```

### Node Pool Management

#### View Node Pool Details

```bash
RESOURCE_GROUP="rg-platsup-dev-eastus"
AKS_NAME="platsup-aks-dev"

# List all node pools
az aks nodepool list \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  -o table

# Get detailed info for specific node pool
az aks nodepool show \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  -o json
```

#### Manual Node Pool Scaling

```bash
# Scale user node pool
az aks nodepool scale \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --node-count 5

# Verify scaling
kubectl get nodes -l agentpool=user
```

#### Update Node Pool Configuration

```bash
# Update max pods per node
az aks nodepool update \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --max-pods 110

# Enable autoscaler
az aks nodepool update \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --enable-cluster-autoscaler \
  --min-count 2 \
  --max-count 10
```

#### Node Maintenance (Cordon and Drain)

```bash
# Cordon node (prevent new pods from scheduling)
kubectl cordon NODE_NAME

# Drain node (evict all pods)
kubectl drain NODE_NAME \
  --ignore-daemonsets \
  --delete-emptydir-data \
  --grace-period=300

# Verify pods are rescheduled
kubectl get pods -o wide --all-namespaces | grep NODE_NAME

# Uncordon when maintenance is complete
kubectl uncordon NODE_NAME
```

### Cluster Autoscaler

The cluster autoscaler automatically adjusts the number of nodes based on resource requests.

#### Monitor Autoscaler

```bash
# View autoscaler logs
kubectl logs -n kube-system -l app=cluster-autoscaler --tail=50

# Check autoscaler status
kubectl get configmap cluster-autoscaler-status \
  -n kube-system \
  -o yaml
```

#### Autoscaler Best Practices

1. **Set appropriate resource requests** on pods
2. **Use Pod Disruption Budgets** to ensure availability
3. **Monitor pending pods** that trigger scale-up
4. **Set node pool size limits** to control costs

## Application Management

### Rolling Updates

#### Update Container Image

```bash
# Update deployment with new image
kubectl set image deployment/platform-copilot-api \
  platform-copilot-api=platsupacr.azurecr.io/platform-copilot-api:v2.0 \
  -n platform-copilot

# Monitor rollout
kubectl rollout status deployment/platform-copilot-api -n platform-copilot

# View rollout history
kubectl rollout history deployment/platform-copilot-api -n platform-copilot
```

#### Rollback Deployment

```bash
# Rollback to previous version
kubectl rollout undo deployment/platform-copilot-api -n platform-copilot

# Rollback to specific revision
kubectl rollout undo deployment/platform-copilot-api \
  --to-revision=3 \
  -n platform-copilot

# Verify rollback
kubectl rollout status deployment/platform-copilot-api -n platform-copilot
```

#### Blue-Green Deployment

```yaml
# Create green deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: platform-copilot-api-green
  namespace: platform-copilot
spec:
  replicas: 3
  selector:
    matchLabels:
      app: platform-copilot-api
      version: green
  template:
    metadata:
      labels:
        app: platform-copilot-api
        version: green
    spec:
      containers:
      - name: api
        image: platsupacr.azurecr.io/platform-copilot-api:v2.0
        # ... rest of spec
```

```bash
# Deploy green version
kubectl apply -f green-deployment.yaml

# Test green version
kubectl port-forward -n platform-copilot \
  deployment/platform-copilot-api-green 8080:80

# Switch traffic to green by updating service selector
kubectl patch service platform-copilot-api \
  -n platform-copilot \
  -p '{"spec":{"selector":{"version":"green"}}}'

# Delete old blue deployment after validation
kubectl delete deployment platform-copilot-api-blue -n platform-copilot
```

### ConfigMap and Secret Management

#### Update ConfigMaps

```bash
# Edit ConfigMap
kubectl edit configmap app-config -n platform-copilot

# Update from file
kubectl create configmap app-config \
  --from-file=config.json \
  --dry-run=client -o yaml | kubectl apply -f -

# Trigger pod restart to pick up changes
kubectl rollout restart deployment/platform-copilot-api -n platform-copilot
```

#### Rotate Secrets

```bash
# Update secret
kubectl create secret generic app-secrets \
  --from-literal=api-key="new-secret-value" \
  --dry-run=client -o yaml | kubectl apply -f -

# For secrets from Azure Key Vault (using CSI driver)
# Restart pods to mount new secrets
kubectl rollout restart deployment/platform-copilot-api -n platform-copilot
```

### Pod Debugging

#### Get Pod Logs

```bash
# Current logs
kubectl logs POD_NAME -n platform-copilot

# Previous container logs (after restart)
kubectl logs POD_NAME -n platform-copilot --previous

# Stream logs
kubectl logs -f POD_NAME -n platform-copilot

# Logs from specific container
kubectl logs POD_NAME -c CONTAINER_NAME -n platform-copilot

# Logs from all containers in pod
kubectl logs POD_NAME --all-containers -n platform-copilot
```

#### Execute Commands in Pod

```bash
# Interactive shell
kubectl exec -it POD_NAME -n platform-copilot -- /bin/bash

# Run specific command
kubectl exec POD_NAME -n platform-copilot -- ls -la /app

# Copy files from pod
kubectl cp platform-copilot/POD_NAME:/app/logs ./local-logs

# Copy files to pod
kubectl cp ./local-config.json platform-copilot/POD_NAME:/app/config.json
```

#### Pod Resource Inspection

```bash
# Describe pod
kubectl describe pod POD_NAME -n platform-copilot

# Get pod YAML
kubectl get pod POD_NAME -n platform-copilot -o yaml

# Check pod metrics
kubectl top pod POD_NAME -n platform-copilot

# View pod events
kubectl get events -n platform-copilot \
  --field-selector involvedObject.name=POD_NAME
```

## Scaling Operations

### Horizontal Pod Autoscaling (HPA)

#### Create HPA

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: platform-copilot-api-hpa
  namespace: platform-copilot
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: platform-copilot-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 0
      policies:
      - type: Percent
        value: 100
        periodSeconds: 30
      - type: Pods
        value: 2
        periodSeconds: 30
      selectPolicy: Max
```

Apply and monitor:

```bash
kubectl apply -f hpa.yaml

# Check HPA status
kubectl get hpa -n platform-copilot

# Describe HPA for details
kubectl describe hpa platform-copilot-api-hpa -n platform-copilot

# Watch HPA
kubectl get hpa -n platform-copilot --watch
```

#### Custom Metrics HPA

Using Azure Monitor metrics:

```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: platform-copilot-api-hpa
  namespace: platform-copilot
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: platform-copilot-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: External
    external:
      metric:
        name: azure_app_requests_per_second
      target:
        type: AverageValue
        averageValue: "1000"
```

### Vertical Pod Autoscaling (VPA)

Install VPA:

```bash
# Clone VPA repository
git clone https://github.com/kubernetes/autoscaler.git
cd autoscaler/vertical-pod-autoscaler

# Install VPA
./hack/vpa-up.sh

# Verify installation
kubectl get pods -n kube-system | grep vpa
```

Create VPA:

```yaml
apiVersion: autoscaling.k8s.io/v1
kind: VerticalPodAutoscaler
metadata:
  name: platform-copilot-api-vpa
  namespace: platform-copilot
spec:
  targetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: platform-copilot-api
  updatePolicy:
    updateMode: "Auto"  # or "Recreate", "Initial", "Off"
  resourcePolicy:
    containerPolicies:
    - containerName: api
      minAllowed:
        cpu: 100m
        memory: 256Mi
      maxAllowed:
        cpu: 2000m
        memory: 4Gi
```

Monitor VPA:

```bash
kubectl get vpa -n platform-copilot
kubectl describe vpa platform-copilot-api-vpa -n platform-copilot
```

## Upgrade Procedures

### Kubernetes Version Upgrade

#### Check Available Versions

```bash
# Get available upgrade versions
az aks get-upgrades \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  -o table

# Check current version
kubectl version --short
```

#### Upgrade Cluster

**Always test in non-production first!**

```bash
# Upgrade cluster (control plane first)
az aks upgrade \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --kubernetes-version 1.29.0 \
  --no-wait

# Monitor upgrade
az aks show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --query "provisioningState" -o tsv

# Upgrade specific node pool
az aks nodepool upgrade \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --kubernetes-version 1.29.0 \
  --no-wait
```

#### Pre-Upgrade Checklist

- [ ] **Backup** all critical data and configurations
- [ ] **Review** Kubernetes changelog for breaking changes
- [ ] **Test** upgrade in dev/staging environment
- [ ] **Verify** all addons are compatible with new version
- [ ] **Schedule** maintenance window
- [ ] **Notify** stakeholders
- [ ] **Create** rollback plan
- [ ] **Check** deprecated APIs in your manifests

```bash
# Check for deprecated APIs
kubectl get all --all-namespaces -o json | \
  jq -r '.items[] | select(.apiVersion | contains("v1beta1"))'
```

#### Post-Upgrade Validation

```bash
# Verify cluster version
kubectl version --short

# Check node versions
kubectl get nodes -o wide

# Verify all pods are running
kubectl get pods --all-namespaces

# Run health checks
./daily-health-check.sh

# Test application functionality
kubectl run curl-test --image=curlimages/curl --rm -it --restart=Never -- \
  curl http://platform-copilot-api.platform-copilot.svc.cluster.local/health
```

### Node Image Upgrade

```bash
# Upgrade node image to latest
az aks nodepool upgrade \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --node-image-only

# Monitor progress
az aks nodepool show \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --query "provisioningState" -o tsv
```

## Backup and Restore

### Velero Setup

Install Velero for backup and restore:

```bash
# Install Velero CLI
brew install velero

# Create Azure storage account for backups
BACKUP_STORAGE_ACCOUNT="platsupbackups"
BACKUP_CONTAINER="velero"

az storage account create \
  --name "$BACKUP_STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP" \
  --sku Standard_GRS \
  --encryption-services blob \
  --https-only true

az storage container create \
  --name "$BACKUP_CONTAINER" \
  --account-name "$BACKUP_STORAGE_ACCOUNT"

# Create service principal for Velero
AZURE_SUBSCRIPTION_ID=$(az account show --query id -o tsv)
AZURE_TENANT_ID=$(az account show --query tenantId -o tsv)

AZURE_CLIENT_SECRET=$(az ad sp create-for-rbac \
  --name "velero-platsup" \
  --role "Contributor" \
  --scopes "/subscriptions/$AZURE_SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP" \
  --query password -o tsv)

AZURE_CLIENT_ID=$(az ad sp list \
  --display-name "velero-platsup" \
  --query [0].appId -o tsv)

# Create credentials file
cat << EOF > credentials-velero
AZURE_SUBSCRIPTION_ID=$AZURE_SUBSCRIPTION_ID
AZURE_TENANT_ID=$AZURE_TENANT_ID
AZURE_CLIENT_ID=$AZURE_CLIENT_ID
AZURE_CLIENT_SECRET=$AZURE_CLIENT_SECRET
AZURE_RESOURCE_GROUP=$RESOURCE_GROUP
AZURE_CLOUD_NAME=AzurePublicCloud
EOF

# Install Velero in cluster
velero install \
  --provider azure \
  --plugins velero/velero-plugin-for-microsoft-azure:v1.8.0 \
  --bucket "$BACKUP_CONTAINER" \
  --secret-file ./credentials-velero \
  --backup-location-config \
    resourceGroup="$RESOURCE_GROUP",storageAccount="$BACKUP_STORAGE_ACCOUNT" \
  --snapshot-location-config \
    apiTimeout=5m,resourceGroup="$RESOURCE_GROUP"

# Verify Velero installation
kubectl get pods -n velero
```

### Create Backups

```bash
# Backup entire namespace
velero backup create platform-copilot-backup \
  --include-namespaces platform-copilot \
  --wait

# Backup with TTL (30 days)
velero backup create platform-copilot-backup-$(date +%Y%m%d) \
  --include-namespaces platform-copilot \
  --ttl 720h \
  --wait

# Scheduled backup (daily at 2 AM)
velero schedule create daily-backup \
  --schedule="0 2 * * *" \
  --include-namespaces platform-copilot \
  --ttl 720h

# Backup specific resources
velero backup create config-backup \
  --include-namespaces platform-copilot \
  --include-resources configmaps,secrets \
  --wait

# Check backup status
velero backup get
velero backup describe platform-copilot-backup
```

### Restore from Backup

```bash
# List available backups
velero backup get

# Restore from backup
velero restore create --from-backup platform-copilot-backup

# Restore to different namespace
velero restore create --from-backup platform-copilot-backup \
  --namespace-mappings platform-copilot:platform-copilot-restored

# Restore only specific resources
velero restore create --from-backup platform-copilot-backup \
  --include-resources deployments,services

# Check restore status
velero restore get
velero restore describe RESTORE_NAME
```

### Persistent Volume Snapshots

```bash
# Create PV snapshot manually
az snapshot create \
  --resource-group "$AKS_NODE_RESOURCE_GROUP" \
  --name "pvc-snapshot-$(date +%Y%m%d)" \
  --source "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$AKS_NODE_RESOURCE_GROUP/providers/Microsoft.Compute/disks/PVC_DISK_NAME"

# List snapshots
az snapshot list \
  --resource-group "$AKS_NODE_RESOURCE_GROUP" \
  -o table
```

## Security Operations

### Certificate Management

#### Cert-Manager Setup

```bash
# Install cert-manager
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

# Verify installation
kubectl get pods -n cert-manager

# Create Let's Encrypt issuer
cat << EOF | kubectl apply -f -
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: admin@example.com
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
EOF
```

#### Create Certificate

```yaml
apiVersion: cert-manager.io/v1
kind: Certificate
metadata:
  name: platform-copilot-tls
  namespace: platform-copilot
spec:
  secretName: platform-copilot-tls
  issuerRef:
    name: letsencrypt-prod
    kind: ClusterIssuer
  dnsNames:
  - copilot.example.com
```

### RBAC Management

#### Create Service Account with Limited Permissions

```yaml
# service-account.yaml
apiVersion: v1
kind: ServiceAccount
metadata:
  name: app-deployer
  namespace: platform-copilot
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: app-deployer-role
  namespace: platform-copilot
rules:
- apiGroups: ["apps"]
  resources: ["deployments"]
  verbs: ["get", "list", "watch", "update", "patch"]
- apiGroups: [""]
  resources: ["pods", "services"]
  verbs: ["get", "list", "watch"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: app-deployer-binding
  namespace: platform-copilot
subjects:
- kind: ServiceAccount
  name: app-deployer
  namespace: platform-copilot
roleRef:
  kind: Role
  name: app-deployer-role
  apiGroup: rbac.authorization.k8s.io
```

Apply:

```bash
kubectl apply -f service-account.yaml

# Get service account token
kubectl create token app-deployer -n platform-copilot
```

### Security Scanning

#### Scan Images with Trivy

```bash
# Install Trivy
brew install trivy

# Scan image in ACR
trivy image platsupacr.azurecr.io/platform-copilot-api:latest

# Scan for critical vulnerabilities only
trivy image --severity CRITICAL,HIGH \
  platsupacr.azurecr.io/platform-copilot-api:latest

# Generate report
trivy image --format json --output report.json \
  platsupacr.azurecr.io/platform-copilot-api:latest
```

#### Runtime Security with Falco

```bash
# Install Falco
helm repo add falcosecurity https://falcosecurity.github.io/charts
helm repo update

helm install falco falcosecurity/falco \
  --namespace falco \
  --create-namespace

# View Falco logs
kubectl logs -n falco -l app=falco -f
```

## Monitoring and Alerting

### Prometheus and Grafana

#### Install Prometheus Stack

```bash
# Add Prometheus Helm repo
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

# Install kube-prometheus-stack
helm install prometheus prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  --create-namespace \
  --set prometheus.prometheusSpec.retention=30d \
  --set prometheus.prometheusSpec.storageSpec.volumeClaimTemplate.spec.resources.requests.storage=50Gi

# Access Grafana
kubectl port-forward -n monitoring svc/prometheus-grafana 3000:80

# Get Grafana admin password
kubectl get secret -n monitoring prometheus-grafana \
  -o jsonpath="{.data.admin-password}" | base64 --decode
```

### Custom Metrics

Create a ServiceMonitor:

```yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: platform-copilot-metrics
  namespace: platform-copilot
spec:
  selector:
    matchLabels:
      app: platform-copilot-api
  endpoints:
  - port: metrics
    interval: 30s
    path: /metrics
```

### Azure Monitor Integration

```bash
# Enable Container Insights
az aks enable-addons \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --addons monitoring \
  --workspace-resource-id "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.OperationalInsights/workspaces/$WORKSPACE_NAME"
```

## Cost Optimization

### Right-Sizing Resources

```bash
# Analyze resource usage
kubectl top nodes
kubectl top pods --all-namespaces

# Get resource requests vs usage
kubectl get pods -n platform-copilot -o json | \
  jq -r '.items[] | "\(.metadata.name): CPU Request: \(.spec.containers[0].resources.requests.cpu // "none"), Memory Request: \(.spec.containers[0].resources.requests.memory // "none")"'
```

### Cluster Autoscaler Optimization

```bash
# Review autoscaler configuration
kubectl get cm cluster-autoscaler-status -n kube-system -o yaml

# Set node pool scale-down delay
az aks nodepool update \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name user \
  --scale-down-delay-after-add 10m
```

### Use Spot Instances

```bash
# Create spot instance node pool
az aks nodepool add \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  --name spot \
  --priority Spot \
  --eviction-policy Delete \
  --spot-max-price -1 \
  --enable-cluster-autoscaler \
  --min-count 1 \
  --max-count 5 \
  --node-vm-size Standard_D4s_v5 \
  --labels workload=batch \
  --node-taints spot=true:NoSchedule
```

## Disaster Recovery

### DR Checklist

- [ ] **Regular backups** with Velero (automated)
- [ ] **Multi-region** ACR geo-replication
- [ ] **Infrastructure as Code** maintained in Git
- [ ] **Disaster recovery plan** documented
- [ ] **Recovery time tested** quarterly
- [ ] **Runbook** for disaster scenarios

### Multi-Region Failover

```bash
# Deploy to secondary region
cd infra/terraform

terraform workspace new prod-westus
terraform apply -var-file=prod-westus.tfvars

# Restore application from backup
velero restore create --from-backup platform-copilot-backup \
  --namespace platform-copilot
```

## Additional Resources

- [AKS Best Practices](https://docs.microsoft.com/en-us/azure/aks/best-practices)
- [Kubernetes Production Best Practices](https://learnk8s.io/production-best-practices)
- [Azure Monitor for Containers](https://docs.microsoft.com/en-us/azure/azure-monitor/containers/container-insights-overview)
- [Velero Documentation](https://velero.io/docs/)
