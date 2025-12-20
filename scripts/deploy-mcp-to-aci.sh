#!/bin/bash

# ACI Deployment Helper Script
# This script deploys the Platform Engineering Copilot MCP server to Azure Container Instances
# with all required environment variables

set -e

echo "🚀 Platform Engineering Copilot - ACI Deployment Helper"
echo "======================================================"
echo

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Load environment variables if .env exists
if [ -f .env ]; then
    echo "📦 Loading environment variables from .env..."
    export $(cat .env | grep -v '^#' | xargs)
    echo "✅ Environment variables loaded"
else
    echo -e "${RED}❌ .env file not found${NC}"
    echo "Please copy .env.aci.example to .env and fill in your values:"
    echo "  cp .env.aci.example .env"
    echo "  nano .env"
    exit 1
fi

echo

# Validate required variables
echo "🔍 Validating configuration..."

REQUIRED_VARS=(
    "AZURE_TENANT_ID"
    "AZURE_CLIENT_ID"
    "AZURE_CLIENT_SECRET"
    "AZURE_SUBSCRIPTION_ID"
    "AZURE_OPENAI_ENDPOINT"
    "AZURE_OPENAI_DEPLOYMENT"
    "AZURE_OPENAI_API_KEY"
    "AZURE_CLOUD_ENVIRONMENT"
)

MISSING_VARS=()
for var in "${REQUIRED_VARS[@]}"; do
    if [ -z "${!var}" ]; then
        MISSING_VARS+=("$var")
    fi
done

