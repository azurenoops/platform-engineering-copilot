#!/bin/bash
# Build and Deploy Platform Engineering Copilot MCP to ACR/ACI

set -e

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ACR_NAME="platengcopilotdevacr759d45b2"
RESOURCE_GROUP="platengcopilot-dev-rg"
ENVIRONMENT="dev"
IMAGE_TAG="latest"

echo "======================================"
echo "Building Platform Engineering Copilot MCP"
echo "======================================"

# Step 1: Build solution to ensure no compilation errors
echo "✓ Building .NET solution..."
cd "$PROJECT_ROOT"
dotnet build Platform.Engineering.Copilot.sln -c Release -q

# Step 2: Build Docker image
echo "✓ Building Docker image..."
docker build \
  --no-cache \
  -t ${ACR_NAME}.azurecr.us/platform-engineering-copilot-mcp:${IMAGE_TAG} \
  -f src/Platform.Engineering.Copilot.Mcp/Dockerfile \
  .

# Step 3: Login to ACR
echo "✓ Logging in to ACR..."
az acr login --name $ACR_NAME

# Step 4: Push to ACR
echo "✓ Pushing image to ACR..."
docker push ${ACR_NAME}.azurecr.us/platform-engineering-copilot-mcp:${IMAGE_TAG}

# Step 5: Restart ACI container
echo "✓ Restarting ACI container..."
CONTAINER_NAME="platengcopilot-dev-platform-mcp-aci"
az container restart \
  --resource-group $RESOURCE_GROUP \
  --name $CONTAINER_NAME

echo ""
echo "======================================"
echo "✅ Deployment Complete!"
echo "======================================"
echo ""
echo "Service Details:"
echo "  ACR: ${ACR_NAME}.azurecr.us"
echo "  Image: platform-engineering-copilot-mcp:${IMAGE_TAG}"
echo "  Resource Group: $RESOURCE_GROUP"
echo "  Container: $CONTAINER_NAME"
echo ""
echo "Testing the deployment:"
echo "  az container logs --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME --follow"
echo ""
