#!/bin/bash

# Test ATO Compliance Prompts with Intelligent Query Endpoint
# This script tests all 10 ATO prompts from CHAT-PROMPTS-GUIDE.md

API_URL="${1:-http://localhost:5000}"
SUBSCRIPTION_ID="${2:-453c25d9-4c05-461f-baa6-3c9074016683}"

echo "=================================================="
echo "Testing ATO Compliance Prompts"
echo "API URL: $API_URL"
echo "Subscription ID: $SUBSCRIPTION_ID"
echo "=================================================="
echo ""

# Function to test a prompt
test_prompt() {
    local test_num=$1
    local prompt=$2
    local conv_id="test-$(date +%s)-$test_num"
    
    echo "----------------------------------------"
    echo "TEST $test_num: $prompt"
    echo "----------------------------------------"
    
    response=$(curl -s -X POST "$API_URL/api/chat/intelligent-query" \
        -H "Content-Type: application/json" \
        -d "{\"message\": \"$prompt\", \"conversationId\": \"$conv_id\"}")
    
    # Extract key fields
    echo "Response:"
    echo "$response" | jq -r '.content' 2>/dev/null || echo "$response"
    echo ""
    
    # Check if it used the right tool
    echo "Intent Classification:"
    echo "$response" | jq '.intent // empty' 2>/dev/null || echo "No intent field"
    echo ""
    
    # Show confidence
    echo "Confidence:"
    echo "$response" | jq '.confidence // empty' 2>/dev/null || echo "No confidence field"
    echo ""
    
    echo "✓ Test $test_num completed"
    echo ""
    sleep 2  # Rate limiting
}

# Test 1: Comprehensive Assessment with Subscription ID
test_prompt "1" "Run a comprehensive ATO compliance assessment on subscription $SUBSCRIPTION_ID"

# Test 2: NIST Compliance Check
test_prompt "2" "Check my subscription $SUBSCRIPTION_ID for NIST compliance"

# Test 3: Continuous Monitoring (missing subscription - should prompt)
test_prompt "3" "What's the continuous monitoring status?"

# Test 4: Evidence Collection with Control Family
test_prompt "4" "Collect evidence for access control (AC) controls in subscription $SUBSCRIPTION_ID"

# Test 5: Remediation Plan (missing subscription - should prompt)
test_prompt "5" "Generate a remediation plan for compliance findings"

# Test 6: ATO Timeline
test_prompt "6" "Show me the ATO timeline for subscription $SUBSCRIPTION_ID"

# Test 7: High-Risk Issues (missing subscription - should prompt)
test_prompt "7" "What are the high-risk compliance issues?"

# Test 8: ATO Certificate
test_prompt "8" "Generate an ATO certificate for subscription $SUBSCRIPTION_ID"

# Test 9: NIST 800-53 Compliance
test_prompt "9" "Are we compliant with NIST 800-53 for subscription $SUBSCRIPTION_ID?"

# Test 10: Compliance Drift
test_prompt "10" "Check compliance drift in my Azure environment for subscription $SUBSCRIPTION_ID"

echo "=================================================="
echo "All tests completed!"
echo "=================================================="
