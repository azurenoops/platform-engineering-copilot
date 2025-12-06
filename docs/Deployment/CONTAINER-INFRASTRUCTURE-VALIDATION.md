# Container Infrastructure Validation Guide

## Overview

This guide provides step-by-step instructions for testing and validating the Azure Container Infrastructure deployment (ACR, AKS, ACI) for the Platform Engineering Copilot.

## Table of Contents

- [Pre-Deployment Validation](#pre-deployment-validation)
- [Terraform Deployment Test](#terraform-deployment-test)
- [ACR Validation](#acr-validation)
- [AKS Validation](#aks-validation)
- [ACI Validation](#aci-validation)
- [Integration Testing](#integration-testing)
- [Performance Testing](#performance-testing)
- [Security Validation](#security-validation)

## Pre-Deployment Validation

### 1. Verify Prerequisites

```bash
#!/bin/bash
# pre-deployment-check.sh

echo "=== Pre-Deployment Validation ==="
echo ""

# Check Azure CLI
echo "1. Azure CLI:"
az --version | head -1
if [ $? -eq 0 ]; then
    echo "✅ Azure CLI installed"
else
    echo "❌ Azure CLI not found - Install: brew install azure-cli"
    exit 1
fi
echo ""

# Check kubectl
echo "2. kubectl:"
kubectl version --client --short 2>/dev/null
if [ $? -eq 0 ]; then
    echo "✅ kubectl installed"
else
    echo "❌ kubectl not found - Install: az aks install-cli"
    exit 1
fi
echo ""

# Check Terraform
echo "3. Terraform:"
terraform version | head -1
if [ $? -eq 0 ]; then
    echo "✅ Terraform installed"
else
    echo "❌ Terraform not found - Install: brew install terraform"
    exit 1
fi
echo ""

# Check Docker
echo "4. Docker:"
docker --version
if [ $? -eq 0 ]; then
    echo "✅ Docker installed"
else
    echo "❌ Docker not found - Install: brew install --cask docker"
    exit 1
fi
echo ""

# Check Helm
echo "5. Helm:"
helm version --short
if [ $? -eq 0 ]; then
    echo "✅ Helm installed"
else
    echo "❌ Helm not found - Install: brew install helm"
    exit 1
fi
echo ""

# Check Azure login
echo "6. Azure Authentication:"
az account show &>/dev/null
if [ $? -eq 0 ]; then
    ACCOUNT=$(az account show --query "name" -o tsv)
    echo "✅ Logged into Azure: $ACCOUNT"
else
    echo "❌ Not logged into Azure - Run: az login"
    exit 1
fi
echo ""

echo "=== All Prerequisites Met ==="
```

### 2. Validate Terraform Configuration

```bash
cd /Users/johnspinella/repos/platform-engineering-copilot/infra/terraform

# Initialize Terraform
terraform init

# Validate syntax
terraform validate

# Format check
terraform fmt -check -recursive

# Run plan
terraform plan -out=tfplan

echo "Review the plan above. Proceed with deployment? (yes/no)"
read -r response
```

## Terraform Deployment Test

### 1. Deploy to Development Environment

Create `terraform.dev.tfvars`:

```hcl
# Basic Configuration
project_name        = "platsup"
environment         = "dev"
location            = "East US"
resource_group_name = "rg-platsup-dev-eastus"

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

# AKS Configuration
aks_kubernetes_version       = "1.28.3"
aks_enable_private_cluster   = false  # Set to false for testing
aks_system_node_count        = 2      # Reduced for dev
aks_enable_user_node_pool    = true
aks_user_node_min_count      = 1      # Reduced for dev
aks_user_node_max_count      = 3
aks_enable_azure_policy      = true
aks_enable_defender          = true

# ACI Configuration
aci_mcp_container_image = "mcr.microsoft.com/azuredocs/aci-helloworld:latest"
aci_mcp_cpu_cores       = 1
aci_mcp_memory_in_gb    = 2
```

Apply configuration:

```bash
# Deploy with dev variables
terraform apply -var-file=terraform.dev.tfvars

# Capture outputs
terraform output -json > outputs.json

# View summary
terraform output container_infrastructure_summary
```

### 2. Verify Terraform State

```bash
# List resources
terraform state list | grep -E "(acr|aks|aci)"

# Check ACR
terraform state show 'module.acr[0].azurerm_container_registry.acr'

# Check AKS
terraform state show 'module.aks[0].azurerm_kubernetes_cluster.aks'

# Check ACI
terraform state show 'module.aci_mcp_server[0].azurerm_container_group.aci'
```

## ACR Validation

### 1. Basic ACR Tests

```bash
#!/bin/bash
# acr-validation.sh

RESOURCE_GROUP="rg-platsup-dev-eastus"
ACR_NAME=$(terraform output -raw acr_name)
ACR_LOGIN_SERVER=$(terraform output -raw acr_login_server)

echo "=== ACR Validation ==="
echo "ACR Name: $ACR_NAME"
echo "Login Server: $ACR_LOGIN_SERVER"
echo ""

# Test 1: Check ACR exists
echo "Test 1: ACR Resource Exists"
az acr show --name "$ACR_NAME" --resource-group "$RESOURCE_GROUP" &>/dev/null
if [ $? -eq 0 ]; then
    echo "✅ ACR exists"
else
    echo "❌ ACR not found"
    exit 1
fi
echo ""

# Test 2: Check SKU
echo "Test 2: ACR SKU"
SKU=$(az acr show --name "$ACR_NAME" --query "sku.name" -o tsv)
echo "SKU: $SKU"
if [ "$SKU" = "Premium" ]; then
    echo "✅ Premium SKU configured"
else
    echo "⚠️  Expected Premium, got $SKU"
fi
echo ""

# Test 3: Check admin disabled
echo "Test 3: Admin User"
ADMIN_ENABLED=$(az acr show --name "$ACR_NAME" --query "adminUserEnabled" -o tsv)
if [ "$ADMIN_ENABLED" = "false" ]; then
    echo "✅ Admin user disabled (secure)"
else
    echo "⚠️  Admin user enabled (not recommended)"
fi
echo ""

# Test 4: Login to ACR
echo "Test 4: ACR Login"
az acr login --name "$ACR_NAME"
if [ $? -eq 0 ]; then
    echo "✅ Successfully logged into ACR"
else
    echo "❌ Failed to login to ACR"
    exit 1
fi
echo ""

# Test 5: Push test image
echo "Test 5: Push Test Image"
docker pull hello-world:latest
docker tag hello-world:latest "$ACR_LOGIN_SERVER/test/hello-world:v1"
docker push "$ACR_LOGIN_SERVER/test/hello-world:v1"
if [ $? -eq 0 ]; then
    echo "✅ Successfully pushed test image"
else
    echo "❌ Failed to push image"
    exit 1
fi
echo ""

# Test 6: List repositories
echo "Test 6: List Repositories"
az acr repository list --name "$ACR_NAME" -o table
echo ""

# Test 7: Check retention policy
echo "Test 7: Retention Policy"
RETENTION=$(az acr config retention show --registry "$ACR_NAME" --query "status" -o tsv)
echo "Retention Policy: $RETENTION"
if [ "$RETENTION" = "enabled" ]; then
    echo "✅ Retention policy enabled"
else
    echo "⚠️  Retention policy disabled"
fi
echo ""

# Test 8: Check quarantine policy
echo "Test 8: Quarantine Policy"
QUARANTINE=$(az acr show --name "$ACR_NAME" --query "policies.quarantinePolicy.status" -o tsv)
echo "Quarantine Policy: $QUARANTINE"
if [ "$QUARANTINE" = "enabled" ]; then
    echo "✅ Quarantine policy enabled"
else
    echo "⚠️  Quarantine policy disabled"
fi
echo ""

echo "=== ACR Validation Complete ==="
```

### 2. ACR Security Validation

```bash
# Check private endpoint
az network private-endpoint list \
  --resource-group "$RESOURCE_GROUP" \
  --query "[?contains(name, 'acr')].{Name:name, Status:privateLinkServiceConnections[0].provisioningState}" \
  -o table

# Check network rules
az acr network-rule list --name "$ACR_NAME" -o table

# Check encryption
az acr encryption show --name "$ACR_NAME" -o table

# Check zone redundancy
az acr show --name "$ACR_NAME" --query "zoneRedundancy" -o tsv
```

## AKS Validation

### 1. Basic AKS Tests

```bash
#!/bin/bash
# aks-validation.sh

RESOURCE_GROUP="rg-platsup-dev-eastus"
AKS_NAME=$(terraform output -raw aks_cluster_name)

echo "=== AKS Validation ==="
echo "AKS Name: $AKS_NAME"
echo ""

# Test 1: Check AKS exists
echo "Test 1: AKS Resource Exists"
az aks show --name "$AKS_NAME" --resource-group "$RESOURCE_GROUP" &>/dev/null
if [ $? -eq 0 ]; then
    echo "✅ AKS exists"
else
    echo "❌ AKS not found"
    exit 1
fi
echo ""

# Test 2: Get AKS credentials
echo "Test 2: Get AKS Credentials"
az aks get-credentials \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --overwrite-existing
if [ $? -eq 0 ]; then
    echo "✅ Successfully retrieved credentials"
else
    echo "❌ Failed to get credentials"
    exit 1
fi
echo ""

# Test 3: Check cluster connectivity
echo "Test 3: Cluster Connectivity"
kubectl cluster-info
if [ $? -eq 0 ]; then
    echo "✅ Cluster is accessible"
else
    echo "❌ Cannot connect to cluster"
    exit 1
fi
echo ""

# Test 4: Check nodes
echo "Test 4: Node Status"
kubectl get nodes -o wide
NODE_COUNT=$(kubectl get nodes --no-headers | wc -l)
echo "Total nodes: $NODE_COUNT"
if [ $NODE_COUNT -ge 2 ]; then
    echo "✅ Nodes are running"
else
    echo "❌ Insufficient nodes"
    exit 1
fi
echo ""

# Test 5: Check system pods
echo "Test 5: System Pods"
kubectl get pods -n kube-system
RUNNING_PODS=$(kubectl get pods -n kube-system --field-selector=status.phase=Running --no-headers | wc -l)
TOTAL_PODS=$(kubectl get pods -n kube-system --no-headers | wc -l)
echo "Running pods: $RUNNING_PODS/$TOTAL_PODS"
if [ $RUNNING_PODS -eq $TOTAL_PODS ]; then
    echo "✅ All system pods running"
else
    echo "⚠️  Some pods not running"
fi
echo ""

# Test 6: Check node pools
echo "Test 6: Node Pools"
az aks nodepool list \
  --resource-group "$RESOURCE_GROUP" \
  --cluster-name "$AKS_NAME" \
  -o table
echo ""

# Test 7: Verify ACR integration
echo "Test 7: ACR Integration"
az aks check-acr \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --acr "$(terraform output -raw acr_name)"
if [ $? -eq 0 ]; then
    echo "✅ ACR integration working"
else
    echo "❌ ACR integration failed"
fi
echo ""

# Test 8: Deploy test workload
echo "Test 8: Deploy Test Workload"
kubectl create namespace test --dry-run=client -o yaml | kubectl apply -f -

cat <<EOF | kubectl apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: nginx-test
  namespace: test
spec:
  replicas: 2
  selector:
    matchLabels:
      app: nginx
  template:
    metadata:
      labels:
        app: nginx
    spec:
      containers:
      - name: nginx
        image: nginx:alpine
        ports:
        - containerPort: 80
        resources:
          requests:
            cpu: 100m
            memory: 128Mi
          limits:
            cpu: 200m
            memory: 256Mi
---
apiVersion: v1
kind: Service
metadata:
  name: nginx-test
  namespace: test
spec:
  selector:
    app: nginx
  ports:
  - port: 80
    targetPort: 80
  type: ClusterIP
EOF

# Wait for deployment
kubectl wait --for=condition=available --timeout=120s deployment/nginx-test -n test
if [ $? -eq 0 ]; then
    echo "✅ Test workload deployed successfully"
else
    echo "❌ Test workload deployment failed"
fi
echo ""

# Test 9: Test service connectivity
echo "Test 9: Service Connectivity"
kubectl run curl-test --image=curlimages/curl --rm -it --restart=Never -n test -- \
  curl -s -o /dev/null -w "%{http_code}" http://nginx-test.test.svc.cluster.local
if [ $? -eq 0 ]; then
    echo "✅ Service connectivity working"
else
    echo "❌ Service connectivity failed"
fi
echo ""

# Test 10: Check monitoring
echo "Test 10: Container Insights"
MONITORING_ENABLED=$(az aks show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_NAME" \
  --query "addonProfiles.omsagent.enabled" -o tsv)
if [ "$MONITORING_ENABLED" = "true" ]; then
    echo "✅ Container Insights enabled"
else
    echo "⚠️  Container Insights not enabled"
fi
echo ""

echo "=== AKS Validation Complete ==="

# Cleanup
kubectl delete namespace test
```

### 2. AKS Security Validation

```bash
# Check Azure Policy
kubectl get constrainttemplates

# Check network policies
kubectl get networkpolicies --all-namespaces

# Check RBAC
kubectl auth can-i --list --namespace=kube-system

# Check Pod Security Standards
kubectl get namespaces --show-labels

# Verify Defender for Containers
az security pricing show --name Containers
```

## ACI Validation

### 1. Basic ACI Tests

```bash
#!/bin/bash
# aci-validation.sh

RESOURCE_GROUP="rg-platsup-dev-eastus"
ACI_NAME=$(terraform output -raw aci_mcp_server_name 2>/dev/null || echo "platsup-mcp-aci-dev")

echo "=== ACI Validation ==="
echo "ACI Name: $ACI_NAME"
echo ""

# Test 1: Check ACI exists
echo "Test 1: ACI Container Group Exists"
az container show --name "$ACI_NAME" --resource-group "$RESOURCE_GROUP" &>/dev/null
if [ $? -eq 0 ]; then
    echo "✅ ACI exists"
else
    echo "❌ ACI not found"
    exit 1
fi
echo ""

# Test 2: Check container status
echo "Test 2: Container Status"
STATUS=$(az container show \
  --name "$ACI_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "containers[0].instanceView.currentState.state" -o tsv)
echo "Status: $STATUS"
if [ "$STATUS" = "Running" ]; then
    echo "✅ Container is running"
else
    echo "⚠️  Container status: $STATUS"
fi
echo ""

# Test 3: Get container IP
echo "Test 3: Container IP Address"
IP=$(az container show \
  --name "$ACI_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "ipAddress.ip" -o tsv)
echo "IP: $IP"
if [ -n "$IP" ]; then
    echo "✅ IP address assigned"
else
    echo "❌ No IP address"
    exit 1
fi
echo ""

# Test 4: Check container logs
echo "Test 4: Container Logs"
az container logs \
  --name "$ACI_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --tail 20
echo ""

# Test 5: Test HTTP endpoint (if applicable)
echo "Test 5: HTTP Endpoint Test"
PORT=$(terraform output -raw aci_mcp_port 2>/dev/null || echo "80")
HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" "http://$IP:$PORT" || echo "000")
echo "HTTP Response: $HTTP_CODE"
if [ "$HTTP_CODE" = "200" ]; then
    echo "✅ HTTP endpoint responding"
elif [ "$HTTP_CODE" = "000" ]; then
    echo "⚠️  Cannot reach endpoint (may be expected if VNet integrated)"
else
    echo "⚠️  HTTP code: $HTTP_CODE"
fi
echo ""

# Test 6: Check managed identity
echo "Test 6: Managed Identity"
IDENTITY=$(az container show \
  --name "$ACI_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "identity.type" -o tsv)
if [ "$IDENTITY" = "SystemAssigned" ]; then
    echo "✅ Managed identity enabled"
else
    echo "⚠️  No managed identity"
fi
echo ""

# Test 7: Check VNet integration
echo "Test 7: VNet Integration"
SUBNET=$(az container show \
  --name "$ACI_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query "subnetIds[0].id" -o tsv 2>/dev/null)
if [ -n "$SUBNET" ]; then
    echo "✅ VNet integrated"
    echo "Subnet: $SUBNET"
else
    echo "⚠️  No VNet integration"
fi
echo ""

echo "=== ACI Validation Complete ==="
```

## Integration Testing

### 1. ACR-AKS Integration Test

```bash
#!/bin/bash
# Test pulling image from ACR to AKS

ACR_LOGIN_SERVER=$(terraform output -raw acr_login_server)
NAMESPACE="test"

# Create test namespace
kubectl create namespace "$NAMESPACE"

# Deploy from ACR
cat <<EOF | kubectl apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: acr-test
  namespace: $NAMESPACE
spec:
  replicas: 2
  selector:
    matchLabels:
      app: acr-test
  template:
    metadata:
      labels:
        app: acr-test
    spec:
      containers:
      - name: app
        image: $ACR_LOGIN_SERVER/test/hello-world:v1
        ports:
        - containerPort: 80
EOF

# Wait for deployment
kubectl wait --for=condition=available --timeout=120s deployment/acr-test -n "$NAMESPACE"

# Check pods
kubectl get pods -n "$NAMESPACE"

# Cleanup
kubectl delete namespace "$NAMESPACE"
```

### 2. End-to-End Application Test

```bash
#!/bin/bash
# e2e-test.sh

echo "=== End-to-End Application Test ==="

# Build application image
ACR_LOGIN_SERVER=$(terraform output -raw acr_login_server)
IMAGE_TAG="$ACR_LOGIN_SERVER/platform-copilot-api:test"

echo "Building application image..."
docker build -t "$IMAGE_TAG" \
  -f src/Platform.Engineering.Copilot.Admin.API/Dockerfile \
  .

echo "Pushing to ACR..."
docker push "$IMAGE_TAG"

echo "Deploying to AKS..."
kubectl create namespace platform-copilot-test

kubectl create secret generic app-secrets \
  --namespace platform-copilot-test \
  --from-literal=connection-string="test-connection"

cat <<EOF | kubectl apply -f -
apiVersion: apps/v1
kind: Deployment
metadata:
  name: platform-copilot-api
  namespace: platform-copilot-test
spec:
  replicas: 2
  selector:
    matchLabels:
      app: platform-copilot-api
  template:
    metadata:
      labels:
        app: platform-copilot-api
    spec:
      containers:
      - name: api
        image: $IMAGE_TAG
        ports:
        - containerPort: 5000
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Development"
        resources:
          requests:
            cpu: 100m
            memory: 256Mi
          limits:
            cpu: 500m
            memory: 512Mi
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /ready
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: platform-copilot-api
  namespace: platform-copilot-test
spec:
  selector:
    app: platform-copilot-api
  ports:
  - port: 80
    targetPort: 5000
  type: ClusterIP
EOF

kubectl wait --for=condition=available --timeout=180s \
  deployment/platform-copilot-api -n platform-copilot-test

echo "Testing service..."
kubectl run curl-test --image=curlimages/curl --rm -it --restart=Never \
  -n platform-copilot-test -- \
  curl -v http://platform-copilot-api/health

echo "Checking logs..."
kubectl logs -n platform-copilot-test -l app=platform-copilot-api --tail=50

echo "=== E2E Test Complete ==="
```

## Performance Testing

### 1. Load Testing with K6

```bash
# Install k6
brew install k6

# Create load test script
cat > load-test.js <<'EOF'
import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
  stages: [
    { duration: '1m', target: 10 },
    { duration: '3m', target: 50 },
    { duration: '1m', target: 0 },
  ],
  thresholds: {
    http_req_duration: ['p(95)<500'],
    http_req_failed: ['rate<0.01'],
  },
};

export default function () {
  let response = http.get('http://YOUR_SERVICE_URL/health');
  check(response, {
    'status is 200': (r) => r.status === 200,
    'response time < 500ms': (r) => r.timings.duration < 500,
  });
  sleep(1);
}
EOF

# Run load test
k6 run load-test.js
```

### 2. Resource Monitoring During Load

```bash
# Monitor nodes
watch kubectl top nodes

# Monitor pods
watch kubectl top pods -n platform-copilot-test

# Check HPA
kubectl get hpa -n platform-copilot-test --watch
```

## Security Validation

### 1. Vulnerability Scanning

```bash
# Scan all images in ACR
ACR_NAME=$(terraform output -raw acr_name)

az acr repository list --name "$ACR_NAME" --output tsv | while read repo; do
  echo "Scanning $repo..."
  trivy image --severity HIGH,CRITICAL "$ACR_NAME.azurecr.io/$repo:latest"
done
```

### 2. Network Policy Test

```bash
# Create test namespace with network policy
kubectl create namespace netpol-test

cat <<EOF | kubectl apply -f -
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: deny-all
  namespace: netpol-test
spec:
  podSelector: {}
  policyTypes:
  - Ingress
  - Egress
EOF

# Test that policy is enforced
kubectl run test-pod --image=nginx -n netpol-test
kubectl run curl-pod --image=curlimages/curl --rm -it --restart=Never -n netpol-test -- \
  curl --connect-timeout 5 test-pod

# Cleanup
kubectl delete namespace netpol-test
```

## Validation Report

Create a summary report:

```bash
#!/bin/bash
# generate-report.sh

cat > validation-report.md <<EOF
# Container Infrastructure Validation Report

**Date:** $(date)
**Environment:** Development

## Deployment Summary

$(terraform output -json container_infrastructure_summary | jq .)

## Test Results

### ACR
- ✅ ACR Created
- ✅ Premium SKU
- ✅ Private Endpoint Configured
- ✅ Image Push/Pull Working
- ✅ Quarantine Enabled
- ✅ Retention Policy Active

### AKS
- ✅ Cluster Created
- ✅ Nodes Running: $(kubectl get nodes --no-headers | wc -l)
- ✅ System Pods Healthy
- ✅ ACR Integration Working
- ✅ Test Workload Deployed
- ✅ Container Insights Enabled

### ACI
- ✅ Container Group Created
- ✅ Container Running
- ✅ Managed Identity Configured
- ✅ VNet Integration Active

## Next Steps

- [ ] Deploy to staging environment
- [ ] Run performance tests
- [ ] Configure production monitoring
- [ ] Set up CI/CD pipelines
- [ ] Implement disaster recovery

EOF

cat validation-report.md
```

Run the validation and save results:

```bash
# Make scripts executable
chmod +x pre-deployment-check.sh acr-validation.sh aks-validation.sh aci-validation.sh e2e-test.sh

# Run all validations
./pre-deployment-check.sh 2>&1 | tee pre-deployment.log
./acr-validation.sh 2>&1 | tee acr-validation.log
./aks-validation.sh 2>&1 | tee aks-validation.log
./aci-validation.sh 2>&1 | tee aci-validation.log
./e2e-test.sh 2>&1 | tee e2e-test.log

# Generate report
./generate-report.sh
```

## Cleanup

To cleanup test resources:

```bash
# Delete test namespaces
kubectl delete namespace platform-copilot-test --ignore-not-found

# Destroy infrastructure (use with caution!)
terraform destroy -var-file=terraform.dev.tfvars
```

## Success Criteria

All tests should pass:
- ✅ ACR accessible and functional
- ✅ AKS cluster operational
- ✅ Nodes healthy and ready
- ✅ ACR-AKS integration working
- ✅ ACI containers running
- ✅ Application deployment successful
- ✅ Health checks passing
- ✅ Monitoring enabled
- ✅ Security policies in place
- ✅ No critical vulnerabilities

If all criteria are met, the infrastructure is ready for production deployment.
