# Kubernetes Manifests

This directory contains Kubernetes manifests for deploying the Platform Engineering Copilot to Azure Kubernetes Service (AKS).

## Overview

The manifests are organized to support:
- **Namespace isolation** for multi-tenancy
- **Azure Workload Identity** for passwordless authentication
- **Network Policies** for IL5/IL6 compliance
- **Pod Security Standards** for container security
- **Auto-scaling** with HPA for high availability
- **Ingress** with Application Gateway or NGINX

## Directory Structure

```
kubernetes/
├── namespace.yaml              # Namespace definitions
├── workload-identity.yaml      # Azure Workload Identity service account
├── configmap.yaml              # Application configuration
├── secret.yaml                 # Secrets (template - use Key Vault)
├── mcp-deployment.yaml         # MCP server deployment + service + HPA
├── chat-deployment.yaml        # Chat server deployment + service + HPA
├── admin-api-deployment.yaml   # Admin API deployment + service + HPA
├── admin-client-deployment.yaml # Admin client deployment + service + HPA
├── ingress.yaml                # Ingress configuration (AGIC + NGINX)
├── network-policy.yaml         # Network policies for micro-segmentation
├── pod-security.yaml           # Pod security standards
├── kustomization.yaml          # Kustomize configuration
└── README.md                   # This file
```

## Prerequisites

1. **AKS Cluster** with:
   - Workload Identity enabled
   - Azure Key Vault Provider for Secrets Store CSI Driver
   - Azure Policy add-on
   - Application Gateway Ingress Controller (AGIC) or NGINX Ingress Controller

2. **Azure Resources**:
   - Azure Container Registry (ACR)
   - Azure SQL Database
   - Azure Redis Cache
   - Azure Storage Account
   - Azure Key Vault
   - Application Insights

3. **kubectl** configured to connect to your AKS cluster:
   ```bash
   az aks get-credentials --resource-group <rg-name> --name <aks-name>
   ```

4. **Environment Variables** (replace placeholders):
   - `${ACR_LOGIN_SERVER}` - ACR login server (e.g., myacr.azurecr.io)
   - `${IMAGE_TAG}` - Docker image tag (e.g., latest, v1.0.0)
   - `${AZURE_TENANT_ID}` - Azure AD tenant ID
   - `${AZURE_SUBSCRIPTION_ID}` - Azure subscription ID
   - `${MANAGED_IDENTITY_CLIENT_ID}` - Managed identity client ID
   - `${KEY_VAULT_NAME}` - Azure Key Vault name
   - `${DOMAIN_NAME}` - Your domain name
   - `${SQL_SERVER_FQDN}` - Azure SQL server FQDN
   - `${SQL_DATABASE_NAME}` - SQL database name

## Deployment Steps

### 1. Create Namespaces
```bash
kubectl apply -f namespace.yaml
```

### 2. Configure Workload Identity

Create federated identity credential:
```bash
# Get OIDC issuer URL
AKS_OIDC_ISSUER=$(az aks show --resource-group <rg-name> --name <aks-name> --query "oidcIssuerProfile.issuerUrl" -o tsv)

# Create federated credential
az identity federated-credential create \
  --name platform-engineering-federated-credential \
  --identity-name <managed-identity-name> \
  --resource-group <rg-name> \
  --issuer $AKS_OIDC_ISSUER \
  --subject system:serviceaccount:platform-engineering:platform-workload-identity-sa \
  --audience api://AzureADTokenExchange
```

Apply workload identity:
```bash
kubectl apply -f workload-identity.yaml
```

### 3. Configure Secrets

**Option A: Using Azure Key Vault (Recommended)**
```bash
# Install Key Vault Provider
helm repo add csi-secrets-store-provider-azure https://azure.github.io/secrets-store-csi-driver-provider-azure/charts
helm install csi csi-secrets-store-provider-azure/csi-secrets-store-provider-azure --namespace kube-system

# Apply secret provider class
kubectl apply -f secret.yaml
```

**Option B: Using Kubernetes Secrets**
```bash
# Create ACR pull secret
kubectl create secret docker-registry acr-secret \
  --docker-server=<acr-login-server> \
  --docker-username=<acr-username> \
  --docker-password=<acr-password> \
  --namespace=platform-engineering

# Create SQL secret
kubectl create secret generic sql-secret \
  --from-literal=username=<sql-admin-username> \
  --from-literal=password=<sql-admin-password> \
  --namespace=platform-engineering
```

### 4. Apply ConfigMaps
```bash
# Edit configmap.yaml with your values first
kubectl apply -f configmap.yaml
```

### 5. Deploy Applications

**Deploy all at once:**
```bash
kubectl apply -f mcp-deployment.yaml
kubectl apply -f chat-deployment.yaml
kubectl apply -f admin-api-deployment.yaml
kubectl apply -f admin-client-deployment.yaml
```

