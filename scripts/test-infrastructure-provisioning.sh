#!/bin/bash

# Test Infrastructure Provisioning End-to-End
# This script tests the intelligent chat infrastructure provisioning capability

set -e

echo "======================================"
echo "Infrastructure Provisioning Test"
echo "======================================"
echo ""

# Test 1: Storage Account Creation Request
echo "Test 1: Create storage account request"
echo "--------------------------------------"
curl -X POST http://localhost:5000/api/chat/intelligent-query \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Create a storage account named testdata001 in eastus",
    "conversationId": "test-infra-001",
    "context": {
      "userId": "test-user@example.com",
      "environment": "development"
    }
  }' | jq '.'

echo ""
echo "======================================"
echo ""

# Test 2: Cost Analysis Request
echo "Test 2: Azure cost analysis request"
echo "--------------------------------------"
curl -X POST http://localhost:5000/api/chat/intelligent-query \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Analyze Azure costs for the last 3 months",
    "conversationId": "test-infra-002",
    "context": {
      "userId": "test-user@example.com"
    }
  }' | jq '.'

echo ""
echo "======================================"
echo ""

# Test 3: Compliance Scan Request
echo "Test 3: FedRAMP compliance scan request"
echo "--------------------------------------"
curl -X POST http://localhost:5000/api/chat/intelligent-query \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Check FedRAMP compliance for our production environment",
    "conversationId": "test-infra-003",
    "context": {
      "userId": "test-user@example.com",
      "environment": "production"
    }
  }' | jq '.'

echo ""
echo "======================================"
echo "Tests Complete!"
echo "======================================"
