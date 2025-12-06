#!/bin/bash

# Test script to verify Resource Graph integration
# Tests the Discovery Agent's GetResourceDetailsAsync function directly

RESOURCE_ID="/subscriptions/453c2549-4cc5-464f-ba66-acad920823e8/resourceGroups/rg-ml-sbx-jrs/providers/Microsoft.Web/sites/web-ml-sbx-jrs"

echo "🔍 Testing Resource Graph Integration"
echo "======================================"
echo ""
echo "Resource ID: $RESOURCE_ID"
echo ""

# Send request to the chat API
echo "📤 Sending request to Discovery Agent..."
curl -X POST http://localhost:5001/api/conversations/send \
  -H "Content-Type: application/json" \
  -d "{
    \"conversationId\": \"rg-test-$(date +%s)\",
    \"userId\": \"test-user\",
    \"message\": \"Use the Discovery Agent to get resource details with extended properties for: $RESOURCE_ID\"
  }" 2>/dev/null &

CURL_PID=$!

echo ""
echo "⏳ Waiting for response (checking logs)..."
sleep 3

echo ""
echo "📋 Recent Application Logs:"
echo "======================================"
docker logs plaform-engineering-copilot-platform-chat 2>&1 | tail -50 | grep -E "(Discovery|Resource Graph|GetResource|Initializing|lazy)" || echo "No Discovery/Resource Graph logs found"

echo ""
echo "======================================"
echo "✅ Test Complete"
echo ""
echo "Expected to see:"
echo "  - 'Initializing Resource Graph client for first use...' (lazy init)"
echo "  - 'Getting resource details from query'"
echo "  - 'Retrieved resource from Resource Graph' OR 'falling back to API'"
echo ""
echo "If you see NO logs above, the Discovery Agent is NOT being invoked."
echo "The Orchestrator may be routing to MCP instead."

wait $CURL_PID 2>/dev/null
