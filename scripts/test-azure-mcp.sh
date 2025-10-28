#!/bin/bash

# Test Azure MCP Integration
# This script tests the Azure MCP Server integration by running a simple .NET console app

set -e

echo "🔧 Azure MCP Integration Test"
echo "=============================="
echo ""

# Check prerequisites
echo "Checking prerequisites..."

# 1. Node.js
if ! command -v node &> /dev/null; then
    echo "❌ Node.js not found. Please install Node.js 18+ from https://nodejs.org"
    exit 1
fi
NODE_VERSION=$(node --version)
echo "✅ Node.js: $NODE_VERSION"

# 2. npx
if ! command -v npx &> /dev/null; then
    echo "❌ npx not found. Please install Node.js 18+ which includes npx"
    exit 1
fi
echo "✅ npx available"

# 3. Azure CLI
if ! command -v az &> /dev/null; then
    echo "❌ Azure CLI not found. Please install from https://docs.microsoft.com/cli/azure/install-azure-cli"
    exit 1
fi
echo "✅ Azure CLI installed"

# 4. Azure authentication
if ! az account show &> /dev/null; then
    echo "❌ Not authenticated to Azure. Please run: az login"
    exit 1
fi
SUBSCRIPTION_ID=$(az account show --query id -o tsv)
SUBSCRIPTION_NAME=$(az account show --query name -o tsv)
echo "✅ Authenticated to Azure"
echo "   Subscription: $SUBSCRIPTION_NAME"
echo "   ID: $SUBSCRIPTION_ID"

echo ""
echo "Prerequisites check passed! ✅"
echo ""

# Test Azure MCP Server directly (without .NET integration)
echo "Testing Azure MCP Server (npx)..."
echo "=================================="
echo ""

# Create a temporary test script for Azure MCP
cat > /tmp/test-azure-mcp.json << 'EOF'
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test","version":"1.0.0"}}}
{"jsonrpc":"2.0","id":2,"method":"tools/list","params":{}}
EOF

echo "Starting Azure MCP Server (this may take a moment on first run)..."
echo "Command: npx -y @azure/mcp@latest server start --read-only"
echo ""

# Note: This will hang waiting for input, so we'll just verify it starts
timeout 10s npx -y @azure/mcp@latest server start --read-only < /tmp/test-azure-mcp.json > /tmp/azure-mcp-output.txt 2>&1 || true

if [ -f /tmp/azure-mcp-output.txt ]; then
    echo "Azure MCP Server output:"
    cat /tmp/azure-mcp-output.txt | head -20
    echo ""
    
    if grep -q "error" /tmp/azure-mcp-output.txt; then
        echo "⚠️  Detected errors in output"
    else
        echo "✅ Azure MCP Server appears to be working"
    fi
else
    echo "⚠️  No output captured"
fi

echo ""
echo "Next steps:"
echo "==========="
echo "1. Run the xUnit tests in Platform.Engineering.Copilot.Tests.Manual/AzureMcpIntegrationTest.cs"
echo "2. Remove the [Skip] attributes to enable the tests"
echo "3. Run: dotnet test tests/Platform.Engineering.Copilot.Tests.Manual --filter AzureMcpIntegrationTest"
echo ""
echo "Or test with the .NET integration:"
echo "dotnet run --project tests/Platform.Engineering.Copilot.Tests.Manual -- azure-mcp"
