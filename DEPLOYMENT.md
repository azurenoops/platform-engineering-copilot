# Deployment Guide

This guide covers all deployment options for the Platform Engineering Copilot, including Docker, Kubernetes, and cloud deployments.

## 📋 Table of Contents

1. [Docker Deployment](#docker-deployment)
2. [Kubernetes Deployment](#kubernetes-deployment)
3. [Azure Deployment](#azure-deployment)
4. [Production Configuration](#production-configuration)
5. [Monitoring & Observability](#monitoring--observability)
6. [Security Considerations](#security-considerations)

---

## 🐳 Docker Deployment

### Architecture Overview

The Docker setup includes the following services:

- **Platform API**: ASP.NET Core Web API (Port 7001)
- **Chat Service**: SignalR Chat Interface (Port 5000)
- **SQL Server**: Microsoft SQL Server 2022 (Port 1433)
- **Redis**: Caching service (Port 6379) - Optional
- **Nginx**: Reverse proxy and load balancer (Port 80) - Optional
- **Adminer**: Database administration tool (Port 8081) - Development only

### Quick Docker Start

#### Prerequisites

- Docker Desktop installed and running
- Git (to clone the repository)
- At least 4GB RAM available for Docker

#### 1. Clone and Setup

```bash
git clone https://github.com/jrspinella/platform-mcp-Platform.Engineering.Copilot.git
cd platform-mcp-supervisor

# Copy environment template
cp .env.example .env
```

#### 2. Configure Environment

Edit `.env` file:

```bash
# Azure Configuration
AZURE_SUBSCRIPTION_ID=your-subscription-id
AZURE_TENANT_ID=your-tenant-id
AZURE_ENVIRONMENT=AzureUSGovernment

# Database
SA_PASSWORD=YourStrong@Passw0rd
CONNECTION_STRING=Server=sqlserver,1433;Database=PlatformSupervisor;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=true;

# Application URLs
API_BASE_URL=http://localhost:7001
CHAT_BASE_URL=http://localhost:5000

# Optional: Azure OpenAI
AZURE_OPENAI_ENDPOINT=your-openai-endpoint
AZURE_OPENAI_API_KEY=your-api-key
```

#### 3. Start Services

```bash
# Development environment
docker-compose up -d

# Production environment  
docker-compose -f docker-compose.prod.yml up -d

# Development with live reload
docker-compose -f docker-compose.dev.yml up -d
```

#### 4. Verify Deployment

```bash
# Check service status
docker-compose ps

# View logs
docker-compose logs -f platform-api
docker-compose logs -f chat-service

# Test endpoints
curl http://localhost:7001/health
curl http://localhost:5000/health
```

### Docker Compose Configurations

#### Production (`docker-compose.prod.yml`)

```yaml
version: '3.8'

services:
  platform-api:
    build:
      context: .
      dockerfile: Dockerfile.api
    ports:
      - "7001:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=${CONNECTION_STRING}
      - Azure__SubscriptionId=${AZURE_SUBSCRIPTION_ID}
      - Azure__TenantId=${AZURE_TENANT_ID}
      - Azure__CloudEnvironment=${AZURE_ENVIRONMENT}
    depends_on:
      - sqlserver
      - redis
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost/health"]
      interval: 30s
      timeout: 10s
      retries: 3

  chat-service:
    build:
      context: .
      dockerfile: Dockerfile.chat
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - PlatformServices__ApiBaseUrl=http://platform-api
    depends_on:
      - platform-api
    restart: unless-stopped

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=${SA_PASSWORD}
    ports:
      - "1433:1433"
    volumes:
      - sqlserver_data:/var/opt/mssql
    restart: unless-stopped

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis_data:/data
    restart: unless-stopped
    command: redis-server --appendonly yes

  nginx:
    image: nginx:alpine
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./ssl:/etc/nginx/ssl:ro
    depends_on:
      - platform-api
      - chat-service
    restart: unless-stopped

volumes:
  sqlserver_data:
  redis_data:
```

#### Development (`docker-compose.dev.yml`)

```yaml
version: '3.8'

services:
  platform-api:
    build:
      context: .
      dockerfile: Dockerfile.api
      target: development
    ports:
      - "7001:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=${CONNECTION_STRING}
    volumes:
      - ./src/Platform.Engineering.Copilot.API:/app/src/Platform.Engineering.Copilot.API
      - ./src/Platform.Engineering.Copilot.Core:/app/src/Platform.Engineering.Copilot.Core
    depends_on:
      - sqlserver

  chat-service:
    build:
      context: .
      dockerfile: Dockerfile.chat
      target: development
    ports:
      - "5000:80"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - PlatformServices__ApiBaseUrl=http://platform-api
    volumes:
      - ./src/Platform.Engineering.Copilot.Chat:/app/src/Platform.Engineering.Copilot.Chat

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=${SA_PASSWORD}
    ports:
      - "1433:1433"

  adminer:
    image: adminer
    ports:
      - "8081:8080"
    depends_on:
      - sqlserver
```

### Docker Management Commands

```bash
# View running containers
docker-compose ps

# View logs
docker-compose logs -f [service-name]

# Restart specific service
docker-compose restart platform-api

# Update and restart services
docker-compose pull
docker-compose up -d

# Stop all services
docker-compose down

# Stop and remove volumes (destructive)
docker-compose down -v

# Build specific service
docker-compose build platform-api

# Scale services
docker-compose up -d --scale platform-api=3
```

---

## ☸️ Kubernetes Deployment

### Prerequisites

- Kubernetes cluster (AKS, EKS, GKE, or local)
- kubectl configured
- Helm 3.x installed

### 1. Namespace Setup

```yaml
# namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: platform-supervisor
  labels:
    name: platform-supervisor
```

```bash
kubectl apply -f namespace.yaml
```

### 2. Secrets Configuration

```bash
# Create secrets
kubectl create secret generic azure-config \
  --from-literal=subscription-id=$AZURE_SUBSCRIPTION_ID \
  --from-literal=tenant-id=$AZURE_TENANT_ID \
  --from-literal=environment=$AZURE_ENVIRONMENT \
  -n platform-supervisor

kubectl create secret generic database-config \
  --from-literal=connection-string=$CONNECTION_STRING \
  -n platform-supervisor
```

### 3. Deploy with Helm

```bash
# Add Helm repository (if you publish to a Helm repo)
helm repo add platform-supervisor https://your-helm-repo.com
helm repo update

# Install with Helm
helm install platform-supervisor ./helm/platform-supervisor \
  --namespace platform-supervisor \
  --values values.prod.yaml
```

### 4. Manual Kubernetes Deployment

#### Platform API Deployment

```yaml
# platform-api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: platform-api
  namespace: platform-supervisor
spec:
  replicas: 3
  selector:
    matchLabels:
      app: platform-api
  template:
    metadata:
      labels:
        app: platform-api
    spec:
      containers:
      - name: platform-api
        image: your-registry/platform-api:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: database-config
              key: connection-string
        - name: Azure__SubscriptionId
          valueFrom:
            secretKeyRef:
              name: azure-config
              key: subscription-id
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 80
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5

---
apiVersion: v1
kind: Service
metadata:
  name: platform-api-service
  namespace: platform-supervisor
spec:
  selector:
    app: platform-api
  ports:
  - port: 80
    targetPort: 80
  type: ClusterIP
```

#### Ingress Configuration

```yaml
# ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: platform-supervisor-ingress
  namespace: platform-supervisor
  annotations:
    nginx.ingress.kubernetes.io/rewrite-target: /
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - platform.yourdomain.com
    secretName: platform-supervisor-tls
  rules:
  - host: platform.yourdomain.com
    http:
      paths:
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: platform-api-service
            port:
              number: 80
      - path: /
        pathType: Prefix
        backend:
          service:
            name: chat-service
            port:
              number: 80
```

---

## ☁️ Azure Deployment

### Azure Container Instances (ACI)

```bash
# Create resource group
az group create --name platform-supervisor-rg --location usgovvirginia

# Deploy to ACI
az container create \
  --resource-group platform-supervisor-rg \
  --name platform-supervisor \
  --image your-registry/platform-api:latest \
  --ports 7001 5000 \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    Azure__SubscriptionId=$AZURE_SUBSCRIPTION_ID \
  --secure-environment-variables \
    ConnectionStrings__DefaultConnection=$CONNECTION_STRING \
  --cpu 2 \
  --memory 4
```

### Azure Kubernetes Service (AKS)

```bash
# Create AKS cluster
az aks create \
  --resource-group platform-supervisor-rg \
  --name platform-supervisor-aks \
  --node-count 3 \
  --node-vm-size Standard_D2s_v3 \
  --enable-addons monitoring \
  --generate-ssh-keys

# Get credentials
az aks get-credentials --resource-group platform-supervisor-rg --name platform-supervisor-aks

# Deploy to AKS
kubectl apply -f k8s/
```

### Azure Container Apps

```bash
# Create Container Apps environment
az containerapp env create \
  --name platform-supervisor-env \
  --resource-group platform-supervisor-rg \
  --location usgovvirginia

# Deploy API service
az containerapp create \
  --name platform-api \
  --resource-group platform-supervisor-rg \
  --environment platform-supervisor-env \
  --image your-registry/platform-api:latest \
  --target-port 80 \
  --ingress external \
  --min-replicas 1 \
  --max-replicas 10 \
  --cpu 1.0 \
  --memory 2Gi
```

---

## 🔧 Production Configuration

### Environment Variables

```bash
# Production environment variables
export ASPNETCORE_ENVIRONMENT=Production
export AZURE_SUBSCRIPTION_ID=your-subscription-id
export AZURE_TENANT_ID=your-tenant-id
export AZURE_ENVIRONMENT=AzureUSGovernment
export CONNECTION_STRING="Server=prod-sql-server;Database=PlatformSupervisor;..."

# Security
export CORS_ORIGINS="https://yourdomain.com,https://api.yourdomain.com"
export JWT_SECRET=your-jwt-secret
export ENCRYPTION_KEY=your-encryption-key

# Performance
export REDIS_CONNECTION_STRING=your-redis-connection
export MAX_CONCURRENT_REQUESTS=100
export REQUEST_TIMEOUT_SECONDS=30
```

### Application Configuration

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Azure": {
    "SubscriptionId": "#{AZURE_SUBSCRIPTION_ID}#",
    "TenantId": "#{AZURE_TENANT_ID}#",
    "CloudEnvironment": "AzureUSGovernment"
  },
  "PlatformServices": {
    "ApiBaseUrl": "https://api.yourdomain.com",
    "EnableCaching": true,
    "CacheExpirationMinutes": 15
  },
  "Security": {
    "EnableHttpsRedirection": true,
    "EnableHsts": true,
    "RequireHttps": true
  },
  "Performance": {
    "MaxConcurrentRequests": 100,
    "RequestTimeoutSeconds": 30,
    "EnableCompression": true
  }
}
```

---

## 📊 Monitoring & Observability

### Application Insights Integration

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=your-key;IngestionEndpoint=..."
  },
  "Logging": {
    "ApplicationInsights": {
      "LogLevel": {
        "Default": "Information"
      }
    }
  }
}
```

### Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddSqlServer(connectionString)
    .AddRedis(redisConnectionString)
    .AddAzureKeyVault(options => { ... });

app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

### Prometheus Metrics

```yaml
# prometheus-config.yaml
global:
  scrape_interval: 15s

scrape_configs:
  - job_name: 'platform-supervisor'
    static_configs:
      - targets: ['platform-api:80']
    metrics_path: /metrics
    scrape_interval: 5s
```

---

## 🔒 Security Considerations

### HTTPS Configuration

```nginx
# nginx.conf for production
server {
    listen 443 ssl http2;
    server_name yourdomain.com;
    
    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;
    
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_ciphers ECDHE-RSA-AES128-GCM-SHA256:ECDHE-RSA-AES256-GCM-SHA384;
    ssl_prefer_server_ciphers off;
    
    add_header Strict-Transport-Security "max-age=63072000" always;
    add_header X-Frame-Options DENY;
    add_header X-Content-Type-Options nosniff;
    
    location / {
        proxy_pass http://chat-service:80;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
    
    location /api {
        proxy_pass http://platform-api:80;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
```

### Secrets Management

```bash
# Using Azure Key Vault
az keyvault create --name platform-supervisor-kv --resource-group platform-supervisor-rg

# Store secrets
az keyvault secret set --vault-name platform-supervisor-kv --name "ConnectionString" --value "$CONNECTION_STRING"
az keyvault secret set --vault-name platform-supervisor-kv --name "AzureSubscriptionId" --value "$AZURE_SUBSCRIPTION_ID"
```

### Network Security

```yaml
# network-policy.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: platform-supervisor-network-policy
  namespace: platform-supervisor
spec:
  podSelector:
    matchLabels:
      app: platform-api
  policyTypes:
  - Ingress
  - Egress
  ingress:
  - from:
    - podSelector:
        matchLabels:
          app: chat-service
    ports:
    - protocol: TCP
      port: 80
  egress:
  - to: []
    ports:
    - protocol: TCP
      port: 443  # HTTPS
    - protocol: TCP
      port: 1433 # SQL Server
```

---

## 🚀 Deployment Automation

### GitHub Actions

```yaml
# .github/workflows/deploy.yml
name: Deploy to Production

on:
  push:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Login to Azure
      uses: azure/login@v1
      with:
        creds: ${{ secrets.AZURE_CREDENTIALS }}
    
    - name: Build and push Docker images
      run: |
        docker build -t ${{ secrets.REGISTRY_URL }}/platform-api:${{ github.sha }} -f Dockerfile.api .
        docker push ${{ secrets.REGISTRY_URL }}/platform-api:${{ github.sha }}
    
    - name: Deploy to AKS
      run: |
        az aks get-credentials --resource-group platform-supervisor-rg --name platform-supervisor-aks
        kubectl set image deployment/platform-api platform-api=${{ secrets.REGISTRY_URL }}/platform-api:${{ github.sha }}
```

### Azure DevOps Pipeline

```yaml
# azure-pipelines.yml
trigger:
- main

pool:
  vmImage: 'ubuntu-latest'

variables:
  buildConfiguration: 'Release'

stages:
- stage: Build
  jobs:
  - job: Build
    steps:
    - task: Docker@2
      displayName: 'Build and Push API Image'
      inputs:
        command: 'buildAndPush'
        repository: 'platform-api'
        dockerfile: 'Dockerfile.api'
        tags: '$(Build.BuildId)'

- stage: Deploy
  jobs:
  - deployment: Deploy
    environment: 'production'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: KubernetesManifest@0
            displayName: 'Deploy to Kubernetes'
            inputs:
              action: 'deploy'
              manifests: 'k8s/*.yaml'
```

---

## 📝 Troubleshooting

### Common Deployment Issues

#### Container Won't Start

```bash
# Check container logs
docker logs platform-api

# Check resource usage
docker stats

# Verify environment variables
docker exec platform-api env | grep AZURE
```

#### Service Discovery Issues

```bash
# Check DNS resolution
kubectl exec -it platform-api -- nslookup sqlserver

# Check service endpoints
kubectl get endpoints -n platform-supervisor

# Test connectivity
kubectl exec -it platform-api -- telnet sqlserver 1433
```

#### Performance Issues

```bash
# Monitor resource usage
kubectl top pods -n platform-supervisor

# Check health endpoints
curl http://localhost:7001/health
curl http://localhost:7001/health/ready

# Review application metrics
curl http://localhost:7001/metrics
```

### Rollback Procedures

```bash
# Kubernetes rollback
kubectl rollout undo deployment/platform-api -n platform-supervisor

# Docker Compose rollback
docker-compose down
git checkout previous-tag
docker-compose up -d

# Azure Container Apps rollback
az containerapp revision list --name platform-api --resource-group platform-supervisor-rg
az containerapp revision activate --revision revision-name
```

---

*For additional support, see the [main documentation](DOCUMENTATION.md) or create an issue on GitHub.*