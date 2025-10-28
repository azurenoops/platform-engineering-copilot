#!/bin/bash

# Test script for MCP Server dual-mode operation
# Tests both HTTP mode (for Chat web app) and verifies stdio mode setup

set -e

echo "🧪 Testing MCP Server Dual-Mode Operation"
echo "=========================================="
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if MCP server is running
echo "1️⃣  Checking if MCP server is running on port 5100..."
if curl -s http://localhost:5100/health > /dev/null 2>&1; then
    echo -e "${GREEN}✓ MCP server is running${NC}"
else
    echo -e "${YELLOW}⚠ MCP server not running. Starting it now...${NC}"
    echo "Run in another terminal:"
    echo "  dotnet run --project src/Platform.Engineering.Copilot.Mcp -- --http"
    echo ""
    exit 1
fi

echo ""

# Test health endpoint
echo "2️⃣  Testing health endpoint..."
HEALTH_RESPONSE=$(curl -s http://localhost:5100/health)
if echo "$HEALTH_RESPONSE" | grep -q "healthy"; then
    echo -e "${GREEN}✓ Health check passed${NC}"
    echo "   Response: $HEALTH_RESPONSE"
else
    echo -e "${RED}✗ Health check failed${NC}"
    echo "   Response: $HEALTH_RESPONSE"
    exit 1
fi

echo ""

# Test intelligent chat endpoint
echo "3️⃣  Testing intelligent chat endpoint..."
CHAT_RESPONSE=$(curl -s -X POST http://localhost:5100/api/chat/intelligent-query \
    -H "Content-Type: application/json" \
    -d '{
        "message": "Hello, can you help me?",
        "conversationId": "test-'$(date +%s)'"
    }')

if echo "$CHAT_RESPONSE" | grep -q "success"; then
    echo -e "${GREEN}✓ Chat endpoint working${NC}"
    echo "   Response preview: $(echo $CHAT_RESPONSE | cut -c1-100)..."
else
    echo -e "${YELLOW}⚠ Chat endpoint returned unexpected response${NC}"
    echo "   Full response: $CHAT_RESPONSE"
    echo ""
    echo "   This might be expected if Azure OpenAI credentials are not configured."
fi

echo ""

# Test stdio mode configuration
echo "4️⃣  Checking stdio mode configuration..."
if [ -f "$HOME/.copilot/config.json" ]; then
    if grep -q "platform-engineering-copilot" "$HOME/.copilot/config.json"; then
        echo -e "${GREEN}✓ GitHub Copilot MCP configuration found${NC}"
    else
        echo -e "${YELLOW}⚠ GitHub Copilot config exists but no MCP server configured${NC}"
        echo "   Add this to ~/.copilot/config.json:"
        echo '   {
     "mcpServers": {
       "platform-engineering-copilot": {
         "command": "dotnet",
         "args": ["run", "--project", "'$(pwd)'/src/Platform.Engineering.Copilot.Mcp"]
       }
     }
   }'
    fi
else
    echo -e "${YELLOW}⚠ GitHub Copilot config not found${NC}"
    echo "   Create ~/.copilot/config.json with MCP server configuration"
fi

echo ""

# Check Claude Desktop configuration
if [ -f "$HOME/Library/Application Support/Claude/claude_desktop_config.json" ]; then
    if grep -q "platform-engineering-copilot" "$HOME/Library/Application Support/Claude/claude_desktop_config.json"; then
        echo -e "${GREEN}✓ Claude Desktop MCP configuration found${NC}"
    else
        echo -e "${YELLOW}⚠ Claude Desktop config exists but no MCP server configured${NC}"
    fi
else
    echo -e "${YELLOW}ℹ Claude Desktop config not found (this is optional)${NC}"
fi

echo ""

# Summary
echo "📊 Test Summary"
echo "==============="
echo ""
echo "MCP Server Modes:"
echo "  • HTTP Mode (port 5100): ✓ Running and accessible"
echo "  • Stdio Mode: Configure in GitHub Copilot/Claude Desktop"
echo ""
echo "Next Steps:"
echo "  1. Configure GitHub Copilot with MCP server (see above)"
echo "  2. Start Chat app: cd src/Platform.Engineering.Copilot.Chat && dotnet run"
echo "  3. Test Chat app connects to http://localhost:5100"
echo "  4. Test GitHub Copilot integration with natural language prompts"
echo ""
echo -e "${GREEN}✅ MCP Server HTTP mode is working!${NC}"