if [ ${#MISSING_VARS[@]} -gt 0 ]; then
    echo -e "${RED}❌ Missing required environment variables:${NC}"
    for var in "${MISSING_VARS[@]}"; do
        echo "   - $var"
    done
    exit 1
fi

echo -e "${GREEN}✅ All required variables configured${NC}"
echo

# Get or prompt for deployment variables
read -p "📋 Resource Group [platengcopilot-dev-rg]: " RESOURCE_GROUP
RESOURCE_GROUP="${RESOURCE_GROUP:-platengcopilot-dev-rg}"

read -p "📋 Container Name [platform-mcp-aci]: " CONTAINER_NAME
CONTAINER_NAME="${CONTAINER_NAME:-platform-mcp-aci}"

read -p "📋 ACR Name [platengcopilotdevacr759d45b2]: " ACR_NAME
ACR_NAME="${ACR_NAME:-platengcopilotdevacr759d45b2}"

read -p "📋 Azure Region [usgovvirginia]: " LOCATION
LOCATION="${LOCATION:-usgovvirginia}"

echo

# Build the image tag
ACR_LOGIN_SERVER="${ACR_NAME}.azurecr.us"
MCP_IMAGE="${ACR_LOGIN_SERVER}/platform-engineering-copilot-mcp:latest"

echo "🔧 Configuration Summary:"
echo "  Resource Group:     $RESOURCE_GROUP"
echo "  Container Name:     $CONTAINER_NAME"
echo "  ACR:                $ACR_NAME"
echo "  Location:           $LOCATION"
echo "  Image:              $MCP_IMAGE"
echo "  Azure OpenAI:       $AZURE_OPENAI_ENDPOINT"
echo

read -p "✅ Proceed with deployment? (yes/no): " CONFIRM
CONFIRM=$(echo "$CONFIRM" | tr '[:upper:]' '[:lower:]')
if [[ ! "$CONFIRM" =~ ^(y|yes)$ ]]; then
    echo "❌ Deployment cancelled"
    exit 0
fi

echo

# Get ACR credentials
echo "🔑 Getting ACR credentials..."
ACR_USERNAME=$(az acr credential show --name $ACR_NAME --query username -o tsv 2>/dev/null)
ACR_PASSWORD=$(az acr credential show --name $ACR_NAME --query passwords[0].value -o tsv 2>/dev/null)

if [ -z "$ACR_USERNAME" ] || [ -z "$ACR_PASSWORD" ]; then
    echo -e "${RED}❌ Failed to get ACR credentials${NC}"
    exit 1
fi

echo "✅ ACR credentials obtained"
echo

# Set defaults for optional variables
AZURE_OPENAI_CHAT_DEPLOYMENT="${AZURE_OPENAI_CHAT_DEPLOYMENT:-gpt-4o}"
AZURE_OPENAI_EMBEDDING_DEPLOYMENT="${AZURE_OPENAI_EMBEDDING_DEPLOYMENT:-text-embedding-ada-002}"
AZURE_USE_MANAGED_IDENTITY="${AZURE_USE_MANAGED_IDENTITY:-false}"
NIST_CONTROLS_BASE_URL="${NIST_CONTROLS_BASE_URL:-https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json}"
NIST_CONTROLS_CACHE_DURATION="${NIST_CONTROLS_CACHE_DURATION:-24}"
NIST_CONTROLS_OFFLINE_FALLBACK="${NIST_CONTROLS_OFFLINE_FALLBACK:-true}"

echo "📋 Default values set for optional variables"
echo "  ChatDeploymentName: $AZURE_OPENAI_CHAT_DEPLOYMENT"
echo "  EmbeddingDeploymentName: $AZURE_OPENAI_EMBEDDING_DEPLOYMENT"
echo

# Deploy container
echo "🚀 Deploying MCP container to ACI..."
az container create \
  --resource-group $RESOURCE_GROUP \
  --name $CONTAINER_NAME \
  --image $MCP_IMAGE \
  --os-type Linux \
  --registry-login-server $ACR_LOGIN_SERVER \
  --registry-username $ACR_USERNAME \
  --registry-password $ACR_PASSWORD \
  --cpu 2 \
  --memory 4 \
  --ports 5100 \
  --dns-name-label "${CONTAINER_NAME}" \
  --location $LOCATION \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT="Production" \
    ASPNETCORE_URLS="http://+:5100" \
    Gateway__Azure__TenantId="$AZURE_TENANT_ID" \
    Gateway__Azure__ClientId="$AZURE_CLIENT_ID" \
    Gateway__Azure__ClientSecret="$AZURE_CLIENT_SECRET" \
    Gateway__Azure__SubscriptionId="$AZURE_SUBSCRIPTION_ID" \
    Gateway__Azure__CloudEnvironment="$AZURE_CLOUD_ENVIRONMENT" \
    Gateway__Azure__UseManagedIdentity="$AZURE_USE_MANAGED_IDENTITY" \
    Gateway__AzureOpenAI__Endpoint="$AZURE_OPENAI_ENDPOINT" \
    Gateway__AzureOpenAI__DeploymentName="$AZURE_OPENAI_DEPLOYMENT" \
    Gateway__AzureOpenAI__ApiKey="$AZURE_OPENAI_API_KEY" \
    Gateway__AzureOpenAI__ChatDeploymentName="$AZURE_OPENAI_CHAT_DEPLOYMENT" \
    Gateway__AzureOpenAI__EmbeddingDeploymentName="$AZURE_OPENAI_EMBEDDING_DEPLOYMENT" \
    NistControls__BaseUrl="$NIST_CONTROLS_BASE_URL" \
    NistControls__CacheDurationHours="$NIST_CONTROLS_CACHE_DURATION" \
    NistControls__UseOfflineFallback="$NIST_CONTROLS_OFFLINE_FALLBACK"

if [ $? -eq 0 ]; then
    echo -e "${GREEN}✅ Container deployed successfully${NC}"
else
    echo -e "${RED}❌ Container deployment failed${NC}"
    exit 1
fi

echo

# Get FQDN
echo "⏳ Waiting for container to be ready..."
sleep 10

FQDN=$(az container show \
  --resource-group $RESOURCE_GROUP \
  --name $CONTAINER_NAME \
  --query ipAddress.fqdn -o tsv 2>/dev/null)

if [ -z "$FQDN" ]; then
    echo -e "${YELLOW}⚠️  Could not retrieve FQDN${NC}"
    echo "Check deployment status with:"
    echo "  az container show --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME"
    exit 0
fi

echo -e "${GREEN}✅ Container is ready${NC}"
echo

# Test the health endpoint
echo "🔍 Testing MCP health endpoint..."
HEALTH_URL="http://${FQDN}:5100/health"

for i in {1..6}; do
    if curl -s "$HEALTH_URL" | jq . > /dev/null 2>&1; then
        echo -e "${GREEN}✅ Health check passed${NC}"
        echo "MCP Server is running at: $HEALTH_URL"
        break
    else
        if [ $i -lt 6 ]; then
            echo "⏳ Waiting for health endpoint... ($i/5)"
            sleep 5
        else
            echo -e "${YELLOW}⚠️  Health endpoint not responding yet${NC}"
            echo "Wait a few more seconds and try:"
            echo "  curl -s $HEALTH_URL | jq ."
        fi
    fi
done

echo

# Display next steps
echo "📋 Next Steps:"
echo "=================================================="
echo "Container URL:   http://${FQDN}:5100"
echo "Health Check:    http://${FQDN}:5100/health"
echo
echo "Test MCP Endpoint:"
echo "  curl -X POST http://${FQDN}:5100/mcp/chat \\"
echo "    -H 'Content-Type: application/json' \\"
echo "    -d '{\"message\": \"Generate a Bicep template\", \"conversationId\": \"test-001\"}' | jq ."
echo
echo "View container logs:"
echo "  az container logs --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME"
echo
echo "Restart container:"
echo "  az container restart --resource-group $RESOURCE_GROUP --name $CONTAINER_NAME"
echo

echo -e "${GREEN}✅ Deployment complete!${NC}"
