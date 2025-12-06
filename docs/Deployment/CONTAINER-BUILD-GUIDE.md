# Container Build Guide

## Overview

This guide covers best practices for building, optimizing, and securing container images for the Platform Engineering Copilot using BuildKit and modern container build techniques.

## Table of Contents

- [BuildKit Overview](#buildkit-overview)
- [Multi-Stage Builds](#multi-stage-builds)
- [Build Optimization](#build-optimization)
- [Security Best Practices](#security-best-practices)
- [CI/CD Integration](#cicd-integration)
- [Image Scanning](#image-scanning)
- [Registry Operations](#registry-operations)
- [Performance Tuning](#performance-tuning)

## BuildKit Overview

BuildKit is the next-generation Docker build backend with improved performance, storage management, and extensibility.

### Enable BuildKit

```bash
# Enable BuildKit for Docker
export DOCKER_BUILDKIT=1

# Or add to ~/.docker/config.json
{
  "features": {
    "buildkit": true
  }
}

# Verify BuildKit is enabled
docker buildx version
```

### BuildKit Features

- **Parallel builds**: Build independent stages simultaneously
- **Cache mounting**: Persistent cache between builds
- **Secret mounting**: Secure handling of build secrets
- **SSH mounting**: SSH agent forwarding for private repos
- **Multi-platform builds**: Build for multiple architectures
- **Build contexts**: Remote Git repositories and HTTP URLs

## Multi-Stage Builds

### .NET Application Example

Create an optimized Dockerfile for the Platform Engineering Copilot API:

```dockerfile
# syntax=docker/dockerfile:1.4
# Use BuildKit syntax for advanced features

# ==============================================================================
# Stage 1: Build Stage
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy solution and project files
COPY ["Platform.Engineering.Copilot.sln", "./"]
COPY ["src/Platform.Engineering.Copilot.Core/*.csproj", "./src/Platform.Engineering.Copilot.Core/"]
COPY ["src/Platform.Engineering.Copilot.Chat/*.csproj", "./src/Platform.Engineering.Copilot.Chat/"]
COPY ["src/Platform.Engineering.Copilot.Admin.API/*.csproj", "./src/Platform.Engineering.Copilot.Admin.API/"]

# Restore dependencies (cached layer)
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "Platform.Engineering.Copilot.sln"

# Copy source code
COPY ["src/", "./src/"]

# Build application
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet build "Platform.Engineering.Copilot.sln" \
    -c Release \
    --no-restore

# ==============================================================================
# Stage 2: Publish Stage
# ==============================================================================
FROM build AS publish
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "src/Platform.Engineering.Copilot.Admin.API/Platform.Engineering.Copilot.Admin.API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    --no-build \
    /p:UseAppHost=false

# ==============================================================================
# Stage 3: Runtime Stage
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime

# Install runtime dependencies
RUN apk add --no-cache \
    icu-libs \
    tzdata

# Create non-root user
RUN addgroup -g 1000 appuser && \
    adduser -D -u 1000 -G appuser appuser

WORKDIR /app

# Copy published application
COPY --from=publish --chown=appuser:appuser /app/publish .

# Switch to non-root user
USER appuser

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:5000/health || exit 1

# Expose port
EXPOSE 5000

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

# Run application
ENTRYPOINT ["dotnet", "Platform.Engineering.Copilot.Admin.API.dll"]
```

### Build the Image

```bash
# Build with BuildKit
docker build \
  --tag platsupacr.azurecr.io/platform-copilot-api:latest \
  --file Dockerfile \
  --build-arg BUILDKIT_INLINE_CACHE=1 \
  .

# Build with specific platform
docker buildx build \
  --platform linux/amd64,linux/arm64 \
  --tag platsupacr.azurecr.io/platform-copilot-api:latest \
  --file Dockerfile \
  .
```

## Build Optimization

### Cache Mounting

Use cache mounts to persist build caches between builds:

```dockerfile
# NPM cache example
FROM node:20-alpine AS build
WORKDIR /app

# Use cache mount for npm packages
RUN --mount=type=cache,target=/root/.npm \
    npm install

# Use cache mount for build artifacts
RUN --mount=type=cache,target=/app/.next/cache \
    npm run build
```

### Layer Optimization

**Best Practices:**
1. **Order matters**: Place less frequently changing layers first
2. **Combine commands**: Reduce layer count
3. **Clean up in same layer**: Remove temporary files
4. **Use .dockerignore**: Exclude unnecessary files

Example `.dockerignore`:

```
# .dockerignore
**/.git
**/.gitignore
**/.dockerignore
**/node_modules
**/dist
**/obj
**/bin
**/.vscode
**/.vs
**/appsettings.Development.json
**/appsettings.local.json
**/*.md
**/Dockerfile*
**/docker-compose*
**/.env
**/.env.*
**/README.md
**/LICENSE
**/*.log
**/.DS_Store
**/Thumbs.db
```

### BuildKit Cache Export

Export build cache for CI/CD:

```bash
# Build with cache export
docker buildx build \
  --cache-from type=registry,ref=platsupacr.azurecr.io/cache:latest \
  --cache-to type=registry,ref=platsupacr.azurecr.io/cache:latest,mode=max \
  --tag platsupacr.azurecr.io/platform-copilot-api:latest \
  --push \
  .
```

### Parallel Builds

BuildKit automatically parallelizes independent build stages:

```dockerfile
# These stages can build in parallel
FROM base AS frontend-deps
RUN npm install --production

FROM base AS frontend-build
RUN npm run build

FROM base AS backend-deps
RUN dotnet restore

FROM base AS backend-build
RUN dotnet build

# Final stage combines results
FROM runtime AS final
COPY --from=frontend-build /app/dist ./wwwroot
COPY --from=backend-build /app/bin ./
```

## Security Best Practices

### Use Minimal Base Images

```dockerfile
# ❌ Bad: Full image (1+ GB)
FROM ubuntu:22.04

# ✅ Good: Slim image (100-200 MB)
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine

# ✅ Better: Distroless image (50-100 MB)
FROM gcr.io/distroless/dotnet8
```

### Run as Non-Root User

```dockerfile
# Create and use non-root user
RUN addgroup -g 1000 appuser && \
    adduser -D -u 1000 -G appuser appuser

# Set ownership
COPY --chown=appuser:appuser . .

# Switch user
USER appuser
```

### Secret Management

**Never** hardcode secrets in Dockerfiles:

```dockerfile
# ❌ Bad: Secret in build arg
ARG API_KEY=secret123
RUN echo $API_KEY > config

# ✅ Good: Mount secret
RUN --mount=type=secret,id=api_key \
    API_KEY=$(cat /run/secrets/api_key) && \
    configure-app --key=$API_KEY
```

Build with secret:

```bash
# Provide secret at build time
docker buildx build \
  --secret id=api_key,src=./secrets/api-key.txt \
  --tag myapp:latest \
  .
```

### Scan for Vulnerabilities

```dockerfile
# Add security scanning to build process
FROM alpine:latest
RUN apk add --no-cache trivy

# Scan image
RUN trivy image --severity HIGH,CRITICAL myimage:latest
```

### Sign Images with Cosign

```bash
# Install cosign
brew install cosign

# Generate key pair
cosign generate-key-pair

# Sign image
cosign sign --key cosign.key platsupacr.azurecr.io/platform-copilot-api:v1.0

# Verify signature
cosign verify --key cosign.pub platsupacr.azurecr.io/platform-copilot-api:v1.0
```

## CI/CD Integration

### GitHub Actions

Create `.github/workflows/build-and-push.yml`:

```yaml
name: Build and Push Container Image

on:
  push:
    branches: [main, develop]
    tags: ['v*']
  pull_request:
    branches: [main]

env:
  REGISTRY: platsupacr.azurecr.io
  IMAGE_NAME: platform-copilot-api

jobs:
  build:
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write

    steps:
    - name: Checkout code
      uses: actions/checkout@v4

    - name: Set up Docker Buildx
      uses: docker/setup-buildx-action@v3

    - name: Log in to Azure Container Registry
      uses: docker/login-action@v3
      with:
        registry: ${{ env.REGISTRY }}
        username: ${{ secrets.ACR_USERNAME }}
        password: ${{ secrets.ACR_PASSWORD }}

    - name: Extract metadata
      id: meta
      uses: docker/metadata-action@v5
      with:
        images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
        tags: |
          type=ref,event=branch
          type=ref,event=pr
          type=semver,pattern={{version}}
          type=semver,pattern={{major}}.{{minor}}
          type=sha,prefix={{branch}}-

    - name: Build and push
      uses: docker/build-push-action@v5
      with:
        context: .
        file: ./Dockerfile
        push: ${{ github.event_name != 'pull_request' }}
        tags: ${{ steps.meta.outputs.tags }}
        labels: ${{ steps.meta.outputs.labels }}
        cache-from: type=registry,ref=${{ env.REGISTRY }}/cache:latest
        cache-to: type=registry,ref=${{ env.REGISTRY }}/cache:latest,mode=max
        platforms: linux/amd64,linux/arm64
        build-args: |
          BUILD_DATE=${{ github.event.head_commit.timestamp }}
          VCS_REF=${{ github.sha }}
          VERSION=${{ steps.meta.outputs.version }}

    - name: Scan image with Trivy
      uses: aquasecurity/trivy-action@master
      with:
        image-ref: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ steps.meta.outputs.version }}
        format: 'sarif'
        output: 'trivy-results.sarif'

    - name: Upload Trivy results to GitHub Security
      uses: github/codeql-action/upload-sarif@v2
      with:
        sarif_file: 'trivy-results.sarif'
```

### Azure DevOps

Create `azure-pipelines.yml`:

```yaml
trigger:
  branches:
    include:
    - main
    - develop
  tags:
    include:
    - v*

pool:
  vmImage: 'ubuntu-latest'

variables:
  containerRegistry: 'platsupacr.azurecr.io'
  imageName: 'platform-copilot-api'
  dockerfilePath: '$(Build.SourcesDirectory)/Dockerfile'

stages:
- stage: Build
  displayName: Build and Push Image
  jobs:
  - job: Build
    displayName: Build Docker Image
    steps:
    - task: Docker@2
      displayName: Login to ACR
      inputs:
        command: login
        containerRegistry: $(containerRegistry)

    - task: Docker@2
      displayName: Build and Push
      inputs:
        command: buildAndPush
        repository: $(imageName)
        dockerfile: $(dockerfilePath)
        containerRegistry: $(containerRegistry)
        tags: |
          $(Build.BuildId)
          latest
        arguments: |
          --build-arg BUILDKIT_INLINE_CACHE=1
          --cache-from $(containerRegistry)/cache:latest

    - task: trivy@1
      displayName: Scan Image
      inputs:
        image: '$(containerRegistry)/$(imageName):$(Build.BuildId)'
        severities: 'CRITICAL,HIGH'
        exitCode: '1'
```

### GitLab CI

Create `.gitlab-ci.yml`:

```yaml
stages:
  - build
  - scan
  - push

variables:
  DOCKER_DRIVER: overlay2
  DOCKER_BUILDKIT: 1
  IMAGE_TAG: $CI_REGISTRY_IMAGE:$CI_COMMIT_SHORT_SHA

build:
  stage: build
  image: docker:24
  services:
    - docker:24-dind
  before_script:
    - docker login -u $CI_REGISTRY_USER -p $CI_REGISTRY_PASSWORD $CI_REGISTRY
  script:
    - |
      docker buildx create --use
      docker buildx build \
        --cache-from type=registry,ref=$CI_REGISTRY_IMAGE:cache \
        --cache-to type=registry,ref=$CI_REGISTRY_IMAGE:cache,mode=max \
        --tag $IMAGE_TAG \
        --tag $CI_REGISTRY_IMAGE:latest \
        --push \
        .

scan:
  stage: scan
  image: aquasec/trivy:latest
  script:
    - trivy image --exit-code 1 --severity CRITICAL,HIGH $IMAGE_TAG

push:
  stage: push
  image: docker:24
  script:
    - docker tag $IMAGE_TAG $CI_REGISTRY_IMAGE:latest
    - docker push $CI_REGISTRY_IMAGE:latest
  only:
    - main
```

## Image Scanning

### Trivy Integration

```bash
# Install Trivy
brew install trivy

# Scan local image
trivy image platform-copilot-api:latest

# Scan image in ACR
trivy image --severity HIGH,CRITICAL \
  platsupacr.azurecr.io/platform-copilot-api:latest

# Generate JSON report
trivy image --format json --output report.json \
  platsupacr.azurecr.io/platform-copilot-api:latest

# Scan with exit code on vulnerabilities
trivy image --exit-code 1 --severity CRITICAL \
  platsupacr.azurecr.io/platform-copilot-api:latest

# Scan filesystem
trivy fs --security-checks vuln,config .

# Scan IaC configurations
trivy config ./infra/terraform
```

### Microsoft Defender for Containers

Enable in ACR:

```bash
# Enable Defender for Containers
az security pricing create \
  --name Containers \
  --tier Standard

# View scan results
az security assessment list \
  --resource-group "$RESOURCE_GROUP"
```

### Anchore Engine

```bash
# Install Anchore CLI
pip install anchorecli

# Scan image
anchore-cli image add platsupacr.azurecr.io/platform-copilot-api:latest
anchore-cli image wait platsupacr.azurecr.io/platform-copilot-api:latest
anchore-cli image vuln platsupacr.azurecr.io/platform-copilot-api:latest all
```

## Registry Operations

### Azure Container Registry

#### Build in ACR

```bash
# Build using ACR Tasks (BuildKit enabled by default)
az acr build \
  --registry platsupacr \
  --image platform-copilot-api:v1.0 \
  --file Dockerfile \
  --platform linux/amd64 \
  .

# Multi-platform build
az acr build \
  --registry platsupacr \
  --image platform-copilot-api:v1.0 \
  --file Dockerfile \
  --platform linux/amd64,linux/arm64 \
  .
```

#### Image Import

```bash
# Import from Docker Hub
az acr import \
  --name platsupacr \
  --source docker.io/library/nginx:latest \
  --image nginx:latest

# Import from another registry
az acr import \
  --name platsupacr \
  --source mcr.microsoft.com/dotnet/aspnet:8.0 \
  --image base/aspnet:8.0
```

#### Content Trust (Notary)

```bash
# Enable content trust
export DOCKER_CONTENT_TRUST=1
export DOCKER_CONTENT_TRUST_SERVER=https://platsupacr.azurecr.io

# Push signed image
docker push platsupacr.azurecr.io/platform-copilot-api:v1.0

# Verify signature
docker trust inspect platsupacr.azurecr.io/platform-copilot-api:v1.0
```

#### Image Cleanup

```bash
# Delete untagged images
az acr repository show-manifests \
  --name platsupacr \
  --repository platform-copilot-api \
  --query "[?tags[0]==null].digest" -o tsv | \
  xargs -I% az acr repository delete \
    --name platsupacr \
    --image platform-copilot-api@% \
    --yes

# Delete old images (retention policy)
az acr config retention update \
  --registry platsupacr \
  --status enabled \
  --days 30 \
  --type UntaggedManifests
```

## Performance Tuning

### Build Performance

#### Use BuildKit Cache

```bash
# Export cache to local directory
docker buildx build \
  --cache-from type=local,src=/tmp/.buildx-cache \
  --cache-to type=local,dest=/tmp/.buildx-cache \
  --tag myapp:latest \
  .

# Use inline cache
docker buildx build \
  --build-arg BUILDKIT_INLINE_CACHE=1 \
  --tag myapp:latest \
  .
```

#### Optimize Layer Caching

```dockerfile
# ✅ Good: Cache dependencies separately
COPY package*.json ./
RUN npm install
COPY . .

# ❌ Bad: Copy everything first
COPY . .
RUN npm install
```

#### Use Heredocs (BuildKit 1.4+)

```dockerfile
# Multiple commands in single layer
RUN <<EOF
  apt-get update
  apt-get install -y curl wget
  apt-get clean
  rm -rf /var/lib/apt/lists/*
EOF

# Create files inline
COPY <<EOF /app/config.json
{
  "environment": "production",
  "debug": false
}
EOF
```

### Image Size Optimization

#### Use Alpine or Distroless

```dockerfile
# Alpine (smaller)
FROM node:20-alpine
RUN apk add --no-cache python3

# Distroless (smallest, most secure)
FROM gcr.io/distroless/nodejs20
COPY --from=build /app /app
```

#### Multi-Stage Build Optimization

```dockerfile
# Install build tools in build stage only
FROM node:20 AS build
RUN apt-get update && apt-get install -y python3 make g++
RUN npm install
RUN npm run build

# Runtime stage has minimal dependencies
FROM node:20-alpine
COPY --from=build /app/dist ./dist
COPY --from=build /app/node_modules ./node_modules
```

#### Use .dockerignore

```
# Exclude large files
*.log
*.tar.gz
*.zip
node_modules/
.git/
.vscode/
```

### Measure Image Size

```bash
# View image layers
docker history platform-copilot-api:latest

# Analyze image size
docker images platform-copilot-api:latest

# Use dive to analyze layers
brew install dive
dive platform-copilot-api:latest
```

## Advanced BuildKit Features

### Build Secrets

```dockerfile
# Use secret during build
RUN --mount=type=secret,id=npmrc,target=/root/.npmrc \
    npm install
```

```bash
# Build with secret
docker buildx build \
  --secret id=npmrc,src=$HOME/.npmrc \
  --tag myapp:latest \
  .
```

### SSH Agent Forwarding

```dockerfile
# Use SSH for private repos
RUN --mount=type=ssh \
    git clone git@github.com:private/repo.git
```

```bash
# Build with SSH
docker buildx build \
  --ssh default=$SSH_AUTH_SOCK \
  --tag myapp:latest \
  .
```

### Remote Build Context

```bash
# Build from Git repository
docker buildx build \
  --tag myapp:latest \
  https://github.com/user/repo.git#branch

# Build from tarball
docker buildx build \
  --tag myapp:latest \
  https://example.com/context.tar.gz
```

## Best Practices Summary

### ✅ Do's

- **Use multi-stage builds** to minimize final image size
- **Run as non-root user** for security
- **Use .dockerignore** to exclude unnecessary files
- **Cache dependencies** in separate layers
- **Scan images** for vulnerabilities
- **Sign images** for supply chain security
- **Use specific tags** instead of `latest`
- **Set resource limits** in Kubernetes
- **Implement health checks** in Dockerfile
- **Use minimal base images** (Alpine, Distroless)

### ❌ Don'ts

- **Don't use `latest` tag** in production
- **Don't run as root** unless absolutely necessary
- **Don't hardcode secrets** in Dockerfile
- **Don't install unnecessary packages**
- **Don't skip vulnerability scanning**
- **Don't build on production systems**
- **Don't commit credentials** to Git
- **Don't use untrusted base images**

## Additional Resources

- [BuildKit Documentation](https://github.com/moby/buildkit)
- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
- [Dockerfile Reference](https://docs.docker.com/engine/reference/builder/)
- [Multi-Stage Builds](https://docs.docker.com/build/building/multi-stage/)
- [Container Security](https://cheatsheetseries.owasp.org/cheatsheets/Docker_Security_Cheat_Sheet.html)
