#!/bin/bash

# Test Script for Phase 1: Infrastructure Intent Classification
# Tests if AI correctly classifies infrastructure provisioning requests

echo "=================================================="
echo "Phase 1 Test: Infrastructure Intent Classification"
echo "=================================================="
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test cases
declare -a test_cases=(
    "Create a storage account named testdata in resource group demo-rg in eastus"
    "Deploy a Key Vault named secrets-kv in Azure Government usgovvirginia"
    "Provision a VNet with 3 subnets in westus"
    "Generate a Bicep template for an AKS cluster"
    "Generate Terraform for SQL database"
    "Show me all storage accounts in my subscription"
    "How much does an AKS cluster cost?"
    "What is zero trust networking?"
    "I need to onboard a new mission"
    "Create a storage account"
)

declare -a expected_tools=(
    "create_azure_resource"
    "create_azure_resource"
    "create_azure_resource"
    "generate_bicep_template"
    "generate_terraform_template"
    "azure_get_resource"
    "information_request"
    "conversational"
    "flankspeed_start_onboarding"
    "create_azure_resource"
)

echo "Test Cases:"
echo "----------"
for i in "${!test_cases[@]}"; do
    echo "$((i+1)). \"${test_cases[$i]}\""
    echo "   Expected: ${expected_tools[$i]}"
    echo ""
done

echo ""
echo "📋 To manually test:"
echo "-------------------"
echo "1. Start Platform API:"
echo "   dotnet run src/Platform.Engineering.Copilot.API/Platform.Engineering.Copilot.API.csproj"
echo ""
echo "2. Start Chat App:"
echo "   ./scripts/start-chat.sh"
echo ""
echo "3. Open browser:"
echo "   http://localhost:3001"
echo ""
echo "4. Send each test message above"
echo ""
echo "5. Check Platform API logs for:"
echo "   'AI Classification Result: IntentType=..., ToolName=...'"
echo ""
echo "✅ Expected for Test 1:"
echo "   AI Classification Result: IntentType=tool_execution, ToolName=create_azure_resource"
echo ""
echo "❌ Current System Response:"
echo "   'No handler found for tool: create_azure_resource'"
echo "   (This is EXPECTED - tool routing not implemented yet)"
echo ""
echo "=================================================="
echo "Phase 1 Success Criteria:"
echo "=================================================="
echo "✅ AI classifies 'Create storage account' as create_azure_resource"
echo "✅ AI classifies 'Deploy Key Vault' as create_azure_resource"
echo "✅ AI classifies 'Generate Bicep' as generate_bicep_template"
echo "✅ AI classifies 'Show storage accounts' as azure_get_resource"
echo "✅ AI asks for missing parameters when incomplete"
echo ""
echo "🎯 Target: 9/10 test cases classified correctly"
echo ""
echo "=================================================="
echo "Automated Log Check (if services running):"
echo "=================================================="
echo ""

# Check if services are running
API_PORT=7001
CHAT_PORT=3001

if lsof -Pi :$API_PORT -sTCP:LISTEN -t >/dev/null 2>&1; then
    echo -e "${GREEN}✅ Platform API is running on port $API_PORT${NC}"
else
    echo -e "${RED}❌ Platform API is NOT running${NC}"
    echo "   Start with: dotnet run src/Platform.Engineering.Copilot.API/Platform.Engineering.Copilot.API.csproj"
fi

if lsof -Pi :$CHAT_PORT -sTCP:LISTEN -t >/dev/null 2>&1; then
    echo -e "${GREEN}✅ Chat App is running on port $CHAT_PORT${NC}"
else
    echo -e "${RED}❌ Chat App is NOT running${NC}"
    echo "   Start with: ./scripts/start-chat.sh"
fi

echo ""
echo "=================================================="
echo "Next Steps After Verification:"
echo "=================================================="
echo ""
echo "When Phase 1 is verified working:"
echo ""
echo "✅ Phase 1 Complete (Intent Classification) - 15 min"
echo "⏭️  Phase 2: Tool Name Mapping - 15 min"
echo "⏭️  Phase 3: Register Tools - 20 min (Already done!)"
echo "⏭️  Phase 4: Parameter Extraction - 45 min"
echo "⏭️  Phase 5: Tool Routing - 30 min"
echo "⏭️  Phase 6: Confirmation Flow - 45 min"
echo "⏭️  Phase 7: Wire Together - 30 min"
echo ""
echo "Total Remaining: ~3 hours for full feature"
echo ""
echo "=================================================="