**Or deploy individually:**
```bash
kubectl apply -f mcp-deployment.yaml
kubectl wait --for=condition=available --timeout=300s deployment/mcp-server -n platform-engineering

kubectl apply -f admin-api-deployment.yaml
kubectl wait --for=condition=available --timeout=300s deployment/admin-api -n platform-engineering

kubectl apply -f chat-deployment.yaml
kubectl wait --for=condition=available --timeout=300s deployment/chat-server -n platform-engineering

kubectl apply -f admin-client-deployment.yaml
kubectl wait --for=condition=available --timeout=300s deployment/admin-client -n platform-engineering
```

### 6. Apply Network Policies
```bash
kubectl apply -f network-policy.yaml
kubectl apply -f pod-security.yaml
```

### 7. Configure Ingress

**For Application Gateway Ingress Controller:**
```bash
# Edit ingress.yaml to uncomment AGIC section and update values
kubectl apply -f ingress.yaml
```

**For NGINX Ingress Controller:**
```bash
# Install NGINX Ingress Controller
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx --create-namespace \
  --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-health-probe-request-path"=/healthz

# Apply NGINX ingress
kubectl apply -f ingress.yaml
```

### 8. Using Kustomize (Alternative)

```bash
# Deploy using kustomize
kubectl apply -k .

# Or with custom overlays
kubectl apply -k overlays/dev
kubectl apply -k overlays/prod
```

## Verification

Check deployment status:
```bash
kubectl get all -n platform-engineering
kubectl get ingress -n platform-engineering
kubectl get networkpolicy -n platform-engineering
```

Check pod logs:
```bash
kubectl logs -n platform-engineering -l app=mcp-server --tail=50
kubectl logs -n platform-engineering -l app=chat-server --tail=50
kubectl logs -n platform-engineering -l app=admin-api --tail=50
kubectl logs -n platform-engineering -l app=admin-client --tail=50
```

Check HPA status:
```bash
kubectl get hpa -n platform-engineering
```

Check workload identity:
```bash
kubectl describe sa platform-workload-identity-sa -n platform-engineering
```

## Troubleshooting

### Pods not pulling images
```bash
# Check image pull secrets
kubectl get secrets -n platform-engineering
kubectl describe secret acr-secret -n platform-engineering

# Verify ACR credentials
az acr login --name <acr-name>
```

### Workload Identity not working
```bash
# Verify federated credential
az identity federated-credential list --identity-name <identity-name> --resource-group <rg-name>

# Check pod annotations
kubectl describe pod -n platform-engineering -l azure.workload.identity/use=true
```

### Network connectivity issues
```bash
# Check network policies
kubectl describe networkpolicy -n platform-engineering

# Test pod-to-pod connectivity
kubectl run test-pod -n platform-engineering --image=busybox --rm -it -- wget -O- http://mcp-service:5000/health/ready
```

### Ingress not working
```bash
# Check ingress controller logs
kubectl logs -n ingress-nginx -l app.kubernetes.io/component=controller

# Verify ingress configuration
kubectl describe ingress platform-ingress -n platform-engineering
```

## Security Considerations

1. **Always use Azure Key Vault** for sensitive data (connection strings, passwords)
2. **Enable Pod Security Standards** at the namespace level
3. **Apply Network Policies** to restrict pod-to-pod communication
4. **Use Workload Identity** instead of service principals or passwords
5. **Run containers as non-root** with read-only root filesystem
6. **Set resource limits** to prevent resource exhaustion
7. **Enable Azure Policy** for governance and compliance
8. **Use private endpoints** for Azure services (SQL, Storage, Key Vault)

## Scaling

### Manual scaling
```bash
kubectl scale deployment mcp-server --replicas=5 -n platform-engineering
kubectl scale deployment chat-server --replicas=10 -n platform-engineering
```

### Auto-scaling (HPA)
Auto-scaling is configured via HPA resources in each deployment file:
- Min replicas: 3
- Max replicas: 10-20 (depending on service)
- Target CPU: 70%
- Target Memory: 80%

### Cluster auto-scaling
Enable cluster autoscaler in AKS:
```bash
az aks update \
  --resource-group <rg-name> \
  --name <aks-name> \
  --enable-cluster-autoscaler \
  --min-count 3 \
  --max-count 10
```

## Monitoring

View metrics:
```bash
kubectl top nodes
kubectl top pods -n platform-engineering
```

View Application Insights:
- Navigate to Azure Portal → Application Insights
- View Live Metrics, Performance, Failures

## Cleanup

Remove all resources:
```bash
kubectl delete namespace platform-engineering
```

Or remove individual resources:
```bash
kubectl delete -f .
```

## Related Documentation

- [AKS Deployment Guide](../../docs/AKS-DEPLOYMENT-GUIDE.md) - Coming soon
- [Kubernetes Operations Guide](../../docs/KUBERNETES-GUIDE.md) - Coming soon
- [Container Build Guide](../../docs/CONTAINER-BUILD-GUIDE.md) - Coming soon
- [Container Infrastructure Status](../../CONTAINER-INFRASTRUCTURE-STATUS.md)
