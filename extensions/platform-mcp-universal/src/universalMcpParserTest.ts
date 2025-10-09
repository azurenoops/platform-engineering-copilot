import { UniversalMcpParser } from './universalMcpParser';

/**
 * Test the Universal MCP Parser with different response formats
 */
export function testUniversalMcpParser() {
    console.log('🧪 Testing Universal MCP Parser...');

    // Test 1: JSON quoted string format (current issue)
    const quotedJsonResponse = {
        markdown: '{"success":true,"message":"Service template created successfully","resultType":"service_template","data":{"serviceName":"test-service","serviceType":"microservice","framework":"dotnet","templateId":"12345","action":"create"},"files":[{"name":"deployment.yaml","description":"Kubernetes deployment manifest","type":"yaml"},{"name":"values.yaml","description":"Helm chart values","type":"yaml"}],"suggestions":["Review templates","Commit to Git"],"nextSteps":["Deploy to dev","Set up monitoring"]}'
    };

    console.log('Testing quoted JSON format...');
    const result1 = UniversalMcpParser.parseResponse(quotedJsonResponse);
    console.log('✅ Result 1:', JSON.stringify(result1, null, 2));

    // Test 2: JSON code block format
    const codeBlockResponse = {
        markdown: '```json\n{"success":true,"message":"Infrastructure provisioned","resultType":"infrastructure","data":{"resourceName":"test-aks","resourceType":"kubernetes"}}\n```'
    };

    console.log('Testing JSON code block format...');
    const result2 = UniversalMcpParser.parseResponse(codeBlockResponse);
    console.log('✅ Result 2:', JSON.stringify(result2, null, 2));

    // Test 3: Direct object format
    const directObjectResponse = {
        success: true,
        message: "Cost analysis completed",
        resultType: "cost_analysis",
        data: {
            totalCost: 150.75,
            currency: "USD",
            period: "last-30-days"
        },
        files: [],
        suggestions: ["Set up alerts"],
        nextSteps: ["Review costs"]
    };

    console.log('Testing direct object format...');
    const result3 = UniversalMcpParser.parseResponse(directObjectResponse);
    console.log('✅ Result 3:', JSON.stringify(result3, null, 2));

    // Test 4: Legacy format
    const legacyResponse = {
        serviceName: "legacy-service",
        files: [
            { name: "legacy.yaml", description: "Legacy file", type: "yaml" }
        ]
    };

    console.log('Testing legacy format...');
    const result4 = UniversalMcpParser.parseResponse(legacyResponse);
    console.log('✅ Result 4:', JSON.stringify(result4, null, 2));

    // Test type checking functions
    console.log('\n🔍 Testing type checking functions...');
    
    if (result1 && UniversalMcpParser.isServiceTemplateResult(result1)) {
        const serviceData = UniversalMcpParser.getServiceTemplateData(result1);
        console.log('✅ Service template data:', serviceData);
    }

    if (result3 && UniversalMcpParser.isCostAnalysisResult(result3)) {
        const costData = UniversalMcpParser.getCostAnalysisData(result3);
        console.log('✅ Cost analysis data:', costData);
    }

    console.log('🎯 Universal MCP Parser tests completed!');
}

// Test function is exported above